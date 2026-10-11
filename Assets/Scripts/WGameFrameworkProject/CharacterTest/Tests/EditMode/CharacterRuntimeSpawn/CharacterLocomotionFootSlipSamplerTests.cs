using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterLocomotionFootSlipSamplerTests
    {
        [Test]
        public void Sampler_ResolvesAbstractFootBindingsBeforeExplicitPaths()
        {
            var rootObject = new GameObject("actor-semantic-feet");
            var left = new GameObject("semantic-left");
            var right = new GameObject("semantic-right");
            left.transform.SetParent(rootObject.transform);
            right.transform.SetParent(rootObject.transform);
            CharacterAbstractBoneRuntimeMap map = rootObject.AddComponent<CharacterAbstractBoneRuntimeMap>();
            map.Configure(new[]
            {
                new CharacterAbstractBoneRuntimeBinding(CharacterAbstractBoneIds.LeftFoot, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id50, left.transform),
                new CharacterAbstractBoneRuntimeBinding(CharacterAbstractBoneIds.RightFoot, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id52, right.transform)
            });

            try
            {
                ResourceKey clip = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
                CharacterLocomotionFootSlipSnapshot snapshot = new CharacterLocomotionFootSlipSampler(
                        MxAnimationFootSlipThresholds.Default)
                    .Sample(1, 0.1f, rootObject.transform, null, null, null, CreateSnapshot(clip, 0f), true,
                        "missing/left", "missing/right");

                Assert.IsTrue(snapshot.LeftFootResolved);
                Assert.IsTrue(snapshot.RightFootResolved);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Sampler_ResolvesFallbackFootPathsAndReportsSlipDuringContact()
        {
            var rootObject = new GameObject("actor");
            GameObject rig = new GameObject("Rig");
            GameObject left = new GameObject("left_foot");
            GameObject right = new GameObject("right_foot");
            rig.transform.SetParent(rootObject.transform);
            left.transform.SetParent(rig.transform);
            right.transform.SetParent(rig.transform);
            left.transform.position = new Vector3(-0.1f, 0f, 0f);
            right.transform.position = new Vector3(0.1f, 0f, 0f);

            try
            {
                ResourceKey clip = new ResourceKey("demo.animation.walk_f", ResourceTypeIds.AnimationClip);
                var definition = new MxAnimationSetDefinition(
                    "set.base",
                    1,
                    clip,
                    clip,
                    locomotionClipCalibrations: new[]
                    {
                        new MxAnimationLocomotionClipCalibration(
                            "walk_f",
                            clip,
                            0f,
                            1f,
                            cycleDurationSeconds: 1f,
                            leftFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.4f, 1f) },
                            rightFootContacts: new[] { new MxAnimationFootContactWindow(0.1f, 0.4f, 1f) })
                    });
                var probe = new MxAnimationLocomotionBlendProbeSnapshot(
                    "blend.move2d",
                    new MxAnimationBlend2DControllerDomain(-1000, 1000, -1000, 1000),
                    0,
                    1000,
                    null,
                    new[] { new MxAnimationBlend2DWeight(clip, 0, 1000, 1f, 1f, true) },
                    weightsFromBackend: true);
                MxAnimationDiagnosticSnapshot animation = CreateSnapshot(clip, normalizedTime: 0.2f);
                var sampler = new CharacterLocomotionFootSlipSampler(
                    new MxAnimationFootSlipThresholds(3f, 3f, 8f, 8f));

                sampler.Sample(1, 0.1f, rootObject.transform, null, definition, probe, animation, true, "Rig/left_foot", "Rig/right_foot");
                left.transform.position += new Vector3(0.1f, 0f, 0f);
                right.transform.position += new Vector3(0.1f, 0f, 0f);
                CharacterLocomotionFootSlipSnapshot snapshot = sampler.Sample(
                    2,
                    0.1f,
                    rootObject.transform,
                    null,
                    definition,
                    probe,
                    animation,
                    true,
                    "Rig/left_foot",
                    "Rig/right_foot");

                Assert.IsTrue(snapshot.LeftFootResolved);
                Assert.IsTrue(snapshot.RightFootResolved);
                Assert.AreEqual(1f, snapshot.Frame.LeftFootContactConfidence, 0.0001f);
                Assert.AreEqual(1f, snapshot.Frame.RightFootContactConfidence, 0.0001f);
                Assert.AreEqual(100f, snapshot.Frame.LeftFootSlipCmPerSecond, 0.001f);
                Assert.AreEqual(10f, snapshot.Frame.MaxSlipDistanceCm, 0.001f);
                Assert.AreEqual(MxAnimationFootSlipGrade.Bad, snapshot.Grade);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Sampler_DominantClipChangeResetsContactAnchors()
        {
            var rootObject = new GameObject("actor-transition");
            GameObject rig = new GameObject("Rig");
            GameObject left = new GameObject("left_foot");
            GameObject right = new GameObject("right_foot");
            rig.transform.SetParent(rootObject.transform);
            left.transform.SetParent(rig.transform);
            right.transform.SetParent(rig.transform);
            try
            {
                ResourceKey walk = new ResourceKey("demo.animation.walk", ResourceTypeIds.AnimationClip);
                ResourceKey run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
                var contacts = new[] { new MxAnimationFootContactWindow(0f, 1f, 1f) };
                var definition = new MxAnimationSetDefinition(
                    "set.base",
                    1,
                    walk,
                    walk,
                    locomotionClipCalibrations: new[]
                    {
                        new MxAnimationLocomotionClipCalibration("walk", walk, 0f, 1f, leftFootContacts: contacts, rightFootContacts: contacts),
                        new MxAnimationLocomotionClipCalibration("run", run, 0f, 2f, leftFootContacts: contacts, rightFootContacts: contacts)
                    });
                var sampler = new CharacterLocomotionFootSlipSampler(MxAnimationFootSlipThresholds.Default);
                var walkProbe = CreateProbe(walk);
                var runProbe = CreateProbe(run);

                sampler.Sample(1, 0.1f, rootObject.transform, null, definition, walkProbe, CreateSnapshot(walk, 0.2f), true, "Rig/left_foot", "Rig/right_foot");
                left.transform.position += new Vector3(0.1f, 0f, 0f);
                right.transform.position += new Vector3(0.1f, 0f, 0f);
                CharacterLocomotionFootSlipSnapshot transitioned = sampler.Sample(
                    2,
                    0.1f,
                    rootObject.transform,
                    null,
                    definition,
                    runProbe,
                    CreateSnapshot(run, 0.2f),
                    true,
                    "Rig/left_foot",
                    "Rig/right_foot");

                Assert.AreEqual(0f, transitioned.Frame.LeftFootSlipCmPerSecond, 0.0001f);
                Assert.AreEqual(0f, transitioned.Frame.RightFootSlipCmPerSecond, 0.0001f);
                Assert.AreEqual(0f, transitioned.Frame.MaxSlipDistanceCm, 0.0001f);
                Assert.AreEqual(MxAnimationFootSlipGrade.Ok, transitioned.Grade);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Sampler_BlendSampleChangeResetsContactAnchors()
        {
            var rootObject = new GameObject("actor-speed-ramp");
            GameObject rig = new GameObject("Rig");
            GameObject left = new GameObject("left_foot");
            GameObject right = new GameObject("right_foot");
            rig.transform.SetParent(rootObject.transform);
            left.transform.SetParent(rig.transform);
            right.transform.SetParent(rig.transform);
            try
            {
                ResourceKey run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
                var contacts = new[] { new MxAnimationFootContactWindow(0f, 1f, 1f) };
                var definition = new MxAnimationSetDefinition(
                    "set.base",
                    1,
                    run,
                    run,
                    locomotionClipCalibrations: new[]
                    {
                        new MxAnimationLocomotionClipCalibration(
                            "run",
                            run,
                            0f,
                            2f,
                            leftFootContacts: contacts,
                            rightFootContacts: contacts)
                    });
                var sampler = new CharacterLocomotionFootSlipSampler(MxAnimationFootSlipThresholds.Default);
                var startProbe = CreateProbe(run, 0, 600);
                var nextProbe = CreateProbe(run, 0, 650);

                sampler.Sample(1, 0.1f, rootObject.transform, null, definition, startProbe,
                    CreateSnapshot(run, 0.2f), true, "Rig/left_foot", "Rig/right_foot");
                left.transform.position += new Vector3(0.1f, 0f, 0f);
                right.transform.position += new Vector3(0.1f, 0f, 0f);
                CharacterLocomotionFootSlipSnapshot transitioned = sampler.Sample(
                    2,
                    0.1f,
                    rootObject.transform,
                    null,
                    definition,
                    nextProbe,
                    CreateSnapshot(run, 0.2f),
                    true,
                    "Rig/left_foot",
                    "Rig/right_foot");

                Assert.AreEqual(0f, transitioned.Frame.LeftFootSlipCmPerSecond, 0.0001f);
                Assert.AreEqual(0f, transitioned.Frame.RightFootSlipCmPerSecond, 0.0001f);
                Assert.AreEqual(0f, transitioned.Frame.MaxSlipDistanceCm, 0.0001f);
                Assert.AreEqual(MxAnimationFootSlipGrade.Ok, transitioned.Grade);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static MxAnimationLocomotionBlendProbeSnapshot CreateProbe(
            ResourceKey clip,
            int sampleX = 0,
            int sampleY = 1000)
        {
            return new MxAnimationLocomotionBlendProbeSnapshot(
                "blend.move2d",
                new MxAnimationBlend2DControllerDomain(-1000, 1000, -1000, 1000),
                sampleX,
                sampleY,
                null,
                new[] { new MxAnimationBlend2DWeight(clip, 0, 1000, 1f, 1f, true) },
                weightsFromBackend: true);
        }

        private static MxAnimationDiagnosticSnapshot CreateSnapshot(ResourceKey clip, float normalizedTime)
        {
            var layer = new MxAnimationLayerDiagnostic(
                MxAnimationLayerId.Base,
                MxAnimationLayerStatus.Playing,
                clip,
                default,
                currentClipIsFallback: false,
                currentWeight: 1f,
                outgoingWeight: 0f,
                activePlayableCount: 1,
                fade: null,
                lastError: ResourceError.None,
                activeClipPlaybacks: new[]
                {
                    new MxAnimationClipPlaybackDiagnostic(clip, 1f, normalizedTime, 1f, true, true, false)
                });

            return new MxAnimationDiagnosticSnapshot(
                "test",
                "actor",
                "set.base",
                1,
                graphIsValid: true,
                isReleased: false,
                defaultClip: null,
                fallbackClip: null,
                layerStates: new[] { layer },
                activeFades: null,
                recentRequests: null,
                recentResourceErrors: null);
        }
    }
}
