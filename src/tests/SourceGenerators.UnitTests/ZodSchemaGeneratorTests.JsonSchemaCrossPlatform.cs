using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecurityDriven;
using ZodSharp.JsonSchema;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Produces the shared Guid/UUID parity artifacts consumed by the TypeScript cross-platform test
/// (<c>tests/ts/guid-uuid-parity.test.ts</c>). Each artifact records a schema exported by the generator
/// (<c>BuildJsonSchema</c>) alongside payloads and the outcome of the generated C# validator, so the TS side
/// can prove that importing the exported schema with Zod's <c>z.fromJSONSchema</c> agrees with C#.
/// </summary>
/// <remarks>
/// The producer lives here because the generator test harness can compile a <c>[ZodSchema]</c> model — and
/// the stand-in <c>Purview.ValueObjects</c> scalar contracts — without the repository referencing the
/// value-objects package. FastGuid is used for the time-bound identifier values the consumer generates;
/// those are deliberately not RFC UUIDs.
/// </remarks>
public partial class ZodSchemaGeneratorTests
{
	const string ParityOutputRelativePath = "src/tests/cross-platform/guid-parity";

	static readonly JsonSerializerOptions ParitySerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
	};

	const string NonRfcGuid = "9f41585f-31c5-fc4b-0d04-b1fe5b523139";
	const string PlainGuid = "1025f652-88f2-4b16-b4e1-a82d3b5f47ce";
	const string V4Uuid = "550e8400-e29b-41d4-a716-446655440000";
	const string V7Uuid = "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c";
	const string NilUuid = "00000000-0000-0000-0000-000000000000";

	/// <summary>
	/// The models compiled for the parity run: a Guid holder (plain, optional, non-empty, list, scalar and
	/// scalar list), a UUID holder (versionless, V4, V7), and a bare Guid-backed scalar.
	/// </summary>
	const string GuidParitySource =
		"using System.Linq;\nusing System.Text.Json;\nusing System.Text.Json.Serialization;\n"
		+ AutomaticScalarPrelude
		+ """
			namespace Testing
			{
				/// <summary>Reads/writes a Guid-backed scalar as its canonical string wire form.</summary>
				public sealed class GuidScalarJsonConverterFactory : JsonConverterFactory
				{
					public override bool CanConvert(Type typeToConvert) =>
						typeToConvert.GetInterfaces().Any(i =>
							i.IsGenericType
							&& i.GetGenericTypeDefinition() == typeof(Purview.ValueObjects.IScalarValueObject<,>)
							&& i.GetGenericArguments()[1] == typeof(Guid));

					public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
						(JsonConverter)Activator.CreateInstance(
							typeof(GuidScalarJsonConverter<>).MakeGenericType(typeToConvert)
						)!;
				}

				public sealed class GuidScalarJsonConverter<TSelf> : JsonConverter<TSelf>
					where TSelf : Purview.ValueObjects.IScalarValueObject<TSelf, Guid>
				{
					public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
					{
						var value = reader.GetGuid();
						var boxed = Activator.CreateInstance(typeToConvert)!;
						typeToConvert.GetProperty("Value")!.SetValue(boxed, value);
						return (TSelf)boxed;
					}

					public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options) =>
						writer.WriteStringValue(value.Value);
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[ZodSchema]
				[NonSentinel]
				[JsonConverter(typeof(GuidScalarJsonConverterFactory))]
				public partial record struct TenantId : Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
				{
					public Guid Value { get; init; }
				}

				[ZodSchema]
				public class GuidHolder
				{
					public Guid Plain { get; set; }

					public Guid? Optional { get; set; }

					[NonSentinel]
					public Guid NonEmpty { get; set; }

					public System.Collections.Generic.List<Guid> PlainGuids { get; set; } = new();

					public TenantId Tenant { get; set; }

					public System.Collections.Generic.List<TenantId> Tenants { get; set; } = new();
				}

				[ZodSchema]
				public class UuidHolder
				{
					[UUID]
					public string Any { get; set; } = string.Empty;

					[UUID(UuidVersion.V4)]
					public string V4 { get; set; } = string.Empty;

					[UUID(UuidVersion.V7)]
					public string V7 { get; set; } = string.Empty;
				}
			}
			""";

	[Test]
	public async Task JsonSchema_GivenGeneratedGuidSchemas_WritesZodParityArtifacts(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — real time-bound identifier values. FastGuid's database generators are not RFC UUIDs, so
		// they must survive the exported schema.
		var sqlServerGuid = FastGuid.NewSqlServerGuid();
		var postgreSqlGuid = FastGuid.NewPostgreSqlGuid();
		var v7Guid = FastGuid.UUID.CreateVersion7();

		using var driverResult = await GenerateAsync(
			GuidParitySource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var outputDir = Path.Combine("..", "..", "..", "..", "..", "..", ParityOutputRelativePath);

		// Act & Assert
		await WriteParityArtifactAsync(
			assembly,
			"GuidHolder",
			[
				("reproduction", GuidHolderPayload(NonRfcGuid, PlainGuid), true),
				("fastguid-sqlserver", GuidHolderPayload($"{sqlServerGuid:D}", $"{sqlServerGuid:D}"), true),
				("fastguid-postgresql", GuidHolderPayload($"{postgreSqlGuid:D}", $"{postgreSqlGuid:D}"), true),
				("fastguid-v7", GuidHolderPayload($"{v7Guid:D}", $"{v7Guid:D}"), true),
				("empty-plain", GuidHolderPayload(NilUuid, PlainGuid), true),
				("empty-nonsentinel", GuidHolderPayload(PlainGuid, NilUuid), false),
				("malformed", GuidHolderPayload("not-a-guid", PlainGuid), false),
			],
			outputDir,
			cancellationToken
		);

		await WriteParityArtifactAsync(
			assembly,
			"UuidHolder",
			[
				("versionless-accepts-v4", UuidHolderPayload(V4Uuid, V4Uuid, V7Uuid), true),
				("versionless-accepts-v7", UuidHolderPayload(V7Uuid, V4Uuid, V7Uuid), true),
				("non-rfc-rejected-by-versionless", UuidHolderPayload(NonRfcGuid, V4Uuid, V7Uuid), false),
				("v4-mismatch", UuidHolderPayload(V4Uuid, V7Uuid, V7Uuid), false),
				("v7-mismatch", UuidHolderPayload(V7Uuid, V4Uuid, V4Uuid), false),
			],
			outputDir,
			cancellationToken
		);

		await WriteParityArtifactAsync(
			assembly,
			"TenantId",
			[
				("reproduction", JsonString(NonRfcGuid), true),
				("fastguid", JsonString($"{sqlServerGuid:D}"), true),
				("empty", JsonString(NilUuid), false),
				("malformed", JsonString("not-a-guid"), false),
			],
			outputDir,
			cancellationToken
		);
	}

	static string GuidHolderPayload(string plain, string nonEmpty) =>
			/*lang=json,strict*/$$"""
			{
				"plain": "{{plain}}",
				"nonEmpty": "{{nonEmpty}}",
				"plainGuids": ["{{plain}}"],
				"tenant": "{{PlainGuid}}",
				"tenants": ["{{PlainGuid}}"]
			}
			""";

	static string UuidHolderPayload(string any, string v4, string v7) =>
			/*lang=json,strict*/$$"""
			{
				"any": "{{any}}",
				"v4": "{{v4}}",
				"v7": "{{v7}}"
			}
			""";

	static string JsonString(string value) => $"\"{value}\"";

	static async Task WriteParityArtifactAsync(
		Assembly assembly,
		string typeName,
		IReadOnlyList<(string Name, string Value, bool Valid)> cases,
		string outputDir,
		CancellationToken cancellationToken
	)
	{
		var schemaType = assembly.GetType($"Testing.{typeName}Schema")!;
		var modelType = assembly.GetType($"Testing.{typeName}")!;
		var schema = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;
		var validate = schemaType.GetMethod("Validate")!;
		JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

		List<ParityCase> results = [with(cases.Count)];
		foreach (var (name, value, expected) in cases)
		{
			var actual = ComputeValidity(modelType, validate, value, jsonOptions);
			await Assert.That(actual).IsEqualTo(expected).Because($"{typeName}/{name}");
			results.Add(new ParityCase(name, value, actual));
		}

		ParityArtifact artifact = new(typeName, schema, results);
		var json = JsonSerializer.Serialize(artifact, ParitySerializerOptions);

		Directory.CreateDirectory(outputDir);
		await File.WriteAllTextAsync(Path.Combine(outputDir, $"{typeName}.json"), json, cancellationToken);
	}

	static bool ComputeValidity(Type modelType, MethodInfo validate, string value, JsonSerializerOptions jsonOptions)
	{
		try
		{
			var model = JsonSerializer.Deserialize(value, modelType, jsonOptions);
			if (model is null)
				return false;

			var result = validate.Invoke(null, [model])!;
			return (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;
		}
		catch (JsonException)
		{
			// A payload the C# wire contract cannot even deserialize is rejected end to end.
			return false;
		}
	}

	sealed record ParityCase(string Name, string Value, bool Valid);

	sealed record ParityArtifact(string Name, JsonSchemaDefinition Schema, List<ParityCase> Cases);
}
