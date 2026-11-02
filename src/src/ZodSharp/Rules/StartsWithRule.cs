using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for string prefix.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct StartsWithRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the required prefix and <c>{1}</c> the value.</summary>
	public const string MessageFormat = "String must start with '{0}'";

	static readonly string?[] TemplateNames = [Core.ZodIssueParameters.Prefix, RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly string _prefix;
	readonly StringComparison _comparison;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the StartsWithRule struct.
	/// </summary>
	/// <param name="prefix">The required prefix</param>
	/// <param name="comparison">The string comparison type</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public StartsWithRule(
		string prefix,
		StringComparison comparison = StringComparison.Ordinal,
		string? message = null,
		string? code = null
	)
	{
		_prefix = prefix.OrNull() ?? throw new ArgumentNullException(nameof(prefix));
		_comparison = comparison;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value starts with the specified prefix.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value != null && value.StartsWith(_prefix, _comparison);

	/// <summary>
	/// Validates that the span starts with the specified prefix without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.StartsWith(_prefix.AsSpan(), _comparison);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, _prefix, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, _prefix, value.ToString());

	/// <summary>Applies a JSON Schema anchored <c>pattern</c> for the required prefix.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		JsonSchema.JsonSchemaRules.ConstrainPattern(schema, "^" + Regex.Escape(_prefix));
		schema.SetVendorExtension("x-zod-startsWith", _prefix);
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		RuleParameters.Two(Core.ZodIssueParameters.Format, "starts_with", Core.ZodIssueParameters.Prefix, _prefix);
}
