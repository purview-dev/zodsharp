using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for URL format.
/// <strong>This will allocate if the default Regex fails to validate by utilising <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/></strong>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Naming",
	"PDS0004:Use correct acronym capitalization",
	Justification = "Name is real"
)]
[Core.ZodRule]
public readonly record struct UrlRule : Core.IValidationRule<string>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Invalid URL format: {0}";

	static readonly Regex UrlRegex = new(
		@"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase,
		RegexRule.DefaultMatchTimeout
	);

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the UrlRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public UrlRule(string? message = null, string? code = null)
	{
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a valid URL.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) =>
		!string.IsNullOrWhiteSpace(value)
		&& (
			UrlRegex.IsMatch(value)
			|| (
				Uri.TryCreate(value, UriKind.Absolute, out var uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
			)
		);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value);

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

	string? Core.IZodRule.Origin => null;
}
