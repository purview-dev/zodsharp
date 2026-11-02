namespace ZodSharp.Core;

/// <summary>
/// The <see cref="ValidationError.Code"/> values reported by the built-in schemas. The same failure reports the
/// same code whichever producer reports it (runtime schema, generated validator, JSON Schema import); the
/// matching Zod issue code is reported as <see cref="ValidationError.Category"/> (see
/// <see cref="ZodIssueCategories"/>). Rules declare their own codes as <c>ErrorCode</c> constants.
/// </summary>
public static class ZodIssueCodes
{
	/// <summary>The value has the wrong type, or is <see langword="null"/> where a value is required.</summary>
	public const string InvalidType = "invalid_type";

	/// <summary>A required field (or member) is missing. Category <see cref="ZodIssueCategories.InvalidType"/>.</summary>
	public const string MissingField = "missing_field";

	/// <summary>A refinement (<c>Refine</c>, <c>SuperRefine</c>, a custom validation method) failed.</summary>
	public const string RefinementFailed = "refinement_failed";

	/// <summary>No option of a union matched.</summary>
	public const string InvalidUnion = "invalid_union";

	/// <summary>The discriminator property of a discriminated union is absent.</summary>
	public const string MissingDiscriminator = "missing_discriminator";

	/// <summary>The discriminator value does not select any option of a discriminated union.</summary>
	public const string InvalidDiscriminator = "invalid_discriminator";

	/// <summary>The value is not one of an enum's allowed values.</summary>
	public const string InvalidEnumValue = "invalid_enum_value";

	/// <summary>The value does not equal a literal.</summary>
	public const string InvalidLiteral = "invalid_literal";

	/// <summary>A tuple has the wrong number of elements.</summary>
	public const string InvalidTupleLength = "invalid_tuple_length";

	/// <summary>An object contains keys its schema does not declare (one aggregated issue).</summary>
	public const string UnrecognizedKeys = "unrecognized_keys";

	/// <summary>An object contains a key its schema does not declare (one issue per key, opt-in compatibility mode).</summary>
	public const string UnrecognizedKey = "unrecognized_key";

	/// <summary>A size or range bound was not reached.</summary>
	public const string TooSmall = "too_small";

	/// <summary>A size or range bound was exceeded.</summary>
	public const string TooBig = "too_big";

	/// <summary>A transform threw.</summary>
	public const string TransformError = "transform_error";
}
