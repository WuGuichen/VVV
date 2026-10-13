using System;
using System.Globalization;
using System.Linq;
using MxFramework.Game.CharacterTest.TacticalCard;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private void OnNumbersChanged()
        {
            if (_binding) return;
            ReadNumbers(); RefreshConfiguration();
        }

        private bool ReadNumbers()
        {
            bool fixedMode = _session.Draft.InitialHand.Exists(card => card.FixedAssembly);
            int cost = _session.Draft.PreparationCost;
            if (!int.TryParse(_momentum.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int motion)
                || motion < 0 || motion > MomentumLedger.Maximum
                || !int.TryParse(_seed.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)
                || !fixedMode && (!int.TryParse(_cost.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out cost) || cost < 0))
            { _configurationIssue = fixedMode ? "初始动势须为 0–100；种子须为整数。"
                : "初始动势须为 0–100；种子须为整数；测试准备费用须为非负整数。"; return false; }
            _session.Draft.InitialMomentum = motion; _session.Draft.Seed = seed; _session.Draft.PreparationCost = cost;
            _configurationIssue = "";
            return true;
        }

        private void RequestStart()
        {
            if (!ReadNumbers() || !ValidateHand()) { RefreshReadout(); return; }
            if (_session.OwnsSession) SetVisible(_restartControls, true);
            else _session.Start(false);
        }

        private bool ValidateHand()
        {
            if (_session.Draft.InitialHand.Count == 0)
            { _configurationIssue = "请至少添加一张初始手牌。"; return false; }
            if (_session.Draft.InitialHand.Any(item => item.FixedAssembly) && _session.Draft.InitialHand.Any(item =>
                item.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId || TacticalInteractionTrialCards.TryCreate(item.CardId, out _)
                    || _session.Content.Catalog.Card(item.CardId)?.InteractionPreparation != null))
            { _configurationIssue = "固定组合试验只允许行动主卡；请移除旧独立准备／测试工具卡。"; return false; }
            foreach (var item in _session.Draft.InitialHand)
            {
                string assemblyIssue = CharacterTestRuntimeAiLabFixedAssembly.Validate(item);
                if (assemblyIssue != null) { _configurationIssue = assemblyIssue; return false; }
                if (_session.Content.Catalog.Card(item.CardId)?.HasActionSlot != true) continue;
                if (!IsEligibleAction(item.TacticalActionId))
                { _configurationIssue = "请为「" + CardName(item.CardId) + "」选择当前玩家角色可用的动作。"; return false; }
            }
            return true;
        }

        private bool IsEligibleAction(string id)
        {
            var action = _session.Content.Catalog.Action(id);
            string character = _session.Content.Character(_session.Draft.PlayerCharacterId).TacticalDefaults?.CharacterStableId;
            return action != null && action.CharacterStableIds.Contains(character);
        }

        private void RefreshConfiguration()
        {
            if (_root == null || _session.Draft == null) return;
            _player.title = _session.Content.Character(_session.Draft.PlayerCharacterId).DisplayName ?? "请选择角色";
            _opponent.title = _session.Content.Character(_session.Draft.OpponentCharacterId).DisplayName ?? "请选择角色";
            _player.tooltips = _player.title;
            _opponent.tooltips = _opponent.title + "\n点击配置角色、行为牌、动作、试验起点或固定组合手测预设。";
            Required("playerName").text = _player.title; Required("opponentName").text = _opponent.title;
            Required("playerName").asTextField.autoSize = Required("opponentName").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            BindPortrait(_player, _session.Draft.PlayerCharacterId); BindPortrait(_opponent, _session.Draft.OpponentCharacterId);
            RefreshPreparationFeeControls();
            _configuration.numItems = EditingDeck.Count;
            _configuration.RefreshVirtualList();
            _configurationTitle.text = "已装配卡组 · " + EditingDeck.Count + " 张主卡";
            bool valid = ReadNumbers() && ValidateHand();
            _start.enabled = valid;
            _start.title = "开始实验";
            _resume.title = "继续实验";
            if (_offers != null) { RefreshMainOffers(); RefreshAssemblyTarget(); }
            if (_roles != null) { _roles.numItems = _session.Draft.ParticipantCount; _roles.RefreshVirtualList(); }
            RefreshReadout();
        }

        private void RenderConfiguration(int index, Fgui.GObject item)
        {
            var row = item.asCom; var selection = _session.Draft.InitialHand[index];
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.RowControls);
            row.GetChild("slotLabel").touchable = false; row.GetChild("slotLabel").text = "第 " + (index + 1) + " 张";
            var face = row.GetChild("face").asCom;
            BindPreviewFace(face, selection, _session.Draft.PlayerCharacterId, _session.Draft.PreparationCost, 300);
            face.onClick.Set(context => { context.StopPropagation(); OpenPicker(TrialPickerMode.ReplaceCard, index); });
            RowButton(row, "replaceCard", () => OpenPicker(TrialPickerMode.ReplaceCard, index));
            RowButton(row, "copyCard", () => { _session.Draft.InitialHand.Insert(index + 1, selection.Clone()); RefreshConfiguration(); });
            RowButton(row, "remove", () => { _session.Draft.InitialHand.RemoveAt(index); RefreshConfiguration(); });
            var action = RowButton(row, "chooseAction", () => OpenPicker(TrialPickerMode.Assembly, index));
            bool needsAction = _session.Content.Catalog.Card(selection.CardId)?.HasActionSlot == true;
            action.enabled = needsAction;
            action.title = !needsAction ? "无需绑定动作" : IsEligibleAction(selection.TacticalActionId)
                ? _session.Content.Catalog.Action(selection.TacticalActionId).DisplayName + " ▾" : "请选择装配动作 ▾";
            if (needsAction) action.title += "\n" + CharacterTestRuntimeAiLabFixedAssembly.Summary(selection);
            action.tooltips = action.title;
            action.GetChild("title").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            action.GetChild("title").asTextField.singleLine = false;
        }

        private void RefreshPage()
        {
            bool setup = _session.Page == CharacterTestAiLabCardTrialPage.Configuration;
            bool picker = _pickerMode != TrialPickerMode.None;
            _renderedPage = _session.Page;
            SetVisible(_configurationControls, setup && !picker);
            SetVisible(_observationControls, !setup && !picker);
            SetVisible(_pickerControls, picker);
            SetVisible(_restartControls, false);
            _resume.visible = setup && !picker && _session.OwnsSession;
            _title.visible = _subtitle.visible = Required("headerBackground").visible = setup || picker;
            Required("footerBackground").visible = setup || picker;
            if (setup || picker) _trialMenuOpen = false;
            Required("returnObservation").visible = setup || picker;
            RefreshObservationVisibility();
            RefreshPreparationFeeControls();
            _title.text = picker ? "选择配置内容" : "试验配置";
            _subtitle.text = picker ? "从当前正式目录直接选择；确认后用于下一次试验。"
                : setup ? "参战角色、初始条件与手牌配好后，进入场景试验。" : "选择手牌后确认；需要定点时点击有效地面位置。";
            _tooltips.Hide();
            RefreshReadout();
            RefreshSetupSection();
        }

        private void RefreshObservationVisibility()
        {
            bool observing = _session.Page == CharacterTestAiLabCardTrialPage.Observation && _pickerMode == TrialPickerMode.None;
            SetVisible(_trialMenuControls, observing && _trialMenuOpen);
            bool details = observing && _conditionsDetailsVisible;
            _conditionsDetail.visible = _frozenHand.visible = Required("conditionsDetailBackground").visible = Required("closeConditions").visible = details;
            bool pending = observing && _session.Hud?.CanCancelPending == true;
            _confirm.visible = _cancel.visible = pending;
            Required("worldHint").visible = pending && string.IsNullOrEmpty(_observationFeedback.text);
            _observationFeedback.visible = observing && !string.IsNullOrEmpty(_observationFeedback.text);
        }

        private void RefreshReadout()
        {
            if (_root == null) return;
            _conditions.text = _session.OwnsSession ? (_session.HasConfigurationChanges
                ? "配置已修改 · 新开试验时生效；当前试验保留原条件。" : "当前试验仍保留，可返回继续。") : "开始后冻结本次条件；同条件重试使用这份冻结配置。";
            _feedback.text = !string.IsNullOrEmpty(_configurationIssue) ? _configurationIssue : _session.Issue;
            var stage = _session.Runtime?.Stage; var hud = _session.Hud;
            _observationFeedback.text = !string.IsNullOrEmpty(_session.Issue) ? _session.Issue
                : !string.IsNullOrEmpty(hud?.FaultMessage) ? hud.FaultMessage : hud?.CommandFeedbackMessage ?? "";
            _conditionsDetail.text = "本次冻结条件\n" + _session.FrozenParticipants;
            if (stage == null) { _readout.text = "尚未开始试验"; return; }
            string phase = stage.Phase switch {
                TacticalStagePhase.Booting => "准备试验", TacticalStagePhase.AwaitingIngressSettle => "等待角色就绪",
                TacticalStagePhase.Planning => "等待出牌", TacticalStagePhase.StageCompleted => "试验已结束", _ => stage.Time.IsWorldPaused ? "已暂停" : "执行中"
            };
            _readout.text = $"{phase} · 回合 {stage.TurnIndex} · 动势 {stage.Ledger.Spendable}"
                + (stage.Ledger.Reserved > 0 ? $" · 预留 {stage.Ledger.Reserved}" : "");
            _conditionsDetail.text += $"\n手牌 {stage.Deck.Hand.Count} · 备用 {stage.Deck.DrawPile.Count} · 弃牌 {stage.Deck.DiscardPile.Count} · 已消耗 {stage.Deck.ExhaustPile.Count}";
            RefreshFixedReadout(stage);
            var pending = stage.Deck.PendingAction;
            Required("worldHint").text = pending != null && stage.Driver.GetRequiredInputKind(pending) == TacticalExecutionInputKind.WorldPoint
                ? "点击有效地面位置，再确认出牌。" : "已选择手牌，确认后执行。";
            RefreshObservationVisibility();
        }

        private void RenderFrozenHand(int index, Fgui.GObject item)
        {
            var row = item.asCom; var card = _session.FrozenHand[index];
            ValidateControls(row, CharacterTestAiLabCardTrialControlContract.CardChoiceControls);
            row.GetChild("select").asButton.enabled = false;
            row.GetChild("select").asButton.title = "第 " + (index + 1) + " 张 · 已冻结";
            var face = row.GetChild("face").asCom; face.onClick.Clear();
            BindPreviewFace(face, card, _session.FrozenPlayerCharacterId, _session.FrozenPreparationCost, 280);
        }

        private string CardName(string id) => id == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId ? "测试准备"
            : TacticalInteractionTrialCards.TryCreate(id, out var trial) ? trial.Name : _session.Content.Catalog.Card(id)?.DisplayName ?? "失效卡引用";
        private string CardDescription(string id) => id == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId
            ? "仅观察真实出牌事务；不避伤、不换位、不改变体型。"
            : TacticalInteractionTrialCards.TryCreate(id, out var trial) ? trial.Description : _session.Content.Catalog.Card(id)?.Description ?? "";
        private string CardKind(string id) => id == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId ? "测试工具卡"
            : TacticalInteractionTrialCards.TryCreate(id, out _) || _session.Content.Catalog.Card(id)?.InteractionPreparation != null ? "交互准备卡" : "行动卡";
        private int CardCost(string id) => id == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId ? _session.Draft.PreparationCost
            : TacticalInteractionTrialCards.TryCreate(id, out var trial) ? trial.BaseMomentumCost : _session.Content.Catalog.Card(id)?.MotionCost ?? 0;
    }
}
