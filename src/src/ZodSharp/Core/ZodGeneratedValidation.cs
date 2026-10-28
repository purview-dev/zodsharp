using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DataAnnotationsResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ZodSharp.Core;

/// <summary>
/// Identifies a member validated by generated code: the declaring type, the C# member name, the display
/// name used in messages and the canonical path. Generated schemas create one per validated member.
/// </summary>
/// <remarks>
/// The display name is resolved on every read when it comes from a resource
/// (<c>[Display(Name = ..., ResourceType = ...)]</c>), so a message follows the current UI culture.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ZodGeneratedMember
{
	readonly Func<string?>? _displayNameResolver;

	/// <summary>
	/// Initializes a new instance.
	/// </summary>
	/// <param name="declaringType">The full name of the declaring type.</param>
	/// <param name="member">The C# member name; empty for the type itself.</param>
	/// <param name="displayName">The display name, or the fallback when <paramref name="displayNameResolver"/> returns nothing.</param>
	/// <param name="path">The canonical path of the member.</param>
	/// <param name="displayNameResolver">Resolves a localized display name at runtime, when one is declared.</param>
	public ZodGeneratedMember(
		string declaringType,
		string member,
		string displayName,
		ZodPath path,
		Func<string?>? displayNameResolver = null
	)
	{
		DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
		Member = member ?? throw new ArgumentNullException(nameof(member));
		DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
		Path = path;
		_displayNameResolver = displayNameResolver;
	}

	/// <summary>Gets the full name of the declaring type.</summary>
	public string DeclaringType { get; }

	/// <summary>Gets the C# member name; empty for the type itself.</summary>
	public string Member { get; }

	/// <summary>Gets the canonical path of the member.</summary>
	public ZodPath Path { get; }

	/// <summary>Gets the display name used in messages.</summary>
	public string DisplayName
	{
		get
		{
			if (_displayNameResolver is null)
				return field;

			var resolved = _displayNameResolver();
			return string.IsNullOrEmpty(resolved) ? field : resolved;
		}
	}
}

/// <summary>
/// Helpers called by source-generated validators. Not intended to be called directly.
/// </summary>
/// <remarks>
/// Every helper runs only on the failure path (or when walking a path after a failure), so the success path
/// of a generated validator stays allocation-free.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[SuppressMessage(
	"Design",
	"CA1002:Do not expose generic lists",
	Justification = "Generated validators collect errors in a lazily created List<ValidationError>; these helpers operate on it directly to stay allocation-free on success."
)]
[SuppressMessage(
	"Design",
	"CA1045:Do not pass types by reference",
	Justification = "Generated validators pass the lazily created error list by reference so the success path allocates nothing."
)]
public static class ZodGeneratedValidation
{
	/// <summary>
	/// The code reported for a failed DataAnnotations input (<c>IValidatableObject</c>,
	/// <c>AsyncValidationAttribute</c>) that carries no code of its own.
	/// </summary>
	public const string DataAnnotationsErrorCode = "validation_failed";

	/// <summary>
	/// Creates an error whose message is produced at runtime from <paramref name="messageFormat"/>, so the
	/// error keeps the template (<see cref="ValidationError.MessageFormat"/>) and the values that fill it
	/// (<see cref="ValidationError.Parameters"/>).
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="messageFormat">
	/// The message template. Named placeholders resolve from the parameters and the error's built-in fields;
	/// positional placeholders are first renamed through <paramref name="ordinalNames"/>.
	/// </param>
	/// <param name="path">The error path.</param>
	/// <param name="member">The member that failed.</param>
	/// <param name="origin">The structured origin.</param>
	/// <param name="category">The Zod issue category.</param>
	/// <param name="lower">The lower bound of a size or range issue.</param>
	/// <param name="upper">The upper bound of a size or range issue.</param>
	/// <param name="ordinalNames">
	/// The names positional placeholders bind to, in DataAnnotations argument order (<c>{0}</c> is always the
	/// display name).
	/// </param>
	/// <param name="values">Additional named values (rule arguments).</param>
	/// <returns>The error.</returns>
	public static ValidationError Error(
		string code,
		string messageFormat,
		ZodPath path,
		ZodGeneratedMember member,
		string? origin = null,
		string? category = null,
		ErrorBound? lower = null,
		ErrorBound? upper = null,
		string[]? ordinalNames = null,
		KeyValuePair<string, object?>[]? values = null
	)
	{
		ArgumentNullException.ThrowIfNull(member);

		var named = ToNamedFormat(messageFormat ?? string.Empty, ordinalNames);
		var parameters = CreateParameters(member, null, values);
		var minimum = lower is { Value: int lowerValue } ? lowerValue : (int?)null;
		var maximum = upper is { Value: int upperValue } ? upperValue : (int?)null;
		var inclusive = lower?.Inclusive ?? upper?.Inclusive;

		var error = ValidationError.Create(
			code,
			string.Empty,
			path,
			parameters,
			origin,
			minimum,
			maximum,
			inclusive,
			category
		) with
		{
			Lower = lower,
			Upper = upper,
			MessageFormat = named,
		};

		return error with
		{
			Message = ZodMessageFormat.Format(named, in error, System.Globalization.CultureInfo.InvariantCulture),
		};
	}

