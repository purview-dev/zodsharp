using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZodSharp.Core;

// A stand-in for the Purview.ValueObjects [Scalar] attribute, which the generator recognizes by name and namespace.
#pragma warning disable IDE0130 // The stand-in must live in the namespace the generator recognizes.
namespace Purview.ValueObjects.Serialization
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	sealed class ScalarAttribute(string propertyName = "Value") : Attribute
	{
		public string PropertyName { get; } = propertyName;
	}
}

#pragma warning restore IDE0130

namespace ZodSharp.TrimSafety
{
	/// <summary>A scalar value object read by a strict converter, as Purview.ValueObjects generates it.</summary>
	[Purview.ValueObjects.Serialization.Scalar]
	[ZodSchema]
	[JsonConverter(typeof(ContactEmailJsonConverter))]
	readonly partial record struct ContactEmail
	{
		public ContactEmail(string value) => Value = value;

		[Required]
		[EmailAddress]
		public string Value { get; }
	}

	[ZodSchema]
	sealed partial record PlaceOrderRequest(ContactEmail CustomerEmail, int Quantity);

	/// <summary>Rejects an invalid value with <c>JsonException(inner: ZodException)</c>.</summary>
	sealed class ContactEmailJsonConverter : JsonConverter<ContactEmail>
	{
		public override ContactEmail Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var result = ContactEmailSchema.Validate(new ContactEmail(reader.GetString() ?? string.Empty));
			return result.IsSuccess
				? result.Value
				: throw new JsonException(
					"Cannot deserialize ContactEmail: the value failed validation.",
					new ZodException(result.Errors)
				);
		}

		public override void Write(Utf8JsonWriter writer, ContactEmail value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Value);
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(PlaceOrderRequest))]
	sealed partial class StrictJsonContext : JsonSerializerContext;

	/// <summary>
	/// A body rejected by a strict value object converter while it is deserialized: located under the member that held
	/// the value, in a Native AOT minimal API (request delegate generator, slim builder, source-generated contract) and
	/// through the System.Text.Json and HttpClient integrations.
	/// </summary>
	static class StrictDeserializationChecks
	{
		const string InvalidEmailBody = /*lang=json,strict*/
			"""{"customerEmail":"not-an-email","quantity":5}""";
		const string ExpectedKey = "CustomerEmail.Value";
		const string ExpectedMessage = "Field 'CustomerEmail' must be a valid email address.";

		static readonly HttpClient Client = new();

		public static async Task MinimalAPI(Harness h)
		{
			foreach (var handleBindingFailures in new[] { true, false })
			{
				var mode = handleBindingFailures ? "HandleBindingFailures" : "the .NET 11 binding problem";
				await using var app = await StartAsync(handleBindingFailures);

				using var rejected = await PostAsync(app, InvalidEmailBody);
				var root = rejected.Document?.RootElement;
				h.Expect(
					rejected.Status == 400 && rejected.MediaType == "application/problem+json",
					$"a rejected value object is a 400 problem ({mode})"
				);
				h.Expect(
					root is { } r
						&& r.TryGetProperty("errors", out var errors)
						&& errors.TryGetProperty(ExpectedKey, out var messages)
						&& messages[0].GetString() == ExpectedMessage,
					$"the error is keyed by the member path and named after the member ({mode}; got {rejected.Text})"
				);
				h.Expect(
					root is { } issueRoot
						&& issueRoot.TryGetProperty("issues", out var issues)
						&& issues[0].GetProperty("code").GetString() == "invalid_string"
						&& string.Join(
							'|',
							issues[0].GetProperty("segments").EnumerateArray().Select(static s => s.GetString())
						) == "CustomerEmail|Value",
					$"the issue keeps its code and carries the member segments ({mode})"
				);
			}

			await using var optedIn = await StartAsync(handleBindingFailures: true);
			using var malformed = await PostAsync(optedIn, """{"customerEmail":""");
			h.Expect(
				malformed.Status == 400
					&& malformed.MediaType == "application/problem+json"
					&& malformed.Document?.RootElement.TryGetProperty("issues", out _) == false,
				"malformed JSON is a standard 400 problem without issues"
			);
		}

		public static async Task JsonIntegrations(Harness h)
		{
			var typeInfo = StrictJsonContext.Default.PlaceOrderRequest;
			PlaceOrderRequestSchemaValidator validator = new();

			var deserialized = validator.DeserializeAndValidate(InvalidEmailBody, typeInfo);
			h.Expect(
				!deserialized.IsSuccess
					&& deserialized.Errors.Single().FullPath.ToDottedString() == ExpectedKey
					&& deserialized.Errors.Single().Message == ExpectedMessage,
				"DeserializeAndValidate reports the rerooted error, not invalid_json"
			);

			using StringContent content = new(InvalidEmailBody, Encoding.UTF8, "application/json");
			var read = await content.ReadFromJsonAndValidateAsync(validator, typeInfo);
			h.Expect(
				!read.IsSuccess && read.Errors.Single().FullPath.ToDottedString() == ExpectedKey,
				"ReadFromJsonAndValidateAsync reports the rerooted error, not invalid_json"
			);
		}

		static async Task<WebApplication> StartAsync(bool handleBindingFailures)
		{
			var builder = WebApplication.CreateSlimBuilder(
				new WebApplicationOptions { EnvironmentName = Environments.Production }
			);
			builder.WebHost.UseUrls("http://127.0.0.1:0");
			builder.Logging.ClearProviders();
			builder.Services.ConfigureHttpJsonOptions(static options =>
				options.SerializerOptions.TypeInfoResolverChain.Insert(0, StrictJsonContext.Default)
			);
			builder.Services.AddZodSharpProblemDetails(o => o.HandleBindingFailures = handleBindingFailures);

			var app = builder.Build();
			app.UseExceptionHandler();
			app.MapPost("/orders", static (PlaceOrderRequest request) => TypedResults.Ok());
			await app.StartAsync();

			return app;
		}

		static async Task<Response> PostAsync(WebApplication app, string body)
		{
			using StringContent content = new(body, Encoding.UTF8, "application/json");
			using var response = await Client.PostAsync(new Uri(new Uri(app.Urls.First()), "/orders"), content);
			var text = await response.Content.ReadAsStringAsync();
			return new(
				(int)response.StatusCode,
				response.Content.Headers.ContentType?.MediaType,
				text,
				text.Length == 0 ? null : JsonDocument.Parse(text)
			);
		}

		sealed record Response(int Status, string? MediaType, string Text, JsonDocument? Document) : IDisposable
		{
			public void Dispose() => Document?.Dispose();
		}
	}
}
