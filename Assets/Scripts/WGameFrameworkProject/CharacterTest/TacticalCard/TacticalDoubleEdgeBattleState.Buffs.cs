using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly List<BuffLineage> _buffLineages = new List<BuffLineage>();
        internal readonly struct BuffLineage
        {
            internal readonly GameplayEntityId Target;
            internal readonly int Buff, CombatSource;
            internal readonly bool ActualEnemy;
            internal readonly long StartFrame, Revision;
            internal readonly ProducerBinding Binding;
            internal BuffLineage(GameplayEntityId target, int buff, int combatSource, long start, long revision, ProducerBinding binding, bool actualEnemy)
            { Target = target; Buff = buff; CombatSource = combatSource; StartFrame = start; Revision = revision; Binding = binding; ActualEnemy = actualEnemy; }
        }
        internal BuffLineage FindBuffLineage(GameplayEntityId target, in GameplayComponentBuffEntry buff)
        {
            foreach (var saved in _buffLineages)
                if (saved.Target == target && saved.Buff == buff.BuffId && saved.StartFrame == buff.StartFrame) return saved;
            return default;
        }

        // Refresh preserves the actual existing Buff instance and its initial lineage. Removal/reapplication creates a new revision,
        // including a new application in the same frame. An unobserved pre-existing Buff remains unowned on refresh.
        internal bool BindBuffApplication(GameplayEntityId target, in GameplayComponentBuffEntry buff, bool refreshed, long frame,
            int combatSource, in TacticalDirectAttackStamp root, in TacticalSecondBatchSource parent,
            in TacticalSecondBatchHitKey acceptedParent = default, bool actualEnemy = false)
        {
            if (!HasState) return true;
            int index = -1;
            for (int i = 0; i < _buffLineages.Count; i++)
                if (_buffLineages[i].Target == target && _buffLineages[i].Buff == buff.BuffId) { index = i; break; }
            if (refreshed && index >= 0 && _buffLineages[index].StartFrame == buff.StartFrame) return true;
            long revision = index < 0 ? 1 : checked(_buffLineages[index].Revision + 1);
            if (revision > uint.MaxValue) return false;
            long id = ((long)(uint)target.Index << 32) | (uint)buff.BuffId;
            long generation = ((long)(uint)target.Generation << 32) | (uint)revision;
            if (refreshed && index >= 0 && _buffLineages[index].StartFrame != buff.StartFrame) refreshed = false;
            ProducerBinding binding = default;
            if (!refreshed && root.IsValid) {
                if (!BindProducer(7, id, generation, root, parent, frame, acceptedParent)) return false;
                binding = FindProducer(7, id, generation);
            }
            var value = new BuffLineage(target, buff.BuffId, refreshed ? 0 : combatSource, buff.StartFrame, revision, binding, !refreshed && actualEnemy);
            if (index >= 0) _buffLineages[index] = value;
            else {
                index = 0;
                while (index < _buffLineages.Count && (_buffLineages[index].Target.CompareTo(target) < 0
                    || _buffLineages[index].Target == target && _buffLineages[index].Buff < buff.BuffId)) index++;
                _buffLineages.Insert(index, value);
            }
            _revision++; return true;
        }

        private void AddBuffLineageHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(8, _buffLineages.Count);
            for (int i = 0; i < _buffLineages.Count; i++) {
                var b = _buffLineages[i]; hash.BeginElement(i);
                hash.AddInt(1, b.Target.Index); hash.AddInt(2, b.Target.Generation); hash.AddInt(3, b.Buff);
                hash.AddInt(4, b.CombatSource); hash.AddLong(5, b.StartFrame); hash.AddLong(6, b.Revision);
                hash.AddLong(7, b.Binding.Id); hash.AddLong(8, b.Binding.Generation); hash.AddBool(9, b.ActualEnemy);
                hash.EndElement();
            }
            hash.EndArray();
        }
        private void AppendBuffLineageProjection(List<long[]> rows)
        {
            foreach (var b in _buffLineages) rows.Add(new long[] { 12, b.Target.Index, b.Target.Generation, b.Buff,
                b.CombatSource, b.StartFrame, b.Revision, b.Binding.Id, b.Binding.Generation, b.ActualEnemy ? 1 : 0 });
        }
    }
}
