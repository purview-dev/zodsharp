using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenPrivateNestedClass_GeneratesCompilingSchema(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
	public partial class Outer
	{
		[ZodSchema]
		private class Inner
		{
			public string? Name { get; set; }
		}
	}
}
";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("InnerSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("private static partial class InnerSchema");
		await Assert.That(generatedSource).ContainsGeneratedCode("public partial class Outer");
	}

	[Test]
	public async Task Generate_GivenMultiLevelNestedClass_GeneratesCompilingSchema(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
	public partial class Outer
	{
		public partial class Middle
		{
			[ZodSchema]
			private class Inner
			{
				public string? Name { get; set; }
			}
		}
	}
}
";

		var driverResult = await GenerateAsync(source, cancellationToken);

		await Assert.That(driverResult.GetSource("InnerSchema")).IsNotEmpty();
	}

	[Test]
	public async Task NestedClass_Runtime_ValidatesNestedSchema(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	public partial class Outer
	{
		[ZodSchema]
		private class Inner
		{
			[Required]
			[StringLength(10, MinimumLength = 2)]
			public string Name { get; set; } = string.Empty;
		}
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var outerType = assembly.GetType("Testing.Outer")!;
		var innerType = outerType.GetNestedType("Inner", BindingFlags.NonPublic)!;
		var schemaType = outerType.GetNestedType("InnerSchema", BindingFlags.NonPublic)!;

		var instance = Activator.CreateInstance(innerType)!;
		innerType.GetProperty("Name")!.SetValue(instance, "A");

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		var isSuccess = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;

		await Assert.That(isSuccess).IsFalse();
	}

	[Test]
	public async Task Generate_GivenNestedSchemaType_DoesNotPlaceGeneratedAttributesOnContainingTypes(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public partial class Outer
	{
		public partial class Middle
		{
			[ZodSchema]
			private class Inner
			{
				public string? Name { get; set; }
			}
		}
	}
}
";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("InnerSchema");
		await Assert.That(generatedSource).IsNotNull();

		var root = await CSharpSyntaxTree
			.ParseText(generatedSource!, cancellationToken: cancellationToken)
			.GetRootAsync(cancellationToken);

		var containingDeclarations = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Where(static declaration => declaration.Identifier.ValueText is "Outer" or "Middle")
			.ToList();

		await Assert.That(containingDeclarations.Count).IsEqualTo(2);
		foreach (var containingDeclaration in containingDeclarations)
			await Assert.That(containingDeclaration.AttributeLists.Any()).IsFalse();

		var schemaDeclaration = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(static declaration => declaration.Identifier.ValueText == "InnerSchema");

		await Assert.That(schemaDeclaration.AttributeLists.Any()).IsTrue();
	}

	[Test]
	public async Task Generate_GivenSealedNestedSchemaType_DoesNotPlaceGeneratedAttributesOnSealedContainingType(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	sealed partial class WebAppKit
	{
		[ZodSchema]
		sealed partial class WebAppKitOptions
		{
			public string? Name { get; set; }
		}
	}
}
";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("WebAppKitOptionsSchema");
		await Assert.That(generatedSource).IsNotNull();

		var root = await CSharpSyntaxTree
			.ParseText(generatedSource!, cancellationToken: cancellationToken)
			.GetRootAsync(cancellationToken);

		var containingDeclarations = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Where(static declaration => declaration.Identifier.ValueText == "WebAppKit")
			.ToList();

		await Assert.That(containingDeclarations.Count).IsEqualTo(1);
		foreach (var containingDeclaration in containingDeclarations)
			await Assert.That(containingDeclaration.AttributeLists.Any()).IsFalse();

		var schemaDeclaration = root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Single(static declaration => declaration.Identifier.ValueText == "WebAppKitOptionsSchema");

		await Assert.That(schemaDeclaration.AttributeLists.Any()).IsTrue();
	}
}
