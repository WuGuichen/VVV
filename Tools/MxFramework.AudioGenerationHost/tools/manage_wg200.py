#!/usr/bin/env python3
"""Deploy and operate the single-user SFX host on wg200 without copying secrets."""

from __future__ import annotations

import argparse
import base64
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path
from typing import Any

HOST_ROOT = Path(__file__).resolve().parents[1]
REMOTE_HOST = os.getenv("MX_AUDIO_REMOTE_HOST", "wg200")
REMOTE_ENDPOINT = os.getenv("MX_AUDIO_GENERATION_ENDPOINT", "http://192.168.1.200:10188").rstrip("/")
if REMOTE_ENDPOINT == "http://192.168.1.200:9890":
    REMOTE_ENDPOINT = "http://192.168.1.200:10188"
REMOTE_ROOT = os.getenv("MX_AUDIO_REMOTE_ROOT", r"E:\MxFramework\AudioGenerationHost")
PROVIDER_ROOT = os.getenv("MX_AUDIO_PROVIDER_ROOT", r"E:\MxFramework\stable-audio-3")
PROVIDER_COMMIT = "8a3ded325ec6082113dbce11620b7b25b31ce6b0"
TASK_NAME = "MxFramework_AudioGenerationHost"
SYNC_FILES = (
    "audio_generation_host.py",
    "audio_generation_dsp.py",
    "audio_generation_models.py",
    "requirements.txt",
    "README.md",
    "start.ps1",
    "start_wg200.ps1",
)


class OperatorError(RuntimeError):
    def __init__(self, code: str, message: str, detail: str = "") -> None:
        super().__init__(message)
        self.code = code
        self.detail = _redact(detail)


def _redact(value: str) -> str:
    text = str(value or "")[-1600:]
    for name in ("MX_AUDIO_GENERATION_TOKEN", "HF_TOKEN"):
        secret = os.getenv(name, "")
        if secret:
            text = text.replace(secret, "***")
    return text


def _emit(status: str, progress: int, message: str, **extra: Any) -> None:
    print(json.dumps({"status": status, "progress": progress, "message": message, **extra}, ensure_ascii=False), flush=True)


def _run(args: list[str], timeout: int, code: str) -> str:
    result = subprocess.run(args, capture_output=True, timeout=timeout)
    raw = (result.stdout or b"") + (result.stderr or b"")
    try:
        output = raw.decode("utf-8")
    except UnicodeDecodeError:
        output = raw.decode("gb18030", errors="replace")
    if result.returncode:
        raise OperatorError(code, f"命令执行失败：{args[0]}", output)
    return output.strip()


def _powershell(script: str, timeout: int = 30, code: str = "AUDIO_HOST_SSH_FAILED") -> str:
    encoded = base64.b64encode(script.encode("utf-16le")).decode("ascii")
    return _run(["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=8", REMOTE_HOST,
                 "powershell", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], timeout, code)


