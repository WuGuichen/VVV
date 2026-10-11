using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Rendering
{
    public sealed class CharacterTestVegetationBrushTests
    {
        private Scene _scene, _previous;
        private CharacterTestVegetationSource _owner;
        private GameObject _prefab;
        private readonly List<UnityEngine.Object> _temporaryAssets = new List<UnityEngine.Object>();
        private string _folder;

        private sealed class PlaneSurface : IVegetationSurfaceProvider
        {
            internal bool Available = true;
            public bool TryProject(Vector3 candidate, out VegetationSurfaceHit hit)
            { hit = new VegetationSurfaceHit(new Vector3(candidate.x, 2f, candidate.z), Vector3.up); return Available; }
        }
        [SetUp]
        public void SetUp()
        {
            _previous = SceneManager.GetActiveScene();
            // An unsaved user scene cannot coexist with additive authoring scenes.
            // Only the batch runner's clean blank scene may be replaced.
            bool cleanBatchBlank = Application.isBatchMode && SceneManager.sceneCount == 1 && string.IsNullOrEmpty(_previous.path) && !_previous.isDirty;
            if (!cleanBatchBlank)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        Assert.Ignore("Save the existing untitled scene before running vegetation authoring checks.");
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, cleanBatchBlank ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestVegetationAuthoring.GrassPrefabPath);
            Assert.NotNull(_prefab);
            var root = new GameObject(CharacterTestVegetationAuthoring.GroupName);
            SceneManager.MoveGameObjectToScene(root, _scene);
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>(); _temporaryAssets.Add(data);
            _owner = new CharacterTestVegetationSource(data, new MxFramework.Rendering.MxScenePresentationBinding {
                Data = data, Anchor = root.transform, OwnerId = "brush-fixture" }, _scene, null);
            _owner.EditableTypes.Add(new CharacterTestVegetationType { Prefab = _prefab });
        }
        [TearDown]
        public void TearDown()
        {
            _owner?.Renderer.Disable();
            if (_owner?.Data != null) Undo.ClearUndo(_owner.Data);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                CharacterTestVegetationAssetAuthoring.ReleaseScene(_scene);
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(_scene, true);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            foreach (var asset in _temporaryAssets) if (asset != null && !AssetDatabase.Contains(asset)) UnityEngine.Object.DestroyImmediate(asset);
            _temporaryAssets.Clear();
            if (!string.IsNullOrEmpty(_folder)) { AssetDatabase.DeleteAsset(_folder); _folder = null; }
        }
        [Test]
        public void PaintAcrossMultipleStamps_IsOneUndoAndRedoRestoresExactSeededData()
        {
            var settings = new CharacterTestVegetationBrushSettings { Radius = 2, Density = 1, MinimumSpacing = 0.4f, Seed = 7654 };
            using (var stroke = new CharacterTestVegetationBrushStroke(_owner, settings, new PlaneSurface()))
            { stroke.Paint(Vector3.zero, false); stroke.Paint(new Vector3(2, 0, 0), false); stroke.Complete(); }
            Assert.Greater(_owner.Instances.Count, 10); string painted = EditorJsonUtility.ToJson(_owner.Data);
            Undo.PerformUndo(); Assert.AreEqual(0, _owner.Instances.Count);
            Undo.PerformRedo(); Assert.AreEqual(painted, EditorJsonUtility.ToJson(_owner.Data));
            foreach (var item in _owner.Instances) Assert.AreEqual(2f, item.Position.y);
            for (int i = 0; i < _owner.Instances.Count; i++) for (int j = i + 1; j < _owner.Instances.Count; j++)
                Assert.GreaterOrEqual(Vector3.Distance(_owner.Instances[i].Position, _owner.Instances[j].Position), 0.4f - 0.0001f);
        }
        [Test]
        public void SameSeedAndGestureOrder_ReproducesPositionsYawAndScale()
        {
            var settings = new CharacterTestVegetationBrushSettings { Seed = 91 };
            using (var a = new CharacterTestVegetationBrushStroke(_owner, settings, new PlaneSurface()))
            { a.Paint(Vector3.zero, false); a.Complete(); }
            string expected = JsonUtility.ToJson(_owner.Data);
            Undo.PerformUndo();
            using (var b = new CharacterTestVegetationBrushStroke(_owner, settings, new PlaneSurface()))
            { b.Paint(Vector3.zero, false); b.Complete(); }
            Assert.AreEqual(expected, JsonUtility.ToJson(_owner.Data));
        }
        [Test]
        public void PointPlacementAndCancelledGesture_DoNotLeaveExtraInstances()
        {
            var settings = new CharacterTestVegetationBrushSettings();
            using (var a = new CharacterTestVegetationBrushStroke(_owner, settings, new PlaneSurface()))
            { Assert.AreEqual(1, a.Paint(Vector3.zero, true)); a.Complete(); }
            string before = JsonUtility.ToJson(_owner.Data);
            using (var b = new CharacterTestVegetationBrushStroke(_owner, settings, new PlaneSurface()))
            { b.Paint(new Vector3(5, 0, 0), false); b.Complete(cancel: true); }
            Assert.AreEqual(before, JsonUtility.ToJson(_owner.Data));
        }
        [Test]
        public void MissedSurface_DoesNotConsumeIdsSeedsOrCreateUndoMutation()
        {
            string before = JsonUtility.ToJson(_owner.Data);
            using (var stroke = new CharacterTestVegetationBrushStroke(_owner, new CharacterTestVegetationBrushSettings(), new PlaneSurface { Available = false }))
            { Assert.AreEqual(0, stroke.Paint(Vector3.zero, false)); stroke.Complete(); }
            Assert.AreEqual(before, JsonUtility.ToJson(_owner.Data));
        }
        [Test]
        public void EraseMultipleStamps_UndoRedoRestoresTheWholeGesture()
        {
            AddInstance(new Vector3(0, 2, 0)); AddInstance(new Vector3(2, 2, 0)); AddInstance(new Vector3(10, 2, 0));
            string before = JsonUtility.ToJson(_owner.Data);
            using (var stroke = new CharacterTestVegetationBrushStroke(_owner, new CharacterTestVegetationBrushSettings { Radius = 0.75f }, new PlaneSurface()))
            { stroke.Erase(new Vector3(0, 2, 0)); stroke.Erase(new Vector3(2, 2, 0)); stroke.Complete(); }
            Assert.AreEqual(1, _owner.Instances.Count);
            Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(_owner.Data));
            Undo.PerformRedo(); Assert.AreEqual(1, _owner.Instances.Count);
            Assert.AreEqual(new Vector3(10, 2, 0), _owner.Instances[0].Position);
        }
        [Test]
        public void BatchAdjustment_TouchesOnlySelectedIdsAndCanBeUndone()
        {
            int a = AddInstance(Vector3.zero), b = AddInstance(Vector3.right * 5);
            string before = JsonUtility.ToJson(_owner.Data);
            Assert.AreEqual(1, CharacterTestVegetationAuthoring.Adjust(_owner, new HashSet<int> { a }, 1.5f, 45));
            Assert.AreEqual(Vector3.one * 1.5f, _owner.Instances[0].Scale);
            Assert.AreEqual(Vector3.one, _owner.Instances[1].Scale); Assert.AreEqual(b, _owner.Instances[1].Id);
            Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(_owner.Data));
        }
        [Test]
        public void RealMeshSampling_RespectsScopeMissAndSlopeWithoutPlaneFallback()
        {
            var ground = CreateGround();
            var sampler = new CharacterTestVegetationSurfaceSampler(ground.transform, 40);
            Assert.IsEmpty(sampler.Issue);
            Assert.IsTrue(sampler.TryRay(new Ray(new Vector3(0, 10, 0), Vector3.down), out var hit));
            Assert.That(hit.Position.y, Is.EqualTo(2).Within(0.0001));
            Assert.IsFalse(sampler.TryRay(new Ray(new Vector3(20, 10, 0), Vector3.down), out _));
            ground.transform.rotation = Quaternion.Euler(60, 0, 0);
            sampler = new CharacterTestVegetationSurfaceSampler(ground.transform, 40);
            Assert.IsFalse(sampler.TryRay(new Ray(new Vector3(0, 10, 0), Vector3.down), out _));
            sampler = new CharacterTestVegetationSurfaceSampler(ground.transform, 70);
            Assert.IsTrue(sampler.TryRay(new Ray(new Vector3(0, 10, 0), Vector3.down), out _));
            Assert.IsFalse(new CharacterTestVegetationSurfaceSampler(null, 89).TryProject(Vector3.zero, out _));
        }
        [Test]
        public void AssetBindingIsUniqueAndRebindingUndoRestoresOriginalAuthority()
        {
            _folder = "Assets/__VegetationBrushTests_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            AssetDatabase.CreateAsset(_owner.Data, _folder + "/First.asset");
            var second = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            second.EditableTypes.Add(new CharacterTestVegetationType { Prefab = _prefab });
            AssetDatabase.CreateAsset(second, _folder + "/Second.asset");
            var first = CharacterTestVegetationAssetAuthoring.Bind(_scene, _owner.Data, _owner.transform);
            var anchor = first.transform; string ownerId = first.Binding.OwnerId;
            var bound = CharacterTestVegetationAssetAuthoring.Bind(_scene, second);
            Assert.AreSame(second, bound.Data); Assert.AreSame(anchor, bound.transform); Assert.AreEqual(ownerId, bound.Binding.OwnerId);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            _owner = CharacterTestVegetationAssetAuthoring.Find(_scene);
            Assert.AreSame(first.Data, _owner.Data); Assert.AreSame(anchor, _owner.transform);
            Assert.AreEqual(1, anchor.GetComponent<MxFramework.Rendering.MxSceneRenderingSceneRoot>().Configuration.Presentations.Length);
            Assert.AreEqual(0, anchor.childCount);
        }
        [Test]
        public void InvalidData_BlocksSave()
        {
            AddInstance(Vector3.zero); var item = _owner.EditableInstances[0]; item.Scale = new Vector3(float.NaN, 1, 1); _owner.EditableInstances[0] = item;
            Assert.IsFalse(_owner.TryValidate(out string issue)); Assert.IsNotEmpty(issue);
        }
        [Test]
        public void NativeUnifiedSaveAndReopen_PreservesAuthorityAndCreatesNoPerClumpObjects()
        {
            var game = new GameObject("GameManager"); SceneManager.MoveGameObjectToScene(game, _scene); game.AddComponent<GameManager>();
            _folder = "Assets/__VegetationBrushTests_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            string path = _folder + "/Vegetation.unity"; Assert.IsTrue(EditorSceneManager.SaveScene(_scene, path));
            _owner.Renderer.Disable();
            _owner = CharacterTestVegetationAssetAuthoring.Create(_scene);
            Assert.AreSame(_owner, CharacterTestVegetationAssetAuthoring.Create(_scene), "Repeated creation must use the unique binding.");
            string assetPath = _folder + "/Data.asset";
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(_owner.Data), assetPath));
            using (var stroke = new CharacterTestVegetationBrushStroke(_owner, new CharacterTestVegetationBrushSettings(), new PlaneSurface()))
            { stroke.Paint(Vector3.zero, false); stroke.Complete(); }
            string expected = JsonUtility.ToJson(_owner.Data); int count = _owner.Instances.Count;
            var module = new CharacterTestVegetationLevelEditorModule();
            try
            {
                module.OnEnable();
                Assert.IsTrue(MxLevelEditorSaveCoordinator.TrySaveModules(new[] { module }, out string message), message);
            }
            finally { module.OnDisable(); }
            Assert.IsFalse(_scene.isDirty);
            CharacterTestVegetationAssetAuthoring.ReleaseScene(_scene);
            var savedData = _owner.Data;
            if (SceneManager.sceneCount > 1) Assert.IsTrue(EditorSceneManager.CloseScene(_scene, true));
            UnityEngine.Resources.UnloadAsset(savedData);
            _scene = EditorSceneManager.OpenScene(path, _previous.IsValid() && _previous.isLoaded ? OpenSceneMode.Additive : OpenSceneMode.Single); SceneManager.SetActiveScene(_scene);
            _owner = CharacterTestVegetationAssetAuthoring.Find(_scene);
            Assert.NotNull(_owner); Assert.AreEqual(expected, JsonUtility.ToJson(_owner.Data)); Assert.AreEqual(count, _owner.Instances.Count);
            Assert.AreEqual(0, _owner.transform.childCount); Assert.AreEqual(_prefab, _owner.Types[0].Prefab);
            Assert.Greater(_owner.Renderer.Batches.Count, 0);
        }
        [TestCase(1000)]
        [TestCase(10000)]
        public void RepresentativeLayout_CachesSpatialBatchesAndSharesPresentation(int count)
        {
            int width = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++) AddInstance(new Vector3((i % width) * 0.5f, 2, (i / width) * 0.5f));
            _owner.InvalidatePresentation(); var renderer = _owner.Renderer; renderer.EnsureBuilt();
            string authority = JsonUtility.ToJson(_owner.Data);
            int sum = 0; Mesh mesh = _prefab.GetComponent<MeshFilter>().sharedMesh; Material material = _prefab.GetComponent<MeshRenderer>().sharedMaterial;
            foreach (var batch in renderer.Batches)
            { Assert.AreSame(mesh, batch.Mesh); Assert.AreSame(material, batch.Material); sum += batch.Matrices.Length; }
            Assert.AreEqual(count, sum); Assert.AreEqual(0, _owner.transform.childCount);
            var first = renderer.Batches[0].Matrices;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) renderer.EnsureBuilt();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated); Assert.AreSame(first, renderer.Batches[0].Matrices);
            renderer.Submit(null); Assert.AreEqual(authority, JsonUtility.ToJson(_owner.Data), "Presentation must never rewrite authored authority.");
            TestContext.WriteLine("CPU structure only: instances=" + count + ", chunks=" + renderer.ChunkCount + ", batches=" + renderer.Batches.Count +
                ", rebuildMs=" + renderer.RebuildMilliseconds.ToString("0.###") + ", warmCacheAllocBytes=" + allocated + "; these are culling cells, not draw submissions; no GPU/frame-time claim.");
        }

        [TestCase(744, 1)]
        [TestCase(8192, 1)]
        [TestCase(10000, 2)]
        public void IndirectPreparation_PacksAcrossCellsAtConfigured8192(int count, int expectedDraws)
        {
            for (int i = 0; i < count; i++) AddInstance(new Vector3((i % 100) * 0.5f, 2, (i / 100) * 0.5f));
            var camera = CreateVegetationCamera(new Vector3(25, 60, 25));
            Assert.AreEqual(8192, _owner.DrawBatchSize);
            string authority = JsonUtility.ToJson(_owner.Data);
            var draws = _owner.Renderer.Prepare(camera, true);
            Assert.AreEqual(expectedDraws, draws.Count);
            int total = 0;
            foreach (var draw in draws) { Assert.IsTrue(draw.Indirect); Assert.LessOrEqual(draw.Count, 8192); total += draw.Count; }
            Assert.AreEqual(count, total);
            if (count == 10000) { Assert.AreEqual(8192, draws[0].Count); Assert.AreEqual(1808, draws[1].Count); }
            Assert.AreEqual(authority, JsonUtility.ToJson(_owner.Data));
            Assert.Greater(_owner.Renderer.ChunkCount, 1, "Cells must not become draw boundaries.");
        }

        [Test]
        public void DrawCapacity_IsConfigurableAndFallbackReportsItsActualSmallerPackets()
        {
            for (int i = 0; i < 10000; i++) AddInstance(new Vector3((i % 100) * 0.5f, 2, (i / 100) * 0.5f));
            var camera = CreateVegetationCamera(new Vector3(25, 60, 25));
            _owner.SetDrawBatchSize(10000);
            Assert.AreEqual(1, _owner.Renderer.Prepare(camera, true).Count);
            var fallback = _owner.Renderer.Prepare(camera, false);
            Assert.AreEqual(40, fallback.Count);
            int total = 0;
            foreach (var draw in fallback) { Assert.IsFalse(draw.Indirect); Assert.LessOrEqual(draw.Count, 256); total += draw.Count; }
            Assert.AreEqual(10000, total);
        }

        [Test]
        public void SharedMeshAndMaterial_MergeTypeIdsButKeepTheirOwnDistanceFilters()
        {
            _owner.EditableTypes.Add(new CharacterTestVegetationType { Id = 2, Prefab = _prefab, DrawDistance = 100 });
            int first = AddInstance(new Vector3(1, 2, 1));
            int second = AddInstance(new Vector3(20, 2, 1));
            var item = _owner.EditableInstances[1]; item.TypeId = 2; _owner.EditableInstances[1] = item;
            _owner.InvalidatePresentation();
            var camera = CreateVegetationCamera(new Vector3(10, 60, 0));
            var draws = _owner.Renderer.Prepare(camera, true);
            Assert.AreEqual(1, draws.Count); Assert.AreEqual(2, draws[0].Count);
            _owner.EditableTypes[1].DrawDistance = 1;
            draws = _owner.Renderer.Prepare(camera, true);
            Assert.AreEqual(1, draws.Count); Assert.AreEqual(1, draws[0].Count);
            Assert.AreEqual(first, _owner.Instances[0].Id); Assert.AreEqual(second, _owner.Instances[1].Id);
        }

        [Test]
        public void CameraPreparation_DoesNotOverwriteAnotherCameraAndDisableClearsDerivedResources()
        {
            AddInstance(new Vector3(1, 2, 1)); AddInstance(new Vector3(20, 2, 1));
            var first = CreateVegetationCamera(new Vector3(10, 60, 0));
            var second = CreateVegetationCamera(new Vector3(1000, 60, 1000));
            var draws = _owner.Renderer.Prepare(first, true);
            var matrix = draws[0].Instances[0].ObjectToWorld;
            Assert.AreEqual(0, _owner.Renderer.Prepare(second, true).Count);
            Assert.AreEqual(1, draws.Count); Assert.AreEqual(2, draws[0].Count);
            Assert.AreEqual(matrix, draws[0].Instances[0].ObjectToWorld);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++) _owner.Renderer.Prepare(first, true);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            string authority = JsonUtility.ToJson(_owner.Data);
            _owner.Renderer.Disable(); Assert.AreEqual(0, _owner.Renderer.ResidentGpuBytes);
            Assert.AreEqual(0, draws.Count); Assert.AreEqual(authority, JsonUtility.ToJson(_owner.Data));
            _owner.Renderer.Invalidate(); Assert.AreEqual(1, _owner.Renderer.Prepare(first, true).Count);
        }

        [Test]
        public void DifferentSharedMaterials_RemainSeparateEvenWithTheSameMeshAndShader()
        {
            var material = new Material(_prefab.GetComponent<MeshRenderer>().sharedMaterial);
            _temporaryAssets.Add(material);
            var source = new GameObject("Alternative vegetation binding"); SceneManager.MoveGameObjectToScene(source, _scene);
            source.AddComponent<MeshFilter>().sharedMesh = _prefab.GetComponent<MeshFilter>().sharedMesh;
            source.AddComponent<MeshRenderer>().sharedMaterial = material;
            _owner.EditableTypes.Add(new CharacterTestVegetationType { Id = 2, Prefab = source });
            AddInstance(new Vector3(1, 2, 1)); AddInstance(new Vector3(20, 2, 1));
            var item = _owner.EditableInstances[1]; item.TypeId = 2; _owner.EditableInstances[1] = item;
            _owner.InvalidatePresentation();
            var draws = _owner.Renderer.Prepare(CreateVegetationCamera(new Vector3(10, 60, 0)), true);
            Assert.AreEqual(2, draws.Count); Assert.AreSame(draws[0].Mesh, draws[1].Mesh);
            Assert.AreNotSame(draws[0].Material, draws[1].Material);
        }

        [Test]
        public void GpuMatrixPair_PreservesNonuniformOwnerAndInstanceTransforms()
        {
            AddInstance(new Vector3(2, 2, 3));
            var item = _owner.EditableInstances[0]; item.Scale = new Vector3(2, 0.6f, 1.4f);
            item.Rotation = Quaternion.Euler(12, 37, 4); _owner.EditableInstances[0] = item;
            _owner.transform.localScale = new Vector3(1.1f, 0.8f, 1.3f); _owner.transform.rotation = Quaternion.Euler(0, 19, 0);
            _owner.InvalidatePresentation(); var camera = CreateVegetationCamera(new Vector3(10, 60, 0));
            var data = _owner.Renderer.Prepare(camera, true)[0].Instances[0];
            Assert.AreEqual(128, System.Runtime.InteropServices.Marshal.SizeOf<CharacterTestVegetationRenderer.GpuInstance>());
            var expected = _owner.transform.localToWorldMatrix * Matrix4x4.TRS(item.Position, item.Rotation.normalized, item.Scale);
            Assert.AreEqual(expected, data.ObjectToWorld);
            var normal = new Vector3(0.3f, 0.8f, 0.5f).normalized;
            Assert.Less(Vector3.Distance(expected.inverse.transpose.MultiplyVector(normal).normalized,
                data.WorldToObject.transpose.MultiplyVector(normal).normalized), 0.00001f);
        }

        private UnityCamera CreateVegetationCamera(Vector3 position)
        {
            var go = new GameObject("Vegetation acceptance camera"); SceneManager.MoveGameObjectToScene(go, _scene);
            var camera = go.AddComponent<UnityCamera>(); camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 128; camera.nearClipPlane = 0.1f; camera.farClipPlane = 512;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            return camera;
        }

        private int AddInstance(Vector3 position)
        {
            int id = _owner.AllocateId(); _owner.EditableInstances.Add(new CharacterTestVegetationInstance { Id = id, TypeId = 1, Position = position, Rotation = Quaternion.identity, Scale = Vector3.one });
            _owner.InvalidatePresentation(); return id;
        }
        private GameObject CreateGround()
        {
            var go = new GameObject("Ground"); SceneManager.MoveGameObjectToScene(go, _scene); go.transform.position = Vector3.up * 2;
            var mesh = new Mesh { vertices = new[] { new Vector3(-4, 0, -4), new Vector3(-4, 0, 4), new Vector3(4, 0, 4), new Vector3(4, 0, -4) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _temporaryAssets.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = _prefab.GetComponent<MeshRenderer>().sharedMaterial;
            return go;
        }
    }
}
