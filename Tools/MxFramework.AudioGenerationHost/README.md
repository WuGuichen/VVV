# MxFramework Audio Generation Host

Authoring-only local provider for `stabilityai/stable-audio-3-small-sfx`. It is
designed for the RTX 4080 workstation and exposes port `10188`; browsers never
connect to it directly.

## Host setup

1. Accept the model license and set `HF_TOKEN` on the host.
2. Set a long random `MX_AUDIO_GENERATION_TOKEN` on both the host and the local
   Authoring server environment.
3. Pin `MX_AUDIO_MODEL_REVISION` to the immutable Hugging Face model commit.
   The host passes that revision to each gated checkpoint download; `main` is
   rejected. The deployed provider source is separately pinned to official
   `stable-audio-3` commit `8a3ded325ec6082113dbce11620b7b25b31ce6b0`.
4. Configure `MX_AUDIO_VOICE_STOP_COMMANDS` and
   `MX_AUDIO_VOICE_START_COMMANDS` as `||`-separated Windows commands. Configure
   `MX_AUDIO_VOICE_HEALTH_URLS` for IndexTTS2 and CosyVoice.
5. Install the official `stable-audio-3` package/repository and this folder's
   Python requirements, then run `start.ps1`.

Every terminal branch unloads the SFX model and restores/verifies the voice
services. Candidate files remain under the configured host temp directory.

wg200 uses FP32 inference for `small-sfx` (`MX_AUDIO_MODEL_HALF=0`) because the
RTX 4080 FP16 path produced saturated decoder output. Each layer receives its
own English prompt with `TrackType: SFX`. Raw clipping ratio, crest factor and
spectral flatness are measured before adoption; `Clipped` and `NoiseLike`
candidates are retained for diagnosis but cannot be selected or adopted.

New Workbench and public-matrix recipes use `durationMode=NaturalDecay`.
Stable Audio still renders on its stable five-second canvas, but
`maxDurationSeconds` is only an upper bound: the host detects the main onset,
follows a smoothed 10ms RMS envelope to a sustained natural decay, snaps to a
nearby zero crossing, and writes the measured event without padding it to the
window. Candidates report source/max/trimmed duration, decay time and late-tail
energy. A result that does not decay inside the window, or retains excessive
late energy, is marked `ReverbLike`; it remains available for diagnosis but
cannot be edited into or adopted as a valid source. Requests without the new
policy keep `LegacyFixed`, preserving historical Job behavior.

The host snapshots IndexTTS2 `9888` and CosyVoice `9889` before every real job.
Only services that were healthy before generation are stopped and restarted;
services that were already cold remain cold. IndexTTS2 keeps its own COC
on-demand startup ownership.

## Mac operator

The Mac-side operator provides the same cold-start shape as the COC TTS tools:

```bash
python Tools/MxFramework.AudioGenerationHost/tools/manage_wg200.py health
python Tools/MxFramework.AudioGenerationHost/tools/manage_wg200.py ensure
python Tools/MxFramework.AudioGenerationHost/tools/manage_wg200.py smoke
python Tools/MxFramework.AudioGenerationHost/tools/manage_wg200.py stop
```

`ensure` checks `10188`, validates wg200, atomically syncs the host files, creates
the `MxFramework_AudioGenerationHost` scheduled task and waits up to five
minutes for health. Audio Workbench invokes this same operator through the local
Authoring server; the browser never receives SSH or machine credentials.

Required wg200 Machine or User environment variables:

- `MX_AUDIO_GENERATION_TOKEN`
- `HF_TOKEN`
- `MX_AUDIO_MODEL_REVISION` pinned to the accepted immutable model commit
- `MX_AUDIO_MODEL_DEVICE` is `cuda` on wg200. The bootstrap installs the Windows CUDA 12.6 Torch/Torchaudio wheels into the pinned provider venv, verifies `torch.cuda.is_available()` and the RTX 4080 device name, then launches directly with that venv Python so `uv` cannot re-resolve the lock back to CPU wheels.

The operator never uploads or prints these values. The matching
`MX_AUDIO_GENERATION_TOKEN` must also be present in the local Authoring server
environment before starting a real generation job.

Remote paths and current baseline:

- Host: `E:\MxFramework\AudioGenerationHost`
- Stable Audio provider: `E:\MxFramework\stable-audio-3`
- Provider commit: `8a3ded325ec6082113dbce11620b7b25b31ce6b0`
- Terminal Job shelf: the Host restores at most 20 jobs from the last 7 days. `GET /v1/sfx/jobs?audioCueId=...` lists them and `POST /v1/sfx/jobs/{id}/extend` appends layer takes without resetting numbering.
- Logs: `E:\MxFramework\AudioGenerationHost\logs\host.out.log` and `host.err.log`

The COC and MxFramework repositories share the workstation and the deployed
voice-service launch commands, but neither repository imports source code from
the other.

## Troubleshooting

- `AUDIO_HOST_ENV_MISSING`: configure the required variable on wg200 and retry.
- `AUDIO_HOST_PROVIDER_REVISION_MISMATCH`: restore the pinned provider checkout.
- `AUDIO_HOST_TASK_FAILED`: inspect Windows Task Scheduler and the remote logs.
- `AUDIO_HOST_START_TIMEOUT`: inspect `host.err.log`; do not start a second host.
- `voiceRecoveryStatus=Failed`: generated candidates are retained, but restore
  the voice service reported by `voiceServices[]` before using COC speech.

For smoke tests only, set `MX_AUDIO_GENERATION_FAKE=1`; fake output must never be
adopted as production audio.
