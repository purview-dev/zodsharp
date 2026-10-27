using System.Text.RegularExpressions;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Guards the analyzer release tracking files: every diagnostic is recorded in
/// <c>AnalyzerReleases.Shipped.md</c>, the unshipped file declares no new rules, and each rule identifier is
/// tracked exactly once. These files drive the Roslyn release-tracking analyzers
/// (<c>RS2000</c>/<c>RS2001</c>), which report at build time when a diagnostic is missing from either file.
/// </summary>
/// <remarks>
/// <para>
/// <c>ZODSGEN037</c>–<c>ZODSGEN044</c> are recorded under a <c>## Release 2.0.2</c> block,
/// <c>ZODSGEN045</c> under <c>## Release 2.1.0</c>, and <c>ZODSGEN046</c> under <c>## Release 2.2.0</c>. A
/// rule stays recorded under the release that introduced it, so an existing block is never rewritten. The
/// unshipped file declares no rules; the next diagnostic added goes there and moves across when it ships.
/// </para>
/// <para>
/// <see cref="RuleIdRegex"/> accepts a rule row with or without a leading pipe, because the two files are
/// written in different table styles — <c>AnalyzerReleases.Shipped.md</c> delimits rows with pipes and
/// <c>AnalyzerReleases.Unshipped.md</c> does not. Matching only the pipe-delimited form made
/// <see cref="UnshippedRelease_GivenUnreleasedRules_DeclaresExactlyThoseRules"/> vacuous: a rule parked in the
/// unshipped file parsed as no rules at all, so the guard passed while the catalogue was wrong.
/// </para>
/// </remarks>
public partial class AnalyzerReleaseTrackingTests
{
	static readonly string ReleaseDirectory = Path.Combine(
		"..",
		"..",
		"..",
		"..",
		"..",
		"..",
		"src",
		"src",
		"SourceGenerators"
	);

	static readonly string[] ExpectedShippedRuleIds =
	[
		"ZODSGEN001",
		"ZODSGEN003",
		"ZODSGEN004",
		"ZODSGEN005",
		"ZODSGEN006",
		"ZODSGEN007",
		"ZODSGEN008",
		"ZODSGEN009",
		"ZODSGEN010",
		"ZODSGEN011",
		"ZODSGEN012",
		"ZODSGEN013",
		"ZODSGEN014",
		"ZODSGEN015",
		"ZODSGEN016",
		"ZODSGEN017",
		"ZODSGEN018",
		"ZODSGEN019",
		"ZODSGEN020",
		"ZODSGEN021",
		"ZODSGEN027",
		"ZODSGEN028",
		"ZODSGEN029",
		"ZODSGEN030",
		"ZODSGEN031",
		"ZODSGEN032",
		"ZODSGEN033",
		"ZODSGEN034",
		"ZODSGEN035",
		"ZODSGEN036",
		"ZODSASP001",
		"ZODSASP002",
		"ZODSASP003",
		"ZODSASP100",
		"ZODSASP101",
		// Release 2.0.2.
		"ZODSGEN037",
		"ZODSGEN038",
		"ZODSGEN039",
		"ZODSGEN040",
		"ZODSGEN041",
		"ZODSGEN042",
		"ZODSGEN043",
		"ZODSGEN044",
		// Release 2.1.0.
		"ZODSGEN045",
		// Release 2.2.0.
		"ZODSGEN046",
	];

	/// <summary>
	/// Rules added since the last release. A new or changed diagnostic is added here and moves into
	/// <c>AnalyzerReleases.Shipped.md</c> when it ships.
	/// </summary>
	static readonly string[] ExpectedUnshippedRuleIds =
	[
		"ZODSGEN047",
		"ZODSGEN048",
		"ZODSGEN049",
		"ZODSGEN050",
		"ZODSGEN051",
		"ZODSGEN052",
		"ZODSGEN053",
	];

	[Test]
	public async Task ShippedReleases_GivenEveryRelease_DeclareEveryDiagnosticId(CancellationToken cancellationToken)
	{
		// Arrange
		var markdown = await ReadReleaseFileAsync("AnalyzerReleases.Shipped.md", cancellationToken);

		// Act
		var ruleIds = ReadRuleIds(markdown);

		// Assert — each release block the catalogue has published, and the full rule set across them.
		await Assert.That(markdown).Contains("## Release 2.0.0");
		await Assert.That(markdown).Contains("## Release 2.0.2");
		await Assert.That(markdown).Contains("## Release 2.1.0");
		await Assert.That(SortedRuleIds(ruleIds)).IsEqualTo(SortedRuleIds(ExpectedShippedRuleIds));
	}

	[Test]
	public async Task UnshippedRelease_GivenUnreleasedRules_DeclaresExactlyThoseRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var markdown = await ReadReleaseFileAsync("AnalyzerReleases.Unshipped.md", cancellationToken);

		// Act
		var ruleIds = ReadRuleIds(markdown);

		// Assert — the header stays so the file keeps its purpose, and it declares exactly the unreleased rules.
		await Assert.That(markdown).Contains("; Unshipped analyzer release");
		await Assert.That(SortedRuleIds(ruleIds)).IsEqualTo(SortedRuleIds(ExpectedUnshippedRuleIds));
	}

	[Test]
	public async Task ReleaseFiles_GivenAllDiagnostics_TrackEachRuleIdExactlyOnce(CancellationToken cancellationToken)
	{
		// Arrange
		var shippedRuleIds = ReadRuleIds(await ReadReleaseFileAsync("AnalyzerReleases.Shipped.md", cancellationToken));
		var unshippedRuleIds = ReadRuleIds(
			await ReadReleaseFileAsync("AnalyzerReleases.Unshipped.md", cancellationToken)
		);

		// Act
		var duplicatedShippedRuleIds = shippedRuleIds
			.GroupBy(static id => id, StringComparer.Ordinal)
			.Where(static group => group.Count() > 1)
			.Select(static group => group.Key)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToArray();
		var rulesInBothFiles = shippedRuleIds
			.Intersect(unshippedRuleIds, StringComparer.Ordinal)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToArray();

		// Assert
		await Assert.That(string.Join(",", duplicatedShippedRuleIds)).IsEqualTo(string.Empty);
		await Assert.That(string.Join(",", rulesInBothFiles)).IsEqualTo(string.Empty);
		await Assert.That(shippedRuleIds.Length).IsEqualTo(ExpectedShippedRuleIds.Length);
		await Assert.That(unshippedRuleIds.Length).IsEqualTo(ExpectedUnshippedRuleIds.Length);
	}

	static async Task<string> ReadReleaseFileAsync(string fileName, CancellationToken cancellationToken) =>
		await File.ReadAllTextAsync(Path.Combine(ReleaseDirectory, fileName), cancellationToken);

	static string[] ReadRuleIds(string markdown) =>
		[.. RuleIdRegex().Matches(markdown).Select(static match => match.Groups[1].Value)];

	[GeneratedRegex(@"(?m)^[ \t]*\|?[ \t]*(ZODS[A-Z]{3}\d{3})[ \t]*\|")]
	private static partial Regex RuleIdRegex();

	static string SortedRuleIds(IEnumerable<string> ruleIds) =>
		string.Join(",", ruleIds.Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal));
}
