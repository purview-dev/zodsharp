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
	/// changes (an input bound with <c>@bind-Value:event="oninput"</c>) validates once. Each change supersedes and
	/// cancels the pending check.
	/// </summary>
	Input = 4,
}