	/// <summary>
	/// Adds the member's parameters (<c>displayName</c>, <c>member</c>, <c>declaringType</c>) to an error built
	/// elsewhere (a shared <see cref="ZodIssues"/> factory or <see cref="ZodRuleErrors"/>), and optionally
	/// replaces its message with a template such as an attribute's <c>ErrorMessage</c>.
	/// </summary>
	/// <param name="error">The error.</param>
	/// <param name="member">The member that failed.</param>
	/// <param name="fallbackCategory">The category used when the error has none.</param>
	/// <param name="messageFormat">A message template that replaces the error's message.</param>
	/// <param name="ordinalNames">The names positional placeholders in <paramref name="messageFormat"/> bind to.</param>
	/// <returns>The error.</returns>
	public static ValidationError ForMember(
		in ValidationError error,
		ZodGeneratedMember member,
		string? fallbackCategory = null,
		string? messageFormat = null,
		string[]? ordinalNames = null
	)
	{
		ArgumentNullException.ThrowIfNull(member);

		var result = error.WithParameters(CreateParameters(member, error.Parameters, null)) with
		{
			Category = error.Category ?? fallbackCategory,
		};

		if (messageFormat is null)
			return result;

		var named = ToNamedFormat(messageFormat, ordinalNames);
		result = result with { MessageFormat = named };
		return result with
		{
			Message = ZodMessageFormat.Format(named, in result, System.Globalization.CultureInfo.InvariantCulture),
		};
	}

	/// <summary>
	/// Renames positional placeholders (<c>{0}</c>, <c>{1,5:N2}</c>) to the named placeholders in
	/// <paramref name="ordinalNames"/>, so a DataAnnotations-style message becomes a named template a host can
	/// re-format. Escaped braces, named placeholders and positions without a name are left as they are. Never
	/// throws.
	/// </summary>
	/// <param name="format">The message format.</param>
	/// <param name="ordinalNames">The name for each position.</param>
	/// <returns>The named template.</returns>
	public static string ToNamedFormat(string format, string[]? ordinalNames)
	{
		if (string.IsNullOrEmpty(format) || ordinalNames is null || ordinalNames.Length == 0)
			return format ?? string.Empty;

		if (format.IndexOf('{', StringComparison.Ordinal) < 0)
			return format;

		StringBuilder builder = new(format.Length + 16);
		var i = 0;
		while (i < format.Length)
		{
			var c = format[i];
			if (c != '{')
			{
				builder.Append(c);
				i++;
				continue;
			}

			if (i + 1 < format.Length && format[i + 1] == '{')
			{
				builder.Append("{{");
				i += 2;
				continue;
			}

			var start = i + 1;
			var end = start;
			while (end < format.Length && char.IsAsciiDigit(format[end]))
				end++;

			if (
				end > start
				&& end < format.Length
				&& format[end] is '}' or ',' or ':'
				&& int.TryParse(
					format.AsSpan(start, end - start),
					System.Globalization.NumberStyles.None,
					System.Globalization.CultureInfo.InvariantCulture,
					out var position
				)
				&& position < ordinalNames.Length
			)
			{
				builder.Append('{').Append(ordinalNames[position]);
				i = end;
				continue;
			}

			builder.Append(c);
			i++;
		}

		return builder.ToString();
	}

	/// <summary>
	/// Adds <paramref name="error"/> to <paramref name="errors"/>, creating the list on first use.
	/// </summary>
	/// <param name="errors">The error list.</param>
	/// <param name="error">The error.</param>
	public static void Add(ref List<ValidationError>? errors, in ValidationError error) => (errors ??= []).Add(error);

