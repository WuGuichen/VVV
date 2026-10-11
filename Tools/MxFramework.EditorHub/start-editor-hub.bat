@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\.." >nul
set "TOOLS_ROOT=%CD%"
popd >nul

set "AUTO_EXTERNAL_PROJECT="
set "SIBLING_FRAMEWORK_ROOT="
for %%I in ("%TOOLS_ROOT%\..\WGameFramework") do set "SIBLING_FRAMEWORK_ROOT=%%~fI"
if /I not "%SIBLING_FRAMEWORK_ROOT%"=="%TOOLS_ROOT%" if exist "%SIBLING_FRAMEWORK_ROOT%\Tools\MxFramework.Authoring\src\MxFramework.Authoring.Cli\MxFramework.Authoring.Cli.csproj" (
    if exist "%TOOLS_ROOT%\ProjectSettings\ProjectVersion.txt" (
        set "PROJECT_ROOT=%TOOLS_ROOT%"
        set "TOOLS_ROOT=%SIBLING_FRAMEWORK_ROOT%"
        set "AUTO_EXTERNAL_PROJECT=1"
    )
)

if "%AUTO_EXTERNAL_PROJECT%"=="" set "PROJECT_ROOT=%~3"
if "%PROJECT_ROOT%"=="" if "%AUTO_EXTERNAL_PROJECT%"=="" set "PROJECT_ROOT=%MXFRAMEWORK_EDITOR_HUB_PROJECT_ROOT%"
if "%PROJECT_ROOT%"=="" if "%AUTO_EXTERNAL_PROJECT%"=="" set "PROJECT_ROOT=%TOOLS_ROOT%"
for %%I in ("%PROJECT_ROOT%") do set "PROJECT_ROOT=%%~fI"

set "CLI_PROJECT=%TOOLS_ROOT%\Tools\MxFramework.Authoring\src\MxFramework.Authoring.Cli\MxFramework.Authoring.Cli.csproj"
set "DEFAULT_PACKAGE=Tools\MxFramework.Authoring\samples\character-codex-flow-test"

set "PORT=%~1"
if "%PORT%"=="" set "PORT=%MXFRAMEWORK_EDITOR_HUB_PORT%"
if "%PORT%"=="" if "%AUTO_EXTERNAL_PROJECT%"=="1" set "PORT=4973"
if "%PORT%"=="" set "PORT=4873"

set "PACKAGE_RELATIVE=%~2"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%MXFRAMEWORK_EDITOR_HUB_PACKAGE%"
if "%PACKAGE_RELATIVE%"=="" if "%AUTO_EXTERNAL_PROJECT%"=="1" if exist "%PROJECT_ROOT%\Authoring\CharacterPackages\wgame-b-prototype\manifest.json" set "PACKAGE_RELATIVE=Authoring\CharacterPackages\wgame-b-prototype"
if "%PACKAGE_RELATIVE%"=="" set "PACKAGE_RELATIVE=%DEFAULT_PACKAGE%"

if "%MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER%"=="" set "MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER=1"
set "URL=http://127.0.0.1:%PORT%/Tools/MxFramework.EditorHub/web/"
set "HEALTH_URL=http://127.0.0.1:%PORT%/api/character/packages"
set "ANIMATION_HEALTH_URL=http://127.0.0.1:%PORT%/api/authoring/animation/packages"
set "DAMAGE_HEALTH_URL=http://127.0.0.1:%PORT%/api/character-actions/damage-analysis"
set "BUFF_HEALTH_URL=http://127.0.0.1:%PORT%/api/authoring/buffs/options"
set "UI_COMPOSER_HEALTH_URL=http://127.0.0.1:%PORT%/api/authoring/ui-composer/capabilities"
set "COMBAT_DESIGN_HEALTH_URL=http://127.0.0.1:%PORT%/api/combat-design/query"

echo %PORT%| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [ERROR] Port must be numeric. Example: Tools\MxFramework.EditorHub\start-editor-hub.bat 4874
    exit /b 1
)

if not exist "%CLI_PROJECT%" (
    echo [ERROR] Authoring CLI project not found: %CLI_PROJECT%
    exit /b 1
)

