using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Transient presentation shared by point selection and validation fixtures. Owns only lines and materials.</summary>
    /// <remarks>LowFreqAlloc on construction/disposal. Show reuses the position buffer.</remarks>
    internal sealed class TacticalPointSelectionPreview : IDisposable
    {
        private const int Segments = 64;
        private readonly Vector3[] _positions = new Vector3[Segments];
        private GameObject _root;
        private readonly LineRenderer _range, _candidate, _direction;
        private readonly Vector3[] _arrow = new Vector3[5];
        private readonly Material _rangeMaterial, _candidateMaterial, _directionMaterial;
        internal bool IsAvailable => _root != null;
        internal TacticalPointSelectionPreview() : this(null) { }
        internal TacticalPointSelectionPreview(Transform owner)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            _root = new GameObject("TacticalPointSelection") { hideFlags = HideFlags.DontSave };
            if (owner != null) _root.transform.SetParent(owner, false);
            _rangeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _candidateMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            SetColor(_rangeMaterial, new Color(0.2f, 0.85f, 1f));
            _range = CreateLine("Range", _rangeMaterial);
            _candidate = CreateLine("Destination", _candidateMaterial);
            _directionMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            SetColor(_directionMaterial, new Color(1f, 0.85f, 0.2f));
            _direction = CreateLine("InitialMovementDirection", _directionMaterial);
            _direction.loop = false; _direction.positionCount = 5; _direction.enabled = false;
        }
        internal void Show(TacticalWorldPoint origin, int radiusMillimeters, bool hasCandidate,
            TacticalWorldPoint candidate, int arrivalMillimeters, bool valid)
        {
            if (_root == null) return;
            _root.SetActive(true);
            _direction.enabled = false; _range.enabled = true;
            Circle(_range, origin, radiusMillimeters / 1000f);
            _candidate.enabled = hasCandidate;
            if (!hasCandidate) return;
            Circle(_candidate, candidate, arrivalMillimeters / 1000f);
            SetColor(_candidateMaterial, valid ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.2f, 0.15f));
        }
        // Presentation dimensions only. This arrow is the current initial velocity direction, never a path guarantee.
        internal void ShowDirection(TacticalWorldPoint origin, Vector2 direction)
        {
            if (_root == null || !TryBuildDirectionArrow(origin, direction, _arrow)) { Hide(); return; }
            _root.SetActive(true); _range.enabled = false; _candidate.enabled = false; _direction.enabled = true;
            _direction.SetPositions(_arrow);
        }
        internal static bool TryBuildDirectionArrow(TacticalWorldPoint origin, Vector2 direction, Vector3[] positions)
        {
            if (positions == null || positions.Length != 5 || direction.sqrMagnitude <= 0.000001f
                || float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsInfinity(direction.x) || float.IsInfinity(direction.y)) return false;
            var forward = new Vector3(direction.x, 0, direction.y).normalized;
            var right = new Vector3(forward.z, 0, -forward.x);
            var start = new Vector3(origin.X / 1000f, origin.Y / 1000f + 0.06f, origin.Z / 1000f);
            var tip = start + forward * 0.9f;
            positions[0] = start; positions[1] = tip; positions[2] = tip - forward * 0.2f + right * 0.14f;
            positions[3] = tip; positions[4] = tip - forward * 0.2f - right * 0.14f;
            return true;
        }
        internal void Hide() { if (_root != null) _root.SetActive(false); }
        private LineRenderer CreateLine(string name, Material material)
        {
            var child = new GameObject(name) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(_root.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.loop = true;
            line.positionCount = Segments; line.widthMultiplier = 0.04f;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        private void Circle(LineRenderer line, TacticalWorldPoint point, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * (2f * Mathf.PI / Segments);
                _positions[i] = new Vector3(point.X / 1000f + Mathf.Cos(angle) * radius,
                    point.Y / 1000f + 0.06f, point.Z / 1000f + Mathf.Sin(angle) * radius);
            }
            line.SetPositions(_positions);
        }
        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
        public void Dispose()
        {
            Hide();
            Destroy(_root); Destroy(_rangeMaterial); Destroy(_candidateMaterial); Destroy(_directionMaterial); _root = null;
        }
    }
}
