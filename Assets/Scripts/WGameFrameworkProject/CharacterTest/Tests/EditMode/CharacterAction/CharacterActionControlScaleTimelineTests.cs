using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionControlScaleTimelineTests
    {
        private static JObject Action() => JObject.Parse(@"{
            'actionId':301,'durationFrames':8,'motionTrack':{'events':[],
            'movementControlKeys':[{'frame':2,'value':2,'stableEventId':9001,'stableEventOrder':1},
                {'frame':5,'value':1,'stableEventId':9002,'stableEventOrder':2}],
            'rotationControlKeys':[{'frame':3,'value':0,'stableEventId':9003,'stableEventOrder':3}]}}");

        [Test]
        public void Projection_IndependentAxesAndInclusiveRangesRegenerateForDuration()
        {
            JObject action = Action();
            CharacterActionControlScaleJsonProjection.SynchronizeAction(action);
            var events = (JArray)action["motionTrack"]["events"];
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(4, events[0].Value<int>("endFrame"));
            Assert.AreEqual(7, events[1].Value<int>("endFrame"));
            Assert.IsNull(events[0]["rotationControlScale"]);
            Assert.IsNull(events[2]["movementControlScale"]);
            action["durationFrames"] = 12;
            CharacterActionControlScaleJsonProjection.SynchronizeAction(action);
            events = (JArray)action["motionTrack"]["events"];
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(11, events[1].Value<int>("endFrame"));
            CharacterActionControlScaleJsonProjection.RemoveGeneratedEvents(action);
            Assert.AreEqual(0, ((JArray)action["motionTrack"]["events"]).Count);
            Assert.AreEqual(2, ((JArray)action["motionTrack"]["movementControlKeys"]).Count);
        }

        [TestCase(-1f)]
        [TestCase(10.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Projection_InvalidValuesRejectWithoutReplacingOriginalEvents(float value)
        {
            JObject action = Action();
            action["motionTrack"]["movementControlKeys"][0]["value"] = value;
            string before = action.ToString();
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterActionControlScaleJsonProjection.SynchronizeAction(action));
            Assert.AreEqual(before, action.ToString());
        }

        [TestCase(-1)]
        [TestCase(8)]
        public void Projection_OutOfRangeFramesReject(int frame)
        {
            JObject action = Action();
            action["motionTrack"]["movementControlKeys"][0]["frame"] = frame;
            Assert.Throws<FormatException>(() => CharacterActionControlScaleJsonProjection.SynchronizeAction(action));
        }

        [Test]
        public void Migration_OverlapsRestoreLowerValueAndPreserveOmittedAxis()
        {
            JObject action = JObject.Parse(@"{'actionId':301,'durationFrames':8,'motionTrack':{'events':[
                {'frame':0,'endFrame':5,'kind':'SetControlScale','movementControlScale':0.5,'rotationControlScale':0.75,'stableEventId':1,'stableEventOrder':1},
                {'frame':2,'endFrame':3,'kind':'SetControlScale','movementControlScale':0,'stableEventId':2,'stableEventOrder':2},
                {'frame':2,'endFrame':2,'kind':'SetControlScale','movementControlScale':0.25,'stableEventId':3,'stableEventOrder':3}]}}");
            string before = action.ToString();
            CharacterActionControlScaleJsonProjection.MigrateAction(action);
            var dto = action.ToObject<CharacterActionJsonDto>();
            float[] movement = { 0.5f, 0.5f, 0.25f, 0f, 0.5f, 0.5f, 1f, 1f };
            for (int frame = 0; frame < 8; frame++)
            {
                Assert.AreEqual(movement[frame], CharacterActionControlScaleAuthoring.Evaluate(dto, false, frame));
                Assert.AreEqual(frame <= 5 ? 0.75f : 1f, CharacterActionControlScaleAuthoring.Evaluate(dto, true, frame));
            }
            Assert.AreEqual(0, dto.MotionTrack.Events.Length);
            Assert.IsFalse(string.IsNullOrEmpty(dto.MotionTrack.MovementControlKeys[0].StableEventLabel));
            string canonical = action.ToString();
            CharacterActionControlScaleJsonProjection.MigrateAction(action);
            Assert.AreEqual(canonical, action.ToString());
        }

        [Test]
        public void Timeline_FixedRowsDefaultOneAndDeletingKeyResumesPreviousValue()
        {
            var action = Action().ToObject<CharacterActionJsonDto>();
            var snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            Assert.IsTrue(snapshot.Tracks.Any(track => track.Kind == TimelineTrackKind.MovementControl));
            Assert.IsTrue(snapshot.Tracks.Any(track => track.Kind == TimelineTrackKind.RotationControl));
            Assert.AreEqual(1f, CharacterActionControlScaleAuthoring.Evaluate(action, false, 1));
            var keys = snapshot.ItemsByTrack[TimelineTrackKind.MovementControl];
            keys[1].SetRange(2, 2);
            Assert.AreEqual(5, keys[1].StartFrame, "Dragging onto an existing key must not create a duplicate.");
            keys[1].Remove();
            Assert.AreEqual(2f, CharacterActionControlScaleAuthoring.Evaluate(action, false, 7));
            var roundTrip = JsonConvert.DeserializeObject<CharacterActionJsonDto>(JsonConvert.SerializeObject(action));
            Assert.AreEqual(2f, CharacterActionControlScaleAuthoring.Evaluate(roundTrip, false, 7));
            keys[0].Remove();
            Assert.AreEqual(1f, CharacterActionControlScaleAuthoring.Evaluate(action, false, 7));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(10f)]
        public void DispatchAndCharacterTestComponent_PreserveScaleThroughTen(float scale)
        {
            var dispatch = new CharacterActionTrackDispatchEvent(CharacterActionTrackKind.Motion,
                CharacterActionTrackEventKind.SetControlScale, 0, stableEventId: 9001,
                movementControlScale: scale, rotationControlScale: scale, endFrame: 7, stableEventOrder: 1);
            Assert.AreEqual(scale, dispatch.MovementControlScale);
            var request = new CharacterActionMotionRequest(default, default, default,
                CharacterActionTrackEventKind.SetControlScale, CharacterMovementMode.Idle, 0, 0, 0,
                movementControlScale: scale, rotationControlScale: scale);
            var sink = new CharacterActionMotionRequestAccumulatorSink();
            sink.SubmitMotionRequest(request);
            Assert.AreEqual((int)(scale * 1000), sink.ToFrame().MovementControlScaleMilli);
            var component = MxFramework.Game.CharacterTest.CharacterTestCharacterActionComponent.None
                .WithControlScale(CharacterMovementMode.Idle, scale, scale, false);
            Assert.AreEqual(scale, component.MovementControlScale);
        }
    }
}
