using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Precompiled socket frames resolved once at a low-frequency package/config seam.</summary>
    /// <remarks>LowFreqAlloc on construction; <see cref="TryGetFrame"/> is NoAlloc and uses integer indexing.</remarks>
    internal sealed class CharacterTestBakedSocketTrack
    {
        private readonly MxAnimationBakedSocketFrame[] _framesByLocalFrame;

        private CharacterTestBakedSocketTrack(
            string clipKey,
            int socketId,
            MxAnimationBakedSocketFrame[] framesByLocalFrame)
        {
            ClipKey = clipKey ?? string.Empty;
            SocketId = socketId;
            _framesByLocalFrame = framesByLocalFrame ?? Array.Empty<MxAnimationBakedSocketFrame>();
        }

        public string ClipKey { get; }
        public int SocketId { get; }
        public bool IsValid => _framesByLocalFrame.Length > 0;
        public int FrameCount => _framesByLocalFrame.Length;

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetFrame(int localFrame, out MxAnimationBakedSocketFrame frame)
        {
            if ((uint)localFrame < (uint)_framesByLocalFrame.Length)
            {
                frame = _framesByLocalFrame[localFrame];
                return frame != null;
            }

            frame = null;
            return false;
        }

        /// <remarks>LowFreqAlloc.</remarks>
        public static CharacterTestBakedSocketTrack Create(
            string clipKey,
            int socketId,
            IReadOnlyList<MxAnimationBakedSocketFrame> sourceFrames)
        {
            int maxFrame = -1;
            for (int i = 0; sourceFrames != null && i < sourceFrames.Count; i++)
            {
                MxAnimationBakedSocketFrame candidate = sourceFrames[i];
                if (candidate != null
                    && (candidate.SocketId == socketId)
                    && candidate.LocalFrame > maxFrame)
                {
                    maxFrame = candidate.LocalFrame;
                }
            }
            if (maxFrame < 0)
                return null;

            var frames = new MxAnimationBakedSocketFrame[maxFrame + 1];
            for (int i = 0; i < sourceFrames.Count; i++)
            {
                MxAnimationBakedSocketFrame candidate = sourceFrames[i];
                if (candidate != null
                    && (candidate.SocketId == socketId))
                {
                    frames[candidate.LocalFrame] = candidate;
                }
            }
            return new CharacterTestBakedSocketTrack(clipKey, socketId, frames);
        }
    }

    internal interface ICharacterTestBakedWeaponTraceSource
    {
        bool TryGetFrame(string clipKey, int traceProfileId, int localFrame, out MxAnimationBakedWeaponTraceFrame frame);
        bool TryGetSocketFrame(string clipKey, int socketId, int localFrame, out MxAnimationBakedSocketFrame frame);

        /// <remarks>LowFreqAlloc. Resolve during entity/package registration, never from Tick.</remarks>
        bool TryResolveSocketTrack(string clipKey, int socketId, out CharacterTestBakedSocketTrack track);
    }

    internal interface ICharacterTestPackageBakedWeaponTraceSource
    {
        bool TryGetFrame(string generatedPackageRoot, string clipKey, int traceProfileId,
            int localFrame, out MxAnimationBakedWeaponTraceFrame frame);
    }

    internal interface ICharacterTestPackageContactMotionSource
    {
        bool TryResolveSocketTrack(string packageRoot, string clipKey, int socketId, out CharacterTestBakedSocketTrack track);
    }

    internal static class CharacterTestBakedWeaponTraceSourceExtensions
    {
        internal static bool TryGetFrameForEntity(this ICharacterTestBakedWeaponTraceSource source,
            CharacterTestCharacterEntity entity, string clipKey, int traceProfileId,
            int localFrame, out MxAnimationBakedWeaponTraceFrame frame)
        {
            if (source is ICharacterTestPackageBakedWeaponTraceSource packages)
                return packages.TryGetFrame(entity?.Identity.GeneratedPackageRoot, clipKey,
                    traceProfileId, localFrame, out frame);
            return source.TryGetFrame(clipKey, traceProfileId, localFrame, out frame);
        }
    }

    internal interface ICharacterTestPackageBakedSocketSource
    {
        bool TryGetSocketFrame(
            string generatedPackageRoot,
            string clipKey,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame);
    }

    internal sealed class CharacterTestDefaultBakedWeaponTraceSource :
        ICharacterTestBakedWeaponTraceSource,
        ICharacterTestPackageContactMotionSource,
        ICharacterTestPackageBakedWeaponTraceSource,
        ICharacterTestPackageBakedSocketSource
    {
        private static CharacterTestDefaultBakedWeaponTraceSource _instance;

        private readonly CharacterWeaponTraceBakeCatalog _catalog;
        private readonly Dictionary<string, CharacterWeaponTraceBakeCatalog> _weaponCatalogsByPackageRoot =
            new Dictionary<string, CharacterWeaponTraceBakeCatalog>(StringComparer.OrdinalIgnoreCase);
        private readonly int _socketTraceProfileId;
        private readonly Dictionary<string, CharacterWeaponTraceBakeCatalog> _contactCatalogsByPackageRoot =
            new Dictionary<string, CharacterWeaponTraceBakeCatalog>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CharacterSocketBakeCatalog> _socketCatalogsByPackageRoot =
            new Dictionary<string, CharacterSocketBakeCatalog>(StringComparer.OrdinalIgnoreCase);

        private CharacterTestDefaultBakedWeaponTraceSource()
        {
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadDefaultCharacterPackage();
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.LoadInspectionCatalog();
            CharacterWeaponTraceBakeRequest[] requests = CharacterTestWeaponTraceBakeRequests.Create(
                package,
                actionRuntime?.Registry);
            string catalogPath = Path.Combine(
                package.RootPath,
                CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath);
            if (File.Exists(catalogPath))
            {
                _catalog = CharacterWeaponTraceBakeStorage.Load(package.RootPath, package.PackageId, false,
                    out CharacterWeaponTraceBakeBuildReport loadReport);
                BuildReport = ValidateLoadedCatalog(_catalog, requests, loadReport);
            }
            else
            {
                _catalog = new CharacterWeaponTraceBakeCatalog();
                BuildReport = MissingCatalogReport(requests, NormalizePath(catalogPath));
            }
            _socketTraceProfileId = ResolveFirstTraceId(package);
        }

        public static CharacterTestDefaultBakedWeaponTraceSource Instance =>
            _instance ?? (_instance = new CharacterTestDefaultBakedWeaponTraceSource());

        /// <summary>Creates a fresh reader on next access. Existing session readers retain their frozen inputs.</summary>
        /// <remarks>LowFreqAlloc on next access. Offline authoring/measurement bootstrap only.</remarks>
        internal static void InvalidateCachedInstanceForMeasurement()
        { _instance = null; CharacterTestTacticalPreparationCatalog.Invalidate(); }

        internal CharacterWeaponTraceBakeBuildReport BuildReport { get; }

        public bool TryGetFrame(
            string clipKey,
            int traceProfileId,
            int localFrame,
            out MxAnimationBakedWeaponTraceFrame frame)
        {
            return _catalog.TryGetFrame(clipKey, traceProfileId, localFrame, out frame);
        }

        public bool TryGetFrame(string generatedPackageRoot, string clipKey, int traceProfileId,
            int localFrame, out MxAnimationBakedWeaponTraceFrame frame)
        {
            CharacterWeaponTraceBakeCatalog catalog = LoadWeaponCatalog(generatedPackageRoot);
            frame = null;
            return catalog != null && catalog.TryGetFrame(clipKey, traceProfileId, localFrame, out frame);
        }

        /// <remarks>LowFreqAlloc: load the package catalog at spawn, before attack evaluation.</remarks>
        internal static void PrepareSpawnedEntity(CharacterTestCharacterEntity entity)
        {
            if (entity != null)
                Instance.LoadWeaponCatalog(entity.Identity.GeneratedPackageRoot);
        }

        /// <remarks>LowFreqAlloc on the first package lookup; warmed frame reads only consult the cache.</remarks>
        private CharacterWeaponTraceBakeCatalog LoadWeaponCatalog(string generatedPackageRoot)
        {
            if (string.IsNullOrWhiteSpace(generatedPackageRoot)
                || string.Equals(generatedPackageRoot, CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot,
                    StringComparison.OrdinalIgnoreCase))
                return _catalog;
            if (_weaponCatalogsByPackageRoot.TryGetValue(generatedPackageRoot, out CharacterWeaponTraceBakeCatalog cached))
                return cached;

            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(generatedPackageRoot);
            CharacterWeaponTraceBakeCatalog loaded = null;
            if (package != null && !string.IsNullOrWhiteSpace(package.RootPath))
            {
                string path = Path.Combine(package.RootPath, CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath);
                if (File.Exists(path))
                {
                    loaded = CharacterWeaponTraceBakeStorage.Load(package.RootPath, package.PackageId, false, out var report);
                    if (report.Issues.Length > 0)
                        loaded = null;
                }
            }
            _weaponCatalogsByPackageRoot[generatedPackageRoot] = loaded;
            return loaded;
        }

        public bool TryGetSocketFrame(
            string clipKey,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame)
        {
            return TryGetSocketFrame(string.Empty, clipKey, socketId, localFrame, out frame);
        }

        public bool TryGetSocketFrame(
            string generatedPackageRoot,
            string clipKey,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame)
        {
            string packageRoot = string.IsNullOrWhiteSpace(generatedPackageRoot)
                ? CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot
                : generatedPackageRoot;
            CharacterSocketBakeCatalog socketCatalog = LoadSocketCatalog(packageRoot);
            if (socketCatalog != null && socketCatalog.TryGetFrame(clipKey, socketId, localFrame, out frame))
                return true;

            frame = null;
            return false;
        }

        public bool TryResolveSocketTrack(
            string clipKey,
            int socketId,
            out CharacterTestBakedSocketTrack track)
        {
            return TryResolveSocketTrack(CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot, clipKey, socketId, out track);
        }

        public bool TryResolveSocketTrack(string packageRoot, string clipKey, int socketId,
            out CharacterTestBakedSocketTrack track)
        {
            track = null;
            packageRoot = string.IsNullOrWhiteSpace(packageRoot)
                ? CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot : packageRoot;
            if (!_contactCatalogsByPackageRoot.TryGetValue(packageRoot, out CharacterWeaponTraceBakeCatalog catalog))
            {
                CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(packageRoot);
                if (package != null)
                {
                    catalog = CharacterWeaponTraceBakeStorage.Load(package.RootPath, package.PackageId, true, out var report);
                    if (!report.Success) catalog = null;
                }
                _contactCatalogsByPackageRoot[packageRoot] = catalog;
            }
            if (catalog == null) return false;
            foreach (var pair in catalog.EnumerateArtifacts())
            {
                if (!string.Equals(pair.Key.ClipKey, clipKey, StringComparison.Ordinal)) continue;
                track = CharacterTestBakedSocketTrack.Create(clipKey, socketId, pair.Value.SocketFrames);
                if (track != null && track.IsValid) return true;
            }
            return false;
        }

        /// <remarks>LowFreqAlloc on the first package lookup; subsequent reads reuse the existing package catalog cache.</remarks>
        internal bool TryGetPackageSocketCatalog(
            string generatedPackageRoot,
            out CharacterSocketBakeCatalog catalog)
        {
            string packageRoot = string.IsNullOrWhiteSpace(generatedPackageRoot)
                ? CharacterTestGeneratedPackageSource.DefaultCharacterPackageRoot
                : generatedPackageRoot;
            catalog = LoadSocketCatalog(packageRoot);
            return catalog != null && catalog.TrackCount > 0;
        }

        private static int ResolveFirstTraceId(CharacterImportedPackage package)
        {
            CharacterWeaponTraceRuntimeBinding[] traces = package?.Geometry?.WeaponTraces
                ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (trace != null && trace.TraceId > 0)
                    return trace.TraceId;
            }

            return 0;
        }

        private static CharacterWeaponTraceBakeBuildReport MissingCatalogReport(
            CharacterWeaponTraceBakeRequest[] requests,
            string catalogPath)
        {
            var issues = new List<CharacterWeaponTraceBakeBuildIssue>
            {
                new CharacterWeaponTraceBakeBuildIssue(
                    CharacterWeaponTraceBakeCatalogJson.IssueCatalogMissing,
                    "Generated weapon trace bake catalog is missing. Weapon hit traces will not use a synthetic fallback.",
                    CharacterActionId.None,
                    string.Empty,
                    0,
                    -1,
                    catalogPath,
                    "catalog")
            };
            return new CharacterWeaponTraceBakeBuildReport(
                requests != null ? requests.Length : 0,
                0,
                0,
                issues.ToArray());
        }

        private static CharacterWeaponTraceBakeBuildReport ValidateLoadedCatalog(
            CharacterWeaponTraceBakeCatalog catalog,
            CharacterWeaponTraceBakeRequest[] requests,
            CharacterWeaponTraceBakeBuildReport loadReport)
        {
            var issues = new List<CharacterWeaponTraceBakeBuildIssue>(
                loadReport?.Issues ?? Array.Empty<CharacterWeaponTraceBakeBuildIssue>());
            int checkedFrameCount = 0;
            for (int i = 0; requests != null && i < requests.Length; i++)
            {
                CharacterWeaponTraceBakeRequest request = requests[i];
                if (request == null)
                    continue;

                for (int frame = request.ActiveStartFrame; frame <= request.ActiveEndFrame; frame++)
                {
                    checkedFrameCount++;
                    if (catalog.TryGetFrame(request.ClipKey, request.TraceId, frame, out _))
                        continue;

                    issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                        CharacterWeaponTraceBakeCatalogBuilder.IssueFrameMissing,
                        "Generated weapon trace bake catalog has no frame for this active hit frame.",
                        request.ActionId,
                        request.ClipKey,
                        request.TraceId,
                        frame,
                        CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath,
                        "bakes/" + request.ActionId.Value + "/" + (request.TraceId)));
                }
            }

            return new CharacterWeaponTraceBakeBuildReport(
                requests != null ? requests.Length : 0,
                loadReport?.ArtifactCount ?? 0,
                checkedFrameCount,
                issues.ToArray());
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private CharacterSocketBakeCatalog LoadSocketCatalog(string generatedPackageRoot)
        {
            string key = NormalizePath(generatedPackageRoot).TrimEnd('/');
            if (_socketCatalogsByPackageRoot.TryGetValue(key, out CharacterSocketBakeCatalog cached))
                return cached;

            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(generatedPackageRoot);
            if (package == null || string.IsNullOrWhiteSpace(package.RootPath))
            {
                _socketCatalogsByPackageRoot[key] = null;
                return null;
            }

            // Socket bake is a v3 index + payload pair. A missing, legacy or mismatched pair is a
            // hard failure: the registration stays unavailable and never falls back to another source.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    package.RootPath,
                    package.PackageId,
                    out CharacterSocketBakeCatalog loaded,
                    out _,
                    out string[] issues))
            {
                UnityEngine.Debug.LogError(
                    "MxFramework socket bake catalog is unavailable for package '" + package.PackageId
                    + "' at " + package.RootPath + ": " + string.Join(" | ", issues));
                _socketCatalogsByPackageRoot[key] = null;
                return null;
            }

            _socketCatalogsByPackageRoot[key] = loaded;
            return loaded;
        }
    }
}
