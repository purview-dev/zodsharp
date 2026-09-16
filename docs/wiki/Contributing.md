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

This wiki lives in `docs/wiki/`. The site build (purview-dev Astro/Starlight) pulls these files via `github-path` sync and rewrites relative `.md` links into site routes.

Conventions:

- `_Sidebar.md` declares page order (parsed by the sync, never rendered).
- Every page starts with a single `# ` heading (the page title) followed by a one-paragraph description.
- Link to other pages with relative `.md` links: `[Getting Started](Getting-Started.md)`.
- GitHub alert blockquotes (`> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`, `> [!CAUTION]`, `> [!IMPORTANT]`) are converted to Starlight asides.
- Relative repository-path links (e.g. `src/src/ZodSharp/Z.cs`) are rewritten to GitHub blob URLs automatically.
- Keep the `CrossPlatformUserSchema` (C#) and `UserSchema` (TypeScript) in sync when either changes.

## Formatting

Formatting is enforced with CSharpier. `.editorconfig` at the repo root defines style (tabs for code, 2-space for XML/JSON/YAML/markdown).