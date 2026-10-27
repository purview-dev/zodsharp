using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that lazily evaluates its inner schema.
/// Useful for recursive schemas and circular references.
/// Equivalent to Zod's lazy method.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
/// <remarks>
/// Initializes a new instance of the ZodLazy class.
/// </remarks>
/// <param name="schemaGetter">The function that gets the schema</param>
public class ZodLazy<T>(Func<IZodSchema<T, T>> schemaGetter) : ZodType<T>
{
	readonly Lock _lock = new();

	/// <summary>
	/// Gets the inner schema, evaluating it lazily if needed.
	/// </summary>
	public IZodSchema<T, T> Schema
	{
		get
		{
			lock (_lock)
				field ??= schemaGetter();

			return field;
		}
		private set;
	}

	/// <summary>
	/// Parses and validates the value using the lazy schema.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<T> ParseInternal(T value) => Schema.Validate(value);
}
