using System;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestMainMenuStatus
    {
        public bool IsPaused { get; set; }
        public bool IsMenuVisible { get; set; }
        public bool MenuPausesRuntime { get; set; } = true;
        public bool HasEnteredGameplay { get; set; }
        public bool IsNetworkSessionActive { get; set; }
        public CharacterTestNetworkSessionKind NetworkSessionKind { get; set; }
        public long RuntimeFrame { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsTacticalSlice { get; set; }
        public int TacticalPhase { get; set; }
        public bool TacticalBusy { get; set; }
        public bool TacticalSettlementSaved { get; set; }
        public bool HasTacticalCheckpoint { get; set; }
    }

    public readonly struct CharacterTestMainMenuCommandResult
    {
        private CharacterTestMainMenuCommandResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static CharacterTestMainMenuCommandResult Succeeded(string message)
        {
            return new CharacterTestMainMenuCommandResult(true, message);
        }

        public static CharacterTestMainMenuCommandResult Failed(string message)
        {
            return new CharacterTestMainMenuCommandResult(false, message);
        }
    }

    public interface ICharacterTestMainMenuController
    {
        CharacterTestMainMenuStatus CaptureMainMenuStatus();
        CharacterTestMainMenuCommandResult StartOrContinueFromMainMenu();
        CharacterTestMainMenuCommandResult RestartOfflineFromMainMenu();
        CharacterTestMainMenuCommandResult OpenNetworkPanelFromMainMenu();
        CharacterTestMainMenuCommandResult CloseMainMenu();
    }

    internal interface ICharacterTestMainMenuStatusWriter
    {
        void CaptureMainMenuStatusInto(CharacterTestMainMenuStatus target);
    }

    public sealed class CharacterTestFairyGuiMainMenuViewModel
    {
        public CharacterTestMainMenuStatus Status { get; set; } = new CharacterTestMainMenuStatus();
        public string Title { get; set; } = "Pause Menu";
        public string StatusText { get; set; } = "Paused";
        public string Message { get; set; } = "Resume or manage the current CharacterTest session.";
        public string StartLabel { get; set; } = "Resume";
        public bool CanStart { get; set; } = true;
        public bool CanNetwork { get; set; } = true;
        public bool CanAbilities { get; set; }
        public bool CanBehaviorDeck { get; set; }
        public bool CanSettings { get; set; }
        public bool CanRestart { get; set; } = true;
        public bool CanClose { get; set; } = true;
    }

    internal readonly struct CharacterTestFairyGuiMainMenuViewModelSignature :
        IEquatable<CharacterTestFairyGuiMainMenuViewModelSignature>
    {
        private readonly ulong _hashA;
        private readonly ulong _hashB;
        private readonly int _valueCount;

        private CharacterTestFairyGuiMainMenuViewModelSignature(ulong hashA, ulong hashB, int valueCount)
        {
            _hashA = hashA;
            _hashB = hashB;
            _valueCount = valueCount;
        }

        public static CharacterTestFairyGuiMainMenuViewModelSignature Create(
            CharacterTestFairyGuiMainMenuViewModel viewModel,
            CharacterTestUiTextProviderSignature textSignature)
        {
            if (viewModel == null)
                return default;

            return Create(
                viewModel.Status,
                viewModel.CanStart,
                viewModel.CanNetwork,
                viewModel.CanAbilities,
                viewModel.CanBehaviorDeck,
                viewModel.CanSettings,
                viewModel.CanRestart,
                viewModel.CanClose,
                textSignature);
        }

        public static CharacterTestFairyGuiMainMenuViewModelSignature Create(
            CharacterTestMainMenuStatus status,
            bool canStart,
            bool canNetwork,
            bool canAbilities,
            bool canBehaviorDeck,
            bool canSettings,
            bool canRestart,
            bool canClose,
            CharacterTestUiTextProviderSignature textSignature)
        {
            var builder = new SignatureBuilder();
            builder.Add(status != null && status.IsPaused);
            builder.Add(status != null && status.IsMenuVisible);
            builder.Add(status == null || status.MenuPausesRuntime);
            builder.Add(status != null && status.HasEnteredGameplay);
            builder.Add(status != null && status.IsNetworkSessionActive);
            builder.Add(status != null && status.IsTacticalSlice);
            builder.Add(status != null ? status.TacticalPhase : 0);
            builder.Add(status != null && status.TacticalBusy);
            builder.Add(status != null && status.TacticalSettlementSaved);
            builder.Add(status != null && status.HasTacticalCheckpoint);
            builder.Add(status != null ? (int)status.NetworkSessionKind : 0);
            builder.Add(status != null ? status.RuntimeFrame : 0L);
            builder.Add(canStart);
            builder.Add(canNetwork);
            builder.Add(canAbilities);
            builder.Add(canBehaviorDeck);
            builder.Add(canSettings);
            builder.Add(canRestart);
            builder.Add(canClose);
            builder.Add(textSignature.GetHashCode());
            AppendToken(ref builder, status != null ? status.Message : string.Empty);
            return builder.ToSignature();
        }

        public bool Equals(CharacterTestFairyGuiMainMenuViewModelSignature other)
        {
            return _hashA == other._hashA &&
                _hashB == other._hashB &&
                _valueCount == other._valueCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestFairyGuiMainMenuViewModelSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)_hashA;
                hash = (hash * 397) ^ (int)(_hashA >> 32);
                hash = (hash * 397) ^ (int)_hashB;
                hash = (hash * 397) ^ (int)(_hashB >> 32);
                hash = (hash * 397) ^ _valueCount;
                return hash;
            }
        }

        public static bool operator ==(
            CharacterTestFairyGuiMainMenuViewModelSignature left,
            CharacterTestFairyGuiMainMenuViewModelSignature right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CharacterTestFairyGuiMainMenuViewModelSignature left,
            CharacterTestFairyGuiMainMenuViewModelSignature right)
        {
            return !left.Equals(right);
        }

        private static void AppendToken(ref SignatureBuilder builder, string value)
        {
            builder.Add(value ?? string.Empty);
        }

        private struct SignatureBuilder
        {
            private const ulong OffsetA = 14695981039346656037UL;
            private const ulong PrimeA = 1099511628211UL;
            private const ulong OffsetB = 10996422393982105731UL;
            private const ulong PrimeB = 14029467366897019727UL;

            private ulong _hashA;
            private ulong _hashB;
            private int _valueCount;

            public void Add(bool value)
            {
                Add(value ? 1 : 0);
            }

            public void Add(int value)
            {
                unchecked
                {
                    Mix((uint)value);
                    _valueCount++;
                }
            }

            public void Add(long value)
            {
                unchecked
                {
                    Mix((uint)value);
                    Mix((uint)(value >> 32));
                    _valueCount++;
                }
            }

            public void Add(string value)
            {
                if (value == null)
                {
                    Add(-1);
                    return;
                }

                Add(value.Length);
                for (int i = 0; i < value.Length; i++)
                    Mix(value[i]);
                _valueCount++;
            }

            public CharacterTestFairyGuiMainMenuViewModelSignature ToSignature()
            {
                ulong hashA = _hashA == 0UL ? OffsetA : _hashA;
                ulong hashB = _hashB == 0UL ? OffsetB : _hashB;
                return new CharacterTestFairyGuiMainMenuViewModelSignature(hashA, hashB, _valueCount);
            }

            private void Mix(uint value)
            {
                unchecked
                {
                    if (_hashA == 0UL)
                        _hashA = OffsetA;
                    if (_hashB == 0UL)
                        _hashB = OffsetB;

                    _hashA ^= value;
                    _hashA *= PrimeA;
                    _hashB ^= ((ulong)value << 32) | value;
                    _hashB *= PrimeB;
                    _hashB = (_hashB << 31) | (_hashB >> 33);
                }
            }
        }
    }
}
