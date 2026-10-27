using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for regex pattern matching.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct RegexRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the required pattern. A custom message may use <c>{1}</c> for the offending value.</summary>
	public const string MessageFormat = "String does not match the required pattern: {0}";

	/// <summary>
	/// The match timeout applied to every pattern this library compiles itself.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A pattern is attacker-adjacent: it reaches the library from a <c>[Regex]</c>/
	/// <c>[RegularExpression]</c> attribute, a fluent call, or an imported JSON Schema, and is then run
	/// against untrusted input. Without a timeout a catastrophically backtracking pattern hangs the
	/// calling thread, so every pattern the library builds carries this budget, and exceeding it is
	/// reported as a validation failure rather than an escaping exception.
	/// </para>
	/// <para>
	/// Two seconds matches the default of <c>System.ComponentModel.DataAnnotations</c>'
	/// <c>RegularExpressionAttribute</c>, which the generated attribute support mirrors. A much tighter
	/// budget is counter-productive for a validation library: on a loaded machine a thread-starved match
	/// can exceed it and reject input that is actually valid, which is a correctness bug rather than a
	/// security win. Bounding the work is what defeats ReDoS; the exact bound is not the control.
	/// Supply a <see cref="Regex"/> yourself if you need a different budget.
	/// </para>
	/// </remarks>
	public static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(DefaultMatchTimeoutMilliseconds);

	/// <summary>
	/// <see cref="DefaultMatchTimeout"/> in milliseconds, as a constant so it can be used in attribute
	/// arguments (for example <c>[GeneratedRegex(pattern, options, RegexRule.DefaultMatchTimeoutMilliseconds)]</c>).
	/// </summary>
	public const int DefaultMatchTimeoutMilliseconds = 2000;

	readonly string? _message;

	// Read by the JSON Schema exporter, which previously reached this by reflecting on the field name.
	internal Regex Pattern { get; }

	/// <summary>
	/// Initializes a new instance of the RegexRule struct.
	/// </summary>
	/// <param name="pattern">The regex pattern</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public RegexRule(Regex pattern, string? message = null, string? code = null)
	{
		Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Initializes a new instance of the RegexRule struct.
	/// </summary>
	/// <param name="pattern">The regex pattern</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public RegexRule(string pattern, string? message = null, string? code = null)
		: this(new Regex(pattern, RegexOptions.Compiled, DefaultMatchTimeout), message, code) { }

	/// <summary>
	/// Validates that the value matches the regex pattern.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise. A pattern that exceeds its match timeout is invalid.</returns>
	public bool IsValid(in string value)
	{
		try
		{
			return Pattern.IsMatch(value);
		}
		catch (RegexMatchTimeoutException)
		{
			// Input the pattern cannot decide within its budget is treated as not matching. Letting
			// this escape would turn a slow input into an unhandled exception (a 500 under ASP.NET
			// Core) rather than the validation failure the caller is equipped to handle.
			return false;
		}
	}

	/// <summary>
	/// Validates that the span matches the regex pattern without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise. A pattern that exceeds its match timeout is invalid.</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		try
		{
			return Pattern.IsMatch(value);
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
	public string GetErrorMessage(in string value) =>
		RuleMessage.Format(_message ?? MessageFormat, Pattern.ToString(), value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) => GetErrorMessage(value.ToString());

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		RuleParameters.Two(
			Core.ZodIssueParameters.Format,
			"regex",
			Core.ZodIssueParameters.Pattern,
			Pattern.ToString()
		);

	/// <summary>Applies the JSON Schema <c>pattern</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		JsonSchema.JsonSchemaRules.ConstrainPattern(schema, Pattern.ToString());
	}
}
