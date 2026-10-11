$ErrorActionPreference = 'Stop'
if (-not $env:MX_AUDIO_GENERATION_TOKEN) { throw 'MX_AUDIO_GENERATION_TOKEN is required.' }
if (-not $env:HF_TOKEN -and $env:MX_AUDIO_GENERATION_FAKE -ne '1') { throw 'HF_TOKEN is required for Stable Audio.' }
if ((-not $env:MX_AUDIO_MODEL_REVISION -or $env:MX_AUDIO_MODEL_REVISION -in @('main', 'unresolved')) -and $env:MX_AUDIO_GENERATION_FAKE -ne '1') { throw 'MX_AUDIO_MODEL_REVISION must be an immutable Hugging Face commit.' }
python -m uvicorn audio_generation_host:app --host 0.0.0.0 --port 10188
