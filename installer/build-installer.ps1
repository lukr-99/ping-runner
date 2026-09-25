<#
  Publish Ping Runner self-contained for win-x64 and compile the per-user Inno Setup installer, with
  a SHA-256 checksum beside it. From the CodePrint .NET profile.

  Release (default): -p:PingRunnerReleaseBuild=true, so the version carries no -dev suffix. -Dev builds
  a -dev installer for trying a build without mistaking it for a release.

  Output: installer/dist/PingRunner-<version>-setup.exe (+ .sha256). Ignored by Git.
#>
[CmdletBinding()]
param(
    [switch]$Dev,
    [string]$IsccPath,
    [switch]$RequireSigned
)

$ErrorActionPreference = 'Stop'
$installerRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repositoryRoot 'src\PingRunner.App\PingRunner.App.csproj'

[xml]$versionProps = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Version.props')
$baseVersion = @($versionProps.Project.PropertyGroup.PingRunnerVersion | Where-Object { $_ })[0]
if ($baseVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Version.props has no X.Y.Z PingRunnerVersion: $baseVersion" }
$version = if ($Dev) { "$baseVersion-dev" } else { $baseVersion }
$versionInfoVersion = "$baseVersion.0"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK command 'dotnet' was not found."
}
if (-not $IsccPath) {
    $IsccPath = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'Inno Setup 6 ISCC.exe was not found. Install it or pass -IsccPath.'
}

$publish = Join-Path $installerRoot 'publish'
$dist = Join-Path $installerRoot 'dist'
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
[System.IO.Directory]::CreateDirectory($publish) | Out-Null
[System.IO.Directory]::CreateDirectory($dist) | Out-Null

$releaseFlag = if ($Dev) { 'false' } else { 'true' }
Write-Host "Publishing Ping Runner $version..." -ForegroundColor Cyan
# Self-contained, so nobody has to install the .NET Desktop Runtime first.
& dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PingRunnerReleaseBuild=$releaseFlag -p:DebugType=none -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$definition = Join-Path $installerRoot 'PingRunner.iss'
& $IsccPath /Q "/DMyAppVersion=$version" "/DMyVersionInfoVersion=$versionInfoVersion" "/DPublishDir=$publish" $definition
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
# The publish folder is only the installer's input; the installer is the artifact.
Remove-Item -LiteralPath $publish -Recurse -Force

$setup = Join-Path $dist "PingRunner-$version-setup.exe"
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw "Expected installer was not produced: $setup" }

$signature = Get-AuthenticodeSignature -LiteralPath $setup
if ($RequireSigned -and $signature.Status -ne 'Valid') { throw "Installer signature is not valid: $($signature.Status)" }
if ($signature.Status -ne 'Valid') {
    Write-Warning 'The installer is not Authenticode-signed. Publish its SHA-256 checksum with it.'
}

$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$setup.sha256", "$hash  $([System.IO.Path]::GetFileName($setup))`n", [System.Text.UTF8Encoding]::new($false))

$size = [math]::Round((Get-Item -LiteralPath $setup).Length / 1MB, 1)
Write-Host "Built $setup ($size MB) and its SHA-256 checksum." -ForegroundColor Green
