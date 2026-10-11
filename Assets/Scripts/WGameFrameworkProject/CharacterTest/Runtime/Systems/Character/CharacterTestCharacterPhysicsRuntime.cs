using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCharacterPhysicsRuntime
    {
        public const int StaticBodyIdStart = 10000;
        public const int DynamicBodyIdStart = 20000;
        public const int DynamicCharacterColliderId = 1;
        public const int DynamicCharacterLayer = 8;
        public const int CharacterHurtboxLayer = 10;
        public const int CharacterHurtboxColliderIdStart = 1000;
        public const int CharacterHurtboxBodyIdStart = 50000;
        public const int BodyPresenceBodyIdStart = 100000;
        public const int BodyPresenceColliderIdStart = 1;
        public const int BodyPresenceColliderCapacity = 4;
        public const int BodyPresenceMaximumActiveCharacters = 8;
        public const int BodyPresenceMaximumActiveLeaves =
            BodyPresenceColliderCapacity * BodyPresenceMaximumActiveCharacters;
        public const int BodyPresenceMaximumEntityId =
            CharacterHurtboxBodyIdStart - DynamicBodyIdStart - 1;
        public const int BodyPresenceLayer = 11;
        public const int DynamicMovableObjectBodyIdStart = 900000;
        public const int DynamicMovableObjectColliderId = 2;
        public const int DynamicMovableObjectLayer = 9;
        private readonly Dictionary<int, CharacterColliderSyncState> _characterColliderSyncStates =
            new Dictionary<int, CharacterColliderSyncState>();
        private readonly Dictionary<int, int> _movableObjectColliderSyncVersions =
            new Dictionary<int, int>();
        private readonly Dictionary<int, BodyPresenceColliderSyncState> _bodyPresenceColliderSyncStates =
            new Dictionary<int, BodyPresenceColliderSyncState>(BodyPresenceMaximumActiveCharacters);

        public CharacterTestCharacterPhysicsRuntime(
            CombatPhysicsWorld world,
            CombatPhysicsLayerMask obstacleLayerMask,
            FixVector3 characterHalfExtents,
            Fix64 skinWidth,
            Fix64 maxStepHeight,
            int staticColliderCount,
            string sourceDescription)
            : this(
                world,
                obstacleLayerMask,
                characterHalfExtents,
                skinWidth,
                maxStepHeight,
                staticColliderCount,
                sourceDescription,
                Fix64.Zero)
        {
        }

        public CharacterTestCharacterPhysicsRuntime(
            CombatPhysicsWorld world,
            CombatPhysicsLayerMask obstacleLayerMask,
            FixVector3 characterHalfExtents,
            Fix64 skinWidth,
            Fix64 maxStepHeight,
            int staticColliderCount,
            string sourceDescription,
            Fix64 groundSnapDistance)
        {
            World = world;
            ObstacleLayerMask = obstacleLayerMask;
            StaticObstacleLayerMask = obstacleLayerMask;
            DynamicCharacterObstacleLayerMask = CombatPhysicsLayerMask.FromLayer(DynamicCharacterLayer);
            DynamicMovableObjectObstacleLayerMask = CombatPhysicsLayerMask.FromLayer(DynamicMovableObjectLayer);
            BodyPresenceLayerMask = CombatPhysicsLayerMask.FromLayer(BodyPresenceLayer);
            MotionObstacleLayerMask = obstacleLayerMask
                .AddLayer(DynamicCharacterLayer)
                .AddLayer(DynamicMovableObjectLayer);
            CharacterHalfExtents = characterHalfExtents;
            SkinWidth = skinWidth;
            MaxStepHeight = maxStepHeight < Fix64.Zero ? Fix64.Zero : maxStepHeight;
            GroundSnapDistance = groundSnapDistance < Fix64.Zero ? Fix64.Zero : groundSnapDistance;
            StaticColliderCount = staticColliderCount < 0 ? 0 : staticColliderCount;
            SourceDescription = sourceDescription ?? string.Empty;
        }

        public CombatPhysicsWorld World { get; }
        public CombatPhysicsLayerMask ObstacleLayerMask { get; }
        public CombatPhysicsLayerMask StaticObstacleLayerMask { get; }
        public CombatPhysicsLayerMask DynamicCharacterObstacleLayerMask { get; }
        public CombatPhysicsLayerMask DynamicMovableObjectObstacleLayerMask { get; }
        public CombatPhysicsLayerMask BodyPresenceLayerMask { get; }
        public CombatPhysicsLayerMask MotionObstacleLayerMask { get; }
        public FixVector3 CharacterHalfExtents { get; }
        public Fix64 SkinWidth { get; }
        public Fix64 MaxStepHeight { get; }
        public Fix64 GroundSnapDistance { get; }
        public int StaticColliderCount { get; }
        public string SourceDescription { get; }

        public bool IsReady => World != null && StaticColliderCount > 0;

        internal CharacterTestNavigationSessionBuildInput? NavigationBuildInput { get; private set; }

        internal CharacterTestNavigationSceneInputResult NavigationInputResult { get; private set; }

        internal void SetNavigationBuildInput(
            CharacterTestNavigationSessionBuildInput? navigationBuildInput,
            CharacterTestNavigationSceneInputResult navigationInputResult = default)
        {
            NavigationBuildInput = navigationBuildInput;
            NavigationInputResult = navigationInputResult.Status == CharacterTestNavigationSceneInputStatus.None
                ? new CharacterTestNavigationSceneInputResult(
                    navigationBuildInput.HasValue
                        ? CharacterTestNavigationSceneInputStatus.Success
                        : CharacterTestNavigationSceneInputStatus.None)
                : navigationInputResult;
        }

        internal int SyncedCharacterColliderCount => _characterColliderSyncStates.Count;

        internal int SyncedMovableObjectColliderCount => _movableObjectColliderSyncVersions.Count;

        internal int SyncedBodyPresenceColliderCount => _bodyPresenceColliderSyncStates.Count;

        public CombatBodyId GetBodyId(CharacterTestCharacterEntity entity)
        {
            return entity == null
                ? CombatBodyId.None
                : new CombatBodyId(DynamicBodyIdStart + entity.Id.Value);
        }

        public CombatEntityId GetEntityId(CharacterTestCharacterEntity entity)
        {
            return entity == null
                ? CombatEntityId.None
                : new CombatEntityId(DynamicBodyIdStart + entity.Id.Value);
        }

        public CombatBodyId GetHurtboxBodyId(CharacterTestCharacterEntity entity)
        {
            return entity == null ? CombatBodyId.None : new CombatBodyId(CharacterHurtboxBodyIdStart + entity.Id.Value);
        }

        public bool TryGetBodyPresenceBodyId(int entityId, out CombatBodyId bodyId)
        {
            bodyId = CombatBodyId.None;
            if (entityId <= 0 || entityId > BodyPresenceMaximumEntityId)
                return false;

            bodyId = new CombatBodyId(checked(BodyPresenceBodyIdStart + entityId));
            return true;
        }

        public bool TryGetBodyPresenceBodyId(
            CharacterTestCharacterEntity entity,
            out CombatBodyId bodyId)
        {
            return TryGetBodyPresenceBodyId(entity == null ? 0 : entity.Id.Value, out bodyId);
        }

        public static bool TryGetBodyPresenceColliderId(
            int zeroBasedVolumeOrdinal,
            out CombatColliderId colliderId)
        {
            colliderId = CombatColliderId.None;
            if (zeroBasedVolumeOrdinal < 0
                || zeroBasedVolumeOrdinal >= BodyPresenceColliderCapacity)
            {
                return false;
            }

            colliderId = new CombatColliderId(
                checked(BodyPresenceColliderIdStart + zeroBasedVolumeOrdinal));
            return true;
        }

        public CombatBodyId GetMovableObjectBodyId(int numericId)
        {
            return numericId <= 0
                ? CombatBodyId.None
                : new CombatBodyId(DynamicMovableObjectBodyIdStart + numericId);
        }

        public CombatEntityId GetMovableObjectEntityId(int numericId)
        {
            return numericId <= 0
                ? CombatEntityId.None
                : new CombatEntityId(DynamicMovableObjectBodyIdStart + numericId);
        }

        public bool TryResolveMovableObjectNumericId(CombatBodyId bodyId, out int numericId)
        {
            numericId = 0;
            if (bodyId.Value < DynamicMovableObjectBodyIdStart)
                return false;

            numericId = bodyId.Value - DynamicMovableObjectBodyIdStart;
            return numericId > 0;
        }

        internal bool IsMovableObjectColliderSynced(int numericId, int syncVersion)
        {
            return numericId > 0
                && _movableObjectColliderSyncVersions.TryGetValue(numericId, out int syncedVersion)
                && syncedVersion == syncVersion;
        }

        internal void MarkMovableObjectColliderSynced(int numericId, int syncVersion)
        {
            if (numericId <= 0)
                return;

            _movableObjectColliderSyncVersions[numericId] = syncVersion;
        }

        internal bool IsCharacterColliderSynced(
            int entityId,
            bool blocksCharacters,
            in FixVector3 centerPosition,
            in FixVector3 halfExtents)
        {
            return entityId > 0
                && _characterColliderSyncStates.TryGetValue(entityId, out CharacterColliderSyncState state)
                && state.Equals(new CharacterColliderSyncState(blocksCharacters, centerPosition, halfExtents));
        }

        internal bool IsCharacterColliderShapeSynced(
            int entityId,
            bool blocksCharacters,
            in FixVector3 halfExtents)
        {
            return entityId > 0
                && _characterColliderSyncStates.TryGetValue(entityId, out CharacterColliderSyncState state)
                && state.HasSameShape(blocksCharacters, halfExtents);
        }

        internal void MarkCharacterColliderSynced(
            int entityId,
            bool blocksCharacters,
            in FixVector3 centerPosition,
            in FixVector3 halfExtents)
        {
            if (entityId <= 0)
                return;

            _characterColliderSyncStates[entityId] =
                new CharacterColliderSyncState(blocksCharacters, centerPosition, halfExtents);
        }

        internal bool TryGetBodyPresenceSyncWitness(
            int entityId,
            out int bindingRevision,
            out string bindingContentHash,
            out int activeLeafCount)
        {
            if (_bodyPresenceColliderSyncStates.TryGetValue(
                    entityId,
                    out BodyPresenceColliderSyncState state))
            {
                bindingRevision = state.BindingRevision;
                bindingContentHash = state.BindingContentHash;
                activeLeafCount = state.ActiveLeafCount;
                return true;
            }

            bindingRevision = 0;
            bindingContentHash = string.Empty;
            activeLeafCount = 0;
            return false;
        }

        internal bool IsBodyPresenceColliderSynced(
            int entityId,
            int rootXMillimeters,
            int rootYMillimeters,
            int rootZMillimeters,
            int rootYawMilliDegrees,
            int bindingRevision,
            string bindingContentHash,
            int activeLeafCount)
        {
            return _bodyPresenceColliderSyncStates.TryGetValue(
                    entityId,
                    out BodyPresenceColliderSyncState state)
                && state.Equals(new BodyPresenceColliderSyncState(
                    rootXMillimeters,
                    rootYMillimeters,
                    rootZMillimeters,
                    rootYawMilliDegrees,
                    bindingRevision,
                    bindingContentHash,
                    activeLeafCount));
        }

        internal void MarkBodyPresenceColliderSynced(
            int entityId,
            int rootXMillimeters,
            int rootYMillimeters,
            int rootZMillimeters,
            int rootYawMilliDegrees,
            int bindingRevision,
            string bindingContentHash,
            int activeLeafCount)
        {
            if (entityId <= 0 || entityId > BodyPresenceMaximumEntityId)
                return;

            _bodyPresenceColliderSyncStates[entityId] = new BodyPresenceColliderSyncState(
                rootXMillimeters,
                rootYMillimeters,
                rootZMillimeters,
                rootYawMilliDegrees,
                bindingRevision,
                bindingContentHash,
                activeLeafCount);
        }

        internal void ForgetBodyPresenceColliderSync(int entityId)
        {
            _bodyPresenceColliderSyncStates.Remove(entityId);
        }

        internal void ClearBodyPresenceColliderSyncStates()
        {
            _bodyPresenceColliderSyncStates.Clear();
        }

        public CombatMotionConfig CreateMotionConfig(int ticksPerSecond)
        {
            return CreateMotionConfig(ticksPerSecond, Fix64.Half);
        }

        public CombatMotionConfig CreateMotionConfig(int ticksPerSecond, Fix64 groundMinNormalY)
        {
            return CreateMotionConfig(
                ticksPerSecond,
                groundMinNormalY,
                CharacterTestCharacterPhysicsComponent.Default);
        }

        public CombatMotionConfig CreateMotionConfig(
            int ticksPerSecond,
            Fix64 groundMinNormalY,
            CharacterTestCharacterPhysicsComponent physics,
            Fix64 fixedBounceGravityPerSecond = default,
            CombatPhysicsLayerMask obstacleLayerMask = default)
        {
            CombatPhysicsLayerMask resolvedObstacleLayerMask = obstacleLayerMask.Value == 0
                ? MotionObstacleLayerMask
                : obstacleLayerMask;
            return new CombatMotionConfig(
                ticksPerSecond <= 0 ? CombatMotionConfig.DefaultTicksPerSecond : ticksPerSecond,
                Fix64.One,
                Fix64.Zero,
                Fix64.Zero,
                -Fix64.FromInt(200),
                SkinWidth,
                Fix64.Clamp(groundMinNormalY, Fix64.Zero, Fix64.One),
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                GetCharacterHalfExtents(physics),
                resolvedObstacleLayerMask,
                MaxStepHeight,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                fixedBounceGravityPerSecond,
                GroundSnapDistance);
        }

        public CombatMotionConfig CreateMotionConfig(
            int ticksPerSecond,
            Fix64 groundMinNormalY,
            CharacterTestColliderPostureComponent posture,
            Fix64 fixedBounceGravityPerSecond = default,
            CombatPhysicsLayerMask obstacleLayerMask = default)
        {
            CombatPhysicsLayerMask resolvedObstacleLayerMask = obstacleLayerMask.Value == 0
                ? MotionObstacleLayerMask
                : obstacleLayerMask;
            return new CombatMotionConfig(
                ticksPerSecond <= 0 ? CombatMotionConfig.DefaultTicksPerSecond : ticksPerSecond,
                Fix64.One,
                Fix64.Zero,
                Fix64.Zero,
                -Fix64.FromInt(200),
                SkinWidth,
                Fix64.Clamp(groundMinNormalY, Fix64.Zero, Fix64.One),
                Fix64.Half,
                CombatMotionConfig.DefaultMaxCollisionIterations,
                GetCharacterHalfExtents(posture),
                resolvedObstacleLayerMask,
                MaxStepHeight,
                CombatMotionConfig.DefaultMinElasticImpactSpeed,
                fixedBounceGravityPerSecond,
                GroundSnapDistance);
        }

        public FixVector3 GetCharacterHalfExtents(CharacterTestCharacterEntity entity)
        {
            return entity == null
                ? CharacterHalfExtents
                : GetCharacterHalfExtents(entity.ColliderPosture);
        }

        public FixVector3 GetCharacterHalfExtents(CharacterTestColliderPostureComponent posture)
        {
            if (!posture.IsValid)
                return CharacterHalfExtents;

            Fix64 radius = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                posture.RadiusMillimeters);
            return new FixVector3(
                radius,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    posture.HeightMillimeters / 2),
                radius);
        }

        public FixVector3 GetCharacterHalfExtents(CharacterTestCharacterPhysicsComponent physics)
        {
            if (!physics.IsConfigured)
                return CharacterHalfExtents;

            Fix64 radius = CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                physics.EffectiveRadiusMillimeters);
            return new FixVector3(
                radius,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    physics.EffectiveHeightMillimeters / 2),
                radius);
        }

        public Fix64 GetCharacterCenterOffsetY(CharacterTestCharacterEntity entity)
        {
            return entity == null
                ? CharacterHalfExtents.Y
                : GetCharacterCenterOffsetY(entity.ColliderPosture);
        }

        public Fix64 GetCharacterCenterOffsetY(CharacterTestColliderPostureComponent posture)
        {
            return posture.IsValid
                ? CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    posture.CenterYMillimeters > 0
                        ? posture.CenterYMillimeters
                        : posture.HeightMillimeters / 2)
                : CharacterHalfExtents.Y;
        }

        public Fix64 GetCharacterCenterOffsetY(CharacterTestCharacterPhysicsComponent physics)
        {
            return physics.IsConfigured
                ? CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    physics.CapsuleCenterYMillimeters > 0
                        ? physics.CapsuleCenterYMillimeters
                        : physics.EffectiveHeightMillimeters / 2)
                : CharacterHalfExtents.Y;
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)System.Math.Round(value * Fix64.Scale));
        }

        private readonly struct CharacterColliderSyncState : System.IEquatable<CharacterColliderSyncState>
        {
            public CharacterColliderSyncState(
                bool blocksCharacters,
                FixVector3 centerPosition,
                FixVector3 halfExtents)
            {
                BlocksCharacters = blocksCharacters;
                CenterPosition = centerPosition;
                HalfExtents = halfExtents;
            }

            public bool BlocksCharacters { get; }
            public FixVector3 CenterPosition { get; }
            public FixVector3 HalfExtents { get; }

            public bool Equals(CharacterColliderSyncState other)
            {
                return BlocksCharacters == other.BlocksCharacters
                    && CenterPosition.Equals(other.CenterPosition)
                    && HalfExtents.Equals(other.HalfExtents);
            }

            public bool HasSameShape(bool blocksCharacters, in FixVector3 halfExtents)
            {
                return BlocksCharacters == blocksCharacters
                    && HalfExtents.Equals(halfExtents);
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterColliderSyncState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = BlocksCharacters ? 1 : 0;
                    hash = (hash * 397) ^ CenterPosition.GetHashCode();
                    hash = (hash * 397) ^ HalfExtents.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly struct BodyPresenceColliderSyncState : IEquatable<BodyPresenceColliderSyncState>
        {
            public BodyPresenceColliderSyncState(
                int rootXMillimeters,
                int rootYMillimeters,
                int rootZMillimeters,
                int rootYawMilliDegrees,
                int bindingRevision,
                string bindingContentHash,
                int activeLeafCount)
            {
                RootXMillimeters = rootXMillimeters;
                RootYMillimeters = rootYMillimeters;
                RootZMillimeters = rootZMillimeters;
                RootYawMilliDegrees = rootYawMilliDegrees;
                BindingRevision = bindingRevision;
                BindingContentHash = bindingContentHash ?? string.Empty;
                ActiveLeafCount = activeLeafCount;
            }

            public int RootXMillimeters { get; }
            public int RootYMillimeters { get; }
            public int RootZMillimeters { get; }
            public int RootYawMilliDegrees { get; }
            public int BindingRevision { get; }
            public string BindingContentHash { get; }
            public int ActiveLeafCount { get; }

            public bool Equals(BodyPresenceColliderSyncState other)
            {
                return RootXMillimeters == other.RootXMillimeters
                    && RootYMillimeters == other.RootYMillimeters
                    && RootZMillimeters == other.RootZMillimeters
                    && RootYawMilliDegrees == other.RootYawMilliDegrees
                    && BindingRevision == other.BindingRevision
                    && string.Equals(
                        BindingContentHash,
                        other.BindingContentHash,
                        StringComparison.Ordinal)
                    && ActiveLeafCount == other.ActiveLeafCount;
            }

            public override bool Equals(object obj)
            {
                return obj is BodyPresenceColliderSyncState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = RootXMillimeters;
                    hash = (hash * 397) ^ RootYMillimeters;
                    hash = (hash * 397) ^ RootZMillimeters;
                    hash = (hash * 397) ^ RootYawMilliDegrees;
                    hash = (hash * 397) ^ BindingRevision;
                    hash = (hash * 397) ^ BindingContentHash.GetHashCode();
                    hash = (hash * 397) ^ ActiveLeafCount;
                    return hash;
                }
            }
        }
    }
}
