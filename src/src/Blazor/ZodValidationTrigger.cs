namespace ZodSharp.Blazor;

/// <summary>
/// When <see cref="ZodValidator"/> validates. Form-level validation on submit (and on every
/// <c>EditContext.ValidateAsync</c> call) always runs; these flags choose the field-level triggers.
/// </summary>
[Flags]
public enum ZodValidationTrigger
{
	/// <summary>
	/// Validate only when the form is submitted or <c>EditContext.ValidateAsync</c> is called. Field changes do not
	/// validate.
	/// </summary>
	Submit = 1,

	/// <summary>
	/// Also validate a field as soon as it changes (an input's <c>onchange</c>, the default binding event).
	/// </summary>
	Change = 2,

	/// <summary>
	/// Also validate a field as it changes, delayed by <see cref="ZodValidator.DebounceMilliseconds"/> so a burst of
	/// changes validates once. Each change supersedes and cancels the pending check. The input must report a change per
	/// keystroke: an <c>InputText</c> subclass whose <c>&lt;input&gt;</c> binds with <c>@bind:event="oninput"</c>
	/// (component bindings such as <c>InputText</c>'s <c>@bind-Value</c> have no event to choose).
	/// </summary>
	Input = 4,
}
