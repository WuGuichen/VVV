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

[Test]
        public void CameraBridge_MissingTarget_DoesNotWarnBeforeCharactersExist()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var host = new GameObject("CharacterTest Camera Bridge Missing Target Startup Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);

                InvokePrivate(
                    bridge,
                    "BuildTargetSnapshots",
                    (object)null);

                Assert.IsFalse(GetPrivateField<bool>(bridge, "_missingTargetLogged"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_DefaultsToExternalPresentationTick()
        {
            var host = new GameObject("CharacterTest Camera Bridge External Tick Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();

                Assert.IsTrue(bridge.UseExternalPresentationTick);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_CameraModeShortcut_EnqueuesAndAppliesNextMode()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var host = new GameObject("CharacterTest Camera Mode Shortcut Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);
                SetPrivateField(
                    bridge,
                    "_inputProvider",
                    new FixedInputProvider(CreateCameraLookSnapshot(
                        Vector2.zero,
                        cameraModeNextPressed: true)));

                Assert.AreEqual(CharacterTestCameraMode.ThirdPersonControlled, slice.CameraMode);
                InvokePrivate(bridge, "UpdateCursorAndViewInput", (object)null);
                slice.Tick(0d);

                Assert.AreEqual(CharacterTestCameraMode.MultiTargetAuto, slice.CameraMode);
                Assert.That(
                    slice.RecentRuntimeEvents,
                    Has.Some.Matches<CharacterTestRuntimeEvent>(evt =>
                        evt.Type == CharacterTestRuntimeEventType.CameraModeChanged));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_UiCursorReleaseScope_IsNestedAndRespectsDebugRelease()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var host = new GameObject("CharacterTest Camera Bridge UI Cursor Scope Test");
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);
                Assert.IsTrue(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual(CursorLockMode.Locked, cursorDriver.LockState);
                Assert.IsFalse(cursorDriver.Visible);

                IDisposable first = bridge.PushCursorReleasedByUi();
                IDisposable second = bridge.PushCursorReleasedByUi();

                Assert.IsFalse(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.UI.Legacy", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);
                Assert.IsTrue(cursorDriver.Visible);

                bridge.RequestCursorReleasedByDebugUi();
                first.Dispose();

                Assert.IsFalse(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Debug", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);

                second.Dispose();

                Assert.IsFalse(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Debug", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);
                Assert.IsTrue(cursorDriver.Visible);

                bridge.RequestCursorCapturedByDebugUi();

                Assert.IsTrue(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Camera", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.Locked, cursorDriver.LockState);
                Assert.IsFalse(cursorDriver.Visible);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                cursorController.Dispose();
            }
        }

        [Test]
        public void CameraBridge_FocusGainRestoresCursorAfterEditorUnlock()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var host = new GameObject("CharacterTest Camera Bridge Focus Cursor Test");
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);
                cursorDriver.OverrideState(visible: false, CursorLockMode.None);

                Assert.IsFalse(bridge.CursorCaptured);

                host.SendMessage("OnApplicationFocus", true);

                Assert.IsTrue(bridge.CursorCaptured);
                Assert.AreEqual(CursorLockMode.Locked, cursorDriver.LockState);
                Assert.IsFalse(cursorDriver.Visible);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_MultiTargetAuto_DoesNotCreateCapturedCursorScope()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var host = new GameObject("CharacterTest Camera Bridge Auto Cursor Scope Test");
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);
                Assert.IsTrue(bridge.CursorCaptured);

                bridge.CameraMode = CharacterTestCameraMode.MultiTargetAuto;

                Assert.IsFalse(bridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("default", cursorController.Current.Owner);
                Assert.IsTrue(cursorDriver.Visible);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                cursorController.Dispose();
            }
        }

        [Test]
        public void CameraBridge_RuntimeAiLabMultiTargetAuto_UsesSafeAreaComposition()
        {
            var host = new GameObject("Runtime AI Lab Camera Safe Area Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.ConfigureRuntimeAiLabAutoFraming(true);
                bridge.CameraMode = CharacterTestCameraMode.MultiTargetAuto;
                SetPrivateField(bridge, "_presentationDeltaTime", 1f / 60f);

                var profile = new MxCameraProfileDefinition();
                InvokePrivate(bridge, "UpdateProfile", null, profile);

                Assert.AreEqual(MxCameraMode.GroupFollowPerspective, profile.Mode);
                Assert.AreEqual(8f, profile.MinDistance, 0.001f);
                Assert.AreEqual(90f, profile.MaxDistance, 0.001f);
                Assert.AreEqual(2.5f, profile.TargetPadding, 0.001f);
                Assert.AreEqual(1.68f, profile.LocalOffset.X, 0.001f);
                Assert.AreEqual(-2.76f, profile.LocalOffset.Y, 0.001f);
                Assert.AreEqual(0f, profile.LocalOffset.Z, 0.001f);
                Assert.AreEqual(0f, profile.WorldOffset.X, 0.001f);
                Assert.AreEqual(1.45f, profile.WorldOffset.Y, 0.001f);
                Assert.AreEqual(0f, profile.WorldOffset.Z, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CameraBridge_LockedTargetProfile_SynchronizesControlledViewForUnlock()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var host = new GameObject("CharacterTest Camera Bridge Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);
                SetPrivateField(bridge, "_controlledYaw", 180f);
                SetPrivateField(bridge, "_controlledPitch", 65f);

                var profile = new MxCameraProfileDefinition();
                InvokePrivate(
                    bridge,
                    "UpdateProfile",
                    primaryTarget,
                    profile);

                float controlledYaw = GetPrivateField<float>(bridge, "_controlledYaw");
                float controlledPitch = GetPrivateField<float>(bridge, "_controlledPitch");

                Assert.That(Mathf.DeltaAngle(profile.Yaw, controlledYaw), Is.EqualTo(0f).Within(0.001f));
                Assert.That(controlledPitch, Is.EqualTo(profile.Pitch).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_LockedTargetInput_DoesNotAccumulateFreeLookRotation()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var host = new GameObject("CharacterTest Camera Bridge Input Test");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);
                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(new InputSnapshot(
                    Vector2.zero,
                    new Vector2(500f, -500f),
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
                    debugTogglePressed: false)));
                SetPrivateField(bridge, "_controlledYaw", 12f);
                SetPrivateField(bridge, "_controlledPitch", 24f);

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(12f, GetPrivateField<float>(bridge, "_controlledYaw"));
                Assert.AreEqual(24f, GetPrivateField<float>(bridge, "_controlledPitch"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_LockedTargetLookBelowThreshold_DoesNotSubmitViewSwitch()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var cameraHost = new GameObject("CharacterTest Camera Target Lock Threshold Camera");
            var bridgeHost = new GameObject("CharacterTest Camera Target Lock Threshold Bridge");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                UnityEngine.Camera camera = CreateTargetLockSwitchTestCamera(cameraHost);
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, camera, cursorController);
                SetPrivateField(bridge, "_mouseYawSensitivity", 1f);
                SetPrivateField(bridge, "_targetLockViewSwitchThresholdDegrees", 6f);
                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(new Vector2(2f, 0f))));

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_LockedTargetLookDeadzoneDoesNotAccumulateViewSwitch()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var cameraHost = new GameObject("CharacterTest Camera Target Lock Deadzone Camera");
            var bridgeHost = new GameObject("CharacterTest Camera Target Lock Deadzone Bridge");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                UnityEngine.Camera camera = CreateTargetLockSwitchTestCamera(cameraHost);
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, camera, cursorController);
                SetPrivateField(bridge, "_mouseYawSensitivity", 1f);
                SetPrivateField(bridge, "_targetLockViewSwitchThresholdDegrees", 0.3f);
                SetPrivateField(bridge, "_targetLockViewSwitchInputDeadzoneDegrees", 0.1f);
                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(new Vector2(0.05f, 0f))));

                for (int i = 0; i < 12; i++)
                {
                    InvokePrivate(
                        bridge,
                        "UpdateCursorAndViewInput",
                        primaryTarget);
                }

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_DeadLockedTargetDoesNotSubmitViewSwitchAndRestoresFreeLook()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            CharacterTestCharacterEntity lockedTarget = ResolveLockedTargetForTest(slice, primaryTarget);
            SetTargetDead(slice.CharacterRuntime, lockedTarget, slice.CurrentFrame);
            Assert.IsFalse(lockedTarget.BattleState.CanReceiveHpDamage);

            var bridgeHost = new GameObject("CharacterTest Camera Dead Target Lock Bridge");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);
                SetPrivateField(bridge, "_controlledYaw", 12f);
                SetPrivateField(bridge, "_controlledPitch", 24f);
                SetPrivateField(bridge, "_mouseYawSensitivity", 1f);
                SetPrivateField(bridge, "_mousePitchSensitivity", 1f);
                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(new Vector2(2f, 0f))));

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);
                Assert.That(GetPrivateField<float>(bridge, "_controlledYaw"), Is.EqualTo(14f).Within(0.001f));
                Assert.That(GetPrivateField<float>(bridge, "_controlledPitch"), Is.EqualTo(24f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_LockedTargetLookSelectsCandidateInViewportDirection()
        {
            AssertCameraLookSwitchesToViewportTarget(Vector2.right, "right");
            AssertCameraLookSwitchesToViewportTarget(Vector2.left, "left");
            AssertCameraLookSwitchesToViewportTarget(Vector2.up, "up");
            AssertCameraLookSwitchesToViewportTarget(Vector2.down, "down");
        }

[Test]
        public void CameraBridge_LockedTargetLookCooldownBlocksImmediateSecondSwitch()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            CharacterTestCharacterEntity lockedTarget = ResolveLockedTargetForTest(slice, primaryTarget);
            CharacterTestCharacterEntity rightTarget = SpawnCameraSwitchOpponent(slice, 2f, 1f, 4f);
            CharacterTestCharacterEntity leftTarget = SpawnCameraSwitchOpponent(slice, -2f, 1f, 4f);
            primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
            lockedTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 4f));

            var cameraHost = new GameObject("CharacterTest Camera Target Lock Cooldown Camera");
            var bridgeHost = new GameObject("CharacterTest Camera Target Lock Cooldown Bridge");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                UnityEngine.Camera camera = CreateTargetLockSwitchTestCamera(cameraHost);
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, camera, cursorController);
                ConfigureTargetLockSwitchTestBridge(bridge);

                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(Vector2.right * 2f)));
                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);
                slice.Tick(0d);
                Assert.AreEqual(rightTarget.Id.Value, slice.LockedTargetEntityId);

                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(
                    CreateCameraLookSnapshot(Vector2.left * 2f)));
                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);
                Assert.AreEqual(rightTarget.Id.Value, slice.LockedTargetEntityId);
                Assert.NotNull(leftTarget);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_FreeLookProfile_PreservesFullTurnYaw()
        {
            using var resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                AllowMissingPreloadGroupInEditor = true
            });
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources,
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity primaryTarget = SpawnLocalPlayer(slice);
            var host = new GameObject("CharacterTest Camera Bridge Full Turn Yaw Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);
                SetPrivateField(bridge, "_presentationDeltaTime", 1f / 60f);
                SetPrivateField(bridge, "_controlledYaw", 0f);

                var warmupProfile = new MxCameraProfileDefinition();
                InvokePrivate(
                    bridge,
                    "UpdateProfile",
                    primaryTarget,
                    warmupProfile);

                SetPrivateField(bridge, "_controlledYaw", 405f);
                var profile = new MxCameraProfileDefinition();
                InvokePrivate(
                    bridge,
                    "UpdateProfile",
                    primaryTarget,
                    profile);

                Assert.That(profile.Yaw, Is.EqualTo(405f).Within(0.001f));
                Assert.That(GetPrivateField<float>(bridge, "_controlledYaw"), Is.EqualTo(405f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_TargetLock_UsesSemanticTargetLockInputInsteadOfPointerButtons()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var host = new GameObject("CharacterTest Camera Bridge Target Lock Input Test");
            var cursorController = new MxCursorController(new RecordingCursorDriver());
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);

                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(new InputSnapshot(
                    Vector2.zero,
                    Vector2.zero,
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
                    clickPressed: false,
                    clickHeld: false,
                    clickReleased: false,
                    rightClickPressed: true,
                    rightClickHeld: false,
                    rightClickReleased: false)));

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(0, slice.PendingRuntimeCommandCount);

                SetPrivateField(bridge, "_inputProvider", new FixedInputProvider(new InputSnapshot(
                    Vector2.zero,
                    Vector2.zero,
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
                    clickPressed: false,
                    clickHeld: false,
                    clickReleased: false,
                    rightClickPressed: false,
                    rightClickHeld: false,
                    rightClickReleased: false,
                    middleClickPressed: false,
                    middleClickHeld: false,
                    middleClickReleased: false,
                    targetLockPressed: true)));

                InvokePrivate(
                    bridge,
                    "UpdateCursorAndViewInput",
                    primaryTarget);

                Assert.AreEqual(1, slice.PendingRuntimeCommandCount);
                slice.Tick(0d);
                Assert.IsFalse(slice.TargetLockEnabled);
                Assert.AreEqual(0, slice.LockedTargetEntityId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                cursorController.Dispose();
            }
        }

