using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterControlPresentationKind
    {
        None = 0,
        Rooted = 1,
        Stasis = 2,
        Stunned = 3,
        Paralyzed = 4,
        Knockdown = 5,
        Knockup = 6,
    }

    [Flags]
    internal enum CharacterControlPresentationOverlayFlags
    {
        None = 0,
        Stasis = 1 << 0,
        Poisoned = 1 << 1,
    }

    internal readonly struct CharacterControlPresentationState
    {
        public static readonly CharacterControlPresentationState None =
            new CharacterControlPresentationState(
                CharacterControlPresentationKind.None,
                CharacterControlPresentationOverlayFlags.None,
                hasRooted: false,
                hasStasis: false,
                hasStunned: false,
                hasParalyzed: false);

        public CharacterControlPresentationState(
            CharacterControlPresentationKind primaryKind,
            CharacterControlPresentationOverlayFlags overlayFlags,
            bool hasRooted,
            bool hasStasis,
            bool hasStunned,
            bool hasParalyzed)
        {
            PrimaryKind = primaryKind;
            OverlayFlags = overlayFlags;
            HasRooted = hasRooted;
            HasStasis = hasStasis;
            HasStunned = hasStunned;
            HasParalyzed = hasParalyzed;
        }

        public CharacterControlPresentationKind PrimaryKind { get; }
        public CharacterControlPresentationOverlayFlags OverlayFlags { get; }
        public bool HasRooted { get; }
        public bool HasStasis { get; }
        public bool HasStunned { get; }
        public bool HasParalyzed { get; }

        public bool IsActionFrozen =>
            HasStasis
            || HasStunned
            || HasParalyzed
            || PrimaryKind == CharacterControlPresentationKind.Knockdown
            || PrimaryKind == CharacterControlPresentationKind.Knockup;

        public bool ShouldPauseAnimationPresentation =>
            PrimaryKind == CharacterControlPresentationKind.Stasis;

        public string ActionFrozenRejectReason
        {
            get
            {
                if (HasParalyzed)
                    return CharacterTestParalysisIds.ActionFrozenRejectReason;
                if (HasStunned)
                    return CharacterTestStunIds.ActionFrozenRejectReason;
                if (HasStasis)
                    return CharacterTestStasisIds.ActionFrozenRejectReason;
                return string.Empty;
            }
        }

        public CharacterTestActionRejectCode ActionFrozenRejectReasonId => HasParalyzed
            ? CharacterTestActionRejectCode.ActionFrozenByParalysis
            : HasStunned ? CharacterTestActionRejectCode.ActionFrozenByStun
            : HasStasis ? CharacterTestActionRejectCode.ActionFrozenByStasis
            : CharacterTestActionRejectCode.None;
    }

    internal static class CharacterTestControlPresentationResolver
    {
        private static readonly GameplayStatusId AreaConfinedStatusId =
            new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId);
        private static readonly GameplayStatusId StasisStatusId =
            new GameplayStatusId(CharacterTestStasisIds.StasisStatusId);
        private static readonly GameplayStatusId StunnedStatusId =
            new GameplayStatusId(CharacterTestStunIds.StunnedStatusId);
        private static readonly GameplayStatusId ParalyzedStatusId =
            new GameplayStatusId(CharacterTestParalysisIds.ParalyzedStatusId);
        private static readonly GameplayStatusId PoisonedStatusId =
            new GameplayStatusId(CharacterTestPoisonIds.PoisonedStatusId);

        public static CharacterControlPresentationState Resolve(GameplayStatusComponent statuses)
        {
            if (statuses.Count == 0)
                return CharacterControlPresentationState.None;

            bool hasRooted = statuses.Contains(AreaConfinedStatusId);
            bool hasStasis = statuses.Contains(StasisStatusId);
            bool hasStunned = statuses.Contains(StunnedStatusId);
            bool hasParalyzed = statuses.Contains(ParalyzedStatusId);
            bool hasPoisoned = statuses.Contains(PoisonedStatusId);
            CharacterControlPresentationKind primary =
                ResolvePrimaryKind(hasRooted, hasStasis, hasStunned, hasParalyzed);
            CharacterControlPresentationOverlayFlags overlays =
                CharacterControlPresentationOverlayFlags.None;
            if (hasStasis && primary != CharacterControlPresentationKind.Stasis)
                overlays |= CharacterControlPresentationOverlayFlags.Stasis;
            if (hasPoisoned)
                overlays |= CharacterControlPresentationOverlayFlags.Poisoned;

            return new CharacterControlPresentationState(
                primary,
                overlays,
                hasRooted,
                hasStasis,
                hasStunned,
                hasParalyzed);
        }

        private static CharacterControlPresentationKind ResolvePrimaryKind(
            bool hasRooted,
            bool hasStasis,
            bool hasStunned,
            bool hasParalyzed)
        {
            if (hasParalyzed)
                return CharacterControlPresentationKind.Paralyzed;
            if (hasStunned)
                return CharacterControlPresentationKind.Stunned;
            if (hasStasis)
                return CharacterControlPresentationKind.Stasis;
            if (hasRooted)
                return CharacterControlPresentationKind.Rooted;
            return CharacterControlPresentationKind.None;
        }
    }

    internal enum CharacterTestMechanicMotionPolicyKind
    {
        None = 0,
        RootActiveMovement = 1,
        ControlScale = 2,
    }

    internal enum CharacterTestMechanicExternalMotionPolicy
    {
        Allow = 0,
    }

    internal readonly struct CharacterTestMechanicMotionPolicyDefinition :
        IEquatable<CharacterTestMechanicMotionPolicyDefinition>
    {
        private CharacterTestMechanicMotionPolicyDefinition(
            int policyId,
            string stableId,
            GameplayStatusId statusId,
            CharacterTestMechanicMotionPolicyKind kind,
            CharacterMovementMode movementMode,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            bool hasMovementControlScale,
            bool hasRotationControlScale,
            CharacterTestMechanicExternalMotionPolicy externalMotionPolicy,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            if (policyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(policyId), "Mechanic motion policy id must be positive.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Mechanic motion policy stable id cannot be null or empty.", nameof(stableId));
            if (!statusId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(statusId), "Mechanic motion policy requires a valid status id.");
            if (!Enum.IsDefined(typeof(CharacterTestMechanicMotionPolicyKind), kind)
                || kind == CharacterTestMechanicMotionPolicyKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Mechanic motion policy kind is not supported.");
            }

            if (!Enum.IsDefined(typeof(CharacterTestMechanicExternalMotionPolicy), externalMotionPolicy))
                throw new ArgumentOutOfRangeException(nameof(externalMotionPolicy), "External motion policy is not supported.");
            if (stableEventId <= 0)
                throw new ArgumentException("Mechanic motion policy stable event id cannot be null or empty.", nameof(stableEventId));

            PolicyId = policyId;
            StableId = stableId;
            StatusId = statusId;
            Kind = kind;
            MovementMode = movementMode;
            MovementControlScaleMilli =
                CharacterTestDeterministicMath.ClampControlScaleMilli(movementControlScaleMilli);
            RotationControlScaleMilli =
                CharacterTestDeterministicMath.ClampControlScaleMilli(rotationControlScaleMilli);
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            ExternalMotionPolicy = externalMotionPolicy;
            SourceActionId = sourceActionId;
            StableEventId = stableEventId;
        }

        public int PolicyId { get; }
        public string StableId { get; }
        public GameplayStatusId StatusId { get; }
        public CharacterTestMechanicMotionPolicyKind Kind { get; }
        public CharacterMovementMode MovementMode { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public CharacterTestMechanicExternalMotionPolicy ExternalMotionPolicy { get; }
        public CharacterActionId SourceActionId { get; }
        public int StableEventId { get; }

        public static CharacterTestMechanicMotionPolicyDefinition RootActiveMovement(
            int policyId,
            string stableId,
            GameplayStatusId statusId,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterTestMechanicMotionPolicyDefinition(
                policyId,
                stableId,
                statusId,
                CharacterTestMechanicMotionPolicyKind.RootActiveMovement,
                CharacterMovementMode.ControlLocked,
                0,
                CharacterTestDeterministicUnits.ScaleFactorScale,
                hasMovementControlScale: true,
                hasRotationControlScale: true,
                CharacterTestMechanicExternalMotionPolicy.Allow,
                sourceActionId,
                stableEventId);
        }

        public static CharacterTestMechanicMotionPolicyDefinition ControlScaleRaw(
            int policyId,
            string stableId,
            GameplayStatusId statusId,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            CharacterMovementMode movementMode,
            CharacterActionId sourceActionId,
            int stableEventId,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true)
        {
            return new CharacterTestMechanicMotionPolicyDefinition(
                policyId,
                stableId,
                statusId,
                CharacterTestMechanicMotionPolicyKind.ControlScale,
                movementMode,
                movementControlScaleMilli,
                rotationControlScaleMilli,
                hasMovementControlScale,
                hasRotationControlScale,
                CharacterTestMechanicExternalMotionPolicy.Allow,
                sourceActionId,
                stableEventId);
        }

        public bool Equals(CharacterTestMechanicMotionPolicyDefinition other)
        {
            return PolicyId == other.PolicyId
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && StatusId.Equals(other.StatusId)
                && Kind == other.Kind
                && MovementMode == other.MovementMode
                && MovementControlScaleMilli == other.MovementControlScaleMilli
                && RotationControlScaleMilli == other.RotationControlScaleMilli
                && HasMovementControlScale == other.HasMovementControlScale
                && HasRotationControlScale == other.HasRotationControlScale
                && ExternalMotionPolicy == other.ExternalMotionPolicy
                && SourceActionId == other.SourceActionId
                && StableEventId == other.StableEventId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestMechanicMotionPolicyDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PolicyId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ StatusId.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)MovementMode;
                hash = (hash * 397) ^ MovementControlScaleMilli;
                hash = (hash * 397) ^ RotationControlScaleMilli;
                hash = (hash * 397) ^ HasMovementControlScale.GetHashCode();
                hash = (hash * 397) ^ HasRotationControlScale.GetHashCode();
                hash = (hash * 397) ^ (int)ExternalMotionPolicy;
                hash = (hash * 397) ^ SourceActionId.GetHashCode();
                hash = (hash * 397) ^ StableEventId;
                return hash;
            }
        }
    }

    internal sealed class CharacterTestMechanicMotionPolicyRegistry
    {
        private readonly CharacterTestMechanicMotionPolicyDefinition[] _policies;

        public CharacterTestMechanicMotionPolicyRegistry(
            params CharacterTestMechanicMotionPolicyDefinition[] policies)
        {
            _policies = CopySorted(policies);
        }

        public int Count => _policies.Length;

        public CharacterTestMechanicMotionPolicyDefinition[] ToArray()
        {
            if (_policies.Length == 0)
                return Array.Empty<CharacterTestMechanicMotionPolicyDefinition>();

            var copy = new CharacterTestMechanicMotionPolicyDefinition[_policies.Length];
            Array.Copy(_policies, copy, _policies.Length);
            return copy;
        }

        public void CopyMatchingPolicies(
            GameplayStatusComponent statuses,
            List<CharacterTestMechanicMotionPolicyDefinition> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            for (int i = 0; i < _policies.Length; i++)
            {
                if (statuses.Contains(_policies[i].StatusId))
                    destination.Add(_policies[i]);
            }
        }

        public int ComputeStableHash()
        {
            unchecked
            {
                int hash = _policies.Length;
                for (int i = 0; i < _policies.Length; i++)
                    hash = (hash * 397) ^ _policies[i].GetHashCode();
                return hash;
            }
        }

        private static CharacterTestMechanicMotionPolicyDefinition[] CopySorted(
            CharacterTestMechanicMotionPolicyDefinition[] policies)
        {
            if (policies == null || policies.Length == 0)
                return Array.Empty<CharacterTestMechanicMotionPolicyDefinition>();

            var copy = new CharacterTestMechanicMotionPolicyDefinition[policies.Length];
            Array.Copy(policies, copy, policies.Length);
            Array.Sort(copy, ComparePolicies);
            var policyIds = new HashSet<int>();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].PolicyId <= 0)
                    throw new ArgumentException("Mechanic motion policy registry cannot contain default policies.", nameof(policies));
                if (!policyIds.Add(copy[i].PolicyId))
                    throw new ArgumentException("Mechanic motion policy registry cannot contain duplicate policy ids.", nameof(policies));
                if (i > 0 && string.Equals(copy[i - 1].StableId, copy[i].StableId, StringComparison.Ordinal))
                    throw new ArgumentException("Mechanic motion policy registry cannot contain duplicate stable ids.", nameof(policies));
            }

            return copy;
        }

        private static int ComparePolicies(
            CharacterTestMechanicMotionPolicyDefinition left,
            CharacterTestMechanicMotionPolicyDefinition right)
        {
            int stable = StringComparer.Ordinal.Compare(left.StableId, right.StableId);
            return stable != 0 ? stable : left.PolicyId.CompareTo(right.PolicyId);
        }
    }

    internal static class CharacterTestMechanicMotionPolicySource
    {
        public static CharacterTestMechanicMotionPolicyRegistry CreateDefaultRegistry()
        {
            return new CharacterTestMechanicMotionPolicyRegistry(
                CharacterTestMechanicMotionPolicyDefinition.RootActiveMovement(
                    CharacterTestAreaConfinementIds.MotionPolicyId,
                    CharacterTestAreaConfinementIds.MotionPolicyStableId,
                    new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                    CharacterTestCharacterActionConfigSource.AreaConfinementActionId,
                    CharacterTestCharacterActionConfigSource.AreaConfinementRootedStableEventId),
                CharacterTestMechanicMotionPolicyDefinition.ControlScaleRaw(
                    CharacterTestStasisIds.StasisMotionPolicyId,
                    CharacterTestStasisIds.StasisMotionPolicyStableId,
                    new GameplayStatusId(CharacterTestStasisIds.StasisStatusId),
                    movementControlScaleMilli: 0,
                    rotationControlScaleMilli: 0,
                    CharacterMovementMode.ControlLocked,
                    CharacterActionId.None,
                    CharacterTestStasisIds.StasisActionFreezeStableEventId),
                CharacterTestMechanicMotionPolicyDefinition.ControlScaleRaw(
                    CharacterTestStunIds.StunMotionPolicyId,
                    CharacterTestStunIds.StunMotionPolicyStableId,
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId),
                    movementControlScaleMilli: 0,
                    rotationControlScaleMilli: 0,
                    CharacterMovementMode.ControlLocked,
                    CharacterActionId.None,
                    CharacterTestStunIds.StunControlStableEventId));
        }
    }

    public interface ICharacterTestStatusControlProvider
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        bool IsActionFrozen(CharacterTestCharacterEntity entity);

        /// <remarks>NoAlloc after initialization.</remarks>
        bool ShouldPauseAnimationPresentation(CharacterTestCharacterEntity entity);

        /// <remarks>NoAlloc after initialization.</remarks>
        string GetActionFrozenRejectReason(CharacterTestCharacterEntity entity);

        /// <remarks>NoAlloc. Numeric identity consumed by the action state owner.</remarks>
        CharacterTestActionRejectCode GetActionFrozenRejectReasonId(CharacterTestCharacterEntity entity);
    }

    internal interface ICharacterTestControlPresentationProvider
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        bool TryGetControlPresentationState(
            CharacterTestCharacterEntity entity,
            out CharacterControlPresentationState state);
    }

    internal sealed class CharacterTestStatusControlEffectBridge :
        ICharacterTestStatusControlProvider,
        ICharacterTestControlPresentationProvider
    {
        private readonly Dictionary<int, CharacterControlPresentationState> _statesByEntityId =
            new Dictionary<int, CharacterControlPresentationState>();

        public CharacterTestStatusControlEffectBridge()
        {
        }

        [Obsolete("Use the default constructor; control presentation now resolves the full status set.")]
        public CharacterTestStatusControlEffectBridge(GameplayStatusId stasisStatusId)
        {
            if (!stasisStatusId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(stasisStatusId), "Stasis status id must be valid.");
        }

        public int LastActionFrozenCount { get; private set; }
        public int LastPrimaryPresentationCount { get; private set; }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world)
        {
            _statesByEntityId.Clear();
            LastActionFrozenCount = 0;
            LastPrimaryPresentationCount = 0;
            if (world == null
                || !world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore))
            {
                return;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || entity.BattleState.LifeState != CharacterTestCharacterLifeState.Alive
                    || !statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses))
                {
                    continue;
                }

                CharacterControlPresentationState state =
                    CharacterTestControlPresentationResolver.Resolve(statuses);
                if (state.PrimaryKind == CharacterControlPresentationKind.None
                    && !state.IsActionFrozen
                    && state.OverlayFlags == CharacterControlPresentationOverlayFlags.None)
                {
                    continue;
                }

                _statesByEntityId[entity.Id.Value] = state;
                if (state.IsActionFrozen)
                    LastActionFrozenCount++;
                if (state.PrimaryKind != CharacterControlPresentationKind.None)
                    LastPrimaryPresentationCount++;
            }
        }

        public bool IsActionFrozen(CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state) && state.IsActionFrozen;
        }

        public bool ShouldPauseAnimationPresentation(CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state)
                && state.ShouldPauseAnimationPresentation;
        }

        public string GetActionFrozenRejectReason(CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state)
                ? state.ActionFrozenRejectReason
                : string.Empty;
        }

        public CharacterTestActionRejectCode GetActionFrozenRejectReasonId(CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state)
                ? state.ActionFrozenRejectReasonId : CharacterTestActionRejectCode.None;
        }

        public CharacterControlPresentationKind GetPrimaryControlPresentationKind(
            CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state)
                ? state.PrimaryKind
                : CharacterControlPresentationKind.None;
        }

        public CharacterControlPresentationOverlayFlags GetOverlayPresentationFlags(
            CharacterTestCharacterEntity entity)
        {
            return TryGetState(entity, out CharacterControlPresentationState state)
                ? state.OverlayFlags
                : CharacterControlPresentationOverlayFlags.None;
        }

        public bool TryGetControlPresentationState(
            CharacterTestCharacterEntity entity,
            out CharacterControlPresentationState state)
        {
            return TryGetState(entity, out state);
        }

        private bool TryGetState(
            CharacterTestCharacterEntity entity,
            out CharacterControlPresentationState state)
        {
            if (entity != null && _statesByEntityId.TryGetValue(entity.Id.Value, out state))
                return true;

            state = CharacterControlPresentationState.None;
            return false;
        }
    }

    internal sealed class CharacterTestMechanicMotionEffectBridge
    {
        private readonly CharacterTestMechanicMotionPolicyRegistry _policies;
        private readonly List<CharacterTestMechanicMotionPolicyDefinition> _activePolicies =
            new List<CharacterTestMechanicMotionPolicyDefinition>();

        public CharacterTestMechanicMotionEffectBridge()
            : this(CharacterTestMechanicMotionPolicySource.CreateDefaultRegistry())
        {
        }

        public CharacterTestMechanicMotionEffectBridge(
            CharacterTestMechanicMotionPolicyRegistry policies)
        {
            _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }

        public int LastLockedCount { get; private set; }
        public int LastAppliedPolicyCount { get; private set; }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world)
        {
            LastLockedCount = 0;
            LastAppliedPolicyCount = 0;
            if (world == null
                || !world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore))
            {
                return;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null
                    || entity.BattleState.LifeState != CharacterTestCharacterLifeState.Alive
                    || !statusStore.TryGet(entity.GameplayEntityId, out GameplayStatusComponent statuses))
                {
                    continue;
                }

                _activePolicies.Clear();
                _policies.CopyMatchingPolicies(statuses, _activePolicies);
                if (_activePolicies.Count == 0)
                    continue;

                CharacterTestCharacterMotionRequest request = ResolveMotionRequest(_activePolicies);
                if (!request.HasValue)
                    continue;

                entity.EnqueueMotionRequest(request);
                LastAppliedPolicyCount++;
                if (request.Kind == CharacterTestCharacterMotionRequestKind.LockMovement)
                    LastLockedCount++;
            }

            _activePolicies.Clear();
        }

        private static CharacterTestCharacterMotionRequest ResolveMotionRequest(
            IReadOnlyList<CharacterTestMechanicMotionPolicyDefinition> policies)
        {
            CharacterTestMechanicMotionPolicyDefinition rootPolicy = default;
            bool hasRoot = false;
            for (int i = 0; i < policies.Count; i++)
            {
                if (policies[i].Kind != CharacterTestMechanicMotionPolicyKind.RootActiveMovement)
                    continue;

                rootPolicy = policies[i];
                hasRoot = true;
                break;
            }

            bool hasMovementScale = false;
            bool hasRotationScale = false;
            int movementScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale;
            int rotationScaleMilli = CharacterTestDeterministicUnits.ScaleFactorScale;
            CharacterTestMechanicMotionPolicyDefinition metadataPolicy = default;
            bool hasMetadataPolicy = false;
            for (int i = 0; i < policies.Count; i++)
            {
                CharacterTestMechanicMotionPolicyDefinition policy = policies[i];
                if (policy.Kind != CharacterTestMechanicMotionPolicyKind.ControlScale)
                    continue;

                if (!hasMetadataPolicy)
                {
                    metadataPolicy = policy;
                    hasMetadataPolicy = true;
                }

                if (policy.HasMovementControlScale)
                {
                    movementScaleMilli = hasMovementScale
                        ? Math.Min(movementScaleMilli, policy.MovementControlScaleMilli)
                        : policy.MovementControlScaleMilli;
                    hasMovementScale = true;
                }

                if (policy.HasRotationControlScale)
                {
                    rotationScaleMilli = hasRotationScale
                        ? Math.Min(rotationScaleMilli, policy.RotationControlScaleMilli)
                        : policy.RotationControlScaleMilli;
                    hasRotationScale = true;
                }
            }

            if (hasMetadataPolicy && hasRoot)
            {
                if (rootPolicy.HasMovementControlScale)
                {
                    movementScaleMilli = hasMovementScale
                        ? Math.Min(movementScaleMilli, rootPolicy.MovementControlScaleMilli)
                        : rootPolicy.MovementControlScaleMilli;
                    hasMovementScale = true;
                }

                if (rootPolicy.HasRotationControlScale)
                {
                    rotationScaleMilli = hasRotationScale
                        ? Math.Min(rotationScaleMilli, rootPolicy.RotationControlScaleMilli)
                        : rootPolicy.RotationControlScaleMilli;
                    hasRotationScale = true;
                }
            }

            if (!hasMetadataPolicy)
            {
                if (hasRoot)
                {
                    return CharacterTestCharacterMotionRequest.LockMovement(
                        rootPolicy.MovementMode,
                        rootPolicy.SourceActionId,
                        rootPolicy.StableEventId);
                }

                return default;
            }

            return CharacterTestCharacterMotionRequest.ControlScaleRaw(
                movementScaleMilli,
                rotationScaleMilli,
                metadataPolicy.MovementMode,
                metadataPolicy.SourceActionId,
                metadataPolicy.StableEventId,
                hasMovementScale,
                hasRotationScale);
        }
    }
}
