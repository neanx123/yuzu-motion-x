@echo off
REM Local build (needs the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0)
cd /d "%~dp0"
dotnet publish YuzuMotionX.csproj -c Release -o publish
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)
copy /y "publish\YuzuMotionX.exe" "publish\Yuzu Motion X v1.2.exe" >nul
echo.
echo Done! Your program is at: %~dp0publish\Yuzu Motion X v1.2.exe
pause
