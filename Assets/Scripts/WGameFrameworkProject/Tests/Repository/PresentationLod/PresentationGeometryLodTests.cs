using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.PresentationLod
{
    public sealed class PresentationGeometryLodTests
    {
        private Scene _scene;
        private readonly List<CharacterTestVegetationSource> _vegetation = new List<CharacterTestVegetationSource>();
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        [SetUp] public void SetUp() { _scene = EditorSceneManager.NewPreviewScene(); }
        [TearDown] public void TearDown()
        {
            foreach (var source in _vegetation) source.Renderer.Disable(); _vegetation.Clear();
            foreach (var asset in _owned) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _owned.Clear(); EditorSceneManager.ClosePreviewScene(_scene);
        }
        private GameObject Object(string name)
        {
            var root = new GameObject(name); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, _scene); return root;
        }
        private UnityCamera Camera(float size)
        {
            var root = Object("Owned LOD camera"); var camera = root.AddComponent<UnityCamera>();
            camera.enabled = true; camera.orthographic = true; camera.orthographicSize = size;
            root.transform.position = new Vector3(0, 200, 0); root.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.nearClipPlane = .01f; camera.farClipPlane = 500; camera.aspect = 2; root.SetActive(true); return camera;
        }
        private CharacterTestVegetationSource Vegetation(int count)
        {
            var anchor = Object("Owned vegetation anchor");
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>(); _owned.Add(data);
            var owner = new CharacterTestVegetationSource(data, new MxFramework.Rendering.MxScenePresentationBinding {
                Data = data, Anchor = anchor.transform, OwnerId = "lod-fixture" }, _scene, null); _vegetation.Add(owner);
            const string path = "Assets/Art/WGame/Environment/Vegetation/Meadow/meadow_tall";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab"); Assert.NotNull(prefab);
            owner.EditableTypes.Add(new CharacterTestVegetationType { Prefab = prefab, DrawDistance = 1000,
                Lod1Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path + "_lod1.asset"),
                Lod2Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path + "_lod2.asset") });
            for (int i = 0; i < count; i++) owner.EditableInstances.Add(new CharacterTestVegetationInstance
            { Id = i + 1, TypeId = 1, Position = new Vector3((i % 250) * .4f - 50, 0, (i / 250) * .4f - 40),
                Rotation = Quaternion.identity, Scale = Vector3.one, Seed = i });
            return owner;
        }
        [Test] public void HysteresisPromotesAndReducesOnlyBeyondSeparateBoundaries()
        {
            Assert.AreEqual(0, PresentationGeometryLodPolicy.Select(.09f, .08f, .025f, -1, .15f));
            Assert.AreEqual(0, PresentationGeometryLodPolicy.Select(.071f, .08f, .025f, 0, .15f));
            Assert.AreEqual(1, PresentationGeometryLodPolicy.Select(.06f, .08f, .025f, 0, .15f));
            Assert.AreEqual(1, PresentationGeometryLodPolicy.Select(.085f, .08f, .025f, 1, .15f));
            Assert.AreEqual(0, PresentationGeometryLodPolicy.Select(.095f, .08f, .025f, 1, .15f));
            Assert.AreEqual(2, PresentationGeometryLodPolicy.Select(.01f, .08f, .025f, 1, .15f));
            Assert.AreEqual(0, PresentationGeometryLodPolicy.Select(float.NaN, .08f, .025f, 2, .15f));
        }
        [TestCase(10000, 2)] [TestCase(50000, 7)]
        public void RealReducedMeshCrossCellPackingKeeps8192Capacity(int count, int packets)
        {
            var owner = Vegetation(count); var draws = owner.Renderer.Prepare(Camera(100), true);
            Assert.AreEqual(packets, draws.Count); Assert.Greater(owner.Renderer.ChunkCount, 1);
            int total = 0; long triangles = 0;
            foreach (var draw in draws)
            { Assert.AreSame(owner.Types[0].Lod2Mesh, draw.Mesh); Assert.AreEqual(2, draw.Lod);
                Assert.LessOrEqual(draw.Count, 8192); total += draw.Count; triangles += (long)draw.Mesh.GetIndexCount(0) / 3 * draw.Count; }
            Assert.AreEqual(count, total); Assert.AreEqual(12L * count, triangles);
            TestContext.WriteLine("instances=" + count + "; draws=" + draws.Count + "; submittedTriangles=" + triangles + "; capacity=8192; CPU preparation only");
        }
        [Test] public void CamerasHaveIndependentTiersAndMatricesAndFallbackActuallyUses256()
        {
            var owner = Vegetation(10000); var near = Camera(3); var far = Camera(100);
            near.transform.position = new Vector3(0, 200, -32);
            var nearDraws = owner.Renderer.Prepare(near, true); Assert.Greater(nearDraws.Count, 0);
            Assert.AreEqual(0, nearDraws[0].Lod); Mesh original = nearDraws[0].Mesh;
            var matrix = nearDraws[0].Instances[0].ObjectToWorld;
            var farDraws = owner.Renderer.Prepare(far, true); Assert.AreEqual(2, farDraws[0].Lod);
            Assert.AreSame(original, nearDraws[0].Mesh); Assert.AreEqual(matrix, nearDraws[0].Instances[0].ObjectToWorld);
            var fallback = owner.Renderer.Prepare(far, false); Assert.AreEqual(40, fallback.Count);
            foreach (var draw in fallback) { Assert.IsFalse(draw.Indirect); Assert.LessOrEqual(draw.Count, 256); }
            // Format changes must repopulate both payloads, even with unchanged instance identity.
            Matrix4x4 fallbackMatrix = fallback[0].Matrices[0];
            Assert.AreEqual(fallbackMatrix, owner.Renderer.Prepare(far, true)[0].Instances[0].ObjectToWorld);
        }
        [Test] public void MissingOrRepeatedReducedMeshUsesOriginalGeometry()
        {
            var owner = Vegetation(20); owner.EditableTypes[0].Lod2Mesh = owner.Types[0].Lod1Mesh;
            var draws = owner.Renderer.Prepare(Camera(100), true);
            Assert.AreEqual(0, draws[0].Lod); Assert.AreSame(owner.Types[0].Prefab.GetComponent<MeshFilter>().sharedMesh, draws[0].Mesh);
        }
        [Test] public void WarmSelectionKeepsAuthorityAndAllocatesNoManagedMemory()
        {
            var owner = Vegetation(10000); var camera = Camera(100); string authority = JsonUtility.ToJson(owner.Data);
            owner.Renderer.Prepare(camera, true); owner.Renderer.Prepare(camera, true);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10; i++) owner.Renderer.Prepare(camera, true);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.AreEqual(authority, JsonUtility.ToJson(owner.Data));
            owner.Renderer.Disable(); Assert.AreEqual(0, owner.Renderer.ResidentGpuBytes);
            Assert.AreEqual(2, owner.Renderer.Prepare(camera, true).Count);
        }
        [Test] public void RegisteredHeadPadProjectsOnceAndRecoversFromCameraChangeAtTimeScaleZero()
        {
            var root = Object("Owned view"); root.SetActive(true);
            var view = new CharacterTestRuntimeCharacterView(new CharacterTestCharacterEntityId(901), root, null, null, null);
            var pad = CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out var ui);
            typeof(CharacterTestRuntimeCharacterView).GetField("_headPad", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, pad);
            var service = new CharacterTestPresentationLod(); var handle = service.Register(view);
            var camera = Camera(10); Assert.IsTrue(service.RegisterGameplayCamera(camera, PresentationLodCameraPurpose.GameplayBase));
            float scale = Time.timeScale;
            try
            {
                pad.BindPresentationLod(service, handle, 901); Time.timeScale = 0;
                pad.SyncWorldAnchor(new Vector3(0, 0, 0)); Assert.AreEqual(0, service.ProjectionComputations);
                service.BeginPresentation();
                Assert.IsTrue(service.TryProject(handle, camera, out var first)); long projections = service.ProjectionComputations;
                Assert.IsTrue(service.TryProject(handle, camera, out var same)); Assert.AreEqual(first, same);
                Assert.AreEqual(projections, service.ProjectionComputations);
                camera.transform.position += Vector3.right * 2; service.BeginPresentation();
                Assert.IsTrue(service.TryProject(handle, camera, out var moved)); Assert.AreNotEqual(first.x, moved.x);
                service.Unregister(handle, 901); Assert.IsFalse(service.TryProject(handle, camera, out _));
            }
            finally { Time.timeScale = scale; service.Clear(); pad.Dispose(); }
        }
        [Test] public void ActualMagicaGroupReceivesJoinedViewLodAndSessionStateWithoutChangingRequestedState()
        {
            var prefab = UnityEngine.Resources.Load<GameObject>("MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview");
            Assert.NotNull(prefab);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            var view = new CharacterTestRuntimeCharacterView(new CharacterTestCharacterEntityId(902), actor, null, null, null);
            var cloth = new MxFramework.CharacterCloth.CharacterClothPresentationService();
            var lod = new CharacterTestPresentationLod();
            try
            {
                view.AttachClothPresentation(cloth); view.AttachPresentationLod(lod);
                Assert.Greater(cloth.GroupCount, 0); Assert.IsTrue(view.HasPresentationCloth);
                MonoBehaviour controller = null;
                foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component.GetType().Name == "MagicaCharacterClothController") { controller = component; break; }
                Assert.NotNull(controller);
                foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component.GetType().Name == "MagicaCharacterClothSystem")
                        component.GetType().GetMethod("RefreshControllers").Invoke(component, null);
                var state = controller.GetType().GetProperty("EffectiveState"); var requested = controller.GetType().GetProperty("RequestedState");
                object local = requested.GetValue(controller);
                view.SetClothLodPaused(true); Assert.AreEqual("Paused", state.GetValue(controller).ToString());
                cloth.SetEnabled(false, true); view.SetClothLodPaused(false); Assert.AreEqual("DisabledKeep", state.GetValue(controller).ToString());
                cloth.SetPaused(true); cloth.SetEnabled(true); Assert.AreEqual("Paused", state.GetValue(controller).ToString());
                cloth.SetPaused(false); Assert.AreEqual(local, requested.GetValue(controller));
                Assert.AreEqual(local.ToString(), state.GetValue(controller).ToString());
                view.ResetClothAfterTeleport(); Assert.AreEqual(local, requested.GetValue(controller));
            }
            finally { lod.Clear(); cloth.Clear(false); }
            // This checks the actual Unity/Magica adapter state in EditMode, not solver time or animated cloth appearance.
        }
        [TestCase(false)] [TestCase(true)]
        public void GeometryConsumerUsesDistinctNativeMeshesAndProtectsUnprovenSkinnedShadowPose(bool skinnedCaster)
        {
            var root = Object("Owned model"); var group = root.AddComponent<LODGroup>(); var levels = new LOD[3];
            var animator = root.AddComponent<Animator>(); animator.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                var child = Object("Geometry tier " + i); child.transform.SetParent(root.transform, false); child.SetActive(true);
                int sides = 12 >> i; var vertices = new Vector3[sides + 1]; var indices = new int[sides * 3];
                for (int v = 0; v < sides; v++)
                { float angle = 2 * Mathf.PI * v / sides; vertices[v + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    indices[v * 3] = 0; indices[v * 3 + 1] = v + 1; indices[v * 3 + 2] = (v + 1) % sides + 1; }
                var mesh = new Mesh { vertices = vertices, triangles = indices };
                mesh.RecalculateBounds(); _owned.Add(mesh);
                Renderer renderer;
                if (skinnedCaster) { var skin = child.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = mesh; renderer = skin; }
                else { child.AddComponent<MeshFilter>().sharedMesh = mesh; renderer = child.AddComponent<MeshRenderer>(); }
                renderer.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                levels[i] = new LOD(i == 0 ? .08f : i == 1 ? .025f : 0, new Renderer[] { renderer });
            }
            group.SetLODs(levels); group.RecalculateBounds(); root.SetActive(true);
            var consumer = new PresentationGeometryLodConsumer(); consumer.Bind(root);
            Assert.AreEqual(1, consumer.Count); var far = Camera(100); var near = Camera(2);
            consumer.Begin(false); consumer.Observe(1, far.worldToCameraMatrix, far.projectionMatrix, true); consumer.Apply();
            Assert.AreEqual(1, consumer.Transitions); Assert.AreEqual(skinnedCaster ? 0 : 2, consumer.AppliedTierAt(0));
            consumer.Begin(false); consumer.Observe(1, far.worldToCameraMatrix, far.projectionMatrix, true);
            consumer.Observe(2, near.worldToCameraMatrix, near.projectionMatrix, true); consumer.Apply();
            Assert.AreEqual(skinnedCaster ? 1 : 2, consumer.Transitions); Assert.AreEqual(0, consumer.AppliedTierAt(0));
            Assert.IsFalse(animator.enabled); Assert.AreEqual(ShadowCastingMode.On, levels[0].renderers[0].shadowCastingMode);
            foreach (var level in levels) Assert.IsTrue(level.renderers[0].gameObject.activeSelf);
            consumer.RemoveCamera(2); consumer.Clear(); Assert.AreEqual(0, consumer.Count);
        }
    }
}
