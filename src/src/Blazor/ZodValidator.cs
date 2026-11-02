using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.Blazor;

/// <summary>
/// Validates an <see cref="EditForm"/> model with ZodSharp and keeps every error at full fidelity - code, category,
/// origin, bounds and parameters - in addition to the message shown on the field.
/// </summary>
/// <remarks>
/// <para>
/// Use it in place of <c>&lt;DataAnnotationsValidator /&gt;</c> when the UI needs more than the message string, when
/// the model is validated by a runtime schema (<see cref="Schema"/>), or for debounced, per-keystroke validation.
/// The model's validator comes from <see cref="Schema"/>, or from the ZodSharp validators registered with
/// <c>AddZodSharpBlazor</c>.
/// </para>
/// <para>
/// Form-level validation runs through <c>ValidationRequestedEventArgs.AddAsyncValidator</c> and field-level validation
/// through <c>EditContext.RegisterAsyncFieldValidator</c>, so the platform's pending and faulted states,
/// cancellation and supersession all apply. A validator that throws is logged and marks the field (or form) faulted.
/// </para>
/// <para>
/// Read the full errors with <c>EditContext.GetZodErrors(...)</c>, or render them with
/// <see cref="ZodValidationMessage{TValue}"/> and <see cref="ZodValidationSummary"/>.
/// </para>
/// </remarks>
public sealed partial class ZodValidator : ComponentBase, IDisposable
{
	/// <summary>
	/// The <c>EditContext.Properties</c> key under which the full-fidelity errors of the form are kept. Read them with
	/// <c>EditContext.GetZodErrors(...)</c>.
	/// </summary>
	public static readonly object ErrorsKey = new();

	/// <summary>
	/// The value <see cref="ZodValidator"/> writes under the <c>DataAnnotationsValidator</c> key to enable static-SSR
	/// client validation, distinguishable from the <see langword="true"/> <c>DataAnnotationsValidator</c> writes.
	/// </summary>
	static readonly object ClientValidationMarker = new();

	EditContext? _editContext;
	ValidationMessageStore? _store;
	ZodFormErrors? _errors;
	ZodValidatableInfoResolver? _resolver;
	ZodBlazorOptions _options = new();
	ZodMessageRenderer? _renderer;
	TimeProvider _timeProvider = TimeProvider.System;
	ILogger _logger = NullLogger.Instance;
	bool _wroteClientValidationKey;
	bool _warnedCoexistence;

	[CascadingParameter]
	EditContext? CurrentEditContext { get; set; }

	[Inject]
	IServiceProvider Services { get; set; } = default!;

	/// <summary>
	/// Gets or sets an explicit validator for the form model, for models without a registered validator - for example
	/// a runtime <c>Z.Object(...)</c> schema over a CLR type (see <see cref="ZodModelSchema"/>). It also validates
	/// nested objects of its own type. When <see langword="null"/>, the registered validator of the model type is used.
	/// </summary>
	[Parameter]
	public IZodUntypedValidator? Schema { get; set; }

	/// <summary>
	/// Gets or sets the field-level triggers. Defaults to <see cref="ZodValidationTrigger.Change"/>. Form-level
	/// validation on submit always runs.
	/// </summary>
	[Parameter]
	public ZodValidationTrigger ValidateOn { get; set; } = ZodValidationTrigger.Change;

	/// <summary>
	/// Gets or sets the delay, in milliseconds, before an <see cref="ZodValidationTrigger.Input"/>-triggered field
	/// validation runs. Each change supersedes (and cancels) the pending one, so a burst of keystrokes validates once.
	/// Ignored unless <see cref="ValidateOn"/> includes <see cref="ZodValidationTrigger.Input"/>.
	/// </summary>
	[Parameter]
	public int DebounceMilliseconds { get; set; }

	/// <summary>
	/// Gets or sets whether a statically rendered (static SSR) form emits client-side validation rules, exactly as
	/// it does for <c>DataAnnotationsValidator</c>. Defaults to <see langword="true"/>. Client rules come from the
	/// registered <c>ClientValidationProvider</c>; see <c>AddZodSharpClientValidation</c> in
	/// Purview.ZodSharp.AspNetCore for ZodSharp rules.
	/// </summary>
	[Parameter]
	public bool EnableClientValidation { get; set; } = true;

