using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Two compilations, the second referencing the first, both using <c>[ZodSchema]</c> and <c>[ErrorType]</c> - the
/// shape of every multi-project solution. Each compilation runs both generators, as a package consumer's build does.
/// </summary>
/// <remarks>
/// The generators used to emit <c>ZodSchemaAttribute</c> and <c>ErrorTypeAttribute</c>, public and documented, into
/// every compilation. Every project in a solution then exposed the same type and XML documentation ID, and the
/// ASP.NET Core OpenAPI XML-comment generator - which reads a project's own documented types and its referenced
/// projects' XML files - added the same key twice and crashed at runtime. Both attributes are now defined once, in
/// Purview.ZodSharp, and the generators emit no marker type at all.
/// </remarks>
public sealed class CrossAssemblyMarkerAttributeTests
{
	const string ContractsAssemblyName = "Contracts";
	const string ConsumerAssemblyName = "Consumer";

	const string ContractsSource = """
		using System.ComponentModel.DataAnnotations;
		using System.Runtime.CompilerServices;
		using ZodSharp;
		using ZodSharp.Core;

		// A test project is the usual friend assembly; it must not see a second copy of any marker attribute.
		[assembly: InternalsVisibleTo("Consumer")]

		namespace Contracts
		{
			/// <summary>A customer.</summary>
			[ZodSchema]
			public class Customer
			{
				/// <summary>The email address.</summary>
				[Required]
				[EmailAddress]
				public string? Email { get; set; }
			}

			/// <summary>Errors raised by the contracts.</summary>
			public static partial class ContractErrors
			{
				/// <summary>The customer already exists.</summary>
				[ErrorType]
				public static readonly ErrorType CustomerExists = new(
					Code: "customer_exists",
					Description: "The customer already exists.",
					HttpStatus: 409,
					MessageFormat: "Customer '{Email}' already exists",
					Parameters: [new ErrorTypeParameter("Email", typeof(string))]
				);
			}
		}
		""";

	const string ConsumerSource = """
		using System.ComponentModel.DataAnnotations;
		using Contracts;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Consumer
		{
			/// <summary>An order.</summary>
			[ZodSchema]
			public class Order
			{
				/// <summary>The customer.</summary>
				[Required]
				public Customer Customer { get; set; } = new();

				/// <summary>The quantity.</summary>
				[Range(1, 10)]
				public int Quantity { get; set; } = 1;
			}

			/// <summary>Errors raised by the consumer.</summary>
			public static partial class ConsumerErrors
			{
				/// <summary>The order was rejected.</summary>
				[ErrorType]
				public static readonly ErrorType OrderRejected = new(
					Code: "order_rejected",
					Description: "The order was rejected.",
					HttpStatus: 422,
					MessageFormat: "Order for '{Email}' was rejected",
					Parameters: [new ErrorTypeParameter("Email", typeof(string))]
				);
			}

			public static class Probe
			{
				public static string Run()
				{
					var invalid = OrderSchema.Validate(new Order { Customer = new Customer { Email = "nope" }, Quantity = 0 });
					var valid = OrderSchema.Validate(new Order { Customer = new Customer { Email = "a@example.com" } });
					var contractError = ContractErrors.CreateCustomerExists("a@example.com");
					var consumerError = ConsumerErrors.CreateOrderRejected("a@example.com");

					return string.Join(
						"|",
						invalid.IsSuccess,
						valid.IsSuccess,
						contractError.Code,
						contractError.Message,
						consumerError.Code,
						consumerError.Message
					);
				}
			}
		}
		""";

	// What ZodSharp 2.4.x generated into every compilation, verbatim in shape: public, documented and [Embedded].
	const string LegacyGeneratedMarkersSource = """
		namespace Microsoft.CodeAnalysis
		{
			internal sealed partial class EmbeddedAttribute : global::System.Attribute { }
		}

		namespace ZodSharp
		{
			/// <summary>Attribute to mark a class for automatic schema generation.</summary>
			[global::Microsoft.CodeAnalysis.Embedded]
			[global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
			public sealed partial class ZodSchemaAttribute : global::System.Attribute
			{
				public string? SchemaName { get; init; }
			}
		}

		namespace ZodSharp.Core
		{
			/// <summary>Attribute to mark a static ErrorType field for strongly-typed helper generation.</summary>
			[global::Microsoft.CodeAnalysis.Embedded]
			[global::System.AttributeUsage(global::System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
			public sealed partial class ErrorTypeAttribute : global::System.Attribute { }
		}
		""";

