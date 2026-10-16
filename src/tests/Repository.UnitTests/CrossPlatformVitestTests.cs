using CliWrap;
using CliWrap.Buffered;

namespace ZodSharp.Repository;

/// <summary>
/// Runs the TypeScript/Zod cross-platform suites in <c>tests/ts/</c> from the .NET test run, as child
/// processes through CliWrap, so <c>just test</c> proves both halves of the cross-platform parity
/// guarantee instead of only the C# half.
/// </summary>
/// <remarks>
/// <para>
/// The two sides are one guarantee: the C# tests write fixtures under
/// <c>src/tests/cross-platform/</c> and the vitest suites assert that Zod agrees with what C# produced.
/// Until this existed, the vitest half ran only in the dedicated <c>cross-platform</c> job in
/// <c>.github/workflows/pr.yml</c>, so a TypeScript-side regression could not fail a local run — and a
/// gap in that job's own wiring (a producer it never ran) went unnoticed for a whole release.
/// </para>
/// <para>
/// This is a duplicate of that job's work, deliberately: the job stays the authoritative gate because it
/// controls producer ordering and installs bun from scratch, while these tests make the same failure
/// reproducible with one command on a developer machine. One test per vitest suite keeps a failure
/// attributable to a file, and <see cref="VitestSuites_GivenEveryTestFileOnDisk_AreAllCovered"/> fails
/// when a suite is added that is not listed here, so the duplication cannot silently fall behind.
/// </para>
/// <para>
/// <b>Prerequisites are skipped, not failed.</b> bun and <c>node_modules/</c> are absent on the shared
/// build pipeline's runner (only the <c>cross-platform</c> job sets bun up) and on a checkout where
/// <c>bun install</c> has not been run. Failing there would report a missing toolchain as a broken
/// repository. A skip is reported by the test platform with its reason, so it stays visible.
/// </para>
/// </remarks>
public sealed class CrossPlatformVitestTests
{
	/// <summary>
	/// How long to wait for a producer's fixtures to appear. <c>src/tests/cross-platform/</c> is
	/// gitignored, so a clean checkout starts with nothing, and the C# producers live in other assemblies
	/// that the test platform may still be running in parallel with this one. Waiting removes that race
	/// without nesting a second <c>dotnet test</c> inside this one.
	/// </summary>
	static readonly TimeSpan FixtureWaitTimeout = TimeSpan.FromMinutes(2);

	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	/// <summary>
	/// A vitest suite and the fixture directory its assertions read. Keep in step with
	/// <c>tests/ts/*.test.ts</c> and with the producer steps in <c>.github/workflows/pr.yml</c>.
	/// </summary>
	public sealed record VitestSuite(string TestFile, string FixtureDirectory)
	{
		public override string ToString() => TestFile;
	}

	public static IEnumerable<Func<VitestSuite>> Suites() =>
		[
			() => new VitestSuite("tests/ts/cross-platform.test.ts", "src/tests/cross-platform/output"),
			() => new VitestSuite("tests/ts/guid-uuid-parity.test.ts", "src/tests/cross-platform/guid-parity"),
		];

	[Test]
	[MethodDataSource(nameof(Suites))]
	public async Task Vitest_GivenACrossPlatformSuite_PassesAgainstTheCSharpFixtures(
		VitestSuite suite,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var root = RepositoryRoot.Value;
		SkipUnlessToolchainIsAvailable(root);

		var fixtureDirectory = Path.Combine(root, suite.FixtureDirectory.Replace('/', Path.DirectorySeparatorChar));
		if (!await WaitForFixturesAsync(fixtureDirectory, cancellationToken))
		{
			Skip.Test(
				$"No fixtures in '{suite.FixtureDirectory}' after waiting {FixtureWaitTimeout.TotalSeconds:N0}s. "
					+ "The C# producer for this suite did not run — run the whole suite with `just test`, which "
					+ "covers every producer, then re-run."
			);
		}

		// Act — vitest exits non-zero on failure; capture rather than throw so the output reaches the assertion.
		var result = await Cli.Wrap("bun")
			.WithArguments(["run", "vitest", "run", suite.TestFile])
			.WithWorkingDirectory(root)
			.WithValidation(CommandResultValidation.None)
			.ExecuteBufferedAsync(cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(0)
			.Because(
				$"`bun run vitest run {suite.TestFile}` failed:{Environment.NewLine}"
					+ $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}"
			);
	}

