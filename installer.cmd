@echo off
REM =====================================================================
REM Smart Fan Cooling - Build & Package Installer
REM Usage:  .\installer              (use version from version.json)
REM         .\installer 1.0.1        (set version to 1.0.1 then build)
REM =====================================================================

if "%~1"=="" goto :build

REM Update version.json with the provided version number
echo { "version": "%~1" }> version.json
echo [VERSION] Updated to v%~1

:build
REM Read current version from version.json and display it
for /f "tokens=2 delims=:}" %%a in ('type version.json ^| findstr "version"') do (
    set "VER=%%~a"
)
REM Trim leading/trailing spaces
for /f "tokens=* delims= " %%b in ("%VER%") do set "VER=%%~b"
echo [BUILD] Building Smart Fan Cooling Hub v%VER% ...

dotnet build -t:Installer -p:AppVersion=%VER% %2 %3 %4 %5