	// The duplicate-definition diagnostics a second copy of a marker type would raise.
	static readonly string[] DuplicateTypeDiagnosticIds = ["CS0433", "CS0434", "CS0435", "CS0436", "CS0437", "CS0438"];

	static readonly string[] MarkerTypeNames = ["ZodSchemaAttribute", "ErrorTypeAttribute", "EmbeddedAttribute"];

	[Test]
	public async Task Generators_GivenAnyCompilation_EmitNoMarkerAttributeTypes(CancellationToken cancellationToken)
	{
		// Arrange
		var compilation = CreateCompilation(ContractsAssemblyName, ContractsSource, [], cancellationToken);

		// Act
		var (_, generated) = RunGenerators(compilation, cancellationToken);

		// Assert
		var declared = DeclaredTypeNames(generated, cancellationToken);
		await Assert.That(declared).IsNotEmpty();
		await Assert.That(declared.Intersect(MarkerTypeNames, StringComparer.Ordinal)).IsEmpty();
	}

	[Test]
	public async Task Generators_GivenAnyCompilation_BindTheMarkerAttributesToPurviewZodSharp(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var compilation = CreateCompilation(ContractsAssemblyName, ContractsSource, [], cancellationToken);

		// Act
		var (output, _) = RunGenerators(compilation, cancellationToken);

		// Assert
		var zodSchema = output.GetTypeByMetadataName("ZodSharp.ZodSchemaAttribute");
		var errorType = output.GetTypeByMetadataName("ZodSharp.Core.ErrorTypeAttribute");
		await Assert
			.That(zodSchema?.ContainingAssembly.Name)
			.IsEqualTo(typeof(ZodSchemaAttribute).Assembly.GetName().Name);
		await Assert
			.That(errorType?.ContainingAssembly.Name)
			.IsEqualTo(typeof(ZodSchemaAttribute).Assembly.GetName().Name);
		await Assert.That(zodSchema!.DeclaredAccessibility).IsEqualTo(Accessibility.Public);
		await Assert.That(errorType!.DeclaredAccessibility).IsEqualTo(Accessibility.Public);
	}

	[Test]
	public async Task Generators_GivenConsumerReferencingContracts_CompileBothWithoutDuplicateTypes(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (contracts, contractsDiagnostics, _) = BuildContracts(ContractsSource, cancellationToken);
		var consumer = CreateCompilation(
			ConsumerAssemblyName,
			ConsumerSource,
			[contracts.Reference],
			cancellationToken
		);

		// Act
		var (output, generated) = RunGenerators(consumer, cancellationToken);
		var consumerDiagnostics = output.GetDiagnostics(cancellationToken);

		// Assert
		await Assert.That(DuplicateTypeDiagnostics(contractsDiagnostics)).IsEmpty();
		await Assert.That(DuplicateTypeDiagnostics(consumerDiagnostics)).IsEmpty();
		await Assert.That(Errors(consumerDiagnostics)).IsEmpty();
		await Assert
			.That(DeclaredTypeNames(generated, cancellationToken).Intersect(MarkerTypeNames, StringComparer.Ordinal))
			.IsEmpty();
	}

	[Test]
	public async Task Generators_GivenConsumerReferencingContracts_ValidateAndCreateErrorsAcrossAssemblies(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (contracts, _, _) = BuildContracts(ContractsSource, cancellationToken);
		var consumer = CreateCompilation(
			ConsumerAssemblyName,
			ConsumerSource,
			[contracts.Reference],
			cancellationToken
		);
		var (output, _) = RunGenerators(consumer, cancellationToken);
		var consumerImage = Emit(output, cancellationToken);

		// Act
		var result = RunProbe(contracts.Image, consumerImage);

		// Assert
		await Assert
			.That(result)
			.IsEqualTo(
				"False|True|customer_exists|Customer 'a@example.com' already exists|order_rejected|"
					+ "Order for 'a@example.com' was rejected"
			);
	}

	[Test]
	public async Task Generators_GivenContractsBuiltWithLegacyGeneratedMarkers_ConsumerBindsToPurviewZodSharp(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a library compiled by ZodSharp 2.4.x carries its own public, [Embedded] copies of the markers.
		var (contracts, _, _) = BuildContracts(
			ContractsSource + Environment.NewLine + LegacyGeneratedMarkersSource,
			cancellationToken
		);
		var consumer = CreateCompilation(
			ConsumerAssemblyName,
			ConsumerSource,
			[contracts.Reference],
			cancellationToken
		);

		// Act
		var (output, _) = RunGenerators(consumer, cancellationToken);
		var diagnostics = output.GetDiagnostics(cancellationToken);

		// Assert - [Embedded] hides the legacy copies from the consumer, so there is no ambiguity with the real ones.
		await Assert.That(DuplicateTypeDiagnostics(diagnostics)).IsEmpty();
		await Assert.That(Errors(diagnostics)).IsEmpty();
		await Assert
			.That(output.GetTypeByMetadataName("ZodSharp.ZodSchemaAttribute")?.ContainingAssembly.Name)
			.IsEqualTo(typeof(ZodSchemaAttribute).Assembly.GetName().Name);
	}

