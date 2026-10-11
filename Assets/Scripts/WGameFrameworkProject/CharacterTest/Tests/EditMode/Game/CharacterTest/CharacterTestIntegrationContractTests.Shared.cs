using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Config;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {

private const string LightAttackSecondIntentId = "LightAttack2";

private const string LightAttackThirdIntentId = "LightAttack3";

private static readonly CharacterActionId LightAttackSecondActionId = new CharacterActionId(324);

private static readonly CharacterActionId LightAttackThirdActionId = new CharacterActionId(325);

private const string LightAttackSecondAnimationClipResourceKey = "art.character.skeleton.animation.katana_atk2";

private const string LightAttackThirdAnimationClipResourceKey = "art.character.skeleton.animation.katana_atk3";

private const int IronSwordTraceProfileId = CharacterTestAuthoredContentIds.D403_Id554678986;


private const int LightAttackHitTraceStartFrame = 23;

private const int LightAttackHitTraceStopFrame = 28;

[TearDown]
        public void TearDown()
        {
            DestroyObjectNamed("MxFramework Default Input Service");
            ResetFairyGuiTestState();
        }

private static void AssertMainMenuPackageCanCreateImageBackedComponentAfterReload(
            ResourceManager resourceManager)
        {
            var bridge = new MxFairyGuiResourceBridge(resourceManager);
            var package = new MxFairyGuiPackageDescriptor(
                CharacterTestFairyGuiMainMenuIds.PackageId,
                CharacterTestFairyGuiMainMenuIds.PackageBytesKey,
                new[]
                {
                    new MxFairyGuiPackageResourceDescriptor(
                        CharacterTestFairyGuiMainMenuIds.Atlas0Key,
                        MxFairyGuiPackageResourceKind.Atlas)
                });

            for (int i = 0; i < 2; i++)
                AssertMainMenuPackageCreatesImageBackedComponent(bridge, package);
        }

private static void AssertMainMenuPackageCreatesImageBackedComponent(
            MxFairyGuiResourceBridge bridge,
            MxFairyGuiPackageDescriptor package)
        {
            MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
            Assert.IsTrue(load.Success, load.Failure.Message);

            var host = new MxFairyGuiHost();
            try
            {
                string failure;
                Assert.IsTrue(host.EnsurePackage(load.Scope, out failure), failure);
                Assert.IsTrue(host.TryCreateComponent(
                    CharacterTestFairyGuiMainMenuIds.PackageId,
                    CharacterTestFairyGuiMainMenuIds.ComponentName,
                    out IMxFairyGuiComponentHandle handle,
                    out failure), failure);

                try
                {
                    var concrete = handle as MxFairyGuiComponentHandle;
                    Assert.NotNull(concrete);
                    AssertImageTextureAvailable(
                        concrete.Component,
                        CharacterTestFairyGuiMainMenuIds.PanelFrame);
                    AssertImageTextureAvailable(concrete.Component, "panelTrim");
                }
                finally
                {
                    handle?.Dispose();
                }
            }
            finally
            {
                if (load.Scope != null)
                {
                    host.ReleasePackage(load.Scope.PackageId);
                    load.Scope.Release();
                }

                RemoveCharacterTestMainMenuPackageIfLoaded();
            }
        }

private static void AssertPublishedNetworkPanelControlTypes()
        {
            Fgui.GObject publishedObject = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiNetworkPanelIds.PackageId,
                CharacterTestFairyGuiNetworkPanelIds.ComponentName);
            try
            {
                Fgui.GComponent panel = publishedObject?.asCom;
                Assert.NotNull(panel);
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.PortInput));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.RoomNameInput));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.DirectLanMode));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.RoomMode));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.Host));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.Join));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.Disconnect));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiNetworkPanelIds.Close));
                Assert.IsNull(panel.GetChild("btnSettings"));
            }
            finally
            {
                publishedObject?.Dispose();
            }
        }

private static void AssertPublishedSettingsPanelControlTypes()
        {
            Fgui.GObject publishedObject = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.ComponentName);
            try
            {
                Fgui.GComponent panel = publishedObject?.asCom;
                Assert.NotNull(panel);
                Assert.IsInstanceOf<Fgui.GImage>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.PanelFrame));
                Assert.IsInstanceOf<Fgui.GGraph>(
                    panel.GetChild("sidebarBackground"));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput));
                Assert.IsInstanceOf<Fgui.GTextInput>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.NavGraphics));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.NavAudio));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.NavControls));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.GamepadTab));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.LanguagePrev));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.LanguageNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.Apply));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.CancelChanges));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.ResetInput));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiSettingsPanelIds.Close));
                Fgui.GList inputList = panel.GetChild(
                    CharacterTestFairyGuiSettingsPanelIds.InputList)?.asList;
                Assert.NotNull(inputList);
                Assert.IsNotEmpty(inputList.defaultItem);
            }
            finally
            {
                publishedObject?.Dispose();
            }
        }

private static void AssertPublishedAbilityBindingPanelControlTypes()
        {
            Fgui.GObject publishedObject = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                CharacterTestFairyGuiAbilityBindingPanelIds.ComponentName);
            try
            {
                Fgui.GComponent panel = publishedObject?.asCom;
                Assert.NotNull(panel);
                Assert.AreEqual(1920f, panel.width);
                Assert.AreEqual(1080f, panel.height);
                Assert.IsInstanceOf<Fgui.GImage>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.PanelFrame));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Title));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Status));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Message));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(1)));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(0)));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(11)));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Clear));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Save));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.Cancel));
            }
            finally
            {
                publishedObject?.Dispose();
            }
        }

private static void AssertPublishedMainMenuControlTypes()
        {
            Fgui.GObject publishedObject = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiMainMenuIds.PackageId,
                CharacterTestFairyGuiMainMenuIds.ComponentName);
            try
            {
                Fgui.GComponent panel = publishedObject?.asCom;
                Assert.NotNull(panel);
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Title));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Status));
                Assert.IsInstanceOf<Fgui.GTextField>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Message));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Start));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Network));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Abilities));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.BehaviorDeck));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Settings));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Restart));
                Assert.IsInstanceOf<Fgui.GButton>(
                    panel.GetChild(CharacterTestFairyGuiMainMenuIds.Close));
            }
            finally
            {
                publishedObject?.Dispose();
            }
        }

private static void AssertPublishedMainMenuImageTexturesAvailable()
        {
            Fgui.GObject publishedObject = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiMainMenuIds.PackageId,
                CharacterTestFairyGuiMainMenuIds.ComponentName);
            try
            {
                Fgui.GComponent panel = publishedObject?.asCom;
                Assert.NotNull(panel);
                AssertImageTextureAvailable(panel, CharacterTestFairyGuiMainMenuIds.PanelFrame);
                AssertImageTextureAvailable(panel, "panelTrim");
            }
            finally
            {
                publishedObject?.Dispose();
            }
        }

private static void AssertImageTextureAvailable(Fgui.GComponent panel, string childName)
        {
            var image = panel.GetChild(childName) as Fgui.GImage;
            Assert.NotNull(image, childName);
            Assert.NotNull(image.texture, childName);
            Assert.IsFalse(image.texture.disposed, childName);
            Assert.NotNull(image.texture.nativeTexture, childName);
        }

private static Fgui.GComponent GetOpenMainMenuComponent(CharacterTestFairyGuiMainMenuHost host)
        {
            Assert.NotNull(host);
            MxFairyGuiNavigator navigator = GetPrivateField<MxFairyGuiNavigator>(host, "_navigator");
            Assert.NotNull(navigator);
            Assert.IsTrue(navigator.TryGetOpenView(CharacterTestFairyGuiMainMenuIds.ViewId, out IMxUiView view));
            var typedView = view as MxFairyGuiView<CharacterTestFairyGuiMainMenuViewModel>;
            Assert.NotNull(typedView);
            var handle = typedView.Component as MxFairyGuiComponentHandle;
            Assert.NotNull(handle);
            Assert.IsFalse(handle.IsDisposed);
            Assert.NotNull(handle.Component);
            return handle.Component;
        }

private static void AssertEntry(
            ResourceCatalog catalog,
            string id,
            string typeId,
            string providerId,
            string requiredLabel)
        {
            ResourceCatalogEntry entry = catalog.Entries.FirstOrDefault(candidate => candidate.Id == id);
            Assert.NotNull(entry, "Missing catalog entry: " + id);
            Assert.AreEqual(typeId, entry.TypeId);
            Assert.AreEqual(providerId, entry.ProviderId);
            Assert.That(entry.Address, Is.Not.Empty);
            Assert.That(entry.Labels, Does.Contain(requiredLabel));
        }

private static void AssertSlotWindow(
            CharacterActionConfig action,
            int actionSlotMask,
            int startFrame,
            int endFrame)
        {
            CharacterCancelRule rule = action.CancelRules.Single(candidate =>
                candidate.CancelInputMask == (CharacterCancelInputMask)actionSlotMask);
            Assert.AreEqual(startFrame, rule.StartFrame);
            Assert.AreEqual(endFrame, rule.EndFrame);
            Assert.AreEqual(CharacterActionRequestType.Any, rule.RequestTypeMask);
        }

private static CharacterTestCharacterEntity CreateRuntimeCharacter()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            return entity;
        }

private static readonly CharacterActionId TransitionFollowUpActionId = new CharacterActionId(1202001);
        private static readonly CharacterActionId TransitionAbilityFollowUpActionId = new CharacterActionId(1202002);

private static CharacterTestCharacterActionRuntime CreateTransitionActionRuntime(
            bool allowFollowUp = true,
            bool allowAbilityFollowUp = true)
        {
            var cancelRules = new List<CharacterCancelRule>();
            if (allowFollowUp)
            {
                cancelRules.Add(new CharacterCancelRule(
                    0,
                    2));
            }

            if (allowAbilityFollowUp)
            {
                cancelRules.Add(new CharacterCancelRule(
                    0,
                    2));
            }

            var actionSet = new CharacterActionSetConfig(
                1,
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                "Transition Test",
                string.Empty,
                string.Empty,
                new[]
                {
                    new CharacterActionBinding(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, new MxFramework.CharacterAction.CharacterActionId(301)),
                    new CharacterActionBinding(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0004, TransitionFollowUpActionId),
                    new CharacterActionBinding(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0001, TransitionAbilityFollowUpActionId),
                },
                Array.Empty<CharacterAbilityActionBinding>(),
                Array.Empty<CharacterReactionBinding>(),
                0,
                string.Empty,
                MxFramework.CharacterAction.CharacterActionId.None);
            CharacterActionConfig lightAttack = CreateTransitionAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId.Value,
                "light_attack",
                CharacterActionCategory.BasicAttack,
                cancelRules.ToArray());
            CharacterActionConfig followUp = CreateTransitionAction(
                TransitionFollowUpActionId.Value,
                "follow_up",
                CharacterActionCategory.BasicAttack);
            CharacterActionConfig abilityFollowUp = CreateTransitionAction(
                TransitionAbilityFollowUpActionId.Value,
                "ability_follow_up",
                CharacterActionCategory.Skill);
            CharacterActionConfig[] actions =
            {
                lightAttack,
                followUp,
                abilityFollowUp
            };
            var import = new CharacterActionJsonImportResult(
                new[] { actionSet },
                actions,
                Array.Empty<CharacterActionJsonDiagnostic>());
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                "transition-test-action-sets",
                "transition-test-actions");
            CharacterActionValidationReport validation = CharacterActionValidationReport.Validate(
                registry,
                new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Ignore));
            Assert.IsTrue(validation.Success);

            return new CharacterTestCharacterActionRuntime(
                import,
                registry,
                validation,
                actionSet,
                CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback,
                "transition test",
                "transition-test-action-sets",
                "transition-test-actions",
                Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>());
        }

private static CharacterActionConfig CreateTransitionAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            CharacterCancelRule[] cancelRules = null)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                category,
                CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(),
                priority: 10,
                durationFrames: 6,
                requirements: Array.Empty<CharacterActionRequirement>(),
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 5)
                },
                cancelRules: cancelRules,
                interruptRules: Array.Empty<CharacterInterruptRule>());
        }

private static AbilityRuntimeModule CreateTransitionAbilityRuntime(
            out AbilityDefinitionConfig ability)
        {
            var registry = new AbilityDefinitionRegistry();
            ability = new AbilityDefinitionConfig(
                900,
                "ability.transition_follow_up",
                "Transition Follow Up",
                AbilityInputBindingConfig.Empty,
                new AbilityCooldownConfig(30L, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0082),
                Array.Empty<AbilityCostConfig>(),
                0,
                new AbilityActionBindingConfig(TransitionAbilityFollowUpActionId, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0001),
                default,
                0,
                0);
            Assert.IsTrue(registry.TryRegister(ability, out string failure), failure);
            return new AbilityRuntimeModule(registry);
        }

private static MxCameraUnityObstructionContext CreateCameraObstructionContext(
            Vector3 desiredPosition,
            float recoverySharpness,
            Vector3? focusCenter = null)
        {
            Vector3 resolvedFocus = focusCenter ?? new Vector3(0f, 1.45f, 0f);
            var state = new MxCameraState(
                new MxCameraRigId("rig"),
                new MxCameraProfileId("profile"),
                MxCameraUnityConversions.ToCameraVector(desiredPosition),
                new MxCameraVector3(0f, 0f, -1f),
                MxCameraVector3.Up,
                new MxCameraEulerRotation(0f, 180f, 0f),
                MxCameraProjectionKind.Perspective,
                60f,
                5f,
                MxCameraUnityConversions.ToCameraVector(resolvedFocus),
                MxCameraVector3.Zero,
                0f,
                0f,
                MxCameraStateSource.Normal);
            return new MxCameraUnityObstructionContext(
                MxCameraUnityObstructionMode.FrameworkPhysicsAabb,
                state,
                desiredPosition,
                Quaternion.Euler(0f, 180f, 0f),
                1f / 60f,
                new MxCameraUnityObstructionSettings
                {
                    radius = 0.1f,
                    padding = 0.1f,
                    minDistance = 0.25f,
                    focusClearance = 0.25f,
                    recoverySharpness = recoverySharpness
                });
        }

private static CharacterTestCharacterPhysicsRuntime CreateCameraObstructionPhysicsRuntime(
            int obstacleLayer,
            int obstacleMaskLayer)
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(10000);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(10000),
                bodyId,
                ToFixVector3(new Vector3(0f, 1.45f, -1.5f))));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                obstacleLayer,
                ToFixVector3(new Vector3(-0.5f, -0.5f, -0.2f)),
                ToFixVector3(new Vector3(0.5f, 0.5f, 0.2f)),
                Fix64.Zero));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(obstacleMaskLayer),
                ToFixVector3(new Vector3(0.45f, 0.9f, 0.45f)),
                Fix64.Zero,
                Fix64.Zero,
                1,
                "camera obstruction test");
        }

private static CharacterTestCharacterPhysicsRuntime CreateCameraGroundObstructionPhysicsRuntime(
            int obstacleLayer,
            int obstacleMaskLayer)
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(10001);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(10001),
                bodyId,
                ToFixVector3(new Vector3(0f, -0.025f, 0f))));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                obstacleLayer,
                ToFixVector3(new Vector3(-20f, -0.025f, -20f)),
                ToFixVector3(new Vector3(20f, 0.025f, 20f)),
                Fix64.Zero));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(obstacleMaskLayer),
                ToFixVector3(new Vector3(0.45f, 0.9f, 0.45f)),
                Fix64.Zero,
                Fix64.Zero,
                1,
                "camera ground obstruction test");
        }

private static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(ToFix64(value.x), ToFix64(value.y), ToFix64(value.z));
        }

private static CharacterActionAudioCueRequest CreateAudioCueRequest(int cueId)
        {
            return new CharacterActionAudioCueRequest(
                new CharacterActionAdapterDispatchMetadata(
                    RuntimeFrame.Zero,
                    instanceId: 1,
                    planId: 1,
                    actionId: CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    localFrame: 0,
                    stableEventId: cueId,
                    sourceId: 1,
                    traceId: new RuntimeTraceIdentity(927253)),
                "1",
                cueId);
        }

private static void AssertVfxVector(float expectedX, float expectedY, float expectedZ, VfxVector3 actual)
        {
            Assert.AreEqual(ToVfxRaw(expectedX), actual.RawX);
            Assert.AreEqual(ToVfxRaw(expectedY), actual.RawY);
            Assert.AreEqual(ToVfxRaw(expectedZ), actual.RawZ);
        }

private static void AssertVfxVectorNear(
            float expectedX,
            float expectedY,
            float expectedZ,
            VfxVector3 actual,
            float tolerance)
        {
            long toleranceRaw = ToVfxRaw(tolerance);
            Assert.LessOrEqual(Math.Abs(ToVfxRaw(expectedX) - actual.RawX), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(ToVfxRaw(expectedY) - actual.RawY), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(ToVfxRaw(expectedZ) - actual.RawZ), toleranceRaw);
        }

private static void AssertFixVector(float expectedX, float expectedY, float expectedZ, FixVector3 actual)
        {
            Assert.AreEqual(ToFixRaw(expectedX), actual.X.RawValue);
            Assert.AreEqual(ToFixRaw(expectedY), actual.Y.RawValue);
            Assert.AreEqual(ToFixRaw(expectedZ), actual.Z.RawValue);
        }

