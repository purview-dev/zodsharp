namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for an inclusive lower numeric bound (greater than or equal to).
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The comparable type</typeparam>
[Core.ZodRule]
public readonly record struct GreaterThanOrEqualRule<T>
	: Core.IValidationRule<T>,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
	where T : IComparable<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_small";

	/// <summary>Gets the message format; <c>{0}</c> is the bound and <c>{1}</c> the offending value.</summary>
	public const string MessageFormat = "Value must be greater than or equal to {0}, but got {1}";

	// Read by the JSON Schema exporter, which previously reached this by reflecting on the field name.
	internal T MinValue { get; }
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the GreaterThanOrEqualRule struct.
	/// </summary>
	/// <param name="minValue">The inclusive lower bound</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public GreaterThanOrEqualRule(T minValue, string? message = null, string? code = null)
	{
		MinValue = minValue;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is greater than or equal to the configured bound.
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

	/// <summary>Applies the JSON Schema <c>minimum</c> constraint (inclusive).</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (JsonSchema.JsonSchemaNumbers.TryToDouble(MinValue, out var minimum))
			schema.Minimum = minimum;
		else
			JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(GreaterThanOrEqualRule<>));
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Of(typeof(T));

	string? Core.IZodRule.Category => Core.ZodIssueCategories.TooSmall;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(MinValue);
}
