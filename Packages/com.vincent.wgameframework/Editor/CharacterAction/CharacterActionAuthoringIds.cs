using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>
    /// Converts the editor's label-friendly fields at the authoring boundary.
    /// Runtime DTOs remain numeric; labels are resolved through the persisted
    /// content identity catalog instead of being hashed or silently discarded.
    /// </summary>
    internal static class CharacterActionAuthoringIds
    {
        internal static int Resolve(string value, int domainId, bool allocate = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
            {
                if (numeric <= 0)
                    return 0;
                CharacterContentIdentityAuthoringCatalog.GetSourceKey(domainId, numeric);
                return numeric;
            }
            return allocate
                ? CharacterContentIdentityAuthoringCatalog.GetOrAllocate(domainId, value)
                : CharacterContentIdentityAuthoringCatalog.GetExisting(domainId, value);
        }

        internal static string Label(int value, int domainId, string fallback = null)
        {
            if (value <= 0)
                return fallback ?? string.Empty;
            try
            {
                return CharacterContentIdentityAuthoringCatalog.GetSourceKey(domainId, value);
            }
            catch (Exception)
            {
                return fallback ?? value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string Label(int? value, int domainId, string fallback = null) =>
            Label(value.GetValueOrDefault(), domainId, fallback);

        internal static int[] ResolveMany(IEnumerable<string> values, int domainId)
        {
            if (values == null)
                return Array.Empty<int>();
            var result = new List<int>();
            foreach (string value in values)
            {
                int id = Resolve(value, domainId);
                if (id > 0 && !result.Contains(id))
                    result.Add(id);
            }
            return result.ToArray();
        }

        internal static string[] Labels(IEnumerable<int> values, int domainId, IEnumerable<string> fallbacks = null)
        {
            var result = new List<string>();
            if (values == null)
                return result.ToArray();
            string[] fallbackArray = fallbacks == null ? Array.Empty<string>() : new List<string>(fallbacks).ToArray();
            int index = 0;
            foreach (int value in values)
            {
                string fallback = index < fallbackArray.Length ? fallbackArray[index] : null;
                result.Add(Label(value, domainId, fallback));
                index++;
            }
            return result.ToArray();
        }

        internal static List<string> LabelsList(IEnumerable<int> values, int domainId)
        {
            return new List<string>(Labels(values, domainId));
        }
    }

    internal static class AudioCueIdentityAuthoringCatalog
    {
        internal static int GetExisting(string sourceKey) =>
            CharacterActionAuthoringIds.Resolve(sourceKey, 401, allocate: false);
    }

    internal static class CharacterActionDamageIdentityAuthoring
    {
        internal static string Label(int domainId, int id, string fallback) =>
            CharacterActionAuthoringIds.Label(id, domainId, fallback);

        internal static string FormulaLabel(int id, string fallback) =>
            CharacterActionAuthoringIds.Label(id, 410, fallback);

        internal static int ExistingId(int domainId, string sourceKey) =>
            CharacterActionAuthoringIds.Resolve(sourceKey, domainId, allocate: true);

        internal static int[] ExistingTags(IEnumerable<string> values) =>
            CharacterActionAuthoringIds.ResolveMany(values, 410);

        internal static int[] NormalizeIds(IEnumerable<int> values)
        {
            var result = new List<int>();
            if (values == null)
                return Array.Empty<int>();
            foreach (int value in values)
            {
                if (value <= 0)
                    continue;
                CharacterContentIdentityAuthoringCatalog.GetSourceKey(410, value);
                if (!result.Contains(value))
                    result.Add(value);
            }
            return result.ToArray();
        }

        internal static string[] TagLabels(IEnumerable<int> values, IEnumerable<string> fallbacks) =>
            CharacterActionAuthoringIds.Labels(values, 410, fallbacks);
    }

    internal static class CharacterActionEventIdentityAuthoring
    {
        internal static void AssignMissingEventOrders(CharacterActionJsonDto action)
        {
            if (action == null)
                return;

            IReadOnlyDictionary<int, int> orders = null;
            AssignMissingOrder(action.MotionTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
            AssignMissingOrder(action.AnimationTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
            AssignMissingOrder(action.CombatTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
            AssignMissingOrder(action.GameplayTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
            AssignMissingOrder(action.PresentationTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
            AssignMissingOrder(action.DebugTrack?.Events,
                evt => evt.StableEventId, evt => evt.StableEventOrder,
                (evt, order) => evt.StableEventOrder = order, ref orders);
        }

        private static void AssignMissingOrder<T>(
            T[] events,
            Func<T, int> getId,
            Func<T, int> getOrder,
            Action<T, int> setOrder,
            ref IReadOnlyDictionary<int, int> orders)
            where T : class
        {
            if (events == null)
                return;
            for (int i = 0; i < events.Length; i++)
            {
                T evt = events[i];
                if (evt == null || getOrder(evt) > 0)
                    continue;
                int id = getId(evt);
                if (id <= 0)
                    continue;
                orders ??= CharacterContentIdentityAuthoringCatalog.ReadEventOrders();
                if (!orders.TryGetValue(id, out int order))
                    throw new InvalidOperationException("Event identity is missing or retired: " + id);
                setOrder(evt, order);
            }
        }

        internal static void AssignCopiedEventIds(CharacterActionJsonDto action)
        {
            if (action == null)
                return;
            Assign(action, action.MotionTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "motion");
            Assign(action, action.MotionTrack?.MovementControlKeys, (key, i) => key.StableEventId,
                (key, id) => { key.StableEventId = id; key.StableEventOrder = 0; }, "movement-control");
            Assign(action, action.MotionTrack?.RotationControlKeys, (key, i) => key.StableEventId,
                (key, id) => { key.StableEventId = id; key.StableEventOrder = 0; }, "rotation-control");
            Assign(action, action.CombatTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "combat");
            Assign(action, action.GameplayTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "gameplay");
            Assign(action, action.AnimationTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "animation");
            Assign(action, action.PresentationTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "presentation");
            Assign(action, action.DebugTrack?.Events, (evt, i) => evt.StableEventId,
                (evt, id) => evt.StableEventId = id, "debug");
        }

        private static void Assign<T>(CharacterActionJsonDto action, T[] events,
            Func<T, int, int> get, Action<T, int> set, string track)
            where T : class
        {
            if (events == null)
                return;
            for (int i = 0; i < events.Length; i++)
            {
                T evt = events[i];
                if (evt == null)
                    continue;
                int source = get(evt, i);
                string key = "event/" + action.ActionId.ToString(CultureInfo.InvariantCulture)
                    + "/copy." + track + "/" + i.ToString(CultureInfo.InvariantCulture);
                set(evt, CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, key));
            }
        }
    }
}
