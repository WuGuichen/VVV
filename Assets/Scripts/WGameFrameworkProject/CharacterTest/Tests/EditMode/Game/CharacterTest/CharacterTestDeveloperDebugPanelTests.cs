using System;
using System.Reflection;
using MxFramework.DebugUI;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestDeveloperDebugPanelTests
    {
        [Test]
        public void View_BindUpdatesCollapsedSummaryAndToolbarState()
        {
            var host = new VisualElement();
            var view = new CharacterTestDeveloperDebugView();
            view.Build(host);
            view.SetAnchor(CharacterTestDeveloperDebugPanelAnchor.BottomRight);

            view.Bind(CreateSnapshot(refreshPaused: true, cursorCaptured: true), 3, DebugUiVisibility.Expanded);
            view.BindGuidanceSelection(
                true,
                new CharacterTestGuidanceSelectionSnapshot(
                    "target-unlock",
                    selectedStepIndex: 4,
                    stepCount: 6,
                    completed: false,
                    autoAdvanceRemainingSeconds: 0f));

            Assert.That(host.Q<Label>(CharacterTestDeveloperDebugView.CollapsedSummaryName).text, Does.Contain("Camera/Input"));
            Assert.That(host.Q<Label>(CharacterTestDeveloperDebugView.CollapsedSummaryName).text, Does.Contain("mouse captured"));
            Assert.AreEqual(
                "Resume Refresh",
                host.Q<Button>(CharacterTestDeveloperDebugView.RefreshPauseButtonName).text);
            Assert.AreEqual("Move: BR", host.Q<Button>(CharacterTestDeveloperDebugView.AnchorButtonName).text);
            Assert.AreEqual("Free Mouse", host.Q<Button>(CharacterTestDeveloperDebugView.CursorButtonName).text);
            Assert.AreEqual(
                "测试 1：锁定目标",
                host.Q<Button>(CharacterTestDeveloperDebugView.GuidanceLockButtonName).text);
            Assert.AreEqual(
                "▶ 测试 2：取消锁定",
                host.Q<Button>(CharacterTestDeveloperDebugView.GuidanceUnlockButtonName).text);
            Assert.IsNotNull(host.Q<VisualElement>(CharacterTestDeveloperDebugView.GizmoControlsName));
        }

        [Test]
        public void View_GizmoControlsBindAllOptions()
        {
            var host = new VisualElement();
            var view = new CharacterTestDeveloperDebugView();
            view.Build(host);
            CharacterTestGizmoDisplaySettings settings =
                CharacterTestGizmoDisplaySettings.Defaults.With(
                    CharacterTestGizmoDisplayOption.AnimatedHurtbox,
                    false).With(
                    CharacterTestGizmoDisplayOption.BodyPartContacts,
                    false);

            view.BindGizmoSettings(settings);
            Toggle hurtbox = host.Q<Toggle>(CharacterTestDeveloperDebugView.GizmoAnimatedHurtboxToggleName);
            Toggle contacts = host.Q<Toggle>(CharacterTestDeveloperDebugView.GizmoBodyPartContactsToggleName);
            Toggle hardRoot = host.Q<Toggle>(CharacterTestDeveloperDebugView.GizmoHardRootToggleName);
            Assert.IsFalse(hurtbox.value);
            Assert.IsFalse(contacts.value);
            Assert.IsTrue(hardRoot.value);
        }

        [Test]
        public void GizmoPreferences_RoundTripAllOptionsAndApplyLayerMask()
        {
            var store = new FakeGizmoPreferenceStore();
            var preferences = new CharacterTestGizmoPreferences(store);
            CharacterTestGizmoDisplaySettings original = CharacterTestGizmoRuntimeDisplay.Current;
            CharacterTestGizmoDisplaySettings expected = CharacterTestGizmoDisplaySettings.Defaults
                .With(CharacterTestGizmoDisplayOption.RuntimePhysicsWorld, false)
                .With(CharacterTestGizmoDisplayOption.BodyPresence, false)
                .With(CharacterTestGizmoDisplayOption.ActiveTrace, false);
            try
            {
                preferences.Save(expected);
                CharacterTestGizmoDisplaySettings loaded = preferences.Load();
                CharacterTestGizmoRuntimeDisplay.Apply(loaded);

                Assert.IsFalse(loaded.RuntimePhysicsWorld);
                Assert.IsTrue(loaded.HardRoot);
                Assert.IsFalse(loaded.BodyPresence);
                Assert.IsTrue(loaded.AnimatedHurtbox);
                Assert.IsFalse(loaded.ActiveTrace);
                Assert.IsTrue(loaded.BodyPartContacts);
                Assert.AreEqual(
                    CharacterTestLargeCharacterPresenceDebugLayer.HardRoot
                        | CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox,
                    CharacterTestLargeCharacterPresenceDebug.EnabledLayers);
                Assert.AreEqual(1, store.SaveCount);
            }
            finally
            {
                CharacterTestGizmoRuntimeDisplay.Apply(original);
            }
        }

        [Test]
        public void DamageZoneDiagnosticsCard_ProjectsAuthorityLockFieldsForGameView()
        {
            CharacterTestDeveloperDebugCard card =
                CharacterTestDeveloperDebugSnapshotBuilder.CreateDamageZoneDiagnosticsCard(
                    new[]
                    {
                        new CharacterTestDamageZoneLockStateSnapshot
                        {
                            SourceEntityId = 7,
                            TargetGameplayEntityIndex = 11,
                            TargetGameplayEntityGeneration = 3,
                            TargetCombatEntityId = 13,
                            ResolvedActionRevision = 17,
                            ZoneId = CharacterTestAuthoredContentIds.D400_Id152,
                            HitZoneId = CharacterTestAuthoredContentIds.D400_Id104,
                            LogicalHurtboxId = CharacterTestAuthoredContentIds.D400_Id150,
                            OpportunityLevel = (int)CharacterTestAttackOpportunityLevel.CurrentPose,
                            EarliestHitTick = 4,
                            BindingRevision = 101,
                            HurtboxRevision = 103,
                            WorldRevision = 107,
                            ZoneRevision = 109,
                            SelectedFrame = 19,
                            SwitchReason = (int)CharacterTestDamageZoneSwitchReason.PreviousLockKept
                        }
                    });

            Assert.AreEqual("DamageZone Selection", card.Title);
            string projection = string.Empty;
            for (int i = 0; i < card.Lines.Count; i++)
                projection += card.Lines[i].Name + "=" + card.Lines[i].Value + "\n";
            Assert.That(projection, Does.Contain("DamageZone=152"));
            Assert.That(projection, Does.Contain("HitZone=104"));
            Assert.That(projection, Does.Contain("Hurtbox=150"));
            Assert.That(projection, Does.Contain("Opportunity=CurrentPose / earliest tick 4"));
            Assert.That(projection, Does.Contain("Decision=PreviousLockKept @ frame 19"));
            Assert.That(projection, Does.Contain("Revisions=binding=101 zone=109 hurtbox=103 world=107"));
        }

        [Test]
        public void EngagementAnchorDiagnosticsCard_ProjectsSelectionPathAndReservation()
        {
            CharacterTestDeveloperDebugCard card =
                CharacterTestDeveloperDebugSnapshotBuilder.CreateEngagementAnchorDiagnosticsCard(
                    new[]
                    {
                        new CharacterTestEngagementDiagnosticSnapshot
                        {
                            SourceEntityId = 7,
                            TargetEntityId = 11,
                            ResolvedActionRevision = 17,
                            PlannerFrame = 19,
                            Status = CharacterTestEngagementCandidateStatus.Success,
                            ZoneId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0089,
                            AnchorId = global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0007,
                            CandidateXMillimeters = 1200,
                            CandidateYMillimeters = 0,
                            CandidateZMillimeters = -800,
                            CandidateYawMilliDegrees = 90000,
                            PathCostMillimeters = 2400,
                            HasNextWaypoint = true,
                            NextWaypointXMillimeters = 600,
                            NextWaypointYMillimeters = 0,
                            NextWaypointZMillimeters = -400,
                            ReservationCount = 1,
                            Capacity = 2,
                            CapacitySlot = 0,
                            HasReservation = true
                        }
                    });

            Assert.AreEqual("Engagement Anchor", card.Title);
            string projection = string.Empty;
            for (int i = 0; i < card.Lines.Count; i++)
                projection += card.Lines[i].Name + "=" + card.Lines[i].Value + "\n";
            Assert.That(projection, Does.Contain("Result=Success"));
            Assert.That(projection, Does.Contain("Anchor=1000007"));
            Assert.That(projection, Does.Contain("DamageZone=1000089"));
            Assert.That(projection, Does.Contain("CandidatePose=(1200, 0, -800) mm yaw=90000 mdeg"));
            Assert.That(projection, Does.Contain("Path Cost=2400 mm"));
            Assert.That(projection, Does.Contain("Next Waypoint=(600, 0, -400) mm"));
            Assert.That(projection, Does.Contain("Reservation=1/2 slot=0"));
        }

        [Test]
        public void EngagementAnchorDiagnosticsCard_ProjectsStableRejectionReason()
        {
            CharacterTestDeveloperDebugCard card =
                CharacterTestDeveloperDebugSnapshotBuilder.CreateEngagementAnchorDiagnosticsCard(
                    new[]
                    {
                        new CharacterTestEngagementDiagnosticSnapshot
                        {
                            SourceEntityId = 8,
                            TargetEntityId = 12,
                            ResolvedActionRevision = 18,
                            PlannerFrame = 20,
                            Status = CharacterTestEngagementCandidateStatus.CapacityFull
                        }
                    });

            string projection = string.Empty;
            for (int i = 0; i < card.Lines.Count; i++)
                projection += card.Lines[i].Name + "=" + card.Lines[i].Value + "\n";
            Assert.That(projection, Does.Contain("Result=CapacityFull"));
            Assert.That(projection, Does.Not.Contain("Anchor="));
        }

        [Test]
        public void Panel_InputShortcutsToggleVisibilityAndNavigateTabs()
        {
            var input = new FakeInputProvider();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: input,
                enableCharacterSimulation: false);
            var gameObject = new GameObject("CharacterTest Debug Panel Shortcut Test");
            CharacterTestDeveloperDebugPanel panel = null;
            try
            {
                panel = gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
                panel.Configure(
                    slice,
                    () => false,
                    initialVisibility: DebugUiVisibility.Hidden,
                    showOnStart: true,
                    logger: NullRuntimeLogger.Instance,
                    inputProvider: input);

                input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
                InvokeUpdate(panel);
                Assert.AreEqual(DebugUiVisibility.Collapsed, panel.Visibility);

                input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
                InvokeUpdate(panel);
                Assert.AreEqual(DebugUiVisibility.Expanded, panel.Visibility);

                input.Enqueue(new InputCommand(0, 1, InputIntent.DebugCycle));
                InvokeUpdate(panel);
                Assert.AreEqual(1, panel.ActiveTab);
                Assert.AreEqual(DebugUiVisibility.Expanded, panel.Visibility);
            }
            finally
            {
                panel?.Release();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Panel_DebugStepRefreshesWhenPaused()
        {
            var input = new FakeInputProvider();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: input,
                enableCharacterSimulation: false);
            var gameObject = new GameObject("CharacterTest Debug Panel Step Test");
            CharacterTestDeveloperDebugPanel panel = null;
            try
            {
                panel = gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
                panel.Configure(
                    slice,
                    () => false,
                    initialVisibility: DebugUiVisibility.Expanded,
                    showOnStart: true,
                    logger: NullRuntimeLogger.Instance,
                    inputProvider: input);
                SetPrivateField(panel, "_refreshPaused", true);
                SetPrivateField(panel, "_nextRefreshTime", 99999f);

                input.Enqueue(new InputCommand(0, 1, InputIntent.DebugStep));
                InvokeUpdate(panel);

                Assert.AreEqual(1, panel.LastInputResult.HandledCommandCount);
                Assert.IsTrue(panel.RefreshPaused);
                Assert.Less(GetPrivateField<float>(panel, "_nextRefreshTime"), 99999f);
            }
            finally
            {
                panel?.Release();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Panel_ReloadCharacterActionsUsesConfiguredRuntimeSession()
        {
            var input = new FakeInputProvider();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: input,
                enableCharacterSimulation: false);
            var gameObject = new GameObject("CharacterTest Debug Panel Action Reload Test");
            CharacterTestDeveloperDebugPanel panel = null;
            try
            {
                panel = gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
                panel.Configure(
                    slice,
                    () => false,
                    initialVisibility: DebugUiVisibility.Expanded,
                    showOnStart: true,
                    logger: NullRuntimeLogger.Instance,
                    inputProvider: input);

                Assert.IsTrue(panel.TryReloadCharacterActions(out CharacterTestCharacterActionReloadResult result));
                Assert.IsTrue(result.Success, result.Message);

                panel.Release();
                Assert.IsFalse(panel.TryReloadCharacterActions(out _));
            }
            finally
            {
                panel?.Release();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Panel_AnchorCyclesThroughConfiguredPresets()
        {
            var input = new FakeInputProvider();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: input,
                enableCharacterSimulation: false);
            var gameObject = new GameObject("CharacterTest Debug Panel Anchor Test");
            CharacterTestDeveloperDebugPanel panel = null;
            try
            {
                panel = gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
                panel.Configure(
                    slice,
                    () => false,
                    initialVisibility: DebugUiVisibility.Expanded,
                    showOnStart: true,
                    logger: NullRuntimeLogger.Instance,
                    inputProvider: input);

                Assert.AreEqual(CharacterTestDeveloperDebugPanelAnchor.TopRight, panel.Anchor);
                InvokePrivate(panel, "CycleAnchor");
                Assert.AreEqual(CharacterTestDeveloperDebugPanelAnchor.TopLeft, panel.Anchor);
                InvokePrivate(panel, "CycleAnchor");
                Assert.AreEqual(CharacterTestDeveloperDebugPanelAnchor.BottomRight, panel.Anchor);
            }
            finally
            {
                panel?.Release();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Panel_ReenableKeepsInputAdapterUsable()
        {
            var input = new FakeInputProvider();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                inputProvider: input,
                enableCharacterSimulation: false);
            var gameObject = new GameObject("CharacterTest Debug Panel Reenable Test");
            CharacterTestDeveloperDebugPanel panel = null;
            try
            {
                panel = gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
                panel.Configure(
                    slice,
                    () => false,
                    initialVisibility: DebugUiVisibility.Hidden,
                    showOnStart: true,
                    logger: NullRuntimeLogger.Instance,
                    inputProvider: input);

                InvokePrivate(panel, "OnDisable");
                InvokePrivate(panel, "OnEnable");
                input.Enqueue(new InputCommand(0, 1, InputIntent.ToggleHud));
                InvokeUpdate(panel);

                Assert.AreEqual(DebugUiVisibility.Collapsed, panel.Visibility);
            }
            finally
            {
                panel?.Release();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static CharacterTestDeveloperDebugSnapshot CreateSnapshot(bool refreshPaused, bool cursorCaptured)
        {
            return new CharacterTestDeveloperDebugSnapshot(
                true,
                42L,
                refreshPaused,
                2,
                "#1",
                "light_attack",
                "locked entity=2",
                true,
                cursorCaptured,
                new[]
                {
                    new CharacterTestDeveloperDebugTabSnapshot(
                        CharacterTestDeveloperDebugSnapshot.TabNames[0],
                        Array.Empty<CharacterTestDeveloperDebugCard>())
                });
        }

        private static void InvokeUpdate(CharacterTestDeveloperDebugPanel panel)
        {
            InvokePrivate(panel, "Update");
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        private sealed class FakeGizmoPreferenceStore : ICharacterTestGizmoPreferenceStore
        {
            private readonly System.Collections.Generic.Dictionary<string, int> _values =
                new System.Collections.Generic.Dictionary<string, int>();

            internal int SaveCount { get; private set; }

            public int GetInt(string key, int fallback) =>
                _values.TryGetValue(key, out int value) ? value : fallback;

            public void SetInt(string key, int value) => _values[key] = value;
            public void Save() => SaveCount++;
        }
    }
}