	/// <summary>
	/// Gets or sets whether a field change re-validates the whole object that owns the field (cross-field rules such
	/// as <c>[Compare]</c> and type-level rules) and updates every field of that object. When <see langword="false"/>
	/// (the default) a field change runs only that member's own rules.
	/// </summary>
	[Parameter]
	public bool ValidateNestedOnFieldChange { get; set; }

	/// <inheritdoc/>
	protected override void OnInitialized()
	{
		if (CurrentEditContext is null)
			throw new InvalidOperationException(
				$"{nameof(ZodValidator)} requires a cascading parameter of type {nameof(EditContext)}. For example, "
					+ $"you can use {nameof(ZodValidator)} inside an {nameof(EditForm)}."
			);

		_editContext = CurrentEditContext;
		_store = new(_editContext);
		_errors = ZodFormErrors.GetOrCreate(_editContext);
		_errors.Validator = this;
		_editContext.Properties[typeof(ZodValidator)] = this;

		_resolver = Services.GetService<ZodValidatableInfoResolver>();
		_options = Services.GetService<IOptions<ZodBlazorOptions>>()?.Value ?? new();
		_renderer = ZodMessageRenderer.Create(Services, _options);
		_timeProvider = Services.GetService<TimeProvider>() ?? TimeProvider.System;
		_logger = (ILogger?)Services.GetService<ILogger<ZodValidator>>() ?? NullLogger.Instance;

		if (EnableClientValidation && !_editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out _))
		{
			_editContext.Properties[typeof(DataAnnotationsValidator)] = ClientValidationMarker;
			_wroteClientValidationKey = true;
		}

