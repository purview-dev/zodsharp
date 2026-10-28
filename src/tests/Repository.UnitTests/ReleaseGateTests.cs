using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

namespace ZodSharp.Repository;

/// <summary>
/// Guards the release gates themselves: the compiler-host leak scan must catch every kind of leak it claims to,
/// every CI job that publishes must run it, and pack validation must keep forbidding compiler-host content outside
/// <c>analyzers/</c>.
/// </summary>
/// <remarks>
/// <para>
/// A gate that passes vacuously is worse than none, because it is trusted. The leak scan
/// (<c>scripts/assert-no-compiler-host-assemblies.ps1</c>) exists because
/// <c>Purview.SourceGeneratorFramework.dll</c> once reached application output while every publish gate stayed
/// green; these tests run it against crafted output, so a regex or file-pattern change that stops it seeing a leak
/// fails here rather than in a consumer's Blazor WebAssembly AOT build.
/// </para>
/// <para>
/// The scan is a PowerShell script, as the CI jobs run it, so these tests run it through <c>pwsh</c> as a child
/// process. They do not skip when <c>pwsh</c> is missing: the gate cannot run without it either.
/// </para>
/// </remarks>
public sealed partial class ReleaseGateTests
{
	const string LeakScanScript = "scripts/assert-no-compiler-host-assemblies.ps1";

	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	/// <summary>A file written into otherwise clean build output.</summary>
	public sealed record OutputFile(string Description, string RelativePath, string Content, bool Brotli = false)
	{
		public override string ToString() => Description;
	}

	public static IEnumerable<Func<OutputFile>> Leaks() =>
		[
			() => new("the generator framework assembly", "Purview.SourceGeneratorFramework.dll", "MZ"),
			() => new("a Roslyn assembly in a subfolder", "refs/Microsoft.CodeAnalysis.CSharp.dll", "MZ"),
			() =>
				new(
					"a fingerprinted WebAssembly generator",
					"wwwroot/_framework/Purview.ZodSharp.SourceGenerators.ab12cd34ef.wasm",
					"wasm"
				),
			() =>
				new(
					"a compressed WebAssembly code-fix assembly",
					"wwwroot/_framework/Purview.ZodSharp.CodeFixes.ab12cd34ef.wasm.br",
					"br"
				),
			() =>
				new(
					"a deps.json library entry",
					"PackedApp.deps.json",
					"""{"libraries":{"Purview.SourceGeneratorFramework/1.2.2":{"type":"package"}}}"""
				),
			() =>
				new(
					"a runtimeconfig.json reference",
					"PackedApp.runtimeconfig.json",
					"""{"runtimeOptions":{"configProperties":{"Microsoft.CodeAnalysis.Leak":true}}}"""
				),
			() =>
				new(
					"a blazor.boot.json asset",
					"wwwroot/_framework/blazor.boot.json",
					"""{"resources":{"assembly":{"Purview.ZodSharp.SourceGenerators.wasm":"sha256-x"}}}"""
				),
			() =>
				new(
					"a fingerprinted dotnet.boot.js asset",
					"wwwroot/_framework/dotnet.boot.ab12cd34ef.js",
					"""export const config = {"assembly":[{"name":"Purview.ZodSharp.CodeFixes.wasm"}]};"""
				),
			() =>
				new(
					"a Brotli-compressed dotnet.js boot manifest",
					"wwwroot/_framework/dotnet.js.br",
					"""const config = {"assembly":[{"name":"Purview.SourceGeneratorFramework.wasm"}]};""",
					Brotli: true
				),
		];

