# verify-interaction.ps1
# Reproducible, isolated integration check for the tray interaction flow.
#
# It starts the real ark_left.exe under an ISOLATED state dir, an ISOLATED
# instance name suffix and no direct login session (queries stay offline).
# NON-EXISTENT ARK_LEFT_CLI isolates retained CLI capabilities. It only touches windows and
# processes that belong to PIDs it started itself; it never signals, closes or
# kills a normal user instance.
#
# Flow:
#   1. Instance A (--show): wait for a visible window; WM_CLOSE it and assert it
#      HIDES (does not exit) and stays alive.
#   2. Instance B (no --show): existing A is woken (window visible again), B
#      exits 0 and only A remains -> still single instance.
#   3. Stop A. Start C (no --show) with the same marker: it must stay silent
#      (no visible window) yet be alive.
#   4. Start D (--show): D exits 0 and wakes C (C's window becomes visible).
#
# On any failure the script exits non-zero. The finally block stops only the
# PIDs this script started, restores the environment and removes the isolated
# state directory.

param([string]$OutputDir = 'bin')
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
# Relative OutputDir resolves against the repo root; an absolute OutputDir is
# used verbatim (Join-Path would concatenate and break the path).
if ([System.IO.Path]::IsPathRooted($OutputDir)) { $outDir = $OutputDir }
else { $outDir = Join-Path $root $OutputDir }
$exe  = Join-Path $outDir 'ark_left.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1') -OutputDir $OutputDir
    if (-not $?) { throw 'build failed' }
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ArkLeftWin {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    public static IntPtr FindVisibleWindow(int targetPid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == (uint)targetPid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static bool HasVisibleWindow(int targetPid) {
        return FindVisibleWindow(targetPid) != IntPtr.Zero;
    }
}
"@

function Wait-Visible([int]$procId, [int]$timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if ([ArkLeftWin]::HasVisibleWindow($procId)) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

function Wait-Hidden([int]$procId, [int]$timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if (-not [ArkLeftWin]::HasVisibleWindow($procId)) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

function Test-Alive($p) {
    if ($null -eq $p) { return $false }
    try { $p.Refresh() } catch { return $false }
    return (-not $p.HasExited)
}

function Stop-TestProc($p) {
    if ($null -eq $p) { return }
    try { $p.Refresh() } catch { return }
    try { if (-not $p.HasExited) { $p.Kill() } } catch { }
    try { $p.WaitForExit(5000) | Out-Null } catch { }
}

$failures = 0
function Fail([string]$msg) {
    $script:failures++
    Write-Host ("FAIL: " + $msg)
}

# ---- isolated environment ----
$oldState = $env:ARK_LEFT_STATE_DIR
$oldSuffix = $env:ARK_LEFT_INSTANCE_SUFFIX
$oldCli = $env:ARK_LEFT_CLI
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$stateDir = Join-Path $env:TEMP ('ark_left_verify_' + $suffix)

$started = New-Object System.Collections.ArrayList

function Start-Instance([string[]]$extraArgs) {
    $p = $null
    if ($null -ne $extraArgs -and $extraArgs.Count -gt 0) {
        $p = Start-Process -FilePath $exe -ArgumentList $extraArgs -PassThru
    } else {
        $p = Start-Process -FilePath $exe -PassThru
    }
    [void]$started.Add($p)
    return $p
}

try {
    $env:ARK_LEFT_STATE_DIR = $stateDir
    $env:ARK_LEFT_INSTANCE_SUFFIX = $suffix
    # No direct session in the fresh state keeps quota queries offline; isolate CLI too.
    $env:ARK_LEFT_CLI = 'C:\ark_left_verify_missing\arkcli-does-not-exist.exe'

    Write-Host ("verify-interaction: suffix=" + $suffix)

    # ---- 1) first instance: show, then WM_CLOSE hides it ----
    $a = Start-Instance @('--show')
    if (-not (Wait-Visible $a.Id 15000)) {
        Fail 'first instance did not show a visible window'
    } else {
        Write-Host 'ok: first instance visible window'
    }

    $hwnd = [ArkLeftWin]::FindVisibleWindow($a.Id)
    if ($hwnd -eq [IntPtr]::Zero) {
        Fail 'could not resolve first instance window handle'
    } else {
        [void][ArkLeftWin]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)  # WM_CLOSE
        if (-not (Wait-Hidden $a.Id 5000)) {
            Fail 'WM_CLOSE did not hide the first instance window'
        } else {
            Write-Host 'ok: WM_CLOSE hid the panel (process kept running)'
        }
        if (-not (Test-Alive $a)) { Fail 'first instance exited on WM_CLOSE' }
    }

    # ---- 2) second instance without --show wakes it, exits 0, single instance ----
    $b = Start-Instance @()
    $bExited = $b.WaitForExit(15000)
    if (-not $bExited) {
        Fail 'second instance did not exit'
        Stop-TestProc $b
    } elseif ($b.ExitCode -ne 0) {
        Fail ('second instance exit code ' + $b.ExitCode)
    } else {
        Write-Host 'ok: second instance (no --show) exited 0'
    }
    if (-not (Wait-Visible $a.Id 8000)) {
        Fail 'first instance window was not woken by the second instance'
    } else {
        Write-Host 'ok: first instance window woken by second launch'
    }
    $alive = 0
    foreach ($p in $started) { if (Test-Alive $p) { $alive++ } }
    if ($alive -ne 1) { Fail ("expected 1 test instance alive, got " + $alive) }
    else { Write-Host 'ok: still a single test instance' }

    # ---- 3) stop first; same marker -> new instance silent (no window) ----
    Stop-TestProc $a
    Start-Sleep -Milliseconds 500

    $c = Start-Instance @()
    Start-Sleep -Milliseconds 2500
    if (-not (Test-Alive $c)) {
        Fail 'silent third instance exited unexpectedly'
    } elseif ([ArkLeftWin]::HasVisibleWindow($c.Id)) {
        Fail 'silent third instance showed a window despite existing marker'
    } else {
        Write-Host 'ok: silent startup (marker present, no window)'
    }

    # ---- 4) --show second instance wakes the silent one ----
    $d = Start-Instance @('--show')
    $dExited = $d.WaitForExit(15000)
    if (-not $dExited) {
        Fail 'fourth instance (--show) did not exit'
        Stop-TestProc $d
    } elseif ($d.ExitCode -ne 0) {
        Fail ('fourth instance exit code ' + $d.ExitCode)
    } else {
        Write-Host 'ok: --show second instance exited 0'
    }
    if (-not (Wait-Visible $c.Id 8000)) {
        Fail 'silent instance was not woken by --show second instance'
    } else {
        Write-Host 'ok: --show woke the silent instance'
    }
}
finally {
    foreach ($p in $started) { Stop-TestProc $p }
    $env:ARK_LEFT_STATE_DIR = $oldState
    $env:ARK_LEFT_INSTANCE_SUFFIX = $oldSuffix
    $env:ARK_LEFT_CLI = $oldCli
    try { if (Test-Path -LiteralPath $stateDir) { Remove-Item -LiteralPath $stateDir -Recurse -Force } }
    catch { }
}

if ($failures -gt 0) {
    Write-Host ("verify-interaction failed: " + $failures)
    exit 1
}
Write-Host 'verify-interaction passed'
exit 0
