using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private readonly Dictionary<(string Card, string Action, int Character, int Fee), CharacterTestCardUiPreviewData> _previewFaces = new();

        // LowFreqAlloc: projects exact published source and bindings without freezing a deployment or starting a trial.
        private CharacterTestCardUiPreviewData PreviewFace(CharacterTestRuntimeAiLabCardTrialSelection selection, int characterId, int fee)
        {
            var key = (selection.CardId, selection.TacticalActionId ?? "", characterId, fee);
            if (_previewFaces.TryGetValue(key, out var cached)) return cached;
            var metadata = _session.Content.Catalog.Card(selection.CardId);
            int cost = selection.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId ? fee : CardCost(selection.CardId);
            var data = new CharacterTestCardUiPreviewData { Id = selection.CardId, Name = CardName(selection.CardId), Cost = cost,
                IconKey = metadata?.IconKey ?? "ui.behavior-card.custom", Description = CardDescription(selection.CardId),
                Actions = Array.Empty<CharacterTestTacticalActionLocation>() };
            var source = metadata?.PresentationSource;
            if (source != null)
            {
                var action = _session.Content.Catalog.Action(selection.TacticalActionId);
                var names = new Dictionary<string, string>(StringComparer.Ordinal);
                if (metadata.HasActionSlot)
                {
                    string name = action?.DisplayName ?? "未装配动作";
                    names[(source.IsLegacy ? "preview-legacy/" : "") + metadata.ActionSlotPath] = name;
                    data.Actions = new[] { new CharacterTestTacticalActionLocation { Path = metadata.ActionSlotPath, Label = metadata.ActionSlotPath,
                        ActionName = name, ActionId = action == null ? "" : CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(action.ActionId) } };
                }
                var document = CharacterTestTacticalCardPresentationProjection.Document(selection.CardId, data.Name, source);
                var notes = new List<string>();
                var inputs = CharacterTestTacticalCardPresentationProjection.Prepare(source, document, data.Actions, characterId, notes,
                    () => CharacterTestTacticalPreparationCatalog.Role(characterId, _slice.CharacterRuntime?.ActionRuntime), null, out string issue);
                CharacterTestTacticalCardPresentationProjection.Project(source, document, data, inputs, issue, names,
                    _session.CardTextProvider, _session.CardFrameSeconds, notes);
            }
            if (_previewFaces.Count >= 256) _previewFaces.Clear();
            // A failed metrics preparation can become available after the existing role resources finish loading.
            if (source == null || data.Prepared) _previewFaces[key] = data;
            return data;
        }

        private void BindPreviewFace(Fgui.GComponent face, CharacterTestRuntimeAiLabCardTrialSelection selection,
            int characterId, int fee, float width, bool selected = false)
        {
            var data = PreviewFace(selection, characterId, fee);
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(face);
            CharacterTestFairyGuiPlayerCardPresentation.Bind(face, data, 34);
            _skin.Bind(face, data);
            float scale = width / CharacterTestFairyGuiPlayerCardPresentation.NativeWidth;
            face.SetScale(scale, scale);
            face.opaque = true;
            face.GetChild("focus").visible = selected;
            _tooltips.Bind(face, CharacterTestFairyGuiTacticalDescriptionHints.TooltipText(data),
                new MxFramework.UI.FairyGui.MxFairyGuiTooltipOptions(preferredPlacement: MxFramework.UI.FairyGui.MxFairyGuiTooltipPlacement.Above,
                    minimumTextWidth: 320, maximumTextWidth: 560, allowPin: false));
        }

        private CharacterTestRuntimeAiLabCardTrialSelection PreviewSelection(string cardId)
        {
            string action = _pickerMode == TrialPickerMode.ReplaceCard && _pickerIndex >= 0
                ? _session.Draft.InitialHand[_pickerIndex].TacticalActionId : "";
            action = _session.Content.ResolveActionBinding(cardId, _session.Draft.PlayerCharacterId, action);
            return new CharacterTestRuntimeAiLabCardTrialSelection(cardId, action);
        }

        private void BindPortrait(Fgui.GComponent host, int characterId)
        {
            var icon = host.GetChild("icon")?.asLoader
                ?? throw new InvalidOperationException("角色头像组件缺少已发布的 icon loader。");
            if (host.GetChild("title") is Fgui.GObject title) title.visible = false;
            icon.touchable = false;
            icon.SetXY(8, 8); icon.SetSize(Mathf.Max(1, host.width - 16), Mathf.Max(1, host.height - 16));
            var images = _session.Content.Character(characterId).PresentationImages;
            _characterImages.Bind(icon, images?.ResolveResourceKey(false));
        }
    }
}