	/// <summary>
	/// Adds the errors of a nested result under <paramref name="prefix"/>.
	/// </summary>
	/// <typeparam name="T">The nested type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="nested">The nested result.</param>
	/// <param name="prefix">The path the nested errors are placed under.</param>
	public static void AddNested<T>(ref List<ValidationError>? errors, in ValidationResult<T> nested, ZodPath prefix)
	{
		if (nested.IsSuccess || nested.Errors.IsDefaultOrEmpty)
			return;

		errors ??= [];
		foreach (var error in nested.Errors)
			errors.Add(error.WithPathPrefix(prefix));
	}

	/// <summary>
	/// Adds the errors of a nested scalar value object's result under <paramref name="prefix"/>, naming them after
	/// <paramref name="owner"/>: the <c>displayName</c> parameter becomes the owner's display name, and a message
	/// produced from the error's <see cref="ValidationError.MessageFormat"/> is re-formatted with it. A scalar's own
	/// member (<c>Value</c>) is an implementation detail, so <c>"Field 'Value' is required"</c> reads
	/// <c>"Field 'Email' is required"</c>. A message the template does not reproduce (a rule's own text, an attribute
	/// message without a placeholder) is kept as it is.
	/// </summary>
	/// <typeparam name="T">The nested type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="nested">The nested result.</param>
	/// <param name="prefix">The path the nested errors are placed under.</param>
	/// <param name="owner">The member that holds the scalar.</param>
	public static void AddNested<T>(
		ref List<ValidationError>? errors,
		in ValidationResult<T> nested,
		ZodPath prefix,
		ZodGeneratedMember owner
	)
	{
		ArgumentNullException.ThrowIfNull(owner);

		if (nested.IsSuccess || nested.Errors.IsDefaultOrEmpty)
			return;

		errors ??= [];
		var displayName = owner.DisplayName;
		foreach (var error in nested.Errors)
			errors.Add(NameAfterOwner(error, displayName).WithPathPrefix(prefix));
	}

	/// <summary>
	/// Adds the errors of a nested scalar value object's result under <paramref name="prefix"/>, naming them after
	/// <paramref name="owner"/>, and returns the list; see
	/// <see cref="AddNested{T}(ref List{ValidationError}?, in ValidationResult{T}, ZodPath, ZodGeneratedMember)"/>.
	/// </summary>
	/// <typeparam name="T">The nested type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="nested">The nested result.</param>
	/// <param name="prefix">The path the nested errors are placed under.</param>
	/// <param name="owner">The member that holds the scalar.</param>
	/// <returns>The error list.</returns>
	public static List<ValidationError>? WithNested<T>(
		List<ValidationError>? errors,
		ValidationResult<T> nested,
		ZodPath prefix,
		ZodGeneratedMember owner
	)
	{
		AddNested(ref errors, nested, prefix, owner);
		return errors;
	}

	/// <summary>
	/// Completes a nested <c>TryResolveMember</c> step so the resolved container is never a value type. A struct
	/// member's errors resolve to the struct, and a boxed copy of it cannot own a field (Blazor's
	/// <c>FieldIdentifier</c> rejects a value-type model), so they are attributed to the nearest reference-type
	/// owner: <paramref name="owner"/> and its member <paramref name="ownerMember"/>.
	/// </summary>
	/// <param name="resolved">What the nested step returned.</param>
	/// <param name="owner">The object that holds the nested value.</param>
	/// <param name="ownerMember">The member of <paramref name="owner"/> that holds the nested value.</param>
	/// <param name="container">The container the nested step resolved.</param>
	/// <param name="memberName">The member name the nested step resolved.</param>
	/// <returns><paramref name="resolved"/>.</returns>
	public static bool ResolveToReferenceOwner(
		bool resolved,
		object owner,
		string ownerMember,
		ref object? container,
		ref string memberName
	)
	{
		if (container is not null && container.GetType().IsValueType)
		{
			container = owner;
			memberName = ownerMember;
		}

		return resolved;
	}

