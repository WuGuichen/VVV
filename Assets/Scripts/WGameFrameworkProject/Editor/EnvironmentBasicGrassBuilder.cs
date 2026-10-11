using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using MxFramework.ResourceMapping.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Project.Editor
{
    /// <summary>Builds one repository-authored grass fixture; it does not own scene layout or runtime loading.</summary>
    public static class EnvironmentBasicGrassBuilder
    {
        public const string SourcePath = "ArtSource/Environment/Vegetation/basic_grass_clump.json";
        public const string GeneratedRoot = "Assets/Art/WGame/Environment/Vegetation/Generated";
        public const string MeshPath = GeneratedRoot + "/BasicGrassClump.asset";
        public const string MaskPath = GeneratedRoot + "/BasicGrassMask.asset";
        public const string MaterialPath = GeneratedRoot + "/BasicGrassClump.mat";
        public const string PrefabPath = GeneratedRoot + "/BasicGrassClump.prefab";
        public const string ResourceId = "environment.grass.basic_clump";
        public const string PackageId = "wgame";
        private const string ProducerId = "wgame.environment.basic-grass";

        /// <summary>Rebuilds the technical grass assets and publishes their resource identity in Edit Mode.</summary>
        /// <remarks>Allocates during explicit authoring; never called from a runtime Tick path.</remarks>
        [MenuItem("WGame/Environment/Build Basic Grass Assets")]
        public static void BuildAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build grass assets in Edit Mode.");
            Recipe recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(SourcePath));
            Validate(recipe);
            Shader shader = Shader.Find("WGame/FoliageMossLit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("WGame/FoliageMossLit must be imported and supported.");
            EnsureFolder(GeneratedRoot);
            Mesh mesh = SaveGenerated(CreateMesh(recipe), MeshPath);
            Texture2D mask = SaveGenerated(CreateMask(recipe), MaskPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "BasicGrassClump" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_WgFoliageColor", recipe.foliageColor);
            material.SetColor("_WgDryColor", recipe.dryColor);
            material.SetTexture("_WgMaskTex", mask);
            material.SetFloat("_WgAlphaCutoff", 0.42f);
            material.SetFloat("_WgDryCoverage", recipe.dryCoverage);
            material.SetFloat("_WgMossCoverage", 0f);
            material.SetFloat("_WgVertexTintStrength", recipe.geometricLeaves ? 0.8f : 0f);
            material.SetFloat("_WgLeafNormalUpBlend", recipe.geometricLeaves ? 0.45f : 0f);
            ApplyWindDefaults(material);
            material.SetFloat("_WgDebugOverlayAmount", 0f);
            material.SetFloat("_WgWetnessOverride", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            SavePrefab(mesh, material);
            PublishMapping();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("Basic grass technical fixture rebuilt and registered. Drag the prefab into the scene; " +
                "keep it outside the Combat court and static light-transport source root. " + PrefabPath);
        }

        /// <summary>Applies the grass wind preset without rebuilding meshes, prefabs or scene data.</summary>
        /// <remarks>Edit Mode authoring only; the caller owns marking/saving the material.</remarks>
        public static void ApplyWindDefaults(Material material)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply grass wind defaults in Edit Mode.");
            if (material == null || material.shader == null || material.shader.name != "WGame/FoliageMossLit")
                throw new ArgumentException("Wind defaults require a WGame/FoliageMossLit material.", nameof(material));
            material.SetFloat("_WgWindSource", 0f);
            material.SetVector("_WgWindDirection", new Vector4(0.8f, 0f, 0.6f, 0f));
            material.SetFloat("_WgWindBendStrength", 0.55f);
            material.SetFloat("_WgWindBendFrequency", 0.85f);
            material.SetFloat("_WgWindGustStrength", 0.65f);
            material.SetFloat("_WgWindTipFlutter", 0.3f);
        }

        private static Mesh CreateMesh(Recipe recipe)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var indices = new List<int>();
            foreach (Blade blade in recipe.blades)
            {
                if (recipe.geometricLeaves)
                {
                    AppendGeometricLeaf(blade, recipe, vertices, uv, colors, indices);
                    continue;
                }
                int start = vertices.Count;
                Quaternion rotation = Quaternion.Euler(0f, blade.yawDegrees, 0f);
                for (int row = 0; row < 3; row++)
                {
                    float t = row * 0.5f;
                    Vector3 center = new Vector3(blade.x, 0f, blade.z) +
                        rotation * new Vector3(blade.lean * t * t, blade.height * t, 0f);
                    Vector3 side = rotation * Vector3.right * (blade.width * 0.5f);
                    vertices.Add(center - side);
                    vertices.Add(center + side);
                    uv.Add(new Vector2(0f, t));
                    uv.Add(new Vector2(1f, t));
                    colors.Add(Color.white);
                    colors.Add(Color.white);
                }
                for (int row = 0; row < 2; row++)
                {
                    int a = start + row * 2;
                    indices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "BasicGrassClump" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Three folded cross-sections followed by one shared pointed tip: ten
        // vertices/triangles per leaf. The outline is geometry, not an alpha card.
        private static void AppendGeometricLeaf(Blade blade, Recipe recipe, List<Vector3> vertices,
            List<Vector2> uv, List<Color> colors, List<int> indices)
        {
            int start = vertices.Count;
            float yaw = blade.yawDegrees * Mathf.Deg2Rad;
            float sinYaw = (float)Math.Sin(yaw), cosYaw = (float)Math.Cos(yaw);
            for (int row = 0; row < 4; row++)
            {
                float t = row == 0 ? 0f : row == 1 ? 0.3f : row == 2 ? 0.68f : 1f;
                float taper = row == 0 ? 0.32f : row == 1 ? 1f : row == 2 ? 0.63f : 0f;
                float twist = blade.twistDegrees * Mathf.Deg2Rad * t * t;
                float sideX = (float)Math.Cos(twist), sideZ = (float)Math.Sin(twist);
                float centerX = blade.sideLean * t * t;
                float centerZ = blade.lean * (float)Math.Pow(t, blade.curvePower);
                float halfWidth = blade.width * taper * 0.5f;
                Color color = Color.Lerp(recipe.rootTint, recipe.tipTint, t) * blade.tint;
                color.a = 1f;
                for (int column = 0; column < (row == 3 ? 1 : 3); column++)
                {
                    float side = row == 3 ? 0f : column - 1;
                    float x = centerX + sideX * halfWidth * side;
                    float z = centerZ + sideZ * halfWidth * side;
                    if (row != 3 && column == 1)
                        z += blade.width * recipe.leafFold * (float)Math.Sin(Math.PI * t);
                    vertices.Add(new Vector3(blade.x + cosYaw * x + sinYaw * z,
                        blade.height * t, blade.z - sinYaw * x + cosYaw * z));
                    uv.Add(new Vector2(side * 0.5f + 0.5f, t));
                    colors.Add(color);
                }
            }
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 2; column++)
                {
                    int a = start + row * 3 + column;
                    indices.AddRange(new[] { a, a + 3, a + 1, a + 1, a + 3, a + 4 });
                }
            indices.AddRange(new[] { start + 6, start + 9, start + 7,
                start + 7, start + 9, start + 8 });
        }

        private static Texture2D CreateMask(Recipe recipe)
        {
            var texture = new Texture2D(recipe.maskWidth, recipe.maskHeight, TextureFormat.RGBA32, true, true)
            {
                name = "BasicGrassMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[recipe.maskWidth * recipe.maskHeight];
            for (int y = 0; y < recipe.maskHeight; y++)
            {
                float v = (y + 0.5f) / recipe.maskHeight;
                float halfWidth = 0.39f * Mathf.Pow(1f - v, 0.7f);
                for (int x = 0; x < recipe.maskWidth; x++)
                {
                    float distance = Mathf.Abs((x + 0.5f) / recipe.maskWidth - 0.5f);
                    // Geometric leaves keep the established R-coverage contract,
                    // using full coverage so alpha cannot narrow their actual outline.
                    float coverage = recipe.geometricLeaves ? 1f :
                        Mathf.Clamp01((halfWidth - distance) * recipe.maskWidth + 0.5f);
                    pixels[y * recipe.maskWidth + x] = new Color(coverage, 0f, recipe.dryCoverage, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static T SaveGenerated<T>(T generated, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            try
            {
                if (generated is Mesh generatedMesh && existing is Mesh existingMesh)
                {
                    // Native setters invalidate resident GPU geometry. CopySerialized
                    // updates the asset data but can leave an already drawn mesh stale.
                    existingMesh.Clear();
                    existingMesh.vertices = generatedMesh.vertices;
                    existingMesh.uv = generatedMesh.uv;
                    existingMesh.colors = generatedMesh.colors;
                    existingMesh.triangles = generatedMesh.triangles;
                    existingMesh.RecalculateNormals();
                    existingMesh.RecalculateBounds();
                }
                else EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            finally { UnityEngine.Object.DestroyImmediate(generated); }
        }

        private static void SavePrefab(Mesh mesh, Material material)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("BasicGrassClump");
            try
            {
                if (root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Generated grass prefab must remain presentation-only.");
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                root.isStatic = false;
                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter == null) filter = root.AddComponent<MeshFilter>();
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = root.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save the basic grass prefab.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PublishMapping()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("Grass prefab must exist before resource registration.");
            var snapshot = new ResourceMappingProducerSnapshot { ProducerId = ProducerId };
            snapshot.Entries.Add(new ResourceMappingEntry
            {
                AuthoringRecordId = ProducerId + ":" + ResourceId,
                ProducerReceipt = new ResourceMappingProducerReceipt
                {
                    ProducerId = ProducerId, ProducerContractVersion = "1",
                    SourceRecordFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(File.ReadAllText(SourcePath))
                },
                ResourceKey = new ResourceMappingResourceKey
                    { Id = ResourceId, TypeId = "GameObject", PackageId = PackageId },
                DeliveryIntent = ResourceMappingDeliveryIntent.Internal,
                OverrideIntent = ResourceMappingOverrideIntent.Forbid,
                SourceLocator = new ResourceMappingSourceLocator
                {
                    Kind = ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
                    CachedPath = PrefabPath, GeneratedAssetId = ResourceId
                },
                BundleIntent = new ResourceMappingBundleIntent { RuleId = "common.models" },
                Labels = new List<string> { "domain.environment", "environment.grass", "package.wgame" }
            });
            var aggregator = new ResourceMappingProfileFileAggregator();
            var state = aggregator.Read(GlobalResourceMappingBuilder.ProfilePath);
            if (!state.Success) throw new InvalidOperationException("Cannot read the global resource Profile.");
            var result = aggregator.ApplyCompleteSnapshots(state.ProfilePath, state.Revision,
                new[] { snapshot }, EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!result.Success)
                throw new InvalidOperationException("Grass resource registration failed: " + result.ErrorCode + " " + result.Message);
            if (result.Changed) AssetDatabase.ImportAsset(state.ProfilePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Validate(Recipe recipe)
        {
            if (recipe == null || recipe.format != "wgame.basic-grass-clump.v1" ||
                recipe.blades == null || recipe.blades.Length == 0 || recipe.blades.Length > 32 ||
                recipe.maskWidth < 16 || recipe.maskWidth > 256 || recipe.maskHeight < 16 || recipe.maskHeight > 256 ||
                !InRange(recipe.dryCoverage, 0f, 1f) || !ValidColor(recipe.foliageColor) || !ValidColor(recipe.dryColor))
                throw new InvalidDataException("Invalid basic grass source recipe.");
            if (recipe.geometricLeaves && (!InRange(recipe.leafFold, 0f, 0.5f) ||
                !ValidColor(recipe.rootTint) || !ValidColor(recipe.tipTint)))
                throw new InvalidDataException("Invalid geometric grass shading recipe.");
            foreach (Blade blade in recipe.blades)
                if (blade == null || !InRange(blade.x, -0.5f, 0.5f) || !InRange(blade.z, -0.5f, 0.5f) ||
                    !InRange(blade.yawDegrees, -360f, 360f) || !InRange(blade.height, 0.05f, 1f) ||
                    !InRange(blade.width, 0.01f, 0.5f) || !InRange(blade.lean, -0.5f, 0.5f) ||
                    (recipe.geometricLeaves && (!InRange(blade.sideLean, -0.25f, 0.25f) ||
                        !InRange(blade.curvePower, 1f, 4f) || !InRange(blade.twistDegrees, -60f, 60f) ||
                        !InRange(blade.tint, 0.7f, 1.2f))))
                    throw new InvalidDataException("Invalid blade dimensions in basic grass source recipe.");
        }

        private static bool InRange(float value, float min, float max) => value >= min && value <= max;
        private static bool ValidColor(Color value) => InRange(value.r, 0f, 1f) && InRange(value.g, 0f, 1f) &&
            InRange(value.b, 0f, 1f) && value.a == 1f;

        [Serializable]
        private sealed class Recipe
        {
            public string format;
            public int maskWidth, maskHeight;
            public Color foliageColor, dryColor;
            public float dryCoverage;
            public bool geometricLeaves;
            public float leafFold;
            public Color rootTint, tipTint;
            public Blade[] blades;
        }

        [Serializable]
        private sealed class Blade
        {
            public float x, z, yawDegrees, height, width, lean;
            public float sideLean, curvePower, twistDegrees, tint;
        }
    }
}
