# verify-launcher.ps1 - isolated acceptance test for launch.ps1 (UX018 v0.10).
#
# Isolation contract (same conventions as tests/verify-interaction.ps1):
#   ARK_LEFT_STATE_DIR       -> fresh temp state dir, pre-seeded with a
#                               first-run marker + state cache + selection
#                               files that must survive byte-identically;
#   ARK_LEFT_INSTANCE_SUFFIX -> unique per-run suffix (isolated single-instance
#                               mutex Local\ark_left_single_instance_<suffix>
#                               and show event; never the real user names);
#   ARK_LEFT_CLI             -> deliberately non-existent path (fully offline).
#
# EVERY launch.ps1 invocation below is PID-restricted via -PreviousProcessId,
# so a real user instance (if any) is never scanned, signalled or closed; at
# the end we only WARN if pre-existing ark_left PIDs disappeared (the user may
# have closed them themselves). The finally block closes ONLY PIDs this script
# started: gentle WM_QUIT first, Kill purely as a last resort.

param([string]$OutputDir = 'bin-v10')
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
if ([System.IO.Path]::IsPathRooted($OutputDir)) { $outDir = $OutputDir }
else { $outDir = Join-Path $root $OutputDir }
$exe = Join-Path $outDir 'ark_left.exe'
$launcher = Join-Path $root 'launch.ps1'
$v05exe = Join-Path $root 'bin-v05\ark_left.exe'

if (-not (Test-Path -LiteralPath $launcher)) { throw 'launch.ps1 not found' }
if (-not (Test-Path -LiteralPath $v05exe)) { throw 'bin-v05\ark_left.exe not found (old-version fixture missing)' }
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1') -OutputDir $OutputDir
    if (-not $?) { throw 'build failed' }
}

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ArkLeftLaunchTest
{
    public static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        // All top-level windows of the target PID as "tid|class|visible" rows
        // (hidden windows included).
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

        public static IntPtr FindVisibleWindow(int targetPid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)targetPid && IsWindowVisible(h)) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static bool HasVisibleWindow(int targetPid) { return FindVisibleWindow(targetPid) != IntPtr.Zero; }

        public static bool AnyWindowThread(int targetPid)
        {
            return WindowRows(targetPid).Count > 0;
        }

        public static bool PostQuitToPid(int targetPid)
        {
            // Same UI-thread strategy as launch.ps1: ONLY the thread owning
            // WinForms windows; no fallback (this project is all-WinForms).
            List<string> rows = WindowRows(targetPid);
            List<uint> ui = new List<uint>();
            foreach (string r in rows)
            {
                string[] parts = r.Split('|');
                if (parts[1].StartsWith("WindowsForms10", StringComparison.OrdinalIgnoreCase)
                    && !ui.Contains(uint.Parse(parts[0]))) { ui.Add(uint.Parse(parts[0])); }
            }
            if (ui.Count != 1) { return false; }
            return PostThreadMessage(ui[0], 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
        }
    }

    public static class Proc
    {
        [DllImport("ntdll.dll")] public static extern uint NtSuspendProcess(IntPtr handle);
        [DllImport("ntdll.dll")] public static extern uint NtResumeProcess(IntPtr handle);
    }
}
"@

function Test-Alive($p) {
    if ($null -eq $p) { return $false }
    try { $p.Refresh() } catch { return $false }
    return (-not $p.HasExited)
}

