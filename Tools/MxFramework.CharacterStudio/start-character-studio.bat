@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\.." >nul
set "ROOT_DIR=%CD%"
popd >nul

set "CLI_PROJECT=%ROOT_DIR%\Tools\MxFramework.Authoring\src\MxFramework.Authoring.Cli\MxFramework.Authoring.Cli.csproj"
set "DEFAULT_PACKAGE=Tools\MxFramework.Authoring\samples\character-iron-vanguard"

set "PORT=%~1"
if "%PORT%"=="" set "PORT=%CHARACTER_STUDIO_PORT%"
if "%PORT%"=="" set "PORT=4873"

set "PACKAGE_RELATIVE=%~2"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%CHARACTER_STUDIO_PACKAGE%"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%DEFAULT_PACKAGE%"

if "%CHARACTER_STUDIO_OPEN_BROWSER%"=="" set "CHARACTER_STUDIO_OPEN_BROWSER=1"
set "URL=http://127.0.0.1:%PORT%/Tools/MxFramework.CharacterStudio/web/"
set "STATE_URL=http://127.0.0.1:%PORT%/api/character/state"

echo %PORT%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [ERROR] Port must be numeric. Example: Tools\MxFramework.CharacterStudio\start-character-studio.bat 4874
    exit /b 1
)

if not exist "%CLI_PROJECT%" (
    echo [ERROR] Authoring CLI project not found: %CLI_PROJECT%
    exit /b 1
)

if not exist "%ROOT_DIR%\%PACKAGE_RELATIVE%\" (
    echo [ERROR] Character package folder not found: %PACKAGE_RELATIVE%
    exit /b 1
)

if not exist "%ROOT_DIR%\%PACKAGE_RELATIVE%\manifest.json" (
    echo [ERROR] Character package manifest not found: %PACKAGE_RELATIVE%\manifest.json
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

if not "%MXFRAMEWORK_FBX2GLTF%"=="" (
    if not exist "%MXFRAMEWORK_FBX2GLTF%" (
        echo [WARN] MXFRAMEWORK_FBX2GLTF is set but not found: %MXFRAMEWORK_FBX2GLTF%
    )
)

if not exist "%SCRIPT_DIR%node_modules\three\" (
    echo [WARN] CharacterStudio npm dependency three is missing. GLB preview may fall back.
    echo [WARN] Run once: npm --prefix Tools\MxFramework.CharacterStudio install
)

if not exist "%SCRIPT_DIR%node_modules\fbx2gltf\" (
    echo [WARN] CharacterStudio npm dependency fbx2gltf is missing. FBX conversion will be unavailable.
    echo [WARN] Run once: npm --prefix Tools\MxFramework.CharacterStudio install
)

set "PORT_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
    set "PORT_PID=%%P"
)

if not "%PORT_PID%"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%STATE_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
    if not errorlevel 1 (
        echo CharacterStudio-compatible Authoring server is already running on port %PORT%.
        echo URL: %URL%
        if not "%CHARACTER_STUDIO_OPEN_BROWSER%"=="0" start "" "%URL%"
        exit /b 0
    )

    echo [ERROR] Port %PORT% is already in use by process %PORT_PID%.
    echo Retry with another port: Tools\MxFramework.CharacterStudio\start-character-studio.bat 4874
    exit /b 1
)

echo MxFramework CharacterStudio
echo Root   : %ROOT_DIR%
echo Package: %PACKAGE_RELATIVE%
echo Port   : %PORT%
echo URL    : %URL%
echo Stop   : Ctrl+C
echo.

if not "%CHARACTER_STUDIO_OPEN_BROWSER%"=="0" (
    start "CharacterStudio opener" powershell -NoProfile -ExecutionPolicy Bypass -Command "$u='%URL%'; $h='%STATE_URL%'; for($i=0;$i -lt 30;$i++){ try { Invoke-WebRequest -UseBasicParsing -Uri $h -TimeoutSec 1 | Out-Null; Start-Process $u; exit 0 } catch { Start-Sleep -Seconds 1 } }; Write-Host 'CharacterStudio server did not become ready in 30 seconds. Open manually:' $u"
)

pushd "%ROOT_DIR%"
dotnet run --project "%CLI_PROJECT%" -- editor serve --root "%ROOT_DIR%" --port %PORT% --package "%PACKAGE_RELATIVE%"
set "EXITCODE=%ERRORLEVEL%"
popd

endlocal & exit /b %EXITCODE%
