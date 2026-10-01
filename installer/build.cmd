@echo off
rem Publish a self-contained Release build and wrap it in dist\MortisSetup-<version>.exe.
rem Needs Inno Setup 6 (winget install JRSoftware.InnoSetup). Safe while the game is running: it never touches bin\Debug.
cd /d "%~dp0.."
where dotnet >nul 2>nul && (set "DOTNET=dotnet") || (set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe")
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist publish rmdir /s /q publish
"%DOTNET%" publish -nologo -v q -c Release --self-contained -p:PublishSingleFile=true -p:DebugType=none -o publish || exit /b 1
"%ISCC%" /Q installer\Mortis.iss || exit /b 1
dir /b dist
