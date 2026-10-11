using System;
using System.Collections.Generic;

namespace MxFramework.Resources
{
    public static class ResourceCatalogValidator
    {
        public static ResourceCatalogValidationReport Validate(
            ResourceCatalog catalog,
            IEnumerable<string> registeredProviderIds = null)
        {
            var report = new ResourceCatalogValidationReport();
            if (catalog == null)
            {
                report.AddError("CatalogMissing", default, "Resource catalog is null.");
                return report;
            }

            if (catalog.SchemaVersion != 1)
                report.AddError("SchemaUnsupported", default, "Unsupported resource catalog schema version: " + catalog.SchemaVersion + ".");

            HashSet<string> providers = CreateProviderSet(registeredProviderIds);
            var localKeys = new HashSet<ResourceKey>();
            var globalKeys = new HashSet<ResourceKey>();
            var keysByPackage = new HashSet<ResourceKey>();

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                {
                    report.AddError("EntryMissing", default, "Resource catalog contains a null entry.");
                    continue;
                }

                ResourceKey packageKey = entry.CreateKey(catalog.PackageId);
                ResourceKey globalKey = packageKey.WithoutPackage();
                if (!packageKey.IsValid)
                    report.AddError("InvalidKey", packageKey, "Resource key is invalid.");

                if (!localKeys.Add(globalKey))
                    report.AddError("DuplicateLocalKey", globalKey, "Duplicate id + type + variant in catalog.");

                if (!keysByPackage.Add(packageKey))
                    report.AddError("DuplicatePackageKey", packageKey, "Duplicate package-qualified resource key in catalog.");

                globalKeys.Add(globalKey);
                if (providers != null && !providers.Contains(entry.ProviderId))
                    report.AddError("ProviderMissing", packageKey, "Resource provider is not registered: " + entry.ProviderId + ".");

                if (!IsSafeRelativeAddress(entry.Address))
                    report.AddError("UnsafeAddress", packageKey, "Resource address is not a safe relative path: " + entry.Address + ".");
            }

            ValidateDependencies(catalog, keysByPackage, globalKeys, report);
            return report;
        }

        public static bool IsSafeRelativeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;
            if (address.StartsWith("/", StringComparison.Ordinal) || address.StartsWith("\\", StringComparison.Ordinal))
                return false;
            if (address.IndexOf("..", StringComparison.Ordinal) >= 0)
                return false;
            if (address.IndexOf(':') >= 0)
                return false;

            return true;
        }

        private static HashSet<string> CreateProviderSet(IEnumerable<string> registeredProviderIds)
        {
            if (registeredProviderIds == null)
                return null;

            var providers = new HashSet<string>(StringComparer.Ordinal);
            foreach (string providerId in registeredProviderIds)
            {
                if (!string.IsNullOrWhiteSpace(providerId))
                    providers.Add(providerId);
            }

            return providers;
        }

        private static void ValidateDependencies(
            ResourceCatalog catalog,
            HashSet<ResourceKey> keysByPackage,
            HashSet<ResourceKey> globalKeys,
            ResourceCatalogValidationReport report)
        {
            var dependencyGraph = new Dictionary<ResourceKey, List<ResourceKey>>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                ResourceKey owner = entry.CreateKey(catalog.PackageId);
                if (!owner.IsValid)
                    continue;

                var dependencies = new List<ResourceKey>();
                for (int j = 0; j < entry.Dependencies.Count; j++)
                {
                    ResourceKey dependency = entry.Dependencies[j];
                    if (!dependency.IsValid)
                    {
                        report.AddError("InvalidDependencyKey", owner, "Dependency key is invalid: " + dependency + ".");
                        continue;
                    }

                    if (!ContainsDependency(dependency, keysByPackage, globalKeys))
                    {
                        report.AddError("DependencyMissing", owner, "Dependency is not present in catalog: " + dependency + ".");
                        continue;
                    }

                    dependencies.Add(ResolveDependencyKey(dependency, catalog.PackageId));
                }

                dependencyGraph[owner] = dependencies;
            }

            var visiting = new HashSet<ResourceKey>();
            var visited = new HashSet<ResourceKey>();
            foreach (ResourceKey key in dependencyGraph.Keys)
            {
                if (ContainsCycle(key, dependencyGraph, visiting, visited))
                    report.AddError("DependencyCycle", key, "Resource dependency graph contains a cycle.");
            }
        }

        private static bool ContainsDependency(
            ResourceKey dependency,
            HashSet<ResourceKey> keysByPackage,
            HashSet<ResourceKey> globalKeys)
        {
            if (!string.IsNullOrWhiteSpace(dependency.PackageId))
                return keysByPackage.Contains(dependency);

            return globalKeys.Contains(dependency.WithoutPackage());
        }

        private static ResourceKey ResolveDependencyKey(ResourceKey dependency, string fallbackPackageId)
        {
            if (!string.IsNullOrWhiteSpace(dependency.PackageId))
                return dependency;

            return new ResourceKey(dependency.Id, dependency.TypeId, dependency.Variant, fallbackPackageId);
        }

        private static bool ContainsCycle(
            ResourceKey key,
            Dictionary<ResourceKey, List<ResourceKey>> dependencyGraph,
            HashSet<ResourceKey> visiting,
            HashSet<ResourceKey> visited)
        {
            if (visited.Contains(key))
                return false;
            if (!visiting.Add(key))
                return true;

            if (dependencyGraph.TryGetValue(key, out List<ResourceKey> dependencies))
            {
                for (int i = 0; i < dependencies.Count; i++)
                {
                    if (ContainsCycle(dependencies[i], dependencyGraph, visiting, visited))
                    {
                        visiting.Remove(key);
                        visited.Add(key);
                        return true;
                    }
                }
            }

            visiting.Remove(key);
            visited.Add(key);
            return false;
        }
    }
}
