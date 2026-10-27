namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// How the generated validator invokes the custom validation method.
/// </summary>
enum CustomValidationInvocationKind
{
	/// <summary>No custom method — synchronous fallback path.</summary>
	None,

	/// <summary>Invoke as a static method on the schema model type: TypeName.MethodName(value, ct).</summary>
	StaticOnModelType,

	/// <summary>Invoke as a method on the generated schema validator</summary>
	DefinedOnSchemaValidator,
}

/// <summary>
/// Immutable result of custom async validation method discovery and validation.
/// Carries the method name, resolved symbol (if valid), and invocation kind.
/// </summary>
readonly record struct CustomValidationMethodData(
	bool IsConfigured,
	bool Exists,
	bool IsValid,
	string MethodName,
	CustomValidationInvocationKind InvocationKind
)
{
	public static readonly CustomValidationMethodData None = new(
		IsConfigured: false,
		Exists: false,
		IsValid: false,
		MethodName: string.Empty,
		InvocationKind: CustomValidationInvocationKind.None
	);

	public bool HasCustomValidation => IsValid && InvocationKind != CustomValidationInvocationKind.None;

	/// <summary>
	/// Whether the method takes the caller's services: <c>(T value, IServiceProvider services, CancellationToken ct)</c>.
	/// </summary>
	public bool RequiresServices { get; init; }

	/// <summary>
	/// The members declared by <c>[ZodAsyncMember]</c> on the method; member-level async validation runs the method
	/// only for these.
	/// </summary>
	public EquatableArray<string> AsyncMembers { get; init; } = new([]);
}
