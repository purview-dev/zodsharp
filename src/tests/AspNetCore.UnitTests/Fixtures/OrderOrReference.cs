#if NET11_0_OR_GREATER
namespace ZodSharp.AspNetCore.Fixtures;

/// <summary>
/// A C# 15 union body whose <see cref="OrderRequest"/> case ZodSharp validates; the union itself has no validator.
/// The cases have different JSON value kinds (object and string), so System.Text.Json can pick the case.
/// </summary>
public union OrderOrReference(OrderRequest, string);
#endif
