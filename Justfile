set quiet

root_folder := "src"
solution := root_folder / "ZodSharp.slnx"
perf_tests_project := root_folder / "src" / "Benchmarks" / "Benchmarks.csproj"
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
