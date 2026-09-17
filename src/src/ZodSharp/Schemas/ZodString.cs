using System.Text.RegularExpressions;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for string validation.
/// Provides fluent API for common string validations.
/// </summary>
public class ZodString : ZodType<string>
{
	static readonly string[] EmptyPath = [];

	/// <summary>
	/// Parses and validates a string value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<string> ParseInternal(string value) =>
		value == null
			? ValidationResult<string>.Failure(
				new ValidationError("invalid_type", "Expected string, but got null", EmptyPath)
			)
			: ValidationResult<string>.Success(value);

	/// <summary>
	/// Validates a ReadOnlySpan of characters without allocating a string.
	/// </summary>
	/// <param name="value">The span to validate</param>
	/// <returns>A validation result</returns>
	public ValidationResult<string> ValidateSpan(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty && value.Length == 0)
		{
			return ValidationResult<string>.Success(string.Empty);
		}

		var str = value.ToString();
		return Validate(str);
	}

	/// <summary>
	/// Adds a minimum length validation.
	/// </summary>
	/// <param name="minLength">The minimum length</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Min(int minLength)
	{
		AddRule(new MinLengthRule(minLength));
		return this;
	}

	/// <summary>
	/// Adds a maximum length validation.
	/// </summary>
	/// <param name="maxLength">The maximum length</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Max(int maxLength)
	{
		AddRule(new MaxLengthRule(maxLength));
		return this;
	}

	/// <summary>
	/// Adds an email format validation.
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	public ZodString Email()
	{
		AddRule(new EmailRule());
		return this;
	}

	/// <summary>
	/// Adds a regex pattern validation.
	/// </summary>
	/// <param name="pattern">The regex pattern</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Regex(Regex pattern, string? message = null)
	{
		AddRule(new RegexRule(pattern, message));
		return this;
	}

	/// <summary>
	/// Adds a regex pattern validation from a string.
	/// </summary>
	/// <param name="pattern">The regex pattern string</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Regex(string pattern, string? message = null)
	{
		Regex regex = new(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		return Regex(regex, message);
	}

	/// <summary>
	/// Sets the exact string length.
	/// </summary>
	/// <param name="length">The exact length</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Length(int length)
	{
		AddRule(new MinLengthRule(length));
		AddRule(new MaxLengthRule(length));
		return this;
	}

	/// <summary>
	/// Adds a URL format validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Naming",
		"PDS0004:Use correct acronym capitalization",
		Justification = "Name is real"
	)]
	public ZodString Url(string? message = null)
	{
		AddRule(new UrlRule(message));
		return this;
	}

	/// <summary>
	/// Adds a phone number format validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Phone(string? message = null)
	{
		AddRule(new PhoneRule(message));
		return this;
	}

	/// <summary>
	/// Adds a credit card number format validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString CreditCard(string? message = null)
	{
		AddRule(new CreditCardRule(message));
		return this;
	}

	/// <summary>
	/// Adds a Base64 string format validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString Base64String(string? message = null)
	{
		AddRule(new Base64StringRule(message));
		return this;
	}

	/// <summary>
	/// Adds a UUID format validation. Accepts RFC 9562 versions 1-8 with the RFC
	/// variant nibble, plus the nil and max UUIDs.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString UUID(string? message = null)
	{
		AddRule(new UUIDRule(message));
		return this;
	}

	/// <summary>
	/// Adds a UUID format validation requiring a specific RFC 9562 version.
	/// </summary>
	/// <param name="version">The required UUID version</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString UUID(UuidVersion version, string? message = null)
	{
		AddRule(new UUIDRule(version, message));
		return this;
	}

	/// <summary>
	/// Adds a validation that the string must start with the specified prefix.
	/// </summary>
	/// <param name="prefix">The required prefix</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString StartsWith(string prefix, string? message = null)
	{
		AddRule(new StartsWithRule(prefix, message));
		return this;
	}

	/// <summary>
	/// Adds a validation that the string must end with the specified suffix.
	/// </summary>
	/// <param name="suffix">The required suffix</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodString EndsWith(string suffix, string? message = null)
	{
		AddRule(new EndsWithRule(suffix, message));
		return this;
	}

	/// <summary>
	/// Transforms the string to lowercase.
	/// </summary>
	/// <returns>A new schema that transforms the value</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public ZodString ToLower()
	{
		var transform = Transform(static s => s.ToLowerInvariant());
		return new ZodStringWrapper(transform);
	}

	/// <summary>
	/// Transforms the string to uppercase.
	/// </summary>
	/// <returns>A new schema that transforms the value</returns>
	public ZodString ToUpper()
	{
		var transform = Transform(static s => s.ToUpperInvariant());
		return new ZodStringWrapper(transform);
	}

	/// <summary>
	/// Trims whitespace from the string.
	/// </summary>
	/// <returns>A new schema that transforms the value</returns>
	public ZodString Trim()
	{
		var transform = Transform(static s => s.Trim());
		return new ZodStringWrapper(transform);
	}

	class ZodStringWrapper(ZodTransform<string, string> transform) : ZodString
	{
		protected override ValidationResult<string> ParseInternal(string value) => transform.Validate(value);
	}
}
