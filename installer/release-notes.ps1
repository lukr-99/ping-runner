<#
  Write a version's section of CHANGELOG.md to a temporary file and print its path, for
  `gh release create --notes-file`. Fails when the changelog has no section for the version, so a
  release never goes out without notes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$changelog = Join-Path $PSScriptRoot '..\CHANGELOG.md'
$lines = Get-Content -LiteralPath $changelog -Encoding utf8
$start = -1
for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match "^## \[$([regex]::Escape($Version))\]") {
        $start = $index + 1
        break
    }
}
if ($start -lt 0) { throw "CHANGELOG.md has no section for $Version." }

$end = $lines.Count
for ($index = $start; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match '^## \[') {
        $end = $index
        break
    }
}

$notes = ($lines[$start..($end - 1)] -join "`n").Trim()
$path = Join-Path ([System.IO.Path]::GetTempPath()) "pingrunner-$Version-notes.md"
[System.IO.File]::WriteAllText($path, $notes + "`n", [System.Text.UTF8Encoding]::new($false))
$path
