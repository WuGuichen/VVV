using System;
using System.Collections.Generic;
using MxFramework.Core.Math;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterMotionModifierContext
    {
        public CharacterMotionModifierContext(
            CharacterCommand command,
            CharacterControlState controlState,
            CharacterControlLockMask lockMask)
        {
            Command = command;
            ControlState = controlState;
            LockMask = lockMask;
        }

        public CharacterCommand Command { get; }

        public CharacterControlState ControlState { get; }

        public CharacterControlLockMask LockMask { get; }
    }

    public readonly struct CharacterMotionModifier : IEquatable<CharacterMotionModifier>
    {
        public CharacterMotionModifier(
            string source,
            Fix64 moveSpeedScale,
            string reason = "",
            int priority = 0)
        {
            if (moveSpeedScale < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedScale), "Motion modifier scale cannot be negative.");

            Source = source ?? string.Empty;
            MoveSpeedScale = moveSpeedScale;
            Reason = reason ?? string.Empty;
            Priority = priority;
        }

        public string Source { get; }

        public Fix64 MoveSpeedScale { get; }

        public string Reason { get; }

        public int Priority { get; }

        public bool Equals(CharacterMotionModifier other)
        {
            return string.Equals(Source, other.Source, StringComparison.Ordinal)
                && MoveSpeedScale.Equals(other.MoveSpeedScale)
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterMotionModifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Source ?? string.Empty);
                hash = (hash * 397) ^ MoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Reason ?? string.Empty);
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }
    }

    public readonly struct CharacterMotionModifierResult
    {
        public CharacterMotionModifierResult(Fix64 finalMoveSpeedScale, CharacterMotionModifier[] modifiers)
        {
            if (finalMoveSpeedScale < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(finalMoveSpeedScale), "Final motion modifier scale cannot be negative.");

            FinalMoveSpeedScale = finalMoveSpeedScale;
            Modifiers = modifiers ?? Array.Empty<CharacterMotionModifier>();
        }

        public Fix64 FinalMoveSpeedScale { get; }

        public IReadOnlyList<CharacterMotionModifier> Modifiers { get; }

        public int Count => Modifiers.Count;

        public static CharacterMotionModifierResult Identity { get; } =
            new CharacterMotionModifierResult(Fix64.One, Array.Empty<CharacterMotionModifier>());
    }

    public interface ICharacterMotionModifierProvider
    {
        void CollectModifiers(CharacterMotionModifierContext context, IList<CharacterMotionModifier> destination);
    }

    public sealed class CharacterMotionModifierAggregator
    {
        private static readonly ModifierComparer Comparer = new ModifierComparer();
        private readonly ICharacterMotionModifierProvider[] _providers;
        private readonly List<CharacterMotionModifier> _buffer = new List<CharacterMotionModifier>(8);

        public CharacterMotionModifierAggregator(ICharacterMotionModifierProvider[] providers)
        {
            _providers = providers ?? Array.Empty<ICharacterMotionModifierProvider>();
        }

        public CharacterMotionModifierResult Evaluate(CharacterMotionModifierContext context)
        {
            if (_providers.Length == 0)
            {
                return CharacterMotionModifierResult.Identity;
            }

            _buffer.Clear();
            for (int i = 0; i < _providers.Length; i++)
            {
                _providers[i]?.CollectModifiers(context, _buffer);
            }

            if (_buffer.Count == 0)
            {
                return CharacterMotionModifierResult.Identity;
            }

            _buffer.Sort(Comparer);
            Fix64 finalScale = Fix64.One;
            var modifiers = new CharacterMotionModifier[_buffer.Count];
            for (int i = 0; i < _buffer.Count; i++)
            {
                CharacterMotionModifier modifier = _buffer[i];
                finalScale *= modifier.MoveSpeedScale;
                modifiers[i] = modifier;
            }

            return new CharacterMotionModifierResult(finalScale, modifiers);
        }

        private sealed class ModifierComparer : IComparer<CharacterMotionModifier>
        {
            public int Compare(CharacterMotionModifier x, CharacterMotionModifier y)
            {
                int priority = x.Priority.CompareTo(y.Priority);
                if (priority != 0)
                {
                    return priority;
                }

                int source = string.CompareOrdinal(x.Source, y.Source);
                if (source != 0)
                {
                    return source;
                }

                return string.CompareOrdinal(x.Reason, y.Reason);
            }
        }
    }
}
