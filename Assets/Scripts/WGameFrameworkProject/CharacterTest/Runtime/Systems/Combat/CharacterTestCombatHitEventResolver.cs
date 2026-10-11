using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestCombatWorldTargetKind
    {
        Unknown = 0,
        Wall = 1,
        Ground = 2,
        Static = 3
    }

    internal enum CharacterTestCombatWorldQualification
    {
        Rejected = 0,
        QualifiedBlocker = 1,
        UnknownFailClosedBlocker = 2,
        StaleAuthority = 3
    }

    internal readonly struct CharacterTestCombatWorldTargetClassification
    {
        internal CharacterTestCombatWorldTargetClassification(
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            CharacterTestCombatWorldTargetKind kind)
        {
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            Kind = kind;
        }

        internal CombatBodyId BodyId { get; }
        internal CombatColliderId ColliderId { get; }
        internal int Layer { get; }
        internal CharacterTestCombatWorldTargetKind Kind { get; }
        internal bool IsValid =>
            !BodyId.IsNone
            && !ColliderId.IsNone
            && Layer >= 0
            && Layer <= 31
            && Kind != CharacterTestCombatWorldTargetKind.Unknown;
    }

    /// <summary>
    /// Frozen explicit world-kind metadata. Current production CombatPhysics
    /// does not author Wall/Ground metadata, so absent entries remain Unknown
    /// fail-closed blockers and never masquerade as a concrete subtype. No
    /// normal/height heuristic is used.
    /// </summary>
    internal sealed class CharacterTestCombatWorldTargetClassificationSnapshot
    {
        private readonly List<CharacterTestCombatWorldTargetClassification> _entries;
        private bool _isSealed;
        private RuntimeFrame _runtimeFrame;
        private int _combatPhysicsRevision;

        internal CharacterTestCombatWorldTargetClassificationSnapshot(int initialCapacity = 0)
        {
            _entries = new List<CharacterTestCombatWorldTargetClassification>(
                initialCapacity < 0 ? 0 : initialCapacity);
        }

        internal bool IsSealed => _isSealed;
        internal RuntimeFrame RuntimeFrame => _runtimeFrame;
        internal int CombatPhysicsRevision => _combatPhysicsRevision;

        internal void Reset(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            _entries.Clear();
            _runtimeFrame = runtimeFrame;
            _combatPhysicsRevision = combatPhysicsRevision;
            _isSealed = false;
        }

        internal bool TryAdd(CharacterTestCombatWorldTargetClassification classification)
        {
            if (_isSealed || !classification.IsValid)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestCombatWorldTargetClassification existing = _entries[i];
                if (existing.BodyId.Equals(classification.BodyId)
                    && existing.ColliderId.Equals(classification.ColliderId))
                {
                    return false;
                }
            }

            _entries.Add(classification);
            return true;
        }

        internal bool Seal(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            if (_isSealed
                || runtimeFrame != _runtimeFrame
                || combatPhysicsRevision != _combatPhysicsRevision
                || combatPhysicsRevision < 0)
            {
                return false;
            }

            _isSealed = true;
            return true;
        }

        internal bool IsCurrent(RuntimeFrame runtimeFrame, int combatPhysicsRevision)
        {
            return _isSealed
                && runtimeFrame == _runtimeFrame
                && combatPhysicsRevision == _combatPhysicsRevision;
        }

        internal bool TryResolve(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatPhysicsWorld world,
            CombatQueryResult hit,
            out CharacterTestCombatWorldTargetKind kind,
            out bool explicitIdentityMismatch)
        {
            kind = CharacterTestCombatWorldTargetKind.Unknown;
            explicitIdentityMismatch = false;
            if (!IsCurrent(runtimeFrame, combatPhysicsRevision)
                || world == null
                || world.Revision != combatPhysicsRevision
                || !CharacterTestCombatHitEventResolver.TryResolveColliderLayer(
                    world,
                    hit.TargetBodyId,
                    hit.TargetColliderId,
                    out int actualLayer))
            {
                return false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                CharacterTestCombatWorldTargetClassification entry = _entries[i];
                if (entry.BodyId.Equals(hit.TargetBodyId)
                    && entry.ColliderId.Equals(hit.TargetColliderId))
                {
                    if (entry.Layer != actualLayer)
                    {
                        explicitIdentityMismatch = true;
                        return false;
                    }

                    kind = entry.Kind;
                    return true;
                }
            }

            return false;
        }
    }

    internal static class CharacterTestCombatHitEventResolver
    {
        public static CombatPhysicsLayerMask ResolveQueryLayerMask(
            CombatHitEventConfig config,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            CombatHitTargetTypes targetTypes = config.EffectiveTargetTypes;
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.None;
            if (HasAnyCharacterTarget(targetTypes))
                mask = mask.AddLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
            if ((targetTypes & CombatHitTargetTypes.DamageableObject) != 0)
                mask = mask.AddLayer(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer);
            if (CombatHitEventConfig.HasAnyWorldTarget(targetTypes) && physicsRuntime != null)
                mask = new CombatPhysicsLayerMask(mask.Value | physicsRuntime.ObstacleLayerMask.Value);

            return mask.Value == 0
                ? CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer)
                : mask;
        }

        public static HitTargetStateFlags ResolveTargetState(
            CombatEntityId targetId,
            CharacterTestCombatEntityLookup lookup)
        {
            if (IsStaticEnvironmentTarget(targetId))
                return HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking;

            return lookup == null ? HitTargetStateFlags.None : lookup.ResolveTargetState(targetId);
        }

        public static bool AllowsTarget(
            CombatHitEventConfig config,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            CharacterTestCombatEntityLookup lookup,
            out bool environmentTarget)
        {
            environmentTarget = IsStaticEnvironmentTarget(targetId);
            CombatHitTargetTypes targetTypes = config.EffectiveTargetTypes;
            if (environmentTarget)
            {
                // Target-id-only projectile paths cannot classify a concrete
                // collider. Preserve a generic fail-closed blocker without
                // claiming that the surface is Wall, Ground, or Static.
                return config.AllowsEnvironmentBlockedPresentation
                    && CombatHitEventConfig.HasAnyWorldTarget(targetTypes);
            }

            if (source == null || lookup == null)
                return false;

            CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
            if (target == null)
                return (targetTypes & CombatHitTargetTypes.DamageableObject) != 0;

            return AllowsTarget(
                config,
                ResolveCharacterTargetKind(
                    source.Id.Value,
                    target.Id.Value,
                    source.Team.TeamId,
                    target.Team.TeamId),
                targetId,
                CharacterTestCombatWorldTargetKind.Unknown,
                out environmentTarget);
        }

        /// <summary>
        /// Shared actual/preview world qualification. Explicit classifications
        /// either match exactly or are rejected. Missing classification remains
        /// an Unknown fail-closed blocker, never an inferred Wall/Ground.
        /// </summary>
        internal static CharacterTestCombatWorldQualification QualifyWorldTarget(
            CombatHitEventConfig config,
            CombatQueryResult hit,
            RuntimeFrame runtimeFrame,
            CombatPhysicsWorld world,
            CharacterTestCombatWorldTargetClassificationSnapshot classifications,
            out CharacterTestCombatWorldTargetKind worldTargetKind)
        {
            worldTargetKind = CharacterTestCombatWorldTargetKind.Unknown;
            if (!IsStaticEnvironmentTarget(hit.TargetEntityId))
                return CharacterTestCombatWorldQualification.Rejected;
            if (world == null
                || classifications == null
                || !classifications.IsCurrent(runtimeFrame, world.Revision)
                || !TryResolveColliderLayer(
                    world,
                    hit.TargetBodyId,
                    hit.TargetColliderId,
                    out _))
            {
                return CharacterTestCombatWorldQualification.StaleAuthority;
            }

            if (classifications.TryResolve(
                    runtimeFrame,
                    world.Revision,
                    world,
                    hit,
                    out worldTargetKind,
                    out bool explicitIdentityMismatch))
            {
                return AllowsWorldTarget(config, worldTargetKind)
                    ? CharacterTestCombatWorldQualification.QualifiedBlocker
                    : CharacterTestCombatWorldQualification.Rejected;
            }

            if (explicitIdentityMismatch)
                return CharacterTestCombatWorldQualification.Rejected;

            return config.AllowsEnvironmentBlockedPresentation
                && CombatHitEventConfig.HasAnyWorldTarget(config.EffectiveTargetTypes)
                    ? CharacterTestCombatWorldQualification.UnknownFailClosedBlocker
                    : CharacterTestCombatWorldQualification.Rejected;
        }

        /// <summary>
        /// Pure target-qualification contract shared by runtime resolution and
        /// CharacterTest PreviewTrace. Preview callers capture the relation kind
        /// at the decision boundary and do not consult mutable team state.
        /// </summary>
        internal static bool AllowsTarget(
            CombatHitEventConfig config,
            CharacterTestAttackOpportunityTargetKind targetKind,
            CombatEntityId targetId,
            CharacterTestCombatWorldTargetKind worldTargetKind,
            out bool environmentTarget)
        {
            environmentTarget = IsStaticEnvironmentTarget(targetId);
            CombatHitTargetTypes targetTypes = config.EffectiveTargetTypes;
            if (environmentTarget)
                return AllowsWorldTarget(config, worldTargetKind);

            switch (targetKind)
            {
                case CharacterTestAttackOpportunityTargetKind.HostileCharacter:
                    return (targetTypes & CombatHitTargetTypes.HostileCharacter) != 0;
                case CharacterTestAttackOpportunityTargetKind.FriendlyCharacter:
                    return (targetTypes & CombatHitTargetTypes.FriendlyCharacter) != 0;
                case CharacterTestAttackOpportunityTargetKind.Self:
                    return (targetTypes & CombatHitTargetTypes.Self) != 0;
                case CharacterTestAttackOpportunityTargetKind.ThirdPartyCharacter:
                    return (targetTypes & CombatHitTargetTypes.ThirdPartyCharacter) != 0;
                case CharacterTestAttackOpportunityTargetKind.DamageableObject:
                    return (targetTypes & CombatHitTargetTypes.DamageableObject) != 0;
                default:
                    return false;
            }
        }

        internal static bool AllowsTarget(
            CombatHitEventConfig config,
            CharacterTestAttackOpportunityTargetKind targetKind,
            CombatEntityId targetId,
            out bool environmentTarget)
        {
            return AllowsTarget(
                config,
                targetKind,
                targetId,
                CharacterTestCombatWorldTargetKind.Unknown,
                out environmentTarget);
        }

        internal static bool AllowsWorldTarget(
            CombatHitEventConfig config,
            CharacterTestCombatWorldTargetKind worldTargetKind)
        {
            if (!config.AllowsEnvironmentBlockedPresentation)
                return false;

            CombatHitTargetTypes requiredType;
            switch (worldTargetKind)
            {
                case CharacterTestCombatWorldTargetKind.Wall:
                    requiredType = CombatHitTargetTypes.WorldWall;
                    break;
                case CharacterTestCombatWorldTargetKind.Ground:
                    requiredType = CombatHitTargetTypes.WorldGround;
                    break;
                case CharacterTestCombatWorldTargetKind.Static:
                case CharacterTestCombatWorldTargetKind.Unknown:
                    requiredType = CombatHitTargetTypes.WorldStatic;
                    break;
                default:
                    return false;
            }

            return (config.EffectiveTargetTypes & requiredType) != 0;
        }

        internal static CharacterTestAttackOpportunityTargetKind ResolveCharacterTargetKind(
            int sourceEntityId,
            int targetEntityId,
            int sourceTeamId,
            int targetTeamId)
        {
            if (sourceEntityId == targetEntityId)
                return CharacterTestAttackOpportunityTargetKind.Self;
            if (sourceTeamId == targetTeamId)
                return CharacterTestAttackOpportunityTargetKind.FriendlyCharacter;
            return IsPrimaryEnemyTeam(sourceTeamId, targetTeamId)
                ? CharacterTestAttackOpportunityTargetKind.HostileCharacter
                : CharacterTestAttackOpportunityTargetKind.ThirdPartyCharacter;
        }

        public static bool IsStaticEnvironmentTarget(CombatEntityId targetId)
        {
            return targetId.Value >= CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart
                && targetId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
        }

        internal static bool TryResolveColliderLayer(
            CombatPhysicsWorld world,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            out int layer)
        {
            layer = 0;
            if (world == null || bodyId.IsNone || colliderId.IsNone)
                return false;
            if (world.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider aabb))
            {
                layer = aabb.Layer;
                return true;
            }
            if (world.TryGetSphereCollider(bodyId, colliderId, out CombatPhysicsSphereCollider sphere))
            {
                layer = sphere.Layer;
                return true;
            }
            if (world.TryGetCapsuleCollider(bodyId, colliderId, out CombatPhysicsCapsuleCollider capsule))
            {
                layer = capsule.Layer;
                return true;
            }
            if (world.TryGetObbCollider(bodyId, colliderId, out CombatPhysicsObbCollider obb))
            {
                layer = obb.Layer;
                return true;
            }

            return false;
        }

        private static bool HasAnyCharacterTarget(CombatHitTargetTypes targetTypes)
        {
            return (targetTypes & (CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.FriendlyCharacter
                | CombatHitTargetTypes.Self
                | CombatHitTargetTypes.ThirdPartyCharacter)) != 0;
        }

        private static bool IsPrimaryEnemyTeam(int sourceTeamId, int targetTeamId)
        {
            return (sourceTeamId == 1 && targetTeamId == 2)
                || (sourceTeamId == 2 && targetTeamId == 1);
        }
    }
}
