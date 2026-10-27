namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for ISO 8601 time string format (<c>HH:MM</c>, optionally with <c>:SS</c> and
/// fractional seconds).
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct TimeStringRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid time";

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the TimeStringRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public TimeStringRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an ISO 8601 time.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is an ISO 8601 time without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty || value.IsWhiteSpace())
			return false;

		// Validate the time portion (HH:mm[:ss[.fff]]) with seconds optional
		return IsIsoTime(value, secondsRequired: false);
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

	static bool IsIsoTime(ReadOnlySpan<char> value, bool secondsRequired)
	{
		if (value.Length < 5 || !IsTwoDigitNumber(value, 0, 24) || value[2] != ':' || !IsTwoDigitNumber(value, 3, 60))
			return false;

		if (value.Length == 5)
			return !secondsRequired;

		if (value[5] != ':' || value.Length < 8 || !IsTwoDigitNumber(value, 6, 60))
			return false;

		if (value.Length == 8)
			return true;

		if (value[8] != '.' || value.Length == 9)
			return false;

		for (var i = 9; i < value.Length; i++)
		{
			if (!char.IsAsciiDigit(value[i]))
				return false;
		}

		return true;
	}

	static bool IsTwoDigitNumber(ReadOnlySpan<char> value, int index, int maxExclusive)
	{
		if (index + 1 >= value.Length)
			return false;

		var a = value[index];
		var b = value[index + 1];
		if (!char.IsAsciiDigit(a) || !char.IsAsciiDigit(b))
			return false;

		var numeric = ((a - '0') * 10) + (b - '0');
		return numeric < maxExclusive;
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("time");

	/// <summary>The JSON Schema <c>pattern</c> for the local <c>HH:mm[:ss[.fff]]</c> time this rule accepts.</summary>
	public const string JsonSchemaPattern = "^(?:[01]\\d|2[0-3]):[0-5]\\d(?::[0-5]\\d(?:\\.\\d+)?)?$";

	/// <summary>
	/// Applies <see cref="JsonSchemaPattern"/>. JSON Schema's <c>format: time</c> is an RFC 3339 full-time and
	/// requires an offset this rule never accepts, so the lexical space is expressed as a pattern and the
	/// intent is recorded in the <c>x-zod-format</c> vendor extension.
	/// </summary>
	/// <param name="schema">The schema being built for the validated string.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		JsonSchema.JsonSchemaRules.ConstrainPattern(schema, JsonSchemaPattern);
		schema.SetVendorExtension("x-zod-format", "time");
	}
}
