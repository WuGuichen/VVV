@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\.." >nul
set "ROOT_DIR=%CD%"
popd >nul

set "CLI_PROJECT=%ROOT_DIR%\Tools\MxFramework.Authoring\src\MxFramework.Authoring.Cli\MxFramework.Authoring.Cli.csproj"
set "DEFAULT_PACKAGE=Tools/MxFramework.Authoring/samples/character-iron-vanguard"

set "PORT=%~1"
if "%PORT%"=="" set "PORT=%MXFRAMEWORK_ANIMATION_EDITOR_PORT%"
if "%PORT%"=="" set "PORT=4873"

set "PACKAGE_RELATIVE=%~2"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%MXFRAMEWORK_ANIMATION_EDITOR_PACKAGE%"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%DEFAULT_PACKAGE%"

if "%MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER%"=="" set "MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER=1"
set "URL_PACKAGE=%PACKAGE_RELATIVE:\=/%"
set "URL=http://127.0.0.1:%PORT%/Tools/MxFramework.AnimationEditor/web/?package=%URL_PACKAGE%"
set "HEALTH_PACKAGES_URL=http://127.0.0.1:%PORT%/api/authoring/animation/packages"
set "HEALTH_LOAD_URL=http://127.0.0.1:%PORT%/api/authoring/animation/load?package=%URL_PACKAGE%"

echo %PORT%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [ERROR] Port must be numeric. Example: Tools\MxFramework.AnimationEditor\start-animation-editor.bat 4874
    exit /b 1
)

if not exist "%CLI_PROJECT%" (
    echo [ERROR] Authoring CLI project not found: %CLI_PROJECT%
    exit /b 1
)

if not exist "%ROOT_DIR%\%PACKAGE_RELATIVE%\" (
    echo [ERROR] Package context folder not found: %PACKAGE_RELATIVE%
    exit /b 1
)

if not exist "%ROOT_DIR%\%PACKAGE_RELATIVE%\manifest.json" (
    echo [ERROR] Package context manifest not found: %PACKAGE_RELATIVE%\manifest.json
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet SDK not found in PATH. Install .NET 9 SDK first.
    exit /b 1
)

set "HAS_DOTNET9="
for /f "tokens=1 delims=." %%V in ('dotnet --list-sdks') do (
    if %%V GEQ 9 set "HAS_DOTNET9=1"
)

if "%HAS_DOTNET9%"=="" (
    echo [ERROR] Authoring CLI targets net9.0, but .NET 9+ SDK was not found.
    echo Installed SDKs:
    dotnet --list-sdks
    exit /b 1
)

set "PORT_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
    set "PORT_PID=%%P"
)

if not "%PORT_PID%"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%HEALTH_PACKAGES_URL%' -TimeoutSec 1 | Out-Null; Invoke-WebRequest -UseBasicParsing -Uri '%HEALTH_LOAD_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
    if not errorlevel 1 (
        echo Animation Editor-compatible Authoring server is already running on port %PORT%.
        echo URL: %URL%
        if not "%MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER%"=="0" start "" "%URL%"
        exit /b 0
    )

    echo [ERROR] Port %PORT% is already in use by process %PORT_PID%.
    echo [ERROR] The existing process is not an Animation Editor-compatible Authoring server.
    echo Stop the old process or retry with another port: Tools\MxFramework.AnimationEditor\start-animation-editor.bat 4874
    exit /b 1
)

echo MxFramework Animation Editor
echo Root   : %ROOT_DIR%
echo Context: %PACKAGE_RELATIVE%
echo Port   : %PORT%
echo URL    : %URL%
echo Stop   : Ctrl+C
echo.

if not "%MXFRAMEWORK_ANIMATION_EDITOR_OPEN_BROWSER%"=="0" (
    start "Animation Editor opener" powershell -NoProfile -ExecutionPolicy Bypass -Command "$u='%URL%'; $packages='%HEALTH_PACKAGES_URL%'; $load='%HEALTH_LOAD_URL%'; for($i=0;$i -lt 30;$i++){ try { Invoke-WebRequest -UseBasicParsing -Uri $packages -TimeoutSec 1 | Out-Null; Invoke-WebRequest -UseBasicParsing -Uri $load -TimeoutSec 1 | Out-Null; Start-Process $u; exit 0 } catch { Start-Sleep -Seconds 1 } }; Write-Host 'Animation Editor server did not become ready in 30 seconds. Open manually:' $u"
)

pushd "%ROOT_DIR%"
dotnet run --project "%CLI_PROJECT%" -- editor serve --root "%ROOT_DIR%" --port %PORT% --package "%PACKAGE_RELATIVE%"
set "EXITCODE=%ERRORLEVEL%"
popd

endlocal & exit /b %EXITCODE%
