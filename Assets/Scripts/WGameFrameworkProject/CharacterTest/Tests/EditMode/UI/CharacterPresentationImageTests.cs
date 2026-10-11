using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI
{
    public sealed class CharacterPresentationImageTests
    {
        [Test]
        public void AppearanceOverride_InheritsEachSlotIndependently()
        {
            var images = new CharacterPresentationImages { PortraitResourceKey = "base.portrait", StandingResourceKey = "base.standing" };
            images.Variants.Add(new CharacterPresentationImageVariant { VariantId = "appearance.winter", PortraitResourceKey = "winter.portrait" });
            Assert.AreEqual("winter.portrait", images.ResolveResourceKey(false, "appearance.winter"));
            Assert.AreEqual("base.standing", images.ResolveResourceKey(true, "appearance.winter"));
            Assert.AreEqual("base.portrait", images.ResolveResourceKey(false, "unknown"));
        }

        [Test]
        public void MissingKey_LeavesVisibleFallback()
        {
            var row = CreateRow();
            using (var images = new CharacterTestFairyGuiCharacterImageBinding(new ResourceManager()))
            {
                images.Bind(row, "missing.portrait");
                var icon = row.GetChild("icon").asLoader;
                Assert.IsTrue(icon.visible);
                Assert.NotNull(icon.texture);
                Assert.AreEqual(40, icon.width);
                Assert.AreEqual(40, icon.height);
            }
            row.Dispose();
        }

        [Test]
        public void ReusedRow_ResetsImageAndReleasesSharedHandleOnce()
        {
            var texture = new Texture2D(4, 4);
            var provider = new MemoryResourceProvider().Register("portrait", texture);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog("images", "", new[] { new ResourceCatalogEntry("test.portrait", "Texture2D", "memory", "portrait") }));
            var row = CreateRow();
            var secondRow = CreateRow();
            var images = new CharacterTestFairyGuiCharacterImageBinding(manager);
            try
            {
                images.Bind(row, "test.portrait");
                images.Bind(secondRow, "test.portrait");
                Assert.AreSame(texture, row.GetChild("icon").asLoader.texture.nativeTexture);
                Assert.AreEqual(1, manager.CreateDebugSnapshot().TotalRefCount);
                images.Bind(row, "");
                Assert.AreNotSame(texture, row.GetChild("icon").asLoader.texture.nativeTexture);
                images.Dispose();
                images.Dispose();
                Assert.AreEqual(0, manager.CreateDebugSnapshot().TotalRefCount);
                Assert.AreEqual(1, provider.ReleaseCount);
                Assert.IsTrue(texture != null, "Resource-owned texture must not be destroyed by the FairyGUI wrapper.");
            }
            finally
            {
                images.Dispose(); row.Dispose(); secondRow.Dispose(); Object.DestroyImmediate(texture);
            }
        }

        private static Fgui.GButton CreateRow()
        {
            var row = new Fgui.GButton(); row.SetSize(320, 56);
            row.AddChild(new Fgui.GLoader { name = "icon" });
            row.AddChild(new Fgui.GTextField { name = "title" });
            return row;
        }
    }
}
