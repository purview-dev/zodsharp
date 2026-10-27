namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for a Uri.
/// <strong>This will allocate by utilising <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/></strong>
/// </summary>
[Core.ZodRule]
public readonly record struct UriRule : Core.IValidationRule<string>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format; <c>{0}</c> is the <see cref="UriKind"/>. It never contains the input; a custom message may use <c>{1}</c> for the value.</summary>
	public const string MessageFormat = "Invalid Uri, kind: {0}";

	readonly UriKind _uriKind;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the UriRule struct.
	/// </summary>
	/// <param name="uriKind">The uri kind to validate against</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public UriRule(UriKind uriKind, string? message = null, string? code = null)
	{
		_uriKind = uriKind;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a valid URL.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) =>
		!string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, _uriKind, out var _);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, _uriKind, value);

	/// <summary>Applies the JSON Schema <c>uri</c> string format.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.Format = "uri";
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("uri");
}
