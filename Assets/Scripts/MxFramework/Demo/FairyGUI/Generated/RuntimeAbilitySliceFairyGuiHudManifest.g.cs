using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class RuntimeAbilitySliceFairyGuiHudManifest
    {
        public static MxFairyGuiManifest Create()
        {
            ResourceKey packageBytes = RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey;

            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                        PackageName = RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                        PackageSourcePath = "FGUIProject/assets/MxRuntimeHud/package.xml",
                        PackageBytesPath = "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes",
                        PackageBytes = packageBytes
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = RuntimeAbilitySliceFairyGuiHudIds.ViewId,
                        PackageId = RuntimeAbilitySliceFairyGuiHudIds.PackageId,
                        ComponentName = RuntimeAbilitySliceFairyGuiHudIds.ComponentName,
                        ComponentSourcePath = "FGUIProject/assets/MxRuntimeHud/RuntimeHudPanel.xml",
                        ViewModelType = typeof(RuntimeAbilitySliceHudViewModel).FullName,
                        Layer = MxUiLayer.Hud,
                        RequiredResources = new[] { packageBytes },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.Title, "Text", bindPath: "Title"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.Mode, "Text", bindPath: "ModeName"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.PlayerName, "Text", bindPath: "Player.DisplayName"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.PlayerHp, "Text", bindPath: "Player.Hp"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.EnemyName, "Text", bindPath: "Enemy.DisplayName"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.EnemyHp, "Text", bindPath: "Enemy.Hp"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.RecentAction, "Text", bindPath: "Feedback.RecentActionText"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.Strike, "Button"),
                            new MxFairyGuiNamedControl(RuntimeAbilitySliceFairyGuiHudIds.Reset, "Button")
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding(RuntimeAbilitySliceHudCommandIds.Strike, RuntimeAbilitySliceFairyGuiHudIds.Strike),
                            new MxFairyGuiCommandBinding(RuntimeAbilitySliceHudCommandIds.Reset, RuntimeAbilitySliceFairyGuiHudIds.Reset)
                        }
                    }
                }
            };
        }

        public static MxUiViewContract CreateViewContract()
        {
            return MxFairyGuiManifestProjection.CreateViewContracts(Create())[0];
        }

        public static MxFairyGuiPackageDescriptor CreatePackageDescriptor()
        {
            MxFairyGuiManifest manifest = Create();
            MxFairyGuiManifestPackage package = manifest.Packages[0];
            return new MxFairyGuiPackageDescriptor(package.PackageId, package.PackageBytes);
        }
    }
}
