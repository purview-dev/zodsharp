using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ZodSharp.Core;

/// <summary>
/// Recovers the ZodSharp errors of a value that failed validation while System.Text.Json was reading it, located
/// under the member of the enclosing document that held it.
/// </summary>
/// <remarks>
/// <para>
/// A converter cannot know where in the document it is reading, so a <see cref="ZodException"/> thrown while
/// creating a value object carries only the value object's own path (<c>Value</c>). A converter that wraps it in a
/// <see cref="JsonException"/> (<c>new JsonException(message, zodException)</c>) lets the serializer record that
/// location in <see cref="JsonException.Path"/> (<c>$.customerEmail</c>); these helpers join the two, so the error
/// reads <c>CustomerEmail.Value</c>, exactly as validating the deserialized model would report it.
/// </para>
/// <para>
/// The serializer's path uses JSON names. Given the root contract, every member segment is mapped back to its member
/// name through the contract's metadata (no reflection beyond what the contract already holds), collection indexes
/// are kept and dictionary entries become key segments. Without a root contract, or past a point the contract cannot
/// describe, the JSON names are kept.
/// </para>
/// <para>
/// An error located at the value object's own member (one member segment, displayed under that member's name) is
/// named after the member that holds the value object, as nested validation names a scalar value object's errors:
/// <c>"Field 'Value' ..."</c> reads <c>"Field 'CustomerEmail' ..."</c>.
/// </para>
/// </remarks>
public static class ZodJsonExceptions
{
	const int MaxDepth = 16;

	/// <summary>
	/// Finds a <see cref="JsonException"/> in <paramref name="exception"/> or its inner exceptions whose own inner
	/// exceptions contain a <see cref="ZodException"/>, and returns that exception's errors located under the
	/// <see cref="JsonException.Path"/>.
	/// </summary>
	/// <param name="exception">The exception, for example a <c>BadHttpRequestException</c> from body binding.</param>
	/// <param name="rootTypeInfo">
	/// The contract of the deserialized root type, used to map JSON names back to member names; when
	/// <see langword="null"/>, the JSON names are kept.
	/// </param>
	/// <param name="errors">The re-rooted errors.</param>
	/// <returns><see langword="true"/> when such a chain was found.</returns>
	public static bool TryGetErrors(
		Exception exception,
		JsonTypeInfo? rootTypeInfo,
		out ImmutableArray<ValidationError> errors
	)
	{
		ArgumentNullException.ThrowIfNull(exception);

		if (!TryFind(exception, out var jsonPath, out var zodException))
		{
			errors = default;
			return false;
		}

		errors = Reroot(zodException.Errors, ZodPath.ParseJsonPath(jsonPath), rootTypeInfo);
		return true;
	}

	/// <summary>
	/// Like <see cref="TryGetErrors"/>, but returns a <see cref="ZodException"/> carrying the re-rooted errors, for
	/// code that handles a bare <see cref="ZodException"/> already.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <param name="rootTypeInfo">The contract of the deserialized root type, or <see langword="null"/>.</param>
	/// <param name="zodException">The exception carrying the re-rooted errors.</param>
	/// <returns><see langword="true"/> when such a chain was found.</returns>
	public static bool TryReroot(
		Exception exception,
		JsonTypeInfo? rootTypeInfo,
		[NotNullWhen(true)] out ZodException? zodException
	)
	{
		zodException = TryGetErrors(exception, rootTypeInfo, out var errors) ? new ZodException(errors) : null;
		return zodException is not null;
	}

	/// <summary>
	/// Locates <paramref name="errors"/>, whose paths are relative to a value read at <paramref name="jsonPath"/>,
	/// under that location. Every other field of each error (code, category, origin, bounds, parameters, union
	/// errors) is kept.
	/// </summary>
	/// <param name="errors">The errors, relative to the value.</param>
	/// <param name="jsonPath">Where the value was read, in JSON names (see <see cref="ZodPath.ParseJsonPath"/>).</param>
	/// <param name="rootTypeInfo">The contract of the deserialized root type, or <see langword="null"/>.</param>
	/// <returns>The re-rooted errors.</returns>
	public static ImmutableArray<ValidationError> Reroot(
		ImmutableArray<ValidationError> errors,
		ZodPath jsonPath,
		JsonTypeInfo? rootTypeInfo
	)
	{
		if (errors.IsDefaultOrEmpty)
			return [];

		var prefix = ToMemberPath(jsonPath, rootTypeInfo);
		if (prefix.IsRoot)
			return errors;

		var owner = LastMemberName(prefix);
		var builder = ImmutableArray.CreateBuilder<ValidationError>(errors.Length);
		foreach (var error in errors)
		{
			var named =
				owner is not null && IsAtOwnMember(error) ? ZodGeneratedValidation.NameAfterOwner(error, owner) : error;
			builder.Add(named.WithPathPrefix(prefix));
		}

		return builder.MoveToImmutable();
	}