private static FixVector3 ResolveExpectedArcMineForwardTarget(
            CharacterTestCharacterEntity caster,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            out FixVector3 normalizedForward)
        {
            FixVector3 center;
            if (physicsRuntime?.World != null
                && physicsRuntime.World.TryGetBody(physicsRuntime.GetBodyId(caster), out CombatPhysicsBody body))
            {
                center = body.Position;
            }
            else
            {
                center = new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(caster.Transform.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(caster.Transform.YMillimeters)
                        + (physicsRuntime?.GetCharacterCenterOffsetY(caster) ?? Fix64.One),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(caster.Transform.ZMillimeters));
            }

            FixVector3 forward = CharacterTestFireballProjectileAdapter.ResolveDirection(caster);
            forward = new FixVector3(forward.X, Fix64.Zero, forward.Z);
            if (!forward.TryNormalize(out normalizedForward))
                normalizedForward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            Fix64 fallback = Fix64.FromInt(CharacterTestArcMineIds.FallbackForwardMeters);
            return new FixVector3(
                center.X + normalizedForward.X * fallback,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(caster.Transform.YMillimeters),
                center.Z + normalizedForward.Z * fallback);
        }

private static void AssertFixVectorNear(FixVector3 expected, FixVector3 actual, long toleranceRaw)
        {
            Assert.LessOrEqual(Math.Abs(expected.X.RawValue - actual.X.RawValue), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(expected.Y.RawValue - actual.Y.RawValue), toleranceRaw);
            Assert.LessOrEqual(Math.Abs(expected.Z.RawValue - actual.Z.RawValue), toleranceRaw);
        }

private static void AssertFix64Near(Fix64 expected, Fix64 actual, long toleranceRaw)
        {
            Assert.LessOrEqual(Math.Abs(expected.RawValue - actual.RawValue), toleranceRaw);
        }

private static int ExpectedProjectileLifetimeFrames(
            FixVector3 origin,
            FixVector3 targetPoint,
            Fix64 speedPerFrame)
        {
            Fix64 distance = (targetPoint - origin).LengthSquared().Sqrt();
            if (distance <= Fix64.Zero || speedPerFrame <= Fix64.Zero)
                return 1;

            Fix64 frames = distance / speedPerFrame;
            long whole = frames.RawValue / Fix64.Scale;
            long remainder = frames.RawValue % Fix64.Scale;
            if (remainder > 0)
                whole++;
            if (whole <= 0)
                return 1;
            if (whole > int.MaxValue)
                return int.MaxValue;
            return (int)whole;
        }

private static void AlignTargetsWithDefaultProjectileSocketLine(
            CharacterTestCharacterEntity firstTarget,
            CharacterTestCharacterEntity secondTarget)
        {
            AlignTargetWithDefaultProjectileSocketLine(firstTarget);
            AlignTargetWithDefaultProjectileSocketLine(secondTarget);
        }

private static void AlignTargetWithDefaultProjectileSocketLine(CharacterTestCharacterEntity target)
        {
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0.95f, 0f));
        }

private static void PlaceTargetOnProjectileLine(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity target,
            CombatProjectileSnapshot projectile,
            Fix64 distance)
        {
            FixVector3 center = projectile.Origin + projectile.Direction * distance;
            Fix64 transformY = center.Y - physicsRuntime.GetCharacterCenterOffsetY(target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(
                ToFloat(center.X),
                ToFloat(transformY),
                ToFloat(center.Z),
                0f));
            SyncCharacterCombatCollider(physicsRuntime, target);
        }

private static void SyncCharacterCombatCollider(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity target)
        {
            CombatBodyId bodyId = physicsRuntime.GetBodyId(target);
            CombatEntityId entityId = physicsRuntime.GetEntityId(target);
            FixVector3 halfExtents = physicsRuntime.GetCharacterHalfExtents(target);
            FixVector3 centerPosition = new FixVector3(
                ToFix64(target.Transform.X),
                ToFix64(target.Transform.Y) + physicsRuntime.GetCharacterCenterOffsetY(target),
                ToFix64(target.Transform.Z));
            physicsRuntime.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, centerPosition));
            physicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId),
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                new FixVector3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z),
                halfExtents));
        }

private static void AssertSkillSlotStartsAbility(
            int slotIndex,
            int abilityId,
            string expectedActionId,
            long expectedCooldownFrames)
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime,
                skillSlots: CharacterTestSkillSlotRuntime.CreateDefault());

            Assert.IsTrue(abilityRuntime.Definitions.TryGet(abilityId, out AbilityDefinitionConfig ability));
            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.SkillSlot,
                RuntimeFrame.Zero,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(slotIndex));

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(expectedActionId, attacker.Action.ActiveActionId);
            Assert.AreEqual(
                expectedCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(attacker.Id.Value, ability, RuntimeFrame.Zero));
        }

private static long ToVfxRaw(float value)
        {
            return (long)Math.Round(value * VfxVector3.Scale);
        }

private static long ToFixRaw(float value)
        {
            return (long)Math.Round(value * Fix64.Scale);
        }

private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

private static CharacterTestInteractionEvent CreateTestInteraction(
            RuntimeFrame frame,
            int stableId,
            int sourceOrder)
        {
            return new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                stableId,
                sourceOrder,
                sourceEntityId: 1,
                targetEntityId: 0,
                new FixVector3(Fix64.FromInt(1), Fix64.Zero, Fix64.FromInt(2)),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                Fix64.FromRatio(5, 2),
                intensity: 8,
                traceId: 42,
                CharacterTestInteractionSourceDomains.CombatProjectileAreaPulse,
                CharacterTestInteractionFieldProfiles.FireballExplosionRadialImpulse(8));
        }

private static void SyncDynamicCombatCollidersForTest(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(physicsRuntime);
            ((ICharacterTestCharacterColliderSync)motion).SyncAllCharacterCombatColliders(world.Entities);
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(
                world.MovableNaturalObjects,
                physicsRuntime);
        }

private static CombatPhysicsLayerMask CreateTornadoDynamicLayerMaskForTest()
        {
            return CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer)
                .AddLayer(CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectLayer);
        }

private static CombatQueryHeader CreateTornadoQueryHeaderForTest(
            CharacterTestInteractionEvent interaction,
            CombatPhysicsLayerMask layerMask)
        {
            return new CombatQueryHeader(
                interaction.TraceId,
                CombatQueryKind.Capsule,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + interaction.SourceEntityId),
                interaction.TraceId,
                actionId: 0,
                interaction.SourceOrder,
                layerMask);
        }

private static void GetTornadoCapsuleForTest(
            CharacterTestInteractionEvent interaction,
            out FixVector3 pointA,
            out FixVector3 pointB)
        {
            CharacterTestInteractionPhysicalField field = interaction.PhysicalField;
            pointA = interaction.AnchorPosition
                + new FixVector3(Fix64.Zero, field.CapsuleBottomOffset, Fix64.Zero);
            pointB = interaction.AnchorPosition
                + new FixVector3(Fix64.Zero, field.CapsuleTopOffset, Fix64.Zero);
        }

private static int[] ResultBodyIds(IReadOnlyList<CombatQueryResult> results)
        {
            var bodyIds = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
                bodyIds[i] = results[i].TargetBodyId.Value;
            return bodyIds;
        }

private static CharacterTestInteractionEvent CreateTornadoAttractionInteraction(
            RuntimeFrame frame,
            FixVector3 position,
            FixVector3 direction)
        {
            return new CharacterTestInteractionEvent(
                frame,
                CharacterTestInteractionKind.PhysicalField,
                CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId,
                sourceOrder: 17,
                sourceEntityId: 1,
                targetEntityId: 0,
                position,
                direction,
                CharacterTestInteractionFieldProfiles.TornadoAttractionRadius,
                CharacterTestInteractionFieldProfiles.TornadoAttractionAccelerationMillimetersPerSecondSquared,
                traceId: 42,
                CharacterTestInteractionSourceDomains.CombatProjectileActiveField,
                CharacterTestInteractionFieldProfiles.TornadoAttractionCapsule());
        }

private static CharacterTestCharacterRuntimeWorld CreateRuntimeWorldWithLocalPlayer(
            out CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

private static void AssertOnHitDefenseBreakBuff(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            int expectedSourceId,
            long expectedEndFrame = -1L)
        {
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(
                CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                out GameplayComponentBuffEntry buff));
            Assert.AreEqual(expectedSourceId, buff.SourceId);
            Assert.Greater(buff.EndFrame, buff.StartFrame);
            if (expectedEndFrame >= 0L)
                Assert.AreEqual(expectedEndFrame, buff.EndFrame);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultDefense
                + CharacterTestGuardAuraIds.OnHitDefensePenalty,
                GameplayComponentAttributeQuery.GetCurrentValueOrDefault(
                    world.GameplayComponentWorld,
                    target.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Defense));
        }

private static void AssertStasisBuff(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            int expectedSourceId,
            long expectedEndFrame)
        {
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(
                CharacterTestStasisIds.StasisBuffId,
                out GameplayComponentBuffEntry buff));
            Assert.AreEqual(expectedSourceId, buff.SourceId);
            Assert.AreEqual(CharacterTestStasisIds.StasisBuffDurationFrames, buff.EndFrame - buff.StartFrame);
            Assert.AreEqual(expectedEndFrame, buff.EndFrame);
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(target.GameplayEntityId, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
        }

private static void AssertPoisonedBuff(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            int expectedSourceId,
            long expectedStartFrame,
            long expectedEndFrame)
        {
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(
                CharacterTestPoisonIds.PoisonedBuffId,
                out GameplayComponentBuffEntry buff));
            Assert.AreEqual(expectedSourceId, buff.SourceId);
            Assert.AreEqual(expectedStartFrame, buff.StartFrame);
            Assert.AreEqual(expectedEndFrame, buff.EndFrame);
            Assert.AreEqual(1, buff.StackCount);
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(target.GameplayEntityId, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(new GameplayStatusId(CharacterTestPoisonIds.PoisonedStatusId)));
        }

private static void AssertStunnedBuff(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target,
            int expectedSourceId)
        {
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore));
            Assert.IsTrue(buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs));
            Assert.IsTrue(buffs.TryGet(
                CharacterTestStunIds.StunBuffId,
                out GameplayComponentBuffEntry buff));
            Assert.AreEqual(expectedSourceId, buff.SourceId);
            Assert.AreEqual(CharacterTestStunIds.StunBuffDurationFrames, buff.EndFrame - buff.StartFrame);
            Assert.AreEqual(1, buff.StackCount);
            Assert.IsTrue(world.GameplayComponentWorld.TryGetStore(
                out GameplayComponentStore<GameplayStatusComponent> statusStore));
            Assert.IsTrue(statusStore.TryGet(target.GameplayEntityId, out GameplayStatusComponent statuses));
            Assert.IsTrue(statuses.Contains(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
        }

private static void AssertNoStasisBuff(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity target)
        {
            if (world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore)
                && buffStore.TryGet(target.GameplayEntityId, out GameplayComponentBuffSetComponent buffs))
            {
                Assert.IsFalse(buffs.TryGet(CharacterTestStasisIds.StasisBuffId, out _));
            }

            if (world.GameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayStatusComponent> statusStore)
                && statusStore.TryGet(target.GameplayEntityId, out GameplayStatusComponent statuses))
            {
                Assert.IsFalse(statuses.Contains(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            }
        }

private static CharacterTestCharacterRuntimeWorld CreateRuntimeWorldWithAttackerAndTarget(
            int targetTeamId,
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    targetTeamId,
                    0.55f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

private static bool TryGetTornadoAttractionRequest(
            CharacterTestCharacterEntity entity,
            out CharacterTestCharacterMotionRequest request)
        {
            IReadOnlyList<CharacterTestCharacterMotionRequest> requests = entity.MotionRequests;
            for (int i = 0; i < requests.Count; i++)
            {
                CharacterTestCharacterMotionRequest candidate = requests[i];
                if (candidate.Kind == CharacterTestCharacterMotionRequestKind.ExternalForce
                    && candidate.SourceActionId == CharacterTestCharacterActionConfigSource.TornadoActionId
                    && candidate.StableEventId == CharacterTestCharacterActionConfigSource.TornadoAttractionStableEventId)
                {
                    request = candidate;
                    return true;
                }
            }

            request = default;
            return false;
        }

private static void TickTornadoDamageTestFrame(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            int frameIndex)
        {
            RuntimeFrame frame = new RuntimeFrame(frameIndex);
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                runtime.TickEntity(entities[entityIndex]);

            motion.Tick(world, 1f / 60f, frame);
            combat.Tick(world, runtime, frame);
        }

private static CharacterTestCharacterRuntimeWorld CreateRuntimeWorldWithAttackerAndTwoTargets(
            int targetTeamId,
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity firstTarget,
            out CharacterTestCharacterEntity secondTarget)
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            firstTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    targetTeamId,
                    0.55f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            secondTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    targetTeamId,
                    0.55f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            firstTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            secondTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

private static CharacterTestExplosionRuntimeSliceResult RunFireballExplosionInteractionPhysicsRuntimeHostSlice()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var vfx = new VfxRuntimeModule(
                CharacterTestVfxDefinitionSource.CreateDefinitions(),
                new RuntimeCommandBuffer());
            var audioService = new RecordingAudioService();
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false,
                audioService: audioService,
                audioCueCatalog: CharacterTestAudioCueCatalog.CreateDefault(),
                interactionVfxRuntime: vfx);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();

                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)));
                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    0.55f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None)));
                host.Tick(0, 1d / 60d, 0d);

                CharacterTestCharacterEntity attacker = module.Entities[0];
                CharacterTestCharacterEntity target = module.Entities[1];
                AlignTargetWithDefaultProjectileSocketLine(target);
                Assert.IsTrue(module.ActionRuntime.TryStartIntent(
                    attacker,
                    CharacterTestCharacterActionConfigSource.FireballExplosionIntentId,
                    new RuntimeFrame(1)));

                int interactionFrame = -1;
                for (int frameIndex = 1; frameIndex < 60; frameIndex++)
                {
                    host.Tick(frameIndex, 1d / 60d, frameIndex / 60d);
                    if (module.InteractionPhysicsSummary.AppliedImpulseCount > 0)
                    {
                        interactionFrame = frameIndex;
                        break;
                    }
                }

                Assert.Greater(interactionFrame, 0, "Expected fireball explosion interaction physics to apply.");
                var participant = new CharacterTestNetworkSnapshotParticipant(module, null);
                long hashBeforeMotionTick = participant.ComputeStateHash(new RuntimeFrame(interactionFrame));
                int pendingImpulseCount = target.PendingMotionRequestCount;
                CharacterActionId pendingSourceActionId = target.MotionRequests.Count > 0
                    ? target.MotionRequests[0].SourceActionId
                    : CharacterActionId.None;
                int pendingStableEventId = target.MotionRequests.Count > 0
                    ? target.MotionRequests[0].StableEventId
                    : 0;
                int processedEventCount = module.InteractionPhysicsSummary.ProcessedEventCount;
                int appliedImpulseCount = module.InteractionPhysicsSummary.AppliedImpulseCount;
                FrameworkPerformanceCounterSnapshot performance =
                    module.CreateInteractionPerformanceCounterSnapshot();
                int performanceLastFrameEvents = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.LastFrameEventsCounterId).Value;
                int performancePhysicsQueries = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId).Value;
                int performancePhysicsQueryResults = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.PhysicsQueryResultsCounterId).Value;
                int performancePhysicsAppliedForces = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.PhysicsAppliedForcesCounterId).Value;
                int performanceVfxSpawned = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.VfxSpawnedCounterId).Value;
                int performanceAudioPlayed = (int)FindInteractionCounter(
                    performance,
                    CharacterTestInteractionPerformanceCounterSource.AudioPlayedCounterId).Value;
                long hashAfterCounterRead = participant.ComputeStateHash(new RuntimeFrame(interactionFrame));

                RuntimeFrame nextFrame = new RuntimeFrame(interactionFrame + 1);
                host.Tick(nextFrame.Value, 1d / 60d, nextFrame.Value / 60d);
                long hashAfterMotionTick = participant.ComputeStateHash(nextFrame);

                return new CharacterTestExplosionRuntimeSliceResult(
                    interactionFrame,
                    pendingImpulseCount,
                    pendingSourceActionId,
                    pendingStableEventId,
                    processedEventCount,
                    appliedImpulseCount,
                    hashBeforeMotionTick,
                    hashAfterCounterRead,
                    hashAfterMotionTick,
                    performanceLastFrameEvents,
                    performancePhysicsQueries,
                    performancePhysicsQueryResults,
                    performancePhysicsAppliedForces,
                    performanceVfxSpawned,
                    performanceAudioPlayed,
                    target.HorizontalVelocityXMillimetersPerSecond,
                    target.HorizontalVelocityYMillimetersPerSecond);
            }
        }

