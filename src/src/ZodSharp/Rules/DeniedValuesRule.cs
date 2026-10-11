namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that rejects a value that is one of a denied set, mirroring
/// <c>System.ComponentModel.DataAnnotations.DeniedValuesAttribute</c>.
/// </summary>
/// <typeparam name="T">The value type the rule validates.</typeparam>
/// <remarks>
/// The generated <c>[DeniedValuesZod]</c> attribute exposes both a single value and a <c>params object[]</c>
/// array; the array elements are converted back to the member type by the resolver. The name is suffixed with
/// <c>Zod</c> because <c>DeniedValuesAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </remarks>
[Core.ZodRule]
public readonly record struct DeniedValuesRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_value";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Value '{0}' is one of the denied values";

	readonly T[]? _values;
	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeniedValuesRule{T}"/> struct that rejects a single value.
	/// </summary>
	/// <param name="value">The single denied value.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public DeniedValuesRule(T value, string? message = null, string? code = null)
		: this([value], message, code) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="DeniedValuesRule{T}"/> struct.
	/// </summary>
	/// <param name="values">The denied values; <see langword="null"/> or empty denies nothing.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public DeniedValuesRule(T[]? values, string? message = null, string? code = null)
	{
		_values = values is { Length: > 0 } ? values : null;
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is not one of the denied values.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => _values is null || Array.IndexOf(_values, value) < 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>Applies a JSON Schema <c>not</c> constraint against the denied values.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (_values is { Length: > 0 })
			schema.Not = new JsonSchema.JsonSchemaDefinition { Enum = [.. _values.Cast<object?>()] };
	}

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}
