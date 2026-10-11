using MxFramework.Game.CharacterTest;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class ShallowWaterTests
    {
        [Test] public void ReflectionPreservesPlaneAndRoundTripsPoints()
        {
            var mirror=CharacterTestShallowWaterSample.ReflectionMatrix(2.5f);
            var point=new Vector3(7,8,-4);
            Assert.AreEqual(new Vector3(7,-3,-4),mirror.MultiplyPoint(point));
            Assert.AreEqual(point,mirror.MultiplyPoint(mirror.MultiplyPoint(point)));
            Assert.AreEqual(new Vector3(1,2.5f,3),mirror.MultiplyPoint(new Vector3(1,2.5f,3)));
        }
        [Test] public void RotatedSurfaceBoundsContainEveryWaterCorner()
        {
            var position = new Vector3(3,.025f,-7); var rotation = Quaternion.Euler(0,37,0);
            var size = new Vector2(36,28);
            var bounds = CharacterTestShallowWaterSample.SurfaceBounds(position,rotation,size);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 corner = position + rotation * new Vector3(x*size.x*.5f,0,z*size.y*.5f);
                Assert.That(bounds.SqrDistance(corner),Is.LessThan(1e-8f));
            }
        }
        [Test] public void RippleOverflowKeepsBoundAndReplacesOldest()
        {
            var waves=new CharacterTestShallowWaterRipples();
            for(int i=0;i<100;i++)waves.Add(new Vector2(i,0),i,1);
            Assert.AreEqual(32,waves.Count);Assert.AreEqual(68,waves.Replaced);
            for(int i=0;i<waves.Count;i++){Assert.GreaterOrEqual(waves.Sources[i].z,68);Assert.LessOrEqual(waves.Sources[i].w,.08f);}
        }
        [Test] public void RippleExpiryAndClockRestoreRemoveStalePackets()
        {
            var waves=new CharacterTestShallowWaterRipples();waves.Add(Vector2.zero,1,.02f);waves.Add(Vector2.one,2,.02f);
            waves.Expire(2.5f,1);Assert.AreEqual(1,waves.Count);
            waves.Expire(.5f,1);Assert.AreEqual(0,waves.Count);
        }
        [Test] public void InvalidWaveInputIsRejected()
        {
            var waves=new CharacterTestShallowWaterRipples();waves.Add(new Vector2(float.NaN,0),0,.02f);
            waves.Add(Vector2.zero,float.PositiveInfinity,.02f);waves.Add(Vector2.zero,0,-1);
            Assert.AreEqual(0,waves.Count);
        }
        [Test] public void AuxiliaryAdmissionEndsWhenDisabledOrDestroyed()
        {
            var source=new GameObject("Water test source");var secondary=new GameObject("Water test secondary");
            try
            {
                var camera=source.AddComponent<UnityEngine.Camera>();var auxiliaryCamera=secondary.AddComponent<UnityEngine.Camera>();
                var marker=secondary.AddComponent<MxSceneRenderingAuxiliaryCamera>();marker.SourceCamera=camera;
                Assert.IsTrue(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                marker.enabled=false;Assert.IsFalse(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                marker.enabled=true;Assert.IsTrue(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
                Object.DestroyImmediate(marker);Assert.IsFalse(MxSceneRenderingAuxiliaryCamera.Matches(auxiliaryCamera,camera));
            }
            finally{Object.DestroyImmediate(secondary);Object.DestroyImmediate(source);}
        }
    }
}
