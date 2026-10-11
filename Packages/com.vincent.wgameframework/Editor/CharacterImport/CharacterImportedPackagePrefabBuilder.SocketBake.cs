using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Projectiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private sealed class SocketBakeStatus
        {
            public string Status { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public int RequestCount { get; set; }
            public int ClipCount { get; set; }
            public int SocketTrackCount { get; set; }
            public int CheckedFrameCount { get; set; }
            public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } = Array.Empty<CharacterWeaponTraceBakeIssueReport>();

            // v3 product identity. Empty until the pair has been written and verified.
            public string Format { get; set; } = string.Empty;
            public string IndexPath { get; set; } = string.Empty;
            public string IndexHash { get; set; } = string.Empty;
            public string BinaryPath { get; set; } = string.Empty;
            public string BinaryHash { get; set; } = string.Empty;
            public long BinarySize { get; set; }
        }

        private sealed class SocketBakeRequest
        {
            public CharacterActionId ActionId;
            public string ClipKey = string.Empty;
            public int MaxFrame;
            public readonly HashSet<int> SocketIds = new HashSet<int>();
            public readonly HashSet<int> BasisChannelIds = new HashSet<int>();
        }

        private sealed class SocketBakeTrackState
        {
            public int TrackId;
            public string TrackPath = string.Empty;
            public CharacterContactMotionSocketSample Sample;
            public MxAnimationBakedSocketFrame[] Frames = Array.Empty<MxAnimationBakedSocketFrame>();
            public CharacterSocketBakeBasisFrame[] BasisFrames;
            public Vector3 Previous;
            public bool IsValid = true;
        }

        private static SocketBakeStatus CollectSocketBakeStatus(CharacterImportedPackage package)
        {
            string registryIssue = string.Empty;
            if (package == null
                || !TryLoadCharacterActionRegistryForPackage(package, out CharacterActionConfigRegistry registry, out registryIssue))
            {
                return new SocketBakeStatus
                {
                    Status = "Failed",
                    Issues = new[] { SocketIssue("SocketBakeActionRegistryMissing", registryIssue, string.Empty, string.Empty, -1) }
                };
            }

            SocketBakeRequest[] requests = CreateSocketBakeRequests(package, registry);
            if (package.Geometry?.AuthorityPoseRig?.IsAuthored == true && requests.Length == 0)
            {
                return new SocketBakeStatus
                {
                    Status = "Failed",
                    Issues = new[]
                    {
                        SocketIssue(
                            "SocketBakeAuthorityPoseClipRegistryMissing",
                            "Authority-pose basis bake requires the formal animation clip registry.",
                            string.Empty,
                            string.Empty,
                            -1)
                    }
                };
            }
            string inputHash = ComputeSocketBakeHash(package, requests);
            if (requests.Length == 0)
            {
                return PublishSocketBake(
                    package,
                    new CharacterSocketBakeCatalog(),
                    requests,
                    0,
                    new List<CharacterWeaponTraceBakeIssueReport>(),
                    inputHash);
            }

            if (TryReuseSocketBake(package, inputHash, out SocketBakeStatus cached))
                return cached;

            CharacterSocketBakeCatalog catalog = BuildSocketBakeCatalog(package, requests, out List<CharacterWeaponTraceBakeIssueReport> issues, out int checkedFrames);
            return PublishSocketBake(package, catalog, requests, checkedFrames, issues, inputHash);
        }

        private static SocketBakeStatus PublishSocketBake(
            CharacterImportedPackage package,
            CharacterSocketBakeCatalog catalog,
            SocketBakeRequest[] requests,
            int checkedFrames,
            List<CharacterWeaponTraceBakeIssueReport> issues,
            string inputHash)
        {
            var clips = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; requests != null && i < requests.Length; i++)
                clips.Add(requests[i].ClipKey);

            if (issues.Count != 0)
            {
                return new SocketBakeStatus
                {
                    Status = "Failed",
                    Hash = inputHash,
                    RequestCount = requests?.Length ?? 0,
                    ClipCount = clips.Count,
                    SocketTrackCount = catalog.TrackCount,
                    CheckedFrameCount = checkedFrames,
                    Issues = issues.ToArray()
                };
            }

            // 7. Success evidence is published only after the pair verified on disk.
            try
            {
                SocketBakeProduct product = WriteSocketBakeCatalog(package, catalog);
                var status = new SocketBakeStatus
                {
                    Status = "Ready",
                    Hash = inputHash,
                    RequestCount = requests?.Length ?? 0,
                    ClipCount = clips.Count,
                    SocketTrackCount = catalog.TrackCount,
                    CheckedFrameCount = checkedFrames,
                    Issues = Array.Empty<CharacterWeaponTraceBakeIssueReport>(),
                    Format = product.Format,
                    IndexPath = CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath,
                    IndexHash = product.IndexHash,
                    BinaryPath = CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath,
                    BinaryHash = product.BinaryHash,
                    BinarySize = product.BinarySize
                };
                WriteSocketBakeCache(package, status);
                return status;
            }
            catch (Exception ex) when (ex is CharacterSocketBakeBinaryException
                || ex is IOException
                || ex is UnauthorizedAccessException)
            {
                return new SocketBakeStatus
                {
                    Status = "Failed",
                    Hash = inputHash,
                    RequestCount = requests?.Length ?? 0,
                    ClipCount = clips.Count,
                    SocketTrackCount = catalog.TrackCount,
                    CheckedFrameCount = checkedFrames,
                    Issues = new[]
                    {
                        SocketIssue("SocketBakeWriteFailed", ex.Message, string.Empty, string.Empty, -1)
                    }
                };
            }
        }

        private static SocketBakeRequest[] CreateSocketBakeRequests(
            CharacterImportedPackage package,
            CharacterActionConfigRegistry registry)
        {
            var byClip = new Dictionary<string, SocketBakeRequest>(StringComparer.Ordinal);
            string spawnProfilesPath = MxFramework.Editor.CharacterAction.CharacterActionProjectileAuthoringStore.DefaultGeneratedProjectileSpawnProfilesPath;
            CombatProjectileSpawnProfileRegistry spawnProfiles = File.Exists(spawnProfilesPath)
                ? CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(spawnProfilesPath)
                : new CombatProjectileSpawnProfileRegistry();
            CharacterActionConfig[] actions = registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            for (int i = 0; i < actions.Length; i++)
            {
                CharacterActionConfig action = actions[i];
                CombatTrackEvent[] events = action?.CombatTrack?.Events ?? Array.Empty<CombatTrackEvent>();
                for (int j = 0; j < events.Length; j++)
                {
                    CombatTrackEvent evt = events[j];
                    if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow
                        || evt.HitVolume.SourceKind != CharacterActionHitVolumeSourceKind.BakedSocketCapsule
                        || !evt.HitVolume.HasVolume)
                        continue;
                    string clipKey = ResolveActiveClipKey(action, evt.Frame);
                    if (string.IsNullOrWhiteSpace(clipKey))
                        continue;
                    if (!byClip.TryGetValue(clipKey, out SocketBakeRequest request))
                    {
                        request = new SocketBakeRequest { ActionId = action.ActionId, ClipKey = clipKey };
                        byClip.Add(clipKey, request);
                    }
                    request.MaxFrame = Math.Max(request.MaxFrame, ResolveTraceBakeMaxFrame(action, evt.EndFrame));
                    request.SocketIds.Add(evt.HitVolume.StartSocketId);
                    request.SocketIds.Add(evt.HitVolume.EndSocketId);
                }

                GameplayTrackEvent[] gameplayEvents = action?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEvent>();
                for (int j = 0; j < gameplayEvents.Length; j++)
                {
                    GameplayTrackEvent evt = gameplayEvents[j];
                    if (evt.Kind != CharacterActionTrackEventKind.SendGameplayRequest
                        || !spawnProfiles.TryGet(evt.RequestId, out CombatProjectileSpawnProfile profile)
                        || profile.Origin.Kind != CombatProjectileSpawnOriginKind.Socket)
                        continue;
                    string clipKey = ResolveActiveClipKey(action, evt.Frame);
                    if (string.IsNullOrWhiteSpace(clipKey))
                        continue;
                    if (!byClip.TryGetValue(clipKey, out SocketBakeRequest request))
                    {
                        request = new SocketBakeRequest { ActionId = action.ActionId, ClipKey = clipKey };
                        byClip.Add(clipKey, request);
                    }
                    request.MaxFrame = Math.Max(request.MaxFrame, ResolveTraceBakeMaxFrame(action, evt.Frame));
                    request.SocketIds.Add(profile.Origin.SocketId);
                }
            }

            CharacterAuthorityPoseRigRuntimeBinding rig = package?.Geometry?.AuthorityPoseRig;
            CharacterAnimationClipRegistryEntry[] clips = package?.CompiledAnimationArtifacts?.AnimationClipRegistry?.Clips
                ?? Array.Empty<CharacterAnimationClipRegistryEntry>();
            if (rig?.IsAuthored == true)
            {
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    string clipKey = clips[clipIndex]?.RuntimeResourceKey ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(clipKey))
                        continue;
                    if (!byClip.TryGetValue(clipKey, out SocketBakeRequest request))
                    {
                        request = new SocketBakeRequest { ClipKey = clipKey };
                        byClip.Add(clipKey, request);
                    }
                    for (int channelIndex = 0; channelIndex < rig.Channels.Length; channelIndex++)
                        request.BasisChannelIds.Add(rig.Channels[channelIndex].ChannelId);
                }
            }
            var result = new List<SocketBakeRequest>(byClip.Values);
            result.Sort((left, right) => string.CompareOrdinal(left.ClipKey, right.ClipKey));
            return result.ToArray();
        }

        private static CharacterSocketBakeCatalog BuildSocketBakeCatalog(
            CharacterImportedPackage package,
            SocketBakeRequest[] requests,
            out List<CharacterWeaponTraceBakeIssueReport> issues,
            out int checkedFrames)
        {
            var catalog = new CharacterSocketBakeCatalog();
            issues = new List<CharacterWeaponTraceBakeIssueReport>();
            checkedFrames = 0;
            string prefabPath = GetPreviewPrefabPath(package);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                issues.Add(SocketIssue("SocketBakePreviewPrefabMissing", "Generated preview prefab is required for socket sampling.", string.Empty, string.Empty, -1));
                return catalog;
            }

            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject ?? UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);
                Transform bodyModel = FindChildByName(instance.transform, "BodyModel") ?? instance.transform;
                CharacterSocketRuntimeBinding[] sockets = package?.Geometry?.Sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
                for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
                {
                    SocketBakeRequest request = requests[requestIndex];
                    AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, request.ClipKey);
                    if (clip == null)
                    {
                        issues.Add(SocketIssue("SocketBakeClipMissing", "Animation clip could not be loaded.", request.ClipKey, string.Empty, -1));
                        continue;
                    }
                    int maxFrame = request.BasisChannelIds.Count > 0
                        ? Math.Max(request.MaxFrame, Math.Max(0, Mathf.CeilToInt(clip.length * 60f) - 1))
                        : request.MaxFrame;
                    var socketTracks = new List<SocketBakeTrackState>();
                    var orderedSocketIds = new List<int>(request.SocketIds);
                    orderedSocketIds.Sort();
                    for (int socketIndex = 0; socketIndex < orderedSocketIds.Count; socketIndex++)
                    {
                        int socketId = orderedSocketIds[socketIndex];
                        CharacterSocketRuntimeBinding binding = FindSocketBinding(sockets, socketId);
                        if (binding == null)
                        {
                            issues.Add(SocketIssue("SocketBakeSocketBindingMissing", "Socket is not declared in geometry/sockets.json.", request.ClipKey, socketId, -1));
                            continue;
                        }
                        var legacyIssues = new List<CharacterWeaponTraceBakeBuildIssue>();
                        if (!TryResolveBodyPartContactMotionSocketSample(instance, bodyModel, binding, clip, request.ClipKey, legacyIssues, out CharacterContactMotionSocketSample sample))
                        {
                            issues.Add(SocketIssue("SocketBakeBoneMissing", "Socket bone could not be resolved on the imported model.", request.ClipKey, socketId, -1));
                            continue;
                        }
                        for (int i = 0; i < legacyIssues.Count; i++)
                            issues.Add(SocketIssue(legacyIssues[i].Code, legacyIssues[i].Message, request.ClipKey, socketId, legacyIssues[i].Frame));
                        socketTracks.Add(new SocketBakeTrackState
                        {
                            TrackId = socketId,
                            TrackPath = FormatContactMotionSocketPath(sample),
                            Sample = sample,
                            Frames = new MxAnimationBakedSocketFrame[maxFrame + 1]
                        });
                    }

                    var basisTracks = new List<SocketBakeTrackState>();
                    var orderedChannelIds = new List<int>(request.BasisChannelIds);
                    orderedChannelIds.Sort();
                    for (int channelIndex = 0; channelIndex < orderedChannelIds.Count; channelIndex++)
                    {
                        int channelId = orderedChannelIds[channelIndex];
                        CharacterAuthorityPoseChannelRuntimeBinding channel = FindAuthorityPoseChannel(
                            package.Geometry.AuthorityPoseRig.Channels,
                            channelId);
                        Transform bone = channel != null
                            ? FindBodyChild(bodyModel, channel.BonePath)
                            : null;
                        if (bone == null)
                        {
                            issues.Add(SocketIssue(
                                "SocketBakeAuthorityPoseBoneMissing",
                                "Authority-pose channel bone could not be resolved on the imported model.",
                                request.ClipKey,
                                channelId,
                                -1));
                            continue;
                        }

                        var sample = new CharacterContactMotionSocketSample(
                            channel.ChannelId,
                            channel.BonePath,
                            bone,
                            Vector3.zero,
                            null,
                            CharacterContactMotionSocketSampleSourceKind.AnimatedBone);
                        basisTracks.Add(new SocketBakeTrackState
                        {
                            TrackId = channelId,
                            TrackPath = channel.BonePath,
                            Sample = sample,
                            Frames = new MxAnimationBakedSocketFrame[maxFrame + 1],
                            BasisFrames = new CharacterSocketBakeBasisFrame[maxFrame + 1]
                        });
                    }

                    using var poseSampler = new WeaponTracePoseSampler(instance, clip);
                    for (int frame = 0; frame <= maxFrame; frame++)
                    {
                        float time = ResolveBakeSampleTime(clip, frame);
                        poseSampler.Sample(time);
                        for (int trackIndex = 0; trackIndex < socketTracks.Count; trackIndex++)
                        {
                            SocketBakeTrackState track = socketTracks[trackIndex];
                            if (!track.Sample.TryGetLocalPosition(instance.transform, time, out Vector3 local))
                            {
                                issues.Add(SocketIssue("SocketBakeFrameMissing", "Socket position could not be sampled.", request.ClipKey, track.TrackId, frame));
                                continue;
                            }
                            track.Frames[frame] = CreateSocketBakeFrame(track, frame, local);
                            track.Previous = local;
                            checkedFrames++;
                        }
                        for (int trackIndex = 0; trackIndex < basisTracks.Count; trackIndex++)
                        {
                            SocketBakeTrackState track = basisTracks[trackIndex];
                            if (!track.IsValid)
                                continue;
                            if (!track.Sample.TryGetLocalPosition(instance.transform, time, out Vector3 local)
                                || !track.Sample.TryGetLocalBasis(instance.transform, out Vector3 right, out Vector3 up, out Vector3 forward))
                            {
                                issues.Add(SocketIssue(
                                    "SocketBakeAuthorityPoseFrameMissing",
                                    "Authority-pose position or basis could not be sampled.",
                                    request.ClipKey,
                                    track.TrackId,
                                    frame));
                                track.IsValid = false;
                                continue;
                            }
                            track.Frames[frame] = CreateSocketBakeFrame(track, frame, local);
                            track.BasisFrames[frame] = new CharacterSocketBakeBasisFrame(
                                frame,
                                Bake(right, package.Geometry.AuthorityPoseRig.NormalQuantizationScale),
                                Bake(up, package.Geometry.AuthorityPoseRig.NormalQuantizationScale),
                                Bake(forward, package.Geometry.AuthorityPoseRig.NormalQuantizationScale));
                            track.Previous = local;
                            checkedFrames++;
                        }
                    }

                    for (int trackIndex = 0; trackIndex < socketTracks.Count; trackIndex++)
                    {
                        SocketBakeTrackState track = socketTracks[trackIndex];
                        catalog.RegisterTrack(request.ClipKey, track.TrackId, CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale, track.Frames);
                    }
                    for (int trackIndex = 0; trackIndex < basisTracks.Count; trackIndex++)
                    {
                        SocketBakeTrackState track = basisTracks[trackIndex];
                        if (!track.IsValid)
                            continue;
                        catalog.RegisterTrack(
                            request.ClipKey,
                            track.TrackId,
                            CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale,
                            track.Frames,
                            package.Geometry.AuthorityPoseRig.NormalQuantizationScale,
                            track.BasisFrames);
                    }
                }
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
            return catalog;
        }

        private static MxAnimationBakedSocketFrame CreateSocketBakeFrame(
            SocketBakeTrackState track,
            int frame,
            Vector3 local)
        {
            return new MxAnimationBakedSocketFrame(
                frame,
                track.TrackId,
                track.TrackPath,
                Bake(local, CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale),
                Bake(frame == 0 ? Vector3.zero : local - track.Previous, CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale));
        }

        /// <summary>Verified identity of the published v3 pair.</summary>
        private readonly struct SocketBakeProduct
        {
            public SocketBakeProduct(string format, string indexHash, string binaryHash, long binarySize)
            {
                Format = format;
                IndexHash = indexHash;
                BinaryHash = binaryHash;
                BinarySize = binarySize;
            }

            public string Format { get; }
            public string IndexHash { get; }
            public string BinaryHash { get; }
            public long BinarySize { get; }
        }

        private static readonly HashSet<string> SocketBakePublishingRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void WriteSocketBakeCatalogForTests(
            CharacterImportedPackage package, CharacterSocketBakeCatalog catalog, Action<string> publishStage = null)
        {
            WriteSocketBakeCatalog(package, catalog, publishStage);
        }

        private static SocketBakeProduct WriteSocketBakeCatalog(
            CharacterImportedPackage package, CharacterSocketBakeCatalog catalog, Action<string> publishStage = null)
        {
            string root = Path.GetFullPath(package.RootPath);
            lock (SocketBakePublishingRoots)
            {
                if (!SocketBakePublishingRoots.Add(root))
                    throw new IOException("Socket Bake publication is already active for " + root);
            }
            try { return WriteSocketBakeCatalogCore(package, catalog, publishStage); }
            finally { lock (SocketBakePublishingRoots) SocketBakePublishingRoots.Remove(root); }
        }

        private static SocketBakeProduct WriteSocketBakeCatalogCore(
            CharacterImportedPackage package, CharacterSocketBakeCatalog catalog, Action<string> publishStage)
        {
            string relativeIndex = CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath;
            string relativeBinary = CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath;
            string indexPath = Path.Combine(package.RootPath, relativeIndex);
            string binaryPath = Path.Combine(package.RootPath, relativeBinary);
            Directory.CreateDirectory(Path.GetDirectoryName(indexPath));
            Directory.CreateDirectory(Path.GetDirectoryName(binaryPath));

            // 2. Encode, then read the result back in memory and compare it field by field.
            byte[] binary = CharacterSocketBakeCatalogBinary.Serialize(package.PackageId, catalog);
            string indexJson = CharacterSocketBakeCatalogBinary.SerializeIndex(package.PackageId, catalog, binary);
            byte[] indexBytes = new UTF8Encoding(false).GetBytes(indexJson);
            if (!CharacterSocketBakeCatalogBinary.TryLoad(indexJson, binary, package.PackageId, out CharacterSocketBakeCatalog roundTrip, out string[] roundTripIssues)
                || !CatalogsAreEquivalent(catalog, roundTrip))
            {
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "socket bake round-trip verification failed: " + string.Join(" | ", roundTripIssues));
            }

            string binaryHash = CharacterSocketBakeCatalogBinary.ComputeSha256(binary);
            string indexHash = CharacterSocketBakeCatalogBinary.ComputeSha256(indexBytes);

            // 3. An unchanged pair keeps its content, mtime and meta.
            if (File.Exists(indexPath) && File.Exists(binaryPath))
            {
                byte[] existingBinary = File.ReadAllBytes(binaryPath);
                byte[] existingIndex = File.ReadAllBytes(indexPath);
                if (ByteArraysEqual(existingBinary, binary) && ByteArraysEqual(existingIndex, indexBytes))
                    return new SocketBakeProduct(CharacterSocketBakeCatalogBinary.Format, indexHash, binaryHash, binary.LongLength);
            }

            // 4. Stage the new pair next to a backup of the current one.
            string stagingRoot = Path.GetFullPath(Path.Combine(
                "Temp",
                "CharacterSocketBakeMigration",
                "publish",
                SanitizeName(package.PackageId)));
            Directory.CreateDirectory(stagingRoot);
            string stagedBinary = Path.Combine(stagingRoot, "character_socket_bake_catalog.bytes");
            string stagedIndex = Path.Combine(stagingRoot, "character_socket_bake_catalog.json");
            File.WriteAllBytes(stagedBinary, binary);
            File.WriteAllBytes(stagedIndex, indexBytes);

            bool hadBinary = File.Exists(binaryPath);
            bool hadIndex = File.Exists(indexPath);
            string backupBinary = Path.Combine(stagingRoot, "previous.bytes");
            string backupIndex = Path.Combine(stagingRoot, "previous.json");
            if (hadBinary)
                File.Copy(binaryPath, backupBinary, true);
            if (hadIndex)
                File.Copy(indexPath, backupIndex, true);
            File.WriteAllText(
                Path.Combine(stagingRoot, "manifest.json"),
                new JObject
                {
                    ["packageId"] = package.PackageId,
                    ["indexPath"] = relativeIndex,
                    ["binaryPath"] = relativeBinary,
                    ["indexHash"] = indexHash,
                    ["binaryHash"] = binaryHash,
                    ["binarySize"] = binary.LongLength,
                    ["hadIndex"] = hadIndex,
                    ["hadBinary"] = hadBinary,
                    ["stagedUtc"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                }.ToString(Formatting.Indented) + "\n");

            // 5. Publish the binary first, then the index, with auto-refresh suspended.
            try
            {
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    AssetDatabase.ReleaseCachedFileHandles();
                    publishStage?.Invoke("BeforeBinary");
                    File.Copy(stagedBinary, binaryPath, true);
                    publishStage?.Invoke("AfterBinary");
                    File.Copy(stagedIndex, indexPath, true);
                    publishStage?.Invoke("AfterIndex");
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                }
            }
            catch (Exception)
            {
                RestoreSocketBakePair(binaryPath, indexPath, backupBinary, backupIndex, hadBinary, hadIndex, stagingRoot);
                throw;
            }

            // 6. Re-verify the published pair through the runtime loader, then refresh the assets.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    package.RootPath,
                    package.PackageId,
                    out _,
                    out CharacterSocketBakeCatalogEvidence evidence,
                    out string[] verifyIssues))
            {
                RestoreSocketBakePair(binaryPath, indexPath, backupBinary, backupIndex, hadBinary, hadIndex, stagingRoot);
                throw new CharacterSocketBakeBinaryException(
                    CharacterSocketBakeDiagnosticCodes.PayloadInvalid,
                    "published socket bake pair did not verify: " + string.Join(" | ", verifyIssues));
            }

            AssetDatabase.ImportAsset(NormalizeProjectPath(indexPath), ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(NormalizeProjectPath(binaryPath), ImportAssetOptions.ForceSynchronousImport);

            return new SocketBakeProduct(evidence.Format, evidence.IndexHash, evidence.BinaryHash, evidence.BinarySize);
        }

        private static void RestoreSocketBakePair(
            string binaryPath,
            string indexPath,
            string backupBinary,
            string backupIndex,
            bool hadBinary,
            bool hadIndex,
            string stagingRoot)
        {
            try
            {
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    AssetDatabase.ReleaseCachedFileHandles();
                    if (hadBinary && File.Exists(backupBinary))
                        File.Copy(backupBinary, binaryPath, true);
                    else if (!hadBinary && File.Exists(binaryPath))
                        File.Delete(binaryPath);
                    if (hadIndex && File.Exists(backupIndex))
                        File.Copy(backupIndex, indexPath, true);
                    else if (!hadIndex && File.Exists(indexPath))
                        File.Delete(indexPath);
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                }
            }
            catch (Exception restoreFailure)
            {
                Debug.LogError(
                    "MxFramework socket bake recovery failed for staging root " + stagingRoot
                    + "; the package requires a re-import. " + restoreFailure.Message);
            }
        }

        private static bool CatalogsAreEquivalent(CharacterSocketBakeCatalog left, CharacterSocketBakeCatalog right)
        {
            if (left == null || right == null)
                return ReferenceEquals(left, right);
            var leftTracks = new List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>(left.EnumerateTracks());
            var rightTracks = new List<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>>(right.EnumerateTracks());
            if (leftTracks.Count != rightTracks.Count)
                return false;
            leftTracks.Sort((a, b) => CompareKeys(a.Key, b.Key));
            rightTracks.Sort((a, b) => CompareKeys(a.Key, b.Key));
            for (int i = 0; i < leftTracks.Count; i++)
            {
                CharacterSocketBakeKey leftKey = leftTracks[i].Key;
                CharacterSocketBakeKey rightKey = rightTracks[i].Key;
                if (!leftKey.Equals(rightKey))
                    return false;
                CharacterSocketBakeTrack leftTrack = leftTracks[i].Value;
                CharacterSocketBakeTrack rightTrack = rightTracks[i].Value;
                if (leftTrack.QuantizationScale != rightTrack.QuantizationScale
                    || leftTrack.BasisQuantizationScale != rightTrack.BasisQuantizationScale
                    || leftTrack.HasBasis != rightTrack.HasBasis
                    || leftTrack.Frames.Count != rightTrack.Frames.Count
                    || leftTrack.BasisFrames.Count != rightTrack.BasisFrames.Count)
                    return false;
                for (int f = 0; f < leftTrack.Frames.Count; f++)
                {
                    MxAnimationBakedSocketFrame a = leftTrack.Frames[f];
                    MxAnimationBakedSocketFrame b = rightTrack.Frames[f];
                    if (a == null || b == null)
                    {
                        if (!ReferenceEquals(a, b))
                            return false;
                        continue;
                    }
                    if (a.LocalFrame != b.LocalFrame
                        || a.SocketId != b.SocketId
                        || !string.Equals(a.SocketPath, b.SocketPath, StringComparison.Ordinal)
                        || !a.Position.Equals(b.Position)
                        || !a.DeltaPosition.Equals(b.DeltaPosition))
                        return false;
                }
                for (int f = 0; f < leftTrack.BasisFrames.Count; f++)
                {
                    CharacterSocketBakeBasisFrame a = leftTrack.BasisFrames[f];
                    CharacterSocketBakeBasisFrame b = rightTrack.BasisFrames[f];
                    if (a == null || b == null)
                    {
                        if (!ReferenceEquals(a, b))
                            return false;
                        continue;
                    }
                    if (a.LocalFrame != b.LocalFrame
                        || !a.Right.Equals(b.Right)
                        || !a.Up.Equals(b.Up)
                        || !a.Forward.Equals(b.Forward))
                        return false;
                }
            }
            return true;
        }

        private static int CompareKeys(CharacterSocketBakeKey left, CharacterSocketBakeKey right)
        {
            int clip = string.CompareOrdinal(left.ClipKey, right.ClipKey);
            return clip != 0 ? clip : left.SocketId.CompareTo(right.SocketId);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }
            return true;
        }

        private static string ComputeSocketBakeHash(CharacterImportedPackage package, SocketBakeRequest[] requests)
        {
            var value = new System.Text.StringBuilder("CharacterSocketBake.v3|runtime-pose-v1|")
                .Append(BuilderVersion).Append('|')
                .Append(package?.ImportReport?.ResourceMappingHash ?? string.Empty).Append('|')
                .Append(ComputeUnityArtifactAggregateHash(package?.UnityResourceCatalog));
            CharacterAnimationClipRegistryEntry[] clips = package?.CompiledAnimationArtifacts?.AnimationClipRegistry?.Clips
                ?? Array.Empty<CharacterAnimationClipRegistryEntry>();
            for (int i = 0; i < clips.Length; i++)
            {
                string clipKey = clips[i]?.RuntimeResourceKey ?? string.Empty;
                AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, clipKey);
                value.Append("|clip:").Append(clipKey).Append(':')
                    .Append(clip != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(clip)).ToString() : "missing");
            }
            for (int i = 0; requests != null && i < requests.Length; i++)
            {
                value.Append('|').Append(requests[i].ClipKey).Append(':').Append(requests[i].MaxFrame);
                var sockets = new List<int>(requests[i].SocketIds);
                sockets.Sort();
                for (int j = 0; j < sockets.Count; j++)
                {
                    CharacterSocketRuntimeBinding binding = FindSocketBinding(package?.Geometry?.Sockets, sockets[j]);
                    value.Append(":socket:").Append(sockets[j]).Append(':')
                        .Append(JsonConvert.SerializeObject(binding, Formatting.None));
                }
                var channels = new List<int>(requests[i].BasisChannelIds);
                channels.Sort();
                for (int j = 0; j < channels.Count; j++)
                {
                    CharacterAuthorityPoseChannelRuntimeBinding channel = FindAuthorityPoseChannel(
                        package?.Geometry?.AuthorityPoseRig?.Channels,
                        channels[j]);
                    value.Append(":basis:").Append(channels[j]).Append(':')
                        .Append(JsonConvert.SerializeObject(channel, Formatting.None));
                }
            }
            return ComputeStringContentHash(value.ToString());
        }

        private static bool TryReuseSocketBake(
            CharacterImportedPackage package,
            string inputHash,
            out SocketBakeStatus status)
        {
            status = null;
            JObject cache = ReadEditorImportCache(package.RootPath, "socket_bake");
            if (!string.Equals((string)cache?["inputHash"], inputHash, StringComparison.Ordinal)
                || !string.Equals((string)cache?["format"], CharacterSocketBakeCatalogBinary.Format, StringComparison.Ordinal))
            {
                return false;
            }

            string indexPath = Path.Combine(package.RootPath, CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath);
            string binaryPath = Path.Combine(package.RootPath, CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath);
            if (!CachedFileMatches(indexPath, cache?["index"] as JObject)
                || !CachedFileMatches(binaryPath, cache?["binary"] as JObject))
            {
                return false;
            }

            // A half-written, legacy or tampered pair must never hit the cache.
            if (!CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(
                    package.RootPath,
                    package.PackageId,
                    out _,
                    out CharacterSocketBakeCatalogEvidence evidence,
                    out _))
            {
                return false;
            }
            if (!string.Equals(evidence.IndexHash, (string)cache?["indexHash"], StringComparison.Ordinal)
                || !string.Equals(evidence.BinaryHash, (string)cache?["binaryHash"], StringComparison.Ordinal))
            {
                return false;
            }

            status = new SocketBakeStatus
            {
                Status = "Ready",
                Hash = inputHash,
                RequestCount = (int?)cache["requestCount"] ?? 0,
                ClipCount = (int?)cache["clipCount"] ?? 0,
                SocketTrackCount = (int?)cache["trackCount"] ?? 0,
                CheckedFrameCount = (int?)cache["checkedFrameCount"] ?? 0,
                Format = evidence.Format,
                IndexPath = CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath,
                IndexHash = evidence.IndexHash,
                BinaryPath = CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath,
                BinaryHash = evidence.BinaryHash,
                BinarySize = evidence.BinarySize
            };
            return true;
        }

        private static void WriteSocketBakeCache(CharacterImportedPackage package, SocketBakeStatus status)
        {
            string indexPath = Path.Combine(package.RootPath, CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath);
            string binaryPath = Path.Combine(package.RootPath, CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath);
            WriteEditorImportCache(package.RootPath, "socket_bake", new JObject
            {
                ["format"] = status.Format ?? string.Empty,
                ["inputHash"] = status.Hash ?? string.Empty,
                ["requestCount"] = status.RequestCount,
                ["clipCount"] = status.ClipCount,
                ["trackCount"] = status.SocketTrackCount,
                ["checkedFrameCount"] = status.CheckedFrameCount,
                ["indexHash"] = status.IndexHash ?? string.Empty,
                ["binaryHash"] = status.BinaryHash ?? string.Empty,
                ["binarySize"] = status.BinarySize,
                ["index"] = CaptureCachedFile(indexPath),
                ["binary"] = CaptureCachedFile(binaryPath)
            });
        }

        private static CharacterAuthorityPoseChannelRuntimeBinding FindAuthorityPoseChannel(
            CharacterAuthorityPoseChannelRuntimeBinding[] channels,
            int channelId)
        {
            for (int i = 0; channels != null && i < channels.Length; i++)
            {
                if (channels[i] != null && channels[i].ChannelId == channelId)
                    return channels[i];
            }
            return null;
        }

        private static CharacterWeaponTraceBakeIssueReport SocketIssue(string code, string message, string clipKey, int socketId, int frame)
        {
            return SocketIssue(code, message, clipKey, socketId.ToString(System.Globalization.CultureInfo.InvariantCulture), frame);
        }

        private static CharacterWeaponTraceBakeIssueReport SocketIssue(string code, string message, string clipKey, string socketId, int frame)
        {
            return new CharacterWeaponTraceBakeIssueReport
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                ClipKey = clipKey ?? string.Empty,
                TraceId = socketId ?? string.Empty,
                Frame = frame,
                SourcePath = string.IsNullOrWhiteSpace(clipKey) ? "geometry/sockets.json" : "generated/character_socket_bake_catalog.json",
                SourceObjectPath = "socketBake/" + (clipKey ?? string.Empty) + "/" + (socketId ?? string.Empty)
            };
        }
    }
}
