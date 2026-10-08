# Exercise the real installer in an isolated directory, including upgrade and uninstall.
param([string]$Installer = 'dist\ark_left-0.23.0-windows-setup.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
if (-not [System.IO.Path]::IsPathRooted($Installer)) { $Installer = Join-Path $root $Installer }
if (-not (Test-Path -LiteralPath $Installer -PathType Leaf)) { throw 'installer not found' }
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{658A2A59-691E-45D8-90DA-31C9376DE765}_is1'
if (Test-Path -LiteralPath $registration) { throw 'ark_left is already installed; use a clean Windows user for installer acceptance' }

$testRoot = Join-Path $root ('bin-installer-' + [Guid]::NewGuid().ToString('N'))
$installDir = Join-Path $testRoot 'installed app'
$stateDir = Join-Path $testRoot 'state'
$group = 'ark_left acceptance ' + [Guid]::NewGuid().ToString('N')
$groupDir = Join-Path ([Environment]::GetFolderPath('Programs')) $group
$previousState = $env:ARK_LEFT_STATE_DIR
$previousCli = $env:ARK_LEFT_CLI
$previousSuffix = $env:ARK_LEFT_INSTANCE_SUFFIX
[System.IO.Directory]::CreateDirectory($stateDir) | Out-Null
$env:ARK_LEFT_STATE_DIR = $stateDir
$env:ARK_LEFT_CLI = Join-Path $testRoot 'missing-cli.exe'
$env:ARK_LEFT_INSTANCE_SUFFIX = [Guid]::NewGuid().ToString('N')
$sentinel = Join-Path $stateDir 'preserve-user-data.txt'
[System.IO.File]::WriteAllText($sentinel, 'installer must preserve user data')

function Invoke-Setup([string]$LogName) {
    $log = Join-Path $testRoot $LogName
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="' + $installDir + '" /GROUP="' + $group + '" /LOG="' + $log + '"'
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "installer failed: exit=$($process.ExitCode), log=$log" }
}

try {
    Invoke-Setup 'install.log'
    foreach ($relative in @('ark_left.exe', 'ark_left-check.exe', 'docs\setup.md', 'unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDir $relative) -PathType Leaf)) { throw "missing installed file: $relative" }
    }
    if (-not (Test-Path -LiteralPath $registration)) { throw 'uninstall registration missing' }
    if (Test-Path -LiteralPath (Join-Path $installDir 'ark_left-tests.exe')) { throw 'test runner must not be distributed' }
    if (Test-Path -LiteralPath (Join-Path $installDir 'runtime-bootstrap')) { throw 'ArkCLI must not be distributed' }
    $shortcutPath = Join-Path $groupDir 'ark_left.lnk'
    if (-not (Test-Path -LiteralPath $shortcutPath)) { throw 'Start menu shortcut missing' }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath -ne (Join-Path $installDir 'ark_left.exe') -or $shortcut.Arguments -ne '--show') { throw 'Start menu shortcut target/arguments incorrect' }
    $buildHash = (Get-FileHash -LiteralPath (Join-Path $root 'bin-release\ark_left.exe') -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath (Join-Path $installDir 'ark_left.exe') -Algorithm SHA256).Hash
    if ($buildHash -ne $installedHash) { throw 'installed application differs from release build' }
    Write-Host 'PASS: silent install, payload, registration and Start menu shortcut'

    & (Join-Path $root 'tests\verify-interaction.ps1') -OutputDir $installDir
    if (-not $?) { throw 'installed application interaction check failed' }
    Write-Host 'PASS: installed application startup, hide, single-instance wake and exit'

    # Simulate the exact bundled paths left by a previous installation. The
    # upgrade must remove these files while retaining unrelated user files.
    foreach ($legacyArch in @('amd64', 'arm64')) {
        $legacyDir = Join-Path $installDir "runtime-bootstrap\$legacyArch"
        [System.IO.Directory]::CreateDirectory($legacyDir) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $legacyDir 'arkcli.exe'), 'legacy bundled runtime fixture')
    }
    $runtimeUserFile = Join-Path $installDir 'runtime-bootstrap\user-notes.txt'
    [System.IO.File]::WriteAllText($runtimeUserFile, 'preserve unrelated user file')
    Invoke-Setup 'upgrade.log'
    if ((Get-FileHash -LiteralPath (Join-Path $installDir 'ark_left.exe') -Algorithm SHA256).Hash -ne $buildHash) { throw 'upgrade changed application bytes' }
    if ([System.IO.File]::ReadAllText($sentinel) -ne 'installer must preserve user data') { throw 'upgrade changed user state' }
    foreach ($legacyArch in @('amd64', 'arm64')) {
        if (Test-Path -LiteralPath (Join-Path $installDir "runtime-bootstrap\$legacyArch\arkcli.exe")) { throw 'upgrade left legacy bundled ArkCLI' }
    }
    if ([System.IO.File]::ReadAllText($runtimeUserFile) -ne 'preserve unrelated user file') { throw 'upgrade removed unrelated user file' }
    Write-Host 'PASS: upgrade removes legacy bundled runtime and preserves user data'

    $uninstallLog = Join-Path $testRoot 'uninstall.log'
    $uninstaller = Start-Process -FilePath (Join-Path $installDir 'unins000.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + $uninstallLog + '"') -Wait -PassThru
    if ($uninstaller.ExitCode -ne 0) { throw "uninstall failed: exit=$($uninstaller.ExitCode), log=$uninstallLog" }
    if (Test-Path -LiteralPath (Join-Path $installDir 'ark_left.exe')) { throw 'uninstall left application executable' }
    if (Test-Path -LiteralPath $registration) { throw 'uninstall left registration' }
    if (Test-Path -LiteralPath $shortcutPath) { throw 'uninstall left Start menu shortcut' }
    if ([System.IO.File]::ReadAllText($sentinel) -ne 'installer must preserve user data') { throw 'uninstall changed user state' }
    Write-Host 'PASS: uninstall removes app, registration and shortcut; user data retained'
    Write-Host "Installer acceptance passed; logs: $testRoot"
} finally {
    $env:ARK_LEFT_STATE_DIR = $previousState
    $env:ARK_LEFT_CLI = $previousCli
    $env:ARK_LEFT_INSTANCE_SUFFIX = $previousSuffix
}
