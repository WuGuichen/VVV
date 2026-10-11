using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Abstract Bone Runtime Map")]
    public sealed class CharacterAbstractBoneRuntimeMap : MonoBehaviour
    {
        [SerializeField] private CharacterAbstractBoneRuntimeBinding[] _bindings =
            Array.Empty<CharacterAbstractBoneRuntimeBinding>();

        public IReadOnlyList<CharacterAbstractBoneRuntimeBinding> Bindings => _bindings;

        public void Configure(CharacterAbstractBoneRuntimeBinding[] bindings)
        {
            _bindings = bindings ?? Array.Empty<CharacterAbstractBoneRuntimeBinding>();
        }

        public bool TryResolve(string abstractBoneId, out Transform target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(abstractBoneId))
                return false;
            for (int i = 0; i < _bindings.Length; i++)
            {
                CharacterAbstractBoneRuntimeBinding binding = _bindings[i];
                if (binding != null
                    && string.Equals(binding.AbstractBoneId, abstractBoneId, StringComparison.Ordinal)
                    && binding.Target != null)
                {
                    target = binding.Target;
                    return true;
                }
            }
            return false;
        }
    }

    [Serializable]
    public sealed class CharacterAbstractBoneRuntimeBinding
    {
        [SerializeField] private string _abstractBoneId = string.Empty;
        [SerializeField] private int _socketId;
        [SerializeField] private Transform _target;

        public string AbstractBoneId => _abstractBoneId;
        public int SocketId => _socketId;
        public Transform Target => _target;

        public CharacterAbstractBoneRuntimeBinding(string abstractBoneId, int socketId, Transform target)
        {
            _abstractBoneId = abstractBoneId ?? string.Empty;
            _socketId = socketId;
            _target = target;
        }
    }
}
