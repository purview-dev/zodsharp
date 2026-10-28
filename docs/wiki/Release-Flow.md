# Release Flow

Purview.ZodSharp releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the GitHub Actions workflows in `.github/workflows/`.

## Versioning

The package version comes from `package.json` (`version` field). The current stable line is `2.4.2`, and `3.0.0` (which drops `net8.0` and `net9.0`) is in prerelease; bump `package.json` to release a new version (prerelease builds use a `MAJOR.MINOR.PATCH-prerelease.N` suffix).

Package identities are `Purview.ZodSharp.*` (core, SystemTextJson, NewtonsoftJson, AspNetCore). Central package management lives in `Directory.Packages.props`; package versions there are minimum requirements, not exact pins, so the resolved graph can drift.

## Releasing a version

A push to `main` publishes, so everything below has to be true *before* the merge.

1. **Rename the changelog heading to the version being released.** `CHANGELOG.md`'s newest `## <version>` heading is the one that ships; there is no `Unreleased` heading, precisely so a merge cannot publish an unnamed release. Consumer-visible renames and signature changes go under that version's `### Upgrade notes`, each naming the old form, the new form and the edit to make.
2. **Move new diagnostics into a shipped block.** Anything in `AnalyzerReleases.Unshipped.md` moves to a `## Release <version>` block in `AnalyzerReleases.Shipped.md`. A rule stays recorded under the release that *introduced* it, so earlier blocks are never rewritten.
3. **Bump the package-validation baseline.** `src/Directory.Build.targets` sets `PackageValidationBaselineVersion` to the previous stable release; it must name the version currently on NuGet, not the one being released. The build then fails `dotnet pack` on an unintended public-API change. An intended one needs a `CompatibilitySuppressions.xml` beside the owning project (`dotnet pack /p:GenerateCompatibilitySuppressionFile=true`) *and* a matching upgrade note — the two are each other's evidence.
4. **Run `just pipeline-pr`**, which covers restore, build, lint, tests, pack and pack validation in one pass.
5. **Run the [pre-release checklist](#pre-release-checklist)**, which adds the gates that consume the packed packages
   and the Native AOT and WebAssembly publishes.

## Release gates

Each gate proves one thing the others cannot. Most build against ProjectReferences, which is fast and debuggable but
is not what ships; the packed gates consume the `.nupkg` files themselves. A gate that has nothing to check fails
rather than passing.

| Gate | What it proves | Local | CI |
|---|---|---|---|
| Build, lint and tests | The solution builds warning-free, CSharpier reports no change, and every test project passes (including the cross-platform vitest suites, through `Repository.UnitTests`). | `just build`, `just lint-check`, `just test` | `pr.yml` `build` |
| Pack and pack validation | Every package packs; package validation holds the public API to the 2.4.2 baseline; `purview-build.json` `PackValidation` finds exactly the declared content (`RequiredContent`, `RequireExplicitContent`) and none of the `ForbiddenContent`. | `just pipeline-pack-validate` | `pr.yml` `build` (`run-pack`, `validate-pack`) |
| Forbidden-content rule proof | Every `ForbiddenContent` rule rejects a package: a matching file is injected into scratch copies of each package and the real `purview-build` validation must fail, per package and per rule. | `just verify-pack-rules` | `pr.yml` `packed-consumers` |
| Packed generator | The IL-merged generator in the `.nupkg` loads and generates in a net10.0 consumer (it has regressed twice while the in-repo tests, which use the unmerged generator, stayed green). | `just smoke-packed-generator` | `pr.yml` `packed-generator` |
| Packed consumers | Every shipping package, packed to an isolated feed and cache, works for consumers that reference packages only: a Native AOT console (generated validation, member resolution, count attributes, JSON Schema export, the validation bridge, ProblemDetails, OpenAPI options), a Blazor WebAssembly app with `<ZodValidator>` and a scalar value object carrying a type-level rule (trimmed, and `RunAOTCompilation` when `wasm-tools` is installed; `_framework` holds the ZodSharp assemblies, `_content` the static web assets) and a Native AOT minimal API (an invalid POST returns 400 `application/problem+json` with `issues`; the OpenAPI document carries `ZodValidationProblemDetails`), and a multi-project API - a `[ZodSchema]`/`[ErrorType]` class library referenced by a minimal API that references `Microsoft.AspNetCore.OpenApi` with XML documentation on (the OpenAPI document is served with both projects' schemas and XML-comment descriptions, and no documentation ID is duplicated). Every bin and publish folder is leak-scanned. | `just smoke-packed-consumers` | `pr.yml` `packed-consumers` (linux-x64, `-WasmAot Require`) |
| Compiler-host leak scan | No generator, code-fix, Purview.SourceGeneratorFramework or Roslyn assembly reaches build or publish output, by file name or in a `deps.json`, `runtimeconfig.json` or boot manifest. Purview.SourceGeneratorFramework.dll once leaked this way and broke Blazor WebAssembly AOT while every gate passed. | `scripts/assert-no-compiler-host-assemblies.ps1 -Path <dir>` (`just scan-compiler-host-leaks <dir>`); every publish recipe below runs it | a step in every job that publishes; `ReleaseGateTests` fails a job that publishes without it |
| Trim and Native AOT harness | The whole trim-safe surface is warning-free under ILC with every ZodSharp assembly rooted (IL3054 alone tolerated, see [Trimming and Native AOT](Trimming-and-AOT.md#the-rooted-publish-and-il3054-generic-recursion)), and again as a plain consumer; each native binary checks its behaviour. | `just verify-trim <rid>` | `pr.yml` `verify-trim` |
| Native AOT sample | Examples.MinimalApi publishes with `PublishAot=true` and no IL warning, and the native binary returns the ProblemDetails contract. | `just publish-aot-sample` | `pr.yml` `aot-sample` |
| WebAssembly size budget | Examples.Blazor.Client publishes trimmed with no IL warning, and each ZodSharp assembly stays within 10% of `.config/wasm-size-baseline.json`. | `just wasm-size` | `pr.yml` `wasm-trimmed` |
| WebAssembly AOT and browser | Examples.Blazor.Client compiles with `RunAOTCompilation=true`; the published Blazor sample passes its browser checks. | `just wasm-size -p:RunAOTCompilation=true` | `wasm-nightly.yml` `wasm-aot`, `blazor-e2e` |

`ReleaseGateTests` (in `src/tests/Repository.UnitTests`, so `just test` runs it) guards the gates themselves: the leak
scan must catch each kind of leak it claims to, every workflow job that publishes must scan its output, the
`packed-consumers` job must run the gate and the rule proof, and `ForbiddenContent` must keep every rule.

### Purview.SourceGeneratorFramework

The generator is built against the Purview.SourceGeneratorFramework version `Directory.Packages.props` pins. Until that
version is on nuget.org, CI cannot restore it: the `packed-consumers` job fails at its first step with what to do,
rather than skipping. Publish it, or set the `ZODSHARP_SGF_FEED` repository variable to a feed that has it. Locally,
`just smoke-packed-consumers` also reads `ZODSHARP_SGF_FEED` or `LOCAL_NUGET_FEED_PATH`, and uses that feed for
Purview.SourceGeneratorFramework only.

### Pre-release checklist

Run these on a clean tree before bumping and merging a release. Each must pass.

```powershell
$env:LOCAL_NUGET_FEED_PATH = 'P:\_sync-projects\.local-nuget'   # wherever Purview.SourceGeneratorFramework is, until it is on nuget.org

just scrub                                    # delete bin/obj, clean, force-restore, stop build servers
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
                                              # restore, build, lint, tests, pack, pack validation, local publish
just smoke-packed-generator                   # the IL-merged generator loads and generates from the .nupkg
just smoke-packed-consumers                   # Native AOT, Blazor WebAssembly, minimal API and multi-project API consumers
just wasm-size                                # trimmed WebAssembly publish, size budget and leak scan
just verify-trim <rid>                        # rooted and consumer Native AOT harness, e.g. win-x64 or linux-x64
just publish-aot-sample                       # Native AOT minimal API sample and leak scan
```

`just verify-pack-rules` re-proves the forbidden-content rules against the packages in `artifacts/` (after `just pipeline-local-release`, which moves them to the local feed, pass `-PackagesFolder <feed>`); run it after changing
`PackValidation`.

## Workflows

| Workflow | Trigger | Pipeline mode |
|---|---|---|
| `pr.yml` | pull requests against `main` | `purview-build.yml` with `run-pack: true`, `validate-pack: true` |
| `release.yml` | pushes to `main` | `purview-release.yml` with `release-mode: NuGet` |

Both consume `purview-build.json`:

- `Build` — solution (`src/ZodSharp.slnx`), test root (`src/tests`), patterns (`*Tests.csproj`), filter (`/*/*/*/*`).
- `PackValidation` — requires symbol packages/files and validates `RequiredContent` per package (per-TFM DLL + XML, analyzer assemblies and `buildTransitive/Purview.ZodSharp.props` for the core package, `README.md` and `purview-logo-light.png` for every package). `ForbiddenContent` rejects, in every package, a compiler-host assembly (`Purview.SourceGeneratorFramework*`, `Microsoft.CodeAnalysis*`) anywhere NuGet hands files to an application (the root, `lib/`, `ref/`, `runtimes/`, `build*/`, `tools/`, `content*/`, `staticwebassets/`), a loose copy of either under `analyzers/`, and the generator or code fixes under `lib/`, `ref/` or `runtimes/`.
- `Release.Mode` — `None` for PR/local runs.

## Pipeline commands

```bash
just pipeline-pr             # restore, build, lint, tests, pack, validate pack
just pipeline-build          # restore, build, lint (no tests, no release)
just pipeline-tests          # pipeline with tests enabled
just pipeline-release        # pack, publish, GitHub release (NuGet mode)
just pipeline-local-release  # pack + publish to a local NuGet feed
```

See the `Justfile` for the full command set (`just build`, `just test`, `just lint-check`, `just lint-fix`, `just pack`, `just perf-tests`).

## Commit conventions

Commits must follow [Conventional Commits](https://www.conventionalcommits.org/), enforced by Lefthook + Commitlint (`.config/lefthook.yml` and `commitlint.config.mts`). Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.

## Quality gates

- `just build` succeeds with no new warnings/errors.
- Relevant tests pass (`just test`).
- `just lint-check` (CSharpier) reports no formatting changes.
- Packed packages match `purview-build.json` `PackValidation`.
- Generated code is deterministic and reviewable.
- Before a release, every gate in [Release gates](#release-gates) passes, through the
  [pre-release checklist](#pre-release-checklist).