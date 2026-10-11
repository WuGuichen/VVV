using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiCombatHudAbilitySlotViewModel
    {
        public int SlotNumber { get; set; }
        public string KeyText { get; set; } = string.Empty;
        public string AbilityText { get; set; } = "Empty";
        public string CooldownText { get; set; } = string.Empty;
        public float CooldownRatio { get; set; }
        public bool IsBound { get; set; }
        public bool IsSelected { get; set; }
    }

    public sealed class CharacterTestFairyGuiCombatHudBuffViewModel
    {
        private bool _hasStaticPresentationCache;
        private CharacterTestUiTextProviderSignature _staticTextProviderSignature;
        private int _staticBuffId;
        private int _staticStackCount;
        private bool _staticIsPermanent;
        private GameplayBuffPresentationPolarity _staticPolarity;
        private string _staticDisplayName = string.Empty;
        private string _staticNameKey = string.Empty;
        private string _staticShortName = string.Empty;
        private string _staticDescription = string.Empty;
        private string _staticDescriptionMode = string.Empty;
        private string _staticEffectSummary = string.Empty;

        public int BuffId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public string StackText { get; set; } = string.Empty;
        public string TooltipText { get; set; } = string.Empty;
        public float Remaining01 { get; set; }
        public bool IsPermanent { get; set; }
        public GameplayBuffPresentationPolarity Polarity { get; set; } =
            GameplayBuffPresentationPolarity.Neutral;

        internal bool CanReuseStaticPresentation(
            in GameplayBuffPresentationItem item,
            CharacterTestUiTextProviderSignature textProviderSignature)
        {
            return _hasStaticPresentationCache
                && _staticTextProviderSignature == textProviderSignature
                && _staticBuffId == item.BuffId
                && _staticStackCount == item.StackCount
                && _staticIsPermanent == item.IsPermanent
                && _staticPolarity == item.Polarity
                && string.Equals(_staticDisplayName, item.DisplayName, StringComparison.Ordinal)
                && string.Equals(_staticNameKey, item.NameKey, StringComparison.Ordinal)
                && string.Equals(_staticShortName, item.ShortName, StringComparison.Ordinal)
                && string.Equals(_staticDescription, item.Description, StringComparison.Ordinal)
                && string.Equals(_staticDescriptionMode, item.DescriptionMode, StringComparison.Ordinal)
                && string.Equals(_staticEffectSummary, item.EffectSummary, StringComparison.Ordinal);
        }

        internal void StoreStaticPresentation(
            in GameplayBuffPresentationItem item,
            CharacterTestUiTextProviderSignature textProviderSignature)
        {
            _hasStaticPresentationCache = true;
            _staticTextProviderSignature = textProviderSignature;
            _staticBuffId = item.BuffId;
            _staticStackCount = item.StackCount;
            _staticIsPermanent = item.IsPermanent;
            _staticPolarity = item.Polarity;
            _staticDisplayName = item.DisplayName;
            _staticNameKey = item.NameKey;
            _staticShortName = item.ShortName;
            _staticDescription = item.Description;
            _staticDescriptionMode = item.DescriptionMode;
            _staticEffectSummary = item.EffectSummary;
        }
    }

    public sealed class CharacterTestFairyGuiCombatHudViewModel
    {
        private const string DefaultPlayerActionText = "Action: Ready";
        private const string DefaultNetworkLatencyTone = "good";
        private const int VitalsPairTextCacheLimit = 128;
        private static readonly string[,] VitalsPairTextCache = CreateVitalsPairTextCache();

        private CharacterTestFairyGuiCombatHudAbilitySlotViewModel[] _abilitySlots =
            Array.Empty<CharacterTestFairyGuiCombatHudAbilitySlotViewModel>();
        private readonly List<CharacterTestFairyGuiCombatHudBuffViewModel> _playerBuffs =
            new List<CharacterTestFairyGuiCombatHudBuffViewModel>(4);
        private readonly List<CharacterTestFairyGuiCombatHudBuffViewModel> _targetBuffs =
            new List<CharacterTestFairyGuiCombatHudBuffViewModel>(4);
        private int _playerHealth;
        private int _playerMaxHealth = 1;
        private int _playerScatterPressure;
        private int _playerMaxScatterPressure = 1;
        private int _targetHealth;
        private int _targetMaxHealth = 1;
        private int _targetScatterPressure;
        private int _targetMaxScatterPressure = 1;
        private string _playerHealthText = string.Empty;
        private string _playerScatterPressureText = string.Empty;
        private string _targetHealthText = string.Empty;
        private string _targetScatterPressureText = string.Empty;
        private bool _playerHealthTextDirty = true;
        private bool _playerScatterPressureTextDirty = true;
        private bool _targetHealthTextDirty = true;
        private bool _targetScatterPressureTextDirty = true;

        public bool HasLocalPlayer { get; set; }
        public int PlayerEntityId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int PlayerHealth
        {
            get => _playerHealth;
            set
            {
                if (_playerHealth == value)
                    return;

                _playerHealth = value;
                _playerHealthTextDirty = true;
            }
        }

        public int PlayerMaxHealth
        {
            get => _playerMaxHealth;
            set
            {
                if (_playerMaxHealth == value)
                    return;

                _playerMaxHealth = value;
                _playerHealthTextDirty = true;
            }
        }

        public float PlayerHealthRatio { get; set; }
        public int PlayerScatterPressure
        {
            get => _playerScatterPressure;
            set
            {
                if (_playerScatterPressure == value)
                    return;

                _playerScatterPressure = value;
                _playerScatterPressureTextDirty = true;
            }
        }

        public int PlayerMaxScatterPressure
        {
            get => _playerMaxScatterPressure;
            set
            {
                if (_playerMaxScatterPressure == value)
                    return;

                _playerMaxScatterPressure = value;
                _playerScatterPressureTextDirty = true;
            }
        }

        public float PlayerScatterPressureRatio { get; set; }
        public string PlayerScatterPressureBand { get; set; } = string.Empty;
        public int PlayerPosturePressure
        {
            get => PlayerScatterPressure;
            set => PlayerScatterPressure = value;
        }

        public int PlayerMaxPosturePressure
        {
            get => PlayerMaxScatterPressure;
            set => PlayerMaxScatterPressure = value;
        }

        public string PlayerActionText { get; set; } = DefaultPlayerActionText;

        public bool TargetVisible { get; set; }
        public bool TargetLocked { get; set; }
        public int TargetEntityId { get; set; }
        public string TargetName { get; set; } = string.Empty;
        public int TargetHealth
        {
            get => _targetHealth;
            set
            {
                if (_targetHealth == value)
                    return;

                _targetHealth = value;
                _targetHealthTextDirty = true;
            }
        }

        public int TargetMaxHealth
        {
            get => _targetMaxHealth;
            set
            {
                if (_targetMaxHealth == value)
                    return;

                _targetMaxHealth = value;
                _targetHealthTextDirty = true;
            }
        }

        public float TargetHealthRatio { get; set; }
        public int TargetScatterPressure
        {
            get => _targetScatterPressure;
            set
            {
                if (_targetScatterPressure == value)
                    return;

                _targetScatterPressure = value;
                _targetScatterPressureTextDirty = true;
            }
        }

        public int TargetMaxScatterPressure
        {
            get => _targetMaxScatterPressure;
            set
            {
                if (_targetMaxScatterPressure == value)
                    return;

                _targetMaxScatterPressure = value;
                _targetScatterPressureTextDirty = true;
            }
        }

        public float TargetScatterPressureRatio { get; set; }
        public string TargetScatterPressureBand { get; set; } = string.Empty;
        public int TargetPosturePressure
        {
            get => TargetScatterPressure;
            set => TargetScatterPressure = value;
        }

        public int TargetMaxPosturePressure
        {
            get => TargetMaxScatterPressure;
            set => TargetMaxScatterPressure = value;
        }

        public string TargetStatusText { get; set; } = string.Empty;
        public bool FeedbackVisible { get; set; }
        public string FeedbackText { get; set; } = string.Empty;
        public bool NetworkLatencyVisible { get; set; }
        public string NetworkLatencyText { get; set; } = string.Empty;
        public string NetworkLatencyTone { get; set; } = DefaultNetworkLatencyTone;
        public bool BehaviorCardVisible { get; set; }
        public string BehaviorCardIconText { get; set; } = string.Empty;
        public string BehaviorCardIconKey { get; set; } = string.Empty;
        public string BehaviorCardName { get; set; } = string.Empty;
        public string BehaviorCardReasonText { get; set; } = string.Empty;
        public string PlayerHealthText
        {
            get
            {
                if (_playerHealthTextDirty)
                    SetPairText(
                        ref _playerHealthText,
                        ref _playerHealthTextDirty,
                        _playerHealth,
                        _playerMaxHealth);

                return _playerHealthText;
            }
        }

        public string PlayerScatterPressureText
        {
            get
            {
                if (_playerScatterPressureTextDirty)
                    SetPairText(
                        ref _playerScatterPressureText,
                        ref _playerScatterPressureTextDirty,
                        _playerScatterPressure,
                        _playerMaxScatterPressure);

                return _playerScatterPressureText;
            }
        }

        public string TargetHealthText
        {
            get
            {
                if (_targetHealthTextDirty)
                    SetPairText(
                        ref _targetHealthText,
                        ref _targetHealthTextDirty,
                        _targetHealth,
                        _targetMaxHealth);

                return _targetHealthText;
            }
        }

        public string TargetScatterPressureText
        {
            get
            {
                if (_targetScatterPressureTextDirty)
                    SetPairText(
                        ref _targetScatterPressureText,
                        ref _targetScatterPressureTextDirty,
                        _targetScatterPressure,
                        _targetMaxScatterPressure);

                return _targetScatterPressureText;
            }
        }

        public IReadOnlyList<CharacterTestFairyGuiCombatHudAbilitySlotViewModel> AbilitySlots => _abilitySlots;
        public IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> PlayerBuffs => _playerBuffs;
        public IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> TargetBuffs => _targetBuffs;

        internal void ResetForBuild()
        {
            HasLocalPlayer = false;
            PlayerEntityId = 0;
            PlayerName = string.Empty;
            PlayerScatterPressureBand = string.Empty;
            PlayerActionText = DefaultPlayerActionText;
            TargetVisible = false;
            TargetLocked = false;
            TargetEntityId = 0;
            TargetName = string.Empty;
            TargetScatterPressureBand = string.Empty;
            TargetStatusText = string.Empty;
            FeedbackVisible = false;
            FeedbackText = string.Empty;
            NetworkLatencyVisible = false;
            NetworkLatencyText = string.Empty;
            NetworkLatencyTone = DefaultNetworkLatencyTone;
            BehaviorCardVisible = false;
            BehaviorCardIconText = string.Empty;
            BehaviorCardIconKey = string.Empty;
            BehaviorCardName = string.Empty;
            BehaviorCardReasonText = string.Empty;
            _playerBuffs.Clear();
            _targetBuffs.Clear();
        }

        internal void ClearPlayerVitals()
        {
            _playerHealth = 0;
            _playerMaxHealth = 1;
            PlayerHealthRatio = 0f;
            _playerScatterPressure = 0;
            _playerMaxScatterPressure = 1;
            PlayerScatterPressureRatio = 0f;
            _playerHealthText = string.Empty;
            _playerScatterPressureText = string.Empty;
            _playerHealthTextDirty = false;
            _playerScatterPressureTextDirty = false;
        }

        internal void ClearTargetVitals()
        {
            _targetHealth = 0;
            _targetMaxHealth = 1;
            TargetHealthRatio = 0f;
            _targetScatterPressure = 0;
            _targetMaxScatterPressure = 1;
            TargetScatterPressureRatio = 0f;
            _targetHealthText = string.Empty;
            _targetScatterPressureText = string.Empty;
            _targetHealthTextDirty = false;
            _targetScatterPressureTextDirty = false;
        }

        internal void SetPlayerVitals(
            int health,
            int maxHealth,
            float healthRatio,
            int scatterPressure,
            int maxScatterPressure,
            float scatterPressureRatio)
        {
            SetVitalsPair(
                ref _playerHealth,
                ref _playerMaxHealth,
                ref _playerHealthText,
                ref _playerHealthTextDirty,
                health,
                maxHealth);
            SetVitalsPair(
                ref _playerScatterPressure,
                ref _playerMaxScatterPressure,
                ref _playerScatterPressureText,
                ref _playerScatterPressureTextDirty,
                scatterPressure,
                maxScatterPressure);
            PlayerHealthRatio = healthRatio;
            PlayerScatterPressureRatio = scatterPressureRatio;
        }

        internal void SetTargetVitals(
            int health,
            int maxHealth,
            float healthRatio,
            int scatterPressure,
            int maxScatterPressure,
            float scatterPressureRatio)
        {
            SetVitalsPair(
                ref _targetHealth,
                ref _targetMaxHealth,
                ref _targetHealthText,
                ref _targetHealthTextDirty,
                health,
                maxHealth);
            SetVitalsPair(
                ref _targetScatterPressure,
                ref _targetMaxScatterPressure,
                ref _targetScatterPressureText,
                ref _targetScatterPressureTextDirty,
                scatterPressure,
                maxScatterPressure);
            TargetHealthRatio = healthRatio;
            TargetScatterPressureRatio = scatterPressureRatio;
        }

        internal void ClearAbilitySlots()
        {
            _abilitySlots = Array.Empty<CharacterTestFairyGuiCombatHudAbilitySlotViewModel>();
        }

        internal CharacterTestFairyGuiCombatHudAbilitySlotViewModel[] EnsureAbilitySlots(int count)
        {
            if (count <= 0)
            {
                ClearAbilitySlots();
                return _abilitySlots;
            }

            if (_abilitySlots.Length != count)
                _abilitySlots = new CharacterTestFairyGuiCombatHudAbilitySlotViewModel[count];

            for (int i = 0; i < _abilitySlots.Length; i++)
                _abilitySlots[i] ??= new CharacterTestFairyGuiCombatHudAbilitySlotViewModel();

            return _abilitySlots;
        }

        public void SetAbilitySlots(IReadOnlyList<CharacterTestFairyGuiCombatHudAbilitySlotViewModel> slots)
        {
            if (slots == null || slots.Count == 0)
            {
                ClearAbilitySlots();
                return;
            }

            CharacterTestFairyGuiCombatHudAbilitySlotViewModel[] abilitySlots =
                EnsureAbilitySlots(slots.Count);
            for (int i = 0; i < slots.Count; i++)
                abilitySlots[i] = slots[i] ?? new CharacterTestFairyGuiCombatHudAbilitySlotViewModel();
        }

        public void SetPlayerBuffs(IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> buffs)
        {
            CopyBuffsInto(_playerBuffs, buffs);
        }

        public void SetTargetBuffs(IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> buffs)
        {
            CopyBuffsInto(_targetBuffs, buffs);
        }

        internal List<CharacterTestFairyGuiCombatHudBuffViewModel> EnsurePlayerBuffs(int count)
        {
            return EnsureBuffs(_playerBuffs, count);
        }

        internal List<CharacterTestFairyGuiCombatHudBuffViewModel> EnsureTargetBuffs(int count)
        {
            return EnsureBuffs(_targetBuffs, count);
        }

        private static List<CharacterTestFairyGuiCombatHudBuffViewModel> EnsureBuffs(
            List<CharacterTestFairyGuiCombatHudBuffViewModel> buffs,
            int count)
        {
            if (count <= 0)
            {
                buffs.Clear();
                return buffs;
            }

            while (buffs.Count < count)
                buffs.Add(new CharacterTestFairyGuiCombatHudBuffViewModel());
            if (buffs.Count > count)
                buffs.RemoveRange(count, buffs.Count - count);
            return buffs;
        }

        private static void CopyBuffsInto(
            List<CharacterTestFairyGuiCombatHudBuffViewModel> output,
            IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> buffs)
        {
            if (ReferenceEquals(output, buffs))
                return;

            output.Clear();
            if (buffs == null || buffs.Count == 0)
                return;

            for (int i = 0; i < buffs.Count; i++)
                output.Add(buffs[i] ?? new CharacterTestFairyGuiCombatHudBuffViewModel());
        }

        private static void SetVitalsPair(
            ref int currentField,
            ref int maxField,
            ref string text,
            ref bool dirty,
            int current,
            int max)
        {
            if (currentField == current && maxField == max && !dirty)
                return;

            currentField = current;
            maxField = max;
            SetPairText(ref text, ref dirty, current, max);
        }

        private static void SetPairText(
            ref string text,
            ref bool dirty,
            int current,
            int max)
        {
            text = FormatVitalsPairText(current, max);
            dirty = false;
        }

        private static string FormatVitalsPairText(int current, int max)
        {
            if (current >= 0
                && max >= 0
                && current <= VitalsPairTextCacheLimit
                && max <= VitalsPairTextCacheLimit)
            {
                return VitalsPairTextCache[current, max];
            }

            return current.ToString(CultureInfo.InvariantCulture)
                + "/"
                + max.ToString(CultureInfo.InvariantCulture);
        }

        private static string[,] CreateVitalsPairTextCache()
        {
            var cache = new string[VitalsPairTextCacheLimit + 1, VitalsPairTextCacheLimit + 1];
            for (int current = 0; current <= VitalsPairTextCacheLimit; current++)
            {
                for (int max = 0; max <= VitalsPairTextCacheLimit; max++)
                {
                    cache[current, max] = current.ToString(CultureInfo.InvariantCulture)
                        + "/"
                        + max.ToString(CultureInfo.InvariantCulture);
                }
            }

            return cache;
        }
    }

    internal readonly struct CharacterTestFairyGuiCombatHudViewModelSignature :
        IEquatable<CharacterTestFairyGuiCombatHudViewModelSignature>
    {
        private readonly ulong _hashA;
        private readonly ulong _hashB;
        private readonly int _valueCount;

        private CharacterTestFairyGuiCombatHudViewModelSignature(ulong hashA, ulong hashB, int valueCount)
        {
            _hashA = hashA;
            _hashB = hashB;
            _valueCount = valueCount;
        }

        public static CharacterTestFairyGuiCombatHudViewModelSignature Create(
            CharacterTestFairyGuiCombatHudViewModel viewModel)
        {
            if (viewModel == null)
                return default;

            var builder = new SignatureBuilder();
            builder.Add(viewModel.HasLocalPlayer);
            builder.Add(viewModel.PlayerEntityId);
            builder.Add(viewModel.PlayerHealth);
            builder.Add(viewModel.PlayerMaxHealth);
            builder.Add(viewModel.PlayerScatterPressure);
            builder.Add(viewModel.PlayerMaxScatterPressure);
            builder.Add(viewModel.TargetVisible);
            builder.Add(viewModel.TargetLocked);
            builder.Add(viewModel.TargetEntityId);
            builder.Add(viewModel.TargetHealth);
            builder.Add(viewModel.TargetMaxHealth);
            builder.Add(viewModel.TargetScatterPressure);
            builder.Add(viewModel.TargetMaxScatterPressure);
            builder.Add(viewModel.FeedbackVisible);
            builder.Add(viewModel.NetworkLatencyVisible);
            builder.Add(viewModel.PlayerName);
            builder.Add(viewModel.PlayerScatterPressureBand);
            builder.Add(viewModel.PlayerActionText);
            builder.Add(viewModel.TargetName);
            builder.Add(viewModel.TargetScatterPressureBand);
            builder.Add(viewModel.TargetStatusText);
            builder.Add(viewModel.FeedbackText);
            builder.Add(viewModel.NetworkLatencyText);
            builder.Add(viewModel.NetworkLatencyTone);
            builder.Add(viewModel.BehaviorCardVisible);
            builder.Add(viewModel.BehaviorCardIconText);
            builder.Add(viewModel.BehaviorCardIconKey);
            builder.Add(viewModel.BehaviorCardName);
            builder.Add(viewModel.BehaviorCardReasonText);
            IReadOnlyList<CharacterTestFairyGuiCombatHudAbilitySlotViewModel> slots =
                viewModel.AbilitySlots ?? Array.Empty<CharacterTestFairyGuiCombatHudAbilitySlotViewModel>();
            builder.Add(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestFairyGuiCombatHudAbilitySlotViewModel slot = slots[i];
                builder.Add(slot != null ? slot.SlotNumber : 0);
                builder.Add(slot != null && slot.IsBound);
                builder.Add(slot != null && slot.IsSelected);
                builder.AddRoundedMillis(slot != null ? slot.CooldownRatio : 0f);
                builder.Add(slot != null ? slot.AbilityText : string.Empty);
                builder.Add(slot != null ? slot.CooldownText : string.Empty);
            }
            AppendBuffs(ref builder, "playerBuffs", viewModel.PlayerBuffs);
            AppendBuffs(ref builder, "targetBuffs", viewModel.TargetBuffs);
            return builder.ToSignature();
        }

        public bool Equals(CharacterTestFairyGuiCombatHudViewModelSignature other)
        {
            return _hashA == other._hashA &&
                _hashB == other._hashB &&
                _valueCount == other._valueCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestFairyGuiCombatHudViewModelSignature other && Equals(other);
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
            CharacterTestFairyGuiCombatHudViewModelSignature left,
            CharacterTestFairyGuiCombatHudViewModelSignature right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CharacterTestFairyGuiCombatHudViewModelSignature left,
            CharacterTestFairyGuiCombatHudViewModelSignature right)
        {
            return !left.Equals(right);
        }

        private static void AppendBuffs(
            ref SignatureBuilder builder,
            string prefix,
            IReadOnlyList<CharacterTestFairyGuiCombatHudBuffViewModel> buffs)
        {
            buffs ??= Array.Empty<CharacterTestFairyGuiCombatHudBuffViewModel>();
            builder.Add(prefix);
            builder.Add(buffs.Count);
            for (int i = 0; i < buffs.Count; i++)
            {
                CharacterTestFairyGuiCombatHudBuffViewModel buff = buffs[i];
                builder.Add(buff != null ? buff.BuffId : 0);
                builder.Add(buff != null ? (int)buff.Polarity : 0);
                builder.Add(buff != null && buff.IsPermanent);
                builder.AddRoundedMillis(buff != null ? buff.Remaining01 : 0f);
                builder.Add(buff != null ? buff.ShortName : string.Empty);
                builder.Add(buff != null ? buff.TimeText : string.Empty);
                builder.Add(buff != null ? buff.StackText : string.Empty);
                builder.Add(buff != null ? buff.TooltipText : string.Empty);
            }
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

            public void AddRoundedMillis(float value)
            {
                if (float.IsNaN(value))
                {
                    Add(int.MinValue);
                    return;
                }

                Add((int)Math.Round(value * 1000f));
            }

            public CharacterTestFairyGuiCombatHudViewModelSignature ToSignature()
            {
                ulong hashA = _hashA == 0UL ? OffsetA : _hashA;
                ulong hashB = _hashB == 0UL ? OffsetB : _hashB;
                return new CharacterTestFairyGuiCombatHudViewModelSignature(hashA, hashB, _valueCount);
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
