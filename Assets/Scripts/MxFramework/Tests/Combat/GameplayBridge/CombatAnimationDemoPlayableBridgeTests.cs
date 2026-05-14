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
            });

            VisualElement root = document.rootVisualElement;
            Assert.AreEqual("LightAttack", root.Q<Label>("player-action").text);
            Assert.AreEqual("Active", root.Q<Label>("player-phase").text);
            Assert.AreEqual("12", root.Q<Label>("player-frame").text);
            Assert.AreEqual("100/100", root.Q<Label>("player-hp").text);
            Assert.AreEqual("85/100", root.Q<Label>("dummy-hp").text);
            Assert.That(root.Q<Label>("weapon-trace").text, Does.Contain("candidates=1"));
            Assert.That(root.Q<VisualElement>("event-list").Q<Label>("event-row").text, Does.Contain("Bridge HP"));
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
    }
}
