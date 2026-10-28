using System.Text.Json;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.JsonSchema;

/// <summary>
/// The string-format rules that gained a JSON Schema mapping, the vendor extensions rules record for hosts that
/// carry them (OpenAPI), and the runtime entry point the OpenAPI integration uses.
/// </summary>
public class ToJsonSchemaVendorExtensionTests
{
	[Test]
	[Arguments(IPAddressRuleType.IPv4, "ipv4")]
	[Arguments(IPAddressRuleType.IPv6, "ipv6")]
	public async Task ToJsonSchema_GivenIPAddressFamily_ExportsFormat(IPAddressRuleType ruleType, string format)
	{
		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().IP(ruleType));

		// Assert
		await Assert.That(jsonSchema.Format).IsEqualTo(format);
		await Assert.That(jsonSchema.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task ToJsonSchema_GivenAnyIPAddress_RecordsVendorExtensionAndUnsupported()
	{
		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().IP());

		// Assert
		await Assert.That(jsonSchema.Format).IsNull();
		await Assert.That(jsonSchema.GetVendorExtensions()["x-zod-ipVersion"]).IsEqualTo("any");
		await Assert.That(jsonSchema.UnsupportedRules!).Contains(nameof(IPAddressRule));
	}

	[Test]
	public async Task ToJsonSchema_GivenCidrAndJwt_RecordVendorExtensionsAndStayUnsupported()
	{
		// Act
		var cidr = Z.ToJsonSchema(Z.String().Cidr(CidrRuleType.IPv6));
		var jwt = Z.ToJsonSchema(Z.String().JWT());

		// Assert
		await Assert.That(cidr.GetVendorExtensions()["x-zod-cidr"]).IsEqualTo("v6");
		await Assert.That(cidr.UnsupportedRules!).Contains(nameof(CidrRule));
		await Assert.That(jwt.GetVendorExtensions()["x-zod-jwt"] is true).IsTrue();
		await Assert.That(jwt.UnsupportedRules!).Contains(nameof(JWTRule));
	}

	[Test]
	public async Task ToJsonSchema_GivenTemporalStringRules_ExportsFormatsAndPatterns()
	{
		// Act
		var date = Z.ToJsonSchema(Z.String().Date());
		var dateTime = Z.ToJsonSchema(Z.String().Datetime());
		var time = Z.ToJsonSchema(Z.String().Time());
		var duration = Z.ToJsonSchema(Z.String().Duration());

		// Assert
		await Assert.That(date.Format).IsEqualTo("date");
		await Assert.That(dateTime.Format).IsEqualTo("date-time");
		await Assert.That(dateTime.Pattern).IsEqualTo(DatetimeStringRule.JsonSchemaPattern);
		await Assert.That(time.Format).IsNull();
		await Assert.That(time.Pattern).IsEqualTo(TimeStringRule.JsonSchemaPattern);
		await Assert.That(time.GetVendorExtensions()["x-zod-format"]).IsEqualTo("time");
		await Assert.That(duration.Format).IsEqualTo("duration");
	}

	[Test]
	public async Task ToJsonSchema_GivenBase64String_ExportsContentEncoding()
	{
		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().Base64String());

		// Assert
		await Assert.That(jsonSchema.ContentEncoding).IsEqualTo("base64");
	}

	[Test]
	public async Task ToJsonSchema_GivenLiteralRules_RecordsTheLiteral()
	{
		// Act
		var startsWith = Z.ToJsonSchema(Z.String().StartsWith("a.b"));
		var uuid = Z.ToJsonSchema(Z.String().UUID(UuidVersion.V7));

		// Assert
		await Assert.That(startsWith.Pattern).IsEqualTo("^a\\.b");
		await Assert.That(startsWith.GetVendorExtensions()["x-zod-startsWith"]).IsEqualTo("a.b");
		await Assert.That(uuid.GetVendorExtensions()["x-zod-uuidVersion"]).IsEqualTo(7);
	}

	[Test]
	public async Task ToJsonSchema_GivenVendorExtensions_DoesNotSerializeThem()
	{
		// Arrange
		var jsonSchema = Z.ToJsonSchema(Z.String().StartsWith("ab"));

		// Act
		var json = JsonSerializer.Serialize(jsonSchema);

		// Assert - the exported JSON Schema is unchanged; only hosts that read the extensions see them.
		await Assert.That(json).DoesNotContain("x-zod-startsWith");
		await Assert.That(json).DoesNotContain("VendorExtensions");
	}

	[Test]
	public async Task SetVendorExtension_GivenNameWithoutPrefix_Throws()
	{
		// Arrange
		JsonSchemaDefinition schema = new();

		// Act
		void Act() => schema.SetVendorExtension("zod-thing", true);

		// Assert
		await Assert.That(Act).Throws<ArgumentException>();
	}

	[Test]
	public async Task ToJsonSchema_GivenDiscriminatedUnion_ExportsOneOfInDeclarationOrder()
	{
		// Arrange
		var schema = Z.DiscriminatedUnion("kind")
			.Option("square", Z.Object().Field("kind", Z.Literal("square")).Build())
			.Option("circle", Z.Object().Field("kind", Z.Literal("circle")).Build())
			.Build();

		// Act
		var jsonSchema = Z.ToJsonSchema(schema);

		// Assert
		await Assert.That(jsonSchema.AnyOf).IsNull();
		await Assert.That(jsonSchema.OneOf!.Count).IsEqualTo(2);
		await Assert.That(jsonSchema.GetVendorExtensions()["x-zod-discriminator"]).IsEqualTo("kind");
		await Assert.That(jsonSchema.OneOf[0].GetVendorExtensions()["x-zod-discriminatorValue"]).IsEqualTo("square");
		await Assert.That(jsonSchema.OneOf[1].GetVendorExtensions()["x-zod-discriminatorValue"]).IsEqualTo("circle");
	}

	[Test]
	public async Task ConvertUntyped_GivenRegisteredRuntimeValidator_UnwrapsTheAdapters()
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ZodSchemaValidator<string>(Z.String().Min(3)));
		factory.TryGetUntypedValidator(typeof(string), out var validator);

		// Act
		var jsonSchema = ToJsonSchemaConverter.ConvertUntyped(validator!);

		// Assert
		await Assert.That(jsonSchema.Type).IsEqualTo("string");
		await Assert.That(jsonSchema.MinLength).IsEqualTo(3);
	}
}