	/// <summary>
	/// Maps the member segments of a JSON-named path to member names through <paramref name="rootTypeInfo"/>.
	/// Collection indexes are kept, and the entries of a dictionary become key segments. A segment the contract
	/// cannot describe, and every segment after it, is kept as it is.
	/// </summary>
	/// <param name="jsonPath">The JSON-named path.</param>
	/// <param name="rootTypeInfo">The contract of the root type, or <see langword="null"/> to keep the JSON names.</param>
	/// <returns>The member-named path.</returns>
	public static ZodPath ToMemberPath(ZodPath jsonPath, JsonTypeInfo? rootTypeInfo)
	{
		if (rootTypeInfo is null || jsonPath.IsRoot)
			return jsonPath;

		var builder = ImmutableArray.CreateBuilder<ZodPathSegment>(jsonPath.Length);
		var current = rootTypeInfo;
		foreach (var segment in jsonPath.Segments)
			builder.Add(current is null ? segment : Step(ref current, segment));

		return ZodPath.From(builder.MoveToImmutable());
	}

	static bool TryFind(Exception? exception, out string? jsonPath, [NotNullWhen(true)] out ZodException? zodException)
	{
		// The JsonException nearest the ZodException that the serializer gave a path to: an outer wrapper (such as a
		// BadHttpRequestException) adds no location, and a converter's own JsonException is the one the serializer fills.
		JsonException? carrier = null;
		var current = exception;
		for (var depth = 0; current is not null && depth < MaxDepth; depth++, current = current.InnerException)
		{
			if (current is ZodException found)
			{
				if (carrier is null)
					break;

				jsonPath = carrier.Path;
				zodException = found;
				return true;
			}

			if (current is JsonException json && (carrier?.Path is null || json.Path is not null))
				carrier = json;
		}

		jsonPath = null;
		zodException = null;
		return false;
	}

	static ZodPathSegment Step(ref JsonTypeInfo? current, ZodPathSegment segment)
	{
		var info = current!;
		current = null;

		if (info.Kind == JsonTypeInfoKind.Object && segment.Kind == ZodPathSegmentKind.Member)
		{
			var comparison = info.Options.PropertyNameCaseInsensitive
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;
			foreach (var property in info.Properties)
			{
				if (!string.Equals(property.Name, segment.Name, comparison))
					continue;

				current = Resolve(info.Options, property.PropertyType);
				// The contract's attribute provider is the property or field it binds (source-generated contracts
				// record it too), so its name is the member name; a custom contract may not set it.
				return ZodPathSegment.Member(
					property.AttributeProvider is MemberInfo member ? member.Name : property.Name
				);
			}

			return segment;
		}

		if (info.Kind == JsonTypeInfoKind.Enumerable && segment.Kind == ZodPathSegmentKind.Index)
		{
			current = Resolve(info.Options, info.ElementType);
			return segment;
		}

		if (
			info.Kind == JsonTypeInfoKind.Dictionary
			&& segment.Kind is ZodPathSegmentKind.Member or ZodPathSegmentKind.Key
		)
		{
			current = Resolve(info.Options, info.ElementType);
			return ZodPathSegment.Key(segment.Name ?? string.Empty);
		}

		// A scalar (or a value with its own converter) has no members the contract can describe.
		return segment;
	}

	static JsonTypeInfo? Resolve(JsonSerializerOptions options, Type? type)
	{
		if (type is null)
			return null;

		try
		{
			return options.TryGetTypeInfo(type, out var typeInfo) ? typeInfo : null;
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
		{
			// The resolver cannot describe the member type; the rest of the path keeps its JSON names.
			return null;
		}
	}

	static string? LastMemberName(ZodPath path)
	{
		for (var i = path.Length - 1; i >= 0; i--)
		{
			if (path[i].Kind == ZodPathSegmentKind.Member)
				return path[i].Name;
		}

		return null;
	}

	/// <summary>
	/// Whether <paramref name="error"/> is located at the value object itself or at its own single member and is
	/// displayed under that member's name - the shape of a scalar value object's error (<c>Value</c>, <c>"Field 'Value'
	/// ..."</c>). A deeper error, or one with its own display name, keeps it.
	/// </summary>
	static bool IsAtOwnMember(in ValidationError error)
	{
		var path = error.FullPath;
		if (path.IsRoot)
			return true;

		return path.Length == 1
			&& path[0].Kind == ZodPathSegmentKind.Member
			&& error.Parameters is { } parameters
			&& parameters.TryGetValue(ZodIssueParameters.DisplayName, out var displayName)
			&& displayName is string name
			&& string.Equals(name, path[0].Name, StringComparison.Ordinal);
	}
}
