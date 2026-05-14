using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Demo.CombatAnimation;
using MxFramework.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Combat.GameplayBridge
{
    public sealed class CombatAnimationDemoPlayableBridgeTests
    {
        private const string ScenePath = "Assets/Scenes/CombatAnimationDemo.unity";
        private const string PanelSettingsPath = "Assets/UI/MxFramework/CombatAnimationPanelSettings.asset";
        private const string UxmlPath = "Assets/UI/MxFramework/CombatAnimationHud.uxml";
        private const string UssPath = "Assets/UI/MxFramework/CombatAnimationHud.uss";

        private GameObject _hudObject;

        [TearDown]
        public void TearDown()
        {
            if (_hudObject != null)
            {
                Object.DestroyImmediate(_hudObject);
                _hudObject = null;
            }
        }

        [Test]
        public void SceneRoot_BindsUidocumentPanelSettingsUxmlAndDemoCompositionRoot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("CombatAnimationDemoRoot");
            Assert.IsNotNull(root);
            Assert.AreEqual(1, Object.FindObjectsByType<CombatAnimationDemoBootstrap>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(root.GetComponent<DefaultInputService>());
            Assert.IsNotNull(root.GetComponent<CombatAnimationDemoBootstrap>());
            Assert.IsNotNull(root.GetComponent<CombatAnimationHudController>());

            UIDocument document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            Assert.AreEqual(PanelSettingsPath, AssetDatabase.GetAssetPath(document.panelSettings));
            Assert.AreEqual(UxmlPath, AssetDatabase.GetAssetPath(document.visualTreeAsset));
        }

        [Test]
        public void HudUxml_ContainsCriticalVisibleTextBindings()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            Assert.IsNotNull(tree);
            Assert.IsNotNull(styleSheet);

            TemplateContainer root = tree.CloneTree();
            string[] labelNames =
            {
                "title",
                "instructions",
                "player-action",
                "player-phase",
                "player-frame",
                "player-hp",
                "dummy-hp",
                "weapon-trace",
            };

            for (int i = 0; i < labelNames.Length; i++)
            {
                Label label = root.Q<Label>(labelNames[i]);
                Assert.IsNotNull(label, labelNames[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), labelNames[i]);
            }

            Assert.IsNotNull(root.Q<VisualElement>("event-list"));
            Assert.IsNotNull(root.Q<VisualElement>("runtime-diagnostic-panel"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-action-state-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-hit-application-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-gameplay-attribute-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-bridge-map-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-runtime-hash-list"));
            Assert.IsNotNull(root.Q<VisualElement>("diagnostic-event-queue-list"));
        }

        [Test]
        public void HudController_RefreshWritesGameplayBridgeHudModelLabels()
        {
            _hudObject = new GameObject("CombatAnimationHudControllerTest");
            UIDocument document = _hudObject.AddComponent<UIDocument>();
            CombatAnimationHudController controller = _hudObject.AddComponent<CombatAnimationHudController>();
            controller.ConfigureAssets(
                document,
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath),
                AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath));

            controller.Refresh(new CombatAnimationHudModel
            {
                PlayerAction = "LightAttack",
                PlayerPhase = "Active",
                PlayerLocalFrame = "12",
                PlayerHp = "100/100",
                DummyHp = "85/100",
                WeaponTrace = "active=1 candidates=1 frame=12",
                Instructions = "WASD move | J light | K heavy | Space dodge",
                RecentEvents = new[] { "Bridge HP: Dummy 100->85 (-15)" },
                Diagnostics = new CombatRuntimeDiagnosticHudModel
                {
                    ActionStateRows = new[] { "Entity 1:1 action=1001 phase=Active localFrame=12" },
                    HitApplicationRows = new[] { "Command frame=12 entity=2:1 attribute=100 delta=-15 trace=201" },
                    GameplayAttributeRows = new[] { "Entity 2:1 attribute=100 current=85" },
                    BridgeMapRows = new[] { "Combat 2 <-> Gameplay 2:1" },
                    RuntimeHashRows = new[] { "Demo diagnostic hash frame=12 value=123", "Contributors: mxframework.gameplay.component-world" },
                    EventQueueRows = new[] { "Pending=0 type=MxFramework.Gameplay.GameplayRuntimeEvent nextSequence=1" },
                },
            });

            VisualElement root = document.rootVisualElement;
            Assert.AreEqual("LightAttack", root.Q<Label>("player-action").text);
            Assert.AreEqual("Active", root.Q<Label>("player-phase").text);
            Assert.AreEqual("12", root.Q<Label>("player-frame").text);
            Assert.AreEqual("100/100", root.Q<Label>("player-hp").text);
            Assert.AreEqual("85/100", root.Q<Label>("dummy-hp").text);
            Assert.That(root.Q<Label>("weapon-trace").text, Does.Contain("candidates=1"));
            Assert.That(root.Q<VisualElement>("event-list").Q<Label>("event-row").text, Does.Contain("Bridge HP"));
            Assert.That(root.Q<VisualElement>("diagnostic-action-state-list").Q<Label>("diagnostic-row").text, Does.Contain("action=1001"));
            Assert.That(root.Q<VisualElement>("diagnostic-hit-application-list").Q<Label>("diagnostic-row").text, Does.Contain("delta=-15"));
            Assert.That(root.Q<VisualElement>("diagnostic-runtime-hash-list").Q<Label>("diagnostic-row").text, Does.Contain("Demo diagnostic hash"));

            VisualElement hudRoot = root.Q<VisualElement>("combat-animation-hud");
            Assert.IsNotNull(hudRoot);
            Assert.Greater(hudRoot.style.backgroundColor.value.a, 0.9f);
            Assert.Greater(hudRoot.style.width.value.value, 500f);
            AssertReadableInlineStyle(root.Q<Label>("title"));
            AssertReadableInlineStyle(root.Q<Label>("instructions"));
            AssertReadableInlineStyle(root.Q<Label>("player-hp"));
            AssertReadableInlineStyle(root.Q<Label>("dummy-hp"));
            AssertReadableInlineStyle(root.Q<VisualElement>("event-list").Q<Label>("event-row"));
            AssertReadableInlineStyle(root.Q<VisualElement>("diagnostic-runtime-hash-list").Q<Label>("diagnostic-row"));
        }

        [Test]
        public void DemoInputToActionAdapter_AttackCommandStartsCombatActionWithoutHpMutation()
        {
            var registry = new CombatActionRegistry();
            var traceProvider = new CombatActionTimelineTraceProvider();
            InvokeRegisterActions(registry, traceProvider);
            var runner = new CombatActionRunner(registry);
            var poseSource = new CombatDemoPoseSource();
            var queue = new InputCommandQueue();
            var adapter = new DemoInputToActionAdapter(
                queue,
                runner,
                poseSource,
                CombatAnimationDemoIds.PlayerEntityId);
            queue.TryEnqueue(new InputCommand(
                frame: 0,
                sourceId: 7,
                InputIntent.AttackPrimary,
                InputCommandPhase.Pressed,
                traceId: "test-light"), out _);

            adapter.Tick(0, InputSnapshot.Empty, 0f, 0f);

            Assert.AreEqual(CombatAnimationDemoIds.LightAttackActionId, adapter.LastStartedActionId);
            CombatActionState? state = runner.GetActionState(CombatAnimationDemoIds.PlayerEntityId);
            Assert.IsTrue(state.HasValue);
            Assert.AreEqual(CombatAnimationDemoIds.LightAttackActionId, state.Value.ActionId);
        }

        [Test]
        public void BootstrapZeroDamageShim_ConvertsWeaponTraceCandidateDamageToDemoActionDamage()
        {
            var results = new List<HitResolveResult>
            {
                new HitResolveResult(
                    CombatAnimationDemoIds.PlayerEntityId,
                    CombatAnimationDemoIds.DummyEntityId,
                    CombatAnimationDemoIds.HeavyAttackActionId,
                    actionInstanceId: 3,
                    traceId: 201,
                    frame: new CombatFrame(20),
                    HitResolveKind.Damage,
                    damage: 0,
                    staggerFrames: 0,
                    FixVector3.Zero),
            };

            InvokeApplyDemoActionDamage(results);

            Assert.AreEqual(30, results[0].Damage);
            Assert.IsTrue(results[0].IsAcceptedDamage);
        }

        [UnityTest]
        public IEnumerator CombatAnimationDemo_PlayModeHudFallback_RendersReadableHudInGameView()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameObject rootObject = GameObject.Find("CombatAnimationDemoRoot");
            Assert.IsNotNull(rootObject);
            Assert.IsTrue(rootObject.GetComponent<CombatAnimationDemoBootstrap>().IsInitialized);

            UIDocument document = rootObject.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            VisualElement root = document.rootVisualElement;
            AssertReadableResolvedHud(root, expectedDummyHp: "100/100");
            VisualElement eventList = root.Q<VisualElement>("event-list");
            Assert.IsNotNull(eventList);
            Assert.Greater(eventList.childCount, 0);
            for (int i = 0; i < eventList.childCount; i++)
            {
                AssertReadableResolvedLabel(eventList[i] as Label, null);
            }

            yield return new ExitPlayMode();
        }

        private static void InvokeRegisterActions(
            CombatActionRegistry registry,
            CombatActionTimelineTraceProvider traceProvider)
        {
            MethodInfo method = typeof(CombatAnimationDemoBootstrap).GetMethod(
                "RegisterActions",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { registry, traceProvider });
        }

        private static void InvokeApplyDemoActionDamage(List<HitResolveResult> results)
        {
            MethodInfo method = typeof(CombatAnimationDemoBootstrap).GetMethod(
                "ApplyDemoActionDamage",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { results });
        }

        private static void AssertReadableInlineStyle(Label label)
        {
            Assert.IsNotNull(label);
            Assert.Greater(label.style.color.value.a, 0.99f, label.name);
            Assert.Greater(label.style.fontSize.value.value, 12f, label.name);
        }

        private static void AssertReadableResolvedHud(VisualElement root, string expectedDummyHp)
        {
            VisualElement hudRoot = root.Q<VisualElement>("combat-animation-hud");
            Assert.IsNotNull(hudRoot);
            Assert.AreEqual(DisplayStyle.Flex, hudRoot.resolvedStyle.display);
            Assert.Greater(hudRoot.resolvedStyle.backgroundColor.a, 0.9f);
            Assert.Greater(hudRoot.resolvedStyle.width, 300f);

            AssertReadableResolvedLabel(root.Q<Label>("title"), "Combat Animation Demo");
            AssertReadableResolvedLabel(root.Q<Label>("instructions"), "WASD");
            AssertReadableResolvedLabel(root.Q<Label>("player-action"), null);
            AssertReadableResolvedLabel(root.Q<Label>("player-phase"), null);
            AssertReadableResolvedLabel(root.Q<Label>("player-hp"), "100/100");
            AssertReadableResolvedLabel(root.Q<Label>("dummy-hp"), expectedDummyHp);
            AssertReadableResolvedLabel(root.Q<Label>("weapon-trace"), null);
            Assert.IsNotNull(root.Q<VisualElement>("runtime-diagnostic-panel"));
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-action-state-list").Q<Label>("diagnostic-row"), null);
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-hit-application-list").Q<Label>("diagnostic-row"), null);
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-gameplay-attribute-list").Q<Label>("diagnostic-row"), null);
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-bridge-map-list").Q<Label>("diagnostic-row"), null);
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-runtime-hash-list").Q<Label>("diagnostic-row"), "Demo diagnostic hash");
            AssertReadableResolvedLabel(root.Q<VisualElement>("diagnostic-event-queue-list").Q<Label>("diagnostic-row"), "Pending=");

            List<Label> panelTitles = root.Query<Label>(className: "panel-title").ToList();
            Assert.GreaterOrEqual(panelTitles.Count, 3);
            for (int i = 0; i < panelTitles.Count; i++)
            {
                AssertReadableResolvedLabel(panelTitles[i], null);
            }
        }

        private static void AssertReadableResolvedLabel(Label label, string expectedText)
        {
            Assert.IsNotNull(label);
            Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), label.name);
            if (!string.IsNullOrEmpty(expectedText))
            {
                Assert.That(label.text, Does.Contain(expectedText), label.name);
            }

            Assert.Greater(label.resolvedStyle.color.a, 0.99f, label.name);
            Assert.Greater(label.resolvedStyle.fontSize, 12f, label.name);
        }
    }
}
