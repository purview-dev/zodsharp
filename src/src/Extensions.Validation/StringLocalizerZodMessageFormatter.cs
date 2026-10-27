using System.Globalization;
using Microsoft.Extensions.Localization;
using ZodSharp.Core;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Localizes ZodSharp messages through <see cref="IStringLocalizer"/>, with the same lookup shape
/// Microsoft.Extensions.Validation uses for DataAnnotations, so one resource file serves both.
/// </summary>
/// <remarks>
/// <para>Keys are tried in order; the first one found is the template:</para>
/// <list type="number">
/// <item><description>the error's <see cref="ZodValidationError.MessageFormat"/> (or, for a literal message,
/// <see cref="ZodValidationError.Message"/>) - localize by the English template;</description></item>
/// <item><description><c>{DeclaringType}_{Member}_{Code}</c>;</description></item>
/// <item><description><c>{DeclaringType}_{Code}</c>;</description></item>
/// <item><description><c>{Code}</c>;</description></item>
/// <item><description>otherwise the error's own <see cref="ZodValidationError.Message"/>.</description></item>
/// </list>
/// <para>
/// <c>{DeclaringType}</c> is the simple name of the type that owns the failing member (generic arity removed),
/// matching Microsoft.Extensions.Validation's key segment. A template is formatted with
/// <see cref="ZodMessageFormat"/>, so it can use named placeholders such as <c>{displayName}</c> or
/// <c>{minimum}</c> and never throws. <c>{displayName}</c> is itself localized from the key
/// <c>{DeclaringType}_{Member}_DisplayName</c> when present.
/// </para>
/// </remarks>
public sealed class StringLocalizerZodMessageFormatter : IZodMessageFormatter
{
	readonly IStringLocalizerFactory _factory;
	readonly Func<Type, IStringLocalizerFactory, IStringLocalizer> _localizerProvider;
	readonly Type? _sharedResourceType;

	/// <summary>
	/// Creates the formatter.
	/// </summary>
	/// <param name="factory">The localizer factory.</param>
	/// <param name="localizerProvider">
	/// Resolves the localizer for a declaring type; pass <c>ValidationOptions.LocalizerProvider</c> so ZodSharp
	/// and DataAnnotations share resources. Defaults to <c>factory.Create(type)</c>.
	/// </param>
	/// <param name="sharedResourceType">
	/// The resource type used when the declaring type is unknown (the <see cref="Format(in ZodValidationError, CultureInfo)"/>
	/// overload); when <see langword="null"/>, such errors keep their own message.
	/// </param>
	public StringLocalizerZodMessageFormatter(
		IStringLocalizerFactory factory,
		Func<Type, IStringLocalizerFactory, IStringLocalizer>? localizerProvider = null,
		Type? sharedResourceType = null
	)
	{
		ArgumentNullException.ThrowIfNull(factory);

		_factory = factory;
		_localizerProvider = localizerProvider ?? (static (type, f) => f.Create(type));
		_sharedResourceType = sharedResourceType;
	}

	/// <inheritdoc/>
	public string Format(in ZodValidationError error, CultureInfo culture) => Format(error, null, culture);

	/// <summary>
	/// Formats <paramref name="error"/> for <paramref name="culture"/>, looking keys up in the localizer of
	/// <paramref name="declaringType"/>.
	/// </summary>
	/// <param name="error">The error.</param>
	/// <param name="declaringType">The type that owns the failing member, when known.</param>
	/// <param name="culture">The culture.</param>
	/// <returns>The localized message, or the error's own message when no key matches.</returns>
	public string Format(in ZodValidationError error, Type? declaringType, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture);

		var resourceType = declaringType ?? _sharedResourceType;
		if (resourceType is null)
			return error.Message;

		var previous = CultureInfo.CurrentUICulture;
		var switchCulture = !Equals(previous, culture);
		if (switchCulture)
			CultureInfo.CurrentUICulture = culture;

		try
		{
			var localizer =
				_localizerProvider(resourceType, _factory)
				?? throw new InvalidOperationException(
					$"The localizer provider returned null for type '{resourceType.FullName}'."
				);

			var typeName = GetKeySegment(declaringType, error);
			var member = GetMember(error);
			var template = FindTemplate(localizer, error, typeName, member);
			if (template is null)
				return error.Message;

			var subject = error;
			if (typeName is not null && member is not null)
			{
				var displayName = localizer[$"{typeName}_{member}_DisplayName"];
				if (!displayName.ResourceNotFound)
					subject = WithDisplayName(error, displayName.Value);
			}

			return ZodMessageFormat.Format(template, subject, culture);
		}
		finally
		{
			if (switchCulture)
				CultureInfo.CurrentUICulture = previous;
		}
	}

	static string? FindTemplate(
		IStringLocalizer localizer,
		in ZodValidationError error,
		string? typeName,
		string? member
	)
	{
		var explicitKey = error.MessageFormat ?? error.Message;
		if (!string.IsNullOrEmpty(explicitKey) && Lookup(localizer, explicitKey) is { } explicitMatch)
			return explicitMatch;

		if (string.IsNullOrEmpty(error.Code))
			return null;

		if (typeName is not null)
		{
			if (member is not null && Lookup(localizer, $"{typeName}_{member}_{error.Code}") is { } memberMatch)
				return memberMatch;

			if (Lookup(localizer, $"{typeName}_{error.Code}") is { } typeMatch)
				return typeMatch;
		}

		return Lookup(localizer, error.Code);
	}

	static string? Lookup(IStringLocalizer localizer, string key)
	{
		var value = localizer[key];
		return value.ResourceNotFound ? null : value.Value;
	}

	static string? GetKeySegment(Type? declaringType, in ZodValidationError error)
	{
		string? name;
		if (declaringType is not null)
			name = (Nullable.GetUnderlyingType(declaringType) ?? declaringType).Name;
		else if (
			error.Parameters?.TryGetValue(ZodIssueParameters.DeclaringType, out var raw) == true
			&& raw is string full
		)
			name = full[(full.LastIndexOfAny(['.', '+']) + 1)..];
		else
			return null;

		var arity = name.IndexOf('`', StringComparison.Ordinal);
		return arity < 0 ? name : name[..arity];
	}

	static string? GetMember(in ZodValidationError error)
	{
		if (error.Parameters?.TryGetValue(ZodIssueParameters.Member, out var raw) == true && raw is string member)
			return member;

		var segments = error.FullPath.Segments;
		for (var i = segments.Length - 1; i >= 0; i--)
		{
			if (segments[i].Kind == ZodPathSegmentKind.Member)
				return segments[i].Name;
		}

		return null;
	}

	static ZodValidationError WithDisplayName(in ZodValidationError error, string displayName)
	{
		Dictionary<string, object?> parameters = new(StringComparer.Ordinal);
		if (error.Parameters is not null)
		{
			foreach (var pair in error.Parameters)
				parameters[pair.Key] = pair.Value;
		}

		parameters[ZodIssueParameters.DisplayName] = displayName;
		return error.WithParameters(parameters);
	}
}
