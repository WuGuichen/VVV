using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public static class RuntimeConfigPatchMerger
    {
        public static ConfigPatchMergeResult<T> Merge<T>(
            ConfigSchema schema,
            IEnumerable<T> baseRows,
            IEnumerable<ConfigPatchEntry<T>> patches) where T : IConfigData
        {
            var rows = new Dictionary<int, T>();
            if (baseRows != null)
            {
                foreach (T row in baseRows)
                {
                    if (row != null && row.Id > 0)
                        rows[row.Id] = row;
                }
            }

            var changeSet = new ConfigChangeSet();
            if (patches != null)
            {
                foreach (ConfigPatchEntry<T> patch in patches)
                    ApplyPatch(rows, patch, changeSet);
            }

            var table = new ConfigTable<T>(schema, ConfigDuplicatePolicy.Replace);
            foreach (T row in rows.Values)
                table.Add(row);

            return new ConfigPatchMergeResult<T>(table, changeSet);
        }

        private static void ApplyPatch<T>(
            Dictionary<int, T> rows,
            ConfigPatchEntry<T> patch,
            ConfigChangeSet changeSet) where T : IConfigData
        {
            ConfigMergeChangeKind changeKind;
            switch (patch.Operation)
            {
                case ConfigPatchOperation.Remove:
                    changeKind = rows.Remove(patch.Id) ? ConfigMergeChangeKind.Removed : ConfigMergeChangeKind.Noop;
                    break;
                default:
                    changeKind = rows.ContainsKey(patch.Id) ? ConfigMergeChangeKind.Replaced : ConfigMergeChangeKind.Added;
                    rows[patch.Id] = patch.Row;
                    break;
            }

            changeSet.Add(new ConfigRowChange(
                typeof(T),
                patch.Id,
                patch.Layer,
                patch.Operation,
                changeKind,
                patch.SourceId));
        }
    }
}
