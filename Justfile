set quiet

root_folder := "src"
solution := root_folder / "ZodSharp.slnx"
perf_tests_project := root_folder / "src" / "Benchmarks" / "Benchmarks.csproj"
aot_sample_project := root_folder / "src" / "Examples.MinimalApi" / "Examples.MinimalApi.csproj"
trim_verification_project := root_folder / "tests" / "TrimSafety.Verification" / "TrimSafety.Verification.csproj"
wasm_client_project := root_folder / "src" / "Examples.Blazor.Client" / "Examples.Blazor.Client.csproj"
# Native AOT cannot cross-compile between operating systems, so the sample publishes for this machine by default.
default_rid := (if os_family() == "windows" { "win" } else if os() == "macos" { "osx" } else { "linux" }) + "-" + (if arch() == "aarch64" { "arm64" } else { "x64" })
build_configuration := "Debug"
artifacts_folder := "./artifacts"
default_test_filter := "/*/*/*/*"

# CSharpier is vendored in .assets/ rather than taken from the tool manifest: the released 1.3.0
# cannot parse C# 15 `union` declarations and fails with CS1001, which would fail linting on the
# four files that declare one. Build:CSharpierPath points the pipeline at the same binary.
csharpier := "dotnet .assets/csharpier/CSharpier.dll"

pipeline_tool := "dotnet purview-build"

[private]
default:
    just --list

# Restore the repository's .NET local tools (.config/dotnet-tools.json)
[private]
ensure-pipeline-tool:
    dotnet tool restore

# Run the PR pipeline (restore, build, lint, tests)
[group('Pipeline')]
pipeline-pr *args: ensure-pipeline-tool
    echo "Running PR pipeline..."
    {{ pipeline_tool }} {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args: ensure-pipeline-tool
    echo "Running build pipeline..."
    {{ pipeline_tool }} --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args: ensure-pipeline-tool
    echo "Running release pipeline..."
    {{ pipeline_tool }} --Release:Mode=NuGet {{ args }}

# Run the pipeline through pack + validate (restore, build, lint, tests, pack, validate pack contents) without publishing/releasing
[group('Pipeline')]
pipeline-pack-validate *args: ensure-pipeline-tool
    echo "Running pack + validate pipeline..."
    {{ pipeline_tool }} --Build:RunPack=true --Build:ValidatePack=true --Release:Mode=None {{ args }}

# Build and run a throwaway consumer against the packed generator.
#
# The in-repo generator tests run against the unmerged generator, and pack validation only checks the
# IL-merged assembly is present in the .nupkg - not that it loads and generates. That merge has regressed
# twice (#36, #38), both times silently breaking consumers while this repository's tests stayed green.
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
[group('Pipeline')]
smoke-packed-consumers *args:
    echo "Running the packed consumer gate..."
    pwsh -NoProfile -File scripts/test-packed-consumers.ps1 {{ args }}

# Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package: injects a matching
# file into scratch copies of the packed packages and runs the real purview-build validation on them. Reads artifacts/,
# so pack first (`just pipeline-pack-validate`, or `just pack ./artifacts -c Release`). `just pipeline-local-release`
# moves its packages to the local feed, so pass -PackagesFolder p:/_sync-projects/.local-nuget after it.
[group('Pipeline')]
verify-pack-rules *args: ensure-pipeline-tool
    echo "Proving the pack-validation forbidden-content rules..."
    pwsh -NoProfile -File scripts/test-pack-validation-rules.ps1 {{ args }}

# Scan build or publish output for compiler-host assemblies (generators, analyzers, Roslyn) that must never ship.
[group('Build and Test')]
scan-compiler-host-leaks +paths:
    pwsh -NoProfile -File scripts/assert-no-compiler-host-assemblies.ps1 -Path {{ paths }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args: ensure-pipeline-tool lint-fix
    echo "Running local release pipeline..."
    {{ pipeline_tool }} --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline with tests enabled
[group('Pipeline')]
pipeline-tests *args:
    just ensure-pipeline-tool
    echo "Running tests pipeline..."
    {{ pipeline_tool }} --Build:RunTests=true --Release:Mode=None {{ args }}

# Build and test with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
build *args:
    echo "Building {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet build {{ solution }} -c {{ build_configuration }} {{ args }}

# Clean the solution with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
clean *args:
    echo "Cleaning {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet clean {{ solution }} -c {{ build_configuration }} {{ args }}

# Run the performance benchmarks with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
perf-tests *args:
    echo "Running performance tests for {{ BLUE }}{{ perf_tests_project }}{{ NORMAL }}"
    # Benchmarks multi-target net10.0/net11.0; default to net10.0. Pass `-f net11.0` to run on .NET 11.
    dotnet run --project {{ perf_tests_project }} -c Release -f net10.0 {{ args }}

# Run tests with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
test filter=default_test_filter *args:
    echo "Running tests for {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} and filter {{ GREEN }}{{ filter }}{{ NORMAL }}"
    dotnet test {{ solution }} -c {{ build_configuration }} --treenode-filter "{{ filter }}" {{ args }}

# Restore dependencies for the solution
[group('Build and Test')]
restore *args:
    echo "Restoring dependencies for {{ BLUE }}{{ solution }}{{ NORMAL }}"
    dotnet restore {{ solution }} {{ args }}

# Create NuGet package for the project
[group('Build and Test')]
pack publish_folder=artifacts_folder *args:
    echo "Packing {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} to {{ GREEN }}{{ publish_folder }}{{ NORMAL }}"
    dotnet pack {{ solution }} -c {{ build_configuration }} -o {{ publish_folder }} {{ args }}

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

# Displays the current version of the project.
# Requires Bun.
[group('Build and Test')]
version:
    bun -e "console.log('Current Version: {{ GREEN }}' + require('./package.json').version + '{{ NORMAL }}')"

# Check code formatting using CSharpier
[group('Utilities')]
lint-check:
    {{ csharpier }} check .

# Fix code formatting issues using CSharpier
[group('Utilities')]
lint-fix:
    {{ csharpier }} format .

# Open the solution in Visual Studio/ Registered application
[group('Utilities')]
vs:
    open {{ solution }}

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown
