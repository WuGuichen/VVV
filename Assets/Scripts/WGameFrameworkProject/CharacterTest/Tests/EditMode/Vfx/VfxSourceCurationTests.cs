using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [Test]
        public void SemanticSelections_RemoveEmptyAndDuplicateOptions()
        {
            List<string> roles = VfxManualCompositionStudioController.SelectedSemanticValues("smoke", "无", "smoke", "glow");
            Assert.That(roles, Is.EqualTo(new[] { "glow", "smoke" }));
        }

        [Test]
        public void VersionedAuthoredPalette_RemainsVisibleWhenSourceIndexIsMissing()
        {
            var source = new VfxSourceLibraryEditorStatus
            {
                Available = false,
                Message = "尚未建立 Piloto 原材料深度索引。",
            };
            var curation = new VfxSourceCurationView
            {
                PaletteMode = VfxSourceCurationView.ExplicitAuthoredPaletteMode,
            };
            var authored = new VfxAuthoredElementLibraryView();
            authored.Elements.Add(new VfxSourceElementView
            {
                ElementId = "vfx.element.authored.test",
                DisplayName = "版本化火焰素材",
                ElementKind = "CompoundGroup",
                CurationStatus = "ExplicitAuthored",
            });

            VfxSourceElementCatalog catalog = VfxSourceElementCurationService.BuildCatalog(
                source,
                curation,
                authored,
                string.Empty);

            Assert.That(catalog.SourceIndexAvailable, Is.False);
            Assert.That(catalog.Available, Is.False, "Deep operations must remain fail-closed without Source Index.");
            Assert.That(catalog.Elements.Select(value => value.ElementId),
                Is.EqualTo(new[] { "vfx.element.authored.test" }));
            Assert.That(catalog.Registry, Is.Not.Null);
            Assert.That(catalog.Message, Does.Contain("1 个版本化素材可浏览"));
            Assert.That(catalog.Message, Does.Contain("预览"));
            Assert.That(catalog.Message, Does.Contain("烘焙已禁用"));
        }

        [Test]
        public void SourceIndexGate_BlocksDeepOperationsButExplainsRecovery()
        {
            var catalog = new VfxSourceElementCatalog
            {
                SourceIndexAvailable = false,
                Available = false,
            };

            bool usable = VfxManualCompositionStudioController.CanUseSourceIndex(
                catalog,
                out string message);

            Assert.That(usable, Is.False);
            Assert.That(message, Does.Contain("重建全量 Source Index"));
        }

        [Test]
        public void SourceCurationEntry_RoundTripsDetailedSemanticFields()
        {
            var entry = new VfxSourceCurationEntryView
            {
                ComponentId = "source.component.test",
                DisplayName = "火焰冲击核心",
                Family = "Impact",
                Theme = "Fire",
                Shape = "Sphere",
                Anchor = "HitPoint",
                TimingPhase = "Peak",
                QualityTier = "GameplayImportant"
            };
            entry.Roles.AddRange(new[] { "core", "glow" });
            entry.UseCases.AddRange(new[] { "Impact", "Projectile" });
            entry.Motions.Add("Expanding");

            VfxSourceCurationEntryView loaded = JsonConvert.DeserializeObject<VfxSourceCurationEntryView>(JsonConvert.SerializeObject(entry));
            Assert.That(loaded.DisplayName, Is.EqualTo(entry.DisplayName));
            Assert.That(loaded.Roles, Is.EqualTo(entry.Roles));
            Assert.That(loaded.Family, Is.EqualTo(entry.Family));
            Assert.That(loaded.UseCases, Is.EqualTo(entry.UseCases));
            Assert.That(loaded.Motions, Is.EqualTo(entry.Motions));
            Assert.That(loaded.QualityTier, Is.EqualTo(entry.QualityTier));
        }

        [Test]
        public void SourceCurationDocument_RoundTripsElementCardDisplayNameOverride()
        {
            var curation = new VfxSourceCurationView();
            curation.ElementOverrides.Add(new VfxSourceElementCurationOverrideView
            {
                ElementId = "vfx.element.test",
                DisplayName = "火焰冲击核心"
            });
            VfxSourceCurationView loaded = JsonConvert.DeserializeObject<VfxSourceCurationView>(JsonConvert.SerializeObject(curation));
            Assert.That(loaded.ElementOverrides.Single().ElementId, Is.EqualTo("vfx.element.test"));
            Assert.That(loaded.ElementOverrides.Single().DisplayName, Is.EqualTo("火焰冲击核心"));
        }

        [Test]
        public void SourceCurationLoadValidation_RejectsDuplicateComponentId()
        {
            var curation = new VfxSourceCurationView();
            curation.Entries.Add(new VfxSourceCurationEntryView { ComponentId = "source.component.duplicate" });
            curation.Entries.Add(new VfxSourceCurationEntryView { ComponentId = "source.component.duplicate" });
            var catalog = new VfxSourceElementCatalog { Available = true };

            bool valid = VfxSourceElementCurationService.ValidateCurationStableIds(curation, catalog);

            Assert.That(valid, Is.False);
            Assert.That(catalog.Available, Is.False);
            Assert.That(catalog.Message, Does.Contain("componentId"));
            Assert.That(catalog.Message, Does.Contain("source.component.duplicate"));
        }

        [Test]
        public void SourceCurationLoadValidation_RejectsDuplicateDisplayNameElementId()
        {
            var curation = new VfxSourceCurationView();
            curation.ElementOverrides.Add(new VfxSourceElementCurationOverrideView
            {
                ElementId = "vfx.element.duplicate",
                DisplayName = "名称 A"
            });
            curation.ElementOverrides.Add(new VfxSourceElementCurationOverrideView
            {
                ElementId = "vfx.element.duplicate",
                DisplayName = "名称 B"
            });
            var catalog = new VfxSourceElementCatalog { Available = true };

            bool valid = VfxSourceElementCurationService.ValidateCurationStableIds(curation, catalog);

            Assert.That(valid, Is.False);
            Assert.That(catalog.Available, Is.False);
            Assert.That(catalog.Message, Does.Contain("显示名覆盖 elementId"));
            Assert.That(catalog.Message, Does.Contain("vfx.element.duplicate"));
        }

        [Test]
        public void RefreshCurationTransaction_SealFailurePreservesAuthorityAndCurrentObject()
        {
            string root = Path.Combine("Temp", "MxFramework", "Tests", "VfxRefreshCuration", Guid.NewGuid().ToString("N"));
            string authority = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            try
            {
                VfxSourceLibraryView current = CreateRefreshCurationIndexFixture();
                string originalBytes = JsonConvert.SerializeObject(current, Formatting.Indented);
                File.WriteAllText(authority, originalBytes);
                VfxSourceCurationView curation = CreateRefreshCurationFixture();

                bool success = VfxSourceLibraryEditorService.RefreshCurationTransactional(
                    current,
                    curation,
                    authority,
                    (string input, string output, out string sealMessage) =>
                    {
                        VfxSourceLibraryView candidate = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(input));
                        Assert.That(candidate.IndexHash, Is.Empty);
                        Assert.That(candidate.Components.Single().Preferred, Is.True);
                        sealMessage = "seal failed";
                        return false;
                    },
                    out VfxSourceLibraryView committed,
                    out string message);

                Assert.That(success, Is.False);
                Assert.That(committed, Is.Null);
                Assert.That(message, Is.EqualTo("seal failed"));
                Assert.That(File.ReadAllText(authority), Is.EqualTo(originalBytes));
                Assert.That(current.IndexHash, Is.EqualTo("sha256:old"));
                Assert.That(current.Components.Single().Preferred, Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void RefreshCurationTransaction_CommitsOnlyValidSealedOutput()
        {
            string root = Path.Combine("Temp", "MxFramework", "Tests", "VfxRefreshCuration", Guid.NewGuid().ToString("N"));
            string authority = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            try
            {
                VfxSourceLibraryView current = CreateRefreshCurationIndexFixture();
                File.WriteAllText(authority, JsonConvert.SerializeObject(current, Formatting.Indented));
                VfxSourceCurationView curation = CreateRefreshCurationFixture();

                bool success = VfxSourceLibraryEditorService.RefreshCurationTransactional(
                    current,
                    curation,
                    authority,
                    (string input, string output, out string sealMessage) =>
                    {
                        VfxSourceLibraryView candidate = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(input));
                        Assert.That(candidate.IndexHash, Is.Empty);
                        Assert.That(candidate.Components.Single().Preferred, Is.True);
                        candidate.IndexHash = "sha256:new";
                        File.WriteAllText(output, JsonConvert.SerializeObject(candidate, Formatting.Indented));
                        sealMessage = string.Empty;
                        return true;
                    },
                    out VfxSourceLibraryView committed,
                    out string message);

                Assert.That(success, Is.True, message);
                Assert.That(committed.IndexHash, Is.EqualTo("sha256:new"));
                Assert.That(committed.Components.Single().Preferred, Is.True);
                VfxSourceLibraryView authoritative = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(authority));
                Assert.That(authoritative.IndexHash, Is.EqualTo("sha256:new"));
                Assert.That(authoritative.Components.Single().Preferred, Is.True);
                Assert.That(current.IndexHash, Is.EqualTo("sha256:old"));
                Assert.That(current.Components.Single().Preferred, Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ClearCurationOverride_SourceRestoreUnavailableStillPersistsClearAndReturnsReloadableSuccess()
        {
            var curation = new VfxSourceCurationView();
            curation.Entries.Add(new VfxSourceCurationEntryView
            {
                ComponentId = "source.component.clear",
                DisplayName = "人工成员名",
                Roles = new List<string> { "core" },
                Family = "Impact",
                Theme = "Fire",
                Shape = "Sphere",
                Anchor = "HitPoint",
                TimingPhase = "Peak",
                UseCases = new List<string> { "Impact" },
                Motions = new List<string> { "Expanding" },
                QualityTier = "Hero",
                Preferred = true,
                Deprecated = true,
                ExistingPrimitiveId = "vfx.primitive.must-survive",
                Note = "人工备注"
            });
            curation.ElementOverrides.Add(new VfxSourceElementCurationOverrideView
            {
                ElementId = "vfx.element.clear",
                DisplayName = "人工 Element 名"
            });
            string persistedJson = string.Empty;

            bool success = VfxSourceLibraryEditorService.ClearCurationOverrideAndPersist(
                curation,
                "source.component.clear",
                "vfx.element.clear",
                false,
                value => persistedJson = JsonConvert.SerializeObject(value),
                out string resultMessage);

            Assert.That(success, Is.True, resultMessage);
            Assert.That(persistedJson, Is.Not.Empty);
            VfxSourceCurationView persisted = JsonConvert.DeserializeObject<VfxSourceCurationView>(persistedJson);
            VfxSourceCurationEntryView entry = persisted.Entries.Single();
            Assert.That(entry.DisplayName, Is.Empty);
            Assert.That(entry.Roles, Is.Empty);
            Assert.That(entry.Family, Is.Empty);
            Assert.That(entry.Theme, Is.Empty);
            Assert.That(entry.Shape, Is.Empty);
            Assert.That(entry.Anchor, Is.Empty);
            Assert.That(entry.TimingPhase, Is.Empty);
            Assert.That(entry.UseCases, Is.Empty);
            Assert.That(entry.Motions, Is.Empty);
            Assert.That(entry.QualityTier, Is.Empty);
            Assert.That(entry.Preferred, Is.False);
            Assert.That(entry.Deprecated, Is.False);
            Assert.That(entry.Note, Is.Empty);
            Assert.That(entry.ExistingPrimitiveId, Is.EqualTo("vfx.primitive.must-survive"));
            Assert.That(persisted.ElementOverrides, Is.Empty);
            Assert.That(resultMessage, Does.Contain("已清除并保存"));
            Assert.That(resultMessage, Does.Contain("重建或刷新 Source Index"));
        }

        [Test]
        public void ClearCurationOverride_SealFailurePreservesIndexAndReportsPersistedPartialSuccess()
        {
            string root = Path.Combine("Temp", "MxFramework", "Tests", "VfxClearCuration", Guid.NewGuid().ToString("N"));
            string authority = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            try
            {
                VfxSourceLibraryView current = CreateRefreshCurationIndexFixture();
                current.Components.Single().DisplayName = "Cached Manual Name";
                string originalBytes = JsonConvert.SerializeObject(current, Formatting.Indented);
                File.WriteAllText(authority, originalBytes);
                VfxSourceCurationView curation = CreateClearCurationFixture();
                bool persisted = false;

                bool success = VfxSourceLibraryEditorService.ClearCurationOverrideTransactional(
                    current,
                    curation,
                    "source.component.refresh",
                    "vfx.element.clear",
                    true,
                    working => working.Components.Single().DisplayName = "Automatic Name",
                    authority,
                    _ => persisted = true,
                    (string input, string output, out string sealMessage) => { sealMessage = "seal failed"; return false; },
                    out VfxSourceLibraryView committed,
                    out string message);

                Assert.That(success, Is.True, message);
                Assert.That(persisted, Is.True);
                Assert.That(committed, Is.Null);
                Assert.That(message, Does.Contain("人工覆盖已清除并保存"));
                Assert.That(message, Does.Contain("Source Index 自动语义恢复未提交"));
                Assert.That(File.ReadAllText(authority), Is.EqualTo(originalBytes));
                Assert.That(current.Components.Single().DisplayName, Is.EqualTo("Cached Manual Name"));
                Assert.That(curation.Entries.Single().ExistingPrimitiveId, Is.EqualTo("vfx.primitive.must-survive"));
                Assert.That(curation.ElementOverrides, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ClearCurationOverride_ValidSealCommitsAutomaticSemanticsAndNewHash()
        {
            string root = Path.Combine("Temp", "MxFramework", "Tests", "VfxClearCuration", Guid.NewGuid().ToString("N"));
            string authority = Path.Combine(root, "index.json");
            Directory.CreateDirectory(root);
            try
            {
                VfxSourceLibraryView current = CreateRefreshCurationIndexFixture();
                current.Components.Single().DisplayName = "Cached Manual Name";
                File.WriteAllText(authority, JsonConvert.SerializeObject(current, Formatting.Indented));
                VfxSourceCurationView curation = CreateClearCurationFixture();

                bool success = VfxSourceLibraryEditorService.ClearCurationOverrideTransactional(
                    current,
                    curation,
                    "source.component.refresh",
                    "vfx.element.clear",
                    true,
                    working => working.Components.Single().DisplayName = "Automatic Name",
                    authority,
                    _ => { },
                    (string input, string output, out string sealMessage) =>
                    {
                        VfxSourceLibraryView candidate = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(input));
                        Assert.That(candidate.IndexHash, Is.Empty);
                        candidate.IndexHash = "sha256:restored";
                        File.WriteAllText(output, JsonConvert.SerializeObject(candidate, Formatting.Indented));
                        sealMessage = string.Empty;
                        return true;
                    },
                    out VfxSourceLibraryView committed,
                    out string message);

                Assert.That(success, Is.True, message);
                Assert.That(committed.IndexHash, Is.EqualTo("sha256:restored"));
                Assert.That(committed.Components.Single().DisplayName, Is.EqualTo("Automatic Name"));
                VfxSourceLibraryView authoritative = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(authority));
                Assert.That(authoritative.IndexHash, Is.EqualTo("sha256:restored"));
                Assert.That(authoritative.Components.Single().DisplayName, Is.EqualTo("Automatic Name"));
                Assert.That(current.IndexHash, Is.EqualTo("sha256:old"));
                Assert.That(current.Components.Single().DisplayName, Is.EqualTo("Cached Manual Name"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static VfxSourceCurationView CreateClearCurationFixture()
        {
            var curation = new VfxSourceCurationView();
            curation.Entries.Add(new VfxSourceCurationEntryView
            {
                ComponentId = "source.component.refresh",
                DisplayName = "Manual Name",
                Roles = new List<string> { "core" },
                ExistingPrimitiveId = "vfx.primitive.must-survive"
            });
            curation.ElementOverrides.Add(new VfxSourceElementCurationOverrideView
            {
                ElementId = "vfx.element.clear",
                DisplayName = "Manual Element"
            });
            return curation;
        }

        private static VfxSourceLibraryView CreateRefreshCurationIndexFixture()
        {
            var index = new VfxSourceLibraryView
            {
                DeepScanComplete = true,
                IndexHash = "sha256:old",
                ParticleSystemCount = 1
            };
            index.Components.Add(new VfxSourceComponentView
            {
                ComponentId = "source.component.refresh",
                Preferred = false,
                ExistingPrimitiveId = string.Empty
            });
            return index;
        }

        private static VfxSourceCurationView CreateRefreshCurationFixture()
        {
            var curation = new VfxSourceCurationView();
            curation.Entries.Add(new VfxSourceCurationEntryView
            {
                ComponentId = "source.component.refresh",
                Preferred = true,
                ExistingPrimitiveId = "vfx.primitive.refresh"
            });
            return curation;
        }

        [Test]
        public void ElementCardDisplayName_UsesVisibleChineseFallbackForRawSourceName()
        {
            var element = new VfxSourceElementView
            {
                DisplayName = "AlphaSmokePuff_Single_Realistic_Erosion_Softr",
                Theme = "Blood"
            };
            element.Roles.Add("smoke");
            string visibleName = VfxManualCompositionStudioController.DisplayElementName(element);
            Assert.That(visibleName, Does.Contain(VfxSourceElementChineseLabels.Format("Blood")));
            Assert.That(visibleName, Does.Contain(VfxSourceElementChineseLabels.Format("smoke")));
            Assert.That(visibleName, Does.Not.Contain("AlphaSmokePuff"));
        }
    }
}
