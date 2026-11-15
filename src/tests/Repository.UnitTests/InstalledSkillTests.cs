using System.Diagnostics;
using System.Text.Json;

namespace ZodSharp.Repository;

/// <summary>
/// Guards the skills installed from other repositories. They are installed by the skills CLI into the
/// universal <c>.agents/skills/</c> folder only, which is a local install: gitignored, reproducible from
/// the committed <c>skills-lock.json</c>, and never tracked - a skill this repository installs but does
/// not publish must not become discoverable through <c>skills add purview-dev/zodsharp</c>.
/// </summary>
/// <remarks>
/// The checks that need an install are skipped on a fresh clone, which has none until
/// <c>just skills-install</c> runs; <c>SkillTests</c> covers this repository's own skills in
/// <c>skills/</c>.
/// </remarks>
public sealed class InstalledSkillTests
{
	static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

	[Test]
	public async Task Lockfile_GivenEveryEntry_HasItsSkillInstalledUnderAgents(CancellationToken cancellationToken)
	{
		// Arrange
		var recorded = await ReadLockfileSkillsAsync(cancellationToken);
		if (!Directory.Exists(AgentsSkillsFolder()))
		{
			// A fresh clone has no local install - it is gitignored and restored with `just skills-install` -
			// so the committed record is what has to be there.
			await Assert.That(recorded).IsNotEmpty().Because("skills-lock.json records no installed skills");
			return;
		}

		// Act
		var missing = recorded
			.Where(static name => !File.Exists(Path.Combine(AgentsSkillsFolder(), name, "SKILL.md")))
			.ToArray();

		// Assert
		await Assert.That(recorded).IsNotEmpty().Because("skills-lock.json records no installed skills");
		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				"skills-lock.json records skills that are not installed in .agents/skills/. Restore them with "
					+ $"`npx -y skills@1 experimental_install`: {string.Join(", ", missing)}"
			);
	}

	[Test]
	public async Task AgentsSkills_GivenEveryInstalledSkill_IsRecordedInTheLockfile(CancellationToken cancellationToken)
	{
		// Arrange
		var recorded = await ReadLockfileSkillsAsync(cancellationToken);
		if (!Directory.Exists(AgentsSkillsFolder()))
		{
			// No local install to check against the record; the record itself is checked above.
			return;
		}

		// Act
		var unrecorded = Directory
			.GetDirectories(AgentsSkillsFolder())
			.Select(static path => Path.GetFileName(path))
			.Where(name => !recorded.Contains(name, StringComparer.Ordinal))
			.ToArray();

		// Assert
		await Assert
			.That(unrecorded)
			.IsEmpty()
			.Because(
				"every folder in .agents/skills/ must be written by the skills CLI, which records it in "
					+ $"skills-lock.json: {string.Join(", ", unrecorded)}"
			);
	}

	[Test]
	public async Task AgentsSkills_GivenTheAuthoredSkills_HoldsNoCopyOfThem(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		var authored = Directory
			.GetDirectories(Path.Combine(RepositoryRoot.Value, "skills"))
			.Select(static path => Path.GetFileName(path))
			.ToArray();

		// Act
		var copied = authored
			.Where(static name => Directory.Exists(Path.Combine(AgentsSkillsFolder(), name)))
			.ToArray();

		// Assert
		await Assert
			.That(copied)
			.IsEmpty()
			.Because(
				"this repository's own skills live in skills/ only; a copy in .agents/skills/ would drift from "
					+ $"the authored one: {string.Join(", ", copied)}"
			);
	}

	[Test]
	public async Task ClaudeFolder_GivenTheInstalledSkills_HoldsNoCopy(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		var claudeSkills = Path.Combine(RepositoryRoot.Value, ".claude", "skills");

		// Act
		var exists = Directory.Exists(claudeSkills);

		// Assert
		await Assert
			.That(exists)
			.IsFalse()
			.Because(
				"skills are installed into .agents/skills/ only. A .claude/skills/ folder means the CLI ran without "
					+ "`-a universal`; delete it and reinstall with `-a universal`"
			);
	}

	[Test]
	public async Task Repository_GivenAgentInstructions_HasNoClaudeMd(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		string[] candidates =
		[
			Path.Combine(RepositoryRoot.Value, "CLAUDE.md"),
			Path.Combine(RepositoryRoot.Value, ".github", "CLAUDE.md"),
		];

		// Act
		var present = candidates.Where(File.Exists).ToArray();

		// Assert
		await Assert
			.That(present)
			.IsEmpty()
			.Because(
				"this repository is not agent-specific: AGENTS.md is its one instruction file, so a CLAUDE.md "
					+ $"must not be added: {string.Join(", ", present)}"
			);
	}

	[Test]
	public async Task AgentsSkills_GivenEverythingTheCLIWrites_IsIgnoredAndNeverTracked(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the folder is a local install, so nothing under it may be tracked, and the rules that
		// keep it (and every per-agent folder) out of this repository must be in place. This is what stops
		// a skill the repository installs but does not publish being found by `skills add
		// purview-dev/zodsharp`.
		var tracked = await TrackedFilesUnderAgentsSkillsAsync(cancellationToken);
		var ignores = await AgentsSkillsIgnoreRulesAsync(cancellationToken);

		// Assert
		await Assert
			.That(tracked)
			.IsEmpty()
			.Because(
				"nothing under .agents/skills/ may be tracked: it is a local install, reproducible from "
					+ $"skills-lock.json with `just skills-install`: {string.Join(", ", tracked)}"
			);
		await Assert
			.That(ignores.Agents)
			.IsTrue()
			.Because(".gitignore must keep .agents/skills/ out of this repository");
		await Assert.That(ignores.Claude).IsTrue().Because(".gitignore must keep .claude/ out of this repository");
	}

	/// <summary>What git tracks under <c>.agents/skills/</c>, asked of the repository itself.</summary>
	static async Task<string[]> TrackedFilesUnderAgentsSkillsAsync(CancellationToken cancellationToken)
	{
		using Process process = new()
		{
			StartInfo = new ProcessStartInfo("git", "ls-files .agents/skills")
			{
				WorkingDirectory = RepositoryRoot.Value,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		process.Start();
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		return
		[
			.. output
				.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Order(StringComparer.Ordinal),
		];
	}

	/// <summary>The ignore rules for the two agent folders, as <c>.gitignore</c> spells them.</summary>
	static async Task<(bool Agents, bool Claude)> AgentsSkillsIgnoreRulesAsync(CancellationToken cancellationToken)
	{
		var lines = await File.ReadAllLinesAsync(Path.Combine(RepositoryRoot.Value, ".gitignore"), cancellationToken);
		var rules = lines.Select(static line => line.Trim()).ToHashSet(StringComparer.Ordinal);
		return (
			rules.Overlaps([".agents/skills/", ".agents/skills", "/.agents/skills/", ".agents/"]),
			rules.Overlaps([".claude/", ".claude", "/.claude/", "/.claude"])
		);
	}

	static string AgentsSkillsFolder() => Path.Combine(RepositoryRoot.Value, ".agents", "skills");

	static async Task<string[]> ReadLockfileSkillsAsync(CancellationToken cancellationToken)
	{
		await using var stream = File.OpenRead(Path.Combine(RepositoryRoot.Value, "skills-lock.json"));
		using var lockfile = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		return
		[
			.. lockfile
				.RootElement.GetProperty("skills")
				.EnumerateObject()
				.Select(static property => property.Name)
				.Order(StringComparer.Ordinal),
		];
	}

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
				&& File.Exists(Path.Combine(directory.FullName, "skills-lock.json"))
				&& Directory.Exists(Path.Combine(directory.FullName, "src", "src"))
			)
				return directory.FullName;
		}

		throw new InvalidOperationException(
			$"No repository root above '{AppContext.BaseDirectory}' carries AGENTS.md, skills-lock.json and src/src."
		);
	}
}
