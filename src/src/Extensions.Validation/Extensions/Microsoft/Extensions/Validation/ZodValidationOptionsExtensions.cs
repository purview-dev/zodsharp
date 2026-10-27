using ZodSharp.Core;
using ZodSharp.Extensions.Validation;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace Microsoft.Extensions.Validation;

/// <summary>
/// ZodSharp extensions for <see cref="ValidationOptions"/>.
/// </summary>
public static class ZodValidationOptionsExtensions
{
	extension(ValidationOptions options)
	{
		/// <summary>
		/// Validates <paramref name="value"/> through the configured resolvers outside of HTTP or Blazor (worker
		/// services, console hosts, tests), returning ZodSharp's error shape.
		/// </summary>
		/// <typeparam name="T">The declared type; the runtime type of <paramref name="value"/> is used when it differs.</typeparam>
		/// <param name="value">The value to validate.</param>
		/// <param name="services">The services made available to validators (service-aware rules, localization).</param>
		/// <param name="cancellationToken">A token that cancels the validation.</param>
		/// <returns>
		/// The report. ZodSharp errors keep full fidelity; errors from other resolvers (DataAnnotations) are
		/// converted with code <c>custom</c> and their path parsed from the Microsoft.Extensions.Validation key.
		/// </returns>
		public async ValueTask<ZodValidationReport> ValidateAsync<T>(
			T value,
			IServiceProvider? services = null,
			CancellationToken cancellationToken = default
		)
		{
			ArgumentNullException.ThrowIfNull(options);

			var type = value?.GetType() ?? typeof(T);
			if (!options.TryGetValidatableTypeInfo(type, out var info))
				return new();

			ValidateContext context = new() { ValidationOptions = options, ServiceProvider = services };
			await info.ValidateAsync(value, context, cancellationToken).ConfigureAwait(false);

			var zod = context.ZodReport;
			var all = context.ValidationErrors;
			if (all is null || all.Count == 0)
				return zod ?? new();

			// Keep ZodSharp's own errors, and add anything another resolver reported at a key ZodSharp did not.
			var zodKeys = zod?.Keys;
			List<ZodValidationError> errors = [.. zod?.Errors ?? []];

			foreach (var (path, atPath) in all)
			{
				if (zodKeys?.Contains(path) == true)
					continue;

				foreach (var error in atPath)
				{
					errors.Add(
						ZodValidationError.Create(
							ZodIssueCategories.Custom,
							error.ErrorMessage,
							ZodPath.ParseDotted(path),
							category: ZodIssueCategories.Custom
						)
					);
				}
			}

			return new(errors);
		}
	}
}
