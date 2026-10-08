# check.ps1 - builds (if needed) and runs the real, sanitized direct-query diagnostic.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$bin  = Join-Path $root 'bin'
$exe  = Join-Path $bin 'ark_left-check.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1')
    if (-not $?) { throw 'build failed' }
}

& $exe
exit $LASTEXITCODE
