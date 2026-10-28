using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// A validation problem returned by an API: the RFC 9457 ProblemDetails body that Purview.ZodSharp.AspNetCore writes
/// (<c>errors</c>, <c>issues</c> and <c>traceId</c>), or a plain ASP.NET Core <c>ValidationProblemDetails</c> with
/// only <c>errors</c>. Read one with <c>HttpResponseMessage.TryReadZodValidationProblemAsync()</c> and map it onto a
/// form with <c>EditContext.AddZodProblemDetails</c>.
/// </summary>
public sealed class ZodValidationProblemDetails
{
	/// <summary>Gets or sets the problem type URI.</summary>
	[JsonPropertyName("type")]
	public string? Type { get; set; }

	/// <summary>Gets or sets the short summary of the problem.</summary>
	[JsonPropertyName("title")]
	public string? Title { get; set; }

	/// <summary>Gets or sets the HTTP status code.</summary>
	[JsonPropertyName("status")]
	public int? Status { get; set; }

	/// <summary>Gets or sets the explanation of this occurrence of the problem.</summary>
	[JsonPropertyName("detail")]
	public string? Detail { get; set; }

	/// <summary>Gets or sets the URI of this occurrence of the problem.</summary>
	[JsonPropertyName("instance")]
	public string? Instance { get; set; }

	/// <summary>Gets or sets the messages per dotted path (<c>Lines[0].Quantity</c>); the root is <c>""</c>.</summary>
	[JsonPropertyName("errors")]
	public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

	/// <summary>Gets or sets the full-fidelity issues, when the server wrote them.</summary>
	[JsonPropertyName("issues")]
	public IReadOnlyList<ZodValidationProblemIssue>? Issues { get; set; }

	/// <summary>Gets or sets the trace identifier of the failed request.</summary>
	[JsonPropertyName("traceId")]
	public string? TraceId { get; set; }

	/// <summary>
	/// Converts the problem into ZodSharp errors: one per issue when <see cref="Issues"/> is present (with its typed
	/// path, code, category, origin, bounds and parameters), otherwise one per <see cref="Errors"/> message, with the
	/// path parsed from the dotted key and the code <c>custom</c>.
	/// </summary>
	/// <returns>The errors.</returns>
	public ImmutableArray<ValidationError> ToValidationErrors()
	{
		var builder = ImmutableArray.CreateBuilder<ValidationError>();
		if (Issues is { Count: > 0 } issues)
		{
			foreach (var issue in issues)
				builder.Add(issue.ToValidationError());

			return builder.ToImmutable();
		}

		if (Errors is { Count: > 0 } errors)
		{
			foreach (var (key, messages) in errors)
			{
				var path = ZodPath.ParseDotted(key);
				foreach (var message in messages ?? [])
				{
					builder.Add(
						ValidationError.Create(
							ZodIssueCategories.Custom,
							message ?? string.Empty,
							path,
							category: ZodIssueCategories.Custom
						)
					);
				}
			}
		}

		return builder.ToImmutable();
	}
}

/// <summary>
/// One issue of a <see cref="ZodValidationProblemDetails"/>, in Zod's issue shape.
/// </summary>
public sealed class ZodValidationProblemIssue
{
	/// <summary>Gets or sets the dotted path (<c>Items[0].Quantity</c>); empty for the root.</summary>
	[JsonPropertyName("path")]
	public string? Path { get; set; }

