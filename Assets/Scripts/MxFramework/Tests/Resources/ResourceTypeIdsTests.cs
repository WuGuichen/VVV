using MxFramework.Resources;
using MxFramework.Resources.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Resources
{
    public class ResourceTypeIdsTests
    {
        [Test]
        public void FromType_WhenAnimationClip_ReturnsBuiltinTypeId()
        {
            Assert.AreEqual(ResourceTypeIds.AnimationClip, ResourceTypeIds.FromType<AnimationClip>());
            Assert.AreEqual("AnimationClip", ResourceTypeIds.AnimationClip);
        }

        [Test]
        public void Resolve_WhenAnimationClipTypeId_ReturnsUnityAnimationClipType()
        {
            Assert.AreEqual(typeof(AnimationClip), UnityResourceTypeResolver.Resolve(ResourceTypeIds.AnimationClip));
        }
    }
}
