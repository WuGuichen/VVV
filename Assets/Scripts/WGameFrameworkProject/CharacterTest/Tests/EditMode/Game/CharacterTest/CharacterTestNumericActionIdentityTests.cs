using System;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNumericActionIdentityTests
    {
        [Test]
        public void ActionStateCopiesKeepTheNumericIdentity()
        {
            var action = Create(CharacterTestActionStateId.Running, CharacterTestActionPhaseId.Startup);
            var copied = action.ClearFrameRequests().WithMovementRequest(CharacterMovementMode.Idle, false)
                .WithControlScaleMilli(CharacterMovementMode.Idle, 500, 750, false);
            Assert.AreEqual(action.State, copied.State);
            Assert.AreEqual(action.Phase, copied.Phase);
            Assert.AreEqual("Running", CharacterTestCharacterActionComponent.ResolveStateText(copied.State));
            Assert.AreEqual("Startup", CharacterTestCharacterActionComponent.ResolvePhaseText(copied.Phase));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create((CharacterTestActionStateId)999, CharacterTestActionPhaseId.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(CharacterTestActionStateId.None, (CharacterTestActionPhaseId)50));
        }

        [Test]
        public void SnapshotSerializesDeclaredActionIdentitiesAsIntegers()
        {
            var snapshot = new CharacterTestNetworkCharacterSnapshot {
                ActionState = CharacterTestActionStateId.Completed,
                ActionPhase = CharacterTestActionPhaseId.Death,
                ActionRunnerState = CharacterActionInstanceState.Finished,
                ActionRunnerPhase = CharacterActionPhaseKind.Recovery
            };
            string json = JsonConvert.SerializeObject(snapshot);
            var document = JObject.Parse(json);
            Assert.AreEqual(JTokenType.Integer, document[nameof(snapshot.ActionState)].Type);
            Assert.AreEqual(JTokenType.Integer, document[nameof(snapshot.ActionPhase)].Type);
            Assert.AreEqual(JTokenType.Integer, document[nameof(snapshot.ActionRunnerState)].Type);
            Assert.AreEqual(JTokenType.Integer, document[nameof(snapshot.ActionRunnerPhase)].Type);
            var restored = JsonConvert.DeserializeObject<CharacterTestNetworkCharacterSnapshot>(json);
            Assert.AreEqual(snapshot.ActionState, restored.ActionState);
            Assert.AreEqual(snapshot.ActionPhase, restored.ActionPhase);
        }

        [TestCase("{\"ActionState\":\"Running\"}")]
        [TestCase("{\"ActionPhase\":\"Startup\"}")]
        [TestCase("{\"ActionRunnerState\":999}")]
        [TestCase("{\"ActionRunnerPhase\":null}")]
        [TestCase("{\"ActionState\":1.0}")]
        [TestCase("{\"ActionState\":2147483648}")]
        public void SnapshotRejectsNamesAndUndefinedNumericValues(string json)
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<CharacterTestNetworkCharacterSnapshot>(json));
        }

        [TestCase(29)] [TestCase(30)] [TestCase(31)] [TestCase(32)] [TestCase(36)] [TestCase(37)]
        public void OldSnapshotVersionIsRejectedBeforeAccessingTheWorld(int oldSchema)
        {
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null);
            var snapshot = new RuntimeCustomState(
                CharacterTestNetworkSnapshotParticipant.NumericParticipantId,
                CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                oldSchema,
                "{}");
            var result = participant.ApplySnapshot(RuntimeFrame.Zero, snapshot);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("schema mismatch", result.Message);
        }

        private static CharacterTestCharacterActionComponent Create(CharacterTestActionStateId state, CharacterTestActionPhaseId phase)
            => CharacterTestCharacterActionComponent.FromDeterministic(new CharacterActionId(301), state, phase, 1,
                default, string.Empty, string.Empty, 0, 0, 0);
    }
}
