using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestLocomotionTransitionPresentation
    {
        private const float MovingEpsilon = 0.0001f;
        private const float TransitionFadeSeconds = 0.15f;
        private const int TurnClipMinimumMilliDegrees = 45000;
        private const int TurnClip180ThresholdMilliDegrees = 135000;

        private static readonly int[] MovementBindingIds = { CharacterTestAuthoredContentIds.D400_Id173, CharacterTestAuthoredContentIds.D400_Id177, CharacterTestAuthoredContentIds.D400_Id181, CharacterTestAuthoredContentIds.D400_Id185, CharacterTestAuthoredContentIds.D400_Id189, CharacterTestAuthoredContentIds.D400_Id193, CharacterTestAuthoredContentIds.D400_Id197, CharacterTestAuthoredContentIds.D400_Id201, CharacterTestAuthoredContentIds.D400_Id175, CharacterTestAuthoredContentIds.D400_Id179, CharacterTestAuthoredContentIds.D400_Id183, CharacterTestAuthoredContentIds.D400_Id187, CharacterTestAuthoredContentIds.D400_Id191, CharacterTestAuthoredContentIds.D400_Id195, CharacterTestAuthoredContentIds.D400_Id199, CharacterTestAuthoredContentIds.D400_Id203, CharacterTestAuthoredContentIds.D400_Id174, CharacterTestAuthoredContentIds.D400_Id178, CharacterTestAuthoredContentIds.D400_Id182, CharacterTestAuthoredContentIds.D400_Id186, CharacterTestAuthoredContentIds.D400_Id190, CharacterTestAuthoredContentIds.D400_Id194, CharacterTestAuthoredContentIds.D400_Id198, CharacterTestAuthoredContentIds.D400_Id202, CharacterTestAuthoredContentIds.D400_Id176, CharacterTestAuthoredContentIds.D400_Id180, CharacterTestAuthoredContentIds.D400_Id184, CharacterTestAuthoredContentIds.D400_Id188, CharacterTestAuthoredContentIds.D400_Id192, CharacterTestAuthoredContentIds.D400_Id196, CharacterTestAuthoredContentIds.D400_Id200, CharacterTestAuthoredContentIds.D400_Id204 };
        private static readonly int[] TurnBindingIds = { CharacterTestAuthoredContentIds.D400_Id205, CharacterTestAuthoredContentIds.D400_Id207, CharacterTestAuthoredContentIds.D400_Id206, CharacterTestAuthoredContentIds.D400_Id208 };

        private readonly CharacterRuntimeLocomotionBlendController _locomotion;
        private bool _hasMotionState;
        private bool _wasMoving;
        private int _lastDirection;
        private int _lastSpeed = 1;
        private readonly int[] _movementBindings = new int[32];
        private readonly int[] _turnBindings = new int[4];
        private readonly Dictionary<long, int> _pointTokens = new Dictionary<long, int>();
        private string _activeActionKey;

        internal bool IsPlaying => !string.IsNullOrEmpty(_activeActionKey)
            && _locomotion != null && _locomotion.IsLocomotionBlendSuppressed
            && string.Equals(_locomotion.LastActionKey, _activeActionKey, StringComparison.Ordinal);

        public CharacterTestLocomotionTransitionPresentation(
            CharacterRuntimeLocomotionBlendController locomotion)
        {
            _locomotion = locomotion;
            MxAnimationSetDefinition set = locomotion?.AnimationSet;
            // Binding identities come from the persisted authoring directory, never display-name lookup.
            for (int i = 0; i < MovementBindingIds.Length; i++)
                if (set != null && set.TryFindBinding(MovementBindingIds[i], out _)) _movementBindings[i] = MovementBindingIds[i];
            for (int i = 0; i < TurnBindingIds.Length; i++)
                if (set != null && set.TryFindBinding(TurnBindingIds[i], out _)) _turnBindings[i] = TurnBindingIds[i];
            var points = locomotion?.LocomotionBlend2DDefinition?.Points;
            for (int i = 0; points != null && i < points.Count; i++)
            {
                var point = points[i];
                if (TryParseDominantLoopResourceId(point.ClipKey.Id, out string speed, out string direction))
                    _pointTokens[PointKey(point.X, point.Y)] = (speed == "walk" ? 1 : 2) * 8 + Array.IndexOf(DirectionTokens, direction);
            }
        }

        public bool TryApply(
            CharacterTestCharacterAnimationComponent animation,
            int turnInPlaceYawMilliDegrees,
            RuntimeFrame frame,
            int entityId,
            CharacterTestTraceRecorder traceRecorder)
        {
            bool moving = IsMoving(animation);
            if (!_hasMotionState)
            {
                SynchronizeWithoutTransition(animation);
                return false;
            }

            int bindingId = 0;
            if (!moving && Math.Abs(turnInPlaceYawMilliDegrees) >= TurnClipMinimumMilliDegrees)
            {
                bindingId = _turnBindings[(turnInPlaceYawMilliDegrees > 0 ? 2 : 0) + (Math.Abs(turnInPlaceYawMilliDegrees) >= TurnClip180ThresholdMilliDegrees ? 1 : 0)];
            }
            else if (!_wasMoving && moving
                && TryResolveMovementTokens(animation, out int startSpeed, out int startDirection))
            {
                bindingId = _movementBindings[(startSpeed - 1) * 8 + startDirection];
            }
            else if (_wasMoving && !moving)
            {
                bindingId = _movementBindings[16 + (_lastSpeed - 1) * 8 + _lastDirection];
            }

            if (moving && TryResolveMovementTokens(animation, out int currentSpeed, out int currentDirection))
            {
                // Turning changes the local movement direction while an old start clip still owns the pose.
                // Resume the current blend instead of completing a phase authored for the previous direction.
                if (_wasMoving && IsPlaying && currentDirection != _lastDirection)
                {
                    _locomotion.ResumeLocomotionBlend();
                    _activeActionKey = null;
                }
                _lastSpeed = currentSpeed;
                _lastDirection = currentDirection;
            }
            _wasMoving = moving;
            _hasMotionState = true;

            if (bindingId <= 0
                || _locomotion == null
                || !_locomotion.TryGetActionPlaybackDuration(bindingId, out float durationSeconds)
                || durationSeconds <= 0f)
            {
                return false;
            }

            bool accepted = _locomotion.TryCrossFadeAction(
                bindingId,
                TransitionFadeSeconds,
                targetStartOffsetSeconds: 0f,
                playbackSpeed: 1f,
                correlationId: "character-test.locomotion-transition:"
                    + entityId + ":" + bindingId + ":" + frame.Value);
            if (!accepted)
                return false;

            // A new phase replaces the previous clip, so its hold must replace the previous deadline too.
            _locomotion.ReplaceLocomotionBlendSuppressionForSeconds(durationSeconds);
            _activeActionKey = _locomotion.LastActionKey;

            traceRecorder?.Locomotion(
                frame,
                entityId,
                "transition key=" + bindingId
                + " duration=" + durationSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " rootMotion=ignored");
            return true;
        }

        public void SynchronizeWithoutTransition(CharacterTestCharacterAnimationComponent animation)
        {
            bool moving = IsMoving(animation);
            if (moving && TryResolveMovementTokens(animation, out int speed, out int direction))
            {
                _lastSpeed = speed;
                _lastDirection = direction;
            }
            _wasMoving = moving;
            _hasMotionState = true;
        }

        public void Reset()
        {
            _hasMotionState = false;
            _wasMoving = false;
            _lastDirection = 0;
            _lastSpeed = 1;
            _activeActionKey = null;
        }

        internal static bool TryResolveTurnActionId(int yawMilliDegrees, out string actionId)
        {
            actionId = string.Empty;
            int absolute = Math.Abs(yawMilliDegrees);
            if (absolute < TurnClipMinimumMilliDegrees)
                return false;

            string side = yawMilliDegrees > 0 ? "right" : "left";
            int angle = absolute >= TurnClip180ThresholdMilliDegrees ? 180 : 90;
            actionId = "locomotion.combat.turn." + side + "." + angle;
            return true;
        }

        internal static string CreateMovementActionId(string phase, string speed, string direction)
        {
            if ((phase != "start" && phase != "stop")
                || (speed != "walk" && speed != "run")
                || !IsDirection(direction))
            {
                return string.Empty;
            }

            return "locomotion.combat." + phase + "." + speed + "." + direction;
        }

        internal static bool TryParseDominantLoopResourceId(
            string resourceId,
            out string speed,
            out string direction)
        {
            speed = string.Empty;
            direction = string.Empty;
            if (string.IsNullOrWhiteSpace(resourceId))
                return false;

            const string walkMarker = "combat_walk_loop_";
            const string runMarker = "combat_run_loop_";
            int markerIndex = resourceId.LastIndexOf(walkMarker, StringComparison.Ordinal);
            string marker;
            if (markerIndex >= 0)
            {
                speed = "walk";
                marker = walkMarker;
            }
            else
            {
                markerIndex = resourceId.LastIndexOf(runMarker, StringComparison.Ordinal);
                if (markerIndex < 0)
                    return false;
                speed = "run";
                marker = runMarker;
            }

            int directionStart = markerIndex + marker.Length;
            for (int i = 0; i < DirectionTokens.Length; i++)
            {
                string token = DirectionTokens[i];
                if (resourceId.Length - directionStart == token.Length
                    && string.CompareOrdinal(resourceId, directionStart, token, 0, token.Length) == 0)
                { direction = token; return true; }
            }
            if (!IsDirection(direction))
            {
                speed = string.Empty;
                direction = string.Empty;
                return false;
            }
            return true;
        }

        private static long PointKey(int x, int y) => ((long)x << 32) | (uint)y;

        private bool TryResolveMovementTokens(CharacterTestCharacterAnimationComponent animation, out int speed, out int direction)
        {
            speed = 0;
            direction = 0;
            if (_locomotion == null || !_locomotion.TryResolveDominantLocomotionPoint(animation.LocomotionBlend, out _, out int x, out int y)
                || !_pointTokens.TryGetValue(PointKey(x, y), out int tokens)) return false;
            speed = tokens / 8;
            direction = tokens % 8;
            return true;
        }

        private static bool IsMoving(CharacterTestCharacterAnimationComponent animation)
        {
            return animation.LocomotionSpeed01 > MovingEpsilon
                && animation.LocomotionBlend.sqrMagnitude > MovingEpsilon;
        }

        private static readonly string[] DirectionTokens = { "f", "fl", "l", "bl", "b", "br", "r", "fr" };

        private static bool IsDirection(string direction)
        {
            switch (direction)
            {
                case "f":
                case "fl":
                case "l":
                case "bl":
                case "b":
                case "br":
                case "r":
                case "fr":
                    return true;
                default:
                    return false;
            }
        }
    }
}
