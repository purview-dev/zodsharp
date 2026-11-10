namespace ZodSharp.Unions;

/// <summary>
/// Marker interface for discriminated union value types.
/// </summary>
/// <remarks>
/// <para>
/// ZodSharp's <see cref="Union{T1,T2}"/> (and higher-arity overloads) implement this interface to provide a
/// common, non-boxing discriminated-union surface, and so does the net11.0-only
/// <c>NativeUnion&lt;T1, T2&gt;</c>.
/// </para>
/// <para>
/// On .NET 11 and later the BCL also defines <c>System.Runtime.CompilerServices.IUnion</c>; the native union
/// implements both, because the runtime's contract exposes only <c>Value</c> while ZodSharp's resolver also
/// needs <see cref="Tag"/> to follow the active case without unboxing it.
/// </para>
/// </remarks>
public interface IUnion
{
	/// <summary>
	/// Gets the boxed value of the union, or <see langword="null"/> when the union is empty. Prefer the non-boxing
	/// <c>TryGetValue</c> overloads in performance-critical paths.
	/// </summary>
	object? Value { get; }

	/// <summary>
	/// Gets the tag identifying which case of the union is active: <c>0</c> when the union is empty, then
	/// <c>1</c>, <c>2</c>, … for the case types in declaration order.
	/// </summary>
	int Tag { get; }
}
