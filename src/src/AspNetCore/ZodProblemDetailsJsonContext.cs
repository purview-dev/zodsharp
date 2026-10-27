using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Source-generated JSON metadata for every type ZodSharp places in a ProblemDetails payload, so the payload
/// serializes under Native AOT (where reflection-based metadata is unavailable). Added to the minimal-API
/// <c>JsonOptions</c> resolver chain by <c>AddZodSharpProblemDetails</c>, and used directly when no
/// <see cref="IProblemDetailsService"/> is registered.
/// </summary>
/// <remarks>
/// <see cref="ZodProblemDetailsWriter"/> normalizes every value it writes into one of the scalar types listed
/// here, which is what makes <c>object</c>-typed members (bounds, parameters, segments) serializable without
/// reflection.
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ValidationIssue[]))]
[JsonSerializable(typeof(ValidationIssue[][]))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(bool))]
sealed partial class ZodProblemDetailsJsonContext : JsonSerializerContext;

/// <summary>
/// Marks that the ZodSharp JSON metadata has been added to the minimal-API JSON options, so repeated registration
/// calls add it once.
/// </summary>
sealed class ZodProblemDetailsJsonMarker;
