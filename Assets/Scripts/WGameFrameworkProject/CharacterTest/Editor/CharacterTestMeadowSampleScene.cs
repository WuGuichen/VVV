using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit independent native-render fixture; never rewrites the current scene.</summary>
    public static class CharacterTestMeadowSampleScene
    {
        public const string ScenePath = "Assets/Scenes/MeadowWindSample.unity";
        public const string LargeScenePath = "Assets/Scenes/MeadowWindSample50k.unity";
        [MenuItem("WGame/Environment/Create Independent Meadow Wind Sample")]
        public static void CreateFromMenu() => Debug.Log("Meadow sample scene: " + Create());

        public static string Create() => Create(true);
        /// <summary>The LOD owner may reuse already prepared native assets to avoid rebuilding its mesh wiring.</summary>
        public static string Create(bool rebuildAssets)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create in Edit Mode after releasing the shared Editor.");
            var recipe = CharacterTestMeadowAssetBuilder.ReadRecipe();
            // Build only assets. Scene mutations below occur exclusively in a newly
            // created additive scene that is closed again before returning.
            if (rebuildAssets) CharacterTestMeadowAssetBuilder.BuildAssets();
            var groundShader = Shader.Find("Universal Render Pipeline/Lit");
            if (groundShader == null || !groundShader.isSupported) throw new InvalidOperationException("URP ground shader unavailable.");
            string groundPath = CharacterTestMeadowAssetBuilder.GeneratedRoot + "/MeadowSampleGround.mat";
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
            if (groundMaterial == null)
            {
                groundMaterial = new Material(groundShader) { name = "MeadowSampleGround" };
                groundMaterial.SetColor("_BaseColor", new Color(.29f, .25f, .14f)); groundMaterial.SetFloat("_Smoothness", .08f);
                AssetDatabase.CreateAsset(groundMaterial, groundPath);
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            string path = AssetDatabase.GenerateUniqueAssetPath(recipe.targetCount > 0 ? LargeScenePath : ScenePath);
            Scene previous = SceneManager.GetActiveScene();
            Scene sample = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(sample);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.59f, .67f, .72f);
                RenderSettings.ambientEquatorColor = new Color(.36f, .39f, .28f);
                RenderSettings.ambientGroundColor = new Color(.16f, .18f, .11f);
                RenderSettings.fog = false;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "MeadowSampleSurface";
                ground.transform.localScale = new Vector3((recipe.width + 12) / 10, 1, (recipe.depth + 12) / 10);
                UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                var anchor = new GameObject("MeadowVegetation");
                var asset = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
                CharacterTestVegetationAssetAuthoring.EnsureFolder("Assets/Config/MxFramework/Vegetation");
                AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath("Assets/Config/MxFramework/Vegetation/" + System.IO.Path.GetFileNameWithoutExtension(path) + ".asset"));
                var data = CharacterTestVegetationAssetAuthoring.Bind(sample, asset, anchor.transform, ground.transform);
                data.SurfaceRoot = ground.transform;
                int count = CharacterTestMeadowSampleAuthoring.Append(data, Vector3.zero);
                if (count == 0) throw new InvalidOperationException("Sample projection produced no plants.");
                if (recipe.targetCount > 0 && count != recipe.targetCount)
                    throw new InvalidOperationException("Projected " + count + " plants, expected " + recipe.targetCount +
                        ". No underfilled large scene will be saved.");
                var sun = new GameObject("MeadowSun").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(38, -35, 0); sun.color = new Color(1, .95f, .82f);
                sun.intensity = 1.3f; sun.shadows = LightShadows.None;
                RenderSettings.sun = sun;
                if (recipe.targetCount > 0)
                {
                    Camera("MeadowNearCamera", new Vector3(-30, 1.45f, -38), new Vector3(-30, .65f, -24), false);
                    Camera("MeadowMidCamera", new Vector3(0, 8, -62), new Vector3(0, .5f, 10), true);
                    Camera("MeadowFarCamera", new Vector3(0, 42, -108), new Vector3(0, .5f, 0), false);
                }
                else
                {
                    Camera("MeadowNearCamera", new Vector3(0, 1.4f, -4), new Vector3(0, .65f, 2), false);
                    Camera("MeadowMidCamera", new Vector3(0, 4, -13), new Vector3(0, .5f, 0), true);
                    Camera("MeadowFarCamera", new Vector3(0, 10, -25), new Vector3(0, .5f, 0), false);
                }
                if (!EditorSceneManager.SaveScene(sample, path)) throw new InvalidOperationException("Independent sample save failed.");
                int[] types = new int[4];
                foreach (var instance in data.Instances)
                    for (int i = 0; i < data.Types.Count; i++) if (instance.TypeId == data.Types[i].Id) types[i]++;
                Debug.Log("Independent meadow saved: " + path + "; serialized instances=" + data.Instances.Count +
                    "; seed=" + recipe.seed + "; extent=" + recipe.width + "x" + recipe.depth +
                    "m; short/tall/flower/plume=" + string.Join("/", types) +
                    ". Only the mid camera starts enabled. Verify saved records and the unified LOD consumer separately. No fixed-shadow bake performed.");
                AssetDatabase.SaveAssetIfDirty(data.Data);
                if (!EditorSceneManager.SaveScene(sample, path)) throw new InvalidOperationException("Could not save independent vegetation binding.");
                return path;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(sample, true);
            }
        }
        private static void Camera(string name, Vector3 position, Vector3 target, bool enabled)
        {
            var camera = new GameObject(name).AddComponent<UnityCamera>();
            camera.transform.position = position; camera.transform.LookAt(target);
            camera.fieldOfView = 55; camera.nearClipPlane = .1f; camera.farClipPlane = 260;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.6f, .67f, .71f);
            camera.enabled = enabled;
        }
    }
}
