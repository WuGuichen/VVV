using System;
using System.Collections;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Rendering
{
    public sealed class CharacterTestVegetationGpuClassificationTests
    {
        private Scene _scene;
        private CharacterTestVegetationSource _owner;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        private readonly List<CharacterTestVegetationGpuClassification> _classifiers = new List<CharacterTestVegetationGpuClassification>();
        private UnityCamera _camera;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsIndirectArgumentsBuffer || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("Native GPU classification acceptance requires Compute, indirect arguments and asynchronous readback.");
            _scene = EditorSceneManager.NewPreviewScene();
            var root = Object("GPU classification fixture");
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>(); _assets.Add(data);
            _owner = new CharacterTestVegetationSource(data, new MxFramework.Rendering.MxScenePresentationBinding {
                Data = data, Anchor = root.transform, OwnerId = "gpu-fixture", Enabled = false }, _scene, null);
            // No global render callbacks and no mutation of the current user scene.

            var prefab = Object("Fixture presentation");
            prefab.AddComponent<MeshFilter>().sharedMesh = Mesh(3);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterTestVegetationAuthoring.GrassPrefabPath);
            Assert.NotNull(source);
            var material = source.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.IsTrue(material.enableInstancing);
            prefab.AddComponent<MeshRenderer>().sharedMaterial = material;
            _owner.EditableTypes.Add(new CharacterTestVegetationType { Prefab = prefab, Lod1Mesh = Mesh(2), Lod2Mesh = Mesh(1),
                DrawDistance = 1000, Lod0ScreenHeight = .08f, Lod1ScreenHeight = .025f });
            _camera = Camera(new Vector3(0, 60, 0), true, 60);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var classifier in _classifiers) classifier.Dispose();
            _classifiers.Clear();
            if (_owner != null) _owner.Renderer.Disable();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [UnityTest]
        public IEnumerator FiftyThousandRecordsCrossCellsWithoutCapacityLossAndStableCameraDoesNotUploadOrScan()
        {
            Populate(50000); var gpu = Create();
            Assert.Greater(_owner.Renderer.Batches.Count, 1);
            gpu.Submit(_camera, 0); gpu.End(_camera);
            Assert.AreEqual(50000, gpu.LastUploadedInstances); Assert.IsTrue(gpu.LastDispatched);
            yield return Compare(gpu, _camera, 50000);
            long classifications = gpu.Classifications, bytes = gpu.ResidentBytes;
            for (int i = 0; i < 5; i++) { gpu.Submit(_camera, 0); gpu.End(_camera); }
            Assert.AreEqual(0, gpu.LastUploadedInstances); Assert.IsFalse(gpu.LastDispatched);
            Assert.AreEqual(classifications, gpu.Classifications); Assert.AreEqual(bytes, gpu.ResidentBytes);
            gpu.Dispose(); Assert.AreEqual(0, gpu.ResidentBytes);
        }

        [UnityTest]
        public IEnumerator PerspectiveAndOrthographicFrustumAndDistanceMatchCpuInstanceReference()
        {
            Populate(4096); _owner.EditableTypes[0].DrawDistance = 80;
            // A centered square actually enters the perspective view; the generic strip fixture sits behind it.
            for (int i = 0; i < _owner.EditableInstances.Count; i++)
            {
                var instance = _owner.EditableInstances[i];
                instance.Position = new Vector3((i % 64 - 32) * .4f, 0, (i / 64 - 32) * .4f);
                _owner.EditableInstances[i] = instance;
            }
            _owner.InvalidatePresentation();
            var gpu = Create();
            var perspective = Camera(new Vector3(5, 8, -22), false, 60);
            perspective.transform.LookAt(new Vector3(0, .6f, 0));
            int partial = 0;
            foreach (var draw in _owner.Renderer.PrepareInstanceReference(perspective, true)) partial += draw.Count;
            Assert.That(partial, Is.InRange(1, 4095), "The perspective comparison must include visible and rejected resident records.");
            foreach (var camera in new[] { perspective, _camera })
            { gpu.Submit(camera, 0); gpu.End(camera); yield return Compare(gpu, camera, camera == perspective ? partial : 4096); }
            perspective.transform.position = new Vector3(200, 8, -22);
            gpu.Submit(perspective, 0); gpu.End(perspective);
            yield return Compare(gpu, perspective, 0);
        }

        [UnityTest]
        public IEnumerator CamerasKeepIndependentLodHistoryAcrossHysteresisAndProjectionChanges()
        {
            Populate(1); var gpu = Create();
            var near = Camera(new Vector3(0, 60, 0), true, 5);
            var far = Camera(new Vector3(0, 60, 0), true, 90);
            // Enter from opposite tiers, then enter the same threshold band.
            foreach (var camera in new[] { near, far })
            { gpu.Submit(camera, 0); gpu.End(camera); yield return Compare(gpu, camera, 1); }
            float radius = _owner.Renderer.Batches[0].Radii[0];
            near.orthographicSize = far.orthographicSize = radius / .08f;
            foreach (var camera in new[] { near, far })
            { gpu.Submit(camera, 0); gpu.End(camera); yield return Compare(gpu, camera, 1); }
            Assert.AreEqual(0, _owner.Renderer.PrepareInstanceReference(near, true)[0].Lod);
            Assert.AreEqual(1, _owner.Renderer.PrepareInstanceReference(far, true)[0].Lod);
        }

        [UnityTest]
        public IEnumerator MissingOrRepeatedLodMeshRetainsOriginalGeometry()
        {
            Populate(300); var type = _owner.EditableTypes[0];
            type.Lod1Mesh = type.Prefab.GetComponent<MeshFilter>().sharedMesh;
            var gpu = Create(); Assert.AreEqual(1, gpu.GroupCount);
            gpu.Submit(_camera, 0); gpu.End(_camera);
            yield return Compare(gpu, _camera, 300);
        }

        [UnityTest]
        public IEnumerator NestedSubmissionsOwnArgumentsAndVisibilityOutputs()
        {
            Populate(300); var gpu = Create();
            gpu.Submit(_camera, 0); long firstBytes = gpu.ResidentBytes;
            _camera.transform.position += Vector3.right * 200;
            gpu.Submit(_camera, 0); Assert.Greater(gpu.ResidentBytes, firstBytes);
            // Snapshot explicitly belongs to outer invocation zero; inner output must not overwrite it.
            CharacterTestVegetationGpuClassification.Snapshot snapshot = null;
            Assert.IsTrue(gpu.RequestSnapshot(_camera, result => snapshot = result));
            yield return Await(() => snapshot != null);
            Assert.IsNull(snapshot.Error); Assert.AreEqual(300, snapshot.VisibleInstances);
            gpu.End(_camera); gpu.End(_camera);
            gpu.Submit(_camera, 0); gpu.End(_camera);
            yield return Compare(gpu, _camera, 0);
        }

        [UnityTest]
        public IEnumerator CameraDeletionAndOwnerReleaseDoNotLeaveResidentOutputsOrUnsafeReadback()
        {
            Populate(300); var gpu = Create(); var transient = Camera(new Vector3(0, 60, 0), true, 60);
            gpu.Submit(_camera, 0); gpu.End(_camera); long singleBytes = gpu.ResidentBytes;
            gpu.Submit(transient, 0); gpu.End(transient); Assert.Greater(gpu.ResidentBytes, singleBytes);
            UnityEngine.Object.DestroyImmediate(transient.gameObject);
            gpu.Submit(_camera, 0); gpu.End(_camera); Assert.AreEqual(singleBytes, gpu.ResidentBytes);
            CharacterTestVegetationGpuClassification.Snapshot snapshot = null;
            Assert.IsTrue(gpu.RequestSnapshot(_camera, result => snapshot = result)); gpu.Dispose();
            yield return Await(() => snapshot != null);
            Assert.IsNotEmpty(snapshot.Error); Assert.AreEqual(0, gpu.ResidentBytes);
        }

        [UnityTest]
        public IEnumerator LayoutInvalidationRebuildsResidentRecordsFromNewAuthority()
        {
            Populate(300); var first = Create(); first.Submit(_camera, 0); first.End(_camera);
            yield return Compare(first, _camera, 300);
            first.Dispose(); _owner.EditableInstances.RemoveRange(200, 100); _owner.InvalidatePresentation();
            var second = Create(); second.Submit(_camera, 0); second.End(_camera);
            Assert.AreEqual(200, second.RecordCount); Assert.AreEqual(200, second.LastUploadedInstances);
            yield return Compare(second, _camera, 200);
        }

        private IEnumerator Compare(CharacterTestVegetationGpuClassification gpu, UnityCamera camera, int expected = -1)
        {
            int total = 0; long triangles = 0; var tiers = new int[3];
            foreach (var draw in _owner.Renderer.PrepareInstanceReference(camera, true))
            { total += draw.Count; tiers[draw.Lod] += draw.Count; triangles += draw.Mesh.GetIndexCount(0) / 3 * (long)draw.Count; }
            if (expected >= 0) Assert.AreEqual(expected, total);
            CharacterTestVegetationGpuClassification.Snapshot snapshot = null;
            Assert.IsTrue(gpu.RequestSnapshot(camera, result => snapshot = result, true));
            yield return Await(() => snapshot != null);
            Assert.IsNull(snapshot.Error); Assert.AreEqual(total, snapshot.VisibleInstances);
            CollectionAssert.AreEqual(tiers, snapshot.LodInstances); Assert.AreEqual(triangles, snapshot.SubmittedTriangles);
            foreach (int count in snapshot.PacketCounts) Assert.That(count, Is.InRange(0, _owner.DrawBatchSize));
            var identities = new HashSet<uint>();
            foreach (uint index in snapshot.VisibleRecordIndices)
            { Assert.Less(index, (uint)gpu.RecordCount); Assert.IsTrue(identities.Add(index), "A visible resident record was submitted twice."); }
            Assert.AreEqual(total, identities.Count);
        }
        private static IEnumerator Await(Func<bool> done)
        {
            double until = EditorApplication.timeSinceStartup + 30;
            while (!done() && EditorApplication.timeSinceStartup < until) yield return null;
            Assert.IsTrue(done(), "Asynchronous GPU diagnostic did not complete within 30 seconds.");
        }
        private CharacterTestVegetationGpuClassification Create()
        {
            Assert.IsTrue(CharacterTestVegetationGpuClassification.TryCreate(_owner.Renderer.Batches, _owner.DrawBatchSize, out var gpu, out string issue), issue);
            _classifiers.Add(gpu); return gpu;
        }
        private void Populate(int count)
        {
            for (int i = 0; i < count; i++) _owner.EditableInstances.Add(new CharacterTestVegetationInstance {
                Id = _owner.AllocateId(), TypeId = 1, Position = new Vector3((i % 224 - 112) * .4f, 0, (i / 224 - 112) * .4f),
                Rotation = Quaternion.identity, Scale = Vector3.one, Seed = i });
            if (count == 1) { var item = _owner.EditableInstances[0]; item.Position = Vector3.zero; _owner.EditableInstances[0] = item; }
            _owner.InvalidatePresentation();
        }
        private GameObject Object(string name)
        { var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, _scene); return item; }
        private UnityCamera Camera(Vector3 position, bool orthographic, float size)
        {
            var camera = Object("Owned GPU camera").AddComponent<UnityCamera>(); camera.enabled = false;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            camera.orthographic = orthographic; camera.orthographicSize = size; camera.aspect = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000; camera.fieldOfView = 55; return camera;
        }
        private Mesh Mesh(int triangles)
        {
            var mesh = new Mesh { name = "GPU fixture " + triangles };
            mesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(0, 1.2f, 0) };
            var indices = new int[triangles * 3]; for (int i = 0; i < indices.Length; i++) indices[i] = i % 3;
            mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); _assets.Add(mesh); return mesh;
        }
    }
}
