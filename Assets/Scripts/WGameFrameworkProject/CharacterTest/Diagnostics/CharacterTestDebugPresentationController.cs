using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestDebugPresentationController
    {
        private readonly CharacterTestRuntimeContext _context;
        private readonly CharacterTestSceneProfile _profile;
        private readonly CharacterTestRuntimeSessionBuilder _sessionBuilder;
        private readonly CharacterTestCombatPhysicsGizmos.RuntimeBuffers _physicsGizmoBuffers = new();

        public CharacterTestDebugPresentationController(
            CharacterTestRuntimeContext context,
            CharacterTestSceneProfile profile,
            CharacterTestRuntimeSessionBuilder sessionBuilder)
        {
            _context = context;
            _profile = profile;
            _sessionBuilder = sessionBuilder;
        }

        public void HandleProceduralEffectPreviewHotkeys()
        {
            if (!_profile.EnableProceduralEffectPreviewHotkeys || _context.Slice == null)
                return;

            IInputProvider inputProvider = _sessionBuilder.ResolveProceduralEffectPreviewInputProvider();
            if (inputProvider == null)
                return;

            InputSnapshot snapshot = inputProvider.Snapshot;
            if (!CharacterTestProceduralEffectPreviewHotkeys.TryResolve(
                    snapshot,
                    out CharacterTestProceduralEffectPreviewId effectId,
                    out string triggerName))
            {
                return;
            }

            CharacterTestProceduralEffectPreviewResult result =
                _context.Slice.Services.Presentation.TryApplyLockedTargetProceduralEffectPreview(effectId, triggerName);
            _context.LogBuffer.Clear()
                .Append("Procedural effect preview ")
                .Append(result.Accepted ? "accepted" : "rejected")
                .Append(". key=")
                .Append(triggerName)
                .Append(" effect=")
                .Append(effectId.ToString())
                .Append(" target=")
                .Append(result.TargetEntityId)
                .Append(" message=")
                .Append(result.Message);
            if (result.Accepted)
                _context.Logger?.Info("CharacterTest.EffectPreview", _context.LogBuffer);
            else
                _context.Logger?.Warning("CharacterTest.EffectPreview", _context.LogBuffer);
        }

        public void DrawGizmos()
        {
            if (!_profile.DrawRuntimePhysicsDebugOnlyWhenSelected)
                DrawRuntimePhysicsDebugGizmos();

            if (!_profile.DrawAttackTraceDebugOnlyWhenSelected)
                DrawAttackTraceDebugGizmos();
        }

        public void DrawGizmosSelected()
        {
            if (_profile.DrawRuntimePhysicsDebugOnlyWhenSelected)
                DrawRuntimePhysicsDebugGizmos();

            if (_profile.DrawAttackTraceDebugOnlyWhenSelected)
                DrawAttackTraceDebugGizmos();
        }

        private void DrawRuntimePhysicsDebugGizmos()
        {
            if (!_profile.DrawRuntimePhysicsDebugGizmos
                || !Application.isPlaying
                || _context.Slice == null
                || _context.Slice.Services.Modules.PhysicsRuntime == null)
            {
                return;
            }

            CharacterTestGizmoDisplaySettings display = CharacterTestGizmoRuntimeDisplay.Current;
            if (display.RuntimePhysicsWorld)
            {
                CharacterTestCombatPhysicsGizmos.DrawRuntimeWorld(
                    _context.Slice.Services.Modules.PhysicsRuntime,
                    _context.Slice.Services.Modules.CharacterEntities,
                    _profile.RuntimePhysicsAabbGizmoColor,
                    _profile.RuntimePhysicsCharacterGizmoColor,
                    _physicsGizmoBuffers);
            }
            CharacterTestLargeCharacterPresenceDebug.DrawGizmos(
                _context.Slice.Services.Modules.CharacterRuntime,
                _context.Slice.Services.Modules.CharacterEntities,
                display.LargeCharacterLayers);

            if (_profile.DrawBodyPartContactDebugGizmos && display.BodyPartContacts)
            {
                CharacterTestCombatPhysicsGizmos.DrawBodyPartContactDebugSnapshot(
                    _context.Slice.Services.BodyPartContactDebugSnapshot,
                    _profile.BodyPartContactInactiveGizmoColor,
                    _profile.BodyPartContactProbeMissGizmoColor,
                    _profile.BodyPartContactHitGizmoColor,
                    _profile.BodyPartContactFilteredGizmoColor,
                    _profile.BodyPartContactAudioQueuedGizmoColor);
            }
        }

        private void DrawAttackTraceDebugGizmos()
        {
            if (!CharacterTestGizmoRuntimeDisplay.Current.ActiveTrace
                || !_profile.DrawAttackTraceDebugGizmos
                || !Application.isPlaying
                || _context.Slice == null
                || _context.Slice.Services.Modules.CombatTraceSystem == null)
            {
                return;
            }

            IReadOnlyList<CharacterTestAttackTraceDebugFrame> frames =
                _context.Slice.Services.Modules.CombatTraceSystem.AttackTraceDebugFrames;
            IReadOnlyList<CharacterTestHitPointDebugFrame> hitPoints =
                _context.Slice.Services.Modules.CombatTraceSystem.HitPointDebugFrames;
            if ((frames == null || frames.Count == 0)
                && (hitPoints == null || hitPoints.Count == 0))
                return;

            long currentFrame = _context.Slice.CurrentFrame.Value;
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                CharacterTestAttackTraceDebugFrame frame = frames[i];
                long age = currentFrame - frame.RuntimeFrame;
                if (age < 0L)
                    age = 0L;

                float alpha = Mathf.Clamp01(1f - (float)age / CharacterTestCharacterCombatTraceSystem.AttackTraceDebugHistoryFrames);
                if (alpha <= 0.02f)
                    continue;

                DrawAttackTraceDebugFrame(frame, alpha);
            }

            for (int i = 0; hitPoints != null && i < hitPoints.Count; i++)
            {
                CharacterTestHitPointDebugFrame frame = hitPoints[i];
                long age = currentFrame - frame.RuntimeFrame;
                if (age < 0L)
                    age = 0L;

                float alpha = Mathf.Clamp01(1f - (float)age / CharacterTestCharacterCombatTraceSystem.AttackTraceDebugHistoryFrames);
                if (alpha <= 0.02f)
                    continue;

                DrawAttackHitPointDebugFrame(frame, alpha);
            }
        }

        private void DrawAttackTraceDebugFrame(CharacterTestAttackTraceDebugFrame frame, float alpha)
        {
            float radius = Mathf.Max(0.01f, frame.Radius);
            Color previous = Gizmos.color;

            Color sweepColor = _profile.AttackTraceSweepGizmoColor;
            sweepColor.a *= alpha;
            Gizmos.color = sweepColor;
            DrawAttackTraceTipSweep(frame, radius);
            Gizmos.DrawLine(frame.RootPrev, frame.RootNow);
            Gizmos.DrawLine(frame.RootPrev, frame.TipPrev);

            Color currentColor = _profile.AttackTraceCurrentBladeGizmoColor;
            currentColor.a *= alpha;
            Gizmos.color = currentColor;
            DrawAttackTraceCapsuleLine(frame.RootNow, frame.TipNow, radius);

            if (frame.HasProximalRange)
            {
                Vector3 axis = frame.TipNow - frame.RootNow;
                if (axis.sqrMagnitude > 0.0001f)
                {
                    axis.Normalize();
                    Color proximalColor = Color.Lerp(currentColor, Color.white, 0.35f);
                    proximalColor.a = currentColor.a;
                    Gizmos.color = proximalColor;
                    DrawAttackTraceCapsuleLine(
                        frame.RootNow - axis * frame.ProximalExtension,
                        frame.RootNow,
                        Mathf.Max(0.01f, frame.ProximalRadius));
                }
            }

            Gizmos.color = previous;
        }

        private void DrawAttackHitPointDebugFrame(CharacterTestHitPointDebugFrame frame, float alpha)
        {
            Vector3 point = ToVector3(frame.HitPointWorld);
            Vector3 normal = ToVector3(frame.HitNormalWorld);
            if (normal.sqrMagnitude <= 0.0001f)
                normal = Vector3.up;
            else
                normal.Normalize();

            Color previous = Gizmos.color;
            Color color = frame.Kind == HitResolveKind.Blocked
                ? _profile.AttackHitPointBlockedGizmoColor
                : _profile.AttackHitPointDamageGizmoColor;
            color.a *= alpha;
            Gizmos.color = color;
            const float radius = 0.075f;
            Gizmos.DrawSphere(point, radius * 0.55f);
            Gizmos.DrawWireSphere(point, radius);
            Gizmos.DrawLine(point, point + normal * 0.32f);
            Gizmos.color = previous;
        }

        private static void DrawAttackTraceTipSweep(CharacterTestAttackTraceDebugFrame frame, float radius)
        {
            int substeps = GetAttackTraceSweepSubstepCount(frame);
            for (int i = 0; i < substeps; i++)
            {
                float t0 = (float)i / substeps;
                float t1 = (float)(i + 1) / substeps;
                DrawAttackTraceCapsuleLine(
                    Vector3.Lerp(frame.TipPrev, frame.TipNow, t0),
                    Vector3.Lerp(frame.TipPrev, frame.TipNow, t1),
                    radius);
            }
        }

        private static int GetAttackTraceSweepSubstepCount(CharacterTestAttackTraceDebugFrame frame)
        {
            float maxDistance = Mathf.Max(
                0.001f,
                CharacterTestCharacterCombatTraceSystem.AttackTraceSweepMaxTipDistancePerSubstep);
            int substeps = Mathf.CeilToInt(Vector3.Distance(frame.TipPrev, frame.TipNow) / maxDistance);
            return Mathf.Clamp(
                Mathf.Max(1, substeps),
                1,
                CharacterTestCharacterCombatTraceSystem.AttackTraceSweepMaxSubsteps);
        }

        private static void DrawAttackTraceCapsuleLine(Vector3 from, Vector3 to, float radius)
        {
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(from, radius);
            Gizmos.DrawWireSphere(to, radius);
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }
    }
}
