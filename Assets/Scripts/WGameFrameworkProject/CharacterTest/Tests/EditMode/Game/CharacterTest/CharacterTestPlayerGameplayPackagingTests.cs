using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Resources;
using NUnit.Framework;
using MxFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestPlayerGameplayPackagingTests
    {
        internal const string GoodGameplayReport = "gameplayProbe.status: Passed\n"
            + "gameplayProbe.actionsReady: true\ngameplayProbe.moved: true\ngameplayProbe.attack: true\n"
            + "gameplayProbe.attackAnimation: true\ngameplayProbe.jump: true\ngameplayProbe.jumpAnimation: true\ngameplayProbe.airborne: true\n"
            + "gameplayProbe.landed: true\ngameplayProbe.moveDistanceMeters: 0.8\ngameplayProbe.jumpHeightMeters: 0.5\n"
            + "gameplayProbe.baselineHipsAboveRoot: 1\ngameplayProbe.minimumHipsAboveRoot: 0.9\n";

        [Test]
        public void GameplayAcceptance_RequiresActionsAndConsistentMovementMeasurements()
        {
            var values = new Dictionary<string, string>();
            foreach (string line in GoodGameplayReport.Split('\n'))
            {
                int separator = line.IndexOf(':');
                if (separator > 0) values[line.Substring(0, separator)] = line.Substring(separator + 1).Trim();
            }
            Assert.DoesNotThrow(() => CharacterTestPackageTestCommand.RequireGameplayProbe(values));
            foreach (string key in new List<string>(values.Keys))
            {
                string old = values[key];
                values.Remove(key);
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageTestCommand.RequireGameplayProbe(values), key);
                values[key] = old;
            }
            foreach (string key in new[] { "moveDistanceMeters", "jumpHeightMeters", "minimumHipsAboveRoot" })
            {
                string full = "gameplayProbe." + key;
                string old = values[full];
                values[full] = "0";
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageTestCommand.RequireGameplayProbe(values), key);
                values[full] = "NaN";
                Assert.Throws<InvalidDataException>(() => CharacterTestPackageTestCommand.RequireGameplayProbe(values), key);
                values[full] = old;
            }
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(-1f, 0f)]
        public void LocomotionTransition_RealPlayerPackageRetainsItsPlaybackHold(float moveX, float moveY)
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000009";
            var package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            Assert.IsTrue(CharacterRuntimeAnimationResources.TryCreateBinding(package, out var binding, out _));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(packageRoot + "/prefabs/000009_character_preview.prefab");
            GameObject actor = UnityEngine.Object.Instantiate(source);
            try
            {
                var controller = actor.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var animator = actor.GetComponentInChildren<Animator>();
                controller.UseExternalRuntimeTick = true;
                controller.Configure(actor.transform.Find("ModelRoot"), animator, Array.Empty<AnimationClip>());
                var resources = new ResourceManager();
                resources.RegisterProvider(new CharacterTestEditorAnimationAssetProvider());
                resources.AddCatalog(new ResourceCatalog("test.player-animation", binding.ResourcePackageId,
                    CharacterTestCharacterAnimationResources.CreateCatalogEntries(binding)));
                Assert.IsTrue(CharacterTestCharacterAnimationResources.TryConfigureLocomotion(controller,
                    resources, binding, actor.name, NullRuntimeLogger.Instance));
                var view = new CharacterTestRuntimeCharacterView(new CharacterTestCharacterEntityId(3), actor,
                    null, null, controller);
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                for (int i = 0; i < 60; i++)
                {
                    view.SyncAnimation(CharacterTestCharacterAnimationComponent.IdleGrounded, new RuntimeFrame(i));
                    controller.TickRuntimeAnimation(1f / 60f, i);
                }
                float initialFeetY = Mathf.Min(left.position.y, right.position.y);
                float initialHipsY = hips.position.y;
                float minimumFeetY = initialFeetY;
                float minimumHipsY = initialHipsY;
                var moving = new CharacterTestCharacterAnimationComponent(new Vector2(moveX, moveY), 1f, true, false);
                view.SyncAnimation(moving, new RuntimeFrame(60));
                bool retained = controller.IsLocomotionBlendSuppressed;
                string transition = controller.LastActionKey;
                for (int i = 60; i < 270; i++)
                {
                    if (i > 60) view.SyncAnimation(i < 180 ? moving
                        : CharacterTestCharacterAnimationComponent.IdleGrounded, new RuntimeFrame(i));
                    controller.TickRuntimeAnimation(1f / 60f, i);
                    minimumFeetY = Mathf.Min(minimumFeetY, left.position.y, right.position.y);
                    minimumHipsY = Mathf.Min(minimumHipsY, hips.position.y);
                }
                string metrics = "retained=" + retained + " transition=" + transition
                    + " initialFeetY=" + initialFeetY + " minFeetY=" + minimumFeetY
                    + " initialHipsY=" + initialHipsY + " minHipsY=" + minimumHipsY;
                TestContext.WriteLine(metrics);
                Assert.IsTrue(retained, metrics);
                StringAssert.StartsWith("locomotion.combat.start.", transition);
                Assert.Greater(minimumHipsY, initialHipsY * 0.7f, metrics);
                Assert.Greater(minimumFeetY, initialFeetY - 0.08f, metrics);
                Assert.IsFalse(controller.IsLocomotionBlendSuppressed, "Stopped locomotion must eventually resume.");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        [TestCase(1f, "combat_run_start_f", 0f)]
        [TestCase(-1f, "combat_run_start_b", 0f)]
        [TestCase(-1f, "combat_run_start_b", 90f)]
        [TestCase(-1f, "combat_run_start_b", -90f)]
        [TestCase(-1f, "combat_run_start_b", 180f)]
        public void LocomotionTransition_InterruptedStopResumesForCurrentMovement(float moveY, string startClipName, float turnAfterRestartDegrees)
        {
            const float step = 1f / 60f;
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
            const string clipRoot = "Assets/Art/MxFramework/Characters/CombatGirlSword/AnimationClips/CombatLocomotion/";
            var package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            Assert.IsTrue(CharacterRuntimeAnimationResources.TryCreateBinding(package, out var binding, out _));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(packageRoot + "/prefabs/000001_character_preview.prefab");
            var startClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipRoot + startClipName + ".anim");
            var stopClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipRoot + "combat_run_stop_f.anim");
            Assert.IsNotNull(source);
            Assert.IsNotNull(startClip);
            Assert.IsNotNull(stopClip);
            Assert.Less(startClip.length + 3f * step, stopClip.length, "The replacement must end before the old hold.");
            GameObject actor = UnityEngine.Object.Instantiate(source);
            try
            {
                var controller = actor.AddComponent<CharacterRuntimeLocomotionBlendController>();
                controller.UseExternalRuntimeTick = true;
                controller.Configure(actor.transform.Find("ModelRoot"), actor.GetComponentInChildren<Animator>(), Array.Empty<AnimationClip>());
                var resources = new ResourceManager();
                resources.RegisterProvider(new CharacterTestEditorAnimationAssetProvider());
                resources.AddCatalog(new ResourceCatalog("test.interrupted-locomotion", binding.ResourcePackageId,
                    CharacterTestCharacterAnimationResources.CreateCatalogEntries(binding)));
                Assert.IsTrue(CharacterTestCharacterAnimationResources.TryConfigureLocomotion(controller,
                    resources, binding, actor.name, NullRuntimeLogger.Instance));
                var view = new CharacterTestRuntimeCharacterView(new CharacterTestCharacterEntityId(3), actor,
                    null, null, controller);
                var forward = new CharacterTestCharacterAnimationComponent(Vector2.up, 1f, true, false);
                view.SyncAnimation(CharacterTestCharacterAnimationComponent.IdleGrounded, new RuntimeFrame(0));
                for (int frame = 1; frame <= 90; frame++)
                {
                    view.SyncAnimation(forward, new RuntimeFrame(frame));
                    controller.TickRuntimeAnimation(step, frame);
                }

                // Releasing the first direction for one frame starts a longer non-looping stop clip.
                view.SyncAnimation(CharacterTestCharacterAnimationComponent.IdleGrounded, new RuntimeFrame(91));
                Assert.IsTrue(controller.IsLocomotionBlendSuppressed);
                StringAssert.EndsWith("combat_run_stop_f", controller.LastActionAnimationResult.ClipKey.Id);
                controller.TickRuntimeAnimation(step, 91);
                var moving = new CharacterTestCharacterAnimationComponent(new Vector2(0f, moveY), 1f, true, false);
                view.SyncAnimation(moving, new RuntimeFrame(92));
                Assert.IsTrue(controller.IsLocomotionBlendSuppressed, "Keep the new start clip's own playback hold.");
                StringAssert.EndsWith(startClipName, controller.LastActionAnimationResult.ClipKey.Id);
                if (turnAfterRestartDegrees != 0f)
                {
                    controller.TickRuntimeAnimation(step, 92);
                    actor.transform.rotation = Quaternion.Euler(0f, turnAfterRestartDegrees, 0f);
                    float yawRadians = turnAfterRestartDegrees * Mathf.Deg2Rad;
                    moving = new CharacterTestCharacterAnimationComponent(
                        new Vector2(Mathf.Sin(yawRadians), -Mathf.Cos(yawRadians)), 1f, true, false);
                    view.SyncAnimation(moving, new RuntimeFrame(93));
                    Assert.IsFalse(controller.IsLocomotionBlendSuppressed,
                        "A start clip for the old local direction must release the pose when facing changes.");
                    Assert.IsEmpty(controller.LastActionKey);
                    controller.TickRuntimeAnimation(step, 93);
                    Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, turnAfterRestartDegrees, 0f), actor.transform.rotation), 0.001f,
                        "Presentation must preserve the authority-facing root rotation.");
                }
                else
                {
                    for (int i = 0; i < Mathf.CeilToInt(startClip.length / step) + 2; i++)
                    {
                        view.SyncAnimation(moving, new RuntimeFrame(92 + i));
                        controller.TickRuntimeAnimation(step, 92 + i);
                    }
                }

                Assert.IsFalse(controller.IsLocomotionBlendSuppressed, "The completed start must not retain the old stop deadline.");
                Assert.IsTrue(controller.LastAnimationResult.Success);
                var snapshot = controller.CreateAnimationSnapshot();
                bool movementLoopRestored = false;
                foreach (var layer in snapshot.LayerStates)
                    if (layer.LayerId == controller.LocomotionBlend2DDefinition.LayerId)
                        movementLoopRestored = layer.Blend2DId == controller.ActiveBlendId && layer.Blend2DWeights.Count > 0;
                Assert.IsTrue(movementLoopRestored,
                    "The movement loop must already be restored before the old stop would have ended.");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        [Test]
        public void PlayerActions_LoadFormalExportFromStreamingAssetsWithoutProjectDirectories()
        {
            string root = Path.Combine(Path.GetTempPath(), "CharacterTestActions-" + Guid.NewGuid().ToString("N"));
            try
            {
                string mirror = Path.Combine(root, CharacterTestCharacterActionConfigSource.PlayerActionsRelativeRoot);
                foreach (string relative in CharacterTestCharacterActionPlayerArtifacts.SelectFiles(
                    CharacterTestCharacterActionPlayerArtifacts.SourceRoot))
                {
                    string target = Path.Combine(mirror, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(Path.Combine(CharacterTestCharacterActionPlayerArtifacts.SourceRoot, relative), target);
                }
                string[] directories = CharacterTestCharacterActionConfigSource.CreateCandidateDirectories(
                    "missing-project-root", root, false);
                Assert.IsNotEmpty(directories);
                Assert.That(directories, Has.All.StartsWith(root.Replace('\\', '/')));
                var catalog = CharacterTestCharacterActionConfigSource.LoadCatalog(
                    new CharacterTestCharacterActionConfigSourceOptions(directories));
                Assert.IsTrue(catalog.IsReady, catalog.ConfigSourceDescription);
                Assert.Greater(catalog.Registry.Actions.Length, 0);
                Assert.AreEqual(CharacterTestCharacterActionPlayerArtifacts.Capture(
                    CharacterTestCharacterActionPlayerArtifacts.SourceRoot), CharacterTestCharacterActionPlayerArtifacts.Capture(mirror));
                string before = CharacterTestCharacterActionPlayerArtifacts.Capture(mirror);
                string changed = Path.Combine(mirror, CharacterTestCharacterActionPlayerArtifacts.SelectFiles(mirror)[0]);
                File.AppendAllText(changed, "\n ");
                Assert.AreNotEqual(before, CharacterTestCharacterActionPlayerArtifacts.Capture(mirror));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void PlayerActions_SelectedCharactersHaveFormalEquipmentActionSets()
        {
            Assert.DoesNotThrow(() => CharacterTestCharacterActionPlayerArtifacts.ValidateSources(
                CharacterTestPlayerCharacterPackageSelection.Resolve()));
        }

        [Test]
        public void PlayerAnimation_DefaultVariantAliasRetainsPublishedProviderAndAddress()
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(new MemoryResourceProvider());
            var published = new ResourceCatalogEntry("char.000003.animation.idle1", ResourceTypeIds.AnimationClip,
                "memory", "published/idle.anim", "default", "000003", hash: "published-hash");
            manager.AddCatalog(new ResourceCatalog("published", "000003", new[] { published }));
            var editorFallback = new ResourceCatalogEntry(published.Id, ResourceTypeIds.AnimationClip,
                CharacterTestEditorAnimationAssetProvider.Id, "Assets/editor-only.anim", packageId: "000003");
            var aliases = CharacterTestCharacterSpawnFlow.CreateMissingEntriesCatalog(manager,
                new ResourceCatalog("aliases", "000003", new[] { editorFallback }));
            Assert.AreEqual(1, aliases.Entries.Count);
            Assert.AreEqual(published.ProviderId, aliases.Entries[0].ProviderId);
            Assert.AreEqual(published.Address, aliases.Entries[0].Address);
            Assert.AreEqual(published.Hash, aliases.Entries[0].Hash);
            Assert.AreEqual(string.Empty, aliases.Entries[0].Variant);
            manager.AddCatalog(aliases);
            Assert.IsTrue(manager.Contains(new ResourceKey(published.Id, ResourceTypeIds.AnimationClip, "", "000003")));
        }

        [Test]
        public void AnimationAcceptance_RejectsVisibleViewWhoseClipFailedToLoad()
        {
            var diagnostics = new CharacterTestAnimationViewDiagnostics(1);
            diagnostics.Add(new CharacterTestRuntimeCharacterAnimationDiagnostics(1, "visible-but-broken",
                true, 1, 1, 1, 1, true, true, "blend.speed", false, "LoadFailed", "idle",
                string.Empty, false, "Success", string.Empty));
            Assert.IsFalse(diagnostics.Ready);
            StringAssert.Contains("LoadFailed", diagnostics.FirstIssue);
            StringAssert.Contains("status: failed", diagnostics.CreateReportText("test"));
        }
    }
}
