using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MxFramework.Resources;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>LowFreqAlloc: keeps resource handles for visible character rows until their view is released.</summary>
    internal sealed class CharacterTestFairyGuiCharacterImageBinding : IDisposable
    {
        private sealed class Entry
        {
            internal IResourceOperation<ResourceHandle<Texture2D>> Operation;
            internal ResourceHandle<Texture2D> Handle;
            internal Fgui.NTexture Texture;
        }

        private readonly IResourceManager _resources;
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<Fgui.GLoader, string> _bindings = new();
        private bool _disposed;
        private Fgui.NTexture _fallback;

        internal CharacterTestFairyGuiCharacterImageBinding(IResourceManager resources)
        {
            _resources = resources;
            var texture = new Texture2D(40, 40, TextureFormat.RGBA32, false) { name = "Character portrait fallback", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[1600];
            for (int y = 0; y < 40; y++) for (int x = 0; x < 40; x++)
            {
                bool head = (x - 20) * (x - 20) + (y - 27) * (y - 27) < 36;
                bool body = y > 5 && y < 20 && Mathf.Abs(x - 20) < 12;
                pixels[y * 40 + x] = head || body ? new Color32(104, 157, 171, 255) : new Color32(25, 42, 53, 255);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            _fallback = new Fgui.NTexture(texture) { destroyMethod = Fgui.DestroyMethod.Destroy };
        }

        internal void Bind(Fgui.GButton row, string key)
        {
            Fgui.GLoader icon = row?.GetChild("icon")?.asLoader;
            if (icon == null) return;
            icon.SetXY(12, 8); icon.SetSize(40, 40);
            Fgui.GTextField title = row.GetChild("title")?.asTextField;
            if (title != null) { title.SetXY(60, 0); title.SetSize(Mathf.Max(1, row.width - 72), row.height); title.align = Fgui.AlignType.Left; }
            row.tooltips = "拖到战场放置 " + row.title + " · 头像未配置/不可用时显示占位";
            Bind(icon, key);
        }

        internal void Bind(Fgui.GLoader icon, string key)
        {
            if (icon == null || icon.isDisposed || _disposed) return;
            _bindings[icon] = key ?? string.Empty;
            icon.color = Color.white; icon.fill = Fgui.FillType.Scale;
            icon.url = null; icon.texture = _fallback; icon.visible = true;
            // Keep the silhouette and name visible while loading, on missing config, and on failure.
            if (_disposed || _resources == null || string.IsNullOrWhiteSpace(key)) return;
            if (!_entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry(); _entries.Add(key, entry);
                try { entry.Operation = _resources.LoadAsync<Texture2D>(new ResourceKey(key, "Texture2D")); }
                catch (Exception) { return; }
                Complete(entry, key);
            }
            Apply(icon, key, entry);
        }

        private async void Complete(Entry entry, string key)
        {
            try
            {
                while (!_disposed && !entry.Operation.IsDone) await Task.Yield();
                if (_disposed) return;
                ResourceLoadResult<ResourceHandle<Texture2D>> result = entry.Operation.Result;
                if (!result.Success || result.Value?.Value == null) return;
                entry.Handle = result.Value;
                entry.Texture = new Fgui.NTexture(entry.Handle.Value) { destroyMethod = Fgui.DestroyMethod.None };
                foreach (var binding in _bindings)
                    if (binding.Value == key) Apply(binding.Key, key, entry);
            }
            catch (Exception) { /* The silhouette and title remain visible. */ }
        }

        private void Apply(Fgui.GLoader icon, string key, Entry entry)
        {
            if (_disposed || icon.isDisposed || entry.Texture == null || !_bindings.TryGetValue(icon, out string current) || current != key) return;
            icon.texture = entry.Texture; icon.visible = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var binding in _bindings) if (!binding.Key.isDisposed) binding.Key.texture = null;
            _bindings.Clear();
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Operation != null && !entry.Operation.IsDone) entry.Operation.Cancel();
                ResourceHandle<Texture2D> handle = entry.Handle;
                if (handle == null && entry.Operation?.IsDone == true && entry.Operation.Result.Success) handle = entry.Operation.Result.Value;
                entry.Texture?.Dispose();
                if (handle != null) _resources?.Release(handle);
            }
            _entries.Clear();
            _fallback?.Dispose(); _fallback = null;
        }
    }
}