private static CharacterTestTornadoPerformanceRuntimeSliceResult RunTornadoInteractionPerformanceRuntimeHostSlice()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false,
                movableNaturalObjectSpawnSpecs: new[]
                {
                    CharacterTestMovableNaturalObjectSpawnSpec.FromMeters(
                        "movable.tornado.performance",
                        701,
                        "movable-natural-object",
                        massKg: 80f,
                        CombatSurfaceMaterial.Default,
                        new Vector3(0.25f, 0.25f, 0.25f),
                        new Vector3(-1.5f, 0.25f, 1.6f))
                });
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();

                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)));
                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    2.25f,
                    0f,
                    1.6f,
                    CharacterTestCharacterControlOwner.None)));
                host.Tick(0, 1d / 60d, 0d);

                CharacterTestCharacterEntity attacker = module.Entities[0];
                Assert.IsTrue(module.ActionRuntime.TryStartIntent(
                    attacker,
                    CharacterTestCharacterActionConfigSource.TornadoIntentId,
                    new RuntimeFrame(1)));

                for (int frameIndex = 1; frameIndex < 90; frameIndex++)
                {
                    host.Tick(frameIndex, 1d / 60d, frameIndex / 60d);
                    if (module.InteractionPhysicsSummary.ProcessedFieldCount <= 0
                        || module.InteractionPhysicsSummary.QueryCount <= 0
                        || module.InteractionPhysicsSummary.CharacterTargetCount <= 0
                        || module.InteractionPhysicsSummary.MovableTargetCount <= 0)
                    {
                        continue;
                    }

                    var frame = new RuntimeFrame(frameIndex);
                    var participant = new CharacterTestNetworkSnapshotParticipant(module, null);
                    long hashBeforeCounterRead = participant.ComputeStateHash(frame);
                    FrameworkPerformanceCounterSnapshot performance =
                        module.CreateInteractionPerformanceCounterSnapshot();
                    long hashAfterCounterRead = participant.ComputeStateHash(frame);
                    return new CharacterTestTornadoPerformanceRuntimeSliceResult(
                        frameIndex,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.LastFrameEventsCounterId).Value,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.PhysicsQueriesCounterId).Value,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.PhysicsQueryResultsCounterId).Value,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.PhysicsCharactersCounterId).Value,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.PhysicsMovablesCounterId).Value,
                        (int)FindInteractionCounter(
                            performance,
                            CharacterTestInteractionPerformanceCounterSource.PhysicsAppliedForcesCounterId).Value,
                        hashBeforeCounterRead,
                        hashAfterCounterRead);
                }
            }

            Assert.Fail("Expected tornado interaction performance counters to record field workload.");
            return default;
        }

private static HitResolveResult RunLightAttackUntilCombatResult(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int maxFrames,
            int startFrame = 0,
            Func<string> failureDetails = null)
        {
            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(startFrame)),
                "Expected light_attack intent to start. startFrame="
                + startFrame
                + " activeAction="
                + attacker.Action.ActiveActionId
                + " state="
                + attacker.Action.State
                + " localFrame="
                + attacker.Action.LocalFrame
                + " reject="
                + attacker.Action.LastRejectReason
                + " details="
                + (failureDetails == null ? string.Empty : failureDetails()));
            return RunUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames,
                startFrame,
                failureDetails);
        }

private static void StartFireballThroughActionSystem(
            CharacterTestCharacterActionSystem system,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity attacker,
            RuntimeFrame frame)
        {
            QueueGameplayIntent(attacker, CharacterTestGameplayIntentKind.AttackSecondary, frame);
            system.Tick(world, frame);
            attacker.SetInput(CharacterTestCharacterInputComponent.None);
        }

private static void QueueGameplayIntent(
            CharacterTestCharacterEntity entity,
            CharacterTestGameplayIntentKind kind,
            RuntimeFrame frame,
            int skillSlotPressedMask = 0,
            int abilitySlotPressed = 0,
            int abilityIdPressed = 0)
        {
            entity.GameplayIntentBuffer.AddOrRefresh(
                kind,
                entity.Id.Value,
                frame.Value,
                skillSlotPressedMask: skillSlotPressedMask,
                abilitySlotPressed: abilitySlotPressed,
                abilityIdPressed: abilityIdPressed);
        }

private static void AdvanceActionToLocalFrame(
            CharacterTestCharacterActionSystem system,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            ref long frame,
            int targetLocalFrame)
        {
            int guard = 0;
            while (entity.Action.LocalFrame < targetLocalFrame && guard < 256)
            {
                frame++;
                system.Tick(world, new RuntimeFrame(frame));
                guard++;
            }

            Assert.AreEqual(targetLocalFrame, entity.Action.LocalFrame);
        }

private static void AdvanceActionUntilInactive(
            CharacterTestCharacterActionSystem system,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            ref long frame,
            int maxFrames)
        {
            int elapsed = 0;
            while (!string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()) && elapsed < maxFrames)
            {
                frame++;
                system.Tick(world, new RuntimeFrame(frame));
                elapsed++;
            }

            Assert.Less(elapsed, maxFrames);
        }

private static List<HitResolveResult> RunLightAttackUntilCombatResults(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int expectedAcceptedDamageCount,
            int maxFrames)
        {
            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(0)));
            return RunUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount,
                maxFrames,
                startFrame: 0);
        }

private static CombatProjectileSnapshot RunUntilProjectileSpawn(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterCombatTraceSystem combat,
            int maxFrames,
            int startFrame = 0)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);
                combat.Tick(world, runtime, frame);
                CombatProjectileRuntimeState state = combat.CreateProjectileStateSnapshot();
                if (state != null && state.ActiveCount > 0)
                    return state.ActiveProjectiles[0];
            }

            Assert.Fail("Expected a projectile spawn within " + maxFrames + " frames.");
            return default;
        }

private static HitResolveResult RunUntilCombatResult(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int maxFrames,
            int startFrame,
            Func<string> failureDetails = null)
        {
            motion.Tick(world, 1f / 60f, new RuntimeFrame(startFrame));
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                if (attacker.LastCombatHitResults.Count > 0)
                    return attacker.LastCombatHitResults[0];
            }

            Assert.Fail(
                "Expected light_attack hit trace to produce a combat result. "
                + "actionState=" + attacker.Action.State
                + " activeTraces=" + combat.ActiveTraceCount
                + " attackTraceFrames=" + combat.AttackTraceDebugFrames.Count
                + " hitPointFrames=" + combat.HitPointDebugFrames.Count
                + " details=" + (failureDetails?.Invoke() ?? string.Empty));
            return default;
        }

private static List<HitResolveResult> RunLightAttackForFrames(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int maxFrames,
            int startFrame = 0,
            List<DamageTelemetryRow> observedTelemetry = null)
        {
            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(startFrame)));
            var observedResults = new List<HitResolveResult>();
            motion.Tick(world, 1f / 60f, new RuntimeFrame(startFrame));
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                if (attacker.LastCombatHitResults.Count > 0)
                    observedResults.AddRange(attacker.LastCombatHitResults);
                if (observedTelemetry != null && combat.DamageTelemetryRows.Count > 0)
                    observedTelemetry.AddRange(combat.DamageTelemetryRows);
            }

            return observedResults;
        }

        private static int RunProximalWeaponTraceScenario(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            List<DamageTelemetryRow> observedTelemetry = null)
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncCharacterCombatCollider(physicsRuntime, target);

            List<HitResolveResult> results = RunLightAttackForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                observedTelemetry: observedTelemetry);
            return results.Count(result => result.IsAcceptedDamage);
        }

private static List<HitResolveResult> RunDashStrikeForFrames(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int maxFrames,
            int startFrame = 0,
            List<DamageTelemetryRow> observedTelemetry = null)
        {
            // DashStrike is not a command binding in the converged codex_flow_test
            // ActionSet. Start it through the same AbilityRuntime -> action-slot path
            // used by CharacterTestCharacterActionSystem in production.
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var actionSystem = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime,
                skillSlots: CharacterTestSkillSlotRuntime.CreateDefault(),
                runtime: runtime);
            var startRuntimeFrame = new RuntimeFrame(startFrame);
            abilityRuntime.Tick(new RuntimeTickContext(
                startFrame,
                startFrame / 60d,
                startFrame / 60d,
                RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                startRuntimeFrame,
                abilitySlotPressed: 1,
                abilityIdPressed: CharacterTestAbilityIds.DashStrikeAbilityId);
            actionSystem.Tick(world, startRuntimeFrame);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DashStrikeActionId,
                attacker.Action.ActiveActionId,
                "DashStrike ability should start its bound action: " + attacker.Action.LastRejectReason);
            var observedResults = new List<HitResolveResult>();
            var rootMotion = new CharacterTestCharacterRootMotionSystem();
            motion.Tick(world, 1f / 60f, new RuntimeFrame(startFrame));
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
                for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                    runtime.TickEntity(entities[entityIndex]);

                rootMotion.Tick(world, runtime, frame);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                if (attacker.LastCombatHitResults.Count > 0)
                    observedResults.AddRange(attacker.LastCombatHitResults);
                if (observedTelemetry != null && combat.DamageTelemetryRows.Count > 0)
                    observedTelemetry.AddRange(combat.DamageTelemetryRows);
            }

            return observedResults;
        }

private static int RunDashStrikeSideTargetAcceptedHits(
            float attackerRadius,
            CharacterTestCharacterActionRuntime runtime = null,
            int targetXMillimeters = 900,
            int targetZMillimeters = 950,
            float attackerYawDegrees = 0f)
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            attacker.SetPhysics(CreateTestPhysicsProfile(radius: attackerRadius));
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, attackerYawDegrees));
            target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: targetXMillimeters,
                yMillimeters: 0,
                zMillimeters: targetZMillimeters));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            runtime = runtime ?? CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            List<HitResolveResult> results = RunDashStrikeForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);
            return results.Count(result => result.IsAcceptedDamage);
        }

private static CharacterTestCharacterActionRuntime LoadDashStrikeRuntimeWithHitVolume(JObject hitVolume)
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(
                    directory,
                    File.ReadAllText(GetGeneratedActionSetsPath()),
                    CreateGeneratedActionsJsonWithDashStrikeHitVolume(hitVolume));
                File.WriteAllText(
                    Path.Combine(
                        directory,
                        CharacterTestCharacterActionConfigSource.ReactionProfilesFileName),
                    File.ReadAllText(GetGeneratedReactionProfilesPath()));
                return CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

private static string CreateGeneratedActionsJsonWithDashStrikeHitVolume(JObject hitVolume)
        {
            JObject root = JObject.Parse(File.ReadAllText(GetGeneratedActionsPath()));
            JObject action = FindActionJson(root, (CharacterTestCharacterActionConfigSource.DashStrikeActionId).ToString());
            JArray events = (JArray)action["combatTrack"]?["events"];
            Assert.IsNotNull(events, "Dash Strike combatTrack.events was not found in generated action JSON.");
            for (int i = 0; i < events.Count; i++)
            {
                if (!(events[i] is JObject evt))
                    continue;

                if (!string.Equals(
                        evt["kind"]?.Value<string>(),
                        CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                evt["hitVolume"] = hitVolume == null
                    ? new JObject { ["sourceKind"] = "CasterBody" }
                    : (JObject)hitVolume.DeepClone();
                return root.ToString(Formatting.Indented);
            }

            Assert.Fail("Dash Strike HitTraceWindow event was not found in generated action JSON.");
            return root.ToString(Formatting.Indented);
        }

private static JObject FindActionJson(JObject root, string stableId)
        {
            JArray actions = (JArray)root?["actions"];
            Assert.IsNotNull(actions, "Generated action JSON does not contain an actions array.");
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is JObject action
                    && string.Equals(action["stableId"]?.Value<string>(), stableId, StringComparison.Ordinal))
                {
                    return action;
                }
            }

            Assert.Fail("Generated action JSON does not contain action " + stableId + ".");
            return new JObject();
        }

private static string GetGeneratedActionSetsPath()
        {
            return Path.Combine(
                "Assets",
                "MxFrameworkGenerated",
                "CharacterActions",
                "ActionSets",
                "codex_flow_test",
                "character_action_sets.json");
        }

private static string GetGeneratedActionsPath()
        {
            return Path.Combine(
                "Assets",
                "MxFrameworkGenerated",
                "CharacterActions",
                "Libraries",
                "Common",
                "character_actions.json");
        }

private static string GetGeneratedReactionProfilesPath()
        {
            return Path.Combine(
                "Assets",
                "MxFrameworkGenerated",
                "CharacterActions",
                "ActionSets",
                "codex_flow_test",
                "character_reaction_profiles.json");
        }

private static List<HitResolveResult> RunUntilCombatResults(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int expectedAcceptedDamageCount,
            int maxFrames,
            int startFrame)
        {
            motion.Tick(world, 1f / 60f, new RuntimeFrame(startFrame));
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
                int acceptedDamageCount = attacker.LastCombatHitResults.Count(result => result.IsAcceptedDamage);
                if (acceptedDamageCount >= expectedAcceptedDamageCount)
                    return new List<HitResolveResult>(attacker.LastCombatHitResults);
            }

            Assert.Fail("Expected light_attack hit trace to produce "
                + expectedAcceptedDamageCount
                + " accepted combat result(s).");
            return new List<HitResolveResult>();
        }

private static void TickCombatUntilActionInactive(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterMotionSystem motion,
            CharacterTestCharacterCombatTraceSystem combat,
            CharacterTestCharacterEntity attacker,
            int startFrame,
            int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (!(attacker.Action.State == CharacterTestActionStateId.Running))
                    return;

                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
            }

            Assert.Fail("Expected action to become inactive before the next light_attack starts.");
        }

private static void SetCurrentAttribute(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            int attributeId,
            int value)
        {
            GameplayComponentStore<GameplayAttributeSetComponent> store =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayAttributeSetComponent>();
            Assert.IsTrue(store.TryGet(entity.GameplayEntityId, out GameplayAttributeSetComponent attributes));
            store.Set(entity.GameplayEntityId, attributes.SetCurrentValue(attributeId, value));
            world.SyncCombatStateHealth(entity);
        }

private static CharacterTestCharacterCombatTraceSystem CreateCombatTraceSystem()
        {
            return new CharacterTestCharacterCombatTraceSystem(NullRuntimeLogger.Instance);
        }

private static CharacterTestCharacterDefinition GetIronVanguardDefinition()
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            return definition;
        }

private static void TickUntilInactive(
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterEntity entity,
            int maxTicks)
        {
            for (int i = 0; i < maxTicks && (entity.Action.State == CharacterTestActionStateId.Running); i++)
                runtime.TickEntity(entity);
        }

private static string FirstImportDiagnostic(CharacterTestCharacterActionRuntime runtime)
        {
            return runtime.ImportResult.Diagnostics.Length == 0
                ? string.Empty
                : runtime.ImportResult.Diagnostics[0].ToString();
        }

private static string FirstValidationDiagnostic(CharacterTestCharacterActionRuntime runtime)
        {
            return runtime.ValidationReport.Diagnostics.Length == 0
                ? string.Empty
                : runtime.ValidationReport.Diagnostics[0].ToString();
        }

private static void AssertHasSourceDiagnostic(
            CharacterTestCharacterActionRuntime runtime,
            string code)
        {
            Assert.That(
                runtime.ConfigSourceDiagnostics,
                Has.Some.Matches<CharacterTestCharacterActionConfigSourceDiagnostic>(
                    diagnostic => diagnostic.Code == code),
                "Missing source diagnostic: " + code);
        }

private static void AssertHasImportDiagnostic(
            CharacterTestCharacterActionRuntime runtime,
            string code,
            string sourcePath)
        {
            Assert.That(
                runtime.ImportResult.Diagnostics,
                Has.Some.Matches<CharacterActionJsonDiagnostic>(
                    diagnostic => diagnostic.Code == code
                        && diagnostic.SourcePath == sourcePath),
                "Missing import diagnostic: " + code + " source=" + sourcePath);
        }

private static void AssertHasValidationDiagnostic(
            CharacterTestCharacterActionRuntime runtime,
            string code,
            string sourcePath,
            string stablePath)
        {
            Assert.That(
                runtime.ValidationReport.Diagnostics,
                Has.Some.Matches<CharacterActionValidationDiagnostic>(
                    diagnostic => diagnostic.Code == code
                        && diagnostic.SourcePath == sourcePath
                        && diagnostic.StablePath == stablePath),
                "Missing validation diagnostic: " + code + " source=" + sourcePath + " path=" + stablePath);
        }

private static void AssertJumpDuration(
            CharacterTestCharacterActionRuntime runtime,
            int expectedDurationFrames)
        {
            Assert.NotNull(runtime);
            Assert.IsTrue(runtime.IsReady, FirstImportDiagnostic(runtime) + FirstValidationDiagnostic(runtime));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.JumpActionId,
                out CharacterActionConfig jump));
            Assert.AreEqual(expectedDurationFrames, jump.DurationFrames);
        }

private static GameSlice CreateSliceWithActionDirectory(string directory)
        {
            return new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                actionConfigOptions: new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
        }

private static CharacterTestCharacterEntity SpawnLocalPlayer(GameSlice slice)
        {
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult spawn = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    frame,
                    CharacterTestRuntimeCommandSources.System,
                    CharacterTestCharacterSpawnSpec.TemporaryPlayer,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0018)));
            Assert.IsTrue(spawn.Success, spawn.Error.Message);
            slice.Tick(0d);
            return slice.CharacterEntities.First(
                entity => entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer);
        }

private static string CreateTempActionConfigDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTest.ActionJson." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

private static void DeleteDirectory(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

private static void WriteActionJsonFiles(string directory, int durationFrames)
        {
            WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(durationFrames));
        }

