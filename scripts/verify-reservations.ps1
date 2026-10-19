[CmdletBinding()]
param(
    [string] $DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot "tests\VehicleHandlers.Reservations.Tests\VehicleHandlers.Reservations.Tests.csproj"

& $DotNet run --project $testProject --configuration Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
