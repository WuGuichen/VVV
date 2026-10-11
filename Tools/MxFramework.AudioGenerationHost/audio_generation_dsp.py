"""Deterministic authoring-only DSP, repair, and analysis helpers."""

from __future__ import annotations

import hashlib
import math
import wave
from pathlib import Path
from typing import Any

import numpy as np

from audio_generation_models import Candidate

def _resample(audio: np.ndarray, old_rate: int, new_rate: int = 48_000) -> np.ndarray:
    if old_rate == new_rate:
        return audio
    old_x = np.linspace(0.0, 1.0, audio.shape[1], endpoint=False)
    new_count = max(1, round(audio.shape[1] * new_rate / old_rate))
    new_x = np.linspace(0.0, 1.0, new_count, endpoint=False)
    return np.stack([np.interp(new_x, old_x, channel) for channel in audio]).astype(np.float32)


def _prepare_audio(audio: np.ndarray, source_rate: int, channels: int) -> np.ndarray:
    audio = _resample(audio, source_rate)
    if channels == 1 and audio.shape[0] > 1:
        audio = np.mean(audio, axis=0, keepdims=True)
    elif channels == 2 and audio.shape[0] == 1:
        audio = np.repeat(audio, 2, axis=0)
    audio = audio[:channels]
    audio -= np.mean(audio, axis=1, keepdims=True)
    return audio


def _finish_audio(audio: np.ndarray) -> np.ndarray:
    if not audio.size:
        return audio
    fade = min(int(0.003 * 48_000), audio.shape[1] // 2)
    if fade:
        ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)
        audio[:, :fade] *= ramp
        audio[:, -fade:] *= ramp[::-1]
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    ceiling = 10 ** (-3 / 20)
    if peak > ceiling:
        audio *= ceiling / peak
    return np.clip(audio, -1.0, 1.0)


def _postprocess(audio: np.ndarray, source_rate: int, channels: int, target_duration: float | None = None) -> np.ndarray:
    """Legacy fixed-window processing retained for old jobs and recipes."""
    audio = _prepare_audio(audio, source_rate, channels)
    if target_duration and target_duration > 0:
        target_frames = max(1, int(target_duration * 48_000))
        if audio.shape[1] > target_frames:
            energy = np.mean(audio * audio, axis=0)
            peak_frame = int(np.argmax(energy))
            pre_roll = min(int(0.05 * 48_000), target_frames // 4)
            start = max(0, min(audio.shape[1] - target_frames, peak_frame - pre_roll))
            audio = audio[:, start:start + target_frames]
    magnitude = np.max(np.abs(audio), axis=0)
    active = np.flatnonzero(magnitude > 10 ** (-60 / 20))
    if active.size:
        pad = int(0.012 * 48_000)
        audio = audio[:, max(0, active[0] - pad):min(audio.shape[1], active[-1] + pad + 1)]
    return _finish_audio(audio)


_DECAY_PROFILE_DB = {
    "Short": (-32.0, -30.0),
    "Natural": (-34.0, -31.0),
    "Brittle": (-32.0, -29.0),
    "Granular": (-31.0, -28.0),
    "Resonant": (-28.0, -26.0),
    "Magic": (-28.0, -26.0),
    "Sweep": (-30.0, -28.0),
}


def _snap_zero_crossing(mono: np.ndarray, index: int, radius: int = 240) -> int:
    if mono.size < 2:
        return max(0, min(int(index), mono.size))
    index = max(1, min(int(index), mono.size - 1))
    low, high = max(1, index - radius), min(mono.size - 1, index + radius)
    crossings = np.flatnonzero(np.signbit(mono[low - 1:high - 1]) != np.signbit(mono[low:high]))
    if not crossings.size:
        return index
    absolute = low + crossings
    return int(absolute[np.argmin(np.abs(absolute - index))])


def _postprocess_natural(
    audio: np.ndarray,
    source_rate: int,
    channels: int,
    max_duration: float,
    decay_profile: str = "Natural",
) -> tuple[np.ndarray, dict[str, Any]]:
    """Extracts one event and stops at its measured natural decay without filling the window."""
    audio = _prepare_audio(audio, source_rate, channels)
    source_duration = audio.shape[1] / 48_000 if audio.size else 0.0
    mono = np.mean(audio, axis=0) if audio.size else np.zeros(0, dtype=np.float32)
    block = 480  # 10ms at 48kHz
    block_count = max(1, math.ceil(mono.size / block))
    rms = np.zeros(block_count, dtype=np.float64)
    for index in range(block_count):
        segment = mono[index * block:min(mono.size, (index + 1) * block)]
        rms[index] = math.sqrt(float(np.mean(segment * segment))) if segment.size else 0.0
    # 30ms smoothing, centered, keeps isolated sample spikes from becoming the event boundary.
    smoothed = np.convolve(rms, np.ones(3, dtype=np.float64) / 3.0, mode="same")
    peak_block = int(np.argmax(smoothed))
    peak_rms = max(float(smoothed[peak_block]), 1e-12)
    onset_threshold = peak_rms * 10 ** (-30.0 / 20.0)
    onset_block = peak_block
    while onset_block > 0 and smoothed[onset_block - 1] >= onset_threshold:
        onset_block -= 1
    start = max(0, onset_block * block - int(0.012 * 48_000))
    max_frames = max(int(0.03 * 48_000), int(max_duration * 48_000))
    max_end = min(mono.size, start + max_frames)
    search_end_block = min(block_count, math.ceil(max_end / block))
    decay_threshold_db, late_limit_db = _DECAY_PROFILE_DB.get(decay_profile, _DECAY_PROFILE_DB["Natural"])
    decay_threshold = peak_rms * 10 ** (decay_threshold_db / 20.0)
    hold_blocks = 5  # sustained 50ms
    decay_block: int | None = None
    for index in range(peak_block + 1, max(peak_block + 1, search_end_block - hold_blocks + 1)):
        if index + hold_blocks <= search_end_block and np.all(smoothed[index:index + hold_blocks] <= decay_threshold):
            decay_block = index
            break
    decay_reached = decay_block is not None
    if decay_reached:
        end = min(max_end, decay_block * block + int(0.015 * 48_000))
    else:
        end = max_end
    if end <= start:
        end = min(mono.size, start + int(0.03 * 48_000))
    start = _snap_zero_crossing(mono, start)
    end = _snap_zero_crossing(mono, end)
    if end - start < int(0.03 * 48_000):
        end = min(mono.size, start + int(0.03 * 48_000))
    selected = audio[:, start:end].copy()
    # Tail energy is measured at the end of the allowed window, not at the end of
    # the trimmed clip (whose final 60ms intentionally contains the audible decay).
    allowed_mono = mono[start:max_end]
    late_frames = min(allowed_mono.size, int(0.060 * 48_000))
    late_rms = math.sqrt(float(np.mean(allowed_mono[-late_frames:] ** 2))) if late_frames else 0.0
    late_relative_db = 20.0 * math.log10(max(late_rms, 1e-12) / peak_rms)
    analysis = {
        "sourceDurationSeconds": round(source_duration, 5),
        "maxDurationSeconds": round(max_duration, 5),
        "trimmedDurationSeconds": round(selected.shape[1] / 48_000, 5),
        "decayTimeMs": round(max(0.0, ((decay_block or search_end_block) - peak_block) * 10.0), 2),
        "lateTailRelativeDb": round(late_relative_db, 2),
        "decayReached": decay_reached,
        "reverbLike": (not decay_reached) or late_relative_db > late_limit_db,
        "decayProfile": decay_profile,
    }
    return _finish_audio(selected), analysis


def _try_auto_repair_tail(
    audio: np.ndarray,
    channels: int,
    max_duration: float,
    decay_profile: str,
    analysis: dict[str, Any],
) -> tuple[np.ndarray, dict[str, Any]]:
    """Fades a moderate residual tail, then re-runs the natural-decay gate."""
    if not analysis.get("reverbLike"):
        return audio, analysis
    original_late_db = float(analysis.get("lateTailRelativeDb", 0.0))
    if original_late_db <= -30.0 or original_late_db > -18.0:
        return audio, analysis
    ratio = max(0.0, min(1.0, (original_late_db + 30.0) / 12.0))
    fade_ms = 60.0 + ratio * 60.0
    fade_frames = min(audio.shape[1], max(1, round(fade_ms * 48_000 / 1000.0)))
    # The natural-decay gate requires 50ms below its threshold. End the fade
    # before that hold window so the repaired clip can prove a real endpoint;
    # the following post-process removes the silent proof window again.
    hold_frames = min(max(0, audio.shape[1] - fade_frames), round(0.050 * 48_000))
    fade_end = audio.shape[1] - hold_frames
    fade_start = max(0, fade_end - fade_frames)
    repaired = audio.copy()
    curve = np.cos(np.linspace(0.0, math.pi / 2.0, fade_frames, dtype=np.float32)) ** 2
    repaired[:, fade_start:fade_end] *= curve
    repaired[:, fade_end:] = 0.0
    repaired, repaired_analysis = _postprocess_natural(
        repaired, 48_000, channels, max_duration, decay_profile,
    )
    if repaired_analysis.get("reverbLike"):
        return audio, analysis
    repaired_analysis["autoTailFadeApplied"] = True
    repaired_analysis["autoTailFadeMs"] = round(fade_ms, 2)
    repaired_analysis["originalLateTailRelativeDb"] = round(original_late_db, 2)
    return repaired, repaired_analysis


def _write_pcm24(path: Path, audio: np.ndarray) -> None:
    interleaved = audio.T.reshape(-1)
    values = np.clip(interleaved * 8_388_607.0, -8_388_608, 8_388_607).astype(np.int32)
    packed = np.empty((values.size, 3), dtype=np.uint8)
    packed[:, 0] = values & 0xff
    packed[:, 1] = (values >> 8) & 0xff
    packed[:, 2] = (values >> 16) & 0xff
    with wave.open(str(path), "wb") as output:
        output.setnchannels(audio.shape[0])
        output.setsampwidth(3)
        output.setframerate(48_000)
        output.writeframes(packed.tobytes())


def _tail_relative_db(audio: np.ndarray) -> float:
    """Measures the last, duration-aware tail window against the clip peak."""
    if not audio.size:
        return 0.0
    mono = np.mean(audio, axis=0)
    peak = max(float(np.max(np.abs(mono))), 1e-12)
    tail_frames = min(mono.size, max(round(0.005 * 48_000), min(round(0.020 * 48_000), round(mono.size * 0.15))))
    tail_rms = math.sqrt(float(np.mean(mono[-tail_frames:] ** 2))) if tail_frames else 0.0
    return 20.0 * math.log10(max(tail_rms, 1e-12) / peak)



def _metrics(path: Path, audio: np.ndarray, raw_audio: np.ndarray, layer: str, take: int, raw_sha256: str, job_id: str,
             duration_analysis: dict[str, Any] | None = None) -> Candidate:
    peak = float(np.max(np.abs(audio)))
    rms = float(np.sqrt(np.mean(audio * audio)))
    mono = np.mean(audio, axis=0)
    spectrum = np.abs(np.fft.rfft(mono)) ** 2
    amplitude_spectrum = np.sqrt(spectrum)
    spectral_flatness = float(np.exp(np.mean(np.log(amplitude_spectrum + 1e-12))) / max(np.mean(amplitude_spectrum), 1e-12))
    raw_peak = float(np.max(np.abs(raw_audio))) if raw_audio.size else 0.0
    raw_rms = float(np.sqrt(np.mean(raw_audio * raw_audio))) if raw_audio.size else 0.0
    clipping_ratio = float(np.mean(np.abs(raw_audio) >= 0.999)) if raw_audio.size else 0.0
    crest_factor_db = 20 * math.log10(max(raw_peak, 1e-12) / max(raw_rms, 1e-12))
    hz = np.fft.rfftfreq(mono.size, 1 / 48_000)
    bands = {}
    for name, low, high in (("low", 20, 250), ("mid", 250, 4000), ("high", 4000, 20000)):
        bands[name] = float(np.sum(spectrum[(hz >= low) & (hz < high)]))
    total = max(sum(bands.values()), np.finfo(float).eps)
    bands = {key: round(value / total, 5) for key, value in bands.items()}
    peak_bins = 128
    samples_per_bin = max(1, math.ceil(mono.size / peak_bins))
    waveform = [
        round(float(np.max(np.abs(mono[index * samples_per_bin:min(mono.size, (index + 1) * samples_per_bin)]))), 5)
        if index * samples_per_bin < mono.size else 0.0
        for index in range(peak_bins)
    ]
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    duration_analysis = duration_analysis or {}
    return Candidate(
        candidateId=path.stem, url=f"/v1/sfx/jobs/{job_id}/candidates/{path.stem}", layer=layer, take=take, fileName=path.name,
        durationSeconds=round(audio.shape[1] / 48_000, 5), channels=audio.shape[0], sampleRate=48_000,
        peakDbfs=round(20 * math.log10(max(peak, np.finfo(float).eps)), 3),
        rmsDbfs=round(20 * math.log10(max(rms, np.finfo(float).eps)), 3),
        rawClippingRatio=round(clipping_ratio, 6), crestFactorDb=round(crest_factor_db, 3), spectralFlatness=round(spectral_flatness, 5),
        frequencyBands=bands, waveformPeaks=waveform, rawSha256=raw_sha256, sha256=digest,
        sourceDurationSeconds=float(duration_analysis.get("sourceDurationSeconds", raw_audio.shape[1] / 48_000 if raw_audio.size else 0.0)),
        maxDurationSeconds=float(duration_analysis.get("maxDurationSeconds", audio.shape[1] / 48_000 if audio.size else 0.0)),
        trimmedDurationSeconds=float(duration_analysis.get("trimmedDurationSeconds", audio.shape[1] / 48_000 if audio.size else 0.0)),
        decayTimeMs=float(duration_analysis.get("decayTimeMs", 0.0)),
        lateTailRelativeDb=float(duration_analysis.get("lateTailRelativeDb", 0.0)),
        decayReached=bool(duration_analysis.get("decayReached", False)),
        autoTailFadeApplied=bool(duration_analysis.get("autoTailFadeApplied", False)),
        autoTailFadeMs=float(duration_analysis.get("autoTailFadeMs", 0.0)),
        originalLateTailRelativeDb=float(duration_analysis.get("originalLateTailRelativeDb", 0.0)),
    )
