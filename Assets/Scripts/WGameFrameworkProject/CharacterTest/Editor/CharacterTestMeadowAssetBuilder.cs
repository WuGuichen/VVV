using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using MxFramework.ResourceMapping.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit Edit Mode authoring of four original meadow assets. No scene mutation.</summary>
    public static class CharacterTestMeadowAssetBuilder
    {
        public const string SourcePath = "ArtSource/Environment/Vegetation/meadow_sample.json";
        public const string GeneratedRoot = "Assets/Art/WGame/Environment/Vegetation/Meadow";
        private const string ProducerId = "wgame.environment.meadow";
        public static string PrefabPath(string id) => GeneratedRoot + "/" + id + ".prefab";
        public static string MeshPath(string id, int lod) => GeneratedRoot + "/" + id + "_lod" + lod + ".asset";
        public static string ResourceKey(string id) => "wgame|environment." + id + "|GameObject|";
        internal static MeadowRecipe ReadRecipe()
        { var r = JsonUtility.FromJson<MeadowRecipe>(File.ReadAllText(SourcePath)); MeadowGeometry.Validate(r); return r; }

        [MenuItem("WGame/Environment/Build Meadow Sample Assets")]
        public static void BuildAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit Mode.");
            var r = ReadRecipe(); var shader = Shader.Find("WGame/FoliageMossLit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Foliage shader unavailable.");
            EnsureFolder(GeneratedRoot);
            string maskPath = GeneratedRoot + "/MeadowCoverage.asset";
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (mask == null)
            {
                mask = new Texture2D(128, 256, TextureFormat.RGBA32, true, true)
                    { name = "MeadowCoverage", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            if (mask.width != 128 || mask.height != 256) throw new InvalidOperationException("Unexpected meadow mask dimensions.");
            var pixels = new Color[128 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 128; x++)
                pixels[y * 128 + x] = new Color(MeadowGeometry.Coverage((x + .5f) / 128, (y + .5f) / 256), 0, 0, 1);
            mask.SetPixels(pixels); mask.Apply(true, false); EditorUtility.SetDirty(mask); AssetDatabase.SaveAssetIfDirty(mask);
            string materialPath = GeneratedRoot + "/MeadowShared.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader; material.enableInstancing = true;
            material.SetTexture("_WgMaskTex", mask); material.SetColor("_WgFoliageColor", Color.white);
            material.SetFloat("_WgAlphaCutoff", .42f); material.SetFloat("_WgMossCoverage", 0);
            material.SetFloat("_WgDryCoverage", 0); material.SetFloat("_WgVertexTintStrength", 1);
            material.SetFloat("_WgVertexTintAmount", 1); material.SetFloat("_WgPatchColorStrength", .75f);
            material.SetFloat("_WgLeafNormalUpBlend", .48f); material.SetFloat("_WgFacetStrength", .5f);
            material.SetFloat("_WgWindSource", 0); material.SetVector("_WgWindDirection", new Vector4(.8f, 0, .6f, 0));
            material.SetFloat("_WgWindBendStrength", .68f); material.SetFloat("_WgWindBendFrequency", .9f);
            material.SetFloat("_WgWindGustStrength", .9f); material.SetFloat("_WgWindTipFlutter", .24f);
            material.SetFloat("_WgWindGustSpatialScale", .24f);
            material.SetFloat("_WgSeparateWindHeight", 1);
            material.SetFloat("_WgRootShadeHeight", .11f); material.SetFloat("_WgRootShadeStrength", .3f);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var snapshot = new ResourceMappingProducerSnapshot { ProducerId = ProducerId };
            string fingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(File.ReadAllText(SourcePath));
            foreach (var plant in r.types)
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    var data = MeadowGeometry.Build(plant, lod); string path = MeshPath(plant.id, lod);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    bool created = mesh == null; if (created) mesh = new Mesh { name = plant.id + "_lod" + lod };
                    mesh.Clear(); var points = new List<Vector3>(data.Points.Count);
                    var uv = new List<Vector2>(data.Points.Count); var windUV = new List<Vector2>(data.Points.Count);
                    var colors = new List<Color>(data.Points.Count);
                    for (int i = 0; i < data.Points.Count; i++)
                    { var p = data.Points[i]; var c = data.Colors[i]; points.Add(new Vector3(p.X, p.Y, p.Z));
                        uv.Add(new Vector2(data.MaskUvs[i][0], data.MaskUvs[i][1])); windUV.Add(new Vector2(data.Heights[i], 0));
                        colors.Add(new Color(c.r, c.g, c.b, 1)); }
                    mesh.SetVertices(points); mesh.SetUVs(0, uv); mesh.SetUVs(1, windUV);
                    mesh.SetColors(colors); mesh.SetTriangles(data.Indices, 0);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    if (created) AssetDatabase.CreateAsset(mesh, path);
                    else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
                    // Explicit Mesh keys make the LOD assets bundle-reachable without
                    // attaching runtime components or inventing an LOD coordinator.
                    AddResource(snapshot, "environment." + plant.id + ".lod" + lod, "Mesh", path, fingerprint);
                }
                SavePrefab(plant.id, AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(plant.id, 0)), material);
                AddResource(snapshot, "environment." + plant.id, "GameObject", PrefabPath(plant.id), fingerprint);
            }
            var aggregator = new ResourceMappingProfileFileAggregator(); var state = aggregator.Read(GlobalResourceMappingBuilder.ProfilePath);
            if (!state.Success) throw new InvalidOperationException("Cannot read resource profile.");
            var result = aggregator.ApplyCompleteSnapshots(state.ProfilePath, state.Revision, new[] { snapshot },
                EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!result.Success) throw new InvalidOperationException(result.ErrorCode + ": " + result.Message);
            if (result.Changed) AssetDatabase.ImportAsset(state.ProfilePath);
            Debug.Log("Four meadow prefabs, twelve actual LOD meshes and one shared material built; no scene was edited.");
        }
        private static void AddResource(ResourceMappingProducerSnapshot snapshot, string id, string type, string path, string fingerprint)
        {
            snapshot.Entries.Add(new ResourceMappingEntry
            {
                AuthoringRecordId = ProducerId + ":" + id,
                ProducerReceipt = new ResourceMappingProducerReceipt { ProducerId = ProducerId, ProducerContractVersion = "1",
                    SourceRecordFingerprint = fingerprint },
                ResourceKey = new ResourceMappingResourceKey { Id = id, TypeId = type, PackageId = "wgame" },
                DeliveryIntent = ResourceMappingDeliveryIntent.Internal, OverrideIntent = ResourceMappingOverrideIntent.Forbid,
                SourceLocator = new ResourceMappingSourceLocator { Kind = ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
                    CachedPath = path, GeneratedAssetId = id },
                BundleIntent = new ResourceMappingBundleIntent { RuleId = "common.models" },
                Labels = new List<string> { "domain.environment", "environment.meadow", "package.wgame" }
            });
        }
        private static void SavePrefab(string id, Mesh mesh, Material material)
        {
            string path = PrefabPath(id); bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(id);
            try
            {
                if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Meadow prefab must remain presentation-only.");
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                root.isStatic = false; var filter = root.GetComponent<MeshFilter>(); if (filter == null) filter = root.AddComponent<MeshFilter>();
                var renderer = root.GetComponent<MeshRenderer>(); if (renderer == null) renderer = root.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true; renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Prefab save failed.");
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
