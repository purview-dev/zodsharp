using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using ZodSharp.AspNetCore.ClientValidation;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Blazor.Ssr;

/// <summary>
/// The C# producer of the client-validator parity fixtures: for each ZodSharp rule, the client rule the server emits
/// (through <see cref="ZodClientValidationRules"/>) and the C# rule's verdict on a set of inputs. The vitest suite
/// <c>tests/ts/client-validators.test.ts</c> runs <c>zodsharp-validation.js</c> against the same inputs and fails when
/// the JavaScript validators disagree with C#.
/// </summary>
public class ClientRuleParityFixtureTests
{
	const string FixtureRelativePath = "src/tests/cross-platform/client-rules";

	static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

	const string V4 = "550e8400-e29b-41d4-a716-446655440000";
	const string V7 = "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c";

	[Test]
	public async Task ClientRules_GivenEveryMappedRule_WritesParityFixtures(CancellationToken cancellationToken)
	{
		// Arrange
		var outputDirectory = Path.GetFullPath(
			Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", FixtureRelativePath)
		);
		Directory.CreateDirectory(outputDirectory);
		foreach (var stale in Directory.EnumerateFiles(outputDirectory, "*.json"))
			File.Delete(stale);

		List<Fixture> fixtures =
		[
			Text(
				"starts-with",
				new StartsWithAttribute("ab"),
				new StartsWithRule("ab"),
				"abc",
				"ab",
				"xab",
				"Abc",
				"a"
			),
			Text(
				"starts-with-ignore-case",
				new StartsWithAttribute("ab", StringComparison.OrdinalIgnoreCase),
				new StartsWithRule("ab", StringComparison.OrdinalIgnoreCase),
				"ABc",
				"aB",
				"ba"
			),
			Text("ends-with", new EndsWithAttribute("yz"), new EndsWithRule("yz"), "xyz", "yz", "yzx", "XYZ"),
			Text(
				"ends-with-ignore-case",
				new EndsWithAttribute("yz", StringComparison.OrdinalIgnoreCase),
				new EndsWithRule("yz", StringComparison.OrdinalIgnoreCase),
				"XYZ",
				"zy"
			),
			Text("includes", new IncludesAttribute("mid"), new IncludesRule("mid"), "amidb", "mid", "MID", "mi d"),
			Text(
				"uuid",
				new UUIDAttribute(),
				new UUIDRule(),
				V4,
				V4.ToUpperInvariant(),
				V7,
				"00000000-0000-0000-0000-000000000000",
				"ffffffff-ffff-ffff-ffff-ffffffffffff",
				"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
				"550e8400-e29b-01d4-a716-446655440000",
				"550e8400-e29b-91d4-a716-446655440000",
				"550e8400-e29b-41d4-c716-446655440000",
				"550e8400e29b41d4a716446655440000",
				"550e8400-e29b-41d4-a716-44665544000g",
				"550e8400-e29b-41d4-a716-4466554400001",
				" 550e8400-e29b-41d4-a716-44665544000"
			),
			Text("uuid-v7", new UUIDAttribute(UuidVersion.V7), new UUIDRule(UuidVersion.V7), V7, V4),
			Text("cuid2", new Cuid2Attribute(), new Cuid2Rule(), "tz4a98xxat96iws9zmbrgj3a", "TZ4A98", "a-b", "abc"),
			Text(
				"ulid",
				new ULIDAttribute(),
				new ULIDRule(),
				"01ARZ3NDEKTSV4RRFFQ69G5FAV",
				"01arz3ndektsv4rrffq69g5fav",
				"81ARZ3NDEKTSV4RRFFQ69G5FAV",
				"01ARZ3NDEKTSV4RRFFQ69G5FAI",
				"01ARZ3NDEKTSV4RRFFQ69G5FAL",
				"01ARZ3NDEKTSV4RRFFQ69G5FAO",
				"01ARZ3NDEKTSV4RRFFQ69G5FAU",
				"01ARZ3NDEKTSV4RRFFQ69G5FA"
			),
			Text(
				"ip-any",
				new IPAddressAttribute(),
				new IPAddressRule(IPAddressRuleType.Any),
				"192.168.0.1",
				"0.0.0.0",
				"255.255.255.255",
				"256.1.1.1",
				"1.2.3.4.5",
				"1..2.3",
				"abc",
				"::1",
				"::",
				"2001:db8::1",
				"2001:0db8:0000:0000:0000:ff00:0042:8329",
				"::ffff:192.168.0.1",
				"1::2::3",
				"12345::",
				"2001:db8:::1",
				"1:2:3:4:5:6:7:8:9",
				"fe80::1%2",
				"127.1",
				"1",
				"0x7f.0.0.1",
				"010.0.0.1",
				"09.0.0.1"
			),
			Text(
				"ip-v4",
				new IPAddressAttribute(IPAddressRuleType.IPv4),
				new IPAddressRule(IPAddressRuleType.IPv4),
				"10.1.2.3",
				"::1"
			),
			Text(
				"ip-v6",
				new IPAddressAttribute(IPAddressRuleType.IPv6),
				new IPAddressRule(IPAddressRuleType.IPv6),
				"10.1.2.3",
				"::1"
			),
			Text(
				"cidr-any",
				new CidrAttribute(),
				new CidrRule(CidrRuleType.Any),
				"10.0.0.0/8",
				"10.0.0.0/0",
				"10.0.0.0/32",
				"10.0.0.0/33",
				"2001:db8::/32",
				"2001:db8::/128",
				"2001:db8::/129",
				"10.0.0.0",
				"/8",
				"10.0.0.0/",
				"10.0.0.0/+8",
				"10.0.0.0/ 8",
				"10.0.0.0/8/8",
				"300.0.0.0/8"
			),
			Text(
				"cidr-v4",
				new CidrAttribute(CidrRuleType.IPv4),
				new CidrRule(CidrRuleType.IPv4),
				"10.0.0.0/8",
				"::/0"
			),
			Text(
				"cidr-v6",
				new CidrAttribute(CidrRuleType.IPv6),
				new CidrRule(CidrRuleType.IPv6),
				"10.0.0.0/8",
				"::/0"
			),
			Text("regex-partial", new RegexAttribute("\\d{3}"), new RegexRule("\\d{3}"), "ab123cd", "123", "ab12"),
			Text(
				"regex-anchored",
				new RegexAttribute("^[a-z]+\\d$"),
				new RegexRule("^[a-z]+\\d$"),
				"abc1",
				"ABC1",
				"abc"
			),
			Number("int", new IntAttribute(), new IntRule<double>(), "1", "-3", "0", "1.5", "-0.25", "1e3"),
			Number(
				"multiple-of-tenth",
				new MultipleOfAttribute(0.1),
				new MultipleOfRule<double>(0.1),
				"0.3",
				"1",
				"-0.2",
				"0.35",
				"0.05"
			),
			Number(
				"multiple-of-five",
				new MultipleOfAttribute(5),
				new MultipleOfRule<double>(5),
				"10",
				"-15",
				"0",
				"11",
				"2.5"
			),
			Number("min-value", new MinValueAttribute(3), new MinValueRule<double>(3), "3", "10", "2.99", "-4"),
			Number("max-value", new MaxValueAttribute(3), new MaxValueRule<double>(3), "3", "-10", "3.01"),
			Number("greater-than", new GreaterThanAttribute(3), new GreaterThanRule<double>(3), "3", "3.01", "2"),
			Number(
				"greater-than-or-equal",
				new GreaterThanOrEqualAttribute(3),
				new GreaterThanOrEqualRule<double>(3),
				"3",
				"2.5"
			),
			Number("less-than", new LessThanAttribute(3), new LessThanRule<double>(3), "3", "2.99", "4"),
			Number(
				"less-than-or-equal",
				new LessThanOrEqualAttribute(3),
				new LessThanOrEqualRule<double>(3),
				"3",
				"3.5"
			),
			Date(
				"date-min",
				new MinValueRule<DateTime>(new(2024, 1, 15)),
				"2024-01-15",
				"2024-01-15T00:00:01",
				"2024-01-14",
				"2024-01-14T23:59:59.9999999",
				"2025-06-01T10:30"
			),
			Date(
				"date-max",
				new MaxValueRule<DateTime>(new(2024, 1, 15, 12, 0, 0)),
				"2024-01-15T12:00",
				"2024-01-15T12:00:01",
				"2024-01-15"
			),
			Date(
				"date-gt",
				new GreaterThanRule<DateTime>(new(2024, 1, 15)),
				"2024-01-15",
				"2024-01-15T00:00:00.0000001"
			),
			Date("date-lt", new LessThanRule<DateTime>(new(2024, 1, 15)), "2024-01-15", "2024-01-14T23:59"),
		];

		// Act
		foreach (var fixture in fixtures)
		{
			await File.WriteAllTextAsync(
				Path.Combine(outputDirectory, fixture.Name + ".json"),
				JsonSerializer.Serialize(fixture, SerializerOptions),
				cancellationToken
			);
		}

		// Assert
		await Assert.That(Directory.EnumerateFiles(outputDirectory, "*.json").Count()).IsEqualTo(fixtures.Count);
		foreach (var fixture in fixtures)
		{
			await Assert.That(fixture.Rules).IsNotEmpty().Because($"{fixture.Name} maps to no client rule");
			await Assert
				.That(fixture.Cases.Any(static c => c.Valid))
				.IsTrue()
				.Because($"{fixture.Name} has no valid case");
			await Assert
				.That(fixture.Cases.Any(static c => !c.Valid))
				.IsTrue()
				.Because($"{fixture.Name} has no invalid case");
		}
	}

