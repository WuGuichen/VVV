using System.Collections;
using System.IO;
using MxFramework.Demo.FairyGui;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.UI.FairyGui
{
    public sealed class UiFoundationDemoFairyGuiTests
    {
        [SetUp]
        public void SetUp()
        {
            RemovePackageIfLoaded();
            FairyGuiTestStageReset.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            RemovePackageIfLoaded();
        }

        [Test]
        public void PublishedPackage_LoadsBindsCommandsAndReleases()
        {
            RemovePackageIfLoaded();
            string root = Directory.GetCurrentDirectory();
            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                root,
                "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_fui.bytes"));
            byte[] atlasBytes = File.ReadAllBytes(Path.Combine(
                root,
                "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_atlas0.png"));
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(atlas.LoadImage(atlasBytes));

            var provider = new MemoryResourceProvider()
                .Register("fgui/MxUiFoundationDemo_fui.bytes", packageBytes)
                .Register("fgui/MxUiFoundationDemo_atlas0.png", atlas);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "ui.foundation.demo.test",
                UiFoundationDemoFairyGuiIds.PackageId,
                new[]
                {
                    Entry(UiFoundationDemoFairyGuiIds.PackageBytesKey, "fgui/MxUiFoundationDemo_fui.bytes"),
                    Entry(UiFoundationDemoFairyGuiIds.Atlas0Key, "fgui/MxUiFoundationDemo_atlas0.png")
                }));
            var sink = new RecordingCommandSink();
            MxFairyGuiNavigator navigator = UiFoundationDemoComposition.CreateNavigator(manager, sink);

            try
            {
                MxUiOpenResult result = navigator.Open(
                    UiFoundationDemoFairyGuiIds.ViewId,
                    new UiFoundationDemoViewModel { StatusText = "已加载" });

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(navigator.TryGetOpenView(UiFoundationDemoFairyGuiIds.ViewId, out IMxUiView view));
                var typedView = view as MxFairyGuiView<UiFoundationDemoViewModel>;
                Assert.IsNotNull(typedView);
                var handle = typedView.Component as MxFairyGuiComponentHandle;
                Assert.IsNotNull(handle);
                Assert.AreEqual("已加载", handle.Component.GetChild(UiFoundationDemoFairyGuiIds.Status).asTextField.text);
                Fgui.GButton primary = handle.Component.GetChild(UiFoundationDemoFairyGuiIds.Primary).asButton;
                var baseImage = primary.GetChild("base") as Fgui.GImage;
                Assert.IsNotNull(baseImage);
                Assert.IsNotNull(baseImage.texture);
                Assert.IsFalse(baseImage.texture.disposed);
                Assert.IsNotNull(baseImage.texture.nativeTexture);

                primary.onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(UiFoundationDemoCommandIds.Primary, sink.Last.CommandId);

                Assert.IsTrue(navigator.Close(UiFoundationDemoFairyGuiIds.ViewId));
                Assert.AreEqual(0, manager.CreateDebugSnapshot().LoadedCount);
                Assert.IsNull(Fgui.UIPackage.GetByName(UiFoundationDemoFairyGuiIds.PackageId));
            }
            finally
            {
                if (navigator.IsOpen(UiFoundationDemoFairyGuiIds.ViewId))
                    navigator.Close(UiFoundationDemoFairyGuiIds.ViewId);
                RemovePackageIfLoaded();
                Object.DestroyImmediate(atlas);
            }
        }

        [UnityTest]
        public IEnumerator PreviewScene_PlayModeButtonsDriveStateInputAndCursor()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MxFramework/UiFoundationDemo.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            UiFoundationDemoPreviewBootstrap bootstrap = Object.FindAnyObjectByType<UiFoundationDemoPreviewBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap.IsOpen);
            Assert.AreEqual(MxFramework.Input.InputContext.UI, bootstrap.ActiveInputContext);
            Assert.AreEqual(MxFramework.Input.MxCursorMode.Released, bootstrap.CursorState.Mode);

            Fgui.GComponent modalLayer = Fgui.GRoot.inst.GetChild("MxFairyGuiLayer_Modal").asCom;
            Assert.IsNotNull(modalLayer);
            Assert.Greater(modalLayer.numChildren, 0);
            Fgui.GComponent panel = modalLayer.GetChildAt(modalLayer.numChildren - 1).asCom;
            panel.GetChild(UiFoundationDemoFairyGuiIds.Primary).asButton.onClick.Call();
            Assert.AreEqual(1, bootstrap.ExecutionCount);
            StringAssert.Contains("1", bootstrap.StatusText);

            panel.GetChild(UiFoundationDemoFairyGuiIds.Secondary).asButton.onClick.Call();
            StringAssert.Contains("已切换", bootstrap.StatusText);

            panel.GetChild(UiFoundationDemoFairyGuiIds.Close).asButton.onClick.Call();
            Assert.IsFalse(bootstrap.IsOpen);
            Assert.AreEqual(MxFramework.Input.InputContext.Gameplay, bootstrap.ActiveInputContext);
            Assert.AreEqual(MxFramework.Input.MxCursorMode.Captured, bootstrap.CursorState.Mode);

            yield return new ExitPlayMode();
        }

        private static ResourceCatalogEntry Entry(ResourceKey key, string address)
        {
            return new ResourceCatalogEntry(
                key.Id,
                key.TypeId,
                MemoryResourceProvider.Id,
                address,
                key.Variant,
                key.PackageId);
        }

        private static void RemovePackageIfLoaded()
        {
            Fgui.UIPackage package = Fgui.UIPackage.GetByName(UiFoundationDemoFairyGuiIds.PackageId);
            if (package != null)
                Fgui.UIPackage.RemovePackage(package.id);
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            public int Count { get; private set; }
            public MxUiCommand Last { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Count++;
                Last = command;
            }
        }
    }
}
