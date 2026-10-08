# launch.ps1 - UX018 (interaction v0.10) local launch entry with smooth
# replacement of old ark_left instances. Contract: docs/contracts.md.
#
# Flow: validate the target dir (whitelist pattern) -> gently close instances
# of THIS project still running from OLD build dirs (exactly one WM_QUIT to
# their WinForms UI thread located via EnumWindows incl. hidden windows; the
# app's own Cleanup() then releases tray/window/CLI resources) -> reuse a
# same-target-dir instance if one exists (wake it with --show) -> otherwise
# build the target exe if missing and start it with --show.
#
# Identity whitelist (ALL three must match before a process is ever touched):
#   1. process name 'ark_left'
#   2. main module full path directly under <root>\<bin | bin-release | bin-vN>
#   3. file name 'ark_left.exe'
# Same-named processes outside the project are never scanned, signalled or
# killed. This script never uses Stop-Process / taskkill / force and never
# overwrites binaries. It never sets environment variables and never writes
# app state or global configuration.
#
# Machine-readable output lines: CLOSED pid=<n> exitcode=<c> / REUSED pid=<n> /
# LAUNCHED pid=<n> path=<exe>. Errors: "LAUNCHER-ERROR: <reason>" on stderr,
# exit code 1; success exits 0.

param(
    [string]$OutputDir = 'bin-v21',
    [switch]$NoLaunch,
    [int]$PreviousProcessId = 0
)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$closeTimeoutMs = 10000   # per old instance, per UX018 (max 10s wait)

function Fail([string]$reason) {
    [Console]::Error.WriteLine('LAUNCHER-ERROR: ' + $reason)
    exit 1
}

# ---- native helpers: GUI thread discovery + gentle WM_QUIT ----
if (-not ('ArkLeftLaunch.Native' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ArkLeftLaunch
{
    public static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        // All top-level windows of the target PID as "tid|class|visible" rows.
        // EnumWindows returns hidden windows too, so tray-only instances with
        // invisible forms are still located (MainWindowHandle is NOT used).
        public static List<string> WindowRows(int targetPid)
        {
            List<string> rows = new List<string>();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint pid;
                uint tid = GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)targetPid)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    GetClassName(h, sb, 256);
                    rows.Add(tid + "|" + sb.ToString() + "|" + (IsWindowVisible(h) ? "1" : "0"));
                }
                return true;
            }, IntPtr.Zero);
            return rows;
        }

        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);

        public static bool PostQuit(uint threadId)
        {
            return PostThreadMessage(threadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        }
    }
}
"@
}

# ---- target dir + whitelist ----
if ([string]::IsNullOrWhiteSpace($OutputDir)) { Fail 'OutputDir is empty' }
if ([System.IO.Path]::IsPathRooted($OutputDir)) { $targetDir = $OutputDir }
else { $targetDir = Join-Path $root $OutputDir }
try { $fullTarget = [System.IO.Path]::GetFullPath($targetDir) } catch { Fail ("bad OutputDir: " + $_.Exception.Message) }
$fullRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\')
if (-not $fullTarget.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    Fail ("target dir outside project root: " + $fullTarget)
}
if ((Split-Path $fullTarget -Parent) -ine $fullRoot) {
    Fail ("target dir must be a direct child of the project root: " + $fullTarget)
}
$targetName = Split-Path $fullTarget -Leaf
if ($targetName -notmatch '^(bin|bin-release|bin-v[0-9]+)$') {
    Fail ("target dir is not a whitelisted build dir: " + $targetName)
}
$targetExe = Join-Path $fullTarget 'ark_left.exe'

# Old dirs per UX018, including the previous v0.15 build.
# If the target IS one of them, its instances are same-version (reuse), never "old".
$oldDirNames = @('bin', 'bin-release', 'bin-v04', 'bin-v05', 'bin-v06', 'bin-v07', 'bin-v08', 'bin-v09', 'bin-v10', 'bin-v11', 'bin-v12', 'bin-v13', 'bin-v14', 'bin-v15', 'bin-v16', 'bin-v17', 'bin-v18', 'bin-v19', 'bin-v20')
$oldDirNames = @($oldDirNames | Where-Object { $_ -ine $targetName })

