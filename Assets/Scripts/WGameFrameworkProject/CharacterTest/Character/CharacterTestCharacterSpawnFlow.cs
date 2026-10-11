using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCharacterSpawnFlow : ICharacterTestCharacterSpawnFlow
    {
        private readonly GlobalResourceRuntimeServices _resources;
        private readonly IRuntimeLogger _logger;
        private readonly CharacterTestRuntimeCommandModule _runtimeCommands;
        private readonly CharacterTestCharacterRuntimeModule _runtimeModule;
        private readonly System.Func<RuntimeFrame> _currentFrameProvider;

        public CharacterTestCharacterSpawnFlow(
            GlobalResourceRuntimeServices resources,
            IRuntimeLogger logger,
            CharacterTestRuntimeCommandModule runtimeCommands,
            CharacterTestCharacterRuntimeModule runtimeModule,
            System.Func<RuntimeFrame> currentFrameProvider)
        {
            _resources = resources;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _runtimeCommands = runtimeCommands;
            _runtimeModule = runtimeModule;
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);

            RegisterTemporaryCharacterResources();
        }

        public bool RequestSpawn(CharacterTestCharacterSpawnSpec spec)
        {
            if (_resources == null || _resources.ResourceManager == null)
            {
                _logger.Warning("CharacterSpawn", "Resource runtime is not available.");
                return false;
            }

            if (_runtimeCommands == null)
            {
                _logger.Warning("CharacterSpawn", "Character runtime command module is not available.");
                return false;
            }

            RuntimeCommandValidationResult result = _runtimeCommands.Enqueue(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    _currentFrameProvider(),
                    CharacterTestRuntimeCommandSources.StoryFlow,
                    spec,
                    new RuntimeTraceIdentity(90875, CharacterTestRuntimeCommandSources.StoryFlow, _currentFrameProvider().Value)));
            if (!result.Success)
            {
                _logger.Warning("CharacterSpawn", "Character spawn command rejected: " + result.Error.Message);
                return false;
            }

            return true;
        }

        public void Release()
        {
            _runtimeModule?.ReleaseAll();
        }

        private void RegisterTemporaryCharacterResources()
        {
            if (_resources == null || _resources.ResourceManager == null)
                return;

            try
            {
                _resources.ResourceManager.RegisterProvider(new ResourcesProvider());
            }
            catch (ResourceCatalogException)
            {
                // Provider may already be registered by another bootstrap path.
            }

#if UNITY_EDITOR
            try
            {
                _resources.ResourceManager.RegisterProvider(new CharacterTestEditorAnimationAssetProvider());
            }
            catch (ResourceCatalogException)
            {
                // Provider may already be registered by another bootstrap path.
            }
#endif

            try
            {
                bool includePublicMainHandWeaponFallbackEntry = !HasDefaultMainHandWeaponCatalog(_resources.ResourceManager);
                bool includePublicOffHandWeaponFallbackEntry = !HasDefaultOffHandWeaponCatalog(_resources.ResourceManager);
                ResourceCatalog catalog = CharacterTestCharacterSpawnResources.CreateCatalog(
                    includeAnimationEntries: true,
                    includePublicMainHandWeaponFallbackEntry: includePublicMainHandWeaponFallbackEntry,
                    includePublicOffHandWeaponFallbackEntry: includePublicOffHandWeaponFallbackEntry);
                ResourceCatalog missingEntriesCatalog = CreateMissingEntriesCatalog(
                    _resources.ResourceManager,
                    catalog);
                if (missingEntriesCatalog.Entries.Count > 0)
                    _resources.ResourceManager.AddCatalog(missingEntriesCatalog);
            }
            catch (ResourceCatalogException ex)
            {
                _logger.Warning("CharacterSpawn", "Temporary character catalog registration skipped: " + ex.Message);
            }
        }

        internal static ResourceCatalog CreateMissingEntriesCatalog(
            IResourceManager manager,
            ResourceCatalog catalog)
        {
            var entries = new List<ResourceCatalogEntry>();
            var emittedPackageKeys = new HashSet<ResourceKey>();
            var emittedGlobalKeys = new HashSet<ResourceKey>();
            for (int i = 0; catalog != null && i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                ResourceKey key = entry.CreateKey(catalog.PackageId);
                if (!emittedPackageKeys.Add(key) || !emittedGlobalKeys.Add(key.WithoutPackage()))
                    continue;
                if (manager.Contains(key))
                    continue;
                if (!entry.AllowOverride && manager.Contains(key.WithoutPackage()))
                    continue;
                // Animation schema v2 requests the unqualified variant. Imported package
                // resources publish their explicit default variant; retain that entry's
                // real provider/address instead of installing an Editor-only substitute.
                if (entry.TypeId == ResourceTypeIds.AnimationClip && string.IsNullOrEmpty(key.Variant)
                    && manager is IResourceCatalogQuery query
                    && query.TryGetEntry(new ResourceKey(key.Id, key.TypeId, "default", key.PackageId),
                        out ResourceCatalogEntry published))
                {
                    entries.Add(new ResourceCatalogEntry(key.Id, key.TypeId, published.ProviderId,
                        published.Address, key.Variant, key.PackageId, published.Dependencies,
                        published.Labels, published.Hash, published.Size, published.AllowOverride, published.ProviderData));
                    continue;
                }
                if (!UnityEngine.Application.isEditor && entry.ProviderId == CharacterTestEditorAnimationAssetProvider.Id)
                    throw new ResourceCatalogException("Player animation is absent from the published resource catalog: " + key);
                entries.Add(entry);
            }

            return new ResourceCatalog(
                catalog?.CatalogId ?? string.Empty,
                catalog?.PackageId ?? string.Empty,
                entries);
        }

        private static bool HasDefaultMainHandWeaponCatalog(IResourceManager manager)
        {
            return manager != null
                && manager.Contains(new ResourceKey(
                    CharacterTestCharacterSpawnResources.DefaultMainHandWeaponResourceId,
                    ResourceTypeIds.GameObject,
                    packageId: "common"));
        }

        private static bool HasDefaultOffHandWeaponCatalog(IResourceManager manager)
        {
            return manager != null
                && manager.Contains(new ResourceKey(
                    CharacterTestCharacterSpawnResources.DefaultOffHandWeaponResourceId,
                    ResourceTypeIds.GameObject,
                    packageId: "common"));
        }
    }
}