def health(timeout: float = 3.0) -> dict[str, Any] | None:
    try:
        with urllib.request.urlopen(f"{REMOTE_ENDPOINT}/health", timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except (OSError, urllib.error.URLError, json.JSONDecodeError):
        return None


def _preflight() -> dict[str, Any]:
    script = (
        "$names=@('MX_AUDIO_GENERATION_TOKEN','HF_TOKEN','MX_AUDIO_MODEL_REVISION');"
        "$envs=@{};foreach($n in $names){$v=[Environment]::GetEnvironmentVariable($n,'Machine');"
        "if(-not $v){$v=[Environment]::GetEnvironmentVariable($n,'User')};$envs[$n]=[bool]$v};"
        f"$commit=(& git -C '{PROVIDER_ROOT}' rev-parse HEAD 2>$null);"
        f"[pscustomobject]@{{uv=[bool](Get-Command uv -ErrorAction SilentlyContinue);gpu=[bool](Get-Command nvidia-smi -ErrorAction SilentlyContinue);provider=(Test-Path '{PROVIDER_ROOT}\\pyproject.toml');commit=$commit;env=$envs}}|ConvertTo-Json -Compress"
    )
    raw = _powershell(script)
    try:
        json_line = next(line for line in reversed(raw.splitlines()) if line.lstrip().startswith("{"))
        facts = json.loads(json_line)
    except (json.JSONDecodeError, StopIteration) as exception:
        raise OperatorError("AUDIO_HOST_PREFLIGHT_INVALID", "wg200 环境检查返回无效数据", str(exception)) from exception
    missing = [name for name, present in facts.get("env", {}).items() if not present]
    if missing:
        raise OperatorError("AUDIO_HOST_ENV_MISSING", "wg200 缺少必要环境变量", ", ".join(missing))
    if not facts.get("uv") or not facts.get("gpu") or not facts.get("provider"):
        raise OperatorError("AUDIO_HOST_DEPENDENCY_MISSING", "wg200 缺少 uv、GPU 工具或 Stable Audio provider", json.dumps(facts, ensure_ascii=False))
    if str(facts.get("commit") or "").strip() != PROVIDER_COMMIT:
        raise OperatorError("AUDIO_HOST_PROVIDER_REVISION_MISMATCH", "Stable Audio provider revision 不匹配", str(facts.get("commit") or "missing"))
    return facts


def _sync() -> None:
    staging = REMOTE_ROOT + ".staging." + uuid.uuid4().hex[:10]
    quoted_files = [str(HOST_ROOT / name) for name in SYNC_FILES]
    _powershell(f"New-Item -ItemType Directory -Force -Path '{staging}'|Out-Null", code="AUDIO_HOST_SYNC_FAILED")
    try:
        _run(["scp", *quoted_files, f"{REMOTE_HOST}:{staging.replace(chr(92), '/')}/"], 120, "AUDIO_HOST_SYNC_FAILED")
        script = (
            f"$root='{REMOTE_ROOT}';$stage='{staging}';"
            "New-Item -ItemType Directory -Force -Path $root|Out-Null;"
            "Get-ChildItem $stage -File|ForEach-Object{Move-Item -Force $_.FullName (Join-Path $root $_.Name)};"
            "Remove-Item -Recurse -Force $stage"
        )
        _powershell(script, timeout=45, code="AUDIO_HOST_SYNC_FAILED")
    except Exception:
        try:
            _powershell(f"Remove-Item -Recurse -Force '{staging}' -ErrorAction SilentlyContinue")
        except Exception:
            pass
        raise


def _ensure_cuda_runtime() -> dict[str, Any]:
    python = PROVIDER_ROOT + r"\.venv\Scripts\python.exe"
    probe = (
        f"$python='{python}';if(-not(Test-Path $python)){{throw 'provider venv python is missing'}};"
        "$raw=& $python -c \"import json,torch;print(json.dumps({'version':torch.__version__,'cudaVersion':torch.version.cuda,'available':torch.cuda.is_available(),'device':torch.cuda.get_device_name(0) if torch.cuda.is_available() else ''}))\";"
        "$raw|Select-Object -Last 1"
    )
    raw = _powershell(probe, timeout=45, code="AUDIO_HOST_CUDA_PROBE_FAILED")
    try:
        facts = json.loads(next(line for line in reversed(raw.splitlines()) if line.lstrip().startswith("{")))
    except (json.JSONDecodeError, StopIteration):
        facts = {"available": False, "detail": raw}
    if not facts.get("available"):
        _emit("Syncing", 32, "安装 Windows CUDA 12.6 PyTorch runtime")
        install = (
            f"& uv pip install --python '{python}' --index-url https://download.pytorch.org/whl/cu126 "
            "'torch==2.7.1+cu126' 'torchaudio==2.7.1+cu126';if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}"
        )
        _powershell(install, timeout=900, code="AUDIO_HOST_CUDA_INSTALL_FAILED")
    _emit("Syncing", 38, "安装 Host 运行依赖并校验 CUDA")
    requirements = REMOTE_ROOT + r"\requirements.txt"
    _powershell(
        f"& uv pip install --python '{python}' -r '{requirements}';if($LASTEXITCODE-ne 0){{exit $LASTEXITCODE}}",
        timeout=300, code="AUDIO_HOST_RUNTIME_INSTALL_FAILED")
    raw = _powershell(probe, timeout=45, code="AUDIO_HOST_CUDA_PROBE_FAILED")
    facts = json.loads(next(line for line in reversed(raw.splitlines()) if line.lstrip().startswith("{")))
    if not facts.get("available") or "4080" not in str(facts.get("device", "")):
        raise OperatorError("AUDIO_HOST_CUDA_UNAVAILABLE", "CUDA runtime 未识别 RTX 4080", json.dumps(facts, ensure_ascii=False))
    return facts


def _start_task() -> None:
    launcher = f'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{REMOTE_ROOT}\\start_wg200.ps1"'
    command = ("netsh advfirewall firewall add rule name=\"MxFramework_AudioGenerationHost\" dir=in action=allow protocol=TCP localport=10188 profile=any|Out-Null;"
               f'schtasks /Create /TN "{TASK_NAME}" /TR \'{launcher}\' /SC ONCE /ST 23:59 /F; if($LASTEXITCODE -ne 0){{exit $LASTEXITCODE}}; schtasks /Run /TN "{TASK_NAME}"')
    _powershell(command, timeout=45, code="AUDIO_HOST_TASK_FAILED")


def ensure(timeout_seconds: int = 300) -> dict[str, Any]:
    current = health()
    if current and current.get("status") == "ok" and current.get("device") == "cuda" and current.get("cudaAvailable"):
        _emit("Ready", 100, "SFX Host 已就绪", health=current, serviceWarm=True)
        return current
    _emit("Checking", 5, "检查 wg200 环境")
    _preflight()
    _emit("Syncing", 25, "同步 Audio Generation Host")
    _sync()
    cuda = _ensure_cuda_runtime()
    _emit("Starting", 45, "创建并启动 Windows 计划任务")
    _start_task()
    started = time.monotonic()
    while time.monotonic() - started < timeout_seconds:
        current = health(timeout=5)
        if current and current.get("status") == "ok":
            _emit("Ready", 100, "SFX Host 已就绪", health=current, serviceWarm=False,
                  startupSeconds=round(time.monotonic() - started, 2), cuda=cuda)
            return current
        elapsed = time.monotonic() - started
        _emit("Loading", min(95, 50 + int(elapsed / max(1, timeout_seconds) * 45)), "等待 10188 服务启动")
        time.sleep(3)
    raise OperatorError("AUDIO_HOST_START_TIMEOUT", "SFX Host 未在限定时间内就绪", f"timeout={timeout_seconds}s")


def stop() -> None:
    script = "$c=Get-NetTCPConnection -LocalPort 10188 -State Listen -ErrorAction SilentlyContinue;if($c){Stop-Process -Id $c.OwningProcess -Force};Write-Output stopped"
    _powershell(script, code="AUDIO_HOST_STOP_FAILED")
    _emit("Stopped", 100, "10188 已停止；语音服务未受影响")


def smoke() -> None:
    ensure()
    token = os.getenv("MX_AUDIO_GENERATION_TOKEN", "")
    if not token:
        raise OperatorError("AUDIO_HOST_LOCAL_TOKEN_MISSING", "Mac 环境缺少 MX_AUDIO_GENERATION_TOKEN")
    request = urllib.request.Request(f"{REMOTE_ENDPOINT}/v1/sfx/jobs", method="POST",
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {token}"},
        data=json.dumps({"prompt": "dry isolated stone footstep, close-mic, game-ready", "durationSeconds": 1.0,
                         "seed": 17, "channels": 1, "layers": [{"name": "body", "takes": 1}]}).encode())
    with urllib.request.urlopen(request, timeout=15) as response:
        job = json.loads(response.read())
    while True:
        poll = urllib.request.Request(f"{REMOTE_ENDPOINT}/v1/sfx/jobs/{job['jobId']}", headers={"Authorization": f"Bearer {token}"})
        with urllib.request.urlopen(poll, timeout=15) as response:
            job = json.loads(response.read())
        _emit(job["status"], int(float(job.get("progress", 0)) * 100), "真实 SFX smoke")
        if job["status"] in {"Succeeded", "Failed", "Cancelled"}:
            break
        time.sleep(2)
    if job["status"] != "Succeeded":
        raise OperatorError("AUDIO_HOST_SMOKE_FAILED", "真实 SFX smoke 失败", job.get("failureReason", ""))
    if not job.get("candidates") or any(candidate.get("status") != "Ready" for candidate in job["candidates"]):
        raise OperatorError("AUDIO_HOST_SMOKE_CANDIDATE_INVALID", "真实 SFX smoke 候选未通过技术校验",
                            json.dumps([candidate.get("status") for candidate in job.get("candidates", [])]))
    _emit("Ready", 100, "真实 SFX smoke 通过", job=job)


def main() -> int:
    parser = argparse.ArgumentParser(description="Operate MxFramework Audio Generation Host on wg200")
    parser.add_argument("command", choices=("ensure", "health", "smoke", "stop"))
    parser.add_argument("--timeout", type=int, default=300)
    args = parser.parse_args()
    try:
        if args.command == "ensure": ensure(args.timeout)
        elif args.command == "health": _emit("Ready" if health() else "Offline", 100, "健康检查完成", health=health())
        elif args.command == "smoke": smoke()
        else: stop()
        return 0
    except OperatorError as exception:
        _emit("Failed", 100, str(exception), code=exception.code, detail=exception.detail, retryable=True)
        return 1
    except Exception as exception:
        _emit("Failed", 100, "未预期的操作错误", code="AUDIO_HOST_OPERATOR_FAILED", detail=_redact(str(exception)), retryable=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
