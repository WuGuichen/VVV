using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestAiTargetSelectionMode
    {
        NearestOpposingTeam = 0
    }

    public sealed class CharacterTestAiBehaviorProfile
    {
        private readonly float[] _navigationCandidateAngles;

        public CharacterTestAiBehaviorProfile(
            float attackRange = CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
            int plannerMaxDepth = CharacterTestAiBehaviorProfileSource.DefaultPlannerMaxDepth,
            int moveToTargetActionId = CharacterTestAiBehaviorProfileSource.DefaultMoveToTargetActionId,
            int lightAttackActionId = CharacterTestAiBehaviorProfileSource.DefaultLightAttackActionId,
            CharacterTestAiTargetSelectionMode targetSelectionMode =
                CharacterTestAiBehaviorProfileSource.DefaultTargetSelectionMode,
            float navigationProbeMinLength =
                CharacterTestAiBehaviorProfileSource.DefaultNavigationProbeMinLength,
            float navigationProbeMaxLength =
                CharacterTestAiBehaviorProfileSource.DefaultNavigationProbeMaxLength,
            IEnumerable<float> navigationCandidateAngles = null)
        {
            AttackRange = attackRange;
            PlannerMaxDepth = plannerMaxDepth;
            MoveToTargetActionId = moveToTargetActionId;
            LightAttackActionId = lightAttackActionId;
            TargetSelectionMode = targetSelectionMode;
            NavigationProbeMinLength = navigationProbeMinLength;
            NavigationProbeMaxLength = navigationProbeMaxLength;
            _navigationCandidateAngles = CopyNavigationCandidateAngles(navigationCandidateAngles);
        }

        public float AttackRange { get; }

        public int PlannerMaxDepth { get; }

        public int MoveToTargetActionId { get; }

        public int LightAttackActionId { get; }

        public CharacterTestAiTargetSelectionMode TargetSelectionMode { get; }

        public float NavigationProbeMinLength { get; }

        public float NavigationProbeMaxLength { get; }

        public IReadOnlyList<float> NavigationCandidateAngles => _navigationCandidateAngles;

        internal float[] CopyNavigationCandidateAngles()
        {
            var copy = new float[_navigationCandidateAngles.Length];
            Array.Copy(_navigationCandidateAngles, copy, _navigationCandidateAngles.Length);
            return copy;
        }

        private static float[] CopyNavigationCandidateAngles(IEnumerable<float> angles)
        {
            if (angles == null)
                return Array.Empty<float>();

            var copy = new List<float>();
            foreach (float angle in angles)
            {
                if (float.IsNaN(angle) || float.IsInfinity(angle))
                    continue;

                copy.Add(angle);
            }

            return copy.ToArray();
        }
    }

    public static class CharacterTestAiBehaviorProfileSource
    {
        public const float DefaultAttackRange = 1.6f;
        public const int DefaultPlannerMaxDepth = 2;
        public const int DefaultMoveToTargetActionId = 910001;
        public const int DefaultLightAttackActionId = 910002;
        public const CharacterTestAiTargetSelectionMode DefaultTargetSelectionMode =
            CharacterTestAiTargetSelectionMode.NearestOpposingTeam;
        public const float DefaultNavigationProbeMinLength = 0.75f;
        public const float DefaultNavigationProbeMaxLength = 2f;

        private static readonly float[] DefaultNavigationCandidateAngles = { 45f, -45f, 90f, -90f };

        public static CharacterTestAiBehaviorProfile CreateDefault()
        {
            return new CharacterTestAiBehaviorProfile(
                DefaultAttackRange,
                DefaultPlannerMaxDepth,
                DefaultMoveToTargetActionId,
                DefaultLightAttackActionId,
                DefaultTargetSelectionMode,
                DefaultNavigationProbeMinLength,
                DefaultNavigationProbeMaxLength,
                DefaultNavigationCandidateAngles);
        }

        public static CharacterTestAiBehaviorProfile Normalize(CharacterTestAiBehaviorProfile profile)
        {
            CharacterTestAiBehaviorProfile source = profile ?? CreateDefault();
            float attackRange = IsPositiveFinite(source.AttackRange)
                ? source.AttackRange
                : DefaultAttackRange;
            int plannerMaxDepth = source.PlannerMaxDepth > 0
                ? source.PlannerMaxDepth
                : DefaultPlannerMaxDepth;
            int moveToTargetActionId = source.MoveToTargetActionId > 0
                ? source.MoveToTargetActionId
                : DefaultMoveToTargetActionId;
            int lightAttackActionId = source.LightAttackActionId > 0
                ? source.LightAttackActionId
                : DefaultLightAttackActionId;
            if (moveToTargetActionId == lightAttackActionId)
            {
                moveToTargetActionId = DefaultMoveToTargetActionId;
                lightAttackActionId = DefaultLightAttackActionId;
            }

            CharacterTestAiTargetSelectionMode targetSelectionMode =
                source.TargetSelectionMode == CharacterTestAiTargetSelectionMode.NearestOpposingTeam
                    ? source.TargetSelectionMode
                    : DefaultTargetSelectionMode;

            float probeMinLength = source.NavigationProbeMinLength;
            float probeMaxLength = source.NavigationProbeMaxLength;
            if (!IsPositiveFinite(probeMinLength)
                || !IsPositiveFinite(probeMaxLength)
                || probeMinLength > probeMaxLength)
            {
                probeMinLength = DefaultNavigationProbeMinLength;
                probeMaxLength = DefaultNavigationProbeMaxLength;
            }

            float[] candidateAngles = source.CopyNavigationCandidateAngles();
            if (candidateAngles.Length == 0)
                candidateAngles = CopyDefaultNavigationCandidateAngles();

            return new CharacterTestAiBehaviorProfile(
                attackRange,
                plannerMaxDepth,
                moveToTargetActionId,
                lightAttackActionId,
                targetSelectionMode,
                probeMinLength,
                probeMaxLength,
                candidateAngles);
        }

        public static float[] CopyDefaultNavigationCandidateAngles()
        {
            var copy = new float[DefaultNavigationCandidateAngles.Length];
            Array.Copy(DefaultNavigationCandidateAngles, copy, DefaultNavigationCandidateAngles.Length);
            return copy;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
