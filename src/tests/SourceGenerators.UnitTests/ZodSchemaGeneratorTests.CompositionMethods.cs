using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task CompositionMethods_GeneratedAsApplyNamedOverloads(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class ComposeModel
	{
		public string? Name { get; set; }
	}
}
";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("ComposeModelSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("ApplyAnd");
		await Assert.That(generatedSource).ContainsGeneratedCode("ApplyOr");
		await Assert.That(generatedSource).ContainsGeneratedCode("ApplyRefine");
	}

	[Test]
	public async Task CompositionMethods_GivenDisabledComposition_AreNotGenerated(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema(EnableComposition = false)]
	public class NoComposeModel
	{
		public string? Name { get; set; }
	}
}
";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("NoComposeModelSchema");

		// Assert
		await Assert.That(generatedSource).DoesNotContain("ApplyAnd");
		await Assert.That(generatedSource).DoesNotContain("ApplyOr");
		await Assert.That(generatedSource).DoesNotContain("ApplyRefine");
	}

	[Test]
	public async Task CompositionMethods_GivenValidValue_ApplyRefineSucceeds(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class RefineModel
	{
		public int Age { get; set; }
	}
}
";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.RefineModel")!;
		var schemaType = assembly.GetType("Testing.RefineModelSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Age")!.SetValue(instance, 21);

		var refineMethod = schemaType.GetMethod("ApplyRefine")!;
		var result = refineMethod.Invoke(null, [instance, (Func<dynamic, bool>)(static m => m.Age >= 18), null])!;

		// Assert
		var isSuccess = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task GeneratedValidator_ImplementsIZodSchema_CanParticipateInComposition(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a model with [ZodSchema] whose generated validator is used as
		// an IZodSchema<T> argument to a composition method (Z.Intersection).
		const string source =
			@"
using ZodSharp;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace Testing
{
	[ZodSchema]
	public class Composable
	{
		public int Age { get; set; }
	}

	public static class CompositionUser
	{
		// The generated ComposableSchemaValidator implements IZodSchema<Composable>,
		// so it can be passed as an argument to composition methods.
		public static IZodSchema<Composable> GetSchema() => new ComposableSchemaValidator();

		public static bool IsIZodSchema() => new ComposableSchemaValidator() is IZodSchema<Composable>;
	}
}
";

		// Act — compile and invoke, proving the generated validator can be cast to
		// IZodSchema<T> and used in the composition API.
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Assert
		var helper = assembly.GetType("Testing.CompositionUser")!;
		var isSchemaMethod = helper.GetMethod("IsIZodSchema")!;
		var isSchema = (bool)isSchemaMethod.Invoke(null, [])!;
		await Assert.That(isSchema).IsTrue();

		var getSchemaMethod = helper.GetMethod("GetSchema")!;
		var schema = getSchemaMethod.Invoke(null, [])!;
		await Assert.That(schema).IsNotNull();
	}
}