private static void WriteActionJsonFiles(
            string directory,
            string actionSetsJson,
            string actionsJson)
        {
            File.WriteAllText(
                Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionSetsFileName),
                actionSetsJson);
            File.WriteAllText(
                Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionsFileName),
                actionsJson);
            File.WriteAllText(
                Path.Combine(directory, CharacterTestCharacterActionConfigSource.ReactionProfilesFileName),
                CreateReactionProfilesJson());
        }

private static string CreateReactionProfilesJson()
        {
            return @"{
  ""schemaVersion"": ""0.4.0"",
  ""reactionProfiles"": [
    {
      ""stableId"": ""reaction.default"",
      ""defaultActionId"": 330,
      ""rules"": [
        { ""actionId"": 330, ""trigger"": ""CohesionBreak"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 100 },
        { ""actionId"": 339, ""trigger"": ""Hit"", ""currentScatterPressureBand"": ""Broken"", ""reactionGroupId"": ""reaction.special.launch"", ""priority"": 115 },
        { ""actionId"": 330, ""trigger"": ""Hit"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 80 }
      ]
    },
    {
      ""stableId"": ""reaction.character_test.katana.default"",
      ""defaultActionId"": 330,
      ""rules"": [
        { ""actionId"": 330, ""trigger"": ""CohesionBreak"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 100 },
        { ""actionId"": 339, ""trigger"": ""Hit"", ""currentScatterPressureBand"": ""Broken"", ""reactionGroupId"": ""reaction.special.launch"", ""priority"": 115 },
        { ""actionId"": 330, ""trigger"": ""Hit"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 80 }
      ]
    }
  ]
}";
        }

private static void WriteMovementProfileJson(
            string directory,
            float runSpeed,
            float gravity,
            float acceleration)
        {
            File.WriteAllText(
                Path.Combine(directory, CharacterTestCharacterMovementProfileSource.ProfilesFileName),
                @"{
  ""schemaVersion"": ""0.2.0"",
  ""profiles"": [
    {
      ""stableId"": ""movement.default"",
      ""defaultMode"": ""Run"",
      ""walkSpeed"": 2.0,
      ""runSpeed"": " + runSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture) + @",
      ""acceleration"": " + acceleration.ToString(System.Globalization.CultureInfo.InvariantCulture) + @",
      ""deceleration"": 20.0,
      ""turnSpeed"": 540.0,
      ""groundFriction"": 8.0,
      ""airControl"": 0.35,
      ""gravity"": " + gravity.ToString(System.Globalization.CultureInfo.InvariantCulture) + @",
      ""jumpImpulse"": 8.0,
      ""slopeLimitDegrees"": 45.0,
      ""locomotionBlendId"": ""locomotion.move"",
      ""maxFallSpeed"": -50.0,
      ""jumpBufferSeconds"": 0.12,
      ""coyoteTimeSeconds"": 0.10,
      ""jumpReleaseVelocityMultiplier"": 0.45,
      ""apexGravityMultiplier"": 0.75,
      ""fallGravityMultiplier"": 1.35
    }
  ]
}");
        }

private static CharacterTestCharacterMovementProfileRuntime CreateMovementRuntime(
            float runSpeed = 4f,
            float gravity = -30f,
            float acceleration = 100f,
            float deceleration = 100f,
            float groundFriction = 0f,
            float airControl = 0.35f,
            float turnSpeed = 720f,
            float jumpReleaseVelocityMultiplier = 0.45f,
            float slopeLimitDegrees = 45f)
        {
            return new CharacterTestCharacterMovementProfileRuntime(
                new CharacterMovementProfileConfig(
                    CharacterTestCharacterMovementProfileSource.DefaultProfileId,
                    CharacterMovementMode.Run,
                    walkSpeed: 2f,
                    runSpeed: runSpeed,
                    acceleration: acceleration,
                    deceleration: deceleration,
                    turnSpeed: turnSpeed,
                    groundFriction: groundFriction,
                    airControl: airControl,
                    gravity: gravity,
                    jumpImpulse: 8f,
                    slopeLimitDegrees: slopeLimitDegrees,
                    locomotionBlendId: "locomotion.move"),
                maxFallSpeed: -50f,
                jumpBufferSeconds: 0.12f,
                coyoteTimeSeconds: 0.1f,
                jumpReleaseVelocityMultiplier: jumpReleaseVelocityMultiplier,
                apexGravityMultiplier: 0.75f,
                fallGravityMultiplier: 1.35f,
                usedFallback: false,
                sourceDescription: "test");
        }

private static CharacterTestCharacterPhysicsComponent CreateTestPhysicsProfile(
            float radius = 0.34f,
            float height = 1.86f,
            float massKg = 80f)
        {
            return new CharacterTestCharacterPhysicsComponent(
                radius,
                height,
                height * 0.5f,
                massKg);
        }

private static float RunContinuousPushDistance(
            float sourceMassKg,
            float targetMassKg)
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.9f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            player.SetPhysics(CreateTestPhysicsProfile(massKg: sourceMassKg));
            target.SetPhysics(CreateTestPhysicsProfile(massKg: targetMassKg));
            player.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            player.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            float targetStartX = target.Transform.X;
            for (int i = 0; i < 120; i++)
                motion.Tick(world, 1f / 60f);

            return target.Transform.X - targetStartX;
        }

private static float HorizontalDistance(
            CharacterTestCharacterEntity a,
            CharacterTestCharacterEntity b)
        {
            float dx = a.Transform.X - b.Transform.X;
            float dz = a.Transform.Z - b.Transform.Z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

private static CombatMotionStepResult CreateDynamicCharacterCollisionStep(
            CombatEntityId targetEntityId,
            CombatBodyId targetBodyId)
        {
            var state = new CombatMotionState(
                CombatFrame.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                grounded: true,
                lastCollisionNormal: FixVector3.Zero,
                CombatMotionCollisionFlags.Grounded);
            var collisions = new[]
            {
                new CombatMotionCollision(
                    targetEntityId,
                    targetBodyId,
                    new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId),
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                    Fix64.Zero,
                    Fix64.Zero,
                    CombatMotionCollisionFlags.Wall)
            };

            return new CombatMotionStepResult(
                state,
                FixVector3.Zero,
                FixVector3.Zero,
                jumpStarted: false,
                CombatMotionCollisionFlags.Wall,
                collisions);
        }

private static void RunPressureContactSample(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity source)
        {
            resolver.Clear();
            resolver.RecordAttemptedContactVelocity(
                source.Id.Value,
                velocityX: 1000,
                velocityY: 0);
            resolver.ResolveCharacterContactPressure(world.Entities);
            resolver.ApplyCharacterContactState(world.Entities);
        }

private static void RunZeroPressureContactSample(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity source)
        {
            resolver.Clear();
            resolver.RecordAttemptedContactVelocity(
                source.Id.Value,
                velocityX: 0,
                velocityY: 0);
            resolver.ResolveCharacterContactPressure(world.Entities);
            resolver.ApplyCharacterContactState(world.Entities);
        }

private static void RunOverlapPressureContactSample(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 0,
                yMillimeters: 0,
                zMillimeters: 0));
            target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 250,
                yMillimeters: 0,
                zMillimeters: 0));

            resolver.Clear();
            resolver.RecordAttemptedContactVelocity(
                source.Id.Value,
                velocityX: 1000,
                velocityY: 0);
            resolver.ResolveCharacterOverlaps(world.Entities);
            resolver.ResolveCharacterContactPressure(world.Entities);
            resolver.ApplyCharacterContactState(world.Entities);
        }

private static void RunOverlapContactSample(
            CharacterTestCharacterContactResolver resolver,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 0,
                yMillimeters: 0,
                zMillimeters: 0));
            target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 250,
                yMillimeters: 0,
                zMillimeters: 0));

            resolver.Clear();
            resolver.ResolveCharacterOverlaps(world.Entities);
            resolver.ApplyCharacterContactState(world.Entities);
        }

private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

private static CharacterTestCharacterPhysicsRuntime CreateFrameworkPhysicsRuntime(
            bool includeWall,
            bool includeLowStep = false,
            Fix64 maxStepHeight = default(Fix64),
            Fix64 groundSurfaceFriction = default(Fix64))
        {
            var physicsWorld = new CombatPhysicsWorld();
            int colliderCount = 1;
            RegisterStaticAabb(
                physicsWorld,
                entity: 100,
                body: 100,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                surfaceFriction: groundSurfaceFriction);
            if (includeWall)
            {
                RegisterStaticAabb(
                    physicsWorld,
                    entity: 101,
                    body: 101,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                    localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(3), Fix64.FromInt(2)));
                colliderCount++;
            }

            if (includeLowStep)
            {
                RegisterStaticAabb(
                    physicsWorld,
                    entity: 102,
                    body: 102,
                    collider: 1,
                    layer: 1,
                    position: FixVector3.Zero,
                    localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                    localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                    surfaceFriction: groundSurfaceFriction);
                colliderCount++;
            }

            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                maxStepHeight,
                colliderCount,
                "test");
        }

private static CharacterTestCharacterPhysicsRuntime CreateBlockedNavigationPhysicsRuntime()
        {
            var physicsWorld = new CombatPhysicsWorld();
            RegisterStaticAabb(
                physicsWorld,
                entity: 100,
                body: 100,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            RegisterStaticAabb(
                physicsWorld,
                entity: 110,
                body: 110,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.Half, Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.Half));
            RegisterStaticAabb(
                physicsWorld,
                entity: 111,
                body: 111,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Half),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(2)));
            RegisterStaticAabb(
                physicsWorld,
                entity: 112,
                body: 112,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(Fix64.Half, Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), -Fix64.Half));
            RegisterStaticAabb(
                physicsWorld,
                entity: 113,
                body: 113,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.Half, Fix64.Zero, Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.FromInt(3), Fix64.FromInt(2)));
            RegisterStaticAabb(
                physicsWorld,
                entity: 114,
                body: 114,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.Half, Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.Half, Fix64.FromInt(3), -Fix64.Half));

            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                6,
                "blocked navigation test");
        }

private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax,
            Fix64 surfaceFriction = default(Fix64))
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localMin,
                localMax,
                surfaceFriction));
        }

private static InputSnapshot CreateInputSnapshot(
            Vector2 move,
            bool jumpPressed = false,
            bool jumpHeld = false,
            bool jumpReleased = false,
            bool attackPrimaryPressed = false)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed,
                jumpHeld,
                jumpReleased,
                attackPrimaryPressed,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

private static CharacterActionCombatRequest CreateHitTraceWindowRequest(
            CharacterActionTrackEventKind eventKind,
            RuntimeFrame frame,
            int sourceEntityId)
        {
            return new CharacterActionCombatRequest(
                new CharacterActionAdapterDispatchMetadata(
                    frame,
                    instanceId: 100L,
                    planId: 200L,
                    actionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.light_attack"),
                    localFrame: (int)frame.Value,
                    stableEventId: 1100100 + (int)eventKind,
                    sourceId: sourceEntityId,
                    traceId: new RuntimeTraceIdentity(929510)),
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + sourceEntityId),
                eventKind,
                combatActionId: 0,
                traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0080,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1);
        }

private static object CreateHitTraceWindowState()
        {
            Type type = typeof(CharacterTestCharacterCombatTraceSystem).Assembly.GetType(
                "MxFramework.Game.CharacterTest.CharacterTestHitTraceWindowState",
                throwOnError: true);
            return Activator.CreateInstance(type);
        }

private static void InvokeHitTraceWindowState(
            object state,
            string methodName,
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            MethodInfo method = state.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method, "Missing hit trace window state method: " + methodName);
            method.Invoke(state, new object[] { world, frame, null });
        }

private static int GetHitTraceWindowStateActiveTraceCount(object state)
        {
            PropertyInfo property = state.GetType().GetProperty(
                "ActiveTraceCount",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, "Missing hit trace window state ActiveTraceCount.");
            return (int)property.GetValue(state);
        }

private static void SetSliceInputSnapshot(GameSlice slice, InputSnapshot snapshot)
        {
            slice.SetLocalInputProvider(new FixedInputProvider(snapshot));
        }

private static void EnqueueCameraModeCommands(
            CharacterTestRuntimeCommandModule commands,
            RuntimeFrame frame)
        {
            for (int i = 0; i < 4; i++)
            {
                RuntimeCommandValidationResult result = commands.CommandBuffer.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetCameraMode(
                        frame,
                        CharacterTestRuntimeCommandSources.System + i,
                        CharacterTestCameraMode.ThirdPersonControlled));
                Assert.IsTrue(result.Success, result.Error.Message);
            }
        }

private static string CreateActionSetsJson()
        {
            return @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    {
      ""id"": 1,
      ""stableId"": ""codex_flow_test.sword_empty"",
      ""displayName"": ""External CharacterTest Default"",
      ""characterStableId"": """",
      ""equipmentStateStableId"": """",
      ""movementProfileId"": ""movement.default"",
      ""reactionProfileId"": ""reaction.default"",
      ""defaultActionId"": 0,
      ""commandBindings"": [
        { ""intentId"": ""Jump"", ""actionId"": 300, ""priority"": 5, ""allowQueue"": false, ""queueWindowFrames"": 0 },
        { ""intentId"": ""LightAttack"", ""actionId"": 301, ""priority"": 10, ""allowQueue"": false, ""queueWindowFrames"": 0 }
      ],
      ""abilityBindings"": [],
      ""reactionBindings"": []
    }
  ]
}";
        }

private static string CreateContextIsolatedActionSetsJson()
        {
            return @"{
  ""schemaVersion"": ""0.3.0"",
  ""actionSets"": [
    {
      ""id"": 11,
      ""stableId"": ""alpha.sword"",
      ""displayName"": ""Alpha Sword"",
      ""characterStableId"": ""char.alpha"",
      ""equipmentStateStableId"": ""equip.alpha.sword"",
      ""movementProfileId"": ""movement.default"",
      ""reactionProfileId"": ""reaction.default"",
      ""defaultActionId"": 0,
      ""commandBindings"": [
        { ""intentId"": ""LightAttack"", ""actionId"": 300, ""priority"": 10, ""allowQueue"": false, ""queueWindowFrames"": 0 }
      ],
      ""commandSequences"": [],
      ""abilityBindings"": [],
      ""reactionBindings"": []
    },
    {
      ""id"": 12,
      ""stableId"": ""beta.sword"",
      ""displayName"": ""Beta Sword"",
      ""characterStableId"": ""char.beta"",
      ""equipmentStateStableId"": ""equip.beta.sword"",
      ""movementProfileId"": ""movement.default"",
      ""reactionProfileId"": ""reaction.default"",
      ""defaultActionId"": 0,
      ""commandBindings"": [
        { ""intentId"": ""LightAttack"", ""actionId"": 301, ""priority"": 10, ""allowQueue"": false, ""queueWindowFrames"": 0 }
      ],
      ""commandSequences"": [],
      ""abilityBindings"": [],
      ""reactionBindings"": []
    }
  ]
}";
        }

private static void SetActionContext(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            string characterStableId,
            string equipmentStateStableId)
        {
            CharacterTestCharacterIdentityComponent identity = entity.Identity;
            Assert.IsTrue(world.SetComponent(
                entity.Id,
                new CharacterTestCharacterIdentityComponent(
                    identity.CharacterId,
                    identity.DisplayName,
                    identity.PrefabKey,
                    identity.AnimationBinding,
                    identity.GeneratedPackageRoot,
                    characterStableId)));
            entity.SetEquipment(new CharacterTestCharacterEquipmentComponent(
                useDefaultLoadout: true,
                equipmentStateStableId));
        }

