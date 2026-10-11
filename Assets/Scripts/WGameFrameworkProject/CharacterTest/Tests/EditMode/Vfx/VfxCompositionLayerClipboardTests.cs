using System.Collections.Generic;
using NUnit.Framework;

namespace MxFramework.Editor.Tests
{
    public sealed class VfxCompositionLayerClipboardTests
    {
        [Test]
        public void RoundtripPreservesLayerPrivateElementAndDependencyEvidence()
        {
            var layer = new VfxCompositionLayerView
            {
                LayerId = "layer_glow",
                ElementId = "vfx.element.local.source-prefab.sample",
                Phase = "Build",
                StartFrame = 12,
                Enabled = true
            };
            VfxSourceElementView element = CreateElement(layer.ElementId);

            string text = VfxCompositionLayerClipboard.Serialize(
                layer,
                element,
                element,
                "sha256:source-index",
                "sha256:curation");

            Assert.That(VfxCompositionLayerClipboard.TryDeserialize(
                text,
                out VfxCompositionLayerClipboardData restored,
                out string message), Is.True, message);
            Assert.That(restored.Layer.LayerId, Is.EqualTo(layer.LayerId));
            Assert.That(restored.Layer.Phase, Is.EqualTo("Build"));
            Assert.That(restored.Layer.StartFrame, Is.EqualTo(12));
            Assert.That(restored.LocalElement.ElementId, Is.EqualTo(element.ElementId));
            Assert.That(restored.LocalElement.MemberComponentIds, Is.EqualTo(element.MemberComponentIds));
            Assert.That(restored.SourceIndexHash, Is.EqualTo("sha256:source-index"));
            Assert.That(restored.CurationHash, Is.EqualTo("sha256:curation"));
            Assert.That(restored.ElementHash, Is.EqualTo(VfxCompositionLayerClipboard.HashElement(element)));
        }

        [Test]
        public void RejectsOrdinaryClipboardTextBeforeJsonParsing()
        {
            Assert.That(VfxCompositionLayerClipboard.TryDeserialize("plain text", out _, out string message), Is.False);
            Assert.That(message, Does.Contain("VFX Layer"));
        }

        [Test]
        public void RejectsClipboardPayloadWithMissingOrFutureSchema()
        {
            var layer = new VfxCompositionLayerView { LayerId = "layer", ElementId = "vfx.element.test", Phase = "Loop" };
            VfxSourceElementView element = CreateElement(layer.ElementId);
            string text = VfxCompositionLayerClipboard.Serialize(layer, null, element, "source", "curation");

            string missing = text.Replace("\"schemaVersion\":\"mx.vfx.composition-layer-clipboard.v1\",", string.Empty);
            Assert.That(VfxCompositionLayerClipboard.TryDeserialize(missing, out _, out _), Is.False);

            string future = text.Replace(VfxCompositionLayerClipboard.SchemaVersion, "mx.vfx.composition-layer-clipboard.v2");
            Assert.That(VfxCompositionLayerClipboard.TryDeserialize(future, out _, out _), Is.False);
        }

        [Test]
        public void RejectsOversizedClipboardBeforeDeserialization()
        {
            var layer = new VfxCompositionLayerView { LayerId = "layer", ElementId = "vfx.element.test", Phase = "Loop" };
            VfxSourceElementView element = CreateElement(layer.ElementId);
            string text = VfxCompositionLayerClipboard.Serialize(layer, null, element, "source", "curation")
                + new string(' ', VfxCompositionLayerClipboard.MaximumTextLength);

            Assert.That(VfxCompositionLayerClipboard.TryDeserialize(text, out _, out string message), Is.False);
            Assert.That(message, Does.Contain("4 MB"));
        }

        [Test]
        public void ElementHashChangesWhenMemberBindingChanges()
        {
            VfxSourceElementView original = CreateElement("vfx.element.test");
            VfxSourceElementView changed = CreateElement("vfx.element.test");
            changed.MemberComponentIds.Add("source.component.sample.2");

            Assert.That(VfxCompositionLayerClipboard.HashElement(changed), Is.Not.EqualTo(VfxCompositionLayerClipboard.HashElement(original)));
        }

        [Test]
        public void GlobalElementEvidenceRejectsCatalogOrElementDrift()
        {
            var layer = new VfxCompositionLayerView { LayerId = "layer", ElementId = "vfx.element.global", Phase = "Loop" };
            VfxSourceElementView element = CreateElement(layer.ElementId);
            var catalog = new VfxSourceElementCatalog
            {
                Index = new VfxSourceLibraryView { IndexHash = "source-a" },
                Curation = new VfxSourceCurationView { CurationHash = "curation-a" }
            };
            catalog.Elements.Add(element);
            string text = VfxCompositionLayerClipboard.Serialize(layer, null, element, "source-a", "curation-a");
            Assert.That(VfxCompositionLayerClipboard.TryDeserialize(text, out VfxCompositionLayerClipboardData data, out _), Is.True);
            Assert.That(VfxCompositionLayerClipboard.TryValidateEvidence(data, catalog, out _, out string message), Is.True, message);

            catalog.Index.IndexHash = "source-b";
            Assert.That(VfxCompositionLayerClipboard.TryValidateEvidence(data, catalog, out _, out message), Is.False);
            Assert.That(message, Does.Contain("索引"));

            catalog.Index.IndexHash = "source-a";
            element.MemberComponentIds.Add("source.component.changed");
            Assert.That(VfxCompositionLayerClipboard.TryValidateEvidence(data, catalog, out _, out message), Is.False);
            Assert.That(message, Does.Contain("Element 内容已经变化"));
        }

        [Test]
        public void PasteUsesSelectedLifecycleOrPreservesCopiedPhaseInFullPreview()
        {
            Assert.That(VfxManualCompositionStudioController.ResolvePastePhase("Build", "Complete"), Is.EqualTo("Build"));
            Assert.That(VfxManualCompositionStudioController.ResolvePastePhase("\u5b8c\u6574\u751f\u547d\u5468\u671f", "Complete"), Is.EqualTo("Complete"));
        }

        private static VfxSourceElementView CreateElement(string elementId)
        {
            return new VfxSourceElementView
            {
                ElementId = elementId,
                DisplayName = "Private Glow",
                MemberComponentIds = new List<string> { "source.component.sample.1" }
            };
        }
    }
}
