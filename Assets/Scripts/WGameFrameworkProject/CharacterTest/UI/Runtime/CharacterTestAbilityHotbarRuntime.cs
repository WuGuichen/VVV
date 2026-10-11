using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestAbilityHotbarRuntime
    {
        public const int SlotCount = 9;
        public const int FirstSlotNumber = 1;
        public const int LastSlotNumber = 9;
        public const int EmptyAbilityId = 0;
        public const int PersistenceSchemaVersion = 1;
        public const int PersistenceDefaultLayoutVersion = 3;
        private const int FireballExplosionDefaultLayoutVersion = 2;
        private const int TornadoDefaultLayoutVersion = 3;

        private static readonly int[] DefaultAbilitySortOrder = CreateDefaultAbilitySortOrder();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };

        private readonly AbilityDefinitionRegistry _definitions;
        private readonly int[] _slotAbilityIds = new int[SlotCount];
        private readonly List<AbilityDefinitionConfig> _availableAbilities =
            new List<AbilityDefinitionConfig>();
        private readonly Dictionary<int, string> _abilityShortNames =
            new Dictionary<int, string>();
        private string _filePath = string.Empty;

        public CharacterTestAbilityHotbarRuntime(AbilityDefinitionRegistry definitions, string filePath = "")
        {
            _definitions = definitions ?? new AbilityDefinitionRegistry();
            _availableAbilities.AddRange(_definitions.CreateSnapshot());
            _availableAbilities.Sort(CompareAbilities);
            for (int i = 0; i < _availableAbilities.Count; i++)
            {
                AbilityDefinitionConfig ability = _availableAbilities[i];
                if (ability != null)
                    _abilityShortNames[ability.AbilityId] = CreateShortName(ability);
            }

            _filePath = filePath ?? string.Empty;
            ResetToDefaults();
        }

        public string FilePath => _filePath;

        public IReadOnlyList<AbilityDefinitionConfig> AvailableAbilities =>
            new ReadOnlyCollection<AbilityDefinitionConfig>(_availableAbilities);

        public CharacterTestAbilityHotbarSlotBinding[] CreateBindingSnapshot()
        {
            var snapshot = new CharacterTestAbilityHotbarSlotBinding[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                snapshot[i] = new CharacterTestAbilityHotbarSlotBinding(i + FirstSlotNumber, _slotAbilityIds[i]);
            return snapshot;
        }

        public CharacterTestAbilityHotbarSlotState[] CreateSlotStates(
            AbilityRuntimeModule abilityRuntime,
            int casterEntityId,
            RuntimeFrame frame)
        {
            var states = new CharacterTestAbilityHotbarSlotState[SlotCount];
            FillSlotStates(abilityRuntime, casterEntityId, frame, states);
            return states;
        }

        public int FillSlotStates(
            AbilityRuntimeModule abilityRuntime,
            int casterEntityId,
            RuntimeFrame frame,
            CharacterTestAbilityHotbarSlotState[] states)
        {
            if (states == null)
                return 0;

            int count = Math.Min(SlotCount, states.Length);
            for (int i = 0; i < count; i++)
            {
                int slotNumber = i + FirstSlotNumber;
                int abilityId = _slotAbilityIds[i];
                AbilityDefinitionConfig ability = null;
                bool known = abilityId > 0 && _definitions.TryGet(abilityId, out ability);
                long remainingFrames = known && abilityRuntime != null
                    ? abilityRuntime.GetRemainingCooldownFrames(casterEntityId, ability, frame)
                    : 0L;
                states[i] = new CharacterTestAbilityHotbarSlotState(
                    slotNumber,
                    abilityId,
                    known,
                    known ? ability.DisplayName : string.Empty,
                    known ? GetShortName(ability) : string.Empty,
                    known ? ability.Cooldown.Frames : 0L,
                    remainingFrames);
            }

            return count;
        }

        public bool TryGetAbilityForSlot(int slotNumber, out AbilityDefinitionConfig ability)
        {
            ability = null;
            if (!IsValidSlot(slotNumber))
                return false;

            int abilityId = _slotAbilityIds[slotNumber - FirstSlotNumber];
            return abilityId > 0 && _definitions.TryGet(abilityId, out ability);
        }

        public int GetAbilityIdForSlot(int slotNumber)
        {
            return IsValidSlot(slotNumber)
                ? _slotAbilityIds[slotNumber - FirstSlotNumber]
                : EmptyAbilityId;
        }

        public bool AssignSlot(int slotNumber, int abilityId, out string message)
        {
            if (!IsValidSlot(slotNumber))
            {
                message = "invalid slot";
                return false;
            }

            if (abilityId <= 0)
            {
                _slotAbilityIds[slotNumber - FirstSlotNumber] = EmptyAbilityId;
                message = "cleared";
                return true;
            }

            if (!_definitions.TryGet(abilityId, out _))
            {
                message = "unknown ability";
                return false;
            }

            _slotAbilityIds[slotNumber - FirstSlotNumber] = abilityId;
            message = "assigned";
            return true;
        }

        public bool ClearSlot(int slotNumber, out string message)
        {
            return AssignSlot(slotNumber, EmptyAbilityId, out message);
        }

        public void ResetToDefaults()
        {
            int[] defaults = CreateDefaultAbilityIds();
            for (int i = 0; i < SlotCount; i++)
            {
                int abilityId = defaults[i];
                _slotAbilityIds[i] = abilityId > 0 && _definitions.TryGet(abilityId, out _)
                    ? abilityId
                    : EmptyAbilityId;
            }
        }

        public void ApplyBindings(IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings)
        {
            for (int i = 0; i < SlotCount; i++)
                _slotAbilityIds[i] = EmptyAbilityId;

            if (bindings == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                CharacterTestAbilityHotbarSlotBinding binding = bindings[i];
                if (!IsValidSlot(binding.SlotNumber))
                    continue;

                if (binding.AbilityId <= 0)
                {
                    _slotAbilityIds[binding.SlotNumber - FirstSlotNumber] = EmptyAbilityId;
                    continue;
                }

                if (_definitions.TryGet(binding.AbilityId, out _))
                    _slotAbilityIds[binding.SlotNumber - FirstSlotNumber] = binding.AbilityId;
            }
        }

        public CharacterTestAbilityHotbarPersistenceResult LoadFromFile(string filePath)
        {
            _filePath = filePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_filePath))
            {
                ResetToDefaults();
                return CharacterTestAbilityHotbarPersistenceResult.Succeeded("No hotbar file path; defaults loaded.");
            }

            if (!File.Exists(_filePath))
            {
                ResetToDefaults();
                return CharacterTestAbilityHotbarPersistenceResult.Succeeded("Hotbar file missing; defaults loaded.");
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                HotbarDto dto = JsonConvert.DeserializeObject<HotbarDto>(json, JsonSettings);
                if (dto == null)
                {
                    ResetToDefaults();
                    return CharacterTestAbilityHotbarPersistenceResult.Failed(
                        "Hotbar file was empty; defaults loaded.");
                }

                ApplyBindings(Convert(dto.slots));
                bool migrated = ApplyDefaultLayoutMigrations(dto.defaultLayoutVersion);
                return CharacterTestAbilityHotbarPersistenceResult.Succeeded(migrated
                    ? "Hotbar loaded; default layout migrations applied."
                    : "Hotbar loaded.");
            }
            catch (Exception ex)
            {
                ResetToDefaults();
                return CharacterTestAbilityHotbarPersistenceResult.Failed(
                    "Hotbar load failed; defaults loaded. " + ex.Message);
            }
        }

        public CharacterTestAbilityHotbarPersistenceResult SaveToFile(string filePath = "")
        {
            string path = string.IsNullOrWhiteSpace(filePath) ? _filePath : filePath;
            _filePath = path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_filePath))
                return CharacterTestAbilityHotbarPersistenceResult.Failed("Hotbar file path is missing.");

            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var dto = new HotbarDto
                {
                    schemaVersion = PersistenceSchemaVersion,
                    defaultLayoutVersion = PersistenceDefaultLayoutVersion,
                    updatedUtc = DateTime.UtcNow.ToString("o"),
                    slots = Convert(CreateBindingSnapshot())
                };
                File.WriteAllText(_filePath, JsonConvert.SerializeObject(dto, JsonSettings));
                return CharacterTestAbilityHotbarPersistenceResult.Succeeded("Hotbar saved.");
            }
            catch (Exception ex)
            {
                return CharacterTestAbilityHotbarPersistenceResult.Failed(
                    "Hotbar save failed. " + ex.Message);
            }
        }

        public static bool IsValidSlot(int slotNumber)
        {
            return slotNumber >= FirstSlotNumber && slotNumber <= LastSlotNumber;
        }

        public static int[] CreateDefaultAbilityIds()
        {
            return new[]
            {
                CharacterTestAbilityIds.FireballAbilityId,
                CharacterTestAbilityIds.FireballBurstAbilityId,
                CharacterTestAbilityIds.FlameLanceAbilityId,
                CharacterTestAbilityIds.FireballLobAbilityId,
                CharacterTestAbilityIds.FireballSurgeAbilityId,
                CharacterTestAbilityIds.FireballReturnAbilityId,
                CharacterTestAbilityIds.TornadoAbilityId,
                CharacterTestAbilityIds.GuardAuraAbilityId,
                CharacterTestAbilityIds.FireballExplosionAbilityId
            };
        }

        private static int[] CreateDefaultAbilitySortOrder()
        {
            return new[]
            {
                CharacterTestAbilityIds.FireballAbilityId,
                CharacterTestAbilityIds.FireballBurstAbilityId,
                CharacterTestAbilityIds.FlameLanceAbilityId,
                CharacterTestAbilityIds.FireballLobAbilityId,
                CharacterTestAbilityIds.FireballSurgeAbilityId,
                CharacterTestAbilityIds.FireballReturnAbilityId,
                CharacterTestAbilityIds.TornadoAbilityId,
                CharacterTestAbilityIds.GuardAuraAbilityId,
                CharacterTestAbilityIds.FireballExplosionAbilityId,
                CharacterTestAbilityIds.PhysicalShieldAbilityId,
                CharacterTestAbilityIds.AreaConfinementAbilityId,
                CharacterTestAbilityIds.BeamLanceAbilityId,
                CharacterTestAbilityIds.ChannelBeamAbilityId,
                CharacterTestAbilityIds.LaserLanceAbilityId,
                CharacterTestAbilityIds.ChainLightningAbilityId,
                CharacterTestAbilityIds.PoisonBottleAbilityId,
                CharacterTestAbilityIds.StunBoltAbilityId,
                CharacterTestAbilityIds.MeteorBarrageAbilityId,
                CharacterTestAbilityIds.SwordWaveAbilityId,
                CharacterTestAbilityIds.DashStrikeAbilityId,
                CharacterTestAbilityIds.HookChainAbilityId,
                CharacterTestAbilityIds.ArcMineAbilityId,
                CharacterTestAbilityIds.RuneTurretAbilityId
            };
        }

        public static string CreateShortName(AbilityDefinitionConfig ability)
        {
            if (ability == null)
                return string.Empty;

            string name = ability.DisplayName ?? string.Empty;
            const string prefix = "CharacterTest ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name.Substring(prefix.Length);
            const string fireballPrefix = "Fireball ";
            if (name.StartsWith(fireballPrefix, StringComparison.Ordinal)
                && name.Length > fireballPrefix.Length)
            {
                name = name.Substring(fireballPrefix.Length);
            }

            return string.IsNullOrWhiteSpace(name)
                ? ability.AbilityId.ToString()
                : name;
        }

        private string GetShortName(AbilityDefinitionConfig ability)
        {
            if (ability == null)
                return string.Empty;

            return _abilityShortNames.TryGetValue(ability.AbilityId, out string shortName)
                ? shortName
                : CreateShortName(ability);
        }

        private static int CompareAbilities(AbilityDefinitionConfig left, AbilityDefinitionConfig right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int leftSort = GetDefaultAbilitySortIndex(left.AbilityId);
            int rightSort = GetDefaultAbilitySortIndex(right.AbilityId);
            if (leftSort != rightSort)
                return leftSort.CompareTo(rightSort);

            return left.AbilityId.CompareTo(right.AbilityId);
        }

        private bool ApplyDefaultLayoutMigrations(int defaultLayoutVersion)
        {
            bool migrated = false;
            if (defaultLayoutVersion < FireballExplosionDefaultLayoutVersion)
                migrated |= TryMigrateFireballExplosionDefaultSlot();
            if (defaultLayoutVersion < TornadoDefaultLayoutVersion)
                migrated |= TryMigrateTornadoDefaultSlot();

            return migrated;
        }

        private bool TryMigrateFireballExplosionDefaultSlot()
        {
            int abilityId = CharacterTestAbilityIds.FireballExplosionAbilityId;
            if (!_definitions.TryGet(abilityId, out _))
                return false;
            if (ContainsAbility(abilityId))
                return false;

            int slotIndex = LastSlotNumber - FirstSlotNumber;
            if (slotIndex < 0 || slotIndex >= _slotAbilityIds.Length)
                return false;
            if (_slotAbilityIds[slotIndex] != EmptyAbilityId)
                return false;

            _slotAbilityIds[slotIndex] = abilityId;
            return true;
        }

        private bool TryMigrateTornadoDefaultSlot()
        {
            if (!_definitions.TryGet(CharacterTestAbilityIds.TornadoAbilityId, out _))
                return false;

            int slotIndex = 7 - FirstSlotNumber;
            if (slotIndex < 0 || slotIndex >= _slotAbilityIds.Length)
                return false;
            if (_slotAbilityIds[slotIndex] != CharacterTestAbilityIds.FireballSwayAbilityId)
                return false;

            _slotAbilityIds[slotIndex] = CharacterTestAbilityIds.TornadoAbilityId;
            return true;
        }

        private bool ContainsAbility(int abilityId)
        {
            for (int i = 0; i < _slotAbilityIds.Length; i++)
            {
                if (_slotAbilityIds[i] == abilityId)
                    return true;
            }

            return false;
        }

        private static int GetDefaultAbilitySortIndex(int abilityId)
        {
            for (int i = 0; i < DefaultAbilitySortOrder.Length; i++)
            {
                if (DefaultAbilitySortOrder[i] == abilityId)
                    return i;
            }

            return int.MaxValue;
        }

        private static CharacterTestAbilityHotbarSlotBinding[] Convert(SlotDto[] slots)
        {
            if (slots == null || slots.Length == 0)
                return Array.Empty<CharacterTestAbilityHotbarSlotBinding>();

            var result = new CharacterTestAbilityHotbarSlotBinding[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                SlotDto slot = slots[i] ?? new SlotDto();
                result[i] = new CharacterTestAbilityHotbarSlotBinding(slot.slotNumber, slot.abilityId);
            }

            return result;
        }

        private static SlotDto[] Convert(IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
                return Array.Empty<SlotDto>();

            var result = new SlotDto[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
            {
                CharacterTestAbilityHotbarSlotBinding binding = bindings[i];
                result[i] = new SlotDto
                {
                    slotNumber = binding.SlotNumber,
                    abilityId = binding.AbilityId
                };
            }

            return result;
        }

        private sealed class HotbarDto
        {
            public int schemaVersion;
            public int defaultLayoutVersion;
            public string updatedUtc;
            public SlotDto[] slots;
        }

        private sealed class SlotDto
        {
            public int slotNumber;
            public int abilityId;
        }
    }

    public readonly struct CharacterTestAbilityHotbarSlotBinding
    {
        public CharacterTestAbilityHotbarSlotBinding(int slotNumber, int abilityId)
        {
            SlotNumber = slotNumber;
            AbilityId = abilityId > 0 ? abilityId : CharacterTestAbilityHotbarRuntime.EmptyAbilityId;
        }

        public int SlotNumber { get; }
        public int AbilityId { get; }
    }

    public readonly struct CharacterTestAbilityHotbarSlotState
    {
        public CharacterTestAbilityHotbarSlotState(
            int slotNumber,
            int abilityId,
            bool knownAbility,
            string displayName,
            string shortName,
            long cooldownFrames,
            long cooldownRemainingFrames)
        {
            SlotNumber = slotNumber;
            AbilityId = abilityId > 0 ? abilityId : CharacterTestAbilityHotbarRuntime.EmptyAbilityId;
            KnownAbility = knownAbility;
            DisplayName = displayName ?? string.Empty;
            ShortName = shortName ?? string.Empty;
            CooldownFrames = cooldownFrames < 0L ? 0L : cooldownFrames;
            CooldownRemainingFrames = cooldownRemainingFrames < 0L ? 0L : cooldownRemainingFrames;
        }

        public int SlotNumber { get; }
        public int AbilityId { get; }
        public bool KnownAbility { get; }
        public bool IsEmpty => AbilityId <= 0;
        public string DisplayName { get; }
        public string ShortName { get; }
        public long CooldownFrames { get; }
        public long CooldownRemainingFrames { get; }
        public float Cooldown01 => CooldownFrames <= 0L || CooldownRemainingFrames <= 0L
            ? 0f
            : Math.Min(1f, (float)CooldownRemainingFrames / CooldownFrames);
    }

    public readonly struct CharacterTestAbilityHotbarPersistenceResult
    {
        private CharacterTestAbilityHotbarPersistenceResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static CharacterTestAbilityHotbarPersistenceResult Succeeded(string message)
        {
            return new CharacterTestAbilityHotbarPersistenceResult(true, message);
        }

        public static CharacterTestAbilityHotbarPersistenceResult Failed(string message)
        {
            return new CharacterTestAbilityHotbarPersistenceResult(false, message);
        }
    }
}
