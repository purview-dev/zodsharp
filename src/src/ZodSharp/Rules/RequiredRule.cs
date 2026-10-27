using System.Runtime.CompilerServices;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a value to be present, mirroring the semantics of
/// <c>System.ComponentModel.DataAnnotations.RequiredAttribute</c>.
/// </summary>
/// <typeparam name="T">The value type. Only strings are inspected for emptiness; every other type is present
/// unless it is <see langword="null"/> (a nullable value type or a reference type).</typeparam>
/// <remarks>
/// <para>
/// For a string value the rule always rejects <see langword="null"/>. When
/// <see cref="RequiredRule{T}(bool, bool, string?, string?)">allowEmptyStrings</see> is <see langword="false"/>
/// an empty string is rejected as well; when it is <see langword="true"/> an empty string is accepted. The
/// <c>trimWhitespace</c> option additionally treats a whitespace-only string as empty, so <c>"   "</c> fails
/// unless empty strings are allowed. Leading/trailing whitespace on a non-empty value is preserved, matching a
/// trim-then-check reading of the option.
/// </para>
/// <para>
/// The generated <c>[RequiredZod]</c> attribute surfaces both options as constructor/property arguments and
/// mirrors <c>System.ComponentModel.DataAnnotations.RequiredAttribute</c>: it is emitted outside the
/// generator's non-null guard, so an absent value fails exactly as <c>[Required]</c> fails. The name is
/// suffixed with <c>Zod</c> because <c>RequiredAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </para>
/// </remarks>
[Core.ZodRule]
public readonly record struct RequiredRule<T>
	: Core.IValidationRule<T>,
		Core.IZodRule,
		Core.IRequiredRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "missing_field";

	/// <summary>
	/// Gets the message format; <c>{0}</c> is the display name (<c>"Value"</c> when the rule does not know the
	/// member it validates), as in <c>System.ComponentModel.DataAnnotations</c>. Hosts that know the member can
	/// re-format the reported <see cref="Core.ZodMessages.Required"/> template instead.
	/// </summary>
	public const string MessageFormat = "{0} is required";

	const string DefaultDisplayName = "Value";

	readonly bool _allowEmptyStrings;
	readonly bool _trimWhitespace;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="RequiredRule{T}"/> struct.
	/// </summary>
	/// <param name="allowEmptyStrings">Whether an empty string satisfies the rule. Defaults to <see langword="false"/>.</param>
	/// <param name="trimWhitespace">Whether a whitespace-only string counts as empty. Defaults to <see langword="false"/>.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public RequiredRule(
		bool allowEmptyStrings = false,
		bool trimWhitespace = false,
		string? message = null,
		string? code = null
	)
	{
		_allowEmptyStrings = allowEmptyStrings;
		_trimWhitespace = trimWhitespace;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is present.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if the value is present, false otherwise</returns>
	public bool IsValid(in T value)
	{
		if (typeof(T) == typeof(string))
		{
			var text = Unsafe.As<T, string?>(ref Unsafe.AsRef(in value));
			if (text is null)
				return false;

			if (_allowEmptyStrings)
				return true;

			// If we get here, the string is not null and empty strings are not allowed. Check for whitespace if needed.
			return _trimWhitespace ? !string.IsNullOrWhiteSpace(text) : text.Length != 0;
		}

		return value is not null;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, DefaultDisplayName);

	/// <summary>
	/// Applies nothing: presence is expressed by the object schema's <c>required</c> list, which the exporter
	/// builds from the property's nullability (and any <c>[Required]</c> attribute), not by a per-property
	/// keyword.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
	}

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidType;

	// The shared "{displayName} is required" template, so a host that knows the member (the generator, the
	// ProblemDetails mapper) can name it; a custom message is reported as-is.
	string? Core.IZodRule.MessageFormat => _message is null ? Core.ZodMessages.Required : null;
}
