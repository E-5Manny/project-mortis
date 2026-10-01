@echo off
rem Build and launch Mortis. Double-click or run from any terminal.
cd /d "%~dp0"
where dotnet >nul 2>nul && (set "DOTNET=dotnet") || (set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe")
"%DOTNET%" build -nologo -v q || (pause & exit /b 1)
start "" "bin\Debug\net10.0\win-x64\Mortis.exe"
