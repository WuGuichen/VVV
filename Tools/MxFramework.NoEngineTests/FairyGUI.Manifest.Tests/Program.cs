using System;
using System.IO;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static class Program
    {
        private static int Main()
        {
            ValidManifest_HasNoDiagnostics();
            RuntimeHudSourceManifest_ValidatesCurrentPackageSources();
            RuntimeHudManifestProjection_FeedsViewContract();
            RuntimeHudSourceManifest_ReportsMissingControl();
            RuntimeHudSourceManifest_ReportsMissingCommandButton();
            InvalidManifest_ReturnsStructuredDiagnostics();
            InvalidSchemaVersion_IsReported();
            CommandBoundToTextControl_IsReported();
            DuplicateIds_AreReported();
            Console.WriteLine("FairyGUI manifest noEngine tests passed.");
            return 0;
        }

        private static void ValidManifest_HasNoDiagnostics()
        {
            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(CreateValidManifest());

            Require(result.Success, "valid manifest should pass validation.");
            Require(result.Diagnostics.Count == 0, "valid manifest should not produce diagnostics.");
        }

        private static void RuntimeHudSourceManifest_ValidatesCurrentPackageSources()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            ResourceCatalog catalog = CreateRuntimeHudCatalog();

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                catalog);

            Require(result.Success, "Runtime HUD manifest should validate against source XML and package bytes.");
        }

        private static void RuntimeHudManifestProjection_FeedsViewContract()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "Runtime HUD manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.runtimehud.main"), "projected view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxRuntimeHud", "projected package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "RuntimeHudPanel", "projected component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Hud, "projected layer should match manifest layer.");
            Require(contract.Commands.Count == 2, "projected commands should come from manifest command bindings.");
            Require(contract.RequiredResources.Count == 1 && contract.RequiredResources[0] == "ui.fairygui.runtimehud.fui", "projected resources should include package bytes id.");
        }

        private static void RuntimeHudSourceManifest_ReportsMissingControl()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = manifest.Views[0].ViewId,
                    PackageId = manifest.Views[0].PackageId,
                    ComponentName = manifest.Views[0].ComponentName,
                    ComponentSourcePath = manifest.Views[0].ComponentSourcePath,
                    ViewModelType = manifest.Views[0].ViewModelType,
                    Layer = manifest.Views[0].Layer,
                    RequiredResources = manifest.Views[0].RequiredResources,
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "Text")
                    },
                    Commands = manifest.Views[0].Commands
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                CreateRuntimeHudCatalog());

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlMissing), "missing renamed control should be reported.");
        }

        private static void RuntimeHudSourceManifest_ReportsMissingCommandButton()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = manifest.Views[0].ViewId,
                    PackageId = manifest.Views[0].PackageId,
                    ComponentName = manifest.Views[0].ComponentName,
                    ComponentSourcePath = manifest.Views[0].ComponentSourcePath,
                    ViewModelType = manifest.Views[0].ViewModelType,
                    Layer = manifest.Views[0].Layer,
                    RequiredResources = manifest.Views[0].RequiredResources,
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("btnGhost", "Button", required: false)
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("runtimeHud.ghost", "btnGhost")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                CreateRuntimeHudCatalog());

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlUnknown), "missing command button in source XML should be reported.");
        }

        private static void InvalidManifest_ReturnsStructuredDiagnostics()
        {
            var manifest = new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "mx.smoke",
                        PackageName = "",
                        PackageBytes = new ResourceKey("Bad Key", MxFairyGuiManifestResourceTypeIds.PackageBytes)
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke"),
                        PackageId = "mx.missing",
                        ComponentName = "",
                        RequiredResources = new[] { new ResourceKey("", ResourceTypeIds.Texture2D) },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("txtTitle", "", required: true)
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("refresh", "btnMissing")
                        }
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(!result.Success, "invalid manifest should fail validation.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageNameMissing), "missing package name should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ResourceKeyInvalid), "invalid resources should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ViewPackageUnknown), "unknown package should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ComponentMissing), "missing component should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlTypeMissing), "missing control type should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlUnknown), "unknown command control should be reported.");

            MxFairyGuiManifestDiagnostic diagnostic = Find(result, MxFairyGuiManifestDiagnosticCode.CommandControlUnknown);
            Require(diagnostic.Target == MxFairyGuiManifestDiagnosticTarget.Command, "command diagnostic should target commands.");
            Require(diagnostic.PackageId == "mx.missing", "command diagnostic should carry package id.");
            Require(diagnostic.ViewId == "ui.smoke", "command diagnostic should carry view id.");
            Require(diagnostic.Field == "commands[0].controlName", "command diagnostic should carry field path.");
        }

        private static void InvalidSchemaVersion_IsReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.SchemaVersion = "mx.fairygui.manifest.v0";

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ManifestSchemaVersionUnsupported), "unsupported schema version should be reported.");
        }

        private static void CommandBoundToTextControl_IsReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "SmokePanel",
                    ViewModelType = "SmokeViewModel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "txtTitle")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlNotButton), "command binding to a text control should be reported.");
        }

        private static void DuplicateIds_AreReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Packages = new[]
            {
                manifest.Packages[0],
                new MxFairyGuiManifestPackage
                {
                    PackageId = "mx.smoke",
                    PackageName = "MxFguiSmoke",
                    PackageBytes = new ResourceKey("ui.smoke.copy.bytes", MxFairyGuiManifestResourceTypeIds.PackageBytes)
                }
            };
            manifest.Views = new[]
            {
                manifest.Views[0],
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "OtherPanel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                        new MxFairyGuiNamedControl("txtTitle", "GTextField")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "txtTitle"),
                        new MxFairyGuiCommandBinding("refresh", "txtTitle")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageIdDuplicate), "duplicate package id should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageNameDuplicate), "duplicate package name should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ViewIdDuplicate), "duplicate view id should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlDuplicate), "duplicate control should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandDuplicate), "duplicate command should be reported.");
        }

        private static MxFairyGuiManifest CreateValidManifest()
        {
            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "mx.smoke",
                        PackageName = "MxFguiSmoke",
                        PackageBytes = new ResourceKey("ui.smoke.bytes", MxFairyGuiManifestResourceTypeIds.PackageBytes),
                        RequiredResources = new[]
                        {
                            new MxFairyGuiManifestResource(new ResourceKey("ui.smoke.atlas", ResourceTypeIds.Texture2D), MxFairyGuiManifestResourceKind.Atlas)
                        }
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke"),
                        PackageId = "mx.smoke",
                        ComponentName = "SmokePanel",
                        ViewModelType = "SmokeViewModel",
                        RequiredResources = new[] { new ResourceKey("ui.smoke.title", ResourceTypeIds.Object) },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                            new MxFairyGuiNamedControl("btnRefresh", "GButton")
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("refresh", "btnRefresh")
                        }
                    }
                }
            };
        }

        private static MxFairyGuiManifest CreateRuntimeHudManifest()
        {
            ResourceKey packageBytes = new ResourceKey(
                "ui.fairygui.runtimehud.fui",
                MxFairyGuiManifestResourceTypeIds.PackageBytes,
                packageId: "MxRuntimeHud");

            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "MxRuntimeHud",
                        PackageName = "MxRuntimeHud",
                        PackageSourcePath = "FGUIProject/assets/MxRuntimeHud/package.xml",
                        PackageBytesPath = "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes",
                        PackageBytes = packageBytes
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.runtimehud.main"),
                        PackageId = "MxRuntimeHud",
                        ComponentName = "RuntimeHudPanel",
                        ComponentSourcePath = "FGUIProject/assets/MxRuntimeHud/RuntimeHudPanel.xml",
                        ViewModelType = "MxFramework.Demo.RuntimeAbilitySliceHudViewModel",
                        Layer = MxUiLayer.Hud,
                        RequiredResources = new[] { packageBytes },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("title", "Text", bindPath: "Title"),
                            new MxFairyGuiNamedControl("mode", "Text", bindPath: "ModeName"),
                            new MxFairyGuiNamedControl("playerName", "Text", bindPath: "Player.DisplayName"),
                            new MxFairyGuiNamedControl("playerHp", "Text", bindPath: "Player.Hp"),
                            new MxFairyGuiNamedControl("enemyName", "Text", bindPath: "Enemy.DisplayName"),
                            new MxFairyGuiNamedControl("enemyHp", "Text", bindPath: "Enemy.Hp"),
                            new MxFairyGuiNamedControl("recentAction", "Text", bindPath: "Feedback.RecentActionText"),
                            new MxFairyGuiNamedControl("btnStrike", "Button"),
                            new MxFairyGuiNamedControl("btnReset", "Button")
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("runtimeHud.strike", "btnStrike"),
                            new MxFairyGuiCommandBinding("runtimeHud.reset", "btnReset")
                        }
                    }
                }
            };
        }

        private static ResourceCatalog CreateRuntimeHudCatalog()
        {
            return new ResourceCatalog(
                "ui.runtimehud.test.catalog",
                "MxRuntimeHud",
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.fairygui.runtimehud.fui",
                        MxFairyGuiManifestResourceTypeIds.PackageBytes,
                        "memory",
                        "fgui/MxRuntimeHud_fui.bytes",
                        packageId: "MxRuntimeHud")
                });
        }

        private static string FindRepositoryRoot()
        {
            string path = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(Path.Combine(path, "FGUIProject", "FGUIProject.fairy")))
                    return path;

                path = Directory.GetParent(path)?.FullName;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }

        private static MxFairyGuiManifestDiagnostic Find(MxFairyGuiManifestValidationResult result, MxFairyGuiManifestDiagnosticCode code)
        {
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Code == code)
                    return result.Diagnostics[i];
            }

            throw new InvalidOperationException("Diagnostic not found: " + code + ".");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
