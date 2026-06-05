# test.ps1 - Run the offline unit test suite (no game, no Unity, no BepInEx).
# Exercises HandOfFateAccess.Core via xUnit on a modern runtime.

$ErrorActionPreference = "Stop"
dotnet test "$PSScriptRoot\HandOfFateAccess.Tests\HandOfFateAccess.Tests.csproj" @args
exit $LASTEXITCODE
