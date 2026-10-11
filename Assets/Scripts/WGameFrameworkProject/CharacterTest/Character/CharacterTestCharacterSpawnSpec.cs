using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.CharacterApplication;
using MxFramework.Resources;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Attribute values the character package actually authored (via its imported attribute profile).
    /// <see cref="IsAvailable"/> is false when the package declares none, so callers never present an
    /// invented default as if it were authored data.
    /// </summary>
    public readonly struct CharacterTestCharacterAttributes
    {
        public CharacterTestCharacterAttributes(int hp, int attack, int defense)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
        }

        public int Hp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public bool IsAvailable => Hp > 0 || Attack > 0 || Defense > 0;
    }

    public readonly struct CharacterTestCharacterDefinition
    {
        public CharacterTestCharacterDefinition(
            int characterId,
            string displayName,
            ResourceKey prefabKey,
            int defaultTeamId,
            CharacterRuntimeAnimationBinding animationBinding)
            : this(
                characterId,
                displayName,
                prefabKey,
                defaultTeamId,
                animationBinding,
                CharacterTestCharacterPhysicsComponent.Default,
                string.Empty)
        {
        }

        public CharacterTestCharacterDefinition(
            int characterId,
            string displayName,
            ResourceKey prefabKey,
            int defaultTeamId,
            CharacterRuntimeAnimationBinding animationBinding,
            CharacterTestCharacterPhysicsComponent physicsProfile)
            : this(
                characterId,
                displayName,
                prefabKey,
                defaultTeamId,
                animationBinding,
                physicsProfile,
                string.Empty)
        {
        }

        public CharacterTestCharacterDefinition(
            int characterId,
            string displayName,
            ResourceKey prefabKey,
            int defaultTeamId,
            CharacterRuntimeAnimationBinding animationBinding,
            string generatedPackageRoot)
            : this(
                characterId,
                displayName,
                prefabKey,
                defaultTeamId,
                animationBinding,
                CharacterTestCharacterPhysicsComponent.Default,
                generatedPackageRoot)
        {
        }

        public CharacterTestCharacterDefinition(
            int characterId,
            string displayName,
            ResourceKey prefabKey,
            int defaultTeamId,
            CharacterRuntimeAnimationBinding animationBinding,
            CharacterTestCharacterPhysicsComponent physicsProfile,
            string generatedPackageRoot = "",
            CharacterGameplayCameraProfile gameplayCameraProfile = null,
            CharacterTestCharacterAttributes attributes = default,
            CharacterTacticalDefaults tacticalDefaults = null,
            CharacterPresentationImages presentationImages = null)
        {
            CharacterId = characterId;
            DisplayName = displayName ?? string.Empty;
            PrefabKey = prefabKey;
            DefaultTeamId = defaultTeamId;
            AnimationBinding = animationBinding;
            PhysicsProfile = physicsProfile.IsConfigured
                ? physicsProfile
                : CharacterTestCharacterPhysicsComponent.Default;
            GeneratedPackageRoot = generatedPackageRoot ?? string.Empty;
            GameplayCameraProfile = gameplayCameraProfile;
            Attributes = attributes;
            TacticalDefaults = tacticalDefaults;
            PresentationImages = presentationImages;
        }

        public int CharacterId { get; }
        public string DisplayName { get; }
        public ResourceKey PrefabKey { get; }
        public int DefaultTeamId { get; }
        public CharacterRuntimeAnimationBinding AnimationBinding { get; }
        public CharacterTestCharacterPhysicsComponent PhysicsProfile { get; }
        public string GeneratedPackageRoot { get; }
        public CharacterGameplayCameraProfile GameplayCameraProfile { get; }
        /// <summary>Authored by the package; <see cref="CharacterTestCharacterAttributes.IsAvailable"/> guards absence.</summary>
        public CharacterTestCharacterAttributes Attributes { get; }
        public CharacterTacticalDefaults TacticalDefaults { get; }
        public CharacterPresentationImages PresentationImages { get; }
    }

    public sealed class CharacterTestCharacterDefinitionRegistry
    {
        public const int DefaultPlayableCharacterId = 668367834;
        public const int IronVanguardCharacterId = DefaultPlayableCharacterId;
        private readonly List<CharacterTestCharacterDefinition> _definitions;

        /// <summary>
        /// Copies the cached playable definitions without reloading imported character packages.
        /// AllocByDesign: allocates the registry-owned list; package discovery is shared until explicit refresh.
        /// </summary>
        public CharacterTestCharacterDefinitionRegistry()
            : this(CharacterTestPlayableCharacterCatalog.Discover())
        {
        }

        internal CharacterTestCharacterDefinitionRegistry(
            IReadOnlyList<CharacterTestCharacterDefinition> definitions)
        {
            _definitions = new List<CharacterTestCharacterDefinition>(definitions?.Count ?? 1);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = definitions[i];
                if (!ContainsCharacterId(definition.CharacterId))
                    _definitions.Add(definition);
            }

        }

        /// <summary>NoAlloc: returns the registry-owned read-only definition view.</summary>
        public IReadOnlyList<CharacterTestCharacterDefinition> Definitions => _definitions;

        /// <summary>NoAlloc: returns the discovered definition count.</summary>
        public int Count => _definitions.Count;

        public bool TryGet(int characterId, out CharacterTestCharacterDefinition definition)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                if (_definitions[i].CharacterId == characterId)
                {
                    definition = _definitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        /// <summary>
        /// Selects the previous or next playable definition with wraparound.
        /// NoAlloc after registry construction.
        /// </summary>
        public bool TrySelectNext(int currentCharacterId, int direction, out int selectedCharacterId)
        {
            selectedCharacterId = 0;
            if (_definitions.Count == 0)
                return false;

            int currentIndex = -1;
            for (int i = 0; i < _definitions.Count; i++)
            {
                if (_definitions[i].CharacterId == currentCharacterId)
                {
                    currentIndex = i;
                    break;
                }
            }

            int step = direction < 0 ? -1 : 1;
            int selectedIndex = currentIndex < 0
                ? 0
                : (currentIndex + step + _definitions.Count) % _definitions.Count;
            selectedCharacterId = _definitions[selectedIndex].CharacterId;
            return true;
        }

        internal static CharacterTestCharacterPhysicsComponent CreatePhysicsProfile(
            CharacterImportedPackage package)
        {
            CharacterBodyGeometryRuntimeProfile bodyProfile = package?.Geometry?.BodyProfile;
            if (bodyProfile == null)
                return CharacterTestCharacterPhysicsComponent.Default;

            float capsuleRadius = bodyProfile.CapsuleRadius > 0f
                ? bodyProfile.CapsuleRadius
                : bodyProfile.RadiusMeters;
            float capsuleHeight = bodyProfile.CapsuleHeight > 0f
                ? bodyProfile.CapsuleHeight
                : bodyProfile.HeightMeters;
            float capsuleCenterY = bodyProfile.CapsuleCenter.Y > 0f
                ? bodyProfile.CapsuleCenter.Y
                : capsuleHeight * 0.5f;
            float massKg = bodyProfile.MassKg > 0f
                ? bodyProfile.MassKg
                : CharacterTestCharacterPhysicsComponent.DefaultMassKg;

            return new CharacterTestCharacterPhysicsComponent(
                capsuleRadius,
                capsuleHeight,
                capsuleCenterY,
                massKg);
        }

        private bool ContainsCharacterId(int characterId)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                if (_definitions[i].CharacterId == characterId)
                    return true;
            }

            return false;
        }
    }

    public readonly struct CharacterTestCharacterSpawnSpec
    {
        public CharacterTestCharacterSpawnSpec(
            int characterId,
            int teamId,
            float x,
            float y,
            float z,
            CharacterTestCharacterControlOwner controlOwner = CharacterTestCharacterControlOwner.LocalPlayer,
            string runtimeAiPlannerProfilePath = "",
            int runtimeAiPlannerProfileSlot = 0,
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection = default,
            string instanceStableId = "")
        {
            CharacterId = characterId;
            TeamId = teamId;
            X = x;
            Y = y;
            Z = z;
            ControlOwner = controlOwner;
            RuntimeAiPlannerProfilePath = runtimeAiPlannerProfilePath ?? string.Empty;
            RuntimeAiBehaviorSelection = runtimeAiBehaviorSelection;
            InstanceStableId = instanceStableId ?? string.Empty;
            RuntimeAiPlannerProfileSlot = runtimeAiBehaviorSelection.HasSelection
                ? runtimeAiBehaviorSelection.ProfileSlot
                : runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
        }

        public string InstanceStableId { get; }
        public int CharacterId { get; }
        public int TeamId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public CharacterTestCharacterControlOwner ControlOwner { get; }
        public string RuntimeAiPlannerProfilePath { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public CharacterTestRuntimeAiBehaviorSelection RuntimeAiBehaviorSelection { get; }

        public CharacterTestCharacterSpawnSpec WithRuntimeAiPlannerProfileSlot(int slot)
        {
            CharacterTestRuntimeAiBehaviorSelection selection = RuntimeAiBehaviorSelection.HasSelection
                ? new CharacterTestRuntimeAiBehaviorSelection(
                    RuntimeAiBehaviorSelection.LoadoutId,
                    RuntimeAiBehaviorSelection.VariantId,
                    RuntimeAiBehaviorSelection.CatalogHash,
                    slot)
                : RuntimeAiBehaviorSelection;
            return new CharacterTestCharacterSpawnSpec(
                CharacterId,
                TeamId,
                X,
                Y,
                Z,
                ControlOwner,
                RuntimeAiPlannerProfilePath,
                slot,
                selection, InstanceStableId);
        }

        public CharacterTestCharacterSpawnSpec WithRuntimeAiBehaviorSelection(
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            return new CharacterTestCharacterSpawnSpec(
                CharacterId,
                TeamId,
                X,
                Y,
                Z,
                ControlOwner,
                RuntimeAiPlannerProfilePath,
                RuntimeAiPlannerProfileSlot,
                selection, InstanceStableId);
        }

        public static CharacterTestCharacterSpawnSpec TemporaryPlayer =>
            new CharacterTestCharacterSpawnSpec(
                characterId: CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                teamId: 1,
                x: 0f,
                y: 1f,
                z: 0f,
                controlOwner: CharacterTestCharacterControlOwner.LocalPlayer);

        public static CharacterTestCharacterSpawnSpec TemporaryCompanion =>
            new CharacterTestCharacterSpawnSpec(
                characterId: CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                teamId: 1,
                x: 2f,
                y: 1f,
                z: 1f,
                controlOwner: CharacterTestCharacterControlOwner.None);

        public static CharacterTestCharacterSpawnSpec TemporaryAiOpponent =>
            new CharacterTestCharacterSpawnSpec(
                characterId: CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                teamId: 2,
                x: -0.25f,
                y: 1f,
                z: 4f,
                controlOwner: CharacterTestCharacterControlOwner.None);

        public static CharacterTestCharacterSpawnSpec TemporaryAiOpponentNearby =>
            new CharacterTestCharacterSpawnSpec(
                characterId: CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                teamId: 2,
                x: 1.25f,
                y: 1f,
                z: 4f,
                controlOwner: CharacterTestCharacterControlOwner.None);
    }
}
