namespace ZodSharp.Core;

/// <summary>
/// Attribute to mark a static ErrorType field for strongly-typed helper generation.
/// When applied to a static ErrorType field in a partial class, Create/Throw helpers are generated at compile time.
/// </summary>
/// <remarks>
/// Defined once, here, rather than generated into every consuming compilation: a generated copy gave every project in
/// a solution an identical type and XML documentation ID, which crashed tools that index documentation across a
/// project and its references (the ASP.NET Core OpenAPI XML-comment generator).
/// </remarks>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ErrorTypeAttribute : Attribute { }
