using Microsoft.AspNetCore.Components.Forms;
using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// One full-fidelity error placed on a field, with the message that was shown for it, so the message components can
/// pair a rendered message with its error.
/// </summary>
readonly record struct ZodFieldError(ValidationError Error, string Message);

/// <summary>
/// The full-fidelity errors of one <see cref="EditContext"/>, held in
/// <c>EditContext.Properties[ZodValidator.ErrorsKey]</c>: the errors <see cref="ZodValidator"/> placed, and the
/// server errors mapped by <c>AddZodProblemDetails</c>.
/// </summary>
/// <remarks>
/// Only touched on the renderer's synchronization context (inside the validator delegates the platform invokes,
/// after marshalling through <c>InvokeAsync</c>), like <see cref="ValidationMessageStore"/> itself.
/// </remarks>
sealed class ZodFormErrors
{
	readonly Dictionary<FieldIdentifier, List<ZodFieldError>> _validator = [];
	readonly Dictionary<FieldIdentifier, List<ZodFieldError>> _server = [];

	/// <summary>
	/// Gets the validator component attached to the form, if any; used to resolve server error paths with the same
	/// validator that validates the form.
	/// </summary>
	public ZodValidator? Validator { get; set; }

	/// <summary>
	/// Gets or sets the store server errors were added to when the caller supplied none.
	/// </summary>
	public ValidationMessageStore? ServerStore { get; set; }

	/// <summary>
	/// Gets the store server errors were added to, per field, so clear-on-change clears the right one.
	/// </summary>
	public Dictionary<FieldIdentifier, ValidationMessageStore> ServerStores { get; } = [];

	/// <summary>
	/// Gets or sets whether the clear-on-field-change handler is attached.
	/// </summary>
	public bool ClearsServerErrorsOnFieldChange { get; set; }

	public static ZodFormErrors GetOrCreate(EditContext editContext)
	{
		if (
			editContext.Properties.TryGetValue(ZodValidator.ErrorsKey, out var existing)
			&& existing is ZodFormErrors errors
		)
			return errors;

		ZodFormErrors created = new();
		editContext.Properties[ZodValidator.ErrorsKey] = created;
		return created;
	}

	public static ZodFormErrors? Get(EditContext editContext) =>
		editContext.Properties.TryGetValue(ZodValidator.ErrorsKey, out var existing) ? existing as ZodFormErrors : null;

	public IEnumerable<FieldIdentifier> ValidatorFields => _validator.Keys;

	public void AddValidatorError(in FieldIdentifier field, in ValidationError error, string message) =>
		Add(_validator, field, new(error, message));

	public void ClearValidator(in FieldIdentifier field) => _validator.Remove(field);

	public void ClearValidator() => _validator.Clear();

	public void AddServerError(in FieldIdentifier field, in ValidationError error, string message) =>
		Add(_server, field, new(error, message));

	public bool ClearServer(in FieldIdentifier field) => _server.Remove(field);

	public void ClearServer()
	{
		_server.Clear();
		ServerStores.Clear();
	}

	public List<ZodFieldError> GetEntries(in FieldIdentifier field)
	{
		List<ZodFieldError> entries = [];
		if (_validator.TryGetValue(field, out var validator))
			entries.AddRange(validator);

		if (_server.TryGetValue(field, out var server))
			entries.AddRange(server);

		return entries;
	}

	public List<(FieldIdentifier Field, ZodFieldError Entry)> GetAllEntries()
	{
		List<(FieldIdentifier, ZodFieldError)> entries = [];
		foreach (var (field, errors) in _validator)
		{
			foreach (var error in errors)
				entries.Add((field, error));
		}

		foreach (var (field, errors) in _server)
		{
			foreach (var error in errors)
				entries.Add((field, error));
		}

		return entries;
	}

	static void Add(
		Dictionary<FieldIdentifier, List<ZodFieldError>> target,
		in FieldIdentifier field,
		ZodFieldError entry
	)
	{
		if (!target.TryGetValue(field, out var list))
		{
			list = [];
			target[field] = list;
		}

		list.Add(entry);
	}
}
