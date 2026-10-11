using System.Collections;
using NUnit.Framework;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Vfx
{
    public sealed class UnityBladeTrailBackendTests
    {
        private GameObject _view;
        private UnityBladeTrailBackend _backend;
        private Source _source;
        private UnityBladeTrailBudget _budget;
        [SetUp] public void SetUp()
        {
            _view = new GameObject("TrailTest"); _backend = _view.AddComponent<UnityBladeTrailBackend>();
            _source = new Source(); _budget = new UnityBladeTrailBudget(1);
            _backend.BindSource(_source, _budget); _backend.ReinitializeAndPlay(null);
            _backend.TickPresentation(0f);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_view); }
        [Test] public void DualBladeEndpoints_UseCurvedSubsamples_AndPreserveWorldHistory()
        {
            _source.Time = 0.04; _backend.TickPresentation(0.04f);
            Assert.Greater(_source.SubSamples, 0);
            Mesh mesh = _view.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            // Fake blade does a real half-turn through the sampled animation, not a linear chord.
            Assert.Greater(vertices[vertices.Length / 2 | 1].z, 0.7f);
            Vector3 oldWorldTip = _view.transform.TransformPoint(vertices[1]);
            _view.transform.position = new Vector3(4,0,0); _backend.TickPresentation(0f);
            Assert.Less(Vector3.Distance(oldWorldTip, _view.transform.TransformPoint(mesh.vertices[1])), 0.00001f);
            Assert.AreEqual((_backend.SliceCount - 1) * 6, mesh.triangles.Length);
        }
        [Test] public void Exit_StopsEmission_FadesBeforePoolRelease()
        {
            _source.Time = 0.02; _backend.TickPresentation(0.02f);
            _backend.BeginPresentationExit(VfxStopMode.Dissipate);
            int samples = _source.SubSamples; Assert.False(_backend.IsPresentationComplete);
            _backend.TickPresentation(0.05f);
            Assert.False(_backend.IsPresentationComplete); Assert.AreEqual(samples, _source.SubSamples);
            Assert.AreEqual(1, _budget.LeasedCount);
            _backend.TickPresentation(0.06f); Assert.True(_backend.IsPresentationComplete);
            _backend.StopAndClear(); Assert.AreEqual(0, _budget.LeasedCount); Assert.True(_source.Disposed);
        }
        [TestCase(true)] [TestCase(false)] public void RewindOrTeleport_BreaksOldGeometry(bool rewind)
        {
            _source.Time = 0.04; _backend.TickPresentation(0.04f); Assert.Greater(_backend.SliceCount, 1);
            if (rewind) _source.Time = 0.01; else { _source.Owner = new Vector3(5,0,0); _source.Time += 0.01; }
            _backend.TickPresentation(0.01f); Assert.AreEqual(1, _backend.SliceCount);
        }
        [Test] public void MissingIntermediateOrExcessiveGap_DoesNotInventASpinChord()
        {
            _source.SamplingAvailable = false; _source.Time = 0.04; _backend.TickPresentation(0.04f);
            Assert.AreEqual(1, _backend.SliceCount);
            _source.SamplingAvailable = true; _source.Time = 0.4; _backend.TickPresentation(0.36f);
            Assert.AreEqual(1, _backend.SliceCount);
        }
        [Test] public void MissingWeaponOrReplacedAction_StopsSafelyAndDoesNotReconnect()
        {
            _source.Time = 0.02; _backend.TickPresentation(0.02f);
            _source.Available = false; _backend.TickPresentation(0.01f);
            int slices = _backend.SliceCount;
            _source.Available = true; _source.Time = 0.03; _backend.TickPresentation(0f);
            Assert.AreEqual(slices, _backend.SliceCount);
            _backend.TickPresentation(0.15f); Assert.True(_backend.IsPresentationComplete);
        }
        [Test] public void BudgetIncludesResiduals_AndRebindClearsLeaseAndGeometry()
        {
            var secondView = new GameObject("Trail2");
            try
            {
                var second = secondView.AddComponent<UnityBladeTrailBackend>(); var rejected = new Source();
                second.BindSource(rejected,_budget); second.ReinitializeAndPlay(null);
                Assert.True(rejected.Disposed); Assert.True(second.IsPresentationComplete);
                _backend.BeginPresentationExit(VfxStopMode.Immediate); Assert.AreEqual(0,_budget.LeasedCount);
                var accepted = new Source(); second.BindSource(accepted,_budget); second.ReinitializeAndPlay(null);
                second.TickPresentation(0); Assert.AreEqual(1,second.SliceCount);
                second.StopAndClear(); Assert.AreEqual(0,_budget.LeasedCount);
            }
            finally { Object.DestroyImmediate(secondView); }
        }
        [Test] public void AnimationSubsample_RestoresOwnerAndSkeletonLocalTransforms()
        {
            var owner = new GameObject("PoseOwner");
            try
            {
                var bone = new GameObject("Bone").transform; bone.SetParent(owner.transform,false);
                var root = new GameObject("Root").transform; root.SetParent(bone,false);
                var tip = new GameObject("Tip").transform; tip.SetParent(bone,false); tip.localPosition = Vector3.right;
                owner.transform.position = new Vector3(10,0,0); bone.localRotation = Quaternion.Euler(0,15,0);
                Quaternion saved = bone.localRotation;
                var sampler = new UnityBladeTrailTransformSampler(owner.transform,root,tip,new Evaluator(bone));
                Assert.True(sampler.TrySample(0.5,new Vector3(20,0,0),Quaternion.identity,out Vector3 sampledRoot,out Vector3 sampledTip));
                Assert.Less(Vector3.Distance(sampledRoot,new Vector3(20,0,0)),0.00001f);
                Assert.Greater(Mathf.Abs(sampledTip.z),0.9f);
                Assert.Less(Vector3.Distance(owner.transform.position,new Vector3(10,0,0)),0.00001f);
                Assert.Less(Quaternion.Angle(saved,bone.localRotation),0.00001f);
                Assert.AreEqual(Vector3.right,tip.localPosition);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void HugeOrNonFiniteActionClock_BreaksHistoryBeforeIntegerStepConversion()
        {
            _source.Time = 0.02; _backend.TickPresentation(0.02f);
            _source.Time = 1e20; _backend.TickPresentation(0.01f); Assert.AreEqual(1,_backend.SliceCount);
            _source.Time = double.NaN; _backend.TickPresentation(0.01f); Assert.AreEqual(0,_backend.SliceCount);
        }
        [UnityTest] public IEnumerator ExplicitDispose_ReleasesLeaseSourceAndOwnedMesh_WithoutDestructionCallback()
        {
            _source.Time = 0.02; _backend.TickPresentation(0.02f);
            Mesh mesh = _view.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(1, _budget.LeasedCount);
            _backend.Dispose();
            Assert.AreEqual(0, _budget.LeasedCount);
            Assert.True(_source.Disposed);
            _backend.Dispose(); Assert.AreEqual(0, _budget.LeasedCount);
            // Unity destroys native objects at the end of the frame in Play Mode.
            // Source/lease disposal and repeated Dispose remain synchronous above.
            yield return null;
            Assert.True(mesh == null);
            Assert.True(_backend != null, "Explicit Dispose must release the mesh without destroying the backend.");
        }
        private sealed class Evaluator : IUnityBladeTrailAnimationEvaluator
        {
            private readonly Transform _bone;
            public Evaluator(Transform bone) { _bone = bone; }
            public bool TryEvaluateBladeTrailPose(double time)
            { _bone.localRotation = Quaternion.Euler(0,(float)time*180,0);return true; }
        }
        private sealed class Source : IUnityBladeTrailPoseSource
        {
            public double Time; public Vector3 Owner; public bool Available = true;
            public bool SamplingAvailable = true; public bool Disposed; public int SubSamples;
            private static Vector3 Tip(double time) => new Vector3(Mathf.Cos((float)time * Mathf.PI / 0.04f),0,
                Mathf.Sin((float)time * Mathf.PI / 0.04f));
            public bool TryGetPresentedPose(out UnityBladeTrailPose pose)
            { pose = new UnityBladeTrailPose(Owner,Owner+Tip(Time),Owner,Quaternion.identity,Time,1); return Available; }
            public bool TrySamplePose(double time, Vector3 position, Quaternion rotation,out Vector3 root,out Vector3 tip)
            { SubSamples++; root=position;tip=position+Tip(time);return SamplingAvailable; }
            public void Dispose() { Disposed = true; }
        }
    }
}