	static ValidationError NameAfterOwner(in ValidationError error, string displayName)
	{
		var parameters = error.Parameters;
		if (
			parameters is null
			|| !parameters.TryGetValue(ZodIssueParameters.DisplayName, out var current)
			|| current is not string currentName
			|| string.Equals(currentName, displayName, StringComparison.Ordinal)
		)
			return error;

		Dictionary<string, object?> renamed = new(parameters.Count, StringComparer.Ordinal);
		foreach (var pair in parameters)
			renamed[pair.Key] = pair.Value;
		renamed[ZodIssueParameters.DisplayName] = displayName;

		var result = error.WithParameters(renamed);
		if (
			error.MessageFormat is { Length: > 0 } format
			&& string.Equals(
				ZodMessageFormat.Format(format, in error, System.Globalization.CultureInfo.InvariantCulture),
				error.Message,
				StringComparison.Ordinal
			)
		)
		{
			result = result with
			{
				Message = ZodMessageFormat.Format(format, in result, System.Globalization.CultureInfo.InvariantCulture),
			};
		}

		return result;
	}

	/// <summary>
	/// Adds the errors of a nested result under <paramref name="prefix"/>, returning the list. Used from async
	/// methods, which cannot take <see langword="ref"/> parameters.
	/// </summary>
	/// <typeparam name="T">The nested type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="nested">The nested result.</param>
	/// <param name="prefix">The path the nested errors are placed under.</param>
	/// <returns>The error list.</returns>
	public static List<ValidationError>? WithNested<T>(
		List<ValidationError>? errors,
		ValidationResult<T> nested,
		ZodPath prefix
	)
	{
		AddNested(ref errors, nested, prefix);
		return errors;
	}

	/// <summary>
	/// Adds a DataAnnotations result. An object-level result (<c>IValidatableObject</c>, reported at the root)
	/// is placed at each of its <c>MemberNames</c>, parsed as dotted paths; a member-level result is placed at
	/// <paramref name="path"/>.
	/// </summary>
	/// <param name="errors">The error list.</param>
	/// <param name="result">The DataAnnotations result; <see langword="null"/> or success adds nothing.</param>
	/// <param name="path">The path of the member that produced the result, or the root for object-level results.</param>
	/// <param name="member">The member that failed.</param>
	public static void AddDataAnnotationsResult(
		ref List<ValidationError>? errors,
		DataAnnotationsResult? result,
		ZodPath path,
		ZodGeneratedMember member
	)
	{
		if (result is null || ReferenceEquals(result, DataAnnotationsResult.Success))
			return;

		ArgumentNullException.ThrowIfNull(member);

		var message = string.IsNullOrEmpty(result.ErrorMessage) ? "Validation failed" : result.ErrorMessage;
		if (path.IsRoot)
		{
			var added = false;
			foreach (var memberName in result.MemberNames)
			{
				if (string.IsNullOrEmpty(memberName))
					continue;

				Add(ref errors, CreateDataAnnotationsError(message, ZodPath.ParseDotted(memberName), member));
				added = true;
			}

			if (added)
				return;
		}

		Add(ref errors, CreateDataAnnotationsError(message, path, member));
	}

	/// <summary>
	/// Adds a DataAnnotations result, returning the list; see
	/// <see cref="AddDataAnnotationsResult(ref List{ValidationError}?, DataAnnotationsResult?, ZodPath, ZodGeneratedMember)"/>.
	/// </summary>
	/// <param name="errors">The error list.</param>
	/// <param name="result">The DataAnnotations result.</param>
	/// <param name="path">The member path, or the root.</param>
	/// <param name="member">The member that failed.</param>
	/// <returns>The error list.</returns>
	public static List<ValidationError>? WithDataAnnotationsResult(
		List<ValidationError>? errors,
		DataAnnotationsResult? result,
		ZodPath path,
		ZodGeneratedMember member
	)
	{
		AddDataAnnotationsResult(ref errors, result, path, member);
		return errors;
	}

	/// <summary>
	/// Creates the DataAnnotations <c>ValidationContext</c> handed to <c>IValidatableObject</c>,
	/// <c>IAsyncValidatableObject</c> and <c>AsyncValidationAttribute</c> inputs. The display name is set
	/// explicitly, so the context never reflects over the instance to find one (trim-safe).
	/// </summary>
	/// <param name="instance">The object being validated.</param>
	/// <param name="displayName">The display name of the object or member.</param>
	/// <param name="services">The caller's services, if any.</param>
	/// <param name="memberName">The member being validated, or <see langword="null"/> for the object itself.</param>
	/// <returns>The context.</returns>
	public static System.ComponentModel.DataAnnotations.ValidationContext CreateValidationContext(
		object instance,
		string displayName,
		IServiceProvider? services,
		string? memberName = null
	)
	{
		System.ComponentModel.DataAnnotations.ValidationContext context = new(instance, displayName, services, null);
		if (memberName is not null)
			context.MemberName = memberName;

		return context;
	}

