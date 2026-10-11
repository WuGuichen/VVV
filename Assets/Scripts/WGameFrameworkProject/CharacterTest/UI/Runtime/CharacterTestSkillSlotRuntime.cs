using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestSkillSlotRuntime
    {
        public const int SlotCount = 9;
        public const string EmptyRejectReason = "SkillSlotEmpty";
        public const string MissingAbilityRejectReason = "SkillSlotAbilityMissing";

        private readonly int[] _slotInputIntentIds = new int[SlotCount];
        private readonly CharacterTestSkillCandidate[] _candidates;
        private string _lastMessage = string.Empty;

        private CharacterTestSkillSlotRuntime(CharacterTestSkillCandidate[] candidates)
        {
            _candidates = candidates ?? Array.Empty<CharacterTestSkillCandidate>();
        }

        public string LastMessage => _lastMessage;
        public IReadOnlyList<CharacterTestSkillCandidate> Candidates => _candidates;

        public static CharacterTestSkillSlotRuntime CreateDefault()
        {
            var runtime = new CharacterTestSkillSlotRuntime(new[]
            {
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballAbilityId,
                    CharacterTestAbilityIds.FireballInputIntentId,
                    "Fireball",
                    "火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballBurstAbilityId,
                    CharacterTestAbilityIds.FireballBurstInputIntentId,
                    "Fireball Burst",
                    "爆裂火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FlameLanceAbilityId,
                    CharacterTestAbilityIds.FlameLanceInputIntentId,
                    "Flame Lance",
                    "火焰枪"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballLobAbilityId,
                    CharacterTestAbilityIds.FireballLobInputIntentId,
                    "Fireball Lob",
                    "抛射火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballSurgeAbilityId,
                    CharacterTestAbilityIds.FireballSurgeInputIntentId,
                    "Fireball Surge",
                    "疾冲火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballReturnAbilityId,
                    CharacterTestAbilityIds.FireballReturnInputIntentId,
                    "Fireball Return",
                    "回返火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.TornadoAbilityId,
                    CharacterTestAbilityIds.TornadoInputIntentId,
                    "Tornado",
                    "Tornado"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballSwayAbilityId,
                    CharacterTestAbilityIds.FireballSwayInputIntentId,
                    "Fireball Sway",
                    "摆动火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballHomingAbilityId,
                    CharacterTestAbilityIds.FireballHomingInputIntentId,
                    "Fireball Homing",
                    "追踪火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.FireballBezierAbilityId,
                    CharacterTestAbilityIds.FireballBezierInputIntentId,
                    "Fireball Bezier",
                    "贝塞尔火球"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.BeamLanceAbilityId,
                    CharacterTestAbilityIds.BeamLanceInputIntentId,
                    "Beam Lance",
                    "Beam Lance"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.ChannelBeamAbilityId,
                    CharacterTestAbilityIds.ChannelBeamInputIntentId,
                    "Channel Beam",
                    "Channel Beam"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.LaserLanceAbilityId,
                    CharacterTestAbilityIds.LaserLanceInputIntentId,
                    "Laser Lance",
                    "Laser Lance"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.ChainLightningAbilityId,
                    CharacterTestAbilityIds.ChainLightningInputIntentId,
                    "Chain Lightning",
                    "Chain Lightning"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.PoisonBottleAbilityId,
                    CharacterTestAbilityIds.PoisonBottleInputIntentId,
                    "Poison Bottle",
                    "Poison Bottle"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.StunBoltAbilityId,
                    CharacterTestAbilityIds.StunBoltInputIntentId,
                    "Stun Bolt",
                    "Stun Bolt"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.SwordWaveAbilityId,
                    CharacterTestAbilityIds.SwordWaveInputIntentId,
                    "Sword Wave",
                    "Sword Wave"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.DashStrikeAbilityId,
                    CharacterTestAbilityIds.DashStrikeInputIntentId,
                    "Dash Strike",
                    "Dash Strike"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.HookChainAbilityId,
                    CharacterTestAbilityIds.HookChainInputIntentId,
                    "Hook Chain",
                    "Hook Chain"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.ArcMineAbilityId,
                    CharacterTestAbilityIds.ArcMineInputIntentId,
                    "Arc Mine",
                    "Arc Mine"),
                new CharacterTestSkillCandidate(
                    CharacterTestAbilityIds.RuneTurretAbilityId,
                    CharacterTestAbilityIds.RuneTurretInputIntentId,
                    "Rune Turret",
                    "Rune Turret")
            });

            for (int i = 0; i < runtime._candidates.Length && i < SlotCount; i++)
                runtime._slotInputIntentIds[i] = runtime._candidates[i].InputIntentId;

            return runtime;
        }

        public bool TryEquip(int slotIndex, int inputIntentId, out string message)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                message = "技能槽编号无效。";
                _lastMessage = message;
                return false;
            }

            if (inputIntentId == 0)
            {
                _slotInputIntentIds[slotIndex] = 0;
                message = "已清空 " + FormatSlot(slotIndex) + "。";
                _lastMessage = message;
                return true;
            }

            if (!TryGetCandidate(inputIntentId, out CharacterTestSkillCandidate candidate))
            {
                message = "技能候选不存在：" + inputIntentId;
                _lastMessage = message;
                return false;
            }

            _slotInputIntentIds[slotIndex] = candidate.InputIntentId;
            message = "已将 " + candidate.DisplayNameCn + " 装备到 " + FormatSlot(slotIndex) + "。";
            _lastMessage = message;
            return true;
        }

        public bool TryResolveSlot(int slotIndex, out CharacterTestSkillSlotBinding binding)
        {
            binding = CharacterTestSkillSlotBinding.Empty(slotIndex);
            if (!IsValidSlotIndex(slotIndex))
                return false;

            int inputIntentId = _slotInputIntentIds[slotIndex];
            if (inputIntentId == 0)
                return false;

            if (TryGetCandidate(inputIntentId, out CharacterTestSkillCandidate candidate))
                binding = new CharacterTestSkillSlotBinding(slotIndex, candidate);
            else
                binding = new CharacterTestSkillSlotBinding(
                    slotIndex,
                    new CharacterTestSkillCandidate(0, inputIntentId, "Unknown skill", "未知技能"));

            return true;
        }

        public CharacterTestSkillPanelSnapshot CreateSnapshot(
            AbilityRuntimeModule abilityRuntime,
            int casterEntityId,
            RuntimeFrame frame,
            int selectedSlotIndex)
        {
            var slots = new CharacterTestSkillSlotSnapshot[SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                TryResolveSlot(i, out CharacterTestSkillSlotBinding binding);
                AbilityDefinitionConfig ability = null;
                long remainingCooldownFrames = 0L;
                bool hasAbility = false;
                if (binding.InputIntentId > 0
                    && abilityRuntime != null
                    && abilityRuntime.Definitions.TryGetByInputIntent(
                        binding.InputIntentId,
                        out ability))
                {
                    hasAbility = true;
                    remainingCooldownFrames = abilityRuntime.GetRemainingCooldownFrames(
                        casterEntityId,
                        ability,
                        frame);
                }

                slots[i] = new CharacterTestSkillSlotSnapshot(
                    i,
                    binding.InputIntentId,
                    binding.DisplayName,
                    binding.DisplayNameCn,
                    binding.AbilityId,
                    hasAbility,
                    remainingCooldownFrames);
            }

            return new CharacterTestSkillPanelSnapshot(
                slots,
                _candidates,
                ClampSlotIndex(selectedSlotIndex),
                _lastMessage);
        }

        public static int FirstPressedSlotIndex(int mask)
        {
            int safeMask = mask & CharacterTestControlIntent.SkillSlotMaskBits;
            for (int i = 0; i < SlotCount; i++)
            {
                if ((safeMask & (1 << i)) != 0)
                    return i;
            }

            return -1;
        }

        public static int ToMask(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex) ? 1 << slotIndex : 0;
        }

        public static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }

        public static int ClampSlotIndex(int slotIndex)
        {
            if (slotIndex < 0)
                return 0;
            if (slotIndex >= SlotCount)
                return SlotCount - 1;
            return slotIndex;
        }

        private bool TryGetCandidate(int inputIntentId, out CharacterTestSkillCandidate candidate)
        {
            for (int i = 0; i < _candidates.Length; i++)
            {
                if (_candidates[i].InputIntentId == inputIntentId)
                {
                    candidate = _candidates[i];
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        private static string FormatSlot(int slotIndex)
        {
            return (slotIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " 号槽";
        }
    }

    public readonly struct CharacterTestSkillCandidate
    {
        public CharacterTestSkillCandidate(
            int abilityId,
            int inputIntentId,
            string displayName,
            string displayNameCn)
        {
            AbilityId = abilityId;
            InputIntentId = inputIntentId;
            DisplayName = displayName ?? string.Empty;
            DisplayNameCn = string.IsNullOrWhiteSpace(displayNameCn)
                ? DisplayName
                : displayNameCn;
        }

        public int AbilityId { get; }
        public int InputIntentId { get; }
        public string DisplayName { get; }
        public string DisplayNameCn { get; }
    }

    public readonly struct CharacterTestSkillSlotBinding
    {
        public CharacterTestSkillSlotBinding(int slotIndex, CharacterTestSkillCandidate candidate)
        {
            SlotIndex = slotIndex;
            Candidate = candidate;
        }

        public int SlotIndex { get; }
        public CharacterTestSkillCandidate Candidate { get; }
        public int AbilityId => Candidate.AbilityId;
        public int InputIntentId => Candidate.InputIntentId;
        public string DisplayName => Candidate.DisplayName;
        public string DisplayNameCn => Candidate.DisplayNameCn;

        public static CharacterTestSkillSlotBinding Empty(int slotIndex)
        {
            return new CharacterTestSkillSlotBinding(
                slotIndex,
                new CharacterTestSkillCandidate(0, 0, "Empty", "空"));
        }
    }

    public readonly struct CharacterTestSkillSlotSnapshot
    {
        public CharacterTestSkillSlotSnapshot(
            int slotIndex,
            int inputIntentId,
            string displayName,
            string displayNameCn,
            int abilityId,
            bool hasAbilityDefinition,
            long remainingCooldownFrames)
        {
            SlotIndex = slotIndex;
            InputIntentId = inputIntentId;
            DisplayName = displayName ?? string.Empty;
            DisplayNameCn = string.IsNullOrWhiteSpace(displayNameCn)
                ? DisplayName
                : displayNameCn;
            AbilityId = abilityId;
            HasAbilityDefinition = hasAbilityDefinition;
            RemainingCooldownFrames = remainingCooldownFrames < 0L ? 0L : remainingCooldownFrames;
        }

        public int SlotIndex { get; }
        public int InputIntentId { get; }
        public string DisplayName { get; }
        public string DisplayNameCn { get; }
        public int AbilityId { get; }
        public bool HasAbilityDefinition { get; }
        public long RemainingCooldownFrames { get; }
        public bool IsEmpty => InputIntentId == 0;
        public bool IsOnCooldown => RemainingCooldownFrames > 0L;
    }

    public sealed class CharacterTestSkillPanelSnapshot
    {
        public CharacterTestSkillPanelSnapshot(
            IReadOnlyList<CharacterTestSkillSlotSnapshot> slots,
            IReadOnlyList<CharacterTestSkillCandidate> candidates,
            int selectedSlotIndex,
            string message)
        {
            Slots = slots ?? Array.Empty<CharacterTestSkillSlotSnapshot>();
            Candidates = candidates ?? Array.Empty<CharacterTestSkillCandidate>();
            SelectedSlotIndex = CharacterTestSkillSlotRuntime.ClampSlotIndex(selectedSlotIndex);
            Message = message ?? string.Empty;
        }

        public IReadOnlyList<CharacterTestSkillSlotSnapshot> Slots { get; }
        public IReadOnlyList<CharacterTestSkillCandidate> Candidates { get; }
        public int SelectedSlotIndex { get; }
        public string Message { get; }
    }
}
