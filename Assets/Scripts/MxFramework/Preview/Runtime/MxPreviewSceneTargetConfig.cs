using UnityEngine;

namespace MxFramework.Preview
{
    /// <summary>
    /// Edit-time configuration for a preview target.
    /// ScenePreviewWorld creates the runtime MxPreviewSceneTarget from this config when needed.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Preview/Scene Target Config")]
    public sealed class MxPreviewSceneTargetConfig : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _targetId = "TestTarget";

        [Header("Initial Stats")]
        [SerializeField] private int _initialHp = 1000;
        [SerializeField] private int _initialAttack = 100;
        [SerializeField] private int _initialDefense = 20;

        [Header("Runtime Behavior")]
        [SerializeField] private bool _resetOnPreviewRun = true;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private bool _createRuntimeTarget = true;

        public string TargetId => string.IsNullOrEmpty(_targetId) ? "TestTarget" : _targetId;
        public int InitialHp => _initialHp;
        public int InitialAttack => _initialAttack;
        public int InitialDefense => _initialDefense;
        public bool ResetOnPreviewRun => _resetOnPreviewRun;
        public bool ShowOverlay => _showOverlay;
        public bool ShouldCreateRuntimeTarget => _createRuntimeTarget;

        public MxPreviewSceneTarget CreateRuntimeTarget()
        {
            var go = new GameObject("RuntimePreview_" + TargetId);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.hideFlags = HideFlags.DontSave;

            var target = go.AddComponent<MxPreviewSceneTarget>();
            target.Configure(
                TargetId,
                _initialHp,
                _initialAttack,
                _initialDefense,
                _resetOnPreviewRun,
                _showOverlay);
            return target;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = TargetId == "TestCaster" ? new Color(0.35f, 0.75f, 1f, 0.9f) : new Color(1f, 0.45f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.45f, transform.position + Vector3.right * 0.45f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.45f, transform.position + Vector3.up * 0.45f);
        }
    }
}
