using System;
using MxFramework.Editor.Authoring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    /// <summary>CharacterAction adapter over the shared isolated authoring preview scene.</summary>
    internal sealed class CharacterActionPreviewRenderScene : ICharacterActionPreviewObjectHost
    {
        private readonly AuthoringPreviewRenderScene _scene;
        public CharacterActionPreviewRenderScene(Action requestRepaint = null) => _scene = new AuthoringPreviewRenderScene(requestRepaint);
        public CharacterActionPreviewSurfaceMode SurfaceMode => CharacterActionPreviewSurfaceMode.Embedded;
        public bool IsDisposed => _scene.IsDisposed;
        public int RootCount => _scene.RootCount;
        public Vector2 OrbitAngles => _scene.OrbitAngles;
        public Vector3 Pivot => _scene.Pivot;
        public float Distance => _scene.Distance;
        public Camera Camera => _scene.Camera;
        public Scene PreviewScene => _scene.PreviewScene;
        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation) => _scene.Instantiate(prefab, position, rotation);
        public void Destroy(GameObject instance) => _scene.Destroy(instance);
        public void RequestRepaint() => _scene.RequestRepaint();
        public void ResetCamera() => _scene.ResetCamera();
        public void Orbit(Vector2 pointerDelta) => _scene.Orbit(pointerDelta);
        public void Pan(Vector2 pointerDelta) => _scene.Pan(pointerDelta);
        public void Zoom(float wheelDelta) => _scene.Zoom(wheelDelta);
        public bool AutoFrame(GameObject preferredRoot = null) => _scene.AutoFrame(preferredRoot);
        public void Draw(Rect rect, Action<Camera> drawReadOnlyOverlays) => _scene.Draw(rect, drawReadOnlyOverlays);
        public void Dispose() => _scene.Dispose();
    }
}