[Test]
        public void CameraBridge_PrimaryTargetVerticalDeadZone_AllowsJumpBeforeCameraYMoves()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity primaryTarget = SpawnLocalPlayer(slice);
            var host = new GameObject("CharacterTest Camera Bridge Vertical Dead Zone Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);
                SetPrivateField(bridge, "_verticalDeadZoneUp", 1f);
                SetPrivateField(bridge, "_verticalDeadZoneDown", 0.4f);

                primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f));
                primaryTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
                Vector3 baseline = InvokePrivate<Vector3>(
                    bridge,
                    "ResolveCameraTargetPosition",
                    primaryTarget,
                    true,
                    new Vector3(0f, 0f, 0f));
                Assert.AreEqual(0f, baseline.y, 0.0001f);

                primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0.6f, 0f));
                primaryTarget.SetMotionState(5f, Vector2.zero, isGrounded: false);
                Vector3 smallJump = InvokePrivate<Vector3>(
                    bridge,
                    "ResolveCameraTargetPosition",
                    primaryTarget,
                    true,
                    new Vector3(0f, 0.6f, 0f));
                Assert.AreEqual(0f, smallJump.y, 0.0001f);

                primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1.4f, 0f));
                Vector3 highJump = InvokePrivate<Vector3>(
                    bridge,
                    "ResolveCameraTargetPosition",
                    primaryTarget,
                    true,
                    new Vector3(0f, 1.4f, 0f));
                Assert.AreEqual(0.4f, highJump.y, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_LockedTargetObstructionFocusOverride_PreventsFarLockGroundHit()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateCameraGroundObstructionPhysicsRuntime(
                obstacleLayer: 2,
                obstacleMaskLayer: 2);
            using var slice = CreateSliceWithLockedTarget(
                out CharacterTestCharacterEntity primaryTarget,
                physicsRuntime);
            CharacterTestCharacterEntity lockedTarget = slice.CharacterEntities.First(
                entity => entity.Id.Value == slice.LockedTargetEntityId);
            primaryTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
            lockedTarget.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 16f, 180f));

            var cameraHost = new GameObject("CharacterTest Camera Far Lock Camera");
            var bridgeHost = new GameObject("CharacterTest Camera Far Lock Bridge");
            try
            {
                UnityEngine.Camera camera = cameraHost.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge bridge =
                    bridgeHost.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance, camera);

                bridge.TickPresentation(1f / 60f, new RuntimeFrame(1));

                Vector3? obstructionFocus = InvokePrivate<Vector3?>(
                    bridge,
                    "ResolveCameraObstructionFocusOverride");

                Assert.Less(bridge.CurrentState.FocusCenter.Y, 1f);
                Assert.IsTrue(obstructionFocus.HasValue);
                Assert.AreEqual(primaryTarget.Transform.Y + 1.45f, obstructionFocus.Value.y, 0.0001f);
                Assert.IsFalse(bridge.LastObstructionResult.IsObstructed);
                Assert.Greater(camera.transform.position.y, 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }

