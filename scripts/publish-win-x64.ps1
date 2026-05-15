[CmdletBinding()]
param(
    [string]$Version = '0.2.1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'KatLedger.csproj'
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish\win-x64'
$zipPath = Join-Path $artifactsRoot 'KatLedger-win-x64.zip'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

& dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o $publishRoot --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'KatLedger.exe'))) {
    throw "Published executable missing from $publishRoot"
}

Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

[pscustomobject]@{
    Version = $Version
    PublishRoot = $publishRoot
    Artifact = $zipPath
    Executable = (Join-Path $publishRoot 'KatLedger.exe')
}
