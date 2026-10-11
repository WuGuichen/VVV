using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class TacticalCardHud
    {
        private TacticalCardInstance _directPlayCard;
        private ulong _directPlayStage, _directPlayTurn, _directPlayRevision;
        private TacticalWorldPoint? _directPlayPoint;
        private string _directPlayIssue="";
        private float _directPlayIssueUntil;
        internal string DirectPlayFeedback => Time.unscaledTime < _directPlayIssueUntil ? _directPlayIssue : "";
        private bool _directPointSubmitted, _directCancelRequested, _directCancelSubmitted, _directConfirmSubmitted;
        internal System.Collections.Generic.IReadOnlyList<CharacterTestCharacterEntity> BattleActors =>
            _preparationManager?.RuntimeContext?.Slice?.Services.CharacterEntities;
        internal MxFramework.Gameplay.GameplayEntityId BattlePlayerActor =>
            _coordinator != null && _coordinator.TryGetRoster(out var actor,out _) ? actor : default;
        internal Vector3 BattleHeadPosition(CharacterTestCharacterEntity entity)
        {
            var bridge=_preparationManager?.RuntimeContext?.Slice?.Services.CharacterRuntime?.ViewBridge;
            if(bridge!=null && bridge.TryResolveAbstractBoneAnchor(entity.Id.Value,
                MxFramework.CharacterRuntimeSpawn.CharacterAbstractBoneIds.Head,out var head,out _))
                return head+Vector3.up*.3f;
            return new Vector3(entity.Transform.X,entity.Transform.Y+entity.Physics.EffectiveHalfHeightMeters*2+.3f,entity.Transform.Z);
        }
        internal bool DirectPlayActive => _directPlayCard != null;
        internal bool DirectPlayNeedsPoint => DirectPlayActive && NeedsPoint;
        internal bool BattleInputBlocked => _inventoryOpen || _automaticBattlePresentation
            || _preparationManager?.RuntimeContext?.UiRuntime?.HasTacticalLoading == true
            || _preparationManager?.RuntimeContext?.UiRuntime?.FairyGuiSettingsPanelHost?.IsOpen == true;
        internal void OpenBattleSettings() => _preparationManager?.RuntimeContext?.UiRuntime?.FairyGuiSettingsPanelHost?.Open();

        internal bool TryDirectPlayHandSlot(int slot, TacticalWorldPoint? point = null)
        {
            if (BattleInputBlocked || DirectPlayActive || !CanSelectHandSlot(slot) || _coordinator.Deck.PendingAction != null) return false;
            var card = GetHandSlot(slot);
            _directPlayIssue="";
            if (!TrySelectHandSlot(slot)) return false;
            if (card.Definition.Kind == TacticalCardKind.Utility) return true;
            _directPlayCard = card; _directPlayStage = _coordinator.StageRunId;
            _directPlayTurn = TurnIndex; _directPlayRevision = _coordinator.SnapshotRevision;
            _directPlayPoint = point; _directPointSubmitted = _directCancelRequested = _directCancelSubmitted = _directConfirmSubmitted = false;
            return true;
        }

        internal bool CanDragHandSlot(int slot) => CanSelectHandSlot(slot)
            || DirectPlayNeedsPoint && ReferenceEquals(GetHandSlot(slot),_coordinator.Deck.PendingAction);
        internal bool HandCardNeedsWorldPoint(int slot) => _coordinator != null
            && _coordinator.Driver.GetRequiredInputKind(GetHandSlot(slot)) == TacticalExecutionInputKind.WorldPoint;
        internal bool TryCompleteDirectPoint(TacticalWorldPoint point)
        {
            if (!DirectPlayNeedsPoint || !TrySelectWorldPoint(point)) return false;
            _directPlayPoint=point;_directPointSubmitted=true;return true;
        }
        internal void ShowDirectPlayIssue(string issue) { _directPlayIssue=issue;_directPlayIssueUntil=Time.unscaledTime+2.5f; }
        internal void PreviewDirectDragPoint(int slot,TacticalWorldPoint point)
        {
            var rules=(GetHandSlot(slot)?.Definition as TacticalActionCardDefinition)?.PointMove;
            if(rules==null)return;
            _pointVisualCandidate=point;_hasPointVisualCandidate=true;_pointPickStatus=TacticalInputStatus.Valid;
            UpdatePointVisuals(rules,default);
        }
        internal void ClearDirectDragVisuals() => HidePointVisuals();
        internal void WarmDirectDragVisuals()
        {
            if(_pointPreview!=null||_pointVisualUnavailable)return;
            _pointPreview=new TacticalPointSelectionPreview(transform);
            _pointVisualUnavailable=!_pointPreview.IsAvailable;_pointPreview.Hide();
        }
        internal void PreviewDirectDragRange(int slot)
        {
            var rules=(GetHandSlot(slot)?.Definition as TacticalActionCardDefinition)?.PointMove;
            if(rules==null||!_coordinator.Driver.TryGetPointOrigin(out var origin)){HidePointVisuals();return;}
            WarmDirectDragVisuals();
            _pointPreview?.Show(origin,rules.Input.RadiusMillimeters,false,default,rules.ArrivalMillimeters,false);
        }

        internal bool TryPreviewCardPoint(int slot, Vector2 screen, out TacticalWorldPoint point)
        {
            point = default;
            var card = GetHandSlot(slot);
            if (card == null || _coordinator == null || UnityEngine.Camera.main == null) return false;
            var ray = UnityEngine.Camera.main.ScreenPointToRay(screen);
            if (!TacticalWorldPoint.TryFromMeters(ray.origin.x,ray.origin.y,ray.origin.z,out var origin)
                || !TacticalWorldPoint.TryFromMeters(ray.direction.x,ray.direction.y,ray.direction.z,out var direction)
                || _coordinator.Driver.TryPickGround(origin,direction,out point) != TacticalInputStatus.Valid) return false;
            if (_coordinator.Driver.GetRequiredInputKind(card) != TacticalExecutionInputKind.WorldPoint) return true;
            if (!_coordinator.TryGetRoster(out var actor,out _) || !_coordinator.Driver.TryGetPointOrigin(out var actorOrigin)) return false;
            var binding = new TacticalExecutionInputBinding(_coordinator.StageRunId,TurnIndex,card.InstanceId,card.Version,actor);
            var input = new TacticalExecutionInput(binding,TacticalExecutionInputSource.Manual,point,actorOrigin);
            return _coordinator.Driver.ValidatePendingInput(card,input) == TacticalInputStatus.Valid;
        }

        internal void TickDirectPlay()
        {
            if (_directPlayCard == null) return;
            if (_coordinator == null || _coordinator.StageRunId != _directPlayStage || TurnIndex != _directPlayTurn
                || _preparationState != TacticalPreparationState.Battle)
            { ResetDirectPlay(); return; }
            var pending = _coordinator.Deck.PendingAction;
            if (!ReferenceEquals(pending,_directPlayCard))
            {
                if (_coordinator.SnapshotRevision != _directPlayRevision || _directPlayCard.Zone != CardZone.Hand || _commands.PendingCount == 0)
                {
                    if(!_directCancelRequested && _directPlayCard.Zone == CardZone.Hand
                        && _coordinator.LastRejection != TacticalStageRejection.None) CaptureDirectPlayRejection();
                    ResetDirectPlay();
                }
                return;
            }
            if (_directCancelRequested)
            {
                if (!_directCancelSubmitted) _directCancelSubmitted = TryCancelPending();
                return;
            }
            if (BattleInputBlocked || !CanPlan) return;
            if (_directConfirmSubmitted)
            {
                if (_commands.PendingCount == 0 && _coordinator.LastRejection != TacticalStageRejection.None)
                { CaptureDirectPlayRejection();_directCancelRequested = true; }
                return;
            }
            if (NeedsPoint && _directPlayPoint.HasValue && !_directPointSubmitted)
            {
                if (!TrySelectWorldPoint(_directPlayPoint.Value))
                { ShowDirectPlayIssue("地点无效，卡牌已退回手牌。");_directCancelRequested = true; return; }
                _directPointSubmitted = true; return;
            }
            if (!CanConfirmExecution) return;
            if (TryConfirmExecution()) _directConfirmSubmitted = true;
        }

        internal void CancelDirectPlay()
        {
            if (!DirectPlayActive) return;
            _directCancelRequested = true;
            if (CanCancelPending) _directCancelSubmitted = TryCancelPending();
        }
        private void CaptureDirectPlayRejection()
        {
            string issue=_coordinator.ExecutionIssue ?? "";
            string message="出牌未接纳，卡牌已退回手牌。";
            if(_coordinator.LastRejection==TacticalStageRejection.ExecutionRejected)
            {
                message=issue.Contains("LoadedProviderMismatch") ? "动作数据校验失败，请返回整备后重新出战。"
                    :issue.Contains("ActionSummary:") ? "动作数据暂不可用，卡牌已退回；请检查装配动作。"
                    :"当前动作无法执行，卡牌已退回；请检查装配动作。";
            }
            ShowDirectPlayIssue(_directPlayCard.Definition.Name+"："+message);
            _directPlayIssueUntil=Time.unscaledTime+5f;
        }
        private void ResetDirectPlay()
        {
            _directPlayCard = null; _directPlayPoint = null;
            _directPointSubmitted = _directCancelRequested = _directCancelSubmitted = _directConfirmSubmitted = false;
        }
    }
}
