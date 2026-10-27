namespace ZodSharp;

/// <summary>
/// Declares which members a <c>[ZodSchema]</c> type's custom async validation method validates, so
/// field-level validation (<c>ValidateMemberAsync</c>, for example a Blazor field change) runs the method only
/// when one of those members changes, and reports only the errors it raises for that member.
/// </summary>
/// <remarks>
/// Without the attribute the custom method runs only on full validation. Apply it to the method named by
/// <c>[ZodSchema(CustomValidationMethodName = ...)]</c> (by default <c>CustomValidationAsync</c>).
/// </remarks>
/// <example>
/// <code>
/// [ZodAsyncMember(nameof(Email))]
/// internal static async ValueTask&lt;ValidationResult&lt;User&gt;&gt; CustomValidationAsync(User value, IServiceProvider services, CancellationToken ct) { ... }
/// </code>
/// </example>
/// <param name="members">The C# member names the method validates.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ZodAsyncMemberAttribute(params string[] members) : Attribute
{
	/// <summary>
	/// Gets the C# member names the method validates.
	/// </summary>
	public IReadOnlyList<string> Members { get; } = members ?? [];
}
