using System;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFairyGuiTacticalBattleBinder : IMxFairyGuiViewBinder<CharacterTestFairyGuiTacticalDeckPanelViewModel>, IDisposable
    {
        private readonly Func<TacticalCardHud> _resolveHud;
        internal CharacterTestFairyGuiTacticalBattleView View { get; private set; }
        internal CharacterTestFairyGuiTacticalBattleBinder(Func<TacticalCardHud> resolveHud) => _resolveHud = resolveHud;
        public void Bind(IMxFairyGuiComponentHandle component, CharacterTestFairyGuiTacticalDeckPanelViewModel args)
        {
            if (View != null && !View.IsDisposed) return;
            var root = (component as MxFairyGuiComponentHandle)?.Component;
            if (root == null || _resolveHud() == null) throw new InvalidOperationException("Battle HUD requires its published component and command owner.");
            View = new CharacterTestFairyGuiTacticalBattleView(_resolveHud(), root);
        }
        public void Dispose() { View?.Dispose(); View = null; }
    }

    internal sealed partial class CharacterTestFairyGuiTacticalBattleView : IDisposable
    {
        private readonly Fgui.GComponent _root;
        private readonly Fgui.GComponent[] _cards = new Fgui.GComponent[3];
        private readonly TacticalCardDefinition[] _definitions = new TacticalCardDefinition[3];
        private readonly CharacterTestCardUiPreviewData[] _data = new CharacterTestCardUiPreviewData[3];
        private readonly CharacterTestFairyGuiTacticalDescriptionHints[] _descriptionHints = new CharacterTestFairyGuiTacticalDescriptionHints[3];
        private readonly Fgui.GList _parameterWindows;
        private readonly CharacterTestFairyGuiCardTooltip _tooltips = new(true);
        private readonly CharacterTestFairyGuiTacticalCardSkin _skin = new();
        private readonly CharacterTestFairyGuiMeshBatcher _meshBatcher;
        private readonly Fgui.GButton _returnPreparation, _settings, _recover;
        private readonly Fgui.GTextField _current, _reserved, _feedback, _outcome, _phase, _execution, _capacity, _hint;
        private readonly Fgui.GGraph _availableFill, _reservedFill;
        private readonly TacticalCardHud _hud;
        private readonly Fgui.GComponent _ghost, _executingCard;
        private TacticalCardDefinition _executionDefinition;
        private int _lastCurrent=-1, _lastReserved=-1;
        private int _inspectedHandSlot = -1;
        private TacticalCardInstance _inspectedHandCard;
        private bool _disposed;
        internal Fgui.GComponent Root => _root;
        internal bool IsDisposed => _disposed || _root.isDisposed || _root.displayObject == null || _root.displayObject.isDisposed;
        internal static CharacterTestFairyGuiTacticalBattleView TryCreate(TacticalCardHud hud) => !Application.isPlaying ? null
            : hud.GetComponent<CharacterTestFairyGuiTacticalDeckPanelHost>()?.OpenBattleView();
        private static Fgui.GObject Get(Fgui.GComponent root,string name) => root.GetChild(name)
            ?? throw new InvalidOperationException("Published battle control missing: "+name);
        private static void Box(Fgui.GObject item,float x,float y,float w,float h)
        { item.SetXY(x,y); item.SetSize(w,h); }
        private static void Passive(Fgui.GObject item) { item.touchable=false; if(item.asCom!=null)foreach(var child in item.asCom.GetChildren()) Passive(child); }

        internal CharacterTestFairyGuiTacticalBattleView(TacticalCardHud hud,Fgui.GComponent root)
        {
            _hud=hud; _root=root; root.name="CharacterTest.TacticalBattleHud"; root.opaque=false;
            _returnPreparation=Get(root,"returnPreparation").asButton; _settings=Get(root,"settings").asButton;
            _recover=Get(root,"recover").asButton;
            _current=Get(root,"motionCurrent").asTextField; _reserved=Get(root,"motionReserve").asTextField;
            _feedback=Get(root,"feedback").asTextField; _outcome=Get(root,"outcome").asTextField;
            _phase=Get(root,"phaseName").asTextField; _execution=Get(root,"executionName").asTextField;
            _capacity=Get(root,"motionCapacity").asTextField; _hint=Get(root,"inputHint").asTextField;
            _availableFill=Get(root,"motionAvailable").asGraph; _reservedFill=Get(root,"motionReserved").asGraph;
            _parameterWindows=Get(root,"parameterWindows").asList;
            _ghost=Get(root,"dragGhost").asCom; _executingCard=Get(root,"executingCard").asCom;
            _ghost.sortingOrder=120;
            Passive(_ghost); Passive(_executingCard);
            hud.WarmDirectDragVisuals();
            foreach(string n in new[]{"phaseSurface","phaseName","executionName","motionSurface","motionIcon","motionCurrent","motionReserve","motionCapacity","motionBack","motionAvailable","motionReserved","feedback","inputHint","dropRing","dragHintSurface","dragHint"})Passive(Get(root,n));
            foreach(string n in new[]{"confirm","cancel","restart","pause","resourceRule","divider"})Get(root,n).visible=false;
            Wire(_returnPreparation,()=>hud.TryRequestRestart()); Wire(_settings,()=>{hud.OpenBattleSettings();return true;});
            Wire(_recover,()=>hud.TryRequestRecovery());
            _tooltips.Bind(_returnPreparation,"返回整备",new MxFairyGuiTooltipOptions(MxFairyGuiTooltipPlacement.Below,allowPin:false));
            _tooltips.Bind(_settings,"设置",new MxFairyGuiTooltipOptions(MxFairyGuiTooltipPlacement.Below,allowPin:false));
            for(int i=0;i<3;i++)
            {
                int slot=i; var card=Get(root,"hand"+i).asCom; _cards[i]=card;
                CharacterTestFairyGuiPlayerCardPresentation.Initialize(card); card.opaque=true;
                _descriptionHints[i]=new CharacterTestFairyGuiTacticalDescriptionHints(card,null,null,root);
                // Clicking a hand card is passive. Only a validated drag release submits play.
                card.onClick.Set(context=>{context.StopPropagation();InspectHandSlot(slot);});
                card.onRollOver.Add(()=>InspectHandSlot(slot));
                card.onRollOut.Add(()=>ClearHandInspection(slot));
                // Capture the pointer without letting FairyGUI move the actual hand slot.
                card.draggable=false;
                card.onTouchBegin.Set(context=>
                {
                    if(context.inputEvent.button!=0)return;
                    BeginHandPointer(slot,ToScreen(context.inputEvent));context.CaptureTouch();
                });
                card.onTouchMove.Set(context=>MoveHandPointer(ToScreen(context.inputEvent)));
                card.onTouchEnd.Set(context=>
                {
                    if(context.inputEvent.button==0)EndHandPointer(ToScreen(context.inputEvent));
                });
            }
            InitializeEnemyCards();
            InitializeTimeTransitionPresentation();
            _meshBatcher = root.displayObject.gameObject.AddComponent<CharacterTestFairyGuiMeshBatcher>();
            root.visible=false;
        }
        private static void Wire(Fgui.GButton button,Func<bool> click)
        {
            button.onClick.Set(ctx=>{ctx.StopPropagation();click();});
            button.titleColor=new Color32(25,54,79,255);
            foreach(string n in new[]{"overOverlay","downOverlay"})if(button.GetChild(n)!=null)button.GetChild(n).alpha=0;
        }
        internal bool ContainsScreenPoint(Vector2 screen,bool includeHand=true)
        {
            if(IsDisposed || !_root.visible)return false;
            var global=new Vector2(screen.x,Screen.height-screen.y);
            if(ContainsEnemyCardPoint(global))return true;
            if(includeHand)foreach(var card in _cards)if(Contains(card,global))return true;
            foreach(string n in new[]{"motionSurface","phaseSurface","settings","returnPreparation","recover","feedback","outcome","parameterWindows"})
                if(Contains(Get(_root,n),global))return true;
            return false;
        }
        private static bool Contains(Fgui.GObject item,Vector2 global)
        {
            if(item==null||!item.visible||item.isDisposed)return false;
            var p=item.GlobalToLocal(global);return p.x>=0&&p.y>=0&&p.x<item.width&&p.y<item.height;
        }
        internal void Sync(bool visible)
        {
            if(IsDisposed)return; _root.visible=visible;
            if(!visible){ClearHandInspection(_inspectedHandSlot);HideTimeTransitionPresentation();ResetEnemyCards();RestoreBattleProjection();CancelCardDrag();_tooltips.Hide();foreach(var h in _descriptionHints)h.RefreshPlacement();return;}
            var stage=Fgui.GRoot.inst;float scale=Mathf.Min(stage.width/1920f,stage.height/1080f);
            _root.SetScale(scale,scale);_root.SetXY((stage.width-1920*scale)/2,stage.height-1080*scale);
            UpdateTimeTransitionPresentation();
            _skin.Tick(); UpdateCardDrag(); UpdateHealthMarkers();
            Box(_returnPreparation,20,20,52,52);Box(_settings,88,20,52,52);
            foreach(var button in new[]{_returnPreparation,_settings})
            {var icon=button.GetChild("icon");if(icon!=null)Box(icon,10,10,32,32);button.GetTextField().visible=false;}
            Box(_recover,804,464,312,52);_recover.title="尝试恢复";
            _recover.visible=_hud.CanRequestRecovery;_recover.enabled=_hud.CanRequestRecovery;
            _returnPreparation.enabled=_hud.CanRequestRestart;
            var motion=_hud.MotionReadout;
            _reserved.visible=false;
            Box(Get(_root,"motionIcon"),666,998,30,30);Box(_current,714,992,74,38);Box(_capacity,1130,996,132,32);
            Box(Get(_root,"motionBack"),808,1006,300,14);
            float available=300f*motion.Spendable/Math.Max(1,motion.Capacity);
            Box(_availableFill,808,1006,available,14);_availableFill.visible=available>0;
            float reserve=300f*motion.Reserved/Math.Max(1,motion.Capacity);
            Box(_reservedFill,808+available,1006,reserve,14);_reservedFill.visible=reserve>0;
            if(_lastCurrent!=motion.Current||_lastReserved!=motion.Reserved)
            {
                _lastCurrent=motion.Current;_lastReserved=motion.Reserved;
                _current.text=motion.Current.ToString(CultureInfo.InvariantCulture);_capacity.text=motion.Current+"/"+motion.Capacity;
                _tooltips.Bind(_current,$"当前 {motion.Current} · 可用 {motion.Spendable} · 预留 {motion.Reserved}",
                    new MxFairyGuiTooltipOptions(MxFairyGuiTooltipPlacement.Above,allowPin:false));
            }
            _phase.text=TimeTransitionLabel();
            _phase.color=Color.Lerp(new Color32(25,54,79,255),new Color32(16,95,112,255),_worldStillness);
            Get(_root,"phaseSurface").alpha=Mathf.Lerp(.65f,1f,_worldStillness);
            _execution.text=_hud.IsWorldPaused ? "战场暂停" : "战场运行中";
            _hint.text="向上拖出手牌使用 · 选点卡拖至落点 · 右键 / Esc 取消";
            _hint.visible=_decisionReady;
            _executingCard.visible=false;
            for(int i=0;i<3;i++)
            {
                var instance=_hud.GetHandSlot(i);var card=_cards[i];
                card.visible=instance!=null&&instance.Zone!=CardZone.Resolving;
                if(instance==null){_definitions[i]=null;_descriptionHints[i].RefreshPlacement();continue;}
                if(!ReferenceEquals(_definitions[i],instance.Definition))
                {
                    _data[i]=_hud.GetBattleCardPresentationData(i); BindCard(card,_data[i],QualityId(instance.Definition));
                    _descriptionHints[i].Bind(_data[i],_hud.BattleTextProvider,34);_definitions[i]=instance.Definition;
                }
                if(instance.Zone==CardZone.Resolving)
                {
                    if(!ReferenceEquals(_executionDefinition,instance.Definition)){BindCard(_executingCard,_data[i],QualityId(instance.Definition));_executionDefinition=instance.Definition;}
                    _executingCard.visible=true;_executingCard.SetScale(.13f,.13f);_executingCard.SetXY(1114,12);
                    _execution.text=instance.Definition.Name;continue;
                }
                bool hovered=!_dragging&&_descriptionHints[i].CardHovered;
                float cardScale=hovered?246f/378f:HandScale;
                float slotCenter=HandSlotCenter(i);
                card.SetScale(cardScale,cardScale);
                card.SetXY(slotCenter-378f*cardScale/2,hovered?HandBottom-528f*cardScale:HandTop-6f*_worldStillness);
                card.sortingOrder=hovered?10:0;
                card.alpha=_dragging&&i==_dragSlot ? .45f : _decisionReady
                    ? (_hud.CanSelectHandSlot(i) || instance.Zone==CardZone.PendingAction ? 1f : .65f)
                    : Mathf.Lerp(.78f,.95f,_worldStillness);
                _skin.SetBattleReadiness(card,_worldStillness*.45f+_readyPulse*.55f);
                card.touchable=true;card.GetChild("focus").visible=false;
                if(_dragging)_descriptionHints[i].HideForInteraction();else _descriptionHints[i].RefreshPlacement();
            }
            _outcome.visible=_hud.HasOutcome;_outcome.text=_hud.OutcomeMessage;
            Box(_outcome,700,220,520,68);
            // Inspecting a disabled card is passive and independent of the drag admission gate.
            // Query every Sync: world frames can change without a card definition/revision change.
            string notice=InspectedHandNotice();
            string feedback=!string.IsNullOrEmpty(_hud.FaultMessage)?_hud.FaultMessage
                : !string.IsNullOrEmpty(notice)?notice
                : !string.IsNullOrEmpty(_hud.DirectPlayFeedback)?_hud.DirectPlayFeedback:_hud.CommandFeedbackMessage;
            if(!string.Equals(_feedback.text,feedback,StringComparison.Ordinal))_feedback.text=feedback;
            _feedback.visible=!string.IsNullOrWhiteSpace(_feedback.text);_feedback.autoSize=Fgui.AutoSizeType.Shrink;
            Box(_feedback,600,550,720,44);
            if(_battleWater!=null)_battleWater.SetBattleSurface(_root,_filterStillness,_waterFlowTime,
                new Vector4(_motionDirection,_visualTravel,_motionDrive,0f));
        }
        private void InspectHandSlot(int slot)
        {
            if(IsDisposed||!_root.visible||_dragging||_hud.BattleInputBlocked)return;
            var instance=_hud.GetHandSlot(slot);
            if(instance==null||instance.Zone==CardZone.Resolving)return;
            _inspectedHandSlot=slot;_inspectedHandCard=instance;
        }
        private void ClearHandInspection(int slot)
        {
            if(slot!=_inspectedHandSlot)return;
            _inspectedHandSlot=-1;_inspectedHandCard=null;
        }
        private string InspectedHandNotice()
        {
            if(_inspectedHandSlot<0)return string.Empty;
            var instance=_hud.GetHandSlot(_inspectedHandSlot);
            if(!ReferenceEquals(instance,_inspectedHandCard)||instance==null||instance.Zone==CardZone.Resolving)
            {ClearHandInspection(_inspectedHandSlot);return string.Empty;}
            if(_dragging||_hud.BattleInputBlocked||_hud.CanSelectHandSlot(_inspectedHandSlot))return string.Empty;
            return _hud.GetHandSlotUseNotice(_inspectedHandSlot);
        }
        private void BindCard(Fgui.GComponent card,CharacterTestCardUiPreviewData data,string qualityId=null)
        {
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(card);
            CharacterTestFairyGuiPlayerCardPresentation.Bind(card,data,34);_skin.Bind(card,data,qualityId);
        }
        private static string QualityId(TacticalCardDefinition definition) => definition is TacticalActionCardDefinition action
            && !string.IsNullOrWhiteSpace(action.PublishedCardId) ? action.PublishedCardId : definition.Id;
        public void Dispose()
        {
            if(_disposed)return;
            if(_meshBatcher!=null){_meshBatcher.enabled=false;UnityEngine.Object.Destroy(_meshBatcher);}
            if(!IsDisposed)CancelCardDrag();RestoreBattleProjection();_battleWater?.Dispose();_battleWater=null;_disposed=true;_hud.ClearDirectDragVisuals();_tooltips.Dispose();_skin.Dispose();
            DisposeEnemyCards();
            foreach(var h in _descriptionHints)h?.Dispose();
        }
    }
}
