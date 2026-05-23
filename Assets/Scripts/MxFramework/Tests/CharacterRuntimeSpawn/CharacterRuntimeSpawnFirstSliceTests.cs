using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public class CharacterRuntimeSpawnFirstSliceTests
    {
        private const string IronVanguardRoot = "Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard";

        [Test]
        public void ImportedIronVanguard_CreatesRuntimeBindingPlan()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));

            CharacterRuntimeSpawnResult result = CharacterRuntimeSpawnResolver.Resolve(
                package,
                new CharacterSpawnRequest(new SpawnProfileId(820001)));

            Assert.IsTrue(result.IsSuccess, string.Join(", ", result.Issues.Select(issue => issue.Code + ":" + issue.Message)));
            Assert.AreEqual(new CharacterConfigId(710001), result.Binding.ResolvedProfile.CharacterId);
            Assert.AreEqual(new EquipmentStateId(770003), result.Binding.ResolvedProfile.ActiveEquipmentStateId);
            Assert.AreEqual("anim.iron_vanguard.sword_shield", result.Binding.ResolvedProfile.AnimationProfileId);
            Assert.AreEqual(package.ImportReport.SourcePackageHash, result.Binding.SourcePackageHash);
            Assert.AreEqual(package.ImportReport.ResourceMappingHash, result.Binding.ResourceMappingHash);
            StringAssert.StartsWith("sha256:", result.Binding.SourcePackageHash);
            StringAssert.StartsWith("sha256:", result.Binding.ResourceMappingHash);
            Assert.AreEqual(4, result.Binding.CombatBodyBindingPlan.Colliders.Length);
            Assert.AreEqual(2, result.Binding.WeaponAttachmentBindingPlan.Attachments.Length);
            Assert.AreEqual(1, result.Binding.WeaponAttachmentBindingPlan.Traces.Length);
            Assert.GreaterOrEqual(result.Binding.ResourcePreloadPlan.ResolvedResources.Length, 5);
            Assert.IsEmpty(result.Binding.ResourcePreloadPlan.MissingResourceKeys);
            StringAssert.Contains("sourcePackageHash=", result.Binding.DebugSummary);
            StringAssert.Contains("resourceMappingHash=", result.Binding.DebugSummary);
        }

        [Test]
        public void RuntimeSpawnModule_ProcessesQueuedSpawnRequestThroughRuntimeHost()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
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
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
            CharacterRuntimeBinding binding = ResolveIronVanguardBinding(package, teamOverride: "team.blue");

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
            Assert.AreEqual("mxframework.character.char.iron_vanguard", build.Definition.StableId);
            Assert.AreEqual(1, build.Definition.SchemaVersion);
            Assert.IsEmpty(build.Diagnostics);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayIdentityComponent> identities));
            Assert.IsTrue(identities.TryGet(entity, out GameplayIdentityComponent identity));
            Assert.AreEqual(710001, identity.DefinitionId);
            Assert.AreEqual(760003, identity.VariantId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams));
            Assert.IsTrue(teams.TryGet(entity, out GameplayTeamComponent team));
            Assert.AreEqual(DeterministicTeamId("team.blue"), team.TeamId);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycle));
            Assert.IsTrue(lifecycle.TryGet(entity, out GameplayLifecycleComponent lifecycleComponent));
            Assert.AreEqual(GameplayLifecycleState.Alive, lifecycleComponent.State);

            Assert.IsTrue(world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributes));
            Assert.IsTrue(attributes.TryGet(entity, out GameplayAttributeSetComponent attributeSet));
            Assert.AreEqual(3, attributeSet.Count);
            Assert.IsTrue(attributeSet.TryGet(920001, out GameplayAttributeValue hp));
            Assert.AreEqual(160, hp.BaseValue);
            Assert.AreEqual(160, hp.CurrentValue);
            Assert.IsTrue(attributeSet.TryGet(920002, out GameplayAttributeValue stamina));
            Assert.AreEqual(100, stamina.BaseValue);
            Assert.AreEqual(100, stamina.CurrentValue);
            Assert.IsTrue(attributeSet.TryGet(920003, out GameplayAttributeValue posture));
            Assert.AreEqual(80, posture.BaseValue);
            Assert.AreEqual(80, posture.CurrentValue);
        }

        [Test]
        public void GameplayAbilityRegistryBuilder_ReportsDeferredAbilityBootstrap()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
            CharacterRuntimeBinding binding = ResolveIronVanguardBinding(package);

            CharacterGameplayAbilityRegistryBuildResult build =
                CharacterGameplayAbilityRegistryBuilder.BuildDeferred(binding);

            Assert.IsTrue(build.IsDeferred);
            Assert.IsNotNull(build.Registry);
            Assert.AreEqual(4, build.EffectiveAbilityIds.Length);
            Assert.IsTrue(build.Diagnostics.Any(line => line.Contains("deferred")));
        }

        [Test]
        public void GameplayRuntimeBootstrap_SpawnsLiveEntityAndRegistersHandle()
        {
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
            CharacterRuntimeBinding binding = ResolveIronVanguardBinding(package);

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
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
            CharacterRuntimeBinding binding = ResolveIronVanguardBinding(package);

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
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
            CharacterRuntimeBinding binding = ResolveIronVanguardBinding(package);

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
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(IronVanguardRoot));
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
            Assert.AreEqual("animation.iron_vanguard", package.CompiledAnimationArtifacts.AnimationSetDefinition.PackageId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets.Length);
            Assert.AreEqual("set.combat", package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].SetId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].Groups.Length);
            Assert.AreEqual("clip.slash", package.CompiledAnimationArtifacts.AnimationSetDefinition.Sets[0].Groups[0].Clips[0].ClipId);
            Assert.AreEqual(1, package.CompiledAnimationArtifacts.AnimationClipRegistry.Clips.Length);
            Assert.AreEqual("char.iron_vanguard.anim.combat", package.CompiledAnimationArtifacts.AnimationClipRegistry.Clips[0].RuntimeResourceKey);
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

        private static string ProjectPath(string path)
        {
            return Path.GetFullPath(path);
        }

        private static CharacterRuntimeBinding ResolveIronVanguardBinding(
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

            CopyDirectory(ProjectPath(IronVanguardRoot), target);
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

        private static void WriteCompiledAnimationArtifacts(string configRoot)
        {
            File.WriteAllText(Path.Combine(configRoot, "animation_set_definition.json"), @"{
  ""format"": ""mx.animationSetDefinition.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.iron_vanguard"",
  ""stableId"": ""charpkg.iron_vanguard.animation"",
  ""displayName"": ""Iron Vanguard Animation"",
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
              ""runtimeResourceKey"": ""char.iron_vanguard.anim.combat"",
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
      ""profileId"": ""anim.iron_vanguard.sword_shield"",
      ""displayName"": ""Sword Shield"",
      ""defaultSetId"": ""set.combat"",
      ""defaultGroupId"": ""group.attacks""
    }
  ]
}");

            File.WriteAllText(Path.Combine(configRoot, "animation_clip_registry.json"), @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.iron_vanguard"",
  ""clips"": [
    {
      ""setId"": ""set.combat"",
      ""groupId"": ""group.attacks"",
      ""clipId"": ""clip.slash"",
      ""displayName"": ""Slash"",
      ""sourceClipName"": ""slash"",
      ""sourceSubClipId"": ""slash_01"",
      ""runtimeResourceKey"": ""char.iron_vanguard.anim.combat""
    }
  ]
}");

            File.WriteAllText(Path.Combine(configRoot, "animation_resource_plan.json"), @"{
  ""format"": ""mx.animationResourcePlan.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.iron_vanguard"",
  ""stableId"": ""charpkg.iron_vanguard.animation"",
  ""planHash"": ""sha256:animation-plan"",
  ""runtimeResourceCatalog"": {
    ""catalogId"": ""animation.package.animation.iron_vanguard.runtime"",
    ""packageId"": ""animation.iron_vanguard"",
    ""entries"": []
  },
  ""characterResourcePlan"": {
    ""packageId"": ""animation.iron_vanguard"",
    ""characterStableId"": ""charpkg.iron_vanguard.animation"",
    ""planHash"": ""sha256:animation-plan""
  },
  ""audioCueManifest"": {
    ""packageId"": ""animation.iron_vanguard"",
    ""characterStableId"": ""charpkg.iron_vanguard.animation"",
    ""banks"": [],
    ""cues"": []
  },
  ""diagnostics"": []
}");
        }
    }
}
