#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Fails when the Purview.ZodSharp assemblies in a published Blazor WebAssembly app grow past the size budget.

.DESCRIPTION
	Reads the Brotli-compressed size of every Purview.ZodSharp* assembly in the publish output's _framework
	folder (what a browser downloads) and compares it with .config/wasm-size-baseline.json. The check fails
	when an assembly grows more than the baseline's tolerancePercent (10%) over its recorded size, when a
	baseline assembly is missing from the output, or when a Purview.ZodSharp assembly appears that the
	baseline does not list.

	Published file names carry a content fingerprint (Purview.ZodSharp.Blazor.h3u4mo6gz6.wasm.br); sizes are
	keyed by the name without it (Purview.ZodSharp.Blazor.wasm).

	The CI wasm-trimmed job runs it after publishing Examples.Blazor.Client; locally, `just wasm-size` does
	both. After an intended change in size, rerun with -Update and commit the new baseline.

.PARAMETER PublishDirectory
	The publish output of Examples.Blazor.Client (the folder that contains wwwroot/_framework).

.PARAMETER BaselinePath
	The committed baseline. Defaults to .config/wasm-size-baseline.json in the repository.

.PARAMETER Update
	Rewrite the baseline from the publish output instead of checking it.
#>
[CmdletBinding()]
param (
	[string] $PublishDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/wasm-trimmed'),
	[string] $BaselinePath = (Join-Path (Split-Path -Parent $PSScriptRoot) '.config/wasm-size-baseline.json'),
	[switch] $Update
)

$ErrorActionPreference = 'Stop'

$framework = Join-Path $PublishDirectory 'wwwroot/_framework'
if (-not (Test-Path $framework)) {
	throw "No published _framework folder at '$framework'. Publish Examples.Blazor.Client first."
}

# Purview.ZodSharp.Blazor.h3u4mo6gz6.wasm.br -> Purview.ZodSharp.Blazor.wasm
$measured = [ordered]@{}
Get-ChildItem $framework -File -Filter 'Purview.ZodSharp*.br' | Sort-Object Name | ForEach-Object {
	if ($_.Name -notmatch '^(?<name>Purview\.ZodSharp(\.[A-Za-z]+)*)\.(?<fingerprint>[a-z0-9]{6,})\.(?<ext>wasm|dll)\.br$') {
		throw "Unexpected Purview.ZodSharp file name '$($_.Name)'."
	}

	$key = "$($Matches.name).$($Matches.ext)"
	if ($measured.Contains($key)) {
		throw "More than one '$key' in '$framework': the folder holds a previous publish. Delete it and publish again."
	}

	$measured[$key] = $_.Length
}

if ($measured.Count -eq 0) {
	throw "No Brotli-compressed Purview.ZodSharp assemblies in '$framework'."
}

if ($Update) {
	$sorted = [ordered]@{}
	foreach ($key in ($measured.Keys | Sort-Object)) { $sorted[$key] = $measured[$key] }

	$baseline = [ordered]@{
		description = 'Brotli-compressed sizes (bytes) of the Purview.ZodSharp assemblies in the trimmed publish of Examples.Blazor.Client. Checked by scripts/check-wasm-size.ps1; regenerate with -Update after an intended change.'
		tolerancePercent = 10
		assemblies = $sorted
	}
	$json = ($baseline | ConvertTo-Json -Depth 3) -replace "`r`n", "`n"
	[System.IO.File]::WriteAllText($BaselinePath, "$json`n")
	Write-Host "Wrote $BaselinePath"
	$sorted.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-45} {1,8:N0} B" -f $_.Key, $_.Value) }
	return
}

$baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json
$tolerance = [double] $baseline.tolerancePercent
$failures = [System.Collections.Generic.List[string]]::new()

Write-Host ("{0,-45} {1,10} {2,10} {3,8}" -f 'Assembly', 'Baseline', 'Measured', 'Change')
foreach ($entry in $baseline.assemblies.PSObject.Properties) {
	$expected = [long] $entry.Value
	if (-not $measured.Contains($entry.Name)) {
		$failures.Add("$($entry.Name) is in the baseline but not in the publish output.")
		continue
	}

	$actual = [long] $measured[$entry.Name]
	$change = ($actual - $expected) * 100.0 / $expected
	Write-Host ("{0,-45} {1,10:N0} {2,10:N0} {3,7:N1}%" -f $entry.Name, $expected, $actual, $change)
	if ($change -gt $tolerance) {
		$failures.Add(("{0} grew {1:N1}% ({2:N0} B -> {3:N0} B), over the {4}% budget." -f $entry.Name, $change, $expected, $actual, $tolerance))
	}
}

foreach ($name in $measured.Keys) {
	if (-not $baseline.assemblies.PSObject.Properties[$name]) {
		$failures.Add("$name ($($measured[$name]) B) is not in the baseline; add it with -Update.")
	}
}

if ($failures.Count -gt 0) {
	foreach ($failure in $failures) {
		if ($env:GITHUB_ACTIONS) { Write-Host "::error::$failure" } else { Write-Host "ERROR: $failure" }
	}

	exit 1
}

Write-Host "Every Purview.ZodSharp assembly is within the $tolerance% size budget."
