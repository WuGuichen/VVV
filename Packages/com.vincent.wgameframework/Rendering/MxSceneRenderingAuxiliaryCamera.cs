using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Rendering
{
    /// <summary>Explicit secondary view of a scene camera. Shares its world lighting, not its screen textures.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class MxSceneRenderingAuxiliaryCamera : MonoBehaviour
    {
        private static readonly Dictionary<Camera, MxSceneRenderingAuxiliaryCamera> Cameras = new Dictionary<Camera, MxSceneRenderingAuxiliaryCamera>();
        public Camera SourceCamera;
        public MxCameraRenderKind Kind = MxCameraRenderKind.Reflection;
        public bool InvertCulling;
        private Camera _camera;
        private void OnEnable() { _camera = GetComponent<Camera>(); Cameras[_camera] = this; }
        private void OnDisable() { if (!ReferenceEquals(_camera, null)) Cameras.Remove(_camera); }
        /// <summary>NoAlloc: a disabled or detached auxiliary camera is never admitted.</summary>
        public static bool Matches(Camera camera, Camera source) => TryGet(camera, out var value) && source != null &&
            value.SourceCamera == source && camera.gameObject.scene == source.gameObject.scene;
        public static bool TryGet(Camera camera, out MxSceneRenderingAuxiliaryCamera value)
        {
            value = null;
            return camera != null && Cameras.TryGetValue(camera, out value) && value != null && value.isActiveAndEnabled;
        }
    }
}
