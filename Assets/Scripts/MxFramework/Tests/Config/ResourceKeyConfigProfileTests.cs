using System.Collections.Generic;
using System.IO;
using MxFramework.Demo;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Config
{
    public class ResourceKeyConfigProfileTests
    {
        private const string SampleProfilePath = "Assets/Config/MxFramework/ResourceProfiles/mxframework_demo_resource_profile.json";
        private const string SampleSchemaPath = "Assets/Config/MxFramework/ResourceProfiles/mxframework_demo_resource_profile.schema.json";

        [Test]
        public void CreateSample_ReferencesExistingCatalogKeysWithExpectedTypesPackageAndVariant()
        {
            ResourceKeyConfigProfile profile = ResourceKeyConfigProfile.CreateSample();
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, catalog);

            Assert.IsFalse(report.HasErrors, CreateReportText(report));
        }

        [Test]
        public void CreateSampleSchema_DeclaresResourceKeyFields()
        {
            var schema = ResourceKeyConfigProfile.CreateSchema();

            Assert.AreEqual("ResourceKeyConfigProfile", schema.TableName);
            Assert.AreEqual(12, schema.Fields.Count);
            Assert.AreEqual(typeof(ResourceKey), schema.Fields[2].ValueType);
            Assert.AreEqual(typeof(ResourceKey[]), schema.Fields[9].ValueType);
            Assert.AreEqual(typeof(ResourceKey[]), schema.Fields[11].ValueType);
        }

        [Test]
        public void SampleProfileFiles_UseOnlyResourceKeyEquivalentValues()
        {
            AssertSampleFilePolicy(File.ReadAllText(SampleProfilePath));
            AssertSampleFilePolicy(File.ReadAllText(SampleSchemaPath));
        }

        [Test]
        public void Validate_WhenCatalogKeyMissing_ReportsSourceFieldExpectedTypeKeyPackageVariantAndCatalogContext()
        {
            var profile = new ResourceKeyConfigProfile(
                660001,
                "test.source",
                SampleKey("ui.start_screen.missing", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                SampleStatusAuraPrefabs(),
                SampleKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject),
                SampleMagicEffectAudioClips());

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, TempImportedResourceCatalog.CreateCatalog());

            AssertIssue(report, "ResourceKeyMissing");
            StringAssert.Contains("source=test.source", report.Issues[0].Message);
            StringAssert.Contains("field=StartScreenButtonNormalTexture", report.Issues[0].Message);
            StringAssert.Contains("expectedType=Texture2D", report.Issues[0].Message);
            StringAssert.Contains("actualKey=ui.start_screen.missing:Texture2D@mxframework.samples", report.Issues[0].Message);
            StringAssert.Contains("package=mxframework.samples", report.Issues[0].Message);
            StringAssert.Contains("variant=", report.Issues[0].Message);
            StringAssert.Contains("catalogContext=catalog=mxframework.samples", report.Issues[0].Message);
        }

        [Test]
        public void Validate_WhenConfigKeyTypeDoesNotMatchExpectedType_ReportsTypeError()
        {
            var profile = new ResourceKeyConfigProfile(
                660001,
                "test.source",
                SampleKey("ui.start_screen.button.normal", ResourceTypeIds.GameObject),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                SampleStatusAuraPrefabs(),
                SampleKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject),
                SampleMagicEffectAudioClips());

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, TempImportedResourceCatalog.CreateCatalog());

            AssertIssue(report, "ExpectedTypeMismatch");
        }

        [Test]
        public void Validate_WhenCatalogEntryTypeDoesNotMatchExpectedType_ReportsCatalogTypeError()
        {
            var profile = new ResourceKeyConfigProfile(
                660001,
                "test.source",
                SampleKey("ui.start_screen.button.normal", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                SampleStatusAuraPrefabs(),
                SampleKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject),
                new[] { SampleKey("audio.magic_effect.wind", ResourceTypeIds.AudioClip) });
            var catalog = new ResourceCatalog(
                TempImportedResourceCatalog.CatalogId,
                TempImportedResourceCatalog.PackageId,
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.start_screen.button.normal",
                        ResourceTypeIds.GameObject,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "mxframework.samples/ui/start_screen/button/normal",
                        packageId: TempImportedResourceCatalog.PackageId),
                    new ResourceCatalogEntry(
                        "vfx.status_aura.burn",
                        ResourceTypeIds.GameObject,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "mxframework.samples/vfx/status_aura/burn",
                        packageId: TempImportedResourceCatalog.PackageId),
                    new ResourceCatalogEntry(
                        "art.weapon.katana.generic_01",
                        ResourceTypeIds.GameObject,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "mxframework.samples/art/weapon/katana/generic_01",
                        packageId: TempImportedResourceCatalog.PackageId),
                    new ResourceCatalogEntry(
                        "audio.magic_effect.wind",
                        ResourceTypeIds.AudioClip,
                        TempImportedResourceCatalog.MemoryProviderId,
                        "mxframework.samples/audio/magic_effect/wind",
                        packageId: TempImportedResourceCatalog.PackageId)
                });

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, catalog);

            AssertIssue(report, "CatalogTypeMismatch");
        }

        [Test]
        public void Validate_WhenPackageOrVariantDoNotMatchSamplePolicy_ReportsPolicyErrors()
        {
            var profile = new ResourceKeyConfigProfile(
                660001,
                "test.source",
                new ResourceKey("ui.start_screen.button.normal", ResourceTypeIds.Texture2D, "pc", "other.package"),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                SampleStatusAuraPrefabs(),
                SampleKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject),
                SampleMagicEffectAudioClips());

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, TempImportedResourceCatalog.CreateCatalog());

            AssertIssue(report, "PackageMismatch");
            AssertIssue(report, "VariantMismatch");
        }

        [Test]
        public void Validate_ForbidsPathsGuidBundleFmodEventBankAndUnityObjectReferences()
        {
            var profile = new ResourceKeyConfigProfile(
                660001,
                "test.source",
                SampleKey("Assets/UI/MxFramework/button_normal.png", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                new[]
                {
                    SampleKey("_TempImportedResources/vfx/status_aura/burn", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.lightning", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.smoke", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.stun", ResourceTypeIds.GameObject)
                },
                SampleKey("1ac258bebac56774ab51d86547617ba5", ResourceTypeIds.GameObject),
                new[]
                {
                    SampleKey("mxframework.samples.audio.bundle", ResourceTypeIds.AudioClip),
                    SampleKey("event:/MxFramework/Demo/OneShot", ResourceTypeIds.AudioClip),
                    SampleKey("bank:/Master", ResourceTypeIds.AudioClip),
                    SampleKey("audio.magic_effect.wind", "UnityEngine.Object")
                });

            ResourceKeyConfigProfileValidationReport report =
                ResourceKeyConfigProfileValidator.Validate(profile, TempImportedResourceCatalog.CreateCatalog());

            AssertIssue(report, "DirectAssetPathForbidden");
            AssertIssue(report, "TempImportedResourcePathForbidden");
            AssertIssue(report, "GuidReferenceForbidden");
            AssertIssue(report, "BundleFileNameForbidden");
            AssertIssue(report, "FmodEventReferenceForbidden");
            AssertIssue(report, "FmodBankReferenceForbidden");
            AssertIssue(report, "UnityObjectReferenceForbidden");
        }

        private static ResourceKey SampleKey(string id, string typeId)
        {
            return new ResourceKey(id, typeId, string.Empty, TempImportedResourceCatalog.PackageId);
        }

        private static ResourceKey[] SampleStatusAuraPrefabs()
        {
            return new[]
            {
                SampleKey("vfx.status_aura.burn", ResourceTypeIds.GameObject),
                SampleKey("vfx.status_aura.lightning", ResourceTypeIds.GameObject),
                SampleKey("vfx.status_aura.smoke", ResourceTypeIds.GameObject),
                SampleKey("vfx.status_aura.stun", ResourceTypeIds.GameObject)
            };
        }

        private static ResourceKey[] SampleMagicEffectAudioClips()
        {
            return new[]
            {
                SampleKey("audio.magic_effect.explosion_fire_1", ResourceTypeIds.AudioClip),
                SampleKey("audio.magic_effect.explosion_lightning_3", ResourceTypeIds.AudioClip),
                SampleKey("audio.magic_effect.loop_fire_2", ResourceTypeIds.AudioClip),
                SampleKey("audio.magic_effect.wind", ResourceTypeIds.AudioClip)
            };
        }

        private static void AssertSampleFilePolicy(string text)
        {
            Assert.IsFalse(text.Contains("Assets/"), text);
            Assert.IsFalse(text.Contains("_TempImportedResources"), text);
            Assert.IsFalse(text.Contains(".bundle"), text);
            Assert.IsFalse(text.Contains(".bank"), text);
            Assert.IsFalse(text.Contains("event:/"), text);
            Assert.IsFalse(text.Contains("bank:/"), text);
            Assert.IsFalse(text.Contains("UnityEngine.Object"), text);
            Assert.IsFalse(ContainsGuidLikeToken(text), text);
        }

        private static void AssertIssue(ResourceKeyConfigProfileValidationReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                    return;
            }

            Assert.Fail("Expected ResourceKey config validation issue: " + code + "\n" + CreateReportText(report));
        }

        private static string CreateReportText(ResourceKeyConfigProfileValidationReport report)
        {
            var lines = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
                lines.Add(report.Issues[i].Code + ": " + report.Issues[i].Message);

            return string.Join("\n", lines.ToArray());
        }

        private static bool ContainsGuidLikeToken(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!IsHex(value[i]))
                    continue;

                int count = 0;
                int j = i;
                while (j < value.Length && IsHex(value[j]))
                {
                    count++;
                    j++;
                }

                if (count == 32)
                    return true;

                i = j;
            }

            return false;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
        }
    }
}