private static string CreateActionsJson(
            int jumpDurationFrames,
            bool lockLightAttackMovement = false,
            string lightAttackMotionTrackOverride = null,
            string lightAttackGameplayTrackOverride = null,
            string lightAttackCohesionSupportOverride = null)
        {
            int recoveryEnd = Math.Max(1, jumpDurationFrames - 1);
            string lightAttackMotionTrack = lightAttackMotionTrackOverride ?? (lockLightAttackMovement
                ? @"{
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 9, ""kind"": ""LockMovement"", ""movementMode"": ""ControlLocked"", ""stableEventId"": ""light_attack.lock_movement"" }
        ]
      }"
                : "null");
            string lightAttackGameplayTrack = lightAttackGameplayTrackOverride ?? "null";
            string lightAttackCohesionSupport = string.IsNullOrWhiteSpace(lightAttackCohesionSupportOverride)
                ? string.Empty
                : @",
      ""cohesionSupport"": " + lightAttackCohesionSupportOverride;
            return @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 300,
      ""displayName"": ""External Basic Jump"",
      ""category"": ""Jump"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [""movement"", ""jump""],
      ""priority"": 10,
      ""durationFrames"": " + jumpDurationFrames + @",
      ""requirements"": [],
      ""phases"": [
        { ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 1 },
        { ""kind"": ""Recovery"", ""startFrame"": 2, ""endFrame"": " + recoveryEnd + @" }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyImpulse"", ""movementMode"": ""Airborne"", ""x"": 0.0, ""y"": 8.0, ""z"": 0.0, ""stableEventId"": ""basic_jump.impulse"" }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          { ""frame"": 0, ""kind"": ""CrossFadeAnimation"", ""animationActionKey"": ""anim.basic_jump"", ""transitionSeconds"": 0.08, ""stableEventId"": ""basic_jump.animation"" }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 301,
      ""displayName"": ""External Light Attack"",
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [""combat"", ""light_attack""],
      ""priority"": 20,
      ""durationFrames"": 10" + lightAttackCohesionSupport + @",
      ""requirements"": [],
      ""phases"": [
        { ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 2 },
        { ""kind"": ""Active"", ""startFrame"": 3, ""endFrame"": 5 },
        { ""kind"": ""Recovery"", ""startFrame"": 6, ""endFrame"": 9 }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""motionTrack"": " + lightAttackMotionTrack + @",
      ""animationTrack"": {
        ""events"": [
          { ""frame"": 0, ""kind"": ""CrossFadeAnimation"", ""animationActionKey"": ""combat.attack.light"", ""transitionSeconds"": 0.12, ""stableEventId"": ""light_attack.animation"" }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": " + lightAttackGameplayTrack + @",
      ""presentationTrack"": {
        ""events"": [
          { ""frame"": 0, ""kind"": ""PlayAudioCue"", ""cueId"": ""sfx.light_attack"", ""stableEventId"": ""sfx.light_attack"" }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 330,
      ""displayName"": ""Katana Cohesion Break React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [""reaction"", ""cohesion_break"", ""katana"", ""hit_stun""],
      ""priority"": 100,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        { ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 17 }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 17, ""kind"": ""SetControlScale"", ""movementMode"": ""ControlLocked"", ""movementControlScale"": 0.0, ""rotationControlScale"": 0.0, ""stableEventId"": ""katana_cohesion_break_react.control_window"" }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          { ""frame"": 0, ""endFrame"": 17, ""playbackSpeed"": 1.0, ""kind"": ""CrossFadeAnimation"", ""animationActionKey"": ""anim.reaction.cohesion_break"", ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_stun01_start"", ""animationStartOffsetFrames"": 0, ""animationEndOffsetFrames"": 0, ""transitionSeconds"": 0.04, ""stableEventId"": ""katana_cohesion_break_react.animation"" }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 339,
      ""displayName"": ""Katana Launch React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [""reaction"", ""hit"", ""launch"", ""katana"", ""special_hit""],
      ""priority"": 115,
      ""durationFrames"": 24,
      ""requirements"": [],
      ""phases"": [
        { ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 23 }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 23, ""kind"": ""SetControlScale"", ""movementMode"": ""ControlLocked"", ""movementControlScale"": 0.0, ""rotationControlScale"": 0.0, ""stableEventId"": ""katana_launch_react.control_window"" }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          { ""frame"": 0, ""endFrame"": 17, ""playbackSpeed"": 1.0, ""kind"": ""CrossFadeAnimation"", ""animationActionKey"": ""anim.reaction.launch"", ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_strong_l_chest"", ""animationStartOffsetFrames"": 0, ""animationEndOffsetFrames"": 0, ""transitionSeconds"": 0.04, ""stableEventId"": ""katana_launch_react.animation"" }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null
    }
  ]
}";
        }

private static string CreateActionsJsonWithLightAttackCancelRule(
            int jumpDurationFrames,
            int startFrame,
            int endFrame)
        {
            JObject root = JObject.Parse(CreateActionsJson(jumpDurationFrames));
            JObject action = FindActionJson(root, (CharacterTestCharacterActionConfigSource.LightAttackActionId).ToString());
            action["cancelRules"] = new JArray
            {
                new JObject
                {
                    ["startFrame"] = startFrame,
                    ["endFrame"] = endFrame,
                    ["targetActionId"] = 300,
                    ["sourceKind"] = CharacterActionSourceKind.LocalInput.ToString(),
                    ["allow"] = true
                }
            };
            return root.ToString(Formatting.Indented);
        }

private static GameSlice CreateSliceWithLockedTarget(
            out CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null)
        {
            var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty,
                physicsRuntime: physicsRuntime);

            RuntimeFrame frame = slice.CurrentFrame;
            slice.EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.SpawnCharacter(
                frame,
                CharacterTestRuntimeCommandSources.System,
                CharacterTestCharacterSpawnSpec.TemporaryPlayer,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0019)));
            slice.EnqueueRuntimeCommand(CharacterTestRuntimeCommandFactory.SpawnCharacter(
                frame,
                CharacterTestRuntimeCommandSources.System,
                CharacterTestCharacterSpawnSpec.TemporaryAiOpponent,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0020)));
            slice.Tick(0d);

            CharacterTestCharacterEntity localPrimaryTarget = slice.CharacterEntities.First(
                entity => entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer);
            primaryTarget = localPrimaryTarget;
            CharacterTestCharacterEntity lockedTarget = slice.CharacterEntities.First(
                entity => entity != localPrimaryTarget);

            RuntimeCommandValidationResult lockResult = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetTargetLock(
                    slice.CurrentFrame,
                    CharacterTestRuntimeCommandSources.System,
                    enabled: true,
                    targetEntityId: lockedTarget.Id.Value,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0021)));
            Assert.IsTrue(lockResult.Success, lockResult.Error.Message);
            slice.Tick(0d);
            Assert.IsTrue(slice.TargetLockEnabled);
            Assert.AreEqual(lockedTarget.Id.Value, slice.LockedTargetEntityId);
            return slice;
        }

private static void AssertCameraLookSwitchesToViewportTarget(Vector2 look, string expectedTargetKey)
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            CharacterTestCharacterEntity lockedTarget = ResolveLockedTargetForTest(slice, primaryTarget);
            CharacterTestCharacterEntity rightTarget = SpawnCameraSwitchOpponent(slice, 2f, 1f, 4f);
            CharacterTestCharacterEntity leftTarget = SpawnCameraSwitchOpponent(slice, -2f, 1f, 4f);
            CharacterTestCharacterEntity upTarget = SpawnCameraSwitchOpponent(slice, 0f, 3f, 4f);
            CharacterTestCharacterEntity downTarget = SpawnCameraSwitchOpponent(slice, 0f, -1f, 4f);
            primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
            lockedTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 4f));

            CharacterTestCharacterEntity expectedTarget;
            switch (expectedTargetKey)
            {
                case "right":
                    expectedTarget = rightTarget;
                    break;
                case "left":
                    expectedTarget = leftTarget;
                    break;
                case "up":
                    expectedTarget = upTarget;
                    break;
                case "down":
                    expectedTarget = downTarget;
                    break;
                default:
                    Assert.Fail("Unknown expected target key: " + expectedTargetKey);
                    return;
            }

            var cameraHost = new GameObject("CharacterTest Camera Target Lock Direction Camera");
            var bridgeHost = new GameObject("CharacterTest Camera Target Lock Direction Bridge");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                UnityEngine.Camera camera = CreateTargetLockSwitchTestCamera(cameraHost);
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, camera, cursorController);
                ConfigureTargetLockSwitchTestBridge(bridge);
                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(look * 2f)));

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);
                Assert.AreEqual(1, slice.PendingRuntimeCommandCount);

                slice.Tick(0d);

                Assert.IsTrue(slice.TargetLockEnabled);
                Assert.AreEqual(expectedTarget.Id.Value, slice.LockedTargetEntityId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
                cursorController.Dispose();
            }
        }

private static CharacterTestCharacterEntity ResolveLockedTargetForTest(
            GameSlice slice,
            CharacterTestCharacterEntity primaryTarget)
        {
            return slice.CharacterEntities.First(
                entity => entity.Id.Value == slice.LockedTargetEntityId
                    && entity.Id.Value != primaryTarget.Id.Value);
        }

private static CharacterTestCharacterEntity SpawnCameraSwitchOpponent(
            GameSlice slice,
            float x,
            float y,
            float z)
        {
            int beforeCount = slice.CharacterEntities.Count;
            RuntimeFrame frame = slice.CurrentFrame;
            RuntimeCommandValidationResult spawn = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    frame,
                    CharacterTestRuntimeCommandSources.System,
                    new CharacterTestCharacterSpawnSpec(
                        CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                        teamId: 2,
                        x,
                        y,
                        z,
                        CharacterTestCharacterControlOwner.None),
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0022)));
            Assert.IsTrue(spawn.Success, spawn.Error.Message);
            slice.Tick(0d);
            Assert.Greater(slice.CharacterEntities.Count, beforeCount);
            CharacterTestCharacterEntity entity = slice.CharacterEntities[slice.CharacterEntities.Count - 1];
            entity.SetTransform(new CharacterTestCharacterTransformComponent(x, y, z));
            return entity;
        }

private static UnityEngine.Camera CreateTargetLockSwitchTestCamera(GameObject host)
        {
            UnityEngine.Camera camera = host.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            host.transform.position = new Vector3(0f, 2f, -10f);
            host.transform.rotation = Quaternion.identity;
            return camera;
        }

private static void ConfigureTargetLockSwitchTestBridge(CharacterTestThirdPersonCameraRigBridge bridge)
        {
            SetPrivateField(bridge, "_mouseYawSensitivity", 1f);
            SetPrivateField(bridge, "_mousePitchSensitivity", 1f);
            SetPrivateField(bridge, "_targetLockViewSwitchThresholdDegrees", 1f);
            SetPrivateField(bridge, "_targetLockViewSwitchCooldownSeconds", 0.18f);
        }

private static InputSnapshot CreateCameraLookSnapshot(
            Vector2 look,
            bool middleClickPressed = false,
            bool cameraModeNextPressed = false)
        {
            return new InputSnapshot(
                Vector2.zero,
                look,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false,
                middleClickPressed: middleClickPressed,
                cameraModeNextPressed: cameraModeNextPressed);
        }

private static CharacterTestRuntimeContext CreateCharacterTestRuntimeContext(
            GameObject host,
            CharacterTestSceneProfile profile = null)
        {
            CharacterTestSceneProfile safeProfile = profile != null ? profile : CharacterTestSceneProfile.LoadDefault();
            GameManager owner = host.GetComponent<GameManager>();
            if (owner == null)
                owner = host.AddComponent<GameManager>();

            var context = new CharacterTestRuntimeContext(owner, safeProfile)
            {
                Logger = new UnityRuntimeLogger(owner, "CharacterTest")
                {
                    Enabled = false,
                    UseRichTextColors = false
                }
            };
            var sessionBuilder = new CharacterTestRuntimeSessionBuilder(owner, context, safeProfile);
            var mainMenuCoordinator = new CharacterTestMainMenuCoordinator(context, safeProfile, sessionBuilder);
            var networkCoordinator = new CharacterTestNetworkCoordinator(context, safeProfile, sessionBuilder);
            var debugPresentationController =
                new CharacterTestDebugPresentationController(context, safeProfile, sessionBuilder);

            context.SessionBuilder = sessionBuilder;
            context.MainMenuCoordinator = mainMenuCoordinator;
            context.NetworkCoordinator = networkCoordinator;
            context.DebugPresentationController = debugPresentationController;
            mainMenuCoordinator.Initialize();
            return context;
        }

private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "Missing private method: " + methodName);
            method.Invoke(target, args);
        }

private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "Missing private method: " + methodName);
            return (T)method.Invoke(target, args);
        }

private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing private field: " + fieldName);
            return (T)field.GetValue(target);
        }

private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing private field: " + fieldName);
            field.SetValue(target, value);
        }

private static void ConfigureBoneRuntimeSyncBinding(
            CharacterBoneRuntimeSync sync,
            string bindingId,
            Transform target)
        {
            var binding = new CharacterBoneRuntimeSyncBinding();
            SetPrivateField(binding, "_bindingId", bindingId);
            SetPrivateField(binding, "_target", target);
            SetPrivateField(sync, "_bindings", new[] { binding });
        }

private static GlobalResourceRuntimeServices CreateStoryDialogResourceServices()
        {
            GlobalResourceRuntimeServices resources = CreateDefaultResourceServicesIfContains(
                CharacterTestFairyGuiStoryDialogIds.PackageBytesKey);
            if (resources != null)
                return resources;

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"));
            var provider = new MemoryResourceProvider()
                .Register("fgui/MxStoryDialog_fui.bytes", packageBytes);
            var options = new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            };
            options.ExternalProviders.Add(provider);
            ResourceCatalog applicationCatalog = new ResourceCatalog(
                "character-test.storydialog.test.catalog",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.Id,
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.TypeId,
                        "memory",
                        "fgui/MxStoryDialog_fui.bytes",
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.Variant,
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.PackageId,
                        allowOverride: true)
                });
            GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options);
            services.ResourceManager.AddCatalog(applicationCatalog);
            return services;
        }

private static GlobalResourceRuntimeServices CreateCombatHudResourceServices(out Texture2D atlasTexture)
        {
            atlasTexture = null;
            GlobalResourceRuntimeServices resources = CreateDefaultResourceServicesIfContains(
                CharacterTestFairyGuiCombatHudIds.PackageBytesKey,
                CharacterTestFairyGuiCombatHudIds.Atlas0Key);
            if (resources != null)
                return resources;

            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_fui.bytes"));
            atlasTexture = LoadPngTexture(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_atlas0.png"));

            var provider = new MemoryResourceProvider()
                .Register("fgui/MxCharacterTestCombatHud_fui.bytes", packageBytes)
                .Register("fgui/MxCharacterTestCombatHud_atlas0.png", atlasTexture);
            var options = new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            };
            options.ExternalProviders.Add(provider);
            ResourceCatalog applicationCatalog = new ResourceCatalog(
                "character-test.combathud.test.catalog",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.Id,
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.TypeId,
                        "memory",
                        "fgui/MxCharacterTestCombatHud_fui.bytes",
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.Variant,
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.Id,
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.TypeId,
                        "memory",
                        "fgui/MxCharacterTestCombatHud_atlas0.png",
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.Variant,
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.PackageId,
                        allowOverride: true)
                });
            GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options);
            services.ResourceManager.AddCatalog(applicationCatalog);
            return services;
        }

private static GlobalResourceRuntimeServices CreateSettingsPanelResourceServices(out Texture2D atlasTexture)
        {
            return CreatePlayerPanelResourceServices(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.PackageBytesKey,
                CharacterTestFairyGuiSettingsPanelIds.Atlas0Key,
                out atlasTexture);
        }

private static GlobalResourceRuntimeServices CreateNetworkPanelResourceServices(out Texture2D atlasTexture)
        {
            return CreatePlayerPanelResourceServices(
                CharacterTestFairyGuiNetworkPanelIds.PackageId,
                CharacterTestFairyGuiNetworkPanelIds.PackageBytesKey,
                CharacterTestFairyGuiNetworkPanelIds.Atlas0Key,
                out atlasTexture);
        }

private static GlobalResourceRuntimeServices CreateAbilityBindingPanelResourceServices(out Texture2D atlasTexture)
        {
            return CreatePlayerPanelResourceServices(
                CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                CharacterTestFairyGuiAbilityBindingPanelIds.PackageBytesKey,
                CharacterTestFairyGuiAbilityBindingPanelIds.Atlas0Key,
                out atlasTexture);
        }

private static GlobalResourceRuntimeServices CreatePlayerPanelResourceServices(
            string packageId,
            ResourceKey packageBytesKey,
            ResourceKey atlas0Key,
            out Texture2D atlasTexture)
        {
            atlasTexture = null;
            bool runtimeAiLab = packageId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId;
            ResourceKey[] required = runtimeAiLab
                ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.Create().Views[0].RequiredResources.ToArray()
                : new[] { packageBytesKey, atlas0Key };
            GlobalResourceRuntimeServices resources = CreateDefaultResourceServicesIfContains(required);
            if (resources != null)
                return resources;

            string packageAddress = "fgui/" + packageId + "_fui.bytes";
            string atlasAddress = "fgui/" + packageId + "_atlas0.png";
            byte[] packageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI",
                packageId,
                packageId + "_fui.bytes"));
            atlasTexture = LoadPngTexture(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI",
                packageId,
                packageId + "_atlas0.png"));

            var provider = new MemoryResourceProvider()
                .Register(packageAddress, packageBytes)
                .Register(atlasAddress, atlasTexture);
            var options = new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            };
            options.ExternalProviders.Add(provider);
            var entries = new List<ResourceCatalogEntry>
                {
                    new ResourceCatalogEntry(
                        packageBytesKey.Id,
                        packageBytesKey.TypeId,
                        "memory",
                        packageAddress,
                        packageBytesKey.Variant,
                        packageBytesKey.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        atlas0Key.Id,
                        atlas0Key.TypeId,
                        "memory",
                        atlasAddress,
                        atlas0Key.Variant,
                        atlas0Key.PackageId,
                        allowOverride: true)
                };
            if (runtimeAiLab)
            {
                foreach (var resource in CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.Create().Packages[0].RequiredResources)
                {
                    ResourceKey key = resource.Key;
                    if (key.Equals(atlas0Key)) continue;
                    string suffix = key.Id.Substring(key.Id.LastIndexOf('.') + 1);
                    string filename = packageId + "_" + suffix + ".png";
                    Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Bundles/FGUI/" + packageId + "/" + filename);
                    Assert.IsNotNull(texture, filename);
                    string address = "fgui/" + filename;
                    provider.Register(address, texture);
                    entries.Add(new ResourceCatalogEntry(key.Id, key.TypeId, "memory", address, key.Variant, key.PackageId, allowOverride: true));
                }
            }
            ResourceCatalog applicationCatalog = new ResourceCatalog("character-test.player-panel.test.catalog", string.Empty, entries.ToArray());
            GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options);
            services.ResourceManager.AddCatalog(applicationCatalog);
            return services;
        }

