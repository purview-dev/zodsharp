using System.Collections;
using Microsoft.Extensions.Validation;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Validates a ZodSharp-validated method parameter (a minimal API argument), following
/// Microsoft.Extensions.Validation's parameter conventions exactly.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>A complex parameter is not prefixed: errors are keyed <c>Name</c>,
/// <c>Address.Street</c>.</description></item>
/// <item><description>A collection parameter prefixes each element with <c>{parameter}[i]</c>:
/// <c>items[0].Name</c>.</description></item>
/// <item><description>An error about the value itself (a missing body, a root refinement) is keyed by the
/// parameter name with no container.</description></item>
/// </list>
/// </remarks>
public sealed class ZodValidatableParameterInfo : IValidatableParameterInfo
{
	readonly ZodValidatableTypeInfo? _typeInfo;
	readonly IValidatableTypeInfo? _delegateInfo;

	internal ZodValidatableParameterInfo(
		string name,
		Type parameterType,
		bool allowsNull,
		bool isCollection,
		ZodValidatableTypeInfo? typeInfo,
		IValidatableTypeInfo? delegateInfo = null
	)
	{
		_delegateInfo = delegateInfo;
		Name = name;
		ParameterType = parameterType;
		AllowsNull = allowsNull;
		IsCollection = isCollection;
		_typeInfo = typeInfo;
	}

	/// <summary>
	/// Gets the parameter name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the declared parameter type.
	/// </summary>
	public Type ParameterType { get; }

	/// <summary>
	/// Gets whether <see langword="null"/> is an acceptable argument (an optional or nullable parameter), in
	/// which case a <see langword="null"/> argument is not validated.
	/// </summary>
	public bool AllowsNull { get; }

	/// <summary>
	/// Gets whether the parameter is a collection whose elements are validated individually.
	/// </summary>
	public bool IsCollection { get; }

	/// <inheritdoc/>
	public void Validate(object? value, ValidateContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (value is null && AllowsNull)
			return;

		if (_delegateInfo is not null)
		{
			_delegateInfo.Validate(value, context);
			return;
		}

		if (_typeInfo is not null || value is not IEnumerable elements)
		{
			_typeInfo?.ValidateCore(value, context, Name, prefixWithKey: IsCollection);
			return;
		}

		var prefix = context.CurrentValidationPath;
		var index = 0;
		foreach (var element in elements)
		{
			if (
				element is not null
				&& context.ValidationOptions.TryGetValidatableTypeInfo(element.GetType(), out var info)
			)
			{
				context.CurrentValidationPath = ElementPath(prefix, index);
				try
				{
					info.Validate(element, context);
				}
				finally
				{
					context.CurrentValidationPath = prefix;
				}
			}

			index++;
		}
	}

	/// <inheritdoc/>
	public async Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (value is null && AllowsNull)
			return;

		if (_delegateInfo is not null)
		{
			await _delegateInfo.ValidateAsync(value, context, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (_typeInfo is not null || value is not IEnumerable elements)
		{
			if (_typeInfo is not null)
				await _typeInfo
					.ValidateCoreAsync(value, context, Name, cancellationToken, prefixWithKey: IsCollection)
					.ConfigureAwait(false);

			return;
		}

		var prefix = context.CurrentValidationPath;
		var index = 0;
		foreach (var element in elements)
		{
			if (
				element is not null
				&& context.ValidationOptions.TryGetValidatableTypeInfo(element.GetType(), out var info)
			)
			{
				context.CurrentValidationPath = ElementPath(prefix, index);
				try
				{
					await info.ValidateAsync(element, context, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					context.CurrentValidationPath = prefix;
				}
			}

			index++;
		}
	}

	string ElementPath(string prefix, int index) =>
		string.IsNullOrEmpty(prefix)
			? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Name}[{index}]")
			: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}.{Name}[{index}]");
}
