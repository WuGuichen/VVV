using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum PackageKind
    {
        Preview,
        Mod
    }

    public enum PatchOperation
    {
        Upsert,
        Remove
    }

    public enum FieldValueKind
    {
        Scalar,
        List,
        Map
    }

    public sealed class FieldValue
    {
        public FieldValueKind Kind { get; set; } = FieldValueKind.Scalar;
        public string Scalar { get; set; } = string.Empty;
        public List<FieldValue> List { get; set; }
        public Dictionary<string, FieldValue> Map { get; set; }

        public static FieldValue FromScalar(string value)
        {
            return new FieldValue { Kind = FieldValueKind.Scalar, Scalar = value ?? string.Empty };
        }

        public static FieldValue FromList(IEnumerable<FieldValue> items)
        {
            var fv = new FieldValue { Kind = FieldValueKind.List, List = new List<FieldValue>() };
            if (items != null)
            {
                foreach (var item in items)
                    fv.List.Add(item);
            }
            return fv;
        }

        public static FieldValue FromMap(IDictionary<string, FieldValue> map)
        {
            var fv = new FieldValue { Kind = FieldValueKind.Map, Map = new Dictionary<string, FieldValue>() };
            if (map != null)
            {
                foreach (var kv in map)
                    fv.Map[kv.Key] = kv.Value;
            }
            return fv;
        }

        public FieldValue Clone()
        {
            var copy = new FieldValue { Kind = Kind, Scalar = Scalar };
            if (Kind == FieldValueKind.List && List != null)
            {
                copy.List = new List<FieldValue>(List.Count);
                for (int i = 0; i < List.Count; i++)
                    copy.List.Add(List[i] != null ? List[i].Clone() : null);
            }
            else if (Kind == FieldValueKind.Map && Map != null)
            {
                copy.Map = new Dictionary<string, FieldValue>();
                foreach (var kv in Map)
                    copy.Map[kv.Key] = kv.Value != null ? kv.Value.Clone() : null;
            }
            return copy;
        }

        public override string ToString()
        {
            return Kind == FieldValueKind.Scalar ? (Scalar ?? string.Empty) : Kind.ToString();
        }

        public static implicit operator FieldValue(string value)
        {
            return FromScalar(value);
        }

        public static implicit operator string(FieldValue value)
        {
            if (value == null) return string.Empty;
            return value.Kind == FieldValueKind.Scalar ? (value.Scalar ?? string.Empty) : string.Empty;
        }
    }

    public static class FieldValueExtensions
    {
        public static string GetScalar(this IDictionary<string, FieldValue> fields, string key)
        {
            if (fields == null || string.IsNullOrEmpty(key)) return string.Empty;
            if (!fields.TryGetValue(key, out FieldValue value) || value == null) return string.Empty;
            return value.Kind == FieldValueKind.Scalar ? (value.Scalar ?? string.Empty) : string.Empty;
        }

        public static bool ContainsScalar(this IDictionary<string, FieldValue> fields, string key)
        {
            if (fields == null || string.IsNullOrEmpty(key)) return false;
            if (!fields.TryGetValue(key, out FieldValue value) || value == null) return false;
            if (value.Kind != FieldValueKind.Scalar) return false;
            return !string.IsNullOrWhiteSpace(value.Scalar);
        }
    }

    public sealed class ModPackageManifest
    {
        public string PackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Version { get; set; } = "0.1.0";
        public string SchemaVersion { get; set; } = "1.0";
        public string GameVersionRange { get; set; } = "*";
        public PackageKind Kind { get; set; } = PackageKind.Mod;
        public string RuntimePatch { get; set; } = string.Empty;
    }

    public sealed class PatchEntry
    {
        public PatchOperation Operation { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Layer { get; set; } = "Mod";
        public Dictionary<string, FieldValue> Fields { get; set; } = new Dictionary<string, FieldValue>();
    }

    public sealed class PatchDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Source { get; set; } = string.Empty;
        public List<PatchEntry> Entries { get; set; } = new List<PatchEntry>();
    }

    public sealed class MergedRecord
    {
        public string Source { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string ChangeKind { get; set; } = string.Empty;
        public string OriginLayer { get; set; } = string.Empty;
        public Dictionary<string, FieldValue> Fields { get; set; } = new Dictionary<string, FieldValue>();
        public Dictionary<string, string> FieldOrigins { get; set; } = new Dictionary<string, string>();
    }

    public sealed class MergePreview
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<MergedRecord> Records { get; set; } = new List<MergedRecord>();
    }

    public static class PatchMerger
    {
        public static MergePreview Merge(PatchDocument patch)
        {
            var preview = new MergePreview { SchemaVersion = patch != null ? patch.SchemaVersion : "1.0" };
            if (patch == null)
                return preview;

            var records = new Dictionary<string, MergedRecord>();
            for (int i = 0; i < patch.Entries.Count; i++)
            {
                PatchEntry entry = patch.Entries[i];
                string key = entry.Source + ":" + entry.Id;
                if (entry.Operation == PatchOperation.Remove)
                {
                    records[key] = new MergedRecord
                    {
                        Source = entry.Source,
                        Id = entry.Id,
                        ChangeKind = "Removed",
                        OriginLayer = entry.Layer
                    };
                    continue;
                }

                MergedRecord record;
                if (records.TryGetValue(key, out MergedRecord existing) && existing.ChangeKind != "Removed")
                {
                    record = existing;
                    record.ChangeKind = "Replaced";
                }
                else
                {
                    record = new MergedRecord
                    {
                        Source = entry.Source,
                        Id = entry.Id,
                        ChangeKind = "Added",
                        OriginLayer = entry.Layer
                    };
                }

                foreach (KeyValuePair<string, FieldValue> field in entry.Fields)
                {
                    record.Fields[field.Key] = field.Value != null ? field.Value.Clone() : FieldValue.FromScalar(string.Empty);
                    record.FieldOrigins[field.Key] = entry.Layer;
                }

                record.OriginLayer = entry.Layer;
                records[key] = record;
            }

            foreach (MergedRecord record in records.Values)
                preview.Records.Add(record);
            return preview;
        }
    }

    public static class LayeredMerger
    {
        public static MergePreview Merge(IEnumerable<PatchDocument> baseLayers, IEnumerable<PatchDocument> patches, IEnumerable<PatchDocument> mods)
        {
            var records = new Dictionary<string, MergedRecord>();
            ApplyLayer(records, baseLayers, "Base");
            ApplyLayer(records, patches, "Patch");
            ApplyLayer(records, mods, "Mod");

            var preview = new MergePreview();
            foreach (MergedRecord record in records.Values)
                preview.Records.Add(record);
            return preview;
        }

        private static void ApplyLayer(Dictionary<string, MergedRecord> records, IEnumerable<PatchDocument> documents, string defaultLayer)
        {
            if (documents == null) return;
            foreach (PatchDocument doc in documents)
            {
                if (doc == null) continue;
                for (int i = 0; i < doc.Entries.Count; i++)
                {
                    PatchEntry entry = doc.Entries[i];
                    string layer = string.IsNullOrEmpty(entry.Layer) ? defaultLayer : entry.Layer;
                    string key = entry.Source + ":" + entry.Id;

                    if (entry.Operation == PatchOperation.Remove)
                    {
                        records.Remove(key);
                        records[key] = new MergedRecord
                        {
                            Source = entry.Source,
                            Id = entry.Id,
                            ChangeKind = "Removed",
                            OriginLayer = layer
                        };
                        continue;
                    }

                    MergedRecord record;
                    if (!records.TryGetValue(key, out record) || record.ChangeKind == "Removed")
                    {
                        record = new MergedRecord
                        {
                            Source = entry.Source,
                            Id = entry.Id,
                            ChangeKind = "Added",
                            OriginLayer = layer
                        };
                        records[key] = record;
                    }
                    else
                    {
                        record.ChangeKind = "Replaced";
                    }

                    foreach (KeyValuePair<string, FieldValue> field in entry.Fields)
                    {
                        record.Fields[field.Key] = field.Value != null ? field.Value.Clone() : FieldValue.FromScalar(string.Empty);
                        record.FieldOrigins[field.Key] = layer;
                    }
                    record.OriginLayer = layer;
                }
            }
        }
    }
}
