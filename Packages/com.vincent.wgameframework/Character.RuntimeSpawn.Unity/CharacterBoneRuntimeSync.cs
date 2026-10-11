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
        [SerializeField] private bool _useExternalRuntimeTick;

        public IReadOnlyList<CharacterBoneRuntimeSyncBinding> Bindings => _bindings;
        public bool UseExternalRuntimeTick
        {
            get => _useExternalRuntimeTick;
            set => _useExternalRuntimeTick = value;
        }

        private void LateUpdate()
        {
            if (_useExternalRuntimeTick)
                return;

            ApplyBindings();
        }

        public void ApplyBindings()
        {
            for (int i = 0; i < _bindings.Length; i++)
                _bindings[i]?.Apply();
        }

        /// <summary>
        /// LowFreqAlloc: replaces generated bone-follow bindings for an instantiated character.
        /// </summary>
        public void Configure(CharacterBoneRuntimeSyncBinding[] bindings, bool useExternalRuntimeTick)
        {
            _bindings = bindings ?? Array.Empty<CharacterBoneRuntimeSyncBinding>();
            _useExternalRuntimeTick = useExternalRuntimeTick;
        }
    }

    [Serializable]
    public sealed class CharacterBoneRuntimeSyncBinding
    {
        [SerializeField] private string _bindingId = string.Empty;
        [SerializeField] private int _socketId;
        [SerializeField] private string _bonePath = string.Empty;
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _bone;
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Quaternion _localRotation = Quaternion.identity;
        [SerializeField] private Vector3 _localScale = Vector3.one;

        public string BindingId => _bindingId;
        public int SocketId => _socketId;
        public string BonePath => _bonePath;
        public Transform Target => _target;
        public Transform Bone => _bone;

        /// <summary>
        /// LowFreqAlloc: fills a runtime-created bone-follow binding from stored local offsets.
        /// </summary>
        public void Configure(
            string bindingId,
            string bonePath,
            Transform target,
            Transform bone,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            int socketId = 0)
        {
            if (socketId < 0) throw new ArgumentOutOfRangeException(nameof(socketId));
            if ((bindingId ?? string.Empty).StartsWith("socket:", StringComparison.Ordinal) && socketId <= 0)
                throw new ArgumentException("Socket-follow bindings require their authored numeric socket identity.", nameof(socketId));
            _socketId = socketId;
            _bindingId = bindingId ?? string.Empty;
            _bonePath = bonePath ?? string.Empty;
            _target = target;
            _bone = bone;
            _localPosition = localPosition;
            _localRotation = localRotation;
            _localScale = localScale == Vector3.zero ? Vector3.one : localScale;
        }

        /// <summary>
        /// LowFreqAlloc: creates a binding that preserves the target's current world-space offset from a bone.
        /// </summary>
        public static CharacterBoneRuntimeSyncBinding FromWorld(
            string bindingId,
            string bonePath,
            Transform target,
            Transform bone,
            int socketId = 0)
        {
            var binding = new CharacterBoneRuntimeSyncBinding();
            if (bone == null || target == null)
                return binding;

            binding.Configure(
                bindingId,
                bonePath,
                target,
                bone,
                Quaternion.Inverse(bone.rotation) * (target.position - bone.position),
                Quaternion.Inverse(bone.rotation) * target.rotation,
                target.localScale, socketId);
            return binding;
        }

        public void Apply()
        {
            if (_target == null || _bone == null)
                return;

            _target.position = _bone.position + (_bone.rotation * _localPosition);
            _target.rotation = _bone.rotation * _localRotation;
            _target.localScale = _localScale == Vector3.zero ? Vector3.one : _localScale;
        }
    }
}
