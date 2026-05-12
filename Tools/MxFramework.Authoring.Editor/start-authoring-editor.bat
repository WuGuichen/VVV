@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\.." >nul
set "ROOT_DIR=%CD%"
popd >nul

set "PORT=%~1"
if "%PORT%"=="" set "PORT=4873"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet SDK not found in PATH. Install .NET 8 SDK first.
    exit /b 1
)

echo MxFramework Authoring Editor
echo Root: %ROOT_DIR%
echo Port: %PORT%
echo URL : http://127.0.0.1:%PORT%/Tools/MxFramework.Authoring.Editor/web/
echo.

pushd "%ROOT_DIR%"
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve --root "%ROOT_DIR%" --port %PORT%
set "EXITCODE=%ERRORLEVEL%"
popd

endlocal & exit /b %EXITCODE%
