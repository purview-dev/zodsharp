set quiet

root_folder := "src"
solution := root_folder / "ZodSharp.slnx"
perf_tests_project := root_folder / "tests" / "ZodSharp.PerformanceTests" / "ZodSharp.PerformanceTests.csproj"
build_configuration := "Release"
artifacts_folder := "./artifacts"
default_test_filter := "/*/*/*/*/"

pipeline_feed := "https://api.nuget.org/v3/index.json"
pipeline_tool := ".tools/purview-build/purview-build"

[private]
default:
    just --list

# Install the shared Purview.Build tool (authenticated to the Purview-Dev feed) if not present
[private]
ensure-pipeline-tool:
    if [ ! -x "{{ pipeline_tool }}" ]; then \
        dotnet tool install Purview.Build --tool-path .tools/purview-build --add-source "{{ pipeline_feed }}"; \
    fi

# Run the PR pipeline (restore, build, lint, tests)
[group('Pipeline')]
pipeline-pr *args:
    just ensure-pipeline-tool
    echo "Running PR pipeline..."
    "{{ pipeline_tool }}" {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args:
    just ensure-pipeline-tool
    echo "Running build pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args:
    just ensure-pipeline-tool
    echo "Running release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=NuGet {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args:
    just ensure-pipeline-tool
    echo "Running local release pipeline..."
    "{{ pipeline_tool }}" --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline with tests enabled
[group('Pipeline')]
pipeline-tests *args:
    just ensure-pipeline-tool
    echo "Running tests pipeline..."
    "{{ pipeline_tool }}" --Build:RunTests=true --Release:Mode=None {{ args }}

# Build and test with the specified configuration, defaulting to "Release"
build *args:
    echo "Building {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet build {{ solution }} -c {{ build_configuration }} {{ args }}

# Build and test with the specified configuration, defaulting to "Release"
clean *args:
    echo "Cleaning {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet clean {{ solution }} -c {{ build_configuration }} {{ args }}

# Run the performance tests with the specified configuration, defaulting to "Release"
perf-tests *args:
    echo "Running performance tests for {{ BLUE }}{{ perf_tests_project }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet run --project {{ perf_tests_project }} -c {{ build_configuration }} {{ args }}

# Run tests with the specified configuration, defaulting to "Release"
test filter=default_test_filter *args:
    echo "Running tests for {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} and filter {{ GREEN }}{{ filter }}{{ NORMAL }}"
    dotnet test {{ solution }} -c {{ build_configuration }} --treenode-filter "{{ filter }}" {{ args }}

# Run tests with the specified configuration, defaulting to "Release"
restore *args:
    echo "Restoring dependencies for {{ BLUE }}{{ solution }}{{ NORMAL }}"
    dotnet restore {{ solution }} {{ args }}

# Create NuGet package for the project
pack publish_folder=artifacts_folder *args:
    echo "Packing {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} to {{ GREEN }}{{ publish_folder }}{{ NORMAL }}"
    dotnet pack {{ solution }} -c {{ build_configuration }} -o {{ publish_folder }} {{ args }}

# Check code formatting using CSharpier
lint-check:
    dotnet csharpier check .
    # dotnet format --verify-no-changes {{ solution }}

# Fix code formatting issues using CSharpier
lint-fix:
    dotnet csharpier format .
    # dotnet format {{ solution }}

# Open the solution in Visual Studio/ Registered application
vs:
    open {{ solution }}

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown
