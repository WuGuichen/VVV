"""Single-user Stable Audio SFX host for MxFramework Authoring.

The production model is loaded lazily and never shares GPU residency with the two
voice services. Candidate audio is deliberately kept outside the repository.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import secrets
import shutil
import subprocess
import threading
import time
import traceback
import uuid
from dataclasses import asdict
from pathlib import Path
from typing import Any

import numpy as np
from fastapi import FastAPI, Header, HTTPException
from fastapi.responses import FileResponse
from audio_generation_dsp import (
    _metrics, _postprocess, _postprocess_natural, _prepare_audio,
    _tail_relative_db, _try_auto_repair_tail, _write_pcm24,
)
from audio_generation_models import (
    FAKE, MODEL_ID, MODEL_REVISION, Candidate, CandidateEditRequest,
    CandidateRepairRequest, Job, LayerRequest, SfxExtendRequest, SfxJobRequest,
)

MODEL_DEVICE = os.getenv("MX_AUDIO_MODEL_DEVICE", "auto").strip().lower() or "auto"
ROOT = Path(os.getenv("MX_AUDIO_GENERATION_ROOT", Path(__file__).parent / "Temp")).resolve()
TOKEN = os.getenv("MX_AUDIO_GENERATION_TOKEN", "")
VOICE_HEALTH = [url.strip() for url in os.getenv(
    "MX_AUDIO_VOICE_HEALTH_URLS", "http://127.0.0.1:9888/health,http://127.0.0.1:9889/health"
).split(",") if url.strip()]
VOICE_STOP = [item for item in os.getenv("MX_AUDIO_VOICE_STOP_COMMANDS", "").split("||") if item.strip()]
VOICE_START = [item for item in os.getenv("MX_AUDIO_VOICE_START_COMMANDS", "").split("||") if item.strip()]




app = FastAPI(title="MxFramework Audio Generation Host", version="1.0")
_jobs: dict[str, Job] = {}
_requests: dict[str, SfxJobRequest] = {}
_gate = threading.Lock()
_worker_gate = threading.Lock()
_model: Any = None
_last_voice_services: list[dict[str, Any]] = []


def _utc() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def _authorize(authorization: str | None) -> None:
    if not TOKEN:
        raise HTTPException(503, "MX_AUDIO_GENERATION_TOKEN is not configured")
    value = authorization or ""
    if not value.startswith("Bearer ") or not secrets.compare_digest(value[7:], TOKEN):
        raise HTTPException(401, "invalid bearer token")


def _safe_job(job_id: str) -> Job:
    if len(job_id) > 64 or any(not (c.isalnum() or c == "-") for c in job_id):
        raise HTTPException(400, "invalid job id")
    job = _jobs.get(job_id)
    if job is None:
        raise HTTPException(404, "job not found")
    return job


def _save(job: Job) -> None:
    job.updatedUtc = _utc()
    folder = ROOT / job.jobId
    folder.mkdir(parents=True, exist_ok=True)
    temp = folder / "job.json.tmp"
    temp.write_text(json.dumps(asdict(job), ensure_ascii=False, indent=2), encoding="utf-8")
    temp.replace(folder / "job.json")


def _restore_jobs() -> None:
    """Restores the bounded terminal shelf; active jobs become failed after a host restart."""
    if not ROOT.exists():
        return
    now = time.time()
    restored: list[tuple[float, Job, Path]] = []
    for path in ROOT.glob("*/job.json"):
        try:
            age = now - path.stat().st_mtime
            if age > 7 * 24 * 3600:
                shutil.rmtree(path.parent, ignore_errors=True)
                continue
            data = json.loads(path.read_text(encoding="utf-8"))
            allowed = {item.name for item in Job.__dataclass_fields__.values()}
            job = Job(**{key: value for key, value in data.items() if key in allowed})
            if job.status in {"Queued", "Preparing", "LoadingModel", "Generating"}:
                job.status = "Failed"
                job.failureReason = "host restarted while job was active"
                job.progress = 1.0
                _save(job)
            restored.append((path.stat().st_mtime, job, path.parent))
        except Exception:
            continue
    restored.sort(key=lambda item: item[0], reverse=True)
    for _, job, _ in restored[:20]:
        _jobs[job.jobId] = job
    for _, _, folder in restored[20:]:
        shutil.rmtree(folder, ignore_errors=True)


def _run_commands(commands: list[str]) -> None:
    for command in commands:
        subprocess.run(command, shell=True, check=True, timeout=90)


def _voice_health(url: str, timeout: float = 0.6) -> bool:
    import urllib.request
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return response.status < 400
    except Exception:
        return False


def _voice_healthy() -> bool:
    return all(_voice_health(url) for url in VOICE_HEALTH)


def _wait_voice(timeout: float = 120.0) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if _voice_healthy():
            return True
        time.sleep(2)
    return False


def _wait_voice_service(url: str, timeout: float) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if _voice_health(url):
            return True
        time.sleep(2)
    return False


def _snapshot_voice_services() -> list[dict[str, Any]]:
    services: list[dict[str, Any]] = []
    for index, url in enumerate(VOICE_HEALTH):
        services.append({
            "name": "IndexTTS2" if "9888" in url else "CosyVoice" if "9889" in url else f"Voice{index + 1}",
            "url": url,
            "wasRunning": _voice_health(url),
            "stopped": False,
            "restored": False,
            "health": _voice_health(url),
        })
    return services


def _run_voice_command(commands: list[str], index: int) -> None:
    if index < len(commands) and commands[index].strip():
        _run_commands([commands[index]])


def _load_model() -> Any:
    global _model
    if _model is not None or FAKE:
        return _model
    if not os.getenv("HF_TOKEN"):
        raise RuntimeError("HF_TOKEN is required for the gated Stable Audio model")
    if MODEL_REVISION in {"", "main", "unresolved"}:
        raise RuntimeError("MX_AUDIO_MODEL_REVISION must be an immutable Hugging Face commit")
    from huggingface_hub import hf_hub_download
    from stable_audio_3 import StableAudioModel
    from stable_audio_3 import model_configs

    class PinnedSmallSfx:
        repo_id = MODEL_ID
        config_path = "model_config.json"
        ckpt_path = "model.safetensors"

        @staticmethod
        def resolve() -> tuple[str, str]:
            return (
                hf_hub_download(repo_id=MODEL_ID, filename="model_config.json", revision=MODEL_REVISION),
                hf_hub_download(repo_id=MODEL_ID, filename="model.safetensors", revision=MODEL_REVISION),
            )

    pinned = PinnedSmallSfx()
    model_configs.models["small-sfx"] = pinned
    model_configs.all_models["small-sfx"] = pinned
    model_half = os.getenv("MX_AUDIO_MODEL_HALF", "0") == "1"
    device = None if MODEL_DEVICE == "auto" else MODEL_DEVICE
    _model = StableAudioModel.from_pretrained("small-sfx", device=device, model_half=model_half)
    return _model


def _unload_model() -> None:
    global _model
    _model = None
    try:
        import gc
        import torch
        gc.collect()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
    except ImportError:
        pass


def _fake_audio(prompt: str, seed: int, duration: float) -> np.ndarray:
    rng = np.random.default_rng(seed)
    frames = max(4800, int(duration * 48_000))
    t = np.arange(frames, dtype=np.float32) / 48_000.0
    envelope = np.exp(-t * (4.0 + seed % 5))
    tone = np.sin(2 * math.pi * (90 + seed % 600) * t)
    noise = rng.normal(0.0, 0.18, frames).astype(np.float32)
    return ((tone * 0.25 + noise) * envelope).reshape(1, -1)


def _generate(prompt: str, negative: str, seed: int, duration: float) -> tuple[np.ndarray, int]:
    if FAKE:
        return _fake_audio(prompt, seed, duration), 48_000
    model = _load_model()
    # Small-SFX becomes numerically unstable on very short diffusion canvases.
    # Generate on the provider's stable minimum canvas, then extract the requested
    # game-ready window around the strongest acoustic event in _postprocess.
    inference_duration = max(5.0, duration)
    result = model.generate(prompt=prompt, duration=inference_duration, seed=seed, negative_prompt=negative)
    if hasattr(result, "detach"):
        tensor = result.detach().cpu()
        is_integer = not bool(getattr(tensor, "is_floating_point", lambda: True)())
        result = tensor.numpy()
    else:
        is_integer = np.issubdtype(np.asarray(result).dtype, np.integer)
    source = np.asarray(result)
    if is_integer:
        limit = float(max(abs(np.iinfo(source.dtype).min), np.iinfo(source.dtype).max))
        audio = source.astype(np.float32) / limit
    else:
        audio = source.astype(np.float32)
        # stable-audio-3 currently returns an int16-domain tensor cast to float.
        # Preserve genuine normalized float output, but scale PCM-domain values.
        if audio.size and float(np.max(np.abs(audio))) > 2.0:
            audio /= 32768.0
    while audio.ndim > 2:
        audio = audio[0]
    if audio.ndim == 1:
        audio = audio.reshape(1, -1)
    return audio, 44_100



def _render_candidate_edit(job: Job, candidate: dict[str, Any], edit: CandidateEditRequest) -> tuple[Candidate, Path]:
    if candidate.get("status") != "Ready":
        raise HTTPException(409, "only Ready candidates can be edited")
    if candidate.get("sha256") != edit.sourceHash:
        raise HTTPException(409, "candidate source hash is stale")
    source_path = (ROOT / job.jobId / candidate["fileName"]).resolve()
    if ROOT not in source_path.parents or not source_path.is_file():
        raise HTTPException(404, "candidate file not found")
    import soundfile as sf
    samples, sample_rate = sf.read(source_path, dtype="float32", always_2d=True)
    audio = samples.T
    duration_ms = audio.shape[1] * 1000.0 / sample_rate
    if edit.outMs > duration_ms + 0.5 or edit.outMs - edit.inMs < 30:
        raise HTTPException(400, "edit range must be within the source and at least 30ms")
    clip_ms = edit.outMs - edit.inMs
    if edit.fadeInMs > clip_ms / 2 or edit.fadeOutMs > clip_ms / 2:
        raise HTTPException(400, "fade cannot exceed half of the edited duration")
    start = max(0, min(audio.shape[1] - 1, round(edit.inMs * sample_rate / 1000)))
    end = max(start + 1, min(audio.shape[1], round(edit.outMs * sample_rate / 1000)))
    mono = np.mean(audio, axis=0)
    radius = max(1, round(0.005 * sample_rate))
    def snap(index: int) -> int:
        low, high = max(1, index - radius), min(audio.shape[1] - 1, index + radius)
        crossings = np.flatnonzero(np.signbit(mono[low - 1:high - 1]) != np.signbit(mono[low:high]))
        return int(low + crossings[np.argmin(np.abs((low + crossings) - index))]) if crossings.size else index
    start, end = snap(start), snap(end)
    if end - start < round(0.03 * sample_rate):
        raise HTTPException(400, "zero-crossing snap produced a clip shorter than 30ms")
    edited = audio[:, start:end].copy()
    for fade_ms, leading in ((edit.fadeInMs, True), (edit.fadeOutMs, False)):
        count = min(edited.shape[1] // 2, round(fade_ms * sample_rate / 1000))
        if count:
            ramp = np.linspace(0, 1, count, dtype=np.float32)
            if leading: edited[:, :count] *= ramp
            else: edited[:, -count:] *= ramp[::-1]
    peak = float(np.max(np.abs(edited))) if edited.size else 0
    ceiling = 10 ** (-3 / 20)
    if peak > ceiling: edited *= ceiling / peak
    edit_key = hashlib.sha256(json.dumps(edit.model_dump(), sort_keys=True).encode()).hexdigest()[:20]
    output = ROOT / job.jobId / "edited" / f"{candidate['candidateId']}-{edit_key}.wav"
    output.parent.mkdir(parents=True, exist_ok=True)
    _write_pcm24(output, edited)
    rendered = _metrics(output, edited, edited, candidate["layer"], candidate["take"], candidate["sha256"], job.jobId)
    rendered.candidateId = edit_key
    rendered.fileName = str(output.relative_to(ROOT / job.jobId)).replace("\\", "/")
    rendered.url = f"/v1/sfx/jobs/{job.jobId}/rendered/{edit_key}"
    if rendered.rawClippingRatio > 0.01: rendered.status = "Clipped"
    elif rendered.spectralFlatness > 0.55 and rendered.crestFactorDb < 6.0: rendered.status = "NoiseLike"
    elif rendered.peakDbfs < -55 or rendered.rmsDbfs < -65: rendered.status = "Silent"
    elif rendered.durationSeconds < 0.03: rendered.status = "AbnormalDuration"
    return rendered, output


def _repair_soft_candidate(job: Job, candidate: dict[str, Any], request: CandidateRepairRequest) -> Candidate:
    """Applies the least destructive tail fade that passes the soft quality gate."""
    if candidate.get("status") == "Ready":
        return Candidate(**{key: value for key, value in candidate.items() if key in Candidate.__dataclass_fields__})
    if candidate.get("status") != "ReverbLike":
        raise HTTPException(409, "only ReverbLike candidates can be auto-repaired")
    if candidate.get("sha256") != request.sourceHash:
        raise HTTPException(409, "candidate source hash is stale")
    source_path = (ROOT / job.jobId / candidate["fileName"]).resolve()
    if ROOT not in source_path.parents or not source_path.is_file():
        raise HTTPException(404, "candidate file not found")
    import soundfile as sf
    samples, sample_rate = sf.read(source_path, dtype="float32", always_2d=True)
    audio = _prepare_audio(samples.T, sample_rate, int(candidate.get("channels") or 1))
    duration_ms = audio.shape[1] * 1000.0 / 48_000
    if duration_ms < 30.0:
        raise HTTPException(409, "candidate is too short to repair safely")
    original_late_db = float(candidate.get("lateTailRelativeDb", _tail_relative_db(audio)))
    severity = max(0.0, min(1.0, (original_late_db + 30.0) / 18.0))
    minimum_fade_ms = min(duration_ms * 0.35, 35.0 + severity * 45.0)
    maximum_fade_ms = min(120.0, duration_ms * 0.65)
    repaired_audio: np.ndarray | None = None
    repaired_late_db = original_late_db
    applied_fade_ms = 0.0
    for fade_ms in np.linspace(minimum_fade_ms, maximum_fade_ms, 7):
        fade_frames = min(audio.shape[1], max(1, round(float(fade_ms) * 48_000 / 1000.0)))
        trial = audio.copy()
        curve = np.cos(np.linspace(0.0, math.pi / 2.0, fade_frames, dtype=np.float32)) ** 3
        trial[:, -fade_frames:] *= curve
        late_db = _tail_relative_db(trial)
        if late_db <= -30.0:
            repaired_audio, repaired_late_db, applied_fade_ms = trial, late_db, float(fade_ms)
            break
    if repaired_audio is None:
        raise HTTPException(422, "automatic tail repair could not reach the quality threshold")
    evidence = {
        "sourceDurationSeconds": float(candidate.get("sourceDurationSeconds", duration_ms / 1000.0)),
        "maxDurationSeconds": float(candidate.get("maxDurationSeconds", duration_ms / 1000.0)),
        "trimmedDurationSeconds": round(duration_ms / 1000.0, 5),
        "decayTimeMs": float(candidate.get("decayTimeMs", 0.0)),
        "lateTailRelativeDb": round(repaired_late_db, 2),
        "decayReached": True,
        "autoTailFadeApplied": True,
        "autoTailFadeMs": round(applied_fade_ms, 2),
        "originalLateTailRelativeDb": round(original_late_db, 2),
    }
    repair_key = hashlib.sha256(f"{request.sourceHash}:{applied_fade_ms:.3f}".encode()).hexdigest()[:20]
    output = ROOT / job.jobId / "repaired" / f"{candidate['candidateId']}-{repair_key}.wav"
    output.parent.mkdir(parents=True, exist_ok=True)
    _write_pcm24(output, repaired_audio)
    rendered = _metrics(
        output, repaired_audio, repaired_audio, candidate["layer"], int(candidate["take"]),
        str(candidate.get("rawSha256") or request.sourceHash), job.jobId, evidence,
    )
    rendered.candidateId = candidate["candidateId"]
    rendered.fileName = str(output.relative_to(ROOT / job.jobId)).replace("\\", "/")
    rendered.url = f"/v1/sfx/jobs/{job.jobId}/candidates/{rendered.candidateId}"
    if rendered.rawClippingRatio > 0.01:
        rendered.status = "Clipped"
    elif rendered.spectralFlatness > 0.55 and rendered.crestFactorDb < 6.0:
        rendered.status = "NoiseLike"
    elif rendered.peakDbfs < -55 or rendered.rmsDbfs < -65:
        rendered.status = "Silent"
    elif rendered.durationSeconds < 0.03:
        rendered.status = "AbnormalDuration"
    if rendered.status != "Ready":
        output.unlink(missing_ok=True)
        raise HTTPException(422, "automatic tail repair introduced a hard quality failure")
    index = job.candidates.index(candidate)
    job.candidates[index] = asdict(rendered)
    if any(item.get("status") == "Ready" for item in job.candidates):
        job.qualityWarning = ""
    _save(job)
    return rendered




def _duration_policy_hash(request: SfxJobRequest) -> str:
    payload = {
        "durationMode": request.durationMode,
        "durationCharacter": request.durationCharacter,
        "decayProfile": request.decayProfile,
        "maxDurationSeconds": request.maxDurationSeconds,
        "durationSeconds": request.durationSeconds,
    }
    return hashlib.sha256(json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def _execute(job: Job, request: SfxJobRequest) -> None:
    global _last_voice_services
    with _worker_gate:
        voice_services: list[dict[str, Any]] = []
        try:
            job.status, job.gpuState, job.progress = "Preparing", "StopVoice", 0.02
            _save(job)
            if VOICE_STOP and not FAKE:
                voice_services = _snapshot_voice_services()
                job.voiceServices = voice_services
                _last_voice_services = voice_services
                for index, service in enumerate(voice_services):
                    if not service["wasRunning"]:
                        continue
                    _run_voice_command(VOICE_STOP, index)
                    service["stopped"] = True
                    service["health"] = False
                    _save(job)
            job.gpuState, job.status, job.progress = "LoadSfx", "LoadingModel", 0.08
            _save(job)
            _load_model()
            if not FAKE:
                job.computeDevice = str(getattr(_model, "device", MODEL_DEVICE))
                try:
                    import torch
                    if torch.cuda.is_available():
                        torch.cuda.reset_peak_memory_stats()
                except ImportError:
                    pass
                _save(job)
            total = sum(layer.takes for layer in request.layers)
            completed = 0
            seen_hashes: set[str] = {item.get("sha256", "") for item in job.candidates}
            batch = {
                "batchId": uuid.uuid4().hex,
                "createdUtc": _utc(),
                "seed": request.seed,
                "layerCounts": {layer.name: layer.takes for layer in request.layers},
                "durationMode": request.durationMode,
                "durationCharacter": request.durationCharacter,
                "decayProfile": request.decayProfile,
                "maxDurationSeconds": request.maxDurationSeconds,
                "layerMaxDurations": {layer.name: layer.maxDurationSeconds for layer in request.layers},
            }
            job.batches.append(batch)
            deadline = time.monotonic() + float(os.getenv("MX_AUDIO_JOB_TIMEOUT_SECONDS", "900"))
            for layer in request.layers:
                first_take = 1 + max((int(item.get("take", 0)) for item in job.candidates if item.get("layer", "").lower() == layer.name.lower()), default=0)
                for take in range(first_take, first_take + layer.takes):
                    if time.monotonic() >= deadline:
                        raise TimeoutError("SFX generation job timed out")
                    if job.cancelled:
                        raise InterruptedError("cancelled")
                    candidate_seed = request.seed + completed
                    layer_prompt = layer.prompt.strip() or f"{request.prompt}. Layer: {layer.name}."
                    raw_audio, sample_rate = _generate(
                        f"TrackType: SFX. {layer_prompt}", request.negativePrompt,
                        candidate_seed, request.durationSeconds,
                    )
                    folder = ROOT / job.jobId
                    raw_folder = folder / "raw"
                    raw_folder.mkdir(parents=True, exist_ok=True)
                    raw_path = raw_folder / f"{layer.name}-{take:02d}.wav"
                    import soundfile as sf
                    sf.write(raw_path, raw_audio.T, sample_rate, subtype="FLOAT")
                    raw_sha256 = hashlib.sha256(raw_path.read_bytes()).hexdigest()
                    duration_analysis: dict[str, Any] | None = None
                    if request.durationMode == "NaturalDecay":
                        maximum = layer.maxDurationSeconds or request.maxDurationSeconds or request.durationSeconds
                        audio, duration_analysis = _postprocess_natural(
                            raw_audio, sample_rate, request.channels, maximum, request.decayProfile,
                        )
                        audio, duration_analysis = _try_auto_repair_tail(
                            audio, request.channels, maximum, request.decayProfile, duration_analysis,
                        )
                    else:
                        audio = _postprocess(raw_audio, sample_rate, request.channels, request.durationSeconds)
                    if time.monotonic() >= deadline:
                        raise TimeoutError("SFX generation job timed out")
                    path = folder / f"{layer.name}-{take:02d}.wav"
                    _write_pcm24(path, audio)
                    candidate = _metrics(path, audio, raw_audio, layer.name, take, raw_sha256, job.jobId, duration_analysis)
                    if candidate.sha256 in seen_hashes:
                        candidate.status = "Duplicate"
                    elif candidate.rawClippingRatio > 0.01:
                        candidate.status = "Clipped"
                    elif candidate.spectralFlatness > 0.55 and candidate.crestFactorDb < 6.0:
                        candidate.status = "NoiseLike"
                    elif candidate.peakDbfs < -55 or candidate.rmsDbfs < -65:
                        candidate.status = "Silent"
                    elif duration_analysis and duration_analysis.get("reverbLike"):
                        candidate.status = "ReverbLike"
                    elif request.durationMode == "NaturalDecay" and candidate.durationSeconds < 0.03:
                        candidate.status = "AbnormalDuration"
                    elif request.durationMode != "NaturalDecay" and (candidate.durationSeconds < min(request.durationSeconds * 0.35, 0.08) or candidate.durationSeconds > request.durationSeconds * 1.15):
                        candidate.status = "AbnormalDuration"
                    seen_hashes.add(candidate.sha256)
                    job.candidates.append(asdict(candidate))
                    completed += 1
                    job.status, job.gpuState = "Generating", "Generate"
                    job.progress = 0.1 + 0.82 * completed / total
                    _save(job)
            ready_count = sum(1 for candidate in job.candidates if candidate.get("status") == "Ready")
            reviewable_count = sum(1 for candidate in job.candidates if candidate.get("status") == "ReverbLike")
            if ready_count > 0:
                job.status = "Succeeded"
            elif reviewable_count > 0:
                job.status = "Succeeded"
                job.qualityWarning = "AUDIO_GENERATION_REVIEW_REQUIRED: generated candidates require trimming or regeneration"
            else:
                job.status = "Failed"
                job.failureReason = "AUDIO_GENERATION_NO_READY_CANDIDATES: all candidates failed technical quality gates"
            job.progress = 0.96
        except InterruptedError:
            job.status, job.failureReason = "Cancelled", "cancelled by user"
        except Exception as exception:
            traceback.print_exc()
            job.status, job.failureReason = "Failed", str(exception)
        finally:
            job.gpuState = "UnloadSfx"
            if not FAKE:
                try:
                    import torch
                    if torch.cuda.is_available():
                        job.gpuPeakMemoryMiB = round(torch.cuda.max_memory_allocated() / (1024 * 1024), 1)
                except Exception:
                    pass
            _unload_model()
            try:
                restore = [service for service in voice_services if service["wasRunning"]]
                if restore and VOICE_START:
                    job.gpuState = "RestartVoice"
                    for index, service in enumerate(voice_services):
                        if not service["wasRunning"]:
                            continue
                        _run_voice_command(VOICE_START, index)
                        timeout = 240.0 if service["name"] == "IndexTTS2" else 120.0
                        service["restored"] = _wait_voice_service(service["url"], timeout)
                        service["health"] = service["restored"]
                        if not service["restored"]:
                            raise RuntimeError(f"{service['name']} did not recover")
                job.voiceRecoveryStatus = "Ready" if restore else "NotRequired"
            except Exception as recovery_error:
                job.voiceRecoveryStatus = "Failed"
                suffix = f"voice recovery failed: {recovery_error}"
                job.failureReason = f"{job.failureReason}; {suffix}".strip("; ")
                job.status = "Failed"
            job.gpuState = "VoiceReady"
            job.progress = 1.0
            job.voiceServices = voice_services
            _last_voice_services = voice_services
            _save(job)


@app.get("/health")
def health() -> dict[str, Any]:
    active = next((job.jobId for job in _jobs.values() if job.status in {"Queued", "Preparing", "LoadingModel", "Generating"}), "")
    cuda_available = False
    cuda_device = ""
    try:
        import torch
        cuda_available = bool(torch.cuda.is_available())
        cuda_device = torch.cuda.get_device_name(0) if cuda_available else ""
    except Exception:
        pass
    return {
        "status": "ok", "provider": "Stable Audio 3 Small SFX", "model": MODEL_ID,
        "revision": MODEL_REVISION, "fake": FAKE, "activeJobId": active,
        "device": MODEL_DEVICE,
        "cudaAvailable": cuda_available, "cudaDevice": cuda_device,
        "voiceReady": _voice_healthy(), "voiceServices": [
            {**service, "health": _voice_health(service["url"])} for service in _last_voice_services
        ] if _last_voice_services else [
            {"name": "IndexTTS2" if "9888" in url else "CosyVoice" if "9889" in url else "Voice", "url": url,
             "wasRunning": False, "stopped": False, "restored": False, "health": _voice_health(url)} for url in VOICE_HEALTH
        ], "gpuPolicy": "on-demand-exclusive",
    }


@app.post("/v1/sfx/jobs", status_code=202)
def create_job(request: SfxJobRequest, authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    with _gate:
        if any(job.status in {"Queued", "Preparing", "LoadingModel", "Generating"} for job in _jobs.values()):
            raise HTTPException(409, "another SFX job is active")
        job_id = uuid.uuid4().hex
        job = Job(jobId=job_id, seed=request.seed, audioCueId=request.audioCueId,
                  eventDefinitionId=request.eventDefinitionId, recipeRevision=request.recipeRevision,
                  recipeHash=request.recipeHash, durationPolicyHash=_duration_policy_hash(request),
                  createdUtc=_utc(), updatedUtc=_utc())
        _jobs[job_id] = job
        _requests[job_id] = request
        _save(job)
        threading.Thread(target=_execute, args=(job, request), daemon=True).start()
        return asdict(job)


@app.get("/v1/sfx/jobs")
def list_jobs(audioCueId: str = "", authorization: str | None = Header(default=None)) -> list[dict[str, Any]]:
    _authorize(authorization)
    jobs = [job for job in _jobs.values() if not audioCueId or job.audioCueId == audioCueId]
    jobs.sort(key=lambda item: item.updatedUtc, reverse=True)
    return [asdict(job) for job in jobs[:20]]


@app.post("/v1/sfx/jobs/{job_id}/extend", status_code=202)
def extend_job(job_id: str, request: SfxExtendRequest, authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    with _gate:
        job = _safe_job(job_id)
        if any(item.status in {"Queued", "Preparing", "LoadingModel", "Generating"} for item in _jobs.values()):
            raise HTTPException(409, "another SFX job is active")
        if job.status not in {"Succeeded", "Failed", "Cancelled"}:
            raise HTTPException(409, "job cannot be extended in its current state")
        if job.recipeHash and request.recipeHash != job.recipeHash:
            raise HTTPException(409, "recipe hash is stale")
        if job.recipeRevision and request.recipeRevision != job.recipeRevision:
            raise HTTPException(409, "recipe revision is stale")
        if job.durationPolicyHash and _duration_policy_hash(request) != job.durationPolicyHash:
            raise HTTPException(409, "duration policy is stale")
        existing_by_layer: dict[str, int] = {}
        for candidate in job.candidates:
            layer = str(candidate.get("layer", "")).lower()
            existing_by_layer[layer] = existing_by_layer.get(layer, 0) + 1
        for layer in request.layers:
            if existing_by_layer.get(layer.name.lower(), 0) + layer.takes > 12:
                raise HTTPException(400, f"layer {layer.name} cannot exceed 12 candidates")
        if len(job.candidates) + sum(layer.takes for layer in request.layers) > 36:
            raise HTTPException(400, "generation job cannot exceed 36 candidates")
        job.status, job.progress, job.failureReason, job.cancelled = "Queued", 0.0, "", False
        _requests[job_id] = request
        _save(job)
        threading.Thread(target=_execute, args=(job, request), daemon=True).start()
        return asdict(job)


@app.get("/v1/sfx/jobs/{job_id}")
def get_job(job_id: str, authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    return asdict(_safe_job(job_id))


@app.get("/v1/sfx/jobs/{job_id}/candidates/{candidate_id}")
def get_candidate(job_id: str, candidate_id: str, authorization: str | None = Header(default=None)) -> FileResponse:
    _authorize(authorization)
    job = _safe_job(job_id)
    candidate = next((item for item in job.candidates if item["candidateId"] == candidate_id), None)
    if candidate is None:
        raise HTTPException(404, "candidate not found")
    path = (ROOT / job.jobId / candidate["fileName"]).resolve()
    if ROOT not in path.parents or not path.is_file():
        raise HTTPException(404, "candidate file not found")
    return FileResponse(path, media_type="audio/wav", filename=path.name)


@app.post("/v1/sfx/jobs/{job_id}/candidates/{candidate_id}/render")
def render_candidate(job_id: str, candidate_id: str, edit: CandidateEditRequest,
                     authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    job = _safe_job(job_id)
    if edit.candidateId != candidate_id:
        raise HTTPException(400, "candidate id mismatch")
    candidate = next((item for item in job.candidates if item["candidateId"] == candidate_id), None)
    if candidate is None:
        raise HTTPException(404, "candidate not found")
    rendered, _ = _render_candidate_edit(job, candidate, edit)
    return asdict(rendered)


@app.post("/v1/sfx/jobs/{job_id}/candidates/{candidate_id}/repair")
def repair_candidate(job_id: str, candidate_id: str, repair: CandidateRepairRequest,
                     authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    job = _safe_job(job_id)
    if repair.candidateId != candidate_id:
        raise HTTPException(400, "candidate id mismatch")
    candidate = next((item for item in job.candidates if item["candidateId"] == candidate_id), None)
    if candidate is None:
        raise HTTPException(404, "candidate not found")
    return asdict(_repair_soft_candidate(job, candidate, repair))


@app.get("/v1/sfx/jobs/{job_id}/rendered/{render_id}")
def get_rendered_candidate(job_id: str, render_id: str,
                           authorization: str | None = Header(default=None)) -> FileResponse:
    _authorize(authorization)
    job = _safe_job(job_id)
    if len(render_id) != 20 or any(char not in "0123456789abcdef" for char in render_id):
        raise HTTPException(400, "invalid render id")
    matches = list((ROOT / job.jobId / "edited").glob(f"*-{render_id}.wav"))
    if len(matches) != 1:
        raise HTTPException(404, "rendered candidate not found")
    return FileResponse(matches[0], media_type="audio/wav", filename=matches[0].name)


@app.post("/v1/sfx/jobs/{job_id}/cancel", status_code=202)
def cancel_job(job_id: str, authorization: str | None = Header(default=None)) -> dict[str, Any]:
    _authorize(authorization)
    job = _safe_job(job_id)
    job.cancelled = True
    _save(job)
    return asdict(job)


def reset_for_tests() -> None:
    _jobs.clear()
    _requests.clear()
    if ROOT.exists():
        shutil.rmtree(ROOT)


_restore_jobs()
