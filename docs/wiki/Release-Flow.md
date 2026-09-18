# Release Flow

ZodSharp releases are driven by the shared [purview-dev/build](https://github.com/purview-dev/build) pipeline through the GitHub Actions workflows in `.github/workflows/`.

## Versioning

The package version comes from `package.json` (`version` field). The repo is currently on the `2.0.0-prerelease.*` line. Bump `package.json` to release a new version.

Package identities are `Purview.ZodSharp.*` (core, SystemTextJson, NewtonsoftJson, AspNetCore). Central package management lives in `Directory.Packages.props`; package versions there are minimum requirements, not exact pins, so the resolved graph can drift.

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