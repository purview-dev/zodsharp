using ZodSharp.Core;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Supplies ZodSharp validators by validated type without reflection. Sources registered in
/// <see cref="ZodValidationOptions.Sources"/> are consulted before <see cref="IZodSchemaFactory"/>.
/// </summary>
public interface IZodValidatableInfoSource
{
	/// <summary>
	/// Gets the validator for <paramref name="type"/>.
	/// </summary>
	/// <param name="type">The validated type.</param>
	/// <param name="validator">The validator, when this source knows the type.</param>
	/// <returns><see langword="true"/> when this source knows the type.</returns>
	bool TryGetValidator(Type type, out IZodUntypedValidator? validator);
}
