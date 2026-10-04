# test.ps1 - builds (if needed) and runs the dependency-free test runner.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$bin  = Join-Path $root 'bin'
$tests = Join-Path $bin 'ark_left-tests.exe'

if (-not (Test-Path -LiteralPath $tests)) {
    & (Join-Path $root 'build.ps1')
    if (-not $?) { throw 'build failed' }
}

# Isolate any state the tests might touch from the real user first-run marker.
$env:ARK_LEFT_STATE_DIR = Join-Path $bin 'test-state'

Write-Host "running: $tests"
& $tests
exit $LASTEXITCODE
