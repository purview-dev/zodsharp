namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// How the generated validator invokes the synchronous refinement method.
/// </summary>
enum SyncValidationInvocationKind
{
	/// <summary>No synchronous refinement — nothing is invoked.</summary>
	None,

	/// <summary>Invoke as an instance method with no arguments: value.MethodName().</summary>
	Parameterless,

	/// <summary>Invoke as an instance method with a <c>RefineCtx&lt;T&gt;</c> argument: value.MethodName(refineCtx).</summary>
	WithRefineContext,
}

/// <summary>
/// Immutable result of synchronous refinement method discovery and validation.
/// Carries the method name, resolved symbol (if valid), and invocation kind.
/// </summary>
readonly record struct SyncValidationMethodData(
	bool IsConfigured,
	bool Exists,
	bool IsValid,
	string MethodName,
	SyncValidationInvocationKind InvocationKind
)
{
	public static readonly SyncValidationMethodData None = new(
		IsConfigured: false,
		Exists: false,
		IsValid: false,
		MethodName: string.Empty,
		InvocationKind: SyncValidationInvocationKind.None
	);

	public bool HasSyncValidation => IsValid && InvocationKind != SyncValidationInvocationKind.None;
}
