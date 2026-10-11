using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public class CharacterRuntimeSpawnFirstSliceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CharacterAttributes_InitializeAuthorityAndHealthMirror(bool legacyHp)
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(ProjectPath(CodexFlowTestRoot + "/config/character_config_patch.json")));
            var profile = json["patch"]["entries"].OfType<Newtonsoft.Json.Linq.JObject>()
                .Single(entry => (string)entry["source"] == CharacterAttributeProfileConfig.TableName)["fields"];
            profile["Attributes"] = Newtonsoft.Json.Linq.JArray.Parse("[{\"AttributeId\":" + (legacyHp ? 920001 : 1)
                + ",\"StableId\":\"attr.hp\",\"Group\":\"Vital\",\"BaseValue\":240,\"InitialValue\":240,\"MinValue\":0,\"MaxValue\":240}]");
            ((Newtonsoft.Json.Linq.JArray)profile["Attributes"]).Add(Newtonsoft.Json.Linq.JObject.FromObject(new
                { AttributeId = 2, StableId = "attr.attack", Group = "Custom", BaseValue = 35, InitialValue = 35, MinValue = 0, MaxValue = 1000 }));
            ((Newtonsoft.Json.Linq.JArray)profile["Attributes"]).Add(Newtonsoft.Json.Linq.JObject.FromObject(new
                { AttributeId = 3, StableId = "attr.defense", Group = "Custom", BaseValue = 18, InitialValue = 18, MinValue = 0, MaxValue = 1000 }));
            profile["BaseCohesion"] = 140;
            profile["ScatterRecoveryRate"] = 3;
            profile["ScatterRecoveryDelayFrames"] = 120;
            profile["CohesionBreakRecoveryDelayFrames"] = 180;
            var package = new CharacterImportedPackage("", "attributes-test", CharacterImportedPackageJson.LoadConfigSet(json.ToString()), null, null, null, null);
            var world = new CharacterTestCharacterRuntimeWorld();
            try
            {
                var definition = new CharacterTestCharacterDefinition(1, "Attributes", default, 1, default);
                var entity = world.Spawn(default, definition, package);
                Assert.AreEqual(240, world.GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(35, world.GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Attack));
                Assert.AreEqual(18, world.GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Defense));
                Assert.AreEqual(240, entity.CombatState.MaxHealth, "Health mirror must be initialized before spawn observers run.");
                Assert.AreEqual(0, world.GetCurrentAttribute(entity, 920001), "No duplicate legacy HP state.");
                Assert.IsTrue(world.TryGetScatterPressure(entity, out var pressure));
                Assert.AreEqual(140, pressure.BaseCohesion);
                Assert.AreEqual(3, pressure.RecoveryRate);
                Assert.AreEqual(120, pressure.RecoveryDelayFrames);
                Assert.AreEqual(180, pressure.BrokenRecoveryDelayFrames);
                world.SyncCombatStateHealth(entity);
                Assert.AreEqual(240, entity.CombatState.MaxHealth);
                Assert.AreEqual(240, entity.CombatState.Health);
                world.SetCurrentAttribute(entity.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 170);
                world.SyncCombatStateHealth(entity);
                Assert.AreEqual(170, entity.CombatState.Health);
            }
            finally { world.Clear(); }
        }

        private const string CodexFlowTestRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
        private const string CombatGirlSwordRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
        private const string Dragon14Root = "Assets/MxFrameworkGenerated/CharacterPackages/000003";

        private const string CorePresenceVolumeJson =
            "{\"volumeId\":\"core\",\"referenceFrame\":\"ModelRoot\",\"shape\":\"Sphere\","
            + "\"localPose\":{\"position\":{\"x\":0,\"y\":1,\"z\":0},"
            + "\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},"
            + "\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"radius\":2,\"height\":0,"
            + "\"size\":{\"x\":0,\"y\":0,\"z\":0},\"skinMillimeters\":10,"
            + "\"pressureScaleMilli\":1000,\"enabled\":true}";

        private const string TailPresenceVolumeJson =
            "{\"volumeId\":\"tail\",\"referenceFrame\":\"ModelRoot\",\"shape\":\"Box\","
            + "\"localPose\":{\"position\":{\"x\":0,\"y\":0.25,\"z\":-3},"
            + "\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},"
            + "\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"radius\":0,\"height\":0,"
            + "\"size\":{\"x\":1,\"y\":0.5,\"z\":4},\"skinMillimeters\":20,"
            + "\"pressureScaleMilli\":750,\"enabled\":false}";

        [Test]
        public void ImportedCodexFlowTest_CreatesRuntimeBindingPlan()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            CharacterRuntimeSpawnResult result = CharacterRuntimeSpawnResolver.Resolve(
                package,
                new CharacterSpawnRequest(new SpawnProfileId(820001)));

            Assert.IsTrue(result.IsSuccess, string.Join(", ", result.Issues.Select(issue => issue.Code + ":" + issue.Message)));
            Assert.AreEqual(new CharacterConfigId(710001), result.Binding.ResolvedProfile.CharacterId);
            Assert.AreEqual(new EquipmentStateId(770004), result.Binding.ResolvedProfile.ActiveEquipmentStateId);
            Assert.AreEqual("profile.locomotion", result.Binding.ResolvedProfile.AnimationProfileId);
            Assert.AreEqual(package.ImportReport.SourcePackageHash, result.Binding.SourcePackageHash);
            Assert.AreEqual(package.ImportReport.ResourceMappingHash, result.Binding.ResourceMappingHash);
            StringAssert.StartsWith("sha256:", result.Binding.SourcePackageHash);
            StringAssert.StartsWith("sha256:", result.Binding.ResourceMappingHash);
            Assert.AreEqual(10, result.Binding.CombatBodyBindingPlan.Colliders.Length);
            Assert.AreEqual(2, result.Binding.WeaponAttachmentBindingPlan.Attachments.Length);
            Assert.AreEqual(1, result.Binding.WeaponAttachmentBindingPlan.Traces.Length);
            CollectionAssert.AreEquivalent(
                new[] { "mount.sword.main", "mount.shield.off" },
                result.Binding.WeaponAttachmentBindingPlan.Attachments.Select(attachment => attachment.AttachmentId).ToArray());
            Assert.GreaterOrEqual(result.Binding.ResourcePreloadPlan.ResolvedResources.Length, 5);
            Assert.IsEmpty(result.Binding.ResourcePreloadPlan.MissingResourceKeys);
            StringAssert.Contains("sourcePackageHash=", result.Binding.DebugSummary);
            StringAssert.Contains("resourceMappingHash=", result.Binding.DebugSummary);
        }

        [Test]
        public void GeometryBindingV1ToV4_PreservesBodyPresenceCompatibilityMatrix()
        {
            CharacterImportedGeometryBinding v1 = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v1\",\"schemaVersion\":\"1.0\","
                + "\"packageId\":\"legacy.v1\",\"bodyColliders\":[],\"hitZoneBindings\":[],"
                + "\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            CharacterImportedGeometryBinding v2 = CharacterImportedPackageJson.LoadGeometryBinding(
                "{\"format\":\"mx.characterGeometryBinding.v2\",\"schemaVersion\":\"2.0\","
                + "\"packageId\":\"legacy.v2\",\"bodyColliders\":[],\"hitZoneBindings\":[],"
                + "\"damageZones\":[],\"sockets\":[],\"weaponAttachments\":[],\"weaponTraces\":[]}");
            CharacterImportedGeometryBinding v3 = CharacterImportedPackageJson.LoadGeometryBinding(
                BuildV3EmptyGeometryBinding());
            CharacterImportedGeometryBinding v4 = CharacterImportedPackageJson.LoadGeometryBinding(
                BuildV4GeometryBinding("[]"));

            foreach (CharacterImportedGeometryBinding legacy in new[] { v1, v2, v3 })
            {
                Assert.AreEqual(CharacterBodyPresenceMode.CompatibilityHardRoot, legacy.BodyPresenceMode);
                Assert.AreEqual(0, legacy.BodyPresenceRevision);
                Assert.AreEqual(string.Empty, legacy.BodyPresenceContentHash);
                Assert.IsEmpty(legacy.BodyPresenceVolumes);
            }

            Assert.AreEqual("mx.characterGeometryBinding.v4", v4.Format);
            Assert.AreEqual("4.0", v4.SchemaVersion);
            Assert.AreEqual(CharacterBodyPresenceMode.Authored, v4.BodyPresenceMode);
            Assert.AreEqual(3, v4.BodyPresenceRevision);
            Assert.AreEqual(BodyPresenceHash(3, "[]"), v4.BodyPresenceContentHash);
            Assert.IsEmpty(v4.BodyPresenceVolumes);
        }

        [Test]
        public void GeometryBindingV4_ParsesCanonicalPresenceAndCenterDiagnosticProjection()
        {
            string volumesJson = "[" + CorePresenceVolumeJson + "," + TailPresenceVolumeJson + "]";
            CharacterImportedGeometryBinding binding = CharacterImportedPackageJson.LoadGeometryBinding(
                BuildV4GeometryBinding(volumesJson));

            Assert.AreEqual(CharacterBodyPresenceMode.Authored, binding.BodyPresenceMode);
            Assert.AreEqual(BodyPresenceHash(3, volumesJson), binding.BodyPresenceContentHash);
            Assert.AreEqual(2, binding.BodyPresenceVolumes.Length);
            CharacterBodyPresenceRuntimeBinding core = binding.BodyPresenceVolumes[0];
            CharacterBodyPresenceRuntimeBinding tail = binding.BodyPresenceVolumes[1];
            Assert.AreEqual("core", core.VolumeId);
            Assert.AreEqual(CharacterBodyPresenceReferenceFrame.ModelRoot, core.ReferenceFrame);
            Assert.AreEqual(CharacterBodyPresenceShape.Sphere, core.Shape);
            Assert.AreEqual(2f, core.Radius);
            Assert.AreEqual(1f, core.LocalPose.Position.Y);
            Assert.IsTrue(core.Enabled);
            Assert.AreEqual("tail", tail.VolumeId);
            Assert.AreEqual(CharacterBodyPresenceShape.Box, tail.Shape);
            Assert.AreEqual(-3f, tail.LocalPose.Position.Z);
            Assert.AreEqual(4f, tail.Size.Z);
            Assert.IsFalse(tail.Enabled);

            Assert.AreEqual(0.25f, binding.AuthorCapsuleCenterX);
            Assert.AreEqual(-0.5f, binding.AuthorCapsuleCenterZ);
            Assert.AreEqual(0f, binding.RuntimeConsumedCapsuleCenterX);
            Assert.AreEqual(0f, binding.RuntimeConsumedCapsuleCenterZ);
            Assert.IsFalse(binding.RuntimeConsumesAuthorCapsuleCenterXZ);
        }

        [Test]
        public void GeometryBindingV4_RejectsFutureInvalidOrderCapacityAndHash()
        {
            string valid = BuildV4GeometryBinding("[" + CorePresenceVolumeJson + "]");
            Assert.Throws<CharacterImportedPackageJsonException>(() =>
                CharacterImportedPackageJson.LoadGeometryBinding(
                    valid.Replace("mx.characterGeometryBinding.v4", "mx.characterGeometryBinding.v6")));
            Assert.Throws<CharacterImportedPackageJsonException>(() =>
                CharacterImportedPackageJson.LoadGeometryBinding(
                    valid.Replace("\"schemaVersion\":\"4.0\"", "\"schemaVersion\":\"6.0\"")));
            AssertBodyPresenceRejected(
                valid.Replace("\"bodyPresenceMode\":\"Authored\",", string.Empty),
                "CHARPRES101_INVALID_BINDING");
            AssertBodyPresenceRejected(
                BuildV4GeometryBinding("[" + TailPresenceVolumeJson + "," + CorePresenceVolumeJson + "]"),
                "CHARPRES101_INVALID_BINDING");

            string[] capacityVolumes = { "a", "b", "c", "d", "e" };
            string capacityJson = "[" + string.Join(",", capacityVolumes.Select(
                id => CorePresenceVolumeJson.Replace("\"core\"", "\"" + id + "\""))) + "]";
            AssertBodyPresenceRejected(
                BuildV4GeometryBinding(capacityJson),
                "CHARPRES101_INVALID_BINDING");
            AssertBodyPresenceRejected(
                BuildV4GeometryBinding(
                    "[" + CorePresenceVolumeJson.Replace("\"Sphere\"", "\"Cylinder\"") + "]"),
                "CHARPRES101_INVALID_BINDING");
            AssertBodyPresenceRejected(
                BuildV4GeometryBinding(
                    "[" + CorePresenceVolumeJson.Replace("\"radius\":2", "\"radius\":1e39") + "]"),
                "CHARPRES101_INVALID_BINDING");
            AssertBodyPresenceRejected(
                BuildV4GeometryBinding(
                    "[" + CorePresenceVolumeJson + "]",
                    "sha256:" + new string('0', 64)),
                "CHARPRES102_CONTENT_HASH_MISMATCH");
        }

        [Test]
        public void ImportedFormalPackages_ResolveLocomotionFromExactProfileSlots()
        {
            CharacterRuntimeSpawnResult codex = ResolveDefault(CodexFlowTestRoot);
            CharacterRuntimeSpawnResult combatGirl = ResolveDefault(CombatGirlSwordRoot);

            Assert.IsTrue(codex.IsSuccess, FormatIssues(codex));
            Assert.AreEqual("profile.locomotion", codex.Binding.AnimationProfileBinding.ProfileId);
            Assert.AreEqual("locomotion", codex.Binding.AnimationProfileBinding.SlotId);
            Assert.AreEqual("set.locomotion", codex.Binding.AnimationProfileBinding.SetId);
            Assert.AreEqual("group.locomotion", codex.Binding.AnimationProfileBinding.GroupId);
            Assert.AreEqual("blend.move2d", codex.Binding.AnimationProfileBinding.BlendId);
            Assert.AreEqual(CharacterAnimationBlendKind.Blend2D, codex.Binding.AnimationProfileBinding.BlendKind);

            Assert.IsTrue(combatGirl.IsSuccess, FormatIssues(combatGirl));
            Assert.AreEqual("profile.sword", combatGirl.Binding.AnimationProfileBinding.ProfileId);
            Assert.AreEqual("locomotion", combatGirl.Binding.AnimationProfileBinding.SlotId);
            Assert.AreEqual("set.sword", combatGirl.Binding.AnimationProfileBinding.SetId);
            Assert.AreEqual("group.locomotion", combatGirl.Binding.AnimationProfileBinding.GroupId);
            Assert.AreEqual("blend.speed", combatGirl.Binding.AnimationProfileBinding.BlendId);
            Assert.AreEqual(CharacterAnimationBlendKind.Blend1D, combatGirl.Binding.AnimationProfileBinding.BlendKind);
        }

        [Test]
        public void LocomotionResolver_RejectsBlendWithMissingDefaultClip()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CombatGirlSwordRoot));
            CharacterAnimationSetDefinition source = package.CompiledAnimationArtifacts.AnimationSetDefinition;
            CharacterAnimationSet[] sets = source.Sets.Select(set =>
                !string.Equals(set.SetId, "set.sword", System.StringComparison.Ordinal)
                    ? set
                    : new CharacterAnimationSet(
                        set.SetId,
                        set.DisplayName,
                        set.Version,
                        set.DefaultClipId,
                        set.FallbackClipId,
                        set.Groups.Select(group =>
                            !string.Equals(group.GroupId, "group.locomotion", System.StringComparison.Ordinal)
                                ? group
                                : new CharacterAnimationGroup(
                                    group.GroupId,
                                    group.DisplayName,
                                    group.Usage,
                                    group.Clips,
                                    group.Blends.Select(blend =>
                                        !string.Equals(blend.BlendId, "blend.speed", System.StringComparison.Ordinal)
                                            ? blend
                                            : new CharacterAnimationBlendReference(
                                                blend.BlendId,
                                                blend.BlendKind,
                                                string.Empty,
                                                blend.ClipIds)).ToArray())).ToArray(),
                        set.ActionBindings)).ToArray();
            var invalidDefinition = new CharacterAnimationSetDefinition(
                source.Format,
                source.SchemaVersion,
                source.PackageId,
                source.StableId,
                source.DisplayName,
                source.SkeletonProfileId,
                source.AvatarProfileId,
                sets,
                source.Profiles);

            CharacterRuntimeAnimationProfileResolveResult result = CharacterRuntimeAnimationProfileResolver.Resolve(
                WithAnimationDefinition(package, invalidDefinition),
                "profile.sword");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(CharacterRuntimeSpawnIssueCodes.MissingLocomotionClip, result.IssueCode);
        }

        [Test]
        public void LocomotionResolver_DoesNotUseLegacyPurposeUsageOrActionBindingFallbacks()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterAnimationSetDefinition source = package.CompiledAnimationArtifacts.AnimationSetDefinition;
            CharacterAnimationSet set = source.Sets.Single(candidate => candidate.SetId == "set.locomotion");
            CharacterAnimationProfile profile = source.Profiles.Single(candidate => candidate.ProfileId == "profile.locomotion");
            var invalidProfile = new CharacterAnimationProfile(
                profile.ProfileId,
                profile.DisplayName,
                set.SetId,
                "group.locomotion",
                new[]
                {
                    new CharacterAnimationProfileSlot(
                        "Locomotion",
                        "Legacy alias",
                        "locomotion",
                        set.SetId,
                        "group.locomotion",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        true)
                });
            var invalidDefinition = new CharacterAnimationSetDefinition(
                source.Format,
                source.SchemaVersion,
                source.PackageId,
                source.StableId,
                source.DisplayName,
                source.SkeletonProfileId,
                source.AvatarProfileId,
                source.Sets,
                new[] { invalidProfile });
            CharacterImportedPackage invalidPackage = WithAnimationDefinition(package, invalidDefinition);

            CharacterRuntimeAnimationProfileResolveResult result = CharacterRuntimeAnimationProfileResolver.Resolve(
                invalidPackage,
                "profile.locomotion");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(CharacterRuntimeSpawnIssueCodes.MissingLocomotionSlot, result.IssueCode);
        }

        [Test]
        public void ImportedCodexFlowTest_WeaponTraceBindingKeepsLocatorPoses()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            CharacterWeaponTraceRuntimeBinding trace = package.Geometry.WeaponTraces.Single(
                candidate => candidate.TraceId == MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986);

            Assert.AreEqual("sword", trace.WeaponId);
            Assert.AreEqual("mainHand", trace.EquipSlot);
            Assert.AreEqual("weapon/main_hand_katana/blade_root", trace.StartLocatorPath);
            Assert.AreEqual("weapon/main_hand_katana/blade_tip", trace.EndLocatorPath);
            Assert.AreEqual("Socket", trace.StartPose.ParentKind);
            Assert.AreEqual("mainHand", trace.StartPose.ParentPath);
            Assert.AreEqual(0f, trace.StartPose.Position.X, 0.0001f);
            Assert.AreEqual(0f, trace.StartPose.Position.Y, 0.0001f);
            Assert.AreEqual(0f, trace.StartPose.Position.Z, 0.0001f);
            Assert.AreEqual("Socket", trace.EndPose.ParentKind);
            Assert.AreEqual("mainHand", trace.EndPose.ParentPath);
            Assert.AreEqual(0f, trace.EndPose.Position.X, 0.0001f);
            Assert.AreEqual(1.071494f, trace.EndPose.Position.Y, 0.0001f);
            Assert.AreEqual(0.000843f, trace.EndPose.Position.Z, 0.0001f);
            Assert.AreEqual(0.055f, trace.Radius, 0.0001f);
            Assert.AreEqual("CapsuleSweep", trace.SampleRule);
            Assert.AreEqual(6, trace.FixedSampleCount);
            CollectionAssert.AreEqual(new[] { "primary", "heavy" }, trace.ActionKeys);
        }

        [Test]
        public void ImportedCodexFlowTest_WeaponTraceCarriesProximalRange()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            CharacterWeaponTraceRuntimeBinding trace = package.Geometry.WeaponTraces.Single(
                candidate => candidate.TraceId == MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986);

            Assert.AreEqual(0.35f, trace.ProximalExtension, 0.0001f);
            Assert.AreEqual(0.18f, trace.ProximalRadius, 0.0001f);

            bool resolved = CharacterWeaponTraceBindingResolver.TryResolve(
                package,
                1,
                explicitTraceProfileId: 0,
                out CharacterResolvedWeaponTraceBinding binding,
                out CharacterWeaponTraceResolveIssue issue);

            Assert.IsTrue(resolved, issue.Code + ":" + issue.Message);
            Assert.AreEqual(0.35f, binding.ProximalExtension, 0.0001f);
            Assert.AreEqual(0.18f, binding.ProximalRadius, 0.0001f);

            CharacterTraceBindingEntry configBinding = package.Configs.Weapons
                .SelectMany(weapon => weapon.TraceBindings)
                .Single(candidate => candidate.TraceProfileId == "trace.iron_sword.blade");
            Assert.AreEqual(0.35f, configBinding.ProximalExtension, 0.0001f);
            Assert.AreEqual(0.18f, configBinding.ProximalRadius, 0.0001f);
        }

        [Test]
        public void WeaponTraceResolver_UsesSlotFirstAttachmentTrace()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            bool resolved = CharacterWeaponTraceBindingResolver.TryResolve(
                package,
                1,
                explicitTraceProfileId: 0,
                out CharacterResolvedWeaponTraceBinding binding,
                out CharacterWeaponTraceResolveIssue issue);

            Assert.IsTrue(resolved, issue.Code + ":" + issue.Message);
            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986, binding.TraceId);
            Assert.AreEqual(1, binding.EquipmentSlotId);
            Assert.AreEqual(1, binding.WeaponId);
            Assert.AreEqual(ExpectedTraceLength(binding.TraceBinding), binding.Length, 0.0001f);
            Assert.AreEqual(0.055f, binding.Radius, 0.0001f);
            Assert.AreEqual("CapsuleSweep", binding.SampleRule);
            Assert.IsFalse(binding.UsedLegacyTraceProfileId);
            Assert.AreSame(package.Geometry.WeaponTraces.Single(trace => trace.TraceId == binding.TraceId), binding.TraceBinding);
        }

        [Test]
        public void WeaponTraceResolver_RetainsLegacyExplicitTraceFallback()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            bool resolved = CharacterWeaponTraceBindingResolver.TryResolve(
                package,
                equipmentSlotId: 0,
                explicitTraceProfileId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                out CharacterResolvedWeaponTraceBinding binding,
                out CharacterWeaponTraceResolveIssue issue);

            Assert.IsTrue(resolved, issue.Code + ":" + issue.Message);
            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986, binding.TraceId);
            Assert.AreEqual(1, binding.EquipmentSlotId);
            Assert.IsTrue(binding.UsedLegacyTraceProfileId);
        }

        [Test]
        public void WeaponTraceResolver_ReportsMissingAndAmbiguousTraceDiagnostics()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            bool missingResolved = CharacterWeaponTraceBindingResolver.TryResolve(
                package,
                1,
                999999,
                out _,
                out CharacterWeaponTraceResolveIssue missing);

            Assert.IsFalse(missingResolved);
            Assert.AreEqual(CharacterWeaponTraceBindingResolver.IssueTraceMissing, missing.Code);
            Assert.AreEqual("geometry/traces.json", missing.SourcePath);
            Assert.AreEqual("geometry/traces/trace.missing.blade", missing.SourceObjectPath);

            CharacterImportedPackage ambiguousPackage = CreateAmbiguousTracePackage(package);
            bool ambiguousResolved = CharacterWeaponTraceBindingResolver.TryResolve(
                ambiguousPackage,
                1,
                0,
                out _,
                out CharacterWeaponTraceResolveIssue ambiguous);

            Assert.IsFalse(ambiguousResolved);
            Assert.AreEqual(CharacterWeaponTraceBindingResolver.IssueTraceAmbiguous, ambiguous.Code);
            Assert.AreEqual("geometry/traces/sword", ambiguous.SourceObjectPath);
        }

        [Test]
        public void WeaponTraceBakeRequestResolver_UsesSlotFirstResolver()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));

            int slotTraceId = CharacterWeaponTraceBakeCatalogBuilder.ResolveTraceIdForEquipmentSlot(
                package,
                1,
                explicitTraceId: 0);
            int legacyTraceId = CharacterWeaponTraceBakeCatalogBuilder.ResolveTraceIdForEquipmentSlot(
                package,
                equipmentSlotId: 0,
                explicitTraceId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986);

            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986, slotTraceId);
            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986, legacyTraceId);
        }

        [Test]
        public void WeaponTraceBakeCatalog_RegistersArtifactAndReadsTraceFrame()
        {
            const string clipKey = "art.character.skeleton.animation.katana_atk1";
            const int traceId = MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986;
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterWeaponTraceRuntimeBinding trace = package.Geometry.WeaponTraces.Single(
                candidate => candidate.TraceId == traceId);
            var catalog = new CharacterWeaponTraceBakeCatalog();

            MxAnimationBakeArtifact artifact =
                CharacterWeaponTraceBakeArtifactBuilder.CreateAuthoredEndpointArtifact(
                    clipKey,
                    traceId,
                    numericTraceId: 17,
                    trace,
                    new CharacterWeaponTraceBakeSampleOptions
                    {
                        MaxFrame = 40,
                        ActiveStartFrame = 18,
                        ActiveEndFrame = 35,
                        QuantizationScale = 1000
                    });
            catalog.RegisterArtifact(clipKey, traceId, artifact);

            Assert.AreEqual(1, catalog.Count);
            Assert.IsTrue(catalog.TryGetArtifact(clipKey, traceId, out MxAnimationBakeArtifact registered));
            Assert.AreSame(artifact, registered);
            Assert.AreEqual(41, registered.WeaponTraceFrames.Count);
            Assert.IsTrue(catalog.TryGetFrame(clipKey, traceId, 23, out MxAnimationBakedWeaponTraceFrame frame));
            Assert.AreEqual(23, frame.LocalFrame);
            Assert.AreEqual(17, frame.TraceId);
            Assert.AreEqual(1, frame.SocketId);
            Assert.AreEqual(0L, frame.RootNow.X);
            Assert.AreEqual(0L, frame.RootNow.Y);
            Assert.AreEqual(0L, frame.RootNow.Z);
            Assert.AreEqual(0L, frame.TipNow.X);
            Assert.AreEqual(1071L, frame.TipNow.Y);
            Assert.AreEqual(1L, frame.TipNow.Z);
            Assert.AreEqual(frame.RootPrev.X, frame.RootNow.X);
            Assert.AreEqual(frame.RootPrev.Y, frame.RootNow.Y);
            Assert.AreEqual(frame.RootPrev.Z, frame.RootNow.Z);
            Assert.AreEqual(frame.TipPrev.X, frame.TipNow.X);
            Assert.AreEqual(frame.TipPrev.Y, frame.TipNow.Y);
            Assert.AreEqual(frame.TipPrev.Z, frame.TipNow.Z);
            Assert.IsFalse(catalog.TryGetFrame(clipKey, traceId, 41, out _));
        }

        [Test]
        public void DefaultBakedWeaponTraceSource_CoversCharacterTestKatanaComboClips()
        {
            const int traceId = MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986;
            CharacterTestDefaultBakedWeaponTraceSource source = CharacterTestDefaultBakedWeaponTraceSource.Instance;
            var requests = CharacterTestWeaponTraceBakeRequests.Create(
                CharacterTestGeneratedPackageSource.LoadDefaultCharacterPackage(),
                CharacterTestCharacterActionConfigSource.LoadInspectionCatalog().Registry);

            Assert.IsTrue(
                source.BuildReport.Success,
                string.Join(", ", source.BuildReport.Issues.Select(issue =>
                    issue.Code + ":" + issue.ActionId + ":" + issue.ClipKey + ":" + issue.TraceId + ":" + issue.Frame)));
            Assert.GreaterOrEqual(source.BuildReport.RequestCount, 3);
            Assert.GreaterOrEqual(source.BuildReport.ArtifactCount, 3);
            Assert.GreaterOrEqual(source.BuildReport.CheckedFrameCount, 3);

            Assert.IsTrue(
                source.TryGetFrame(
                    "art.character.skeleton.animation.katana_atk1",
                    traceId,
                    requests.First(r => r.ClipKey == "art.character.skeleton.animation.katana_atk1").ActiveStartFrame,
                    out MxAnimationBakedWeaponTraceFrame firstFrame),
                "katana_atk1 should have a baked trace frame in the active hit window.");
            Assert.IsTrue(
                source.TryGetFrame(
                    "art.character.skeleton.animation.katana_atk2",
                    traceId,
                    requests.First(r => r.ClipKey == "art.character.skeleton.animation.katana_atk2").ActiveStartFrame,
                    out MxAnimationBakedWeaponTraceFrame secondFrame),
                "katana_atk2 should have a baked trace frame in the active hit window.");
            Assert.IsTrue(
                source.TryGetFrame(
                    "art.character.skeleton.animation.katana_atk3",
                    traceId,
                    requests.First(r => r.ClipKey == "art.character.skeleton.animation.katana_atk3").ActiveStartFrame,
                    out MxAnimationBakedWeaponTraceFrame thirdFrame),
                "katana_atk3 should have a baked trace frame in the active hit window.");

            Assert.AreEqual(firstFrame.TraceId, secondFrame.TraceId);
            Assert.AreEqual(firstFrame.TraceId, thirdFrame.TraceId);
            Assert.AreEqual(1, firstFrame.SocketId);
            Assert.AreEqual(1, secondFrame.SocketId);
            Assert.AreEqual(1, thirdFrame.SocketId);
            Assert.AreNotEqual(0L, firstFrame.RootNow.X);
            Assert.AreNotEqual(0L, firstFrame.RootNow.Y);
            Assert.AreNotEqual(firstFrame.RootNow.X, firstFrame.TipNow.X);
            Assert.AreNotEqual(firstFrame.RootNow.Y, firstFrame.TipNow.Y);
            Assert.AreNotEqual(firstFrame.RootNow.Z, firstFrame.TipNow.Z);
            Assert.AreNotEqual(786533L, firstFrame.TipNow.Y);
        }

        [Test]
        public void Dragon14SocketBake_ResolvesOnlyFromEntityGeneratedPackageAndFailsClosedOnMissingFrame()
        {
            string packageRoot = ProjectPath(Dragon14Root);
            string indexRelativePath = CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath;
            if (!File.Exists(Path.Combine(packageRoot, indexRelativePath)))
                Assert.Ignore("Dragon14 generated socket bake catalog is not present.");

            Assert.IsTrue(
                CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    packageRoot,
                    "000003",
                    out CharacterSocketBakeCatalog catalog,
                    out _,
                    out string[] issues),
                string.Join("; ", issues));
            Assert.AreEqual(0, issues.Length, string.Join("; ", issues));
            Assert.AreEqual(106, catalog.TrackCount);
            Assert.IsTrue(catalog.TryGetFrame(
                "char.000003.animation.attack1a",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id37,
                38,
                out MxAnimationBakedSocketFrame start));
            Assert.IsTrue(catalog.TryGetFrame(
                "char.000003.animation.attack1a",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id38,
                38,
                out MxAnimationBakedSocketFrame end));
            Assert.IsNotNull(start);
            Assert.IsNotNull(end);
            Assert.IsFalse(catalog.TryGetFrame(
                "char.000003.animation.attack1a",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id37,
                121,
                out _));

            CharacterTestDefaultBakedWeaponTraceSource source = CharacterTestDefaultBakedWeaponTraceSource.Instance;
            Assert.IsTrue(source.TryGetSocketFrame(
                Dragon14Root,
                "char.000003.animation.attack1a",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id37,
                38,
                out _));
            Assert.IsFalse(source.TryGetSocketFrame(
                CodexFlowTestRoot,
                "char.000003.animation.attack1a",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id37,
                38,
                out _));
        }

        [Test]
        public void BakedWeaponTraceProvider_UsesBakedTipEndpointInsteadOfBindingLength()
        {
            var frame = new MxAnimationBakedWeaponTraceFrame(
                12,
                17,
                1,
                new MxAnimationBakedVector3(0, 0, 0),
                new MxAnimationBakedVector3(0, 2000000, 0),
                new MxAnimationBakedVector3(0, 0, 0),
                new MxAnimationBakedVector3(0, 2000000, 0));
            var binding = new CharacterTestWeaponTraceBinding(
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                length: 0.1f,
                radius: 0.05f);
            CharacterTestCharacterEntity entity = CreateTestEntity(
                new CharacterTestCharacterTransformComponent(1f, 0.5f, -2f));

            var trace = CharacterTestBakedWeaponTraceProvider.BuildTraceFrame(
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                frame,
                binding,
                entity,
                CombatPhysicsLayerMask.All);

            AssertFix(1f, trace.RootNow.X);
            AssertFix(0.5f, trace.RootNow.Y);
            AssertFix(-2f, trace.RootNow.Z);
            AssertFix(1f, trace.TipNow.X);
            AssertFix(2.5f, trace.TipNow.Y);
            AssertFix(-2f, trace.TipNow.Z);
        }

        [Test]
        public void RuntimeSpawnModule_ProcessesQueuedSpawnRequestThroughRuntimeHost()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            var module = new CharacterRuntimeSpawnModule(package);
            module.Enqueue(new CharacterSpawnRequest(new SpawnProfileId(820001), teamOverride: "team.blue"));

            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                host.Tick(0, 1.0 / 60.0);
            }

            Assert.AreEqual(1, module.Results.Count);
            Assert.IsTrue(module.LastResult.IsSuccess, string.Join(", ", module.LastResult.Issues.Select(issue => issue.Code)));
            Assert.AreEqual("team.blue", module.LastResult.Binding.SpawnPlan.TeamId);
            Assert.AreEqual("team.blue", module.LastResult.Binding.GameplayRegistrationPlan.TeamId);
        }

        [Test]
        public void GameplaySpawnDefinitionBuilder_EmitsCoreGameplayComponents()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package, teamOverride: "team.blue");

            CharacterGameplaySpawnDefinitionBuildResult build =
                CharacterGameplaySpawnDefinitionBuilder.Build(binding, package.Configs);
            GameplayComponentWorld world = CreateGameplayWorld();
            GameplayEntityId entity = world.CreateEntity();
            var context = new GameplayComponentSpawnContext(
                RuntimeFrame.Zero,
                GameplayRuntimeCommandFactory.SpawnComponentEntity(RuntimeFrame.Zero, build.Definition.DefinitionId),
                build.Definition,
                binding.ResolvedProfile.LoadoutId.Value);
            for (int i = 0; i < build.Definition.Initializers.Count; i++)
            {
                RuntimeSaveStateResult<bool> apply = build.Definition.Initializers[i].Apply(world, entity, context);
                Assert.IsTrue(apply.Success, apply.Error.ToString());
            }

            Assert.AreEqual(710001, build.Definition.DefinitionId);
            Assert.AreEqual("mxframework.character." + package.Configs.Characters[0].StableId, build.Definition.StableId);
            Assert.AreEqual(1, build.Definition.SchemaVersion);
            Assert.IsEmpty(build.Diagnostics);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayIdentityComponent> identities));
            Assert.IsTrue(identities.TryGet(entity, out GameplayIdentityComponent identity));
            Assert.AreEqual(710001, identity.DefinitionId);
            Assert.AreEqual(binding.ResolvedProfile.LoadoutId.Value, identity.VariantId);

            Assert.IsTrue(
                world.TryGetStore(
                    out GameplayComponentStore<
                        GameplayTargetClassificationComponent>
                        classifications));
            Assert.IsTrue(
                classifications.TryGet(
                    entity,
                    out GameplayTargetClassificationComponent
                        classification));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                classification.Capabilities);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams));
            Assert.IsTrue(teams.TryGet(entity, out GameplayTeamComponent team));
            Assert.AreEqual(DeterministicTeamId("team.blue"), team.TeamId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycle));
            Assert.IsTrue(lifecycle.TryGet(entity, out GameplayLifecycleComponent lifecycleComponent));
            Assert.AreEqual(GameplayLifecycleState.Alive, lifecycleComponent.State);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributes));
            Assert.IsTrue(attributes.TryGet(entity, out GameplayAttributeSetComponent attributeSet));
            Assert.AreEqual(4, attributeSet.Count);
            Assert.IsTrue(attributeSet.TryGet(1, out GameplayAttributeValue hp));
            Assert.AreEqual(100, hp.BaseValue);
            Assert.AreEqual(100, hp.CurrentValue);
            Assert.IsTrue(attributeSet.TryGet(2, out GameplayAttributeValue attack));
            Assert.AreEqual(20, attack.CurrentValue);
            Assert.IsTrue(attributeSet.TryGet(3, out GameplayAttributeValue defense));
            Assert.AreEqual(10, defense.CurrentValue);
            Assert.IsTrue(attributeSet.TryGet(920002, out GameplayAttributeValue stamina));
            Assert.AreEqual(100, stamina.BaseValue);
            Assert.AreEqual(100, stamina.CurrentValue);
        }

        [Test]
        public void GameplaySpawnDefinitionBuilder_ClassifiesCharacterTargets()
        {
            CharacterImportedPackage package =
                CharacterImportedPackageJson.LoadFromDirectory(
                    ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding =
                ResolveCodexFlowTestBinding(
                    package,
                    teamOverride: "team.blue");
            CharacterGameplaySpawnDefinitionBuildResult build =
                CharacterGameplaySpawnDefinitionBuilder.Build(
                    binding,
                    package.Configs);
            GameplayComponentWorld world = CreateGameplayWorld();
            GameplayEntityId entity = world.CreateEntity();
            var context = new GameplayComponentSpawnContext(
                RuntimeFrame.Zero,
                GameplayRuntimeCommandFactory.SpawnComponentEntity(
                    RuntimeFrame.Zero,
                    build.Definition.DefinitionId),
                build.Definition,
                binding.ResolvedProfile.LoadoutId.Value);

            for (int i = 0;
                 i < build.Definition.Initializers.Count;
                 i++)
            {
                RuntimeSaveStateResult<bool> apply =
                    build.Definition.Initializers[i].Apply(
                        world,
                        entity,
                        context);
                Assert.IsTrue(
                    apply.Success,
                    apply.Error.ToString());
            }

            Assert.IsTrue(
                world.TryGetStore(
                    out GameplayComponentStore<
                        GameplayTargetClassificationComponent>
                        classifications));
            Assert.IsTrue(
                classifications.TryGet(
                    entity,
                    out GameplayTargetClassificationComponent
                        classification));
            Assert.AreEqual(
                GameplayTargetIdentityTypes.Character,
                classification.IdentityType);
            Assert.AreEqual(
                GameplayTargetCapabilities.Damageable
                    | GameplayTargetCapabilities.Movable,
                classification.Capabilities);
        }

        [Test]
        public void GameplayAbilityRegistryBuilder_ReportsDeferredAbilityBootstrap()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);

            CharacterGameplayAbilityRegistryBuildResult build =
                CharacterGameplayAbilityRegistryBuilder.BuildDeferred(binding);

            Assert.IsTrue(build.IsDeferred);
            Assert.IsNotNull(build.Registry);
            Assert.AreEqual(4, build.EffectiveAbilityIds.Length);
            Assert.AreEqual(0, build.RegisteredAbilityIds.Length);
            Assert.AreEqual(4, build.MissingAbilityIds.Length);
            Assert.IsTrue(build.Diagnostics.Any(line => line.Contains("deferred")));
        }

        [Test]
        public void GameplayAbilityRegistryBuilder_RegistersExplicitCatalogBindingsAndReportsMissingIds()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);
            CharacterAbilityId registeredId = binding.ResolvedProfile.EffectiveAbilityIds[0];
            var catalog = new CharacterGameplayAbilityBindingCatalog();
            catalog.Register(
                registeredId,
                new GameplayComponentAttributeDeltaAbility(
                    registeredId.Value,
                    920001,
                    -1,
                    GameplayComponentTargetMode.Self));

            CharacterGameplayAbilityRegistryBuildResult build =
                CharacterGameplayAbilityRegistryBuilder.Build(binding, catalog);

            Assert.IsFalse(build.IsDeferred);
            Assert.AreEqual(4, build.EffectiveAbilityIds.Length);
            Assert.AreEqual(1, build.RegisteredAbilityIds.Length);
            Assert.AreEqual(3, build.MissingAbilityIds.Length);
            Assert.AreEqual(1, build.Registry.Count);
            Assert.IsTrue(build.Registry.TryGet(registeredId.Value, out _));
            Assert.IsTrue(build.Diagnostics.Any(line => line.Contains("No gameplay component ability binding registered")));
        }

        [Test]
        public void GameplayRuntimeBootstrap_UsesExplicitAbilityBindingCatalog()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);
            CharacterAbilityId registeredId = binding.ResolvedProfile.EffectiveAbilityIds[0];
            var catalog = new CharacterGameplayAbilityBindingCatalog();
            catalog.Register(
                registeredId,
                new GameplayComponentAttributeDeltaAbility(
                    registeredId.Value,
                    920001,
                    -1,
                    GameplayComponentTargetMode.Self));

            using (var runtime = new CharacterGameplayRuntimeBootstrap(catalog))
            {
                CharacterGameplaySpawnResult spawn = runtime.Spawn(binding, package.Configs);

                Assert.IsTrue(runtime.AbilityRegistry.TryGet(registeredId.Value, out _));
                Assert.IsTrue(spawn.Success, string.Join(", ", spawn.Diagnostics));
                Assert.IsFalse(spawn.AbilityRegistry.IsDeferred);
                Assert.AreEqual(1, spawn.AbilityRegistry.RegisteredAbilityIds.Length);
                Assert.AreEqual(3, spawn.AbilityRegistry.MissingAbilityIds.Length);
            }
        }

        [Test]
        public void GameplayRuntimeBootstrap_SpawnsLiveEntityAndRegistersHandle()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);

            using (var runtime = new CharacterGameplayRuntimeBootstrap())
            {
                CharacterGameplaySpawnResult spawn = runtime.Spawn(binding, package.Configs);

                Assert.IsTrue(spawn.Success, string.Join(", ", spawn.Diagnostics));
                Assert.IsTrue(spawn.Handle.GameplayEntityId.IsValid);
                Assert.AreNotEqual(binding.GameplayRegistrationPlan.EntityId, spawn.Handle.GameplayEntityId);
                Assert.AreEqual(710001, spawn.Handle.StableCharacterId);
                Assert.AreEqual(710001, spawn.Handle.SpawnDefinitionId);
                Assert.AreEqual(spawn.Handle.GameplayEntityId, spawn.Handle.EntityRef.GameplayEntityId);
                Assert.AreEqual(1, runtime.EntityRegistry.Count);
                Assert.IsTrue(runtime.EntityRegistry.TryGetByStableCharacterId(710001, out CharacterRuntimeEntityHandle byStable));
                Assert.AreEqual(spawn.Handle, byStable);
                Assert.IsTrue(runtime.EntityRegistry.TryGetByGameplayEntityId(spawn.Handle.GameplayEntityId, out CharacterRuntimeEntityHandle byEntity));
                Assert.AreEqual(spawn.Handle, byEntity);
                Assert.IsTrue(runtime.World.IsAlive(spawn.Handle.GameplayEntityId));
                Assert.IsTrue(runtime.World.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributes));
                Assert.IsTrue(attributes.TryGet(spawn.Handle.GameplayEntityId, out GameplayAttributeSetComponent attributeSet));
                Assert.AreEqual(3, attributeSet.Count);
                Assert.IsTrue(spawn.AbilityRegistry.IsDeferred);
            }
        }

        [Test]
        public void GameplayRuntimeBootstrap_RejectsRepeatedSpawnForStableCharacter()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);

            using (var runtime = new CharacterGameplayRuntimeBootstrap())
            {
                CharacterGameplaySpawnResult first = runtime.Spawn(binding, package.Configs);
                CharacterGameplaySpawnResult second = runtime.Spawn(binding, package.Configs);

                Assert.IsTrue(first.Success, string.Join(", ", first.Diagnostics));
                Assert.IsFalse(second.Success);
                Assert.AreEqual("CharacterStableIdAlreadyRegistered", second.Reason);
                Assert.AreEqual(1, runtime.EntityRegistry.Count);
                Assert.AreEqual(1, runtime.World.CountAlive);
            }
        }

        [Test]
        public void GameplayRuntimeBootstrap_DestroyRemovesRegistryAndWorldState()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            CharacterRuntimeBinding binding = ResolveCodexFlowTestBinding(package);

            using (var runtime = new CharacterGameplayRuntimeBootstrap())
            {
                CharacterGameplaySpawnResult spawn = runtime.Spawn(binding, package.Configs);
                Assert.IsTrue(spawn.Success, string.Join(", ", spawn.Diagnostics));

                CharacterGameplayDestroyResult destroy = runtime.Destroy(710001);

                Assert.IsTrue(destroy.Success, destroy.Reason);
                Assert.AreEqual(spawn.Handle, destroy.Handle);
                Assert.AreEqual(0, runtime.EntityRegistry.Count);
                Assert.IsFalse(runtime.EntityRegistry.TryGetByStableCharacterId(710001, out _));
                Assert.IsFalse(runtime.World.IsAlive(spawn.Handle.GameplayEntityId));
                Assert.AreEqual(0, runtime.World.CountAlive);
            }
        }

        [Test]
        public void SpawnBlockedImportReport_DoesNotCreateRuntimeBinding()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(CodexFlowTestRoot));
            var blockedReport = new CharacterUnityImportRuntimeReport(
                package.ImportReport.PackageId,
                package.ImportReport.PackageStableId,
                "SpawnBlocked",
                package.ImportReport.CanWriteToUnityProject,
                canSpawnAfterImport: false,
                package.ImportReport.TargetRootPath,
                package.ImportReport.ReportPath,
                package.ImportReport.SourcePackageHash,
                package.ImportReport.GeneratedConfigHash,
                package.ImportReport.GeometryBindingHash,
                package.ImportReport.ResourceMappingHash,
                package.ImportReport.WritePlanHash,
                package.ImportReport.ConflictCount,
                package.ImportReport.ErrorCount);
            var blockedPackage = new CharacterImportedPackage(
                package.RootPath,
                package.PackageId,
                package.Configs,
                package.Geometry,
                package.ResourceMapping,
                package.UnityResourceCatalog,
                blockedReport);

            CharacterRuntimeSpawnResult result = CharacterRuntimeSpawnResolver.Resolve(
                blockedPackage,
                new CharacterSpawnRequest(new SpawnProfileId(820001)));

            Assert.AreEqual(CharacterRuntimeSpawnStatus.SpawnBlocked, result.Status);
            Assert.IsNull(result.Binding);
            Assert.IsTrue(result.Issues.Any(issue => issue.Code == CharacterRuntimeSpawnIssueCodes.SpawnBlocked));
        }

        [Test]
        public void ImportedPackage_LoadsCompiledAnimationArtifactsWithoutReadingAuthoringJson()
        {
            string packageRoot = CreateTempImportedPackage("compiled-animation-artifacts");
            string configRoot = Path.Combine(packageRoot, "config");
            WriteCompiledAnimationArtifacts(configRoot);
            File.WriteAllText(Path.Combine(configRoot, "animation_authoring.json"), "{ this is intentionally not compiled runtime json");

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);

            Assert.IsTrue(package.CompiledAnimationArtifacts.HasRequiredArtifacts);
            Assert.IsEmpty(package.CompiledAnimationArtifacts.Diagnostics);
            Assert.AreEqual("mx.animationSetDefinition.v1", package.CompiledAnimationArtifacts.AnimationSetDefinition.Format);
            Assert.AreEqual("animation.codex_flow_test", package.CompiledAnimationArtifacts.AnimationSetDefinition.PackageId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets.Length);
            Assert.AreEqual("set.combat", package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].SetId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].Groups.Length);
            Assert.AreEqual("clip.slash", package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].Groups[0].Clips[0].ClipId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationClipRegistry.Clips.Length);
            Assert.AreEqual("char.codex_flow_test.anim.combat", package.CompiledAnimationArtifacts.AnimationClipRegistry.Clips[0].RuntimeResourceKey);
            Assert.AreEqual("sha256:animation-plan", package.CompiledAnimationArtifacts.AnimationResourcePlan.PlanHash);
            Assert.IsTrue(package.CompiledAnimationArtifacts.HasRuntimeAnimationContracts);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.RuntimeMappingProvider.Definitions.Count);
            Assert.AreEqual("set.combat", package.CompiledAnimationArtifacts.RuntimeMappingProvider.Definitions[0].SetId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.RuntimeClipRegistry.Entries.Count);
        }

        [Test]
        public void ImportedPackage_MissingCompiledAnimationArtifactsReportsStableDiagnostics()
        {
            string packageRoot = CreateTempImportedPackage("missing-animation-artifacts");
            string configRoot = Path.Combine(packageRoot, "config");
            File.Delete(Path.Combine(configRoot, "animation_set_definition.json"));
            File.Delete(Path.Combine(configRoot, "animation_clip_registry.json"));
            File.Delete(Path.Combine(configRoot, "animation_resource_plan.json"));
            File.WriteAllText(Path.Combine(configRoot, "animation_authoring.json"), "{\"format\":\"authoring-only\"}");

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);

            Assert.IsFalse(package.CompiledAnimationArtifacts.HasRequiredArtifacts);
            Assert.AreEqual(3, package.CompiledAnimationArtifacts.Diagnostics.Length);
            Assert.IsTrue(package.CompiledAnimationArtifacts.Diagnostics.All(issue => issue.Code == CharacterRuntimeSpawnIssueCodes.MissingCompiledAnimationArtifact));
            Assert.IsTrue(package.CompiledAnimationArtifacts.Diagnostics.Any(issue => issue.SourcePath == "config/animation_set_definition.json"));
            Assert.IsTrue(package.CompiledAnimationArtifacts.Diagnostics.Any(issue => issue.SourcePath == "config/animation_clip_registry.json"));
            Assert.IsTrue(package.CompiledAnimationArtifacts.Diagnostics.Any(issue => issue.SourcePath == "config/animation_resource_plan.json"));
        }

        [Test]
        public void Dragon14Package_PreservesExplicitHurtboxResolveAndMaterialFields()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                ProjectPath("Assets/MxFrameworkGenerated/CharacterPackages/000003"));

            Assert.That(package.Geometry.BodyColliders, Has.Length.EqualTo(18));
            CharacterBodyColliderRuntimeBinding head = package.Geometry.BodyColliders.Single(
                collider => collider.ColliderId == MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id150);
            Assert.That(head.PartId, Is.EqualTo("head"));
            Assert.That(head.PhysicsLayer, Is.EqualTo("CharacterHurtbox"));
            Assert.That(head.DamageMultiplierOverride, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(head.ImpactPowerScaleOverride, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(head.MaterialStableId, Is.EqualTo("material.000003.scale"));
        }

        private static string BuildV3EmptyGeometryBinding()
        {
            return "{\"format\":\"mx.characterGeometryBinding.v3\",\"schemaVersion\":\"3.0\","
                + "\"packageId\":\"legacy.v3\",\"bodyColliders\":[],\"hitZoneBindings\":[],"
                + "\"damageZones\":[],\"engagementAnchorRevision\":1,"
                + "\"engagementAnchorContentHash\":\"" + Sha256("revision|1\n") + "\","
                + "\"engagementAnchors\":[],\"sockets\":[],\"weaponAttachments\":[],"
                + "\"weaponTraces\":[]}";
        }

        private static string BuildV4GeometryBinding(
            string volumesJson,
            string contentHashOverride = null)
        {
            string contentHash = contentHashOverride ?? BodyPresenceHash(3, volumesJson);
            return "{\"format\":\"mx.characterGeometryBinding.v4\",\"schemaVersion\":\"4.0\","
                + "\"packageId\":\"presence.test\",\"bodyProfile\":{\"profileId\":\"body.presence\","
                + "\"bodyKind\":\"Large\",\"bodyScale\":1,\"heightMeters\":8,\"radiusMeters\":2,"
                + "\"massKg\":1000,\"defaultCapsule\":{\"height\":8,\"radius\":2,"
                + "\"center\":{\"x\":0.25,\"y\":4,\"z\":-0.5}}},\"bodyColliders\":[],"
                + "\"hitZoneBindings\":[],\"damageZones\":[],\"sockets\":[],"
                + "\"weaponAttachments\":[],\"weaponTraces\":[],\"bodyPresenceMode\":\"Authored\","
                + "\"bodyPresenceRevision\":3,\"bodyPresenceContentHash\":\"" + contentHash + "\","
                + "\"bodyPresenceVolumes\":" + volumesJson + "}";
        }

        private static string BodyPresenceHash(int revision, string volumesJson)
        {
            return Sha256(
                "{\"format\":\"mx.characterBodyPresence.v1\",\"schemaVersion\":\"1.0\","
                + "\"revision\":" + revision + ",\"volumes\":" + volumesJson + "}");
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder("sha256:");
                for (int i = 0; i < hash.Length; i++)
                    result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }

        private static void AssertBodyPresenceRejected(string json, string expectedCode)
        {
            CharacterImportedPackageJsonException exception =
                Assert.Throws<CharacterImportedPackageJsonException>(() =>
                    CharacterImportedPackageJson.LoadGeometryBinding(json));
            StringAssert.Contains(expectedCode, exception.Message);
        }

        private static string ProjectPath(string path)
        {
            return Path.GetFullPath(path);
        }

        private static CharacterTestCharacterEntity CreateTestEntity(
            CharacterTestCharacterTransformComponent transform)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(1),
                new CharacterTestCharacterIdentityComponent(1, "Test", default, default),
                new CharacterTestCharacterTeamComponent(1, 1),
                transform,
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(1, 1));
        }

        private static void AssertFix(float expected, Fix64 actual)
        {
            Assert.AreEqual(expected, actual.RawValue / (float)Fix64.Scale, 0.0001f);
        }

        private static CharacterRuntimeBinding ResolveCodexFlowTestBinding(
            CharacterImportedPackage package,
            string teamOverride = "")
        {
            var request = string.IsNullOrEmpty(teamOverride)
                ? new CharacterSpawnRequest(new SpawnProfileId(820001))
                : new CharacterSpawnRequest(new SpawnProfileId(820001), teamOverride: teamOverride);
            CharacterRuntimeSpawnResult result = CharacterRuntimeSpawnResolver.Resolve(package, request);
            Assert.IsTrue(result.IsSuccess, string.Join(", ", result.Issues.Select(issue => issue.Code + ":" + issue.Message)));
            return result.Binding;
        }

        private static GameplayComponentWorld CreateGameplayWorld()
        {
            var world = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            return world;
        }

        private static int DeterministicTeamId(string teamId)
        {
            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261);
                const int fnvPrime = 16777619;
                int hash = fnvOffset;
                string normalized = teamId.Trim().ToLowerInvariant();
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= fnvPrime;
                }

                return (hash & 0x7fffffff) == 0 ? 1 : hash & 0x7fffffff;
            }
        }

        private static string CreateTempImportedPackage(string name)
        {
            string target = Path.Combine(ProjectPath("Temp/MxFrameworkTests/CharacterRuntimeSpawn"), name);
            if (Directory.Exists(target))
                Directory.Delete(target, true);

            CopyDirectory(ProjectPath(CodexFlowTestRoot), target);
            return target;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(directory.Replace(source, target));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(source, target), true);
        }

        private static CharacterImportedPackage CreateAmbiguousTracePackage(CharacterImportedPackage package)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments = package.Geometry.WeaponAttachments
                .Select(attachment => string.Equals(attachment.EquipSlot, "mainHand")
                    ? new CharacterWeaponAttachmentRuntimeBinding(
                        attachment.WeaponId,
                        attachment.EquipSlot,
                        attachment.AttachSocketId,
                        attachment.LocalGripPose,
                        attachment.PreviewResourceKey,
                        0,
                        attachment.RuntimeResourceKey,
                        attachment.ResourcePackageId,
                        attachment.ResourceSourceProviderId,
                        attachment.TraceStartSocketId,
                        attachment.TraceEndSocketId,
                        attachment.TraceRadius,
                        attachment.TraceSampleRule)
                    : attachment)
                .ToArray();
            CharacterWeaponTraceRuntimeBinding source = package.Geometry.WeaponTraces.Single(trace => trace.TraceId == MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986);
            CharacterWeaponTraceRuntimeBinding duplicate = new CharacterWeaponTraceRuntimeBinding(
                999998,
                source.WeaponId,
                source.EquipSlot,
                source.StartLocatorPath,
                source.EndLocatorPath,
                source.Radius,
                source.SampleRule,
                source.FixedSampleCount,
                source.ActionKeys,
                source.StartPose,
                source.EndPose,
                source.ProximalExtension,
                source.ProximalRadius);
            CharacterWeaponTraceRuntimeBinding[] traces = package.Geometry.WeaponTraces.Concat(new[] { duplicate }).ToArray();
            var geometry = new CharacterImportedGeometryBinding(
                package.Geometry.PackageId,
                package.Geometry.BodyProfile,
                package.Geometry.BodyColliders,
                package.Geometry.HitZoneBindings,
                package.Geometry.Sockets,
                attachments,
                traces);
            return new CharacterImportedPackage(
                package.RootPath,
                package.PackageId,
                package.Configs,
                geometry,
                package.ResourceMapping,
                package.UnityResourceCatalog,
                package.ImportReport,
                package.RuntimeResourceCatalog,
                package.RuntimeResourcePlan,
                package.AudioCueManifest,
                package.CompiledAnimationArtifacts);
        }

        private static float ExpectedTraceLength(CharacterWeaponTraceRuntimeBinding trace)
        {
            float x = trace.EndPose.Position.X - trace.StartPose.Position.X;
            float y = trace.EndPose.Position.Y - trace.StartPose.Position.Y;
            float z = trace.EndPose.Position.Z - trace.StartPose.Position.Z;
            return (float)System.Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private static CharacterRuntimeSpawnResult ResolveDefault(string packageRoot)
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(packageRoot));
            SpawnProfileConfig spawnProfile = package.Configs.GetDefaultSpawnProfile();
            Assert.IsNotNull(spawnProfile, "Formal package must contain a default spawn profile: " + packageRoot);
            return CharacterRuntimeSpawnResolver.Resolve(package, new CharacterSpawnRequest(spawnProfile.SpawnProfileId));
        }

        private static string FormatIssues(CharacterRuntimeSpawnResult result)
        {
            return string.Join(", ", result.Issues.Select(issue => issue.Code + ":" + issue.Message));
        }

        private static CharacterImportedPackage WithAnimationDefinition(
            CharacterImportedPackage package,
            CharacterAnimationSetDefinition definition)
        {
            CharacterCompiledAnimationArtifacts source = package.CompiledAnimationArtifacts;
            var artifacts = new CharacterCompiledAnimationArtifacts(
                definition,
                source.AnimationClipRegistry,
                source.AnimationResourcePlan,
                source.Diagnostics,
                source.RuntimeMappingProvider,
                source.RuntimeClipRegistry);
            return new CharacterImportedPackage(
                package.RootPath,
                package.PackageId,
                package.Configs,
                package.Geometry,
                package.ResourceMapping,
                package.UnityResourceCatalog,
                package.ImportReport,
                package.RuntimeResourceCatalog,
                package.RuntimeResourcePlan,
                package.AudioCueManifest,
                artifacts);
        }

        private static void WriteCompiledAnimationArtifacts(string configRoot)
        {
            File.WriteAllText(Path.Combine(configRoot, "animation_set_definition.json"), @"{
  ""format"": ""mx.animationSetDefinition.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.codex_flow_test"",
  ""stableId"": ""charpkg.codex_flow_test.animation"",
  ""displayName"": ""Codex Flow Test Animation"",
  ""skeletonProfileId"": ""skeleton.humanoid"",
  ""avatarProfileId"": ""avatar.humanoid"",
  ""sets"": [
    {
      ""setId"": ""set.combat"",
      ""displayName"": ""Combat"",
      ""version"": ""1.0"",
      ""defaultClipId"": ""clip.slash"",
      ""fallbackClipId"": ""clip.slash"",
      ""groups"": [
        {
          ""groupId"": ""group.attacks"",
          ""displayName"": ""Attacks"",
          ""usage"": ""combat"",
          ""clips"": [
            {
              ""clipId"": ""clip.slash"",
              ""displayName"": ""Slash"",
              ""runtimeResourceKey"": ""char.codex_flow_test.anim.combat"",
              ""sourceClipName"": ""slash"",
              ""sourceSubClipId"": ""slash_01"",
              ""loop"": false,
              ""speed"": 1.0,
              ""rootMotionPolicy"": ""Ignore""
            }
          ]
        }
      ],
      ""actionBindings"": [
        {
          ""bindingId"": ""binding.light_attack"",
          ""actionId"": ""light_attack"",
          ""groupId"": ""group.attacks"",
          ""clipId"": ""clip.slash"",
          ""blendId"": """",
          ""timelineId"": """",
          ""required"": true
        }
      ]
    }
  ],
  ""profiles"": [
    {
      ""profileId"": ""anim.codex_flow_test.sword_shield"",
      ""displayName"": ""Sword Shield"",
      ""defaultSetId"": ""set.combat"",
      ""defaultGroupId"": ""group.attacks""
    }
  ]
}");

            File.WriteAllText(Path.Combine(configRoot, "animation_clip_registry.json"), @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.codex_flow_test"",
  ""clips"": [
    {
      ""setId"": ""set.combat"",
      ""groupId"": ""group.attacks"",
      ""clipId"": ""clip.slash"",
      ""displayName"": ""Slash"",
      ""sourceClipName"": ""slash"",
      ""sourceSubClipId"": ""slash_01"",
      ""runtimeResourceKey"": ""char.codex_flow_test.anim.combat""
    }
  ]
}");

            File.WriteAllText(Path.Combine(configRoot, "animation_resource_plan.json"), @"{
  ""format"": ""mx.animationResourcePlan.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.codex_flow_test"",
  ""stableId"": ""charpkg.codex_flow_test.animation"",
  ""planHash"": ""sha256:animation-plan"",
  ""runtimeResourceCatalog"": {
    ""catalogId"": ""animation.package.animation.codex_flow_test.runtime"",
    ""packageId"": ""animation.codex_flow_test"",
    ""entries"": []
  },
  ""characterResourcePlan"": {
    ""packageId"": ""animation.codex_flow_test"",
    ""characterStableId"": ""charpkg.codex_flow_test.animation"",
    ""planHash"": ""sha256:animation-plan""
  },
  ""audioCueManifest"": {
    ""packageId"": ""animation.codex_flow_test"",
    ""characterStableId"": ""charpkg.codex_flow_test.animation"",
    ""banks"": [],
    ""cues"": []
  },
  ""diagnostics"": []
}");
        }
    }
}
