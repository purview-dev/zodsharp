using System.Collections.Immutable;
using System.Globalization;

namespace ZodSharp.Core;

/// <summary>
/// Builds the errors the built-in schemas report for structural failures (null and wrong-type values, missing
/// fields, refinements, unions, array sizes), with the code, Zod category, parameters, message template and
/// formatted message every producer shares. Generated validators and the JSON integrations use these too, so the
/// same failure produces an identical <see cref="ValidationError"/> whichever producer reports it.
/// </summary>
public static class ZodIssues
{
	/// <summary>
	/// Creates the <see cref="ZodIssueCodes.InvalidType"/> error for a <see langword="null"/> value.
	/// </summary>
	/// <param name="expected">The expected Zod type name (see <see cref="ZodTypeNames"/>).</param>
	/// <param name="path">The error path.</param>
	/// <param name="origin">The optional origin.</param>
	/// <returns>The error: <c>"Expected {expected}, received null"</c>.</returns>
	public static ValidationError NullValue(string expected, ZodPath path = default, string? origin = null) =>
		Formatted(
			ZodIssueCodes.InvalidType,
			ZodIssueCategories.InvalidType,
			ZodMessages.NullValue,
			path,
			origin,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[ZodIssueParameters.Expected] = expected,
				[ZodIssueParameters.Received] = ZodTypeNames.Null,
			}
		);

	/// <summary>
	/// Creates the <see cref="ZodIssueCodes.InvalidType"/> error for a value of the wrong type. Only the received
	/// value's Zod type name is reported, never the value.
	/// </summary>
	/// <param name="expected">The expected Zod type name (see <see cref="ZodTypeNames"/>).</param>
	/// <param name="received">The received value.</param>
	/// <param name="path">The error path.</param>
	/// <param name="origin">The optional origin.</param>
	/// <returns>The error: <c>"Expected {expected}, received {received}"</c>.</returns>
	public static ValidationError InvalidType(
		string expected,
		object? received,
		ZodPath path = default,
		string? origin = null
	) =>
		received is null
			? NullValue(expected, path, origin)
			: Formatted(
				ZodIssueCodes.InvalidType,
				ZodIssueCategories.InvalidType,
				ZodMessages.InvalidType,
				path,
				origin,
				new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					[ZodIssueParameters.Expected] = expected,
					[ZodIssueParameters.Received] = ZodTypeNames.OfValue(received),
				}
			);

	/// <summary>
	/// Creates the <see cref="ZodIssueCodes.MissingField"/> error for an absent required field.
	/// </summary>
	/// <param name="displayName">The field's display name (the key or member name when there is no other).</param>
	/// <param name="path">The error path (normally the field's own path).</param>
	/// <returns>The error: <c>"{displayName} is required"</c>.</returns>
	public static ValidationError MissingField(string displayName, ZodPath path) =>
		Formatted(
			ZodIssueCodes.MissingField,
			ZodIssueCategories.InvalidType,
			ZodMessages.Required,
			path,
			null,
			new Dictionary<string, object?>(StringComparer.Ordinal) { [ZodIssueParameters.DisplayName] = displayName }
		);

	/// <summary>
	/// Creates the <see cref="ZodIssueCodes.RefinementFailed"/> error for a failed refinement.
	/// </summary>
	/// <param name="message">The refinement's message; <see langword="null"/> uses <see cref="ZodMessages.RefinementFailed"/>.</param>
	/// <param name="path">The error path.</param>
	/// <returns>The error, with category <see cref="ZodIssueCategories.Custom"/>.</returns>
	public static ValidationError RefinementFailed(string? message = null, ZodPath path = default) =>
		ValidationError.Create(
			ZodIssueCodes.RefinementFailed,
			message ?? ZodMessages.RefinementFailed,
			path,
			category: ZodIssueCategories.Custom
		) with
		{
			MessageFormat = message is null ? ZodMessages.RefinementFailed : null,
		};

	/// <summary>
	/// Creates the <see cref="ZodIssueCodes.InvalidUnion"/> error carrying each option's issues.
	/// </summary>
	/// <param name="unionErrors">The issues of each option, in declaration order, with paths relative to the union.</param>
	/// <param name="options">The option type names shown in the message, or <see langword="null"/> for none.</param>
	/// <param name="path">The error path.</param>
	/// <returns>The error, with the per-option issues under <see cref="ZodIssueParameters.UnionErrors"/>.</returns>
	public static ValidationError InvalidUnion(
		ImmutableArray<ImmutableArray<ValidationError>> unionErrors,
		IReadOnlyList<string>? options = null,
		ZodPath path = default
	)
	{
		Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
		{
			[ZodIssueParameters.UnionErrors] = unionErrors.IsDefault ? [] : unionErrors,
		};

		if (options is { Count: > 0 })
			parameters[ZodIssueParameters.Options] = ImmutableArray.CreateRange(options);

		return Formatted(
			ZodIssueCodes.InvalidUnion,
			ZodIssueCategories.InvalidUnion,
			options is { Count: > 0 } ? ZodMessages.InvalidUnionOf : ZodMessages.InvalidUnion,
			path,
			null,
			parameters
		);
	}

	/// <summary>
	/// Creates the size error for a collection of <paramref name="count"/> elements, or <see langword="null"/>
	/// when the count satisfies both bounds. Equal bounds report the exact-length message.
	/// </summary>
	/// <param name="count">The element count.</param>
	/// <param name="minimum">The inclusive minimum, if any.</param>
	/// <param name="maximum">The inclusive maximum, if any.</param>
	/// <param name="tooSmallMessage">A custom message for a count below <paramref name="minimum"/>.</param>
	/// <param name="tooBigMessage">A custom message for a count above <paramref name="maximum"/>.</param>
	/// <param name="path">The error path.</param>
	/// <param name="origin">The origin; <c>"array"</c> by default.</param>
	/// <returns>The error, or <see langword="null"/>.</returns>
	public static ValidationError? CollectionSize(
		int count,
		int? minimum,
		int? maximum,
		string? tooSmallMessage = null,
		string? tooBigMessage = null,
		ZodPath path = default,
		string origin = "array"
	)
	{
		var tooSmall = minimum is { } min && count < min;
		var tooBig = !tooSmall && maximum is { } max && count > max;
		if (!tooSmall && !tooBig)
			return null;

		var exact = minimum is not null && minimum == maximum;
		var template =
			exact ? ZodMessages.ArrayExactLength
			: tooSmall ? ZodMessages.ArrayTooSmall
			: ZodMessages.ArrayTooBig;
		// A custom message is formatted with the same never-throw engine, so it may use the same placeholders.
		var format = (tooSmall ? tooSmallMessage : tooBigMessage) ?? template;

		var error = ValidationError.Create(
			tooSmall ? ZodIssueCodes.TooSmall : ZodIssueCodes.TooBig,
			string.Empty,
			path,
			new Dictionary<string, object?>(StringComparer.Ordinal) { [ZodIssueParameters.Count] = count },
			origin,
			minimum,
			maximum,
			inclusive: true,
			category: tooSmall ? ZodIssueCategories.TooSmall : ZodIssueCategories.TooBig
		) with
		{
			MessageFormat = format,
		};

		return error with
		{
			Message = ZodMessageFormat.Format(format, error, CultureInfo.InvariantCulture),
		};
	}

	/// <summary>
	/// Builds an error whose message is <paramref name="template"/> formatted (invariantly) against the error's
	/// own parameters and fields, reporting the template as <see cref="ValidationError.MessageFormat"/>.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="category">The Zod issue code.</param>
	/// <param name="template">The message template.</param>
	/// <param name="path">The error path.</param>
	/// <param name="origin">The origin.</param>
	/// <param name="parameters">The parameters.</param>
	/// <returns>The error.</returns>
	public static ValidationError Formatted(
		string code,
		string? category,
		string template,
		ZodPath path,
		string? origin,
		IReadOnlyDictionary<string, object?>? parameters
	)
	{
		var error = ValidationError.Create(code, string.Empty, path, parameters, origin, category: category) with
		{
			MessageFormat = template,
		};

		return error with
		{
			Message = ZodMessageFormat.Format(template, error, CultureInfo.InvariantCulture),
		};
	}

	/// <summary>
	/// Gets the per-option issues of an <see cref="ZodIssueCategories.InvalidUnion"/> error (see
	/// <see cref="ZodIssueParameters.UnionErrors"/>), or an empty array when the error carries none.
	/// </summary>
	/// <param name="error">The error.</param>
	/// <returns>The issues of each option, with paths relative to the union.</returns>
	public static ImmutableArray<ImmutableArray<ValidationError>> GetUnionErrors(this ValidationError error) =>
		error.Parameters is { } parameters
		&& parameters.TryGetValue(ZodIssueParameters.UnionErrors, out var value)
		&& value is ImmutableArray<ImmutableArray<ValidationError>> unionErrors
			? unionErrors
			: [];
}
