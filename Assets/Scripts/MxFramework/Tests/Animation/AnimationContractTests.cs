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

        private static void AssertIssue(ResourceCatalogValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected animation validation issue: " + code);
        }
    }
}
