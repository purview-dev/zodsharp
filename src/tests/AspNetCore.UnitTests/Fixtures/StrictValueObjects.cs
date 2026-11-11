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

namespace ZodSharp.AspNetCore.Fixtures
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

	/// <summary>A numeric scalar value object read by a strict converter.</summary>
	[Purview.ValueObjects.Serialization.Scalar]
	[ZodSchema]
	[JsonConverter(typeof(OrderQuantityJsonConverter))]
	public readonly partial record struct OrderQuantity
	{
		public OrderQuantity(int value) => Value = value;

		[Range(1, 100)]
		public int Value { get; }
	}

	[ZodSchema]
	public sealed partial record PlaceOrderRequest(EmailAddress CustomerEmail, OrderQuantity Quantity);

	[ZodSchema]
	public sealed partial record ShippingCustomer(EmailAddress Email);

	[ZodSchema]
	public sealed partial record ShippingLine(OrderQuantity Quantity);

	/// <summary>The nested and collection cases: <c>Customer.Email.Value</c> and <c>Lines[0].Quantity.Value</c>.</summary>
	[ZodSchema]
	public sealed partial record ShippingOrder(ShippingCustomer Customer, IReadOnlyList<ShippingLine> Lines);

	/// <summary>
	/// What a Purview.ValueObjects strict converter does: create the value, and turn a validation failure into a
	/// <see cref="JsonException"/> carrying the <see cref="ZodException"/>, so the serializer records where it was read.
	/// </summary>
	static class StrictValueObject
	{
		public static T Create<T>(T candidate, Func<T, ValidationResult<T>> validate)
		{
			var result = validate(candidate);
			return result.IsSuccess
				? result.Value
				: throw new JsonException(
					$"Cannot deserialize {typeof(T).Name}: the value failed validation.",
					new ZodException(result.Errors)
				);
		}
	}

	public sealed class EmailAddressJsonConverter : JsonConverter<EmailAddress>
	{
		public override EmailAddress Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		) =>
			StrictValueObject.Create(new EmailAddress(reader.GetString() ?? string.Empty), static v =>
				EmailAddressSchema.Validate(v)
			);

		public override void Write(Utf8JsonWriter writer, EmailAddress value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Value);
	}

	public sealed class OrderQuantityJsonConverter : JsonConverter<OrderQuantity>
	{
		public override OrderQuantity Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		) =>
			StrictValueObject.Create(new OrderQuantity(reader.GetInt32()), static v => OrderQuantitySchema.Validate(v));

		public override void Write(Utf8JsonWriter writer, OrderQuantity value, JsonSerializerOptions options) =>
			writer.WriteNumberValue(value.Value);
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(PlaceOrderRequest))]
	[JsonSerializable(typeof(ShippingOrder))]
	public sealed partial class StrictValueObjectJsonContext : JsonSerializerContext;
}
