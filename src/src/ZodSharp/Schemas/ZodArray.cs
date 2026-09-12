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
public class ZodArray<T>(IZodSchema<T, T> elementSchema) : ZodType<T[], T[]>
{
	static readonly string[] EmptyPath = [];

	int? _minLength;
	int? _maxLength;

	string? _errorMessage;

	/// <summary>
	/// Parses and validates an array value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<T[]> ParseInternal(T[] value)
	{
		if (value == null)
		{
			return ValidationResult<T[]>.Failure(
				new ValidationError("invalid_type", "Expected array, but got null", EmptyPath)
			);
		}

		List<ValidationError> errors = [with(value.Length)];
		List<T> validatedItems = [with(value.Length)];

		for (var i = 0; i < value.Length; i++)
		{
			var itemResult = elementSchema.Validate(value[i]);
			if (!itemResult.IsSuccess)
			{
				foreach (var error in itemResult.Errors)
				{
					var path = new string[error.Path.Length + 1];
					error.Path.CopyTo(path, error.Path.Length);
					path[error.Path.Length] = $"[{i}]";
					errors.Add(new(error.Code, error.Message, path, error.Parameters));
				}
			}
			else
				validatedItems.Add(itemResult.Value);
		}

		if (errors.Count > 0)
			return ValidationResult<T[]>.Failure(errors);

		// When no errors...
		return _minLength.HasValue && validatedItems.Count < _minLength.Value
				? ValidationResult<T[]>.Failure(
					new ValidationError(
						"too_small",
						_errorMessage
							?? $"Array must have at least {_minLength.Value} elements, but got {validatedItems.Count}",
						EmptyPath
					)
				)
			: _maxLength.HasValue && validatedItems.Count > _maxLength.Value
				? ValidationResult<T[]>.Failure(
					new ValidationError(
						"too_big",
						_errorMessage
							?? $"Array must have at most {_maxLength.Value} elements, but got {validatedItems.Count}",
						EmptyPath
					)
				)
			: ValidationResult<T[]>.Success([.. validatedItems]);
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
		_errorMessage = message.Or($"Array must have at least {minLength} elements");

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
		_errorMessage = message.Or($"Array must have at most {maxLength} elements");

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
		_errorMessage = message.Or($"Array must have exactly {length} elements");

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
		_errorMessage = message.Or("Array must not be empty");

		return this;
	}
}
