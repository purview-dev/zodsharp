using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation.Fixtures;

static class TestHelpers
{
	public static ZodValidationError Error(string code, string message, params ZodPathSegment[] path) =>
		ZodValidationError.Create(code, message, ZodPath.From(path));

	public static ZodPathSegment M(string name) => ZodPathSegment.Member(name);

	public static ZodPathSegment I(int index) => ZodPathSegment.At(index);

	public static ZodPathSegment K<TKey>(TKey key) => ZodPathSegment.Key(key);

	public static ServiceProvider CreateProvider(
		Action<IZodSchemaFactory> register,
		Action<IServiceCollection>? configure = null,
		Action<ZodValidationOptions>? options = null
	)
	{
		ServiceCollection services = new();
		services.AddZodSharpFactory(register);
		services.AddZodSharpValidation(options);
		configure?.Invoke(services);
		return services.BuildServiceProvider();
	}

	public static ValidationOptions GetValidationOptions(this IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<ValidationOptions>>().Value;

	public static ValidateContext CreateContext(this IServiceProvider provider) =>
		new() { ValidationOptions = provider.GetValidationOptions(), ServiceProvider = provider };

	public static IValidatableTypeInfo GetTypeInfo<T>(this IServiceProvider provider)
	{
		// A conditional expression would turn the guard into a throw expression inside the return.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (!provider.GetValidationOptions().TryGetValidatableTypeInfo(typeof(T), out var info))
			throw new InvalidOperationException($"No type info for {typeof(T).Name}.");
#pragma warning restore IDE0046

		return info;
	}

	public static Microsoft.Extensions.Validation.ValidationError Single(this ValidateContext context)
	{
		var errors = context.ValidationErrors ?? throw new InvalidOperationException("No errors.");
		return errors.SelectMany(static pair => pair.Value).Single();
	}

	public static ImmutableArray<Microsoft.Extensions.Validation.ValidationError> All(this ValidateContext context) =>
		[
			.. (
				context.ValidationErrors
				?? ImmutableDictionary<string, IReadOnlyList<Microsoft.Extensions.Validation.ValidationError>>.Empty
			).SelectMany(static pair => pair.Value),
		];
}

sealed class RecordingSink : IZodValidationReportSink
{
	public List<(Type Type, string Prefix, ImmutableArray<ZodValidationError> Errors)> Calls { get; } = [];

	public void Add(Type validatedType, string pathPrefix, ImmutableArray<ZodValidationError> errors) =>
		Calls.Add((validatedType, pathPrefix, errors));
}

sealed class FakeStringLocalizerFactory(Dictionary<string, string> resources) : IStringLocalizerFactory
{
	public List<Type> RequestedTypes { get; } = [];

	public IStringLocalizer Create(Type resourceSource)
	{
		RequestedTypes.Add(resourceSource);
		return new FakeStringLocalizer(resources);
	}

	public IStringLocalizer Create(string baseName, string location) => new FakeStringLocalizer(resources);
}

sealed class FakeStringLocalizer(Dictionary<string, string> resources) : IStringLocalizer
{
	public LocalizedString this[string name]
	{
		get
		{
			// Culture-specific entries are keyed "{culture}:{key}" so tests can prove the culture is honoured.
			var culture = CultureInfo.CurrentUICulture.Name;
			// A conditional expression would nest the culture-specific lookup inside the fallback lookup.
#pragma warning disable IDE0046 // Convert to conditional expression
			if (resources.TryGetValue($"{culture}:{name}", out var specific))
				return new(name, specific, resourceNotFound: false);
#pragma warning restore IDE0046

			return resources.TryGetValue(name, out var value)
				? new(name, value, resourceNotFound: false)
				: new(name, name, resourceNotFound: true);
		}
	}

	public LocalizedString this[string name, params object[] arguments] => this[name];

	public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
		resources.Select(static pair => new LocalizedString(pair.Key, pair.Value));
}

sealed class FixedFormatter(string prefix) : IZodMessageFormatter
{
	public string Format(in ZodValidationError error, CultureInfo culture) => prefix + error.Message;
}
