"""Request and persisted job contracts for the audio generation host."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from typing import Any

from pydantic import BaseModel, Field, model_validator

MODEL_ID = "stabilityai/stable-audio-3-small-sfx"
MODEL_REVISION = os.getenv("MX_AUDIO_MODEL_REVISION", "unresolved")
FAKE = os.getenv("MX_AUDIO_GENERATION_FAKE", "0") == "1"

class LayerRequest(BaseModel):
    name: str = Field(min_length=1, max_length=32)
    takes: int = Field(default=1, ge=1, le=12)
    prompt: str = Field(default="", max_length=4000)
    maxDurationSeconds: float | None = Field(default=None, ge=0.03, le=30.0)


class SfxJobRequest(BaseModel):
    audioCueId: str = Field(default="", max_length=256)
    eventDefinitionId: str = Field(default="", max_length=256)
    recipeRevision: int = Field(default=0, ge=0)
    recipeHash: str = Field(default="", max_length=128)
    prompt: str = Field(min_length=8, max_length=4000)
    negativePrompt: str = Field(default="music, speech, voice, background noise, clipping", max_length=2000)
    durationSeconds: float = Field(default=1.0, ge=0.1, le=30.0)
    durationMode: str = Field(default="LegacyFixed", pattern="^(LegacyFixed|NaturalDecay)$")
    durationCharacter: str = Field(default="Auto", pattern="^(Auto|Shorter|Natural|Extended)$")
    decayProfile: str = Field(default="Natural", pattern="^(Short|Natural|Brittle|Granular|Resonant|Magic|Sweep)$")
    maxDurationSeconds: float | None = Field(default=None, ge=0.03, le=30.0)
    seed: int = Field(default=0, ge=0, le=2_147_483_647)
    channels: int = Field(default=1, ge=1, le=2)
    layers: list[LayerRequest] = Field(default_factory=lambda: [LayerRequest(name="body", takes=1)])

    @model_validator(mode="after")
    def validate_candidate_total(self) -> "SfxJobRequest":
        if sum(layer.takes for layer in self.layers) > 36:
            raise ValueError("generation job cannot exceed 36 candidates")
        return self


class SfxExtendRequest(SfxJobRequest):
    pass


class CandidateEditRequest(BaseModel):
    candidateId: str = Field(min_length=1, max_length=64)
    sourceHash: str = Field(min_length=64, max_length=64)
    inMs: float = Field(ge=0)
    outMs: float = Field(gt=0)
    fadeInMs: float = Field(default=3, ge=0, le=200)
    fadeOutMs: float = Field(default=3, ge=0, le=200)
    revision: int = Field(default=1, ge=1)


class CandidateRepairRequest(BaseModel):
    candidateId: str = Field(min_length=1, max_length=64)
    sourceHash: str = Field(min_length=64, max_length=64)


@dataclass
class Candidate:
    candidateId: str
    url: str
    layer: str
    take: int
    fileName: str
    durationSeconds: float
    channels: int
    sampleRate: int
    peakDbfs: float
    rmsDbfs: float
    rawClippingRatio: float
    crestFactorDb: float
    spectralFlatness: float
    frequencyBands: dict[str, float]
    waveformPeaks: list[float]
    rawSha256: str
    sha256: str
    sourceDurationSeconds: float = 0.0
    maxDurationSeconds: float = 0.0
    trimmedDurationSeconds: float = 0.0
    decayTimeMs: float = 0.0
    lateTailRelativeDb: float = 0.0
    decayReached: bool = False
    autoTailFadeApplied: bool = False
    autoTailFadeMs: float = 0.0
    originalLateTailRelativeDb: float = 0.0
    status: str = "Ready"


@dataclass
class Job:
    jobId: str
    provider: str = "local-stability-ai"
    model: str = MODEL_ID
    revision: str = MODEL_REVISION
    fake: bool = FAKE
    status: str = "Queued"
    progress: float = 0.0
    seed: int = 0
    audioCueId: str = ""
    eventDefinitionId: str = ""
    recipeRevision: int = 0
    recipeHash: str = ""
    durationPolicyHash: str = ""
    batches: list[dict[str, Any]] = field(default_factory=list)
    candidates: list[dict[str, Any]] = field(default_factory=list)
    failureReason: str = ""
    qualityWarning: str = ""
    gpuState: str = "VoiceReady"
    computeDevice: str = ""
    gpuPeakMemoryMiB: float = 0.0
    voiceRecoveryStatus: str = "NotRequired"
    voiceServices: list[dict[str, Any]] = field(default_factory=list)
    createdUtc: str = ""
    updatedUtc: str = ""
    cancelled: bool = False
