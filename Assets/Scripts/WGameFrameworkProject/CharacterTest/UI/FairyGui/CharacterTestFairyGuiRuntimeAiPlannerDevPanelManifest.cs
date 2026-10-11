using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest
    {
        public static MxFairyGuiManifest Create()
        {
            ResourceKey packageBytes = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey;
            ResourceKey packageResource0 = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key;
            ResourceKey packageResource1 = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Page1Key;
            ResourceKey packageResource2 = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Page2Key;
            ResourceKey packageResource3 = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Page3Key;
            ResourceKey recoveryPackageBytes =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageBytesKey;
            ResourceKey recoveryAtlas0 =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryAtlas0Key;

            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        PackageName = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        PackageSourcePath = @"FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/package.xml",
                        PackageBytesPath = @"Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi_fui.bytes",
                        PackageBytes = packageBytes,
                        RequiredResources = new[]
                        {
                            new MxFairyGuiManifestResource(
                                packageResource0,
                                MxFairyGuiManifestResourceKind.Atlas),
                            new MxFairyGuiManifestResource(packageResource1, MxFairyGuiManifestResourceKind.Atlas),
                            new MxFairyGuiManifestResource(packageResource2, MxFairyGuiManifestResourceKind.Atlas),
                            new MxFairyGuiManifestResource(packageResource3, MxFairyGuiManifestResourceKind.Atlas)
                        }
                    },
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageId,
                        PackageName = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageId,
                        PackageSourcePath =
                            @"FGUIProject/assets/MxCharacterTestRuntimeAiLabRecoveryUi/package.xml",
                        PackageBytesPath =
                            @"Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabRecoveryUi/MxCharacterTestRuntimeAiLabRecoveryUi_fui.bytes",
                        PackageBytes = recoveryPackageBytes,
                        RequiredResources = new[]
                        {
                            new MxFairyGuiManifestResource(
                                recoveryAtlas0,
                                MxFairyGuiManifestResourceKind.Atlas)
                        }
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        PackageId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        ComponentName = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName,
                        ComponentSourcePath =
                            @"FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel.xml",
                        ViewModelType =
                            typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel).FullName,
                        Layer = MxUiLayer.Debug,
                        RequiredResources = new[]
                        {
                            packageBytes,
                            packageResource0,
                            packageResource1,
                            packageResource2
                        },
                        NamedControls = CreateNamedControls(),
                        Commands = CreateCommands()
                    },
                    new MxFairyGuiManifestView
                    {
                        ViewId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId,
                        PackageId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageId,
                        ComponentName = CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryComponentName,
                        ComponentSourcePath =
                            @"FGUIProject/assets/MxCharacterTestRuntimeAiLabRecoveryUi/RuntimeAiLabRecoverySurface.xml",
                        ViewModelType =
                            typeof(CharacterTestRuntimeAiLabShellSurfaceViewModel).FullName,
                        Layer = MxUiLayer.Debug,
                        RequiredResources = new[]
                        {
                            recoveryPackageBytes,
                            recoveryAtlas0
                        },
                        NamedControls = CreateRecoveryNamedControls(),
                        Commands = CreateRecoveryCommands()
                    }
                }
            };
        }

        public static MxUiViewContract CreateViewContract()
        {
            return MxFairyGuiManifestProjection.CreateViewContracts(Create())[0];
        }

        internal static MxUiViewContract CreateRecoveryViewContract()
        {
            return MxFairyGuiManifestProjection.CreateViewContracts(Create())[1];
        }

        public static MxFairyGuiPackageDescriptor CreatePackageDescriptor()
        {
            MxFairyGuiManifest manifest = Create();
            return CreatePackageDescriptor(manifest.Packages[0]);
        }

        internal static MxFairyGuiPackageDescriptor CreateRecoveryPackageDescriptor()
        {
            MxFairyGuiManifest manifest = Create();
            return CreatePackageDescriptor(manifest.Packages[1]);
        }

        private static MxFairyGuiPackageDescriptor CreatePackageDescriptor(
            MxFairyGuiManifestPackage package)
        {
            var resources = new MxFairyGuiPackageResourceDescriptor[package.RequiredResources.Count];
            for (int i = 0; i < resources.Length; i++)
            {
                MxFairyGuiManifestResource resource = package.RequiredResources[i];
                resources[i] = new MxFairyGuiPackageResourceDescriptor(
                    resource.Key,
                    ToPackageResourceKind(resource.Kind),
                    resource.Required);
            }

            return new MxFairyGuiPackageDescriptor(package.PackageId, package.PackageBytes, resources);
        }

        private static MxFairyGuiNamedControl[] CreateNamedControls()
        {
            var controls = new List<MxFairyGuiNamedControl>
            {
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, "Title"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, "StatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, "PhaseText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, "FrameSummary"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewHeader, "PreviewHeader"),
                Text(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewInstructionsText,
                    "PreviewInstructionsText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewStatusText, "PreviewStatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupHeader, "SetupHeader"),
                Text(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupInstructionsText,
                    "SetupInstructionsText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedSlotText, "SetupSelectedSlotText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText, "SetupValidationText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1Header),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2Header),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText, "SetupCharacterText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText, "SetupBehaviorGroupText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupPositionText, "SetupPositionText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupWorkspaceBackground),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopBreadcrumb),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation, "SetupTopValidationText"),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1SummaryCard),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2SummaryCard),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedCardBackground),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadge),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadgeText, "SetupAgentBadgeText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesBackground),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesHeader),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupDistanceText, "SetupDistanceText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyBackground),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyTitle, "SetupReadyTitleText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyDescription, "SetupReadyDescriptionText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneStatusBar),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneText, "SetupSceneText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentCountText, "SetupAgentCountText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupCountText, "SetupBehaviorGroupCountText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupFrameLimitStatusText, "SetupFrameLimitStatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineHeader, "TimelineHeader"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineText, "TimelineText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineScrubTrack),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressTrack),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressFill),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressThumb),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentHeader, "AgentHeader"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentListText, "AgentListText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DecisionHeader, "DecisionHeader"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SelectedAgentText, "SelectedAgentText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsText, "FactsText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PathText, "PathText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UtilityText, "UtilityText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CommandText, "CommandText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsText, "DiagnosticsText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.EditGateText, "EditGateText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.StateValidationText, "StateValidationText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText, "WorkbenchSummaryText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText, "WorkbenchDetailText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringContextBar),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringGroupText, "AuthoringGroupContextText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringCardText, "AuthoringCardContextText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText, "DraftSaveStatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText, "AuthoringPublishedStateText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText, "CurrentTestSourceStatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringProblemText, "AuthoringProblemText"),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChangesPopup),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NavigationFeedbackOverlay),
                Component(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .AuthoringWorkspacePickerPrototype),
                List(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList, "WorkbenchRows"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardWorkspaceBackground),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TacticalLibraryPage),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TacticalLibrary),
                Button(CharacterTestAiLabCardTrialControlContract.Entry),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText, "DraftStatusText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText, "PublishDraftStatusText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineRevisionTrackText, "TimelineRevisionTrackText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineDecisionTrackText, "TimelineDecisionTrackText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineActionTrackText, "TimelineActionTrackText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineCombatTrackText, "TimelineCombatTrackText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineWarningTrackText, "TimelineWarningTrackText"),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeHost),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LatestFrame),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrameInput, "JumpFrameInputText"),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrame),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopStart),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopEnd),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleLoop),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEditGate),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleStateCapture),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ValidateState),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SyncRecordedState),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ColdBootstrapVerify),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorPanel),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTitle),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorIdentity),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorNameLabel),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorName, "SetupBehaviorGroupEditorDisplayName"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescriptionLabel),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorDescription, "SetupBehaviorGroupEditorDescription"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTagsLabel),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorTags, "SetupBehaviorGroupEditorTagsText"),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorStatus),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTab),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsFactsTab),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsRuntimeFrameTab),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsReplayTab),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReloadProjectDraft),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReapplyProjectDraft),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataPanel),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataTitle),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDisplayName, "projectCardDisplayName"),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardCost, "projectCardCost"),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDescription, "projectCardDescription"),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardUseSummary, "projectCardUseSummary"),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardRiskSummary, "projectCardRiskSummary"),
                Input(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardTags, "projectCardTags"),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit)
            };
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                    controls.Add(Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex)));
            }

            return controls.ToArray();
        }

        private static MxFairyGuiNamedControl[] CreateRecoveryNamedControls()
        {
            return new[]
            {
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceScrim),
                Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfacePanel),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceTitle),
                Text(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceDetail),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellRestore),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellReset),
                Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellHide)
            };
        }

        private static MxFairyGuiCommandBinding[] CreateRecoveryCommands()
        {
            return new[]
            {
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellRestore),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellReset),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellHide,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellHide)
            };
        }

        private static MxFairyGuiNamedControl Component(string name)
        {
            return new MxFairyGuiNamedControl(name, "Component");
        }

        private static MxFairyGuiCommandBinding[] CreateCommands()
        {
            var commands = new List<MxFairyGuiCommandBinding>
            {
                Command(CharacterTestAiLabCardTrialControlContract.OpenCommand, CharacterTestAiLabCardTrialControlContract.Entry),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LatestFrame),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrame),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopStart),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopEnd),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleLoop),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FrameLimit,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEditGate),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleDetails,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleStateCapture,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleStateCapture),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ValidateState,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ValidateState),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SyncRecordedState,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SyncRecordedState),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ColdBootstrapVerify,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ColdBootstrapVerify),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSetupSnap,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MirrorSpawns,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ResetSpawnDefaults,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSelectedSetupSlot,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupCharacter,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupCharacter,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupBehaviorGroup,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CreateSetupBehaviorGroup,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerCreate),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteSetupBehaviorGroup,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupPickerDelete),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveSetupBehaviorGroupBasicInfo,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupEditorSave),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectAuthoringWorkspaceRole,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges),
                Command(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTab),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsFactsTab),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsRuntimeFrameTab),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsReplayTab),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayCancel, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayRetry, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomOut, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomIn, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFit, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanLeft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanRight, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaySelectItem, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayNavigateItem, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayClearItemSelection, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayLocateDivergence, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayReturn, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenBehaviorCard, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateRuntimeFrame, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsReturnSourceFrame, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryUp, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryDown, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.EditSelectedBehaviorData, "btnWorkbenchEdit"),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.UndoDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveBehaviorGroup, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReloadProjectDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReloadProjectDraft),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReapplyProjectDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReapplyProjectDraft),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDisplayName),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardCost, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardCost),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDescription, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDescription),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardUseSummary, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardUseSummary),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardRiskSummary, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardRiskSummary),
                Command(CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardTags, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardTags)
            };
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    commands.Add(Command(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupSlot(teamId, slotIndex),
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex)));
                }
            }

            return commands.ToArray();
        }

        private static MxFairyGuiCommandBinding Command(string commandId, string controlName)
        {
            return new MxFairyGuiCommandBinding(commandId, controlName);
        }

        private static MxFairyGuiNamedControl Text(string name, string bindingPath = "")
        {
            return string.IsNullOrEmpty(bindingPath)
                ? new MxFairyGuiNamedControl(name, "Text")
                : new MxFairyGuiNamedControl(name, "Text", bindPath: bindingPath);
        }

        private static MxFairyGuiNamedControl Input(string name, string bindingPath)
        {
            return new MxFairyGuiNamedControl(name, "Input", bindPath: bindingPath);
        }

        private static MxFairyGuiNamedControl Button(string name)
        {
            return new MxFairyGuiNamedControl(name, "Button");
        }

        private static MxFairyGuiNamedControl List(string name, string bindingPath = "")
        {
            return string.IsNullOrEmpty(bindingPath)
                ? new MxFairyGuiNamedControl(name, "List")
                : new MxFairyGuiNamedControl(name, "List", bindPath: bindingPath);
        }

        private static MxFairyGuiNamedControl Graph(string name)
        {
            return new MxFairyGuiNamedControl(name, "Graph");
        }

        private static MxFairyGuiPackageResourceKind ToPackageResourceKind(
            MxFairyGuiManifestResourceKind kind)
        {
            switch (kind)
            {
                case MxFairyGuiManifestResourceKind.Atlas:
                    return MxFairyGuiPackageResourceKind.Atlas;
                case MxFairyGuiManifestResourceKind.Audio:
                    return MxFairyGuiPackageResourceKind.Audio;
                default:
                    return MxFairyGuiPackageResourceKind.Resource;
            }
        }
    }
}
