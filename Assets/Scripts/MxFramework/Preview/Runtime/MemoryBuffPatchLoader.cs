using System.Collections.Generic;

namespace MxFramework.Preview
{
    /// <summary>
    /// Default in-memory loader. Parses the loadPatch payload's "patches" array and tracks
    /// the patch ids declared inside. Does NOT push entries into a real ConfigRegistry yet
    /// because ConfigBuffFactory hot-reload is not wired in this Phase 4 framework slice.
    /// TODO: replace with a real registry-backed loader when ConfigBuffFactory exposes a Patch API.
    /// </summary>
    public sealed class MemoryBuffPatchLoader : IBuffPatchLoader
    {
        private readonly List<string> _loaded = new List<string>();
        private string _lastSource;

        public IReadOnlyList<string> LoadedPatchIds => _loaded;
        public string LastSource => _lastSource;

        public IReadOnlyList<string> LoadPatch(string sourceJson)
        {
            _loaded.Clear();
            _lastSource = sourceJson ?? string.Empty;

            if (string.IsNullOrEmpty(sourceJson)) return _loaded;

            JsonValue root = PreviewJson.Parse(sourceJson);
            if (root == null || root.Kind != JsonKind.Object) return _loaded;

            string packageId = root.GetString("packageId");
            if (!string.IsNullOrEmpty(packageId)) _loaded.Add(packageId);

            JsonValue patches = root.GetField("patches");
            if (patches != null && patches.Kind == JsonKind.Array)
            {
                for (int i = 0; i < patches.Array.Count; i++)
                {
                    JsonValue p = patches.Array[i];
                    if (p == null || p.Kind != JsonKind.Object) continue;
                    string id = p.GetString("id") ?? p.GetString("patchId");
                    if (!string.IsNullOrEmpty(id) && !_loaded.Contains(id))
                        _loaded.Add(id);
                }
            }

            return _loaded;
        }

        public void Clear()
        {
            _loaded.Clear();
            _lastSource = null;
        }
    }
}
