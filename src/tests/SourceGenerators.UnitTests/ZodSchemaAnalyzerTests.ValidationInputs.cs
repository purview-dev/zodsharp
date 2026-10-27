using ZodSharp.CodeFixes;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task ValidatableType_GivenZodSchemaType_ProducesZODSGEN047(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Microsoft.Extensions.Validation
			{
				[System.AttributeUsage(System.AttributeTargets.Class)]
				public sealed class ValidatableTypeAttribute : System.Attribute
				{
				}
			}

			namespace Testing
			{
				[ZodSchema]
				[Microsoft.Extensions.Validation.ValidatableType]
				public class Customer
				{
					public string? Name { get; set; }
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ValidatableTypeRedundant);
	}

	[Test]
	public async Task ClosedHierarchy_GivenDerivedTypeWithoutSchema_ProducesZODSGEN049(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Testing
			{
				public closed class Shape
				{
				}

				public sealed class Square : Shape
				{
				}

				[ZodSchema]
				public class Drawing
				{
					public Shape? Shape { get; set; }
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(
			source,
			new ZodAnalyzerTestOptions { LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview },
			cancellationToken
		);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.ClosedHierarchyDerivedTypeWithoutSchema);
	}

	[Test]
	public async Task OptionsValidator_GivenHandAuthoredOptionsValidator_ProducesZODSGEN050(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using Microsoft.Extensions.Options;

			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public sealed class QueueOptions
				{
					public int Workers { get; set; }
				}

				[OptionsValidator]
				public partial class QueueOptionsValidator : IValidateOptions<QueueOptions>
				{
					public ValidateOptionsResult Validate(string? name, QueueOptions options) => ValidateOptionsResult.Success;
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.OptionsValidatorConflict);
	}

	[Test]
	public async Task ErrorMessage_GivenUnsuppliedPlaceholder_ProducesZODSGEN052(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public sealed class Ticket
				{
					[Required(ErrorMessage = "{0} needs {1}")]
					public string? Title { get; set; }

					[StringLength(10, ErrorMessage = "{0} must be {2}-{1}")]
					public string? Summary { get; set; }
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert — {1} is not supplied by [Required]; [StringLength] supplies {0}-{2}.
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnresolvableMessagePlaceholder);
		await Assert.That(result.Diagnostics.Count(static diagnostic => diagnostic.Id == "ZODSGEN052")).IsEqualTo(1);
	}

	[Test]
	[Arguments("{0} is required", 0, null)]
	[Arguments("{displayName} is required", 0, null)]
	[Arguments("{{literal}}", 0, null)]
	[Arguments("{0} needs {1}", 0, "{1}")]
	[Arguments("{0} between {1,5:N0} and {2}", 2, null)]
	[Arguments("broken { brace", 0, "{")]
	[Arguments("broken } brace", 0, "}")]
	public async Task FindUnresolvablePlaceholder_GivenMessage_ReturnsTheFirstProblem(
		string message,
		int maxOrdinal,
		string? expected
	)
	{
		// Act
		var problem = SourceGenLibrary.FindUnresolvablePlaceholder(message, maxOrdinal);

		// Assert
		await Assert.That(problem).IsEqualTo(expected);
	}
}

public class ZodSchemaAsyncAnalyzerTests : ZodAnalyzerTestBase
{
	[Test]
	public async Task AsyncValidatableObject_GivenGeneratedValidate_ProducesZODSGEN048(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the .NET 11 interface is declared here so the diagnostic is testable on every TFM.
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using System.Threading;

			namespace System.ComponentModel.DataAnnotations
			{
				public interface IAsyncValidatableObject : IValidatableObject
				{
					IAsyncEnumerable<ValidationResult> ValidateAsync(ValidationContext validationContext, CancellationToken cancellationToken);
				}
			}

			namespace Testing
			{
				[ZodSchema]
				public sealed class Reservation : IAsyncValidatableObject
				{
					public int Nights { get; set; }

					public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];

					public IAsyncEnumerable<ValidationResult> ValidateAsync(ValidationContext validationContext, CancellationToken cancellationToken) =>
						throw new System.NotImplementedException();
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.AsyncOnlyRulesWithSyncValidate);
	}
}

public class ZodSchemaRazorAnalyzerTests
	: TUnitDiagnosticAnalyzerTestBase<ZodSchemaRazorAnalyzer, ZodAnalyzerTestOptions>
{
	[Test]
	[Arguments("Pages/Counter.razor", true)]
	[Arguments("obj/Generated/Pages_Counter_razor.g.cs", true)]
	[Arguments("Counter.razor.g.cs", true)]
	[Arguments("Models/Counter.cs", false)]
	[Arguments(null, false)]
	public async Task IsRazorFile_GivenPath_DetectsRazorSources(string? path, bool expected)
	{
		// Act
		var isRazor = ZodSchemaRazorAnalyzer.IsRazorFile(path);

		// Assert
		await Assert.That(isRazor).IsEqualTo(expected);
	}

	[Test]
	public async Task ZodSchemaType_GivenRegularSourceFile_ProducesNoZODSGEN051(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Testing
			{
				[ZodSchema]
				public sealed class Counter
				{
					public int Count { get; set; }
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.ZodSchemaInRazorFile);
	}
}

public class RedundantValidatableTypeCodeFixTests
	: TUnitCodeFixTestBase<ZodSchemaAnalyzer, RedundantValidatableTypeCodeFixProvider, ZodCodeFixTestOptions>
{
	[Test]
	public async Task CodeFix_GivenRedundantValidatableType_RemovesTheAttribute(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Microsoft.Extensions.Validation
			{
				[System.AttributeUsage(System.AttributeTargets.Class)]
				public sealed class ValidatableTypeAttribute : System.Attribute
				{
				}
			}

			namespace Testing
			{
				[ZodSharp.ZodSchema]
				[Microsoft.Extensions.Validation.ValidatableType]
				public class Customer
				{
					public string? Name { get; set; }
				}
			}
			""";

		// Act
		var result = await ApplyCodeFixAsync(source, new ZodCodeFixTestOptions(), cancellationToken);

		// Assert
		await Assert.That(result.FixedSource).DoesNotContain("[Microsoft.Extensions.Validation.ValidatableType]");
		await Assert.That(result.FixedSource).Contains("[ZodSharp.ZodSchema]");
	}
}
