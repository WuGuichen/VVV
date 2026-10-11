using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit additive data authoring; existing planted records are never replaced.</summary>
    public static class CharacterTestMeadowSampleAuthoring
    {
        [MenuItem("WGame/Environment/Append Meadow Sample To Selected Vegetation")]
        public static void AppendSelected()
        {
            var owner = CharacterTestVegetationAssetAuthoring.Find(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if (owner == null) throw new InvalidOperationException("Select the existing scene vegetation data owner.");
            AppendDefault(owner);
        }
        internal static int AppendDefault(CharacterTestVegetationSource owner)
        { var recipe = CharacterTestMeadowAssetBuilder.ReadRecipe(); return Append(owner, new Vector3(recipe.centerX, 0, recipe.centerZ)); }
        /// <summary>One Undo action. Projects onto the selected mesh scope; no plane fallback, no scene save or bake.</summary>
        internal static int Append(CharacterTestVegetationSource owner, Vector3 worldCenter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || owner == null || !owner.gameObject.scene.IsValid() ||
                owner.SurfaceRoot == null) throw new InvalidOperationException("Need an Edit Mode scene owner and explicit surface scope.");
            CharacterTestVegetationAuthoring.ValidateExclusion(owner.transform, owner.gameObject.scene);
            var recipe = CharacterTestMeadowAssetBuilder.ReadRecipe(); var placements = MeadowGeometry.Scatter(recipe);
            var sampler = new CharacterTestVegetationSurfaceSampler(owner.SurfaceRoot, 40);
            if (!string.IsNullOrEmpty(sampler.Issue)) throw new InvalidOperationException(sampler.Issue);
            var prefabs = new GameObject[recipe.types.Length]; var existingIds = new int[recipe.types.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestMeadowAssetBuilder.PrefabPath(recipe.types[i].id));
                if (prefabs[i] == null) throw new InvalidOperationException("Build meadow assets before authoring.");
                foreach (var type in owner.Types) if (type.Prefab == prefabs[i]) existingIds[i] = type.Id;
            }
            // Resolve valid surface hits and reject proximity to prior plants before
            // touching the owner's IDs, seed history, types or Undo state.
            var accepted = new List<MeadowPlacement>(); var hits = new List<Vector3>();
            var occupied = new Dictionary<Vector2Int, List<Vector3>>(); const float minimum = .12f;
            foreach (var old in owner.Instances) AddOccupied(occupied, owner.transform.TransformPoint(old.Position), minimum);
            foreach (var p in placements)
            {
                var candidate = worldCenter + new Vector3(p.X, 0, p.Z);
                if (!sampler.TryProject(candidate, out var hit) || TooClose(occupied, hit.Position, minimum)) continue;
                accepted.Add(p); hits.Add(hit.Position); AddOccupied(occupied, hit.Position, minimum);
            }
            if (accepted.Count == 0) return 0;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Append clustered meadow sample");
            Undo.RegisterCompleteObjectUndo(owner.UndoTarget, "Append clustered meadow sample");
            try
            {
                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (existingIds[i] != 0) continue; int next = 1;
                    foreach (var type in owner.Types) next = Math.Max(next, type.Id + 1);
                    existingIds[i] = next; owner.EditableTypes.Add(new CharacterTestVegetationType
                    { Id = next, Name = recipe.types[i].name, ResourceKey = CharacterTestMeadowAssetBuilder.ResourceKey(recipe.types[i].id),
                        Prefab = prefabs[i],
                        Lod1Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CharacterTestMeadowAssetBuilder.MeshPath(recipe.types[i].id, 1)),
                        Lod2Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CharacterTestMeadowAssetBuilder.MeshPath(recipe.types[i].id, 2)),
                        DrawDistance = recipe.drawDistance > 0 ? recipe.drawDistance : 100,
                        CastFixedDirectionalShadow = true });
                }
                owner.BeginStrokeSeed(recipe.seed);
                for (int i = 0; i < accepted.Count; i++)
                {
                    var p = accepted[i]; owner.EditableInstances.Add(new CharacterTestVegetationInstance
                    { Id = owner.AllocateId(), TypeId = existingIds[p.Type], Seed = p.Seed,
                        Position = owner.transform.InverseTransformPoint(hits[i]),
                        Rotation = Quaternion.Inverse(owner.transform.rotation) * Quaternion.Euler(0, p.Yaw, 0),
                        Scale = new Vector3(p.Scale, p.HeightScale, p.Scale) });
                }
                EditorUtility.SetDirty(owner.UndoTarget); EditorSceneManager.MarkSceneDirty(owner.gameObject.scene); owner.InvalidatePresentation();
                Undo.CollapseUndoOperations(group); return accepted.Count;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        private static Vector2Int Cell(Vector3 p, float size) => new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
        private static void AddOccupied(Dictionary<Vector2Int, List<Vector3>> occupied, Vector3 p, float size)
        { var key = Cell(p, size); if (!occupied.TryGetValue(key, out var values)) occupied.Add(key, values = new List<Vector3>()); values.Add(p); }
        private static bool TooClose(Dictionary<Vector2Int, List<Vector3>> occupied, Vector3 p, float size)
        {
            var cell = Cell(p, size);
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                if (occupied.TryGetValue(cell + new Vector2Int(x, z), out var values))
                    foreach (var other in values) if ((other - p).sqrMagnitude < size * size) return true;
            return false;
        }
    }
}
