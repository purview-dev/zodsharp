using System.Text;
using System.Text.RegularExpressions;

namespace ZodSharp.Repository;

/// <summary>
/// Guards the shape of the documentation set in <c>docs/wiki</c>: every page sits in a topic folder, is listed once
/// in <c>_Sidebar.md</c>, is reachable from <c>Home.md</c>, and its links resolve to pages that exist.
/// </summary>
/// <remarks>
/// <para>
/// The folder tree is load-bearing, not decorative: purview.dev publishes each page at
/// <c>/docs/zodsharp/&lt;folder&gt;/&lt;page&gt;/</c> and resolves every relative link against the page that contains
/// it, so a page's location is part of its URL and a link is only correct relative to the file it is written in.
/// These tests read the tree the way the sync does - paths relative to <c>docs/wiki</c>, links resolved from the
/// containing file - which is why they can catch a page that exists but is published nowhere discoverable.
/// </para>
/// <para>
/// Every assertion names the offending file, because the failure mode is silence: the site publishes whatever it
/// finds, in whatever order it finds it, and a page missing from the sidebar simply has no entry there.
/// </para>
/// </remarks>
public sealed partial class DocumentationWikiTests
{
	const string SidebarFile = "_Sidebar.md";
	const string HomeFile = "Home.md";
	const string LandingPage = "Getting-Started.md";

	/// <summary>
	/// The pages that must stay at the wiki root: the site's landing page (<c>docs.rootPage</c>) and the two pages
	/// its project catalogue deep-links through <c>docsPage</c>. That slug is a single path segment and must exist
	/// in every documentation channel, so those pages keep root-level URLs.
	/// </summary>
	static readonly string[] RootPages = ["AspNetCore-Integration.md", "Fluent-Schema-API.md", "Getting-Started.md"];

	static readonly Lazy<string> WikiFolder = new(FindWikiFolder);

	[Test]
	public async Task WikiFolder_ResolvesToThisRepository()
	{
		// Arrange
		var folder = WikiFolder.Value;

		// Act
		var indexFiles = new[] { SidebarFile, HomeFile, LandingPage }
			.Where(name => File.Exists(Path.Combine(folder, name)))
			.ToArray();

		// Assert - without the repository marker walk landing here, every other test in this class passes
		// vacuously against files that were never found.
		await Assert
			.That(indexFiles)
			.IsEquivalentTo([SidebarFile, HomeFile, LandingPage])
			.Because($"'{folder}' does not look like this repository's docs/wiki folder");

		await Assert.That(PageFiles()).IsNotEmpty();
	}

	[Test]
	public async Task Pages_GivenEveryPage_LiveAtTheRootOrInOneTopicFolder()
	{
		// Arrange
		var pages = PageFiles();

		// Act - a page's folder is the first segment of its published URL, so two levels of nesting would make
		// the URL say more about the wiki's filing than about the page.
		var tooDeep = pages.Where(path => path.Split('/').Length > 2).ToArray();
		var folders = pages
			.Select(path => path.Split('/'))
			.Where(segments => segments.Length > 1)
			.Select(segments => segments[0])
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();

		// Assert
		await Assert
			.That(tooDeep)
			.IsEmpty()
			.Because("every page belongs at the wiki root or in exactly one folder: " + string.Join(", ", tooDeep));

		await Assert
			.That(folders)
			.IsEquivalentTo(["concepts", "development", "generator", "hosting", "quality", "schemas", "serialization"])
			.Because($"the topic folders are documented in Contributing.md; found: {string.Join(", ", folders)}");
	}

