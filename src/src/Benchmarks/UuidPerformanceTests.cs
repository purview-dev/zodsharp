using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Rules;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Performance tests for UUID validation: char-scan rule vs the legacy compiled regex,
/// plus the schema-level validation paths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class UuidPerformanceTests
{
	const string ValidUuidV4 = "550e8400-e29b-41d4-a716-446655440000";
	const string ValidUuidV7 = "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c";
	const string InvalidUuid = "550e8400-e29b-41d4-a716";
	const string NilUuid = "00000000-0000-0000-0000-000000000000";

	static readonly Regex LegacyUuidRegex = new(
		@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase,
		TimeSpan.FromMilliseconds(100)
	);

	readonly UUIDRule _uuidRule;
	readonly UUIDRule _uuidV7Rule;
	readonly ZodString _uuidSchema;
	readonly ZodString _uuidV7Schema;

	public UuidPerformanceTests()
	{
		_uuidRule = new();
		_uuidV7Rule = new(UuidVersion.V7);
		_uuidSchema = Z.String().UUID();
		_uuidV7Schema = Z.String().UUID(UuidVersion.V7);
	}

	[Benchmark]
	public bool Rule_CharScan_Valid() => _uuidRule.IsValid(ValidUuidV4);

	[Benchmark]
	public bool Rule_LegacyRegex_Valid() => LegacyUuidRegex.IsMatch(ValidUuidV4);

	[Benchmark]
	public bool Rule_CharScan_Invalid() => _uuidRule.IsValid(InvalidUuid);

	[Benchmark]
	public bool Rule_LegacyRegex_Invalid() => LegacyUuidRegex.IsMatch(InvalidUuid);

	[Benchmark]
	public bool Rule_CharScan_Nil() => _uuidRule.IsValid(NilUuid);

	[Benchmark]
	public bool Rule_CharScanV7_Valid() => _uuidV7Rule.IsValid(ValidUuidV7);

	[Benchmark]
	public bool Rule_CharScanV7_Mismatch() => _uuidV7Rule.IsValid(ValidUuidV4);

	[Benchmark]
	public ValidationResult<string> Schema_UUID_Valid() => _uuidSchema.Validate(ValidUuidV4);

	[Benchmark]
	public ValidationResult<string> Schema_UUIDV7_Valid() => _uuidV7Schema.Validate(ValidUuidV7);
}
