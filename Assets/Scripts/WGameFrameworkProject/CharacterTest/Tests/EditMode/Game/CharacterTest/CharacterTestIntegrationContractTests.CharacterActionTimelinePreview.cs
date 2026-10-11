using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {
        [Test]
        public void FairyGuiRuntimeAiLabCharacterActionTimelinePreview_LoadsFromPublishedPackage()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            global::FairyGUI.FontManager.Clear();
            Texture2D atlas = null;
            GlobalResourceRuntimeServices resources = null;
            MxFairyGuiPackageLoadScope packageScope = null;
            var host = new MxFairyGuiHost();
            bool packageEnsured = false;
            try
            {
                resources = CreatePlayerPanelResourceServices(
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                    out atlas);
                var bridge = new MxFairyGuiResourceBridge(resources.ResourceManager);
                var package = new MxFairyGuiPackageDescriptor(
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    new[]
                    {
                        new MxFairyGuiPackageResourceDescriptor(
                            MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                            MxFairyGuiPackageResourceKind.Atlas),
                    });
                MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
                Assert.IsTrue(load.Success, load.Failure.Message);
                packageScope = load.Scope;
                Assert.IsTrue(host.EnsurePackage(packageScope, out string failure), failure);
                packageEnsured = true;

                Assert.IsTrue(host.TryCreateComponent(
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabCharacterActionTimelinePreview",
                    out IMxFairyGuiComponentHandle handle,
                    out failure), failure);
                var concrete = handle as MxFairyGuiComponentHandle;
                Assert.NotNull(concrete);
                Assert.NotNull(concrete.Component.GetChild("trackList")?.asList);
                Assert.NotNull(concrete.Component.GetChild("btnEdit")?.asButton);
                Assert.NotNull(concrete.Component.GetChild("btnRetry")?.asButton);
                handle.Dispose();

                Assert.IsTrue(host.TryCreateComponent(
                    MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabCharacterActionWorkspace",
                    out handle,
                    out failure), failure);
                concrete = handle as MxFairyGuiComponentHandle;
                Assert.NotNull(concrete);
                Assert.NotNull(concrete.Component.GetChild("actionList")?.asList);
                Assert.NotNull(concrete.Component.GetChild("trackList")?.asList);
                Assert.NotNull(concrete.Component.GetChild("inspectorList")?.asList);
                Assert.NotNull(concrete.Component.GetChild("problemsList")?.asList);
                Assert.NotNull(concrete.Component.GetChild("btnSave")?.asButton);
                Assert.NotNull(concrete.Component.GetChild("btnPreviewPlay")?.asButton);
                Assert.NotNull(concrete.Component.GetChild("scenePreviewGuide")?.asGraph);
                Assert.NotNull(concrete.Component.GetChild("previewStateOverlay")?.asGraph);
                Assert.NotNull(concrete.Component.GetChild("btnPreviewRetry")?.asButton);
                Assert.NotNull(concrete.Component.GetChild("btnPreviewFit")?.asButton);
                Assert.NotNull(concrete.Component.GetChild("sourceContextTitle")?.asTextField);
                Assert.NotNull(concrete.Component.GetChild("sourceContextCard")?.asTextField);
                Assert.NotNull(concrete.Component.GetChild("sourceContextRoute")?.asTextField);
                handle.Dispose();
            }
            finally
            {
                if (packageEnsured)
                {
                    host.ReleasePackage(
                        MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
                }
                else
                {
                    packageScope?.Release();
                }
                resources?.Dispose();
                if (atlas != null)
                    Object.DestroyImmediate(atlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                global::FairyGUI.FontManager.Clear();
            }
        }
    }
}