	[Test]
	[MethodDataSource(nameof(Leaks))]
	public async Task LeakScan_GivenACompilerHostLeak_FailsAndNamesTheFile(
		OutputFile leak,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var output = CleanOutput();
		Write(output.Path, leak);

		// Act
		var result = await RunLeakScanAsync([output.Path], cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(1)
			.Because($"the scan missed {leak}:{Environment.NewLine}{result}");
		await Assert
			.That(result.StandardOutput)
			.Contains(Path.GetFileName(leak.RelativePath))
			.Because("a failure must name the leaking file");
	}

	[Test]
	public async Task LeakScan_GivenCleanBuildAndPublishOutput_Passes(CancellationToken cancellationToken)
	{
		// Arrange
		using var output = CleanOutput();

		// Act
		var result = await RunLeakScanAsync([output.Path], cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(0)
			.Because($"clean output was rejected:{Environment.NewLine}{result}");
		await Assert.That(result.StandardOutput).Contains("3 manifest(s)");
	}

	[Test]
	public async Task LeakScan_GivenSeveralPaths_ScansEveryOne(CancellationToken cancellationToken)
	{
		// Arrange
		using var clean = CleanOutput();
		using var leaking = CleanOutput();
		Write(leaking.Path, new("a leak in the second path", "Microsoft.CodeAnalysis.dll", "MZ"));

		// Act - space-separated, exactly as the Justfile recipes pass them through pwsh -File.
		var result = await RunLeakScanAsync([clean.Path, leaking.Path], cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(1)
			.Because($"the second path was not scanned:{Environment.NewLine}{result}");
	}

	/// <summary>
	/// A Blazor server publish carries Microsoft's WebAssembly debugging proxy, whose expression evaluator ships Roslyn.
	/// <c>-Allow</c> exempts that folder alone: a leak anywhere else in the same output must still fail.
	/// </summary>
	[Test]
	public async Task LeakScan_GivenAnAllowedToolFolder_SkipsItButStillFailsALeakElsewhere(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var allowedOnly = CleanOutput();
		Write(allowedOnly.Path, new("the debug proxy's Roslyn", "BlazorDebugProxy/Microsoft.CodeAnalysis.dll", "MZ"));
		using var alsoLeaking = CleanOutput();
		Write(alsoLeaking.Path, new("the debug proxy's Roslyn", "BlazorDebugProxy/Microsoft.CodeAnalysis.dll", "MZ"));
		Write(alsoLeaking.Path, new("a real leak", "Purview.SourceGeneratorFramework.dll", "MZ"));

		// Act
		var allowed = await RunLeakScanAsync([allowedOnly.Path], cancellationToken, ["*BlazorDebugProxy/*"]);
		var leaking = await RunLeakScanAsync([alsoLeaking.Path], cancellationToken, ["*BlazorDebugProxy/*"]);

		// Assert
		await Assert
			.That(allowed.ExitCode)
			.IsEqualTo(0)
			.Because($"the allowed folder was not skipped:{Environment.NewLine}{allowed}");
		await Assert.That(allowed.StandardOutput).Contains("1 allowed by -Allow");
		await Assert
			.That(leaking.ExitCode)
			.IsEqualTo(1)
			.Because($"-Allow hid a leak outside its folder:{Environment.NewLine}{leaking}");
		await Assert.That(leaking.StandardOutput).Contains("Purview.SourceGeneratorFramework.dll");
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task LeakScan_GivenNothingToScan_FailsRatherThanPassingVacuously(
		bool directoryExists,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using TemporaryDirectory output = new();
		var path = directoryExists ? output.Path : System.IO.Path.Combine(output.Path, "missing");

		// Act
		var result = await RunLeakScanAsync([path], cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(1)
			.Because($"scanning nothing passed:{Environment.NewLine}{result}");
	}

	/// <summary>
	/// Every CI job that publishes an application must scan what it published. A job added later without the scan
	/// would otherwise be a publish gate that cannot see the leak this repository has already shipped once.
	/// </summary>
	[Test]
	public async Task Workflows_GivenAJobThatPublishes_ScanItsOutputForCompilerHostAssemblies(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var workflows = Directory.GetFiles(
			System.IO.Path.Combine(RepositoryRoot.Value, ".github", "workflows"),
			"*.yml"
		);

		// Act
		List<string> publishing = [];
		List<string> unscanned = [];
		foreach (var workflow in workflows)
		{
			var text = await File.ReadAllTextAsync(workflow, cancellationToken);
			foreach (var (job, body) in Jobs(text))
			{
				if (!body.Contains("dotnet publish", StringComparison.Ordinal))
					continue;

				var name = $"{System.IO.Path.GetFileName(workflow)}:{job}";
				publishing.Add(name);
				if (
					!body.Contains(
						LeakScanScript.Replace("scripts/", "./scripts/", StringComparison.Ordinal),
						StringComparison.Ordinal
					)
				)
					unscanned.Add(name);
			}
		}

		// Assert
		await Assert.That(publishing).IsNotEmpty().Because("no workflow job publishes, so the job parsing is broken");
		await Assert
			.That(unscanned)
			.IsEmpty()
			.Because($"these jobs publish without running {LeakScanScript}: {string.Join(", ", unscanned)}");
	}

	[Test]
	public async Task PrWorkflow_GivenThePackedConsumersJob_RunsTheGateAndTheRuleProof(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var text = await File.ReadAllTextAsync(
			System.IO.Path.Combine(RepositoryRoot.Value, ".github", "workflows", "pr.yml"),
			cancellationToken
		);

		// Act
		var job = Jobs(text).SingleOrDefault(static j => j.Name == "packed-consumers").Body ?? string.Empty;

		// Assert
		await Assert.That(job).Contains("./scripts/test-packed-consumers.ps1 -PreflightOnly");
		await Assert.That(job).Contains("-WasmAot Require");
		await Assert.That(job).Contains("./scripts/test-pack-validation-rules.ps1");
		await Assert.That(job).Contains("dotnet workload install wasm-tools");
	}

	/// <summary>
	/// Compiler-host assemblies must stay forbidden everywhere NuGet hands files to an application, and the generator
	/// and code fixes everywhere an application loads assemblies from. <c>scripts/test-pack-validation-rules.ps1</c>
	/// proves each rule rejects a package; this proves nobody removes one.
	/// </summary>
	[Test]
	public async Task PackValidation_GivenEveryPackage_ForbidsCompilerHostContentOutsideAnalyzers(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var json = await File.ReadAllTextAsync(
			System.IO.Path.Combine(RepositoryRoot.Value, ".config", "purview-build.json"),
			cancellationToken
		);
		using var document = JsonDocument.Parse(
			json,
			new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
		);
		string[] hostOnly = ["Purview.SourceGeneratorFramework*", "Microsoft.CodeAnalysis*"];
		string[] generatorOnly = ["Purview.ZodSharp.SourceGenerators*", "Purview.ZodSharp.CodeFixes*"];
		string[] applicationFolders =
		[
			"lib",
			"ref",
			"runtimes",
			"build",
			"buildTransitive",
			"buildMultiTargeting",
			"tools",
			"content",
			"contentFiles",
			"staticwebassets",
		];
		string[] assemblyFolders = ["lib", "ref", "runtimes"];

		// Act
		var rules = document
			.RootElement.GetProperty("PackValidation")
			.GetProperty("ForbiddenContent")
			.GetProperty("*")
			.EnumerateArray()
			.Select(static rule => rule.GetString())
			.ToHashSet(StringComparer.Ordinal);

		// Assert
		string[] expected =
		[
			.. hostOnly,
			.. hostOnly.Select(static name => $"analyzers/**/{name}"),
			.. applicationFolders.SelectMany(folder => hostOnly.Select(name => $"{folder}/**/{name}")),
			.. assemblyFolders.SelectMany(folder => generatorOnly.Select(name => $"{folder}/**/{name}")),
		];
		var missing = expected.Where(rule => !rules.Contains(rule)).ToArray();
		await Assert
			.That(missing)
			.IsEmpty()
			.Because($"ForbiddenContent[\"*\"] no longer forbids: {string.Join(", ", missing)}");
	}

	static TemporaryDirectory CleanOutput()
	{
		TemporaryDirectory output = new();
		Write(output.Path, new("the app", "PackedApp.dll", "MZ"));
		Write(
			output.Path,
			new(
				"clean deps.json",
				"PackedApp.deps.json",
				"""{"libraries":{"Purview.ZodSharp/3.0.0":{"type":"package"},"Microsoft.Extensions.Options/11.0.0":{"type":"package"}}}"""
			)
		);
		Write(output.Path, new("clean runtimeconfig", "PackedApp.runtimeconfig.json", """{"runtimeOptions":{}}"""));
		Write(
			output.Path,
			new(
				"clean boot manifest",
				"wwwroot/_framework/dotnet.js",
				"""const config = {"assembly":[{"name":"Purview.ZodSharp.wasm"},{"name":"Purview.ZodSharp.Blazor.wasm"}]};"""
			)
		);
		Write(output.Path, new("the runtime", "wwwroot/_framework/dotnet.native.ab12cd34ef.js", "// runtime"));
		return output;
	}

	static void Write(string root, OutputFile file)
	{
		var path = System.IO.Path.Combine(root, file.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
		var bytes = Encoding.UTF8.GetBytes(file.Content);
		if (!file.Brotli)
		{
			File.WriteAllBytes(path, bytes);
			return;
		}

		using var stream = File.Create(path);
		using BrotliStream brotli = new(stream, CompressionLevel.Optimal);
		brotli.Write(bytes);
	}

	static async Task<ScanResult> RunLeakScanAsync(
		string[] paths,
		CancellationToken cancellationToken,
		string[]? allow = null
	)
	{
		var result = await Cli.Wrap("pwsh")
			.WithArguments([
				"-NoProfile",
				"-NonInteractive",
				"-File",
				System.IO.Path.Combine(RepositoryRoot.Value, LeakScanScript),
				"-Path",
				.. paths,
				.. AllowArguments(allow),
			])
			.WithEnvironmentVariables(static env => env.Set("GITHUB_ACTIONS", null))
			.WithValidation(CommandResultValidation.None)
			.ExecuteBufferedAsync(cancellationToken);

		return new(result.ExitCode, result.StandardOutput, result.StandardError);
	}

	static string[] AllowArguments(string[]? allow) => allow is null ? [] : ["-Allow", .. allow];

	sealed record ScanResult(int ExitCode, string StandardOutput, string StandardError)
	{
		public override string ToString() => $"exit {ExitCode}{Environment.NewLine}{StandardOutput}{StandardError}";
	}

	/// <summary>The jobs of a workflow, split on their two-space-indented keys under <c>jobs:</c>.</summary>
	static IEnumerable<(string Name, string Body)> Jobs(string workflow)
	{
		var jobs = workflow.IndexOf("\njobs:", StringComparison.Ordinal);
		if (jobs < 0)
			yield break;

		var headers = JobHeader().Matches(workflow[jobs..]);
		for (var i = 0; i < headers.Count; i++)
		{
			var start = headers[i].Index;
			var end = i + 1 < headers.Count ? headers[i + 1].Index : workflow.Length - jobs;
			yield return (headers[i].Groups["name"].Value, workflow.Substring(jobs + start, end - start));
		}
	}

	[GeneratedRegex(@"^  (?<name>[A-Za-z0-9_-]+):[ \t]*\r?$", RegexOptions.Multiline)]
	private static partial Regex JobHeader();

	static string FindRepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (
				File.Exists(System.IO.Path.Combine(directory.FullName, "AGENTS.md"))
				&& File.Exists(System.IO.Path.Combine(directory.FullName, LeakScanScript))
			)
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}' carries both AGENTS.md and {LeakScanScript}."
		);
	}

	sealed class TemporaryDirectory : IDisposable
	{
		public string Path { get; } =
			Directory
				.CreateDirectory(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"zs-leak-scan-{Guid.NewGuid():N}")
				)
				.FullName;

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch (IOException) { }
		}
	}
}
