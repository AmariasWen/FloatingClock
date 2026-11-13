@echo off
cd /d "%~dp0"
if exist "release\FloatingClock.exe" (
    start "" "release\FloatingClock.exe"
) else (
    dotnet run --project "%~dp0FloatingClock.csproj" --configuration Release
)
