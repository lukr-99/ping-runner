param(
    [switch]$TryPinToStart = $true,
    [switch]$Reinstall,
    [switch]$PruneOldVersions
)

$ErrorActionPreference = 'Stop'

function Get-AppVersion {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    [xml]$project = Get-Content -Path $ProjectPath

    foreach ($propertyGroup in @($project.Project.PropertyGroup))
    {
        if (-not [string]::IsNullOrWhiteSpace($propertyGroup.Version))
        {
            return $propertyGroup.Version.Trim()
        }
    }

    throw "Could not find a <Version> element in $ProjectPath."
}

function New-AppShortcut {
    param(
        [Parameter(Mandatory)]
        [string]$ShortcutPath,
        [Parameter(Mandatory)]
        [string]$TargetPath,
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = $Description
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
}

function Invoke-PinToStart {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $shell = New-Object -ComObject Shell.Application
    $folder = $shell.Namespace((Split-Path -Path $Path -Parent))

    if ($null -eq $folder)
    {
        return $false
    }

    $item = $folder.ParseName((Split-Path -Path $Path -Leaf))

    if ($null -eq $item)
    {
        return $false
    }

    try
    {
        $pinVerb = $item.Verbs() |
            Where-Object { (($_.Name -replace '&', '').Trim()) -eq 'Pin to Start' } |
            Select-Object -First 1
    }
    catch
    {
        return $false
    }

    if ($null -ne $pinVerb)
    {
        try
        {
            $pinVerb.DoIt()
            return $true
        }
        catch
        {
            return $false
        }
    }

    foreach ($canonicalVerb in @('startpin', 'pintostartscreen'))
    {
        try
        {
            $item.InvokeVerb($canonicalVerb)
            return $true
        }
        catch
        {
        }
    }

    return $false
}

$projectPath = Join-Path -Path $PSScriptRoot -ChildPath 'PingerTool.csproj'
$appVersion = Get-AppVersion -ProjectPath $projectPath
$installRoot = Join-Path -Path $env:LOCALAPPDATA -ChildPath 'Programs\PingRunner'
$installDirectory = Join-Path -Path $installRoot -ChildPath $appVersion
$currentVersionPath = Join-Path -Path $installRoot -ChildPath 'current-version.txt'
$legacyExecutablePath = Join-Path -Path $installRoot -ChildPath 'PingRunner.exe'
$shortcutPath = Join-Path -Path ([Environment]::GetFolderPath('Programs')) -ChildPath 'Ping Runner.lnk'

if (-not (Test-Path -Path $installRoot))
{
    New-Item -ItemType Directory -Path $installRoot | Out-Null
}

if (Test-Path -Path $legacyExecutablePath)
{
    Get-ChildItem -Path $installRoot -Force | Remove-Item -Recurse -Force
}
elseif (Test-Path -Path $installDirectory)
{
    if (-not $Reinstall)
    {
        throw "Ping Runner version $appVersion is already installed at $installDirectory. Re-run with -Reinstall to replace it."
    }

    Remove-Item -Path $installDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $installDirectory

$executablePath = Join-Path -Path $installDirectory -ChildPath 'PingRunner.exe'

if (-not (Test-Path -Path $executablePath))
{
    throw "Expected published executable was not found at $executablePath."
}

Set-Content -Path $currentVersionPath -Value $appVersion -Encoding ascii

if ($PruneOldVersions)
{
    Get-ChildItem -Path $installRoot -Directory |
        Where-Object { $_.Name -ne $appVersion } |
        Remove-Item -Recurse -Force
}

New-AppShortcut `
    -ShortcutPath $shortcutPath `
    -TargetPath $executablePath `
    -WorkingDirectory $installDirectory `
    -Description 'Ping Runner with live graphing and export tools.'

$pinAttempted = $false

if ($TryPinToStart)
{
    $pinAttempted = (Invoke-PinToStart -Path $shortcutPath) -or (Invoke-PinToStart -Path $executablePath)
}

Write-Host "Installed Ping Runner version $appVersion to: $installDirectory"
Write-Host "Created Start Menu shortcut: $shortcutPath"
Write-Host "Recorded current version in: $currentVersionPath"
Write-Host "Use -Reinstall to replace the currently installed version directory."

if ($TryPinToStart)
{
    if ($pinAttempted)
    {
        Write-Host 'Pin to Start was attempted through the available shell verb.'
    }
    else
    {
        Write-Host 'Windows did not expose a usable Pin to Start verb for automation on this machine.'
    }
}
