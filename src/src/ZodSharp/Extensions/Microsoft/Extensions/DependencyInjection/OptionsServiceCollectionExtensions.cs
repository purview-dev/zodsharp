using System.ComponentModel;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using ZodSharp.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="ZodSchemaOptionsValidator{T}"/> instances with
/// <see cref="IServiceCollection"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class OptionsServiceCollectionExtensions
{
	extension<TOptions>(OptionsBuilder<TOptions> builder)
		where TOptions : class
	{
		/// <summary>
		/// Registers a generic <see cref="IValidateOptions{T}"/> singleton that resolves the
		/// source-generated schema validator for <typeparamref name="TOptions"/> from the registered
		/// <see cref="IZodSchemaFactory"/>.
		/// </summary>
		/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="TOptions"/>.</param>
		/// <returns>The <see cref="OptionsBuilder{TOptions}"/> for chaining.</returns>
		/// <seealso cref="ServiceCollectionExtensions.AddZodSchemaOptionsValidator{T}(IServiceCollection, MissingValidatorBehavior)"/>
		public OptionsBuilder<TOptions> AddZodSchemaValidator(
			MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
		)
		{
			builder.Services.AddZodSchemaOptionsValidator<TOptions>(missingValidatorBehavior);

			return builder;
		}
	}
}
