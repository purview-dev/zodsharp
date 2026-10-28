using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using MevValidationError = Microsoft.Extensions.Validation.ValidationError;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Converts ZodSharp errors into Microsoft.Extensions.Validation errors on a <see cref="ValidateContext"/>, and
/// records the full-fidelity errors on the side channels.
/// </summary>
static class ZodErrorEmitter
{
	/// <summary>
	/// Reports <paramref name="errors"/> against <paramref name="context"/>.
	/// </summary>
	/// <param name="validator">The validator that produced the errors (used for member resolution).</param>
	/// <param name="value">The validated value; error paths are relative to it.</param>
	/// <param name="errors">The errors.</param>
	/// <param name="context">The context.</param>
	/// <param name="options">The bridge options.</param>
	/// <param name="valueKey">
	/// When set (a parameter name), errors about the value itself are keyed by it with no container, which is
	/// Microsoft.Extensions.Validation's parameter convention.
	/// </param>
	/// <param name="prefixWithKey">
	/// Whether errors below the value are also prefixed with <paramref name="valueKey"/> (a collection
	/// parameter validated as a whole: <c>items[0].Name</c>).
	/// </param>
	public static void Emit(
		IZodUntypedValidator validator,
		object? value,
		ImmutableArray<ZodValidationError> errors,
		ValidateContext context,
		ZodValidationOptions options,
		string? valueKey = null,
		bool prefixWithKey = false
	)
	{
		if (errors.IsDefaultOrEmpty)
			return;

		var prefixText = context.CurrentValidationPath ?? string.Empty;
		var prefix = ZodPath.ParseDotted(prefixText);
		var namer = options.PathNamer;
		var formatter = ResolveFormatter(context, options);
		var culture = CultureInfo.CurrentUICulture;
		var dictionaryBacked = ZodMemberLocator.IsDictionaryBacked(value);

		var canonical = ImmutableArray.CreateBuilder<ZodValidationError>(errors.Length);
		var keys = new string[errors.Length];
		foreach (var error in errors)
		{
			var repathed = error;
			var relative = error.FullPath;
			object? container;
			string name;

			if (relative.IsRoot && valueKey is not null)
			{
				repathed = error.WithPathPrefix(ZodPathSegment.Member(valueKey));
				container = null;
				name = valueKey;
			}
			else if (relative.IsRoot)
			{
				container = value;
				name = string.Empty;
			}
			else if (value is null || dictionaryBacked)
			{
				container = null;
				name = string.Empty;
			}
			else
			{
				ZodMemberLocator.Locate(validator, value, relative, out container, out name);
			}

			if (prefixWithKey && valueKey is not null && !relative.IsRoot)
				repathed = repathed.WithPathPrefix(ZodPathSegment.Member(valueKey));

			repathed = repathed.WithPathPrefix(prefix);
			var path = repathed.FullPath.ToDottedString(namer);
			if (container is null && name.Length == 0 && !repathed.FullPath.IsRoot)
				name = path; // No owning object (dictionary-backed): Blazor binds the message to the model.

			keys[canonical.Count] = path;
			canonical.Add(repathed);

			context.AddValidationError(
				new MevValidationError
				{
					Name = name,
					Path = path,
					ErrorMessage = FormatMessage(
						formatter,
						repathed,
						container?.GetType() ?? validator.ValidatedType,
						culture
					),
					Container = container,
				}
			);
		}

		var reported = canonical.MoveToImmutable();
		ZodValidateContextExtensions.GetOrCreateZodReport(context).Add(reported, keys);

		if (context.ServiceProvider?.GetService(typeof(IZodValidationReportSink)) is IZodValidationReportSink sink)
			sink.Add(validator.ValidatedType, prefixText, reported);
	}

	static IZodMessageFormatter? ResolveFormatter(ValidateContext context, ZodValidationOptions options)
	{
		return options.MessageFormatter
			?? (
				context.ServiceProvider?.GetService(typeof(IStringLocalizerFactory)) is IStringLocalizerFactory factory
					? new StringLocalizerZodMessageFormatter(factory, context.ValidationOptions.LocalizerProvider)
					: null
			);
	}

	static string FormatMessage(
		IZodMessageFormatter? formatter,
		in ZodValidationError error,
		Type declaringType,
		CultureInfo culture
	) =>
		formatter switch
		{
			null => error.Message,
			StringLocalizerZodMessageFormatter localizer => localizer.Format(error, declaringType, culture),
			_ => formatter.Format(error, culture),
		};
}
