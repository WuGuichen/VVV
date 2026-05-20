using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Bone Runtime Sync")]
    public sealed class CharacterBoneRuntimeSync : MonoBehaviour
    {
        [SerializeField] private CharacterBoneRuntimeSyncBinding[] _bindings = Array.Empty<CharacterBoneRuntimeSyncBinding>();

        public IReadOnlyList<CharacterBoneRuntimeSyncBinding> Bindings => _bindings;

        private void LateUpdate()
        {
            ApplyBindings();
        }

        public void ApplyBindings()
        {
            for (int i = 0; i < _bindings.Length; i++)
                _bindings[i]?.Apply();
        }
    }

    [Serializable]
    public sealed class CharacterBoneRuntimeSyncBinding
    {
        [SerializeField] private string _bindingId = string.Empty;
        [SerializeField] private string _bonePath = string.Empty;
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _bone;
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;

        public string BindingId => _bindingId;
        public string BonePath => _bonePath;
        public Transform Target => _target;
        public Transform Bone => _bone;

        public void Apply()
        {
            if (_target == null || _bone == null)
                return;

            _target.position = _bone.TransformPoint(_localPosition);
            _target.rotation = _bone.rotation * _localRotation;
            _target.localScale = _localScale == Vector3.zero ? Vector3.one : _localScale;
        }
    }
}
