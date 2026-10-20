@echo off
setlocal
cd /d "%~dp0"
if not exist "rebuilt\DrcLink.Probe.exe" (
  echo Build first with PowerShell: .\scripts\Build.ps1
  exit /b 1
)
if "%~1"=="" goto usage
if /i "%~1"=="inspect" goto inspect
if /i "%~1"=="protected" goto protected
if /i "%~1"=="open" goto open
goto usage

:inspect
rebuilt\DrcLink.Probe.exe inspect
exit /b %errorlevel%

:protected
rebuilt\DrcLink.Probe.exe probe --mode protected --seconds 60
exit /b %errorlevel%

:open
rebuilt\DrcLink.Probe.exe probe --mode open --seconds 60
exit /b %errorlevel%

:usage
echo Usage: Run-Probe.cmd inspect ^| protected ^| open
echo Turn OFF Windows Mobile hotspot first. Keep Wi-Fi enabled.
exit /b 2