[Test]
        public void CameraBridge_LockedTargetObstructionFocusOverride_ClearsForMultiTargetAuto()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget);
            var host = new GameObject("CharacterTest Camera Obstruction Focus Override Clear Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                bridge.Bind(slice, NullRuntimeLogger.Instance);

                var profile = new MxCameraProfileDefinition();
                InvokePrivate(
                    bridge,
                    "UpdateProfile",
                    primaryTarget,
                    profile);
                Vector3? lockedFocus = InvokePrivate<Vector3?>(
                    bridge,
                    "ResolveCameraObstructionFocusOverride");

                RuntimeCommandValidationResult unlockResult = slice.EnqueueRuntimeCommand(
                    CharacterTestRuntimeCommandFactory.SetTargetLock(
                        slice.CurrentFrame,
                        CharacterTestRuntimeCommandSources.System,
                        enabled: false,
                        targetEntityId: 0,
                        new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0016)));
                Assert.IsTrue(unlockResult.Success, unlockResult.Error.Message);
                slice.Tick(0d);
                InvokePrivate(
                    bridge,
                    "UpdateProfile",
                    primaryTarget,
                    profile);
                Vector3? unlockedFocus = InvokePrivate<Vector3?>(
                    bridge,
                    "ResolveCameraObstructionFocusOverride");

                bridge.CameraMode = CharacterTestCameraMode.MultiTargetAuto;
                Vector3? clearedFocus = InvokePrivate<Vector3?>(
                    bridge,
                    "ResolveCameraObstructionFocusOverride");

                Assert.IsTrue(lockedFocus.HasValue);
                Assert.IsFalse(unlockedFocus.HasValue);
                Assert.IsFalse(clearedFocus.HasValue);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraObstructionResolver_FrameworkPhysicsAabb_MovesCameraInFrontOfObstacle()
        {
            CharacterTestCharacterPhysicsRuntime runtime = CreateCameraObstructionPhysicsRuntime(
                obstacleLayer: 2,
                obstacleMaskLayer: 2);
            var resolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(() => runtime);
            MxCameraUnityObstructionContext context = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 1.45f, -3f),
                recoverySharpness: 12f);

            MxCameraUnityObstructionResult result = resolver.Resolve(in context);

            Assert.IsTrue(result.IsObstructed);
            Assert.Less(result.ResolvedDistance, result.IdealDistance);
            Assert.Greater(result.Position.z, -3f);
            Assert.AreEqual("CAM_OBSTRUCTION_FRAMEWORK_PHYSICS_AABB", result.Code);
        }

