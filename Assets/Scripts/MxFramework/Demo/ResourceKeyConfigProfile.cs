using System;
using System.Collections.Generic;
using MxFramework.Config;
using MxFramework.Resources;

namespace MxFramework.Demo
{
    public sealed class ResourceKeyConfigProfile : IConfigData
    {
        private readonly List<ResourceKey> _statusAuraPrefabs;
        private readonly List<ResourceKey> _magicEffectAudioClips;

        public ResourceKeyConfigProfile(
            int id,
            string source,
            ResourceKey startScreenButtonNormalTexture,
            ResourceKey startScreenButtonHoverTexture,
            ResourceKey startScreenSeparatorTexture,
            ResourceKey startScreenArchiveIconTexture,
            ResourceKey startScreenContinueIconTexture,
            ResourceKey startScreenExitIconTexture,
            ResourceKey startScreenSettingsIconTexture,
            IEnumerable<ResourceKey> statusAuraPrefabs,
            ResourceKey weaponPrefab,
            IEnumerable<ResourceKey> magicEffectAudioClips)
        {
            Id = id;
            Source = source ?? string.Empty;
            StartScreenButtonNormalTexture = startScreenButtonNormalTexture;
            StartScreenButtonHoverTexture = startScreenButtonHoverTexture;
            StartScreenSeparatorTexture = startScreenSeparatorTexture;
            StartScreenArchiveIconTexture = startScreenArchiveIconTexture;
            StartScreenContinueIconTexture = startScreenContinueIconTexture;
            StartScreenExitIconTexture = startScreenExitIconTexture;
            StartScreenSettingsIconTexture = startScreenSettingsIconTexture;
            _statusAuraPrefabs = statusAuraPrefabs != null
                ? new List<ResourceKey>(statusAuraPrefabs)
                : new List<ResourceKey>();
            WeaponPrefab = weaponPrefab;
            _magicEffectAudioClips = magicEffectAudioClips != null
                ? new List<ResourceKey>(magicEffectAudioClips)
                : new List<ResourceKey>();
        }

        public int Id { get; }
        public string Source { get; }
        public ResourceKey StartScreenButtonNormalTexture { get; }
        public ResourceKey StartScreenButtonHoverTexture { get; }
        public ResourceKey StartScreenSeparatorTexture { get; }
        public ResourceKey StartScreenArchiveIconTexture { get; }
        public ResourceKey StartScreenContinueIconTexture { get; }
        public ResourceKey StartScreenExitIconTexture { get; }
        public ResourceKey StartScreenSettingsIconTexture { get; }
        public IReadOnlyList<ResourceKey> StatusAuraPrefabs => _statusAuraPrefabs;
        public ResourceKey WeaponPrefab { get; }
        public IReadOnlyList<ResourceKey> MagicEffectAudioClips => _magicEffectAudioClips;

        public static ResourceKeyConfigProfile CreateSample()
        {
            return new ResourceKeyConfigProfile(
                660001,
                "mxframework.demo.resource_profile",
                SampleKey("ui.start_screen.button.normal", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.button.hover", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.separator.diamond_line", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.archive_book", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.continue_hourglass", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.exit_door", ResourceTypeIds.Texture2D),
                SampleKey("ui.start_screen.icon.settings_cog", ResourceTypeIds.Texture2D),
                new[]
                {
                    SampleKey("vfx.status_aura.burn", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.lightning", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.smoke", ResourceTypeIds.GameObject),
                    SampleKey("vfx.status_aura.stun", ResourceTypeIds.GameObject)
                },
                SampleKey("art.weapon.katana.generic_01", ResourceTypeIds.GameObject),
                new[]
                {
                    SampleKey("audio.magic_effect.explosion_fire_1", ResourceTypeIds.AudioClip),
                    SampleKey("audio.magic_effect.explosion_lightning_3", ResourceTypeIds.AudioClip),
                    SampleKey("audio.magic_effect.loop_fire_2", ResourceTypeIds.AudioClip),
                    SampleKey("audio.magic_effect.wind", ResourceTypeIds.AudioClip)
                });
        }

        public static ConfigSchema<ResourceKeyConfigProfile> CreateSchema()
        {
            var schema = new ConfigSchema<ResourceKeyConfigProfile>(
                    "ResourceKeyConfigProfile",
                    displayName: "Resource Key Config Profile",
                    description: "Demo config source that stores only ResourceKey references.",
                    idRange: new ConfigIdRange(660000, 660999),
                    structureKind: ConfigStructureKind.Graph);

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, required: true))
                .AddField(new ConfigField("Source", ConfigFieldType.String, required: true))
                .AddField(new ConfigField("StartScreenButtonNormalTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenButtonHoverTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenSeparatorTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenArchiveIconTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenContinueIconTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenExitIconTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StartScreenSettingsIconTexture", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("StatusAuraPrefabs", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey[])))
                .AddField(new ConfigField("WeaponPrefab", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey)))
                .AddField(new ConfigField("MagicEffectAudioClips", ConfigFieldType.Custom, required: true, valueType: typeof(ResourceKey[])));

            return schema;
        }

        private static ResourceKey SampleKey(string id, string typeId)
        {
            return new ResourceKey(id, typeId, string.Empty, TempImportedResourceCatalog.PackageId);
        }
    }
}