private static GlobalResourceRuntimeServices CreateCharacterTestUiResourceServices(
            out Texture2D combatHudAtlasTexture,
            out Texture2D mainMenuAtlasTexture)
        {
            combatHudAtlasTexture = null;
            mainMenuAtlasTexture = null;
            GlobalResourceRuntimeServices resources = CreateDefaultResourceServicesIfContains(
                CharacterTestFairyGuiStoryDialogIds.PackageBytesKey,
                CharacterTestFairyGuiCombatHudIds.PackageBytesKey,
                CharacterTestFairyGuiCombatHudIds.Atlas0Key,
                CharacterTestFairyGuiMainMenuIds.PackageBytesKey,
                CharacterTestFairyGuiMainMenuIds.Atlas0Key);
            if (resources != null)
                return resources;

            byte[] storyPackageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes"));
            byte[] combatHudPackageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_fui.bytes"));
            byte[] mainMenuPackageBytes = File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_fui.bytes"));
            combatHudAtlasTexture = LoadPngTexture(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_atlas0.png"));
            mainMenuAtlasTexture = LoadPngTexture(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_atlas0.png"));

            var provider = new MemoryResourceProvider()
                .Register("fgui/MxStoryDialog_fui.bytes", storyPackageBytes)
                .Register("fgui/MxCharacterTestCombatHud_fui.bytes", combatHudPackageBytes)
                .Register("fgui/MxCharacterTestCombatHud_atlas0.png", combatHudAtlasTexture)
                .Register("fgui/MxCharacterTestUi_fui.bytes", mainMenuPackageBytes)
                .Register("fgui/MxCharacterTestUi_atlas0.png", mainMenuAtlasTexture);
            var options = new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            };
            options.ExternalProviders.Add(provider);
            ResourceCatalog applicationCatalog = new ResourceCatalog(
                "character-test.ui.test.catalog",
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.Id,
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.TypeId,
                        "memory",
                        "fgui/MxStoryDialog_fui.bytes",
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.Variant,
                        CharacterTestFairyGuiStoryDialogIds.PackageBytesKey.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.Id,
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.TypeId,
                        "memory",
                        "fgui/MxCharacterTestCombatHud_fui.bytes",
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.Variant,
                        CharacterTestFairyGuiCombatHudIds.PackageBytesKey.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.Id,
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.TypeId,
                        "memory",
                        "fgui/MxCharacterTestCombatHud_atlas0.png",
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.Variant,
                        CharacterTestFairyGuiCombatHudIds.Atlas0Key.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiMainMenuIds.PackageBytesKey.Id,
                        CharacterTestFairyGuiMainMenuIds.PackageBytesKey.TypeId,
                        "memory",
                        "fgui/MxCharacterTestUi_fui.bytes",
                        CharacterTestFairyGuiMainMenuIds.PackageBytesKey.Variant,
                        CharacterTestFairyGuiMainMenuIds.PackageBytesKey.PackageId,
                        allowOverride: true),
                    new ResourceCatalogEntry(
                        CharacterTestFairyGuiMainMenuIds.Atlas0Key.Id,
                        CharacterTestFairyGuiMainMenuIds.Atlas0Key.TypeId,
                        "memory",
                        "fgui/MxCharacterTestUi_atlas0.png",
                        CharacterTestFairyGuiMainMenuIds.Atlas0Key.Variant,
                        CharacterTestFairyGuiMainMenuIds.Atlas0Key.PackageId,
                        allowOverride: true)
                });
            GlobalResourceRuntimeServices services = GlobalResourceRuntimeServices.Create(options);
            services.ResourceManager.AddCatalog(applicationCatalog);
            return services;
        }

private static GlobalResourceRuntimeServices CreateDefaultResourceServicesIfContains(
            params ResourceKey[] requiredKeys)
        {
            GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });

            if (!resources.HasBootstrapError)
            {
                bool containsAll = true;
                for (int i = 0; i < requiredKeys.Length; i++)
                {
                    if (resources.ResourceManager.Contains(requiredKeys[i]))
                        continue;

                    containsAll = false;
                    break;
                }

                if (containsAll)
                    return resources;
            }

            resources.Dispose();
            return null;
        }

private static Texture2D LoadPngTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(texture, bytes), path);
            texture.name = Path.GetFileNameWithoutExtension(path);
            return texture;
        }

private static string DescribeResourceEntries(ResourceManager manager, string contains)
        {
            ResourceDebugSnapshot snapshot = manager.CreateDebugSnapshot();
            string filter = contains ?? string.Empty;
            string[] matches = snapshot.EntryOrigins
                .Where(origin => origin.Key.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || origin.CatalogId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || origin.PackageId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(origin => origin.Key + " catalog=" + origin.CatalogId + " package=" + origin.PackageId + " override=" + origin.OverridesAnotherEntry)
                .ToArray();
            return "catalogs=" + snapshot.CatalogCount
                + ", entries=" + snapshot.EntryCount
                + ", providers=" + snapshot.ProviderCount
                + ", failed=" + snapshot.FailedCount
                + ", matches=[" + string.Join("; ", matches) + "]";
        }

private static void RemoveCharacterTestStoryDialogPackageIfLoaded()
        {
            Type packageType = Type.GetType("FairyGUI.UIPackage, FairyGUI");
            if (packageType == null)
                return;

            MethodInfo getByName = packageType.GetMethod(
                "GetByName",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo removePackage = packageType.GetMethod(
                "RemovePackage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (getByName == null || removePackage == null)
                return;

            object existing = getByName.Invoke(null, new object[] { CharacterTestFairyGuiStoryDialogIds.PackageId });
            if (existing != null)
                removePackage.Invoke(null, new object[] { CharacterTestFairyGuiStoryDialogIds.PackageId });
        }

private static void RemoveCharacterTestCombatHudPackageIfLoaded()
        {
            Type packageType = Type.GetType("FairyGUI.UIPackage, FairyGUI");
            if (packageType == null)
                return;

            MethodInfo getByName = packageType.GetMethod(
                "GetByName",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo removePackage = packageType.GetMethod(
                "RemovePackage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (getByName == null || removePackage == null)
                return;

            object existing = getByName.Invoke(null, new object[] { CharacterTestFairyGuiCombatHudIds.PackageId });
            if (existing != null)
                removePackage.Invoke(null, new object[] { CharacterTestFairyGuiCombatHudIds.PackageId });
        }

private static void RemoveCharacterTestLegacyPanelPackageIfLoaded()
        {
            Type packageType = Type.GetType("FairyGUI.UIPackage, FairyGUI");
            if (packageType == null)
                return;

            MethodInfo getByName = packageType.GetMethod(
                "GetByName",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo removePackage = packageType.GetMethod(
                "RemovePackage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (getByName == null || removePackage == null)
                return;

            string[] packageIds =
            {
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiNetworkPanelIds.PackageId,
                CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId
            };
            for (int i = 0; i < packageIds.Length; i++)
            {
                object existing = getByName.Invoke(null, new object[] { packageIds[i] });
                if (existing != null)
                    removePackage.Invoke(null, new object[] { packageIds[i] });
            }
        }

private static void RemoveCharacterTestMainMenuPackageIfLoaded()
        {
            Type packageType = Type.GetType("FairyGUI.UIPackage, FairyGUI");
            if (packageType == null)
                return;

            MethodInfo getByName = packageType.GetMethod(
                "GetByName",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo removePackage = packageType.GetMethod(
                "RemovePackage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (getByName == null || removePackage == null)
                return;

            object existing = getByName.Invoke(null, new object[] { CharacterTestFairyGuiMainMenuIds.PackageId });
            if (existing != null)
                removePackage.Invoke(null, new object[] { CharacterTestFairyGuiMainMenuIds.PackageId });
        }

private static void ResetFairyGuiTestState()
        {
            CharacterTestFairyGuiLayerRoots.ReleaseSharedLayerRoots();
            CharacterTestFairyGuiTestStageReset.Reset();
            DestroyObjectNamed("Stage Camera");
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            RemoveCharacterTestCombatHudPackageIfLoaded();
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            RemoveCharacterTestMainMenuPackageIfLoaded();
            InvokeFairyGuiStaticMethod("FairyGUI.FontManager, FairyGUI", "Clear");
            InvokeFairyGuiStaticMethod("FairyGUI.UIConfig, FairyGUI", "ClearResourceRefs");
        }

private static void InvokeFairyGuiStaticMethod(string typeName, string methodName)
        {
            Type type = Type.GetType(typeName);
            MethodInfo method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }

private static void DestroyObjectNamed(string objectName)
        {
            if (string.Equals(objectName, "Stage Camera", StringComparison.Ordinal)
                || string.Equals(objectName, "Stage", StringComparison.Ordinal)
                || string.Equals(objectName, "GRoot", StringComparison.Ordinal))
            {
                CharacterTestFairyGuiTestStageReset.Reset();
                return;
            }

            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }

private static CharacterTestFairyGuiStoryCommandDescriptor FindStoryDialogCommand(
            CharacterTestFairyGuiStoryDialogViewModel viewModel,
            string commandId)
        {
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.Commands);
            for (int i = 0; i < viewModel.Commands.Count; i++)
            {
                CharacterTestFairyGuiStoryCommandDescriptor command = viewModel.Commands[i];
                if (string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                    return command;
            }

            Assert.Fail("Missing CharacterTest Story dialog command: " + commandId);
            return default;
        }

private static Fgui.GComponent CreateCombatHudRoot()
        {
            var root = new Fgui.GComponent();
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerPlate);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerName);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthIcon);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthTrack);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthFill);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthText);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureIcon);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureTrack);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureFill);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerCohesionBreakMarker);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureText);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerActionText);
            for (int slot = 1; slot <= CharacterTestFairyGuiCombatHudIds.BuffSlotCount; slot++)
            {
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBackground(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBorder(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotLabel(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotCooldownFill(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotStack(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotTime(slot));
            }

            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetPlate);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetName);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetStatusText);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetHealthTrack);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetHealthFill);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetHealthText);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureTrack);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureFill);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetCohesionBreakMarker);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureText);
            for (int slot = 1; slot <= CharacterTestFairyGuiCombatHudIds.BuffSlotCount; slot++)
            {
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBorder(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotLabel(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotCooldownFill(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotStack(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotTime(slot));
            }

            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.LockCrosshair);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackPanel);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackTrim);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.FeedbackText);
            AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground);
            AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText);
            for (int slot = 1; slot <= CharacterTestAbilityHotbarRuntime.SlotCount; slot++)
            {
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotBackground(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotSelected(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotKey(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotName(slot));
                AddCombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(slot));
                AddCombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownText(slot));
            }

            return root;
        }

private static Fgui.GComponent CreateNetworkPanelRoot()
        {
            var root = new Fgui.GComponent();
            AddCombatHudObject(root, "modalShade");
            AddCombatHudObject(root, CharacterTestFairyGuiNetworkPanelIds.PanelFrame);
            AddCombatHudObject(root, CharacterTestFairyGuiNetworkPanelIds.PanelTrim);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.Title);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.Status);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.Endpoint);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.DirectLanMode);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.RoomMode);
            AddCombatHudObject(root, "modeBand");
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ModeDescription);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ConnectionHeader);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.PortLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.PortInput);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DisplayNameLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.RoomNameLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RoomNameInput);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ConnectionSummary);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.RoomHeader);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.RoomSummary);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.MemberSummary);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ReadinessSummary);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.SyncProbeSummary);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ReplayHeader);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.ReplaySummary);
            AddCombatHudObject(root, "detailsPanel");
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailsHeader);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailTransport);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailPeer);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailRtt);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailFrames);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailSnapshot);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.DetailDisconnect);
            AddCombatHudText(root, CharacterTestFairyGuiNetworkPanelIds.Message);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Host);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Join);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Disconnect);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Ready);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.EnterGame);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe);
            AddNetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Close);
            return root;
        }

private static CharacterTestNetworkLoopbackDiagnostics CreateNetworkPanelDiagnostics(
            NetworkRoomStatus roomStatus,
            int roomReadyCount,
            int roomConnectedCount,
            NetworkSessionStatus clientStatus = NetworkSessionStatus.Connecting,
            int ownedEntityId = 0,
            int fullSnapshotReceivedCount = 0)
        {
            return new CharacterTestNetworkLoopbackDiagnostics(
                true,
                NetworkSessionStatus.Connected,
                clientStatus,
                RuntimeFrame.Zero,
                RuntimeFrame.Zero,
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0L,
                0,
                0,
                transportMode: "test",
                transportId: "test-room",
                localPeerId: "host-player",
                ownedEntityId: ownedEntityId,
                connectedPeerCount: roomConnectedCount,
                ownershipSummary: "test",
                fullSnapshotReceivedCount: fullSnapshotReceivedCount,
                roomStatus: roomStatus,
                roomId: "character-test-room",
                roomMembers: "host-player[ready][on],remote-player[ready][on]",
                roomReadyCount: roomReadyCount,
                roomConnectedCount: roomConnectedCount,
                roomStateVersion: 1L,
                roomReason: string.Empty);
        }

private static Fgui.GComponent CreateMainMenuRoot()
        {
            var root = new Fgui.GComponent();
            AddCombatHudObject(root, CharacterTestFairyGuiMainMenuIds.PanelFrame);
            AddCombatHudText(root, CharacterTestFairyGuiMainMenuIds.Title);
            AddCombatHudText(root, CharacterTestFairyGuiMainMenuIds.Status);
            AddCombatHudText(root, CharacterTestFairyGuiMainMenuIds.Message);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Start);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Network);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Abilities);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.BehaviorDeck);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Settings);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Restart);
            AddMainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Close);
            return root;
        }

private static Fgui.GComponent CreateAbilityBindingPanelRoot()
        {
            var root = new Fgui.GComponent();
            AddCombatHudObject(root, CharacterTestFairyGuiAbilityBindingPanelIds.PanelFrame);
            AddCombatHudText(root, CharacterTestFairyGuiAbilityBindingPanelIds.Title);
            AddCombatHudText(root, CharacterTestFairyGuiAbilityBindingPanelIds.Status);
            AddCombatHudText(root, CharacterTestFairyGuiAbilityBindingPanelIds.Message);
            AddCombatHudText(root, CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel);
            for (int slot = 1; slot <= CharacterTestAbilityHotbarRuntime.SlotCount; slot++)
                AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(slot));
            for (int i = 0; i < CharacterTestFairyGuiAbilityBindingPanelIds.AbilityChoiceCount; i++)
                AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i));
            AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.Clear);
            AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults);
            AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.Save);
            AddNetworkPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.Cancel);
            return root;
        }

private static Fgui.GComponent CreateSettingsPanelRoot()
        {
            var root = new Fgui.GComponent();
            root.SetSize(1280f, 800f);
            AddCombatHudObject(root, "scrim");
            AddCombatHudObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame);
            AddCombatHudObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelTrim);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.Title);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.Subtitle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.PageTitle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.PageDescription);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.Status);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.DirtyMarker);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.Close);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.NavGraphics);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.NavAudio);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.NavControls);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.GamepadTab);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.GraphicsHeader);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.VSyncLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.VSyncValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.VSyncToggle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.RenderScaleLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityPrev);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountPrev);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionPrev);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ResolutionLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ResolutionValue);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.WindowModeLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.WindowModeValue);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.LanguageLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.LanguageValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.LanguagePrev);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.LanguageNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AudioHeader);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AudioVolumeLabel);
            AddNetworkPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.AudioMutedValue);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ControlsHeader);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.InputInstruction);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.ActionColumnTitle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.PrimaryColumnTitle);
            AddCombatHudText(root, CharacterTestFairyGuiSettingsPanelIds.SecondaryColumnTitle);
            root.AddChild(new Fgui.GList { name = CharacterTestFairyGuiSettingsPanelIds.InputList });

            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.Apply);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.CancelChanges);
            AddNetworkPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.ResetInput);

            SetSettingsPanelTestBounds(root, "scrim", 0f, 0f, 1280f, 800f);
            SetSettingsPanelTestBounds(
                root,
                CharacterTestFairyGuiSettingsPanelIds.PanelFrame,
                48f,
                40f,
                1184f,
                720f);
            SetSettingsPanelTestBounds(
                root,
                CharacterTestFairyGuiSettingsPanelIds.Title,
                148f,
                76f,
                260f,
                48f);
            SetSettingsPanelTestBounds(
                root,
                CharacterTestFairyGuiSettingsPanelIds.InputList,
                320f,
                286f,
                864f,
                334f);
            return root;
        }

private static void SetSettingsPanelTestBounds(
            Fgui.GComponent root,
            string name,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject child = root.GetChild(name);
            child.SetXY(x, y);
            child.SetSize(width, height);
        }

private static MxConfigUiTextProvider CreateCharacterTestTextProvider(string localeId)
        {
            string path = GetCharacterTestLocalizationPath();
            LocalizationTsvLoadResult load = LocalizationTsvLoader.LoadFromText(File.ReadAllText(path));
            Assert.IsFalse(load.HasErrors, load.Issues.Count > 0 ? load.Issues[0].Message : string.Empty);
            return new MxConfigUiTextProvider(load.Provider, new MxUiLocaleId(localeId));
        }

private static string GetCharacterTestLocalizationPath()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/StreamingAssets/MxFramework/Localization/character_test.tsv");
            if (File.Exists(path))
                return path;

            return Path.Combine(
                Application.dataPath,
                "StreamingAssets/MxFramework/Localization/character_test.tsv");
        }