set "PACKAGE_DIR="
if exist "%PACKAGE_RELATIVE%\" set "PACKAGE_DIR=%PACKAGE_RELATIVE%"
if "%PACKAGE_DIR%"=="" if exist "%PROJECT_ROOT%\%PACKAGE_RELATIVE%\" set "PACKAGE_DIR=%PROJECT_ROOT%\%PACKAGE_RELATIVE%"
if "%PACKAGE_DIR%"=="" if exist "%TOOLS_ROOT%\%PACKAGE_RELATIVE%\" set "PACKAGE_DIR=%TOOLS_ROOT%\%PACKAGE_RELATIVE%"

if "%PACKAGE_DIR%"=="" (
    echo [ERROR] Character package folder not found: %PACKAGE_RELATIVE%
    echo [ERROR] Searched project root: %PROJECT_ROOT%
    echo [ERROR] Searched tools root  : %TOOLS_ROOT%
    exit /b 1
)

if not exist "%PACKAGE_DIR%\manifest.json" (
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

if not exist "%TOOLS_ROOT%\Tools\MxFramework.CharacterStudio\node_modules\three\" (
    echo [WARN] CharacterStudio npm dependency three is missing. GLB preview may fall back.
    echo [WARN] Run once from the tools root: npm --prefix Tools\MxFramework.CharacterStudio install
)

set "PORT_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
    set "PORT_PID=%%P"
)

if not "%PORT_PID%"=="" (
    set "EXISTING_SERVER_STATIC_MISSING="
    set "EXISTING_SERVER_INCOMPATIBLE="
    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%HEALTH_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
    if not errorlevel 1 (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%ANIMATION_HEALTH_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
        if not errorlevel 1 (
            powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r=Invoke-WebRequest -UseBasicParsing -Uri '%DAMAGE_HEALTH_URL%' -TimeoutSec 1; $j=$r.Content | ConvertFrom-Json; if ($j.exists -and $j.actions) { exit 0 }; exit 1 } catch { exit 1 }" >nul 2>nul
            if not errorlevel 1 (
                powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%BUFF_HEALTH_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
                if not errorlevel 1 (
                    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%UI_COMPOSER_HEALTH_URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
                    if not errorlevel 1 (
                        powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r=Invoke-WebRequest -UseBasicParsing -Uri '%COMBAT_DESIGN_HEALTH_URL%' -TimeoutSec 5; $j=$r.Content | ConvertFrom-Json; if ($j.schema -eq 'mx.combat-design.query.v1') { exit 0 }; exit 1 } catch { exit 1 }" >nul 2>nul
                        if not errorlevel 1 (
                        powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing -Uri '%URL%' -TimeoutSec 1 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
                        if not errorlevel 1 (
                            echo MxFramework Authoring server is already running on port %PORT%.
                            echo URL: %URL%
                            if not "%MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER%"=="0" start "" "%URL%"
                            exit /b 0
                        )

                        echo [WARN] Port %PORT% has an Authoring server, but Editor Hub static files are not available.
                        set "EXISTING_SERVER_STATIC_MISSING=1"
                        set "EXISTING_SERVER_INCOMPATIBLE=1"
                        ) else (
                            echo [WARN] Port %PORT% has an Authoring server, but Combat Design API/schema is unavailable.
                            set "EXISTING_SERVER_INCOMPATIBLE=1"
                        )
                    ) else (
                        echo [WARN] Port %PORT% has an older Authoring server without UI Composer APIs.
                        set "EXISTING_SERVER_INCOMPATIBLE=1"
                    )
                ) else (
                    echo [WARN] Port %PORT% has an older Authoring server without Buff v2 Workbench APIs.
                    set "EXISTING_SERVER_INCOMPATIBLE=1"
                )
            ) else (
                echo [WARN] Port %PORT% has an older Authoring server without Action Damage Analysis API.
                set "EXISTING_SERVER_INCOMPATIBLE=1"
            )
        )

        if "!EXISTING_SERVER_STATIC_MISSING!"=="" if "!EXISTING_SERVER_INCOMPATIBLE!"=="" echo [WARN] Port %PORT% has an older Authoring server without Animation Editor APIs.
    ) else (
        echo [WARN] Port %PORT% is already in use, but it is not an Editor Hub-compatible Authoring server.
    )

    echo [WARN] Port %PORT% is already in use by process %PORT_PID%.
    set "ORIGINAL_PORT=%PORT%"
    set /a START_PORT=%PORT%+1
    set /a END_PORT=%PORT%+30
    set "FOUND_PORT="
    for /L %%Q in (!START_PORT!,1,!END_PORT!) do (
        if "!FOUND_PORT!"=="" (
            set "CANDIDATE_PID="
            for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%%Q .*LISTENING"') do (
                set "CANDIDATE_PID=%%P"
            )
            if "!CANDIDATE_PID!"=="" (
                set "FOUND_PORT=%%Q"
            )
        )
    )
    if "!FOUND_PORT!"=="" (
        echo [ERROR] No free Authoring server port found in range !START_PORT!-!END_PORT!.
        echo Stop the old process or pass an explicit free port.
        exit /b 1
    )
    set "PORT=!FOUND_PORT!"
    set "URL=http://127.0.0.1:!PORT!/Tools/MxFramework.EditorHub/web/"
    set "HEALTH_URL=http://127.0.0.1:!PORT!/api/character/packages"
    set "ANIMATION_HEALTH_URL=http://127.0.0.1:!PORT!/api/authoring/animation/packages"
    set "DAMAGE_HEALTH_URL=http://127.0.0.1:!PORT!/api/character-actions/damage-analysis"
    set "BUFF_HEALTH_URL=http://127.0.0.1:!PORT!/api/authoring/buffs/options"
    set "UI_COMPOSER_HEALTH_URL=http://127.0.0.1:!PORT!/api/authoring/ui-composer/capabilities"
    set "COMBAT_DESIGN_HEALTH_URL=http://127.0.0.1:!PORT!/api/combat-design/query"
    echo [WARN] Using free fallback port !PORT! instead of !ORIGINAL_PORT!.
)

