using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Fixed-capacity presentation wave packets. Oldest packets are replaced without allocating.</summary>
    internal sealed class CharacterTestShallowWaterRipples
    {
        internal const int Capacity=32;
        internal readonly Vector4[] Sources=new Vector4[Capacity], Shapes=new Vector4[Capacity];
        internal int Count { get; private set; }
        internal int Replaced { get; private set; }
        internal void Clear() { Count=0; Replaced=0; }
        internal void Add(Vector2 point,float time,float amplitude,float width=.25f,float frequency=18)
        {
            if (!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(time)||!float.IsFinite(amplitude)||amplitude<=0)return;
            int index=Count;
            if(Count<Capacity)Count++;
            else { index=0; for(int n=1;n<Capacity;n++)if(Sources[n].z<Sources[index].z)index=n; Replaced++; }
            Sources[index]=new Vector4(point.x,point.y,time,Mathf.Min(.08f,amplitude));
            Shapes[index]=new Vector4(Mathf.Clamp(width,.1f,.6f),Mathf.Clamp(frequency,4,30),0,0);
        }
        internal void Expire(float time,float lifetime)
        {
            for(int i=Count-1;i>=0;i--)
                if(time-Sources[i].z>=lifetime || time<Sources[i].z)
                { int last=--Count; Sources[i]=Sources[last]; Shapes[i]=Shapes[last]; }
        }
    }
}
