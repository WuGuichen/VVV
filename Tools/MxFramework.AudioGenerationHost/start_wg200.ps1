$ErrorActionPreference = 'Stop'
foreach ($name in @('MX_AUDIO_GENERATION_TOKEN', 'HF_TOKEN', 'MX_AUDIO_MODEL_REVISION')) {
    if (-not (Get-Item "Env:$name" -ErrorAction SilentlyContinue)) {
        $value = [Environment]::GetEnvironmentVariable($name, 'Machine')
        if (-not $value) { $value = [Environment]::GetEnvironmentVariable($name, 'User') }
        if ($value) { Set-Item "Env:$name" $value }
    }
}
if (-not $env:MX_AUDIO_GENERATION_TOKEN) { throw 'MX_AUDIO_GENERATION_TOKEN is required.' }
if (-not $env:HF_TOKEN -and $env:MX_AUDIO_GENERATION_FAKE -ne '1') { throw 'HF_TOKEN is required for Stable Audio.' }
if ((-not $env:MX_AUDIO_MODEL_REVISION -or $env:MX_AUDIO_MODEL_REVISION -in @('main', 'unresolved')) -and $env:MX_AUDIO_GENERATION_FAKE -ne '1') { throw 'MX_AUDIO_MODEL_REVISION must be the accepted model commit, not a branch.' }
$env:MX_AUDIO_VOICE_HEALTH_URLS = 'http://127.0.0.1:9888/health,http://127.0.0.1:9889/health'
$env:MX_AUDIO_MODEL_HALF = '1'
$env:MX_AUDIO_MODEL_DEVICE = 'cuda'
$env:MX_AUDIO_VOICE_STOP_COMMANDS = @'
powershell -NoProfile -Command "$c=Get-NetTCPConnection -LocalPort 9888 -State Listen -ErrorAction SilentlyContinue; if ($c) { Stop-Process -Id $c.OwningProcess -Force }"
||powershell -NoProfile -Command "$c=Get-NetTCPConnection -LocalPort 9889 -State Listen -ErrorAction SilentlyContinue; if ($c) { Stop-Process -Id $c.OwningProcess -Force }"
'@.Replace("`r", '').Replace("`n", '')
$env:MX_AUDIO_VOICE_START_COMMANDS = @'
powershell -NoProfile -Command "Start-Process -FilePath 'C:\Users\WuGC\AppData\Roaming\uv\python\cpython-3.10-windows-x86_64-none\python.exe' -ArgumentList 'E:\COC-TTS\indextts\coc_tts_server.py --host 0.0.0.0 --port 9888 --root E:\COC-TTS\indextts --repo E:\COC-TTS\indextts\index-tts' -WorkingDirectory 'E:\COC-TTS\indextts' -WindowStyle Hidden"
||powershell -NoProfile -Command "Start-Process -FilePath 'C:\Users\WuGC\AppData\Local\Programs\Python\Python311\python.exe' -ArgumentList 'E:\COC-TTS\cosyvoice\coc_cosyvoice_server.py --host 0.0.0.0 --port 9889 --root E:\COC-TTS\cosyvoice --repo E:\COC-TTS\cosyvoice\CosyVoice --model-dir E:\COC-TTS\cosyvoice\CosyVoice\pretrained_models\Fun-CosyVoice3-0.5B --prompt-wav E:\COC-TTS\cosyvoice\CosyVoice\asset\zero_shot_prompt.wav' -WorkingDirectory 'E:\COC-TTS\cosyvoice' -WindowStyle Hidden"
'@.Replace("`r", '').Replace("`n", '')

Set-Location $PSScriptRoot
$uvCommand = Get-Command uv -ErrorAction SilentlyContinue
$uv = if ($uvCommand) { $uvCommand.Source } else { 'C:\Users\WuGC\AppData\Roaming\Python\Scripts\uv.exe' }
if (-not (Test-Path $uv)) {
    $uv = 'C:\Users\WuGC\.local\bin\uv.exe'
}
if (-not (Test-Path $uv)) { throw 'uv.exe is not available to the scheduled task.' }
$logs = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$logPath = Join-Path $logs 'host.out.log'
$errorPath = Join-Path $logs 'host.err.log'
$providerRoot = if ($env:MX_AUDIO_PROVIDER_ROOT) { $env:MX_AUDIO_PROVIDER_ROOT } else { 'E:\MxFramework\stable-audio-3' }
if ($env:MX_AUDIO_GENERATION_FAKE -eq '1') {
    $ErrorActionPreference = 'Continue'
    & $uv run --python 3.12 --with-requirements requirements.txt python -m uvicorn audio_generation_host:app --host 0.0.0.0 --port 10188 1>> $logPath 2>> $errorPath
    if ($LASTEXITCODE -ne 0) { throw "Audio Generation Host exited with code $LASTEXITCODE." }
} else {
    if (-not (Test-Path (Join-Path $providerRoot 'pyproject.toml'))) { throw "Pinned stable-audio-3 provider is missing: $providerRoot" }
    $providerVenv = Join-Path $providerRoot '.venv'
    if ((Test-Path $providerVenv) -and -not (Test-Path (Join-Path $providerVenv 'Scripts\python.exe'))) {
        Remove-Item -Recurse -Force $providerVenv
    }
    $env:PYTHONPATH = $providerRoot
    $providerPython = Join-Path $providerVenv 'Scripts\python.exe'
    if (-not (Test-Path $providerPython)) { throw "Provider Python is missing: $providerPython" }
    & $providerPython -c "import torch; assert torch.cuda.is_available(), 'CUDA PyTorch runtime is unavailable'; assert '4080' in torch.cuda.get_device_name(0), torch.cuda.get_device_name(0)"
    if ($LASTEXITCODE -ne 0) { throw 'CUDA validation failed before Host startup.' }
    $ErrorActionPreference = 'Continue'
    & $providerPython -m uvicorn audio_generation_host:app --host 0.0.0.0 --port 10188 1>> $logPath 2>> $errorPath
    if ($LASTEXITCODE -ne 0) { throw "Audio Generation Host exited with code $LASTEXITCODE." }
}
