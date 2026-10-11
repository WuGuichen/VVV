using System;
using System.Collections.Generic;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionDamageSourceKind
    {
        Unknown = 0,
        WeaponTrace = 1,
        Scripted = 2,
    }

    public readonly struct CharacterActionDamageUsageIndexRow : IEquatable<CharacterActionDamageUsageIndexRow>
    {
        public CharacterActionDamageUsageIndexRow(
            CharacterActionId actionId,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame,
            int stableEventId,
            int equipmentSlotId = 0,
            int targetPolicyId = 0,
            CharacterActionDamageSourceKind sourceKind = CharacterActionDamageSourceKind.Unknown,
            CharacterActionDamagePayload damagePayload = default)
        {
            ActionId = actionId;
            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame < 0 ? 0 : frame;
            StableEventId = stableEventId;
            EquipmentSlotId = equipmentSlotId;
            TargetPolicyId = targetPolicyId;
            SourceKind = sourceKind;
            DamagePayload = damagePayload;
        }

        public CharacterActionId ActionId { get; }
        public CharacterActionTrackKind TrackKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int Frame { get; }
        public int StableEventId { get; }
        public int EquipmentSlotId { get; }
        public int TargetPolicyId { get; }
        public CharacterActionDamageSourceKind SourceKind { get; }
        public CharacterActionDamagePayload DamagePayload { get; }

        public bool Equals(CharacterActionDamageUsageIndexRow other)
        {
            return ActionId == other.ActionId
                && TrackKind == other.TrackKind
                && EventKind == other.EventKind
                && Frame == other.Frame
                && (StableEventId == other.StableEventId)
                && (EquipmentSlotId == other.EquipmentSlotId)
                && (TargetPolicyId == other.TargetPolicyId)
                && SourceKind == other.SourceKind
                && DamagePayload.Equals(other.DamagePayload);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDamageUsageIndexRow other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionId.GetHashCode();
                hash = (hash * 397) ^ (int)TrackKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ StableEventId;
                hash = (hash * 397) ^ EquipmentSlotId;
                hash = (hash * 397) ^ TargetPolicyId;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ DamagePayload.GetHashCode();
                return hash;
            }
        }
    }

    public static class CharacterActionDamageUsageIndexBuilder
    {
        public static CharacterActionDamageUsageIndexRow[] Build(params CharacterActionConfig[] actions)
        {
            if (actions == null || actions.Length == 0)
                return Array.Empty<CharacterActionDamageUsageIndexRow>();

            var rows = new List<CharacterActionDamageUsageIndexRow>();
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null)
                    continue;

                AddCombatRows(action, rows);
                AddGameplayRows(action, rows);
            }

            rows.Sort(CompareRows);
            return rows.ToArray();
        }

        private static void AddCombatRows(
            CharacterActionConfig action,
            List<CharacterActionDamageUsageIndexRow> rows)
        {
            CombatTrackEvent[] events = action.CombatTrack?.Events ?? Array.Empty<CombatTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                CombatTrackEvent evt = events[i];
                if (!evt.DamagePayload.HasInlineData)
                    continue;

                rows.Add(new CharacterActionDamageUsageIndexRow(
                    action.ActionId,
                    CharacterActionTrackKind.Combat,
                    evt.Kind,
                    evt.Frame,
                    evt.StableEventId,
                    equipmentSlotId: evt.EquipmentSlotId,
                    sourceKind: CharacterActionDamageSourceKind.WeaponTrace,
                    damagePayload: evt.DamagePayload));
            }
        }

        private static void AddGameplayRows(
            CharacterActionConfig action,
            List<CharacterActionDamageUsageIndexRow> rows)
        {
            GameplayTrackEvent[] events = action.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                GameplayTrackEvent evt = events[i];
                if (!evt.DamagePayload.HasInlineData)
                    continue;

                rows.Add(new CharacterActionDamageUsageIndexRow(
                    action.ActionId,
                    CharacterActionTrackKind.Gameplay,
                    evt.Kind,
                    evt.Frame,
                    evt.StableEventId,
                    targetPolicyId: evt.TargetPolicyId,
                    sourceKind: CharacterActionDamageSourceKind.Scripted,
                    damagePayload: evt.DamagePayload));
            }
        }

        private static int CompareRows(
            CharacterActionDamageUsageIndexRow left,
            CharacterActionDamageUsageIndexRow right)
        {
            int compare = left.ActionId.CompareTo(right.ActionId);
            if (compare != 0)
                return compare;
            compare = left.Frame.CompareTo(right.Frame);
            if (compare != 0)
                return compare;
            compare = left.TrackKind.CompareTo(right.TrackKind);
            if (compare != 0)
                return compare;
            return (left.StableEventId).CompareTo(right.StableEventId);
        }
    }
}