echo MxFramework Editor Hub
echo Tools root  : %TOOLS_ROOT%
echo Project root: %PROJECT_ROOT%
echo Package     : %PACKAGE_RELATIVE%
echo Port        : %PORT%
echo URL         : %URL%
echo Stop        : Ctrl+C
echo.

if not "%MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER%"=="0" (
    start "Editor Hub opener" powershell -NoProfile -ExecutionPolicy Bypass -Command "$u='%URL%'; $h='%HEALTH_URL%'; $a='%ANIMATION_HEALTH_URL%'; $d='%DAMAGE_HEALTH_URL%'; $b='%BUFF_HEALTH_URL%'; $c='%UI_COMPOSER_HEALTH_URL%'; $g='%COMBAT_DESIGN_HEALTH_URL%'; for($i=0;$i -lt 30;$i++){ try { Invoke-WebRequest -UseBasicParsing -Uri $h -TimeoutSec 1 | Out-Null; Invoke-WebRequest -UseBasicParsing -Uri $a -TimeoutSec 1 | Out-Null; $r=Invoke-WebRequest -UseBasicParsing -Uri $d -TimeoutSec 1; $j=$r.Content | ConvertFrom-Json; if (-not ($j.exists -and $j.actions)) { throw 'stale action damage analysis api' }; Invoke-WebRequest -UseBasicParsing -Uri $b -TimeoutSec 1 | Out-Null; Invoke-WebRequest -UseBasicParsing -Uri $c -TimeoutSec 1 | Out-Null; $r=Invoke-WebRequest -UseBasicParsing -Uri $g -TimeoutSec 5; $j=$r.Content | ConvertFrom-Json; if ($j.schema -ne 'mx.combat-design.query.v1') { throw 'combat design api unavailable' }; Invoke-WebRequest -UseBasicParsing -Uri $u -TimeoutSec 1 | Out-Null; Start-Process $u; exit 0 } catch { Start-Sleep -Seconds 1 } }; Write-Host 'Editor Hub server did not become ready in 30 seconds. Open manually:' $u"
)

pushd "%PROJECT_ROOT%"
dotnet run --project "%CLI_PROJECT%" -- editor serve --tools-root "%TOOLS_ROOT%" --project-root "%PROJECT_ROOT%" --port %PORT% --package "%PACKAGE_RELATIVE%"
set "EXITCODE=%ERRORLEVEL%"
popd

endlocal & exit /b %EXITCODE%
