using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestRuntimeAiPlannerFactKeys
    {
        public static readonly AiFactKey TargetExists =
            new AiFactKey("character-test.runtime-ai-planner.target.exists");
        public static readonly AiFactKey TargetInAttackRange =
            new AiFactKey("character-test.runtime-ai-planner.target.inAttackRange");
        public static readonly AiFactKey TargetVisible =
            new AiFactKey("character-test.runtime-ai-planner.target.visible");
        public static readonly AiFactKey TargetLineOfSightBlocked =
            new AiFactKey("character-test.runtime-ai-planner.target.lineOfSightBlocked");
        public static readonly AiFactKey TargetLastSeenAgeFrames =
            new AiFactKey("character-test.runtime-ai-planner.target.lastSeenAgeFrames");
        public static readonly AiFactKey TargetDistance =
            new AiFactKey("character-test.runtime-ai-planner.target.distance");
        public static readonly AiFactKey TargetDistanceAvailable =
            new AiFactKey("character-test.runtime-ai-planner.target.distanceAvailable");
        public static readonly AiFactKey TargetRootDistance =
            new AiFactKey("character-test.runtime-ai-planner.target.rootDistance");
        public static readonly AiFactKey TargetThreatScore =
            new AiFactKey("character-test.runtime-ai-planner.target.threatScore");
        public static readonly AiFactKey TargetDead =
            new AiFactKey("character-test.runtime-ai-planner.target.dead");
        public static readonly AiFactKey TargetLifeState =
            new AiFactKey("character-test.runtime-ai-planner.target.life-state");
        public static readonly AiFactKey SelfActionRunning =
            new AiFactKey("character-test.runtime-ai-planner.self.actionRunning");
        public static readonly AiFactKey SelfActionInterruptible =
            new AiFactKey("character-test.runtime-ai-planner.self.actionInterruptible");
        public static readonly AiFactKey SelfSurfaceKind =
            new AiFactKey("character-test.runtime-ai-planner.self.surfaceKind");
        public static readonly AiFactKey SelfOnHardSlick =
            new AiFactKey("character-test.runtime-ai-planner.self.onHardSlick");
        public static readonly AiFactKey SelfNearMovableObject =
            new AiFactKey("character-test.runtime-ai-planner.self.nearMovableObject");
        public static readonly AiFactKey MovePathAvailable =
            new AiFactKey("character-test.runtime-ai-planner.move.pathAvailable");
        public static readonly AiFactKey MoveDirectPathClear =
            new AiFactKey("character-test.runtime-ai-planner.move.directPathClear");
        public static readonly AiFactKey MoveCandidateIndex =
            new AiFactKey("character-test.runtime-ai-planner.move.candidateIndex");
        public static readonly AiFactKey MoveDirectionX =
            new AiFactKey("character-test.runtime-ai-planner.move.x");
        public static readonly AiFactKey MoveDirectionZ =
            new AiFactKey("character-test.runtime-ai-planner.move.z");
        public static readonly AiFactKey EnvironmentNearObstacle =
            new AiFactKey("character-test.runtime-ai-planner.environment.nearObstacle");
        public static readonly AiFactKey EnvironmentNearHazard =
            new AiFactKey("character-test.runtime-ai-planner.environment.nearHazard");
        public static readonly AiFactKey EnvironmentNearUsefulObject =
            new AiFactKey("character-test.runtime-ai-planner.environment.nearUsefulObject");
        public static readonly AiFactKey TargetAttackWillHitSelf =
            new AiFactKey("character-test.runtime-ai-planner.target.attackWillHitSelf");
        public static readonly AiFactKey TargetAttackImpactFrames =
            new AiFactKey("character-test.runtime-ai-planner.target.attackImpactFrames");
        public static readonly AiFactKey TargetAttackInstanceResponded =
            new AiFactKey("character-test.runtime-ai-planner.target.attackInstanceResponded");
        public static readonly AiFactKey SelfDodgeCooldownReady =
            new AiFactKey("character-test.runtime-ai-planner.self.dodgeCooldownReady");
    }

    [Flags]
    internal enum CharacterTestRuntimeAiPerceptionRequirements
    {
        None = 0, PrimaryAttack = 1, Navigation = 2, Environment = 4,
        All = PrimaryAttack | Navigation | Environment
    }

    internal sealed class CharacterTestRuntimeAiPlannerFactState : IAiWorldState
    {
        internal bool PrimaryAttackFactObserved { get; private set; } = true;
        internal bool NavigationObserved { get; private set; } = true;
        internal bool EnvironmentObserved { get; private set; } = true;
        internal void SetPerceptionRequirements(CharacterTestRuntimeAiPerceptionRequirements requirements)
        {
            NavigationObserved = (requirements & CharacterTestRuntimeAiPerceptionRequirements.Navigation) != 0;
            EnvironmentObserved = (requirements & CharacterTestRuntimeAiPerceptionRequirements.Environment) != 0;
        }
        private static readonly AiFactKey[] NavigationKeys = { CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable,
            CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear, CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex,
            CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX, CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ };
        private static readonly AiFactKey[] EnvironmentKeys = { CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle,
            CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard, CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject,
            CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind, CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick,
            CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject };
        private bool IsObserved(AiFactKey key)
        {
            if (!PrimaryAttackFactObserved && key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange)) return false;
            if (!NavigationObserved) foreach (var value in NavigationKeys) if (value.Equals(key)) return false;
            if (!EnvironmentObserved) foreach (var value in EnvironmentKeys) if (value.Equals(key)) return false;
            return true;
        }

        internal void SetPrimaryAttackFactObserved(bool observed) => PrimaryAttackFactObserved = observed;

        public CharacterTestRuntimeAiPlannerTargetStateMask TargetStateMask { get; private set; } =
            CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget;
        public bool TargetExists => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.ExistingPhaseMask);
        public bool TargetInAttackRange => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange);
        public bool TargetVisible => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable);
        public bool TargetLineOfSightBlocked => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked);
        public int TargetLastSeenAgeFrames { get; private set; }
        public float TargetThreatScore { get; private set; }
        public bool TargetDead => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.Dying
            | CharacterTestRuntimeAiPlannerTargetStateMask.Dead
            | CharacterTestRuntimeAiPlannerTargetStateMask.Reviving);
        public CharacterTestRuntimeAiPlannerTargetLifeState TargetLifeState =>
            ResolveTargetLifeState(TargetStateMask);
        public bool SelfActionRunning { get; private set; }
        public bool SelfActionInterruptible { get; private set; } = true;
        internal float SelfHealthRatio { get; private set; } = float.NaN;
        internal int SelfHealthObservedFrame { get; private set; } = -1;
        internal void CaptureSelfHealth(CharacterTestCharacterEntity entity, int frame)
        {
            SelfHealthRatio = entity != null && entity.CombatState.MaxHealth > 0
                ? (float)entity.CombatState.Health / entity.CombatState.MaxHealth : float.NaN;
            SelfHealthObservedFrame = frame;
        }
        public int SelfSurfaceKind { get; private set; }
        public bool SelfOnHardSlick { get; private set; }
        public bool SelfNearMovableObject { get; private set; }
        public bool MovePathAvailable => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable);
        public float TargetDistance { get; private set; }
        public bool TargetDistanceAvailable { get; private set; }
        public float TargetRootDistance { get; private set; }
        public int TargetEntityId { get; private set; }
        public Vector2 MoveDirection { get; private set; }
        public bool DirectPathClear { get; private set; }
        public int NavigationCandidateIndex { get; private set; }
        public bool EnvironmentNearObstacle { get; private set; }
        public bool EnvironmentNearHazard { get; private set; }
        public bool EnvironmentNearUsefulObject { get; private set; }
        public bool TargetAttackWillHitSelf => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf);
        public int TargetAttackImpactFrames { get; private set; } = -1;
        public bool TargetAttackInstanceResponded => HasAny(
            CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded);
        public bool SelfDodgeCooldownReady { get; private set; } = true;
        public int TargetAttackSequence { get; private set; }
        public CharacterActionId TargetAttackActionId { get; private set; }

        public void SetNoTarget(bool selfActionRunning)
        {
            PrimaryAttackFactObserved = true;
            NavigationObserved = true;
            TargetStateMask = CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget;
            TargetLastSeenAgeFrames = 0;
            TargetThreatScore = 0f;
            SelfActionRunning = selfActionRunning;
            SelfActionInterruptible = !selfActionRunning;
            TargetDistance = 0f;
            TargetDistanceAvailable = false;
            TargetRootDistance = 0f;
            TargetEntityId = 0;
            MoveDirection = Vector2.zero;
            DirectPathClear = false;
            NavigationCandidateIndex = -1;
            SetTargetAttackThreat(CharacterActionId.None, 0, -1, false, false, true);
        }

        public void SetTarget(
            int targetEntityId,
            float targetDistance,
            CharacterTestAiNavigationResult navigation,
            bool selfActionRunning,
            bool targetVisible,
            bool targetLineOfSightBlocked,
            int targetLastSeenAgeFrames,
            float targetThreatScore,
            bool targetDead = false,
            CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState =
                CharacterTestRuntimeAiPlannerTargetLifeState.None)
        {
            SetTarget(
                targetEntityId,
                targetDistance,
                true,
                targetDistance,
                navigation,
                selfActionRunning,
                targetVisible,
                targetLineOfSightBlocked,
                targetLastSeenAgeFrames,
                targetThreatScore,
                targetDead,
                targetLifeState);
        }

        public void SetTarget(
            int targetEntityId,
            float targetDistance,
            bool targetDistanceAvailable,
            float targetRootDistance,
            CharacterTestAiNavigationResult navigation,
            bool selfActionRunning,
            bool targetVisible,
            bool targetLineOfSightBlocked,
            int targetLastSeenAgeFrames,
            float targetThreatScore,
            bool targetDead = false,
            CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState =
                CharacterTestRuntimeAiPlannerTargetLifeState.None)
        {
            TargetEntityId = targetEntityId;
            TargetDistanceAvailable = targetDistanceAvailable;
            TargetDistance = targetDistanceAvailable && targetDistance > 0f
                ? targetDistance
                : 0f;
            TargetRootDistance = targetRootDistance > 0f ? targetRootDistance : 0f;
            TargetLastSeenAgeFrames = targetLastSeenAgeFrames < 0 ? 0 : targetLastSeenAgeFrames;
            TargetThreatScore = targetThreatScore > 0f ? targetThreatScore : 0f;
            CharacterTestRuntimeAiPlannerTargetLifeState normalizedLifeState =
                NormalizeTargetLifeState(targetLifeState, targetDead);
            TargetStateMask = ToPhaseMask(normalizedLifeState);
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable,
                targetVisible);
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked,
                targetLineOfSightBlocked);
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange,
                navigation.TargetInAttackRange);
            MoveDirection = navigation.Move;
            DirectPathClear = navigation.DirectPathClear;
            NavigationCandidateIndex = navigation.SelectedCandidateIndex;
            SelfActionRunning = selfActionRunning;
            SelfActionInterruptible = !selfActionRunning;
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable,
                navigation.TargetInAttackRange || navigation.Move.sqrMagnitude > 0.0001f);
        }

        private static CharacterTestRuntimeAiPlannerTargetLifeState NormalizeTargetLifeState(
            CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState,
            bool targetDead)
        {
            if (targetLifeState >= CharacterTestRuntimeAiPlannerTargetLifeState.Alive
                && targetLifeState <= CharacterTestRuntimeAiPlannerTargetLifeState.Reviving)
            {
                return targetLifeState;
            }

            return targetDead
                ? CharacterTestRuntimeAiPlannerTargetLifeState.Dead
                : CharacterTestRuntimeAiPlannerTargetLifeState.Alive;
        }

        public void SetEnvironment(
            int surfaceKind,
            bool onHardSlick,
            bool nearMovableObject,
            bool nearObstacle,
            bool nearHazard,
            bool nearUsefulObject)
        {
            SelfSurfaceKind = surfaceKind;
            SelfOnHardSlick = onHardSlick;
            SelfNearMovableObject = nearMovableObject;
            EnvironmentNearObstacle = nearObstacle;
            EnvironmentNearHazard = nearHazard;
            EnvironmentNearUsefulObject = nearUsefulObject;
        }

        public void SetSelfActionState(bool running, bool interruptible)
        {
            SelfActionRunning = running;
            SelfActionInterruptible = !running || interruptible;
        }

        public void SetTargetAttackThreat(
            CharacterActionId actionId,
            int attackSequence,
            int impactFrames,
            bool willHitSelf,
            bool instanceResponded,
            bool dodgeCooldownReady)
        {
            TargetAttackActionId = actionId;
            TargetAttackSequence = attackSequence > 0 ? attackSequence : 0;
            TargetAttackImpactFrames = impactFrames >= 0 ? impactFrames : -1;
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf,
                willHitSelf);
            SetTargetStateFlag(
                CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded,
                instanceResponded);
            SelfDodgeCooldownReady = dodgeCooldownReady;
        }

        private bool HasAny(CharacterTestRuntimeAiPlannerTargetStateMask mask) =>
            (TargetStateMask & mask) != 0;

        private void SetTargetStateFlag(
            CharacterTestRuntimeAiPlannerTargetStateMask flag,
            bool enabled)
        {
            if (enabled && TargetExists)
                TargetStateMask |= flag;
            else
                TargetStateMask &= ~flag;
        }

        private void SetTargetLifeState(CharacterTestRuntimeAiPlannerTargetLifeState state)
        {
            CharacterTestRuntimeAiPlannerTargetStateMask phase = ToPhaseMask(state);
            CharacterTestRuntimeAiPlannerTargetStateMask flags = state
                    == CharacterTestRuntimeAiPlannerTargetLifeState.None
                ? CharacterTestRuntimeAiPlannerTargetStateMask.None
                : TargetStateMask & CharacterTestRuntimeAiPlannerTargetStateMask.FlagMask;
            TargetStateMask = phase | flags;
        }

        private static CharacterTestRuntimeAiPlannerTargetStateMask ToPhaseMask(
            CharacterTestRuntimeAiPlannerTargetLifeState state) => state switch
            {
                CharacterTestRuntimeAiPlannerTargetLifeState.Alive =>
                    CharacterTestRuntimeAiPlannerTargetStateMask.Alive,
                CharacterTestRuntimeAiPlannerTargetLifeState.Dying =>
                    CharacterTestRuntimeAiPlannerTargetStateMask.Dying,
                CharacterTestRuntimeAiPlannerTargetLifeState.Dead =>
                    CharacterTestRuntimeAiPlannerTargetStateMask.Dead,
                CharacterTestRuntimeAiPlannerTargetLifeState.Reviving =>
                    CharacterTestRuntimeAiPlannerTargetStateMask.Reviving,
                _ => CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget
            };

        private static CharacterTestRuntimeAiPlannerTargetLifeState ResolveTargetLifeState(
            CharacterTestRuntimeAiPlannerTargetStateMask state)
        {
            if ((state & CharacterTestRuntimeAiPlannerTargetStateMask.Alive) != 0)
                return CharacterTestRuntimeAiPlannerTargetLifeState.Alive;
            if ((state & CharacterTestRuntimeAiPlannerTargetStateMask.Dying) != 0)
                return CharacterTestRuntimeAiPlannerTargetLifeState.Dying;
            if ((state & CharacterTestRuntimeAiPlannerTargetStateMask.Dead) != 0)
                return CharacterTestRuntimeAiPlannerTargetLifeState.Dead;
            if ((state & CharacterTestRuntimeAiPlannerTargetStateMask.Reviving) != 0)
                return CharacterTestRuntimeAiPlannerTargetLifeState.Reviving;
            return CharacterTestRuntimeAiPlannerTargetLifeState.None;
        }

        public bool TryGetValue<T>(AiFactKey key, out T value)
        {
            value = default;
            if (!key.IsValid || !IsObserved(key))
                return false;

            object raw = null;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetExists))
                raw = TargetExists;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange))
                raw = TargetInAttackRange;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible))
                raw = TargetVisible;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked))
                raw = TargetLineOfSightBlocked;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames))
                raw = TargetLastSeenAgeFrames;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistance))
                raw = TargetDistance;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistanceAvailable))
                raw = TargetDistanceAvailable;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetRootDistance))
                raw = TargetRootDistance;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore))
                raw = TargetThreatScore;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDead))
                raw = TargetDead;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState))
                raw = TargetLifeState;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning))
                raw = SelfActionRunning;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionInterruptible))
                raw = SelfActionInterruptible;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind))
                raw = SelfSurfaceKind;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick))
                raw = SelfOnHardSlick;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject))
                raw = SelfNearMovableObject;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable))
                raw = MovePathAvailable;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear))
                raw = DirectPathClear;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex))
                raw = NavigationCandidateIndex;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX))
                raw = MoveDirection.x;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ))
                raw = MoveDirection.y;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle))
                raw = EnvironmentNearObstacle;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard))
                raw = EnvironmentNearHazard;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject))
                raw = EnvironmentNearUsefulObject;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackWillHitSelf))
                raw = TargetAttackWillHitSelf;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackImpactFrames))
                raw = TargetAttackImpactFrames;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackInstanceResponded))
                raw = TargetAttackInstanceResponded;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfDodgeCooldownReady))
                raw = SelfDodgeCooldownReady;

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        public bool Contains(AiFactKey key)
        {
            if (!IsObserved(key)) return false;
            return key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetExists)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistance)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistanceAvailable)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetRootDistance)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDead)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionInterruptible)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackWillHitSelf)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackImpactFrames)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackInstanceResponded)
                || key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfDodgeCooldownReady);
        }

        public object GetRawValue(AiFactKey key)
        {
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetExists))
                return TargetExists;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange))
                return TargetInAttackRange;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible))
                return TargetVisible;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked))
                return TargetLineOfSightBlocked;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames))
                return TargetLastSeenAgeFrames;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistance))
                return TargetDistance;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistanceAvailable))
                return TargetDistanceAvailable;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetRootDistance))
                return TargetRootDistance;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore))
                return TargetThreatScore;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDead))
                return TargetDead;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState))
                return TargetLifeState;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning))
                return SelfActionRunning;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionInterruptible))
                return SelfActionInterruptible;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind))
                return SelfSurfaceKind;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick))
                return SelfOnHardSlick;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject))
                return SelfNearMovableObject;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable))
                return MovePathAvailable;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear))
                return DirectPathClear;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex))
                return NavigationCandidateIndex;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX))
                return MoveDirection.x;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ))
                return MoveDirection.y;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle))
                return EnvironmentNearObstacle;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard))
                return EnvironmentNearHazard;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject))
                return EnvironmentNearUsefulObject;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackWillHitSelf))
                return TargetAttackWillHitSelf;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackImpactFrames))
                return TargetAttackImpactFrames;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackInstanceResponded))
                return TargetAttackInstanceResponded;
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfDodgeCooldownReady))
                return SelfDodgeCooldownReady;

            return null;
        }

        public void SetValue<T>(AiFactKey key, T value)
        {
            if (!key.IsValid)
                throw new ArgumentException("Runtime AI Planner fact key is invalid.", nameof(key));

            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetExists) && value is bool targetExists)
                SetTargetLifeState(targetExists
                    ? (TargetExists ? TargetLifeState : CharacterTestRuntimeAiPlannerTargetLifeState.Alive)
                    : CharacterTestRuntimeAiPlannerTargetLifeState.None);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange) && value is bool inRange)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.InAttackRange, inRange);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible) && value is bool visible)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.Perceivable, visible);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked) && value is bool blocked)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.LineOfSightBlocked, blocked);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames) && value is int seenAge)
                TargetLastSeenAgeFrames = seenAge < 0 ? 0 : seenAge;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistance) && value is float distance)
            {
                TargetDistance = distance;
                TargetDistanceAvailable = true;
            }
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDistanceAvailable) && value is bool distanceAvailable)
                TargetDistanceAvailable = distanceAvailable;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetRootDistance) && value is float rootDistance)
                TargetRootDistance = rootDistance;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore) && value is float threat)
                TargetThreatScore = threat;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDead) && value is bool targetDead)
                SetTargetLifeState(targetDead
                    ? CharacterTestRuntimeAiPlannerTargetLifeState.Dead
                    : CharacterTestRuntimeAiPlannerTargetLifeState.Alive);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState)
                && value is CharacterTestRuntimeAiPlannerTargetLifeState targetLifeState
                && targetLifeState >= CharacterTestRuntimeAiPlannerTargetLifeState.None
                && targetLifeState <= CharacterTestRuntimeAiPlannerTargetLifeState.Reviving)
            {
                SetTargetLifeState(targetLifeState);
            }
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState)
                && value is int targetLifeStateValue
                && targetLifeStateValue >= (int)CharacterTestRuntimeAiPlannerTargetLifeState.None
                && targetLifeStateValue <= (int)CharacterTestRuntimeAiPlannerTargetLifeState.Reviving)
            {
                SetTargetLifeState((CharacterTestRuntimeAiPlannerTargetLifeState)targetLifeStateValue);
            }
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning) && value is bool running)
                SetSelfActionState(running, interruptible: !running);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfActionInterruptible)
                && value is bool interruptible)
                SelfActionInterruptible = interruptible;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind) && value is int surfaceKind)
                SelfSurfaceKind = surfaceKind;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick) && value is bool onHardSlick)
                SelfOnHardSlick = onHardSlick;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject) && value is bool nearMovable)
                SelfNearMovableObject = nearMovable;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable) && value is bool pathAvailable)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable, pathAvailable);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear) && value is bool directClear)
                DirectPathClear = directClear;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex) && value is int candidateIndex)
                NavigationCandidateIndex = candidateIndex;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX) && value is float x)
                MoveDirection = new Vector2(x, MoveDirection.y);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ) && value is float z)
                MoveDirection = new Vector2(MoveDirection.x, z);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle) && value is bool obstacle)
                EnvironmentNearObstacle = obstacle;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard) && value is bool hazard)
                EnvironmentNearHazard = hazard;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject) && value is bool useful)
                EnvironmentNearUsefulObject = useful;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackWillHitSelf) && value is bool willHit)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.AttackThreatToSelf, willHit);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackImpactFrames) && value is int impactFrames)
                TargetAttackImpactFrames = impactFrames >= 0 ? impactFrames : -1;
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetAttackInstanceResponded) && value is bool responded)
                SetTargetStateFlag(CharacterTestRuntimeAiPlannerTargetStateMask.AttackInstanceResponded, responded);
            else if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.SelfDodgeCooldownReady) && value is bool cooldownReady)
                SelfDodgeCooldownReady = cooldownReady;
        }

        public bool Remove(AiFactKey key)
        {
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetDead))
            {
                if (TargetExists)
                    SetTargetLifeState(CharacterTestRuntimeAiPlannerTargetLifeState.Alive);
                return true;
            }
            if (key.Equals(CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState))
            {
                SetTargetLifeState(CharacterTestRuntimeAiPlannerTargetLifeState.None);
                return true;
            }

            return false;
        }

        public IAiWorldState Clone()
        {
            return this;
        }

        public IReadOnlyDictionary<AiFactKey, object> Snapshot()
        {
            var snapshot = new Dictionary<AiFactKey, object>(26)
            {
                [CharacterTestRuntimeAiPlannerFactKeys.TargetExists] = TargetExists,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange] = TargetInAttackRange,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetVisible] = TargetVisible,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked] = TargetLineOfSightBlocked,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames] = TargetLastSeenAgeFrames,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetDistance] = TargetDistance,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetDistanceAvailable] = TargetDistanceAvailable,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetRootDistance] = TargetRootDistance,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore] = TargetThreatScore,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetDead] = TargetDead,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetLifeState] = TargetLifeState,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning] = SelfActionRunning,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfActionInterruptible] = SelfActionInterruptible,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind] = SelfSurfaceKind,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick] = SelfOnHardSlick,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject] = SelfNearMovableObject,
                [CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable] = MovePathAvailable,
                [CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear] = DirectPathClear,
                [CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex] = NavigationCandidateIndex,
                [CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX] = MoveDirection.x,
                [CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ] = MoveDirection.y,
                [CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle] = EnvironmentNearObstacle,
                [CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard] = EnvironmentNearHazard,
                [CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject] = EnvironmentNearUsefulObject,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetAttackWillHitSelf] = TargetAttackWillHitSelf,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetAttackImpactFrames] = TargetAttackImpactFrames,
                [CharacterTestRuntimeAiPlannerFactKeys.TargetAttackInstanceResponded] = TargetAttackInstanceResponded,
                [CharacterTestRuntimeAiPlannerFactKeys.SelfDodgeCooldownReady] = SelfDodgeCooldownReady
            };
            if (!PrimaryAttackFactObserved) snapshot.Remove(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange);
            if (!NavigationObserved) foreach (var key in NavigationKeys) snapshot.Remove(key);
            if (!EnvironmentObserved) foreach (var key in EnvironmentKeys) snapshot.Remove(key);
            return snapshot;
        }
    }
}
