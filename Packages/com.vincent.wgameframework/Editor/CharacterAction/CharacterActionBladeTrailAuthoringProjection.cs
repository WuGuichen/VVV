using System;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>LowFreqAlloc during authoring normalization; identities are allocated only by explicit UI edits.</summary>
    internal static class CharacterActionBladeTrailAuthoringProjection
    {
        internal static void Enable(CharacterActionJsonDto action, CombatTrackEventJsonDto hit)
        {
            if (hit.EquipmentSlotId <= 0 || hit.HitVolume != null
                || !string.Equals(hit.Kind, CharacterActionTrackEventKind.HitTraceWindow.ToString(), StringComparison.Ordinal))
                return;

            hit.BladeTrail = hit.BladeTrail ?? new CharacterActionBladeTrailJsonDto();
            if (hit.BladeTrail.StableEventId <= 0)
            {
                string source = CharacterContentIdentityAuthoringCatalog.GetSourceKey(401, hit.StableEventId);
                hit.BladeTrail.StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401,
                    "blade-trail/" + action.ActionId + "/" + source);
            }
            hit.BladeTrail.Enabled = true;
        }

        internal static void Synchronize(CharacterActionJsonDto action)
        {
            bool needsProjection = false;
            foreach (CombatTrackEventJsonDto hit in action.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>())
                needsProjection |= hit?.BladeTrail?.Enabled == true;
            foreach (PresentationTrackEventJsonDto evt in action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>())
                needsProjection |= evt?.BladeTrailGenerated == true;
            if (!needsProjection) return;
            JObject json = JObject.FromObject(action, JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
            CharacterActionBladeTrailJsonProjection.SynchronizeAction(json, ResolveIdentity, ResolveDefinition);
            action.PresentationTrack = json["presentationTrack"]?.ToObject<PresentationTrackJsonDto>(
                JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings));
            var orders = CharacterContentIdentityAuthoringCatalog.ReadEventOrders();
            foreach (PresentationTrackEventJsonDto evt in action.PresentationTrack?.Events ?? Array.Empty<PresentationTrackEventJsonDto>())
                if (evt.BladeTrailGenerated)
                    evt.StableEventOrder = orders[evt.StableEventId];
        }

        private static int ResolveIdentity(int domain, JToken value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString())) return 0;
            return value.Type == JTokenType.Integer ? value.Value<int>()
                : CharacterContentIdentityAuthoringCatalog.GetExisting(domain, value.Value<string>());
        }

        private static int ResolveDefinition(string resource)
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json");
            JObject catalog = JObject.Parse(File.ReadAllText(path));
            foreach (JObject definition in ((JArray)catalog["definitions"]).Children<JObject>())
                if (definition["prefabResource"] is JObject key && string.Equals(
                    key.Value<string>("id") + ":" + key.Value<string>("typeId") + "@" + key.Value<string>("packageId"),
                    resource, StringComparison.Ordinal))
                    return definition.Value<int>("definitionId");
            throw new InvalidOperationException("Default blade trail is missing from the formal VFX catalog.");
        }
    }
}
