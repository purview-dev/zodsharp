using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for string containment.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct IncludesRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the required substring and <c>{1}</c> the value.</summary>
	public const string MessageFormat = "String must contain '{0}'";

	readonly string _substring;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the IncludesRule struct.
	/// </summary>
	/// <param name="substring">The required substring</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public IncludesRule(string substring, string? message = null, string? code = null)
	{
		_substring = substring ?? throw new ArgumentNullException(nameof(substring));
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value contains the configured substring.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && value.Contains(_substring, StringComparison.Ordinal);

	/// <summary>
	/// Validates that the span contains the configured substring without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Contains(_substring.AsSpan(), StringComparison.Ordinal);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, _substring, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, _substring, value.ToString());

	/// <summary>Applies a JSON Schema <c>pattern</c> for the required substring.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		JsonSchema.JsonSchemaRules.ConstrainPattern(schema, Regex.Escape(_substring));
		schema.SetVendorExtension("x-zod-includes", _substring);
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		RuleParameters.Two(Core.ZodIssueParameters.Format, "includes", Core.ZodIssueParameters.Includes, _substring);
}
