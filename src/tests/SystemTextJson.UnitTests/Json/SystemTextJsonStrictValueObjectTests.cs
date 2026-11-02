using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// A value rejected by a strict value object converter while it is read (<c>JsonException(inner: ZodException)</c>)
/// is reported as its ZodSharp errors under the member that held it, not as <c>invalid_json</c>.
/// </summary>
public partial class SystemTextJsonStrictValueObjectTests
{
	const string InvalidEmailBody = /*lang=json,strict*/
		"""{"customerEmail":"not-an-email","quantity":5}""";

	[Test]
	public async Task DeserializeAndValidateAsync_GivenStrictConverterRejection_ReturnsTheRerootedErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		PassThroughSchema<PlaceOrderRequest> schema = new();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(InvalidEmailBody));

		// Act
		var result = await schema.DeserializeAndValidateAsync(
			stream,
			StrictJsonContext.Default.PlaceOrderRequest,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_string");
		await Assert.That(error.Origin).IsEqualTo("string");
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("CustomerEmail.Value");
		await Assert.That(error.Message).IsEqualTo("Field 'CustomerEmail' must be a valid email address.");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenStrictConverterRejection_ReturnsTheRerootedErrors()
	{
		// Arrange
		PassThroughSchema<PlaceOrderRequest> schema = new();

		// Act
		var result = schema.DeserializeAndValidate(InvalidEmailBody, StrictJsonContext.Default.PlaceOrderRequest);

		// Assert
		await Assert.That(result.Errors.Single().FullPath.ToDottedString()).IsEqualTo("CustomerEmail.Value");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenStrictConverterRejectionInsideValidatingConverter_ReportsTheFullPath()
	{
		// Arrange: the validating converter reads its value through a second serializer call, so the location is joined.
		PassThroughSchema<Wrapper> schema = new();
		JsonSerializerOptions options = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
		options.Converters.Add(new PassThroughSchema<PlaceOrderRequest>().CreateValidatingConverter());
		var typeInfo = (JsonTypeInfo<Wrapper>)options.TypeInfoResolver.GetTypeInfo(typeof(Wrapper), options)!;

		// Act
		var result = schema.DeserializeAndValidate( /*lang=json,strict*/
			"""{"Order":{"CustomerEmail":"not-an-email","Quantity":5}}""",
			typeInfo
		);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_string");
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("Order.CustomerEmail.Value");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenJsonExceptionWithoutZodErrors_StillReportsInvalidJson()
	{
		// Arrange
		PassThroughSchema<PlaceOrderRequest> schema = new();

		// Act
		var result = schema.DeserializeAndValidate(
			"""{"customerEmail":""",
			StrictJsonContext.Default.PlaceOrderRequest
		);

		// Assert
		await Assert.That(result.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	public sealed record Wrapper(PlaceOrderRequest? Order);

	public sealed record PlaceOrderRequest(EmailAddress CustomerEmail, int Quantity);

	[JsonConverter(typeof(EmailAddressConverter))]
	public readonly record struct EmailAddress(string Value);

	/// <summary>What a Purview.ValueObjects strict converter does when the value object's schema rejects the value.</summary>
	public sealed class EmailAddressConverter : JsonConverter<EmailAddress>
	{
		public override EmailAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString() ?? string.Empty;
			if (value.Contains('@', StringComparison.Ordinal))
				return new(value);

			Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
			{
				[ZodIssueParameters.DisplayName] = "Value",
			};
			var error = ValidationError.Create(
				"invalid_string",
				"Field 'Value' must be a valid email address.",
				ZodPath.Member("Value"),
				parameters,
				origin: "string",
				category: ZodIssueCategories.InvalidFormat
			) with
			{
				MessageFormat = "Field '{displayName}' must be a valid email address.",
			};

			throw new JsonException(
				"Cannot deserialize EmailAddress: the value failed validation.",
				new ZodException([error])
			);
		}

		public override void Write(Utf8JsonWriter writer, EmailAddress value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Value);
	}

	sealed class PassThroughSchema<T> : IZodSchema<T, T>
	{
		public ValidationResult<T> Validate(T value) => ValidationResult<T>.Success(value);

		public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
			new(Validate(value));
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(PlaceOrderRequest))]
	internal sealed partial class StrictJsonContext : JsonSerializerContext;
}
