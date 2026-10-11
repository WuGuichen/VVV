using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterAnimationComponent
    {
        public CharacterTestCharacterAnimationComponent(
            Vector2 locomotionBlend,
            float locomotionSpeed01,
            bool grounded,
            bool airborne,
            bool actionActive = false,
            string actionKey = "",
            float actionTransitionSeconds = 0f,
            float actionHoldSeconds = 0f,
            float actionPlaybackSpeed = 1f,
            int bindingId = 0,
            string clipResourceKey = "",
            int authorityBindingId = 0,
            int requestBindingId = 0)
        {
            LocomotionBlend = Vector2.ClampMagnitude(locomotionBlend, 1f);
            LocomotionSpeed01 = Mathf.Clamp01(locomotionSpeed01);
            Grounded = grounded;
            Airborne = airborne;
            ActionActive = actionActive;
            ActionKey = actionKey ?? string.Empty;
            BindingId = bindingId;
            ClipResourceKey = clipResourceKey ?? string.Empty;
            AuthorityBindingId = authorityBindingId;
            RequestBindingId = requestBindingId;
            ActionTransitionSeconds = actionTransitionSeconds < 0f ? 0f : actionTransitionSeconds;
            ActionHoldSeconds = actionHoldSeconds < 0f ? 0f : actionHoldSeconds;
            ActionPlaybackSpeed = actionPlaybackSpeed <= 0.0001f ? 1f : actionPlaybackSpeed;
        }

        public Vector2 LocomotionBlend { get; }
        public float LocomotionSpeed01 { get; }
        public bool Grounded { get; }
        public bool Airborne { get; }
        public bool ActionActive { get; }
        public string ActionKey { get; }
        public int BindingId { get; }
        public int AuthorityBindingId { get; }
        public int RequestBindingId { get; }
        public string ClipResourceKey { get; }
        public float ActionTransitionSeconds { get; }
        public float ActionHoldSeconds { get; }
        public float ActionPlaybackSpeed { get; }

        public static CharacterTestCharacterAnimationComponent IdleGrounded =>
            new CharacterTestCharacterAnimationComponent(Vector2.zero, 0f, grounded: true, airborne: false);
    }
}
