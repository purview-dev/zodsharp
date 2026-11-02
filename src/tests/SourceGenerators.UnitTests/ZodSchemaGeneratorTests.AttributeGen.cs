using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators;

[SuppressMessage(
	"Maintainability",
	"CA1506",
	Justification = "Test class naturally couples to many framework and generated types."
)]
partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenEmptySource_GeneratesNothing(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert - [ZodSchema] is defined in Purview.ZodSharp, so there is no marker source to emit.
		var generated = driverResult.DriverResult.Results.SelectMany(static result => result.GeneratedSources);
		await Assert.That(generated).IsEmpty();
		await Assert.That(ExpectedFileCount).IsEqualTo(0);
	}

	[Test]
	public async Task Generate_GivenSchemaType_DoesNotDeclareMarkerAttributeTypes(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		public string? Name { get; set; }
	}
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert - a generated copy in every project gave each one the same public type and XML documentation ID.
		var query = driverResult.Generated();
		await Assert.That(query.HasClass("WidgetSchema")).IsTrue();
		await Assert.That(query.HasClass("ZodSchemaAttribute")).IsFalse();
		await Assert.That(query.HasClass("EmbeddedAttribute")).IsFalse();
	}

	[Test]
	public async Task Generate_GivenSchemaType_BindsZodSchemaToPurviewZodSharp(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		public string? Name { get; set; }
	}
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		var compilation = driverResult.CompilationResult.Compilation;
		var widget = compilation.GetTypeByMetadataName("Testing.Widget")!;
		var attributeClass = widget.GetAttributes().Single().AttributeClass!;
		await Assert.That(attributeClass.ToDisplayString()).IsEqualTo("ZodSharp.ZodSchemaAttribute");
		await Assert
			.That(attributeClass.ContainingAssembly.Name)
			.IsEqualTo(typeof(ZodSchemaAttribute).Assembly.GetName().Name);
		await Assert.That(attributeClass.DeclaredAccessibility).IsEqualTo(Accessibility.Public);
		await Assert
			.That(compilation.GetDiagnostics(cancellationToken).Any(static diagnostic => diagnostic.Id == "CS0436"))
			.IsFalse();
	}

	[Test]
	public async Task ZodSchemaAttribute_GivenDefaults_MatchesTheDocumentedDefaults(CancellationToken cancellationToken)
	{
		// Arrange
		ZodSchemaAttribute attribute = new();

		// Act
		var usage = (AttributeUsageAttribute)
			Attribute.GetCustomAttribute(typeof(ZodSchemaAttribute), typeof(AttributeUsageAttribute))!;

		// Assert - the shape the generator emitted before the attribute moved into Purview.ZodSharp.
		await Assert.That(usage.ValidOn).IsEqualTo(AttributeTargets.Class | AttributeTargets.Struct);
		await Assert.That(usage.Inherited).IsFalse();
		await Assert.That(usage.AllowMultiple).IsFalse();
		await Assert.That(attribute.SchemaName).IsNull();
		await Assert.That(attribute.GenerateValidateMethod).IsTrue();
		await Assert.That(attribute.GenerateParseMethod).IsTrue();
		await Assert.That(attribute.EnableComposition).IsTrue();
		await Assert.That(attribute.CustomValidationMethodName).IsNull();
		await Assert.That(attribute.GenerateIValidateOptions).IsFalse();
		await Assert.That(attribute.SuppressIValidateOptions).IsFalse();
		await Assert.That(attribute.ValidateEnumValues).IsTrue();
		await Assert.That(attribute.GenerateJsonSchema).IsTrue();
		await Assert.That(attribute.AllowNulls).IsFalse();
		// The refinement name is no longer configured by convention; the OnZodValidate hook replaced it.
		await Assert.That(typeof(ZodSchemaAttribute).GetProperty("RefinementMethodName")).IsNull();
		cancellationToken.ThrowIfCancellationRequested();
	}
}
