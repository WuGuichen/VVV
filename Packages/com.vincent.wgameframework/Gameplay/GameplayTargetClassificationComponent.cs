using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores the base target identity and cross-feature capabilities of one gameplay entity.
    /// </summary>
    /// <remarks>
    /// Allocation: NoAlloc. This component does not replace instance identity, definition identity,
    /// team relation, tags, statuses, or feature-specific target categories.
    /// </remarks>
    public readonly struct GameplayTargetClassificationComponent
        : IGameplayComponent,
          IEquatable<GameplayTargetClassificationComponent>
    {
        public GameplayTargetClassificationComponent(
            GameplayTargetIdentityTypes identityType,
            GameplayTargetCapabilities capabilities = GameplayTargetCapabilities.None)
        {
            GameplayTargetClassificationValidation.ValidateCandidateIdentityType(
                identityType,
                nameof(identityType));
            GameplayTargetClassificationValidation.ValidateCapabilities(
                capabilities,
                nameof(capabilities));

            IdentityType = identityType;
            Capabilities = capabilities;
        }

        public GameplayTargetIdentityTypes IdentityType { get; }

        public GameplayTargetCapabilities Capabilities { get; }

        public bool IsSpecified =>
            IdentityType != GameplayTargetIdentityTypes.None
            || Capabilities != GameplayTargetCapabilities.None;

        public bool MatchesAnyIdentity(GameplayTargetIdentityTypes acceptedIdentityTypes)
        {
            return acceptedIdentityTypes == GameplayTargetIdentityTypes.None
                || (IdentityType & acceptedIdentityTypes) != 0;
        }

        public bool HasAllCapabilities(GameplayTargetCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }

        public bool Equals(GameplayTargetClassificationComponent other)
        {
            return IdentityType == other.IdentityType
                && Capabilities == other.Capabilities;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayTargetClassificationComponent other
                && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)IdentityType * 397) ^ (int)Capabilities;
            }
        }
    }

    internal static class GameplayTargetClassificationValidation
    {
        internal const GameplayTargetIdentityTypes AllIdentityTypes =
            GameplayTargetIdentityTypes.Character
            | GameplayTargetIdentityTypes.WorldObject
            | GameplayTargetIdentityTypes.SpatialPoint
            | GameplayTargetIdentityTypes.SpatialRegion;

        internal const GameplayTargetCapabilities AllCapabilities =
            GameplayTargetCapabilities.Damageable
            | GameplayTargetCapabilities.Movable
            | GameplayTargetCapabilities.Static;

        internal static void ValidateCandidateIdentityType(
            GameplayTargetIdentityTypes identityType,
            string parameterName)
        {
            ValidateIdentityTypes(identityType, parameterName);
            int value = (int)identityType;
            if (value != 0 && (value & (value - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    identityType,
                    "A target classification must use zero or one base identity type.");
            }
        }

        internal static void ValidateIdentityTypes(
            GameplayTargetIdentityTypes identityTypes,
            string parameterName)
        {
            if ((identityTypes & ~AllIdentityTypes) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    identityTypes,
                    "Target identity types contain undefined flags.");
            }
        }

        internal static void ValidateCapabilities(
            GameplayTargetCapabilities capabilities,
            string parameterName)
        {
            if ((capabilities & ~AllCapabilities) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    capabilities,
                    "Target capabilities contain undefined flags.");
            }
        }
    }
}
