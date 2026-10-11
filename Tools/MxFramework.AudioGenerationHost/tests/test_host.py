import importlib
import os
import time
import pytest
from pydantic import ValidationError

os.environ["MX_AUDIO_GENERATION_FAKE"] = "1"
os.environ["MX_AUDIO_GENERATION_TOKEN"] = "test-token"
host = importlib.import_module("audio_generation_host")

from fastapi.testclient import TestClient

client = TestClient(host.app)
headers = {"Authorization": "Bearer test-token"}


def setup_function():
    host.reset_for_tests()


def test_auth_and_fake_job_round_trip():
    assert client.post("/v1/sfx/jobs", json={"prompt": "dry isolated metal impact"}).status_code == 401
    response = client.post("/v1/sfx/jobs", headers=headers, json={
        "prompt": "dry isolated metal impact, close-mic, game-ready",
        "durationSeconds": 0.2, "seed": 11, "channels": 1,
        "layers": [{"name": "body", "takes": 2}],
    })
    assert response.status_code == 202
    job_id = response.json()["jobId"]
    for _ in range(100):
        job = client.get(f"/v1/sfx/jobs/{job_id}", headers=headers).json()
        if job["status"] in {"Succeeded", "Failed", "Cancelled"}:
            break
        time.sleep(0.02)
    assert job["status"] == "Succeeded", job
    assert len(job["candidates"]) == 2
    assert len(job["candidates"][0]["waveformPeaks"]) == 128
    assert len(job["candidates"][0]["rawSha256"]) == 64
    assert job["candidates"][0]["rawSha256"] != job["candidates"][0]["sha256"]
    candidate_id = job["candidates"][0]["candidateId"]
    audio = client.get(f"/v1/sfx/jobs/{job_id}/candidates/{candidate_id}", headers=headers)
    assert audio.status_code == 200
    assert audio.content[:4] == b"RIFF"
    assert job["voiceRecoveryStatus"] == "NotRequired"


def test_candidate_count_boundaries():
    request = host.SfxJobRequest(prompt="dry isolated layered impact game-ready", layers=[
        {"name": "transient", "takes": 12},
        {"name": "body", "takes": 12},
        {"name": "tail", "takes": 12},
    ])
    assert sum(layer.takes for layer in request.layers) == 36
    with pytest.raises(ValidationError):
        host.SfxJobRequest(prompt="dry isolated oversized impact game-ready", layers=[
            {"name": "transient", "takes": 12},
            {"name": "body", "takes": 12},
            {"name": "tail", "takes": 12},
            {"name": "extra", "takes": 1},
        ])


def test_layer_prompt_is_sent_to_generation(monkeypatch):
    prompts = []
    monkeypatch.setattr(host, "_generate", lambda prompt, negative, seed, duration: (prompts.append(prompt) or host._fake_audio(prompt, seed, duration), 48_000))
    job = host.Job(jobId="layer-prompt-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(
        prompt="generic impact fallback",
        durationSeconds=0.1,
        layers=[{"name": "body", "takes": 1, "prompt": "dense stone impact body with short decay"}],
    ))
    assert job.status == "Succeeded"
    assert prompts == ["TrackType: SFX. dense stone impact body with short decay"]


