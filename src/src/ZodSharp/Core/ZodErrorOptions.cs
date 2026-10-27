namespace ZodSharp.Core;

/// <summary>
/// Process-wide options for the errors built-in schemas and rules report.
/// </summary>
public static class ZodErrorOptions
{
	/// <summary>
	/// Gets or sets whether built-in errors carry the raw input in their <see cref="ZodIssueParameters.Received"/>
	/// parameter. Defaults to <see langword="false"/>: errors leave the process (ProblemDetails, logs, UI), and
	/// the input may be a card number, a token or another secret, so by default <c>received</c> holds only the
	/// input's type name (for example <c>"string"</c>). Built-in default messages never contain the input,
	/// whatever this is set to.
	/// </summary>
	/// <remarks>
	/// Intended for local development and diagnostics only; do not enable it in production.
	/// </remarks>
	public static bool IncludeReceivedValues
	{
		get => Volatile.Read(ref field);
		set => Volatile.Write(ref field, value);
	}
}
