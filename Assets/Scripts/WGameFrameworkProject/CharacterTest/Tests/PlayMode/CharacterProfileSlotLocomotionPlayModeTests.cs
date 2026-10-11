using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterProfileSlotLocomotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator FormalPackages_ProfileSlotLocomotionPlaysThroughUnityPlayables()
        {
            Assert.IsTrue(Application.isPlaying);

            VerifyPackage(
                "Assets/MxFrameworkGenerated/CharacterPackages/000001",
                "profile.sword",
                "blend.move2d",
                CharacterAnimationBlendKind.Blend2D,
                expectedBlendPointCount: 17);
            VerifyPackage(
                "Assets/MxFrameworkGenerated/CharacterPackages/000004",
                "profile.sword",
                "blend.move2d",
                CharacterAnimationBlendKind.Blend2D,
                expectedBlendPointCount: 17);

            yield return null;
        }

        private static void VerifyPackage(
            string packageRoot,
            string expectedProfileId,
            string expectedBlendId,
            CharacterAnimationBlendKind expectedBlendKind,
            int expectedBlendPointCount)
        {
            string fullRoot = Path.GetFullPath(packageRoot);
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(fullRoot);
            CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            Assert.IsTrue(spawnResult.IsSuccess, FormatIssues(spawnResult));
            Assert.AreEqual(expectedProfileId, spawnResult.Binding.AnimationProfileBinding.ProfileId);
            Assert.AreEqual(expectedBlendId, spawnResult.Binding.AnimationProfileBinding.BlendId);
            Assert.AreEqual(expectedBlendKind, spawnResult.Binding.AnimationProfileBinding.BlendKind);

            Assert.IsTrue(
                CharacterRuntimeAnimationResources.TryCreateBinding(
                    package,
                    out CharacterRuntimeAnimationBinding binding,
                    out CharacterRuntimeSpawnResult bindingResult,
                    fullRoot),
                FormatIssues(bindingResult));
            Assert.IsTrue(
                CharacterRuntimeAnimationResources.TryCreateLocomotionDefinition(
                    binding,
                    NullRuntimeLogger.Instance,
                    out CharacterRuntimeLocomotionDefinition locomotionDefinition));
            Assert.AreEqual(
                expectedBlendPointCount,
                locomotionDefinition.BlendKind == CharacterAnimationBlendKind.Blend2D
                    ? locomotionDefinition.Blend2D.Points.Count
                    : locomotionDefinition.Blend1D.Points.Count);

            var clips = new List<AnimationClip>();
            var provider = new MemoryResourceProvider();
            var catalogEntries = new List<ResourceCatalogEntry>();
            AddBlendClips(locomotionDefinition, provider, catalogEntries, clips);

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(new ResourceCatalog(
                "playmode." + expectedProfileId,
                binding.ResourcePackageId,
                catalogEntries));

            var actor = new GameObject("profile-slot-" + expectedProfileId);
            try
            {
                Animator animator = actor.AddComponent<Animator>();
                CharacterRuntimeLocomotionBlendController controller =
                    actor.AddComponent<CharacterRuntimeLocomotionBlendController>();
                controller.Configure(actor.transform, animator, clips.ToArray());
                Assert.IsTrue(controller.TryConfigureResourceAnimationBackend(
                    manager,
                    locomotionDefinition,
                    actor.name));

                controller.SetExternalMotion(Vector2.up, 1f);
                controller.TickRuntimeAnimation(1f / 60f, 1);

                Assert.IsTrue(controller.HasAnimationBackend);
                Assert.AreEqual(expectedBlendId, controller.ActiveBlendId);
                Assert.AreEqual(expectedBlendKind, controller.ActiveBlendKind);
                Assert.IsTrue(controller.LastAnimationResult.Success, controller.LastAnimationResult.Message);
                Assert.Greater(FindBaseLayer(controller.CreateAnimationSnapshot()).ActivePlayableCount, 0);
            }
            finally
            {
                Object.Destroy(actor);
                for (int i = 0; i < clips.Count; i++)
                    Object.Destroy(clips[i]);
            }
        }

        private static void AddBlendClips(
            CharacterRuntimeLocomotionDefinition definition,
            MemoryResourceProvider provider,
            List<ResourceCatalogEntry> entries,
            List<AnimationClip> clips)
        {
            var emitted = new HashSet<ResourceKey>();
            if (definition.BlendKind == CharacterAnimationBlendKind.Blend1D)
            {
                for (int i = 0; i < definition.Blend1D.Points.Count; i++)
                    AddClip(definition.Blend1D.Points[i].ClipKey, provider, entries, clips, emitted);
                return;
            }

            for (int i = 0; i < definition.Blend2D.Points.Count; i++)
                AddClip(definition.Blend2D.Points[i].ClipKey, provider, entries, clips, emitted);
        }

        private static void AddClip(
            ResourceKey key,
            MemoryResourceProvider provider,
            List<ResourceCatalogEntry> entries,
            List<AnimationClip> clips,
            HashSet<ResourceKey> emitted)
        {
            if (!key.IsValid || !emitted.Add(key))
                return;

            string address = "playmode/" + clips.Count;
            var clip = new AnimationClip { name = key.Id };
            clips.Add(clip);
            provider.Register(address, clip);
            entries.Add(new ResourceCatalogEntry(
                key.Id,
                key.TypeId,
                provider.ProviderId,
                address,
                key.Variant,
                key.PackageId));
        }

        private static MxAnimationLayerDiagnostic FindBaseLayer(MxAnimationDiagnosticSnapshot snapshot)
        {
            for (int i = 0; snapshot?.LayerStates != null && i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == MxAnimationLayerId.Base)
                    return snapshot.LayerStates[i];
            }

            Assert.Fail("Base animation layer diagnostic was not found.");
            return null;
        }

        private static string FormatIssues(CharacterRuntimeSpawnResult result)
        {
            if (result?.Issues == null)
                return "missing";
            var messages = new List<string>();
            for (int i = 0; i < result.Issues.Length; i++)
                messages.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join(" | ", messages);
        }
    }
}
