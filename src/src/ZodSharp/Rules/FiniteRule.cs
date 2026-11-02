using System.Numerics;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for finite number check.
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type; any <see cref="INumber{T}"/> is supported.</typeparam>
[Core.ZodRule]
public readonly record struct FiniteRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : INumber<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "not_finite";

	/// <summary>Gets the message format. The default never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Number must be finite";

	static readonly string?[] TemplateNames = [RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	static readonly IReadOnlyDictionary<string, object?> FiniteParameters = RuleParameters.One(
		Core.ZodIssueParameters.Expected,
		Core.ZodTypeNames.Number
	);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the FiniteRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public FiniteRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is finite.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => T.IsFinite(value);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>
	/// Applies nothing: JSON has no representation for infinity or NaN, so every JSON number is finite and
	/// the constraint is implicit in the <c>number</c> type.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Number;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidType;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => FiniteParameters;
}
