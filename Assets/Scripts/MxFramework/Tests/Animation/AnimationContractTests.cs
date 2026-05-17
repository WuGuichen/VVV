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
        public void SetDefinitionHash_IncludesCompatibilityExpectation()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var mask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
            var first = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" },
                    new[] { "Hips/Spine/WeaponSocket" },
                    new[]
                    {
                        new MxAnimationClipCompatibilityExpectation(idle, new[] { "Hips/Spine" })
                    },
                    new[]
                    {
                        new MxAnimationAvatarMaskCompatibilityExpectation(mask, new[] { "Hips/Spine" })
                    }));
            var second = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Spine" },
                    new[] { "Hips/Spine/WeaponSocket" },
                    new[]
                    {
                        new MxAnimationClipCompatibilityExpectation(idle, new[] { "Hips/Spine" })
                    },
                    new[]
                    {
                        new MxAnimationAvatarMaskCompatibilityExpectation(mask, new[] { "Hips/Spine" })
                    }));
            var changed = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    "humanoid",
                    "sha256:skeleton",
                    new[] { "Hips/Head" }));

            Assert.AreEqual(first.DefinitionHash, second.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changed.DefinitionHash);
        }

        [Test]
        public void CompatibilityValidator_ReportsSkeletonClipMaskAndBakeMismatches()
        {
            var clip = new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip);
            var mask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
            var skeleton = new MxAnimationSkeletonCompatibilityProfile(
                "humanoid",
                "sha256:skeleton",
                new[] { "Hips", "Hips/Spine" },
                new[] { "Hips/Spine/WeaponSocket" });
            var profile = new MxAnimationCompatibilityProfile(
                skeleton,
                new[]
                {
                    new MxAnimationClipCompatibilityProfile(clip, "humanoid", "sha256:skeleton", new[] { "Hips/Spine" })
                },
                new[]
                {
                    new MxAnimationAvatarMaskCompatibilityProfile(mask, "humanoid", "sha256:skeleton", new[] { "Hips/Spine" })
                },
                new[] { CreateBakeArtifact(clip, "humanoid", "sha256:old-skeleton") });
            var expectation = new MxAnimationCompatibilityExpectation(
                "humanoid",
                "sha256:skeleton",
                new[] { "Hips/Head" },
                new[] { "Hips/LeftHandSocket" },
                new[]
                {
                    new MxAnimationClipCompatibilityExpectation(clip, new[] { "Hips/Arm" })
                },
                new[]
                {
                    new MxAnimationAvatarMaskCompatibilityExpectation(mask, new[] { "Hips/Arm" })
                });

            MxAnimationCompatibilityValidationReport report = MxAnimationCompatibilityValidator.Validate(profile, expectation);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.BonePathMissing, default, "bonePath");
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.SocketPathMissing, default, "socketPath");
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.ClipBindingPathMissing, clip, "clipBindingPath");
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.AvatarMaskPathMissing, mask, "avatarMaskPath");
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.BakeArtifactSkeletonProfileHashMismatch, clip, "bakeSkeletonProfileHash");
        }

        [Test]
        public void CompatibilityValidator_RequiresExactPackageWhenExpectationSpecifiesPackage()
        {
            var expectedClip = new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip, packageId: "package.a");
            var actualClip = new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip, packageId: "package.b");
            var skeleton = new MxAnimationSkeletonCompatibilityProfile("humanoid", "sha256:skeleton", new[] { "Hips" });
            var profile = new MxAnimationCompatibilityProfile(
                skeleton,
                new[]
                {
                    new MxAnimationClipCompatibilityProfile(actualClip, "humanoid", "sha256:skeleton", new[] { "Hips" })
                });
            var expectation = new MxAnimationCompatibilityExpectation(
                "humanoid",
                "sha256:skeleton",
                clipExpectations: new[]
                {
                    new MxAnimationClipCompatibilityExpectation(expectedClip, new[] { "Hips" })
                });

            MxAnimationCompatibilityValidationReport report = MxAnimationCompatibilityValidator.Validate(profile, expectation);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, MxAnimationCompatibilityIssueCodes.ClipProfileMissing, expectedClip, "clipProfile");
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

        private static MxAnimationBakeArtifact CreateBakeArtifact(
            ResourceKey clip,
            string skeletonProfileId,
            string skeletonProfileHash)
        {
            var profile = new MxAnimationBakeProfile(
                "profile.test",
                clip,
                "sha256:source",
                skeletonProfileId,
                skeletonProfileHash,
                sampleTickRate: 30,
                quantizationScale: 1000,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                "import:test");
            return new MxAnimationBakeArtifact(profile);
        }

        private static void AssertIssue(
            MxAnimationCompatibilityValidationReport report,
            string code,
            ResourceKey key,
            string field)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationCompatibilityIssue issue = report.Issues[i];
                if (issue.Code == code
                    && issue.Field == field
                    && (!key.IsValid || issue.Key == key))
                {
                    return;
                }
            }

            Assert.Fail("Missing issue " + code + " field=" + field + "\n" + Describe(report));
        }

        private static string Describe(MxAnimationCompatibilityValidationReport report)
        {
            var lines = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationCompatibilityIssue issue = report.Issues[i];
                lines.Add(issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual);
            }

            return string.Join("\n", lines);
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

        [Test]
        public void SetDefinitionValidator_ValidatesBlend2DCoordinatesParametersAndClips()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
            var catalog = new ResourceCatalog(
                "demo.catalog",
                "demo.package",
                new[]
                {
                    new ResourceCatalogEntry(idle.Id, idle.TypeId, "memory", "idle"),
                    new ResourceCatalogEntry(right.Id, right.TypeId, "memory", "right")
                });
            var definition = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        "move.x",
                        string.Empty,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idle),
                            new MxAnimationBlend2DPoint(0, 0, right),
                            new MxAnimationBlend2DPoint(1000, 0, default)
                        })
                });

            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(definition, catalog);

            AssertIssue(report, "Blend2DParameterMissing");
            AssertIssue(report, "DuplicateBlend2DCoordinate");
            AssertIssue(report, "Blend2DClipMissing");
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

        private static MxAnimationSetDefinition Create2DHashDefinition(
            ResourceKey idle,
            ResourceKey right,
            ResourceKey up,
            string parameterX,
            string parameterY,
            int scaleX,
            int scaleY,
            int changedX,
            ResourceKey rightClip)
        {
            return new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion2d",
                        parameterX,
                        parameterY,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 0, idle),
                            new MxAnimationBlend2DPoint(1000 + changedX, 0, rightClip),
                            new MxAnimationBlend2DPoint(0, 1000, up)
                        },
                        scaleX,
                        scaleY)
                });
        }

        private static void Assert2DWeight(MxAnimationBlend2DWeights weights, ResourceKey clipKey, float expected)
        {
            for (int i = 0; i < weights.Weights.Count; i++)
            {
                if (weights.Weights[i].ClipKey != clipKey)
                    continue;

                Assert.AreEqual(expected, weights.Weights[i].Weight, 0.0001f);
                return;
            }

            Assert.Fail("Expected 2D blend weight for " + clipKey + ".");
        }

        private static float Sum2DWeights(MxAnimationBlend2DWeights weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Weights.Count; i++)
                sum += weights.Weights[i].Weight;
            return sum;
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
        public void Blend1DCalculator_EvaluatesIdleWalkRunWeightsFromQuantizedSpeed()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var walk = new ResourceKey("demo.animation.walk", ResourceTypeIds.AnimationClip);
            var run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
            var blend = new MxAnimationBlend1DDefinition(
                "locomotion",
                "locomotion.speed",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend1DPoint(0, idle),
                    new MxAnimationBlend1DPoint(500, walk),
                    new MxAnimationBlend1DPoint(1000, run)
                });

            MxAnimationBlend1DWeights idleWeights =
                MxAnimationBlend1DCalculator.Evaluate(blend, new MxAnimationQuantizedParameter("locomotion.speed", 0));
            MxAnimationBlend1DWeights blendWeights =
                MxAnimationBlend1DCalculator.Evaluate(blend, new MxAnimationQuantizedParameter("locomotion.speed", 750));
            MxAnimationBlend1DWeights runWeights =
                MxAnimationBlend1DCalculator.Evaluate(blend, new MxAnimationQuantizedParameter("locomotion.speed", 1200));

            Assert.AreEqual(1f, idleWeights.Weights[0].Weight);
            Assert.AreEqual(0.5f, blendWeights.Weights[1].Weight, 0.0001f);
            Assert.AreEqual(0.5f, blendWeights.Weights[2].Weight, 0.0001f);
            Assert.AreEqual(1f, runWeights.Weights[2].Weight);
            Assert.AreEqual(0.75f, blendWeights.Parameter.Value, 0.0001f);
        }

        [Test]
        public void Blend2DCalculator_EvaluatesDeterministicWeightsForCommonTopologies()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
            var up = new ResourceKey("demo.animation.up", ResourceTypeIds.AnimationClip);
            var diagonal = new ResourceKey("demo.animation.diagonal", ResourceTypeIds.AnimationClip);
            var square = new MxAnimationBlend2DDefinition(
                "locomotion2d",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(1000, 0, right),
                    new MxAnimationBlend2DPoint(0, 1000, up),
                    new MxAnimationBlend2DPoint(1000, 1000, diagonal)
                });

            MxAnimationBlend2DWeights exact =
                MxAnimationBlend2DCalculator.Evaluate(square, new MxAnimationQuantizedParameter("move.x", 0), new MxAnimationQuantizedParameter("move.y", 0));
            MxAnimationBlend2DWeights center =
                MxAnimationBlend2DCalculator.Evaluate(square, new MxAnimationQuantizedParameter("move.x", 500), new MxAnimationQuantizedParameter("move.y", 500));
            MxAnimationBlend2DWeights outside =
                MxAnimationBlend2DCalculator.Evaluate(square, new MxAnimationQuantizedParameter("move.x", 1500), new MxAnimationQuantizedParameter("move.y", 500));

            Assert2DWeight(exact, idle, 1f);
            Assert2DWeight(center, idle, 0.25f);
            Assert2DWeight(center, right, 0.25f);
            Assert2DWeight(center, up, 0.25f);
            Assert2DWeight(center, diagonal, 0.25f);
            Assert2DWeight(outside, right, 0.5f);
            Assert2DWeight(outside, diagonal, 0.5f);

            var triangle = new MxAnimationBlend2DDefinition(
                "triangle",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(1000, 0, right),
                    new MxAnimationBlend2DPoint(0, 1000, up)
                });
            MxAnimationBlend2DWeights triangleInside =
                MxAnimationBlend2DCalculator.Evaluate(triangle, new MxAnimationQuantizedParameter("move.x", 250), new MxAnimationQuantizedParameter("move.y", 250));
            MxAnimationBlend2DWeights triangleOutside =
                MxAnimationBlend2DCalculator.Evaluate(triangle, new MxAnimationQuantizedParameter("move.x", 1500), new MxAnimationQuantizedParameter("move.y", 0));

            Assert2DWeight(triangleInside, idle, 0.5f);
            Assert2DWeight(triangleInside, right, 0.25f);
            Assert2DWeight(triangleInside, up, 0.25f);
            Assert2DWeight(triangleOutside, right, 1f);

            var single = new MxAnimationBlend2DDefinition(
                "single",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[] { new MxAnimationBlend2DPoint(250, 250, idle) });
            var doublePoint = new MxAnimationBlend2DDefinition(
                "double",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(1000, 0, right)
                });
            var collinear = new MxAnimationBlend2DDefinition(
                "collinear",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(500, 500, right),
                    new MxAnimationBlend2DPoint(1000, 1000, up)
                });
            var degenerate = new MxAnimationBlend2DDefinition(
                "degenerate",
                "move.x",
                "move.y",
                MxAnimationLayerId.Base,
                new[]
                {
                    new MxAnimationBlend2DPoint(0, 0, idle),
                    new MxAnimationBlend2DPoint(0, 0, right),
                    new MxAnimationBlend2DPoint(1000, 0, up)
                });

            Assert2DWeight(
                MxAnimationBlend2DCalculator.Evaluate(single, new MxAnimationQuantizedParameter("move.x", -500), new MxAnimationQuantizedParameter("move.y", 900)),
                idle,
                1f);
            MxAnimationBlend2DWeights doubleWeights =
                MxAnimationBlend2DCalculator.Evaluate(doublePoint, new MxAnimationQuantizedParameter("move.x", 250), new MxAnimationQuantizedParameter("move.y", 500));
            Assert2DWeight(doubleWeights, idle, 0.75f);
            Assert2DWeight(doubleWeights, right, 0.25f);
            MxAnimationBlend2DWeights collinearWeights =
                MxAnimationBlend2DCalculator.Evaluate(collinear, new MxAnimationQuantizedParameter("move.x", 750), new MxAnimationQuantizedParameter("move.y", 750));
            Assert2DWeight(collinearWeights, right, 0.5f);
            Assert2DWeight(collinearWeights, up, 0.5f);
            Assert.AreEqual(1f, Sum2DWeights(MxAnimationBlend2DCalculator.Evaluate(degenerate, new MxAnimationQuantizedParameter("move.x", 250), new MxAnimationQuantizedParameter("move.y", 0))), 0.0001f);
        }

        [Test]
        public void SetDefinitionHash_IncludesBlend1DDefinition()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var walk = new ResourceKey("demo.animation.walk", ResourceTypeIds.AnimationClip);
            var run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
            var first = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idle),
                            new MxAnimationBlend1DPoint(500, walk),
                            new MxAnimationBlend1DPoint(1000, run)
                        })
                });
            var changed = new MxAnimationSetDefinition(
                "demo.set",
                1,
                idle,
                idle,
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, idle),
                            new MxAnimationBlend1DPoint(400, walk),
                            new MxAnimationBlend1DPoint(1000, run)
                        })
                });

            Assert.That(first.DefinitionHash, Does.StartWith(MxAnimationSetDefinitionHasher.HashPrefix));
            Assert.AreNotEqual(first.DefinitionHash, changed.DefinitionHash);
        }

        [Test]
        public void SetDefinitionHash_IncludesBlend2DDefinition()
        {
            var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
            var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
            var up = new ResourceKey("demo.animation.up", ResourceTypeIds.AnimationClip);
            var changedClip = new ResourceKey("demo.animation.changed", ResourceTypeIds.AnimationClip);
            var first = Create2DHashDefinition(idle, right, up, "move.x", "move.y", 1000, 1000, 0, right);
            var changedCoordinate = Create2DHashDefinition(idle, right, up, "move.x", "move.y", 1000, 1000, 250, right);
            var changedParameter = Create2DHashDefinition(idle, right, up, "move.horizontal", "move.y", 1000, 1000, 0, right);
            var changedScale = Create2DHashDefinition(idle, right, up, "move.x", "move.y", 100, 1000, 0, right);
            var changedPointClip = Create2DHashDefinition(idle, right, up, "move.x", "move.y", 1000, 1000, 0, changedClip);

            Assert.That(first.DefinitionHash, Does.StartWith(MxAnimationSetDefinitionHasher.HashPrefix));
            Assert.AreNotEqual(first.DefinitionHash, changedCoordinate.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changedParameter.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changedScale.DefinitionHash);
            Assert.AreNotEqual(first.DefinitionHash, changedPointClip.DefinitionHash);
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
