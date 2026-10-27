using System.Collections.Immutable;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Validates one member of a ZodSharp-validated type on its own, for Blazor field-level validation.
/// </summary>
/// <remarks>
/// Runs only the member's own rules (see <see cref="IZodMemberValidator{T}"/>): no nested-schema recursion, no
/// type-level rules and no refinements. Blazor attributes every error of a field-level run to the field that
/// changed, so recursing into a complex member would pin all of its nested errors onto that one field.
/// </remarks>
public sealed class ZodValidatablePropertyInfo : IValidatablePropertyInfo
{
	readonly ZodValidatableTypeInfo _owner;

	internal ZodValidatablePropertyInfo(ZodValidatableTypeInfo owner, string memberName)
	{
		_owner = owner;
		MemberName = memberName;
	}

	/// <summary>
	/// Gets the C# member name.
	/// </summary>
	public string MemberName { get; }

	/// <inheritdoc/>
	/// <exception cref="InvalidOperationException">The member has rules that only run asynchronously.</exception>
	public void Validate(object containingObject, ValidateContext context)
	{
		ArgumentNullException.ThrowIfNull(containingObject);
		ArgumentNullException.ThrowIfNull(context);

		var pending = _owner.Validator.ValidateMemberAsync(
			containingObject,
			MemberName,
			context.ServiceProvider,
			default
		);
		if (!pending.IsCompleted)
			throw new InvalidOperationException(
				$"'{_owner.ValidatedType.FullName}.{MemberName}' has asynchronous validation rules, so it cannot be "
					+ "validated synchronously. Use ValidateAsync."
			);

		var errors = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
		ZodErrorEmitter.Emit(_owner.Validator, containingObject, errors, context, _owner.Options);
	}

	/// <inheritdoc/>
	public Task ValidateAsync(object containingObject, ValidateContext context, CancellationToken cancellationToken)
	{
		ValueTask<ImmutableArray<ZodValidationError>> pending;
		try
		{
			ArgumentNullException.ThrowIfNull(containingObject);
			ArgumentNullException.ThrowIfNull(context);

			pending = _owner.Validator.ValidateMemberAsync(
				containingObject,
				MemberName,
				context.ServiceProvider,
				cancellationToken
			);
			if (pending.IsCompletedSuccessfully)
			{
				ZodErrorEmitter.Emit(_owner.Validator, containingObject, pending.Result, context, _owner.Options);
				return Task.CompletedTask;
			}
		}
		catch (Exception ex)
		{
			return Task.FromException(ex);
		}

		return AwaitAsync(pending, containingObject, context);
	}

	async Task AwaitAsync(
		ValueTask<ImmutableArray<ZodValidationError>> pending,
		object containingObject,
		ValidateContext context
	)
	{
		var errors = await pending.ConfigureAwait(false);
		ZodErrorEmitter.Emit(_owner.Validator, containingObject, errors, context, _owner.Options);
	}
}
