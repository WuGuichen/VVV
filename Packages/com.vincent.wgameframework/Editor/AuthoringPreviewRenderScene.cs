using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Editor.Authoring
{
    /// <summary>Shared isolated PreviewRenderUtility scene and camera for Editor authoring surfaces.</summary>
    /// <remarks>AllocByDesign for construction/object changes; Draw and camera navigation are NoAlloc after initialization.</remarks>
    public sealed class AuthoringPreviewRenderScene : IDisposable
    {
        private const float MinimumDistance = 0.35f;
        private const float MaximumDistance = 80f;
        private static readonly Color BackgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
        private readonly PreviewRenderUtility _utility;
        private readonly List<GameObject> _roots = new List<GameObject>(8);
        private readonly Action _requestRepaint;
        private Vector2 _orbit;
        private Vector3 _pivot;
        private float _distance;
        private bool _disposed;

        public AuthoringPreviewRenderScene(Action requestRepaint = null)
        {
            _requestRepaint = requestRepaint;
            _utility = new PreviewRenderUtility();
            _utility.cameraFieldOfView = 32f;
            _utility.camera.nearClipPlane = 0.01f;
            _utility.camera.farClipPlane = 250f;
            _utility.camera.clearFlags = CameraClearFlags.SolidColor;
            _utility.camera.backgroundColor = BackgroundColor;
            _utility.ambientColor = new Color(0.16f, 0.18f, 0.22f, 1f);
            _utility.lights[0].intensity = 1.25f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.65f;
            _utility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 0f);
            ResetCamera();
        }

        public bool IsDisposed => _disposed;
        public int RootCount => _roots.Count;
        public Vector2 OrbitAngles => _orbit;
        public Vector3 Pivot => _pivot;
        public float Distance => _distance;
        public Camera Camera => _disposed ? null : _utility.camera;
        public Scene PreviewScene => _disposed || _utility.camera == null ? default : _utility.camera.scene;

        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AuthoringPreviewRenderScene));
            if (prefab == null) return null;
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.hideFlags = HideFlags.HideAndDontSave;
            return AddOwned(instance);
        }

        public GameObject AddOwned(GameObject instance)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AuthoringPreviewRenderScene));
            if (instance == null) return null;
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                _utility.AddSingleGO(instance);
                _roots.Add(instance);
                return instance;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw;
            }
        }

        public void Destroy(GameObject instance)
        {
            if (instance == null) return;
            _roots.Remove(instance);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        public void RequestRepaint()
        {
            if (!_disposed) _requestRepaint?.Invoke();
        }

        public void ResetCamera()
        {
            if (_disposed) return;
            _orbit = new Vector2(35f, 18f);
            _pivot = new Vector3(0f, 0.9f, 0f);
            _distance = 4.5f;
            RequestRepaint();
        }

        public void Orbit(Vector2 pointerDelta)
        {
            if (_disposed) return;
            _orbit.x = Mathf.Repeat(_orbit.x + pointerDelta.x * 0.5f, 360f);
            _orbit.y = Mathf.Clamp(_orbit.y - pointerDelta.y * 0.5f, -85f, 85f);
            RequestRepaint();
        }

        public void Pan(Vector2 pointerDelta)
        {
            if (_disposed) return;
            Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
            float scale = Mathf.Max(0.0005f, _distance * 0.0018f);
            _pivot += (-(rotation * Vector3.right) * pointerDelta.x + rotation * Vector3.up * pointerDelta.y) * scale;
            RequestRepaint();
        }

        public void Zoom(float wheelDelta)
        {
            if (_disposed) return;
            _distance = Mathf.Clamp(_distance * Mathf.Max(0.1f, 1f + wheelDelta * 0.08f), MinimumDistance, MaximumDistance);
            RequestRepaint();
        }

        public bool AutoFrame(GameObject preferredRoot = null)
        {
            if (_disposed) return false;
            bool hasBounds = false;
            Bounds bounds = default;
            if (preferredRoot != null) EncapsulateRenderers(preferredRoot, ref hasBounds, ref bounds);
            else for (int i = 0; i < _roots.Count; i++) EncapsulateRenderers(_roots[i], ref hasBounds, ref bounds);
            if (!hasBounds) return false;
            _pivot = bounds.center;
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            _distance = Mathf.Clamp(radius / Mathf.Tan(_utility.cameraFieldOfView * Mathf.Deg2Rad * 0.5f) * 1.15f, MinimumDistance, MaximumDistance);
            RequestRepaint();
            return true;
        }

        public void Draw(Rect rect, Action<Camera> drawReadOnlyOverlays)
        {
            if (_disposed || rect.width < 2f || rect.height < 2f || Event.current.type != EventType.Repaint) return;
            _utility.BeginPreview(rect, GUIStyle.none);
            ConfigureCamera(rect);
            _utility.Render(allowScriptableRenderPipeline: true, updatefov: true);
            drawReadOnlyOverlays?.Invoke(_utility.camera);
            Texture result = _utility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _roots.Count - 1; i >= 0; i--)
                if (_roots[i] != null) UnityEngine.Object.DestroyImmediate(_roots[i]);
            _roots.Clear();
            _utility.Cleanup();
        }

        private void ConfigureCamera(Rect rect)
        {
            Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
            Transform cameraTransform = _utility.camera.transform;
            cameraTransform.position = _pivot + rotation * (Vector3.back * _distance);
            cameraTransform.rotation = Quaternion.LookRotation(_pivot - cameraTransform.position, rotation * Vector3.up);
            _utility.camera.aspect = Mathf.Max(0.01f, rect.width / rect.height);
        }

        private static void EncapsulateRenderers(GameObject root, ref bool hasBounds, ref Bounds bounds)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
    }
}
