using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Validation;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// <see cref="ZodValidationOptions.AlsoRunDataAnnotations"/>: validates with ZodSharp, then with the next
/// resolver that knows the type, reporting identical <c>(Path, ErrorMessage)</c> pairs once.
/// </summary>
/// <remarks>
/// ZodSharp validates nested ZodSharp types itself, so the second pass runs on a marked clone of the context in
/// which nested ZodSharp types only run their second pass too. That keeps the nested ZodSharp errors from being
/// produced twice.
/// </remarks>
sealed class ZodCompositeValidatableTypeInfo(ZodValidatableTypeInfo zod, ZodValidatableInfoResolver owner)
	: IValidatableTypeInfo
{
	static readonly ConditionalWeakTable<ValidateContext, object> SecondPassContexts = new();
	static readonly object Marker = new();

	public ZodValidatableTypeInfo Zod => zod;

	public bool TryFindProperty(
		string propertyName,
		ValidationOptions validationOptions,
		[NotNullWhen(true)] out IValidatablePropertyInfo? validatablePropertyInfo
	) => zod.TryFindProperty(propertyName, validationOptions, out validatablePropertyInfo);

	public void Validate(object? value, ValidateContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var next = owner.FindNextTypeInfo(zod.ValidatedType, context.ValidationOptions);
		if (SecondPassContexts.TryGetValue(context, out _))
		{
			next?.Validate(value, context);
			return;
		}

		zod.Validate(value, context);
		if (next is null)
			return;

		var second = CreateSecondPass(context);
		next.Validate(value, second);
		Merge(second, context);
	}

	public async Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var next = owner.FindNextTypeInfo(zod.ValidatedType, context.ValidationOptions);
		if (SecondPassContexts.TryGetValue(context, out _))
		{
			if (next is not null)
				await next.ValidateAsync(value, context, cancellationToken).ConfigureAwait(false);

			return;
		}

		await zod.ValidateAsync(value, context, cancellationToken).ConfigureAwait(false);
		if (next is null)
			return;

		var second = CreateSecondPass(context);
		await next.ValidateAsync(value, second, cancellationToken).ConfigureAwait(false);
		Merge(second, context);
	}

	static ValidateContext CreateSecondPass(ValidateContext context)
	{
		ValidateContext second = new()
		{
			ValidationOptions = context.ValidationOptions,
			ServiceProvider = context.ServiceProvider,
			CurrentDepth = context.CurrentDepth,
			CurrentValidationPath = context.CurrentValidationPath,
		};
		SecondPassContexts.AddOrUpdate(second, Marker);
		return second;
	}

	static void Merge(ValidateContext second, ValidateContext context)
	{
		var added = second.ValidationErrors;
		if (added is null)
			return;

		var existing = context.ValidationErrors;
		foreach (var (path, errors) in added)
		{
			foreach (var error in errors)
			{
				if (!Contains(existing, path, error.ErrorMessage))
					context.AddValidationError(error);
			}
		}
	}

	static bool Contains(
		IReadOnlyDictionary<string, IReadOnlyList<Microsoft.Extensions.Validation.ValidationError>>? errors,
		string path,
		string message
	)
	{
		if (errors is null || !errors.TryGetValue(path, out var atPath))
			return false;

		foreach (var error in atPath)
		{
			if (string.Equals(error.ErrorMessage, message, StringComparison.Ordinal))
				return true;
		}

		return false;
	}
}
