using System;
using System.IO;
using System.Reflection;
using MxFramework.Project.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameBasicGrassAuthoringTests
    {
        [Test]
        public void GeometricRecipe_ProducesAnchoredPointedLeavesWithFiniteNondegenerateGeometry()
        {
            var recipeType = typeof(EnvironmentBasicGrassBuilder).GetNestedType("Recipe", BindingFlags.NonPublic);
            var recipe = JsonUtility.FromJson(File.ReadAllText(EnvironmentBasicGrassBuilder.SourcePath), recipeType);
            typeof(EnvironmentBasicGrassBuilder).GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new[] { recipe });
            var mesh = (Mesh)typeof(EnvironmentBasicGrassBuilder).GetMethod("CreateMesh", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new[] { recipe });
            try
            {
                Assert.AreEqual(160, mesh.vertexCount);
                Assert.AreEqual(160, mesh.triangles.Length / 3);
                Vector3[] vertices = mesh.vertices;
                Vector2[] uv = mesh.uv;
                Color[] colors = mesh.colors;
                for (int leaf = 0; leaf < 16; leaf++)
                {
                    for (int side = 0; side < 3; side++)
                    {
                        int index = leaf * 10 + side;
                        Assert.AreEqual(0f, vertices[index].y);
                        Assert.AreEqual(0f, uv[index].y);
                        Assert.Less(new Vector2(vertices[index].x, vertices[index].z).magnitude, 0.06f);
                    }
                    Assert.AreEqual(1f, uv[leaf * 10 + 9].y);
                }
                foreach (var color in colors) Assert.AreEqual(1f, color.a, "Shading tint must not cut the root.");
                foreach (var normal in mesh.normals)
                {
                    Assert.IsTrue(float.IsFinite(normal.x) && float.IsFinite(normal.y) && float.IsFinite(normal.z));
                    Assert.Greater(normal.sqrMagnitude, 0.5f);
                }
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    Assert.Greater(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude, 1e-10f);
                Assert.Greater(mesh.bounds.size.x, 0.3f);
                Assert.Greater(mesh.bounds.size.z, 0.3f);
                Assert.Greater(mesh.bounds.size.y, 0.5f);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void WindPreset_EnablesVisibleWindWithoutChangingCoverageOrInstancing()
        {
            var material = new Material(Shader.Find("WGame/FoliageMossLit"));
            try
            {
                material.enableInstancing = true;
                material.SetFloat("_WgAlphaCutoff", 0.61f);
                material.SetColor("_WgFoliageColor", new Color(0.2f, 0.3f, 0.4f, 0.7f));
                EnvironmentBasicGrassBuilder.ApplyWindDefaults(material);
                Assert.Greater(material.GetFloat("_WgWindBendStrength"), 0f);
                Assert.Greater(material.GetFloat("_WgWindBendFrequency"), 0f);
                Assert.AreEqual(0.61f, material.GetFloat("_WgAlphaCutoff"));
                Assert.AreEqual(0.7f, material.GetColor("_WgFoliageColor").a);
                Assert.IsTrue(material.enableInstancing);
                Assert.AreEqual(-1, material.FindPass("ShadowCaster"));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void Rebuild_PreservesResourceAssetIdentitiesAndPresentationOnlyContract()
        {
            EnvironmentBasicGrassBuilder.BuildAssets();
            string[] paths = { EnvironmentBasicGrassBuilder.MeshPath, EnvironmentBasicGrassBuilder.MaskPath,
                EnvironmentBasicGrassBuilder.MaterialPath, EnvironmentBasicGrassBuilder.PrefabPath };
            var guids = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++) guids[i] = AssetDatabase.AssetPathToGUID(paths[i]);
            EnvironmentBasicGrassBuilder.BuildAssets();
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.IsNotEmpty(guids[i]);
                Assert.AreEqual(guids[i], AssetDatabase.AssetPathToGUID(paths[i]), paths[i]);
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentBasicGrassBuilder.PrefabPath);
            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true));
            Assert.IsFalse(prefab.isStatic);
            MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
            Assert.IsTrue(renderer.receiveShadows);
            Assert.AreEqual(MotionVectorGenerationMode.ForceNoMotion, renderer.motionVectorGenerationMode);
            Material material = renderer.sharedMaterial;
            Assert.AreEqual("WGame/FoliageMossLit", material.shader.name);
            Assert.AreEqual(0.55f, material.GetFloat("_WgWindBendStrength"));
            Assert.AreEqual(0f, material.GetFloat("_WgWindSource"));
            Assert.AreEqual(0.85f, material.GetFloat("_WgWindBendFrequency"));
            Assert.AreEqual(0.65f, material.GetFloat("_WgWindGustStrength"));
            Assert.AreEqual(0.3f, material.GetFloat("_WgWindTipFlutter"));
            Assert.AreEqual(new Vector4(0.8f, 0f, 0.6f, 0f), material.GetVector("_WgWindDirection"));
            Assert.GreaterOrEqual(material.FindPass("DepthNormals"), 0);
            Assert.GreaterOrEqual(material.FindPass("DepthOnly"), 0);
            Assert.AreEqual(-1, material.FindPass("ShadowCaster"));
            var mask = (Texture2D)material.GetTexture("_WgMaskTex");
            Assert.NotNull(mask);
            Assert.AreEqual(1f, mask.GetPixel(0, 0).r, "Geometry owns the leaf silhouette; mask retains full R coverage.");
            Assert.Greater(mask.GetPixel(mask.width / 2, mask.height / 4).r,
                material.GetFloat("_WgAlphaCutoff"));
            Assert.NotNull(prefab.GetComponent<MeshFilter>().sharedMesh);
        }

        [Test]
        public void SavedPrefabInstance_ReopensWithAuthoredPositionRotationAndScale()
        {
            EnvironmentBasicGrassBuilder.BuildAssets();
            string path = "Assets/__BasicGrassSaveValidation_" + Guid.NewGuid().ToString("N") + ".unity";
            Scene original = SceneManager.GetActiveScene();
            // Both batch startup and the interactive Test Runner use an untitled bootstrap
            // scene. The runner restores the user's saved scene after the test run.
            // Keep an already saved active scene open when this fixture runs additively.
            bool ownsUntitledScene = string.IsNullOrEmpty(original.path);
            Scene fixture = default;
            Vector3 position = new Vector3(33f, 0.02f, -30.55f);
            Quaternion rotation = Quaternion.Euler(0f, 42f, 0f);
            Vector3 scale = new Vector3(0.75f, 0.9f, 0.75f);
            try
            {
                fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    ownsUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentBasicGrassBuilder.PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, fixture);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = scale;
                Assert.IsTrue(EditorSceneManager.SaveScene(fixture, path));
                if (ownsUntitledScene) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else Assert.IsTrue(EditorSceneManager.CloseScene(fixture, true));
                fixture = EditorSceneManager.OpenScene(path,
                    ownsUntitledScene ? OpenSceneMode.Single : OpenSceneMode.Additive);
                GameObject reopened = fixture.GetRootGameObjects()[0];
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(reopened));
                Assert.AreEqual(position, reopened.transform.position);
                Assert.Less(Quaternion.Angle(rotation, reopened.transform.rotation), 0.001f);
                Assert.AreEqual(scale, reopened.transform.localScale);
            }
            finally
            {
                if (ownsUntitledScene) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else
                {
                    if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                    if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                }
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
