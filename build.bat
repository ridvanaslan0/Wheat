@echo off
rem Builds a single self-contained Wheat.exe (requires the .NET 8 SDK).
dotnet publish Wheat.csproj -c Release -o publish
if errorlevel 1 (
    echo.
    echo Build failed.
    exit /b 1
)
echo.
echo Done: publish\Wheat.exe
