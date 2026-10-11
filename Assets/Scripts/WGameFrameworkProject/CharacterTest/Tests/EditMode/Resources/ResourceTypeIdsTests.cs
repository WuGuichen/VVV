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
            Assert.AreEqual(ResourceTypeIds.AvatarMask, ResourceTypeIds.FromType<AvatarMask>());
            Assert.AreEqual("AvatarMask", ResourceTypeIds.AvatarMask);
            Assert.AreEqual("Scene", ResourceTypeIds.Scene);
        }

        [Test]
        public void Resolve_WhenAnimationClipTypeId_ReturnsUnityAnimationClipType()
        {
            Assert.AreEqual(typeof(AnimationClip), UnityResourceTypeResolver.Resolve(ResourceTypeIds.AnimationClip));
            Assert.AreEqual(typeof(AvatarMask), UnityResourceTypeResolver.Resolve(ResourceTypeIds.AvatarMask));
            Assert.AreEqual(typeof(Object), UnityResourceTypeResolver.Resolve(ResourceTypeIds.Scene));
        }

        [Test]
        public void Resolve_WhenFairyGuiPackageBytesTypeId_ReturnsTextAssetType()
        {
            Assert.AreEqual(typeof(TextAsset), UnityResourceTypeResolver.Resolve("MxFairyGuiPackageBytes"));
        }
    }
}
