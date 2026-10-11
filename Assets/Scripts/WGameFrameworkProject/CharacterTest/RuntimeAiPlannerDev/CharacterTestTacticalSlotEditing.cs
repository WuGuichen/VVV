using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalSlotEditing
    {
        internal static void Rename(CharacterTestTacticalAuthoringSource source, string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(newId) || newId.Length > 128
                || newId.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '-' || c == '_')))
                throw new ArgumentException("槽标识使用字母、数字、点、横线或下划线。");
            var slot = source.Slots.Single(s => s.SlotId == oldId);
            if (newId != oldId && source.Slots.Any(s => s.SlotId == newId)) throw new ArgumentException("槽标识已存在。");
            slot.SlotId = newId; slot.Resolver = "binding";
            foreach (var evt in source.Timelines.SelectMany(t => t.Events))
            {
                if (evt.Operation.SlotId == oldId) evt.Operation.SlotId = newId;
                if (evt.Operation.Movement?.ActionSlotId == oldId) evt.Operation.Movement.ActionSlotId = newId;
            }
        }
        internal static void RemoveUnused(CharacterTestTacticalAuthoringSource source)
        {
            var events = source.Timelines.SelectMany(t => t.Events).ToArray();
            source.Slots.RemoveAll(slot => !events.Any(e => e.Operation.SlotId == slot.SlotId || e.Operation.Movement?.ActionSlotId == slot.SlotId));
        }
    }
}
