using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZodSharp.Core;

public partial class ZodJsonExceptionsTests
{
	const string MessageFormat = "Field '{displayName}' must be a valid email address.";

	[Test]
	public async Task TryGetErrors_GivenConverterRejectionInNestedMember_LocatesErrorsUnderMemberNames()
	{
		// Arrange
		var exception = Deserialize( /*lang=json,strict*/
			"""{"customer":{"email_address":"x"}}"""
		);

		// Act
		var found = ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		await Assert.That(found).IsTrue();
		var error = errors.Single();
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("Customer.Email.Value");
		await Assert.That(error.Message).IsEqualTo("Field 'Email' must be a valid email address.");
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.DisplayName)).IsEqualTo("Email");
	}

	[Test]
	public async Task TryGetErrors_GivenConverterRejection_KeepsEveryOtherField()
	{
		// Arrange
		var exception = Deserialize( /*lang=json,strict*/
			"""{"customer":{"email_address":"x"}}"""
		);

		// Act
		ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		var error = errors.Single();
		var original = Rejection();
		await Assert.That(error.Code).IsEqualTo(original.Code);
		await Assert.That(error.Category).IsEqualTo(original.Category);
		await Assert.That(error.Origin).IsEqualTo(original.Origin);
		await Assert.That(error.MessageFormat).IsEqualTo(original.MessageFormat);
		await Assert.That(error.Minimum).IsEqualTo(original.Minimum);
		await Assert.That(error.Maximum).IsEqualTo(original.Maximum);
		await Assert.That(error.Inclusive).IsEqualTo(original.Inclusive);
		await Assert.That(error.Parameters!.Get<string>("pattern")).IsEqualTo("email");
		await Assert
			.That(
				error.Parameters!.Get<ImmutableArray<ImmutableArray<ValidationError>>>(ZodIssueParameters.UnionErrors)
			)
			.IsEquivalentTo(
				original.Parameters!.Get<ImmutableArray<ImmutableArray<ValidationError>>>(
					ZodIssueParameters.UnionErrors
				)
			);
	}

	[Test]
	[Arguments( /*lang=json,strict*/
		"""{"customers":[{},{"email_address":"x"}]}""",
		"Customers[1].Email.Value"
	)]
	[Arguments( /*lang=json,strict*/
		"""{"contact.email":"x"}""",
		"Dotted.Value"
	)]
	[Arguments( /*lang=json,strict*/
		"""{"map":{"en":"x"}}""",
		"Map[en].Value"
	)]
	[Arguments( /*lang=json,strict*/
		"""{"rejected":"x"}""",
		"Rejected.Value"
	)]
	public async Task TryGetErrors_GivenSerializerPathShapes_MapsThemToMemberPaths(string json, string expectedPath)
	{
		// Arrange
		var exception = Deserialize(json);

		// Act
		ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		await Assert.That(errors.Single().FullPath.ToDottedString()).IsEqualTo(expectedPath);
	}

	[Test]
	public async Task TryGetErrors_GivenDictionaryEntry_UsesAKeySegment()
	{
		// Arrange
		var exception = Deserialize( /*lang=json,strict*/
			"""{"map":{"en":"x"}}"""
		);

		// Act
		ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		await Assert.That(errors.Single().FullPath[1].Kind).IsEqualTo(ZodPathSegmentKind.Key);
	}

	[Test]
	public async Task TryGetErrors_GivenNoRootContract_KeepsTheJsonNames()
	{
		// Arrange
		var exception = Deserialize( /*lang=json,strict*/
			"""{"customer":{"email_address":"x"}}"""
		);

		// Act
		var found = ZodJsonExceptions.TryGetErrors(exception, rootTypeInfo: null, out var errors);

		// Assert
		await Assert.That(found).IsTrue();
		var error = errors.Single();
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("customer.email_address.Value");
		await Assert.That(error.Message).IsEqualTo("Field 'email_address' must be a valid email address.");
	}

	[Test]
	public async Task TryGetErrors_GivenWrappedChain_UsesTheSerializersPath()
	{
		// Arrange: the shape minimal-API binding hands the exception handler.
		InvalidOperationException wrapped = new(
			"Failed to read parameter.",
			Deserialize( /*lang=json,strict*/
				"""{"rejected":"x"}"""
			)
		);

		// Act
		var found = ZodJsonExceptions.TryGetErrors(wrapped, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(errors.Single().FullPath.ToDottedString()).IsEqualTo("Rejected.Value");
	}

	[Test]
	public async Task TryGetErrors_GivenErrorBelowTheValueObjectsOwnMember_KeepsItsDisplayName()
	{
		// Arrange
		var nested = Rejection() with
		{
			FullPath = ZodPath.From(ZodPathSegment.Member("Inner"), ZodPathSegment.Member("Value")),
		};
		JsonException exception = new("Rejected.", new ZodException([nested]));

		// Act
		var errors = ZodJsonExceptions.Reroot(
			[nested],
			ZodPath.ParseJsonPath("$.customer.email_address"),
			RejectionJsonContext.Default.Envelope
		);

		// Assert
		await Assert.That(ZodJsonExceptions.TryGetErrors(exception, null, out _)).IsTrue();
		var error = errors.Single();
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("Customer.Email.Inner.Value");
		await Assert.That(error.Message).IsEqualTo(nested.Message);
	}

	[Test]
	public async Task TryGetErrors_GivenBareZodException_ReturnsFalse()
	{
		// Arrange
		ZodException exception = new([Rejection()]);

		// Act
		var found = ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out var errors);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(errors.IsDefault).IsTrue();
	}

	[Test]
	public async Task TryGetErrors_GivenJsonExceptionWithoutZodException_ReturnsFalse()
	{
		// Arrange
		JsonException exception = new("Unexpected end.", "$.customer", 0, 12);

		// Act
		var found = ZodJsonExceptions.TryGetErrors(exception, RejectionJsonContext.Default.Envelope, out _);

		// Assert
		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TryReroot_GivenConverterRejection_ReturnsZodExceptionWithRerootedErrors()
	{
		// Arrange
		var exception = Deserialize( /*lang=json,strict*/
			"""{"rejected":"x"}"""
		);

		// Act
		var found = ZodJsonExceptions.TryReroot(exception, RejectionJsonContext.Default.Envelope, out var rerooted);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(rerooted!.Errors.Single().FullPath.ToDottedString()).IsEqualTo("Rejected.Value");
		await Assert
			.That(rerooted.Errors.Single().Message)
			.IsEqualTo("Field 'Rejected' must be a valid email address.");
	}

	static JsonException Deserialize(string json)
	{
		try
		{
			JsonSerializer.Deserialize(json, RejectionJsonContext.Default.Envelope);
		}
		catch (JsonException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("The document was expected to be rejected.");
	}

	/// <summary>The error a scalar value object's schema reports for its own <c>Value</c>.</summary>
	static ValidationError Rejection()
	{
		Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
		{
			[ZodIssueParameters.DisplayName] = "Value",
			["pattern"] = "email",
			[ZodIssueParameters.UnionErrors] = ImmutableArray.Create(
				ImmutableArray.Create(ValidationError.Create("invalid_type", "Expected string", ZodPath.Root))
			),
		};

		var error = ValidationError.Create(
			"invalid_string",
			"Field 'Value' must be a valid email address.",
			ZodPath.Member("Value"),
			parameters,
			origin: "string",
			minimum: 3,
			maximum: 254,
			inclusive: true,
			category: ZodIssueCategories.InvalidFormat
		);

		return error with
		{
			MessageFormat = MessageFormat,
		};
	}

	public sealed class Envelope
	{
		public Customer? Customer { get; set; }

		public List<Customer>? Customers { get; init; }

		[JsonPropertyName("contact.email")]
		public RejectedValue? Dotted { get; set; }

		public Dictionary<string, RejectedValue>? Map { get; init; }

		public RejectedValue? Rejected { get; set; }
	}

	public sealed class Customer
	{
		[JsonPropertyName("email_address")]
		public RejectedValue? Email { get; set; }
	}

	/// <summary>A value object whose strict converter rejects every value, as Purview.ValueObjects will.</summary>
	[JsonConverter(typeof(RejectingConverter))]
	public readonly record struct RejectedValue(string Value);

	public sealed class RejectingConverter : JsonConverter<RejectedValue>
	{
		public override RejectedValue Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		) =>
			throw new JsonException(
				"Cannot deserialize RejectedValue: the value failed validation.",
				new ZodException([Rejection()])
			);

		public override void Write(Utf8JsonWriter writer, RejectedValue value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Value);
	}

	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	[JsonSerializable(typeof(Envelope))]
	internal sealed partial class RejectionJsonContext : JsonSerializerContext;
}
