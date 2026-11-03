# Contributing

Contributions are welcome. Please open an issue or pull request against [purview-dev/zodsharp](https://github.com/purview-dev/zodsharp).

## Repository layout

```
src/
  src/
    ZodSharp/          Core validation library + JSON Schema export (Z.ToJsonSchema)
    SourceGenerators/  Compile-time [ZodSchema] generator (netstandard2.0, Roslyn)
    SystemTextJson/    System.Text.Json integration + JSON Schema import
    NewtonsoftJson/    Newtonsoft.Json integration + JSON Schema import
    AspNetCore/        ASP.NET Core ProblemDetails integration
    Examples.CLI/      Usage examples
    Benchmarks/        BenchmarkDotNet performance suite
  tests/
    *.UnitTests/       TUnit test projects
src/ts/                TypeScript (Zod) schema + fixture generation
tests/ts/              Vitest cross-platform tests
docs/wiki/             This documentation suite
```

## Commands

```bash
just build            # dotnet build src/ZodSharp.slnx -c Debug
just test             # dotnet test src/ZodSharp.slnx -c Debug --treenode-filter "/*/*/*/*"
just lint-check       # dotnet csharpier check .
just lint-fix         # dotnet csharpier format .
just pack             # dotnet pack src/ZodSharp.slnx -c Debug -o artifacts
just perf-tests       # dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release
```

TypeScript tooling uses Bun:

```bash
bun install
bun run test                # vitest run (cross-platform TS tests)
bun run generate-fixtures   # regenerates src/ts/fixtures/*.json from Zod
```

## Testing bar

- Framework: **TUnit**.
- `[Test]` methods take a `CancellationToken cancellationToken` parameter where relevant.
- Use `// Arrange`, `// Act`, `// Assert` comments.
- Use meaningful, descriptive names (`Action_GivenCondition_ExpectedResult`).
- Treat work as incomplete until the relevant tests pass.

For source generator tests, use the `Purview.SourceGeneratorFramework.Testing.TUnit` base classes (`TUnitSourceGeneratorTestBase`, `TUnitDiagnosticAnalyzerTestBase`) and assert with `CodeQuery`; test incrementally, not just generated text.

## Documentation

This wiki lives in `docs/wiki/`, which is the documentation source for [purview.dev](https://purview.dev): the site build (purview-dev Astro/Starlight) pulls these files via `github-path` sync, publishes each file at `/docs/zodsharp/<folder>/<page>/`, and rewrites relative `.md` links into site routes. `_Sidebar.md` and `Home.md` are not published; the site's landing page is `Getting-Started.md` (`docs.rootPage` in the site's project catalogue).

### Layout

One folder per topic area, so a page can be found by what it is about rather than by remembering its file name:

```
docs/wiki/
  Getting-Started.md          The landing page (docs.rootPage)
  Fluent-Schema-API.md        Deep-linked by the site catalogue (docsPage)
  AspNetCore-Integration.md   Deep-linked by the site catalogue (docsPage)
  Home.md                     The index (not published)
  _Sidebar.md                 Page order (not published)
  concepts/                   Core Concepts; Composition and Transforms
  schemas/                    One page per schema family, plus the rule catalogue
  generator/                  Source Generator, DataAnnotations, Diagnostics
  serialization/              JSON Schema export and import, System.Text.Json, Newtonsoft.Json
  hosting/                    Microsoft.Extensions.Validation, OpenAPI, Blazor, HttpClient, DI, localization
  quality/                    Guarantees, performance, trimming/AOT, cross-platform interop
  development/                This page and the release flow
```

The pages are also grouped into seven sections, in reading order. The same seven sections, in the same order, are the groups in `_Sidebar.md`, the `## ` headings in `Home.md`, and the seven `Where to go next` bullets at the end of `Getting-Started.md` (the last of those name each section by the goal it serves rather than by its title).

| Section | Folder |
| --- | --- |
| Get started | the wiki root and `concepts/` |
| Validation reference | `schemas/`, plus `concepts/Composition-and-Transforms.md` |
| Source generator | `generator/` |
| JSON and serialization | `serialization/` |
| ASP.NET Core, Blazor and .NET 11 | `hosting/`, plus the root-level `AspNetCore-Integration.md` |
| Guarantees, performance and deployment | `quality/` |
| Contributing and releases | `development/` |

### Conventions

- **The folder is part of the URL.** The sync derives a page's slug from its path relative to `docs/wiki/`, so `schemas/Custom-Rules.md` is published at `/docs/zodsharp/schemas/custom-rules/`. Moving a page between folders therefore changes its URL.
- **File and folder names are URL-safe already.** Letters, digits and single dashes only (`SystemTextJson-Integration.md`, not `System Text JSON.md`): each name becomes an address segment verbatim, lowercased, so nothing has to be percent-encoded and the address mirrors the tree. The sync refuses to publish a path whose folder or file name reduces to nothing.
- **Three pages stay at the wiki root.** `Getting-Started.md` is the landing page, and `Fluent-Schema-API.md` / `AspNetCore-Integration.md` are the two pages the site's project catalogue deep-links through `docsPage`. That slug must be a single path segment (the catalogue schema rejects `/`) and must exist in both documentation channels, so those two keep root-level URLs and move into a folder only when the deep link moves with them.
- **`_Sidebar.md` is the page-order authority.** The sync reads its links in document order and orders the published sidebar accordingly, so every page appears there exactly once, in reading order, inside its section, with its path relative to the wiki root. Its bold labels are the section names; they are never rendered by the site, but they are what a reader of the file sees.
- **`Home.md` is the index.** It lists every page at least once, inside the section it belongs to, with a one-line "what it answers" - it is what a reader sees when browsing the folder on GitHub.
- **Every page starts with a single `# ` heading** (the page title, and the sidebar label the site uses) followed by a one-paragraph description.
- **Link to a sibling by file name, and to anything else by its path from the page**: `[Custom Rules](Custom-Rules.md)` within `schemas/`, `[Core Concepts](../concepts/Core-Concepts.md)` across folders, optionally with a fragment (`Core-Concepts.md#error-fields`). The sync resolves each link relative to the page it appears on.
- **Renaming or moving a page changes its published URL.** Add the old slug to `docs.redirects` in the site's `zodsharp.yml` so bookmarks and inbound links keep working; the generated redirect never shadows a real page, so it activates when the move is released.
- GitHub alert blockquotes (`> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`, `> [!CAUTION]`, `> [!IMPORTANT]`) are converted to Starlight asides.
- Relative repository-path links (e.g. `src/src/ZodSharp/Z.cs`) are rewritten to GitHub blob URLs automatically.
- Keep the `CrossPlatformUserSchema` (C#) and `UserSchema` (TypeScript) in sync when either changes.

### Adding a page

1. Add the file to the folder that covers the topic (`docs/wiki/schemas/My-Page.md`), starting with a single `# ` heading and a one-paragraph description. A page that does not fit an existing folder is a new topic area, and a new folder needs a new section - decide that deliberately rather than by adding to an unrelated one.
2. Add it to `_Sidebar.md` exactly once, inside that section's list and in the position a reader should meet it, with its path relative to the wiki root.
3. Add it to `Home.md` exactly once, in that section's table, with a one-line "what it answers".

`DocumentationWikiTests` (`src/tests/Repository.UnitTests`) enforces all three: every page is listed exactly once in the sidebar and is reachable from `Home.md`, every sidebar and page link resolves to an existing file, every sidebar section has a matching `## ` heading in `Home.md`, every folder holds at least one page listed in the sidebar, and every page opens with a single `# ` title. `just test` runs it.

## Formatting

Formatting is enforced with CSharpier. `.editorconfig` at the repo root defines style (tabs for code, 2-space for XML/JSON/YAML/markdown).
