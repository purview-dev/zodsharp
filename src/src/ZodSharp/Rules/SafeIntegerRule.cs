namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for safe integer check.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct SafeIntegerRule
	: Core.IValidationRule<double>,
		Core.IZodRule,
		Core.IZodFailureDescriber<double>,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_big";

	static readonly IReadOnlyDictionary<string, object?> IntParameters = RuleParameters.One(
		Core.ZodIssueParameters.Expected,
		"int"
	);

	/// <summary>Gets the message format. The default never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Number must be a safe integer";

	static readonly string?[] TemplateNames = [RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the SafeIntegerRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public SafeIntegerRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a safe integer (within int.MinValue and int.MaxValue).
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in double value) =>
		value == Math.Truncate(value) && value >= int.MinValue && value <= int.MaxValue;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in double value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>Applies the JSON Schema bounds of a safe integer (an <c>int</c>-range whole number).</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.Type = "integer";
		schema.Minimum = int.MinValue;
		schema.Maximum = int.MaxValue;
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Number;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(int.MinValue);

	Core.ErrorBound? Core.IZodRule.Upper => Core.ErrorBound.Including(int.MaxValue);

	// A fraction (or NaN) is not an integer at all, which Zod reports as invalid_type; an out-of-range
	// integer is too_small or too_big by direction.
	Core.ZodRuleFailure Core.IZodFailureDescriber<double>.DescribeFailure(in double value) =>
		value != Math.Truncate(value) ? new(Core.ZodIssueCategories.InvalidType, IntParameters)
		: value < int.MinValue ? new(Core.ZodIssueCategories.TooSmall)
		: new(Core.ZodIssueCategories.TooBig);
}
