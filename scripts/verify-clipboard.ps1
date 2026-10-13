[CmdletBinding()]
param(
    [string] $DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "tests\VehicleHandlers.Clipboard.Tests\VehicleHandlers.Clipboard.Tests.csproj"

& $DotNet run --project $project --configuration Release --nologo
if ($LASTEXITCODE) { exit $LASTEXITCODE }
