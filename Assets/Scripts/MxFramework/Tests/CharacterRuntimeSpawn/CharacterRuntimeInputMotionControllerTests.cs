using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Input;
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

        private static InputSnapshot CreateMoveSnapshot(Vector2 move)
        {
            return new InputSnapshot(
                move,
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
                debugTogglePressed: false);
        }
    }
}
