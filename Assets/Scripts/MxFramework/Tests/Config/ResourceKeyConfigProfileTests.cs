using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
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
        public void SampleProfileFile_ReferencesExistingCatalogKeysWithExpectedTypesPackageAndVariant()
        {
            ResourceKeyConfigProfile profile = LoadSampleProfile();
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
                SampleKey("ui.start_screen.button.normal", ResourceTypeIds.GameObject),
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

        private static ResourceKeyConfigProfile LoadSampleProfile()
        {
            string json = File.ReadAllText(SampleProfilePath);
            ResourceProfileDto dto = DeserializeProfile(json);
            Assert.NotNull(dto);

            return new ResourceKeyConfigProfile(
                dto.ProfileId,
                dto.Source,
                dto.StartScreen.ButtonNormalTexture.ToKey(),
                dto.StartScreen.ButtonHoverTexture.ToKey(),
                dto.StartScreen.SeparatorTexture.ToKey(),
                dto.StartScreen.Icons.Archive.ToKey(),
                dto.StartScreen.Icons.Continue.ToKey(),
                dto.StartScreen.Icons.Exit.ToKey(),
                dto.StartScreen.Icons.Settings.ToKey(),
                ConvertKeys(dto.StatusAura.Prefabs),
                dto.Weapon.Prefab.ToKey(),
                ConvertKeys(dto.MagicEffects.AudioClips));
        }

        private static ResourceProfileDto DeserializeProfile(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(ResourceProfileDto));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (ResourceProfileDto)serializer.ReadObject(stream);
            }
        }

        private static ResourceKey[] ConvertKeys(ResourceKeyDto[] keys)
        {
            if (keys == null)
                return new ResourceKey[0];

            var converted = new ResourceKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                converted[i] = keys[i].ToKey();

            return converted;
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
            Assert.IsFalse(Contains(text, "Assets/"), text);
            Assert.IsFalse(Contains(text, "Assets\\"), text);
            Assert.IsFalse(Contains(text, "_TempImportedResources"), text);
            Assert.IsFalse(Contains(text, ".bundle"), text);
            Assert.IsFalse(Contains(text, ".bank"), text);
            Assert.IsFalse(Contains(text, "event:/"), text);
            Assert.IsFalse(Contains(text, "bank:/"), text);
            Assert.IsFalse(Contains(text, "UnityEngine.Object"), text);
            Assert.IsFalse(ContainsGuidLikeToken(text), text);
        }

        private static bool Contains(string text, string value)
        {
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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

                if (count == 8 && ContainsHyphenatedGuidToken(value, i))
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

        private static bool ContainsHyphenatedGuidToken(string value, int start)
        {
            int[] groups = { 8, 4, 4, 4, 12 };
            int index = start;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                for (int i = 0; i < groups[groupIndex]; i++)
                {
                    if (index >= value.Length || !IsHex(value[index]))
                        return false;

                    index++;
                }

                if (groupIndex == groups.Length - 1)
                    return true;

                if (index >= value.Length || value[index] != '-')
                    return false;

                index++;
            }

            return false;
        }

        [DataContract]
        [Serializable]
        private sealed class ResourceProfileDto
        {
            [DataMember(Name = "source")]
            public string Source { get; set; }

            [DataMember(Name = "profileId")]
            public int ProfileId { get; set; }

            [DataMember(Name = "startScreen")]
            public StartScreenDto StartScreen { get; set; }

            [DataMember(Name = "statusAura")]
            public StatusAuraDto StatusAura { get; set; }

            [DataMember(Name = "weapon")]
            public WeaponDto Weapon { get; set; }

            [DataMember(Name = "magicEffects")]
            public MagicEffectsDto MagicEffects { get; set; }
        }

        [DataContract]
        [Serializable]
        private sealed class StartScreenDto
        {
            [DataMember(Name = "buttonNormalTexture")]
            public ResourceKeyDto ButtonNormalTexture { get; set; }

            [DataMember(Name = "buttonHoverTexture")]
            public ResourceKeyDto ButtonHoverTexture { get; set; }

            [DataMember(Name = "separatorTexture")]
            public ResourceKeyDto SeparatorTexture { get; set; }

            [DataMember(Name = "icons")]
            public StartScreenIconsDto Icons { get; set; }
        }

        [DataContract]
        [Serializable]
        private sealed class StartScreenIconsDto
        {
            [DataMember(Name = "archive")]
            public ResourceKeyDto Archive { get; set; }

            [DataMember(Name = "continue")]
            public ResourceKeyDto Continue { get; set; }

            [DataMember(Name = "exit")]
            public ResourceKeyDto Exit { get; set; }

            [DataMember(Name = "settings")]
            public ResourceKeyDto Settings { get; set; }
        }

        [DataContract]
        [Serializable]
        private sealed class StatusAuraDto
        {
            [DataMember(Name = "prefabs")]
            public ResourceKeyDto[] Prefabs { get; set; }
        }

        [DataContract]
        [Serializable]
        private sealed class WeaponDto
        {
            [DataMember(Name = "prefab")]
            public ResourceKeyDto Prefab { get; set; }
        }

        [DataContract]
        [Serializable]
        private sealed class MagicEffectsDto
        {
            [DataMember(Name = "audioClips")]
            public ResourceKeyDto[] AudioClips { get; set; }
        }

        [DataContract]
        [Serializable]
        private struct ResourceKeyDto
        {
            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "variant")]
            public string Variant { get; set; }

            [DataMember(Name = "packageId")]
            public string PackageId { get; set; }

            public ResourceKey ToKey()
            {
                return new ResourceKey(Id, Type, Variant, PackageId);
            }
        }
    }
}
