using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Core.Pooling;

namespace MxFramework.Modifiers
{
    /// <summary>
    /// Generic execution context extracted from WGame EntryApplyData.
    /// </summary>
    public sealed class ModifierContext : IReference
    {
        private static readonly ReferencePool<ModifierContext> Pool = new ReferencePool<ModifierContext>();

        public IAttributeOwner Target { get; set; }
        public IBuffPipeline Buffs { get; set; }
        public ICounterStore Counters { get; set; }
        public int[] Parameters { get; set; }
        public int CompareId { get; set; }
        public int CompareValue1 { get; set; }
        public int CompareValue2 { get; set; }
        public object Source { get; set; }
        public Dictionary<string, object> Extra { get; set; }

        public static ModifierContext Get()
        {
            return Pool.Get();
        }

        public static void Push(ModifierContext context)
        {
            if (context == null)
                return;

            Pool.Release(context);
        }

        public void Clear()
        {
            Target = null;
            Buffs = null;
            Counters = null;
            Parameters = null;
            CompareId = 0;
            CompareValue1 = 0;
            CompareValue2 = 0;
            Source = null;
            if (Extra != null)
                Extra.Clear();
        }

        public void EnsureExtra()
        {
            if (Extra == null)
                Extra = new Dictionary<string, object>();
        }
    }
}