	internal sealed record FixtureRule(string Name, string Message, IReadOnlyDictionary<string, string> Params);

	internal sealed record FixtureCase(string Value, bool Valid);

	internal sealed record Fixture(string Name, IReadOnlyList<FixtureRule> Rules, IReadOnlyList<FixtureCase> Cases);

	static IReadOnlyList<FixtureRule> RulesOf(ValidationAttribute attribute) =>
		[
			.. ZodClientValidationRules
				.FromAttribute(attribute)
				.Select(static r => new FixtureRule(r.Name, r.Message, r.Parameters)),
		];

	static Fixture Text<TRule>(string name, ValidationAttribute attribute, TRule rule, params string[] values)
		where TRule : IValidationRule<string> =>
		new(name, RulesOf(attribute), [.. values.Select(value => new FixtureCase(value, rule.IsValid(value)))]);

	static Fixture Number<TRule>(string name, ValidationAttribute attribute, TRule rule, params string[] values)
		where TRule : IValidationRule<double> =>
		new(
			name,
			RulesOf(attribute),
			[
				.. values.Select(value => new FixtureCase(
					value,
					rule.IsValid(double.Parse(value, CultureInfo.InvariantCulture))
				)),
			]
		);

	static Fixture Date<TRule>(string name, TRule rule, params string[] values)
		where TRule : IValidationRule<DateTime>, IZodRule =>
		new(
			name,
			[
				.. ZodClientValidationRules
					.FromRule(rule, "Invalid date")
					.Select(static r => new FixtureRule(r.Name, r.Message, r.Parameters)),
			],
			[
				.. values.Select(value => new FixtureCase(
					value,
					rule.IsValid(DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None))
				)),
			]
		);
}
