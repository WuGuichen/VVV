using System;

namespace MxFramework.Items
{
    public readonly struct InventoryInsertionPlan
    {
        public InventoryInsertionPlan(int requestedCount, int mergeCapacity, int emptySlotCapacity)
        {
            RequestedCount = requestedCount < 0 ? 0 : requestedCount;
            MergeCapacity = mergeCapacity < 0 ? 0 : mergeCapacity;
            EmptySlotCapacity = emptySlotCapacity < 0 ? 0 : emptySlotCapacity;
        }

        public int RequestedCount { get; }
        public int MergeCapacity { get; }
        public int EmptySlotCapacity { get; }
        public int TotalCapacity => (int)Math.Min(int.MaxValue, (long)MergeCapacity + EmptySlotCapacity);
        public bool CanFit => RequestedCount > 0 && TotalCapacity >= RequestedCount;
    }

    /// <summary>Plans atomic stack insertion into a fixed-slot inventory container.</summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    public static class InventoryInsertionPlanner
    {
        public static InventoryInsertionPlan CreatePlan(
            InventoryContainer container,
            ItemDefinition definition,
            ItemStack stack)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            long mergeCapacity = 0;
            long emptySlotCapacity = 0;
            for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
            {
                ItemStack current = container.GetStack(slotIndex);
                if (current.IsEmpty)
                {
                    emptySlotCapacity += definition.MaxStackCount;
                    continue;
                }

                if (CanMerge(current, stack))
                {
                    int available = definition.MaxStackCount - current.Count;
                    if (available > 0)
                        mergeCapacity += available;
                }
            }

            // Requests use positive Int32 counts. Saturating the reported capacity preserves
            // exact CanFit decisions without changing the public plan's field types.
            return new InventoryInsertionPlan(stack.Count,
                (int)Math.Min(int.MaxValue, mergeCapacity),
                (int)Math.Min(int.MaxValue, emptySlotCapacity));
        }

        private static bool CanMerge(ItemStack left, ItemStack right)
        {
            return left.CanMergeWith(right);
        }
    }
}
