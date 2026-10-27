using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Turns System.Text.Json deserialization failures into <see cref="ValidationError"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Every failure uses the one code <see cref="SystemTextJsonExtensions.InvalidJsonCode"/> and the category
/// <see cref="ZodIssueCategories.InvalidType"/>. The location is kept: the serializer's JSONPath becomes the
/// error's <see cref="ValidationError.FullPath"/> and the line and byte position become the <c>line</c> and
/// <c>position</c> parameters.
/// </para>
/// <para>
/// The message is fixed and generic. The serializer's own message names CLR types and can quote the input, so
/// it is never passed to a client; the expected type is described with Zod's type names instead.
/// </para>
/// </remarks>
static class JsonDeserializationErrors
{
	/// <summary>The message format of every deserialization failure.</summary>
	public const string MessageFormat = "The JSON value could not be converted to {expected}.";

	/// <summary>The parameter holding the (zero-based) line number reported by the serializer.</summary>
	public const string LineParameter = "line";

	/// <summary>The parameter holding the (zero-based) byte position in the line reported by the serializer.</summary>
	public const string PositionParameter = "position";

	/// <summary>
	/// Maps an exception thrown while deserializing to validation errors.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <param name="typeInfo">The root contract, used to describe the expected type at the failing path.</param>
	/// <param name="errors">The errors, when the exception is a deserialization failure.</param>
	/// <returns><see langword="true"/> when the exception is a deserialization failure.</returns>
	public static bool TryMap(Exception exception, JsonTypeInfo? typeInfo, out ImmutableArray<ValidationError> errors)
	{
		switch (exception)
		{
			// A validating converter failed: its errors are the result, located under the converter's path.
			case ZodJsonValidationException validation:
				errors = validation.Errors;
				return true;

			// A validating converter could not read its value: the location joins the converted value's path
			// with the position inside it.
			case ZodJsonReadException read:
				errors = [Create(read.FullPath, typeInfo, read.LineNumber, read.BytePositionInLine)];
				return true;

			case JsonException json:
				errors = [FromJsonException(json, typeInfo)];
				return true;

			// System.Text.Json throws these for types or metadata it cannot handle. The client cannot fix them,
			// but they are still a failure to read the body rather than an unhandled server error.
			case NotSupportedException:
			case InvalidOperationException and not ObjectDisposedException:
				errors = [Create(ZodPath.Root, typeInfo, line: null, position: null)];
				return true;

			default:
				errors = default;
				return false;
		}
	}

	/// <summary>
	/// The error reported when the JSON deserialized to <see langword="null"/>.
	/// </summary>
	/// <param name="typeInfo">The root contract.</param>
	/// <returns>The error.</returns>
	public static ValidationError NullValue(JsonTypeInfo? typeInfo) =>
		Create(ZodPath.Root, typeInfo, line: null, position: null, received: "null");

	static ValidationError FromJsonException(JsonException exception, JsonTypeInfo? typeInfo) =>
		Create(ZodPath.ParseJsonPath(exception.Path), typeInfo, exception.LineNumber, exception.BytePositionInLine);

	static ValidationError Create(
		ZodPath path,
		JsonTypeInfo? typeInfo,
		long? line,
		long? position,
		string? received = null
	)
	{
		var expected = typeInfo is null ? JsonKinds.Unknown : DescribeExpected(typeInfo, path);

		Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
		{
			[ZodIssueParameters.Expected] = expected,
		};
		if (received is not null)
			parameters[ZodIssueParameters.Received] = received;
		if (line is { } lineNumber)
			parameters[LineParameter] = lineNumber;
		if (position is { } bytePosition)
			parameters[PositionParameter] = bytePosition;

		return ValidationError.Create(
			SystemTextJsonExtensions.InvalidJsonCode,
			ZodMessageFormat.Format(MessageFormat, parameters, CultureInfo.InvariantCulture),
			path,
			parameters,
			category: ZodIssueCategories.InvalidType
		) with
		{
			MessageFormat = MessageFormat,
		};
	}

	/// <summary>
	/// Walks <paramref name="path"/> through the contract to name the JSON expected where reading failed.
	/// </summary>
	static string DescribeExpected(JsonTypeInfo root, ZodPath path)
	{
		try
		{
			var current = root;
			foreach (var segment in path.Segments)
			{
				var next = Step(current, segment);
				if (next is null)
					return JsonKinds.Unknown;

				current = next;
			}

			return JsonKinds.OfContract(current) ?? JsonKinds.Unknown;
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
		{
			// The resolver does not know a member type (for example a source-generated context that omits
			// it). Describing the expected type is best effort; the error itself is still reported.
			return JsonKinds.Unknown;
		}
	}

	static JsonTypeInfo? Step(JsonTypeInfo current, ZodPathSegment segment)
	{
		if (current.Kind == JsonTypeInfoKind.Object)
		{
			if (segment.Kind != ZodPathSegmentKind.Member)
				return null;

			var comparison = current.Options.PropertyNameCaseInsensitive
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;
			foreach (var property in current.Properties)
			{
				if (string.Equals(property.Name, segment.Name, comparison))
					return current.Options.GetTypeInfo(property.PropertyType);
			}

			return null;
		}

		if (current.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
		{
			var expectedSegment =
				current.Kind == JsonTypeInfoKind.Enumerable ? ZodPathSegmentKind.Index : ZodPathSegmentKind.Key;
			if (segment.Kind != expectedSegment && segment.Kind != ZodPathSegmentKind.Member)
				return null;

			return ElementTypeOf(current) is { } element ? current.Options.GetTypeInfo(element) : null;
		}

		// A scalar has no members.
		return null;
	}

	/// <summary>
	/// The element (or dictionary value) type of a collection contract, as the contract exposes it; for a contract
	/// that does not set it, it is read from an array's element type or the collection's generic arguments (the last
	/// one, so a dictionary yields its value type). Neither needs unreferenced metadata.
	/// </summary>
	static Type? ElementTypeOf(JsonTypeInfo collection)
	{
		if (collection.ElementType is { } element)
			return element;

		var type = collection.Type;
		if (type.IsArray)
			return type.GetElementType();

		if (!type.IsGenericType)
			return null;

		var arguments = type.GetGenericArguments();
		return collection.Kind == JsonTypeInfoKind.Dictionary
			? arguments.Length == 2
				? arguments[1]
				: null
			: arguments.Length == 1
				? arguments[0]
				: null;
	}
}
