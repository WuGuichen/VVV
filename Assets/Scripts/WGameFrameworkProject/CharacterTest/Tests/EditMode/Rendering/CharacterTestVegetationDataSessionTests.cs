using System;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using System.Collections.Generic;

namespace MxFramework.Tests.Rendering
{
    public sealed class CharacterTestVegetationDataSessionTests
    {
        private Scene _scene;
        private Scene _previous;
        private GameObject _anchor;
        private VegetationSessionProbeData _probe;
        private MxSceneRenderingSession _session;
        [SetUp]
        public void SetUp()
        {
            _previous = SceneManager.GetActiveScene();
            // Test Runner owns an untitled bootstrap scene. Preview scenes isolate
            // these transient objects without replacing or saving any user scene.
            _scene = EditorSceneManager.NewPreviewScene();
            _anchor = new GameObject("vegetation binding test"); SceneManager.MoveGameObjectToScene(_anchor, _scene);
            _probe = ScriptableObject.CreateInstance<VegetationSessionProbeData>();
        }
        [TearDown]
        public void TearDown()
        {
            _session?.Dispose(); UnityEngine.Object.DestroyImmediate(_probe);
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
        }
        private MxScenePresentationBinding Binding(string id = "owner1") =>
            new MxScenePresentationBinding { OwnerId = id, Data = _probe, Anchor = _anchor.transform };
        [Test]
        public void SameBindingAndOwnerAreCreatedOnceAndDisposeIsIdempotent()
        {
            var binding = Binding(); var duplicate = Binding();
            var c = new MxSceneRenderingConfiguration { Presentations = new[] { binding, binding, duplicate } };
            _session = new MxSceneRenderingSession(() => c, _scene);
            _session.Tick(); _session.Tick();
            Assert.AreEqual(1, _probe.Created); Assert.AreEqual(1, _session.PresentationCount);
            _session.Dispose(); _session.Dispose(); Assert.AreEqual(1, _probe.Disposed);
        }
        [Test]
        public void DisableReenableAndBindingRemovalBalanceResources()
        {
            var binding = Binding(); var c = new MxSceneRenderingConfiguration { Presentations = new[] { binding } };
            _session = new MxSceneRenderingSession(() => c, _scene);
            for (int i = 0; i < 3; i++) { binding.Enabled = false; _session.Tick(); binding.Enabled = true; _session.Tick(); }
            Assert.AreEqual(4, _probe.Created); Assert.AreEqual(3, _probe.Disposed);
            c.Presentations = Array.Empty<MxScenePresentationBinding>(); _session.Tick();
            Assert.AreEqual(_probe.Created, _probe.Disposed); Assert.AreEqual(0, _session.PresentationCount);
        }
        [Test]
        public void SceneUnloadReleasesSessionConsumers()
        {
            var c = new MxSceneRenderingConfiguration { Presentations = new[] { Binding() } };
            _session = new MxSceneRenderingSession(() => c, _scene);
            EditorSceneManager.ClosePreviewScene(_scene); _session.Tick();
            Assert.AreEqual(1, _probe.Disposed); Assert.AreEqual(0, _session.PresentationCount);
        }
        [Test]
        public void ForeignSceneAnchorIsNotRegistered()
        {
            var binding = Binding(); var foreign = new GameObject("foreign anchor");
            SceneManager.MoveGameObjectToScene(foreign, _previous);
            try
            {
                binding.Anchor = foreign.transform;
                _session = new MxSceneRenderingSession(() => new MxSceneRenderingConfiguration { Presentations = new[] { binding } }, _scene);
                Assert.AreEqual(0, _probe.Created);
            }
            finally { UnityEngine.Object.DestroyImmediate(foreign); }
        }
        [Test]
        public void DataClonePreservesRecordsAndCountersWithoutAliases()
        {
            var original = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            original.EditableTypes.Add(new CharacterTestVegetationType { Id = 3, Name = "retained",
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/WGame/Environment/Vegetation/Generated/BasicGrassClump.prefab") });
            original.BeginStrokeSeed(219);
            var instance = new CharacterTestVegetationInstance { Id = original.AllocateId(), TypeId = 3,
                Position = new Vector3(.321f, -.023f, 17.123f), Rotation = Quaternion.Euler(0, 31.7f, 0), Scale = new Vector3(.7f, 1.2f, .8f), Seed = 1987 };
            original.EditableInstances.Add(instance);
            var data = UnityEngine.Object.Instantiate(original);
            try
            {
                Assert.AreEqual(JsonUtility.ToJson(original), JsonUtility.ToJson(data));
                Assert.AreEqual(original.NextInstanceId, data.NextInstanceId); Assert.AreEqual(original.StrokeOrdinal, data.StrokeOrdinal);
                original.EditableTypes[0].Name = "changed original"; original.EditableInstances.Clear();
                Assert.AreEqual("retained", data.Types[0].Name); Assert.AreEqual(1, data.Instances.Count);
                Assert.AreEqual(instance.Position, data.Instances[0].Position); Assert.AreEqual(instance.Rotation, data.Instances[0].Rotation);
                Assert.AreEqual(instance.Scale, data.Instances[0].Scale); Assert.AreEqual(instance.Seed, data.Instances[0].Seed);
            }
            finally { UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(original); }
        }
        [Test]
        public void InstanceUndoRedoUsesDataAssetAsAuthority()
        {
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            try
            {
                int id = data.AllocateId();
                data.EditableInstances.Add(new CharacterTestVegetationInstance { Id = id, TypeId = 1, Rotation = Quaternion.identity, Scale = Vector3.one, Seed = 289 });
                Undo.IncrementCurrentGroup();
                Undo.RegisterCompleteObjectUndo(data, "vegetation data authority test");
                data.EditableInstances.Clear(); data.InvalidatePresentation();
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.AreEqual(1, data.Instances.Count); Assert.AreEqual(289, data.Instances[0].Seed);
                Undo.PerformRedo(); Assert.AreEqual(0, data.Instances.Count);
            }
            finally { Undo.ClearUndo(data); UnityEngine.Object.DestroyImmediate(data); }
        }
        [Test]
        public void RemovingReferencedTypeIsRejectedAndUnusedTypeRemovalSupportsUndo()
        {
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            var source = new CharacterTestVegetationSource(data, new MxScenePresentationBinding { Data = data, Anchor = _anchor.transform }, _scene, null);
            data.EditableTypes.Add(new CharacterTestVegetationType { Id = 1 });
            data.EditableTypes.Add(new CharacterTestVegetationType { Id = 2 });
            data.EditableInstances.Add(new CharacterTestVegetationInstance { Id = data.AllocateId(), TypeId = 1,
                Rotation = Quaternion.identity, Scale = Vector3.one });
            try
            {
                string before = JsonUtility.ToJson(data);
                Assert.Throws<InvalidOperationException>(() => CharacterTestVegetationAuthoring.RemoveType(source, 1));
                Assert.AreEqual(before, JsonUtility.ToJson(data));
                Assert.IsTrue(CharacterTestVegetationAuthoring.RemoveType(source, 2)); Assert.AreEqual(1, data.Types.Count);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(data));
            }
            finally { Undo.ClearUndo(data); UnityEngine.Object.DestroyImmediate(data); }
        }
        [Test]
        public void CreatingDataForUnsavedSceneDoesNotAllocateOrBind()
        {
            int roots = _scene.rootCount;
            Assert.Throws<InvalidOperationException>(() => CharacterTestVegetationAssetAuthoring.Create(_scene));
            Assert.AreEqual(roots, _scene.rootCount); Assert.IsNull(CharacterTestVegetationAssetAuthoring.Find(_scene));
        }
        [Test]
        public void InvalidTypeLodSettingsRejectConsumerCreationWithoutChangingAuthority()
        {
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            data.EditableTypes.Add(new CharacterTestVegetationType { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/WGame/Environment/Vegetation/Generated/BasicGrassClump.prefab"), LodHysteresis = .6f });
            try
            {
                string before = JsonUtility.ToJson(data);
                Assert.IsFalse(data.TryValidate(out string issue)); Assert.IsNotEmpty(issue);
                Assert.Throws<InvalidOperationException>(() => data.CreateConsumer(new MxScenePresentationBinding { Data = data,
                    Anchor = _anchor.transform }, _scene, false));
                Assert.AreEqual(before, JsonUtility.ToJson(data));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }
        [Test]
        public void AssetBrushGesturesAndSelectedAdjustmentPreserveUndoAuthority()
        {
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            data.EditableTypes.Add(new CharacterTestVegetationType { Id = 1,
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/WGame/Environment/Vegetation/Generated/BasicGrassClump.prefab") });
            var binding = new MxScenePresentationBinding { Data = data, Anchor = _anchor.transform, OwnerId = "asset-brush" };
            var source = new CharacterTestVegetationSource(data, binding, _scene, null);
            var settings = new CharacterTestVegetationBrushSettings { Radius = 2, Density = 1, MinimumSpacing = .4f, Seed = 7654 };
            try
            {
                string empty = JsonUtility.ToJson(data);
                using (var stroke = new CharacterTestVegetationBrushStroke(source, settings, new BrushPlane()))
                { stroke.Paint(Vector3.zero, false); stroke.Paint(new Vector3(2, 0, 0), false); stroke.Complete(); }
                Assert.Greater(data.Instances.Count, 10);
                string painted = JsonUtility.ToJson(data);
                Undo.PerformUndo(); Assert.AreEqual(empty, JsonUtility.ToJson(data));
                Undo.PerformRedo(); Assert.AreEqual(painted, JsonUtility.ToJson(data));
                using (var stroke = new CharacterTestVegetationBrushStroke(source, settings, new BrushPlane()))
                { stroke.Paint(new Vector3(20, 0, 0), true); stroke.Complete(cancel: true); }
                Assert.AreEqual(painted, JsonUtility.ToJson(data), "Cancelled point placement must restore IDs and stroke seeds too.");
                var untouched = data.Instances[1];
                Assert.AreEqual(1, CharacterTestVegetationAuthoring.Adjust(source, new HashSet<int> { data.Instances[0].Id }, 1.5f, 25));
                Assert.AreEqual(untouched.Scale, data.Instances[1].Scale);
                Assert.AreEqual(untouched.Rotation, data.Instances[1].Rotation);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(painted, JsonUtility.ToJson(data));
                using (var stroke = new CharacterTestVegetationBrushStroke(source, settings, new BrushPlane()))
                { stroke.Erase(Vector3.zero); stroke.Complete(); }
                string erased = JsonUtility.ToJson(data); Assert.AreNotEqual(painted, erased);
                Undo.PerformUndo(); Assert.AreEqual(painted, JsonUtility.ToJson(data));
                Undo.PerformRedo(); Assert.AreEqual(erased, JsonUtility.ToJson(data));
                Assert.AreEqual(0, _anchor.GetComponents<MonoBehaviour>().Length);
                Assert.AreEqual(0, _anchor.transform.childCount);
            }
            finally { Undo.ClearUndo(data); UnityEngine.Object.DestroyImmediate(data); }
        }
        private sealed class BrushPlane : IVegetationSurfaceProvider
        {
            public bool TryProject(Vector3 candidate, out VegetationSurfaceHit hit)
            { hit = new VegetationSurfaceHit(new Vector3(candidate.x, 0, candidate.z), Vector3.up); return true; }
        }
        [Test]
        public void RealVegetationConsumerReleasesBuffersAcrossRepeatedActivation()
        {
            if (!SystemInfo.supportsInstancing || !SystemInfo.supportsIndirectArgumentsBuffer || !SystemInfo.supportsComputeShaders)
                Assert.Ignore("Requires native indirect-capable graphics device.");
            var data = ScriptableObject.CreateInstance<CharacterTestVegetationData>();
            var cameraRoot = new GameObject("owned resource test camera"); SceneManager.MoveGameObjectToScene(cameraRoot, _scene);
            var camera = cameraRoot.AddComponent<UnityEngine.Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 8, -12); camera.transform.LookAt(Vector3.zero);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/WGame/Environment/Vegetation/Meadow/meadow_short.prefab");
            Assert.NotNull(prefab);
            data.EditableTypes.Add(new CharacterTestVegetationType { Id = 1, Prefab = prefab,
                Lod1Mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/WGame/Environment/Vegetation/Meadow/meadow_short_lod1.asset"),
                Lod2Mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/WGame/Environment/Vegetation/Meadow/meadow_short_lod2.asset") });
            for (int i = 0; i < 64; i++) data.EditableInstances.Add(new CharacterTestVegetationInstance
            { Id = data.AllocateId(), TypeId = 1, Position = new Vector3(i % 8 - 4, 0, i / 8 - 4), Rotation = Quaternion.identity, Scale = Vector3.one, Seed = i * 17 });
            var binding = new MxScenePresentationBinding { Data = data, Anchor = _anchor.transform, OwnerId = "real-resources" };
            var source = new CharacterTestVegetationSource(data, binding, _scene, null);
            var consumer = new CharacterTestVegetationConsumer(source);
            string authority = EditorJsonUtility.ToJson(data);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    binding.Enabled = true; consumer.Tick(); consumer.BeginCamera(camera); consumer.EndCamera(camera);
                    Assert.Greater(source.Renderer.ResidentGpuBytes, 0, "Native buffers must actually be created.");
                    binding.Enabled = false; consumer.Tick(); Assert.AreEqual(0, source.Renderer.ResidentGpuBytes);
                }
                consumer.Dispose(); consumer.Dispose(); Assert.AreEqual(0, source.Renderer.ResidentGpuBytes);
                Assert.AreEqual(authority, EditorJsonUtility.ToJson(data), "Derived rendering must not mutate persisted authority.");
            }
            finally { consumer.Dispose(); UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(cameraRoot); }
        }
    }
    public sealed class VegetationSessionProbeData : MxScenePresentationData
    {
        [NonSerialized] public int Created, Disposed;
        public override IMxScenePresentationConsumer CreateConsumer(MxScenePresentationBinding binding, Scene scene, bool editorPreview)
        { Created++; return new Probe(this); }
        private sealed class Probe : IMxScenePresentationConsumer
        {
            private readonly VegetationSessionProbeData _owner; private bool _disposed;
            internal Probe(VegetationSessionProbeData owner) { _owner = owner; }
            public void Tick() { }
            public void BeginCamera(UnityEngine.Camera camera) { }
            public void EndCamera(UnityEngine.Camera camera) { }
            public void Dispose() { if (_disposed) return; _disposed = true; _owner.Disposed++; }
        }
    }
}
