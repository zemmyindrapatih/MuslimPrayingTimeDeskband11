@echo off
REM Build single self-contained EXE in Release mode
REM Usage: run this batch file from the project directory

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o testing-build

echo.
echo Build complete. Output in testing-build\
pause
