using System.ComponentModel.DataAnnotations;
using BenchmarkDotNet.Attributes;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Performance tests for source-generated ([ZodSchema]) validation: the success path, the failure path and
/// a nested graph.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class GeneratedValidationPerformanceTests
{
	readonly BenchmarkCustomer _valid = new()
	{
		Name = "Ada Lovelace",
		Email = "ada@example.com",
		Age = 36,
		Tags = ["math", "engines"],
		Address = new() { Street = "12 St James's Square", City = "London" },
	};

	readonly BenchmarkCustomer _invalid = new()
	{
		Name = "A",
		Email = "not-an-email",
		Age = 250,
		Tags = [],
		Address = new() { Street = "", City = "L" },
	};

	[Benchmark(Baseline = true)]
	public ValidationResult<BenchmarkCustomer> Validate_Valid() => BenchmarkCustomerSchema.Validate(_valid);

	[Benchmark]
	public ValidationResult<BenchmarkCustomer> Validate_Invalid() => BenchmarkCustomerSchema.Validate(_invalid);

	[Benchmark]
	public ValidationResult<BenchmarkCustomer> ValidateMember_Valid() =>
		BenchmarkCustomerSchema.ValidateMember(_valid, nameof(BenchmarkCustomer.Email));

	[Benchmark]
	public ValidationResult<BenchmarkCustomer> ValidateMember_Invalid() =>
		BenchmarkCustomerSchema.ValidateMember(_invalid, nameof(BenchmarkCustomer.Email));

	[Benchmark]
	public bool TryResolveMember_NestedPath() =>
		BenchmarkCustomerSchema.TryResolveMember(_invalid, NestedPath, out _, out _);

	static readonly ZodPath NestedPath = BenchmarkCustomerSchema.Paths.Address.Append(
		ZodPathSegment.Member(nameof(BenchmarkAddress.City))
	);
}

/// <summary>A generated-schema benchmark model.</summary>
[ZodSchema]
public sealed class BenchmarkCustomer
{
	/// <summary>The name.</summary>
	[Required]
	[StringLength(100, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	/// <summary>The email.</summary>
	[Required]
	[EmailAddress]
	public string Email { get; set; } = string.Empty;

	/// <summary>The age.</summary>
	[Range(0, 150)]
	public int Age { get; set; }

	/// <summary>The tags.</summary>
	[MinLength(1)]
	public IReadOnlyList<string> Tags { get; init; } = [];

	/// <summary>The address.</summary>
	public BenchmarkAddress Address { get; set; } = new();
}

/// <summary>A nested generated-schema benchmark model.</summary>
[ZodSchema]
public sealed class BenchmarkAddress
{
	/// <summary>The street.</summary>
	[Required]
	public string Street { get; set; } = string.Empty;

	/// <summary>The city.</summary>
	[Required]
	[MinLength(2)]
	public string City { get; set; } = string.Empty;
}