	static (
		(MetadataReference Reference, byte[] Image) Contracts,
		ImmutableArray<Diagnostic> Diagnostics,
		ImmutableArray<SyntaxTree> Generated
	) BuildContracts(string source, CancellationToken cancellationToken)
	{
		var compilation = CreateCompilation(ContractsAssemblyName, source, [], cancellationToken);
		var (output, generated) = RunGenerators(compilation, cancellationToken);
		var image = Emit(output, cancellationToken);

		return ((MetadataReference.CreateFromImage(image), image), output.GetDiagnostics(cancellationToken), generated);
	}

	static (Compilation Output, ImmutableArray<SyntaxTree> Generated) RunGenerators(
		CSharpCompilation compilation,
		CancellationToken cancellationToken
	)
	{
		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[new ZodSchemaGenerator().AsSourceGenerator(), new ErrorTypeGenerator().AsSourceGenerator()],
			parseOptions: ParseOptions
		);
		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out var output,
			out var generatorDiagnostics,
			cancellationToken
		);

		if (generatorDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
			throw new InvalidOperationException(string.Join(Environment.NewLine, generatorDiagnostics));

		return (output, driver.GetRunResult().GeneratedTrees);
	}

	static byte[] Emit(Compilation compilation, CancellationToken cancellationToken)
	{
		using MemoryStream stream = new();
		var result = compilation.Emit(stream, cancellationToken: cancellationToken);
		if (!result.Success)
		{
			throw new InvalidOperationException(
				$"{compilation.AssemblyName} did not compile:{Environment.NewLine}{string.Join(Environment.NewLine, Errors(result.Diagnostics))}"
			);
		}

		return stream.ToArray();
	}

	static string RunProbe(byte[] contractsImage, byte[] consumerImage)
	{
		ProbeLoadContext context = new(contractsImage);
		try
		{
			var consumer = context.LoadFromStream(new MemoryStream(consumerImage));
			var probe = consumer.GetType("Consumer.Probe", throwOnError: true)!;
			return (string)probe.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
		}
		finally
		{
			context.Unload();
		}
	}

	static List<string> DeclaredTypeNames(ImmutableArray<SyntaxTree> trees, CancellationToken cancellationToken) =>
		[
			.. trees
				.SelectMany(tree => tree.GetRoot(cancellationToken).DescendantNodes())
				.OfType<BaseTypeDeclarationSyntax>()
				.Select(static declaration => declaration.Identifier.ValueText),
		];

	static List<Diagnostic> DuplicateTypeDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
		[.. diagnostics.Where(static diagnostic => DuplicateTypeDiagnosticIds.Contains(diagnostic.Id))];

	static List<Diagnostic> Errors(IEnumerable<Diagnostic> diagnostics) =>
		[.. diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

	static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(
		LanguageVersion.Preview
	);

	static CSharpCompilation CreateCompilation(
		string assemblyName,
		string source,
		MetadataReference[] additionalReferences,
		CancellationToken cancellationToken
	) =>
		CSharpCompilation.Create(
			assemblyName,
			[
				CSharpSyntaxTree.ParseText(
					source,
					ParseOptions,
					path: $"/repo/{assemblyName}/Source.cs",
					cancellationToken: cancellationToken
				),
			],
			[.. References.Value, .. additionalReferences],
			new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
		);

	static readonly Lazy<ImmutableArray<MetadataReference>> References = new(static () =>
	{
		var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
		return
		[
			.. trusted
				.Append(typeof(ZodSchemaAttribute).Assembly.Location)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(static path => MetadataReference.CreateFromFile(path)),
		];
	});

	/// <summary>
	/// Loads the consumer and the contracts it references into an isolated, collectible context; everything else -
	/// Purview.ZodSharp and the framework - comes from the default context.
	/// </summary>
	sealed class ProbeLoadContext(byte[] contractsImage) : AssemblyLoadContext(isCollectible: true)
	{
		protected override Assembly? Load(AssemblyName assemblyName) =>
			assemblyName.Name == ContractsAssemblyName ? LoadFromStream(new MemoryStream(contractsImage)) : null;
	}
}
