using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Owns one isolated, continuously rendered Development Agent candidate preview.</summary>
    /// <remarks>Editor-only, AllocByDesign during selection. Playback updates do not create managed collections.</remarks>
    internal sealed class VfxAgentCandidateLivePreview : IDisposable
    {
        private PreviewRenderUtility _preview;
        private GameObject _instance;
        private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
        private string _assetPath = string.Empty;
        private double _lastTime;
        private bool _playing;
        private bool _loop;
        private float _speed = 1f;

        public string AssetPath => _assetPath;
        public bool IsPlaying => _playing;

        public bool Select(string assetPath)
        {
            DisposePreview();
            _assetPath = assetPath ?? string.Empty;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
            if (prefab == null) return false;
            _preview = new PreviewRenderUtility();
            _instance = _preview.InstantiatePrefabInScene(prefab);
            _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].useAutoRandomSeed = false;
                _particles[i].randomSeed = unchecked(17u + (uint)i * 7919u);
            }
            ConfigureCamera();
            Restart();
            return true;
        }

        public void Play() { if (_instance == null) return; _playing = true; _lastTime = EditorApplication.timeSinceStartup; }
        public void Pause() => _playing = false;
        public void SetLoop(bool loop) => _loop = loop;
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 0.05f, 4f);

        public void Restart()
        {
            if (_instance == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particles[i].Simulate(0f, true, true, true);
                _particles[i].Play(true);
            }
            _playing = true;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        public void Draw(Rect rect, string label)
        {
            EditorGUI.DrawRect(rect, new Color(0.012f, 0.016f, 0.021f, 1f));
            if (_preview == null || _instance == null)
            {
                GUI.Label(rect, string.IsNullOrEmpty(label) ? "请选择已完成候选" : label, EditorStyles.centeredGreyMiniLabel);
                return;
            }
            Advance();
            Rect renderRect = new Rect(rect.x, rect.y + 20f, rect.width, Mathf.Max(1f, rect.height - 20f));
            _preview.BeginPreview(renderRect, GUIStyle.none);
            _preview.Render(true, true);
            Texture texture = _preview.EndPreview();
            if (texture != null) GUI.DrawTexture(renderRect, texture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), label + (_playing ? "  ·  播放中" : "  ·  已暂停"), EditorStyles.miniBoldLabel);
        }

        public void Dispose() => DisposePreview();

        private void Advance()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = _playing ? Mathf.Clamp((float)(now - _lastTime) * _speed, 0f, 0.1f) : 0f;
            _lastTime = now;
            if (delta <= 0f) return;
            bool alive = false;
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Simulate(delta, true, false, false);
                alive |= _particles[i].IsAlive(true);
            }
            if (!alive)
            {
                if (_loop) Restart();
                else _playing = false;
            }
        }

        private void ConfigureCamera()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            Renderer[] renderers = _instance.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!found) { bounds = renderers[i].bounds; found = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            float radius = Mathf.Max(1f, bounds.extents.magnitude);
            _preview.camera.transform.position = bounds.center + new Vector3(0.7f, 0.45f, -1f).normalized * radius * 3.2f;
            _preview.camera.transform.LookAt(bounds.center);
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = radius * 15f + 20f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.012f, 0.016f, 0.021f, 1f);
            _preview.cameraFieldOfView = 32f;
            _preview.ambientColor = new Color(0.35f, 0.36f, 0.4f, 1f);
            if (_preview.lights.Length > 0) _preview.lights[0].intensity = 1.1f;
        }

        private void DisposePreview()
        {
            _playing = false;
            _particles = Array.Empty<ParticleSystem>();
            _instance = null;
            _assetPath = string.Empty;
            if (_preview == null) return;
            _preview.Cleanup();
            _preview = null;
        }
    }

    internal sealed class VfxAgentCandidateComparePreview : IDisposable
    {
        private readonly VfxAgentCandidateLivePreview _a = new VfxAgentCandidateLivePreview();
        private readonly VfxAgentCandidateLivePreview _b = new VfxAgentCandidateLivePreview();
        private string _labelA = "A";
        private string _labelB = "B";

        public bool HasAny => !string.IsNullOrEmpty(_a.AssetPath) || !string.IsNullOrEmpty(_b.AssetPath);

        public bool SelectNext(string assetPath, string label)
        {
            if (string.Equals(_a.AssetPath, assetPath, StringComparison.Ordinal) || string.IsNullOrEmpty(_a.AssetPath))
            { _labelA = "A · " + label; return _a.Select(assetPath); }
            _labelB = "B · " + label;
            return _b.Select(assetPath);
        }

        public void Play() { _a.Play(); _b.Play(); }
        public void Pause() { _a.Pause(); _b.Pause(); }
        public void Restart() { _a.Restart(); _b.Restart(); }
        public void SetLoop(bool value) { _a.SetLoop(value); _b.SetLoop(value); }
        public void SetSpeed(float value) { _a.SetSpeed(value); _b.SetSpeed(value); }

        public void Draw(Rect rect)
        {
            float gap = 4f;
            float width = Mathf.Max(1f, (rect.width - gap) * 0.5f);
            _a.Draw(new Rect(rect.x, rect.y, width, rect.height), _labelA);
            _b.Draw(new Rect(rect.x + width + gap, rect.y, width, rect.height), _labelB);
        }

        public void Dispose() { _a.Dispose(); _b.Dispose(); }
    }
}
