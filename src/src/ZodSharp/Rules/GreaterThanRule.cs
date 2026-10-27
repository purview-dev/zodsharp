namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for an exclusive minimum numeric value (strictly greater than).
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type</typeparam>
[Core.ZodRule]
public readonly record struct GreaterThanRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : IComparable<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_small";

	/// <summary>Gets the message format; <c>{0}</c> is the bound. The default never contains the input; a custom message may use <c>{1}</c> for the offending value.</summary>
	public const string MessageFormat = "Value must be greater than {0}";

	static readonly string?[] TemplateNames = [RuleMessage.Minimum, RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly T _exclusiveMinimum;

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the GreaterThanRule struct.
	/// </summary>
	/// <param name="exclusiveMinimum">The value the input must be strictly greater than</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public GreaterThanRule(T exclusiveMinimum, string? message = null, string? code = null)
	{
		_exclusiveMinimum = exclusiveMinimum;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is strictly greater than the configured bound.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => value.CompareTo(_exclusiveMinimum) > 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) =>
		RuleMessage.Format(_message ?? MessageFormat, _exclusiveMinimum, value);

	/// <summary>Applies the JSON Schema <c>exclusiveMinimum</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (JsonSchema.JsonSchemaNumbers.TryToDouble(_exclusiveMinimum, out var minimum))
			schema.ExclusiveMinimum = minimum;
		else
			JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(GreaterThanRule<>));
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Of(typeof(T));

	string? Core.IZodRule.Category => Core.ZodIssueCategories.TooSmall;

	// Exclusive: the value must be strictly greater than the bound.
	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Excluding(_exclusiveMinimum);
}