	/// <summary>
	/// Merges a custom validation result into the error list.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="result">The custom result.</param>
	/// <returns>The error list.</returns>
	public static List<ValidationError>? WithResult<T>(List<ValidationError>? errors, ValidationResult<T> result)
	{
		if (result.IsSuccess || result.Errors.IsDefaultOrEmpty)
			return errors;

		errors ??= [];
		errors.AddRange(result.Errors);
		return errors;
	}

	/// <summary>
	/// Keeps only the errors whose path starts with <paramref name="member"/>.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="errors">The error list.</param>
	/// <param name="result">The result to filter.</param>
	/// <param name="member">The member name.</param>
	/// <returns>The error list.</returns>
	public static List<ValidationError>? WithMemberErrors<T>(
		List<ValidationError>? errors,
		ValidationResult<T> result,
		string member
	)
	{
		if (result.IsSuccess || result.Errors.IsDefaultOrEmpty)
			return errors;

		foreach (var error in result.Errors)
		{
			var path = error.FullPath;
			if (
				!path.IsRoot
				&& path[0].Kind == ZodPathSegmentKind.Member
				&& string.Equals(path[0].Name, member, StringComparison.Ordinal)
			)
			{
				(errors ??= []).Add(error);
			}
		}

		return errors;
	}

	/// <summary>
	/// Builds the final result from the collected errors.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="value">The validated value.</param>
	/// <param name="errors">The collected errors.</param>
	/// <returns>The result.</returns>
	public static ValidationResult<T> ToResult<T>(T value, List<ValidationError>? errors) =>
		errors is null ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(errors);

	/// <summary>
	/// Projects a pending result onto its errors (empty when valid), completing synchronously when the
	/// validation did.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="pending">The pending result.</param>
	/// <returns>The errors.</returns>
	public static ValueTask<ImmutableArray<ValidationError>> ToErrorsAsync<T>(ValueTask<ValidationResult<T>> pending)
	{
		return pending.IsCompletedSuccessfully ? new(ToErrors(pending.Result)) : AwaitErrors(pending);

		static async ValueTask<ImmutableArray<ValidationError>> AwaitErrors(ValueTask<ValidationResult<T>> pending) =>
			ToErrors(await pending.ConfigureAwait(false));
	}

	static ImmutableArray<ValidationError> ToErrors<T>(in ValidationResult<T> result) =>
		result.IsSuccess || result.Errors.IsDefault ? [] : result.Errors;

	/// <summary>
	/// Resolves the element of <paramref name="collection"/> addressed by an index segment, without
	/// reflection. Lists and arrays are indexed directly; other sequences are enumerated (failure path only).
	/// </summary>
	/// <typeparam name="TElement">The element type.</typeparam>
	/// <param name="collection">The collection.</param>
	/// <param name="segment">The index segment.</param>
	/// <param name="element">The element.</param>
	/// <returns><see langword="true"/> when the element exists.</returns>
	public static bool TryGetElement<TElement>(
		IEnumerable<TElement>? collection,
		ZodPathSegment segment,
		[MaybeNullWhen(false)] out TElement element
	)
	{
		element = default;
		if (collection is null || segment.Kind != ZodPathSegmentKind.Index)
			return false;

		var index = segment.Index;
		switch (collection)
		{
			case IList<TElement> list:
				if (index >= list.Count)
					return false;
				element = list[index];
				return true;
			case IReadOnlyList<TElement> readOnlyList:
				if (index >= readOnlyList.Count)
					return false;
				element = readOnlyList[index];
				return true;
			default:
				var position = 0;
				foreach (var item in collection)
				{
					if (position++ == index)
					{
						element = item;
						return true;
					}
				}

				return false;
		}
	}

