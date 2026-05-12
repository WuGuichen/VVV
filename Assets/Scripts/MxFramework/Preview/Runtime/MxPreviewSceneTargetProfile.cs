using System;
using UnityEngine;

namespace MxFramework.Preview
{
    [CreateAssetMenu(menuName = "MxFramework/Preview/Scene Target Profile")]
    public sealed class MxPreviewSceneTargetProfile : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Config/MxFramework/Preview/RuntimeVerticalSlicePreviewTargets.asset";

        [SerializeField] private bool _enabled = true;
        [SerializeField] private MxPreviewSceneTargetDefinition[] _targets =
        {
            new MxPreviewSceneTargetDefinition("TestTarget", 1000, 100, 20, true, false),
            new MxPreviewSceneTargetDefinition("TestCaster", 1000, 100, 20, true, false)
        };

        public bool Enabled => _enabled;
        public MxPreviewSceneTargetDefinition[] Targets => _targets ?? Array.Empty<MxPreviewSceneTargetDefinition>();

        public static MxPreviewSceneTargetProfile CreateDefault()
        {
            var profile = CreateInstance<MxPreviewSceneTargetProfile>();
            profile.name = "RuntimeVerticalSlicePreviewTargetsRuntimeDefault";
            return profile;
        }

        public static MxPreviewSceneTargetProfile LoadDefault()
        {
            return CreateDefault();
        }
    }

    [Serializable]
    public sealed class MxPreviewSceneTargetDefinition
    {
        [SerializeField] private string _targetId = "TestTarget";
        [SerializeField] private int _initialHp = 1000;
        [SerializeField] private int _initialAttack = 100;
        [SerializeField] private int _initialDefense = 20;
        [SerializeField] private bool _resetOnPreviewRun = true;
        [SerializeField] private bool _showOverlay = true;
        [SerializeField] private bool _createRuntimeTarget = true;

        public MxPreviewSceneTargetDefinition() { }

        public MxPreviewSceneTargetDefinition(
            string targetId,
            int initialHp,
            int initialAttack,
            int initialDefense,
            bool resetOnPreviewRun,
            bool showOverlay)
        {
            _targetId = targetId;
            _initialHp = initialHp;
            _initialAttack = initialAttack;
            _initialDefense = initialDefense;
            _resetOnPreviewRun = resetOnPreviewRun;
            _showOverlay = showOverlay;
            _createRuntimeTarget = true;
        }

        public string TargetId => string.IsNullOrEmpty(_targetId) ? "TestTarget" : _targetId;
        public int InitialHp => _initialHp;
        public int InitialAttack => _initialAttack;
        public int InitialDefense => _initialDefense;
        public bool ResetOnPreviewRun => _resetOnPreviewRun;
        public bool ShowOverlay => _showOverlay;
        public bool ShouldCreateRuntimeTarget => _createRuntimeTarget;

        public MxPreviewSceneTarget CreateRuntimeTarget(Transform parent)
        {
            var go = new GameObject("RuntimePreview_" + TargetId);
            if (parent != null)
                go.transform.SetParent(parent, false);
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
    }
}
