namespace ZodSharp.AspNetCore.SourceGenerators.Models;

/// <summary>
/// Value-equatable pipeline model describing one declared <c>ErrorType</c> parameter: the
/// placeholder name and the components needed to emit its strongly typed parameter type. All
/// Roslyn objects are removed before this point; array and nullable modifiers are applied by the
/// emitter so the target compilation's nullable context is honoured.
/// </summary>
/// <param name="Name">The declared placeholder name.</param>
/// <param name="Type">The underlying type identity (nullable value types are unwrapped).</param>
/// <param name="IsNullable">Whether the type is <c>Nullable&lt;T&gt;</c>.</param>
/// <param name="ArrayRank">The array rank, or <c>0</c> when the type is not an array.</param>
readonly record struct ErrorTypeParameterModel(string Name, TypeIdentity Type, bool IsNullable, int ArrayRank);
