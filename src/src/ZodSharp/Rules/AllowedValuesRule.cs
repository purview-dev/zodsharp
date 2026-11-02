namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a value to be one of an allowed set, mirroring
/// <c>System.ComponentModel.DataAnnotations.AllowedValuesAttribute</c>.
/// </summary>
/// <typeparam name="T">The value type the rule validates.</typeparam>
/// <remarks>
/// The generated <c>[AllowedValuesZod]</c> attribute exposes both a single value and a <c>params object[]</c>
/// array; the array elements are converted back to the member type by the resolver. The name is suffixed with
/// <c>Zod</c> because <c>AllowedValuesAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </remarks>
[Core.ZodRule]
public readonly record struct AllowedValuesRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_value";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Value is not one of the allowed values";

	static readonly string?[] TemplateNames = [RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly T[]? _values;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="AllowedValuesRule{T}"/> struct that accepts a single value.
	/// </summary>
	/// <param name="value">The single allowed value.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public AllowedValuesRule(T value, string? message = null, string? code = null)
		: this([value], message, code) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="AllowedValuesRule{T}"/> struct.
	/// </summary>
	/// <param name="values">The allowed values; <see langword="null"/> or empty accepts every value.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public AllowedValuesRule(T[]? values, string? message = null, string? code = null)
	{
		_values = values is { Length: > 0 } ? values : null;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is one of the allowed values.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => _values is null || Array.IndexOf(_values, value) >= 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>Applies the JSON Schema <c>enum</c> constraint.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		if (_values is { Length: > 0 })
			schema.Enum = [.. _values.Cast<object?>()];
	}

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Of(typeof(T));

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidValue;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		_values is null ? null : RuleParameters.One(Core.ZodIssueParameters.Options, Array.AsReadOnly(_values));
}
