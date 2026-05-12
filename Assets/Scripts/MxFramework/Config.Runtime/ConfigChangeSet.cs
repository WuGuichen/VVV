using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public readonly struct ConfigRowChange
    {
        public ConfigRowChange(
            Type configType,
            int id,
            ConfigLayerKind layer,
            ConfigPatchOperation operation,
            ConfigMergeChangeKind changeKind,
            string sourceId)
        {
            ConfigType = configType;
            Id = id;
            Layer = layer;
            Operation = operation;
            ChangeKind = changeKind;
            SourceId = sourceId ?? string.Empty;
        }

        public Type ConfigType { get; }
        public int Id { get; }
        public ConfigLayerKind Layer { get; }
        public ConfigPatchOperation Operation { get; }
        public ConfigMergeChangeKind ChangeKind { get; }
        public string SourceId { get; }
    }

    public sealed class ConfigChangeSet
    {
        private readonly List<ConfigRowChange> _changes = new List<ConfigRowChange>();

        public IReadOnlyList<ConfigRowChange> Changes => _changes;
        public int Count => _changes.Count;

        public void Add(ConfigRowChange change)
        {
            _changes.Add(change);
        }

        public string ToReportText()
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Config ChangeSet\n");
            if (_changes.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < _changes.Count; i++)
            {
                ConfigRowChange change = _changes[i];
                builder.Append("- type=")
                    .Append(change.ConfigType != null ? change.ConfigType.Name : string.Empty)
                    .Append(" id=").Append(change.Id)
                    .Append(" layer=").Append(change.Layer)
                    .Append(" op=").Append(change.Operation)
                    .Append(" change=").Append(change.ChangeKind);
                if (!string.IsNullOrEmpty(change.SourceId))
                    builder.Append(" source=").Append(change.SourceId);
                builder.Append('\n');
            }

            return builder.ToString();
        }
    }

    public sealed class ConfigPatchMergeResult<T> where T : IConfigData
    {
        public ConfigPatchMergeResult(ConfigTable<T> table, ConfigChangeSet changeSet)
        {
            Table = table;
            ChangeSet = changeSet ?? new ConfigChangeSet();
        }

        public ConfigTable<T> Table { get; }
        public ConfigChangeSet ChangeSet { get; }
    }
}
