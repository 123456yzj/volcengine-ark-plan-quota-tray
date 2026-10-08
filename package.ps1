# Build the application and create a per-user Windows installer with Inno Setup 6.
param(
    [string]$IsccPath = '',
    [string]$OutputDir = 'dist'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not [System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir = Join-Path $root $OutputDir }

if (-not $IsccPath) {
    $isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($isccCommand) { $IsccPath = $isccCommand.Source }
    else {
        foreach ($candidate in @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $IsccPath = $candidate; break }
        }
    }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'Inno Setup 6 compiler not found. Install it or pass -IsccPath.'
}

$buildDir = Join-Path $root 'bin-release'
& (Join-Path $root 'build.ps1') -OutputDir $buildDir
if (-not $?) { throw 'release build failed' }
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $buildDir 'ark_left.exe'))
$version = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
if (-not (Test-Path -LiteralPath $OutputDir)) { [System.IO.Directory]::CreateDirectory($OutputDir) | Out-Null }

& $IsccPath "/DBuildDir=$buildDir" "/DOutputDir=$OutputDir" "/DAppVersion=$version" (Join-Path $root 'setup.iss')
if ($LASTEXITCODE -ne 0) { throw "installer compilation failed (exit $LASTEXITCODE)" }
$installer = Join-Path $OutputDir "ark_left-$version-windows-setup.exe"
$digest = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
$checksums = Join-Path $OutputDir 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($checksums, "$digest  $([System.IO.Path]::GetFileName($installer))`r`n", [System.Text.Encoding]::ASCII)
Write-Host "Installer: $installer"
Write-Host "SHA256: $digest"