function Test-WhitelistedExe([string]$exePath) {
    if ([string]::IsNullOrEmpty($exePath)) { return $false }
    try { $full = [System.IO.Path]::GetFullPath($exePath) } catch { return $false }
    if ((Split-Path $full -Leaf) -ine 'ark_left.exe') { return $false }
    $parent = Split-Path $full -Parent
    if (-not $parent.StartsWith($fullRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { return $false }
    $rel = $parent.Substring($fullRoot.Length + 1)
    if ($rel.Contains('\')) { return $false }   # must be a DIRECT child dir of the root
    if ($rel -ieq $targetName) { return $true }
    foreach ($n in $oldDirNames) { if ($rel -ieq $n) { return $true } }
    return $false
}

function Get-ExePathOf($procObj) {
    try { return $procObj.MainModule.FileName } catch { return $null }
}

# ---- candidate discovery ----
$candidates = @()
if ($PreviousProcessId -gt 0) {
    # Test/restricted mode: ONLY this PID is considered; other PIDs are never
    # scanned or signalled. The whitelist is still fully enforced.
    $proc = Get-Process -Id $PreviousProcessId -ErrorAction SilentlyContinue
    if ($null -eq $proc) { Fail ("specified process not found: " + $PreviousProcessId) }
    if ($proc.ProcessName -ine 'ark_left') { Fail ("specified process " + $PreviousProcessId + " is not ark_left") }
    $exePath = Get-ExePathOf $proc
    if ([string]::IsNullOrEmpty($exePath)) { Fail ("cannot read executable path of process " + $PreviousProcessId) }
    if (-not (Test-WhitelistedExe $exePath)) { Fail ("process " + $PreviousProcessId + " path outside project whitelist: " + $exePath) }
    $null = $proc.Handle   # cache while alive, so ExitCode stays readable after exit
    $candidates += ,@($proc, $exePath)
}
else {
    # Production mode: enumerate by name only to FIND candidates; every one is
    # then strictly identity-checked. Readable paths outside the whitelist are
    # skipped and never touched; an ark_left-named process whose path cannot
    # be verified fails the launch (safe side, nothing killed), so we never
    # wake an unverifiable old instance by starting the new exe.
    foreach ($proc in (Get-Process -Name 'ark_left' -ErrorAction SilentlyContinue)) {
        $exePath = Get-ExePathOf $proc
        if ([string]::IsNullOrEmpty($exePath)) { Fail ("cannot verify executable path of ark_left pid " + $proc.Id + "; refusing to proceed") }
        if (-not (Test-WhitelistedExe $exePath)) { continue }
        $null = $proc.Handle   # cache while alive, so ExitCode stays readable after exit
        $candidates += ,@($proc, $exePath)
    }
}

$samePath = @()
$oldPath = @()
foreach ($c in $candidates) {
    if ($c[1] -ieq $targetExe) { $samePath += ,$c } else { $oldPath += ,$c }
}

# ---- ensure the target exe exists / can start BEFORE closing anything ----
# (a build failure must keep the old instance running; -NoLaunch never builds)
if (-not $NoLaunch) {
    if (-not (Test-Path -LiteralPath $targetExe)) {
        Write-Host ("BUILD " + $fullTarget)
        try { & (Join-Path $root 'build.ps1') -OutputDir $fullTarget } catch { Fail ("build into " + $fullTarget + " failed: " + $_.Exception.Message) }
        if ($LASTEXITCODE -ne 0) { Fail ("build into " + $fullTarget + " failed (exit " + $LASTEXITCODE + ")") }
    }
    if (-not (Test-Path -LiteralPath $targetExe)) { Fail ("target exe missing: " + $targetExe) }
    try { if ((Get-Item -LiteralPath $targetExe).Length -le 0) { Fail ("target exe is empty: " + $targetExe) } } catch { Fail ("cannot stat target exe: " + $targetExe) }
}

# ---- gently close old-dir instances (one WM_QUIT, wait <= 10s, exit 0) ----
foreach ($c in $oldPath) {
    $proc = $c[0]
    $procId = $proc.Id
    try { $proc.Refresh() } catch { }
    try { if ($proc.HasExited) { continue } } catch { continue }
    $rows = [ArkLeftLaunch.Native]::WindowRows($procId)
    if ($rows.Count -eq 0) { Fail ("no GUI thread (no top-level window) for pid " + $procId) }
    # The app's UI thread is the one owning WinForms windows (class
    # WindowsForms10*). This project is all-WinForms; there is NO fallback:
    # zero or multiple matching threads are a hard error. Exactly ONE UI
    # thread receives exactly ONE WM_QUIT.
    $uiTids = @($rows | ForEach-Object {
        $parts = $_.Split('|')
        if ($parts[1] -like 'WindowsForms10*') { [uint32]$parts[0] }
    } | Select-Object -Unique)
    if ($uiTids.Count -eq 0) { Fail ("no WinForms UI thread found for pid " + $procId) }
    if ($uiTids.Count -gt 1) { Fail ("cannot uniquely identify GUI thread for pid " + $procId + " (" + $uiTids.Count + " UI-thread candidates)") }
    if (-not [ArkLeftLaunch.Native]::PostQuit($uiTids[0])) { Fail ("WM_QUIT delivery failed for pid " + $procId + " thread " + $uiTids[0]) }
    $exited = $false
    try { $exited = $proc.WaitForExit($closeTimeoutMs) } catch { $exited = $false }
    if (-not $exited) { Fail ("old instance pid " + $procId + " did not exit within 10s after WM_QUIT") }
    try { $null = $proc.WaitForExit(); $exitCode = $proc.ExitCode } catch { Fail ("cannot read exit code of pid " + $procId) }
    if ("$exitCode" -ne '0') { Fail ("old instance pid " + $procId + " exited with code " + "$exitCode" + " (expected 0)") }
    Write-Host ("CLOSED pid=" + $procId + " exitcode=" + $exitCode)
}

# ---- same-target-dir instance: reuse, never close ----
if ($samePath.Count -gt 0) {
    $reusedPid = $samePath[0][0].Id
    Write-Host ("REUSED pid=" + $reusedPid)
    if (-not $NoLaunch) {
        # Wake the existing instance via the app's own single-instance flow:
        # the duplicate signals Local\ark_left_show_event[+suffix] and exits 0.
        $wake = Start-Process -FilePath $targetExe -ArgumentList '--show' -WorkingDirectory $fullTarget -PassThru
        $wakeDone = $wake.WaitForExit($closeTimeoutMs)
        if (-not $wakeDone) { Fail ("duplicate --show start did not exit within 10s (existing pid " + $reusedPid + ")") }
        if ($wake.ExitCode -ne 0) { Fail ("duplicate --show start exited with code " + $wake.ExitCode + " (existing instance not woken)") }
    }
    exit 0
}

if ($NoLaunch) {
    Write-Host 'NOTHING-TO-LAUNCH (-NoLaunch)'
    exit 0
}

# ---- start the new instance ----
$newProc = Start-Process -FilePath $targetExe -ArgumentList '--show' -WorkingDirectory $fullTarget -PassThru
Write-Host ("LAUNCHED pid=" + $newProc.Id + " path=" + $targetExe)
exit 0
