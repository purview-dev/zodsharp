namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for Base64 strings.
/// Mirrors the behavior of System.ComponentModel.DataAnnotations.Base64StringAttribute.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct Base64StringRule : Core.IValidationRule<string>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Invalid Base64 string format: {0}";

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the Base64StringRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public Base64StringRule(string? message = null, string? code = null)
	{
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a valid Base64 string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		try
		{
			Convert.FromBase64String(value);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;

	/// <summary>Applies <c>contentEncoding: base64</c>.</summary>
	/// <param name="schema">The schema being built for the validated string.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.ContentEncoding = "base64";
	}
}
