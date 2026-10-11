using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static void CompareAudio(
            CharacterTestRuntimeAiLabFrameSyncAudioSample expected,
            CharacterTestRuntimeAiLabFrameSyncAudioSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "audio", 0, "serviceAvailable", expected.ServiceAvailable, actual.ServiceAvailable);
            AddMismatch(rows, "audio", 0, "initialized", expected.Initialized, actual.Initialized);
            AddMismatch(rows, "audio", 0, "totalPlayRequests", expected.TotalPlayRequests, actual.TotalPlayRequests);
            AddMismatch(rows, "audio", 0, "totalStopRequests", expected.TotalStopRequests, actual.TotalStopRequests);
            AddMismatch(rows, "audio", 0, "activeEventCount", expected.ActiveEventCount, actual.ActiveEventCount);
            CompareAudioActiveEvents(expected.ActiveEvents, actual.ActiveEvents, rows);
            CompareAudioBusStates(expected.BusStates, actual.BusStates, rows);
            CompareAudioRecentErrors(expected.RecentErrors, actual.RecentErrors, rows);
            CompareAudioIntentDrain(expected.IntentDrain, actual.IntentDrain, rows);
            CompareInteractionAudio(expected.Interaction, actual.Interaction, rows);
            ComparePhysicalAudio(expected.Physical, actual.Physical, rows);
            CompareWeaponHitAudio(expected.WeaponHit, actual.WeaponHit, rows);
        }

        private static void CompareAudioActiveEvents(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "audio", 0, "activeEvents.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample left = expected[i];
                if (!TryFindAudioActiveEvent(
                    actual,
                    left.HandleId,
                    left.EventId,
                    left.EmitterId,
                    out CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "audio",
                        left.EmitterId,
                        "activeEvent." + left.HandleId.ToString(CultureInfo.InvariantCulture) + ".missing",
                        "present",
                        "missing"));
                    continue;
                }

                string prefix = "activeEvent." + left.HandleId.ToString(CultureInfo.InvariantCulture) + ".";
                AddMismatch(rows, "audio", left.EmitterId, prefix + "eventId", left.EventId, right.EventId);
                AddMismatch(rows, "audio", left.EmitterId, prefix + "handleState", left.HandleState, right.HandleState);
                AddMismatch(rows, "audio", left.EmitterId, prefix + "name", left.Name, right.Name);
                AddMismatch(rows, "audio", left.EmitterId, prefix + "busId", left.BusId, right.BusId);
            }
        }

        private static bool TryFindAudioActiveEvent(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample> samples,
            int handleId,
            int eventId,
            int emitterId,
            out CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.HandleId == handleId
                    && sample.EventId == eventId
                    && sample.EmitterId == emitterId)
                {
                    return true;
                }
            }

            sample = default;
            return false;
        }

        private static void CompareAudioBusStates(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioBusSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioBusSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "audio", 0, "bus.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAudioBusSample left = expected[i];
                if (!TryFindAudioBus(actual, left.BusId, out CharacterTestRuntimeAiLabFrameSyncAudioBusSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "audio",
                        left.BusId,
                        "bus.missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "audio", left.BusId, "bus.name", left.Name, right.Name);
                AddFloatMismatch(rows, "audio", left.BusId, "bus.volume", left.Volume, right.Volume);
                AddMismatch(rows, "audio", left.BusId, "bus.muted", left.Muted, right.Muted);
            }
        }

        private static bool TryFindAudioBus(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioBusSample> samples,
            int busId,
            out CharacterTestRuntimeAiLabFrameSyncAudioBusSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.BusId == busId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareAudioRecentErrors(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "audio", 0, "error.count", expectedCount, actualCount);
            int compareCount = expectedCount < actualCount ? expectedCount : actualCount;
            for (int i = 0; i < compareCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncAudioErrorSample left = expected[i];
                CharacterTestRuntimeAiLabFrameSyncAudioErrorSample right = actual[i];
                string prefix = "error." + i.ToString(CultureInfo.InvariantCulture) + ".";
                AddMismatch(rows, "audio", i + 1, prefix + "code", left.Code, right.Code);
                AddMismatch(rows, "audio", i + 1, prefix + "message", left.Message, right.Message);
                AddMismatch(rows, "audio", i + 1, prefix + "frame", left.Frame, right.Frame);
                AddMismatch(rows, "audio", i + 1, prefix + "traceId", left.TraceId, right.TraceId);
            }
        }

        private static void CompareAudioIntentDrain(
            CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample expected,
            CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            const string prefix = "intent.";
            AddMismatch(rows, "audio", 0, prefix + "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "audio", 0, prefix + "requestCount", expected.RequestCount, actual.RequestCount);
            AddMismatch(rows, "audio", 0, prefix + "playedCount", expected.PlayedCount, actual.PlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "failedCount", expected.FailedCount, actual.FailedCount);
            AddMismatch(rows, "audio", 0, prefix + "droppedCount", expected.DroppedCount, actual.DroppedCount);
            AddMismatch(rows, "audio", 0, prefix + "skippedCount", expected.SkippedCount, actual.SkippedCount);
                AddMismatch(rows, "audio", 0, prefix + "lastCueId", expected.LastCueId, actual.LastCueId);
            AddMismatch(rows, "audio", 0, prefix + "lastEventId", expected.LastEventId, actual.LastEventId);
            AddMismatch(rows, "audio", 0, prefix + "lastEmitterId", expected.LastEmitterId, actual.LastEmitterId);
        }

        private static void CompareInteractionAudio(
            CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample expected,
            CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            const string prefix = "interaction.";
            AddMismatch(rows, "audio", 0, prefix + "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "audio", 0, prefix + "processedInteractionCount", expected.ProcessedInteractionCount, actual.ProcessedInteractionCount);
            AddMismatch(rows, "audio", 0, prefix + "audioPlayedCount", expected.AudioPlayedCount, actual.AudioPlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "skippedCount", expected.SkippedCount, actual.SkippedCount);
            AddMismatch(rows, "audio", 0, prefix + "lastStableId", expected.LastStableId, actual.LastStableId);
            AddMismatch(rows, "audio", 0, prefix + "lastForceMode", expected.LastForceMode, actual.LastForceMode);
            AddMismatch(rows, "audio", 0, prefix + "lastFieldShape", expected.LastFieldShape, actual.LastFieldShape);
            AddMismatch(rows, "audio", 0, prefix + "lastAudioCueKey", expected.LastAudioCueKey, actual.LastAudioCueKey);
        }

        private static void ComparePhysicalAudio(
            CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample expected,
            CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            const string prefix = "physical.";
            AddMismatch(rows, "audio", 0, prefix + "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "audio", 0, prefix + "characterCount", expected.CharacterCount, actual.CharacterCount);
            AddMismatch(rows, "audio", 0, prefix + "movingGroundedCharacterCount", expected.MovingGroundedCharacterCount, actual.MovingGroundedCharacterCount);
            AddMismatch(rows, "audio", 0, prefix + "bodyPartContactCount", expected.BodyPartContactCount, actual.BodyPartContactCount);
            AddMismatch(rows, "audio", 0, prefix + "bodyPartContactPlayedCount", expected.BodyPartContactPlayedCount, actual.BodyPartContactPlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "bodyPartProbeMissCount", expected.BodyPartProbeMissCount, actual.BodyPartProbeMissCount);
            AddMismatch(rows, "audio", 0, prefix + "bodyPartFilteredContactCount", expected.BodyPartFilteredContactCount, actual.BodyPartFilteredContactCount);
            AddMismatch(rows, "audio", 0, prefix + "leftFootContactCount", expected.LeftFootContactCount, actual.LeftFootContactCount);
            AddMismatch(rows, "audio", 0, prefix + "rightFootContactCount", expected.RightFootContactCount, actual.RightFootContactCount);
            AddMismatch(rows, "audio", 0, prefix + "landingPlayedCount", expected.LandingPlayedCount, actual.LandingPlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "objectContactCount", expected.ObjectContactCount, actual.ObjectContactCount);
            AddMismatch(rows, "audio", 0, prefix + "objectCollisionPlayedCount", expected.ObjectCollisionPlayedCount, actual.ObjectCollisionPlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "footProbeMissCount", expected.FootProbeMissCount, actual.FootProbeMissCount);
            AddMismatch(rows, "audio", 0, prefix + "cooldownSkipCount", expected.CooldownSkipCount, actual.CooldownSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "presentationMotionResolvedCount", expected.PresentationMotionResolvedCount, actual.PresentationMotionResolvedCount);
            AddMismatch(rows, "audio", 0, prefix + "bakedMotionResolvedCount", expected.BakedMotionResolvedCount, actual.BakedMotionResolvedCount);
            AddMismatch(rows, "audio", 0, prefix + "staticMotionCount", expected.StaticMotionCount, actual.StaticMotionCount);
            AddMismatch(rows, "audio", 0, prefix + "motionFallbackCount", expected.MotionFallbackCount, actual.MotionFallbackCount);
            AddMismatch(rows, "audio", 0, prefix + "missingMotionSocketCount", expected.MissingMotionSocketCount, actual.MissingMotionSocketCount);
            AddMismatch(rows, "audio", 0, prefix + "missingMotionClipCount", expected.MissingMotionClipCount, actual.MissingMotionClipCount);
            AddMismatch(rows, "audio", 0, prefix + "missingMotionFrameCount", expected.MissingMotionFrameCount, actual.MissingMotionFrameCount);
            AddMismatch(rows, "audio", 0, prefix + "missingMotionSourceCount", expected.MissingMotionSourceCount, actual.MissingMotionSourceCount);
            AddMismatch(rows, "audio", 0, prefix + "missingPhysicsRuntimeSkipCount", expected.MissingPhysicsRuntimeSkipCount, actual.MissingPhysicsRuntimeSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "missingContactPointConfigSkipCount", expected.MissingContactPointConfigSkipCount, actual.MissingContactPointConfigSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "skippedCount", expected.SkippedCount, actual.SkippedCount);
            AddMismatch(rows, "audio", 0, prefix + "lastCueKey", expected.LastCueKey, actual.LastCueKey);
            AddMismatch(rows, "audio", 0, prefix + "lastEmitterId", expected.LastEmitterId, actual.LastEmitterId);
        }

        private static void CompareWeaponHitAudio(
            CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample expected,
            CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            const string prefix = "weaponHit.";
            AddMismatch(rows, "audio", 0, prefix + "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "audio", 0, prefix + "processedCount", expected.ProcessedCount, actual.ProcessedCount);
            AddMismatch(rows, "audio", 0, prefix + "playedCount", expected.PlayedCount, actual.PlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "damagePlayedCount", expected.DamagePlayedCount, actual.DamagePlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "blockedPlayedCount", expected.BlockedPlayedCount, actual.BlockedPlayedCount);
            AddMismatch(rows, "audio", 0, prefix + "ignoredResultCount", expected.IgnoredResultCount, actual.IgnoredResultCount);
            AddMismatch(rows, "audio", 0, prefix + "missingRuleSkipCount", expected.MissingRuleSkipCount, actual.MissingRuleSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "missingAudioServiceSkipCount", expected.MissingAudioServiceSkipCount, actual.MissingAudioServiceSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "missingCueSkipCount", expected.MissingCueSkipCount, actual.MissingCueSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "cooldownSkipCount", expected.CooldownSkipCount, actual.CooldownSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "playbackFailedSkipCount", expected.PlaybackFailedSkipCount, actual.PlaybackFailedSkipCount);
            AddMismatch(rows, "audio", 0, prefix + "lastCueKey", expected.LastCueKey, actual.LastCueKey);
            AddMismatch(rows, "audio", 0, prefix + "lastEventId", expected.LastEventId, actual.LastEventId);
            AddMismatch(rows, "audio", 0, prefix + "lastEmitterId", expected.LastEmitterId, actual.LastEmitterId);
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAudioSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAudioSample(
            bool serviceAvailable,
            bool initialized,
            int totalPlayRequests,
            int totalStopRequests,
            int activeEventCount,
            CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample[] activeEvents,
            CharacterTestRuntimeAiLabFrameSyncAudioBusSample[] busStates,
            CharacterTestRuntimeAiLabFrameSyncAudioErrorSample[] recentErrors,
            CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample intentDrain,
            CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample interaction,
            CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample physical,
            CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample weaponHit)
        {
            ServiceAvailable = serviceAvailable;
            Initialized = initialized;
            TotalPlayRequests = totalPlayRequests;
            TotalStopRequests = totalStopRequests;
            ActiveEventCount = activeEventCount;
            ActiveEvents = activeEvents
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample>();
            BusStates = busStates
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioBusSample>();
            RecentErrors = recentErrors
                ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample>();
            IntentDrain = intentDrain;
            Interaction = interaction;
            Physical = physical;
            WeaponHit = weaponHit;
        }

        public bool ServiceAvailable { get; }
        public bool Initialized { get; }
        public int TotalPlayRequests { get; }
        public int TotalStopRequests { get; }
        public int ActiveEventCount { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample> ActiveEvents { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioBusSample> BusStates { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample> RecentErrors { get; }
        public CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample IntentDrain { get; }
        public CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample Interaction { get; }
        public CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample Physical { get; }
        public CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample WeaponHit { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAudioSample Capture(
            IAudioService audioService,
            CharacterTestAudioIntentDrainSummary intentDrain,
            CharacterTestInteractionAudioSummary interaction,
            CharacterTestPhysicalAudioSummary physical,
            CharacterTestWeaponHitAudioSummary weaponHit)
        {
            AudioDebugSnapshot snapshot = audioService != null
                ? audioService.CaptureSnapshot()
                : AudioDebugSnapshot.Empty;
            if (snapshot == null)
                snapshot = AudioDebugSnapshot.Empty;

            return new CharacterTestRuntimeAiLabFrameSyncAudioSample(
                serviceAvailable: audioService != null,
                initialized: snapshot.Initialized,
                totalPlayRequests: snapshot.TotalPlayRequests,
                totalStopRequests: snapshot.TotalStopRequests,
                activeEventCount: snapshot.ActiveEventCount,
                activeEvents: CaptureActiveEvents(snapshot.ActiveEvents),
                busStates: CaptureBusStates(snapshot.BusStates),
                recentErrors: CaptureRecentErrors(snapshot.RecentErrors),
                intentDrain: CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample.Capture(intentDrain),
                interaction: CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample.Capture(interaction),
                physical: CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample.Capture(physical),
                weaponHit: CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample.Capture(weaponHit));
        }

        private static CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample[] CaptureActiveEvents(
            IReadOnlyList<AudioDebugActiveEvent> activeEvents)
        {
            int count = activeEvents != null ? activeEvents.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample.Capture(activeEvents[i]);
            return samples;
        }

        private static CharacterTestRuntimeAiLabFrameSyncAudioBusSample[] CaptureBusStates(
            IReadOnlyList<AudioDebugBusState> busStates)
        {
            int count = busStates != null ? busStates.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioBusSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncAudioBusSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncAudioBusSample.Capture(busStates[i]);
            return samples;
        }

        private static CharacterTestRuntimeAiLabFrameSyncAudioErrorSample[] CaptureRecentErrors(
            IReadOnlyList<AudioDebugError> recentErrors)
        {
            int count = recentErrors != null ? recentErrors.Count : 0;
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncAudioErrorSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncAudioErrorSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncAudioErrorSample.Capture(recentErrors[i]);
            return samples;
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample(
            int handleId,
            int eventId,
            int emitterId,
            int handleState,
            string name,
            int busId)
        {
            HandleId = handleId;
            EventId = eventId;
            EmitterId = emitterId;
            HandleState = handleState;
            Name = name ?? string.Empty;
            BusId = busId;
        }

        public int HandleId { get; }
        public int EventId { get; }
        public int EmitterId { get; }
        public int HandleState { get; }
        public string Name { get; }
        public int BusId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample Capture(
            AudioDebugActiveEvent activeEvent)
        {
            AudioHandle handle = activeEvent.Handle;
            return new CharacterTestRuntimeAiLabFrameSyncAudioActiveEventSample(
                handleId: handle.Id,
                eventId: handle.EventId,
                emitterId: activeEvent.EmitterId,
                handleState: (int)handle.State,
                name: activeEvent.Name,
                busId: activeEvent.BusId);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAudioBusSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAudioBusSample(
            int busId,
            string name,
            float volume,
            bool muted)
        {
            BusId = busId;
            Name = name ?? string.Empty;
            Volume = volume;
            Muted = muted;
        }

        public int BusId { get; }
        public string Name { get; }
        public float Volume { get; }
        public bool Muted { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAudioBusSample Capture(
            AudioDebugBusState busState)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAudioBusSample(
                busId: busState.BusId,
                name: busState.Name,
                volume: busState.Volume,
                muted: busState.Muted);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAudioErrorSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAudioErrorSample(
            int code,
            string message,
            long frame,
            AudioTraceIdentity traceId)
        {
            Code = code;
            Message = message ?? string.Empty;
            Frame = frame;
            TraceId = traceId;
        }

        public int Code { get; }
        public string Message { get; }
        public long Frame { get; }
        public AudioTraceIdentity TraceId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAudioErrorSample Capture(
            AudioDebugError error)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAudioErrorSample(
                code: (int)error.Code,
                message: error.Message,
                frame: error.Frame,
                traceId: error.TraceId);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample
    {
        public CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample(
            long frame,
            int requestCount,
            int playedCount,
            int failedCount,
            int droppedCount,
            int skippedCount,
            int lastCueId,
            int lastEventId,
            int lastEmitterId)
        {
            Frame = frame;
            RequestCount = requestCount;
            PlayedCount = playedCount;
            FailedCount = failedCount;
            DroppedCount = droppedCount;
            SkippedCount = skippedCount;
            LastCueId = lastCueId;
            LastEventId = lastEventId;
            LastEmitterId = lastEmitterId;
        }

        public long Frame { get; }
        public int RequestCount { get; }
        public int PlayedCount { get; }
        public int FailedCount { get; }
        public int DroppedCount { get; }
        public int SkippedCount { get; }
        public int LastCueId { get; }
        public int LastEventId { get; }
        public int LastEmitterId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample Capture(
            CharacterTestAudioIntentDrainSummary summary)
        {
            return new CharacterTestRuntimeAiLabFrameSyncAudioIntentDrainSample(
                frame: summary.Frame.Value,
                requestCount: summary.RequestCount,
                playedCount: summary.PlayedCount,
                failedCount: summary.FailedCount,
                droppedCount: summary.DroppedCount,
                skippedCount: summary.SkippedCount,
                lastCueId: summary.LastCueId,
                lastEventId: summary.LastEventId,
                lastEmitterId: summary.LastEmitterId);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample
    {
        public CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample(
            long frame,
            int processedInteractionCount,
            int audioPlayedCount,
            int skippedCount,
            int lastStableId,
            int lastForceMode,
            int lastFieldShape,
            int lastAudioCueKey)
        {
            Frame = frame;
            ProcessedInteractionCount = processedInteractionCount;
            AudioPlayedCount = audioPlayedCount;
            SkippedCount = skippedCount;
            LastStableId = lastStableId;
            LastForceMode = lastForceMode;
            LastFieldShape = lastFieldShape;
            LastAudioCueKey = lastAudioCueKey;
        }

        public long Frame { get; }
        public int ProcessedInteractionCount { get; }
        public int AudioPlayedCount { get; }
        public int SkippedCount { get; }
        public int LastStableId { get; }
        public int LastForceMode { get; }
        public int LastFieldShape { get; }
        public int LastAudioCueKey { get; }

        public static CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample Capture(
            CharacterTestInteractionAudioSummary summary)
        {
            return new CharacterTestRuntimeAiLabFrameSyncInteractionAudioSample(
                frame: summary.Frame.Value,
                processedInteractionCount: summary.ProcessedInteractionCount,
                audioPlayedCount: summary.AudioPlayedCount,
                skippedCount: summary.SkippedCount,
                lastStableId: summary.LastStableId,
                lastForceMode: (int)summary.LastForceMode,
                lastFieldShape: (int)summary.LastFieldShape,
                lastAudioCueKey: summary.LastAudioCueKey);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample
    {
        public CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample(
            long frame,
            int characterCount,
            int movingGroundedCharacterCount,
            int bodyPartContactCount,
            int bodyPartContactPlayedCount,
            int bodyPartProbeMissCount,
            int bodyPartFilteredContactCount,
            int leftFootContactCount,
            int rightFootContactCount,
            int landingPlayedCount,
            int objectContactCount,
            int objectCollisionPlayedCount,
            int footProbeMissCount,
            int cooldownSkipCount,
            int presentationMotionResolvedCount,
            int bakedMotionResolvedCount,
            int staticMotionCount,
            int motionFallbackCount,
            int missingMotionSocketCount,
            int missingMotionClipCount,
            int missingMotionFrameCount,
            int missingMotionSourceCount,
            int missingPhysicsRuntimeSkipCount,
            int missingContactPointConfigSkipCount,
            int skippedCount,
            string lastCueKey,
            int lastEmitterId)
        {
            Frame = frame;
            CharacterCount = characterCount;
            MovingGroundedCharacterCount = movingGroundedCharacterCount;
            BodyPartContactCount = bodyPartContactCount;
            BodyPartContactPlayedCount = bodyPartContactPlayedCount;
            BodyPartProbeMissCount = bodyPartProbeMissCount;
            BodyPartFilteredContactCount = bodyPartFilteredContactCount;
            LeftFootContactCount = leftFootContactCount;
            RightFootContactCount = rightFootContactCount;
            LandingPlayedCount = landingPlayedCount;
            ObjectContactCount = objectContactCount;
            ObjectCollisionPlayedCount = objectCollisionPlayedCount;
            FootProbeMissCount = footProbeMissCount;
            CooldownSkipCount = cooldownSkipCount;
            PresentationMotionResolvedCount = presentationMotionResolvedCount;
            BakedMotionResolvedCount = bakedMotionResolvedCount;
            StaticMotionCount = staticMotionCount;
            MotionFallbackCount = motionFallbackCount;
            MissingMotionSocketCount = missingMotionSocketCount;
            MissingMotionClipCount = missingMotionClipCount;
            MissingMotionFrameCount = missingMotionFrameCount;
            MissingMotionSourceCount = missingMotionSourceCount;
            MissingPhysicsRuntimeSkipCount = missingPhysicsRuntimeSkipCount;
            MissingContactPointConfigSkipCount = missingContactPointConfigSkipCount;
            SkippedCount = skippedCount;
            LastCueKey = lastCueKey ?? string.Empty;
            LastEmitterId = lastEmitterId;
        }

        public long Frame { get; }
        public int CharacterCount { get; }
        public int MovingGroundedCharacterCount { get; }
        public int BodyPartContactCount { get; }
        public int BodyPartContactPlayedCount { get; }
        public int BodyPartProbeMissCount { get; }
        public int BodyPartFilteredContactCount { get; }
        public int LeftFootContactCount { get; }
        public int RightFootContactCount { get; }
        public int LandingPlayedCount { get; }
        public int ObjectContactCount { get; }
        public int ObjectCollisionPlayedCount { get; }
        public int FootProbeMissCount { get; }
        public int CooldownSkipCount { get; }
        public int PresentationMotionResolvedCount { get; }
        public int BakedMotionResolvedCount { get; }
        public int StaticMotionCount { get; }
        public int MotionFallbackCount { get; }
        public int MissingMotionSocketCount { get; }
        public int MissingMotionClipCount { get; }
        public int MissingMotionFrameCount { get; }
        public int MissingMotionSourceCount { get; }
        public int MissingPhysicsRuntimeSkipCount { get; }
        public int MissingContactPointConfigSkipCount { get; }
        public int SkippedCount { get; }
        public string LastCueKey { get; }
        public int LastEmitterId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample Capture(
            CharacterTestPhysicalAudioSummary summary)
        {
            return new CharacterTestRuntimeAiLabFrameSyncPhysicalAudioSample(
                frame: summary.Frame.Value,
                characterCount: summary.CharacterCount,
                movingGroundedCharacterCount: summary.MovingGroundedCharacterCount,
                bodyPartContactCount: summary.BodyPartContactCount,
                bodyPartContactPlayedCount: summary.BodyPartContactPlayedCount,
                bodyPartProbeMissCount: summary.BodyPartProbeMissCount,
                bodyPartFilteredContactCount: summary.BodyPartFilteredContactCount,
                leftFootContactCount: summary.LeftFootContactCount,
                rightFootContactCount: summary.RightFootContactCount,
                landingPlayedCount: summary.LandingPlayedCount,
                objectContactCount: summary.ObjectContactCount,
                objectCollisionPlayedCount: summary.ObjectCollisionPlayedCount,
                footProbeMissCount: summary.FootProbeMissCount,
                cooldownSkipCount: summary.CooldownSkipCount,
                presentationMotionResolvedCount: summary.PresentationMotionResolvedCount,
                bakedMotionResolvedCount: summary.BakedMotionResolvedCount,
                staticMotionCount: summary.StaticMotionCount,
                motionFallbackCount: summary.MotionFallbackCount,
                missingMotionSocketCount: summary.MissingMotionSocketCount,
                missingMotionClipCount: summary.MissingMotionClipCount,
                missingMotionFrameCount: summary.MissingMotionFrameCount,
                missingMotionSourceCount: summary.MissingMotionSourceCount,
                missingPhysicsRuntimeSkipCount: summary.MissingPhysicsRuntimeSkipCount,
                missingContactPointConfigSkipCount: summary.MissingContactPointConfigSkipCount,
                skippedCount: summary.SkippedCount,
                lastCueKey: summary.LastCueKey,
                lastEmitterId: summary.LastEmitterId);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample
    {
        public CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample(
            long frame,
            int processedCount,
            int playedCount,
            int damagePlayedCount,
            int blockedPlayedCount,
            int ignoredResultCount,
            int missingRuleSkipCount,
            int missingAudioServiceSkipCount,
            int missingCueSkipCount,
            int cooldownSkipCount,
            int playbackFailedSkipCount,
            string lastCueKey,
            int lastEventId,
            int lastEmitterId)
        {
            Frame = frame;
            ProcessedCount = processedCount;
            PlayedCount = playedCount;
            DamagePlayedCount = damagePlayedCount;
            BlockedPlayedCount = blockedPlayedCount;
            IgnoredResultCount = ignoredResultCount;
            MissingRuleSkipCount = missingRuleSkipCount;
            MissingAudioServiceSkipCount = missingAudioServiceSkipCount;
            MissingCueSkipCount = missingCueSkipCount;
            CooldownSkipCount = cooldownSkipCount;
            PlaybackFailedSkipCount = playbackFailedSkipCount;
            LastCueKey = lastCueKey ?? string.Empty;
            LastEventId = lastEventId;
            LastEmitterId = lastEmitterId;
        }

        public long Frame { get; }
        public int ProcessedCount { get; }
        public int PlayedCount { get; }
        public int DamagePlayedCount { get; }
        public int BlockedPlayedCount { get; }
        public int IgnoredResultCount { get; }
        public int MissingRuleSkipCount { get; }
        public int MissingAudioServiceSkipCount { get; }
        public int MissingCueSkipCount { get; }
        public int CooldownSkipCount { get; }
        public int PlaybackFailedSkipCount { get; }
        public string LastCueKey { get; }
        public int LastEventId { get; }
        public int LastEmitterId { get; }

        public static CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample Capture(
            CharacterTestWeaponHitAudioSummary summary)
        {
            return new CharacterTestRuntimeAiLabFrameSyncWeaponHitAudioSample(
                frame: summary.Frame.Value,
                processedCount: summary.ProcessedCount,
                playedCount: summary.PlayedCount,
                damagePlayedCount: summary.DamagePlayedCount,
                blockedPlayedCount: summary.BlockedPlayedCount,
                ignoredResultCount: summary.IgnoredResultCount,
                missingRuleSkipCount: summary.MissingRuleSkipCount,
                missingAudioServiceSkipCount: summary.MissingAudioServiceSkipCount,
                missingCueSkipCount: summary.MissingCueSkipCount,
                cooldownSkipCount: summary.CooldownSkipCount,
                playbackFailedSkipCount: summary.PlaybackFailedSkipCount,
                lastCueKey: summary.LastCueKey,
                lastEventId: summary.LastEventId,
                lastEmitterId: summary.LastEmitterId);
        }
    }
}
