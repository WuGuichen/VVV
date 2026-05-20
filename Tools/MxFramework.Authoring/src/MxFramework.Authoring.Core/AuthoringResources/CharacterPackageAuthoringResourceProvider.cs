using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class CharacterPackageAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.CharacterPackage; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Character Package",
                SourceKind = AuthoringResourceSourceKind.PackageResource,
                Available = true,
                Status = "Ready"
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            CharacterPackageResourceCatalog catalog = context != null ? context.PackageResourceCatalog : null;
            return FromPackageResourceCatalog(catalog, context);
        }

        public static AuthoringResourceCollection FromPackageResourceCatalog(
            CharacterPackageResourceCatalog catalog,
            AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : BuildScopeId(context)
            };
            if (context != null)
            {
                AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AddIfPresent(collection.Metadata, "projectRootPath", context.ProjectRootPath);
            }
            collection.Providers.Add(new CharacterPackageAuthoringResourceProvider().Describe(context));

            if (catalog == null)
                return collection;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                collection.Items.Add(FromPackageResourceEntry(entry, context));
            }

            AddIdentityDiagnostics(collection);
            return collection;
        }

        public static AuthoringResourceItem FromPackageResourceEntry(
            CharacterPackageResourceEntry entry,
            AuthoringResourceProviderContext context)
        {
            if (entry == null)
                return new AuthoringResourceItem();

            string stableId = !string.IsNullOrWhiteSpace(entry.StableId)
                ? entry.StableId
                : BuildStableId(entry, context);
            string providerResourceKey = entry.ResourceKey ?? string.Empty;
            string contentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);

            var item = new AuthoringResourceItem
            {
                ResourceId = BuildResourceId(AuthoringResourceProviderIds.CharacterPackage, stableId, providerResourceKey),
                StableId = stableId,
                DisplayName = !string.IsNullOrWhiteSpace(entry.LocalId) ? entry.LocalId : providerResourceKey,
                Kind = entry.TypeId ?? string.Empty,
                Usage = entry.Usage ?? string.Empty,
                SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
                SourceKind = AuthoringResourceSourceKind.PackageResource,
                BindingKind = MapBindingKind(entry),
                ImportStatus = AuthoringResourceImportStatus.New,
                RuntimeAvailability = MapRuntimeAvailability(entry),
                Tags = entry.Tags != null ? new List<string>(entry.Tags) : new List<string>()
            };
            AddItemMetadata(item, entry, contentHash);

            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                BindingKind = AuthoringResourceBindingKind.PackageResource,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageResourceKey,
                DisplayValue = providerResourceKey,
                IsPrimary = true,
                ProviderResourceKey = providerResourceKey,
                PackageResourceKey = providerResourceKey,
                AssetType = entry.TypeId ?? string.Empty,
                Hash = contentHash,
                ProviderData = BuildProviderData(entry, contentHash)
            });
            AddExternalSourcePathBinding(item, entry);
            AddDependencyBindings(item, entry);

            if (entry.Preview != null)
            {
                item.Preview.ThumbnailProviderResourceKey = entry.Preview.ThumbnailResourceKey ?? string.Empty;
                item.Preview.PreviewMeshProviderResourceKey = entry.Preview.PreviewMeshResourceKey ?? string.Empty;
                item.Preview.PreviewCameraPresetId = entry.Preview.PreviewCameraPresetId ?? string.Empty;
                item.Preview.IsPlaceholder = entry.Preview.IsPlaceholder;
            }

            return item;
        }

        private static AuthoringResourceBindingKind MapBindingKind(CharacterPackageResourceEntry entry)
        {
            if (entry != null && string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceBindingKind.GeneratedPreviewOnly;

            return AuthoringResourceBindingKind.PackageResource;
        }

        private static AuthoringResourceRuntimeAvailability MapRuntimeAvailability(CharacterPackageResourceEntry entry)
        {
            if (entry != null && string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceRuntimeAvailability.PreviewOnly;

            return AuthoringResourceRuntimeAvailability.Unknown;
        }

        private static void AddItemMetadata(AuthoringResourceItem item, CharacterPackageResourceEntry entry, string contentHash)
        {
            if (item == null || entry == null)
                return;

            AddIfPresent(item.Metadata, "packageId", entry.PackageId);
            AddIfPresent(item.Metadata, "localId", entry.LocalId);
            AddIfPresent(item.Metadata, "variant", entry.Variant);
            AddIfPresent(item.Metadata, "sourceFormat", entry.SourceFormat);
            AddIfPresent(item.Metadata, "relativePath", entry.RelativePath);
            AddIfPresent(item.Metadata, "contentHash", contentHash);
            if (entry.ImportHints != null)
            {
                AddIfPresent(item.Metadata, "importProviderHint", entry.ImportHints.ProviderId);
                AddIfPresent(item.Metadata, "targetPathPolicy", entry.ImportHints.TargetPathPolicy);
                AddIfPresent(item.Metadata, "targetRelativePath", entry.ImportHints.TargetRelativePath);
            }
        }

        private static void AddExternalSourcePathBinding(AuthoringResourceItem item, CharacterPackageResourceEntry entry)
        {
            if (item == null || entry == null || string.IsNullOrWhiteSpace(entry.RelativePath))
                return;

            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageRelativePath,
                DisplayValue = entry.RelativePath,
                ExternalSourcePath = entry.RelativePath
            });
        }

        private static void AddDependencyBindings(AuthoringResourceItem item, CharacterPackageResourceEntry entry)
        {
            if (item == null || entry == null || entry.Dependencies == null || entry.Dependencies.Count == 0)
                return;

            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                    continue;

                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                    BindingKind = AuthoringResourceBindingKind.PackageResource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.Dependency,
                    DisplayValue = dependency.ResourceKey,
                    ProviderResourceKey = dependency.ResourceKey,
                    PackageResourceKey = dependency.ResourceKey,
                    ProviderData = new Dictionary<string, string>
                    {
                        { "relation", dependency.Relation ?? string.Empty },
                        { "required", dependency.Required ? "true" : "false" },
                        { "affectsDependencyHash", dependency.AffectsDependencyHash ? "true" : "false" }
                    }
                });
            }
        }

        private static Dictionary<string, string> BuildProviderData(CharacterPackageResourceEntry entry, string contentHash)
        {
            var data = new Dictionary<string, string>();
            if (entry == null)
                return data;

            AddIfPresent(data, "packageId", entry.PackageId);
            AddIfPresent(data, "localId", entry.LocalId);
            AddIfPresent(data, "variant", entry.Variant);
            AddIfPresent(data, "sourceFormat", entry.SourceFormat);
            AddIfPresent(data, "relativePath", entry.RelativePath);
            AddIfPresent(data, "contentHash", contentHash);
            if (entry.ImportHints != null)
            {
                AddIfPresent(data, "targetPathPolicy", entry.ImportHints.TargetPathPolicy);
                AddIfPresent(data, "targetRelativePath", entry.ImportHints.TargetRelativePath);
            }

            return data;
        }

        private static void AddIdentityDiagnostics(AuthoringResourceCollection collection)
        {
            if (collection == null)
                return;

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var providerKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(item.StableId) && !stableIds.Add(item.StableId))
                {
                    collection.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.StableIdDuplicate,
                        item,
                        "resource stable id must be unique.",
                        "Give each resource item a unique stable id."));
                }

                string providerKey = GetPrimaryProviderResourceKey(item);
                if (!string.IsNullOrWhiteSpace(providerKey) && !providerKeys.Add(providerKey))
                {
                    collection.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.ResourceKeyDuplicate,
                        item,
                        "provider-local resource key must be unique inside the provider.",
                        "Use a unique provider-local key or merge duplicate source entries."));
                }
            }
        }

        private static string GetPrimaryProviderResourceKey(AuthoringResourceItem item)
        {
            if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
                return string.Empty;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding == null)
                    continue;
                if (binding.IsPrimary || string.Equals(binding.BindingKeyKind, AuthoringResourceBindingKeyKinds.PackageResourceKey, StringComparison.Ordinal))
                    return binding.ProviderResourceKey ?? string.Empty;
            }

            AuthoringResourceProviderBinding fallback = item.ProviderBindings[0];
            return fallback != null ? fallback.ProviderResourceKey ?? string.Empty : string.Empty;
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            AuthoringResourceItem item,
            string message,
            string suggestedFix)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = item != null ? item.ResourceId ?? string.Empty : string.Empty,
                ResourceStableId = item != null ? item.StableId ?? string.Empty : string.Empty,
                ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static string BuildScopeId(AuthoringResourceProviderContext context)
        {
            if (context != null && !string.IsNullOrWhiteSpace(context.PackageId))
                return "characterPackage:" + NormalizeSegment(context.PackageId);

            return "characterPackage";
        }

        private static string BuildStableId(CharacterPackageResourceEntry entry, AuthoringResourceProviderContext context)
        {
            string packageId = entry != null && !string.IsNullOrWhiteSpace(entry.PackageId)
                ? entry.PackageId
                : context != null ? context.PackageId : string.Empty;
            string local = entry != null && !string.IsNullOrWhiteSpace(entry.LocalId)
                ? entry.LocalId
                : entry != null ? entry.ResourceKey : string.Empty;
            return "resource.characterPackage." + NormalizeSegment(packageId) + "." + NormalizeSegment(local);
        }

        private static string BuildResourceId(string providerId, string stableId, string providerResourceKey)
        {
            string idSource = !string.IsNullOrWhiteSpace(stableId) ? stableId : providerResourceKey;
            return providerId + ":" + NormalizeSegment(idSource);
        }

        private static string NormalizeSegment(string value)
        {
            return CharacterPackageResourceKeyGenerator.NormalizeSegment(value ?? string.Empty);
        }

        private static void AddIfPresent(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            data[key] = value;
        }
    }
}
