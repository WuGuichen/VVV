using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Animation
{
    public sealed class AnimationContractTests
    {
        [Test]
        public void LayerId_DefaultsToBase()
        {
            var defaultLayer = default(MxAnimationLayerId);

            Assert.AreEqual(MxAnimationLayerId.Base, defaultLayer);
            Assert.AreEqual("base", defaultLayer.Value);
        }

        [Test]
        public void SetDefinition_FindsBindingByBindingIdOrActionKey()
        {
            var clipKey = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var binding = new MxAnimationActionBinding(
                "idle",
                "action.idle",
                clipKey,
                MxAnimationLayerId.Base,
                playbackSpeed: 1.25f,
                loop: true);
            var definition = new MxAnimationSetDefinition("demo.set", 1, default, default, new[] { binding });

            Assert.IsTrue(definition.TryFindBinding("idle", string.Empty, out MxAnimationActionBinding byBinding));
            Assert.IsTrue(definition.TryFindBinding(string.Empty, "action.idle", out MxAnimationActionBinding byAction));
            Assert.AreEqual(clipKey, byBinding.Clip);
            Assert.AreEqual(byBinding, byAction);
            Assert.AreEqual(ResourceTypeIds.AnimationClip, byAction.Clip.TypeId);
        }

        [Test]
        public void PresentationSyncState_CopiesLayerTransitionsAndBlendParameters()
        {
            var upperBody = new MxAnimationLayerId("upper_body");
            var layer = new MxAnimationLayerSyncState(
                upperBody,
                currentWeight: 0.25f,
                targetWeight: 1f,
                transitionStartedAtFrame: 120,
                transitionDurationFrames: 10,
                transitionRemainingFrames: 6,
                transitionPolicyId: "upper.attack.fade_in",
                correlationId: "entity:7/action:12/instance:3");
            var speed = new MxAnimationQuantizedParameter("move.speed", 3250, 1000);

            var state = new MxAnimationPresentationSyncState(
                actorId: "entity:7",
                animationSetId: "skeleton.combat",
                animationSetVersion: 2,
                animationSetHash: "set-hash",
                resourceCatalogHash: "catalog-hash",
                clipRegistryVersion: 5,
                actionId: 12,
                actionKey: "action:12",
                actionInstanceId: 3,
                startedAtCombatFrame: 110,
                localFrame: 14,
                status: MxAnimationPresentationSyncStatus.Running,
                layerStates: new[] { layer },
                blendParameters: new[] { speed },
                correlationId: "sync:entity:7");

            Assert.AreEqual("entity:7", state.ActorId);
            Assert.AreEqual(14, state.LocalFrame);
            Assert.IsTrue(state.TryFindLayerState(upperBody, out MxAnimationLayerSyncState foundLayer));
            Assert.IsTrue(foundLayer.IsTransitioning);
            Assert.AreEqual(0.25f, foundLayer.CurrentWeight);
            Assert.AreEqual(1f, foundLayer.TargetWeight);
            Assert.AreEqual(6, foundLayer.TransitionRemainingFrames);
            Assert.IsTrue(state.TryFindBlendParameter("move.speed", out MxAnimationQuantizedParameter foundSpeed));
            Assert.AreEqual(3250, foundSpeed.QuantizedValue);
            Assert.AreEqual(3.25f, foundSpeed.Value);
        }

        [Test]
        public void LayerSyncState_ClampsTransitionFrames()
        {
            var layer = new MxAnimationLayerSyncState(
                new MxAnimationLayerId("upper_body"),
                currentWeight: float.NaN,
                targetWeight: 2f,
                transitionStartedAtFrame: -4,
                transitionDurationFrames: 4,
                transitionRemainingFrames: 9);
            var zeroDuration = new MxAnimationLayerSyncState(
                MxAnimationLayerId.Base,
                currentWeight: 0f,
                targetWeight: 1f,
                transitionDurationFrames: 0,
                transitionRemainingFrames: 5);

            Assert.AreEqual(0f, layer.CurrentWeight);
            Assert.AreEqual(1f, layer.TargetWeight);
            Assert.AreEqual(0, layer.TransitionStartedAtFrame);
            Assert.AreEqual(4, layer.TransitionRemainingFrames);
            Assert.AreEqual(0, zeroDuration.TransitionRemainingFrames);
            Assert.IsFalse(zeroDuration.IsTransitioning);
        }

        [Test]
        public void PresentationEventDedupeKey_UsesStableEquality()
        {
            var first = new MxAnimationPresentationEventDedupeKey(
                "entity:7",
                actionInstanceId: 3,
                worldFrame: 120,
                localFrame: 10,
                eventId: "event:footstep",
                sourceOrder: 1);
            var duplicate = new MxAnimationPresentationEventDedupeKey(
                "entity:7",
                actionInstanceId: 3,
                worldFrame: 120,
                localFrame: 10,
                eventId: "event:footstep",
                sourceOrder: 1);
            var nextInstance = new MxAnimationPresentationEventDedupeKey(
                "entity:7",
                actionInstanceId: 4,
                worldFrame: 120,
                localFrame: 10,
                eventId: "event:footstep",
                sourceOrder: 1);
            var legacyInstance = new MxAnimationPresentationEventDedupeKey(
                "entity:7",
                actionInstanceId: 0,
                worldFrame: 120,
                localFrame: 10,
                eventId: "event:footstep",
                sourceOrder: 1);
            var seen = new HashSet<MxAnimationPresentationEventDedupeKey>();

            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(legacyInstance.IsValid);
            Assert.IsTrue(seen.Add(first));
            Assert.IsFalse(seen.Add(duplicate));
            Assert.IsTrue(seen.Add(nextInstance));
        }

        [Test]
        public void PresentationSyncValidator_ReportsVersionMismatchDiagnostics()
        {
            var state = new MxAnimationPresentationSyncState(
                actorId: "entity:7",
                animationSetId: "skeleton.combat",
                animationSetVersion: 2,
                animationSetHash: "actual-set-hash",
                resourceCatalogHash: "catalog-hash",
                clipRegistryVersion: 5,
                actionId: 12,
                actionKey: "action:12",
                actionInstanceId: 3,
                startedAtCombatFrame: 110,
                localFrame: 14,
                status: MxAnimationPresentationSyncStatus.Running);
            var expectation = new MxAnimationPresentationSyncVersionExpectation(
                "skeleton.combat",
                2,
                "expected-set-hash",
                "catalog-hash",
                5);

            MxAnimationPresentationSyncValidationResult result =
                MxAnimationPresentationSyncValidator.Validate(state, expectation);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxAnimationPresentationSyncValidationCode.AnimationSetHashMismatch, result.Code);
            Assert.AreEqual("animationSetHash", result.Field);
            Assert.AreEqual("expected-set-hash", result.Expected);
            Assert.AreEqual("actual-set-hash", result.Actual);
            StringAssert.Contains("version mismatch", result.Message);
        }

        [Test]
        public void PresentationSyncValidator_RejectsMissingActorId()
        {
            var state = new MxAnimationPresentationSyncState(
                actorId: "",
                animationSetId: "skeleton.combat",
                animationSetVersion: 2,
                animationSetHash: "set-hash",
                resourceCatalogHash: "catalog-hash",
                clipRegistryVersion: 5,
                actionId: 12,
                actionKey: "action:12",
                actionInstanceId: 3,
                startedAtCombatFrame: 110,
                localFrame: 14,
                status: MxAnimationPresentationSyncStatus.Running);
            var expectation = new MxAnimationPresentationSyncVersionExpectation(
                "skeleton.combat",
                2,
                "set-hash",
                "catalog-hash",
                5);

            MxAnimationPresentationSyncValidationResult result =
                MxAnimationPresentationSyncValidator.Validate(state, expectation);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MxAnimationPresentationSyncValidationCode.MissingActorId, result.Code);
            Assert.AreEqual("actorId", result.Field);
        }

        [Test]
        public void PresentationSyncValidator_AllowsVersionlessExpectation()
        {
            var state = new MxAnimationPresentationSyncState(
                actorId: "entity:7",
                animationSetId: "skeleton.combat",
                animationSetVersion: 2,
                animationSetHash: "set-hash",
                resourceCatalogHash: "catalog-hash",
                clipRegistryVersion: 5,
                actionId: 12,
                actionKey: "action:12",
                actionInstanceId: 0,
                startedAtCombatFrame: 110,
                localFrame: 14,
                status: MxAnimationPresentationSyncStatus.Running);

            MxAnimationPresentationSyncValidationResult result =
                MxAnimationPresentationSyncValidator.Validate(
                    state,
                    MxAnimationPresentationSyncVersionExpectation.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(MxAnimationPresentationSyncValidationCode.Success, result.Code);
        }
    }
}