private static void AddNetworkPanelInput(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GTextInput { name = name });
        }

private static void AddNetworkPanelButton(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GButton { name = name });
        }

private static void AddMainMenuButton(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GButton { name = name });
        }

private static void AssertNetworkPanelChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static void AssertSettingsPanelChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static void AssertMainMenuChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static void AssertMainMenuRelationDeclared(
            string panelText,
            string childName,
            string sidePair)
        {
            string normalized = panelText.Replace("\r\n", "\n");
            int childIndex = normalized.IndexOf("name=\"" + childName + "\"", StringComparison.Ordinal);
            Assert.GreaterOrEqual(childIndex, 0, "Expected MainMenu child: " + childName);
            int relationIndex = normalized.IndexOf(
                "<relation target=\"\" sidePair=\"" + sidePair + "\"/>",
                childIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(relationIndex, 0, "Expected MainMenu child " + childName + " relation " + sidePair + ".");
        }

private static void AssertAbilityBindingPanelChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static void AssertFairyGuiArenaPanelChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static void AssertAbilityBindingPanelRelationDeclared(
            string panelText,
            string childName,
            string sidePair)
        {
            string normalized = panelText.Replace("\r\n", "\n");
            int childIndex = normalized.IndexOf(
                "name=\"" + childName + "\"",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(childIndex, 0, "Expected AbilityBindingPanel child: " + childName);

            int relationIndex = normalized.IndexOf(
                "<relation target=\"\" sidePair=\"" + sidePair + "\"/>",
                childIndex,
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(
                relationIndex,
                0,
                "Expected AbilityBindingPanel child "
                + childName
                + " to declare relation sidePair="
                + sidePair);
        }

private static void AssertCombatHudChildDeclared(string panelText, string childName)
        {
            StringAssert.Contains("name=\"" + childName + "\"", panelText);
        }

private static CharacterTestFairyGuiCombatHudAbilitySlotViewModel[] CreateCombatHudSlotViewModels()
        {
            var slots = new CharacterTestFairyGuiCombatHudAbilitySlotViewModel[
                CharacterTestAbilityHotbarRuntime.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                int slotNumber = i + 1;
                slots[i] = new CharacterTestFairyGuiCombatHudAbilitySlotViewModel
                {
                    SlotNumber = slotNumber,
                    KeyText = slotNumber.ToString(),
                    AbilityText = slotNumber == 1 ? "Flame Lance" : slotNumber == 8 ? "Empty" : "Fireball",
                    IsBound = slotNumber != 8,
                    IsSelected = slotNumber == 1,
                    CooldownRatio = slotNumber == 1 ? 0.5f : 0f,
                    CooldownText = slotNumber == 1 ? "30f" : string.Empty
                };
            }

            return slots;
        }

private static CharacterTestFairyGuiCombatHudViewModel CreateCombatHudBinderTestViewModel()
        {
            var viewModel = new CharacterTestFairyGuiCombatHudViewModel
            {
                HasLocalPlayer = true,
                PlayerName = "Wanderer",
                PlayerHealth = 50,
                PlayerMaxHealth = 100,
                PlayerHealthRatio = 0.5f,
                PlayerScatterPressure = 25,
                PlayerMaxScatterPressure = 100,
                PlayerScatterPressureRatio = 0.25f,
                PlayerActionText = "Action: light_attack",
                TargetVisible = true,
                TargetLocked = true,
                TargetName = "Sentinel",
                TargetHealth = 30,
                TargetMaxHealth = 60,
                TargetHealthRatio = 0.5f,
                TargetScatterPressure = 40,
                TargetMaxScatterPressure = 80,
                TargetScatterPressureRatio = 0.5f,
                TargetStatusText = "HITSTUN 4f",
                FeedbackVisible = true,
                FeedbackText = "Input rejected: blocked.",
                NetworkLatencyVisible = true,
                NetworkLatencyText = "NET 4f / 67ms",
                NetworkLatencyTone = "warn"
            };
            viewModel.SetAbilitySlots(CreateCombatHudSlotViewModels());
            viewModel.SetPlayerBuffs(new[]
            {
                new CharacterTestFairyGuiCombatHudBuffViewModel
                {
                    BuffId = CharacterTestGuardAuraIds.BuffId,
                    ShortName = "DEF+",
                    TimeText = "2f",
                    Remaining01 = 0.5f,
                    Polarity = GameplayBuffPresentationPolarity.Aura,
                    TooltipText = "Guard Aura\nDefense(3) +5"
                }
            });
            viewModel.SetTargetBuffs(new[]
            {
                new CharacterTestFairyGuiCombatHudBuffViewModel
                {
                    BuffId = CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    ShortName = "DEF-",
                    TimeText = "300f",
                    Remaining01 = 0.25f,
                    Polarity = GameplayBuffPresentationPolarity.Debuff,
                    TooltipText = "Defense Break\nDefense(3) -3"
                }
            });

            _ = viewModel.PlayerHealthText;
            _ = viewModel.PlayerScatterPressureText;
            _ = viewModel.TargetHealthText;
            _ = viewModel.TargetScatterPressureText;
            return viewModel;
        }

private static CharacterTestNetworkSessionStatus CreateHudNetworkStatus(
            NetworkSessionStatus clientStatus,
            int rttFrames = 0,
            int tickRate = 60,
            string disconnectReason = "")
        {
            return new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                LaunchMode = CharacterTestNetworkLaunchMode.Room,
                NetworkMode = CharacterTestNetworkMode.UnityTransportRoomClient,
                NetworkTickRate = tickRate,
                ClientStatus = clientStatus,
                ServerStatus = NetworkSessionStatus.Connected,
                RttEstimateFrames = rttFrames,
                DisconnectReason = disconnectReason ?? string.Empty
            };
        }

private static void AddCombatHudObject(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GGraph { name = name });
        }

private static void AddCombatHudText(Fgui.GComponent root, string name)
        {
            root.AddChild(new Fgui.GTextField { name = name });
        }

private static string CombatHudText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

private static Fgui.GObject CombatHudObject(Fgui.GComponent root, string name)
        {
            return root.GetChild(name);
        }

private static Fgui.GComponent HeadPadBadge(Fgui.GComponent root, int slot)
        {
            return root.GetChild("buffBadge" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture)).asCom;
        }

