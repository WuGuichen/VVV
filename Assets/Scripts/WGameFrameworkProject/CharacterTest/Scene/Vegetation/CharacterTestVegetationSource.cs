using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Reads one data asset through its scene binding; derived resources never own records.</summary>
    internal sealed class CharacterTestVegetationSource
    {
        internal readonly CharacterTestVegetationData Data;
        internal readonly MxScenePresentationBinding Binding;
        internal readonly UnityEngine.Object BindingOwner;
        private CharacterTestVegetationRenderer _renderer;
        private int _rendererRevision = -1;
        private readonly Scene _scene;
        internal CharacterTestVegetationSource(CharacterTestVegetationData data, MxScenePresentationBinding binding, Scene scene, UnityEngine.Object bindingOwner)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            _scene = scene; BindingOwner = bindingOwner;
        }
        internal UnityEngine.Object UndoTarget => Data;
        internal Transform transform => Binding.Anchor;
        internal GameObject gameObject => transform.gameObject;
        internal Scene Scene => _scene;
        internal bool isActiveAndEnabled => _scene.IsValid() && _scene.isLoaded && Data != null &&
            Binding.Anchor != null && Binding.Enabled && Binding.Anchor.gameObject.activeInHierarchy;
        internal IReadOnlyList<CharacterTestVegetationInstance> Instances => Data.Instances;
        internal IReadOnlyList<CharacterTestVegetationType> Types => Data.Types;
        internal List<CharacterTestVegetationInstance> EditableInstances => Data.EditableInstances;
        internal List<CharacterTestVegetationType> EditableTypes => Data.EditableTypes;
        internal int SchemaVersion => Data.SchemaVersion;
        internal int AllocateId() => Data.AllocateId();
        internal int BeginStrokeSeed(int seed) => Data.BeginStrokeSeed(seed);
        internal int StrokeOrdinal => Data.StrokeOrdinal;
        internal float ChunkSize => Data.ChunkSize;
        internal int DrawBatchSize => Data.DrawBatchSize;
        internal CharacterTestVegetationClassificationMode ClassificationMode => Data.ClassificationMode;
        internal void SetDrawBatchSize(int value) => Data.SetDrawBatchSize(value);
        internal Transform SurfaceRoot { get => Binding.SurfaceRoot; set => Binding.SurfaceRoot = value; }
        internal bool TryValidate(out string issue) => Data.TryValidate(out issue);
        internal void InvalidatePresentation() { Data.InvalidatePresentation(); _renderer?.Invalidate(); }
        internal CharacterTestVegetationRenderer Renderer
        {
            get
            {
                if (_renderer == null) _renderer = new CharacterTestVegetationRenderer(this);
                if (_rendererRevision != Data.Revision) { _rendererRevision = Data.Revision; _renderer.Invalidate(); }
                return _renderer;
            }
        }
    }

    internal sealed class CharacterTestVegetationConsumer : IMxScenePresentationConsumer
    {
        private readonly CharacterTestVegetationSource _source;
        private int _revision = -1;
        private bool _disposed, _active;
        internal CharacterTestVegetationConsumer(CharacterTestVegetationSource source) { _source = source; }
        public void Tick()
        {
            if (_disposed) return;
            if (!_source.isActiveAndEnabled) { if (_active) _source.Renderer.Disable(); _active = false; return; }
            if (!_active) { _active = true; _source.Renderer.Invalidate(); }
            if (_revision != _source.Data.Revision) { _revision = _source.Data.Revision; _source.Renderer.Invalidate(); }
        }
        public void BeginCamera(UnityEngine.Camera camera) { if (!_disposed && _active) _source.Renderer.Submit(camera); }
        public void EndCamera(UnityEngine.Camera camera) { if (!_disposed) _source.Renderer.EndCamera(camera); }
        public void Dispose() { if (_disposed) return; _disposed = true; _source.Renderer.Disable(); }
    }
}