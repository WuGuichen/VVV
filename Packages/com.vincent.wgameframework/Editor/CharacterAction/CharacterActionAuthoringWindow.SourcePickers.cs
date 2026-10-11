using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private VisualElement CreateSourcePopupField(
            string fieldLabel,
            CharacterActionAuthoringSourceEntry[] sources,
            string currentDirectory,
            string emptyLabel,
            Action<int> onSourceIndexSelected,
            bool showLabel = true)
        {
            sources = sources ?? Array.Empty<CharacterActionAuthoringSourceEntry>();
            var labels = new List<string> { CharacterActionAuthoringUiLabels.ToMenuSafeLabel(emptyLabel) };
            var sourceIndices = new List<int> { -1 };

            for (int i = 0; i < sources.Length; i++)
            {
                labels.Add(sources[i].FlatChoiceLabel);
                sourceIndices.Add(i);
            }

            int selectedIndex = 0;
            int currentIndex = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(sources, currentDirectory);
            if (currentIndex >= 0)
                selectedIndex = currentIndex + 1;

            var container = new VisualElement();
            container.AddToClassList("ca-source-item");

            if (showLabel)
            {
                var caption = new Label(fieldLabel);
                caption.AddToClassList("ca-source-label");
                container.Add(caption);
            }

            var popup = new PopupField<string>(string.Empty, labels, selectedIndex);
            popup.AddToClassList("ca-source-popup");
            popup.RegisterValueChangedCallback(evt =>
            {
                int choiceIndex = labels.IndexOf(evt.newValue);
                if (choiceIndex < 0)
                    return;

                int sourceIndex = sourceIndices[choiceIndex];
                if (sourceIndex < 0)
                    return;

                onSourceIndexSelected?.Invoke(sourceIndex);
            });
            container.Add(popup);
            return container;
        }

        private bool IsActionSetBoundByProfile()
        {
            if (string.IsNullOrWhiteSpace(_profileSourceDirectory))
                return false;

            string[] ids = _model?.Profile?.ActionSetIds;
            return ids != null && ids.Length > 0 && !string.IsNullOrWhiteSpace(ids[0]);
        }
    }
}
