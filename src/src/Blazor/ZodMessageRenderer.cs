using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;
using ValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Blazor;

/// <summary>
/// Renders the message shown for a ZodSharp error, through the same formatter chain the Microsoft.Extensions.Validation
/// bridge uses, so <c>DataAnnotationsValidator</c> and <see cref="ZodValidator"/> show identical text.
/// </summary>
sealed class ZodMessageRenderer
{
	readonly IZodMessageFormatter? _formatter;

	ZodMessageRenderer(IZodMessageFormatter? formatter) => _formatter = formatter;

	public static ZodMessageRenderer Create(IServiceProvider services, ZodBlazorOptions options)
	{
		if (options.MessageFormatter is { } configured)
			return new(configured);

		if (services.GetService<IOptions<ZodValidationOptions>>()?.Value.MessageFormatter is { } bridge)
			return new(bridge);

		if (services.GetService<IStringLocalizerFactory>() is { } factory)
		{
			var localizerProvider = services.GetService<IOptions<ValidationOptions>>()?.Value.LocalizerProvider;
			return new(new StringLocalizerZodMessageFormatter(factory, localizerProvider));
		}

		return new(null);
	}

	public string Render(in ValidationError error, Type? declaringType)
	{
		var culture = CultureInfo.CurrentUICulture;
		try
		{
			return _formatter switch
			{
				null => error.Message ?? string.Empty,
				StringLocalizerZodMessageFormatter localizer => localizer.Format(error, declaringType, culture),
				_ => _formatter.Format(error, culture),
			};
		}
		catch (Exception)
		{
			// A formatter must never turn a validation failure into an exception.
			return error.Message ?? string.Empty;
		}
	}
}
