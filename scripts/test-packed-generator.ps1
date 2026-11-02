#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Proves the packed source generator actually works when consumed from the .nupkg.

.DESCRIPTION
	Every in-repo generator test runs against the *unmerged* generator: ZodSharp.csproj deliberately keeps
	the loose build output so the tests are fast and debuggable. What ships is different — the
	IL-merged, framework-internalized assembly produced by Purview.SourceGeneratorFramework's
	GetPurviewMergedAnalyzerFile target, packed under analyzers/dotnet/cs.

	Pack validation checks that assembly is *present*. Nothing checked that it *loads and generates*, and
	that merge has regressed twice before (#36 "fixed source gen il-merge", #38 "sg leaking types"). Both
	times the symptom was a consumer whose schemas silently stopped generating — invisible to this
	repository's own test suite.

	This script closes that gap: it packs, then builds and runs a throwaway consumer against the resulting
	package, with an isolated package cache so a previously extracted copy of the same version cannot be
	reused.

.PARAMETER Configuration
	Build configuration to pack. Defaults to Debug to match the other local recipes.

.PARAMETER KeepArtifacts
	Leave the temporary directory in place for inspection.
#>
[CmdletBinding()]
param (
	[string] $Configuration = 'Debug',
	[switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $repositoryRoot 'package.json') -Raw | ConvertFrom-Json).version
$workspace = Join-Path ([System.IO.Path]::GetTempPath()) "zodsharp-packed-smoke-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

$feed = Join-Path $workspace 'feed'
$packages = Join-Path $workspace 'packages'
$consumer = Join-Path $workspace 'consumer'

New-Item -ItemType Directory -Path $feed, $packages, $consumer -Force | Out-Null

# Restore the whole solution before packing, which is what the shared pipeline does.
#
# Packing ZodSharp.csproj alone is not enough on a clean checkout: PackZodSharpCodeFixes reaches
# CodeFixes.csproj through the MSBuild *task*, not a ProjectReference, so `dotnet pack` never
# restores it and the pack fails with NETSDK1004 ("Assets file ... project.assets.json not found").
# Locally it appeared to work only because an earlier solution build had left assets behind; CI
# starts cold, so it failed there and nowhere else.
Write-Host 'Restoring the solution so every project the pack reaches has assets...' -ForegroundColor Cyan

& dotnet restore (Join-Path $repositoryRoot 'src/ZodSharp.slnx')
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

Write-Host "Packing Purview.ZodSharp $version ($Configuration) to an isolated feed..." -ForegroundColor Cyan

& dotnet pack (Join-Path $repositoryRoot 'src/src/ZodSharp/ZodSharp.csproj') `
	--configuration $Configuration `
	--output $feed
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

# Only the freshly packed feed, so the consumer cannot silently resolve a published package instead.
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<clear />
		<add key="packed" value="$feed" />
		<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
	</packageSources>
</configuration>
"@ | Set-Content (Join-Path $consumer 'nuget.config') -Encoding utf8

@"
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net10.0</TargetFramework>
		<Nullable>enable</Nullable>
		<!-- A generator failure must fail this build, not warn. -->
		<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
		<ZodSharpDefaultImmutableArrayBehavior>TreatAsEmpty</ZodSharpDefaultImmutableArrayBehavior>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Purview.ZodSharp" Version="$version" />
	</ItemGroup>
</Project>
"@ | Set-Content (Join-Path $consumer 'consumer.csproj') -Encoding utf8

'{ "sdk": { "allowPrerelease": true } }' | Set-Content (Join-Path $consumer 'global.json') -Encoding utf8

# Exercises the generator end to end: the [ZodSchema] attribute comes from the package, the generated
# PersonSchema.Validate is what the merged generator has to produce, and the DataAnnotations attributes make
# it emit rules rather than an empty schema.
@'
using System.ComponentModel.DataAnnotations;
using System.Collections.Immutable;
using ZodSharp;

namespace PackedSmoke;

[ZodSchema]
public sealed class Person
{
	[Required]
	[StringLength(50, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[Range(0, 130)]
	public int Age { get; set; }

	[EmailAddress]
	public string Email { get; set; } = string.Empty;

	[MinLength(1)]
	public ImmutableArray<string>? Aliases { get; set; } = default(ImmutableArray<string>);

	[Required]
	[CollectionBehavior(DefaultImmutableArrayBehavior = DefaultImmutableArrayBehavior.ValidationError)]
	public ImmutableArray<string> Tags { get; set; } = ImmutableArray<string>.Empty;
}

public static class Program
{
	public static int Main()
	{
		// The generated validator. If the merged generator did not run, this does not compile.
		var valid = PersonSchema.Validate(new Person { Name = "Ada", Age = 36, Email = "ada@example.com", Aliases = ImmutableArray.Create("Ada") });
		var invalid = PersonSchema.Validate(new Person { Name = "A", Age = 500, Email = "nope" });
		var missing = PersonSchema.Validate(new Person { Name = "Ada", Age = 36, Email = "ada@example.com", Tags = default });

		if (missing.Errors.Length != 2
			|| !System.Linq.Enumerable.Any(missing.Errors, error => error.Path[0] == "Aliases" && error.Code == "too_small")
			|| !System.Linq.Enumerable.Any(missing.Errors, error => error.Path[0] == "Tags" && error.Code == "missing_field"))
		{
			System.Console.Error.WriteLine("FAIL: immutable-array build/property defaults were not honored.");
			return 1;
		}

		if (!valid.IsSuccess)
		{
			System.Console.Error.WriteLine("FAIL: a valid person was rejected.");

			return 1;
		}

		if (invalid.IsSuccess)
		{
			System.Console.Error.WriteLine("FAIL: an invalid person was accepted, so no rules were generated.");

			return 1;
		}

		System.Console.WriteLine($"PASS: generated validator reported {invalid.Errors.Length} error(s) for the invalid person.");

		return 0;
	}
}
'@ | Set-Content (Join-Path $consumer 'Program.cs') -Encoding utf8

Write-Host 'Building and running the consumer against the packed generator...' -ForegroundColor Cyan

# An isolated extraction directory: NuGet reuses an already-extracted package for the same id and version,
# so without this a stale copy from a previous run would be used instead of what was just packed.
$env:NUGET_PACKAGES = $packages

try {
	& dotnet run --project (Join-Path $consumer 'consumer.csproj') --configuration Release
	$exitCode = $LASTEXITCODE
}
finally {
	Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue

	if ($KeepArtifacts) {
		Write-Host "Artifacts kept at $workspace" -ForegroundColor Yellow
	}
	else {
		Remove-Item $workspace -Recurse -Force -ErrorAction SilentlyContinue
	}
}

if ($exitCode -ne 0) {
	throw "The packed generator smoke test failed with exit code $exitCode. The IL-merged generator in the .nupkg did not produce a working validator."
}

Write-Host 'Packed generator smoke test passed.' -ForegroundColor Green
