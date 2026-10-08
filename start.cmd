@echo off
setlocal
set "ROOT=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%launch.ps1" -OutputDir bin-v23
set "LAUNCH_RC=%ERRORLEVEL%"
if not "%LAUNCH_RC%"=="0" (
  echo ark_left launch failed. See LAUNCHER-ERROR above.
  pause
  exit /b %LAUNCH_RC%
)
exit /b 0