	/// <summary>Gets or sets the typed path: strings for members and keys, integers for indexes.</summary>
	[JsonPropertyName("segments")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public JsonElement[]? Segments { get; set; }

	/// <summary>Gets or sets the issue code.</summary>
	[JsonPropertyName("code")]
	public string? Code { get; set; }

	/// <summary>Gets or sets the category (the Zod issue code for built-in errors).</summary>
	[JsonPropertyName("category")]
	public string? Category { get; set; }

	/// <summary>Gets or sets the origin (<c>string</c>, <c>number</c>, ...).</summary>
	[JsonPropertyName("origin")]
	public string? Origin { get; set; }

	/// <summary>Gets or sets the lower bound: a JSON number, or an ISO-8601 string for dates.</summary>
	[JsonPropertyName("minimum")]
	public JsonElement? Minimum { get; set; }

	/// <summary>Gets or sets the upper bound: a JSON number, or an ISO-8601 string for dates.</summary>
	[JsonPropertyName("maximum")]
	public JsonElement? Maximum { get; set; }

	/// <summary>Gets or sets whether the bound is inclusive.</summary>
	[JsonPropertyName("inclusive")]
	public bool? Inclusive { get; set; }

	/// <summary>Gets or sets the message.</summary>
	[JsonPropertyName("message")]
	public string? Message { get; set; }

	/// <summary>Gets or sets the issue parameters (invariant scalars and arrays of them).</summary>
	[JsonPropertyName("parameters")]
	public IReadOnlyDictionary<string, JsonElement>? Parameters { get; set; }

	/// <summary>Gets or sets, for a failed union, the issues of each option.</summary>
	[JsonPropertyName("unionErrors")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public ZodValidationProblemIssue[][]? UnionErrors { get; set; }

	/// <summary>
	/// Gets the typed path: from <see cref="Segments"/> when present, otherwise parsed from <see cref="Path"/>.
	/// </summary>
	/// <returns>The path.</returns>
	public ZodPath ToZodPath()
	{
		if (Segments is not { Length: > 0 } segments)
			return ZodPath.ParseDotted(Path);

		var builder = ImmutableArray.CreateBuilder<ZodPathSegment>(segments.Length);
		foreach (var segment in segments)
		{
			// The ValueKind switch only distinguishes indexes from names: everything else is rendered as a name.
#pragma warning disable IDE0010 // Add missing cases
			switch (segment.ValueKind)
			{
				case JsonValueKind.Number when segment.TryGetInt32(out var index) && index >= 0:
					builder.Add(ZodPathSegment.At(index));
					break;
				case JsonValueKind.String:
					builder.Add(ZodPathSegment.Member(segment.GetString() ?? string.Empty));
					break;
				default:
					builder.Add(ZodPathSegment.Member(segment.GetRawText()));
					break;
			}
#pragma warning restore IDE0010 // Add missing cases
		}

		return ZodPath.From(builder.MoveToImmutable());
	}

	/// <summary>
	/// Converts the issue into a ZodSharp error.
	/// </summary>
	/// <returns>The error.</returns>
	public ValidationError ToValidationError()
	{
		Dictionary<string, object?>? parameters = null;
		if (Parameters is { Count: > 0 })
		{
			parameters = [with(StringComparer.Ordinal)];
			foreach (var (name, value) in Parameters)
				parameters[name] = ToScalar(value);
		}

		if (UnionErrors is { Length: > 0 } unionErrors)
		{
			var options = ImmutableArray.CreateBuilder<ImmutableArray<ValidationError>>(unionErrors.Length);
			foreach (var option in unionErrors)
				options.Add([.. (option ?? []).Select(static issue => issue.ToValidationError())]);

			parameters ??= [with(StringComparer.Ordinal)];
			parameters[ZodIssueParameters.UnionErrors] = options.MoveToImmutable();
		}

		var error = ValidationError.Create(
			Code ?? ZodIssueCategories.Custom,
			Message ?? string.Empty,
			ToZodPath(),
			parameters,
			Origin,
			category: Category
		);

		var lower = ToBound(Minimum);
		var upper = ToBound(Maximum);
		return lower is null && upper is null ? error : error with { Lower = lower, Upper = upper };
	}

	ErrorBound? ToBound(JsonElement? element) =>
		element is { } value && ToScalar(value) is { } scalar ? new ErrorBound(scalar, Inclusive ?? true) : null;

	static object? ToScalar(JsonElement value)
	{
#pragma warning disable IDE0010 // Add missing cases
		switch (value.ValueKind)
		{
			case JsonValueKind.String:
				return value.GetString();
			case JsonValueKind.Number:
				if (value.TryGetInt64(out var integer))
					return integer;

				return value.TryGetDouble(out var number) ? number : value.GetRawText();
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			case JsonValueKind.Array:
				return value.EnumerateArray().Select(ToScalar).ToImmutableArray();
			default:
				return value.GetRawText();
		}
#pragma warning restore IDE0010 // Add missing cases
	}

	/// <inheritdoc/>
	public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Path}: {Code} {Message}");
}

/// <summary>
/// Source-generated JSON metadata for reading validation problems, so reading is trim- and AOT-safe in Blazor
/// WebAssembly.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ZodValidationProblemDetails))]
sealed partial class ZodBlazorJsonContext : JsonSerializerContext;
