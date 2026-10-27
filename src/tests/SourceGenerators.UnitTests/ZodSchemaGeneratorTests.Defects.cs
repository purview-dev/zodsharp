using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// One regression test per generator defect in the error-model register (G1-G24). Each test failed before
/// its fix.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	const string RequiredSemanticsSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing
		{
			[ZodSchema]
			public class Contact
			{
				[Required]
				public string? Name { get; set; }

				[Required(AllowEmptyStrings = true)]
				public string? Nickname { get; set; }
			}
		}
		""";

	// Defect G1
	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Required_GivenDefaultAllowEmptyStringsAndMissingText_ReportsMissingField(
		string? name,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			RequiredSemanticsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var contact = Create(assembly, "Testing.Contact", ("Name", name), ("Nickname", "nick"));

		// Act
		var errors = RunValidate(assembly, "Testing.ContactSchema", contact);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Name");
	}

	// Defect G1
	[Test]
	[Arguments("", 0)]
	[Arguments("   ", 0)]
	[Arguments(null, 1)]
	public async Task Required_GivenAllowEmptyStrings_RejectsOnlyNull(
		string? nickname,
		int expectedErrors,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			RequiredSemanticsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var contact = Create(assembly, "Testing.Contact", ("Name", "Ada"), ("Nickname", nickname));

		// Act
		var errors = RunValidate(assembly, "Testing.ContactSchema", contact);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(expectedErrors);
	}
}
