using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    internal static class CharacterActionControlScaleAuthoring
    {
        internal static void Migrate(CharacterActionJsonDto action)
        {
            if (action?.MotionTrack == null) return;
            if (action.MotionTrack.MovementControlKeys != null || action.MotionTrack.RotationControlKeys != null)
            {
                action.MotionTrack.Events = Array.FindAll(action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>(),
                    evt => evt != null && !evt.ControlScaleGenerated);
                return;
            }
            JObject json = JObject.FromObject(action, JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
            try { CharacterActionControlScaleJsonProjection.MigrateAction(json); }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            { action.MotionTrack.ControlScaleMigrationIssue = ex.Message; return; }
            action.MotionTrack = json["motionTrack"].ToObject<MotionTrackJsonDto>(
                JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
        }

        internal static void Prepare(CharacterActionJsonDto action)
        {
            if (action?.MotionTrack == null) return;
            if (!string.IsNullOrEmpty(action.MotionTrack.ControlScaleMigrationIssue))
                throw new InvalidOperationException(action.MotionTrack.ControlScaleMigrationIssue);
            PrepareKeys(action, action.MotionTrack.MovementControlKeys, "movement");
            PrepareKeys(action, action.MotionTrack.RotationControlKeys, "rotation");
        }

        private static void PrepareKeys(CharacterActionJsonDto action, ControlScaleKeyJsonDto[] keys, string axis)
        {
            if (keys == null) return;
            var frames = new HashSet<int>();
            foreach (ControlScaleKeyJsonDto key in keys)
            {
                if (key == null || key.Frame < 0 || key.Frame >= action.DurationFrames || !frames.Add(key.Frame))
                    throw new InvalidOperationException("控制倍率标记必须在动作范围内，同一行同一帧只能有一个标记。");
                CharacterActionControlScaleJsonProjection.ValidateScale(key.Value);
            }
            foreach (ControlScaleKeyJsonDto key in keys)
            {
                if (key.StableEventId <= 0)
                {
                    if (string.IsNullOrEmpty(key.StableEventLabel))
                        key.StableEventLabel = "control-key/" + action.ActionId + "/" + axis + "/" + Guid.NewGuid().ToString("N");
                    key.StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, key.StableEventLabel);
                }
            }
            var orders = CharacterContentIdentityAuthoringCatalog.ReadEventOrders();
            foreach (ControlScaleKeyJsonDto key in keys) key.StableEventOrder = orders[key.StableEventId];
        }

        internal static ControlScaleKeyJsonDto[] Keys(CharacterActionJsonDto action, bool rotation) =>
            (rotation ? action.MotionTrack?.RotationControlKeys : action.MotionTrack?.MovementControlKeys)
            ?? Array.Empty<ControlScaleKeyJsonDto>();

        internal static float Evaluate(CharacterActionJsonDto action, bool rotation, int frame)
        {
            int latest = -1;
            float value = 1f;
            foreach (var key in Keys(action, rotation))
                if (key != null && key.Frame <= frame && key.Frame > latest) { latest = key.Frame; value = key.Value; }
            return value;
        }

        internal static ControlScaleKeyJsonDto Insert(CharacterActionJsonDto action, bool rotation, int frame)
        {
            foreach (var key in Keys(action, rotation)) if (key.Frame == frame) return key;
            var added = new ControlScaleKeyJsonDto { Frame = frame, Value = Evaluate(action, rotation, frame),
                StableEventId = CharacterContentIdentityAuthoringCatalog.AllocateNewEvent(action.ActionId, rotation ? "rotation-control" : "movement-control") };
            var keys = new List<ControlScaleKeyJsonDto>(Keys(action, rotation)) { added };
            action.MotionTrack ??= new MotionTrackJsonDto();
            SetKeys(action, rotation, keys.ToArray());
            Prepare(action);
            return added;
        }

        internal static void SetKeys(CharacterActionJsonDto action, bool rotation, ControlScaleKeyJsonDto[] keys)
        {
            Array.Sort(keys, (left, right) => left.Frame.CompareTo(right.Frame));
            if (rotation) action.MotionTrack.RotationControlKeys = keys;
            else action.MotionTrack.MovementControlKeys = keys;
        }

        internal static string Publish(string actionsJson)
        {
            JObject document = JObject.Parse(actionsJson);
            foreach (JObject action in ((JArray)document["actions"]).Children<JObject>())
                CharacterActionControlScaleJsonProjection.SynchronizeAction(action);
            return document.ToString(Formatting.Indented);
        }

        // Disposable draft validation/preview identities do not touch the persisted catalog.
        // Publishing must instead call Prepare on the owned DTOs before serialization.
        internal static string ValidationJson(string json)
        {
            JObject document = JObject.Parse(json);
            int temporaryId = int.MaxValue;
            foreach (JObject action in ((JArray)document["actions"]).Children<JObject>())
            {
                if (!(action["motionTrack"] is JObject track)) continue;
                foreach (string axis in new[] { "movementControlKeys", "rotationControlKeys" })
                    foreach (JObject key in (track[axis] as JArray ?? new JArray()).Children<JObject>())
                    {
                        if (key.Value<int>("stableEventId") <= 0) key["stableEventId"] = temporaryId--;
                        if (key.Value<int>("stableEventOrder") <= 0) key["stableEventOrder"] = 1;
                    }
            }
            return document.ToString(Formatting.None);
        }
    }
}
