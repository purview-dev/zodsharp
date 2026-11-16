# Recipes for Purview.ZodSharp. Run `just` for the list.
#
# The shared recipes come from .build/just/, copied verbatim from the purview-buildsdk-justfile
# skill. Change them there, not here; this file holds what belongs to this repository.

set quiet

# Let a definition here override a module's default variable or no-op hook.
set allow-duplicate-variables
set allow-duplicate-recipes

import ".build/just/index.just"

# The one solution. Purview.Build names the same path in .config/purview-build.json.
solution_file := "src/ZodSharp.slnx"

# Every test, matching TestFilter in .config/purview-build.json so a local run selects what CI does.
test_filter := "/*/*/*/*"

# The skills targets are invoked on a project, not the solution: Benchmarks deliberately opts out of
# Purview.BuildSdk (it shadows Directory.Build.props for BenchmarkDotNet's generated project), so
# MSBuild finds no skill target in it and a solution-wide run fails with MSB4057. Any project that
# imports the SDK carries the targets, and the work is repository-wide either way.
skills_project := "src/src/ZodSharp/ZodSharp.csproj"

root_folder := "src"
perf_tests_project := root_folder / "src" / "Benchmarks" / "Benchmarks.csproj"
aot_sample_project := root_folder / "src" / "Examples.MinimalApi" / "Examples.MinimalApi.csproj"
trim_verification_project := root_folder / "tests" / "TrimSafety.Verification" / "TrimSafety.Verification.csproj"
wasm_client_project := root_folder / "src" / "Examples.Blazor.Client" / "Examples.Blazor.Client.csproj"
# Native AOT cannot cross-compile between operating systems, so the sample publishes for this machine by default.
default_rid := (if os_family() == "windows" { "win" } else if os() == "macos" { "osx" } else { "linux" }) + "-" + (if arch() == "aarch64" { "arm64" } else { "x64" })

[private]
default:
    just --list

# Local: this repository formats the tree first, so a local release cannot fail its own lint stage on
# unformatted code. Everything else is the shared recipe's behaviour, and the arguments pass through.
[doc('Run the release pipeline, publishing to a local NuGet feed (restore, build, lint, tests, pack, validate, publish)')]
[group('Pipeline')]
pipeline-local-release *args: ensure-tools lint-fix
    echo "==> Running the local release pipeline"
    {{ pipeline_tool }} --Release:Mode=LocalNuGet {{ args }}

# Build and run a throwaway consumer against the packed generator.
#
# The in-repo generator tests run against the unmerged generator, and pack validation only checks the
# IL-merged assembly is present in the .nupkg - not that it loads and generates. That merge has regressed
# twice (#36, #38), both times silently breaking consumers while this repository's tests stayed green.
[doc('Build and run a throwaway consumer against the packed generator')]
[group('Pipeline')]
smoke-packed-generator *args:
    echo "Running the packed generator smoke test..."
    pwsh -NoProfile -File scripts/test-packed-generator.ps1 {{ args }}

# Pack every shipping package to an isolated feed and cache, then publish and run throwaway consumers that reference
# the packages only: a Native AOT console, a Blazor WebAssembly app (trimmed, plus RunAOTCompilation when wasm-tools is
# installed), a Native AOT minimal API and a multi-project API (a [ZodSchema] class library under an OpenAPI app with
# XML documentation), each leak-scanned for compiler-host assemblies.
#
# Every other publish gate builds against ProjectReferences; this is the one that proves what ships. The CI
# `packed-consumers` job runs it for linux-x64. Purview.SourceGeneratorFramework is restored from nuget.org, plus
# ZODSHARP_SGF_FEED or LOCAL_NUGET_FEED_PATH when set (or pass -SourceGeneratorFrameworkFeed).
[doc('Pack every package to an isolated feed, then publish throwaway consumers that reference the packages only')]
[group('Pipeline')]
smoke-packed-consumers *args:
    echo "Running the packed consumer gate..."
    pwsh -NoProfile -File scripts/test-packed-consumers.ps1 {{ args }}

# Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package: injects a matching
# file into scratch copies of the packed packages and runs the real purview-build validation on them. Reads artifacts/,
# so pack first (`just pipeline-pack-validate`, or `just pack ./artifacts -c Release`). `just pipeline-local-release`
# moves its packages to the local feed, so pass -PackagesFolder p:/_sync-projects/.local-nuget after it.
[doc('Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package')]
[group('Pipeline')]
verify-pack-rules *args: ensure-tools
    echo "Proving the pack-validation forbidden-content rules..."
    pwsh -NoProfile -File scripts/test-pack-validation-rules.ps1 {{ args }}

