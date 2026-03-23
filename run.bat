@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "SLN=%ROOT%DesktopAssistantLite.sln"
set "EXE=%ROOT%DesktopAssistantLite.App\bin\Debug\net8.0-windows\DesktopAssistantLite.App.exe"
set "INSTALLER=%ROOT%installers\dotnet-sdk-8.0.419-win-x64.exe"

if exist "%EXE%" goto run_exe

where dotnet >nul 2>nul
if errorlevel 1 goto missing_dotnet

pushd "%ROOT%"
dotnet build "%SLN%"
if errorlevel 1 goto build_failed
popd

if not exist "%EXE%" goto exe_missing

:run_exe
start "" "%EXE%"
exit /b 0

:missing_dotnet
echo [ERROR] dotnet was not found.
if exist "%INSTALLER%" echo Install "%INSTALLER%" first.
if not exist "%INSTALLER%" echo Install .NET 8 SDK or .NET 8 Windows Desktop Runtime first.
pause
exit /b 1

:build_failed
popd
echo [ERROR] Build failed.
pause
exit /b 1

:exe_missing
echo [ERROR] Build finished, but exe was not found:
echo %EXE%
pause
exit /b 1
