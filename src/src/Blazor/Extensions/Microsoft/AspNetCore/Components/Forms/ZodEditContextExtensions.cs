using System.Linq.Expressions;
using ZodSharp.Blazor;
using ZodSharp.Core;

namespace Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// ZodSharp extensions for <see cref="EditContext"/>: the full-fidelity errors behind the messages, and server
/// validation problems mapped onto fields.
/// </summary>
public static class ZodEditContextExtensions
{
	extension(EditContext editContext)
	{
		/// <summary>
		/// Gets the full ZodSharp errors shown on <paramref name="field"/>: those <see cref="ZodValidator"/> placed and
		/// those mapped by <c>AddZodProblemDetails</c>. Empty when the form only uses
		/// <c>DataAnnotationsValidator</c>, whose store holds strings only.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <returns>The errors.</returns>
		public IReadOnlyList<ValidationError> GetZodErrors(in FieldIdentifier field)
		{
			ArgumentNullException.ThrowIfNull(editContext);
			return ZodFormErrors.Get(editContext) is { } errors
				? [.. errors.GetEntries(field).Select(static e => e.Error)]
				: [];
		}

		/// <summary>
		/// Gets the full ZodSharp errors shown on the field <paramref name="accessor"/> identifies.
		/// </summary>
		/// <typeparam name="TField">The field type.</typeparam>
		/// <param name="accessor">The field accessor, for example <c>() =&gt; order.Customer.Email</c>.</param>
		/// <returns>The errors.</returns>
		public IReadOnlyList<ValidationError> GetZodErrors<TField>(Expression<Func<TField>> accessor)
		{
			ArgumentNullException.ThrowIfNull(accessor);
			return editContext.GetZodErrors(FieldIdentifier.Create(accessor));
		}

		/// <summary>
		/// Gets every full ZodSharp error on the form.
		/// </summary>
		/// <returns>The errors.</returns>
		public IReadOnlyList<ValidationError> GetZodErrors()
		{
			ArgumentNullException.ThrowIfNull(editContext);
			return ZodFormErrors.Get(editContext) is { } errors
				? [.. errors.GetAllEntries().Select(static e => e.Entry.Error)]
				: [];
		}

		/// <summary>
		/// Maps a server validation problem (an API's ProblemDetails response) onto the form's fields. Issues are placed
		/// by their typed <c>segments</c>, so <c>Lines[0].Quantity</c> lands on <c>order.Lines[0].Quantity</c>; when the
		/// response has no <c>issues</c> (a plain Microsoft.Extensions.Validation or DataAnnotations response) the dotted
		/// <c>errors</c> keys are parsed instead. Member names are matched case-insensitively when no exact match
		/// exists, so camel-cased server paths place too.
		/// </summary>
		/// <param name="problem">The problem.</param>
		/// <param name="store">
		/// The store to add the messages to; <see langword="null"/> to use one the form keeps for server errors.
		/// </param>
		/// <param name="clearOnFieldChange">
		/// Whether a field's server errors are cleared when the field changes ("server error until edited"). Defaults to
		/// <see langword="true"/>.
		/// </param>
		/// <remarks>Previously mapped server errors are replaced.</remarks>
		public void AddZodProblemDetails(
			ZodValidationProblemDetails problem,
			ValidationMessageStore? store = null,
			bool clearOnFieldChange = true
		)
		{
			ArgumentNullException.ThrowIfNull(editContext);
			ArgumentNullException.ThrowIfNull(problem);

			var errors = ZodFormErrors.GetOrCreate(editContext);
			ClearServerErrors(errors);

			var target = store ?? (errors.ServerStore ??= new ValidationMessageStore(editContext));
			var validator = errors.Validator?.RootValidator;
			foreach (var error in problem.ToValidationErrors())
			{
				var field = ZodFieldLocator.Locate(editContext.Model, error.FullPath, validator, ignoreCase: true);
				var message = error.Message ?? string.Empty;
				target.Add(field, message);
				errors.AddServerError(field, error, message);
				errors.ServerStores[field] = target;
			}

			if (clearOnFieldChange && !errors.ClearsServerErrorsOnFieldChange)
			{
				errors.ClearsServerErrorsOnFieldChange = true;
				editContext.OnFieldChanged += static (sender, e) =>
				{
					if (sender is not EditContext context || ZodFormErrors.Get(context) is not { } state)
						return;

					var field = e.FieldIdentifier;
					if (state.ServerStores.Remove(field, out var fieldStore))
					{
						fieldStore.Clear(field);
						state.ClearServer(field);
						context.NotifyValidationStateChanged();
					}
				};
			}

			editContext.NotifyValidationStateChanged();
		}

		/// <summary>
		/// Clears every ZodSharp error on the form: the messages and errors <see cref="ZodValidator"/> placed and the
		/// server errors mapped by <c>AddZodProblemDetails</c>.
		/// </summary>
		public void ClearZodErrors()
		{
			ArgumentNullException.ThrowIfNull(editContext);

			if (ZodFormErrors.Get(editContext) is not { } errors)
				return;

			ClearServerErrors(errors);
			errors.Validator?.ClearOwnErrors();
			errors.ClearValidator();
			editContext.NotifyValidationStateChanged();
		}
	}

	static void ClearServerErrors(ZodFormErrors errors)
	{
		foreach (var (field, store) in errors.ServerStores)
			store.Clear(field);

		errors.ServerStore?.Clear();
		errors.ClearServer();
	}
}
