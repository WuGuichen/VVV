using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalBattleView
    {
        private readonly Dictionary<int,Fgui.GComponent> _healthMarkers = new();
        private void UpdateHealthMarkers()
        {
            foreach(var marker in _healthMarkers.Values)marker.visible=false;
            var entities=_hud.BattleActors;var camera=UnityEngine.Camera.main;
            if(entities==null||camera==null)return;
            int playerTeam=0;
            foreach(var entity in entities)if(entity.GameplayEntityId==_hud.BattlePlayerActor)playerTeam=entity.Team.TeamId;
            foreach(var entity in entities)
            {
                var combat=entity.CombatState;
                if(combat.MaxHealth<=0||combat.Health<=0)continue;
                if(!_healthMarkers.TryGetValue(entity.Id.Value,out var marker))
                {
                    marker=Fgui.UIPackage.CreateObject("MxCharacterTestTacticalDeckUi","TacticalBattleHealthMarker").asCom;
                    Passive(marker);marker.sortingOrder=1;_root.AddChild(marker);_healthMarkers.Add(entity.Id.Value,marker);
                }
                var position=_hud.BattleHeadPosition(entity);
                var screen=camera.WorldToScreenPoint(position);
                if(screen.z<=camera.nearClipPlane)continue;
                var point=_root.GlobalToLocal(new Vector2(screen.x,Screen.height-screen.y));
                marker.SetXY(point.x-52,point.y-8);marker.visible=true;
                var back=marker.GetChild("back").asGraph;back.DrawRect(104,8,1,new Color32(25,47,63,255),new Color32(213,228,236,230));
                var fill=marker.GetChild("fill").asGraph;float width=102f*Mathf.Clamp01((float)combat.Health/combat.MaxHealth);
                fill.SetXY(1,1);fill.DrawRect(width,6,0,Color.clear,entity.Team.TeamId==playerTeam?new Color32(80,187,155,255):new Color32(224,155,81,255));
            }
        }
    }
}
