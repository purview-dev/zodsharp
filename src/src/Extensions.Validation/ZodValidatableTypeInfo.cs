using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Adapts a ZodSharp validator to <see cref="IValidatableTypeInfo"/>, so Microsoft.Extensions.Validation hosts
/// (minimal APIs, Blazor) validate the type with ZodSharp.
/// </summary>
/// <remarks>
/// <para>
/// ZodSharp validates the whole object graph itself, so this adapter does not recurse through
/// Microsoft.Extensions.Validation for nested members. Every ZodSharp error becomes one
/// <c>Microsoft.Extensions.Validation.ValidationError</c>: <c>Path</c> is the error path prefixed with
/// <see cref="ValidateContext.CurrentValidationPath"/>, and <c>Container</c>/<c>Name</c> are the object that
/// owns the failing member and the member's name, which is what Blazor needs to place the message on a field.
/// </para>
/// <para>
/// Like Microsoft.Extensions.Validation's own type info, it throws <see cref="InvalidOperationException"/> when
/// <see cref="ValidateContext.CurrentDepth"/> has reached <see cref="ValidationOptions.MaxDepth"/>.
/// </para>
/// </remarks>
public sealed class ZodValidatableTypeInfo : IValidatableTypeInfo
{
	readonly ZodValidationOptions _options;
	readonly ConcurrentDictionary<string, ZodValidatablePropertyInfo?> _properties = new(StringComparer.Ordinal);

	/// <summary>
	/// Creates the adapter.
	/// </summary>
	/// <param name="validator">The validator.</param>
	/// <param name="options">The bridge options; defaults when <see langword="null"/>.</param>
	public ZodValidatableTypeInfo(IZodUntypedValidator validator, ZodValidationOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(validator);

		Validator = validator;
		_options = options ?? new();
	}

	/// <summary>
	/// Gets the wrapped validator.
	/// </summary>
	public IZodUntypedValidator Validator { get; }

	/// <summary>
	/// Gets the validated type.
	/// </summary>
	public Type ValidatedType => Validator.ValidatedType;

	internal ZodValidationOptions Options => _options;

	/// <inheritdoc/>
	/// <remarks>
	/// Returns an adapter only for members the validator can validate on their own (generated validators mark
	/// them; see <see cref="IZodMemberValidator{T}"/>). Field-level validation runs that member's own rules -
	/// never nested-schema recursion or type-level rules, because Blazor attributes every error of a field-level
	/// run to the changed field.
	/// </remarks>
	public bool TryFindProperty(
		string propertyName,
		ValidationOptions validationOptions,
		[NotNullWhen(true)] out IValidatablePropertyInfo? validatablePropertyInfo
	)
	{
		ArgumentNullException.ThrowIfNull(propertyName);

		var property = _properties.GetOrAdd(
			propertyName,
			static (name, self) => self.Validator.IsValidatableMember(name) ? new(self, name) : null,
			this
		);

		validatablePropertyInfo = property;
		return property is not null;
	}

	/// <inheritdoc/>
	/// <exception cref="InvalidOperationException">
	/// The validator has rules that only run asynchronously, so a synchronous run cannot give a complete answer.
	/// Use <see cref="ValidateAsync"/>.
	/// </exception>
	public void Validate(object? value, ValidateContext context) => ValidateCore(value, context, valueKey: null);

	/// <inheritdoc/>
	public Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken) =>
		ValidateCoreAsync(value, context, valueKey: null, cancellationToken);

	internal void ValidateCore(object? value, ValidateContext context, string? valueKey, bool prefixWithKey = false)
	{
		ArgumentNullException.ThrowIfNull(context);

		ThrowIfTooDeep(context, ValidatedType);
		if (Validator.RequiresAsyncValidation)
			throw new InvalidOperationException(
				$"'{ValidatedType.FullName}' has asynchronous validation rules, so it cannot be validated "
					+ "synchronously. Use ValidateAsync (EditContext.ValidateAsync in Blazor)."
			);

		var errors = value is null ? NullValue() : Validator.Validate(value);
		ZodErrorEmitter.Emit(Validator, value, errors, context, _options, valueKey, prefixWithKey);
	}

	internal Task ValidateCoreAsync(
		object? value,
		ValidateContext context,
		string? valueKey,
		CancellationToken cancellationToken,
		bool prefixWithKey = false
	)
	{
		ValueTask<ImmutableArray<ZodValidationError>> pending;
		try
		{
			ArgumentNullException.ThrowIfNull(context);
			ThrowIfTooDeep(context, ValidatedType);

			if (value is null)
			{
				ZodErrorEmitter.Emit(Validator, value, NullValue(), context, _options, valueKey, prefixWithKey);
				return Task.CompletedTask;
			}

			pending = Validator.ValidateAsync(value, context.ServiceProvider, cancellationToken);
			if (pending.IsCompletedSuccessfully)
			{
				ZodErrorEmitter.Emit(Validator, value, pending.Result, context, _options, valueKey, prefixWithKey);
				return Task.CompletedTask;
			}
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}

		return AwaitAsync(pending, value, context, valueKey, prefixWithKey);
	}

	async Task AwaitAsync(
		ValueTask<ImmutableArray<ZodValidationError>> pending,
		object? value,
		ValidateContext context,
		string? valueKey,
		bool prefixWithKey
	)
	{
		var errors = await pending.ConfigureAwait(false);
		ZodErrorEmitter.Emit(Validator, value, errors, context, _options, valueKey, prefixWithKey);
	}

	// A null value has nothing to validate members on: report it rather than hand null to a validator that may
	// not expect it. Optional and nullable parameters never get here (see ZodValidatableParameterInfo.AllowsNull).
	ImmutableArray<ZodValidationError> NullValue() => [ZodIssues.NullValue(ZodTypeNames.Of(ValidatedType))];

	internal static void ThrowIfTooDeep(ValidateContext context, Type type)
	{
		// Same check and message as Microsoft.Extensions.Validation's generated type info.
		if (context.CurrentDepth >= context.ValidationOptions.MaxDepth)
			throw new InvalidOperationException(
				$"Maximum validation depth of {context.ValidationOptions.MaxDepth} exceeded at "
					+ $"'{context.CurrentValidationPath}' in '{type.Name}'. "
					+ "This is likely caused by a circular reference in the object graph. "
					+ "Consider increasing the MaxDepth in ValidationOptions if deeper validation is required."
			);
	}
}
