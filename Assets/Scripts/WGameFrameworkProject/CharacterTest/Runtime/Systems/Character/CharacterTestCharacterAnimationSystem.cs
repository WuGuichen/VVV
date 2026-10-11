using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCharacterAnimationSystem
    {
        private const float ActiveLocomotionEpsilon = 0.0001f;

        private CharacterTestCharacterMovementProfileRuntime _movementProfile =
            CharacterTestCharacterMovementProfileSource.Load(null, null);
        private CharacterTestTraceRecorder _traceRecorder;

        public void SetMovementProfile(CharacterTestCharacterMovementProfileRuntime profile)
        {
            _movementProfile = profile.Profile != null
                ? profile
                : CharacterTestCharacterMovementProfileSource.Load(null, null);
        }

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame = default)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            CharacterTestComponentWorld components = world.CharacterComponents;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                CharacterTestCharacterAnimationRequestComponent request =
                    components.GetAnimationRequest(entity.Id);
                CharacterActionId activeActionId = entity.Action.ActiveActionId;
                bool hasCurrentAnimationRequest = request.CanEmit(frame.Value, activeActionId);
                Vector2 locomotionBlend = ResolveLocalLocomotionBlend(
                    ResolveAnimationWorldMove(entity),
                    entity.Transform.YawDegrees);
                float locomotionSpeed01 = Mathf.Clamp01(locomotionBlend.magnitude);
                bool airborne = !entity.IsGrounded;
                bool actionActive = IsRunningAction(entity)
                    || CharacterTestCharacterActionSystem.IsDeathTerminalActionHold(entity);

                entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                    locomotionBlend,
                    locomotionSpeed01,
                    entity.IsGrounded,
                    airborne,
                    actionActive,
                    hasCurrentAnimationRequest ? request.AnimationKey : string.Empty,
                    hasCurrentAnimationRequest ? request.TransitionSeconds : 0f,
                    hasCurrentAnimationRequest ? request.HoldSeconds : 0f,
                    hasCurrentAnimationRequest ? request.PlaybackSpeed : 1f,
                    hasCurrentAnimationRequest ? request.BindingId : entity.Animation.BindingId,
                    hasCurrentAnimationRequest ? request.ClipResourceKey : entity.Animation.ClipResourceKey,
                    hasCurrentAnimationRequest ? request.AuthorityBindingId : entity.Animation.AuthorityBindingId,
                    hasCurrentAnimationRequest ? request.BindingId : 0));

                if (hasCurrentAnimationRequest)
                {
                    components.MarkAnimationRequestEmitted(entity.Id, frame.Value);

                    _traceRecorder?.AttackAnimation(
                        frame,
                        entity.Id.Value,
                        "sent key=" + request.AnimationKey
                        + " action=" + EmptyAsNone(request.ActionId)
                        + " requestFrame=" + request.Frame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " localFrame=" + request.LocalFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " event=" + EmptyAsNone(request.StableEventId)
                        + " transition=" + request.TransitionSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        + " hold=" + request.HoldSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        + " speed=" + request.PlaybackSpeed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                        warning: false);
                }
                else if (ShouldRecordMissingAnimationRequest(entity, request, frame.Value))
                {
                    _traceRecorder?.AttackAnimation(
                        frame,
                        entity.Id.Value,
                        "missing animation request action=" + EmptyAsNone(activeActionId)
                        + " localFrame=" + entity.Action.LocalFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " lastKey=" + EmptyAsNone(entity.Action.LastAnimationActionKey)
                        + " actionFrameKey=" + EmptyAsNone(entity.Action.AnimationActionKeyThisFrame)
                        + " request=" + DescribeRequest(request),
                        warning: true);
                }
            }
        }

        private static bool ShouldRecordMissingAnimationRequest(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterAnimationRequestComponent request,
            long frame)
        {
            if (!IsRunningAction(entity))
                return false;
            if (entity.Action.LocalFrame > 1)
                return false;

            bool hasExpectedAnimationKey =
                !string.IsNullOrWhiteSpace(entity.Action.AnimationActionKeyThisFrame);
            if (!hasExpectedAnimationKey)
                return false;

            if (!request.IsValid)
                return true;
            if (!request.IsPending)
                return true;
            if (request.ActionId != entity.Action.ActiveActionId)
            {
                return true;
            }

            return frame < request.CreatedFrame || frame > request.ExpiresFrame;
        }

        private static string DescribeRequest(CharacterTestCharacterAnimationRequestComponent request)
        {
            if (!request.IsValid)
                return "none";

            return StatusName(request.Status)
                + "/action=" + EmptyAsNone(request.ActionId)
                + "/frame=" + request.Frame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/expires=" + request.ExpiresFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/key=" + EmptyAsNone(request.AnimationKey);
        }

        private static string StatusName(CharacterTestCharacterAnimationRequestStatus status)
        {
            switch (status)
            {
                case CharacterTestCharacterAnimationRequestStatus.Pending:
                    return "Pending";
                case CharacterTestCharacterAnimationRequestStatus.Emitted:
                    return "Emitted";
                case CharacterTestCharacterAnimationRequestStatus.Expired:
                    return "Expired";
                default:
                    return "None";
            }
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static string EmptyAsNone(int value) => value == 0 ? "none" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        private Vector2 ResolveAnimationWorldMove(CharacterTestCharacterEntity entity)
        {
            Vector2 desiredVelocity = entity.DesiredVelocity;
            if (desiredVelocity.sqrMagnitude <= ActiveLocomotionEpsilon)
                return Vector2.zero;

            CharacterMovementProfileConfig profile =
                _movementProfile.Profile ?? CharacterTestCharacterMovementProfileSource.DefaultProfile;
            float runSpeed = Mathf.Max(0.001f, profile.RunSpeed);
            return Vector2.ClampMagnitude(desiredVelocity / runSpeed, 1f);
        }

        private static bool IsRunningAction(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.Action.ActiveActionId.IsValid
                && (entity.Action.State == CharacterTestActionStateId.Running);
        }

        private static Vector2 ResolveLocalLocomotionBlend(Vector2 worldMove, float yawDegrees)
        {
            Vector2 clamped = Vector2.ClampMagnitude(worldMove, 1f);
            if (clamped.sqrMagnitude <= ActiveLocomotionEpsilon)
                return Vector2.zero;

            float yawRadians = yawDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(yawRadians), Mathf.Cos(yawRadians));
            Vector2 right = new Vector2(forward.y, -forward.x);
            return Vector2.ClampMagnitude(
                new Vector2(
                    Vector2.Dot(clamped, right),
                    Vector2.Dot(clamped, forward)),
                1f);
        }
    }
}
