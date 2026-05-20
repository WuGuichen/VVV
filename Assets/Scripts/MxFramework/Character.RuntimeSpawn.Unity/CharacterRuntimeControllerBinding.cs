using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Controller Binding")]
    public sealed class CharacterRuntimeControllerBinding : MonoBehaviour
    {
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private int _stableCharacterId;
        [SerializeField] private int _gameplayEntityIndex;
        [SerializeField] private int _gameplayEntityGeneration;
        [SerializeField] private int _combatEntityId;
        [SerializeField] private int _combatBodyId;
        [SerializeField] private string _characterId = string.Empty;
        [SerializeField] private string _spawnProfileId = string.Empty;
        [SerializeField] private string _loadoutId = string.Empty;
        [SerializeField] private string _teamId = string.Empty;
        [SerializeField] private string _controllerKind = string.Empty;
        [SerializeField] private int _colliderCount;
        [SerializeField] private int _weaponAttachmentCount;
        [SerializeField] private int _weaponTraceCount;
        [SerializeField] private int _requiredResourceCount;
        [SerializeField] private int _resolvedResourceCount;
        [SerializeField] private string _sourcePackageHash = string.Empty;
        [SerializeField] private string _resourceMappingHash = string.Empty;
        [SerializeField] private string _debugSummary = string.Empty;
        [SerializeField] private bool _initializeOnAwake = true;

        private CharacterControlStateMachine _stateMachine;

        public string PackageId => _packageId;
        public int StableCharacterId => _stableCharacterId;
        public string CharacterId => _characterId;
        public string SpawnProfileId => _spawnProfileId;
        public string LoadoutId => _loadoutId;
        public string TeamId => _teamId;
        public string ControllerKind => _controllerKind;
        public int ColliderCount => _colliderCount;
        public int WeaponAttachmentCount => _weaponAttachmentCount;
        public int WeaponTraceCount => _weaponTraceCount;
        public int RequiredResourceCount => _requiredResourceCount;
        public int ResolvedResourceCount => _resolvedResourceCount;
        public string SourcePackageHash => _sourcePackageHash;
        public string ResourceMappingHash => _resourceMappingHash;
        public string DebugSummary => _debugSummary;
        public CharacterControlEntityRef EntityRef => CreateEntityRef();
        public CharacterControlStateMachine StateMachine => _stateMachine;
        public bool IsInitialized => _stateMachine != null;

        private void Awake()
        {
            if (_initializeOnAwake)
                Initialize();
        }

        public void ConfigureFromRuntimeBinding(string packageId, CharacterRuntimeBinding binding)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            CharacterControlEntityRef entityRef = binding.EntityRef;
            _packageId = packageId ?? string.Empty;
            _stableCharacterId = entityRef.StableId;
            _gameplayEntityIndex = entityRef.GameplayEntityId.Index;
            _gameplayEntityGeneration = entityRef.GameplayEntityId.Generation;
            _combatEntityId = entityRef.CombatEntityId.Value;
            _combatBodyId = entityRef.CombatBodyId.Value;
            _characterId = binding.SpawnPlan.CharacterId.Value.ToString();
            _spawnProfileId = binding.SpawnPlan.SpawnProfileId.Value.ToString();
            _loadoutId = binding.SpawnPlan.LoadoutId.Value.ToString();
            _teamId = binding.SpawnPlan.TeamId;
            _controllerKind = binding.SpawnPlan.ControllerKind.ToString();
            _colliderCount = binding.CombatBodyBindingPlan == null ? 0 : binding.CombatBodyBindingPlan.Colliders.Length;
            _weaponAttachmentCount = binding.WeaponAttachmentBindingPlan == null ? 0 : binding.WeaponAttachmentBindingPlan.Attachments.Length;
            _weaponTraceCount = binding.WeaponAttachmentBindingPlan == null ? 0 : binding.WeaponAttachmentBindingPlan.Traces.Length;
            _requiredResourceCount = binding.ResourcePreloadPlan == null ? 0 : binding.ResourcePreloadPlan.RequiredResources.Length;
            _resolvedResourceCount = binding.ResourcePreloadPlan == null ? 0 : binding.ResourcePreloadPlan.ResolvedResources.Length;
            _sourcePackageHash = binding.SourcePackageHash;
            _resourceMappingHash = binding.ResourceMappingHash;
            _debugSummary = binding.DebugSummary;
            _stateMachine = null;
        }

        public CharacterControlStateMachine Initialize()
        {
            if (_stateMachine != null)
                return _stateMachine;

            CharacterControlEntityRef entityRef = CreateEntityRef();
            if (!entityRef.IsValid)
            {
                Debug.LogError("MxFramework Character: runtime controller binding has no valid entity reference.", this);
                return null;
            }

            _stateMachine = new CharacterControlStateMachine(entityRef);
            return _stateMachine;
        }

        private CharacterControlEntityRef CreateEntityRef()
        {
            var gameplayEntityId = new GameplayEntityId(_gameplayEntityIndex, _gameplayEntityGeneration);
            var combatEntityId = new CombatEntityId(_combatEntityId);
            var combatBodyId = new CombatBodyId(_combatBodyId);
            return CharacterControlEntityRef.FromGameplayAndCombat(
                gameplayEntityId,
                combatEntityId,
                combatBodyId,
                _stableCharacterId);
        }
    }
}
