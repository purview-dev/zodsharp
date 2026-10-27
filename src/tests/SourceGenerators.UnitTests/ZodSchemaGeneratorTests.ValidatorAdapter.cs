using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenZodSchema_AlsoEmitsValidatorAdapter(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        public string? Name { get; set; }
    }
}";
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("WidgetSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("class WidgetSchemaValidator");
		await Assert.That(generatedSource).ContainsGeneratedCode("IZodSchemaValidator");
		await Assert.That(generatedSource).ContainsGeneratedCode("Widget");
		await Assert.That(generatedSource).ContainsGeneratedCode("Validate(");
		await Assert.That(generatedSource).ContainsGeneratedCode("return WidgetSchema.Validate(value);");
	}

	[Test]
	public async Task Generate_GivenNestedTypeDeclaredAfterReferrer_KeepsTheNestedTypesOwnValidatorAdapter(
		CancellationToken cancellationToken
	)
	{
		// Arrange — `Wrapper` is declared first, so its attribute context discovers `Inner` as a secondary
		// (nested) schema before `Inner`'s own [ZodSchema] context is processed. Deduplication must still
		// keep the primary copy, otherwise the nested type silently loses its DI adapter and registration.
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Wrapper
    {
        public Inner Inner { get; set; } = new();
    }

    [ZodSchema]
    public partial record struct Inner
    {
        public string? Name { get; set; }
    }
}";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var schemaSource = driverResult.GetSource("InnerSchema");
		var registrationSource = driverResult.GetSource("InnerSchemaRegistration");

		// Assert — the primary schema wins, so the adapter and its registration are emitted.
		await Assert.That(schemaSource).ContainsGeneratedCode("class InnerSchemaValidator");
		await Assert.That(registrationSource).ContainsGeneratedCode("typeof(global::Testing.InnerSchemaValidator)");
	}

	[Test]
	public async Task Generate_GivenZodSchema_EmitsModuleAttribute(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Gadget { }
}";
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var allGenerated = string.Join("\n", driverResult.AllSyntaxTrees.Select(static t => t.GetText().ToString()));

		await Assert.That(allGenerated).ContainsGeneratedCode("ZodSchemaGenerated");
		await Assert.That(allGenerated).ContainsGeneratedCode("Gadget");
	}

	[Test]
	public async Task Generate_GivenZodSchema_ValidatorAdapterImplementsValidateAsync(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Gizmo
    {
        public string? Name { get; set; }
    }
}";
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("GizmoSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("ValidateAsync");
		await Assert.That(generatedSource).ContainsGeneratedCode("ValueTask");
		await Assert.That(generatedSource).ContainsGeneratedCode("GizmoSchemaValidator");
	}

	[Test]
	public async Task Generate_GivenZodSchemaWithCustomAsyncValidator_ValidatorAdapterCallsCustomValidateAsync(
		CancellationToken cancellationToken
	)
	{
		var rnd = $"{Guid.NewGuid()}";

		var source =
			$@"
	using System.Threading;
	using System.Threading.Tasks;
	using ZodSharp.Core;

	namespace Testing
	{{
	    [ZodSchema]
	    public class Gizmo
	    {{
	        public string? Name {{ get; set; }}
	    }}

		partial class GizmoSchemaValidator
		{{
			async ValueTask<ValidationResult<Gizmo>> CustomValidationAsync(Gizmo value, CancellationToken cancellationToken = default)
			{{
				throw new Exception(""{rnd}"");
			}}
		}}
	}}";
		using var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.Compile, cancellationToken);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var gizmoType = assembly.GetType("Testing.Gizmo");
		var gizmoSchemaType = assembly.GetType("Testing.GizmoSchemaValidator");

		await Assert.That(gizmoType).IsNotNull();
		await Assert.That(gizmoSchemaType).IsNotNull();

		var methods = gizmoSchemaType.GetMethods(
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);
		await Assert.That(methods).Contains(m => m.Name == "CustomValidationAsync");

		var gizmo = Activator.CreateInstance(gizmoType);
		var gizmoSchemaValidator = Activator.CreateInstance(gizmoSchemaType);

		// The adapter also exposes the service-aware ValidateAsync overload, so select by signature.
		var method = gizmoSchemaType.GetMethod(
			nameof(IZodSchemaValidator<>.ValidateAsync),
			[gizmoType!, typeof(CancellationToken)]
		);
		await Assert.That(method).IsNotNull();

		var valueTaskResult = method!.Invoke(gizmoSchemaValidator, [gizmo, cancellationToken])!;

		void CallResult() => ((dynamic)valueTaskResult).GetAwaiter().GetResult();

		await Assert.That(CallResult).Throws<Exception>().WithMessage(rnd, StringComparison.Ordinal);
	}
}
