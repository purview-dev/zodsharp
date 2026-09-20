namespace ZodSharp.Core;

/// <summary>
/// Declares a named parameter of an <c>ErrorType</c> message template, together with the CLR
/// <see cref="Type"/> the value is expected to have.
/// </summary>
/// <param name="Name">The named placeholder this parameter maps to.</param>
/// <param name="Type">The expected value type (for example <c>typeof(string)</c>).</param>
public sealed record ErrorTypeParameter(string Name, Type Type);
