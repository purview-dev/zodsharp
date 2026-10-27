using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for ISO 8601 duration format, equivalent to Zod's <c>z.string().duration()</c>.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct DurationRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid duration";

	/// <summary>
	/// Regular expression for ISO 8601-1 durations. Does not accept the 8601-2 extensions (negative
	/// durations or fractional/negative components), matching Zod.
	/// </summary>
	public static readonly Regex DurationRegex = new(
		@"^P(?:(\d+W)|(?!.*W)(?=\d|T\d)(\d+Y)?(\d+M)?(\d+D)?(T(?=\d)(\d+H)?(\d+M)?(\d+([.,]\d+)?S)?)?)$",
		RegexOptions.Compiled,
		RegexRule.DefaultMatchTimeout
	);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="DurationRule"/> struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public DurationRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an ISO 8601 duration.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value)
	{
		if (value is null)
			return false;

		try
		{
			return DurationRegex.IsMatch(value);
		}
		catch (RegexMatchTimeoutException)
		{
			// Report as invalid rather than letting the exception escape into the host. See
			// RegexRule.DefaultMatchTimeout.
			return false;
		}
	}

	/// <summary>
	/// Validates that the span is an ISO 8601 duration without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		try
		{
			return DurationRegex.IsMatch(value);
		}
		catch (RegexMatchTimeoutException)
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

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, value.ToString());

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("duration");
}
