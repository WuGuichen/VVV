using System;
using System.Collections.Generic;
using MagicaCloth2;
using MxFramework.CharacterCloth;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity
{
    /// <summary>
    /// Maps the framework cloth states onto one or more Magica Cloth components.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Cloth/Magica Cloth Controller")]
    public sealed class MagicaCharacterClothController : MonoBehaviour, ICharacterClothController
    {
        [SerializeField] private CharacterClothState _requestedState = CharacterClothState.Running;
        [SerializeField] private bool _autoCollectFromChildren = true;
        [SerializeField] private bool _includeInactive = true;
        [SerializeField] private MagicaCloth[] _clothComponents = Array.Empty<MagicaCloth>();

        [NonSerialized] private CharacterClothSystemOverride _systemOverride;
        [NonSerialized] private MagicaCharacterClothSystem _owningSystem;
        private readonly Dictionary<MagicaCloth, bool> _pendingResets = new Dictionary<MagicaCloth, bool>();
        private bool _hasCollisionQuality;
        private ColliderCollisionConstraint.Mode _colliderMode;
        private SelfCollisionConstraint.SelfCollisionMode _selfMode;

        /// <inheritdoc />
        public CharacterClothState RequestedState => _requestedState;

        /// <inheritdoc />
        public CharacterClothState EffectiveState =>
            CharacterClothStateResolver.Resolve(_requestedState, _systemOverride);

        /// <inheritdoc />
        public CharacterClothSystemOverride SystemOverride => _systemOverride;

        /// <summary>Gets the Magica components controlled by this adapter.</summary>
        public IReadOnlyList<MagicaCloth> ClothComponents => _clothComponents;

        /// <summary>Gets the number of configured Magica components, including missing entries.</summary>
        public int ClothComponentCount => _clothComponents.Length;

        internal MagicaCharacterClothSystem OwningSystem => _owningSystem;

        private void Awake()
        {
            if (_autoCollectFromChildren)
            {
                RefreshClothComponents();
                return;
            }

            EnsureComponentArray();
            SubscribeToBuildEvents();
            ApplyEffectiveState();
        }

        private void OnEnable()
        {
            ApplyEffectiveState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromBuildEvents();
            _pendingResets.Clear();

            MagicaCharacterClothSystem owner = _owningSystem;
            _owningSystem = null;
            if (owner != null)
                owner.NotifyControllerDestroyed(this);
        }

        private void OnValidate()
        {
            EnsureComponentArray();
        }

        /// <inheritdoc />
        public void SetRequestedState(CharacterClothState state)
        {
            CharacterClothStateResolver.Resolve(state, _systemOverride);
            if (_requestedState == state)
                return;

            _requestedState = state;
            ApplyEffectiveState();
        }

        /// <inheritdoc />
        public void SetSystemOverride(CharacterClothSystemOverride systemOverride)
        {
            CharacterClothStateResolver.Resolve(_requestedState, systemOverride);
            if (_systemOverride == systemOverride)
                return;

            _systemOverride = systemOverride;
            ApplyEffectiveState();
        }

        /// <inheritdoc />
        /// <remarks>LowFreqAlloc when a reset must wait for asynchronous build completion.</remarks>
        public void Reset(bool keepPose = false)
        {
            EnsureComponentArray();
            for (int i = 0; i < _clothComponents.Length; i++)
            {
                MagicaCloth cloth = _clothComponents[i];
                if (cloth == null || !IsFirstOccurrence(i)) continue;
                if (cloth.IsValid()) cloth.ResetCloth(keepPose);
                else _pendingResets[cloth] = keepPose;
            }
        }

        /// <summary>
        /// Replaces the controlled Magica components and the local requested state.
        /// </summary>
        /// <remarks>LowFreqAlloc when the caller creates the component array.</remarks>
        public void Configure(MagicaCloth[] clothComponents, CharacterClothState requestedState)
        {
            CharacterClothStateResolver.Resolve(requestedState, _systemOverride);

            UnsubscribeFromBuildEvents();
            _pendingResets.Clear();
            _autoCollectFromChildren = false;
            _clothComponents = clothComponents ?? Array.Empty<MagicaCloth>();
            _requestedState = requestedState;
            SubscribeToBuildEvents();
            ApplyEffectiveState();
            ReapplyCollisionQuality();
        }

        /// <summary>
        /// Rebuilds the controlled component list from this object and its children.
        /// </summary>
        /// <remarks>LowFreqAlloc through Unity component discovery.</remarks>
        public void RefreshClothComponents()
        {
            UnsubscribeFromBuildEvents();
            _clothComponents = GetComponentsInChildren<MagicaCloth>(_includeInactive);
            if (_pendingResets.Count > 0)
            {
                var pending = new List<MagicaCloth>(_pendingResets.Keys);
                for (int i = 0; i < pending.Count; i++)
                    if (Array.IndexOf(_clothComponents, pending[i]) < 0) _pendingResets.Remove(pending[i]);
            }
            SubscribeToBuildEvents();
            ApplyEffectiveState();
            ReapplyCollisionQuality();
        }

        /// <summary>
        /// Reapplies the resolved framework state to every configured Magica component.
        /// </summary>
        /// <remarks>NoAlloc after component discovery.</remarks>
        public void ApplyEffectiveState()
        {
            EnsureComponentArray();
            CharacterClothState state = EffectiveState;
            for (int i = 0; i < _clothComponents.Length; i++)
            {
                MagicaCloth cloth = _clothComponents[i];
                if (cloth != null && IsFirstOccurrence(i))
                    ApplyState(cloth, state);
            }
        }

        internal bool TryAttachSystem(MagicaCharacterClothSystem system)
        {
            if (system == null)
                return false;
            if (_owningSystem != null && !ReferenceEquals(_owningSystem, system))
                return false;

            _owningSystem = system;
            return true;
        }

        internal void DetachSystem(MagicaCharacterClothSystem system)
        {
            if (ReferenceEquals(_owningSystem, system))
                _owningSystem = null;
        }

        private void HandleBuildComplete(MagicaCloth cloth, bool succeeded)
        {
            if (!succeeded || cloth == null)
                return;

            ApplyState(cloth, EffectiveState);
            if (_hasCollisionQuality) ApplyCollisionQuality(cloth);
            if (_pendingResets.TryGetValue(cloth, out bool keepPose))
            {
                _pendingResets.Remove(cloth);
                cloth.ResetCloth(keepPose);
            }
        }

        /// <summary>Changes only supported runtime collision modes on this instance.</summary>
        /// <remarks>LowFreqAlloc if the backend grows its parameter/collision bookkeeping.
        /// Call on the Unity main thread; no cloth rebuild or authored resource change.</remarks>
        public void SetCollisionQuality(ColliderCollisionConstraint.Mode colliderMode,
            SelfCollisionConstraint.SelfCollisionMode selfMode)
        {
            if (colliderMode < ColliderCollisionConstraint.Mode.None || colliderMode > ColliderCollisionConstraint.Mode.Edge)
                throw new ArgumentOutOfRangeException(nameof(colliderMode));
            if (selfMode != SelfCollisionConstraint.SelfCollisionMode.None
                && selfMode != SelfCollisionConstraint.SelfCollisionMode.FullMesh)
                throw new ArgumentOutOfRangeException(nameof(selfMode));
            _hasCollisionQuality = true;
            _colliderMode = colliderMode;
            _selfMode = selfMode;
            ReapplyCollisionQuality();
        }

        private void ReapplyCollisionQuality()
        {
            if (!_hasCollisionQuality) return;
            EnsureComponentArray();
            for (int i = 0; i < _clothComponents.Length; i++)
            {
                MagicaCloth cloth = _clothComponents[i];
                if (cloth == null || !IsFirstOccurrence(i)) continue;
                ApplyCollisionQuality(cloth);
            }
        }

        private void ApplyCollisionQuality(MagicaCloth cloth)
        {
            cloth.SerializeData.colliderCollisionConstraint.mode = _colliderMode;
            cloth.SerializeData.selfCollisionConstraint.selfMode = _selfMode;
            cloth.SetParameterChange();
        }

        private static void ApplyState(MagicaCloth cloth, CharacterClothState state)
        {
            switch (state)
            {
                case CharacterClothState.Running:
                    EnableSimulation(cloth, 1f);
                    break;
                case CharacterClothState.Paused:
                    EnableSimulation(cloth, 0f);
                    break;
                case CharacterClothState.DisabledReset:
                    DisableSimulation(cloth, ClothDisableMode.Reset);
                    break;
                case CharacterClothState.DisabledKeep:
                    DisableSimulation(cloth, ClothDisableMode.Keep);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown character cloth state.");
            }
        }

        private static void EnableSimulation(MagicaCloth cloth, float timeScale)
        {
            if (!cloth.enabled)
                cloth.enabled = true;

            if (!cloth.IsValid())
                return;

            cloth.SetSkipWriting(false);
            cloth.SetTimeScale(timeScale);
        }

        private static void DisableSimulation(MagicaCloth cloth, ClothDisableMode disableMode)
        {
            bool modeChanged = cloth.SerializeData.disableMode != disableMode;
            cloth.SerializeData.disableMode = disableMode;
            cloth.SetParameterChange();

            // Re-entering and immediately leaving an already disabled active component makes
            // a Keep -> Reset transition execute Magica's normal disable-path pose restore.
            if (!cloth.enabled && modeChanged && cloth.gameObject.activeInHierarchy)
                cloth.enabled = true;

            cloth.enabled = false;
        }

        private void SubscribeToBuildEvents()
        {
            EnsureComponentArray();
            for (int i = 0; i < _clothComponents.Length; i++)
            {
                MagicaCloth cloth = _clothComponents[i];
                if (cloth == null || !IsFirstOccurrence(i))
                    continue;

                cloth.OnBuildComplete -= HandleBuildComplete;
                cloth.OnBuildComplete += HandleBuildComplete;
            }
        }

        private void UnsubscribeFromBuildEvents()
        {
            EnsureComponentArray();
            for (int i = 0; i < _clothComponents.Length; i++)
            {
                MagicaCloth cloth = _clothComponents[i];
                if (cloth != null && IsFirstOccurrence(i))
                    cloth.OnBuildComplete -= HandleBuildComplete;
            }
        }

        private bool IsFirstOccurrence(int index)
        {
            MagicaCloth candidate = _clothComponents[index];
            for (int i = 0; i < index; i++)
            {
                if (ReferenceEquals(_clothComponents[i], candidate))
                    return false;
            }

            return true;
        }

        private void EnsureComponentArray()
        {
            if (_clothComponents == null)
                _clothComponents = Array.Empty<MagicaCloth>();
        }
    }
}
