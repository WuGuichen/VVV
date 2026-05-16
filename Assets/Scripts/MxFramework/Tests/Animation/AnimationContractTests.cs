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
        public void SetDefinitionHash_IsStableAcrossBindingOrder()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var attack = new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip);
            var fallback = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip);
            var first = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                fallback,
                new[]
                {
                    new MxAnimationActionBinding("attack", "action:2", attack, new MxAnimationLayerId("upper_body")),
                    new MxAnimationActionBinding("idle", "action:1", idle, MxAnimationLayerId.Base)
                });
            var second = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                fallback,
                new[]
                {
                    new MxAnimationActionBinding("idle", "action:1", idle, MxAnimationLayerId.Base),
                    new MxAnimationActionBinding("attack", "action:2", attack, new MxAnimationLayerId("upper_body"))
                });
            var changed = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                fallback,
                new[]
                {
                    new MxAnimationActionBinding("idle", "action:1", idle, MxAnimationLayerId.Base, playbackSpeed: 1.5f),
                    new MxAnimationActionBinding("attack", "action:2", attack, new MxAnimationLayerId("upper_body"))
                });

            Assert.That(first.DefinitionHash, Does.StartWith(MxAnimationSetDefinitionHasher.HashPrefix));
            Assert.AreEqual(first.DefinitionHash, second.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changed.DefinitionHash);
        }

        [Test]
        public void SetDefinitionHash_IsStableAcrossFullEventPayloadOrder()
        {
            var clip = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var firstPayload = new ResourceKey("demo.vfx.slash", ResourceTypeIds.GameObject, "blue", "demo.package");
            var secondPayload = new ResourceKey("demo.sfx.slash", ResourceTypeIds.AudioClip, "short", "demo.package");
            var first = new MxAnimationSetDefinition(
                "demo.set",
                1,
                clip,
                clip,
                new[]
                {
                    new MxAnimationActionBinding(
                        "idle",
                        "action:1",
                        clip,
                        MxAnimationLayerId.Base,
                        presentationEvents: new[]
                        {
                            new MxAnimationPresentationEvent("event.hit", MxAnimationEventTimeDomain.CombatFrame, 4f, "vfx", firstPayload, "hand.r", "slash"),
                            new MxAnimationPresentationEvent("event.hit", MxAnimationEventTimeDomain.CombatFrame, 4f, "vfx", secondPayload, "hand.l", "slash")
                        })
                });
            var second = new MxAnimationSetDefinition(
                "demo.set",
                1,
                clip,
                clip,
                new[]
                {
                    new MxAnimationActionBinding(
                        "idle",
                        "action:1",
                        clip,
                        MxAnimationLayerId.Base,
                        presentationEvents: new[]
                        {
                            new MxAnimationPresentationEvent("event.hit", MxAnimationEventTimeDomain.CombatFrame, 4f, "vfx", secondPayload, "hand.l", "slash"),
                            new MxAnimationPresentationEvent("event.hit", MxAnimationEventTimeDomain.CombatFrame, 4f, "vfx", firstPayload, "hand.r", "slash")
                        })
                });

            Assert.AreEqual(first.DefinitionHash, second.DefinitionHash);
        }

        [Test]
        public void SetDefinitionHash_IncludesLayerDefinitions()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var mask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
            var first = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), "humanoid.upper", 0.25f, MxAnimationLayerBlendMode.Override, mask)
                });
            var second = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), "humanoid.upper", 0.25f, MxAnimationLayerBlendMode.Override, mask)
                });
            var changed = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), "humanoid.upper", 1f, MxAnimationLayerBlendMode.Override, mask)
                });

            Assert.AreEqual(first.DefinitionHash, second.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changed.DefinitionHash);
        }

        [Test]
        public void StaticMappingProvider_FindsDefinitionBySetId()
        {
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip),
                new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip));
            var provider = new MxAnimationStaticMappingProvider(new[] { definition });

            Assert.IsTrue(provider.TryFindDefinition("demo.set", out MxAnimationSetDefinition found));
            Assert.AreEqual(definition, found);
            Assert.IsFalse(provider.TryFindDefinition("missing.set", out _));
        }

        [Test]
        public void ClipRegistryBuilder_FiltersCatalogAnimationClips()
        {
            var catalog = new ResourceCatalog(
                "demo.catalog",
                "demo.package",
                new[]
                {
                    new ResourceCatalogEntry("demo.text.title", ResourceTypeIds.TextAsset, "memory", "title"),
                    new ResourceCatalogEntry("demo.animation.attack", ResourceTypeIds.AnimationClip, "memory", "attack", hash: "attack-hash"),
                    new ResourceCatalogEntry("demo.animation.idle", ResourceTypeIds.AnimationClip, "memory", "idle", hash: "idle-hash")
                });

            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(
                catalog,
                version: 7,
                catalogHash: "catalog-hash");

            Assert.AreEqual(7, registry.Version);
            Assert.AreEqual("demo.catalog", registry.CatalogId);
            Assert.AreEqual("catalog-hash", registry.CatalogHash);
            Assert.AreEqual(2, registry.Entries.Count);
            Assert.AreEqual("demo.animation.attack", registry.Entries[0].ClipKey.Id);
            Assert.IsTrue(registry.Contains(new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip)));
            Assert.IsFalse(registry.Contains(new ResourceKey("demo.text.title", ResourceTypeIds.TextAsset)));
        }

        [Test]
        public void SetDefinitionValidator_ReportsMissingCatalogAndFallback()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                default,
                new[]
                {
                    new MxAnimationActionBinding("idle", "action:1", idle, MxAnimationLayerId.Base)
                });

            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(definition);

            AssertIssue(report, "CatalogMissing");
            AssertIssue(report, "FallbackClipMissing");
        }

        [Test]
        public void SetDefinitionValidator_ReportsDuplicateActionKeyAndWrongType()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var wrongType = new ResourceKey("demo.animation.attack", ResourceTypeIds.TextAsset);
            var fallback = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip);
            var catalog = new ResourceCatalog(
                "demo.catalog",
                "demo.package",
                new[]
                {
                    new ResourceCatalogEntry(idle.Id, idle.TypeId, "memory", "idle"),
                    new ResourceCatalogEntry(fallback.Id, fallback.TypeId, "memory", "fallback")
                });
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                fallback,
                new[]
                {
                    new MxAnimationActionBinding("idle", "action:1", idle, MxAnimationLayerId.Base),
                    new MxAnimationActionBinding("attack", "action:1", wrongType, MxAnimationLayerId.Base),
                    new MxAnimationActionBinding("bad", "bad action", idle, MxAnimationLayerId.Base)
                });

            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(definition, catalog);

            AssertIssue(report, "DuplicateActionKey");
            AssertIssue(report, "ActionKeyInvalid");
            AssertIssue(report, "ClipTypeMismatch");
        }

        [Test]
        public void SetDefinitionValidator_CatalogLookupHonorsVariantAndPackage()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip, packageId: "demo.package");
            var fallback = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip, "alt");
            var wrongPackage = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip, packageId: "other.package");
            var wrongVariant = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip, "missing");
            var catalog = new ResourceCatalog(
                "demo.catalog",
                "demo.package",
                new[]
                {
                    new ResourceCatalogEntry("demo.animation.idle", ResourceTypeIds.AnimationClip, "memory", "idle"),
                    new ResourceCatalogEntry("demo.animation.fallback", ResourceTypeIds.AnimationClip, "memory", "fallback", variant: "alt")
                });
            var valid = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                fallback);
            var invalid = new MxAnimationSetDefinition(
                "demo.set",
                1,
                wrongPackage,
                wrongVariant);

            ResourceCatalogValidationReport validReport = MxAnimationSetDefinitionValidator.Validate(valid, catalog);
            ResourceCatalogValidationReport invalidReport = MxAnimationSetDefinitionValidator.Validate(invalid, catalog);

            Assert.IsFalse(validReport.HasErrors);
            AssertIssue(invalidReport, "ClipCatalogEntryMissing");
        }

        [Test]
        public void SetDefinitionValidator_StructureOnlySkipsCatalogRequirement()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var definition = new MxAnimationSetDefinition("demo.set", 1, idle, idle);

            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(
                definition,
                catalog: null,
                requireCatalog: false);

            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void SetDefinitionValidator_ValidatesAvatarMaskLayerKeys()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var wrongMask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.TextAsset);
            var missingMask = new ResourceKey("demo.animation.mask.missing", ResourceTypeIds.AvatarMask);
            var catalog = new ResourceCatalog(
                "demo.catalog",
                "demo.package",
                new[]
                {
                    new ResourceCatalogEntry(idle.Id, idle.TypeId, "memory", "idle"),
                    new ResourceCatalogEntry("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask, "memory", "mask")
                });
            var wrongType = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), avatarMaskKey: wrongMask)
                });
            var missing = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                layers: new[]
                {
                    new MxAnimationLayerDefinition(new MxAnimationLayerId("upper_body"), avatarMaskKey: missingMask)
                });

            ResourceCatalogValidationReport wrongTypeReport = MxAnimationSetDefinitionValidator.Validate(wrongType, catalog);
            ResourceCatalogValidationReport missingReport = MxAnimationSetDefinitionValidator.Validate(missing, catalog);

            AssertIssue(wrongTypeReport, "AvatarMaskTypeMismatch");
            AssertIssue(missingReport, "AvatarMaskCatalogEntryMissing");
        }

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected animation validation issue: " + code);
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
        public void QuantizedParameter_DefaultValueIsZero()
        {
            var parameter = default(MxAnimationQuantizedParameter);

            Assert.AreEqual(0f, parameter.Value);
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
        public void PresentationEventDispatchSink_DropsDuplicateWithinActionInstance()
        {
            var recordingSink = new RecordingPresentationEventSink();
            var dispatchSink = new MxAnimationPresentationEventDispatchSink(recordingSink, maxDedupeEntries: 8);
            var evt = new MxAnimationPresentationEvent(
                "event:footstep",
                MxAnimationEventTimeDomain.PresentationFrame,
                4f,
                "SFX",
                new ResourceKey("sfx.footstep", ResourceTypeIds.AudioClip));
            var first = new MxAnimationPresentationEventDispatch(
                "entity:7",
                "action:12",
                "walk",
                actionInstanceId: 3,
                worldFrame: 120,
                localFrame: 4,
                sourceOrder: 1,
                evt,
                "first");
            var duplicate = new MxAnimationPresentationEventDispatch(
                "entity:7",
                "action:12",
                "walk",
                actionInstanceId: 3,
                worldFrame: 120,
                localFrame: 4,
                sourceOrder: 1,
                evt,
                "duplicate");
            var nextInstance = new MxAnimationPresentationEventDispatch(
                "entity:7",
                "action:12",
                "walk",
                actionInstanceId: 4,
                worldFrame: 120,
                localFrame: 4,
                sourceOrder: 1,
                evt,
                "next");

            Assert.IsTrue(dispatchSink.TryDispatch(first, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic firstDiagnostic));
            Assert.IsFalse(dispatchSink.TryDispatch(duplicate, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic duplicateDiagnostic));
            Assert.IsTrue(dispatchSink.TryDispatch(nextInstance, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic nextDiagnostic));

            Assert.AreEqual(2, recordingSink.Dispatches.Count);
            Assert.AreEqual(MxAnimationPresentationEventDispatchStatus.Dispatched, firstDiagnostic.Status);
            Assert.AreEqual(MxAnimationPresentationEventDispatchStatus.DuplicateDropped, duplicateDiagnostic.Status);
            Assert.AreEqual(MxAnimationPresentationEventDispatchStatus.Dispatched, nextDiagnostic.Status);
        }

        [Test]
        public void EventTimelineBuilder_ListsCombatFrameAndPresentationTimeRows()
        {
            var clip = new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip);
            var payload = new ResourceKey("fx.slash", ResourceTypeIds.GameObject);
            var combatFrameEvent = new MxAnimationPresentationEvent(
                "event:hit",
                MxAnimationEventTimeDomain.CombatFrame,
                6f,
                "VFX",
                payload,
                "weapon",
                "slash");
            var secondsEvent = new MxAnimationPresentationEvent(
                "event:swing",
                MxAnimationEventTimeDomain.Seconds,
                0.2f,
                "SFX",
                new ResourceKey("sfx.swing", ResourceTypeIds.AudioClip),
                replayPolicy: MxAnimationPresentationEventReplayPolicy.CatchUpSafe);
            var definition = new MxAnimationSetDefinition(
                "combat.demo",
                1,
                clip,
                clip,
                new[]
                {
                    new MxAnimationActionBinding(
                        "attack",
                        "action:12",
                        clip,
                        MxAnimationLayerId.Base,
                        presentationEvents: new[] { combatFrameEvent, secondsEvent })
                });

            IReadOnlyList<MxAnimationEventTimelineRow> rows =
                MxAnimationEventTimelineBuilder.BuildRows(definition);

            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(ContainsRow(rows, "event:hit", MxAnimationEventTimeDomain.CombatFrame, true));
            Assert.IsTrue(ContainsRow(rows, "event:swing", MxAnimationEventTimeDomain.Seconds, false));
            Assert.AreEqual(MxAnimationPresentationEventReplayPolicy.CatchUpSafe, rows[0].ReplayPolicy);
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

        private static bool ContainsRow(
            IReadOnlyList<MxAnimationEventTimelineRow> rows,
            string eventId,
            MxAnimationEventTimeDomain domain,
            bool hasDeterministicCorrelation)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                MxAnimationEventTimelineRow row = rows[i];
                if (row.EventId == eventId
                    && row.TimeDomain == domain
                    && row.HasDeterministicCorrelation == hasDeterministicCorrelation)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingPresentationEventSink : IMxAnimationPresentationEventSink
        {
            public readonly List<MxAnimationPresentationEventDispatch> Dispatches =
                new List<MxAnimationPresentationEventDispatch>();

            public void Dispatch(MxAnimationPresentationEventDispatch dispatch)
            {
                Dispatches.Add(dispatch);
            }
        }
    }
}
