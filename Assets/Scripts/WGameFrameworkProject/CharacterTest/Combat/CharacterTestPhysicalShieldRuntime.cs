using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestPhysicalShieldTickSummary
    {
        public CharacterTestPhysicalShieldTickSummary(
            int activeCount,
            int createdOrRefreshedCount,
            int expiredCount)
        {
            ActiveCount = activeCount;
            CreatedOrRefreshedCount = createdOrRefreshedCount;
            ExpiredCount = expiredCount;
        }

        public int ActiveCount { get; }
        public int CreatedOrRefreshedCount { get; }
        public int ExpiredCount { get; }
    }

    internal sealed class CharacterTestPhysicalShieldRuntimeState
    {
        public const int CurrentSchemaVersion = 2;
        public const int ModuleIdValue = 1106;
        public const string ModuleLabel = "CharacterTest.PhysicalShield";
        public const int ShieldRuntimeModuleIdentity = 1107;
        public const string ShieldRuntimeModuleId = "CharacterTest.PhysicalShield.CombatShieldRuntime";

        public CharacterTestPhysicalShieldRuntimeState(
            int schemaVersion,
            int moduleId,
            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> activeShields,
            CombatShieldRuntimeState shieldRuntime, bool borrow = false)
        {
            SchemaVersion = schemaVersion;
            ModuleId = moduleId;
            ActiveShields = borrow ? activeShields : CopyActiveShields(activeShields);
            ShieldRuntime = shieldRuntime;
        }

        public int SchemaVersion { get; }
        public int ModuleId { get; }
        public IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> ActiveShields { get; }
        public CombatShieldRuntimeState ShieldRuntime { get; internal set; }
        public int ActiveShieldCount => ActiveShields != null ? ActiveShields.Count : 0;

        private static CharacterTestPhysicalShieldActiveSnapshot[] CopyActiveShields(
            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> activeShields)
        {
            int count = activeShields != null ? activeShields.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestPhysicalShieldActiveSnapshot>();

            var result = new CharacterTestPhysicalShieldActiveSnapshot[count];
            for (int i = 0; i < count; i++)
                result[i] = activeShields[i];
            return result;
        }
    }

    internal readonly struct CharacterTestPhysicalShieldActiveSnapshot
    {
        public CharacterTestPhysicalShieldActiveSnapshot(
            int ownerId,
            int shieldEntityId,
            int bodyId,
            long centerXRaw,
            long centerYRaw,
            long centerZRaw,
            long endFrame)
        {
            OwnerId = ownerId;
            ShieldEntityId = shieldEntityId;
            BodyId = bodyId;
            CenterXRaw = centerXRaw;
            CenterYRaw = centerYRaw;
            CenterZRaw = centerZRaw;
            EndFrame = endFrame;
        }

        public int OwnerId { get; }
        public int ShieldEntityId { get; }
        public int BodyId { get; }
        public long CenterXRaw { get; }
        public long CenterYRaw { get; }
        public long CenterZRaw { get; }
        public long EndFrame { get; }
    }

    internal sealed class CharacterTestPhysicalShieldRuntime
    {
        public const int ShieldEntityIdStart = 760000;
        public const int ShieldBodyIdStart = 760000;
        public const int ShieldColliderId = 1;
        public const int ShieldLayer = 10;
        public const int LifetimeFrames = 300;
        public const int BaseCohesionLimit = 48;
        public const int RecoveryRate = 1;
        public const int RecoveryDelayFrames = 45;

        private const int ForwardOffsetMillimeters = 1100;
        private const int HeightOffsetMillimeters = 1100;
        private static readonly FixVector3 ShieldHalfExtents = new FixVector3(
            Fix64.FromRatio(110, 100),
            Fix64.FromRatio(115, 100),
            Fix64.FromRatio(12, 100));
        private static readonly CombatShieldPotencyTriplet BasePotency =
            new CombatShieldPotencyTriplet(motion: 0, cohesion: 64, scatter: 0);

        private readonly List<ActiveShield> _activeShields = new List<ActiveShield>(4);
        private CombatPhysicsWorld _world;
        private CombatShieldRuntime _runtime;
        private int _createdOrRefreshedThisFrame;

        public CombatShieldRuntime Runtime => _runtime;
        public CharacterTestPhysicalShieldTickSummary LastTickSummary { get; private set; }
        public int ActiveCount => _activeShields.Count;

        public void Configure(CombatPhysicsWorld world)
        {
            if (ReferenceEquals(_world, world))
                return;

            _runtime?.Clear();
            _activeShields.Clear();
            _world = world;
            _runtime = world == null ? null : new CombatShieldRuntime(world);
            _createdOrRefreshedThisFrame = 0;
            LastTickSummary = default;
        }

        private CharacterTestPhysicalShieldRuntimeState _stateView;
        internal CharacterTestPhysicalShieldRuntimeState ReadStateView()
        {
            if (_runtime == null) return null;
            _stateView ??= new CharacterTestPhysicalShieldRuntimeState(
                CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion,
                CharacterTestPhysicalShieldRuntimeState.ModuleIdValue,
                new CharacterTestRuntimeStateView<CharacterTestPhysicalShieldActiveSnapshot>(() => _activeShields.Count, ReadActiveShield),
                null, true);
            _stateView.ShieldRuntime = _runtime.ReadStateView(CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleId, CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleIdentity);
            return _stateView;
        }

        private CharacterTestPhysicalShieldActiveSnapshot ReadActiveShield(int i)
        {
                ActiveShield shield = _activeShields[i];
                FixVector3 center = FixVector3.Zero;
                if (_world != null && _world.TryGetBody(shield.BodyId, out CombatPhysicsBody body))
                    center = body.Position;

                return new CharacterTestPhysicalShieldActiveSnapshot(
                    shield.OwnerId,
                    shield.ShieldEntityId.Value,
                    shield.BodyId.Value,
                    center.X.RawValue,
                    center.Y.RawValue,
                    center.Z.RawValue,
                    shield.EndFrame);
        }

        public CharacterTestPhysicalShieldRuntimeState CreateStateSnapshot()
        {
            if (_runtime == null)
                return null;

            var active = new CharacterTestPhysicalShieldActiveSnapshot[_activeShields.Count];
            for (int i = 0; i < _activeShields.Count; i++)
            {
                ActiveShield shield = _activeShields[i];
                FixVector3 center = FixVector3.Zero;
                if (_world != null && _world.TryGetBody(shield.BodyId, out CombatPhysicsBody body))
                    center = body.Position;

                active[i] = new CharacterTestPhysicalShieldActiveSnapshot(
                    shield.OwnerId,
                    shield.ShieldEntityId.Value,
                    shield.BodyId.Value,
                    center.X.RawValue,
                    center.Y.RawValue,
                    center.Z.RawValue,
                    shield.EndFrame);
            }

            return new CharacterTestPhysicalShieldRuntimeState(
                CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion,
                CharacterTestPhysicalShieldRuntimeState.ModuleIdValue,
                active,
                _runtime.CreateStateSnapshot(
                    CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleId, CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleIdentity));
        }

        public bool TryApplyStateSnapshot(
            CharacterTestPhysicalShieldRuntimeState state,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            out string failure)
        {
            failure = string.Empty;
            if (state != null
                && (state.SchemaVersion != CharacterTestPhysicalShieldRuntimeState.CurrentSchemaVersion
                    || state.ModuleId != CharacterTestPhysicalShieldRuntimeState.ModuleIdValue))
            {
                failure = "Unsupported CharacterTest physical shield runtime state schema.";
                return false;
            }

            // Validate the complete incoming graph before Configure/Clear can change
            // physics bodies or existing shield state.
            if (state != null)
            {
                if (state.ActiveShields == null
                    || (state.ActiveShieldCount > 0 && state.ShieldRuntime == null)
                    || (state.ShieldRuntime != null
                        && (state.ShieldRuntime.SchemaVersion != CombatShieldRuntimeState.CurrentSchemaVersion
                            || state.ShieldRuntime.ModuleIdentity != CharacterTestPhysicalShieldRuntimeState.ShieldRuntimeModuleIdentity)))
                {
                    failure = "Physical shield state contains a missing or incompatible nested state.";
                    return false;
                }
                var shieldIds = new HashSet<int>();
                var ownerIds = new HashSet<int>();
                for (int i = 0; i < state.ActiveShieldCount; i++)
                {
                    CharacterTestPhysicalShieldActiveSnapshot active = state.ActiveShields[i];
                    if (active.OwnerId <= 0 || active.ShieldEntityId <= 0 || active.BodyId <= 0
                        || !shieldIds.Add(active.ShieldEntityId) || !ownerIds.Add(active.OwnerId))
                    {
                        failure = "Physical shield state contains an invalid or repeated instance identity.";
                        return false;
                    }
                }
                if (state.ShieldRuntime != null)
                {
                    var potencyIds = new HashSet<int>();
                    for (int i = 0; i < state.ShieldRuntime.Shields.Count; i++)
                    {
                        CombatShieldPotencyComponent potency = state.ShieldRuntime.Shields[i];
                        if (!shieldIds.Contains(potency.ShieldEntityId.Value)
                            || !potencyIds.Add(potency.ShieldEntityId.Value)
                            || potency.BodyId.Value <= 0 || potency.ColliderId.Value <= 0)
                        {
                            failure = "Physical shield potency state contains an invalid or missing instance reference.";
                            return false;
                        }
                    }
                    if (potencyIds.Count != shieldIds.Count)
                    {
                        failure = "Physical shield potency coverage does not match its active instances.";
                        return false;
                    }
                }
            }

            if (physicsRuntime == null || physicsRuntime.World == null)
            {
                if (state == null || state.ActiveShieldCount == 0)
                {
                    ClearActiveState();
                    Configure(null);
                    return true;
                }

                failure = "CharacterTest physical shield physics runtime is missing.";
                return false;
            }

            Configure(physicsRuntime.World);
            ClearActiveState();
            if (_runtime == null)
                _runtime = new CombatShieldRuntime(physicsRuntime.World);

            if (state == null || state.ActiveShieldCount == 0)
            {
                _runtime.Clear();
                LastTickSummary = default;
                _createdOrRefreshedThisFrame = 0;
                return true;
            }

            if (state.ShieldRuntime == null)
            {
                failure = "CharacterTest physical shield potency state is missing.";
                return false;
            }

            IReadOnlyList<CharacterTestPhysicalShieldActiveSnapshot> activeShields = state.ActiveShields;
            for (int i = 0; i < activeShields.Count; i++)
            {
                CharacterTestPhysicalShieldActiveSnapshot active = activeShields[i];
                if (active.OwnerId <= 0 || active.ShieldEntityId <= 0 || active.BodyId <= 0)
                    continue;

                var shieldEntityId = new CombatEntityId(active.ShieldEntityId);
                var bodyId = new CombatBodyId(active.BodyId);
                var colliderId = new CombatColliderId(ShieldColliderId);
                var center = new FixVector3(
                    Fix64.FromRaw(active.CenterXRaw),
                    Fix64.FromRaw(active.CenterYRaw),
                    Fix64.FromRaw(active.CenterZRaw));
                physicsRuntime.World.UpsertBody(new CombatPhysicsBody(shieldEntityId, bodyId, center));
                physicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    bodyId,
                    colliderId,
                    ShieldLayer,
                    -ShieldHalfExtents,
                    ShieldHalfExtents));
                _activeShields.Add(new ActiveShield(
                    active.OwnerId,
                    shieldEntityId,
                    bodyId,
                    active.EndFrame));
            }

            try
            {
                _runtime.RestoreRuntimeState(state.ShieldRuntime);
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                ClearActiveState();
                return false;
            }

            _createdOrRefreshedThisFrame = 0;
            LastTickSummary = new CharacterTestPhysicalShieldTickSummary(
                _activeShields.Count,
                createdOrRefreshedCount: 0,
                expiredCount: 0);
            return true;
        }

        public RuntimeCommandValidationResult EnqueueCast(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame)
        {
            if (world == null)
                return Failed(frame, caster, "CharacterTest physical shield world is missing.");
            if (physicsRuntime == null || physicsRuntime.World == null)
                return Failed(frame, caster, "CharacterTest physical shield physics runtime is unavailable.");
            if (caster == null || !caster.BattleState.CountsAsAlive)
                return Failed(frame, caster, "CharacterTest physical shield caster is invalid.");

            Configure(physicsRuntime.World);
            if (_runtime == null)
                return Failed(frame, caster, "CharacterTest physical shield runtime is unavailable.");

            CombatEntityId ownerEntityId = physicsRuntime.GetEntityId(caster);
            CombatEntityId shieldEntityId = GetShieldEntityId(caster.Id.Value);
            CombatBodyId bodyId = GetShieldBodyId(caster.Id.Value);
            var colliderId = new CombatColliderId(ShieldColliderId);
            FixVector3 center = ResolveShieldCenter(caster);

            physicsRuntime.World.UpsertBody(new CombatPhysicsBody(shieldEntityId, bodyId, center));
            physicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                ShieldLayer,
                -ShieldHalfExtents,
                ShieldHalfExtents));
            _runtime.UpsertShield(new CombatShieldPotencyComponent(
                shieldEntityId,
                ownerEntityId,
                bodyId,
                colliderId,
                BasePotency,
                BaseCohesionLimit,
                RecoveryRate,
                RecoveryDelayFrames));
            UpsertActive(caster.Id.Value, shieldEntityId, bodyId, frame.Value + LifetimeFrames);
            _createdOrRefreshedThisFrame++;
            return RuntimeCommandValidationResult.Accepted(CreateDiagnosticCommand(frame, caster));
        }

        /// <summary>
        /// Advances active CharacterTest shield bodies and shield pressure state. NoAlloc after initialization.
        /// </summary>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            if (_runtime == null || physicsRuntime == null || physicsRuntime.World == null)
            {
                LastTickSummary = default;
                _createdOrRefreshedThisFrame = 0;
                return;
            }

            int expiredCount = 0;
            for (int i = _activeShields.Count - 1; i >= 0; i--)
            {
                ActiveShield active = _activeShields[i];
                if (frame.Value >= active.EndFrame || !TryFindEntity(world, active.OwnerId, out CharacterTestCharacterEntity owner))
                {
                    RemoveActiveAt(i);
                    expiredCount++;
                    continue;
                }

                SyncShieldBody(owner, active.BodyId);
            }

            _runtime.Tick(ToCombatFrame(frame));
            LastTickSummary = new CharacterTestPhysicalShieldTickSummary(
                _activeShields.Count,
                _createdOrRefreshedThisFrame,
                expiredCount);
            _createdOrRefreshedThisFrame = 0;
        }

        private void UpsertActive(
            int ownerId,
            CombatEntityId shieldEntityId,
            CombatBodyId bodyId,
            long endFrame)
        {
            for (int i = 0; i < _activeShields.Count; i++)
            {
                if (_activeShields[i].OwnerId == ownerId)
                {
                    _activeShields[i] = new ActiveShield(ownerId, shieldEntityId, bodyId, endFrame);
                    return;
                }
            }

            _activeShields.Add(new ActiveShield(ownerId, shieldEntityId, bodyId, endFrame));
        }

        private void RemoveActiveAt(int index)
        {
            ActiveShield active = _activeShields[index];
            _runtime?.RemoveShield(active.ShieldEntityId);
            _world?.RemoveBody(active.BodyId);
            _activeShields.RemoveAt(index);
        }

        private void ClearActiveState()
        {
            for (int i = _activeShields.Count - 1; i >= 0; i--)
            {
                ActiveShield active = _activeShields[i];
                _world?.RemoveBody(active.BodyId);
            }

            _activeShields.Clear();
            _runtime?.Clear();
            _createdOrRefreshedThisFrame = 0;
            LastTickSummary = default;
        }

        private void SyncShieldBody(CharacterTestCharacterEntity owner, CombatBodyId bodyId)
        {
            _world.SetBodyPosition(bodyId, ResolveShieldCenter(owner));
        }

        private static bool TryFindEntity(
            CharacterTestCharacterRuntimeWorld world,
            int ownerId,
            out CharacterTestCharacterEntity entity)
        {
            entity = null;
            if (world == null)
                return false;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (candidate != null && candidate.Id.Value == ownerId)
                {
                    entity = candidate;
                    return true;
                }
            }

            return false;
        }

        private static FixVector3 ResolveShieldCenter(CharacterTestCharacterEntity owner)
        {
            CharacterTestCharacterTransformComponent transform = owner.Transform;
            ResolveForwardMillimeters(transform.YawMilliDegrees, out int forwardX, out int forwardZ);
            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    transform.XMillimeters + ScaleMillimeters(forwardX, ForwardOffsetMillimeters)),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    transform.YMillimeters + HeightOffsetMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                    transform.ZMillimeters + ScaleMillimeters(forwardZ, ForwardOffsetMillimeters)));
        }

        private static void ResolveForwardMillimeters(int yawMilliDegrees, out int forwardX, out int forwardZ)
        {
            double yawRadians = CharacterTestDeterministicUnits.MilliDegreesToDegrees(yawMilliDegrees)
                * Math.PI
                / 180.0;
            forwardX = (int)Math.Round(Math.Sin(yawRadians) * 1000.0);
            forwardZ = (int)Math.Round(Math.Cos(yawRadians) * 1000.0);
            if (forwardX != 0 || forwardZ != 0)
                return;

            forwardX = 0;
            forwardZ = 1000;
        }

        private static int ScaleMillimeters(int normalizedMilli, int millimeters)
        {
            return (int)Math.Round((double)normalizedMilli * millimeters / 1000.0);
        }

        private static CombatFrame ToCombatFrame(RuntimeFrame frame)
        {
            long value = frame.Value;
            if (value < 0L)
                value = 0L;
            if (value > int.MaxValue)
                value = int.MaxValue;
            return new CombatFrame((int)value);
        }

        private static CombatEntityId GetShieldEntityId(int ownerId)
        {
            return new CombatEntityId(ShieldEntityIdStart + Math.Max(0, ownerId));
        }

        private static CombatBodyId GetShieldBodyId(int ownerId)
        {
            return new CombatBodyId(ShieldBodyIdStart + Math.Max(0, ownerId));
        }

        private static RuntimeCommandValidationResult Failed(
            RuntimeFrame frame,
            CharacterTestCharacterEntity caster,
            string message)
        {
            RuntimeCommand command = CreateDiagnosticCommand(frame, caster);
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                frame,
                message));
        }

        private static RuntimeCommand CreateDiagnosticCommand(
            RuntimeFrame frame,
            CharacterTestCharacterEntity caster)
        {
            return new RuntimeCommand(
                frame,
                caster != null ? caster.Id.Value : 0,
                commandId: 0,
                targetId: 0,
                traceId: new RuntimeTraceIdentity(90872, caster != null ? caster.Id.Value : 0, frame.Value, CharacterTestCharacterActionConfigSource.PhysicalShieldGameplayRequestId));
        }

        private readonly struct ActiveShield
        {
            public ActiveShield(
                int ownerId,
                CombatEntityId shieldEntityId,
                CombatBodyId bodyId,
                long endFrame)
            {
                OwnerId = ownerId;
                ShieldEntityId = shieldEntityId;
                BodyId = bodyId;
                EndFrame = endFrame;
            }

            public int OwnerId { get; }
            public CombatEntityId ShieldEntityId { get; }
            public CombatBodyId BodyId { get; }
            public long EndFrame { get; }
        }
    }
}
