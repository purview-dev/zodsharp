using System.Text.Json;

namespace ZodSharp.Json;

/// <summary>
/// Cross-platform tests: C# (System.Text.Json) $lt;--$gt; TS (Zod).
///
/// Mirrors the Newtonsoft cross-platform tests but using System.Text.Json.
/// </summary>
public class SystemTextCrossPlatformTests
{
	static readonly string FixturesDir = Path.Combine("..", "..", "..", "..", "..", "..", "src", "ts", "fixtures");
	static readonly string OutputDir = Path.Combine(
		"..",
		"..",
		"..",
		"..",
		"..",
		"..",
		"src",
		"tests",
		"cross-platform",
		"output"
	);
	static readonly string ManifestPath = Path.Combine(FixturesDir, "manifest.json");

	static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	static readonly string[] InvalidKeys =
	[
		"invalidEmptyName",
		"invalidNegativeAge",
		"invalidOverMaxAge",
		"invalidBadEmail",
		"invalidMissingName",
	];
	static readonly string[] ValidKeys = ["valid", "validMinimal", "validMaxAge"];

	static readonly JsonSerializerOptions ManifestOptions = new() { PropertyNameCaseInsensitive = true };

	static CrossPlatformManifest LoadManifest()
	{
		var json = File.ReadAllText(ManifestPath);
		return JsonSerializer.Deserialize<CrossPlatformManifest>(json, ManifestOptions)!;
	}

	static IEnumerable<(string Name, string Json)> LoadAllFixtures()
	{
		foreach (var file in Directory.GetFiles(FixturesDir, "*.json"))
		{
			var name = Path.GetFileNameWithoutExtension(file);
			if (name == "manifest")
				continue;
			yield return (name, File.ReadAllText(file));
		}
	}

	[Test]
	[MethodDataSource(nameof(ValidFixtureNames))]
	public async Task SystemText_CanDeserializeAndValidate_ValidFixtureFromTS(string fixtureName)
	{
		var json = File.ReadAllText(Path.Combine(FixturesDir, $"{fixtureName}.json"));
		CrossPlatformUserSchema schema = new();

		var result = schema.DeserializeAndValidate(json, CamelCase);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Name).IsNotNull();
		await Assert.That(result.Value!.Name!.Length).IsGreaterThanOrEqualTo(1);
		await Assert.That(result.Value!.Age).IsGreaterThanOrEqualTo(0);
		await Assert.That(result.Value!.Age).IsLessThanOrEqualTo(120);
	}

	[Test]
	[MethodDataSource(nameof(InvalidFixtureNames))]
	public async Task SystemText_Rejects_InvalidFixtureFromTS(string fixtureName)
	{
		var json = File.ReadAllText(Path.Combine(FixturesDir, $"{fixtureName}.json"));
		CrossPlatformUserSchema schema = new();

		var result = schema.DeserializeAndValidate(json, CamelCase);

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task SystemText_ValidationOutcomes_MatchTSManifest()
	{
		var manifest = LoadManifest();

		foreach (var (name, json) in LoadAllFixtures())
		{
			CrossPlatformUserSchema schema = new();
			var result = schema.DeserializeAndValidate(json, CamelCase);
			var expectedValid = manifest[name].Valid;

			await Assert
				.That(result.IsSuccess)
				.IsEqualTo(expectedValid)
				.Because(
					$"fixture '{name}': C# result ({result.IsSuccess}) should match TS manifest ({expectedValid})"
				);
		}
	}

	[Test]
	public async Task SystemText_SerializesValidData_CanBeParsedByTS_Zod(CancellationToken cancellationToken)
	{
		// Serialize valid data using System.Text.Json, write to output dir for TS tests to consume
		CrossPlatformUserSchema schema = new();
		CrossPlatformUser user = new()
		{
			Name = "CSharp Export",
			Age = 42,
			Email = "csharp@example.com",
			Tags = ["systemtext", "export"],
		};

		var result = schema.ValidateAndSerialize(user, CamelCase);
		await Assert.That(result.IsSuccess).IsTrue();

		Directory.CreateDirectory(OutputDir);
		var outputPath = Path.Combine(OutputDir, "systemtext-valid.json");
		await File.WriteAllTextAsync(outputPath, result.Value, cancellationToken);

		// Verify the output is valid JSON with expected fields
		var writtenJson = await File.ReadAllTextAsync(outputPath, cancellationToken);
		await Assert.That(writtenJson).Contains("CSharp Export");
		await Assert.That(writtenJson).Contains("42");
		await Assert.That(writtenJson).Contains("csharp@example.com");
	}

	[Test]
	public async Task SystemText_RoundTrip_TSFixture_ToCSharp_ToJSON_BackToCSharp(CancellationToken cancellationToken)
	{
		var originalJson = await File.ReadAllTextAsync(Path.Combine(FixturesDir, "valid.json"), cancellationToken);
		CrossPlatformUserSchema schema = new();

		var firstResult = schema.DeserializeAndValidate(originalJson, CamelCase);
		await Assert.That(firstResult.IsSuccess).IsTrue();

		var serializeResult = schema.ValidateAndSerialize(firstResult.Value!, CamelCase);
		await Assert.That(serializeResult.IsSuccess).IsTrue();

		var secondResult = schema.DeserializeAndValidate(serializeResult.Value!, CamelCase);
		await Assert.That(secondResult.IsSuccess).IsTrue();
		await Assert.That(secondResult.Value!.Name).IsEqualTo(firstResult.Value!.Name);
		await Assert.That(secondResult.Value!.Age).IsEqualTo(firstResult.Value!.Age);
	}

	public static IEnumerable<string> ValidFixtureNames() => ValidKeys;

	public static IEnumerable<string> InvalidFixtureNames() => InvalidKeys;
}
