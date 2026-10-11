using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterMovementBasis
    {
        public static readonly CharacterTestCharacterMovementBasis Identity =
            new CharacterTestCharacterMovementBasis(
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                false,
                Vector2.zero);

        public CharacterTestCharacterMovementBasis(
            Vector2 forward,
            Vector2 right,
            bool hasLockedTarget,
            Vector2 lockedTargetPosition)
        {
            Forward = NormalizeOrDefault(forward, new Vector2(0f, 1f));
            Right = NormalizeOrDefault(right, new Vector2(1f, 0f));
            HasLockedTarget = hasLockedTarget;
            LockedTargetPosition = lockedTargetPosition;
        }

        public Vector2 Forward { get; }
        public Vector2 Right { get; }
        public bool HasLockedTarget { get; }
        public Vector2 LockedTargetPosition { get; }

        public Vector2 ToWorldMove(Vector2 input)
        {
            Vector2 clamped = Vector2.ClampMagnitude(input, 1f);
            return Vector2.ClampMagnitude(Right * clamped.x + Forward * clamped.y, 1f);
        }

        private static Vector2 NormalizeOrDefault(Vector2 value, Vector2 fallback)
        {
            return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
        }
    }
}
