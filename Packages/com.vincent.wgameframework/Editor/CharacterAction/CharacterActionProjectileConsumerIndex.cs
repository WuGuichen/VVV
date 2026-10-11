using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Read-only index of CharacterAction and Ability consumers of compiled projectile requests.</summary>
    internal sealed class CharacterActionProjectileConsumerIndex
    {
        private readonly Dictionary<string, Entry> _byRequestId;

        private CharacterActionProjectileConsumerIndex(Dictionary<string, Entry> byRequestId)
        {
            _byRequestId = byRequestId ?? new Dictionary<string, Entry>(StringComparer.Ordinal);
        }

        public static CharacterActionProjectileConsumerIndex Build(
            CharacterActionProjectileAuthoringStore store,
            CharacterActionAuthoringModel model,
            JObject abilityAuthoringRoot,
            string actionSourcePath,
            string abilitySourcePath)
        {
            store ??= CharacterActionProjectileAuthoringStore.LoadDefault();
            var byRequestId = new Dictionary<string, Entry>(StringComparer.Ordinal);
            for (int i = 0; i < store.Records.Count; i++)
            {
                CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record = store.Records[i];
                if (!CharacterActionProjectileRequestEligibility.IsCompiledReferenceable(record))
                    continue;
                byRequestId[record.RequestId] = new Entry(record);
            }

            AddActionUsages(byRequestId, model, actionSourcePath);
            AddAbilityTimelineUsages(byRequestId, abilityAuthoringRoot, abilitySourcePath);
            return new CharacterActionProjectileConsumerIndex(byRequestId);
        }

        public bool TryGet(string requestId, out Entry entry)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                entry = null;
                return false;
            }
            return _byRequestId.TryGetValue(requestId, out entry);
        }

        private static void AddActionUsages(
            Dictionary<string, Entry> byRequestId,
            CharacterActionAuthoringModel model,
            string sourcePath)
        {
            List<CharacterActionJsonDto> actions = model?.Actions;
            for (int actionIndex = 0; actions != null && actionIndex < actions.Count; actionIndex++)
            {
                CharacterActionJsonDto action = actions[actionIndex];
                GameplayTrackEventJsonDto[] events = action?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
                for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                {
                    GameplayTrackEventJsonDto evt = events[eventIndex];
                    if (evt == null
                        || !string.Equals(evt.Kind, CharacterActionTrackEventKind.SendGameplayRequest.ToString(), StringComparison.Ordinal)
                        || !byRequestId.TryGetValue(CharacterActionAuthoringIds.Label(evt.RequestId, 408), out Entry entry))
                    {
                        continue;
                    }

                    entry.Add(new Usage(
                        CharacterActionProjectileConsumerKind.ActionGameplayRequest,
                        new CharacterActionId(action?.ActionId ?? 0),
                        string.Empty,
                        CharacterActionAuthoringIds.Label(evt.StableEventId, 401),
                        evt.Frame,
                        AppendPath(sourcePath, "actions[" + actionIndex.ToString(CultureInfo.InvariantCulture)
                            + "].gameplayTrack.events[" + eventIndex.ToString(CultureInfo.InvariantCulture) + "]")));
                }
            }
        }

        private static void AddAbilityTimelineUsages(
            Dictionary<string, Entry> byRequestId,
            JObject root,
            string sourcePath)
        {
            JArray timelines = root?["effectTimelines"] as JArray;
            for (int timelineIndex = 0; timelines != null && timelineIndex < timelines.Count; timelineIndex++)
            {
                if (!(timelines[timelineIndex] is JObject timeline))
                    continue;
                string timelineId = timeline["timelineId"]?.Value<string>() ?? string.Empty;
                JArray events = timeline["events"] as JArray;
                for (int eventIndex = 0; events != null && eventIndex < events.Count; eventIndex++)
                {
                    if (!(events[eventIndex] is JObject evt)
                        || !string.Equals(evt["eventKind"]?.Value<string>(), "SpawnProjectile", StringComparison.Ordinal)
                        || !byRequestId.TryGetValue(evt["requestId"]?.Value<string>() ?? string.Empty, out Entry entry))
                    {
                        continue;
                    }

                    entry.Add(new Usage(
                        CharacterActionProjectileConsumerKind.AbilityEffectTimeline,
                        CharacterActionId.None,
                        timelineId,
                        evt["eventId"]?.Value<string>(),
                        evt["frame"]?.Value<int>() ?? 0,
                        AppendPath(sourcePath, "effectTimelines[" + timelineIndex.ToString(CultureInfo.InvariantCulture)
                            + "].events[" + eventIndex.ToString(CultureInfo.InvariantCulture) + "]")));
                }
            }
        }

        private static string AppendPath(string path, string jsonPath)
            => string.IsNullOrWhiteSpace(path) ? jsonPath : path + " " + jsonPath;

        internal sealed class Entry
        {
            private readonly List<Usage> _usages = new List<Usage>();

            internal Entry(CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
            {
                Record = record ?? throw new ArgumentNullException(nameof(record));
            }

            public CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord Record { get; }
            public IReadOnlyList<Usage> Usages => _usages;
            public int UsageCount => _usages.Count;
            internal void Add(Usage usage) => _usages.Add(usage);
        }

        internal readonly struct Usage
        {
            internal Usage(
                CharacterActionProjectileConsumerKind kind,
                CharacterActionId actionId,
                string timelineId,
                string eventId,
                int frame,
                string sourcePath)
            {
                Kind = kind;
                ActionId = actionId;
                TimelineId = timelineId ?? string.Empty;
                EventId = eventId ?? string.Empty;
                Frame = frame;
                SourcePath = sourcePath ?? string.Empty;
            }

            public CharacterActionProjectileConsumerKind Kind { get; }
            public CharacterActionId ActionId { get; }
            public string TimelineId { get; }
            public string EventId { get; }
            public int Frame { get; }
            public string SourcePath { get; }
        }
    }

    internal enum CharacterActionProjectileConsumerKind
    {
        ActionGameplayRequest = 0,
        AbilityEffectTimeline = 1,
    }
}
