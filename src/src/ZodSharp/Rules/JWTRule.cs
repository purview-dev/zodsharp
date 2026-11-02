namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for JSON Web Token (JWT) format: three base64url-encoded segments separated by periods.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct JWTRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid JWT";

	static readonly string?[] TemplateNames = [RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the JWTRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public JWTRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a JWT.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is a JWT without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty || value.IsWhiteSpace())
			return false;

		var firstDot = value.IndexOf('.');
		if (firstDot <= 0)
			return false;

		var secondDot = value[(firstDot + 1)..].IndexOf('.');
		if (secondDot <= 0)
			return false;

		secondDot += firstDot + 1;

		return IsBase64Url(value[..firstDot])
			&& IsBase64Url(value[(firstDot + 1)..secondDot])
			&& IsBase64Url(value[(secondDot + 1)..]);
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, value.ToString());

	static bool IsBase64Url(ReadOnlySpan<char> segment)
	{
		if (segment.IsEmpty)
			return false;

		foreach (var c in segment)
		{
			if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
				return false;
		}

		return true;
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("jwt");

	/// <summary>
	/// A JWT has no JSON Schema format, so the rule is recorded as unsupported and described by the
	/// <c>x-zod-jwt</c> vendor extension.
	/// </summary>
	/// <param name="schema">The schema being built for the validated string.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.SetVendorExtension("x-zod-jwt", true);
		JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(JWTRule));
	}
}
