@echo off
cd /d "%~dp0"
if not exist "artifacts\demo\ZmxyDemo.exe" (
  echo Please run build.ps1 -Restore first.
  pause
  exit /b 1
)
start "" "artifacts\demo\ZmxyDemo.exe" %*
