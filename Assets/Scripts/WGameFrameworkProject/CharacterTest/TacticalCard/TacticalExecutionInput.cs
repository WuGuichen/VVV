using System;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalExecutionInputKind { None = 0, Character = 1, WorldPoint = 2 }
    internal enum TacticalExecutionInputSource { Manual = 1, Automatic = 2 }
    internal enum TacticalPointRangeCenter { Selection = 1, Execution = 2, Both = 3 }
    internal enum TacticalInputStatus
    {
        Valid = 0, MissingInput = 1, WrongInputKind = 2, InvalidIdentity = 3, StaleBinding = 4,
        CharacterUnavailable = 5, OutOfRange = 6, GroundUnavailable = 7, InvalidGround = 8,
        PathUnavailable = 9, Unreachable = 10, UnsupportedProvider = 11, PointUnavailable = 12
    }

    /// <summary>One immutable execution parameter in deterministic world millimeters, never a card definition.</summary>
    internal readonly struct TacticalWorldPoint : IEquatable<TacticalWorldPoint>
    {
        public readonly int X, Y, Z;
        public TacticalWorldPoint(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(TacticalWorldPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is TacticalWorldPoint other && Equals(other);
        public override int GetHashCode() { unchecked { return ((X * 397) ^ Y) * 397 ^ Z; } }

        public static bool TryFromMeters(double x, double y, double z, out TacticalWorldPoint point)
        {
            point = default;
            if (!TryMillimeters(x, out int px) || !TryMillimeters(y, out int py) || !TryMillimeters(z, out int pz)) return false;
            point = new TacticalWorldPoint(px, py, pz);
            return true;
        }
        private static bool TryMillimeters(double meters, out int value)
        {
            value = 0;
            if (double.IsNaN(meters) || double.IsInfinity(meters)) return false;
            double millimeters = Math.Round(meters * 1000d, MidpointRounding.AwayFromZero);
            if (millimeters < int.MinValue || millimeters > int.MaxValue) return false;
            value = (int)millimeters;
            return true;
        }
    }

    internal readonly struct TacticalExecutionInputBinding : IEquatable<TacticalExecutionInputBinding>
    {
        public readonly ulong StageRun, Turn, CardInstance;
        public readonly uint CardVersion;
        public readonly GameplayEntityId Actor;
        public TacticalExecutionInputBinding(ulong stageRun, ulong turn, ulong cardInstance, uint cardVersion, GameplayEntityId actor)
        { StageRun = stageRun; Turn = turn; CardInstance = cardInstance; CardVersion = cardVersion; Actor = actor; }
        public bool IsValid => StageRun != 0 && CardInstance != 0 && CardVersion != 0 && Actor.IsValid;
        public bool Equals(TacticalExecutionInputBinding other) => StageRun == other.StageRun && Turn == other.Turn
            && CardInstance == other.CardInstance && CardVersion == other.CardVersion && Actor == other.Actor;
        public override bool Equals(object obj) => obj is TacticalExecutionInputBinding other && Equals(other);
        public override int GetHashCode() { unchecked { return (((StageRun.GetHashCode() * 397 ^ Turn.GetHashCode()) * 397
            ^ CardInstance.GetHashCode()) * 397 ^ (int)CardVersion) * 397 ^ Actor.GetHashCode(); } }
    }

    internal readonly struct TacticalExecutionInput
    {
        public readonly TacticalExecutionInputKind Kind;
        public readonly TacticalExecutionInputSource Source;
        public readonly TacticalExecutionInputBinding Binding;
        public readonly GameplayEntityId TargetCharacter;
        public readonly TacticalWorldPoint Point, SelectionOrigin;
        public TacticalExecutionInput(TacticalExecutionInputBinding binding, TacticalExecutionInputSource source,
            TacticalWorldPoint point, TacticalWorldPoint selectionOrigin)
        { Binding = binding; Source = source; Kind = TacticalExecutionInputKind.WorldPoint; Point = point;
            SelectionOrigin = selectionOrigin; TargetCharacter = default; }
        public TacticalExecutionInput(TacticalExecutionInputBinding binding, TacticalExecutionInputSource source, GameplayEntityId targetCharacter)
        { Binding = binding; Source = source; Kind = TacticalExecutionInputKind.Character; TargetCharacter = targetCharacter;
            Point = SelectionOrigin = default; }
    }

    /// <summary>Explicit designer/composition rules. There are no inferred product defaults or player overrides.</summary>
    internal readonly struct TacticalPointInputRules
    {
        public readonly int RadiusMillimeters;
        public readonly TacticalPointRangeCenter RangeCenter;
        public readonly bool RequireReachable;
        public TacticalPointInputRules(int radiusMillimeters, TacticalPointRangeCenter rangeCenter, bool requireReachable)
        {
            if (radiusMillimeters <= 0) throw new ArgumentOutOfRangeException(nameof(radiusMillimeters));
            if (rangeCenter < TacticalPointRangeCenter.Selection || rangeCenter > TacticalPointRangeCenter.Both)
                throw new ArgumentOutOfRangeException(nameof(rangeCenter));
            RadiusMillimeters = radiusMillimeters; RangeCenter = rangeCenter; RequireReachable = requireReachable;
        }
        public bool IsConfigured => RadiusMillimeters > 0 && RangeCenter >= TacticalPointRangeCenter.Selection
            && RangeCenter <= TacticalPointRangeCenter.Both;
    }

    /// <summary>Read-only project geometry/path adapter; it does not own movement, resources or the world clock.</summary>
    internal interface ITacticalPointValidationPort
    {
        TacticalInputStatus ValidateGround(GameplayEntityId actor, TacticalWorldPoint point);
        TacticalInputStatus ValidatePath(GameplayEntityId actor, TacticalWorldPoint origin, TacticalWorldPoint point);
    }

    internal static class TacticalExecutionInputValidation
    {
        /// <summary>NoAlloc. Both input sources pass through the same admission and delayed-start checks.</summary>
        public static TacticalInputStatus ValidatePoint(in TacticalExecutionInput input,
            in TacticalExecutionInputBinding expected, in TacticalPointInputRules rules,
            TacticalWorldPoint authoritativeSelectionOrigin, TacticalWorldPoint currentOrigin,
            bool actorAvailable, ITacticalPointValidationPort geometry)
        {
            if (input.Kind == TacticalExecutionInputKind.None) return TacticalInputStatus.MissingInput;
            if (input.Kind != TacticalExecutionInputKind.WorldPoint) return TacticalInputStatus.WrongInputKind;
            if (!expected.IsValid || !input.Binding.IsValid || !rules.IsConfigured
                || (input.Source != TacticalExecutionInputSource.Manual && input.Source != TacticalExecutionInputSource.Automatic))
                return TacticalInputStatus.InvalidIdentity;
            if (!input.Binding.Equals(expected)) return TacticalInputStatus.StaleBinding;
            if (!actorAvailable) return TacticalInputStatus.CharacterUnavailable;
            // Selection origin comes from the owner capture, never from an untrusted UI/provider parameter.
            if (!input.SelectionOrigin.Equals(authoritativeSelectionOrigin)) return TacticalInputStatus.StaleBinding;
            if (rules.RangeCenter != TacticalPointRangeCenter.Execution && !WithinRange(input.SelectionOrigin, input.Point, rules.RadiusMillimeters)
                || rules.RangeCenter != TacticalPointRangeCenter.Selection && !WithinRange(currentOrigin, input.Point, rules.RadiusMillimeters))
                return TacticalInputStatus.OutOfRange;
            if (geometry == null) return TacticalInputStatus.GroundUnavailable;
            var ground = geometry.ValidateGround(expected.Actor, input.Point);
            if (ground != TacticalInputStatus.Valid) return ground == TacticalInputStatus.InvalidGround
                ? ground : TacticalInputStatus.GroundUnavailable;
            if (!rules.RequireReachable) return TacticalInputStatus.Valid;
            var path = geometry.ValidatePath(expected.Actor, currentOrigin, input.Point);
            return path == TacticalInputStatus.Valid || path == TacticalInputStatus.Unreachable
                ? path : TacticalInputStatus.PathUnavailable;
        }

        private static bool WithinRange(TacticalWorldPoint origin, TacticalWorldPoint point, int radius)
        {
            long dx = (long)point.X - origin.X, dz = (long)point.Z - origin.Z;
            if (dx < -radius || dx > radius || dz < -radius || dz > radius) return false;
            return dx * dx + dz * dz <= (long)radius * radius;
        }
    }
}
