namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for an exclusive maximum numeric value (strictly less than).
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type</typeparam>
[Core.ZodRule]
public readonly record struct LessThanRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : IComparable<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_big";

	/// <summary>Gets the message format; <c>{0}</c> is the bound and <c>{1}</c> the offending value.</summary>
	public const string MessageFormat = "Value must be less than {0}, but got {1}";

	readonly T _exclusiveMaximum;

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the LessThanRule struct.
	/// </summary>
	/// <param name="exclusiveMaximum">The value the input must be strictly less than</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public LessThanRule(T exclusiveMaximum, string? message = null, string? code = null)
	{
		_exclusiveMaximum = exclusiveMaximum;
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is strictly less than the configured bound.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => value.CompareTo(_exclusiveMaximum) < 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) =>
		RuleMessage.Format(_message ?? MessageFormat, _exclusiveMaximum, value);

	/// <summary>Applies the JSON Schema <c>exclusiveMaximum</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (JsonSchema.JsonSchemaNumbers.TryToDouble(_exclusiveMaximum, out var maximum))
			schema.ExclusiveMaximum = maximum;
		else
			JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(LessThanRule<>));
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}