# Scan build or publish output for compiler-host assemblies (generators, analyzers, Roslyn) that must never ship.
[group('Build and Test')]
scan-compiler-host-leaks +paths:
    pwsh -NoProfile -File scripts/assert-no-compiler-host-assemblies.ps1 -Path {{ paths }}

# Run the performance benchmarks in Release for net10.0; pass `-f net11.0` to run them on .NET 11
[group('Build and Test')]
perf-tests *args:
    echo "Running performance tests for {{ BLUE }}{{ perf_tests_project }}{{ NORMAL }}"
    dotnet run --project {{ perf_tests_project }} -c Release -f net10.0 {{ args }}

# The CI `aot-sample` job runs the same publish for linux-x64. Windows needs the MSVC C++ build tools, Linux clang.
# Publish the Examples.MinimalApi sample with Native AOT, failing on any trim (IL2xxx) or AOT (IL3xxx) warning
[group('Build and Test')]
publish-aot-sample rid=default_rid *args:
    echo "Publishing {{ BLUE }}{{ aot_sample_project }}{{ NORMAL }} with Native AOT for {{ YELLOW }}{{ rid }}{{ NORMAL }}"
    dotnet publish {{ aot_sample_project }} -c Release -r {{ rid }} -o {{ artifacts_folder }}/aot-sample/{{ rid }} -warnaserror {{ args }}
    pwsh -NoProfile -File scripts/assert-no-compiler-host-assemblies.ps1 -Path {{ artifacts_folder }}/aot-sample/{{ rid }} {{ root_folder }}/src/Examples.MinimalApi/bin/Release

# The CI `verify-trim` job runs the same recipe for linux-x64. Windows needs the MSVC C++ build tools, Linux clang.
# The harness publishes twice - every ZodSharp assembly rooted, then as a plain consumer - and each binary exits non-zero on any mismatch.
# Publish and run the trim and Native AOT verification harness, failing on any IL2xxx/IL3xxx warning
[group('Build and Test')]
verify-trim rid=default_rid *args:
    echo "Publishing {{ BLUE }}{{ trim_verification_project }}{{ NORMAL }} with Native AOT for {{ YELLOW }}{{ rid }}{{ NORMAL }} (rooted)"
    dotnet publish {{ trim_verification_project }} -c Release -r {{ rid }} -o {{ artifacts_folder }}/trim-verification/{{ rid }}/rooted -p:TrimVerificationMode=Rooted -warnaserror {{ args }}
    {{ artifacts_folder }}/trim-verification/{{ rid }}/rooted/ZodSharp.TrimSafety.Verification
    echo "Publishing {{ BLUE }}{{ trim_verification_project }}{{ NORMAL }} with Native AOT for {{ YELLOW }}{{ rid }}{{ NORMAL }} (consumer)"
    dotnet publish {{ trim_verification_project }} -c Release -r {{ rid }} -o {{ artifacts_folder }}/trim-verification/{{ rid }}/consumer -p:TrimVerificationMode=Consumer -warnaserror {{ args }}
    {{ artifacts_folder }}/trim-verification/{{ rid }}/consumer/ZodSharp.TrimSafety.Verification
    pwsh -NoProfile -File scripts/assert-no-compiler-host-assemblies.ps1 -Path {{ artifacts_folder }}/trim-verification/{{ rid }}/rooted {{ artifacts_folder }}/trim-verification/{{ rid }}/consumer {{ root_folder }}/tests/TrimSafety.Verification/bin/Release

# The CI `wasm-trimmed` job runs the same publish and check; the nightly `wasm-aot` job adds -p:RunAOTCompilation=true (needs `dotnet workload install wasm-tools`).
# Publish the Blazor WebAssembly sample trimmed, failing on any IL2xxx/IL3xxx warning, then check the ZodSharp size budget
[group('Build and Test')]
wasm-size *args:
    echo "Publishing {{ BLUE }}{{ wasm_client_project }}{{ NORMAL }} trimmed for WebAssembly"
    rm -rf {{ artifacts_folder }}/wasm-trimmed
    dotnet publish {{ wasm_client_project }} -c Release -o {{ artifacts_folder }}/wasm-trimmed -warnaserror {{ args }}
    pwsh -NoProfile -File scripts/check-wasm-size.ps1 -PublishDirectory {{ artifacts_folder }}/wasm-trimmed
    pwsh -NoProfile -File scripts/assert-no-compiler-host-assemblies.ps1 -Path {{ artifacts_folder }}/wasm-trimmed {{ root_folder }}/src/Examples.Blazor.Client/bin/Release