function Wait-Visible([int]$procId, [int]$timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if ([ArkLeftLaunchTest.Native]::HasVisibleWindow($procId)) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

function Wait-Windowless([int]$procId, [int]$timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if (-not [ArkLeftLaunchTest.Native]::AnyWindowThread($procId)) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

function Wait-SingletonMutex([string]$suffix, [int]$timeoutMs) {
    $name = 'Local\ark_left_single_instance_' + $suffix
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        $m = $null
        $ok = [System.Threading.Mutex]::TryOpenExisting($name, [ref]$m)
        if ($ok -and $m) { $m.Dispose(); return $true }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

function Wait-MutexGone([string]$suffix, [int]$timeoutMs) {
    $name = 'Local\ark_left_single_instance_' + $suffix
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        $m = $null
        $ok = [System.Threading.Mutex]::TryOpenExisting($name, [ref]$m)
        if ($ok -and $m) { $m.Dispose() } else { return $true }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

function Stop-TestProc($p) {
    if ($null -eq $p) { return }
    try { $p.Refresh() } catch { return }
    try {
        if (-not $p.HasExited) {
            [void][ArkLeftLaunchTest.Native]::PostQuitToPid($p.Id)
            try { $p.WaitForExit(5000) | Out-Null } catch { }
        }
    } catch { }
    try { $p.Refresh() } catch { }
    try { if (-not $p.HasExited) { $p.Kill() } } catch { }
    try { $p.WaitForExit(5000) | Out-Null } catch { }
}

function Invoke-Launcher([string[]]$launcherArgs) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # launcher errors arrive on stderr
    try {
        $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $launcher @launcherArgs 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prev
    }
    $text = (@($out) | ForEach-Object { $_.ToString() }) -join "`n"
    return [pscustomobject]@{ ExitCode = $code; Output = $text }
}

function Get-StateHashes([string]$dir) {
    $map = @{}
    foreach ($name in @('first-run.done', 'floating-preferences.json', 'floating-settings.json', 'quota-cache.dat')) {
        $p = Join-Path $dir $name
        if (Test-Path -LiteralPath $p) { $map[$name] = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
        else { $map[$name] = '<missing>' }
    }
    return $map
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
$stateDir = Join-Path $env:TEMP ('ark_left_launcher_verify_' + $suffix)
$rejectDir = Join-Path $env:TEMP ('ark_left_reject_' + $suffix)

$started = New-Object System.Collections.ArrayList
$preExisting = @(Get-Process -Name 'ark_left' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$newPid = 0
$newProc = $null

try {
    $env:ARK_LEFT_STATE_DIR = $stateDir
    $env:ARK_LEFT_INSTANCE_SUFFIX = $suffix
    $env:ARK_LEFT_CLI = 'C:\ark_left_verify_missing\arkcli-does-not-exist.exe'

    Write-Host ("verify-launcher: suffix=" + $suffix)

    # Pre-seed state: first-run marker + state cache + selection files. Neither
    # the v05 fixture nor bin-v10 may ever rewrite / delete them.
    New-Item -ItemType Directory -Path $stateDir | Out-Null
    Set-Content -LiteralPath (Join-Path $stateDir 'first-run.done') -Value 'version=0.5.0.0 date=2026-10-04' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $stateDir 'floating-preferences.json') -Value '{"Version":2,"PositionLocked":false,"ReduceMotion":false}' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $stateDir 'floating-settings.json') -Value '{"Version":1}' -Encoding ASCII
    [System.IO.File]::WriteAllBytes((Join-Path $stateDir 'quota-cache.dat'), [System.Text.Encoding]::ASCII.GetBytes('seed-bytes-not-a-valid-snapshot'))
    $before = Get-StateHashes $stateDir

    # ---- 1) v05 fixture: silent start, owns the isolated singleton mutex ----
    $v05 = Start-Process -FilePath $v05exe -PassThru
    [void]$started.Add($v05)
    if (-not (Wait-SingletonMutex $suffix 15000)) { Fail 'v05 fixture did not take the isolated single-instance mutex' }
    else { Write-Host 'ok: v05 fixture running and holding isolated mutex' }

    # ---- 2) launcher replaces v05 with bin-v10 (PID-restricted) ----
    $r1 = Invoke-Launcher @('-OutputDir', $OutputDir, '-PreviousProcessId', [string]$v05.Id)
    if ($r1.ExitCode -ne 0) {
        Fail ("launcher run 1 exit " + $r1.ExitCode + " output: " + $r1.Output)
    }
    if ($r1.Output -notmatch ('CLOSED pid=' + $v05.Id + ' exitcode=0')) {
        Fail 'launcher run 1 did not report CLOSED pid=<v05> exitcode=0'
    } else { Write-Host 'ok: launcher reported v05 closed with exitcode 0' }

    try { $v05.WaitForExit(5000) | Out-Null } catch { }
    if (-not $v05.HasExited) { Fail 'v05 still running after launcher' }
    elseif ($v05.ExitCode -ne 0) { Fail ('v05 exit code ' + $v05.ExitCode + ' (expected 0 after gentle WM_QUIT)') }
    else { Write-Host 'ok: old v05 exited with code 0' }

    if (-not (Wait-Windowless $v05.Id 5000)) { Fail 'v05 still owns top-level windows after exit' }
    else { Write-Host 'ok: v05 left no top-level windows behind' }
    if (-not (Wait-SingletonMutex $suffix 5000)) { Fail 'isolated singleton mutex not held after replacement' }
    else { Write-Host 'ok: isolated mutex held again - the new instance took it over, so the old one released it' }

    $m = [regex]::Match($r1.Output, 'LAUNCHED pid=(\d+) path=(.+)')
    if (-not $m.Success) { Fail ('launcher run 1 did not report LAUNCHED: ' + $r1.Output) }
    else {
        $newPid = [int]$m.Groups[1].Value
        $launchedPath = $m.Groups[2].Value.Trim()
        if ($launchedPath -ine $exe) { Fail ('launched path mismatch: ' + $launchedPath) }
        else { Write-Host ('ok: new instance path = ' + $launchedPath) }
        $newProc = Get-Process -Id $newPid -ErrorAction SilentlyContinue
        if ($null -eq $newProc) { Fail 'new instance process not alive' }
        else { [void]$started.Add($newProc) }
        if (-not (Wait-Visible $newPid 15000)) { Fail ('new ' + $OutputDir + ' instance did not show a visible window') }
        else { Write-Host ('ok: new ' + $OutputDir + ' instance visible') }
    }

    # ---- 3) first-run marker + state cache + selection files preserved ----
    $after1 = Get-StateHashes $stateDir
    foreach ($name in @('first-run.done', 'floating-preferences.json', 'floating-settings.json', 'quota-cache.dat')) {
        if ($after1[$name] -ne $before[$name]) { Fail ('state file changed during replacement: ' + $name) }
    }
    if ($script:failures -eq 0) { Write-Host 'ok: marker / cache / selection files byte-identical after replacement' }

    # ---- 4) same-version relaunch reuses the original pid ----
    if ($newPid -gt 0) {
        $r2 = Invoke-Launcher @('-OutputDir', $OutputDir, '-PreviousProcessId', [string]$newPid)
        if ($r2.ExitCode -ne 0) { Fail ('launcher run 2 (reuse) exit ' + $r2.ExitCode + ' output: ' + $r2.Output) }
        if ($r2.Output -notmatch ('REUSED pid=' + $newPid)) { Fail ('launcher run 2 did not report REUSED pid=' + $newPid) }
        else { Write-Host 'ok: same-version relaunch reported REUSED' }
        if (-not (Test-Alive $newProc)) { Fail ('original ' + $OutputDir + ' pid exited on same-version relaunch') }
        else { Write-Host ('ok: original ' + $OutputDir + ' pid still running after relaunch') }
    }

    # ---- 5) exit-failure path: suspended instance ignores WM_QUIT -> timeout ----
    $suffix2 = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $env:ARK_LEFT_INSTANCE_SUFFIX = $suffix2
    $v05b = Start-Process -FilePath $v05exe -PassThru
    [void]$started.Add($v05b)
    if (-not (Wait-SingletonMutex $suffix2 15000)) { Fail 'second v05 fixture did not take its isolated mutex' }
    Start-Sleep -Milliseconds 500
    $nt = [ArkLeftLaunchTest.Proc]::NtSuspendProcess($v05b.Handle)
    if ($nt -ne 0) { Fail ('NtSuspendProcess failed with status ' + $nt) }
    $r3 = Invoke-Launcher @('-OutputDir', $OutputDir, '-PreviousProcessId', [string]$v05b.Id, '-NoLaunch')
    if ($r3.ExitCode -eq 0) { Fail ('launcher accepted a non-exiting old instance (output: ' + $r3.Output + ')') }
    elseif ($r3.Output -notmatch 'did not exit within 10s') { Fail ('launcher failed for an unexpected reason (output: ' + $r3.Output + ')') }
    else { Write-Host 'ok: launcher reported timeout failure (non-zero exit) for the suspended instance' }
    if ($r3.Output -match 'BUILD ') { Fail '-NoLaunch run attempted a build' }
    else { Write-Host 'ok: -NoLaunch run did not attempt a build' }
    if (Test-Alive $v05b) { Write-Host 'ok: suspended instance was NOT killed by the launcher' }
    else { Fail 'suspended instance died during launcher timeout wait' }
    $null = [ArkLeftLaunchTest.Proc]::NtResumeProcess($v05b.Handle)
    $resumed = $v05b.WaitForExit(10000)
    if ($resumed -and $v05b.ExitCode -eq 0) {
        Write-Host 'ok: resumed v05 processed the queued WM_QUIT and exited 0'
        if (-not (Wait-MutexGone $suffix2 5000)) { Fail 'suspended fixture mutex still openable after its clean exit' }
        else { Write-Host 'ok: suspended fixture mutex released after its clean exit' }
    }
    else { Write-Host 'WARN: resumed v05 did not exit 0 within 10s (queued WM_QUIT re-delivery not verified); cleanup handles it' }

    # ---- 5b) nested target dir is rejected before anything else happens ----
    $rN = Invoke-Launcher @('-OutputDir', 'tests\nested-mock\bin-v10', '-PreviousProcessId', [string]$newPid)
    if ($rN.ExitCode -eq 0) { Fail ('launcher accepted a nested target dir (output: ' + $rN.Output + ')') }
    elseif ($rN.Output -notmatch 'direct child of the project root') { Fail ('nested target rejected for an unexpected reason (output: ' + $rN.Output + ')') }
    else { Write-Host 'ok: nested target dir rejected with non-zero exit' }
    if (-not (Test-Alive $newProc)) { Fail ($OutputDir + ' instance closed by nested-target rejection run') }
    else { Write-Host ('ok: ' + $OutputDir + ' instance untouched by the nested-target rejection') }

    # ---- 6) non-project path is rejected and untouched ----
    $suffix3 = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $env:ARK_LEFT_INSTANCE_SUFFIX = $suffix3
    New-Item -ItemType Directory -Path $rejectDir | Out-Null
    $rejectExe = Join-Path $rejectDir 'ark_left.exe'
    Copy-Item -LiteralPath $exe -Destination $rejectExe
    $copy = Start-Process -FilePath $rejectExe -PassThru
    [void]$started.Add($copy)
    if (-not (Wait-SingletonMutex $suffix3 15000)) { Fail 'out-of-project copy did not take its isolated mutex' }
    $r4 = Invoke-Launcher @('-OutputDir', $OutputDir, '-PreviousProcessId', [string]$copy.Id, '-NoLaunch')
    if ($r4.ExitCode -eq 0) { Fail ('launcher accepted an out-of-project path (output: ' + $r4.Output + ')') }
    elseif ($r4.Output -notmatch 'outside project whitelist') { Fail ('launcher rejected for an unexpected reason (output: ' + $r4.Output + ')') }
    else { Write-Host 'ok: out-of-project path rejected with non-zero exit' }
    if (-not (Test-Alive $copy)) { Fail 'out-of-project copy was closed/killed by the launcher' }
    else { Write-Host 'ok: out-of-project copy untouched by the launcher' }

    # ---- 7) restricted mode never touched the running bin-v10 instance ----
    if ($null -ne $newProc) {
        if (-not (Test-Alive $newProc)) { Fail ($OutputDir + ' instance disappeared during failure-path runs') }
        else { Write-Host ('ok: ' + $OutputDir + ' instance unaffected by failure-path launcher runs') }
    }
}
finally {
    foreach ($p in $started) { Stop-TestProc $p }
    if ($null -ne $oldState) { $env:ARK_LEFT_STATE_DIR = $oldState } else { Remove-Item Env:\ARK_LEFT_STATE_DIR -ErrorAction SilentlyContinue }
    if ($null -ne $oldSuffix) { $env:ARK_LEFT_INSTANCE_SUFFIX = $oldSuffix } else { Remove-Item Env:\ARK_LEFT_INSTANCE_SUFFIX -ErrorAction SilentlyContinue }
    if ($null -ne $oldCli) { $env:ARK_LEFT_CLI = $oldCli } else { Remove-Item Env:\ARK_LEFT_CLI -ErrorAction SilentlyContinue }
    foreach ($preId in $preExisting) {
        if ($null -eq (Get-Process -Id $preId -ErrorAction SilentlyContinue)) {
            Write-Host ("WARN: pre-existing ark_left pid " + $preId + " is no longer running (the launcher was always PID-restricted here; verify no external cause)")
        }
    }
    try { Remove-Item -LiteralPath $stateDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -LiteralPath $rejectDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
}

if ($failures -gt 0) {
    Write-Host ("verify-launcher: " + $failures + " failure(s)")
    exit 1
}
Write-Host 'verify-launcher: all checks passed'
exit 0