		_editContext.OnValidationRequested += OnValidationRequested;
		_editContext.OnFieldChanged += OnFieldChanged;
	}

	/// <inheritdoc/>
	protected override void OnParametersSet()
	{
		if (CurrentEditContext != _editContext)
			throw new InvalidOperationException(
				$"{nameof(ZodValidator)} does not support changing the {nameof(EditContext)} dynamically."
			);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_editContext is null)
			return;

		_editContext.OnValidationRequested -= OnValidationRequested;
		_editContext.OnFieldChanged -= OnFieldChanged;

		_store?.Clear();
		_errors?.ClearValidator();
		if (_errors is not null && ReferenceEquals(_errors.Validator, this))
			_errors.Validator = null;

		if (
			_editContext.Properties.TryGetValue(typeof(ZodValidator), out var attached)
			&& ReferenceEquals(attached, this)
		)
			_editContext.Properties.Remove(typeof(ZodValidator));

		if (
			_wroteClientValidationKey
			&& _editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out var marker)
			&& ReferenceEquals(marker, ClientValidationMarker)
		)
			_editContext.Properties.Remove(typeof(DataAnnotationsValidator));

		_editContext.NotifyValidationStateChanged();
		_editContext = null;
	}

	/// <summary>
	/// Gets the validator of the form model: <see cref="Schema"/>, or the registered validator of the model type.
	/// </summary>
	internal IZodUntypedValidator? RootValidator =>
		_editContext is null ? null : ResolveValidator(_editContext.Model.GetType(), preferSchema: true);

	IZodUntypedValidator? ResolveValidator(Type type, bool preferSchema)
	{
		if (preferSchema && Schema is { } schema && schema.ValidatedType.IsAssignableFrom(type))
			return schema;

		// A conditional expression would nest the schema and resolver lookups; the ordered ifs keep the
		// precedence of the three sources explicit.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (_resolver is not null && _resolver.TryGetValidator(type, out var registered))
			return registered;
#pragma warning restore IDE0046

		return !preferSchema && Schema is { } fallback && fallback.ValidatedType.IsAssignableFrom(type)
			? fallback
			: null;
	}

	/// <summary>
	/// Gets whether a <c>DataAnnotationsValidator</c> shares the form: its client-validation key holds the value it
	/// writes rather than ours. (A <c>DataAnnotationsValidator</c> with client validation disabled is not detected.)
	/// </summary>
	bool IsDataAnnotationsValidatorPresent =>
		_editContext is not null
		&& _editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out var value)
		&& !ReferenceEquals(value, ClientValidationMarker);

	/// <summary>
	/// Gets whether messages for <paramref name="modelType"/> are already shown by a <c>DataAnnotationsValidator</c>
	/// on the same form, because the ZodSharp bridge validates that type for it. The full errors are still recorded.
	/// </summary>
	bool ShouldSuppressMessages(Type modelType)
	{
		if (!IsDataAnnotationsValidatorPresent || _resolver is null || !_resolver.TryGetValidator(modelType, out _))
			return false;

		if (!_warnedCoexistence && _options.WarnOnValidatorCoexistence)
		{
			_warnedCoexistence = true;
			LogValidatorCoexistence(_logger, modelType.FullName ?? modelType.Name);
		}

		return true;
	}

	void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
	{
		if (_editContext is null || RootValidator is not { } validator)
			return;

		var model = _editContext.Model;
		try
		{
			e.AddAsyncValidator(cancellationToken => ValidateFormAsync(model, validator, cancellationToken));
		}
		catch (InvalidOperationException)
		{
			// The obsolete synchronous EditContext.Validate(): no async validators can be registered.
			ValidateFormSynchronously(model, validator);
		}
	}

	void ValidateFormSynchronously(object model, IZodUntypedValidator validator)
	{
		if (validator.RequiresAsyncValidation)
			throw new InvalidOperationException(
				$"'{validator.ValidatedType.FullName}' has asynchronous validation rules, so it cannot be validated "
					+ "synchronously. Use EditContext.ValidateAsync."
			);

		ApplyFormErrors(model, validator, validator.Validate(model));
	}

	async Task ValidateFormAsync(object model, IZodUntypedValidator validator, CancellationToken cancellationToken)
	{
		ImmutableArray<ValidationError> errors;
		try
		{
			errors = await validator.ValidateAsync(model, Services, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogValidationFaulted(_logger, ex, model.GetType().FullName ?? model.GetType().Name, string.Empty);
			throw;
		}

		cancellationToken.ThrowIfCancellationRequested();
		await InvokeAsync(() => ApplyFormErrors(model, validator, errors)).ConfigureAwait(false);
	}

	void ApplyFormErrors(object model, IZodUntypedValidator validator, ImmutableArray<ValidationError> errors)
	{
		if (_editContext is null || _store is null || _errors is null)
			return;

		_store.Clear();
		_errors.ClearValidator();

		var suppress = ShouldSuppressMessages(model.GetType());
		foreach (var error in errors.IsDefault ? [] : errors)
		{
			var field = ZodFieldLocator.Locate(model, error.FullPath, validator, ignoreCase: false);
			Place(field, error, suppress);
		}

		_editContext.NotifyValidationStateChanged();
	}

	void Place(in FieldIdentifier field, in ValidationError error, bool suppressMessage)
	{
		var message = _renderer!.Render(error, field.Model.GetType());
		_errors!.AddValidatorError(field, error, message);
		if (!suppressMessage)
			_store!.Add(field, message);
	}

	void OnFieldChanged(object? sender, FieldChangedEventArgs e)
	{
		if (_editContext is null || (ValidateOn & (ZodValidationTrigger.Change | ZodValidationTrigger.Input)) == 0)
			return;

		var field = e.FieldIdentifier;
		var plan = PlanFieldValidation(field);
		if (plan is null)
			return;

		var debounce =
			(ValidateOn & ZodValidationTrigger.Input) != 0 && DebounceMilliseconds > 0
				? TimeSpan.FromMilliseconds(DebounceMilliseconds)
				: TimeSpan.Zero;

		_editContext.RegisterAsyncFieldValidator(field, cancellationToken =>
			ValidateFieldAsync(field, plan.Value, debounce, cancellationToken)
		);
	}

	/// <summary>
	/// How a changed field is validated.
	/// </summary>
	/// <param name="Validator">The validator that runs.</param>
	/// <param name="Target">The object it validates: the field's owner, or the form model.</param>
	/// <param name="MemberOnly">Whether only the member's own rules run (<see cref="IZodUntypedValidator.ValidateMemberAsync"/>).</param>
	readonly record struct FieldPlan(IZodUntypedValidator Validator, object Target, bool MemberOnly);

	FieldPlan? PlanFieldValidation(in FieldIdentifier field)
	{
		var owner = field.Model;
		var ownerValidator = ResolveValidator(
			owner.GetType(),
			preferSchema: ReferenceEquals(owner, _editContext!.Model)
		);
		if (ownerValidator is not null)
		{
			if (ValidateNestedOnFieldChange)
				return new FieldPlan(ownerValidator, owner, MemberOnly: false);

			if (!string.IsNullOrEmpty(field.FieldName) && ownerValidator.IsValidatableMember(field.FieldName))
				return new FieldPlan(ownerValidator, owner, MemberOnly: true);
		}

		// No member validation available (a runtime schema): validate the whole form and keep this field's errors.
		return RootValidator is { } root ? new FieldPlan(root, _editContext.Model, MemberOnly: false) : null;
	}

	async Task ValidateFieldAsync(
		FieldIdentifier field,
		FieldPlan plan,
		TimeSpan debounce,
		CancellationToken cancellationToken
	)
	{
		if (debounce > TimeSpan.Zero)
			await Task.Delay(debounce, _timeProvider, cancellationToken).ConfigureAwait(false);

		ImmutableArray<ValidationError> errors;
		try
		{
			errors = plan.MemberOnly
				? await plan
					.Validator.ValidateMemberAsync(plan.Target, field.FieldName, Services, cancellationToken)
					.ConfigureAwait(false)
				: await plan.Validator.ValidateAsync(plan.Target, Services, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogValidationFaulted(
				_logger,
				ex,
				field.Model.GetType().FullName ?? field.Model.GetType().Name,
				field.FieldName
			);
			throw;
		}

		// Superseded by a newer change (or a submit) while validating: the newer run owns the field now.
		cancellationToken.ThrowIfCancellationRequested();
		await InvokeAsync(() => ApplyFieldErrors(field, plan, errors)).ConfigureAwait(false);
	}

	void ApplyFieldErrors(FieldIdentifier field, FieldPlan plan, ImmutableArray<ValidationError> errors)
	{
		if (_editContext is null || _store is null || _errors is null)
			return;

		var suppress = ShouldSuppressMessages(plan.Target.GetType());
		var scopeToOwner = ValidateNestedOnFieldChange;

		// Every field this run speaks for is reset; other fields' messages are left exactly as they were.
		List<FieldIdentifier> reset = [field];
		if (scopeToOwner)
		{
			foreach (var tracked in _errors.ValidatorFields)
			{
				if (ReferenceEquals(tracked.Model, field.Model) && !tracked.Equals(field))
					reset.Add(tracked);
			}
		}

		foreach (var target in reset)
		{
			_store.Clear(target);
			_errors.ClearValidator(target);
		}

		foreach (var error in errors.IsDefault ? [] : errors)
		{
			if (plan.MemberOnly)
			{
				// Member validation runs only the field's own rules: every error belongs to the field.
				Place(field, error, suppress);
				continue;
			}

			var located = ZodFieldLocator.Locate(plan.Target, error.FullPath, plan.Validator, ignoreCase: false);
			var inScope = scopeToOwner ? ReferenceEquals(located.Model, field.Model) : located.Equals(field);
			if (inScope)
				Place(located, error, suppress);
		}

		_editContext.NotifyValidationStateChanged();
	}

	/// <summary>
	/// Clears every message and error this validator placed.
	/// </summary>
	internal void ClearOwnErrors()
	{
		_store?.Clear();
		_errors?.ClearValidator();
	}

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Warning,
		Message = "ZodValidator and DataAnnotationsValidator share a form for '{ModelType}'. ZodValidator records the "
			+ "full errors but leaves the messages to DataAnnotationsValidator; use a single validator."
	)]
	static partial void LogValidatorCoexistence(ILogger logger, string modelType);

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Error,
		Message = "ZodSharp validation of '{ModelType}' field '{FieldName}' threw; the field is marked faulted."
	)]
	static partial void LogValidationFaulted(ILogger logger, Exception exception, string modelType, string fieldName);
}
