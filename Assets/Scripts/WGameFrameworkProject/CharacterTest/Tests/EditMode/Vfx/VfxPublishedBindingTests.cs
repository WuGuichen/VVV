using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed partial class VfxManualCompositionTests
    {
        [Test]
        public void ConsumerCompatibility_OneShotActiveFramesUsesVisibleLifecyclePhases()
        {
            var receipt = new VfxCompositionBakeReceipt
            {
                Phases = new List<VfxCompositionBakedPhaseView>
                {
                    new VfxCompositionBakedPhaseView { Phase = "Build", DurationFrames = 2 },
                    new VfxCompositionBakedPhaseView { Phase = "Rebuild", DurationFrames = 50 },
                    new VfxCompositionBakedPhaseView { Phase = "Loop", DurationFrames = 3 },
                    new VfxCompositionBakedPhaseView { Phase = "Complete", DurationFrames = 4 },
                    new VfxCompositionBakedPhaseView { Phase = "Dissipate", DurationFrames = 60 }
                }
            };

            Assert.That(VfxConsumerCompatibilityValidator.ResolveOneShotActiveFrames(receipt), Is.EqualTo(9));
        }

        [Test]
        public void ConsumerCompatibility_RejectsUnsafeChargeCompression()
        {
            EnsureAssetFolder("Assets/VFX/MxCandidates/Manual");
            var root = new GameObject("skill-candidate");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, SkillCandidatePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var receipt = new VfxCompositionBakeReceipt
            {
                OutputPrefabPath = SkillCandidatePath,
                OutputPrefabGuid = AssetDatabase.AssetPathToGUID(SkillCandidatePath),
                CandidateContentHash = VfxCompositionDraftService.HashFile(SkillCandidatePath),
                LoopPlaybackMode = "OneShot",
                Phases = new List<VfxCompositionBakedPhaseView>
                {
                    new VfxCompositionBakedPhaseView { Phase = "Build", DurationFrames = 20 },
                    new VfxCompositionBakedPhaseView { Phase = "Loop", DurationFrames = 6 }
                }
            };
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");

            VfxConsumerCompatibilityReport compatible =
                VfxConsumerCompatibilityValidator.Validate(
                    VfxConsumerContractResolver.ResolveFireballBurst()[0],
                    receipt);
            Assert.IsTrue(compatible.Compatible, compatible.Summary);

            receipt.Phases[1].DurationFrames = 7;
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            VfxConsumerCompatibilityReport blocked =
                VfxConsumerCompatibilityValidator.Validate(
                    VfxConsumerContractResolver.ResolveFireballBurst()[0],
                    receipt);
            Assert.IsFalse(blocked.Compatible);
            Assert.That(blocked.Summary, Does.Contain("超过技能前摇"));
        }

        [Test]
        public void ConsumerContractResolver_ReadsCurrentFireballBurstAuthority()
        {
            IReadOnlyList<VfxConsumerContract> contracts =
                VfxConsumerContractResolver.ResolveFireballBurst();

            Assert.That(contracts, Has.Count.EqualTo(3));
            Assert.That(contracts[0].ResourceKey, Is.EqualTo(new ResourceKey("vfx.authored.3fc1a5a8-a28b-4d01-a9ba-21083b97bc38", ResourceTypeIds.GameObject, packageId: "mxframework.samples")));
            Assert.That(contracts[0].ActiveFrames, Is.EqualTo(26));
            Assert.That(contracts[0].Anchor, Is.EqualTo("FollowSocket:mainHand"));
            Assert.That(contracts[1].ResourceKey, Is.EqualTo(new ResourceKey("vfx.authored.2c98b752-503c-41af-933e-264fe69cceb8", ResourceTypeIds.GameObject, packageId: "mxframework.samples")));
            Assert.That(contracts[1].ActiveFrames, Is.EqualTo(90));
            Assert.That(contracts[1].LoopMode, Is.EqualTo("Continuous"));
            Assert.That(contracts[2].ResourceKey, Is.EqualTo(new ResourceKey("vfx.authored.sample.fireball_impact", ResourceTypeIds.GameObject, packageId: "mxframework.samples")));
            Assert.That(contracts[2].ActiveFrames, Is.EqualTo(112));
            Assert.That(contracts[2].Anchor, Is.EqualTo("HitPoint"));
            Assert.That(contracts.All(value => !string.IsNullOrWhiteSpace(value.ContractHash)), Is.True);
        }

        [Test]
        public void ConsumerVariantCompiler_ShrinksBodyWithoutMutatingSourceEntryExit()
        {
            var source = new VfxCompositionDraftView
            {
                DraftId = "vfx.composition.test.consumer",
                LoopPlaybackMode = "Continuous",
                Layers = new List<VfxCompositionLayerView>
                {
                    new VfxCompositionLayerView
                    {
                        LayerId = "build",
                        Phase = "Build",
                        Timing = new VfxLayerTimingView
                        {
                            RetimeMode = nameof(VfxRetimeMode.ExtendBody),
                            SourceSegments = new VfxTimeSegmentsView
                            {
                                EntryFrames = 1,
                                BodyFrames = 300,
                                ExitFrames = 1,
                                MinimumBodyFrames = 1
                            },
                            TargetArtisticFrames = 302
                        }
                    },
                    new VfxCompositionLayerView
                    {
                        LayerId = "loop",
                        Phase = "Loop",
                        Timing = new VfxLayerTimingView
                        {
                            RetimeMode = nameof(VfxRetimeMode.ExtendBody),
                            SourceSegments = new VfxTimeSegmentsView
                            {
                                BodyFrames = 178,
                                MinimumBodyFrames = 1
                            },
                            TargetArtisticFrames = 178
                        }
                    }
                }
            };

            bool success = VfxConsumerContractVariantCompiler.TryCompile(
                source,
                VfxConsumerContractResolver.ResolveFireballBurst()[0],
                out VfxCompositionDraftView variant,
                out string message);

            Assert.IsTrue(success, message);
            Assert.That(source.Layers[0].Timing.TargetArtisticFrames, Is.EqualTo(302));
            Assert.That(variant, Is.Not.SameAs(source));
            Assert.That(variant.LoopPlaybackMode, Is.EqualTo("OneShot"));
            Assert.That(
                variant.PhaseDurationOverrides.BuildFrames + variant.PhaseDurationOverrides.LoopFrames,
                Is.EqualTo(26));
            Assert.That(variant.Layers[0].Timing.SourceSegments.EntryFrames, Is.EqualTo(1));
            Assert.That(variant.Layers[0].Timing.SourceSegments.ExitFrames, Is.EqualTo(1));
            Assert.That(variant.Layers[0].Timing.TargetArtisticFrames, Is.LessThan(302));
        }

        [Test]
        public void ReadyApprovalReceipt_BindsPrefabAndConsumerContract()
        {
            EnsureAssetFolder("Assets/VFX/MxCandidates/Manual");
            var root = new GameObject("approval-prefab");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, SkillCandidatePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            string receiptPath = "Temp/MxFramework/VfxBindingActivation/ready-approval-test.json";
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            VfxConsumerContract contract = VfxConsumerContractResolver.ResolveFireballBurst()[2];
            var receipt = new VfxReadyApprovalReceipt
            {
                EffectId = contract.ResourceKey.Id,
                ResourceKey = contract.ResourceKey.ToString(),
                PrefabPath = SkillCandidatePath,
                PrefabGuid = AssetDatabase.AssetPathToGUID(SkillCandidatePath),
                PrefabContentHash = VfxCompositionDraftService.HashFile(SkillCandidatePath),
                ConsumerContractHash = contract.ContractHash,
                ApprovedBy = "test",
                ApprovedUtc = "2026-07-16T00:00:00Z"
            };
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt, Formatting.Indented));
            var effect = new JObject
            {
                ["approval"] = new JObject
                {
                    ["receiptPath"] = receiptPath,
                    ["receiptHash"] = receipt.ReceiptHash
                }
            };

            Assert.IsTrue(
                VfxManualCompositionReviewService.ValidateReadyApproval(
                    effect,
                    contract,
                    SkillCandidatePath,
                    out string message),
                message);

            receipt.PrefabContentHash = "sha256:tampered";
            File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt, Formatting.Indented));
            Assert.IsFalse(
                VfxManualCompositionReviewService.ValidateReadyApproval(
                    effect,
                    contract,
                    SkillCandidatePath,
                    out _));
            File.Delete(receiptPath);
        }

        [Test]
        public void PublishedUnityBindingAsset_ResolvesStableResourceKey()
        {
            var prefab = new GameObject("binding-prefab");
            var asset = ScriptableObject.CreateInstance<VfxPublishedUnityBindingAsset>();
            try
            {
                ResourceKey key = VfxConsumerContractResolver.FireballBurstImpactKey;
                var binding = new VfxUnityPrefabBinding();
                binding.Configure(key, "mxframework.samples/vfx/fireball_burst/impact", prefab);
                asset.Configure("sha256:test", new List<VfxUnityPrefabBinding> { binding });

                Assert.IsTrue(asset.TryResolve(key, out VfxUnityPrefabBinding resolved));
                Assert.AreSame(prefab, resolved.Prefab);
                Assert.AreEqual(key, resolved.ResourceKey);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void StablePublisher_ExportsVfxRuntimeBinaryAndRefreshesPlayerMirror()
        {
            string[] paths =
            {
                VfxStablePrefabPublicationService.DefinitionBinaryCatalogPath,
                VfxStablePrefabPublicationService.PlayerDefinitionCatalogPath,
                VfxStablePrefabPublicationService.PlayerDefinitionBinaryCatalogPath
            };
            var backups = paths.ToDictionary(
                path => path,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null,
                StringComparer.OrdinalIgnoreCase);
            try
            {
                Assert.DoesNotThrow(VfxStablePrefabPublicationService.PublishRuntimeDefinitions);
                Assert.That(File.ReadAllBytes(VfxStablePrefabPublicationService.DefinitionBinaryCatalogPath), Is.Not.Empty);
                Assert.That(
                    File.ReadAllBytes(VfxStablePrefabPublicationService.PlayerDefinitionCatalogPath),
                    Is.EqualTo(File.ReadAllBytes(VfxStablePrefabPublicationService.DefinitionCatalogPath)));
                Assert.That(
                    File.ReadAllBytes(VfxStablePrefabPublicationService.PlayerDefinitionBinaryCatalogPath),
                    Is.EqualTo(File.ReadAllBytes(VfxStablePrefabPublicationService.DefinitionBinaryCatalogPath)));
            }
            finally
            {
                foreach (KeyValuePair<string, byte[]> backup in backups)
                {
                    if (backup.Value == null)
                    {
                        if (File.Exists(backup.Key)) File.Delete(backup.Key);
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(backup.Key);
                        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                        File.WriteAllBytes(backup.Key, backup.Value);
                    }
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void ConsumerEvidence_UnrelatedVfxCatalogChangeDoesNotInvalidateImpactContract()
        {
            string path = VfxStablePrefabPublicationService.DefinitionCatalogPath;
            byte[] original = File.ReadAllBytes(path);
            try
            {
                string before = VfxConsumerContractResolver.Find(
                    VfxConsumerContractResolver.ResolveFireballBurst()[2].ResourceKey).ContractHash;
                JObject definitions = JObject.Parse(File.ReadAllText(path));
                definitions["publicationOrderRegression"] = Guid.NewGuid().ToString("N");
                File.WriteAllText(path, definitions.ToString(Formatting.Indented) + Environment.NewLine);
                string after = VfxConsumerContractResolver.Find(
                    VfxConsumerContractResolver.ResolveFireballBurst()[2].ResourceKey).ContractHash;

                Assert.That(after, Is.EqualTo(before));

                VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifestUnchecked();
                Assert.That(manifest, Is.Not.Null);
                VfxPublishedBindingEditorService.ApplyCurrentConsumerContractEvidence(manifest);
                manifest.ManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);

                Assert.That(
                    VfxPublishedBindingEditorService.ValidateManifest(manifest, out string message),
                    Is.True,
                    message);
            }
            finally
            {
                File.WriteAllBytes(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void ConsumerEvidence_RelatedDefinitionChangeRefreshesBeforeStrictValidation()
        {
            string path = VfxStablePrefabPublicationService.DefinitionCatalogPath;
            byte[] original = File.ReadAllBytes(path);
            try
            {
                string before = VfxConsumerContractResolver.Find(
                    VfxConsumerContractResolver.ResolveFireballBurst()[2].ResourceKey).ContractHash;
                JObject root = JObject.Parse(File.ReadAllText(path));
                JObject definition = (root["definitions"] as JArray)?.OfType<JObject>().First(value =>
                    value.Value<int>("definitionId") == 20000);
                definition["defaultLifetimeFrames"] = definition.Value<int>("defaultLifetimeFrames") + 1;
                File.WriteAllText(path, root.ToString(Formatting.Indented) + Environment.NewLine);
                string after = VfxConsumerContractResolver.Find(
                    VfxConsumerContractResolver.ResolveFireballBurst()[2].ResourceKey).ContractHash;

                Assert.That(after, Is.Not.EqualTo(before));
                VfxPublishedBindingManifest manifest = VfxPublishedBindingEditorService.LoadManifestUnchecked();
                VfxPublishedBindingEditorService.ApplyCurrentConsumerContractEvidence(manifest);
                manifest.ManifestHash = VfxPublishedBindingEditorService.ComputeManifestHash(manifest);
                Assert.That(
                    VfxPublishedBindingEditorService.ValidateManifest(manifest, out string message),
                    Is.True,
                    message);
            }
            finally
            {
                File.WriteAllBytes(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

    }

    public sealed class VfxPublishedBindingActivationFinalizationTests
    {
        private const string TestRoot = "Assets/MxFrameworkGenerated/Vfx/ActivationFinalizationTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [TestCase("receipt-import")]
        [TestCase("override-clear")]
        [TestCase("override-refresh")]
        [TestCase("success")]
        public void FinalizationTransaction_RestoresFailuresAndCommitsOnlyCompleteSuccess(string outcome)
        {
            string token = Guid.NewGuid().ToString("N");
            string caseRoot = TestRoot + "/" + token;
            string existingRoot = caseRoot + "/ExistingReceipts";
            string receiptRoot = caseRoot + "/Receipts";
            string overrideRoot = caseRoot + "/Overrides";
            EnsureFolder(existingRoot);
            EnsureFolder(overrideRoot);
            string existingReceiptPath = existingRoot + "/fireball_burst_existing.json";
            File.WriteAllText(existingReceiptPath, "{\"sentinel\":\"existing\"}\n");
            AssetDatabase.ImportAsset(existingReceiptPath, ImportAssetOptions.ForceSynchronousImport);
            byte[] existingReceiptBytes = File.ReadAllBytes(existingReceiptPath);
            byte[] existingReceiptMeta = File.ReadAllBytes(existingReceiptPath + ".meta");
            string existingReceiptGuid = AssetDatabase.AssetPathToGUID(existingReceiptPath);
            string overridePath = overrideRoot + "/playtest-overrides.json";
            File.WriteAllText(overridePath, "{\"entries\":[{\"id\":\"sentinel\"}]}\n");
            AssetDatabase.ImportAsset(overridePath, ImportAssetOptions.ForceSynchronousImport);
            byte[] overrideBytes = File.ReadAllBytes(overridePath);
            byte[] overrideMeta = File.ReadAllBytes(overridePath + ".meta");
            string overrideGuid = AssetDatabase.AssetPathToGUID(overridePath);
            int importAttempts = 0;
            Func<string, bool> importer = outcome == "receipt-import"
                ? path =>
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    importAttempts++;
                    return false;
                }
                : null;
            Action<string> clearer = outcome == "override-clear"
                ? _ => throw new IOException("simulated override clear failure")
                : path =>
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                };
            int refreshAttempts = 0;
            Action refresher = outcome == "override-refresh"
                ? () =>
                {
                    refreshAttempts++;
                    if (refreshAttempts == 1) throw new InvalidOperationException("simulated runtime binding refresh failure");
                }
                : () => { refreshAttempts++; };
            var receipt = new VfxBindingActivationReceipt
            {
                BindingSetId = "vfx.skill.test",
                PlanHash = "sha256:test",
                PreviousManifestHash = "sha256:before",
                ActivatedManifestHash = "sha256:after",
                ActivatedUtc = DateTime.UtcNow.ToString("O"),
                ReceiptHash = "sha256:receipt"
            };
            string newReceiptPath;
            using (var transaction = new VfxBindingActivationFinalizationTransaction(
                       receiptRoot, overridePath, receiptImporter: importer,
                       overrideClearer: clearer, runtimeRefresher: refresher))
            {
                newReceiptPath = transaction.ReceiptPath;
                bool prepared = transaction.PrepareReceipt(receipt, out string prepareMessage);
                if (outcome == "receipt-import")
                {
                    Assert.That(prepared, Is.False);
                    Assert.That(prepareMessage, Does.Contain("prepare failed"));
                }
                else
                {
                    Assert.That(prepared, Is.True, prepareMessage);
                    bool committed = transaction.ClearOverrideAndCommit(out string commitMessage);
                    Assert.That(committed, Is.EqualTo(outcome == "success"), commitMessage);
                }
            }

            Assert.That(File.ReadAllBytes(existingReceiptPath), Is.EqualTo(existingReceiptBytes));
            Assert.That(File.ReadAllBytes(existingReceiptPath + ".meta"), Is.EqualTo(existingReceiptMeta));
            Assert.That(AssetDatabase.AssetPathToGUID(existingReceiptPath), Is.EqualTo(existingReceiptGuid));
            if (outcome == "success")
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(newReceiptPath), Is.Not.Null);
                Assert.That(File.Exists(overridePath), Is.False);
                Assert.That(File.Exists(overridePath + ".meta"), Is.False);
            }
            else
            {
                Assert.That(File.Exists(newReceiptPath), Is.False);
                Assert.That(File.Exists(newReceiptPath + ".meta"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(receiptRoot), Is.False);
                Assert.That(File.ReadAllBytes(overridePath), Is.EqualTo(overrideBytes));
                Assert.That(File.ReadAllBytes(overridePath + ".meta"), Is.EqualTo(overrideMeta));
                Assert.That(AssetDatabase.AssetPathToGUID(overridePath), Is.EqualTo(overrideGuid));
            }
            if (outcome == "receipt-import") Assert.That(importAttempts, Is.EqualTo(1));
            if (outcome == "override-refresh") Assert.That(refreshAttempts, Is.EqualTo(2));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
