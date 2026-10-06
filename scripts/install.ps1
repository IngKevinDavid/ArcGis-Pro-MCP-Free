$ErrorActionPreference = 'Stop'
if (Get-Process -Name ArcGISPro -ErrorAction SilentlyContinue) {
    throw 'Close ArcGIS Pro before installing. Its loaded DLL and assembly cache cannot be updated safely while it is running.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagePath = Join-Path $repoRoot 'package\ArcGisProMcpFree.esriAddinX'
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw 'Build the add-in package first.' }
$installRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ArcGIS\AddIns\ArcGISPro'
$installedPath = Join-Path $installRoot 'ArcGisProMcpFree.esriAddinX'
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ESRI\ArcGISPro\AssemblyCache'))
$cachePath = [IO.Path]::GetFullPath((Join-Path $cacheRoot '{59c64cb3-4493-4270-8a30-43b67c25e82e}'))
$backupRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ArcGIS-Lab-MCP\backups'))
$backupPath = [IO.Path]::GetFullPath((Join-Path $backupRoot (Get-Date -Format 'yyyyMMdd_HHmmss_fff')))

# Move only this add-in's exact cache folder, into this installer's backup root.
if ((Split-Path -Parent $cachePath) -ne $cacheRoot -or (Split-Path -Leaf $cachePath) -ne '{59c64cb3-4493-4270-8a30-43b67c25e82e}' -or
    (Split-Path -Parent $backupPath) -ne $backupRoot) { throw 'Unexpected cache or backup path.' }
if (Test-Path -LiteralPath $cachePath) {
    if ((Get-Item -LiteralPath $cachePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The add-in cache must not be a link.' }
}
New-Item -ItemType Directory -Path $backupPath | Out-Null
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
if (Test-Path -LiteralPath $installedPath) {
    Copy-Item -LiteralPath $installedPath -Destination (Join-Path $backupPath 'ArcGisProMcpFree.esriAddinX')
}
if (Test-Path -LiteralPath $cachePath) {
    Move-Item -LiteralPath $cachePath -Destination (Join-Path $backupPath 'assembly-cache')
}
Copy-Item -LiteralPath $packagePath -Destination $installedPath -Force
if ((Get-FileHash -LiteralPath $packagePath).Hash -ne (Get-FileHash -LiteralPath $installedPath).Hash) { throw 'Installed package hash does not match.' }
Write-Output "Installed: $installedPath"
Write-Output "Previous package/cache backup: $backupPath"
Write-Output 'Open ArcGIS Pro and click GIS Lab MCP > Start.'
