using System;
using System.Globalization;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionContentHashTests
    {
        private const int StartSocketId = 1;
        private const int EndSocketId = 2;
        [Test]
        public void WeaponTraceUsesAttackerPackageForSharedLightAttackClip()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000004";
            const string clip = "art.character.skeleton.animation.katana_atk1";
            const int trace = CharacterTestAuthoredContentIds.D403_Id554678986;
            var entity = PackageSource(packageRoot);
            ICharacterTestBakedWeaponTraceSource source = CharacterTestDefaultBakedWeaponTraceSource.Instance;
            var expected = MxFramework.CharacterRuntimeSpawn.CharacterWeaponTraceBakeStorage.Load(
                packageRoot, "000004", false, out var report);
            Assert.IsTrue(report.Success);
            Assert.IsTrue(expected.TryGetFrame(clip, trace, 25, out var packageFrame));
            Assert.IsTrue(source.TryGetFrameForEntity(entity, clip, trace, 25, out var actual));
            Assert.IsTrue(source.TryGetFrame(clip, trace, 25, out var defaultFrame));
            Assert.AreEqual(packageFrame.RootNow, actual.RootNow);
            Assert.AreEqual(packageFrame.TipNow, actual.TipNow);
            Assert.AreNotEqual(defaultFrame.TipNow, actual.TipNow,
                "Characters sharing a clip and trace id must retain their own baked skeleton/weapon poses.");

            var action = LoadAction();
            var bindings = CharacterTestDefaultWeaponTraceBindingResolver.Instance;
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action, entity, null, source, bindings,
                out string packageHash, out string issue), issue);
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action,
                PackageSource(CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot), null, source, bindings,
                out string defaultHash, out issue), issue);
            Assert.AreNotEqual(defaultHash, packageHash, "Tactical evidence must include the same package-local trace as combat.");
        }

        [Test]
        public void MissingAttackerPackageDoesNotBorrowDefaultWeaponOrTrace()
        {
            var entity = PackageSource("Assets/MxFrameworkGenerated/CharacterPackages/missing-trace-regression");
            Assert.IsFalse(CharacterTestDefaultBakedWeaponTraceSource.Instance.TryGetFrameForEntity(entity,
                "art.character.skeleton.animation.katana_atk1", CharacterTestAuthoredContentIds.D403_Id554678986, 25, out _));
            Assert.IsFalse(CharacterTestDefaultWeaponTraceBindingResolver.Instance.TryResolve(entity,
                CharacterTestAuthoredContentIds.D405_Id1, CharacterTestAuthoredContentIds.D403_Id554678986, out _));
        }

        private static CharacterTestCharacterEntity PackageSource(string packageRoot)
        {
            return new CharacterTestCharacterEntity(new CharacterTestCharacterEntityId(1),
                new CharacterTestCharacterIdentityComponent(4, "trace regression", default, default, packageRoot),
                default, default, new CharacterTestCharacterEquipmentComponent(true), default, new GameplayEntityId(1, 1));
        }

        [Test]
        public void CasterBodyFingerprintRequiresAndTracksActualSourceCapsule()
        {
            var action = LoadAction(value => value["combatTrack"]["events"][0]["hitVolume"] =
                new JObject { ["sourceKind"] = "CasterBody" });
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, action.CombatTrack.Events[0].HitVolume.SourceKind);
            Assert.IsFalse(CharacterActionContentHashUtility.TryCompute(action, null, null, null, null, out _, out _));
            var physics = new CharacterTestCharacterPhysicsRuntime(new CombatPhysicsWorld(), CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half), Fix64.FromRatio(1, 100), Fix64.Zero,
                staticColliderCount: 0, sourceDescription: "tactical-hash");
            var first = Source(0.3f, 1.8f, 0.9f);
            var second = Source(0.4f, 2f, 1f);
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action, first, physics, null, null, out string a, out string issue), issue);
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action, second, physics, null, null, out string b, out issue), issue);
            Assert.AreNotEqual(a, b);
        }

        private static CharacterTestCharacterEntity Source(float radius, float height, float center)
        {
            return new CharacterTestCharacterEntity(new CharacterTestCharacterEntityId(1), default, default, default,
                default, default, new GameplayEntityId(1, 1), new CharacterTestCharacterPhysicsComponent(radius, height, center, 70));
        }

        [Test]
        public void TransientSocketMotionDoesNotInvalidateNominalContentButAuthoredRigChangesDo()
        {
            var action=LoadAction();var source=Source(.3f,1.8f,.9f);var bake=new BakeSource();var bindings=new BindingSource();
            var physics=new CharacterTestCharacterPhysicsRuntime(new CombatPhysicsWorld(),CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half,Fix64.One,Fix64.Half),Fix64.Zero,Fix64.Zero,staticColliderCount:0,sourceDescription:"nominal-hash-pose");
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action,source,physics,bake,bindings,out _,out var issue),issue);
            Assert.Greater(bake.LastSocketId,0);
            var binding=new CharacterAuthoritySecondaryBinding(1,CharacterAuthorityPoseRegionIds.Default,1,bake.LastSocketId,
                Fix64.FromRatio(22,100),Fix64.FromRatio(58,100),Fix64.FromRatio(4,100),Fix64.FromRatio(9,1000));
            source.AuthorityPose.ConfigureDefinition(new CharacterAuthorityPoseRigDefinition(new[]{binding}));
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action,source,physics,bake,bindings,out var nominal,out issue),issue);
            var impulse=new CharacterAuthorityPoseImpulseRequest(CharacterAuthorityPoseRegionIds.Default,
                new FixVector3(Fix64.One,Fix64.Zero,Fix64.Zero),Fix64.One,10);
            Assert.IsTrue(source.AuthorityPose.ApplySecondaryImpulse(impulse,RuntimeFrame.Zero,out issue),issue);
            source.AuthorityPose.Tick(new RuntimeFrame(1));
            Assert.IsTrue(source.AuthorityPose.TryGetSocketOffset(bake.LastSocketId,out var offset));Assert.IsFalse(offset.IsZero);
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action,source,physics,bake,bindings,out var moving,out issue),issue);
            Assert.AreEqual(nominal,moving,"Hit feedback changes the live pose, not the nominal action's authored content.");
            var changed=new CharacterAuthoritySecondaryBinding(1,CharacterAuthorityPoseRegionIds.Default,1,bake.LastSocketId,
                binding.Stiffness,binding.DampingPerFrame,Fix64.FromRatio(5,100),binding.StrengthScale);
            source.AuthorityPose.ConfigureDefinition(new CharacterAuthorityPoseRigDefinition(new[]{changed}));
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action,source,physics,bake,bindings,out var authored,out issue),issue);
            Assert.AreNotEqual(nominal,authored,"Real rig edits must still invalidate the summary.");
        }

        [Test]
        public void CurrentBattleCharacterSummaryRebuildAcceptsTransientPoseButStillRejectsChangedContent()
        {
            int character=CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000009");
            var measurement=CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            string directory=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/MxFramework/TacticalPoseRegression",Guid.NewGuid().ToString("N")));
            var store=new CharacterActionTacticalMetricsStore(directory);
            using var probe=CharacterActionReferenceProbeSession.ForTestAction(character,measurement.ReferenceDummy,301);
            var impulse=new CharacterAuthorityPoseImpulseRequest(CharacterAuthorityPoseRegionIds.Default,
                new FixVector3(Fix64.One,Fix64.Zero,Fix64.Zero),Fix64.One,10);
            Assert.IsTrue(probe.Source.AuthorityPose.ApplySecondaryImpulse(impulse,RuntimeFrame.Zero,out var issue),issue);
            probe.Source.AuthorityPose.Tick(new RuntimeFrame(1));
            Assert.IsTrue(probe.Source.AuthorityPose.TryGetSocketOffset(12,out var offset));Assert.IsFalse(offset.IsZero);
            Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryDescribe(probe.Runtime,probe.Source,measurement,out var request,out issue,301),issue);
            Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryRebuild(request,store,out var result,out issue),issue);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,result.Status);
            string path=store.GetPath(character,new CharacterActionId(301));byte[] before=File.ReadAllBytes(path);
            var changed=new CharacterTestTacticalSummaryRequest(request.CharacterId,request.ActionId,request.ActionSetId,
                request.Equipment,"changed-provider",request.Measurement,testActionOverride:true);
            Assert.IsFalse(CharacterTestTacticalSummaryPreparation.TryRebuild(changed,store,out _,out issue));
            Assert.That(issue,Does.Contain("LoadedProviderMismatch"));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }

        [Test]
        public void RealActionAndBakeContentFingerprintIsCultureIndependent()
        {
            var action = LoadAction();
            string first = Compute(action, new BakeSource(), new BindingSource());
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.AreEqual(first, Compute(action, new BakeSource(), new BindingSource()));
                Assert.AreEqual(64, first.Length);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("phase")]
        [TestCase("cancel")]
        [TestCase("animation")]
        [TestCase("rootMotion")]
        [TestCase("hitVolume")]
        public void ActualActionInputsChangeFingerprint(string dimension)
        {
            string original = Compute(LoadAction(), new BakeSource(), new BindingSource());
            var changed = LoadAction(action => {
                switch (dimension)
                {
                    case "phase": action["phases"][0]["endFrame"] = 16; break;
                    case "cancel": action["cancelRules"][0]["startFrame"] = 36; break;
                    case "animation": action["animationTrack"]["events"][0]["playbackSpeed"] = 1.1f; break;
                    case "rootMotion": action["motionTrack"]["rootMotion"]["frames"][20]["z"] = 0.25f; break;
                    case "hitVolume": action["combatTrack"]["events"][0]["hitVolume"] = new JObject {
                        ["shapeKind"] = "Capsule", ["radius"] = 0.2f,
                        ["localPointA"] = new JObject { ["x"] = 0, ["y"] = 1, ["z"] = 0 },
                        ["localPointB"] = new JObject { ["x"] = 0, ["y"] = 1, ["z"] = 1 } }; break;
                }
            });
            Assert.AreNotEqual(original, Compute(changed, new BakeSource(), new BindingSource()), dimension);
        }

        [Test]
        public void ActualBakeFrameAndResolvedWeaponDimensionsAreHashed()
        {
            var action = LoadAction();
            string original = Compute(action, new BakeSource(), new BindingSource());
            Assert.AreNotEqual(original, Compute(action, new BakeSource { TipDelta = 100 }, new BindingSource()));
            Assert.AreNotEqual(original, Compute(action, new BakeSource(), new BindingSource { Radius = 0.2f }));
            Assert.AreNotEqual(original, Compute(action, new BakeSource(), new BindingSource { ProximalExtension = 0.3f }));
        }

        [Test]
        public void MissingRequiredBakeFrameProducesNoUsableFingerprint()
        {
            Assert.IsFalse(CharacterActionContentHashUtility.TryCompute(LoadAction(), null, null,
                new BakeSource { Missing = true }, new BindingSource(), out string hash, out string issue));
            Assert.AreEqual(string.Empty, hash);
            StringAssert.StartsWith("WeaponBakeFrameUnavailable", issue);
        }

        [Test]
        public void SocketPositionsAreHashedAndMissingSocketFailsClosed()
        {
            var action = LoadAction(value => {
                var evt = value["combatTrack"]["events"][0];
                evt["equipmentSlotId"] = 0;
                evt["traceProfileId"] = 0;
                evt["hitVolume"] = new JObject { ["sourceKind"] = "BakedSocketCapsule", ["radius"] = 0.2f,
                    ["startSocketId"] = StartSocketId, ["endSocketId"] = EndSocketId };
            });
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.BakedSocketCapsule, action.CombatTrack.Events[0].HitVolume.SourceKind);
            Assert.IsTrue(action.CombatTrack.Events[0].HitVolume.HasVolume);
            string original = Compute(action, new BakeSource(), new BindingSource());
            Assert.AreNotEqual(original, Compute(action, new BakeSource { SocketDelta = 100 }, new BindingSource()));
            Assert.IsFalse(CharacterActionContentHashUtility.TryCompute(action, null, null,
                new BakeSource { Missing = true }, new BindingSource(), out string hash, out string issue));
            Assert.IsEmpty(hash);
            StringAssert.StartsWith("SocketBakeFrameUnavailable", issue);
        }

        [Test]
        public void DummyFingerprintCoversAllGeometryComponentsAndVersion()
        {
            string original = CharacterActionContentHashUtility.ComputeReferenceDummyHash(new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            foreach (var dummy in new[] {
                new CharacterActionReferenceDummy(2, 1800, 300, 0, 900, 0),
                new CharacterActionReferenceDummy(1, 1900, 300, 0, 900, 0),
                new CharacterActionReferenceDummy(1, 1800, 350, 0, 900, 0),
                new CharacterActionReferenceDummy(1, 1800, 300, 1, 900, 0),
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 901, 0),
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 1) })
                Assert.AreNotEqual(original, CharacterActionContentHashUtility.ComputeReferenceDummyHash(dummy));
        }

        internal static CharacterActionConfig LoadAction(Action<JObject> edit = null)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json");
            var document = JObject.Parse(File.ReadAllText(path));
            foreach (JObject item in (JArray)document["actions"])
                if ((int)item["actionId"] == 301) { edit?.Invoke(item); break; }
            var result = CharacterActionJsonImporter.ImportActions(document.ToString());
            Assert.IsTrue(result.Success, string.Join("\n", Array.ConvertAll(result.Diagnostics, item => item.ToString())));
            return Array.Find(result.Actions, item => item.ActionId.Value == 301);
        }

        private static string Compute(CharacterActionConfig action, BakeSource bake, BindingSource bindings)
        {
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(action, null, null, bake, bindings, out string hash, out string issue), issue);
            return hash;
        }

        private sealed class BindingSource : ICharacterTestWeaponTraceBindingResolver
        {
            internal float Radius = 0.1f;
            internal float ProximalExtension;
            public bool TryResolve(CharacterTestCharacterEntity entity, int slot, int profile, out CharacterTestWeaponTraceBinding binding)
            {
                int resolvedProfile = profile > 0 ? profile : CharacterTestAuthoredContentIds.D403_Id554678986;
                binding = new CharacterTestWeaponTraceBinding(resolvedProfile, 1, Radius, equipmentSlotId: slot, proximalExtension: ProximalExtension);
                return true;
            }
        }

        private sealed class BakeSource : ICharacterTestBakedWeaponTraceSource
        {
            internal int LastSocketId;
            internal long TipDelta;
            internal long SocketDelta;
            internal bool Missing;
            public bool TryGetFrame(string clip, int profile, int localFrame, out MxAnimationBakedWeaponTraceFrame frame)
            {
                frame = null;
                if (Missing || !CharacterTestDefaultBakedWeaponTraceSource.Instance.TryGetFrame(clip, profile, localFrame, out var original)) return false;
                LastSocketId=original.SocketId;
                frame = new MxAnimationBakedWeaponTraceFrame(original.LocalFrame, original.TraceId, original.SocketId,
                    original.RootPrev, original.TipPrev, original.RootNow,
                    new MxAnimationBakedVector3(original.TipNow.X, original.TipNow.Y, original.TipNow.Z + TipDelta));
                return true;
            }
            public bool TryGetSocketFrame(string clip, int socket, int localFrame, out MxAnimationBakedSocketFrame frame)
            {
                frame = Missing ? null : new MxAnimationBakedSocketFrame(localFrame, socket, socket.ToString(),
                    new MxAnimationBakedVector3(0, 1000, (socket == StartSocketId ? 0 : 1000) + SocketDelta), MxAnimationBakedVector3.Zero);
                return frame != null;
            }
            public bool TryResolveSocketTrack(string clip, int socket, out CharacterTestBakedSocketTrack track) { track = null; return false; }
        }
    }
}
