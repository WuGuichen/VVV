import ast
import importlib.util
from pathlib import Path


MODULE_PATH = Path(__file__).parents[1] / "tools" / "manage_wg200.py"
SPEC = importlib.util.spec_from_file_location("manage_wg200", MODULE_PATH)
manager = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
SPEC.loader.exec_module(manager)


def test_sync_files_include_local_python_dependencies():
    synced = set(manager.SYNC_FILES)
    pending = [name for name in synced if name.endswith(".py")]
    checked = set()

    while pending:
        filename = pending.pop()
        if filename in checked:
            continue
        checked.add(filename)
        tree = ast.parse((manager.HOST_ROOT / filename).read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if not isinstance(node, (ast.Import, ast.ImportFrom)):
                continue
            modules = [alias.name for alias in node.names] if isinstance(node, ast.Import) else [node.module or ""]
            for module in modules:
                dependency = module.split(".", 1)[0] + ".py"
                if (manager.HOST_ROOT / dependency).is_file():
                    assert dependency in synced, f"{filename} imports local module {dependency}, but it is not deployed"
                    pending.append(dependency)


def test_ensure_returns_without_sync_when_host_is_ready(monkeypatch):
    calls = []
    monkeypatch.setattr(manager, "health", lambda timeout=3: {"status": "ok", "device": "cuda", "cudaAvailable": True})
    monkeypatch.setattr(manager, "_sync", lambda: calls.append("sync"))
    assert manager.ensure() == {"status": "ok", "device": "cuda", "cudaAvailable": True}
    assert calls == []


def test_preflight_rejects_missing_secret_without_exposing_values(monkeypatch):
    monkeypatch.setattr(manager, "_powershell", lambda *args, **kwargs: '{"uv":true,"gpu":true,"provider":true,"commit":"%s","env":{"HF_TOKEN":false}}' % manager.PROVIDER_COMMIT)
    try:
        manager._preflight()
    except manager.OperatorError as error:
        assert error.code == "AUDIO_HOST_ENV_MISSING"
        assert "HF_TOKEN" in error.detail
    else:
        raise AssertionError("missing environment must fail")


def test_stop_targets_only_sfx_port(monkeypatch):
    commands = []
    monkeypatch.setattr(manager, "_powershell", lambda script, **kwargs: commands.append(script) or "")
    manager.stop()
    assert len(commands) == 1
    assert "10188" in commands[0]
    assert "9888" not in commands[0]
    assert "9889" not in commands[0]
