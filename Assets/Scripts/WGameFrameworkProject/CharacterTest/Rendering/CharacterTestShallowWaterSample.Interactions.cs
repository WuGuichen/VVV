using System;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestShallowWaterSession
    {
        private static readonly int RippleCountId=Shader.PropertyToID("_WgWaterRippleCount");
        private static readonly int RipplesId=Shader.PropertyToID("_WgWaterRipples");
        private static readonly int ShapesId=Shader.PropertyToID("_WgWaterRippleShape");
        private readonly CharacterTestShallowWaterRipples _ripples=new CharacterTestShallowWaterRipples();
        private CharacterTestCharacterRuntimeModule _runtime;
        private const int MaximumTrackedSubjects=128;
        private readonly Track[] _tracks=new Track[MaximumTrackedSubjects];
        private long _motionFrame=-1;
        public int FootstepRipples {get;private set;}
        public int MovementRipples {get;private set;}
        public int LandingRipples {get;private set;}
        public int ActiveRipples => _ripples.Count;
        private struct Track { internal int Id; internal Vector3 Position; internal long Frame; }
        private void BindInteractions()
        {
            var runtime=Owner?.RenderingCharacterRuntime;
            if(ReferenceEquals(runtime,_runtime))return;
            UnbindInteractions(); _runtime=runtime;
            if(runtime==null)return;
            runtime.PresentationMotionObserved+=Motion;
            runtime.PhysicalContactObserved+=Contact;
        }
        private void UnbindInteractions()
        {
            if(_runtime!=null){_runtime.PresentationMotionObserved-=Motion;_runtime.PhysicalContactObserved-=Contact;}
            _runtime=null;ClearRipples();
        }
        public void ClearRipples() { _ripples.Clear();Array.Clear(_tracks,0,_tracks.Length);_motionFrame=-1; }
        private bool OnWater(Vector3 position,float tolerance=.15f)
        {
            Vector3 local=LocalToWorld.inverse.MultiplyPoint3x4(position);
            return Settings!=null && Mathf.Abs(local.x)<=Settings.Size.x*.5f && Mathf.Abs(local.z)<=Settings.Size.y*.5f && Mathf.Abs(local.y)<=tolerance;
        }
        public bool EmitRipple(Vector3 position,float amplitude)
        {
            if(!SurfaceEnabled||!RipplesEnabled||!OnWater(position))return false;
            _ripples.Expire((float)Clock,Settings.RippleLifetime);
            _ripples.Add(new Vector2(position.x,position.z),(float)Clock,amplitude,Settings.RippleWidth);return true;
        }
        private void Contact(CombatPhysicalContactEvent contact)
        {
            if(Settings==null || contact.Kind!=CombatPhysicalContactKind.BodyPartContact || contact.Phase!=CombatPhysicalContactPhase.Started)return;
            var point=new Vector3(ToFloat(contact.Point.X),ToFloat(contact.Point.Y),ToFloat(contact.Point.Z));
            if(ToFloat(contact.Normal.Y)<.5f)return;
            if(EmitRipple(point,Settings.FootstepAmplitude))FootstepRipples++;
        }
        private void Motion(CharacterTestCharacterRuntimeWorld world,CharacterTestCharacterMotionSystem motion,RuntimeFrame frame)
        {
            if(!SurfaceEnabled||!RipplesEnabled||Settings==null)return;
            if(frame.Value<=_motionFrame)ClearRipples();_motionFrame=frame.Value;
            float spacingSquared=Settings.WakeSpacing*Settings.WakeSpacing;
            for(int n=0;n<world.Entities.Count;n++)
            {
                var entity=world.Entities[n];var t=entity.Transform;
                var point=new Vector3(t.XMillimeters*.001f,t.YMillimeters*.001f,t.ZMillimeters*.001f);
                int index=-1,free=-1;
                for(int i=0;i<_tracks.Length;i++)
                {if(_tracks[i].Id==entity.Id.Value){index=i;break;}if(free<0&&(_tracks[i].Id==0||frame.Value-_tracks[i].Frame>120))free=i;}
                if(index<0){if(free>=0)_tracks[free]=new Track{Id=entity.Id.Value,Position=point,Frame=frame.Value};continue;}
                ref Track track=ref _tracks[index];track.Frame=frame.Value;
                float distance=(point-track.Position).sqrMagnitude;
                if(!entity.IsGrounded||distance>9||!OnWater(point)){track.Position=point;continue;}
                if(distance>=spacingSquared)
                {if(EmitRipple(point,Settings.FootstepAmplitude*.6f))MovementRipples++;track.Position=point;}
            }
            for(int i=0;i<motion.LandingEventCount;i++)
            {
                var landing=motion.GetLandingEvent(i);var t=landing.Entity.Transform;
                var point=new Vector3(t.XMillimeters*.001f,t.YMillimeters*.001f,t.ZMillimeters*.001f);
                float strength=Settings.LandingAmplitude*Mathf.Clamp(landing.DownwardSpeedMillimetersPerSecond/4000f,.25f,1);
                if(EmitRipple(point,strength))LandingRipples++;
            }
        }
        private void WriteRipples(MaterialPropertyBlock block)
        {
            _ripples.Expire((float)Clock,Settings.RippleLifetime);
            block.SetInt(RippleCountId,RipplesEnabled?_ripples.Count:0);
            block.SetVectorArray(RipplesId,_ripples.Sources);block.SetVectorArray(ShapesId,_ripples.Shapes);
        }
        private static float ToFloat(Fix64 value) => (float)(value.RawValue / (double)Fix64.Scale);
    }
}