private static Fgui.GObject HeadPadBadgeObject(Fgui.GComponent root, int slot, string suffix)
        {
            return HeadPadBadge(root, slot).GetChild(
                "buffBadge" + suffix + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

private static string HeadPadBadgeText(Fgui.GComponent root, int slot, string suffix)
        {
            return HeadPadBadgeObject(root, slot, suffix).asTextField.text;
        }

private static string NetworkPanelText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

private static Fgui.GObject NetworkPanelObject(Fgui.GComponent root, string name)
        {
            return root.GetChild(name);
        }

private static Fgui.GTextField NetworkPanelInput(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField;
        }

private static Fgui.GButton NetworkPanelButton(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asButton;
        }

private static string SettingsPanelText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

private static Fgui.GTextField SettingsPanelInput(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField;
        }

private static Fgui.GObject SettingsPanelObject(Fgui.GComponent root, string name)
        {
            return root.GetChild(name);
        }

private static Fgui.GButton SettingsPanelButton(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asButton;
        }

private static string MainMenuText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

private static Fgui.GButton MainMenuButton(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asButton;
        }

private static string AbilityPanelText(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asTextField.text;
        }

private static Fgui.GButton AbilityPanelButton(Fgui.GComponent root, string name)
        {
            return root.GetChild(name).asButton;
        }

private sealed class RecordingUiCommandSink : IMxUiCommandSink
        {
            public int Count { get; private set; }
            public MxUiCommand Last { get; private set; }

            public void Enqueue(MxUiCommand command)
            {
                Count++;
                Last = command;
            }
        }

private sealed class RecordingUiFeedbackPlayer : IMxUiFeedbackPlayer
        {
            public List<MxUiFeedbackKind> Kinds { get; } =
                new List<MxUiFeedbackKind>();

            public bool TryPlay(
                MxUiFeedbackKind kind,
                in MxUiFeedbackContext context)
            {
                Kinds.Add(kind);
                return true;
            }
        }

private sealed class RecordingSettingsStore : IMxSettingsStore
        {
            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public MxSettingsProfile SavedProfile { get; private set; }
            public MxSettingsProfile LoadProfile { get; set; } = MxSettingsProfile.CreateDefault();

            public MxSettingsLoadResult LoadOrDefault()
            {
                LoadCount++;
                return MxSettingsLoadResult.Succeeded(LoadProfile);
            }

            public MxSettingsSaveResult Save(MxSettingsProfile profile)
            {
                SaveCount++;
                SavedProfile = profile;
                return MxSettingsSaveResult.Succeeded();
            }
        }

private sealed class RecordingSettingsApplier : IMxSettingsApplier
        {
            public string ApplierId => "test.character.settings.applier";
            public int ApplyCount { get; private set; }
            public MxSettingsProfile LastProfile { get; private set; }

            public MxSettingsApplyResult Apply(MxSettingsProfile profile)
            {
                ApplyCount++;
                LastProfile = profile;
                return MxSettingsApplyResult.Succeeded();
            }
        }

private sealed class RecordingInputRebindingService : IInputRebindingService
        {
            public bool IsRebinding { get; private set; }
            public int StartCount { get; private set; }
            public int CancelCount { get; private set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public int ResetCount { get; private set; }
            public string LastActionId { get; private set; }
            public int LastBindingIndex { get; private set; }

            public void StartRebind(string actionId, int bindingIndex)
            {
                StartCount++;
                LastActionId = actionId;
                LastBindingIndex = bindingIndex;
                IsRebinding = true;
            }

            public void Cancel()
            {
                CancelCount++;
                IsRebinding = false;
            }

            public void Save()
            {
                SaveCount++;
            }

            public void Load()
            {
                LoadCount++;
            }

            public void ResetToDefault()
            {
                ResetCount++;
                IsRebinding = false;
            }
        }

private sealed class RecordingInputRebindingEditSessionService :
            IInputRebindingService,
            IInputRebindingEditSessionService
        {
            public bool IsRebinding { get; private set; }
            public bool IsEditSessionActive { get; private set; }
            public bool HasPendingChanges { get; private set; }
            public int BeginEditSessionCount { get; private set; }
            public int CommitEditSessionCount { get; private set; }
            public int RollbackEditSessionCount { get; private set; }

            public void StartRebind(string actionId, int bindingIndex)
            {
                IsRebinding = true;
            }

            public void Cancel()
            {
                IsRebinding = false;
            }

            public void Save()
            {
            }

            public void Load()
            {
            }

            public void ResetToDefault()
            {
                IsRebinding = false;
                HasPendingChanges = IsEditSessionActive;
            }

            public void BeginEditSession()
            {
                BeginEditSessionCount++;
                IsEditSessionActive = true;
                HasPendingChanges = false;
            }

            public void CommitEditSession()
            {
                CommitEditSessionCount++;
                IsRebinding = false;
                IsEditSessionActive = false;
                HasPendingChanges = false;
            }

            public void RollbackEditSession()
            {
                RollbackEditSessionCount++;
                IsRebinding = false;
                IsEditSessionActive = false;
                HasPendingChanges = false;
            }
        }

private sealed class RecordingNetworkSessionController :
            ICharacterTestNetworkSessionController,
            ICharacterTestNetworkLobbyController,
            ICharacterTestNetworkDiagnosticController,
            ICharacterTestNetworkReplayController
        {
            public CharacterTestNetworkSessionStatus Status { get; set; } =
                CharacterTestNetworkSessionStatus.Offline();
            public CharacterTestNetworkSyncProbeStatus ProbeStatus { get; set; }
            public CharacterTestReplayPlaybackStatus ReplayStatus { get; set; } =
                CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay has not run.");
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int ReadyCount { get; private set; }
            public int StartRoomCount { get; private set; }
            public int EnterGameCount { get; private set; }
            public int StartProbeCount { get; private set; }
            public int StopProbeCount { get; private set; }
            public int SetAutoProbeCount { get; private set; }
            public int BeginReplayCount { get; private set; }
            public int ValidateReplayCount { get; private set; }
            public bool AutoProbeEnabled { get; private set; }
            public bool LastReady { get; private set; }
            public CharacterTestNetworkSessionOptions LastStartOptions { get; private set; }

            public CharacterTestNetworkSessionOptions CreateDefaultNetworkSessionOptions(
                CharacterTestNetworkSessionKind kind)
            {
                switch (kind)
                {
                    case CharacterTestNetworkSessionKind.Host:
                        return CharacterTestNetworkSessionOptions.HostDefaults(9911, "0.0.0.0", "server");
                    case CharacterTestNetworkSessionKind.Join:
                        return CharacterTestNetworkSessionOptions.JoinDefaults("10.1.2.3", 9911, "server", "client-0");
                    default:
                        return new CharacterTestNetworkSessionOptions
                        {
                            Kind = CharacterTestNetworkSessionKind.Offline,
                            BindAddress = "0.0.0.0",
                            RemoteAddress = "10.1.2.3",
                            Port = 9911,
                            ServerPeerId = "server",
                            ClientPeerId = "client-0"
                        };
                }
            }

            public CharacterTestNetworkSessionResult StartNetworkSession(
                CharacterTestNetworkSessionOptions options)
            {
                StartCount++;
                LastStartOptions = options?.Clone();
                CharacterTestNetworkSessionKind kind =
                    LastStartOptions != null ? LastStartOptions.Kind : CharacterTestNetworkSessionKind.Join;
                CharacterTestNetworkLaunchMode launchMode = LastStartOptions != null
                    ? LastStartOptions.LaunchMode
                    : CharacterTestNetworkLaunchMode.DirectLan;
                CharacterTestNetworkMode mode = CharacterTestNetworkSessionModeMapper.ToNetworkMode(kind, launchMode);
                Status = new CharacterTestNetworkSessionStatus
                {
                    Kind = kind,
                    LaunchMode = launchMode,
                    NetworkMode = mode,
                    Message = kind.ToString(),
                    BindAddress = LastStartOptions != null ? LastStartOptions.BindAddress : "0.0.0.0",
                    RemoteAddress = LastStartOptions != null ? LastStartOptions.RemoteAddress : "10.1.2.3",
                    Port = LastStartOptions != null ? LastStartOptions.Port : (ushort)9911,
                    ServerPeerId = LastStartOptions != null ? LastStartOptions.ServerPeerId : "server",
                    ClientPeerId = LastStartOptions != null ? LastStartOptions.ClientPeerId : "client-0",
                    RoomId = LastStartOptions != null ? LastStartOptions.RoomId : "character-test-room",
                    RoomName = LastStartOptions != null ? LastStartOptions.RoomName : "CharacterTest LAN Room",
                    RoomStatus = launchMode == CharacterTestNetworkLaunchMode.Room
                        ? NetworkRoomStatus.Open
                        : NetworkRoomStatus.Closed,
                    RoomReadyCount = launchMode == CharacterTestNetworkLaunchMode.Room ? 1 : 0,
                    RoomConnectedCount = launchMode == CharacterTestNetworkLaunchMode.Room ? 1 : 0,
                    IsRoomHost = launchMode == CharacterTestNetworkLaunchMode.Room
                        && kind == CharacterTestNetworkSessionKind.Host,
                    CanSetRoomReady = launchMode == CharacterTestNetworkLaunchMode.Room,
                    CanStartRoomGame = launchMode == CharacterTestNetworkLaunchMode.Room && kind == CharacterTestNetworkSessionKind.Host,
                    CanRequestEnterGameplay = launchMode == CharacterTestNetworkLaunchMode.Room && kind == CharacterTestNetworkSessionKind.Host,
                    CanEnterGameplay = false,
                    ServerStatus = kind == CharacterTestNetworkSessionKind.Host
                        ? NetworkSessionStatus.Connected
                        : NetworkSessionStatus.Disconnected,
                    ClientStatus = kind == CharacterTestNetworkSessionKind.Join
                        ? NetworkSessionStatus.Connecting
                        : NetworkSessionStatus.Disconnected
                };
                return CharacterTestNetworkSessionResult.Succeeded(kind, mode, Status.Message, Status);
            }

            public CharacterTestNetworkSessionResult StopNetworkSession()
            {
                StopCount++;
                Status = CharacterTestNetworkSessionStatus.Offline("Offline");
                return CharacterTestNetworkSessionResult.Succeeded(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    "Offline",
                    Status);
            }

            public CharacterTestNetworkSessionResult SetLocalReady(bool ready)
            {
                ReadyCount++;
                LastReady = ready;
                Status.LocalRoomReady = ready;
                Status.RoomReadyCount = ready ? Math.Max(1, Status.RoomReadyCount) : 0;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    ready ? "Ready." : "Not ready.",
                    Status);
            }

            public CharacterTestNetworkSessionResult StartRoomGame()
            {
                StartRoomCount++;
                Status.RoomStatus = NetworkRoomStatus.InGame;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    "Started.",
                    Status);
            }

            public CharacterTestNetworkSessionResult EnterNetworkGameplay()
            {
                EnterGameCount++;
                Status.CanEnterGameplay = true;
                return CharacterTestNetworkSessionResult.Succeeded(
                    Status.Kind,
                    Status.NetworkMode,
                    "Entered.",
                    Status);
            }

            public CharacterTestNetworkSessionStatus CaptureNetworkSessionStatus()
            {
                return Status;
            }

            public CharacterTestNetworkSyncProbeStatus StartNetworkSyncProbe()
            {
                StartProbeCount++;
                ProbeStatus = new CharacterTestNetworkSyncProbeStatus
                {
                    Result = CharacterTestNetworkSyncProbeResult.Running,
                    IsRunning = true,
                    CanStart = true,
                    AutoRunEnabled = AutoProbeEnabled,
                    ResultCode = "RUNNING",
                    Summary = "Sync Probe RUNNING: test",
                    Detail = "auth recv=0"
                };
                Status.Message = ProbeStatus.Summary;
                return ProbeStatus;
            }

            public CharacterTestNetworkSyncProbeStatus StopNetworkSyncProbe()
            {
                StopProbeCount++;
                ProbeStatus = new CharacterTestNetworkSyncProbeStatus
                {
                    Result = CharacterTestNetworkSyncProbeResult.Cancelled,
                    IsRunning = false,
                    CanStart = true,
                    AutoRunEnabled = AutoProbeEnabled,
                    ResultCode = "CANCELLED",
                    Summary = "Sync Probe CANCELLED: test"
                };
                Status.Message = ProbeStatus.Summary;
                return ProbeStatus;
            }

            public void SetNetworkSyncProbeAutoRun(bool enabled)
            {
                SetAutoProbeCount++;
                AutoProbeEnabled = enabled;
                if (ProbeStatus != null)
                {
                    ProbeStatus.AutoRunEnabled = enabled;
                    if (!enabled)
                        ProbeStatus.AutoRunPending = false;
                }
            }

            public CharacterTestNetworkSyncProbeStatus CaptureNetworkSyncProbeStatus()
            {
                CharacterTestNetworkSyncProbeStatus status =
                    ProbeStatus ?? CharacterTestNetworkSyncProbeStatus.Idle(
                        Status != null && Status.IsNetworkActive && Status.CanEnterGameplay);
                status.AutoRunEnabled = AutoProbeEnabled || status.AutoRunEnabled;
                return status;
            }

            public CharacterTestReplayPlaybackStatus BeginNetworkReplayRecording()
            {
                BeginReplayCount++;
                ReplayStatus = new CharacterTestReplayPlaybackStatus
                {
                    Available = true,
                    Recording = true,
                    ResultCode = "RECORDING",
                    Summary = "CharacterTest replay recording started."
                };
                return ReplayStatus;
            }

            public CharacterTestReplayPlaybackStatus ValidateNetworkReplay()
            {
                ValidateReplayCount++;
                return ReplayStatus;
            }

            public CharacterTestReplayPlaybackStatus CaptureNetworkReplayStatus()
            {
                return ReplayStatus;
            }
        }

private sealed class RecordingLobbyNetworkRunner :
            ICharacterTestNetworkRunner,
            ICharacterTestNetworkLobbyRunner
        {
            public RecordingLobbyNetworkRunner(GameSlice slice)
            {
                Slice = slice;
            }

            public GameSlice Slice { get; }
            public CharacterTestNetworkLoopbackDiagnostics Diagnostics { get; set; }
            public bool IsRoomHost { get; set; } = true;
            public bool LocalRoomReady { get; set; } = true;
            public bool CanSetRoomReady { get; set; } = true;
            public bool CanStartRoomGame { get; set; }
            public int StartRoomCount { get; private set; }

            public void Tick(double deltaTime)
            {
            }

            public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
            {
                return Diagnostics;
            }

            public bool TrySetLocalRoomReady(bool ready, out string reason)
            {
                LocalRoomReady = ready;
                reason = string.Empty;
                return true;
            }

            public bool TryStartRoomGame(out string reason)
            {
                StartRoomCount++;
                reason = string.Empty;
                Diagnostics = CreateNetworkPanelDiagnostics(
                    NetworkRoomStatus.InGame,
                    roomReadyCount: 2,
                    roomConnectedCount: 2);
                return true;
            }

            public void Dispose()
            {
            }
        }

private sealed class RecordingInteractionConsumer : ICharacterTestInteractionConsumer
        {
            private readonly List<string> _calls;

            public RecordingInteractionConsumer(
                string consumerId,
                int consumerOrder,
                List<string> calls)
            {
                ConsumerId = consumerId;
                ConsumerOrder = consumerOrder;
                _calls = calls;
            }

            public string ConsumerId { get; }
            public int ConsumerOrder { get; }
            public int LastInteractionCount { get; private set; }
            public int LastTickRate { get; private set; }

            public void Consume(CharacterTestInteractionConsumerContext context)
            {
                _calls?.Add(ConsumerId);
                LastInteractionCount = context.Interactions.Count;
                LastTickRate = context.TickRate;
            }
        }

private sealed class RecordingAudioService : IAudioService
        {
            public int PlayOneShotCount { get; private set; }
            public AudioPlayRequest LastPlayRequest { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                PlayOneShotCount++;
                LastPlayRequest = request;
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
            {
                handle = new AudioHandle(1, request.EventId, request.EmitterId, AudioHandleState.Playing);
                return AudioPlayResult.Ok(handle);
            }

            public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusVolume(int busId, float volume)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusMuted(int busId, bool muted)
            {
                return AudioResult.Ok();
            }

            public AudioDebugSnapshot CaptureSnapshot()
            {
                return new AudioDebugSnapshot(
                    initialized: true,
                    totalPlayRequests: PlayOneShotCount,
                    totalStopRequests: 0,
                    activeEventCount: 0,
                    activeEvents: null,
                    busStates: null,
                    recentErrors: null);
            }

            public void Tick(float deltaTime)
            {
            }
        }

private sealed class RecordingMainMenuController : ICharacterTestMainMenuController
        {
            public CharacterTestMainMenuStatus Status { get; set; } = new CharacterTestMainMenuStatus
            {
                IsPaused = true,
                IsMenuVisible = true,
                MenuPausesRuntime = true,
                NetworkSessionKind = CharacterTestNetworkSessionKind.Offline,
                Message = "Offline"
            };

            public int StartCount { get; private set; }
            public int RestartCount { get; private set; }
            public int NetworkCount { get; private set; }
            public int CloseCount { get; private set; }

            public CharacterTestMainMenuStatus CaptureMainMenuStatus()
            {
                return Status;
            }

            public CharacterTestMainMenuCommandResult StartOrContinueFromMainMenu()
            {
                StartCount++;
                return CharacterTestMainMenuCommandResult.Succeeded("Started.");
            }

            public CharacterTestMainMenuCommandResult RestartOfflineFromMainMenu()
            {
                RestartCount++;
                return CharacterTestMainMenuCommandResult.Succeeded("Restarted.");
            }

            public CharacterTestMainMenuCommandResult OpenNetworkPanelFromMainMenu()
            {
                NetworkCount++;
                return CharacterTestMainMenuCommandResult.Succeeded("Network opened.");
            }

            public CharacterTestMainMenuCommandResult CloseMainMenu()
            {
                CloseCount++;
                return CharacterTestMainMenuCommandResult.Succeeded("Closed.");
            }
        }

private sealed class RecordingCursorDriver : IMxCursorDriver
        {
            private readonly List<MxCursorState> _states = new List<MxCursorState>();

            public bool Visible { get; private set; }
            public CursorLockMode LockState { get; private set; }
            public IReadOnlyList<MxCursorState> States => _states;

            public void Apply(MxCursorState state)
            {
                Visible = state.Visible;
                LockState = state.LockState;
                _states.Add(state);
            }

            public void OverrideState(bool visible, CursorLockMode lockState)
            {
                Visible = visible;
                LockState = lockState;
            }
        }

private static FrameworkPerformanceCounterSample FindInteractionCounter(
            FrameworkPerformanceCounterSnapshot snapshot,
            string counterId)
        {
            Assert.IsNotNull(snapshot);
            return snapshot.Samples.Single(sample => sample.CounterId == counterId);
        }

private static FrameworkPerformanceCounterSample FindRuntimeCounter(
            FrameworkPerformanceCounterSnapshot snapshot,
            string counterId)
        {
            Assert.IsNotNull(snapshot);
            return snapshot.Samples.Single(sample => sample.CounterId == counterId);
        }

private static FrameworkPerformanceCounterSample FindUiCounter(
            FrameworkPerformanceCounterSnapshot snapshot,
            string counterId)
        {
            Assert.IsNotNull(snapshot);
            return snapshot.Samples.Single(sample => sample.CounterId == counterId);
        }

private sealed class FakeStoryChoiceReader : IStoryChoiceSnapshotReader
        {
            private readonly StoryChoiceView[] _choices;

            public FakeStoryChoiceReader(StoryChoiceView[] choices)
            {
                _choices = choices ?? Array.Empty<StoryChoiceView>();
            }

            public int GetChoices(int beatInstanceId, int choiceSetId, Span<StoryChoiceView> buffer)
            {
                int written = Math.Min(_choices.Length, buffer.Length);
                for (int i = 0; i < written; i++)
                    buffer[i] = _choices[i];

                return _choices.Length;
            }
        }

private readonly struct CharacterTestExplosionRuntimeSliceResult
        {
            public CharacterTestExplosionRuntimeSliceResult(
                int interactionFrame,
                int pendingImpulseCountAtInteractionFrame,
                CharacterActionId pendingImpulseSourceActionId,
                int pendingImpulseStableEventId,
                int processedEventCount,
                int appliedImpulseCount,
                long hashBeforeMotionTick,
                long hashAfterCounterRead,
                long hashAfterMotionTick,
                int performanceLastFrameEvents,
                int performancePhysicsQueries,
                int performancePhysicsQueryResults,
                int performancePhysicsAppliedForces,
                int performanceVfxSpawned,
                int performanceAudioPlayed,
                int targetVelocityXMillimetersPerSecond,
                int targetVelocityYMillimetersPerSecond)
            {
                InteractionFrame = interactionFrame;
                PendingImpulseCountAtInteractionFrame = pendingImpulseCountAtInteractionFrame;
                PendingImpulseSourceActionId = pendingImpulseSourceActionId;
                PendingImpulseStableEventId = pendingImpulseStableEventId;
                ProcessedEventCount = processedEventCount;
                AppliedImpulseCount = appliedImpulseCount;
                HashBeforeMotionTick = hashBeforeMotionTick;
                HashAfterCounterRead = hashAfterCounterRead;
                HashAfterMotionTick = hashAfterMotionTick;
                PerformanceLastFrameEvents = performanceLastFrameEvents;
                PerformancePhysicsQueries = performancePhysicsQueries;
                PerformancePhysicsQueryResults = performancePhysicsQueryResults;
                PerformancePhysicsAppliedForces = performancePhysicsAppliedForces;
                PerformanceVfxSpawned = performanceVfxSpawned;
                PerformanceAudioPlayed = performanceAudioPlayed;
                TargetVelocityXMillimetersPerSecond = targetVelocityXMillimetersPerSecond;
                TargetVelocityYMillimetersPerSecond = targetVelocityYMillimetersPerSecond;
            }

            public int InteractionFrame { get; }
            public int PendingImpulseCountAtInteractionFrame { get; }
            public CharacterActionId PendingImpulseSourceActionId { get; }
            public int PendingImpulseStableEventId { get; }
            public int ProcessedEventCount { get; }
            public int AppliedImpulseCount { get; }
            public long HashBeforeMotionTick { get; }
            public long HashAfterCounterRead { get; }
            public long HashAfterMotionTick { get; }
            public int PerformanceLastFrameEvents { get; }
            public int PerformancePhysicsQueries { get; }
            public int PerformancePhysicsQueryResults { get; }
            public int PerformancePhysicsAppliedForces { get; }
            public int PerformanceVfxSpawned { get; }
            public int PerformanceAudioPlayed { get; }
            public int TargetVelocityXMillimetersPerSecond { get; }
            public int TargetVelocityYMillimetersPerSecond { get; }
        }

private readonly struct CharacterTestTornadoPerformanceRuntimeSliceResult
        {
            public CharacterTestTornadoPerformanceRuntimeSliceResult(
                int interactionFrame,
                int performanceLastFrameEvents,
                int performancePhysicsQueries,
                int performancePhysicsQueryResults,
                int performancePhysicsCharacters,
                int performancePhysicsMovables,
                int performancePhysicsAppliedForces,
                long hashBeforeCounterRead,
                long hashAfterCounterRead)
            {
                InteractionFrame = interactionFrame;
                PerformanceLastFrameEvents = performanceLastFrameEvents;
                PerformancePhysicsQueries = performancePhysicsQueries;
                PerformancePhysicsQueryResults = performancePhysicsQueryResults;
                PerformancePhysicsCharacters = performancePhysicsCharacters;
                PerformancePhysicsMovables = performancePhysicsMovables;
                PerformancePhysicsAppliedForces = performancePhysicsAppliedForces;
                HashBeforeCounterRead = hashBeforeCounterRead;
                HashAfterCounterRead = hashAfterCounterRead;
            }

            public int InteractionFrame { get; }
            public int PerformanceLastFrameEvents { get; }
            public int PerformancePhysicsQueries { get; }
            public int PerformancePhysicsQueryResults { get; }
            public int PerformancePhysicsCharacters { get; }
            public int PerformancePhysicsMovables { get; }
            public int PerformancePhysicsAppliedForces { get; }
            public long HashBeforeCounterRead { get; }
            public long HashAfterCounterRead { get; }
        }

private sealed class TrackingInputProvider : IInputProvider
        {
            private readonly InputCommandQueue _commands = new InputCommandQueue();
            private readonly InputContextStack _contexts = new InputContextStack();

            public TrackingInputProvider(InputSnapshot snapshot, InputContext initialContext = InputContext.Gameplay)
            {
                Snapshot = snapshot;
                _contexts.Set(initialContext);
            }

            public InputSnapshot Snapshot { get; }
            public InputCommandQueue Commands => _commands;
            public InputContext CurrentContext => _contexts.ActiveContext;
            public int ActiveScopeCount { get; private set; }
            public int PushCount { get; private set; }
            public int DisposeCount { get; private set; }

            public bool IsContextEnabled(InputContext context)
            {
                return _contexts.IsContextEnabled(context);
            }

            public bool ContainsContext(InputContext context)
            {
                return _contexts.Contains(context);
            }

            public void SetContext(InputContext context)
            {
                _contexts.Set(context);
            }

            public IDisposable PushContext(
                InputContext context,
                InputContextPolicy policy = InputContextPolicy.Exclusive)
            {
                IDisposable scope = _contexts.Push(context, policy);
                PushCount++;
                ActiveScopeCount++;
                return new Scope(this, scope);
            }

            private void DisposeScope(IDisposable scope)
            {
                scope?.Dispose();
                ActiveScopeCount--;
                DisposeCount++;
            }

            private sealed class Scope : IDisposable
            {
                private TrackingInputProvider _owner;
                private IDisposable _scope;

                public Scope(TrackingInputProvider owner, IDisposable scope)
                {
                    _owner = owner;
                    _scope = scope;
                }

                public void Dispose()
                {
                    TrackingInputProvider owner = _owner;
                    if (owner == null)
                        return;

                    IDisposable scope = _scope;
                    _owner = null;
                    _scope = null;
                    owner.DisposeScope(scope);
                }
            }
        }

private sealed class ScriptedInputService : InputService
        {
            private static readonly FieldInfo SnapshotField =
                typeof(InputService).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly FieldInfo FrameField =
                typeof(InputService).GetField("_frame", BindingFlags.Instance | BindingFlags.NonPublic);

            public void SetSnapshotForTests(InputSnapshot snapshot, long lastCommandFrame)
            {
                Assert.NotNull(SnapshotField);
                Assert.NotNull(FrameField);
                SnapshotField.SetValue(this, snapshot);
                FrameField.SetValue(this, lastCommandFrame + 1L);
            }

            protected override InputActionAsset CreateRuntimeActions(out bool ownsActions)
            {
                ownsActions = false;
                return null;
            }
        }

private sealed class FixedAbilityHotbarInputSource : ICharacterTestAbilityHotbarInputSource
        {
            private readonly int _slot;

            public FixedAbilityHotbarInputSource(int slot)
            {
                _slot = slot;
            }

            public int ReadCount { get; private set; }

            public int ReadPressedSlot()
            {
                ReadCount++;
                return _slot;
            }
        }

private sealed class FixedInputProvider : IInputProvider
        {
            public FixedInputProvider(InputSnapshot snapshot)
            {
                Snapshot = snapshot;
                Commands = new InputCommandQueue();
            }

            public InputSnapshot Snapshot { get; }
            public InputCommandQueue Commands { get; }
            public InputContext CurrentContext => InputContext.Gameplay;

            public bool IsContextEnabled(InputContext context)
            {
                return context == InputContext.Gameplay;
            }

            public bool ContainsContext(InputContext context)
            {
                return context == InputContext.Gameplay;
            }

            public void SetContext(InputContext context)
            {
            }

            public IDisposable PushContext(
                InputContext context,
                InputContextPolicy policy = InputContextPolicy.Exclusive)
            {
                return NoopDisposable.Instance;
            }
        }

private sealed class FixedMovementBasisProvider : ICharacterTestCharacterMovementBasisProvider
        {
            public FixedMovementBasisProvider(CharacterTestCharacterMovementBasis basis)
            {
                CurrentMovementBasis = basis;
            }

            public CharacterTestCharacterMovementBasis CurrentMovementBasis { get; }
        }

private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose()
            {
            }
        }
}
}
