<#
  build.ps1 - compiles ark_left without any .NET SDK / NuGet dependency.

  Uses the .NET Framework C# compiler (C# 5) found under
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe, falling back to the
  32-bit Framework directory if needed.

  Produces:
    bin\ark_left.exe            (WinForms tray app, Windows subsystem)
    bin\ark_left-check.exe      (console diagnostic, real query + sanitized output)
    bin\ark_left-tests.exe      (console test runner, synthetic fixtures)
#>

param([string]$OutputDir = 'bin')
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$src  = Join-Path $root 'src'
$test = Join-Path $root 'tests'
# Relative OutputDir is resolved against the repo root; an absolute OutputDir is
# used verbatim (Join-Path would otherwise concatenate and break the path).
if ([System.IO.Path]::IsPathRooted($OutputDir)) { $bin = $OutputDir }
else { $bin = Join-Path $root $OutputDir }

# Verify / create the output directory (build scripts may create bin).
if (-not (Test-Path -LiteralPath $bin)) {
    New-Item -ItemType Directory -Path $bin | Out-Null
}
if (-not (Test-Path -LiteralPath $bin -PathType Container)) {
    throw "cannot create output directory: $bin"
}

function Find-Csc {
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return (Resolve-Path -LiteralPath $c).Path }
    }
    throw 'csc.exe (C# 5 compiler) not found under the .NET Framework directories.'
}

$csc = Find-Csc
Write-Host "compiler: $csc"

# Generate a small .ico (sky-blue circle with fog-white center) so the exe has a native icon.
# Fully offline: written as raw bytes, no download and no image library.
$icoPath = Join-Path $bin 'ark_left.ico'
if (-not (Test-Path -LiteralPath $icoPath) -or
    (Get-Item -LiteralPath $PSCommandPath).LastWriteTimeUtc -gt (Get-Item -LiteralPath $icoPath).LastWriteTimeUtc) {
    try {
        Add-Type -AssemblyName System.Drawing
        $bmp = New-Object System.Drawing.Bitmap 32, 32
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(58, 131, 247))
        $g.FillEllipse($brush, 2, 2, 27, 27)
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.FillEllipse($white, 11, 12, 10, 10)
        $g.Dispose()
        $hicon = $bmp.GetHicon()
        $icon = [System.Drawing.Icon]::FromHandle($hicon)
        $fs = [System.IO.File]::Create($icoPath)
        $icon.Save($fs)
        $fs.Close()
        # release native icon handle
        Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool DestroyIcon(System.IntPtr h);' -Name NativeIcon -Namespace ArkLeftBuild | Out-Null
        [ArkLeftBuild.NativeIcon]::DestroyIcon($hicon) | Out-Null
        $icon.Dispose(); $bmp.Dispose()
        Write-Host "generated icon: $icoPath"
    } catch {
        Write-Host "icon generation skipped: $($_.Exception.Message)"
    }
}

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Security.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll'
)

$srcFiles = Get-ChildItem -LiteralPath $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$commonFiles = @()
$libraryFiles = @()
foreach ($f in $srcFiles) {
    $leaf = Split-Path $f -Leaf
    if ($leaf -eq 'Check.cs') { continue }
    $commonFiles += $f
    if ($leaf -ne 'Program.cs') { $libraryFiles += $f }
}

# 1) Tray app (Windows subsystem)
$appArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+',
             ('/out:' + (Join-Path $bin 'ark_left.exe')),
             ('/win32manifest:' + (Join-Path $root 'app.manifest')))
if (Test-Path -LiteralPath $icoPath) { $appArgs += ('/win32icon:' + $icoPath) }
$appArgs += $commonFiles
$appArgs += @('/reference:' + ($refs -join ','))
Write-Host 'building ark_left.exe ...'
& $csc @appArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed building ark_left.exe (exit $LASTEXITCODE)" }

# 2) Console check tool
$checkArgs = @('/nologo', '/target:exe', '/platform:anycpu', '/optimize+',
               '/main:ArkLeft.Check.Program',
               ('/out:' + (Join-Path $bin 'ark_left-check.exe')))
foreach ($f in $libraryFiles) { $checkArgs += $f }
$checkArgs += (Join-Path $src 'Check.cs')
$checkArgs += @('/reference:' + ($refs -join ','))
Write-Host 'building ark_left-check.exe ...'
& $csc @checkArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed building ark_left-check.exe (exit $LASTEXITCODE)" }

# 3) Test runner
$testArgs = @('/nologo', '/target:exe', '/platform:anycpu', '/optimize+',
              '/main:ArkLeft.Tests.QuotaTests',
              ('/out:' + (Join-Path $bin 'ark_left-tests.exe')))
foreach ($f in $libraryFiles) { $testArgs += $f }
$testArgs += (Join-Path $test 'QuotaTests.cs')
$testArgs += @('/reference:' + ($refs -join ','))
Write-Host 'building ark_left-tests.exe ...'
& $csc @testArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed building ark_left-tests.exe (exit $LASTEXITCODE)" }

Write-Host 'build ok:'
Get-ChildItem -LiteralPath $bin -Filter '*.exe' | ForEach-Object { Write-Host ("  " + $_.Name) }