	/// <summary>
	/// Resolves the value of the map entry addressed by a key segment, comparing the key's invariant
	/// rendering (the same rendering <see cref="ZodPathSegment.Key{TKey}(TKey)"/> records).
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <param name="map">The map.</param>
	/// <param name="segment">The key segment.</param>
	/// <param name="value">The entry value.</param>
	/// <returns><see langword="true"/> when the entry exists.</returns>
	public static bool TryGetEntryValue<TKey, TValue>(
		IEnumerable<KeyValuePair<TKey, TValue>>? map,
		ZodPathSegment segment,
		[MaybeNullWhen(false)] out TValue value
	)
	{
		value = default;
		if (map is null || segment.Kind != ZodPathSegmentKind.Key)
			return false;

		foreach (var entry in map)
		{
			if (string.Equals(ZodPathSegment.FormatKey(entry.Key), segment.Name, StringComparison.Ordinal))
			{
				value = entry.Value;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Renames the member segments of <paramref name="path"/> from <paramref name="offset"/> onwards with
	/// <paramref name="namingPolicy"/>, appending them to <paramref name="segments"/>. Used by generated
	/// JSON-name mapping once a member has no generated schema to delegate to.
	/// </summary>
	/// <param name="path">The path.</param>
	/// <param name="offset">The first segment to append.</param>
	/// <param name="namingPolicy">The naming policy, or <see langword="null"/> to keep names.</param>
	/// <param name="segments">The segments being built.</param>
	public static void AppendRemaining(
		ZodPath path,
		int offset,
		Func<string, string>? namingPolicy,
		ImmutableArray<ZodPathSegment>.Builder segments
	)
	{
		ArgumentNullException.ThrowIfNull(segments);

		for (var i = offset; i < path.Length; i++)
		{
			var segment = path[i];
			segments.Add(
				segment.Kind == ZodPathSegmentKind.Member && namingPolicy is not null
					? ZodPathSegment.Member(namingPolicy(segment.Name!))
					: segment
			);
		}
	}

	/// <summary>
	/// Renders an error as an options validation failure: <c>{path}: {message} ({code})</c>, or
	/// <c>{message} ({code})</c> for an error at the root.
	/// </summary>
	/// <param name="error">The error.</param>
	/// <returns>The failure text.</returns>
	public static string FormatOptionsFailure(in ValidationError error)
	{
		var path = error.FullPath.ToDottedString();
		return path.Length == 0 ? $"{error.Message} ({error.Code})" : $"{path}: {error.Message} ({error.Code})";
	}

	/// <summary>
	/// Throws because <paramref name="type"/> has rules that only run asynchronously.
	/// </summary>
	/// <param name="type">The validated type.</param>
	/// <exception cref="InvalidOperationException">Always.</exception>
	[DoesNotReturn]
	public static void ThrowAsyncValidationRequired(Type type) =>
		throw new InvalidOperationException(
			$"'{type?.FullName}' has validation rules that only run asynchronously (an AsyncValidationAttribute or IAsyncValidatableObject). Call ValidateAsync instead of Validate."
		);

	/// <summary>
	/// Throws because the service-aware validation of <paramref name="type"/> was called without services.
	/// </summary>
	/// <param name="type">The validated type.</param>
	/// <exception cref="InvalidOperationException">Always.</exception>
	[DoesNotReturn]
	public static void ThrowServicesRequired(Type type) =>
		throw new InvalidOperationException(
			$"'{type?.FullName}' has a service-aware validation method that needs an IServiceProvider. Call ValidateAsync with the caller's services."
		);

	static ValidationError CreateDataAnnotationsError(string message, ZodPath path, ZodGeneratedMember member)
	{
		var error = ValidationError.Create(
			DataAnnotationsErrorCode,
			message,
			path,
			CreateParameters(member, null, null),
			category: ZodIssueCategories.Custom
		);
		return error;
	}

	static Dictionary<string, object?> CreateParameters(
		ZodGeneratedMember member,
		ErrorTypeParameters? existing,
		KeyValuePair<string, object?>[]? values
	)
	{
		Dictionary<string, object?> parameters = new(
			3 + (existing?.Count ?? 0) + (values?.Length ?? 0),
			StringComparer.Ordinal
		);

		if (existing is not null)
		{
			foreach (var pair in existing)
				parameters[pair.Key] = pair.Value;
		}

		if (values is not null)
		{
			foreach (var pair in values)
				parameters[pair.Key] = pair.Value;
		}

		parameters[ZodIssueParameters.DisplayName] = member.DisplayName;
		parameters[ZodIssueParameters.DeclaringType] = member.DeclaringType;
		if (member.Member.Length > 0)
			parameters[ZodIssueParameters.Member] = member.Member;

		return parameters;
	}
}
