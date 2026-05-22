using System.Reflection;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Input;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterRuntimeInputMotionControllerTests
    {
        [Test]
        public void StepFrame_UsesInputCommandSourceAndMotionResolverToMoveTransform()
        {
            var go = new GameObject("character");
            try
            {
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController controller = go.AddComponent<CharacterRuntimeInputMotionController>();
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.up));
                controller.ConfigureInputProvider(input);

                Assert.IsTrue(controller.StepFrame());

                Assert.IsTrue(controller.IsInitialized);
                Assert.Greater(go.transform.position.z, 0f);
                Assert.AreEqual(1L, controller.CurrentFrame);
                Assert.AreEqual(710001, controller.LastMotionResult.Command.Entity.StableId);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StepFrame_AppliesGravityAndKeepsRootAbovePreviewGround()
        {
            var go = new GameObject("character");
            try
            {
                go.transform.position = new Vector3(0f, 2f, 0f);
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController controller = go.AddComponent<CharacterRuntimeInputMotionController>();
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.zero));
                controller.ConfigureInputProvider(input);

                Assert.IsTrue(controller.StepFrame());

                Assert.IsTrue(controller.UsesPhysicsWorld);
                Assert.Less(go.transform.position.y, 2f);

                for (int i = 0; i < 180; i++)
                    controller.StepFrame();

                Assert.GreaterOrEqual(go.transform.position.y, -0.01f);
                Assert.LessOrEqual(go.transform.position.y, 0.02f);
                Assert.IsTrue(controller.LastMotionResult.Grounded);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StepFrame_UsesInputJumpWithCombatMotionGravity()
        {
            var go = new GameObject("character");
            try
            {
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController controller = go.AddComponent<CharacterRuntimeInputMotionController>();
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.zero, jumpPressed: true));
                controller.ConfigureInputProvider(input);

                Assert.IsTrue(controller.StepFrame());

                Assert.Greater(go.transform.position.y, 0.05f);
                Assert.IsTrue(controller.LastMotionResult.JumpStarted);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StepFrame_BuffersJumpPressedUntilNextMotionStep()
        {
            var go = new GameObject("character");
            try
            {
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController controller = go.AddComponent<CharacterRuntimeInputMotionController>();
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.zero, jumpPressed: true));
                controller.ConfigureInputProvider(input);

                Assert.IsTrue(controller.EnsureInitialized());
                input.SetSnapshot(CreateMoveSnapshot(Vector2.zero, jumpPressed: false));

                Assert.IsTrue(controller.StepFrame());

                Assert.IsTrue(controller.LastMotionResult.JumpStarted);
                Assert.Greater(go.transform.position.y, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StepFrame_ReadsMovementWhenGameplayHasDebugOverlay()
        {
            var go = new GameObject("character");
            try
            {
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController controller = go.AddComponent<CharacterRuntimeInputMotionController>();
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.PushContext(InputContext.Debug, InputContextPolicy.Overlay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.up, jumpPressed: true));
                controller.ConfigureInputProvider(input);

                Assert.IsTrue(controller.StepFrame());

                Assert.Greater(go.transform.position.z, 0f);
                Assert.IsTrue(controller.LastMotionResult.JumpStarted);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LocomotionBlend_ConsumesMotionVectorAndUsesFallbackWithoutClips()
        {
            var go = new GameObject("character");
            try
            {
                var modelRoot = new GameObject("ModelRoot").transform;
                modelRoot.SetParent(go.transform, false);
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController motion = go.AddComponent<CharacterRuntimeInputMotionController>();
                CharacterRuntimeLocomotionBlendController locomotion = go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                locomotion.Configure(modelRoot, null, null);
                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.up));
                motion.ConfigureInputProvider(input);

                Assert.IsTrue(motion.StepFrame());
                InvokeLateUpdate(locomotion);

                Assert.Greater(locomotion.Speed01, 0.5f);
                Assert.Greater(locomotion.Blend.y, 0.5f);
                Assert.IsTrue(locomotion.UsingFallback);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LocomotionBlend_WithAnimationBackendSendsConfiguredBlend2DRequest()
        {
            var go = new GameObject("character");
            var idleClip = new AnimationClip { name = "Idle" };
            var forwardClip = new AnimationClip { name = "Forward" };
            try
            {
                var modelRoot = new GameObject("ModelRoot").transform;
                modelRoot.SetParent(go.transform, false);
                Animator animator = go.AddComponent<Animator>();
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureBinding(binding);
                CharacterRuntimeInputMotionController motion = go.AddComponent<CharacterRuntimeInputMotionController>();
                CharacterRuntimeLocomotionBlendController locomotion = go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                locomotion.Configure(modelRoot, animator, new[] { idleClip, forwardClip });

                var idleKey = new ResourceKey("test.anim.idle", ResourceTypeIds.AnimationClip);
                var forwardKey = new ResourceKey("test.anim.forward", ResourceTypeIds.AnimationClip);
                var provider = new MemoryResourceProvider()
                    .Register("test/idle", idleClip)
                    .Register("test/forward", forwardClip);
                var manager = new ResourceManager();
                manager.RegisterProvider(provider);
                manager.AddCatalog(new ResourceCatalog(
                        "test.animations",
                        string.Empty,
                        new[]
                        {
                            new ResourceCatalogEntry(idleKey.Id, idleKey.TypeId, "memory", "test/idle"),
                            new ResourceCatalogEntry(forwardKey.Id, forwardKey.TypeId, "memory", "test/forward")
                        }));
                var blend = new MxAnimationBlend2DDefinition(
                    "blend.move2d",
                    "moveX",
                    "moveY",
                    MxAnimationLayerId.Base,
                    new[]
                    {
                        new MxAnimationBlend2DPoint(0, 0, idleKey),
                        new MxAnimationBlend2DPoint(0, 1000, forwardKey)
                    });
                var definition = new MxAnimationSetDefinition(
                    "set.base",
                    1,
                    idleKey,
                    idleKey,
                    blend2DDefinitions: new[] { blend });
                locomotion.ConfigureAnimationBackend(
                    new UnityPlayablesAnimationBackend(animator, manager, definition, "actor.test"),
                    blend);

                var input = new FakeInputProvider();
                input.SetContext(InputContext.Gameplay);
                input.SetSnapshot(CreateMoveSnapshot(Vector2.up));
                motion.ConfigureInputProvider(input);

                Assert.IsTrue(motion.StepFrame());
                InvokeLateUpdate(locomotion);

                Assert.IsTrue(locomotion.HasAnimationBackend);
                Assert.IsFalse(locomotion.UsingFallback);
                Assert.AreEqual("blend.move2d", locomotion.ActiveBlend2DId);
                Assert.Greater(locomotion.LastQuantizedBlendY, 0);
                Assert.IsTrue(locomotion.LastAnimationResult.Success, locomotion.LastAnimationResult.Message);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(forwardClip);
            }
        }

        private static void InvokeLateUpdate(CharacterRuntimeLocomotionBlendController locomotion)
        {
            typeof(CharacterRuntimeLocomotionBlendController)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(locomotion, null);
        }

        private static void ConfigureBinding(CharacterRuntimeControllerBinding binding)
        {
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("_stableCharacterId").intValue = 710001;
            serialized.FindProperty("_gameplayEntityIndex").intValue = 710001;
            serialized.FindProperty("_gameplayEntityGeneration").intValue = 1;
            serialized.FindProperty("_combatEntityId").intValue = 710001;
            serialized.FindProperty("_combatBodyId").intValue = 710001;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputSnapshot CreateMoveSnapshot(Vector2 move, bool jumpPressed = false)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed: jumpPressed,
                jumpHeld: jumpPressed,
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
                debugTogglePressed: false);
        }
    }
}
