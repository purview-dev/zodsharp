using System.Numerics;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for multiple-of check.
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type; any <see cref="INumber{T}"/> is supported.</typeparam>
[Core.ZodRule]
public readonly record struct MultipleOfRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : INumber<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "not_multiple_of";

	/// <summary>Gets the message format; <c>{0}</c> is the divisor. The default never contains the input; a custom message may use <c>{1}</c> for the offending value.</summary>
	public const string MessageFormat = "Number must be a multiple of {0}";

	static readonly string?[] TemplateNames = [Core.ZodIssueParameters.Divisor, RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	/// <summary>
	/// The relative tolerance applied when comparing the distance to the nearest multiple.
	/// </summary>
	const double RelativeTolerance = 1e-12;

	// Read by the JSON Schema exporter, which previously reached this by reflecting on the field name.
	internal T Divisor { get; }
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the MultipleOfRule struct.
	/// </summary>
	/// <param name="divisor">The divisor</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public MultipleOfRule(T divisor, string? message = null, string? code = null)
	{
		if (divisor == T.Zero)
			throw new ArgumentException("Divisor cannot be zero", nameof(divisor));

		Divisor = divisor;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a multiple of the divisor.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	/// <remarks>
	/// Floating-point division is inexact (for example <c>0.3 / 0.1 == 2.9999999999999996</c>), so the
	/// distance to the nearest multiple is compared against a relative tolerance instead of testing the raw
	/// remainder. The tolerance saturates to zero for types that cannot represent it (the integer types), so
	/// those compare exactly.
	/// </remarks>
	public bool IsValid(in T value)
	{
		if (!T.IsFinite(value))
			return false;

		var remainder = T.Abs(value % Divisor);
		var distance = T.Min(remainder, T.Abs(Divisor) - remainder);
		var tolerance = T.CreateSaturating(RelativeTolerance) * T.Max(T.Abs(Divisor), T.Abs(value));
		return distance <= tolerance;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, Divisor, value);

	/// <summary>Applies the JSON Schema <c>multipleOf</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (JsonSchema.JsonSchemaNumbers.TryToDouble(Divisor, out var divisor))
			schema.MultipleOf = divisor;
		else
			JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(MultipleOfRule<>));
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Of(typeof(T));

	string? Core.IZodRule.Category => Core.ZodIssueCategories.NotMultipleOf;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		RuleParameters.One(Core.ZodIssueParameters.Divisor, Divisor);
}