def test_saturated_model_output_is_not_ready(monkeypatch):
    saturated = host.np.ones((2, 4_800), dtype=host.np.float32)
    monkeypatch.setattr(host, "_generate", lambda *args: (saturated, 48_000))
    job = host.Job(jobId="clipped-output-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(prompt="dry isolated impact game-ready", durationSeconds=0.1))
    assert job.status == "Failed"
    assert "NO_READY_CANDIDATES" in job.failureReason
    assert job.candidates[0]["status"] == "Clipped"
    assert job.candidates[0]["rawClippingRatio"] == 1.0


def test_rejects_path_traversal():
    assert client.get("/v1/sfx/jobs/..%2Fsecret", headers=headers).status_code in {400, 404}


def test_fake_mode_never_switches_voice_services(monkeypatch):
    invoked = []
    monkeypatch.setattr(host, "VOICE_STOP", ["must-not-run"])
    monkeypatch.setattr(host, "VOICE_START", ["must-not-run"])
    monkeypatch.setattr(host, "_run_commands", lambda commands: invoked.append(commands))
    response = client.post("/v1/sfx/jobs", headers=headers, json={
        "prompt": "dry isolated stone footstep game-ready", "durationSeconds": 0.15,
    })
    job_id = response.json()["jobId"]
    for _ in range(100):
        job = client.get(f"/v1/sfx/jobs/{job_id}", headers=headers).json()
        if job["status"] in {"Succeeded", "Failed", "Cancelled"}:
            break
        time.sleep(0.02)
    assert job["status"] == "Succeeded", job
    assert invoked == []
    assert job["voiceRecoveryStatus"] == "NotRequired"


def test_partial_voice_stop_failure_still_runs_recovery(monkeypatch):
    calls = []
    monkeypatch.setattr(host, "FAKE", False)
    monkeypatch.setattr(host, "VOICE_STOP", ["stop"])
    monkeypatch.setattr(host, "VOICE_START", ["start"])
    monkeypatch.setattr(host, "_snapshot_voice_services", lambda: [{"name": "IndexTTS2", "url": "http://127.0.0.1:9888/health", "wasRunning": True, "stopped": False, "restored": False, "health": True}])
    monkeypatch.setattr(host, "_wait_voice_service", lambda url, timeout: True)
    monkeypatch.setattr(host, "_unload_model", lambda: None)

    def run(commands):
        calls.append(commands)
        if commands == ["stop"]:
            raise RuntimeError("partial stop failure")

    monkeypatch.setattr(host, "_run_commands", run)
    job = host.Job(jobId="recovery-test", createdUtc=host._utc(), updatedUtc=host._utc())
    request = host.SfxJobRequest(prompt="dry isolated impact game-ready")
    host._execute(job, request)
    assert job.status == "Failed"
    assert calls == [["stop"], ["start"]]
    assert job.voiceRecoveryStatus == "Ready"


def test_cuda_oom_still_restores_voice(monkeypatch):
    calls = []
    monkeypatch.setattr(host, "FAKE", False)
    monkeypatch.setattr(host, "VOICE_STOP", ["stop"])
    monkeypatch.setattr(host, "VOICE_START", ["start"])
    monkeypatch.setattr(host, "_run_commands", lambda commands: calls.append(commands))
    monkeypatch.setattr(host, "_snapshot_voice_services", lambda: [{"name": "CosyVoice", "url": "http://127.0.0.1:9889/health", "wasRunning": True, "stopped": False, "restored": False, "health": True}])
    monkeypatch.setattr(host, "_wait_voice_service", lambda url, timeout: True)
    monkeypatch.setattr(host, "_load_model", lambda: object())
    monkeypatch.setattr(host, "_unload_model", lambda: None)
    monkeypatch.setattr(host, "_generate", lambda *args: (_ for _ in ()).throw(RuntimeError("CUDA out of memory")))
    job = host.Job(jobId="oom-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(prompt="dry isolated impact game-ready"))
    assert job.status == "Failed"
    assert "CUDA out of memory" in job.failureReason
    assert calls == [["stop"], ["start"]]
    assert job.voiceRecoveryStatus == "Ready"


def test_offline_voice_services_remain_offline(monkeypatch):
    calls = []
    monkeypatch.setattr(host, "FAKE", False)
    monkeypatch.setattr(host, "VOICE_STOP", ["stop-index", "stop-cosy"])
    monkeypatch.setattr(host, "VOICE_START", ["start-index", "start-cosy"])
    monkeypatch.setattr(host, "_snapshot_voice_services", lambda: [
        {"name": "IndexTTS2", "url": "http://127.0.0.1:9888/health", "wasRunning": False, "stopped": False, "restored": False, "health": False},
        {"name": "CosyVoice", "url": "http://127.0.0.1:9889/health", "wasRunning": False, "stopped": False, "restored": False, "health": False},
    ])
    monkeypatch.setattr(host, "_run_commands", lambda commands: calls.append(commands))
    monkeypatch.setattr(host, "_load_model", lambda: object())
    monkeypatch.setattr(host, "_unload_model", lambda: None)
    monkeypatch.setattr(host, "_generate", lambda *args: (host._fake_audio("impact", 3, 0.1), 48_000))
    job = host.Job(jobId="offline-voice-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(prompt="dry isolated impact game-ready", durationSeconds=0.1))
    assert job.status == "Succeeded"
    assert calls == []
    assert job.voiceRecoveryStatus == "NotRequired"
    assert all(not item["restored"] for item in job.voiceServices)


def test_cancelled_job_finishes_without_candidates():
    job = host.Job(jobId="cancel-test", createdUtc=host._utc(), updatedUtc=host._utc(), cancelled=True)
    host._execute(job, host.SfxJobRequest(prompt="dry isolated impact game-ready"))
    assert job.status == "Cancelled"
    assert job.candidates == []


def test_extend_appends_monotonic_takes_and_lists_by_cue():
    response = client.post("/v1/sfx/jobs", headers=headers, json={
        "audioCueId": "sfx.test", "eventDefinitionId": "event.test", "recipeRevision": 2, "recipeHash": "recipe-a",
        "prompt": "dry isolated stone impact game-ready", "durationSeconds": 0.1,
        "layers": [{"name": "body", "takes": 1}],
    })
    job_id = response.json()["jobId"]
    for _ in range(100):
        job = client.get(f"/v1/sfx/jobs/{job_id}", headers=headers).json()
        if job["status"] == "Succeeded": break
        time.sleep(0.02)
    extended = client.post(f"/v1/sfx/jobs/{job_id}/extend", headers=headers, json={
        "audioCueId": "sfx.test", "eventDefinitionId": "event.test", "recipeRevision": 2, "recipeHash": "recipe-a",
        "prompt": "dry isolated stone impact game-ready", "durationSeconds": 0.1,
        "layers": [{"name": "body", "takes": 2}],
    })
    assert extended.status_code == 202
    for _ in range(100):
        job = client.get(f"/v1/sfx/jobs/{job_id}", headers=headers).json()
        if job["status"] == "Succeeded" and len(job["candidates"]) == 3: break
        time.sleep(0.02)
    assert [item["take"] for item in job["candidates"]] == [1, 2, 3]
    assert len(job["batches"]) == 2
    shelf = client.get("/v1/sfx/jobs?audioCueId=sfx.test", headers=headers).json()
    assert [item["jobId"] for item in shelf] == [job_id]


def test_extend_rejects_stale_recipe_and_layer_overflow():
    job = host.Job(jobId="extend-limit", audioCueId="sfx.test", recipeHash="recipe-a", status="Succeeded",
                   createdUtc=host._utc(), updatedUtc=host._utc(),
                   candidates=[{"candidateId": f"body-{i:02d}", "layer": "body", "take": i, "sha256": str(i)} for i in range(1, 12)])
    host._jobs[job.jobId] = job
    payload = {"audioCueId": "sfx.test", "recipeHash": "recipe-b", "prompt": "dry isolated impact game-ready", "layers": [{"name": "body", "takes": 1}]}
    assert client.post("/v1/sfx/jobs/extend-limit/extend", headers=headers, json=payload).status_code == 409
    payload["recipeHash"] = "recipe-a"; payload["layers"][0]["takes"] = 2
    assert client.post("/v1/sfx/jobs/extend-limit/extend", headers=headers, json=payload).status_code == 400


def test_integer_model_output_is_scaled_before_wav_processing(monkeypatch):
    class IntegerModel:
        def generate(self, **_):
            return host.np.array([[0, 16384, -32768, 32767]], dtype=host.np.int16)
    monkeypatch.setattr(host, "FAKE", False)
    monkeypatch.setattr(host, "_model", IntegerModel())
    audio, rate = host._generate("dry isolated impact", "noise", 1, 0.1)
    assert rate == 44_100
    assert audio.dtype == host.np.float32
    assert host.np.max(host.np.abs(audio)) <= 1.0
    assert audio[0, 1] == pytest.approx(0.5, abs=0.001)


def test_float_tensor_in_pcm_domain_is_scaled(monkeypatch):
    class FloatPcmModel:
        def generate(self, **_):
            return host.np.array([[0.0, 16384.0, -32768.0, 32767.0]], dtype=host.np.float32)
    monkeypatch.setattr(host, "FAKE", False)
    monkeypatch.setattr(host, "_model", FloatPcmModel())
    audio, _ = host._generate("dry isolated impact", "noise", 1, 0.1)
    assert host.np.max(host.np.abs(audio)) <= 1.0
    assert audio[0, 1] == pytest.approx(0.5, abs=0.001)


def test_postprocess_extracts_requested_window_around_peak():
    audio = host.np.zeros((1, 5 * 48_000), dtype=host.np.float32)
    audio[0, 3 * 48_000:3 * 48_000 + 200] = 0.5
    result = host._postprocess(audio, 48_000, 1, 1.2)
    assert result.shape[0] == 1
    assert result.shape[1] <= int(1.2 * 48_000)
    assert float(host.np.max(host.np.abs(result))) > 0.1


def _synthetic_decay(decay_rate=60.0, tail_level=0.0, frequency=500.0, duration=1.0):
    sample_rate = 48_000
    frames = int(sample_rate * duration)
    audio = host.np.zeros(frames, dtype=host.np.float32)
    start = int(sample_rate * 0.1)
    time_axis = host.np.arange(frames - start, dtype=host.np.float32) / sample_rate
    audio[start:] = 0.8 * host.np.sin(2 * host.math.pi * frequency * time_axis) * host.np.exp(-decay_rate * time_axis)
    if tail_level:
        audio[start:] += tail_level * host.np.sin(2 * host.math.pi * 130 * time_axis)
    return audio.reshape(1, -1)


@pytest.mark.parametrize("profile,decay_rate,maximum", [
    ("Short", 85.0, 0.22),
    ("Natural", 60.0, 0.30),
    ("Brittle", 48.0, 0.38),
    ("Granular", 42.0, 0.42),
    ("Resonant", 35.0, 0.45),
    ("Sweep", 40.0, 0.45),
])
def test_natural_decay_profiles_trim_without_filling_the_window(profile, decay_rate, maximum):
    result, analysis = host._postprocess_natural(_synthetic_decay(decay_rate), 48_000, 1, maximum, profile)
    assert analysis["decayReached"] is True
    assert analysis["reverbLike"] is False
    assert 0.03 <= result.shape[1] / 48_000 < maximum
    assert analysis["sourceDurationSeconds"] == 1.0
    assert analysis["trimmedDurationSeconds"] == pytest.approx(result.shape[1] / 48_000, abs=0.001)


def test_natural_decay_rejects_persistent_room_tail_and_no_end_noise():
    reverberant, reverb = host._postprocess_natural(_synthetic_decay(60.0, tail_level=0.1), 48_000, 1, 0.45, "Natural")
    noise = host.np.random.default_rng(12).normal(0, 0.12, 48_000).astype(host.np.float32).reshape(1, -1)
    noisy, no_end = host._postprocess_natural(noise, 48_000, 1, 0.30, "Natural")
    assert reverberant.shape[1] / 48_000 <= 0.451
    assert reverb["reverbLike"] is True and reverb["decayReached"] is False
    assert noisy.shape[1] / 48_000 <= 0.301
    assert no_end["reverbLike"] is True


def test_natural_decay_job_auto_fades_moderate_tail(monkeypatch):
    monkeypatch.setattr(host, "_generate", lambda *args: (_synthetic_decay(60.0, tail_level=0.02), 48_000))
    job = host.Job(jobId="reverb-like-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(
        prompt="isolated impact in an anechoic booth",
        durationSeconds=0.45,
        durationMode="NaturalDecay",
        maxDurationSeconds=0.45,
    ))
    assert job.status == "Succeeded"
    assert job.qualityWarning == ""
    assert job.failureReason == ""
    assert job.candidates[0]["status"] == "Ready"
    assert job.candidates[0]["autoTailFadeApplied"] is True
    assert 60 <= job.candidates[0]["autoTailFadeMs"] <= 120
    assert -30 < job.candidates[0]["originalLateTailRelativeDb"] <= -18


def test_natural_decay_job_preserves_loud_tail_for_review(monkeypatch):
    monkeypatch.setattr(host, "_generate", lambda *args: (_synthetic_decay(60.0, tail_level=0.1), 48_000))
    job = host.Job(jobId="loud-tail-test", createdUtc=host._utc(), updatedUtc=host._utc())
    host._execute(job, host.SfxJobRequest(
        prompt="isolated impact in an anechoic booth",
        durationSeconds=0.45,
        durationMode="NaturalDecay",
        maxDurationSeconds=0.45,
    ))
    assert job.status == "Succeeded"
    assert "REVIEW_REQUIRED" in job.qualityWarning
    assert job.candidates[0]["status"] == "ReverbLike"
    assert job.candidates[0]["autoTailFadeApplied"] is False
    assert job.candidates[0]["lateTailRelativeDb"] > -18


def test_reverb_like_candidate_can_be_repaired_and_unlocked():
    job = host.Job(jobId="repair-tail-test", status="Succeeded", createdUtc=host._utc(), updatedUtc=host._utc())
    folder = host.ROOT / job.jobId
    folder.mkdir(parents=True, exist_ok=True)
    frames = round(0.115 * 48_000)
    axis = host.np.arange(frames, dtype=host.np.float32) / 48_000
    audio = (0.35 * host.np.sin(2 * host.math.pi * 1_300 * axis)).reshape(1, -1)
    path = folder / "body-03.wav"
    host._write_pcm24(path, audio)
    candidate = host._metrics(path, audio, audio, "body", 3, "a" * 64, job.jobId, {
        "lateTailRelativeDb": -17.8,
        "decayReached": False,
        "trimmedDurationSeconds": 0.115,
    })
    candidate.status = "ReverbLike"
    job.candidates = [host.asdict(candidate)]
    job.qualityWarning = "AUDIO_GENERATION_REVIEW_REQUIRED"
    host._jobs[job.jobId] = job
    host._save(job)

    response = client.post(
        f"/v1/sfx/jobs/{job.jobId}/candidates/{candidate.candidateId}/repair",
        headers=headers,
        json={"candidateId": candidate.candidateId, "sourceHash": candidate.sha256},
    )
    assert response.status_code == 200, response.text
    repaired = response.json()
    assert repaired["status"] == "Ready"
    assert repaired["autoTailFadeApplied"] is True
    assert repaired["originalLateTailRelativeDb"] == pytest.approx(-17.8, abs=0.01)
    assert repaired["lateTailRelativeDb"] <= -30
    assert 0 < repaired["autoTailFadeMs"] <= 120
    assert host._jobs[job.jobId].candidates[0]["sha256"] == repaired["sha256"]
    assert host._jobs[job.jobId].qualityWarning == ""


def test_ready_candidate_can_be_rendered_with_non_destructive_trim_and_fades():
    created = client.post("/v1/sfx/jobs", headers=headers, json={
        "prompt": "dry isolated impact game-ready", "durationSeconds": 0.2,
        "layers": [{"name": "body", "takes": 1}],
    }).json()
    job_id = created["jobId"]
    for _ in range(100):
        job = client.get(f"/v1/sfx/jobs/{job_id}", headers=headers).json()
        if job["status"] in {"Succeeded", "Failed"}: break
        time.sleep(0.02)
    candidate = job["candidates"][0]
    source = client.get(f"/v1/sfx/jobs/{job_id}/candidates/{candidate['candidateId']}", headers=headers).content
    response = client.post(f"/v1/sfx/jobs/{job_id}/candidates/{candidate['candidateId']}/render", headers=headers, json={
        "candidateId": candidate["candidateId"], "sourceHash": candidate["sha256"],
        "inMs": 20, "outMs": 160, "fadeInMs": 10, "fadeOutMs": 12, "revision": 2,
    })
    assert response.status_code == 200, response.text
    rendered = response.json()
    assert rendered["status"] == "Ready"
    assert 0.12 <= rendered["durationSeconds"] <= 0.16
    audio = client.get(rendered["url"], headers=headers)
    assert audio.status_code == 200 and audio.content[:4] == b"RIFF"
    assert client.get(f"/v1/sfx/jobs/{job_id}/candidates/{candidate['candidateId']}", headers=headers).content == source


def test_candidate_render_rejects_stale_hash_short_range_and_invalid_source():
    job = host.Job(jobId="edit-invalid", status="Succeeded", createdUtc=host._utc(), updatedUtc=host._utc())
    folder = host.ROOT / job.jobId
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / "body-01.wav"
    audio = host._fake_audio("impact", 7, 0.1)
    host._write_pcm24(path, audio)
    digest = host.hashlib.sha256(path.read_bytes()).hexdigest()
    job.candidates = [{"candidateId": "body-01", "layer": "body", "take": 1, "status": "Ready", "sha256": digest, "fileName": path.name}]
    host._jobs[job.jobId] = job
    endpoint = "/v1/sfx/jobs/edit-invalid/candidates/body-01/render"
    base = {"candidateId": "body-01", "sourceHash": digest, "inMs": 0, "outMs": 80, "fadeInMs": 3, "fadeOutMs": 3}
    assert client.post(endpoint, headers=headers, json={**base, "sourceHash": "0" * 64}).status_code == 409
    assert client.post(endpoint, headers=headers, json={**base, "outMs": 20}).status_code == 400
    job.candidates[0]["status"] = "NoiseLike"
    assert client.post(endpoint, headers=headers, json=base).status_code == 409
