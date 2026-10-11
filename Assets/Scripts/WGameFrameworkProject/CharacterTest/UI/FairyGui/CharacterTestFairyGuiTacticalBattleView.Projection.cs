using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalBattleView
    {
        private UnityEngine.Camera _projectionCamera;
        internal static Matrix4x4 FitBattleProjection(Matrix4x4 projection,float lowestFoot,float highestHead)
        {
            const float bottom=.46f, top=.92f;
            float span=Mathf.Max(.01f,highestHead-lowestFoot);
            float zoom=Mathf.Min(1f,(top-bottom)/span);
            projection.m00*=zoom; projection.m11*=zoom;
            float foot=.5f+(lowestFoot-.5f)*zoom;
            float head=.5f+(highestHead-.5f)*zoom;
            float shift=Mathf.Clamp(bottom-foot,0,Mathf.Max(0,top-head));
            projection.m12-=shift*2;
            return projection;
        }
        internal void ApplyBattleProjection()
        {
            if(IsDisposed||!_root.visible)return;
            var camera=UnityEngine.Camera.main;var entities=_hud.BattleActors;
            if(camera==null||entities==null||entities.Count==0)return;
            _projectionCamera=camera;camera.ResetProjectionMatrix();
            float min=1,max=0;
            foreach(var entity in entities)
            {
                if(entity.CombatState.Health<=0)continue;
                var pos=new Vector3(entity.Transform.X,entity.Transform.Y,entity.Transform.Z);
                var foot=camera.WorldToViewportPoint(pos);
                var head=camera.WorldToViewportPoint(_hud.BattleHeadPosition(entity));
                if(foot.z<=camera.nearClipPlane||foot.x<-.1f||foot.x>1.1f)continue;
                min=Mathf.Min(min,foot.y);max=Mathf.Max(max,head.y);
            }
            if(max>min)camera.projectionMatrix=FitBattleProjection(camera.projectionMatrix,min,max);
        }
        internal void RefreshBattleOverlayAfterCamera() { if(!IsDisposed&&_root.visible){UpdateHealthMarkers();UpdateEnemyCards();} }
        private void RestoreBattleProjection() { if(_projectionCamera!=null)_projectionCamera.ResetProjectionMatrix();_projectionCamera=null; }
    }
}
