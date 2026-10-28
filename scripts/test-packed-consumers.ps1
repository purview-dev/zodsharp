#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Proves every shipping Purview.ZodSharp package works for real consumers: Native AOT, Blazor WebAssembly and a
	Native AOT minimal API, built from the .nupkg files alone.

.DESCRIPTION
	Every other publish gate (`just wasm-size`, the WebAssembly AOT publish, `just verify-trim`,
	`just publish-aot-sample`) builds against ProjectReferences. What ships is different: packed assemblies, an
	IL-merged generator under analyzers/, buildTransitive imports, static web assets and nuspec dependencies. A
	ProjectReference build exercises none of that, which is how Purview.SourceGeneratorFramework.dll - a
	compiler-host-only assembly - leaked into application output and broke Blazor WebAssembly AOT while every gate
	stayed green.

	This gate consumes the packages exactly as a user does:

	1. Preflight: checks Purview.SourceGeneratorFramework (the generator's build-time framework) can be restored, and
	   fails early, with what to do, if it cannot.
	2. Packs every shipping package (the ids in .config/purview-build.json PackValidation.RequiredContent) to an
	   isolated feed, with an isolated NUGET_PACKAGES, so no previously extracted package of the same version can be
	   reused. Package validation against the API baseline runs as part of the pack. The feed must hold exactly the
	   expected packages, and no package may carry a compiler-host assembly outside analyzers/.
	3. Copies the consumers in scripts/packed-consumers/ outside the repository, beside guard
	   Directory.Build.props/.targets and Directory.Packages.props files that stop any inheritance, with a nuget.config
	   that resolves Purview.ZodSharp* from the isolated feed only:
	   (a) AotConsole - a Native AOT console over Purview.ZodSharp, SystemTextJson, Extensions.Validation, AspNetCore
	       and AspNetCore.OpenApi. Published with -warnaserror, then run: it checks generated validation, member
	       resolution, count attributes, JSON Schema export, the validation bridge and ProblemDetails, and exits
	       non-zero on any mismatch.
	   (b) BlazorWasm - a Blazor WebAssembly app with <ZodValidator>. Published trimmed with -warnaserror, and with
	       RunAOTCompilation=true when the wasm-tools workload is installed. _framework must hold the ZodSharp
	       assemblies and _content the Blazor package's static web assets.
	   (c) MinimalApi - a Native AOT minimal API. Published with -warnaserror and started; an invalid POST must return
	       400 application/problem+json with issues, and /openapi/v1.json must describe ZodValidationProblemDetails.
	4. Scans every consumer bin and publish folder with scripts/assert-no-compiler-host-assemblies.ps1.

	The temporary workspace is deleted afterwards unless -KeepArtifacts is passed. Packing restores the shipping
	projects against the isolated cache, so they are restored again against the normal cache at the end.

	It is separate from scripts/test-packed-generator.ps1 on purpose: that script is a fast net10.0 check of the
	IL-merged generator that needs no native toolchain; this one needs the Native AOT toolchain (MSVC build tools on
	Windows, clang and zlib on Linux) and, for the WebAssembly AOT step, the wasm-tools workload.

.PARAMETER Configuration
	Configuration the packages are packed in. Defaults to Release: this gate checks what ships.

.PARAMETER RuntimeIdentifier
	RID for the Native AOT consumers. Native AOT cannot cross-compile between operating systems, so it defaults to this
	machine's RID (win-x64, linux-x64, osx-arm64, ...).

.PARAMETER SourceGeneratorFrameworkFeed
	A NuGet feed (folder or URL) to restore Purview.SourceGeneratorFramework from, in addition to nuget.org - for a
	version that is not on nuget.org yet. The feed is used for that package only. Defaults to $env:ZODSHARP_SGF_FEED,
	then $env:LOCAL_NUGET_FEED_PATH.

.PARAMETER WasmAot
	Auto (default) runs the WebAssembly AOT publish when the wasm-tools workload is installed and says so when it is
	skipped. Require fails when the workload is missing (CI installs it). Skip never runs it.

.PARAMETER PackOutput
	Also copy the packed .nupkg/.snupkg files here, so a later step can inspect exactly what this gate consumed - CI
	runs scripts/test-pack-validation-rules.ps1 against them.

.PARAMETER PreflightOnly
	Run only the Purview.SourceGeneratorFramework restore check, so CI can fail on it before installing toolchains.

.PARAMETER KeepArtifacts
	Leave the temporary workspace in place for inspection.

.EXAMPLE
	just smoke-packed-consumers

.EXAMPLE
	./scripts/test-packed-consumers.ps1 -RuntimeIdentifier linux-x64 -WasmAot Require -KeepArtifacts
#>
[CmdletBinding()]
param (
	[string] $Configuration = 'Release',
	[string] $RuntimeIdentifier = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
	[string] $SourceGeneratorFrameworkFeed = ($env:ZODSHARP_SGF_FEED ? $env:ZODSHARP_SGF_FEED : $env:LOCAL_NUGET_FEED_PATH),
	[ValidateSet('Auto', 'Require', 'Skip')]
	[string] $WasmAot = 'Auto',
	[string] $PackOutput,
	[switch] $PreflightOnly,
	[switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$leakScan = Join-Path $PSScriptRoot 'assert-no-compiler-host-assemblies.ps1'
$consumerTemplates = Join-Path $PSScriptRoot 'packed-consumers'

$version = (Get-Content (Join-Path $repositoryRoot 'package.json') -Raw | ConvertFrom-Json).version
$packageVersions = [xml] (Get-Content (Join-Path $repositoryRoot 'Directory.Packages.props') -Raw)
$sgfVersion = $packageVersions.SelectSingleNode('//PurviewSourceGenFramework')?.InnerText
$aspNetCoreVersion = $packageVersions.SelectSingleNode(
	"//PackageVersion[@Include='Microsoft.AspNetCore.Components.WebAssembly']"
)?.GetAttribute('Version')
$sdk = (Get-Content (Join-Path $repositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk

if (-not $sgfVersion) { throw 'Could not read PurviewSourceGenFramework from Directory.Packages.props.' }
if (-not $aspNetCoreVersion) { throw 'Could not read the Microsoft.AspNetCore.Components.WebAssembly version from Directory.Packages.props.' }

# The shipping packages are exactly the ids pack validation knows about, so a new package is covered without editing
# this script. Each id maps to src/src/<id without "Purview.ZodSharp."> (Purview.ZodSharp itself to src/src/ZodSharp).
$packValidation = (Get-Content (Join-Path $repositoryRoot '.config/purview-build.json') -Raw | ConvertFrom-Json).PackValidation
$sourceProjects = Get-ChildItem (Join-Path $repositoryRoot 'src/src') -Directory
$shipping = foreach ($id in $packValidation.RequiredContent.PSObject.Properties.Name) {
	$folder = $id -ieq 'purview.zodsharp' ? 'ZodSharp' : ($id -ireplace '^purview\.zodsharp\.', '')
	$directory = $sourceProjects | Where-Object { $_.Name -ieq $folder }
	$project = $directory ? (Join-Path $directory.FullName "$($directory.Name).csproj") : $null
	if (-not $project -or -not (Test-Path $project)) {
		throw "No project for the shipping package '$id' (expected src/src/$folder/$folder.csproj)."
	}

	[pscustomobject]@{ Id = $id; Project = $project }
}

# Packed through Purview.ZodSharp (the IL-merged generator and the code fixes), not on their own. CodeFixes is reached
# through an MSBuild task rather than a ProjectReference, so it must be restored explicitly (NETSDK1004 otherwise).
$packInputs = @(
	$shipping.Project
	(Join-Path $repositoryRoot 'src/src/SourceGenerators/SourceGenerators.csproj')
	(Join-Path $repositoryRoot 'src/src/CodeFixes/CodeFixes.csproj')
)

$workspace = Join-Path ([System.IO.Path]::GetTempPath()) "zs-packed-consumers-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$feed = Join-Path $workspace 'feed'
$packages = Join-Path $workspace 'packages'
$logs = Join-Path $workspace 'logs'
$consumers = Join-Path $workspace 'consumers'
$output = Join-Path $workspace 'out'
$exe = $IsWindows ? '.exe' : ''

$timings = [System.Collections.Generic.List[object]]::new()
$scanned = [System.Collections.Generic.List[string]]::new()
$previousNuGetPackages = $env:NUGET_PACKAGES
$repositoryRestoreTouched = $false

function Write-Failure([string] $message) {
	if ($env:GITHUB_ACTIONS) { Write-Host "::error::$message" } else { Write-Host "ERROR: $message" -ForegroundColor Red }
}

function Invoke-Step([string] $name, [scriptblock] $body) {
	if ($env:GITHUB_ACTIONS) { Write-Host "::group::$name" } else { Write-Host "`n=== $name" -ForegroundColor Cyan }
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$status = 'passed'
	try {
		& $body
	}
	catch {
		$status = 'FAILED'
		throw
	}
	finally {
		$stopwatch.Stop()
		$timings.Add([pscustomobject]@{ Step = $name; Result = $status; Seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1) })
		if ($env:GITHUB_ACTIONS) { Write-Host '::endgroup::' }
	}
}

function Invoke-Skipped([string] $name, [string] $reason) {
	Write-Host "`n=== $name - SKIPPED: $reason" -ForegroundColor Yellow
	if ($env:GITHUB_ACTIONS) { Write-Host "::warning::$name skipped: $reason" }
	$timings.Add([pscustomobject]@{ Step = $name; Result = 'skipped'; Seconds = 0 })
}

# Runs dotnet, tees its output to a log, and fails on a non-zero exit. -warnaserror already fails the publish on any
# trim or AOT warning; the log check is a second gate in case one is ever logged without failing the command.
function Invoke-Dotnet([string] $logName, [string[]] $arguments) {
	$log = Join-Path $logs "$logName.log"
	Write-Host "dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
	& dotnet @arguments 2>&1 | Tee-Object -FilePath $log | Out-Host
	if ($LASTEXITCODE -ne 0) { throw "dotnet $($arguments[0]) failed with exit code $LASTEXITCODE (log: $log)." }

	$warnings = Select-String -Path $log -Pattern 'warning IL[23][0-9]{3}' | Select-Object -First 5
	if ($warnings) {
		throw "dotnet $($arguments[0]) reported trim or AOT warnings:`n$($warnings.Line -join "`n")"
	}
}

function Assert-NoLeaks([string[]] $directories) {
	$global:LASTEXITCODE = 0
	& $leakScan -Path $directories
	if ($LASTEXITCODE -ne 0) { throw "The compiler-host leak scan failed for: $($directories -join ', ')." }
	$scanned.AddRange($directories)
}

function Assert-Contains([string] $directory, [string] $pattern, [string] $what) {
	$found = @(Get-ChildItem $directory -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern })
	if ($found.Count -eq 0) { throw "$what is missing from '$directory' (no file matches '$pattern')." }
	Write-Host "  found $what ($($found[0].Name))"
}

function New-NuGetConfig([string] $path, [hashtable[]] $sources) {
	$sourceXml = ($sources | ForEach-Object { "`t`t<add key=`"$($_.Key)`" value=`"$($_.Value)`" />" }) -join "`n"
	$mappingXml = ($sources | ForEach-Object {
			$patterns = ($_.Patterns | ForEach-Object { "`t`t`t<package pattern=`"$_`" />" }) -join "`n"
			"`t`t<packageSource key=`"$($_.Key)`">`n$patterns`n`t`t</packageSource>"
		}) -join "`n"

	@"
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by scripts/test-packed-consumers.ps1. Used as RestoreConfigFile, so no other configuration applies. -->
<configuration>
	<packageSources>
		<clear />
$sourceXml
	</packageSources>
	<fallbackPackageFolders>
		<clear />
	</fallbackPackageFolders>
	<packageSourceMapping>
$mappingXml
	</packageSourceMapping>
</configuration>
"@ | Set-Content $path -Encoding utf8
}

function Start-Api([string] $directory, [int] $port) {
	$env:ASPNETCORE_URLS = "http://127.0.0.1:$port"
	try {
		return Start-Process -FilePath (Join-Path $directory "PackedApi$exe") -WorkingDirectory $directory -PassThru `
			-RedirectStandardOutput (Join-Path $logs 'minimal-api.stdout.log') `
			-RedirectStandardError (Join-Path $logs 'minimal-api.stderr.log')
	}
	finally {
		Remove-Item Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
	}
}

# PowerShell returns a body it does not recognise as text - application/problem+json among them - as bytes.
function Get-Body($response) {
	return $response.Content -is [byte[]] ? [System.Text.Encoding]::UTF8.GetString($response.Content) : [string] $response.Content
}

function Get-FreePort {
	$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
	$listener.Start()
	try { return ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port } finally { $listener.Stop() }
}

$exitCode = 0
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
	New-Item -ItemType Directory -Path $feed, $packages, $logs, $consumers, $output -Force | Out-Null

	Write-Host "Packed-consumer gate: Purview.ZodSharp $version ($Configuration), RID $RuntimeIdentifier" -ForegroundColor Cyan
	Write-Host "Workspace: $workspace"

	# Guards at the workspace root: MSBuild stops at the first Directory.Build.props/.targets and
	# Directory.Packages.props it finds walking up, so nothing above the workspace - a stray file in the temp folder,
	# a user profile - can change how the consumers build. The consumers must see the packages and nothing else.
	foreach ($guard in 'Directory.Build.props', 'Directory.Build.targets') {
		@"
<Project>
	<!-- Guard written by scripts/test-packed-consumers.ps1: stops $guard inheritance from any parent folder. -->
</Project>
"@ | Set-Content (Join-Path $workspace $guard) -Encoding utf8
	}

	@'
<Project>
	<!-- Guard written by scripts/test-packed-consumers.ps1: the consumers version their packages themselves. -->
	<PropertyGroup>
		<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
	</PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $workspace 'Directory.Packages.props') -Encoding utf8

	# The repository's SDK, without its MSBuild SDKs (Purview.BuildSdk): the consumers are plain projects.
	[ordered]@{ sdk = [ordered]@{ version = $sdk.version; rollForward = $sdk.rollForward; allowPrerelease = $true } } |
		ConvertTo-Json | Set-Content (Join-Path $workspace 'global.json') -Encoding utf8

	# Pack-time sources: nuget.org for everything, plus the optional feed for Purview.SourceGeneratorFramework only.
	$sgfPatterns = @('Purview.SourceGeneratorFramework', 'Purview.SourceGeneratorFramework.*')
	$packSources = @(@{ Key = 'nuget.org'; Value = 'https://api.nuget.org/v3/index.json'; Patterns = @('*') + $sgfPatterns })
	if ($SourceGeneratorFrameworkFeed) {
		$packSources += @{ Key = 'sourcegeneratorframework'; Value = $SourceGeneratorFrameworkFeed; Patterns = $sgfPatterns }
	}
	$packConfig = Join-Path $workspace 'pack.nuget.config'
	New-NuGetConfig $packConfig $packSources

	# Consumer sources: Purview.ZodSharp* from the packages just packed and nowhere else - not nuget.org, not a local
	# feed holding an older build of the same version.
	$consumerConfig = Join-Path $consumers 'nuget.config'
	New-NuGetConfig $consumerConfig @(
		@{ Key = 'packed'; Value = $feed; Patterns = @('Purview.ZodSharp', 'Purview.ZodSharp.*') }
		@{ Key = 'nuget.org'; Value = 'https://api.nuget.org/v3/index.json'; Patterns = @('*') }
	)

	# An isolated extraction directory: NuGet reuses an already-extracted package of the same id and version, so a
	# stale copy from an earlier run - or an earlier SourceGeneratorFramework build - would be used instead.
	$env:NUGET_PACKAGES = $packages


	Invoke-Step "Preflight: restore Purview.SourceGeneratorFramework $sgfVersion" {
		$probe = Join-Path $workspace 'sgf-probe'
		New-Item -ItemType Directory -Path $probe -Force | Out-Null
		@"
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
	</PropertyGroup>
	<ItemGroup>
		<PackageDownload Include="Purview.SourceGeneratorFramework" Version="[$sgfVersion]" />
	</ItemGroup>
</Project>
"@ | Set-Content (Join-Path $probe 'probe.csproj') -Encoding utf8

		& dotnet restore (Join-Path $probe 'probe.csproj') "-p:RestoreConfigFile=$packConfig" 2>&1 |
			Tee-Object -FilePath (Join-Path $logs 'sgf-probe.log') | Out-Host
		if ($LASTEXITCODE -ne 0) {
			$from = ($packSources | ForEach-Object Value) -join ', '
			throw (
				"Purview.SourceGeneratorFramework $sgfVersion (Directory.Packages.props) could not be restored from $from. " +
				'The generator is built against it, so nothing can be packed. If that version is not on nuget.org yet, ' +
				'publish it there, or point this gate at a feed that has it: -SourceGeneratorFrameworkFeed <folder or URL>, ' +
				'or set ZODSHARP_SGF_FEED (LOCAL_NUGET_FEED_PATH is used when set).'
			)
		}
	}

	if ($PreflightOnly) {
		Write-Host "Purview.SourceGeneratorFramework $sgfVersion restores; stopping after the preflight (-PreflightOnly)." -ForegroundColor Green
		return
	}

	if ($IsLinux -and -not (Get-Command clang -ErrorAction SilentlyContinue)) {
		throw 'Native AOT on Linux needs clang and the zlib headers: sudo apt-get install -y clang zlib1g-dev.'
	}

	Invoke-Step 'Pack every shipping package to the isolated feed' {
		$script:repositoryRestoreTouched = $true
		foreach ($project in $packInputs) {
			Invoke-Dotnet "restore-$([System.IO.Path]::GetFileNameWithoutExtension($project))" @(
				'restore', $project, "-p:RestoreConfigFile=$packConfig", '-nr:false'
			)
		}

		foreach ($package in $shipping) {
			Invoke-Dotnet "pack-$($package.Id)" @(
				'pack', $package.Project, '--configuration', $Configuration, '--output', $feed, '--no-restore',
				'-m:4', '-nr:false'
			)
		}

		$expected = @($shipping.Id | ForEach-Object { "$_.$version.nupkg".ToLowerInvariant() } | Sort-Object)
		$actual = @(Get-ChildItem $feed -Filter '*.nupkg' | ForEach-Object { $_.Name.ToLowerInvariant() } | Sort-Object)
		$difference = Compare-Object $expected $actual
		if ($difference) {
			throw "The feed does not hold exactly the shipping packages:`n$($difference | Out-String)"
		}

		# The bytes the consumers restore: a compiler-host assembly anywhere but analyzers/, or a generator or code-fix
		# assembly under lib/, would reach every application that references the package.
		Add-Type -AssemblyName System.IO.Compression.FileSystem
		$hostOnly = '(^|/)(Purview\.SourceGeneratorFramework|Microsoft\.CodeAnalysis)[^/]*$'
		$generatorOnly = '^lib/.*(Purview\.ZodSharp\.SourceGenerators|Purview\.ZodSharp\.CodeFixes)[^/]*$'
		foreach ($nupkg in Get-ChildItem $feed -Filter '*.nupkg') {
			$archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
			try {
				$bad = @($archive.Entries.FullName | Where-Object {
						($_ -match $hostOnly -and $_ -notmatch '^analyzers/') -or $_ -match $generatorOnly
					})
			}
			finally { $archive.Dispose() }

			if ($bad.Count -gt 0) { throw "$($nupkg.Name) ships compiler-host content outside analyzers/: $($bad -join ', ')." }
			Write-Host "  $($nupkg.Name): no compiler-host content outside analyzers/"
		}

		if ($PackOutput) {
			New-Item -ItemType Directory -Path $PackOutput -Force | Out-Null
			Copy-Item (Join-Path $feed '*') $PackOutput -Force
			Write-Host "  copied the packages to $PackOutput"
		}
	}

	Copy-Item (Join-Path $consumerTemplates '*') $consumers -Recurse -Force
	$consumerProperties = @(
		"-p:ZodSharpPackageVersion=$version"
		"-p:AspNetCorePackageVersion=$aspNetCoreVersion"
		"-p:RestoreConfigFile=$consumerConfig"
		'-nr:false'
	)

	Invoke-Step '(a) Native AOT console: publish and run' {
		$project = Join-Path $consumers 'AotConsole/AotConsole.csproj'
		$publish = Join-Path $output 'aot-console'

		# A framework-dependent build first: its bin holds the deps.json and runtimeconfig.json that list every
		# runtime dependency, which is where an analyzer-only assembly that leaks shows up first.
		Invoke-Dotnet 'aot-console-build' (@('build', $project, '--configuration', 'Release', '-warnaserror') + $consumerProperties)
		Assert-Contains (Join-Path $consumers 'AotConsole/bin/Release/net11.0') '^PackedAot\.deps\.json$' 'the build deps.json'

		Invoke-Dotnet 'aot-console-publish' (@(
				'publish', $project, '--configuration', 'Release', '--runtime', $RuntimeIdentifier, '--output', $publish,
				'-warnaserror'
			) + $consumerProperties)

		& (Join-Path $publish "PackedAot$exe") | Out-Host
		if ($LASTEXITCODE -ne 0) { throw "The Native AOT console exited with ${LASTEXITCODE}: a packed behaviour is wrong." }

		Assert-NoLeaks @((Join-Path $consumers 'AotConsole/bin'), $publish)
	}

	$wasmProject = Join-Path $consumers 'BlazorWasm/BlazorWasm.csproj'
	function Assert-WasmOutput([string] $publish) {
		$framework = Join-Path $publish 'wwwroot/_framework'
		Assert-Contains $framework '^Purview\.ZodSharp\.[a-z0-9]+\.wasm$' 'Purview.ZodSharp'
		Assert-Contains $framework '^Purview\.ZodSharp\.Blazor\.[a-z0-9]+\.wasm$' 'Purview.ZodSharp.Blazor'
		Assert-Contains $framework '^Purview\.ZodSharp\.Extensions\.Validation\.[a-z0-9]+\.wasm$' 'Purview.ZodSharp.Extensions.Validation'
		Assert-Contains $framework '^PackedWasm\.[a-z0-9]+\.wasm$' 'the app assembly'
		$content = Join-Path $publish 'wwwroot/_content/Purview.ZodSharp.Blazor'
		Assert-Contains $content '^zodsharp-validation\.js$' 'the Blazor static web asset'
		Assert-Contains $content '^Purview\.ZodSharp\.Blazor\.[a-z0-9]+\.lib\.module\.js$' 'the Blazor JavaScript initializer'
	}

	Invoke-Step '(b) Blazor WebAssembly: trimmed publish' {
		$publish = Join-Path $output 'wasm-trimmed'
		Invoke-Dotnet 'wasm-trimmed-publish' (@(
				'publish', $wasmProject, '--configuration', 'Release', '--output', $publish, '-warnaserror'
			) + $consumerProperties)

		Assert-WasmOutput $publish
		Assert-NoLeaks @((Join-Path $consumers 'BlazorWasm/bin'), $publish)
	}

	$wasmTools = $false
	if ($WasmAot -ne 'Skip') {
		Push-Location $workspace
		try { $wasmTools = [bool] (& dotnet workload list 2>$null | Select-String -Pattern '^\s*wasm-tools\s') }
		finally { Pop-Location }
	}

	if ($WasmAot -eq 'Skip') {
		Invoke-Skipped '(b) Blazor WebAssembly: RunAOTCompilation publish' 'disabled with -WasmAot Skip.'
	}
	elseif (-not $wasmTools -and $WasmAot -eq 'Auto') {
		Invoke-Skipped '(b) Blazor WebAssembly: RunAOTCompilation publish' (
			'the wasm-tools workload is not installed, so the WebAssembly AOT compile was NOT checked. ' +
			'Install it with `dotnet workload install wasm-tools` and rerun, or pass -WasmAot Require to make this an error.'
		)
	}
	else {
		Invoke-Step '(b) Blazor WebAssembly: RunAOTCompilation publish' {
			if (-not $wasmTools) {
				throw 'The wasm-tools workload is not installed (-WasmAot Require): run `dotnet workload install wasm-tools`.'
			}

			# A copy of the project, so the AOT build starts from clean obj/bin rather than the trimmed build's.
			$aotProjectDirectory = Join-Path $consumers 'BlazorWasmAot'
			Copy-Item (Join-Path $consumerTemplates 'BlazorWasm') $aotProjectDirectory -Recurse -Force
			$publish = Join-Path $output 'wasm-aot'
			Invoke-Dotnet 'wasm-aot-publish' (@(
					'publish', (Join-Path $aotProjectDirectory 'BlazorWasm.csproj'), '--configuration', 'Release',
					'--output', $publish, '-p:RunAOTCompilation=true', '-warnaserror'
				) + $consumerProperties)

			Assert-WasmOutput $publish
			Assert-Contains (Join-Path $publish 'wwwroot/_framework') '^dotnet\.native\.[a-z0-9]+\.wasm$' 'the AOT-compiled runtime'
			Assert-NoLeaks @((Join-Path $aotProjectDirectory 'bin'), $publish)
		}
	}

	Invoke-Step '(c) Native AOT minimal API: publish, start and call' {
		$project = Join-Path $consumers 'MinimalApi/MinimalApi.csproj'
		$publish = Join-Path $output 'minimal-api'

		# As for (a): the framework-dependent build's deps.json is scanned too.
		Invoke-Dotnet 'minimal-api-build' (@('build', $project, '--configuration', 'Release', '-warnaserror') + $consumerProperties)
		Assert-Contains (Join-Path $consumers 'MinimalApi/bin/Release/net11.0') '^PackedApi\.deps\.json$' 'the build deps.json'

		Invoke-Dotnet 'minimal-api-publish' (@(
				'publish', $project, '--configuration', 'Release', '--runtime', $RuntimeIdentifier, '--output', $publish,
				'-warnaserror'
			) + $consumerProperties)

		$port = Get-FreePort
		$base = "http://127.0.0.1:$port"
		$process = Start-Api $publish $port
		try {
			$openApi = $null
			$deadline = [DateTime]::UtcNow.AddSeconds(30)
			while (-not $openApi -and [DateTime]::UtcNow -lt $deadline) {
				if ($process.HasExited) { throw "The minimal API exited with $($process.ExitCode) before it was ready." }
				try { $openApi = Invoke-WebRequest "$base/openapi/v1.json" -TimeoutSec 5 }
				catch { Start-Sleep -Milliseconds 500 }
			}
			if (-not $openApi) { throw "The minimal API did not serve $base/openapi/v1.json within 30 seconds." }

			$invalid = Invoke-WebRequest "$base/orders" -Method Post -ContentType 'application/json' -SkipHttpErrorCheck `
				-Body '{"customerEmail":"not-an-email","lines":[{"sku":"abc","quantity":0}]}'
			$contentType = [string] ($invalid.Headers['Content-Type'] -join ',')
			Write-Host "  invalid order: $($invalid.StatusCode) $contentType"
			$invalidBody = Get-Body $invalid
			Write-Host "  $invalidBody"
			if ($invalid.StatusCode -ne 400 -or -not $contentType.StartsWith('application/problem+json')) {
				throw "An invalid order returned $($invalid.StatusCode) $contentType, not 400 application/problem+json."
			}

			$problem = $invalidBody | ConvertFrom-Json
			if (-not ($problem.PSObject.Properties['issues'] -and @($problem.issues).Count -gt 0)) {
				throw 'The ProblemDetails body carries no ZodSharp issues.'
			}
			$paths = @($problem.issues | ForEach-Object { $_.path })
			foreach ($expectedPath in 'CustomerEmail', 'Lines[0].Sku', 'Lines[0].Quantity') {
				if ($paths -notcontains $expectedPath) {
					throw "The ProblemDetails issues do not include '$expectedPath' (got: $($paths -join ', '))."
				}
			}

			$valid = Invoke-WebRequest "$base/orders" -Method Post -ContentType 'application/json' -SkipHttpErrorCheck `
				-Body '{"customerEmail":"buyer@example.com","lines":[{"sku":"ABC-1234","quantity":2}]}'
			if ($valid.StatusCode -ne 200) { throw "A valid order returned $($valid.StatusCode), not 200: $(Get-Body $valid)" }
			Write-Host "  valid order: $($valid.StatusCode) $(Get-Body $valid)"

			if (-not (Get-Body $openApi).Contains('ZodValidationProblemDetails')) {
				throw 'The OpenAPI document does not describe ZodValidationProblemDetails.'
			}
			if (-not (Get-Body $openApi).Contains('"minItems"')) {
				throw 'The OpenAPI document does not carry the [MinCount] constraint (minItems).'
			}
			Write-Host '  OpenAPI document describes ZodValidationProblemDetails and the [MinCount] constraint'
		}
		finally {
			if (-not $process.HasExited) { $process.Kill($true) }
			$process.WaitForExit()
		}

		Assert-NoLeaks @((Join-Path $consumers 'MinimalApi/bin'), $publish)
	}
}
catch {
	$exitCode = 1
	Write-Failure "Packed-consumer gate failed: $($_.Exception.Message)"
	if (Test-Path $logs) { Write-Host "Logs: $logs" }
}
finally {
	if ($null -eq $previousNuGetPackages) { Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue }
	else { $env:NUGET_PACKAGES = $previousNuGetPackages }

	# Packing restored the shipping projects against the isolated cache that is about to be deleted; restore them
	# against the normal cache, so a later --no-restore build does not point at a missing folder.
	if ($repositoryRestoreTouched) {
		Write-Host "`nRestoring the shipping projects against the normal package cache..." -ForegroundColor Cyan
		foreach ($project in $packInputs) {
			& dotnet restore $project -nr:false | Out-Null
			if ($LASTEXITCODE -ne 0) { Write-Failure "Restoring $project against the normal cache failed; run dotnet restore." }
		}
	}

	$totalStopwatch.Stop()
	Write-Host "`nPacked-consumer gate timings:" -ForegroundColor Cyan
	$timings | Format-Table -AutoSize | Out-Host
	Write-Host ("Total: {0:N1}s" -f $totalStopwatch.Elapsed.TotalSeconds)
	if ($scanned.Count -gt 0) { Write-Host "Leak-scanned $($scanned.Count) folder(s)." }

	if ($KeepArtifacts) {
		Write-Host "Artifacts kept at $workspace" -ForegroundColor Yellow
	}
	else {
		Remove-Item $workspace -Recurse -Force -ErrorAction SilentlyContinue
	}
}

if ($exitCode -ne 0) { exit $exitCode }

Write-Host 'Packed-consumer gate passed.' -ForegroundColor Green
