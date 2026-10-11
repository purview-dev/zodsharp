using System.Numerics;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for integer values.
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type; any <see cref="INumber{T}"/> is supported.</typeparam>
[Core.ZodRule]
public readonly record struct IntRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where T : INumber<T>
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_type";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Expected integer, but got {0}";

	readonly string _message;

	public IntRule(string? message = null, string? code = null)
	{
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an integer.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	/// <remarks>
	/// The remainder formulation also rejects infinities, which a truncate comparison would accept
	/// (<c>double.PositiveInfinity == Math.Truncate(double.PositiveInfinity)</c>).
	/// </remarks>
	public bool IsValid(in T value) => value % T.One == T.Zero;

	/// <summary>Applies the JSON Schema <c>integer</c> type.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.Type = "integer";
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}
