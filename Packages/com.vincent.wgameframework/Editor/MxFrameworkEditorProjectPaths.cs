using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Editor
{
    public enum MxFrameworkEditorProjectPathKind
    {
        AbilityAuthoringJson,
        ProjectileAuthoringPackage,
        ProjectileSourceDirectory
    }

    public interface IMxFrameworkEditorProjectPathProvider
    {
        bool TryGetProjectRelativePath(
            MxFrameworkEditorProjectPathKind kind,
            out string projectRelativePath);
    }

    public static class MxFrameworkEditorProjectPaths
    {
        private static readonly object Sync = new object();
        private static readonly List<IMxFrameworkEditorProjectPathProvider> Providers =
            new List<IMxFrameworkEditorProjectPathProvider>();

        public static void Register(IMxFrameworkEditorProjectPathProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            lock (Sync)
            {
                if (!Providers.Contains(provider))
                    Providers.Add(provider);
            }
        }

        public static void Unregister(IMxFrameworkEditorProjectPathProvider provider)
        {
            if (provider == null)
                return;
            lock (Sync)
                Providers.Remove(provider);
        }

        public static bool TryResolve(
            MxFrameworkEditorProjectPathKind kind,
            out string projectRelativePath)
        {
            lock (Sync)
            {
                for (int i = Providers.Count - 1; i >= 0; i--)
                {
                    IMxFrameworkEditorProjectPathProvider provider = Providers[i];
                    if (provider != null
                        && provider.TryGetProjectRelativePath(kind, out string candidate)
                        && TryNormalize(candidate, out projectRelativePath))
                    {
                        return true;
                    }
                }
            }
            projectRelativePath = string.Empty;
            return false;
        }

        private static bool TryNormalize(string path, out string normalized)
        {
            normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (normalized.Length == 0
                || normalized.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(normalized))
            {
                normalized = string.Empty;
                return false;
            }

            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0
                    || string.Equals(segments[i], ".", StringComparison.Ordinal)
                    || string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    normalized = string.Empty;
                    return false;
                }
            }
            return true;
        }
    }
}
