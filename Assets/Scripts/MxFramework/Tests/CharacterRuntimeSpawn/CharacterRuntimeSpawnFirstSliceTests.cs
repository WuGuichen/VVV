using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
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
            Assert.AreEqual("sha256:6887b1c5437e5ec8f788796af2e44010f9804285d5d497c660020d3c0f8e835f", result.Binding.SourcePackageHash);
            Assert.AreEqual("sha256:22ce7bb03da3d4b27a2c502d9b4e2de7493f73d94df87a35aa2eb6dc94ae3800", result.Binding.ResourceMappingHash);
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

        private static string ProjectPath(string path)
        {
            return Path.GetFullPath(path);
        }
    }
}
