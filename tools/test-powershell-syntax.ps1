[CmdletBinding()]
param(
    [string[]]$Roots
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scripts = [System.Collections.Generic.List[System.IO.FileInfo]]::new()

if (-not $Roots) {
    $Roots = @('templates', 'examples', 'tools', 'installer') | Where-Object {
        Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Container
    }
}

foreach ($rootValue in $Roots) {
    $root = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $rootValue))
    $prefix = $repositoryRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    $isRepositoryRoot = $root.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)
    if (-not $isRepositoryRoot -and -not $root.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Syntax-check root must remain under the repository: $root"
    }
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        throw "Syntax-check root does not exist: $root"
    }
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.ps1' | ForEach-Object {
        $scripts.Add($_)
    }
}

$syntaxErrors = [System.Collections.Generic.List[string]]::new()
foreach ($script in $scripts | Sort-Object FullName -Unique) {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $script.FullName,
        [ref]$tokens,
        [ref]$parseErrors
    ) | Out-Null
    $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $script.FullName)
    foreach ($parseError in $parseErrors) {
        $syntaxErrors.Add("$relative`:$($parseError.Extent.StartLineNumber): $($parseError.Message)")
    }

    # Windows PowerShell 5.1 leaves $PSScriptRoot empty inside the parameter defaults of an advanced
    # script started with `powershell -File`, so a default such as (Join-Path $PSScriptRoot '..')
    # throws there while passing under pwsh. Resolve such defaults in the script body instead.
    $scriptAst = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$null, [ref]$null)
    if ($scriptAst.ParamBlock) {
        $scriptAst.ParamBlock.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.VariableExpressionAst] -and
                $node.VariablePath.UserPath -eq 'PSScriptRoot'
        }, $true) | ForEach-Object {
            $syntaxErrors.Add("$relative`:$($_.Extent.StartLineNumber): `$PSScriptRoot in a parameter default is empty under Windows PowerShell 5.1 -File; resolve it in the script body.")
        }
    }
}

if ($syntaxErrors.Count -gt 0) {
    Write-Host 'PowerShell syntax validation failed:' -ForegroundColor Red
    $syntaxErrors | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "PowerShell syntax passed for $($scripts.Count) scripts." -ForegroundColor Green
