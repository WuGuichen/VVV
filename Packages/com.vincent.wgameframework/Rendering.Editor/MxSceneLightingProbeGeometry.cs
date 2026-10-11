using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Rendering.Editor
{
    /// <summary>Offline BVH over the exact static capture triangles. Does not depend on colliders,
    /// Physics layers, Static flags, vegetation, or scene mutation.</summary>
    public sealed class MxSceneLightingProbeGeometry
    {
        private struct Triangle
        {
            public Vector3 A,B,C,Center,Normal;
            public Bounds Bounds;
        }
        private struct Node { public Bounds Bounds; public int Start,Count,Left,Right; }
        private readonly Triangle[] _triangles;
        private readonly int[] _order;
        private readonly List<Node> _nodes=new List<Node>();

        public MxSceneLightingProbeGeometry(IReadOnlyList<Vector3> triangleVertices)
        {
            if(triangleVertices==null || triangleVertices.Count==0 || triangleVertices.Count%3!=0)
                throw new ArgumentException("World-space capture triangle triples are required.");
            _triangles=new Triangle[triangleVertices.Count/3];_order=new int[_triangles.Length];
            for(int i=0;i<_triangles.Length;i++)
            {
                Vector3 a=triangleVertices[i*3],b=triangleVertices[i*3+1],c=triangleVertices[i*3+2];
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);
                _triangles[i]=new Triangle {A=a,B=b,C=c,Center=(a+b+c)/3,Normal=Vector3.Cross(b-a,c-a).normalized,Bounds=bounds};_order[i]=i;
            }
            Build(0,_order.Length);
        }
        private int Build(int start,int count)
        {
            var bounds=_triangles[_order[start]].Bounds;var centers=new Bounds(_triangles[_order[start]].Center,Vector3.zero);
            for(int i=start+1;i<start+count;i++){bounds.Encapsulate(_triangles[_order[i]].Bounds);centers.Encapsulate(_triangles[_order[i]].Center);}
            int id=_nodes.Count;_nodes.Add(new Node {Bounds=bounds,Start=start,Count=count,Left=-1,Right=-1});
            if(count<=8)return id;
            int axis=centers.size.x>=centers.size.y && centers.size.x>=centers.size.z ? 0 : centers.size.y>=centers.size.z ? 1 : 2;
            Array.Sort(_order,start,count,Comparer<int>.Create((a,b)=>_triangles[a].Center[axis].CompareTo(_triangles[b].Center[axis])));
            int half=count/2,left=Build(start,half),right=Build(start+half,count-half);
            _nodes[id]=new Node {Bounds=bounds,Left=left,Right=right};return id;
        }

        public bool Nearest(Vector3 point,float radius,out Vector3 closest,out Vector3 normal,out float distance)
        {
            float squared=radius*radius;closest=normal=Vector3.zero;bool found=false;
            Nearest(0,point,ref squared,ref closest,ref normal,ref found);distance=Mathf.Sqrt(squared);return found;
        }
        private void Nearest(int id,Vector3 point,ref float squared,ref Vector3 closest,ref Vector3 normal,ref bool found)
        {
            Node node=_nodes[id];if(node.Bounds.SqrDistance(point)>squared)return;
            if(node.Left>=0)
            {
                int first=node.Left,second=node.Right;
                if(_nodes[second].Bounds.SqrDistance(point)<_nodes[first].Bounds.SqrDistance(point)){int swap=first;first=second;second=swap;}
                Nearest(first,point,ref squared,ref closest,ref normal,ref found);Nearest(second,point,ref squared,ref closest,ref normal,ref found);return;
            }
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                Triangle t=_triangles[_order[i]];Vector3 p=ClosestPoint(point,t.A,t.B,t.C);float d=(p-point).sqrMagnitude;
                if(d<=squared){squared=d;closest=p;normal=t.Normal;found=true;}
            }
        }
        public bool SegmentClear(Vector3 a,Vector3 b)
        {
            Vector3 delta=b-a;float length=delta.magnitude;
            if(length<=.0002f)return true;
            return !Occluded(0,new Ray(a,delta/length),length);
        }
        private bool Occluded(int id,Ray ray,float length)
        {
            Node node=_nodes[id];
            // Slightly thicken flat triangle bounds for numerically stable slab intersection.
            Bounds bounds=node.Bounds;bounds.Expand(.0001f);
            if(!bounds.IntersectRay(ray,out float entry) || entry>length)return false;
            if(node.Left>=0)return Occluded(node.Left,ray,length)||Occluded(node.Right,ray,length);
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                Triangle t=_triangles[_order[i]];Vector3 e1=t.B-t.A,e2=t.C-t.A,p=Vector3.Cross(ray.direction,e2);
                float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-8f)continue;
                float inv=1/det;Vector3 s=ray.origin-t.A;float u=Vector3.Dot(s,p)*inv;if(u<0 || u>1)continue;
                Vector3 q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0 || u+v>1)continue;
                float hit=Vector3.Dot(e2,q)*inv;if(hit>.0001f && hit<length-.0001f)return true;
            }
            return false;
        }
        private static Vector3 ClosestPoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0 && d2<=0)return a;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0 && d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0)return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0 && d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float denominator=1/(va+vb+vc);return a+ab*(vb*denominator)+ac*(vc*denominator);
        }
    }
}