	[Test]
	public async Task RootPages_GivenTheSiteContracts_HoldTheLandingAndDeepLinkedPages()
	{
		// Arrange
		var rootPages = PageFiles().Where(path => !path.Contains('/')).ToArray();

		// Act
		var unexpected = rootPages.Except(RootPages, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
		var missing = RootPages.Except(rootPages, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

		// Assert - Getting-Started.md is docs.rootPage, and the other two are the docsPage deep links the site's
		// catalogue resolves against the default channel. Moving any of them changes a published URL that another
		// repository points at.
		await Assert
			.That(unexpected)
			.IsEmpty()
			.Because(
				"a page at the wiki root needs a site contract behind it (docs.rootPage or a docsPage deep link); "
					+ "otherwise it belongs in a topic folder: "
					+ string.Join(", ", unexpected)
			);

		await Assert
			.That(missing)
			.IsEmpty()
			.Because("these pages must stay at the wiki root: " + string.Join(", ", missing));
	}

	[Test]
	public async Task Pages_GivenEveryFileAndFolder_UseUrlSafeNames()
	{
		// Arrange
		var pages = PageFiles();

		// Act - a page's folder and file name become its URL, lowercased: `schemas/String-Validation.md` is published
		// at /docs/zodsharp/schemas/string-validation/. A space, a dot or a non-ASCII character would be rewritten
		// into a dash by the sync, so the address would stop mirroring the tree even though it stayed legal.
		var unsafeNames = pages
			.SelectMany(path => path.Split('/'))
			.Select(segment => segment.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? segment[..^3] : segment)
			.Where(segment => !UrlSafeNamePattern().IsMatch(segment))
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();

		// Assert - only letters, digits and single dashes, so no published URL needs percent-encoding.
		await Assert
			.That(unsafeNames)
			.IsEmpty()
			.Because(
				"folders and page names become URL segments verbatim, so they must be letters, digits and dashes: "
					+ string.Join(", ", unsafeNames)
			);
	}

	[Test]
	public async Task Sidebar_GivenEveryPageOnDisk_ListsItExactlyOnce(CancellationToken cancellationToken)
	{
		// Arrange
		var sidebar = await ReadWikiFileAsync(SidebarFile, cancellationToken);
		var listed = PageTargets(sidebar, SidebarFile);
		var pages = PageFiles();

		// Act
		var unlisted = pages.Except(listed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
		var duplicated = listed
			.GroupBy(path => path, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => $"{group.Key} (listed {group.Count()} times)")
			.Order(StringComparer.Ordinal)
			.ToArray();

		// Assert - the sidebar is the page-order authority for the published site, so an unlisted page is
		// published last, and a duplicated one has two competing positions.
		await Assert
			.That(unlisted)
			.IsEmpty()
			.Because(
				$"every page must appear in {SidebarFile}, inside its section's list, by its path from docs/wiki: "
					+ string.Join(", ", unlisted)
			);

		await Assert
			.That(duplicated)
			.IsEmpty()
			.Because($"a page may appear in {SidebarFile} only once: " + string.Join(", ", duplicated));
	}

	[Test]
	public async Task Home_GivenEveryPageOnDisk_LinksToIt(CancellationToken cancellationToken)
	{
		// Arrange
		var home = await ReadWikiFileAsync(HomeFile, cancellationToken);
		var linked = PageTargets(home, HomeFile);
		var pages = PageFiles();

		// Act
		var unlisted = pages.Except(linked, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

		// Assert - Home.md is the index a reader browses the folders with, so a page missing from it is invisible
		// to anyone who did not already know its name. A page may be linked more than once - the feature
		// highlights cross-reference a few pages - which is why only reachability is asserted here; exactly-once
		// ordering belongs to the sidebar.
		await Assert
			.That(unlisted)
			.IsEmpty()
			.Because($"every page must appear in {HomeFile}: " + string.Join(", ", unlisted));
	}

	[Test]
	public async Task Home_GivenEverySidebarSection_HasAMatchingHeading(CancellationToken cancellationToken)
	{
		// Arrange
		var sidebar = StripCode(await ReadWikiFileAsync(SidebarFile, cancellationToken));
		var home = StripCode(await ReadWikiFileAsync(HomeFile, cancellationToken));

		// Act - the labels are what the sidebar shows and the headings are what Home.md shows; a section renamed
		// in one place only leaves the two disagreeing about what the wiki contains.
		var labels = SectionLabelPattern().Matches(sidebar).Select(match => match.Groups[1].Value.Trim()).ToArray();
		var headings = Heading2Pattern().Matches(home).Select(match => match.Groups[1].Value.Trim()).ToArray();
		var unmatched = labels.Except(headings, StringComparer.Ordinal).ToArray();

		// Assert
		await Assert.That(labels).IsNotEmpty();
		await Assert
			.That(unmatched)
			.IsEmpty()
			.Because(
				$"every '{SidebarFile}' section needs a matching '## ' heading in {HomeFile}: "
					+ string.Join(", ", unmatched)
			);
	}

	[Test]
	public async Task WikiFiles_GivenEveryRelativeMarkdownLink_ResolveToAnExistingPage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var wikiFiles = PageFiles().Append(SidebarFile).Append(HomeFile).ToArray();
		List<string> broken = new();

		// Act
		foreach (var file in wikiFiles)
		{
			var markdown = StripCode(await ReadWikiFileAsync(file, cancellationToken));
			foreach (var target in MarkdownLinkTargets(markdown))
			{
				var path = target.Split('#', 2)[0].Split('?', 2)[0];
				if (!IsRelativeMarkdownLink(path))
				{
					continue;
				}

				var resolved = ResolveRelative(file, path);
				if (resolved is null)
				{
					// A link above docs/wiki is a repository path, which the site rewrites to a GitHub blob URL.
					continue;
				}

				if (!File.Exists(Path.Combine(WikiFolder.Value, resolved.Replace('/', Path.DirectorySeparatorChar))))
				{
					broken.Add($"{file} -> {target}");
				}
			}
		}

		// Assert - a link is resolved against the page it appears on, so a page that moved without its incoming
		// links being updated breaks only for the reader who follows it, and the site rewrites the target to a
		// GitHub blob URL rather than failing its own build.
		await Assert
			.That(broken)
			.IsEmpty()
			.Because("these links point at pages that do not exist: " + string.Join(", ", broken));
	}

	[Test]
	public async Task Pages_GivenEveryPage_OpenWithASingleH1Title(CancellationToken cancellationToken)
	{
		// Arrange
		List<string> problems = new();

		// Act
		foreach (var page in PageFiles())
		{
			var markdown = StripCode(await ReadWikiFileAsync(page, cancellationToken));
			var lines = markdown.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
			var titles = lines.Count(line => Heading1Pattern().IsMatch(line));

			if (titles != 1)
			{
				problems.Add($"{page} has {titles} '# ' headings");
			}
			else if (!Heading1Pattern().IsMatch(lines[0]))
			{
				problems.Add($"{page} does not open with its '# ' heading");
			}
		}

		// Assert - the first heading is the title the published sidebar shows, so a page without exactly one is
		// either untitled or titled with whichever section heading happens to come first.
		await Assert.That(problems).IsEmpty().Because(string.Join("; ", problems));
	}

	/// <summary>Every page in the wiki, as a path relative to <c>docs/wiki</c> with <c>/</c> separators.</summary>
	static string[] PageFiles() =>
		Directory
			.GetFiles(WikiFolder.Value, "*.md", SearchOption.AllDirectories)
			.Select(RelativeToWiki)
			.Where(path => path is not SidebarFile and not HomeFile)
			.Order(StringComparer.Ordinal)
			.ToArray();

	static string RelativeToWiki(string absolutePath) =>
		Path.GetRelativePath(WikiFolder.Value, absolutePath).Replace('\\', '/');

	/// <summary>
	/// The pages a document links to, as paths relative to <c>docs/wiki</c>. Links are resolved against the
	/// document's own folder, which is what both the site and GitHub do; a link that climbs above the wiki folder
	/// is a repository path and is dropped.
	/// </summary>
	static string[] PageTargets(string markdown, string fromPath) =>
		MarkdownLinkTargets(StripCode(markdown))
			.Select(target => target.Split('#', 2)[0].Split('?', 2)[0])
			.Where(IsRelativeMarkdownLink)
			.Select(target => ResolveRelative(fromPath, target))
			.OfType<string>()
			.ToArray();

	/// <summary>Resolves a link the way a browser does, or null when it points outside the wiki folder.</summary>
	static string? ResolveRelative(string fromPath, string target)
	{
		if (target.StartsWith('/') || target.Contains("://", StringComparison.Ordinal))
		{
			return null;
		}

		List<string> segments = new();
		var fromSegments = fromPath.Split('/');
		foreach (var segment in fromSegments[..^1])
		{
			segments.Add(segment);
		}

		foreach (var segment in target.Split('/'))
		{
			if (segment is "" or ".")
			{
				continue;
			}
			if (segment == "..")
			{
				if (segments.Count == 0)
				{
					return null;
				}
				segments.RemoveAt(segments.Count - 1);
				continue;
			}
			segments.Add(segment);
		}

		return segments.Count == 0 ? null : string.Join('/', segments);
	}

	static async Task<string> ReadWikiFileAsync(string path, CancellationToken cancellationToken) =>
		(
			await File.ReadAllTextAsync(
				Path.Combine(WikiFolder.Value, path.Replace('/', Path.DirectorySeparatorChar)),
				cancellationToken
			)
		)
		// Line endings are normalised so a markdown pattern does not have to tolerate CRLF; the files are
		// checked out with whatever the machine's autocrlf setting produces.
		.Replace("\r\n", "\n", StringComparison.Ordinal);

	/// <summary>
	/// Removes fenced blocks and inline code spans. Neither is markdown structure - a quoted `# heading` or a
	/// quoted link is an example - so reading them would report problems the reader never sees.
	/// </summary>
	static string StripCode(string markdown)
	{
		StringBuilder builder = new(markdown.Length);
		var inFence = false;

		foreach (var line in markdown.Split('\n'))
		{
			if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
			{
				inFence = !inFence;
				continue;
			}

			if (!inFence)
			{
				builder.Append(InlineCodePattern().Replace(line, string.Empty)).Append('\n');
			}
		}

		return builder.ToString();
	}

	static string[] MarkdownLinkTargets(string markdown) =>
		LinkPattern().Matches(markdown).Select(match => match.Groups[1].Value.Trim()).ToArray();

	/// <summary>
	/// Whether a link target names a page: relative, not anchored, and neither an external URL nor a repository
	/// path.
	/// </summary>
	static bool IsRelativeMarkdownLink(string path) =>
		path.Length > 0
		&& !path.StartsWith('#')
		&& !path.StartsWith('/')
		&& !path.Contains("://", StringComparison.Ordinal)
		&& path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

	static string FindWikiFolder()
	{
		for (
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			directory is not null;
			directory = directory.Parent
		)
		{
			var wiki = Path.Combine(directory.FullName, "docs", "wiki");
			if (
				File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
				&& File.Exists(Path.Combine(wiki, SidebarFile))
				&& File.Exists(Path.Combine(wiki, HomeFile))
			)
			{
				return wiki;
			}
		}

		throw new InvalidOperationException(
			$"No 'docs/wiki' carrying {SidebarFile} and {HomeFile} above '{AppContext.BaseDirectory}'."
		);
	}

	[GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)\)")]
	private static partial Regex LinkPattern();

	[GeneratedRegex(@"`[^`\n]*`")]
	private static partial Regex InlineCodePattern();

	[GeneratedRegex(@"^#\s", RegexOptions.Multiline)]
	private static partial Regex Heading1Pattern();

	[GeneratedRegex(@"^##\s+(.+?)\s*$", RegexOptions.Multiline)]
	private static partial Regex Heading2Pattern();

	[GeneratedRegex(@"^[ \t]*-[ \t]+\*\*(.+?)\*\*[ \t]*$", RegexOptions.Multiline)]
	private static partial Regex SectionLabelPattern();

	[GeneratedRegex(@"^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")]
	private static partial Regex UrlSafeNamePattern();
}
