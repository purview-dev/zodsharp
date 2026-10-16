# AGENTS.md

This file is the **primary** instruction set for agents working in this repository. Treat it as the source of truth for behavior, architecture context, testing standards, and completion criteria.

If another instruction file (for example `.github/copilot-instructions.md`) conflicts with this file, prefer this file unless that file explicitly states an exception.

## Repository overview

Purview.ZodSharp is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It uses struct-based rules and `Span<T>` to minimise allocations, and ships a compile-time source generator for maximum performance.

- The project is maintained at `purview-dev/zodsharp`.
- Public API namespaces are `ZodSharp.*`; packages and assemblies are published under the `Purview.ZodSharp.*` package IDs.
- Multi-targets `net8.0`, `net9.0`, `net10.0` and `net11.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.

## Repository layout

```
assets/                Package assets (purview-logo-light.png) referenced by Directory.Build.props
src/
  ZodSharp.slnx        Solution entry point
  Directory.Build.props / Directory.Build.targets   Purview.BuildSdk imports
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
skills/                Agent skills this repository authors and publishes
.agents/skills/        Skills installed from other repositories by the skills CLI (gitignored)
```

## Build system

The build is driven by `Purview.BuildSdk`, imported via `src/Directory.Build.props` (`Sdk.props`) and `src/Directory.Build.targets` (`Sdk.targets`). Key configuration:

- `NamespacePrefix` is `ZodSharp`.
- Package version comes from `package.json` (SDK version detection).
- Package identities are `Purview.ZodSharp.*`; the SDK derives `AssemblyName`/`PackageId` from `RootNamespace` unless overridden explicitly.
- Package versions are managed centrally in `Directory.Packages.props` (Central Package Management).
- `global.json` pins the `Purview.BuildSdk` MSBuild SDK version.
- Pack layout for all packages is validated by `purview-build.json` (`PackValidation.RequiredContent`).

Before changing build/SDK configuration or adding/moving projects, install and consult the SDK's own skills: `npx skills add purview-dev/build-sdk` (`purview-buildsdk-configuration`, `purview-buildsdk-project-detection`, `purview-buildsdk-project-layout`).

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

`just test` also runs the vitest suites through CliWrap (see [Cross-platform fixtures](#cross-platform-fixtures)), so `bun run test` is for iterating on the TypeScript side rather than the only way to exercise it. It needs `bun install` to have been run.

Formatting is enforced with CSharpier; `.editorconfig` at the repo root defines style (tabs for code, 2-space for XML/JSON/YAML/markdown).

## Testing

- Test framework is **TUnit** (SDK default; `TUnit` and `TUnit.Mocks` packages in `Directory.Packages.props`). Test projects live under `src/tests/*.UnitTests`.
- Follow the repository's testing bar:
  - `[Test]` methods taking a `CancellationToken cancellationToken` parameter.
  - `// Arrange`, `// Act`, `// Assert` comments.
  - Meaningful, descriptive method names (`Action_GivenCondition_ExpectedResult`).
  - Treat work as incomplete until the relevant tests pass.
- For source generators, diagnostic analyzers, code fixes and refactorings, use the `Purview.SourceGeneratorFramework.Testing.TUnit` base classes (`TUnitSourceGeneratorTestBase`, `TUnitDiagnosticAnalyzerTestBase`, `TUnitCodeFixTestBase`, `TUnitRefactoringTestBase`) and assert with `CodeQuery`. Install the framework's skills with `npx skills add purview-dev/sourcegenerator-framework` and load `purview-sourcegenerator-testing` and `purview-sourcegenerator-tunit-tests` before writing or changing these tests.

## Source generators

The `[ZodSchema]` attribute (generated into the `ZodSharp` namespace) marks a class or struct for compile-time validator generation. The generator produces a `{TypeName}Schema` static class with `Validate`/`Parse`, optional value-first composition methods (`ApplyAnd`, `ApplyOr`, `ApplyRefine`, controlled by `EnableComposition`, default `true`), and optional `IValidateOptions<T>` support.

The generator and analyzer are built with `Purview.SourceGeneratorFramework`:

- Use `CodeWriter` emission and incremental pipelines; load `purview-sourcegenerator-codewriter` (`npx skills add purview-dev/sourcegenerator-framework`) before implementing, reviewing, or refactoring generator/analyzer code.
- Keep pipeline values immutable and value-equatable; never retain `ISymbol`, `Compilation`, `SemanticModel`, `IOperation`, `SyntaxNode`, or `Location` in pipeline models.
- Use `ForAttributeWithMetadataName` for attribute-driven discovery.
- Test incrementally, not just generated text (see the skills above).

Validation rules follow a conventions analyzer (`ValidationRuleConventionsAnalyzer`, diagnostic `ZODSGEN042`): a source-declared rule (a type implementing `ZodSharp.Core.IValidationRule<T>`) must expose its error identity as public `const string ErrorCode` and `const string MessageFormat` constants, so tests can assert against the rule rather than duplicating literals. New built-in rules must follow the same convention; keep `AnalyzerReleases.Shipped.md`/`AnalyzerReleases.Unshipped.md` in sync when a diagnostic is added or changed.

## Numeric rules

The numeric rules are split by the constraint each family needs — pick the constraint that matches the operations, never widen a rule unnecessarily:

- **Bound rules** — `MinValueRule<T>`, `MaxValueRule<T>`, `GreaterThanRule<T>`, `LessThanRule<T>`, `GreaterThanOrEqualRule<T>`, `LessThanOrEqualRule<T>` — are generic over `T : IComparable<T>`. Keep this constraint: `ZodDate` closes the bound rules with `DateTime`, which does **not** implement `INumber<T>`. Do not change them to `INumber<T>`.
- **Arithmetic rules** — `IntRule<T>`, `FiniteRule<T>`, `MultipleOfRule<T>`, `EvenRule<T>`, `OddRule<T>` — are generic over `T : INumber<T>` so they close with any numeric type (`int`, `long`, `double`, `decimal`, …).
- **`SafeIntegerRule`** is intentionally `double`-only, because "safe integer" is a JavaScript `Number` concept (`int.MinValue`..`int.MaxValue`); do not make it generic.
- `ZodNumber` closes the rules with `double`, `ZodBigInt` with `long`, and `ZodDate` with `DateTime`. Fluent methods on `ZodNumber` therefore use `XxxRule<double>`.
- Any code that matches a rule by name (for example the JSON Schema converter) must tolerate the generic arity suffix: a generic rule's `Type.Name` is `IntRule\`1`, so match with `StartsWith` rather than equality.

Generated rule attributes carry the rule's **value** parameters as a constructor: a parameter declared without a default is a required constructor argument (so it cannot be silently omitted), a parameter with a default keeps it, and `message`/`code`/`origin` stay properties. Attribute usages for required values are positional (`[MinValue(3)]`, `[Regex("^[a-z]+$")]`); defaulted values may still be set by property name. When changing rule constructor parameters, update the affected generator tests and the wiki attribute tables.

## Packing and package READMEs

Each package ships its own `README.md`, placed in the project's `Sdk/` folder (for example `src/src/ZodSharp/Sdk/README.md`). The SDK's `PurviewAutoSdkPack` automatically maps `Sdk/*.md` to the package root and `Sdk/buildTransitive/**` to `buildTransitive/`, and the repo-root `README.md` is skipped when a package already packs its own README. Packages also ship `purview-logo-light.png` (linked via `src/Directory.Build.props`) and the core package ships `buildTransitive/Purview.ZodSharp.props`.

Keep `purview-build.json`'s `PackValidation` requirements in sync with any packaging change.

## Cross-platform fixtures

Schemas and fixtures shared between TypeScript/Zod and C#/Purview.ZodSharp live under `src/ts/`. The fixture generator (`bun run generate-fixtures`) writes JSON fixtures and a `manifest.json` that the C# tests consume. Keep the TS `UserSchema` and the C# `CrossPlatformUserSchema` in sync.

`src/tests/cross-platform/` is gitignored, so it starts empty on a clean checkout and in CI: **the C# producers must run before vitest**, in the same workspace. There are **two** producer/consumer pairs, in different assemblies, namespaces and classes:

| Vitest consumer | Reads | C# producer |
| --- | --- | --- |
| `tests/ts/cross-platform.test.ts` | `src/tests/cross-platform/output/` | `ZodSharp.Json/*CrossPlatformTests` (the SystemTextJson and NewtonsoftJson unit tests) |
| `tests/ts/guid-uuid-parity.test.ts` | `src/tests/cross-platform/guid-parity/` | `ZodSharp.SourceGenerators/ZodSchemaGeneratorTests/JsonSchema_GivenGeneratedGuidSchemas_WritesZodParityArtifacts` |

`just test` runs the whole suite, so it covers both producers. The `cross-platform` job in `.github/workflows/pr.yml` selects them with treenode filters instead, and therefore needs **one step per producer** — a single `a|b` filter is not honoured as a top-level alternation and silently selects a broader set while skipping the parity producer. Adding a producer means adding a step. Both vitest suites fail loudly rather than passing vacuously when their directory is empty, which is the backstop for exactly this mistake.

`src/tests/Repository.UnitTests/CrossPlatformVitestTests.cs` additionally drives the vitest suites **from the .NET test run**, as child processes through CliWrap, so `just test` alone proves both halves of the guarantee and a TypeScript-side regression fails a local run. It is a deliberate duplicate of the CI job, not a replacement — the job stays the authoritative gate because it controls producer ordering and installs bun from scratch. Three rules keep the duplication honest:

- **One test per suite**, so a failure names the `.test.ts` file, and the child process's stdout/stderr becomes the assertion message.
- **`VitestSuites_GivenEveryTestFileOnDisk_AreAllCovered`** fails when a `tests/ts/*.test.ts` file has no entry in `Suites()`, so a new suite cannot be driven by the CI job alone.
- **These tests never skip.** A skipped test proves nothing, and the parity guarantee is not optional. The runner is resolved per run: **bun** when it is on PATH, otherwise **node**. If neither is available, or the install or the run fails, the test fails and names the step.

The node fallback invokes vitest's entry point (`node_modules/vitest/vitest.mjs`, from vitest's own `package.json` `bin` field) directly, rather than `npx` or `node_modules/.bin`. Both are unreliable: the `.bin` shims are written by whichever package manager installed the tree — a bun install leaves `vitest.exe`/`vitest.bunx` and no node shim — and on Windows `npx` resolves to a `.cmd`/`.ps1` shim, which cannot be started directly as a child process. For the same reason the PATH probe only accepts a real executable image (`.exe`/`.com`), not a shim.

`node_modules/` is gitignored, so when vitest is missing the tests install it first — with `bun install --frozen-lockfile`, or `npm install` when only node is present. There is no npm lockfile (`bun.lock` is the only one), so npm resolves from `package.json`; that is a second reason bun is preferred for both the install and the run.

Because `src/tests/cross-platform/` is gitignored and the producers live in other assemblies that may still be running in parallel, these tests wait for their fixture directory to fill before skipping, rather than nesting a second `dotnet test` inside the run.

## Commit conventions

Commits must follow [Conventional Commits](https://www.conventionalcommits.org/), enforced by Lefthook + Commitlint (see `.config/lefthook.yml` and `commitlint.config.mts`). Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.

## Quality gates

- `just build` succeeds with no new warnings/errors.
- Relevant tests pass.
- `just lint-check` (CSharpier) reports no formatting changes.
- Generated code is deterministic and reviewable; no scope leaks in `CodeWriter` output.
- Packed packages match `purview-build.json` `PackValidation` (including per-package `README.md` and `purview-logo-light.png`).

## Agent skills

Authored skills live in the repository-root `skills/` folder. **Nothing packs them into a NuGet
package** - `Purview.BuildSdk` 1.0.6 removed skill packaging outright. The skills CLI clones the *git*
repository and only discovers a root `skills/` folder, so that is the one delivery route.

`.agents/skills/` is the CLI's install location for skills from **other** repositories, and is
gitignored. This repository's own skills are never installed into it - that would leave two copies to
drift apart. Read them at their authored path, `skills/<name>/SKILL.md`.

```bash
npx skills add purview-dev/zodsharp
```

| Skill | Answers |
| --- | --- |
| `skills/purview-zodsharp-core/` | the runtime API: packages, the `Z` factory, `Validate`/`Parse`, `ValidationResult`, composition, coercion |
| `skills/purview-zodsharp-source-generator/` | `[ZodSchema]`: what it emits, its options, DataAnnotations, `OnZodValidate`, enum validation, `IValidateOptions` |
| `skills/purview-zodsharp-custom-rules/` | writing a rule, `ErrorCode`/`MessageFormat`, `IZodRule`, `[ZodRule]` attributes, scalar value objects |
| `skills/purview-zodsharp-json/` | System.Text.Json and Newtonsoft integrations, JSON Schema export and import, TypeScript Zod interop |
| `skills/purview-zodsharp-aspnetcore/` | ProblemDetails, minimal-API validation, `IZodSchemaFactory` registration, options validation, `ErrorType` |
| `skills/purview-zodsharp-diagnostics/` | what a `ZODSGEN` or `ZODSASP` diagnostic means and how to fix it |

Every skill carries the `purview-zodsharp` prefix. Skills install into one flat directory per agent,
shared with every skill a developer has installed from anywhere, so an unprefixed name collides and
the loser is whichever installed second.

To add one: create `skills/<name>/SKILL.md` with spec-legal frontmatter. There is no per-project
registration.

`src/tests/Repository.UnitTests/SkillTests.cs` validates every skill on every test run, through
`Purview.Skills.Testing`'s rule set (the Agent Skills specification: frontmatter fields, name and
description constraints, bundled links, body size). The `[InheritsTests]` attribute on that class is
load-bearing - TUnit does not walk base types during discovery, so without it the run is green and
nothing is checked. That project is single-targeted at `net10.0`, because it asserts something about
the repository rather than about the runtime's reach.

A relative link from one `SKILL.md` into another skill's folder is a rule violation: only a skill's
own folder is installed with it, so such a link works here and nowhere else. Name the other skill in
prose instead, and keep deep detail in the skill's own `references/`.
