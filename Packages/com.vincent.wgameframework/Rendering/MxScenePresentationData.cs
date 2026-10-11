using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Rendering
{
    /// <summary>Scene dependency whose runtime resources belong to the scene rendering session.</summary>
    public abstract class MxScenePresentationData : ScriptableObject
    {
        public abstract IMxScenePresentationConsumer CreateConsumer(MxScenePresentationBinding binding, Scene scene, bool editorPreview);
    }

    [Serializable]
    public sealed class MxScenePresentationBinding
    {
        public string OwnerId;
        public MxScenePresentationData Data;
        public Transform Anchor;
        // Optional authoring surface, never a second instance-data authority.
        public Transform SurfaceRoot;
        public bool Enabled = true;
    }

    public interface IMxScenePresentationConsumer : IDisposable
    {
        void Tick();
        void BeginCamera(Camera camera);
        void EndCamera(Camera camera);
    }
}