[Test]
        public void CameraObstructionResolver_FrameworkPhysicsAabb_FocusOverrideAvoidsGroundHitFromLowCoreFocus()
        {
            CharacterTestCharacterPhysicsRuntime runtime = CreateCameraGroundObstructionPhysicsRuntime(
                obstacleLayer: 2,
                obstacleMaskLayer: 2);
            MxCameraUnityObstructionContext context = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 2.98f, -3.125f),
                recoverySharpness: 12f,
                focusCenter: new Vector3(0f, -0.334f, 6.31f));
            var lowFocusResolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(() => runtime);
            var overrideResolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(
                () => runtime,
                () => new Vector3(0f, 1.45f, 0f));

            MxCameraUnityObstructionResult lowFocusResult = lowFocusResolver.Resolve(in context);
            MxCameraUnityObstructionResult overrideResult = overrideResolver.Resolve(in context);

            Assert.IsTrue(lowFocusResult.IsObstructed);
            Assert.Less(lowFocusResult.Position.y, 1f);
            Assert.IsFalse(overrideResult.IsObstructed);
            Assert.AreEqual(context.DesiredPosition, overrideResult.Position);
        }

[Test]
        public void CameraObstructionResolver_FrameworkPhysicsAabb_NoObstacleKeepsIdealCamera()
        {
            CharacterTestCharacterPhysicsRuntime runtime = CreateCameraObstructionPhysicsRuntime(
                obstacleLayer: 3,
                obstacleMaskLayer: 2);
            var resolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(() => runtime);
            MxCameraUnityObstructionContext context = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 1.45f, -3f),
                recoverySharpness: 12f);

            MxCameraUnityObstructionResult result = resolver.Resolve(in context);

            Assert.IsFalse(result.IsObstructed);
            Assert.AreEqual(new Vector3(0f, 1.45f, -3f), result.Position);
            Assert.AreEqual(result.IdealDistance, result.ResolvedDistance, 0.001f);
        }

