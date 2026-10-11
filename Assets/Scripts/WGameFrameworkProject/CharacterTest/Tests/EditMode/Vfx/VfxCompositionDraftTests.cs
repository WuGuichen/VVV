using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests
{
    public sealed partial class VfxManualCompositionTests
    {
        private const string RetainedImpactDraftPath = "Assets/Config/MxFramework/Vfx/Compositions/vfx_composition_sample_fireball_impact.json";

        private static VfxSourceElementCatalog LoadElementCatalogForTests()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            if (catalog.Elements.Count == 0)
            {
                VfxCompositionDraftView draft = new VfxCompositionDraftService().Load(RetainedImpactDraftPath, out string message);
                Assert.That(draft, Is.Not.Null, message);
                catalog.SetLocalElements(draft.LocalElements);
            }
            return catalog;
        }

        private static IReadOnlyCollection<VfxSourceElementView> PaletteElements(VfxSourceElementCatalog catalog)
        {
            return catalog.Elements.Count > 0 ? catalog.Elements : catalog.LocalElements;
        }

        [Test]
        public void ExplicitAuthoredCatalog_LoadsVersionedGlobalAndRetainedDraftElementsWithoutAutomaticCuration()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            Assert.That(catalog.Available, Is.True, catalog.Message);
            Assert.That(catalog.Curation.PaletteMode, Is.EqualTo(VfxSourceCurationView.ExplicitAuthoredPaletteMode));
            Assert.That(catalog.Elements, Is.Not.Empty);
            Assert.That(catalog.Elements.All(value =>
                string.Equals(value.CurationStatus, "ExplicitAuthored", StringComparison.Ordinal)), Is.True);
            VfxCompositionDraftView draft = new VfxCompositionDraftService().Load(
                RetainedImpactDraftPath,
                out string loadMessage);
            Assert.That(draft, Is.Not.Null, loadMessage);
            catalog.SetLocalElements(draft.LocalElements);
            Assert.That(catalog.LocalElements, Has.Count.EqualTo(1));
            VfxSourceElementView element = catalog.LocalElements.Single();
            Assert.That(element.ElementId, Is.EqualTo(draft.LocalElements.Single().ElementId));
            Assert.That(element.DisplayName, Is.EqualTo(draft.LocalElements.Single().DisplayName));
            Assert.That(element.ElementKind, Is.EqualTo("CompoundGroup"));
            Assert.That(element.CurationStatus, Is.EqualTo("DraftLocal"));
            Assert.That(element.MemberComponentIds, Has.Count.EqualTo(7));
            Assert.That(new VfxSourceElementCurationService().Generate(out _, out string message), Is.False);
            Assert.That(message, Does.Contain("ExplicitAuthored"));
        }

        [Test]
        public void ExplicitAuthoredCatalog_SemanticReloadCanRebindRetainedElement()
        {
            var draftService = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = draftService.Load(RetainedImpactDraftPath, out string loadMessage);
            Assert.That(draft, Is.Not.Null, loadMessage);
            string retainedElementId = draft.LocalElements.Single().ElementId;

            VfxSourceElementCatalog reloaded = new VfxSourceElementCurationService().Load();
            Assert.That(reloaded.Curation.PaletteMode, Is.EqualTo(VfxSourceCurationView.ExplicitAuthoredPaletteMode));
            Assert.That(reloaded.LocalElements, Is.Empty);

            reloaded.SetLocalElements(draft.LocalElements);

            Assert.That(reloaded.LocalElements, Has.Count.EqualTo(1));
            Assert.That(reloaded.Find(retainedElementId), Is.Not.Null);
        }

        [Test]
        public void ExplicitAuthoredSourceFilter_SeparatesRetainedDraftAndVersionedGlobalElements()
        {
            Assert.That(VfxManualCompositionStudioController.SourceChoices(true), Is.EqualTo(new[]
            {
                "全部素材",
                "当前草稿素材",
                "已收藏"
            }));

            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxCompositionDraftView draft = new VfxCompositionDraftService().Load(
                RetainedImpactDraftPath,
                out string loadMessage);
            Assert.That(draft, Is.Not.Null, loadMessage);
            catalog.SetLocalElements(draft.LocalElements);
            Assert.That(catalog.Elements, Is.Not.Empty);
            Assert.That(
                VfxManualCompositionStudioController.ElementsForSource(catalog, "当前草稿素材").Single().ElementId,
                Is.EqualTo(draft.LocalElements.Single().ElementId));
            Assert.That(
                VfxManualCompositionStudioController.ElementsForSource(catalog, "全部素材").Count(),
                Is.EqualTo(catalog.Elements.Count + catalog.LocalElements.Count));
            string retainedElementId = catalog.LocalElements.Single().ElementId;
            Assert.That(VfxManualCompositionStudioController.ElementsForSource(catalog, "已收藏", new HashSet<string>()).Any(), Is.False);
            Assert.That(VfxManualCompositionStudioController.ElementsForSource(catalog, "已收藏", new HashSet<string> { retainedElementId }).Single().ElementId, Is.EqualTo(retainedElementId));
        }

        [Test]
        public void CompositionDraft_CreateWithUniqueId_UsesImmutableUuidIdentity()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            var service = new VfxCompositionDraftService();

            VfxCompositionDraftView first = service.CreateWithUniqueId(catalog, "First");
            VfxCompositionDraftView second = service.CreateWithUniqueId(catalog, "Second");

            Assert.That(Guid.TryParseExact(first.DraftId, "D", out _), Is.True);
            Assert.That(Guid.TryParseExact(second.DraftId, "D", out _), Is.True);
            Assert.That(second.DraftId, Is.Not.EqualTo(first.DraftId));
        }

        [Test]
        public void CompositionDraft_RoundTripsAndDetectsStaleInputs()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxSourceElementView element = PaletteElements(catalog).First(value => !value.Stale);
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "Manual composition test");
            var layer = new VfxCompositionLayerView { LayerId = "layer_01", ElementId = element.ElementId, PrimitiveId = element.ExistingPrimitiveId };
            layer.VisualStart = new VfxLayerVisualStartView
            {
                Mode = nameof(VfxVisualStartMode.ManualLeadIn),
                ManualLeadInFrames = 12
            };
            string memberId = catalog.Members(element).First().ComponentId;
            var participation = new VfxMemberParticipationView { MemberId = memberId };
            participation.PropertyStates["appearance.palette.primary"] = nameof(VfxMemberParticipationState.Excluded);
            participation.PropertyStates["appearance.opacity"] = nameof(VfxMemberParticipationState.Override);
            layer.MemberParticipation.Add(participation);
            draft.Layers.Add(layer);
            Assert.That(service.Save(draft, out string path, out string message), Is.True, message);
            VfxCompositionDraftView loaded = service.Load(path, out message);
            Assert.That(loaded, Is.Not.Null, message);
            Assert.That(JsonConvert.SerializeObject(loaded), Is.EqualTo(JsonConvert.SerializeObject(draft)));
            Assert.That(loaded.Layers[0].MemberParticipation[0].PropertyStates["appearance.palette.primary"], Is.EqualTo(nameof(VfxMemberParticipationState.Excluded)));
            Assert.That(loaded.Layers[0].MemberParticipation[0].PropertyStates["appearance.opacity"], Is.EqualTo(nameof(VfxMemberParticipationState.Override)));
            Assert.That(loaded.Layers[0].VisualStart.Mode, Is.EqualTo(nameof(VfxVisualStartMode.ManualLeadIn)));
            Assert.That(loaded.Layers[0].VisualStart.ManualLeadInFrames, Is.EqualTo(12));
            Assert.That(service.Validate(loaded, catalog).Valid, Is.True);
            loaded.Layers[0].VisualStart.ManualLeadInFrames = -1;
            Assert.That(service.Validate(loaded, catalog).Valid, Is.False);
            loaded.Layers[0].VisualStart.ManualLeadInFrames = 12;
            loaded.Layers[0].StartOffsetSeconds = -0.1f;
            Assert.That(service.Validate(loaded, catalog).Valid, Is.False);
            loaded.Layers[0].StartOffsetSeconds = 0f;
            loaded.SourceIndexHash = "sha256:" + new string('0', 64);
            loaded.Dependencies.SourceIndexHash = loaded.SourceIndexHash;
            VfxCompositionValidationResult stale = service.Validate(loaded, catalog);
            Assert.That(stale.Valid, Is.False);
            Assert.That(stale.Diagnostics.Any(value => value.Contains("Source Index")), Is.True);
        }

        [Test]
        public void CompositionDraftGuard_RejectsStaleDiskAndRecoveryJournalRoundTrips()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "guard fixture");
            VfxSourceElementView element = PaletteElements(catalog).First(value => !value.Stale);
            draft.Layers.Add(VfxCompositionDraftService.CreateLayerView(catalog, element, "layer_loop", "Loop"));
            Assert.That(service.Save(draft, out string path, out string message), Is.True, message);
            string expectedHash = VfxCompositionDraftService.HashFile(path);
            File.AppendAllText(path, " ");
            string externalBytes = File.ReadAllText(path);
            draft.DisplayName = "session change";
            Assert.That(service.SaveGuarded(draft, path, expectedHash, out _, out _, out message), Is.False);
            Assert.That(message, Does.Contain("三方合并"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(externalBytes));

            var journal = new VfxCompositionRecoveryJournalService();
            Assert.That(journal.PathFor(draft.DraftId), Does.StartWith("Library/MxFramework/VfxSemantic/Recovery/"));
            Assert.That(journal.Write(draft, path, VfxCompositionDraftService.HashFile(path), out message), Is.True, message);
            VfxCompositionRecoveryEnvelopeView loaded = journal.Load(draft.DraftId, out message);
            Assert.That(loaded, Is.Not.Null, message);
            Assert.That(loaded.SessionDraftHash, Is.EqualTo(VfxCompositionDraftService.HashDraft(draft)));
            Assert.That(loaded.BaseDraft, Is.Not.Null);
            journal.Clear(draft.DraftId);
        }

        [Test]
        public void AutomaticRecoveryRestoresSessionWithoutPromptAndKeepsDiskUnchanged()
        {
            var diskDraft = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "磁盘版本" };
            var sessionDraft = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "未保存会话" };
            var recovery = new VfxCompositionRecoveryEnvelopeView
            {
                DraftId = TestDraftId,
                BaseFileHash = "disk-hash",
                WrittenUtc = "2026-07-22T13:30:00Z",
                Draft = sessionDraft,
                SessionDraftHash = VfxCompositionDraftService.HashDraft(sessionDraft)
            };

            bool restored = VfxManualCompositionStudioController.TryResolveAutomaticRecovery(
                diskDraft,
                "disk-hash",
                recovery,
                out VfxCompositionDraftView result,
                out string message);

            Assert.That(restored, Is.True);
            Assert.That(result, Is.SameAs(sessionDraft));
            Assert.That(result.DisplayName, Is.EqualTo("未保存会话"));
            Assert.That(diskDraft.DisplayName, Is.EqualTo("磁盘版本"));
            Assert.That(message, Does.Contain("已自动恢复"));
            Assert.That(message, Does.Contain("磁盘文件未被覆盖"));
        }

        [Test]
        public void AutomaticRecoveryKeepsDiskVersionWhenThreeWayMergeConflicts()
        {
            var baseDraft = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "共同基线" };
            var diskDraft = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "磁盘修改" };
            var sessionDraft = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "会话修改" };
            var recovery = new VfxCompositionRecoveryEnvelopeView
            {
                DraftId = TestDraftId,
                BaseFileHash = "old-disk-hash",
                BaseDraft = baseDraft,
                Draft = sessionDraft,
                SessionDraftHash = VfxCompositionDraftService.HashDraft(sessionDraft)
            };

            bool restored = VfxManualCompositionStudioController.TryResolveAutomaticRecovery(
                diskDraft,
                "new-disk-hash",
                recovery,
                out VfxCompositionDraftView result,
                out string message);

            Assert.That(restored, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(diskDraft.DisplayName, Is.EqualTo("磁盘修改"));
            Assert.That(message, Does.Contain("冲突"));
        }

        [Test]
        public void CompositionDraftDelete_RequiresCurrentIdentityAndHashAndClearsRecovery()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "delete guard fixture");
            Assert.That(service.Save(draft, out string path, out string message), Is.True, message);
            string expectedHash = VfxCompositionDraftService.HashFile(path);
            var journal = new VfxCompositionRecoveryJournalService();
            Assert.That(journal.Write(draft, path, expectedHash, out message), Is.True, message);

            File.AppendAllText(path, " ");
            Assert.That(service.DeleteGuarded(path, draft.DraftId, expectedHash, out message), Is.False);
            Assert.That(message, Does.Contain("发生变化"));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(journal.PathFor(draft.DraftId)), Is.True);

            string currentHash = VfxCompositionDraftService.HashFile(path);
            Assert.That(service.DeleteGuarded(path, "vfx.composition.wrong", currentHash, out message), Is.False);
            Assert.That(message, Does.Contain("Draft ID"));
            Assert.That(service.DeleteGuarded(path, draft.DraftId, currentHash, out message), Is.True, message);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(journal.PathFor(draft.DraftId)), Is.False);
        }

        [Test]
        public void RecoveryJournalWrite_ReturnsFailureWhenDirectoryCreationFails()
        {
            var draft = new VfxCompositionDraftView { DraftId = TestDraftId + ".directory-failure" };
            var journal = new VfxCompositionRecoveryJournalService(
                _ => throw new UnauthorizedAccessException("directory denied"),
                (_, _, _) => { },
                (_, _, _) => { });

            bool success = journal.Write(draft, string.Empty, string.Empty, out string message);

            Assert.That(success, Is.False);
            Assert.That(message, Does.StartWith("恢复日志写入失败："));
            Assert.That(message, Does.Contain("directory denied"));
        }

        [Test]
        public void RecoveryJournalWrite_ReturnsFailureWhenTemporaryFileWriteFails()
        {
            var draft = new VfxCompositionDraftView { DraftId = TestDraftId + ".write-failure" };
            var journal = new VfxCompositionRecoveryJournalService(
                _ => { },
                (_, _, _) => throw new IOException("write denied"),
                (_, _, _) => { });

            bool success = journal.Write(draft, string.Empty, string.Empty, out string message);

            Assert.That(success, Is.False);
            Assert.That(message, Does.StartWith("恢复日志写入失败："));
            Assert.That(message, Does.Contain("write denied"));
        }

        [Test]
        public void MachineLocalVfxAuthoringState_PersistsOutsideUnityTemp()
        {
            Assert.That(VfxCompositionRecoveryJournalService.Root, Does.StartWith("Library/"));
            Assert.That(VfxSourceLibraryEditorService.IndexPath, Does.StartWith("Library/"));
            Assert.That(VfxSourceElementIndexService.IndexPath, Does.StartWith("Library/"));
        }

        [Test]
        public void CompositionThreeWayMerge_MergesIndependentChangesAndReportsConflicts()
        {
            var @base = new VfxCompositionDraftView { DraftId = TestDraftId, DisplayName = "base", Brief = "base brief" };
            VfxCompositionDraftView disk = JsonConvert.DeserializeObject<VfxCompositionDraftView>(JsonConvert.SerializeObject(@base));
            VfxCompositionDraftView session = JsonConvert.DeserializeObject<VfxCompositionDraftView>(JsonConvert.SerializeObject(@base));
            disk.DisplayName = "disk name";
            session.Brief = "session brief";
            VfxCompositionThreeWayMergeResult merged = VfxCompositionThreeWayMergeService.Merge(@base, disk, session);
            Assert.That(merged.CanApply, Is.True, string.Join("\n", merged.Conflicts));
            Assert.That(merged.Merged.DisplayName, Is.EqualTo("disk name"));
            Assert.That(merged.Merged.Brief, Is.EqualTo("session brief"));

            session.DisplayName = "session name";
            VfxCompositionThreeWayMergeResult conflict = VfxCompositionThreeWayMergeService.Merge(@base, disk, session);
            Assert.That(conflict.CanApply, Is.False);
            Assert.That(conflict.Conflicts, Does.Contain("$.displayName"));
        }

        [Test]
        public void SemanticStaleRemap_UpdatesOnlyStableDependenciesAndBlocksUnknownMembers()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxSourceElementView element = PaletteElements(catalog).First(value => !value.Stale && catalog.Members(value).Count > 0);
            VfxSourceComponentView member = catalog.Members(element)[0];
            var draft = new VfxCompositionDraftService().Create(catalog, TestDraftId, "remap fixture");
            VfxCompositionLayerView layer = VfxCompositionDraftService.CreateLayerView(catalog, element, "layer_loop", "Loop");
            layer.MemberParticipation.Add(new VfxMemberParticipationView { MemberId = member.ComponentId });
            draft.Layers.Add(layer);
            draft.Dependencies.SourceIndexHash = "sha256:stale";
            draft.Dependencies.CurationHash = "sha256:stale";
            var service = new VfxSemanticStaleRemapService();
            VfxSemanticStaleRemapPlanView plan = service.Analyze(draft, catalog);
            Assert.That(plan.CanApplyExact, Is.True, string.Join("\n", plan.Diagnostics));
            Assert.That(plan.HasChanges, Is.True);
            Assert.That(plan.IsSafeMetadataOnly, Is.True);
            Assert.That(plan.RequiresConfirmation, Is.False);
            Assert.That(plan.DependencyChangeCount, Is.EqualTo(2));
            Assert.That(plan.Layers[0].MemberMappings[member.ComponentId], Is.EqualTo(member.ComponentId));
            Assert.That(service.ApplyExact(draft, catalog, plan, out string message), Is.True, message);
            Assert.That(draft.Dependencies.SourceIndexHash, Is.EqualTo(catalog.Index.IndexHash));
            Assert.That(draft.Dependencies.CurationHash, Is.EqualTo(catalog.Curation.CurationHash));

            layer.MemberParticipation[0].MemberId = "missing.member";
            VfxSemanticStaleRemapPlanView blocked = service.Analyze(draft, catalog);
            Assert.That(blocked.CanApplyExact, Is.False);
            Assert.That(blocked.Layers[0].UnresolvedMembers, Does.Contain("missing.member"));
        }

        [Test]
        public void ProfileBatchTransaction_RestoresEveryTouchedFileWhenOneWriteFails()
        {
            string manifestPath = VfxSemanticProfileStore.ManifestPath;
            byte[] manifestBytes = File.Exists(manifestPath) ? File.ReadAllBytes(manifestPath) : null;
            string profileId = "vfx.component.test.batch.rollback";
            string profilePath = VfxSemanticProfileStore.Root + "/Components/" + profileId.Replace('.', '_') + ".json";
            try
            {
                var store = new VfxSemanticProfileStore();
                VfxProfileManifestView manifest = store.LoadManifest(out string message);
                Assert.That(manifest, Is.Not.Null, message);
                var writes = new List<VfxProfileBatchWriteView>
                {
                    new VfxProfileBatchWriteView
                    {
                        Profile = new VfxComponentProfileView { ProfileId = profileId },
                        ProfileId = profileId,
                        Kind = "Component",
                        RelativePath = "Components/" + Path.GetFileName(profilePath),
                        ReviewState = nameof(VfxProfileReviewState.Confirmed)
                    },
                    new VfxProfileBatchWriteView
                    {
                        Profile = new VfxComponentProfileView { ProfileId = profileId + ".failure" },
                        ProfileId = profileId + ".failure",
                        Kind = "Component",
                        RelativePath = "Components",
                        ReviewState = nameof(VfxProfileReviewState.Confirmed)
                    }
                };
                Assert.That(store.SaveBatchGuarded(writes, manifest.ManifestHash, out _, out message), Is.False);
                Assert.That(message, Does.Contain("恢复原始字节"));
                Assert.That(File.Exists(profilePath), Is.False);
                Assert.That(File.Exists(manifestPath), Is.EqualTo(manifestBytes != null));
                if (manifestBytes != null) Assert.That(File.ReadAllBytes(manifestPath), Is.EqualTo(manifestBytes));
            }
            finally
            {
                if (File.Exists(profilePath)) File.Delete(profilePath);
                if (manifestBytes != null) File.WriteAllBytes(manifestPath, manifestBytes);
                else if (File.Exists(manifestPath)) File.Delete(manifestPath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ProfileConfirmationAnalysis_ReportsCoverageAndSupportsMemberScope()
        {
            var element = new VfxSourceElementView { ElementId = "vfx.element.profile.coverage" };
            element.MemberComponentIds.Add("component.first");
            element.MemberComponentIds.Add("component.second");
            var resolved = new VfxResolvedLayerAuthoringProfile
            {
                ElementProfile = new VfxElementProfileView { ProfileId = "vfx.element.profile.coverage" },
                ElementProfilePersisted = true
            };
            resolved.Members.Add(new VfxResolvedComponentAuthoringProfile
            {
                MemberId = "component.first",
                Persisted = true,
                Profile = new VfxComponentProfileView { ProfileId = "vfx.component.first" },
                ShaderProfile = new VfxShaderProfileView { ProfileId = "vfx.shader.first" },
                ShaderProfilePersisted = true
            });
            resolved.Members.Add(new VfxResolvedComponentAuthoringProfile
            {
                MemberId = "component.second",
                Persisted = false,
                Profile = new VfxComponentProfileView { ProfileId = "vfx.component.second" },
                ShaderProfile = new VfxShaderProfileView { ProfileId = "vfx.shader.second" },
                ShaderProfilePersisted = false
            });

            var service = new VfxSemanticProfileConfirmationService();
            VfxProfileImpactAnalysisView coverage = service.Analyze(element, new VfxCompositionDraftService(), resolved);
            Assert.That(coverage.TotalComponentCount, Is.EqualTo(2));
            Assert.That(coverage.ConfirmedComponentCount, Is.EqualTo(1));
            Assert.That(coverage.ConfirmedShaderProfileCount, Is.EqualTo(1));
            Assert.That(coverage.ElementProfileConfirmed, Is.True);
            Assert.That(coverage.CoverageSummary, Does.Contain("Component 1/2"));

            VfxProfileImpactAnalysisView scoped = service.Analyze(element, new VfxCompositionDraftService(), resolved, "component.second");
            Assert.That(scoped.ComponentCount, Is.EqualTo(1));
            Assert.That(scoped.ScopeMemberId, Is.EqualTo("component.second"));
            Assert.That(scoped.AffectedProfileIds, Does.Contain("vfx.component.component_second"));
            Assert.That(scoped.AffectedProfileIds, Does.Contain("vfx.shader.second"));
            Assert.That(scoped.Summary, Does.Contain("只确认当前成员"));
        }

        [Test]
        public void ProfileConfirmation_MergesConfirmedDependenciesWithoutDroppingOtherBindings()
        {
            var existing = new List<VfxProfileDependencyView>
            {
                new VfxProfileDependencyView { ProfileId = "vfx.component.first", Hash = "sha256:old" },
                new VfxProfileDependencyView { ProfileId = "vfx.shader.shared", Hash = "sha256:shared" }
            };
            var confirmed = new List<VfxProfileDependencyView>
            {
                new VfxProfileDependencyView { ProfileId = "vfx.component.first", Hash = "sha256:new" },
                new VfxProfileDependencyView { ProfileId = "vfx.element.profile.fixture", Hash = "sha256:element" }
            };

            List<VfxProfileDependencyView> merged = VfxSemanticProfileConfirmationService.MergeDependencies(existing, confirmed);

            Assert.That(merged.Count, Is.EqualTo(3));
            Assert.That(merged.First().ProfileId, Is.EqualTo("vfx.element.profile.fixture"));
            Assert.That(merged.Single(value => value.ProfileId == "vfx.component.first").Hash, Is.EqualTo("sha256:new"));
            Assert.That(merged.Single(value => value.ProfileId == "vfx.shader.shared").Hash, Is.EqualTo("sha256:shared"));
        }

        [Test]
        public void ProfileResolver_RequiresBothManifestAndProfileToBeConfirmed()
        {
            string confirmed = nameof(VfxProfileReviewState.Confirmed);
            string suggested = nameof(VfxProfileReviewState.AutoSuggested);

            Assert.That(VfxSemanticProfileResolver.IsConfirmed(confirmed, confirmed), Is.True);
            Assert.That(VfxSemanticProfileResolver.IsConfirmed(confirmed, suggested), Is.False);
            Assert.That(VfxSemanticProfileResolver.IsConfirmed(suggested, confirmed), Is.False);
        }

        [Test]
        public void PropertyCapabilityAnalyzer_DistinguishesConfirmedPreviewOnlyUnsupportedAndReserved()
        {
            var registry = new VfxSemanticPropertyRegistryView();
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "timing.body.frames", DisplayName = "主体", Group = "Timing" });
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "appearance.opacity", DisplayName = "透明度", Group = "Appearance" });
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "appearance.dissolve", DisplayName = "溶解", Group = "Appearance" });
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "render.material.profile", DisplayName = "材质 Profile", Group = "Render" });
            var confirmed = new VfxResolvedComponentAuthoringProfile
            {
                MemberId = "confirmed",
                Persisted = true,
                Profile = new VfxComponentProfileView { Status = nameof(VfxProfileReviewState.Confirmed) }
            };
            confirmed.Profile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "appearance.opacity",
                Adapter = "ParticleStartColor",
                Target = "main.startColor.alpha"
            });
            var suggested = new VfxResolvedComponentAuthoringProfile
            {
                MemberId = "suggested",
                Persisted = false,
                Profile = new VfxComponentProfileView { Status = nameof(VfxProfileReviewState.AutoSuggested) }
            };
            suggested.Profile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "appearance.opacity",
                Adapter = "ParticleStartColor",
                Target = "main.startColor.alpha"
            });

            List<VfxSemanticPropertyCapabilityRow> layerRows = VfxSemanticPropertyCapabilityAnalyzer.Analyze(
                registry,
                new[] { confirmed, suggested },
                string.Empty);
            Assert.That(layerRows.Single(value => value.PropertyId == "timing.body.frames").State, Is.EqualTo(VfxSemanticPropertyCapabilityState.Partial));
            Assert.That(layerRows.Single(value => value.PropertyId == "appearance.opacity").State, Is.EqualTo(VfxSemanticPropertyCapabilityState.Partial));
            Assert.That(layerRows.Single(value => value.PropertyId == "appearance.dissolve").State, Is.EqualTo(VfxSemanticPropertyCapabilityState.Unsupported));
            Assert.That(layerRows.Single(value => value.PropertyId == "render.material.profile").State, Is.EqualTo(VfxSemanticPropertyCapabilityState.Reserved));

            List<VfxSemanticPropertyCapabilityRow> suggestedRows = VfxSemanticPropertyCapabilityAnalyzer.Analyze(
                registry,
                new[] { confirmed, suggested },
                "suggested");
            Assert.That(suggestedRows.Single(value => value.PropertyId == "appearance.opacity").State, Is.EqualTo(VfxSemanticPropertyCapabilityState.PreviewOnly));
        }

        [Test]
        public void ProfileGapReport_IsStableAndDoesNotPromoteSuggestedProfiles()
        {
            var registry = new VfxSemanticPropertyRegistryView();
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "appearance.opacity", DisplayName = "透明度", Group = "Appearance" });
            registry.Properties.Add(new VfxSemanticPropertyDefinitionView { PropertyId = "render.material.profile", DisplayName = "材质 Profile", Group = "Render" });
            var element = new VfxSourceElementView { ElementId = "vfx.element.profile.gap" };
            var resolved = new VfxResolvedLayerAuthoringProfile();
            var member = new VfxResolvedComponentAuthoringProfile
            {
                MemberId = "component.gap",
                Persisted = false,
                ShaderProfilePersisted = false,
                Profile = new VfxComponentProfileView
                {
                    ProfileId = "vfx.component.gap",
                    Status = nameof(VfxProfileReviewState.AutoSuggested)
                },
                ShaderProfile = new VfxShaderProfileView
                {
                    ProfileId = "vfx.shader.gap",
                    Status = nameof(VfxProfileReviewState.AutoSuggested)
                }
            };
            member.Profile.Bindings.Add(new VfxSemanticBindingView
            {
                PropertyId = "appearance.opacity",
                Adapter = "ParticleStartColor",
                Target = "main.startColor.alpha"
            });
            resolved.Members.Add(member);
            var manifest = new VfxProfileManifestView { ManifestHash = "sha256:manifest" };

            VfxSemanticProfileGapReportView report =
                VfxSemanticProfileGapReportBuilder.Build(element, resolved, registry, manifest);
            string first = report.ToStableJson();
            string second = report.ToStableJson();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(report.MissingComponentProfileIds, Is.EqualTo(new[] { "vfx.component.gap" }));
            Assert.That(report.MissingShaderProfileIds, Is.EqualTo(new[] { "vfx.shader.gap" }));
            Assert.That(report.CapabilityCounts[nameof(VfxSemanticPropertyCapabilityState.PreviewOnly)], Is.EqualTo(1));
            Assert.That(report.CapabilityCounts[nameof(VfxSemanticPropertyCapabilityState.Reserved)], Is.EqualTo(1));
            Assert.That(report.SemanticBakeBlockers, Is.Empty);
            Assert.That(report.CapabilityLimitations, Has.Count.EqualTo(1));
            Assert.That(member.Profile.Status, Is.EqualTo(nameof(VfxProfileReviewState.AutoSuggested)));
            Assert.That(member.ShaderProfile.Status, Is.EqualTo(nameof(VfxProfileReviewState.AutoSuggested)));
            Assert.That(JObject.Parse(first).Value<string>("reportHash"), Does.StartWith("sha256:"));
        }

        [Test]
        public void CompositionDraft_PreviewContextIsBackwardCompatibleAndExcludedFromCandidateContentHash()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "Preview context hash test");
            string contentHash = VfxCompositionDraftService.HashCompositionContent(draft);
            string draftHash = VfxCompositionDraftService.HashDraft(draft);

            draft.PreviewContext.Mode = VfxCompositionPreviewContextView.CharacterAttachedMode;
            draft.PreviewContext.CharacterStableId = "codex_flow_test";
            draft.PreviewContext.PackageId = "codex_flow_test";
            draft.PreviewContext.PosePresetId = "Idle";
            draft.PreviewContext.AnimationResourceKey = "animation.test.idle";
            draft.PreviewContext.SocketId = "mainHand";

            Assert.That(VfxCompositionDraftService.HashCompositionContent(draft), Is.EqualTo(contentHash));
            Assert.That(VfxCompositionDraftService.HashDraft(draft), Is.Not.EqualTo(draftHash));

            Newtonsoft.Json.Linq.JObject legacy = Newtonsoft.Json.Linq.JObject.FromObject(draft);
            legacy.Remove("previewContext");
            VfxCompositionDraftView loaded = JsonConvert.DeserializeObject<VfxCompositionDraftView>(legacy.ToString(), VfxCompositionDraftService.JsonSettings);
            VfxCompositionDraftService.NormalizePreviewContext(loaded);
            Assert.That(loaded.PreviewContext.Mode, Is.EqualTo(VfxCompositionPreviewContextView.VfxOnlyMode));
            Assert.That(loaded.PreviewContext.AnimationSpeed, Is.EqualTo(1f));
        }

        [Test]
        public void DraftArtifactStatus_SeparatesSavePrefabAndPublicationState()
        {
            VfxCompositionArtifactStatusView notExported =
                VfxCompositionArtifactStatusService.ResolveFromEvidence(
                    "sha256:current",
                    true,
                    true,
                    new VfxCompositionArtifactEvidence());
            Assert.That(notExported.SaveText, Is.EqualTo("草稿 未保存"));
            Assert.That(notExported.PrefabState, Is.EqualTo(VfxCompositionPrefabSyncState.NotExported));
            Assert.That(notExported.PublicationText, Is.EqualTo("未发布"));

            var synchronizedEvidence = new VfxCompositionArtifactEvidence
            {
                BindingFound = true,
                PublishedRecordFound = true,
                PrefabExists = true,
                PathsMatch = true,
                GuidsMatch = true,
                RecordedPrefabHash = "sha256:prefab",
                ActualPrefabHash = "sha256:prefab",
                PublishedCompositionHash = "sha256:current",
                VisualMaturity = "Production",
                ReviewStatus = "Ready"
            };
            VfxCompositionArtifactStatusView synchronized =
                VfxCompositionArtifactStatusService.ResolveFromEvidence(
                    "sha256:current",
                    false,
                    true,
                    synchronizedEvidence);
            Assert.That(synchronized.SaveText, Is.EqualTo("草稿 已保存"));
            Assert.That(synchronized.PrefabState, Is.EqualTo(VfxCompositionPrefabSyncState.Synchronized));
            Assert.That(synchronized.PublicationText, Is.EqualTo("Production · Ready"));

            synchronizedEvidence.PublishedCompositionHash = "sha256:older-draft";
            VfxCompositionArtifactStatusView staleDraft =
                VfxCompositionArtifactStatusService.ResolveFromEvidence(
                    "sha256:current",
                    false,
                    true,
                    synchronizedEvidence);
            Assert.That(staleDraft.PrefabState, Is.EqualTo(VfxCompositionPrefabSyncState.DraftChanged));

            synchronizedEvidence.PublishedCompositionHash = "sha256:current";
            synchronizedEvidence.ActualPrefabHash = "sha256:external-change";
            VfxCompositionArtifactStatusView externalPrefab =
                VfxCompositionArtifactStatusService.ResolveFromEvidence(
                    "sha256:current",
                    false,
                    true,
                    synchronizedEvidence);
            Assert.That(externalPrefab.PrefabState, Is.EqualTo(VfxCompositionPrefabSyncState.PrefabChanged));
        }

        [Test]
        public void CompositionDraft_PhasePreviewContextUsesSparseInheritanceAndRoundTrips()
        {
            var draft = new VfxCompositionDraftView();
            draft.PreviewContext.Mode = VfxCompositionPreviewContextView.CharacterAttachedMode;
            draft.PreviewContext.CharacterStableId = "character.shared";
            draft.PreviewContext.PackageId = "package.shared";
            draft.PreviewContext.CharacterEvidenceHash = "sha256:shared";
            draft.PreviewContext.AnimationResourceKey = "animation.shared.idle";
            draft.PreviewContext.SocketId = "mainHand";
            string contentHash = VfxCompositionDraftService.HashCompositionContent(draft);
            string draftHash = VfxCompositionDraftService.HashDraft(draft);

            VfxCompositionPreviewContextOverrideView build = VfxCompositionDraftService.EnsurePreviewContextOverride(draft.PreviewContext, "Build");
            build.PosePresetId = "Cast";
            build.AnimationResourceKey = "animation.shared.cast";
            build.AnimationLoop = false;
            build.SocketId = "headVfx";

            VfxCompositionPreviewContextView resolved = VfxCompositionDraftService.ResolvePreviewContext(draft.PreviewContext, "Build");
            Assert.That(resolved.CharacterStableId, Is.EqualTo("character.shared"));
            Assert.That(resolved.AnimationResourceKey, Is.EqualTo("animation.shared.cast"));
            Assert.That(resolved.AnimationLoop, Is.False);
            Assert.That(resolved.SocketId, Is.EqualTo("headVfx"));
            Assert.That(VfxCompositionDraftService.HashCompositionContent(draft), Is.EqualTo(contentHash));
            Assert.That(VfxCompositionDraftService.HashDraft(draft), Is.Not.EqualTo(draftHash));

            string json = JsonConvert.SerializeObject(draft, Formatting.None, VfxCompositionDraftService.JsonSettings);
            VfxCompositionDraftView loaded = JsonConvert.DeserializeObject<VfxCompositionDraftView>(json, VfxCompositionDraftService.JsonSettings);
            VfxCompositionPreviewContextView loadedBuild = VfxCompositionDraftService.ResolvePreviewContext(loaded.PreviewContext, "Build");
            Assert.That(loadedBuild.AnimationResourceKey, Is.EqualTo("animation.shared.cast"));
            Assert.That(loaded.PreviewContext.PhaseOverrides.Build.FieldCount, Is.EqualTo(4));

            VfxCompositionDraftService.ClearPreviewContextOverride(loaded.PreviewContext, "Build");
            Assert.That(loaded.PreviewContext.PhaseOverrides, Is.Null);
            Assert.That(JsonConvert.SerializeObject(loaded, Formatting.None, VfxCompositionDraftService.JsonSettings), Does.Not.Contain("phaseOverrides"));
        }

        [Test]
        public void CompositionDraft_PhaseCharacterIdentityOverrideResolvesAsOneEvidenceGroup()
        {
            var shared = new VfxCompositionPreviewContextView
            {
                CharacterStableId = "character.a",
                PackageId = "package.a",
                CharacterEvidenceHash = "sha256:a"
            };
            VfxCompositionPreviewContextOverrideView rebuild = VfxCompositionDraftService.EnsurePreviewContextOverride(shared, "Rebuild");
            rebuild.CharacterStableId = "character.b";
            rebuild.PackageId = "package.b";
            rebuild.CharacterEvidenceHash = "sha256:b";

            VfxCompositionPreviewContextView resolved = VfxCompositionDraftService.ResolvePreviewContext(shared, "Rebuild");
            Assert.That(resolved.CharacterStableId, Is.EqualTo("character.b"));
            Assert.That(resolved.PackageId, Is.EqualTo("package.b"));
            Assert.That(resolved.CharacterEvidenceHash, Is.EqualTo("sha256:b"));
        }

        [Test]
        public void CharacterPreviewCatalog_DiscoversReadyCodexFlowTestAndConfiguredSockets()
        {
            IReadOnlyList<VfxCharacterPreviewDescriptor> characters = new VfxCharacterPreviewCatalog().Discover();
            VfxCharacterPreviewDescriptor character = characters.FirstOrDefault(value => value.PackageId == "codex_flow_test");
            Assert.That(character, Is.Not.Null);
            Assert.That(character.PackageId, Is.EqualTo("codex_flow_test"));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(character.PrefabPath), Is.Not.Null);
            Assert.That(character.Animations, Is.Not.Empty);
            Assert.That(character.Sockets.Select(value => value.SocketId), Does.Contain(string.Empty));
            Assert.That(character.Sockets.Select(value => value.SocketId), Is.SupersetOf(new[] { "mainHand", "offHand", "back", "foot.L", "foot.R", "headVfx" }));
        }

        [Test]
        public void FireballWorkingDrafts_AreDiscoverableFromTheWorkbenchSelector()
        {
            var service = new VfxCompositionDraftService();
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            var drafts = new Dictionary<string, VfxCompositionDraftView>(StringComparer.Ordinal);
            foreach (string path in service.ListDrafts())
            {
                VfxCompositionDraftView draft = service.Load(path, out string message);
                Assert.That(draft, Is.Not.Null, path + ": " + message);
                foreach (VfxCompositionLayerView layer in draft.Layers)
                {
                    if (string.IsNullOrWhiteSpace(layer.ElementId)) continue;
                    var availableElementIds = new HashSet<string>(catalog.Elements.Select(value => value.ElementId), StringComparer.Ordinal);
                    availableElementIds.UnionWith(draft.LocalElements.Select(value => value.ElementId));
                    Assert.That(
                        availableElementIds.Contains(layer.ElementId),
                        Is.True,
                        draft.DraftId + "/" + layer.LayerId + " 引用了不可用的 Element：" + layer.ElementId);
                }
                drafts[draft.DraftId] = draft;
            }

            Assert.That(drafts.Keys, Is.EquivalentTo(new[] { "vfx.composition.sample.fireball_impact" }));
            VfxCompositionDraftView retained = drafts["vfx.composition.sample.fireball_impact"];
            Assert.That(retained.DisplayName, Is.EqualTo("Fireball Impact 工作草稿"));
            Assert.That(retained.LocalElements, Has.Count.EqualTo(1));
            Assert.That(retained.LocalElements[0].DisplayName, Is.EqualTo("Hit_Nova_Light"));
            Assert.That(retained.Layers.Any(layer => layer.Enabled && layer.Phase == "Loop"), Is.True);
        }

        [Test]
        public void CompositionDraftV3_RequiresLoopAndRejectsLegacySchemas()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxSourceElementView element = PaletteElements(catalog).First(value => !value.Stale);
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "Lifecycle validation");
            draft.Layers.Add(new VfxCompositionLayerView { LayerId = "build", ElementId = element.ElementId, Phase = "Build" });
            Assert.That(service.Validate(draft, catalog).Valid, Is.False);
            draft.Layers.Add(new VfxCompositionLayerView { LayerId = "loop", ElementId = element.ElementId, Phase = "Loop" });
            Assert.That(service.Validate(draft, catalog).Valid, Is.True);
            draft.SchemaVersion = "mx.vfx.composition-draft.v1";
            VfxCompositionValidationResult legacy = service.Validate(draft, catalog);
            Assert.That(legacy.Valid, Is.False);
            Assert.That(legacy.Diagnostics.Any(value => value.Contains("v1")), Is.True);
        }

        [Test]
        public void CompositionDraftV2Migration_IsTransactionalAndBlocksNonDefaultCoarseMultipliers()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxSourceElementView element = PaletteElements(catalog).First(value => !value.Stale);
            string directory = "Temp/MxFramework/VfxSemantic/Tests";
            string validPath = directory + "/migration-valid-" + Guid.NewGuid().ToString("N") + ".json";
            string blockedPath = directory + "/migration-blocked-" + Guid.NewGuid().ToString("N") + ".json";
            Directory.CreateDirectory(directory);
            var source = new Newtonsoft.Json.Linq.JObject
            {
                ["schemaVersion"] = VfxCompositionDraftView.PreviousSchemaVersion,
                ["draftId"] = "vfx.composition.test.migration",
                ["displayName"] = "Migration fixture",
                ["sourceIndexHash"] = catalog.Index.IndexHash,
                ["curationHash"] = catalog.Curation.CurationHash,
                ["catalogHash"] = string.Empty,
                ["loopPlaybackMode"] = "OneShot",
                ["phaseDurationOverrides"] = new Newtonsoft.Json.Linq.JObject(),
                ["layers"] = new Newtonsoft.Json.Linq.JArray
                {
                    new Newtonsoft.Json.Linq.JObject
                    {
                        ["layerId"] = "layer_01",
                        ["elementId"] = element.ElementId,
                        ["phase"] = "Loop",
                        ["enabled"] = true,
                        ["localPosition"] = new Newtonsoft.Json.Linq.JArray(1f, 2f, 3f),
                        ["localEulerAngles"] = new Newtonsoft.Json.Linq.JArray(0f, 15f, 0f),
                        ["localScale"] = new Newtonsoft.Json.Linq.JArray(1f, 1f, 1f),
                        ["startOffsetSeconds"] = 0.5f,
                        ["playbackSpeed"] = 1f,
                        ["emissionScale"] = 1f,
                        ["lifetimeScale"] = 1f,
                        ["sizeScale"] = 1f,
                        ["colorTint"] = new Newtonsoft.Json.Linq.JArray(1f, 1f, 1f, 1f),
                        ["sortingOrderOffset"] = 0
                    }
                }
            };

            try
            {
                File.WriteAllText(validPath, source.ToString());
                var migration = new VfxCompositionV3MigrationService();
                Assert.That(migration.TryMigrateFile(validPath, catalog, out VfxCompositionDraftView migrated, out VfxCompositionMigrationReceiptView receipt, out string message), Is.True, message);
                Assert.That(migrated.SchemaVersion, Is.EqualTo(VfxCompositionDraftView.CurrentSchemaVersion));
                Assert.That(migrated.Layers[0].StartFrame, Is.EqualTo(30));
                Assert.That(migrated.Layers[0].Transform.Position, Is.EqualTo(new[] { 1f, 2f, 3f }));
                Assert.That(receipt.SourceSchemaVersion, Is.EqualTo(VfxCompositionDraftView.PreviousSchemaVersion));
                Assert.That(receipt.TargetDraftHash, Is.Not.Empty);
                string migratedJson = File.ReadAllText(validPath);
                Assert.That(migratedJson, Does.Not.Contain("\"lifetimeScale\""));
                Assert.That(migratedJson, Does.Contain("\"targetArtisticFrames\""));

                Newtonsoft.Json.Linq.JObject blocked = (Newtonsoft.Json.Linq.JObject)source.DeepClone();
                blocked["layers"][0]["lifetimeScale"] = 1.25f;
                string blockedJson = blocked.ToString();
                File.WriteAllText(blockedPath, blockedJson);
                Assert.That(migration.TryMigrateFile(blockedPath, catalog, out _, out VfxCompositionMigrationReceiptView blockedReceipt, out message), Is.False);
                Assert.That(message, Does.Contain("必须人工转换"));
                Assert.That(blockedReceipt.BlockedLayers, Is.EqualTo(new[] { "layer_01" }));
                Assert.That(File.ReadAllText(blockedPath), Is.EqualTo(blockedJson));
            }
            finally
            {
                if (File.Exists(validPath)) File.Delete(validPath);
                if (File.Exists(blockedPath)) File.Delete(blockedPath);
            }
        }

        [TestCase("backup-write")]
        [TestCase("receipt-partial")]
        [TestCase("source-validation")]
        public void CompositionMigrationFileTransaction_FailureRestoresSourceAndPreservesHistory(string failureMode)
        {
            string root = "Temp/MxFramework/VfxSemantic/MigrationTransactionTests/" + Guid.NewGuid().ToString("N");
            string sourcePath = root + "/draft.json";
            string evidenceRoot = root + "/evidence";
            string historyBackup = evidenceRoot + "/history.v2.bytes";
            string historyReceipt = evidenceRoot + "/history.receipt.json";
            byte[] sourceBytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"mx.vfx.composition-draft.v2\",\"draftId\":\"history\"}");
            byte[] historyBackupBytes = { 1, 3, 5, 7 };
            const string historyReceiptText = "sealed-history";
            Directory.CreateDirectory(evidenceRoot);
            File.WriteAllBytes(sourcePath, sourceBytes);
            File.WriteAllBytes(historyBackup, historyBackupBytes);
            File.WriteAllText(historyReceipt, historyReceiptText);

            try
            {
                CreateMigrationTransactionFixture(sourceBytes, out VfxCompositionDraftView target, out string targetJson, out VfxCompositionMigrationReceiptView receipt);
                Action<string, byte[]> backupWriter = null;
                Action<string, string> receiptWriter = null;
                Func<string, bool> sourceValidator = null;
                if (failureMode == "backup-write")
                {
                    backupWriter = (path, bytes) =>
                    {
                        File.WriteAllBytes(path, bytes.Take(3).ToArray());
                        throw new IOException("Injected backup write failure.");
                    };
                }
                else if (failureMode == "receipt-partial")
                {
                    receiptWriter = (path, contents) =>
                    {
                        File.WriteAllText(path, contents.Substring(0, Math.Min(5, contents.Length)));
                        throw new IOException("Injected partial receipt failure.");
                    };
                }
                else
                {
                    sourceValidator = _ => false;
                }

                using var transaction = new VfxCompositionMigrationFileTransaction(
                    sourcePath,
                    evidenceRoot,
                    target.DraftId,
                    receipt.SourceBytesHash,
                    backupWriter: backupWriter,
                    receiptWriter: receiptWriter,
                    sourceValidator: sourceValidator);
                receipt.BackupPath = transaction.BackupPath;
                receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");

                Assert.That(transaction.Publish(sourceBytes, targetJson, receipt, out string message), Is.False);
                Assert.That(message, Does.Contain("restored the original draft"));
                Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(sourceBytes));
                Assert.That(File.Exists(transaction.BackupPath), Is.False);
                Assert.That(File.Exists(transaction.ReceiptPath), Is.False);
                Assert.That(File.ReadAllBytes(historyBackup), Is.EqualTo(historyBackupBytes));
                Assert.That(File.ReadAllText(historyReceipt), Is.EqualTo(historyReceiptText));
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Any(path => path.EndsWith(".tmp", StringComparison.Ordinal)
                        || path.EndsWith(".replace-backup", StringComparison.Ordinal)), Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void CompositionMigrationFileTransaction_SuccessSealsUniqueCrossValidatedEvidence()
        {
            string root = "Temp/MxFramework/VfxSemantic/MigrationTransactionTests/" + Guid.NewGuid().ToString("N");
            string sourcePath = root + "/draft.json";
            string evidenceRoot = root + "/evidence";
            string historyBackup = evidenceRoot + "/history.v2.bytes";
            string historyReceipt = evidenceRoot + "/history.receipt.json";
            byte[] sourceBytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"mx.vfx.composition-draft.v2\",\"draftId\":\"history\"}");
            Directory.CreateDirectory(evidenceRoot);
            File.WriteAllBytes(sourcePath, sourceBytes);
            File.WriteAllText(historyBackup, "old-backup");
            File.WriteAllText(historyReceipt, "old-receipt");

            try
            {
                CreateMigrationTransactionFixture(sourceBytes, out VfxCompositionDraftView target, out string targetJson, out VfxCompositionMigrationReceiptView receipt);
                using var transaction = new VfxCompositionMigrationFileTransaction(
                    sourcePath,
                    evidenceRoot,
                    target.DraftId,
                    receipt.SourceBytesHash);
                receipt.BackupPath = transaction.BackupPath;
                receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");

                Assert.That(transaction.Publish(sourceBytes, targetJson, receipt, out string message), Is.True, message);
                VfxCompositionDraftView published = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                    File.ReadAllText(sourcePath),
                    VfxCompositionDraftService.JsonSettings);
                VfxCompositionMigrationReceiptView sealedReceipt = JsonConvert.DeserializeObject<VfxCompositionMigrationReceiptView>(
                    File.ReadAllText(transaction.ReceiptPath));
                Assert.That(VfxCanonicalJson.Hash(published), Is.EqualTo(receipt.TargetDraftHash));
                Assert.That(File.ReadAllBytes(transaction.BackupPath), Is.EqualTo(sourceBytes));
                Assert.That(sealedReceipt.BackupPath, Is.EqualTo(transaction.BackupPath));
                Assert.That(sealedReceipt.SourceBytesHash, Is.EqualTo(VfxCanonicalJson.HashUtf8(Convert.ToBase64String(sourceBytes))));
                Assert.That(sealedReceipt.TargetDraftHash, Is.EqualTo(VfxCanonicalJson.Hash(published)));
                Assert.That(sealedReceipt.ReceiptHash, Is.EqualTo(VfxCanonicalJson.Hash(sealedReceipt, "receiptHash")));
                Assert.That(File.ReadAllText(historyBackup), Is.EqualTo("old-backup"));
                Assert.That(File.ReadAllText(historyReceipt), Is.EqualTo("old-receipt"));
                Assert.That(transaction.BackupPath, Is.Not.EqualTo(historyBackup));
                Assert.That(transaction.ReceiptPath, Is.Not.EqualTo(historyReceipt));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void CandidatePrefabTransaction_PostSaveValidationFailurePreservesExistingPrefab()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidatePrefabTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            GameObject sentinel = null;
            GameObject replacement = null;
            string temporaryPath = string.Empty;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                sentinel = CreateCandidateTransactionPrefab("SentinelRoot", "SentinelContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, outputPath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(outputPath);
                string originalContents = File.ReadAllText(outputPath);

                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                using (var transaction = new VfxCandidatePrefabAssetTransaction(outputPath))
                {
                    temporaryPath = transaction.TemporaryPath;
                    Assert.That(transaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(temporaryPath), Is.Not.Null);
                    // A failed post-save validation exits without Commit; Dispose is the rollback boundary.
                }

                GameObject preserved = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(originalGuid));
                Assert.That(File.ReadAllText(outputPath), Is.EqualTo(originalContents));
                Assert.That(preserved.transform.Find("SentinelContent"), Is.Not.Null);
                Assert.That(preserved.transform.Find("ReplacementContent"), Is.Null);
                AssertCandidateTemporaryAssetRemoved(temporaryPath);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void CandidatePrefabTransaction_SuccessfulReplacementPreservesGuidAndUpdatesContent()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidatePrefabTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            GameObject sentinel = null;
            GameObject replacement = null;
            string temporaryPath = string.Empty;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                sentinel = CreateCandidateTransactionPrefab("SentinelRoot", "SentinelContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, outputPath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(outputPath);

                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                using (var transaction = new VfxCandidatePrefabAssetTransaction(outputPath))
                {
                    temporaryPath = transaction.TemporaryPath;
                    Assert.That(transaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(transaction.Commit(out message), Is.True, message);
                    transaction.Complete();
                }

                GameObject updated = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(originalGuid));
                Assert.That(updated.transform.Find("SentinelContent"), Is.Null);
                Assert.That(updated.transform.Find("ReplacementContent"), Is.Not.Null);
                AssertCandidateTemporaryAssetRemoved(temporaryPath);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void StableBakeService_SuccessPublishesUniqueKeyAndPreservesStablePrefabGuid()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxStableBakeServiceTests";
            const string candidatePath = folderPath + "/Candidate.prefab";
            const string stablePath = folderPath + "/Stable.prefab";
            GameObject sentinel = null;
            GameObject replacement = null;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                sentinel = CreateCandidateTransactionPrefab("StableRoot", "OldContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, stablePath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(stablePath);

                replacement = CreateCandidateTransactionPrefab("CandidateRoot", "NewContent");
                PrefabUtility.SaveAsPrefabAsset(replacement, candidatePath);
                var draft = CreateStableBakeDraft("vfx.authored.test.stable-success", stablePath);
                var receipt = new VfxCompositionBakeReceipt { OutputPrefabPath = candidatePath };
                ResourceKey publishedKey = default;

                bool success = VfxStablePrefabBakeService.Publish(
                    draft,
                    receipt,
                    out string message,
                    (VfxCompositionDraftView publishedDraft, VfxCompositionBakeReceipt _, out string publishMessage) =>
                    {
                        publishedKey = publishedDraft.ResourceIdentity.ResourceKey.ToResourceKey();
                        publishMessage = string.Empty;
                        return true;
                    });

                Assert.That(success, Is.True, message);
                Assert.That(publishedKey.Id, Is.EqualTo("vfx.authored.test.stable-success"));
                Assert.That(AssetDatabase.AssetPathToGUID(stablePath), Is.EqualTo(originalGuid));
                GameObject stable = AssetDatabase.LoadAssetAtPath<GameObject>(stablePath);
                Assert.That(stable.transform.Find("OldContent"), Is.Null);
                Assert.That(stable.transform.Find("NewContent"), Is.Not.Null);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void StableBakeService_MetadataFailureRollsBackStablePrefab()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxStableBakeServiceRollbackTests";
            const string candidatePath = folderPath + "/Candidate.prefab";
            const string stablePath = folderPath + "/Stable.prefab";
            GameObject sentinel = null;
            GameObject replacement = null;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                sentinel = CreateCandidateTransactionPrefab("StableRoot", "PreservedContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, stablePath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(stablePath);
                string originalContents = File.ReadAllText(stablePath);

                replacement = CreateCandidateTransactionPrefab("CandidateRoot", "RejectedContent");
                PrefabUtility.SaveAsPrefabAsset(replacement, candidatePath);
                var draft = CreateStableBakeDraft("vfx.authored.test.stable-rollback", stablePath);
                var receipt = new VfxCompositionBakeReceipt { OutputPrefabPath = candidatePath };

                bool success = VfxStablePrefabBakeService.Publish(
                    draft,
                    receipt,
                    out string message,
                    (VfxCompositionDraftView _, VfxCompositionBakeReceipt _, out string publishMessage) =>
                    {
                        publishMessage = "injected metadata failure";
                        return false;
                    });

                Assert.That(success, Is.False);
                Assert.That(message, Does.Contain("injected metadata failure"));
                Assert.That(AssetDatabase.AssetPathToGUID(stablePath), Is.EqualTo(originalGuid));
                Assert.That(File.ReadAllText(stablePath), Is.EqualTo(originalContents));
                GameObject stable = AssetDatabase.LoadAssetAtPath<GameObject>(stablePath);
                Assert.That(stable.transform.Find("PreservedContent"), Is.Not.Null);
                Assert.That(stable.transform.Find("RejectedContent"), Is.Null);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void StableProductionPublication_MarksBindingReadyAndCreatesBoundApproval()
        {
            var binding = new VfxPublishedBindingRecord();
            var effect = new JObject();
            var receipt = new VfxCompositionBakeReceipt { ReceiptHash = "sha256:bake" };
            var approval = new VfxReadyApprovalReceipt
            {
                ReceiptHash = "sha256:approval",
                PrefabContentHash = "sha256:prefab"
            };

            VfxStablePrefabPublicationService.ApplyPublicationState(
                binding,
                effect,
                receipt,
                approval,
                "Assets/MxFrameworkGenerated/Vfx/ReadyApprovals/test.json");

            Assert.That(binding.VisualMaturity, Is.EqualTo("Production"));
            Assert.That(binding.ReviewStatus, Is.EqualTo("Ready"));
            Assert.That(binding.ActivationReceiptHash, Is.EqualTo(approval.ReceiptHash));
            Assert.That(effect.Value<string>("visualMaturity"), Is.EqualTo("Production"));
            Assert.That(effect.Value<string>("reviewStatus"), Is.EqualTo("Ready"));
            Assert.That(effect["approval"]?.Value<string>("receiptHash"), Is.EqualTo(approval.ReceiptHash));
            Assert.That(effect["approval"]?.Value<string>("receiptPath"), Does.EndWith("test.json"));
        }

        [Test]
        public void StableReviewPublication_DemotesChangedContentAndClearsApproval()
        {
            var binding = new VfxPublishedBindingRecord
            {
                VisualMaturity = "Production",
                ReviewStatus = "Ready",
                ActivationReceiptHash = "sha256:old-approval"
            };
            var effect = new JObject
            {
                ["visualMaturity"] = "Production",
                ["reviewStatus"] = "Ready",
                ["approval"] = new JObject { ["receiptHash"] = "sha256:old-approval" }
            };
            var receipt = new VfxCompositionBakeReceipt { ReceiptHash = "sha256:new-bake" };

            VfxStablePrefabPublicationService.ApplyPublicationState(
                binding,
                effect,
                receipt,
                null,
                string.Empty);

            Assert.That(binding.VisualMaturity, Is.EqualTo("Candidate"));
            Assert.That(binding.ReviewStatus, Is.EqualTo("ReviewNeeded"));
            Assert.That(binding.ActivationReceiptHash, Is.EqualTo(receipt.ReceiptHash));
            Assert.That(effect.Value<string>("visualMaturity"), Is.EqualTo("Candidate"));
            Assert.That(effect.Value<string>("reviewStatus"), Is.EqualTo("ReviewNeeded"));
            Assert.That(effect["approval"]?.Type ?? JTokenType.Null, Is.EqualTo(JTokenType.Null));
        }

        private static VfxCompositionDraftView CreateStableBakeDraft(string resourceId, string stablePath)
        {
            return new VfxCompositionDraftView
            {
                DraftId = "vfx.composition.test." + resourceId,
                DisplayName = resourceId,
                ResourceIdentity = new VfxCompositionStableResourceView
                {
                    EffectId = resourceId,
                    ResourceKey = new VfxPublishedResourceKey
                    {
                        Id = resourceId,
                        TypeId = ResourceTypeIds.GameObject,
                        PackageId = "mxframework.tests"
                    },
                    StablePrefabPath = stablePath,
                    Role = "Impact",
                    Anchor = "HitPoint",
                    Space = "World"
                }
            };
        }

        [Test]
        public void CandidatePrefabTransaction_CommitFailureRestoresExistingPrefab()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidatePrefabTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            GameObject sentinel = null;
            GameObject replacement = null;
            string temporaryPath = string.Empty;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                sentinel = CreateCandidateTransactionPrefab("SentinelRoot", "SentinelContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, outputPath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(outputPath);
                string originalContents = File.ReadAllText(outputPath);

                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                using (var transaction = new VfxCandidatePrefabAssetTransaction(
                    outputPath,
                    (_, path) =>
                    {
                        string absolutePath = Path.GetFullPath(Path.Combine(
                            Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                            path));
                        File.WriteAllText(absolutePath, "partial replacement");
                        return false;
                    }))
                {
                    temporaryPath = transaction.TemporaryPath;
                    Assert.That(transaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(transaction.Commit(out message), Is.False);
                    Assert.That(message, Does.Contain("未能替换"));
                }

                GameObject preserved = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(originalGuid));
                Assert.That(File.ReadAllText(outputPath), Is.EqualTo(originalContents));
                Assert.That(preserved.transform.Find("SentinelContent"), Is.Not.Null);
                Assert.That(preserved.transform.Find("ReplacementContent"), Is.Null);
                AssertCandidateTemporaryAssetRemoved(temporaryPath);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void CandidateBaker_PreflightFailuresDoNotCreateTemporaryRootOrCandidateAssets()
        {
            AssertCandidatePreflightFailureLeavesNoObjects(
                "vfx.composition.test.registry-preflight-failure",
                "Registry preflight failure root",
                new VfxCompositionCandidateBaker(
                    (out string message) =>
                    {
                        message = "registry unavailable";
                        return null;
                    },
                    (out string message) => throw new AssertionException("Manifest loader must not run after registry failure.")),
                "registry unavailable");

            AssertCandidatePreflightFailureLeavesNoObjects(
                "vfx.composition.test.manifest-preflight-failure",
                "Manifest preflight failure root",
                new VfxCompositionCandidateBaker(
                    null,
                    (out string message) =>
                    {
                        message = "manifest unavailable";
                        return null;
                    }),
                "manifest unavailable");
        }

        [Test]
        public void CandidateReceiptTransaction_CommitFailureRollsBackExistingPrefabAndReceipt()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidateReceiptTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            const string receiptRoot = "Temp/MxFramework/VfxCandidateReceiptTransactionTests";
            const string receiptPath = receiptRoot + "/receipt.json";
            GameObject sentinel = null;
            GameObject replacement = null;
            try
            {
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
                Directory.CreateDirectory(folderPath);
                Directory.CreateDirectory(receiptRoot);
                sentinel = CreateCandidateTransactionPrefab("SentinelRoot", "SentinelContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, outputPath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(outputPath);
                string originalPrefabContents = File.ReadAllText(outputPath);
                const string originalReceiptContents = "old receipt sentinel";
                File.WriteAllText(receiptPath, originalReceiptContents);

                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                using (var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPath))
                {
                    Assert.That(prefabTransaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(prefabTransaction.Commit(out message), Is.True, message);
                    VfxCompositionBakeReceipt newReceipt = CreateCandidateTransactionReceipt("receipt.commit-failure", outputPath);
                    using var receiptTransaction = new VfxCandidateReceiptFileTransaction(
                        receiptPath,
                        null,
                        (_, path) =>
                        {
                            File.WriteAllText(path, "partial receipt");
                            return false;
                        });
                    Assert.That(receiptTransaction.Prepare(newReceipt, out message), Is.True, message);
                    Assert.That(receiptTransaction.Commit(out message), Is.False);
                }

                GameObject preserved = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(originalGuid));
                Assert.That(File.ReadAllText(outputPath), Is.EqualTo(originalPrefabContents));
                Assert.That(preserved.transform.Find("SentinelContent"), Is.Not.Null);
                Assert.That(File.ReadAllText(receiptPath), Is.EqualTo(originalReceiptContents));
                Assert.That(Directory.GetFiles(receiptRoot, "*.tmp"), Is.Empty);
                Assert.That(Directory.GetFiles(folderPath, "*candidate_tmp*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
            }
        }

        [Test]
        public void CandidateReceiptTransaction_FirstGenerationPrepareFailureLeavesNoPrefabOrReceipt()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidateReceiptTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            const string receiptRoot = "Temp/MxFramework/VfxCandidateReceiptTransactionTests";
            const string receiptPath = receiptRoot + "/receipt.json";
            GameObject replacement = null;
            try
            {
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
                Directory.CreateDirectory(folderPath);
                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                using (var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPath))
                {
                    Assert.That(prefabTransaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(prefabTransaction.Commit(out message), Is.True, message);
                    VfxCompositionBakeReceipt newReceipt = CreateCandidateTransactionReceipt("receipt.prepare-failure", outputPath);
                    using var receiptTransaction = new VfxCandidateReceiptFileTransaction(
                        receiptPath,
                        (_, _) => throw new IOException("receipt write denied"));
                    Assert.That(receiptTransaction.Prepare(newReceipt, out message), Is.False);
                    Assert.That(message, Does.Contain("receipt write denied"));
                }

                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outputPath), Is.Null);
                Assert.That(File.Exists(receiptPath), Is.False);
                Assert.That(Directory.Exists(receiptRoot) ? Directory.GetFiles(receiptRoot, "*.tmp").Length : 0, Is.Zero);
                Assert.That(Directory.GetFiles(folderPath, "*candidate_tmp*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
            }
        }

        [Test]
        public void CandidateReceiptTransaction_SuccessUpdatesPrefabAndReceiptTogether()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxCandidateReceiptTransactionTests";
            const string outputPath = folderPath + "/Candidate.prefab";
            const string receiptRoot = "Temp/MxFramework/VfxCandidateReceiptTransactionTests";
            const string receiptPath = receiptRoot + "/receipt.json";
            GameObject sentinel = null;
            GameObject replacement = null;
            try
            {
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
                Directory.CreateDirectory(folderPath);
                Directory.CreateDirectory(receiptRoot);
                sentinel = CreateCandidateTransactionPrefab("SentinelRoot", "SentinelContent");
                PrefabUtility.SaveAsPrefabAsset(sentinel, outputPath);
                UnityEngine.Object.DestroyImmediate(sentinel);
                sentinel = null;
                string originalGuid = AssetDatabase.AssetPathToGUID(outputPath);
                File.WriteAllText(receiptPath, "old receipt sentinel");

                replacement = CreateCandidateTransactionPrefab("ReplacementRoot", "ReplacementContent");
                VfxCompositionBakeReceipt expectedReceipt = CreateCandidateTransactionReceipt("receipt.success", outputPath);
                using (var prefabTransaction = new VfxCandidatePrefabAssetTransaction(outputPath))
                using (var receiptTransaction = new VfxCandidateReceiptFileTransaction(receiptPath))
                {
                    Assert.That(prefabTransaction.Save(replacement, out string message), Is.True, message);
                    Assert.That(prefabTransaction.Commit(out message), Is.True, message);
                    Assert.That(receiptTransaction.Prepare(expectedReceipt, out message), Is.True, message);
                    Assert.That(receiptTransaction.Commit(out message), Is.True, message);
                    prefabTransaction.Complete();
                    receiptTransaction.Complete();
                }

                GameObject updated = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                VfxCompositionBakeReceipt actualReceipt = JsonConvert.DeserializeObject<VfxCompositionBakeReceipt>(File.ReadAllText(receiptPath));
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(originalGuid));
                Assert.That(updated.transform.Find("SentinelContent"), Is.Null);
                Assert.That(updated.transform.Find("ReplacementContent"), Is.Not.Null);
                Assert.That(actualReceipt.DraftId, Is.EqualTo(expectedReceipt.DraftId));
                Assert.That(actualReceipt.ReceiptHash, Is.EqualTo(expectedReceipt.ReceiptHash));
                Assert.That(Directory.GetFiles(receiptRoot, "*.tmp"), Is.Empty);
                Assert.That(Directory.GetFiles(folderPath, "*candidate_tmp*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                CleanupCandidateReceiptFixture(folderPath, receiptRoot, receiptPath);
            }
        }

        private static VfxCompositionBakeReceipt CreateCandidateTransactionReceipt(string draftId, string outputPath)
        {
            var receipt = new VfxCompositionBakeReceipt
            {
                DraftId = draftId,
                OutputPrefabPath = outputPath,
                OutputPrefabGuid = AssetDatabase.AssetPathToGUID(outputPath),
                CandidateContentHash = VfxCompositionDraftService.HashFile(outputPath)
            };
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            return receipt;
        }

        private static void CleanupCandidateReceiptFixture(string folderPath, string receiptRoot, string receiptPath)
        {
            AssetDatabase.DeleteAsset(folderPath);
            if (File.Exists(receiptPath)) File.Delete(receiptPath);
            if (Directory.Exists(receiptRoot))
            {
                foreach (string temporaryPath in Directory.GetFiles(receiptRoot, "*.tmp", SearchOption.TopDirectoryOnly))
                    File.Delete(temporaryPath);
                if (!Directory.EnumerateFileSystemEntries(receiptRoot).Any()) Directory.Delete(receiptRoot);
            }
        }

        private static void AssertCandidatePreflightFailureLeavesNoObjects(
            string draftId,
            string displayName,
            VfxCompositionCandidateBaker baker,
            string expectedMessage)
        {
            string session = VfxCompositionDraftService.FileToken(draftId);
            string outputRoot = VfxCompositionCandidateBaker.CandidateRoot + "/" + session;
            string outputPath = outputRoot + "/" + session + ".prefab";
            AssetDatabase.DeleteAsset(outputRoot);
            try
            {
                VfxSourceElementCatalog catalog = CreateCandidatePreflightCatalog();
                VfxCompositionDraftView draft = new VfxCompositionDraftService().Create(catalog, draftId, displayName);
                draft.Layers.Add(new VfxCompositionLayerView
                {
                    LayerId = "layer_loop",
                    ElementId = catalog.Elements[0].ElementId,
                    Phase = "Loop",
                    Enabled = true
                });

                bool success = baker.Bake(draft, catalog, out VfxCompositionBakeReceipt receipt, out string message);

                Assert.That(success, Is.False);
                Assert.That(receipt, Is.Null);
                Assert.That(message, Is.EqualTo(expectedMessage));
                Assert.That(UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>()
                    .Any(value => value != null && value.scene.IsValid() && value.name == displayName), Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outputPath), Is.Null);
                Assert.That(Directory.Exists(outputRoot), Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(outputRoot);
            }
        }

        private static VfxSourceElementCatalog CreateCandidatePreflightCatalog()
        {
            var catalog = new VfxSourceElementCatalog
            {
                Available = true,
                Index = new VfxSourceLibraryView { IndexHash = "sha256:preflight-index" },
                Curation = new VfxSourceCurationView { CurationHash = "sha256:preflight-curation" }
            };
            catalog.Elements.Add(new VfxSourceElementView
            {
                ElementId = "vfx.element.preflight",
                DisplayName = "Preflight Element",
                Stale = false
            });
            return catalog;
        }

        private static void CreateMigrationTransactionFixture(
            byte[] sourceBytes,
            out VfxCompositionDraftView target,
            out string targetJson,
            out VfxCompositionMigrationReceiptView receipt)
        {
            target = new VfxCompositionDraftView
            {
                SchemaVersion = VfxCompositionDraftView.CurrentSchemaVersion,
                DraftId = "vfx.composition.test.transaction",
                DisplayName = "Migration transaction fixture"
            };
            targetJson = JsonConvert.SerializeObject(target, Formatting.Indented) + Environment.NewLine;
            receipt = new VfxCompositionMigrationReceiptView
            {
                DraftId = target.DraftId,
                SourceSchemaVersion = VfxCompositionDraftView.PreviousSchemaVersion,
                SourceBytesHash = VfxCanonicalJson.HashUtf8(Convert.ToBase64String(sourceBytes)),
                TargetDraftHash = VfxCanonicalJson.Hash(target)
            };
        }

        private static GameObject CreateCandidateTransactionPrefab(string rootName, string childName)
        {
            var root = new GameObject(rootName);
            var child = new GameObject(childName);
            child.transform.SetParent(root.transform, false);
            return root;
        }

        private static void AssertCandidateTemporaryAssetRemoved(string assetPath)
        {
            string absolutePath = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                assetPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath), Is.Null);
            Assert.That(File.Exists(absolutePath), Is.False);
            Assert.That(File.Exists(absolutePath + ".meta"), Is.False);
        }

        [Test]
        public void CompositionBaker_WritesOnlyCandidateAndPreservesSourceDependencies()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxSourceElementView[] elements = PaletteElements(catalog).Where(value => !value.Stale && value.MemberComponentIds.Count > 0).ToArray();
            var service = new VfxCompositionDraftService();
            VfxCompositionDraftView draft = service.Create(catalog, TestDraftId, "Manual composition bake test");
            draft.PreviewContext.Mode = VfxCompositionPreviewContextView.CharacterAttachedMode;
            draft.PreviewContext.CharacterStableId = "codex_flow_test";
            draft.PreviewContext.PackageId = "codex_flow_test";
            draft.PreviewContext.CharacterEvidenceHash = "sha256:test-character-evidence";
            draft.PreviewContext.SocketId = "mainHand";
            for (int i = 0; i < elements.Length; i++)
                draft.Layers.Add(new VfxCompositionLayerView
                {
                    LayerId = "layer_" + i,
                    ElementId = elements[i].ElementId,
                    PrimitiveId = elements[i].ExistingPrimitiveId,
                    Phase = "Loop"
                });
            bool success = new VfxCompositionCandidateBaker().Bake(draft, catalog, out VfxCompositionBakeReceipt receipt, out string message);
            Assert.That(success, Is.True, message);
            Assert.That(receipt.OutputPrefabPath, Is.EqualTo(CandidatePath));
            Assert.That(receipt.UnitySerializationUsed, Is.True);
            Assert.That(receipt.SourceElementIds, Has.Count.EqualTo(1));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(receipt.OutputPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Candidate prefab asset was not saved or imported.");
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
            Assert.That(receipt.DirectSupportAssetGuids, Is.Not.Empty);
            Assert.That(receipt.CompositionContentHash, Is.EqualTo(VfxCompositionDraftService.HashCompositionContent(draft)));
            Assert.That(prefab.GetComponent<UnityVfxLifecycleController>(), Is.Not.Null, "Lifecycle controller is missing from the Candidate root.");
            Assert.That(prefab.GetComponentInChildren<Animator>(true), Is.Null, "Preview character must never be baked into the VFX Candidate.");
            foreach (string phase in new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" })
                Assert.That(prefab.transform.Find(phase), Is.Not.Null, "Missing phase root: " + phase);
            Assert.That(receipt.SchemaVersion, Is.EqualTo("mx.vfx.composition-bake-receipt.v3"));
            Assert.That(prefab.GetComponent<UnityVfxBakedLayerScheduler>(), Is.Not.Null, "Baked Layer scheduler is missing from the Candidate root.");
            Assert.That(receipt.CompiledLayers, Has.Count.EqualTo(elements.Length));
            Assert.That(receipt.CompiledLayers.All(value => !string.IsNullOrWhiteSpace(value.CompileHash)), Is.True);
            Assert.That(receipt.PreviewCompileHash, Is.Not.Empty);
            Assert.That(receipt.BakeCompileHash, Is.EqualTo(receipt.PreviewCompileHash));
            Assert.That(receipt.PreviewBakeEquivalent, Is.True);
            Assert.That(receipt.FieldDiffHash, Does.StartWith("sha256:"));
            Assert.That(receipt.SourceAssetsUnchanged, Is.True);
            Assert.That(receipt.SourceIntegrityHashAfter, Is.EqualTo(receipt.SourceIntegrityHashBefore));
            Assert.That(receipt.SourceAssets, Is.Not.Empty);
            Assert.That(receipt.Phases.Select(value => value.Phase), Is.EquivalentTo(new[] { "Build", "Rebuild", "Loop", "Complete", "Dissipate" }));
        }
    }
}
