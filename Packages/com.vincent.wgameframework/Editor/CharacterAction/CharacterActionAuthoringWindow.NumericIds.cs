using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddTextField(VisualElement parent, string label, int value, Action<int> setter, int domainId = 401)
        {
            AddTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId),
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }

        private void AddTimelineTextField(
            VisualElement parent,
            string label,
            int value,
            Action<int> setter,
            bool requestFullTracksRefresh = false,
            int domainId = 401)
        {
            AddTimelineTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId),
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), requestFullTracksRefresh);
        }

        private void AddReferenceTextField(
            VisualElement parent,
            string label,
            int value,
            List<int> choices,
            Action<int> setter,
            int domainId = 401)
        {
            var labels = new List<string>();
            if (choices != null)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    string text = CharacterActionAuthoringIds.Label(choices[i], domainId);
                    if (!string.IsNullOrWhiteSpace(text) && !labels.Contains(text))
                        labels.Add(text);
                }
            }
            AddReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), labels,
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }

        private void AddTimelineReferenceTextField(
            VisualElement parent,
            string label,
            int value,
            List<int> suggestions,
            Action<int> setter,
            bool requestFullTracksRefresh = false,
            int domainId = 401)
        {
            var labels = new List<string>();
            if (suggestions != null)
            {
                for (int i = 0; i < suggestions.Count; i++)
                {
                    string text = CharacterActionAuthoringIds.Label(suggestions[i], domainId);
                    if (!string.IsNullOrWhiteSpace(text) && !labels.Contains(text))
                        labels.Add(text);
                }
            }
            AddTimelineReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), labels,
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), requestFullTracksRefresh);
        }

        private void AddCsvField(VisualElement parent, string label, List<int> values, int domainId = 401)
        {
            values = values ?? new List<int>();
            AddTextField(parent, label, string.Join(", ", CharacterActionAuthoringIds.Labels(values, domainId)), text =>
            {
                values.Clear();
                string[] tokens = (text ?? string.Empty).Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++)
                {
                    int id = CharacterActionAuthoringIds.Resolve(tokens[i].Trim(), domainId);
                    if (id > 0 && !values.Contains(id))
                        values.Add(id);
                }
            });
        }

        private void AddGameplayRequestField(
            VisualElement parent,
            string label,
            int value,
            CharacterActionProjectileAuthoringStore projectileStore,
            CharacterActionJsonDto action,
            Action<int> setter,
            bool timelineInspector,
            int domainId = 408)
        {
            AddGameplayRequestField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), projectileStore, action,
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), timelineInspector);
        }

        private void AddTargetPolicyField(VisualElement parent, string label, int value, Action<int> setter, int domainId = 406)
        {
            string current = CharacterActionAuthoringIds.Label(value, domainId);
            AddTargetPolicyField(parent, label, current, text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }

        private void AddEquipmentSlotField(VisualElement parent, string label, int value,
            CharacterActionJsonDto action, Action<int> setter, int domainId = 405)
        {
            AddEquipmentSlotField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), action,
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }

        private void AddTimelineEquipmentSlotField(VisualElement parent, string label, int value,
            CharacterActionJsonDto action, Action<int> setter, bool requestFullTracksRefresh = false, int domainId = 405)
        {
            AddTimelineEquipmentSlotField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), action,
                text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), requestFullTracksRefresh);
        }

        private void AddHitVolumeSocketField(VisualElement parent, string label, int value, List<int> socketIds,
            Action<int> setter, bool timeline, bool requestFullTracksRefresh, int domainId = 400)
        {
            var labels = new List<string>();
            if (socketIds != null)
            {
                for (int i = 0; i < socketIds.Count; i++)
                {
                    string text = CharacterActionAuthoringIds.Label(socketIds[i], domainId);
                    if (!string.IsNullOrWhiteSpace(text) && !labels.Contains(text))
                        labels.Add(text);
                }
            }
            if (timeline)
                AddTimelineReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), labels,
                    text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), requestFullTracksRefresh);
            else
                AddReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), labels,
                    text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }

        private void AddHitVolumeSocketField(VisualElement parent, string label, int value, List<string> socketLabels,
            Action<int> setter, bool timeline, bool requestFullTracksRefresh, int domainId = 400)
        {
            if (timeline)
                AddTimelineReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), socketLabels,
                    text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)), requestFullTracksRefresh);
            else
                AddReferenceTextField(parent, label, CharacterActionAuthoringIds.Label(value, domainId), socketLabels,
                    text => setter(CharacterActionAuthoringIds.Resolve(text, domainId)));
        }
    }
}