[Test]
        public void CameraObstructionResolver_FrameworkPhysicsAabb_RecoversDistanceSmoothlyAfterClear()
        {
            CharacterTestCharacterPhysicsRuntime runtime = CreateCameraObstructionPhysicsRuntime(
                obstacleLayer: 2,
                obstacleMaskLayer: 2);
            var resolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(() => runtime);
            MxCameraUnityObstructionContext blocked = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 1.45f, -3f),
                recoverySharpness: 1f);
            MxCameraUnityObstructionResult blockedResult = resolver.Resolve(in blocked);
            runtime = CreateCameraObstructionPhysicsRuntime(
                obstacleLayer: 3,
                obstacleMaskLayer: 2);
            MxCameraUnityObstructionContext clear = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 1.45f, -3f),
                recoverySharpness: 1f);

            MxCameraUnityObstructionResult clearResult = resolver.Resolve(in clear);

            Assert.IsTrue(blockedResult.IsObstructed);
            Assert.IsFalse(clearResult.IsObstructed);
            Assert.Greater(clearResult.ResolvedDistance, blockedResult.ResolvedDistance);
            Assert.Less(clearResult.ResolvedDistance, clearResult.IdealDistance);
        }

[Test]
        public void CameraObstructionResolver_FrameworkPhysicsAabb_ObstacleMaskIgnoresDynamicCharacterLayer()
        {
            CharacterTestCharacterPhysicsRuntime runtime = CreateCameraObstructionPhysicsRuntime(
                obstacleLayer: CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                obstacleMaskLayer: 2);
            var resolver = new CharacterTestFrameworkPhysicsCameraObstructionResolver(() => runtime);
            MxCameraUnityObstructionContext context = CreateCameraObstructionContext(
                desiredPosition: new Vector3(0f, 1.45f, -3f),
                recoverySharpness: 12f);

            MxCameraUnityObstructionResult result = resolver.Resolve(in context);

            Assert.IsFalse(result.IsObstructed);
            Assert.AreEqual(new Vector3(0f, 1.45f, -3f), result.Position);
        }

[Test]
        public void CameraBridge_DefaultObstructionMode_UsesFrameworkPhysicsAabb()
        {
            var host = new GameObject("CharacterTest Camera Bridge Obstruction Mode Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();

                Assert.AreEqual(MxCameraUnityObstructionMode.FrameworkPhysicsAabb, bridge.ObstructionMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

[Test]
        public void CameraBridge_DefaultShakeMode_UsesProcedural()
        {
            var host = new GameObject("CharacterTest Camera Bridge Shake Mode Test");
            try
            {
                CharacterTestThirdPersonCameraRigBridge bridge =
                    host.AddComponent<CharacterTestThirdPersonCameraRigBridge>();

                Assert.AreEqual(MxCameraUnityShakeMode.Procedural, bridge.ShakeMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
}
}
