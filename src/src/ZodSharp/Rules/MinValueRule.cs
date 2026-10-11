namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for minimum numeric value.
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type</typeparam>
[Core.ZodRule]
public readonly record struct MinValueRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : IComparable<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_small";

	/// <summary>Gets the message format; <c>{0}</c> is the bound and <c>{1}</c> the offending value.</summary>
	public const string MessageFormat = "Value must be at least {0}, but got {1}";

	// Read by the JSON Schema exporter, which previously reached this by reflecting on the field name.
	internal T MinValue { get; }

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the MinValueRule struct.
	/// </summary>
	/// <param name="minValue">The minimum value</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public MinValueRule(T minValue, string? message = null, string? code = null)
	{
		MinValue = minValue;
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is greater than or equal to the minimum value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => value.CompareTo(MinValue) >= 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, MinValue, value);

	/// <summary>Applies the JSON Schema <c>minimum</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (JsonSchema.JsonSchemaNumbers.TryToDouble(MinValue, out var minimum))
			schema.Minimum = minimum;
		else
			JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(MinValueRule<>));
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}
