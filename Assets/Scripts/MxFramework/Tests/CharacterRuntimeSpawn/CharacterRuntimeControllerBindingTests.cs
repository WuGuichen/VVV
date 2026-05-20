using MxFramework.CharacterControl;
using MxFramework.CharacterRuntimeSpawn.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterRuntimeControllerBindingTests
    {
        [Test]
        public void Initialize_CreatesStateMachineFromSerializedEntityReference()
        {
            var go = new GameObject("character");
            try
            {
                CharacterRuntimeControllerBinding binding = go.AddComponent<CharacterRuntimeControllerBinding>();
                SetSerializedEntity(binding, stableId: 710001, gameplayIndex: 710001, gameplayGeneration: 1, combatEntityId: 710001, combatBodyId: 710001);

                CharacterControlStateMachine stateMachine = binding.Initialize();

                Assert.NotNull(stateMachine);
                Assert.IsTrue(binding.IsInitialized);
                Assert.AreEqual(CharacterControlState.Locomotion, stateMachine.CurrentState);
                Assert.AreEqual(710001, stateMachine.Entity.StableId);
                Assert.AreEqual("710001:1", stateMachine.Entity.GameplayEntityId.ToString());
                Assert.AreEqual("710001", stateMachine.Entity.CombatEntityId.ToString());
                Assert.AreEqual("710001", stateMachine.Entity.CombatBodyId.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void SetSerializedEntity(
            CharacterRuntimeControllerBinding binding,
            int stableId,
            int gameplayIndex,
            int gameplayGeneration,
            int combatEntityId,
            int combatBodyId)
        {
            var serialized = new UnityEditor.SerializedObject(binding);
            serialized.FindProperty("_stableCharacterId").intValue = stableId;
            serialized.FindProperty("_gameplayEntityIndex").intValue = gameplayIndex;
            serialized.FindProperty("_gameplayEntityGeneration").intValue = gameplayGeneration;
            serialized.FindProperty("_combatEntityId").intValue = combatEntityId;
            serialized.FindProperty("_combatBodyId").intValue = combatBodyId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
