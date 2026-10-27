namespace ZodSharp.Core;

/// <summary>
/// The default message templates of the built-in schemas, with named placeholders resolved by
/// <see cref="ZodMessageFormat"/> from the error's <see cref="ValidationError.Parameters"/> and built-in fields.
/// Every producer (runtime schemas, generated validators, JSON integrations) uses these so the same failure
/// reads the same everywhere. A template is reported as <see cref="ValidationError.MessageFormat"/> so a host can
/// re-format (or localize) it.
/// </summary>
/// <remarks>
/// No template contains the input value: messages leave the process. See
/// <see cref="ZodErrorOptions.IncludeReceivedValues"/>.
/// </remarks>
public static class ZodMessages
{
	/// <summary>A <see langword="null"/> value where a value of type <c>{expected}</c> is required.</summary>
	public const string NullValue = "Expected {expected}, received null";

	/// <summary>A value of the wrong type; <c>{received}</c> is the received type name, never the value.</summary>
	public const string InvalidType = "Expected {expected}, received {received}";

	/// <summary>A missing required field or member.</summary>
	public const string Required = "{displayName} is required";

	/// <summary>The default message of a failed refinement.</summary>
	public const string RefinementFailed = "Custom validation failed";

	/// <summary>No union option matched.</summary>
	public const string InvalidUnion = "Value does not match any of the union options";

	/// <summary>No union option matched; <c>{options}</c> lists the option type names.</summary>
	public const string InvalidUnionOf = "Value does not match any of the union options ({options})";

	/// <summary>The discriminator property of a discriminated union is absent.</summary>
	public const string MissingDiscriminator = "Discriminator field '{discriminator}' not found";

	/// <summary>The discriminator value does not select an option; <c>{options}</c> lists the valid values.</summary>
	public const string InvalidDiscriminator = "Invalid discriminator value. Expected one of: {options}";

	/// <summary>The value is not one of an enum's allowed values.</summary>
	public const string InvalidEnumValue = "Expected one of: {options}";

	/// <summary>The value does not equal the literal <c>{expected}</c>.</summary>
	public const string InvalidLiteral = "Expected literal {expected}";

	/// <summary>An object contains keys its schema does not declare.</summary>
	public const string UnrecognizedKeys = "Unrecognized keys: {keys}";

	/// <summary>An object contains a key its schema does not declare (per-key compatibility mode).</summary>
	public const string UnrecognizedKey = "Unrecognized key '{key}'";

	/// <summary>An array has fewer elements than its minimum.</summary>
	public const string ArrayTooSmall = "Array must have at least {minimum} elements, but got {count}";

	/// <summary>An array has more elements than its maximum.</summary>
	public const string ArrayTooBig = "Array must have at most {maximum} elements, but got {count}";

	/// <summary>An array does not have its exact required length.</summary>
	public const string ArrayExactLength = "Array must have exactly {minimum} elements, but got {count}";

	/// <summary>An array is empty but must not be.</summary>
	public const string ArrayNonEmpty = "Array must not be empty";

	/// <summary>A tuple has the wrong number of elements.</summary>
	public const string TupleLength = "Expected tuple of length {minimum}, but got {count}";

	/// <summary>A transform threw.</summary>
	public const string TransformFailed = "Transform failed.";
}
