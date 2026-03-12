param(
    [switch]$TryPinToStart = $true
)

$ErrorActionPreference = 'Stop'

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
$installDirectory = Join-Path -Path $env:LOCALAPPDATA -ChildPath 'Programs\PingRunner'
$shortcutPath = Join-Path -Path ([Environment]::GetFolderPath('Programs')) -ChildPath 'Ping Runner.lnk'

if (Test-Path -Path $installDirectory)
{
    Get-ChildItem -Path $installDirectory -Force | Remove-Item -Recurse -Force
}
else
{
    New-Item -ItemType Directory -Path $installDirectory | Out-Null
}

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

Write-Host "Installed Ping Runner to: $installDirectory"
Write-Host "Created Start Menu shortcut: $shortcutPath"

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
