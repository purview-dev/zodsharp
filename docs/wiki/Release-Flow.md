# Release Flow

Purview.ZodSharp releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the GitHub Actions workflows in `.github/workflows/`.

## Versioning

The package version comes from `package.json` (`version` field). The current stable line is `2.4.1`; bump `package.json` to release a new version (prerelease builds use a `MAJOR.MINOR.PATCH-prerelease.N` suffix).

Package identities are `Purview.ZodSharp.*` (core, SystemTextJson, NewtonsoftJson, AspNetCore). Central package management lives in `Directory.Packages.props`; package versions there are minimum requirements, not exact pins, so the resolved graph can drift.

## Releasing a version

A push to `main` publishes, so everything below has to be true *before* the merge.

1. **Rename the changelog heading to the version being released.** `CHANGELOG.md`'s newest `## <version>` heading is the one that ships; there is no `Unreleased` heading, precisely so a merge cannot publish an unnamed release. Consumer-visible renames and signature changes go under that version's `### Upgrade notes`, each naming the old form, the new form and the edit to make.
2. **Move new diagnostics into a shipped block.** Anything in `AnalyzerReleases.Unshipped.md` moves to a `## Release <version>` block in `AnalyzerReleases.Shipped.md`. A rule stays recorded under the release that *introduced* it, so earlier blocks are never rewritten.
3. **Bump the package-validation baseline.** `src/Directory.Build.targets` sets `PackageValidationBaselineVersion` to the previous stable release; it must name the version currently on NuGet, not the one being released. The build then fails `dotnet pack` on an unintended public-API change. An intended one needs a `CompatibilitySuppressions.xml` beside the owning project (`dotnet pack /p:GenerateCompatibilitySuppressionFile=true`) *and* a matching upgrade note — the two are each other's evidence.
4. **Run `just pipeline-pr`**, which covers restore, build, lint, tests, pack and pack validation in one pass.

## Workflows

| Workflow | Trigger | Pipeline mode |
|---|---|---|
| `pr.yml` | pull requests against `main` | `purview-build.yml` with `run-pack: true`, `validate-pack: true` |
| `release.yml` | pushes to `main` | `purview-release.yml` with `release-mode: NuGet` |

Both consume `purview-build.json`:

- `Build` — solution (`src/ZodSharp.slnx`), test root (`src/tests`), patterns (`*Tests.csproj`), filter (`/*/*/*/*`).
- `PackValidation` — requires symbol packages/files and validates `RequiredContent` per package (per-TFM DLL + XML, analyzer assemblies and `buildTransitive/Purview.ZodSharp.props` for the core package, `README.md` and `purview-logo-light.png` for every package).
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