using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Http.Fixtures;

[ZodSchema]
public partial class Order
{
	[MinLengthZod(3)]
	public string Reference { get; set; } = string.Empty;

	[MinValue(1)]
	public int Quantity { get; set; }
}

/// <summary>The references already taken; what the service-aware rule on <see cref="Booking"/> resolves.</summary>
public interface IReferenceRegistry
{
	bool IsTaken(string reference);
}

sealed class ReferenceRegistry(params string[] taken) : IReferenceRegistry
{
	public bool IsTaken(string reference) => taken.Contains(reference, StringComparer.Ordinal);
}

/// <summary>A model with a service-aware async rule: its reference must not be taken.</summary>
[ZodSchema]
public sealed partial class Booking
{
	public const string ReferenceTakenCode = "reference_taken";

	[MinLengthZod(3)]
	public string Reference { get; set; } = string.Empty;

	[ZodAsyncMember(nameof(Reference))]
	internal static ValueTask<ValidationResult<Booking>> CustomValidationAsync(
		Booking value,
		IServiceProvider services,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return
			services.GetService(typeof(IReferenceRegistry)) is IReferenceRegistry registry
			&& registry.IsTaken(value.Reference)
			? ValueTask.FromResult(
				ValidationResult<Booking>.Failure(
					ValidationError.Create(ReferenceTakenCode, "Reference is taken", BookingSchema.Paths.Reference)
				)
			)
			: ValueTask.FromResult(ValidationResult<Booking>.Success(value));
	}
}

/// <summary>The contract a WebAssembly client would supply: source-generated, never reflective.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Booking))]
sealed partial class HttpTestJsonContext : JsonSerializerContext;

/// <summary>Records each request (method, URI and body) and answers with a fixed response.</summary>
sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
	public List<(HttpMethod Method, Uri? Uri, string? MediaType, string Body)> Requests { get; } = [];

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken
	)
	{
		var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
		Requests.Add((request.Method, request.RequestUri, request.Content?.Headers.ContentType?.MediaType, body));
		return new HttpResponseMessage(status) { RequestMessage = request };
	}
}
