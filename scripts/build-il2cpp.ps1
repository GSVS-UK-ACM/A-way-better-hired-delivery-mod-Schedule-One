[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $MelonLoaderRoot,

    [string] $DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "verify-game-api.ps1") -MelonLoaderRoot $MelonLoaderRoot
if ($LASTEXITCODE) { exit $LASTEXITCODE }

& (Join-Path $PSScriptRoot "verify-contracts.ps1") -DotNet $DotNet
if ($LASTEXITCODE) { exit $LASTEXITCODE }

& $DotNet build (Join-Path $repositoryRoot "VehicleHandlers.Il2Cpp.csproj") `
    --configuration Release `
    --property:MelonLoaderRoot="$MelonLoaderRoot" `
    --nologo
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$outputPath = Join-Path $repositoryRoot "bin\Il2Cpp\VehicleHandlers.dll"
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Build completed without the expected output: $outputPath"
}

& (Join-Path $PSScriptRoot "verify-handler-runtime.ps1") `
    -MelonLoaderRoot $MelonLoaderRoot `
    -AssemblyPath $outputPath
if ($LASTEXITCODE) { exit $LASTEXITCODE }

Write-Output "Built $outputPath"

