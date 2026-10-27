namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for minimum string length.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct MinLengthRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		Core.IZodFailureDescriber<string>,
		Core.IZodStringFailureDescriber,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_small";

	/// <summary>Gets the message format; <c>{0}</c> is the minimum length and <c>{1}</c> the actual length.</summary>
	public const string MessageFormat = "String must be at least {0} characters long, but got {1}";

	static readonly string?[] TemplateNames = [RuleMessage.Minimum, Core.ZodIssueParameters.Count];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	// Read by the JSON Schema exporter, which previously reached this by reflecting on the field name.
	internal int MinLength { get; }
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the MinLengthRule struct.
	/// </summary>
	/// <param name="minLength">The minimum length</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public MinLengthRule(int minLength, string? message = null, string? code = null)
	{
		MinLength = minLength;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value meets the minimum length requirement.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value.LengthOrDefault() >= MinLength;

	/// <summary>
	/// Validates that the span meets the minimum length requirement without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Length >= MinLength;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		RuleMessage.Format(_message ?? MessageFormat, MinLength, value.LengthOrDefault());

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, MinLength, value.Length);

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.TooSmall;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(MinLength);

	Core.ZodRuleFailure Core.IZodFailureDescriber<string>.DescribeFailure(in string value) =>
		new(Parameters: RuleParameters.Count(value.LengthOrDefault()));

	Core.ZodRuleFailure Core.IZodStringFailureDescriber.DescribeFailure(ReadOnlySpan<char> value) =>
		new(Parameters: RuleParameters.Count(value.Length));

	/// <summary>Applies the JSON Schema <c>minLength</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.MinLength = MinLength;
	}
}
