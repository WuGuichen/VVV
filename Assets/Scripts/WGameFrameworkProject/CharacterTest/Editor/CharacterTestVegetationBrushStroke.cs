using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [Serializable]
    internal sealed class CharacterTestVegetationBrushSettings
    {
        public float Radius = 2f;
        public float Density = 1f;
        public float MinimumSpacing = 0.55f;
        public float MaximumSlope = 40f;
        public float MinimumScale = 0.72f;
        public float MaximumScale = 1f;
        public int Seed = 12345;
        internal void Sanitize()
        {
            Radius = FiniteClamp(Radius, 0.1f, 20f, 2f); Density = FiniteClamp(Density, 0.01f, 50f, 1f);
            MinimumSpacing = FiniteClamp(MinimumSpacing, 0.05f, 10f, 0.55f); MaximumSlope = FiniteClamp(MaximumSlope, 0, 89, 40f);
            MinimumScale = FiniteClamp(MinimumScale, 0.05f, 10f, 0.72f); MaximumScale = FiniteClamp(MaximumScale, MinimumScale, 10f, 1f);
        }
        private static float FiniteClamp(float value, float min, float max, float fallback) => Mathf.Clamp(float.IsFinite(value) ? value : fallback, min, max);
    }

    /// <summary>One user gesture, one Undo group. Samples and authority are independent of preview objects.</summary>
    internal sealed class CharacterTestVegetationBrushStroke : IDisposable
    {
        private readonly CharacterTestVegetationSource _owner;
        private readonly CharacterTestVegetationBrushSettings _settings;
        private readonly IVegetationSurfaceProvider _surface;
        private readonly int _typeId;
        private readonly Dictionary<Vector3Int, List<Vector3>> _occupied = new Dictionary<Vector3Int, List<Vector3>>();
        private System.Random _random;
        private int _undoGroup = -1;
        private bool _seedCommitted;
        private bool _completed;
        internal int ChangedCount { get; private set; }

        internal CharacterTestVegetationBrushStroke(CharacterTestVegetationSource owner,
            CharacterTestVegetationBrushSettings settings, IVegetationSurfaceProvider surface, int typeId = 1)
        {
            _owner = owner; _settings = settings; _surface = surface; _typeId = typeId; settings.Sanitize();
            // Seed is consumed only when a valid candidate actually changes the scene.
            foreach (var item in owner.Instances) AddOccupied(owner.transform.TransformPoint(item.Position));
        }

        internal int Paint(Vector3 center, bool single)
        {
            if (_completed) return 0;
            int before = ChangedCount;
            if (_random == null) _random = new System.Random(PeekSeed());
            int desired = single ? 1 : Mathf.Min(2048, Mathf.CeilToInt(Mathf.PI * _settings.Radius * _settings.Radius * _settings.Density));
            int attempts = single ? 1 : desired * 8;
            for (int i = 0, accepted = 0; i < attempts && accepted < desired; i++)
            {
                Vector3 candidate = center;
                if (!single)
                {
                    double angle = _random.NextDouble() * Math.PI * 2, radius = Math.Sqrt(_random.NextDouble()) * _settings.Radius;
                    candidate.x += (float)(Math.Cos(angle) * radius); candidate.z += (float)(Math.Sin(angle) * radius);
                }
                if (!_surface.TryProject(candidate, out var hit) || TooClose(hit.Position)) continue;
                BeginChange("刷放植被");
                if (!_seedCommitted) { _owner.BeginStrokeSeed(_settings.Seed); _seedCommitted = true; }
                float yaw = (float)_random.NextDouble() * 360f;
                float scale = Mathf.Lerp(_settings.MinimumScale, _settings.MaximumScale, (float)_random.NextDouble());
                _owner.EditableInstances.Add(new CharacterTestVegetationInstance
                {
                    Id = _owner.AllocateId(), TypeId = _typeId, Position = _owner.transform.InverseTransformPoint(hit.Position),
                    Rotation = Quaternion.Inverse(_owner.transform.rotation) * Quaternion.Euler(0, yaw, 0),
                    Scale = Vector3.one * scale, Seed = _random.Next()
                });
                AddOccupied(hit.Position); accepted++; ChangedCount++;
            }
            if (ChangedCount != before) Changed();
            return ChangedCount - before;
        }

        internal int Erase(Vector3 center)
        {
            if (_completed) return 0;
            int before = ChangedCount;
            var items = _owner.EditableInstances;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (!WithinBrush(_owner.transform.TransformPoint(items[i].Position), center, _settings.Radius)) continue;
                BeginChange("擦除植被"); items.RemoveAt(i); ChangedCount++;
            }
            if (ChangedCount != before) Changed();
            return ChangedCount - before;
        }

        internal void Complete(bool cancel = false)
        {
            if (_completed) return;
            _completed = true;
            if (_undoGroup < 0) return;
            Undo.FlushUndoRecordObjects();
            if (cancel) Undo.RevertAllDownToGroup(_undoGroup); else Undo.CollapseUndoOperations(_undoGroup);
            _owner.InvalidatePresentation(); SceneView.RepaintAll();
        }
        public void Dispose() { Complete(cancel: true); }

        private int PeekSeed()
        {
            var data = new SerializedObject(_owner.UndoTarget);
            return unchecked(_settings.Seed * 397 ^ data.FindProperty("_strokeOrdinal").intValue);
        }
        private void BeginChange(string label)
        {
            if (_undoGroup >= 0) return;
            Undo.IncrementCurrentGroup(); _undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(_owner.UndoTarget, label);
        }
        private void Changed()
        {
            EditorUtility.SetDirty(_owner.UndoTarget); EditorSceneManager.MarkSceneDirty(_owner.gameObject.scene);
            _owner.InvalidatePresentation();
        }
        private Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / _settings.MinimumSpacing),
            Mathf.FloorToInt(p.y / _settings.MinimumSpacing), Mathf.FloorToInt(p.z / _settings.MinimumSpacing));
        private void AddOccupied(Vector3 p)
        {
            var key = Cell(p); if (!_occupied.TryGetValue(key, out var values)) _occupied.Add(key, values = new List<Vector3>()); values.Add(p);
        }
        private bool TooClose(Vector3 p)
        {
            var cell = Cell(p); float limit = _settings.MinimumSpacing * _settings.MinimumSpacing;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                if (_occupied.TryGetValue(cell + new Vector3Int(x, y, z), out var values))
                    foreach (var q in values) if ((p - q).sqrMagnitude < limit) return true;
            return false;
        }
        internal static bool WithinBrush(Vector3 point, Vector3 center, float radius)
        {
            Vector3 delta = point - center;
            return delta.x * delta.x + delta.z * delta.z <= radius * radius && Mathf.Abs(delta.y) <= Mathf.Max(1f, radius);
        }
    }
}
