using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Drives the ZodSharp MSBuild switches through a real globalconfig, parsed by Roslyn's own
/// <see cref="AnalyzerConfig"/>, rather than through a hand-built options dictionary. MSBuild's
/// <c>GenerateMSBuildEditorConfigFile</c> writes every <c>CompilerVisibleProperty</c> into exactly this
/// shape (<c>is_global = true</c>, then <c>build_property.Name = value</c>), so a key the compiler would not
/// expose fails here even when the dictionary-driven tests pass.
/// </summary>
public sealed class MSBuildSwitchGlobalConfigTests
{
	const string Source = """
		using ZodSharp;

		namespace Testing
		{
			[ZodSchema]
			public class Widget
			{
				public string Name { get; set; } = "";
			}
		}
		""";

	[Test]
	public async Task GenerateJsonSchemas_GivenGlobalConfigBuildPropertyFalse_OmitsBuildJsonSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string globalConfig = """
			is_global = true
			build_property.ZodSharpGenerateJsonSchemas = false
			""";

		// Act
		var generated = RunGenerator(globalConfig, cancellationToken);

		// Assert
		await Assert.That(generated).DoesNotContain("BuildJsonSchema");
	}

	[Test]
	public async Task GenerateJsonSchemas_GivenGlobalConfigWithoutSwitch_EmitsBuildJsonSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string globalConfig = """
			is_global = true
			build_property.RootNamespace = Testing
			""";

		// Act
		var generated = RunGenerator(globalConfig, cancellationToken);

		// Assert
		await Assert.That(generated).Contains("BuildJsonSchema");
	}

	[Test]
	public async Task AutoGenerateOptionsValidators_GivenGlobalConfigBuildPropertyFalse_DoesNotEmitValidator(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string globalConfig = """
			is_global = true
			build_property.ZodSharpAutoGenerateOptionsValidators = false
			""";
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed class AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		// Act
		var disabled = RunGenerator(globalConfig, cancellationToken, source);
		var enabled = RunGenerator("is_global = true", cancellationToken, source);

		// Assert
		await Assert.That(enabled).Contains("AdminPortalOptionsValidator");
		await Assert.That(disabled).DoesNotContain("AdminPortalOptionsValidator");
	}

	[Test]
	public async Task BuildTransitiveProps_GivenEverySwitchTheGeneratorReads_DeclaresItCompilerVisible(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a switch missing from the props never reaches the globalconfig, so it silently does nothing.
		var switches = typeof(Helpers.PropertyLibrary)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(field => field.IsLiteral && field.Name.EndsWith("Property", StringComparison.Ordinal))
			.Select(field => (string)field.GetRawConstantValue()!)
			.ToArray();
		var propsPath = Path.Combine(
			FindRepositoryRoot(),
			"src",
			"src",
			"ZodSharp",
			"Sdk",
			"buildTransitive",
			"Purview.ZodSharp.props"
		);

		// Act
		var props = System.Xml.Linq.XDocument.Parse(await File.ReadAllTextAsync(propsPath, cancellationToken));
		var declared = props
			.Descendants("CompilerVisibleProperty")
			.Select(element => (string?)element.Attribute("Include"))
			.ToHashSet(StringComparer.Ordinal);

		// Assert
		await Assert.That(switches).IsNotEmpty();
		await Assert.That(switches.Where(name => !declared.Contains(name))).IsEmpty();
	}

	static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
			directory = directory.Parent;

		return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
	}

	static string RunGenerator(string globalConfig, CancellationToken cancellationToken, string source = Source)
	{
		var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
		var compilation = CreateCompilation(source, parseOptions, cancellationToken);

		var driver = CSharpGeneratorDriver.Create(
			[new ZodSchemaGenerator().AsSourceGenerator()],
			parseOptions: parseOptions,
			optionsProvider: new GlobalConfigOptionsProvider(ParseGlobalOptions(globalConfig))
		);
		var result = driver.RunGenerators(compilation, cancellationToken).GetRunResult();

		return string.Join(
			Environment.NewLine,
			result
				.GeneratedTrees
				// Only the schema outputs are inspected; a generated rule attribute documents its own options.
				.Where(tree => !tree.FilePath.EndsWith("Attribute.g.cs", StringComparison.Ordinal))
				.Select(tree => tree.GetText(cancellationToken).ToString())
		);
	}

	static ImmutableDictionary<string, string> ParseGlobalOptions(string globalConfig)
	{
		var config = AnalyzerConfig.Parse(globalConfig, "/repo/obj/Testing.GeneratedMSBuildEditorConfig.editorconfig");
		var configSet = AnalyzerConfigSet.Create(ImmutableArray.Create(config), out var configDiagnostics);
		if (!configDiagnostics.IsEmpty)
			throw new InvalidOperationException(string.Join(Environment.NewLine, configDiagnostics));

		return configSet.GlobalConfigOptions.AnalyzerOptions;
	}

	static CSharpCompilation CreateCompilation(
		string source,
		CSharpParseOptions parseOptions,
		CancellationToken cancellationToken
	)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(
			source,
			parseOptions,
			path: "/repo/Widget.cs",
			cancellationToken: cancellationToken
		);

		return CSharpCompilation.Create(
			"Testing",
			[syntaxTree],
			GetReferences(),
			new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
		);
	}

	static ImmutableArray<MetadataReference> GetReferences()
	{
		var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
		return
		[
			.. trusted.Select(path => MetadataReference.CreateFromFile(path)),
			MetadataReference.CreateFromFile(typeof(Z).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Options.IValidateOptions<>).Assembly.Location),
		];
	}

	/// <summary>
	/// Exposes the global section of a parsed globalconfig the way the compiler does, through the
	/// case-insensitive dictionary <see cref="AnalyzerConfigSet"/> produced.
	/// </summary>
	sealed class GlobalConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
		: AnalyzerConfigOptionsProvider
	{
		public override AnalyzerConfigOptions GlobalOptions { get; } = new DictionaryOptions(globalOptions);

		public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => DictionaryOptions.Empty;

		public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => DictionaryOptions.Empty;
	}

	sealed class DictionaryOptions(ImmutableDictionary<string, string> options) : AnalyzerConfigOptions
	{
		public static readonly DictionaryOptions Empty = new(ImmutableDictionary<string, string>.Empty);

		public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
			options.TryGetValue(key, out value);
	}
}
