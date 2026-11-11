using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZodSharp.Core;

// A stand-in for the Purview.ValueObjects [Scalar] attribute, which the generator recognizes by name and namespace, so
// these fixtures are shaped exactly like value objects without referencing that package.
#pragma warning disable IDE0130 // The stand-in must live in the namespace the generator recognizes.
namespace Purview.ValueObjects.Serialization
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class ScalarAttribute(string propertyName = "Value") : Attribute
	{
		public string PropertyName { get; } = propertyName;
	}
}

#pragma warning restore IDE0130

namespace ZodSharp.Http.Fixtures
{
	/// <summary>A scalar value object read by a strict converter, which rejects an invalid value while reading.</summary>
	[Purview.ValueObjects.Serialization.Scalar]
	[ZodSchema]
	[JsonConverter(typeof(EmailAddressJsonConverter))]
	public readonly partial record struct EmailAddress
	{
		public EmailAddress(string value) => Value = value;

		[Required]
		[EmailAddress]
		public string Value { get; }
	}

	[ZodSchema]
	public sealed partial record PlaceOrderRequest(EmailAddress CustomerEmail, int Quantity);

	/// <summary>
	/// What a Purview.ValueObjects strict converter does: turn a validation failure into a <see cref="JsonException"/>
	/// carrying the <see cref="ZodException"/>, so the serializer records where it was read.
	/// </summary>
	public sealed class EmailAddressJsonConverter : JsonConverter<EmailAddress>
	{
		public override EmailAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var result = EmailAddressSchema.Validate(new EmailAddress(reader.GetString() ?? string.Empty));
			return result.IsSuccess
				? result.Value
				: throw new JsonException(
					"Cannot deserialize EmailAddress: the value failed validation.",
					new ZodException(result.Errors)
				);
		}

		public override void Write(Utf8JsonWriter writer, EmailAddress value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Value);
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(PlaceOrderRequest))]
	sealed partial class StrictValueObjectJsonContext : JsonSerializerContext;
}
