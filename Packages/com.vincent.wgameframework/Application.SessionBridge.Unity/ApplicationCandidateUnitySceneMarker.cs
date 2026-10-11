using UnityEngine;

namespace MxFramework.ApplicationStartup.SessionBridge.Unity
{
    /// <summary>Non-authoritative serialized marker required on every candidate scene root.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationCandidateUnitySceneMarker : MonoBehaviour
    {
        [SerializeField] private string markerId = string.Empty;
        [SerializeField] private long revision = 0L;

        /// <remarks>NoAlloc.</remarks>
        public string MarkerId => markerId;

        /// <remarks>NoAlloc.</remarks>
        public long Revision => revision;
    }
}
