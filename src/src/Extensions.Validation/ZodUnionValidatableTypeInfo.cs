using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Validation;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Validates a C# union that has no validator of its own by following its active case, the way
/// Microsoft.Extensions.Validation does: the case is reported under <c>Value</c> (<c>Value.Name</c>,
/// <c>Value[0].Name</c>) and validated by whichever resolver knows the case type.
/// </summary>
sealed class ZodUnionValidatableTypeInfo(Type unionType) : IValidatableTypeInfo
{
	const string ValueMember = "Value";

	public bool TryFindProperty(
		string propertyName,
		ValidationOptions validationOptions,
		[NotNullWhen(true)] out IValidatablePropertyInfo? validatablePropertyInfo
	)
	{
		validatablePropertyInfo = null;
		return false;
	}

	public void Validate(object? value, ValidateContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (value is not IUnion { Value: { } caseValue })
			return;

		ZodValidatableTypeInfo.ThrowIfTooDeep(context, unionType);
		var prefix = context.CurrentValidationPath;
		var casePath = string.IsNullOrEmpty(prefix) ? ValueMember : $"{prefix}.{ValueMember}";
		context.CurrentDepth++;
		try
		{
			if (context.ValidationOptions.TryGetValidatableTypeInfo(caseValue.GetType(), out var caseInfo))
			{
				context.CurrentValidationPath = casePath;
				caseInfo.Validate(caseValue, context);
			}
			else if (caseValue is IEnumerable elements and not string)
			{
				var index = 0;
				foreach (var element in elements)
				{
					if (
						element is not null
						&& context.ValidationOptions.TryGetValidatableTypeInfo(element.GetType(), out var elementInfo)
					)
					{
						context.CurrentValidationPath = ElementPath(casePath, index);
						elementInfo.Validate(element, context);
					}

					index++;
				}
			}
		}
		finally
		{
			context.CurrentDepth--;
			context.CurrentValidationPath = prefix;
		}
	}

	public async Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (value is not IUnion { Value: { } caseValue })
			return;

		ZodValidatableTypeInfo.ThrowIfTooDeep(context, unionType);
		var prefix = context.CurrentValidationPath;
		var casePath = string.IsNullOrEmpty(prefix) ? ValueMember : $"{prefix}.{ValueMember}";
		context.CurrentDepth++;
		try
		{
			if (context.ValidationOptions.TryGetValidatableTypeInfo(caseValue.GetType(), out var caseInfo))
			{
				context.CurrentValidationPath = casePath;
				await caseInfo.ValidateAsync(caseValue, context, cancellationToken).ConfigureAwait(false);
			}
			else if (caseValue is IEnumerable elements and not string)
			{
				var index = 0;
				foreach (var element in elements)
				{
					if (
						element is not null
						&& context.ValidationOptions.TryGetValidatableTypeInfo(element.GetType(), out var elementInfo)
					)
					{
						context.CurrentValidationPath = ElementPath(casePath, index);
						await elementInfo.ValidateAsync(element, context, cancellationToken).ConfigureAwait(false);
					}

					index++;
				}
			}
		}
		finally
		{
			context.CurrentDepth--;
			context.CurrentValidationPath = prefix;
		}
	}

	static string ElementPath(string casePath, int index) =>
		string.Create(CultureInfo.InvariantCulture, $"{casePath}[{index}]");
}
