using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests
{
    // Pure JSON projections: no AssetDatabase writes, scene changes or publication calls.
    public sealed class VfxConsumerCurrentContractTests
    {
        private sealed class Inputs
        {
            internal JObject Actions = JObject.Parse(@"{'actions':[{'actionId':910,'gameplayTrack':{'events':[{'kind':'SendGameplayRequest','requestLabel':'character_test.fireball_burst.projectile'}]},'presentationTrack':{'events':[{'kind':'SpawnVisualCue','frame':0,'endFrame':25,'resourceKey':'vfx.authored.charge:GameObject#skin@pkg','vfxAttachment':{'mode':'FollowSocket','socketId':'mainHand'}}]}}]}");
            internal JObject Projectiles = JObject.Parse(@"{'definitions':[{'definitionId':91002,'stableId':'character_test.fireball_burst.projectile','motion':{'lifetimeFrames':90},'impact':{'policyKind':'AreaPulseOnImpact'}}]}");
            internal JObject Bindings = JObject.Parse(@"{'bindings':[{'projectileDefinitionId':91002,'slots':{'flightBody':{'resource':{'id':'vfx.authored.flight','typeId':'GameObject','packageId':'pkg'},'orientation':'ProjectileForward'},'areaImpact':{'resource':{'id':'vfx.authored.impact','typeId':'GameObject','packageId':'pkg'},'vfxDefinitionId':20000,'lifetimeFramesOverride':112}},'exitPolicy':{'runtimeReset':'Immediate'}}]}");
            internal JObject Definitions = JObject.Parse(@"{'definitions':[{'definitionId':20000,'prefabResource':{'id':'vfx.authored.impact','typeId':'GameObject','packageId':'pkg'},'defaultLifetimeFrames':72}]}");
            internal IReadOnlyList<VfxConsumerContract> Resolve() =>
                VfxConsumerContractResolver.ResolveFireballBurst(Actions, Projectiles, Bindings, Definitions);
            internal JObject Area => (JObject)Bindings["bindings"][0]["slots"]["areaImpact"];
            internal JObject Flight => (JObject)Bindings["bindings"][0]["slots"]["flightBody"];
            internal JObject Cue => (JObject)Actions["actions"][0]["presentationTrack"]["events"][0];
        }

        [Test]
        public void CurrentBundle_UsesAuthoredResourceKeysAndActualTiming()
        {
            var input = new Inputs();
            ((JArray)input.Actions["actions"]).Insert(0, JObject.Parse(@"{'actionId':1,'gameplayTrack':null}"));
            IReadOnlyList<VfxConsumerContract> contracts = input.Resolve();
            Assert.That(contracts.Select(value => value.ResourceKey.Id),
                Is.EqualTo(new[] { "vfx.authored.charge", "vfx.authored.flight", "vfx.authored.impact" }));
            Assert.That(contracts[0].ResourceKey.Variant, Is.EqualTo("skin"));
            Assert.That(contracts[0].ResourceKey.PackageId, Is.EqualTo("pkg"));
            Assert.That(contracts[0].ActiveFrames, Is.EqualTo(26));
            Assert.That(contracts[0].Anchor, Is.EqualTo("FollowSocket:mainHand"));
            Assert.That(contracts[1].ActiveFrames, Is.EqualTo(90));
            Assert.That(contracts[1].LoopMode, Is.EqualTo("Continuous"));
            Assert.That(contracts[2].ActiveFrames, Is.EqualTo(112));
            Assert.That(contracts.All(value => !string.IsNullOrEmpty(value.ContractHash)), Is.True);
        }

        [Test]
        public void AreaTimingChange_StalesImpactEvidenceAndPreservesFlightEvidence()
        {
            var input = new Inputs();
            var before = input.Resolve();
            input.Area["lifetimeFramesOverride"] = 113;
            var after = input.Resolve();
            Assert.That(after[0].ContractHash, Is.EqualTo(before[0].ContractHash));
            Assert.That(after[1].ContractHash, Is.EqualTo(before[1].ContractHash));
            Assert.That(after[2].ContractHash, Is.Not.EqualTo(before[2].ContractHash));
        }

        [Test]
        public void FlightChanges_InvalidateFlightEvidence()
        {
            var input = new Inputs();
            string before = input.Resolve()[1].ContractHash;
            input.Flight["orientation"] = "WorldUp";
            Assert.That(input.Resolve()[1].ContractHash, Is.Not.EqualTo(before));
            before = input.Resolve()[1].ContractHash;
            input.Projectiles["definitions"][0]["motion"]["lifetimeFrames"] = 91;
            Assert.That(input.Resolve()[1].ContractHash, Is.Not.EqualTo(before));
            before = input.Resolve()[1].ContractHash;
            input.Bindings["bindings"][0]["exitPolicy"]["runtimeReset"] = "Dissipate";
            Assert.That(input.Resolve()[1].ContractHash, Is.Not.EqualTo(before));
        }

        [Test]
        public void ChargeAttachmentChange_InvalidatesChargeEvidenceOnly()
        {
            var input = new Inputs();
            var before = input.Resolve();
            input.Cue["vfxAttachment"]["socketId"] = "offHand";
            var after = input.Resolve();
            Assert.That(after[0].Anchor, Is.EqualTo("FollowSocket:offHand"));
            Assert.That(after[0].ContractHash, Is.Not.EqualTo(before[0].ContractHash));
            Assert.That(after[1].ContractHash, Is.EqualTo(before[1].ContractHash));
        }

        [Test]
        public void LegacyReplacementKeys_AreReadFromConsumersWithoutHardcodedIdentity()
        {
            var input = new Inputs();
            input.Cue["resourceKey"] = "vfx.magic.fireball_burst.charge:GameObject@pkg";
            input.Flight["resource"]["id"] = "vfx.magic.fireball_burst.flight";
            input.Area["resource"]["id"] = "vfx.magic.fireball_burst.impact";
            input.Definitions["definitions"][0]["prefabResource"]["id"] = "vfx.magic.fireball_burst.impact";
            var contracts = input.Resolve();
            Assert.That(contracts[1].ResourceKey.Id, Is.EqualTo("vfx.magic.fireball_burst.flight"));
            Assert.That(contracts[1].ActiveFrames, Is.EqualTo(90));
        }

        [Test]
        public void MissingOrAmbiguousInputs_FailClosedWithoutSyntheticNullEvidence()
        {
            var input = new Inputs();
            input.Bindings["bindings"] = new JArray();
            Assert.Throws<InvalidDataException>(() => input.Resolve());
            input = new Inputs();
            ((JArray)input.Bindings["bindings"]).Add(input.Bindings["bindings"][0].DeepClone());
            Assert.Throws<InvalidDataException>(() => input.Resolve());
            input = new Inputs();
            input.Cue["vfxAttachment"]["socketId"] = "";
            Assert.Throws<InvalidDataException>(() => input.Resolve());
            input = new Inputs();
            input.Cue["endFrame"] = -1;
            Assert.Throws<InvalidDataException>(() => input.Resolve());
        }

        [Test]
        public void ImpactMapping_UsesFullKeyAndRejectsStalePositiveCache()
        {
            var input = new Inputs();
            input.Area["vfxDefinitionId"] = 12002;
            Assert.Throws<InvalidDataException>(() => input.Resolve());
            input.Area["vfxDefinitionId"] = 0;
            Assert.That(input.Resolve()[2].ActiveFrames, Is.EqualTo(112));
            input.Definitions["definitions"][0]["prefabResource"]["packageId"] = "other";
            Assert.Throws<InvalidDataException>(() => input.Resolve());
            input = new Inputs();
            ((JArray)input.Definitions["definitions"]).Add(input.Definitions["definitions"][0].DeepClone());
            Assert.Throws<InvalidDataException>(() => input.Resolve());
        }

        [Test]
        public void UnrelatedVfxDefinition_DoesNotInvalidateBundleEvidence()
        {
            var input = new Inputs();
            string[] before = input.Resolve().Select(value => value.ContractHash).ToArray();
            ((JArray)input.Definitions["definitions"]).Add(JObject.Parse(@"{'definitionId':888,'prefabResource':{'id':'unrelated','typeId':'GameObject'}}"));
            Assert.That(input.Resolve().Select(value => value.ContractHash), Is.EqualTo(before));
        }

        [Test]
        public void PublishedEvidence_RequiresEachActualConsumerHashAndRejectsDuplicates()
        {
            var input = new Inputs();
            var contracts = input.Resolve();
            var manifest = new VfxPublishedBindingManifest();
            foreach (var contract in contracts)
                manifest.Bindings.Add(new VfxPublishedBindingRecord {
                    ResourceKey = new VfxPublishedResourceKey {
                        Id=contract.ResourceKey.Id, TypeId=contract.ResourceKey.TypeId,
                        Variant=contract.ResourceKey.Variant, PackageId=contract.ResourceKey.PackageId },
                    ContractHash=contract.ContractHash });
            Assert.That(VfxConsumerContractResolver.ValidateEvidence(manifest, contracts, out _), Is.True);
            manifest.Bindings[1].ContractHash = "";
            Assert.That(VfxConsumerContractResolver.ValidateEvidence(manifest, contracts, out string message), Is.False);
            Assert.That(message, Does.Contain("vfx.authored.flight"));
            manifest.Bindings[1].ContractHash = contracts[1].ContractHash;
            input.Area["lifetimeFramesOverride"] = 113;
            Assert.That(VfxConsumerContractResolver.ValidateEvidence(manifest, input.Resolve(), out message), Is.False);
            Assert.That(message, Does.Contain("vfx.authored.impact"));
            manifest.Bindings.Add(manifest.Bindings[0]);
            Assert.That(VfxConsumerContractResolver.ValidateEvidence(manifest, contracts, out _), Is.False);
        }
    }
}
