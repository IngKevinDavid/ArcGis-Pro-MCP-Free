param(
    [Parameter(Mandatory = $true)][string]$ArcGISProDir,
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & $Dotnet build -c Release "-p:ArcGISProDir=$ArcGISProDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Add-in compilation failed.' }
    & $Python (Join-Path $PSScriptRoot 'package_addin.py')
    if ($LASTEXITCODE -ne 0) { throw 'Add-in packaging failed.' }
} finally { Pop-Location }
