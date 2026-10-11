using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Single persisted vegetation instance authority; all derived runtime state belongs to the rendering session.</summary>
    [CreateAssetMenu(menuName = "WGame/Environment/Vegetation Data")]
    public sealed class CharacterTestVegetationData : MxFramework.Rendering.MxScenePresentationData
    {
        [SerializeField] private int _schemaVersion = 1;
        [SerializeField] private float _chunkSize = 16f;
        [SerializeField, Min(1)] private int _drawBatchSize = 8192;
        [SerializeField] private CharacterTestVegetationClassificationMode _classificationMode;
        [SerializeField] private List<CharacterTestVegetationType> _types = new List<CharacterTestVegetationType>();
        [SerializeField] private List<CharacterTestVegetationInstance> _instances = new List<CharacterTestVegetationInstance>();
        [SerializeField] private int _nextInstanceId = 1;
        [SerializeField] private int _strokeOrdinal;
        
        [NonSerialized] private int _revision;
        internal int Revision => _revision;
        internal int StrokeOrdinal => _strokeOrdinal;
        internal int NextInstanceId => _nextInstanceId;

        /// <summary>Read-only scene instance authority. NoAlloc.</summary>
        public IReadOnlyList<CharacterTestVegetationInstance> Instances => _instances;
        /// <summary>Shared presentation bindings. NoAlloc.</summary>
        public IReadOnlyList<CharacterTestVegetationType> Types => _types;
        public float ChunkSize => Mathf.Max(1f, _chunkSize);
        /// <summary>Maximum instances per indirect draw, independently of the spatial cell size.</summary>
        public int DrawBatchSize => _drawBatchSize > 0 ? _drawBatchSize : 8192;
        public CharacterTestVegetationClassificationMode ClassificationMode => _classificationMode;
        /// <summary>Explicit comparison/compatibility switch; does not rebuild or mutate instance data.</summary>
        internal void SetClassificationMode(CharacterTestVegetationClassificationMode mode) { _classificationMode = mode; InvalidatePresentation(); }
        internal void SetDrawBatchSize(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _drawBatchSize = value;
            InvalidatePresentation();
        }
        
        public int SchemaVersion => _schemaVersion;
        internal List<CharacterTestVegetationInstance> EditableInstances => _instances;
        internal List<CharacterTestVegetationType> EditableTypes => _types;
        internal int AllocateId() => _nextInstanceId++;
        internal int BeginStrokeSeed(int seed) => unchecked(seed * 397 ^ _strokeOrdinal++);
        internal void InvalidatePresentation() => _revision++;
        private void OnValidate() { _chunkSize = Mathf.Max(1f, _chunkSize); if (_drawBatchSize <= 0) _drawBatchSize = 8192; _revision++; }
        public override MxFramework.Rendering.IMxScenePresentationConsumer CreateConsumer(
            MxFramework.Rendering.MxScenePresentationBinding binding, UnityEngine.SceneManagement.Scene scene, bool editorPreview)
        {
            if (editorPreview) return null; // Dedicated editor preview owns its independent derived rendering cache.
            if (binding.Anchor == null || binding.Anchor.gameObject.scene != scene)
                throw new InvalidOperationException("Vegetation requires an anchor in its owning scene.");
            if (!TryValidate(out string issue)) throw new InvalidOperationException(issue);
            return new CharacterTestVegetationConsumer(new CharacterTestVegetationSource(this, binding, scene, null));
        }
        internal bool TryValidate(out string issue)
        {
            if (_schemaVersion != 1) { issue = "植被数据版本不受支持。"; return false; }
            var typeIds = new HashSet<int>();
            foreach (var type in _types)
            {
                if (type == null || type.Id <= 0 || !typeIds.Add(type.Id) || type.Prefab == null ||
                    type.Prefab.GetComponent<MeshFilter>()?.sharedMesh == null ||
                    type.Prefab.GetComponent<MeshRenderer>()?.sharedMaterial == null)
                { issue = "植被类型需要唯一 ID 和有效草 Prefab。"; return false; }
                if (!(type.DrawDistance > 0f) || !float.IsFinite(type.DrawDistance))
                { issue = "植被显示距离需要有限正数。"; return false; }
                if (!float.IsFinite(type.Lod0ScreenHeight) || !float.IsFinite(type.Lod1ScreenHeight) ||
                    type.Lod0ScreenHeight <= 0 || type.Lod1ScreenHeight <= 0 || type.Lod1ScreenHeight >= type.Lod0ScreenHeight ||
                    !float.IsFinite(type.LodHysteresis) || type.LodHysteresis < 0 || type.LodHysteresis >= .5f)
                { issue = "LOD 阈值须为有限正数且 LOD0 大于 LOD1，滞回须在 0 到 0.5 之间。"; return false; }
            }
            var ids = new HashSet<int>();
            foreach (var item in _instances)
            {
                if (item.Id <= 0 || !ids.Add(item.Id) || !typeIds.Contains(item.TypeId) ||
                    !Finite(item.Position) || !Finite(item.Scale) ||
                    item.Scale.x <= 0f || item.Scale.y <= 0f || item.Scale.z <= 0f ||
                    !float.IsFinite(item.Rotation.x) || !float.IsFinite(item.Rotation.y) ||
                    !float.IsFinite(item.Rotation.z) || !float.IsFinite(item.Rotation.w) ||
                    Quaternion.Dot(item.Rotation, item.Rotation) < 0.0001f)
                { issue = "植被实例有重复 ID、缺失类型或非法变换。"; return false; }
            }
            issue = string.Empty; return true;
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