	/// <summary>
	/// Every <c>tests/ts/*.test.ts</c> file must be listed in <see cref="Suites"/>. Without this, adding a
	/// vitest suite leaves it driven only by the CI job, which is the gap this class exists to close.
	/// </summary>
	[Test]
	public async Task VitestSuites_GivenEveryTestFileOnDisk_AreAllCovered()
	{
		// Arrange
		var testsDirectory = Path.Combine(RepositoryRoot.Value, "tests", "ts");
		var covered = Suites().Select(suite => Path.GetFileName(suite().TestFile)).ToHashSet(StringComparer.Ordinal);

		// Act
		var onDisk = Directory
			.EnumerateFiles(testsDirectory, "*.test.ts", SearchOption.AllDirectories)
			.Select(Path.GetFileName)
			.ToArray();

		// Assert
		await Assert.That(onDisk).IsNotEmpty().Because($"'{testsDirectory}' declares no vitest suites");

		var uncovered = onDisk.Where(file => !covered.Contains(file!)).ToArray();
		await Assert
			.That(uncovered)
			.IsEmpty()
			.Because(
				"every vitest suite needs an entry in Suites() (and a producer step in pr.yml): "
					+ string.Join(", ", uncovered)
			);
	}

	/// <summary>
	/// Polls for a non-empty fixture directory. Returns false once <see cref="FixtureWaitTimeout"/> elapses.
	/// </summary>
	static async Task<bool> WaitForFixturesAsync(string fixtureDirectory, CancellationToken cancellationToken)
	{
		var deadline = DateTimeOffset.UtcNow + FixtureWaitTimeout;

		while (true)
		{
			if (
				Directory.Exists(fixtureDirectory)
				&& Directory.EnumerateFiles(fixtureDirectory, "*.json", SearchOption.AllDirectories).Any()
			)
				return true;

			if (DateTimeOffset.UtcNow >= deadline)
				return false;

			await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
		}
	}

	static void SkipUnlessToolchainIsAvailable(string root)
	{
		// `bun install` writes node_modules/, which is gitignored; vitest is resolved from it.
		Skip.Unless(
			Directory.Exists(Path.Combine(root, "node_modules")),
			"node_modules/ is absent — run `bun install` to drive the vitest suites from the .NET test run."
		);

		Skip.Unless(
			IsOnPath("bun"),
			"bun is not on PATH — the vitest suites run in the `cross-platform` CI job, which installs it."
		);
	}

	static bool IsOnPath(string executable)
	{
		var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];

		// On Windows an executable is found by appending a PATHEXT suffix; elsewhere the name is used as-is,
		// which the single empty suffix expresses. PATHEXT's separator is Path.PathSeparator on Windows.
		var pathExtensions = OperatingSystem.IsWindows()
			? Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT"
			: string.Empty;
		var extensions = pathExtensions.Split(Path.PathSeparator);

		return paths
			.Where(path => !string.IsNullOrWhiteSpace(path))
			.Any(path =>
				extensions.Any(extension =>
				{
					try
					{
						return File.Exists(Path.Combine(path, executable + extension));
					}
					catch (ArgumentException)
					{
						// A malformed PATH entry is not this test's problem.
						return false;
					}
				})
			);
	}

	/// <summary>
	/// Walks up from the test binaries to the repository root, identified by the markers this repository
	/// keeps there. A relative <c>../../../../../..</c> hop would silently resolve elsewhere if the output
	/// path depth ever changed.
	/// </summary>
	static string FindRepositoryRoot()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			if (
				File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
				&& File.Exists(Path.Combine(directory.FullName, "vitest.config.ts"))
			)
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}' carries both AGENTS.md and vitest.config.ts."
		);
	}
}
