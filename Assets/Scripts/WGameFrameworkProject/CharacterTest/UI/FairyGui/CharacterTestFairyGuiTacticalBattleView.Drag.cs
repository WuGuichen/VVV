using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalBattleView
    {
        private const float HandScale=.42f, HandTop=752f, HandBottom=HandTop+528f*HandScale;
        private const float DragThreshold=8f;
        private int _dragSlot=-1;
        private bool _pointerHeld, _dragging, _returningPreview, _releasedPreview, _dragNeedsWorldPoint;
        private TacticalCardInstance _dragCard;
        private Vector2 _pressScreen, _previewFrom, _previewTo;
        private Vector2 _releaseRingCenter;
        private float _previewStarted;
        private static float HandSlotCenter(int slot)=>960+(slot-1)*(378*HandScale+20);
        private static Vector2 ToScreen(Fgui.InputEvent input)=>new(input.x,Screen.height-input.y);

        // Gestures own presentation only; no command or reservation exists before a legal release.
        internal void BeginHandPointer(int slot,Vector2 screen)
        {
            if(IsDisposed||!_root.visible||_hud.BattleInputBlocked||!_hud.CanDragHandSlot(slot))return;
            CancelCardDrag();
            _dragCard=_hud.GetHandSlot(slot);_dragSlot=slot;_pressScreen=screen;_pointerHeld=true;
            _dragNeedsWorldPoint=_hud.HandCardNeedsWorldPoint(slot);
        }
        internal void MoveHandPointer(Vector2 screen)
        {
            if(!_pointerHeld)return;
            if(_hud.BattleInputBlocked||!ReferenceEquals(_dragCard,_hud.GetHandSlot(_dragSlot))||!_hud.CanDragHandSlot(_dragSlot))
            {CancelCardDrag();return;}
            if(!_dragging)
            {
                if((screen-_pressScreen).sqrMagnitude<DragThreshold*DragThreshold)return;
                _dragging=true;
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(_ghost,_data[_dragSlot].IconKey,
                    0,0,64,new Color32(25,51,78,255),true);
                foreach(var child in _ghost.GetChildren())child.visible=child.name=="behaviorCardIconVisual";
                var icon=_ghost.GetChild("behaviorCardIconVisual").asCom;
                icon.GetChild("badgeBorder").visible=icon.GetChild("badgeFill").visible=false;
                _ghost.SetSize(64,64);_ghost.SetScale(1,1);_ghost.alpha=.9f;_ghost.visible=true;
                foreach(var hints in _descriptionHints)hints.HideForInteraction();
            }
            RenderDragPreview(screen,out _);
        }
        internal void EndHandPointer(Vector2 screen)
        {
            if(!_pointerHeld)return;
            // Move and release can both arrive in FairyGUI LateUpdate, after the raw release edge.
            MoveHandPointer(screen);
            if(!_dragging){CancelCardDrag();return;}
            bool valid=RenderDragPreview(screen,out var world);
            int slot=_dragSlot;
            bool returningToHand=IsInHandBand(screen);
            bool alreadyPending=_hud.DirectPlayNeedsPoint;
            bool needsPoint=_dragNeedsWorldPoint;
            Vector2 from=_ghost.xy;
            _pointerHeld=_dragging=false;_dragSlot=-1;_dragCard=null;
            HideDragIndicators();_hud.ClearDirectDragVisuals();
            bool played=valid&&(alreadyPending?_hud.TryCompleteDirectPoint(world)
                :_hud.TryDirectPlayHandSlot(slot,needsPoint?world:(TacticalWorldPoint?)null));
            _returningPreview=true;_releasedPreview=played&&needsPoint;_previewStarted=Time.unscaledTime;_previewFrom=from;
            _previewTo=played?from-Vector2.up*18:new Vector2(HandSlotCenter(slot)-32,HandTop+60);
            var ring=Get(_root,"dropRing");_releaseRingCenter=ring.xy+ring.size/2;
            ring.visible=_releasedPreview;
            if(!played&&!returningToHand&&valid)_hud.ShowDirectPlayIssue(_hud.GetHandSlotUseNotice(slot));
        }
        private bool IsInHandBand(Vector2 screen)
            =>_root.GlobalToLocal(new Vector2(screen.x,Screen.height-screen.y)).y>=HandTop;
        private bool RenderDragPreview(Vector2 screen,out TacticalWorldPoint world)
        {
            world=default;
            var pointer=_root.GlobalToLocal(new Vector2(screen.x,Screen.height-screen.y));
            bool inScreen=screen.x>=0&&screen.x<Screen.width&&screen.y>=0&&screen.y<Screen.height;
            bool inHand=IsInHandBand(screen);
            if(!_dragNeedsWorldPoint)
            {
                // Input-free cards use a fixed HUD affordance. Cursor coordinates never become a world input.
                bool canUse=inScreen&&!inHand&&!ContainsScreenPoint(screen,includeHand:false);
                // Carry the action icon beside the cursor; only the usage prompt stays fixed above the hand.
                _ghost.SetXY(Mathf.Clamp(pointer.x+18,8,1848),Mathf.Clamp(pointer.y-24,8,1010));
                _ghost.alpha=canUse?.95f:.65f;
                Get(_root,"dropRing").visible=false;_hud.ClearDirectDragVisuals();
                string prompt=inHand?"向上拖出使用":canUse?"松开使用":"移开界面控件";
                SetDragHint(_dragCard.Definition.Name+" · "+prompt,690,HandTop-90,540,64,canUse||inHand);
                return canUse;
            }
            // Test the supplied drop position against this HUD, not Stage's cached physical cursor target.
            bool valid=inScreen&&!inHand&&!ContainsScreenPoint(screen)&&_hud.TryPreviewCardPoint(_dragSlot,screen,out world);
            // Project the picked ground, including perspective; this marker promises no action path.
            Vector2 marker=pointer;
            if(valid&&UnityEngine.Camera.main!=null)
            {
                var projected=UnityEngine.Camera.main.WorldToScreenPoint(new Vector3(world.X/1000f,world.Y/1000f,world.Z/1000f));
                marker=_root.GlobalToLocal(new Vector2(projected.x,Screen.height-projected.y));
            }
            _ghost.SetXY(Mathf.Clamp(marker.x-32,8,1850),Mathf.Clamp(marker.y-78,8,1010));
            _ghost.alpha=inHand?.65f:.95f;
            var ring=Get(_root,"dropRing").asGraph;
            ring.visible=!inHand;
            float pulse=1+Mathf.Sin(Time.unscaledTime*5)*.035f;
            float width=80*pulse,height=28*pulse;
            Box(ring,marker.x-width/2,marker.y-height/2,width,height);
            ring.DrawEllipse(width,height,valid?new Color(.15f,.72f,.62f,.3f):new Color(.9f,.24f,.2f,.35f));
            string suffix=inHand?"移到战场":valid?"松开释放":"不可释放";
            float x=Mathf.Clamp(marker.x-140,8,1632),y=Mathf.Clamp(marker.y+24,8,1028);
            SetDragHint(_dragCard.Definition.Name+" · "+suffix,x,y,280,40,valid||inHand);
            if(valid)_hud.PreviewDirectDragPoint(_dragSlot,world);else _hud.PreviewDirectDragRange(_dragSlot);
            return valid;
        }
        private void SetDragHint(string text,float x,float y,float width,float height,bool legal)
        {
            var hint=Get(_root,"dragHint").asTextField;var surface=Get(_root,"dragHintSurface");
            surface.sortingOrder=115;hint.sortingOrder=121;
            hint.text=text;hint.color=legal?new Color32(25,54,79,255):new Color32(147,60,58,255);
            hint.autoSize=Fgui.AutoSizeType.Shrink;hint.align=Fgui.AlignType.Center;
            var format=hint.textFormat;format.size=24;format.bold=true;hint.textFormat=format;
            const float iconSpace=12;
            surface.visible=hint.visible=true;Box(surface,x,y,width,height);
            Box(hint,x+iconSpace,y+(height-32)/2,width-iconSpace-12,32);
        }
        private void UpdateCardDrag()
        {
            if(_returningPreview)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-_previewStarted)/.16f);
                _ghost.xy=Vector2.Lerp(_previewFrom,_previewTo,1-(1-t)*(1-t));
                _ghost.alpha=(1-t)*.9f;
                if(_releasedPreview)
                {
                    var ring=Get(_root,"dropRing").asGraph;float w=80*(1+t),h=28*(1+t);
                    Box(ring,_releaseRingCenter.x-w/2,_releaseRingCenter.y-h/2,w,h);
                    ring.DrawEllipse(w,h,new Color(.15f,.72f,.62f,.3f*(1-t)));
                }
                if(t>=1){_returningPreview=false;_ghost.visible=false;Get(_root,"dropRing").visible=false;}
            }
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool cancel=keyboard?.escapeKey.wasPressedThisFrame==true||mouse?.rightButton.wasPressedThisFrame==true;
            if(cancel&&!_hud.BattleCancelInputBlocked&&_hud.DirectPlayActive)_hud.CancelDirectPlay();
            if(!_pointerHeld)return;
            if(mouse==null||cancel||_hud.BattleInputBlocked||!Application.isFocused)
            {CancelCardDrag();return;}
            var screen=mouse.position.ReadValue();
            if(mouse.leftButton.isPressed)MoveHandPointer(screen);else EndHandPointer(screen);
        }
        private void HideDragIndicators()
        {
            Get(_root,"dragHint").visible=false;Get(_root,"dragHintSurface").visible=false;Get(_root,"dropRing").visible=false;
        }
        private void CancelCardDrag()
        {
            _pointerHeld=_dragging=_returningPreview=false;_dragSlot=-1;_dragCard=null;
            _ghost.visible=false;HideDragIndicators();_hud.ClearDirectDragVisuals();
        }
    }
}
