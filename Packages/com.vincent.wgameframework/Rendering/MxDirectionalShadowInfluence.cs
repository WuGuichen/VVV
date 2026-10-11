using System;
using System.Collections.Generic;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Rendering
{
    // A conservative envelope of visible receivers in the shader's light space.
    // It contains all 3D receivers, including procedural/instanced surfaces. No
    // ground-plane assumption or camera-visible-caster test is used.
    internal struct MxDirectionalShadowReceiverRegion
    {
        internal Vector2 Minimum, Maximum;
        internal bool Unbounded;
    }

    internal static class MxDirectionalShadowInfluence
    {
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        internal static MxDirectionalShadowReceiverRegion Calculate(Plane[] frustum, Plane[] scratch,
            Vector3 x, Vector3 y, Vector3 direction, Vector4 depthRange)
        {
            var result=new MxDirectionalShadowReceiverRegion { Unbounded=true };
            if(frustum==null || frustum.Length<6 || scratch==null || scratch.Length<8 ||
                !Finite(depthRange.x) || !Finite(depthRange.y) || depthRange.y<=0 ||
                !Finite(x)||!Finite(y)||!Finite(direction)||direction.sqrMagnitude<.5f) return result;
            for(int i=0;i<6;i++)
            {
                var plane=frustum[i];var n=plane.normal;
                if(!Finite(n.x)||!Finite(n.y)||!Finite(n.z)||!Finite(plane.distance)||n.sqrMagnitude<.5f)return result;
                // Expand rather than shrink: tolerate edge roundoff and jitter.
                scratch[i]=new Plane(n,plane.distance+.02f);
            }
            // The receiver shader returns lit outside this depth interval. Clip
            // the actual camera frustum by that 3D slab, not the baked world AABB.
            scratch[6]=new Plane(-direction,-depthRange.x+.02f);
            scratch[7]=new Plane(direction,depthRange.x+depthRange.y+.02f);
            double minX=double.PositiveInfinity,minY=double.PositiveInfinity;
            double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity;int vertices=0;
            for(int a=0;a<6;a++)for(int b=a+1;b<7;b++)for(int c=b+1;c<8;c++)
            {
                var p=scratch[a];var q=scratch[b];var r=scratch[c];
                var u=p.normal;var v=q.normal;var w=r.normal;
                double vxwX=(double)v.y*w.z-(double)v.z*w.y;
                double vxwY=(double)v.z*w.x-(double)v.x*w.z;
                double vxwZ=(double)v.x*w.y-(double)v.y*w.x;
                double determinant=u.x*vxwX+u.y*vxwY+u.z*vxwZ;
                if(Math.Abs(determinant)<1e-12)continue;
                double wxuX=(double)w.y*u.z-(double)w.z*u.y;
                double wxuY=(double)w.z*u.x-(double)w.x*u.z;
                double wxuZ=(double)w.x*u.y-(double)w.y*u.x;
                double uxvX=(double)u.y*v.z-(double)u.z*v.y;
                double uxvY=(double)u.z*v.x-(double)u.x*v.z;
                double uxvZ=(double)u.x*v.y-(double)u.y*v.x;
                double px=(-p.distance*vxwX-q.distance*wxuX-r.distance*uxvX)/determinant;
                double py=(-p.distance*vxwY-q.distance*wxuY-r.distance*uxvY)/determinant;
                double pz=(-p.distance*vxwZ-q.distance*wxuZ-r.distance*uxvZ)/determinant;
                if(double.IsNaN(px)||double.IsInfinity(px)||double.IsNaN(py)||double.IsInfinity(py)||
                    double.IsNaN(pz)||double.IsInfinity(pz)||Math.Abs(px)>1e12||Math.Abs(py)>1e12||Math.Abs(pz)>1e12)return result;
                bool inside=true;
                for(int k=0;k<8;k++)
                {
                    var n=scratch[k].normal;
                    if(n.x*px+n.y*py+n.z*pz+scratch[k].distance<-.0001){inside=false;break;}
                }
                if(!inside)continue;
                double sx=px*x.x+py*x.y+pz*x.z,sy=px*y.x+py*y.y+pz*y.z;
                minX=Math.Min(minX,sx);maxX=Math.Max(maxX,sx);minY=Math.Min(minY,sy);maxY=Math.Max(maxY,sy);vertices++;
            }
            // An empty/degenerate intersection is conservatively retained rather
            // than guessed empty. Invalid/custom projections never lose shadows.
            if(vertices==0)return result;
            float pad=(float)(Math.Max(Math.Max(Math.Abs(minX),Math.Abs(maxX)),Math.Max(Math.Abs(minY),Math.Abs(maxY)))+1)*.00001f;
            result.Minimum=new Vector2((float)minX-pad,(float)minY-pad);
            result.Maximum=new Vector2((float)maxX+pad,(float)maxY+pad);
            result.Unbounded=false;return result;
        }
        internal static bool Overlaps(Bounds bounds,Vector3 x,Vector3 y,MxDirectionalShadowReceiverRegion region,float padding)
        {
            if(region.Unbounded)return true;
            var c=bounds.center;var e=bounds.extents;
            if(!Finite(c.x)||!Finite(c.y)||!Finite(c.z)||!Finite(e.x)||!Finite(e.y)||!Finite(e.z))return true;
            var centre=new Vector2(Vector3.Dot(c,x),Vector3.Dot(c,y));
            var extent=new Vector2(Mathf.Abs(x.x)*e.x+Mathf.Abs(x.y)*e.y+Mathf.Abs(x.z)*e.z,
                Mathf.Abs(y.x)*e.x+Mathf.Abs(y.y)*e.y+Mathf.Abs(y.z)*e.z)+Vector2.one*Mathf.Max(0,padding);
            // Same light ray, irrespective of caster screen visibility or height.
            // Do not aggressively depth-cull: receiver-plane correction on 3D
            // near-grazing surfaces can move the comparison depth substantially.
            return centre.x+extent.x>=region.Minimum.x && centre.x-extent.x<=region.Maximum.x &&
                centre.y+extent.y>=region.Minimum.y && centre.y-extent.y<=region.Maximum.y;
        }
        internal static bool TryClipProjected(Vector2 casterMinimum,Vector2 casterMaximum,
            MxDirectionalShadowReceiverRegion region,float padding,out Vector2 minimum,out Vector2 maximum)
        {
            minimum=casterMinimum;maximum=casterMaximum;
            if(region.Unbounded)return true;
            var border=Vector2.one*Mathf.Max(0,padding);
            minimum=Vector2.Max(casterMinimum,region.Minimum-border);
            maximum=Vector2.Min(casterMaximum,region.Maximum+border);
            return minimum.x<=maximum.x&&minimum.y<=maximum.y;
        }
    }

    internal sealed class MxDirectionalShadowReceiverScopes
    {
        private sealed class Scope
        {
            internal UnityCamera Camera;
            internal Matrix4x4 Matrix;
            internal readonly Plane[] Frustum=new Plane[6], Scratch=new Plane[8];
            internal MxDirectionalShadowReceiverRegion Region;
            internal bool Captured;
        }
        private readonly List<Scope> _cache=new List<Scope>();
        private readonly List<Scope> _frame=new List<Scope>();
        private readonly List<UnityCamera> _cameras=new List<UnityCamera>();
        private bool _unbounded;
        private Vector3 _basisX, _basisY, _direction;
        private Vector4 _depthRange;
        private bool _hasLightSpace;
        internal int Rebuilds {get;private set;}
        internal bool Unbounded => _unbounded;
        internal void Capture(MxSceneRenderingConfiguration configuration,UnityCamera current,MxDirectionalShadowData data,
            IReadOnlyList<UnityCamera> known)
        {
            Rebuilds=0;_frame.Clear();_cameras.Clear();_unbounded=!configuration.CullDirectionalShadowsToReceivers;
            bool lightSpaceChanged=!_hasLightSpace || !_basisX.Equals(data.BasisX) || !_basisY.Equals(data.BasisY) ||
                !_direction.Equals(data.Direction) || !_depthRange.Equals(data.DepthRange);
            _basisX=data.BasisX;_basisY=data.BasisY;_direction=data.Direction;_depthRange=data.DepthRange;_hasLightSpace=true;
            Add(configuration.TargetCamera);Add(current);
            if(configuration.PreviewDirectionalShadowsInSceneView)
                foreach(var camera in known)if(camera!=null&&camera.cameraType==CameraType.SceneView)Add(camera);
            if(configuration.CollectDirectionalShadowReceiverCameras != null)
            {
                try
                {
                    if(!configuration.CollectDirectionalShadowReceiverCameras(_cameras))_unbounded=true;
                }
                catch {_unbounded=true;}
            }
            else if(configuration.PreviewDirectionalShadowsInSceneView && Application.isEditor) _unbounded=true;
            for(int i=_cache.Count-1;i>=0;i--)if(_cache[i].Camera==null)_cache.RemoveAt(i);
            foreach(var camera in _cameras)
            {
                if(camera==null)continue;
                if(camera.stereoEnabled){_unbounded=true;continue;}
                Scope scope=null;foreach(var cached in _cache)if(cached.Camera==camera){scope=cached;break;}
                if(scope==null){scope=new Scope{Camera=camera};_cache.Add(scope);}
                var matrix=camera.cullingMatrix;
                if(lightSpaceChanged || !scope.Captured || !scope.Matrix.Equals(matrix))
                {
                    GeometryUtility.CalculateFrustumPlanes(matrix,scope.Frustum);
                    scope.Region=MxDirectionalShadowInfluence.Calculate(scope.Frustum,scope.Scratch,data.BasisX,data.BasisY,data.Direction,data.DepthRange);
                    scope.Matrix=matrix;scope.Captured=true;Rebuilds++;
                }
                if(!_frame.Contains(scope))_frame.Add(scope);
            }
            if(_frame.Count==0)_unbounded=true;
        }
        private void Add(UnityCamera camera){if(camera!=null&&!_cameras.Contains(camera))_cameras.Add(camera);}
        internal bool ContainsUnchanged(UnityCamera camera)
        {
            if(_unbounded)return true;
            foreach(var scope in _frame)if(scope.Camera==camera)return scope.Matrix.Equals(camera.cullingMatrix);
            return false;
        }
        internal void UseUnbounded(){_unbounded=true;}
        internal bool CanAffect(Bounds bounds,MxDirectionalShadowData data,float padding)
        {
            if(_unbounded)return true;
            foreach(var scope in _frame)
                if(MxDirectionalShadowInfluence.Overlaps(bounds,data.BasisX,data.BasisY,scope.Region,padding))return true;
            return false;
        }
        internal bool TryClip(Bounds bounds,MxDirectionalShadowData data,float padding,out Vector2 minimum,out Vector2 maximum)
        {
            var x=data.BasisX;var y=data.BasisY;var c=bounds.center;var e=bounds.extents;
            var centre=new Vector2(Vector3.Dot(c,x),Vector3.Dot(c,y));
            var extent=new Vector2(Mathf.Abs(x.x)*e.x+Mathf.Abs(x.y)*e.y+Mathf.Abs(x.z)*e.z,
                Mathf.Abs(y.x)*e.x+Mathf.Abs(y.y)*e.y+Mathf.Abs(y.z)*e.z);
            var casterMinimum=centre-extent;var casterMaximum=centre+extent;
            minimum=casterMinimum;maximum=casterMaximum;
            if(_unbounded)return true;
            bool found=false;
            foreach(var scope in _frame)
            {
                if(!MxDirectionalShadowInfluence.TryClipProjected(casterMinimum,casterMaximum,scope.Region,padding,
                    out var lo,out var hi))continue;
                if(!found){minimum=lo;maximum=hi;found=true;}
                else{minimum=Vector2.Min(minimum,lo);maximum=Vector2.Max(maximum,hi);}
            }
            return found;
        }
    }
}
