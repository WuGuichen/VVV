using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Presentation-only floor for the preparation view. It owns no collision or placement authority.</summary>
    [DisallowMultipleComponent]
    internal sealed class TacticalPreparationGroundView : MonoBehaviour
    {
        private TacticalCardSliceCoordinator _flow;
        private GameManager _manager;
        private GameObject _root;
        private GameObject _preparationSurfaces;
        private Transform _surfaceParent;
        private GameObject _markers;
        private GameObject _targetHighlight;
        private Material _teamAMaterial;
        private Material _teamBMaterial;
        private long _markerRevision = -1;
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Mesh> _meshes = new List<Mesh>();

        private void Awake()
        {
            _flow = GetComponent<TacticalCardSliceCoordinator>();
            _manager = GetComponent<GameManager>();
            Build();
        }

        private void Update()
        {
            if (_root != null) _root.SetActive(_flow != null && _flow.UsesBattlefieldOverviewPresentation);
            if (_preparationSurfaces != null)
                _preparationSurfaces.SetActive(_flow != null && _flow.State == TacticalPreparationState.Preparation);
            if (_markers != null) _markers.SetActive(_flow != null && _flow.State == TacticalPreparationState.Preparation);
            TacticalMixedPlayerCardRuntime mixed = _manager?.RuntimeContext?.Slice?.Services.Modules.MixedPlayerTacticalStage;
            CharacterTestCharacterEntity target = mixed?.CurrentTarget;
            if (_targetHighlight != null)
            {
                bool visible = _flow != null && _flow.State == TacticalPreparationState.Battle
                    && mixed != null && !mixed.PlayerDefeated && target != null && target.CombatState.IsAlive;
                _targetHighlight.SetActive(visible);
                if (visible)
                    _targetHighlight.transform.position = new Vector3(target.Transform.X, target.Transform.Y + 0.04f, target.Transform.Z);
            }
            TacticalBattlePreparationService preparation = CharacterTacticalServices.Preparation;
            if (_root != null && _root.activeSelf && preparation != null && _markerRevision != preparation.Revision)
            {
                _markerRevision = preparation.Revision;
                RebuildMarkers(preparation.Working);
            }
        }

        private void Build()
        {
            _root = new GameObject("Tactical Preparation Ground Visual");
            _root.transform.SetParent(transform, false);
            _preparationSurfaces = new GameObject("Preparation Surfaces");
            _preparationSurfaces.transform.SetParent(_root.transform, false);
            _surfaceParent = _preparationSurfaces.transform;
            // The coarse planning grid stays subordinate to the human-scale concrete joints.
            CreateSurface("Grid", Grid(TacticalBattlefieldLayout.Width, TacticalBattlefieldLayout.Depth, 8f, 0.006f),
                new Color32(83, 94, 100, 45), 0.022f);
            CreateSurface("North Boundary", Rectangle(TacticalBattlefieldLayout.Width, 0.075f),
                new Color32(70, 98, 104, 255), 0.026f, new Vector3(0f, 0f, TacticalBattlefieldLayout.HalfDepth));
            CreateSurface("South Boundary", Rectangle(TacticalBattlefieldLayout.Width, 0.075f),
                new Color32(70, 98, 104, 255), 0.026f, new Vector3(0f, 0f, -TacticalBattlefieldLayout.HalfDepth));
            CreateSurface("East Boundary", Rectangle(0.075f, TacticalBattlefieldLayout.Depth),
                new Color32(70, 98, 104, 255), 0.026f, new Vector3(TacticalBattlefieldLayout.HalfWidth, 0f, 0f));
            CreateSurface("West Boundary", Rectangle(0.075f, TacticalBattlefieldLayout.Depth),
                new Color32(70, 98, 104, 255), 0.026f, new Vector3(-TacticalBattlefieldLayout.HalfWidth, 0f, 0f));
            _teamAMaterial = CreateSurface("Team A Edge", Rectangle(16f, 0.1f), new Color32(44, 178, 140, 255), 0.027f,
                new Vector3(0f, 0f, -26f));
            _teamBMaterial = CreateSurface("Team B Edge", Rectangle(16f, 0.1f), new Color32(218, 126, 43, 255), 0.027f,
                new Vector3(0f, 0f, 26f));
            CreateSurface("Center Line", Rectangle(40f, 0.025f), new Color32(91, 106, 110, 60), 0.027f);
            _markers = new GameObject("Entity Markers");
            _markers.transform.SetParent(_root.transform, false);
            _surfaceParent = _root.transform;
            CreateSurface("Player Target Highlight", EllipseRing(0.78f, 0.78f, 0.08f),
                new Color32(230, 78, 70, 255), 0.04f);
            _targetHighlight = _root.transform.Find("Player Target Highlight")?.gameObject;
            if (_targetHighlight != null) _targetHighlight.SetActive(false);
        }

        private Material CreateSurface(string name, Mesh mesh, Color color, float y, Vector3 offset = default)
        {
            _meshes.Add(mesh);
            var child = new GameObject(name);
            child.transform.SetParent(_surfaceParent, false);
            child.transform.localPosition = new Vector3(offset.x, y, offset.z);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            Material material = CharacterTestUnlitMaterials.CreateMarker(name + " (Runtime)", color);
            renderer.sharedMaterial = material;
            _materials.Add(material);
            return material;
        }

        private void RebuildMarkers(TacticalBattleSetup setup)
        {
            for (int i = _markers.transform.childCount - 1; i >= 0; i--) Destroy(_markers.transform.GetChild(i).gameObject);
            if (setup?.Entities == null) return;
            for (int i = 0; i < setup.Entities.Count; i++)
            {
                TacticalBattleEntitySetup entity = setup.Entities[i];
                var marker = new GameObject(entity.DisplayName + " Marker");
                marker.transform.SetParent(_markers.transform, false);
                marker.transform.localPosition = new Vector3(entity.X, 0.034f, entity.Z);
                marker.transform.localRotation = Quaternion.Euler(0f, entity.YawDegrees, 0f);
                Mesh mesh = Marker(); _meshes.Add(mesh);
                marker.AddComponent<MeshFilter>().sharedMesh = mesh;
                marker.AddComponent<MeshRenderer>().sharedMaterial = entity.TeamId == 1 ? _teamAMaterial : _teamBMaterial;
            }
        }

        private static Mesh Rectangle(float width, float depth)
        {
            float x = width * 0.5f, z = depth * 0.5f;
            var mesh = new Mesh { name = "Tactical Preparation Rectangle" };
            mesh.SetVertices(new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z),
                new Vector3(x, 0, z), new Vector3(-x, 0, z) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh Grid(float width, float depth, float spacing, float thickness)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            float halfWidth = width * 0.5f, halfDepth = depth * 0.5f;
            for (float value = -halfDepth; value <= halfDepth + 0.001f; value += spacing)
                AddQuad(vertices, triangles, -halfWidth, value - thickness, halfWidth, value + thickness);
            for (float value = -halfWidth; value <= halfWidth + 0.001f; value += spacing)
            {
                AddQuad(vertices, triangles, value - thickness, -halfDepth, value + thickness, halfDepth);
            }
            var mesh = new Mesh { name = "Tactical Preparation Grid" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh Ellipse(float radiusX, float radiusZ)
        {
            const int segments = 64;
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var triangles = new List<int>(segments * 3);
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ));
            }
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(0); triangles.Add(1 + (i + 1) % segments); triangles.Add(1 + i);
            }
            var mesh = new Mesh { name = "Tactical Preparation Arena" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh EllipseRing(float radiusX, float radiusZ, float thickness)
        {
            const int segments = 64;
            var vertices = new List<Vector3>(segments * 2); var triangles = new List<int>(segments * 6);
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                vertices.Add(new Vector3(x * radiusX, 0f, z * radiusZ));
                vertices.Add(new Vector3(x * (radiusX - thickness), 0f, z * (radiusZ - thickness)));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments, outer = i * 2, inner = outer + 1;
                int nextOuter = next * 2, nextInner = nextOuter + 1;
                triangles.Add(outer); triangles.Add(inner); triangles.Add(nextOuter);
                triangles.Add(inner); triangles.Add(nextInner); triangles.Add(nextOuter);
            }
            var mesh = new Mesh { name = "Tactical Preparation Arena Boundary" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh Marker()
        {
            Mesh ring = EllipseRing(0.62f, 0.62f, 0.055f);
            var vertices = new List<Vector3>(ring.vertices);
            var triangles = new List<int>(ring.triangles);
            int start = vertices.Count;
            vertices.Add(new Vector3(-0.13f, 0f, 0.62f));
            vertices.Add(new Vector3(0.13f, 0f, 0.62f));
            vertices.Add(new Vector3(0f, 0f, 1.02f));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            var mesh = new Mesh { name = "Tactical Preparation Entity Marker" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Destroy(ring); return mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, float x0, float z0, float x1, float z1)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, 0, z0)); vertices.Add(new Vector3(x1, 0, z0));
            vertices.Add(new Vector3(x1, 0, z1)); vertices.Add(new Vector3(x0, 0, z1));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++) if (_materials[i] != null) Destroy(_materials[i]);
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Destroy(_meshes[i]);
            if (_root != null) Destroy(_root);
        }
    }
}
