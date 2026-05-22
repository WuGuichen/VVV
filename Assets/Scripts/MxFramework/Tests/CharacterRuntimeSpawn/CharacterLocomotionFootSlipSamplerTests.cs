using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterLocomotionFootSlipSamplerTests
    {
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
