using System;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public readonly struct ConfigPatchEntry<T> where T : IConfigData
    {
        public ConfigPatchEntry(
            ConfigPatchOperation operation,
            ConfigLayerKind layer,
            T row,
            int id = 0,
            string sourceId = "")
        {
            Operation = operation;
            Layer = layer;
            Row = row;
            Id = id > 0 ? id : ResolveId(operation, row);
            SourceId = sourceId ?? string.Empty;
        }

        public ConfigPatchOperation Operation { get; }
        public ConfigLayerKind Layer { get; }
        public T Row { get; }
        public int Id { get; }
        public string SourceId { get; }

        public static ConfigPatchEntry<T> Upsert(T row, ConfigLayerKind layer = ConfigLayerKind.Patch, string sourceId = "")
        {
            return new ConfigPatchEntry<T>(ConfigPatchOperation.Upsert, layer, row, sourceId: sourceId);
        }

        public static ConfigPatchEntry<T> Remove(int id, ConfigLayerKind layer = ConfigLayerKind.Patch, string sourceId = "")
        {
            return new ConfigPatchEntry<T>(ConfigPatchOperation.Remove, layer, default, id, sourceId);
        }

        private static int ResolveId(ConfigPatchOperation operation, T row)
        {
            if (operation == ConfigPatchOperation.Upsert)
            {
                if (row == null)
                    throw new ArgumentNullException(nameof(row));

                return row.Id;
            }

            return row != null ? row.Id : 0;
        }
    }
}
