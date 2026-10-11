using System.IO;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionVfxSelectionTests
    {
        [Test]
        public void ResourceKeyParser_RoundTripsQualifiedVfxKey()
        {
            const string serialized =
                "vfx.magic.fireball_burst.charge:GameObject@mxframework.samples";

            Assert.That(
                CharacterActionVfxSelectionCatalog.TryParseResourceKey(serialized, out ResourceKey key),
                Is.True);
            Assert.That(key.Id, Is.EqualTo("vfx.magic.fireball_burst.charge"));
            Assert.That(key.TypeId, Is.EqualTo(ResourceTypeIds.GameObject));
            Assert.That(key.PackageId, Is.EqualTo("mxframework.samples"));
            Assert.That(key.ToString(), Is.EqualTo(serialized));
        }

        [Test]
        public void PublishedProjection_ResolvesFireballBurstChargePrefab()
        {
            VfxPublishedUnityBindingAsset projection =
                AssetDatabase.LoadAssetAtPath<VfxPublishedUnityBindingAsset>(
                    "Assets/MxFrameworkGenerated/Vfx/vfx_published_unity_bindings.asset");
            Assert.That(projection, Is.Not.Null);

            var key = new ResourceKey(
                "vfx.magic.fireball_burst.charge",
                ResourceTypeIds.GameObject,
                packageId: "mxframework.samples");
            Assert.That(projection.TryResolve(key, out VfxUnityPrefabBinding binding), Is.True);
            Assert.That(binding.Prefab, Is.Not.Null);
        }

        [Test]
        public void CardSnapshot_WhenGlobalRegistryIsUnavailable_UsesIdentityValidatedProjection()
        {
            IVfxUnityPrefabBindingSource previous = VfxUnityPrefabBindingSourceRegistry.Current;
            try
            {
                VfxUnityPrefabBindingSourceRegistry.Clear();
                CharacterActionVfxCardSnapshot snapshot = CharacterActionVfxCardSnapshot.Resolve(
                    "vfx.authored.sample.fireball_impact:GameObject@mxframework.samples");

                Assert.That(
                    snapshot.ActualPrefabPath,
                    Is.EqualTo(
                        "Assets/VFX/MxFramework/Published/"
                        + "mxframework_samples_vfx_authored_sample_fireball_impact.prefab"));
            }
            finally
            {
                VfxUnityPrefabBindingSourceRegistry.SetCurrent(previous);
            }
        }

        [Test]
        public void SemanticMismatch_IsAdvisory_ExplicitSelectionBlockStillBlocks()
        {
            var impact = new CharacterActionVfxSelectionOption
            {
                Role = "Impact",
                Selectable = true
            };

            Assert.That(CharacterActionVfxPickerWindow.IsRoleRecommended(impact, "Charge"), Is.False);
            Assert.That(CharacterActionVfxPickerWindow.IsSelectionUsable(impact, null), Is.True,
                "An inferred role mismatch is advisory and must not disable a published VFX.");
            Assert.That(CharacterActionVfxPickerWindow.IsContractCompatible(
                impact, _ => "semantic suggestion"), Is.False);
            Assert.That(CharacterActionVfxPickerWindow.IsSelectionUsable(impact, null), Is.True,
                "A semantic diagnostic is displayed separately from hard selection blockers.");

            var candidate = new CharacterActionVfxSelectionOption
            {
                Role = "Impact",
                Selectable = true,
                IsCandidate = true
            };
            Assert.That(
                CharacterActionVfxPickerWindow.IsSelectionUsable(candidate, _ => "incompatible"),
                Is.False,
                "A Candidate with an explicit stable-identity blocker must remain unavailable.");
            Assert.That(CharacterActionVfxPickerWindow.IsSelectionUsable(candidate, null), Is.True);

            impact.Selectable = false;
            Assert.That(CharacterActionVfxPickerWindow.IsSelectionUsable(impact, null), Is.False,
                "A missing or stale resource remains unavailable independently of semantic role.");
        }

        [Test]
        public void SelectionCatalog_DoesNotExposeUnpublishedDraftCandidate()
        {
            const string draftId = "character_action_vfx_selection_unbound_test";
            const string assetFolder = "Assets/VFX/MxCandidates/Manual/CharacterActionVfxSelectionTests";
            const string prefabPath = assetFolder + "/unbound.prefab";
            string receiptPath = VfxCompositionCandidateBaker.ReceiptRoot + "/"
                + VfxCompositionDraftService.FileToken(draftId) + ".json";
            Directory.CreateDirectory(VfxCompositionCandidateBaker.ReceiptRoot);
            VfxCompositionDraftService.EnsureFolder(assetFolder);
            var root = new GameObject("UnboundDraftCandidate");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
                var receipt = new VfxCompositionBakeReceipt
                {
                    DraftId = draftId,
                    OutputPrefabPath = prefabPath,
                    OutputPrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                    CandidateContentHash = VfxCompositionDraftService.HashFile(prefabPath),
                    LoopPlaybackMode = "OneShot",
                    Phases = new System.Collections.Generic.List<VfxCompositionBakedPhaseView>
                    {
                        new VfxCompositionBakedPhaseView { Phase = "Build", DurationFrames = 0 },
                        new VfxCompositionBakedPhaseView { Phase = "Loop", DurationFrames = 72 }
                    }
                };
                receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
                File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt, Formatting.Indented));

                using CharacterActionVfxSelectionCatalog catalog =
                    CharacterActionVfxSelectionCatalog.Load(
                        VfxConsumerContractResolver.FireballBurstImpactKey.ToString());
                CharacterActionVfxSelectionOption option = catalog.Options.Find(
                    value => value.IsCandidate || value.Receipt?.DraftId == draftId);

                Assert.That(option, Is.Null,
                    "消费方 Picker 只能选择已发布的稳定 ResourceKey，暂存 Candidate 不得进入正式选择流程。");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
                AssetDatabase.DeleteAsset(assetFolder);
            }
        }
    }
}
