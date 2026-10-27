using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for array validation.
/// </summary>
/// <typeparam name="T">The element type</typeparam>
/// <remarks>
/// Initializes a new instance of the ZodArray class.
/// </remarks>
/// <param name="elementSchema">The schema for array elements</param>
public class ZodArray<T>(IZodSchema<T, T> elementSchema) : ZodType<T[], T[]>, JsonSchema.IJsonSchemaArrayInfo
{
	int? _minLength;
	int? _maxLength;

	// One message per bound: a shared message meant Min(2).Max(5) reported a too_small failure with the
	// Max message. Null means "use the default template", which reports the count.
	string? _minMessage;
	string? _maxMessage;

	// IJsonSchemaArrayInfo: the JSON Schema exporter's non-generic seam. It previously reflected on private
	// field names that do not exist, so bounds and the element schema were silently dropped from every
	// exported schema. See IJsonSchemaArrayInfo.
	int? JsonSchema.IJsonSchemaArrayInfo.MinItems => _minLength;

	int? JsonSchema.IJsonSchemaArrayInfo.MaxItems => _maxLength;

	object JsonSchema.IJsonSchemaArrayInfo.ElementSchema => elementSchema;

	/// <summary>
	/// Parses and validates an array value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<T[]> ParseInternal(T[] value)
	{
		if (value == null)
		{
			return ValidationResult<T[]>.Failure(ZodIssues.NullValue(ZodTypeNames.Array));
		}

		// The length constraints depend only on the array's length, so they are checked before any element
		// is validated. Checking them afterwards meant an array of a million elements against .Max(10) ran a
		// million element validations and allocated a million ValidationErrors — each with an interpolated
		// index string — before reporting that the array was simply too long. A single request could pin a
		// core and a large heap allocation on input the schema had already declared out of range.
		//
		// This does change which error is reported when an array is both the wrong length and has invalid
		// elements: the length failure now wins. That is the more useful answer, and it matches the
		// container constraint being the outer one.
		if (
			ZodIssues.CollectionSize(value.Length, _minLength, _maxLength, _minMessage, _maxMessage) is
			{ } lengthFailure
		)
			return ValidationResult<T[]>.Failure(lengthFailure);

		List<ValidationError>? errors = null;
		List<T>? rebuilt = null;

		for (var i = 0; i < value.Length; i++)
		{
			var itemResult = elementSchema.Validate(value[i]);
			if (!itemResult.IsSuccess)
			{
				errors ??= [];
				// The index is the prefix and the element's own path follows it, so a nested failure renders
				// as "[0].email". WithPathPrefix keeps every other field of the element's error.
				var segment = ZodPathSegment.At(i);
				foreach (var error in itemResult.Errors)
					errors.Add(error.WithPathPrefix(segment));
			}
			else
			{
				var itemValue = itemResult.Value;
				if (rebuilt is null && !SameValue(itemValue, value[i]))
				{
					rebuilt = [with(value.Length)];
					for (var j = 0; j < i; j++)
						rebuilt.Add(value[j]);
				}

				rebuilt?.Add(itemValue);
			}
		}

		if (errors is { Count: > 0 })
			return ValidationResult<T[]>.Failure(errors);

		// When every element passed through unchanged, the input array is already
		// the validated result, so reuse it instead of copying.
		return rebuilt is not null ? ValidationResult<T[]>.Success([.. rebuilt]) : ValidationResult<T[]>.Success(value);
	}

	/// <summary>
	/// Returns <see langword="true"/> when two element values are interchangeable,
	/// i.e. no transform produced a different value. Reference types compare by
	/// reference; value types by value (so a freshly boxed value still matches).
	/// </summary>
	static bool SameValue(T left, T right)
	{
		if (typeof(T).IsValueType)
			return EqualityComparer<T>.Default.Equals(left, right);

		// Reference types are interchangeable only when they are the same instance.
		return ReferenceEquals(left, right);
	}

	/// <summary>
	/// Sets the minimum array length.
	/// </summary>
	/// <param name="minLength">The minimum length</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodArray<T> Min(int minLength, string? message = null)
	{
		_minLength = minLength;
		_minMessage = message.OrNull();

		return this;
	}

	/// <summary>
	/// Sets the maximum array length.
	/// </summary>
	/// <param name="maxLength">The maximum length</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodArray<T> Max(int maxLength, string? message = null)
	{
		_maxLength = maxLength;
		_maxMessage = message.OrNull();

		return this;
	}

	/// <summary>
	/// Sets the exact array length.
	/// </summary>
	/// <param name="length">The exact length</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodArray<T> Length(int length, string? message = null)
	{
		_minLength = length;
		_maxLength = length;
		_minMessage = message.OrNull();
		_maxMessage = _minMessage;

		return this;
	}

	/// <summary>
	/// Requires the array to be non-empty (at least 1 element).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodArray<T> NonEmpty(string? message = null)
	{
		_minLength = 1;
		_minMessage = message.OrNull() ?? ZodMessages.ArrayNonEmpty;

		return this;
	}
}
