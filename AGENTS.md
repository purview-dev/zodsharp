# AGENTS.md

This file is the **primary** instruction set for agents working in this repository. Treat it as the source of truth for behavior, architecture context, testing standards, and completion criteria.

If another instruction file (for example `.github/copilot-instructions.md`) conflicts with this file, prefer this file unless that file explicitly states an exception.

## Repository overview

ZodSharp is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It uses struct-based rules and `Span<T>` to minimise allocations, and ships a compile-time source generator for maximum performance.

- Fork of [guinhx/ZodSharp](https://github.com/guinhx/ZodSharp), maintained at `purview-dev/zodsharp`.
- Public API namespaces are `ZodSharp.*`; packages and assemblies are published under the `Purview.ZodSharp.*` package IDs.
- Multi-targets `net8.0`, `net9.0` and `net10.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.

## Repository layout

```
assets/                Package assets (purview-logo.png) referenced by Directory.Build.props
src/
  ZodSharp.slnx        Solution entry point
  Directory.Build.props / Directory.Build.targets   Purview.DotNetProjectSdk imports
  src/
    ZodSharp/          Core validation library + JSON Schema export (Z.ToJsonSchema)
    SourceGenerators/  Compile-time [ZodSchema] generator (netstandard2.0, Roslyn)
    SystemTextJson/    System.Text.Json integration + JSON Schema import (Z.FromJsonSchema)
    NewtonsoftJson/    Newtonsoft.Json integration + JSON Schema import
    AspNetCore/        ASP.NET Core ProblemDetails integration
    Examples.CLI/      Usage examples
    Benchmarks/        BenchmarkDotNet performance suite
  tests/
    *.UnitTests/       TUnit test projects (ZodSharp, SystemTextJson, NewtonsoftJson, AspNetCore, SourceGenerators)
    cross-platform/    Output directory shared between the C# and TS cross-platform tests
src/ts/                TypeScript (Zod) schema + fixture generation; consumed by C# and vitest tests
tests/ts/              Vitest cross-platform tests
.agents/               Skills/prompts bundled from Purview.DotNetProjectSdk and Purview.SourceGeneratorFramework
```

## Build system

The build is driven by `Purview.DotNetProjectSdk`, imported via `src/Directory.Build.props` (`Sdk.props`) and `src/Directory.Build.targets` (`Sdk.targets`). Key configuration:

- `NamespacePrefix` is `ZodSharp`.
- Package version comes from `package.json` (SDK version detection).
- Package identities are `Purview.ZodSharp.*`; the SDK derives `AssemblyName`/`PackageId` from `RootNamespace` unless overridden explicitly.
- Package versions are managed centrally in `Directory.Packages.props` (Central Package Management).
- `global.json` pins the `Purview.DotNetProjectSdk` MSBuild SDK version.
- Pack layout for all packages is validated by `purview-build.json` (`PackValidation.RequiredContent`).

Consult `.agents/skills/sdk-configuration-reference/SKILL.md`, `.agents/skills/sdk-project-behavior-and-detection/SKILL.md`, and `.agents/skills/project-placement-defaults/SKILL.md` before changing build/SDK configuration or adding/moving projects.

## Commands

Build and test (see also the `Justfile`):

```bash
just build            # dotnet build src/ZodSharp.slnx -c Debug
just test             # dotnet test src/ZodSharp.slnx -c Debug --treenode-filter "/*/*/*/*"
just lint-check       # dotnet csharpier check .
just lint-fix         # dotnet csharpier format .
just pack             # dotnet pack src/ZodSharp.slnx -c Debug -o artifacts
just perf-tests       # dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Debug
just pipeline-pr      # PR pipeline via Purview.Build (restore, build, lint, tests)
just pipeline-release # Release pipeline (pack, publish, GitHub release)
```

TypeScript-side tooling uses Bun:

```bash
bun install
bun run test                # vitest run (cross-platform TS tests)
bun run generate-fixtures   # regenerates src/ts/fixtures/*.json from Zod
```

Formatting is enforced with CSharpier; `.editorconfig` at the repo root defines style (tabs for code, 2-space for XML/JSON/YAML/markdown).

## Testing

- Test framework is **TUnit** (SDK default; `TUnit` and `TUnit.Mocks` packages in `Directory.Packages.props`). Test projects live under `src/tests/*.UnitTests`.
- Follow the repository's testing bar:
  - `[Test]` methods taking a `CancellationToken cancellationToken` parameter.
  - `// Arrange`, `// Act`, `// Assert` comments.
  - Meaningful, descriptive method names (`Action_GivenCondition_ExpectedResult`).
  - Treat work as incomplete until the relevant tests pass.
- For source generators, diagnostic analyzers, code fixes and refactorings, use the `Purview.SourceGeneratorFramework.Testing.TUnit` base classes (`TUnitSourceGeneratorTestBase`, `TUnitDiagnosticAnalyzerTestBase`, `TUnitCodeFixTestBase`, `TUnitRefactoringTestBase`) and assert with `CodeQuery`. Load `.agents/skills/source-generator-testing/SKILL.md` and `.agents/skills/tunit-test-authoring/SKILL.md` before writing or changing these tests.

## Source generators

The `[ZodSchema]` attribute (generated into the `ZodSharp` namespace) marks a class or struct for compile-time validator generation. The generator produces a `{TypeName}Schema` static class with `Validate`/`Parse`, optional value-first composition methods (`ApplyAnd`, `ApplyOr`, `ApplyRefine`, controlled by `EnableComposition`, default `true`), and optional `IValidateOptions<T>` support.

The generator and analyzer are built with `Purview.SourceGeneratorFramework`:

- Use `CodeWriter` emission and incremental pipelines; load `.agents/skills/source-generator-codewriter-modernization/SKILL.md` before implementing, reviewing, or refactoring generator/analyzer code.
- Keep pipeline values immutable and value-equatable; never retain `ISymbol`, `Compilation`, `SemanticModel`, `IOperation`, `SyntaxNode`, or `Location` in pipeline models.
- Use `ForAttributeWithMetadataName` for attribute-driven discovery.
- Test incrementally, not just generated text (see the skills above).

## Packing and package READMEs

Each package ships its own `README.md`, placed in the project's `Sdk/` folder (for example `src/src/ZodSharp/Sdk/README.md`). The SDK's `PurviewAutoSdkPack` automatically maps `Sdk/*.md` to the package root and `Sdk/buildTransitive/**` to `buildTransitive/`, and the repo-root `README.md` is skipped when a package already packs its own README. Packages also ship `purview-logo.png` (linked via `src/Directory.Build.props`) and the core package ships `buildTransitive/Purview.ZodSharp.props`.

Keep `purview-build.json`'s `PackValidation` requirements in sync with any packaging change.

## Cross-platform fixtures

Schemas and fixtures shared between TypeScript/Zod and C#/ZodSharp live under `src/ts/`. The fixture generator (`bun run generate-fixtures`) writes JSON fixtures and a `manifest.json` that the C# tests consume; the C# cross-platform tests write output to `src/tests/cross-platform/output/`, which the vitest tests (`tests/ts/cross-platform.test.ts`) read. Keep the TS `UserSchema` and the C# `CrossPlatformUserSchema` in sync.

## Commit conventions

Commits must follow [Conventional Commits](https://www.conventionalcommits.org/), enforced by Lefthook + Commitlint (see `.config/lefthook.yml` and `commitlint.config.mts`). Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.

## Quality gates

- `just build` succeeds with no new warnings/errors.
- Relevant tests pass.
- `just lint-check` (CSharpier) reports no formatting changes.
- Generated code is deterministic and reviewable; no scope leaks in `CodeWriter` output.
- Packed packages match `purview-build.json` `PackValidation` (including per-package `README.md` and `purview-logo.png`).
