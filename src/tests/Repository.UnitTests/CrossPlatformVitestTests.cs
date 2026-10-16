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
/// controls producer ordering, while these tests make the same failure reproducible with one command on a
/// developer machine. One test per vitest suite keeps a failure attributable to a file, and
/// <see cref="VitestSuites_GivenEveryTestFileOnDisk_AreAllCovered"/> fails when a suite is added that is
/// not listed here, so the duplication cannot silently fall behind.
/// </para>
/// <para>
/// <b>These tests never skip.</b> A skipped test proves nothing, and the parity guarantee is not optional.
/// The runner is resolved per run: <b>bun</b> when it is on PATH (the shared build pipeline and the
/// <c>cross-platform</c> job both have it), otherwise <b>node</b> against vitest's own entry point. If
/// neither is available, or the install or the run fails, the test fails and says which step and why.
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

	/// <summary>
	/// Installing dependencies on a cold checkout fetches the whole tree, so this is generous.
	/// </summary>
	static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);

	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	/// <summary>
	/// Resolved once per test run: the toolchain probe and any install are shared by every suite, and
	/// running two installs concurrently in the same working directory would corrupt <c>node_modules/</c>.
	/// </summary>
	static readonly SemaphoreSlim RunnerGate = new(1, 1);
	static VitestRunner? CachedRunner;

	/// <summary>
	/// A vitest suite and the fixture directory its assertions read. Keep in step with
	/// <c>tests/ts/*.test.ts</c> and with the producer steps in <c>.github/workflows/pr.yml</c>.
	/// </summary>
	public sealed record VitestSuite(string TestFile, string FixtureDirectory)
	{
		public override string ToString() => TestFile;
	}

	/// <summary>
	/// How to invoke vitest. <paramref name="Arguments"/> are the leading arguments, to which the suite's
	/// test-file path is appended.
	/// </summary>
	sealed record VitestRunner(string Name, string Executable, IReadOnlyList<string> Arguments);

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
		var runner = await GetRunnerAsync(root, cancellationToken);

		var fixtureDirectory = Path.Combine(root, suite.FixtureDirectory.Replace('/', Path.DirectorySeparatorChar));
		if (!await WaitForFixturesAsync(fixtureDirectory, cancellationToken))
		{
			Assert.Fail(
				$"No fixtures in '{suite.FixtureDirectory}' after waiting {FixtureWaitTimeout.TotalSeconds:N0}s, so "
					+ "this suite would verify nothing. Its C# producer did not run — run the whole suite with "
					+ "`just test`, which covers every producer."
			);
		}

		// Act — vitest exits non-zero on failure; capture rather than throw so the output reaches the assertion.
		var result = await Cli.Wrap(runner.Executable)
			.WithArguments([.. runner.Arguments, suite.TestFile])
			.WithWorkingDirectory(root)
			.WithValidation(CommandResultValidation.None)
			.ExecuteBufferedAsync(cancellationToken);

		// Assert
		await Assert
			.That(result.ExitCode)
			.IsEqualTo(0)
			.Because(
				$"vitest ({runner.Name}) failed for {suite.TestFile}:{Environment.NewLine}"
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

	static async Task<VitestRunner> GetRunnerAsync(string root, CancellationToken cancellationToken)
	{
		await RunnerGate.WaitAsync(cancellationToken);
		try
		{
			return CachedRunner ??= await ResolveRunnerAsync(root, cancellationToken);
		}
		finally
		{
			RunnerGate.Release();
		}
	}

	/// <summary>
	/// Prefers bun, falls back to node, and fails when neither can run the suites.
	/// </summary>
	/// <remarks>
	/// The node path invokes vitest's entry point directly rather than going through <c>npx</c> or
	/// <c>node_modules/.bin</c>. Both are unreliable here: the <c>.bin</c> shims are written by whichever
	/// package manager installed the tree (a bun install leaves <c>vitest.exe</c>/<c>vitest.bunx</c> and no
	/// node shim), and <c>npx</c> resolves to a <c>.cmd</c> or <c>.ps1</c> shim on Windows, which is not
	/// directly executable as a child process. The entry point is declared by vitest's own
	/// <c>package.json</c> <c>bin</c> field and is the same file either shim would invoke.
	/// </remarks>
	static async Task<VitestRunner> ResolveRunnerAsync(string root, CancellationToken cancellationToken)
	{
		var bun = FindOnPath("bun");
		var node = FindOnPath("node");

		if (bun is null && node is null)
		{
			Assert.Fail(
				"Neither bun nor node is on PATH, so the TypeScript cross-platform suites cannot run. Install "
					+ "either one: bun is what CI uses, and node is supported as the fallback."
			);
		}

		await EnsureDependenciesInstalledAsync(root, bun, cancellationToken);

		if (bun is not null)
			return new VitestRunner("bun", bun, ["run", "vitest", "run"]);

		var entryPoint = VitestEntryPoint(root);
		if (!File.Exists(entryPoint))
		{
			Assert.Fail(
				$"vitest's entry point is missing at '{entryPoint}' after installing dependencies, so the "
					+ "TypeScript cross-platform suites cannot run."
			);
		}

		return new VitestRunner("node", node!, [entryPoint, "run"]);
	}

	/// <summary>
	/// Installs <c>node_modules/</c> when vitest is not already present. <c>node_modules/</c> is gitignored,
	/// so a fresh checkout and a CI runner both start without it.
	/// </summary>
	/// <remarks>
	/// bun installs from <c>bun.lock</c>; npm has no lockfile to read here, so it resolves from
	/// <c>package.json</c>. That is why bun is preferred for the install as well as the run.
	/// </remarks>
	static async Task EnsureDependenciesInstalledAsync(string root, string? bun, CancellationToken cancellationToken)
	{
		if (File.Exists(VitestEntryPoint(root)))
			return;

		var installer =
			bun is not null ? new VitestRunner("bun", bun, ["install", "--frozen-lockfile"])
			: FindOnPath("npm") is { } npm ? new VitestRunner("npm", npm, ["install"])
			: null;

		if (installer is null)
		{
			Assert.Fail(
				$"vitest is not installed at '{VitestEntryPoint(root)}' and neither bun nor npm is on PATH to "
					+ "install it, so the TypeScript cross-platform suites cannot run."
			);
		}

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(InstallTimeout);

		var result = await Cli.Wrap(installer!.Executable)
			.WithArguments(installer.Arguments)
			.WithWorkingDirectory(root)
			.WithValidation(CommandResultValidation.None)
			.ExecuteBufferedAsync(timeout.Token);

		if (result.ExitCode != 0)
		{
			Assert.Fail(
				$"`{installer.Name} {string.Join(' ', installer.Arguments)}` failed with exit code "
					+ $"{result.ExitCode}, so the TypeScript cross-platform suites cannot run:"
					+ $"{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}"
			);
		}
	}

	static string VitestEntryPoint(string root) => Path.Combine(root, "node_modules", "vitest", "vitest.mjs");

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

	/// <summary>
	/// Resolves an executable's full path from PATH, so CliWrap starts it without depending on the host's
	/// own resolution. Returns null when it is not found.
	/// </summary>
	static string? FindOnPath(string executable)
	{
		var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];

		// On Windows an executable is found by appending a PATHEXT suffix; elsewhere the name is used as-is,
		// which the single empty suffix expresses. Only genuinely executable images are considered: a .cmd or
		// .ps1 shim cannot be started directly as a child process.
		var extensions = OperatingSystem.IsWindows() ? [".EXE", ".COM"] : new[] { string.Empty };

		foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
		{
			foreach (var extension in extensions)
			{
				string candidate;
				try
				{
					candidate = Path.Combine(path, executable + extension);
				}
				catch (ArgumentException)
				{
					// A malformed PATH entry is not this test's problem.
					continue;
				}

				if (File.Exists(candidate))
					return candidate;
			}
		}

		return null;
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
