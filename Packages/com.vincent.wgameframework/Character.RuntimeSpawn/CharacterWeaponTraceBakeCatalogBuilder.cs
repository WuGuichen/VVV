using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.CharacterRuntimeSpawn
{
    public sealed class CharacterWeaponTraceBakeRequest
    {
        public CharacterWeaponTraceBakeRequest(
            CharacterActionId actionId,
            string clipKey,
            int traceId,
            int activeStartFrame,
            int activeEndFrame,
            int maxFrame,
            string sourceClipHash = "")
        {
            ActionId = actionId;
            ClipKey = clipKey ?? string.Empty;
            TraceId = traceId;
            ActiveStartFrame = activeStartFrame;
            ActiveEndFrame = activeEndFrame;
            MaxFrame = maxFrame;
            SourceClipHash = sourceClipHash ?? string.Empty;
        }

        public CharacterActionId ActionId { get; }
        public string ClipKey { get; }
        public int TraceId { get; }
        public int ActiveStartFrame { get; }
        public int ActiveEndFrame { get; }
        public int MaxFrame { get; }
        public string SourceClipHash { get; }
    }

    public readonly struct CharacterWeaponTraceBakeBuildIssue
    {
        public CharacterWeaponTraceBakeBuildIssue(
            string code,
            string message,
            CharacterActionId actionId,
            string clipKey,
            int traceId,
            int frame)
            : this(
                code,
                message,
                actionId,
                clipKey,
                traceId,
                frame,
                string.Empty,
                string.Empty)
        {
        }

        public CharacterWeaponTraceBakeBuildIssue(
            string code,
            string message,
            CharacterActionId actionId,
            string clipKey,
            int traceId,
            int frame,
            string sourcePath,
            string sourceObjectPath)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ActionId = actionId;
            ClipKey = clipKey ?? string.Empty;
            TraceId = traceId;
            Frame = frame;
            SourcePath = sourcePath ?? string.Empty;
            SourceObjectPath = sourceObjectPath ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
        public CharacterActionId ActionId { get; }
        public string ClipKey { get; }
        public int TraceId { get; }
        public int Frame { get; }
        public string SourcePath { get; }
        public string SourceObjectPath { get; }
    }

    public sealed class CharacterWeaponTraceBakeBuildReport
    {
        public CharacterWeaponTraceBakeBuildReport(
            int requestCount,
            int artifactCount,
            int checkedFrameCount,
            CharacterWeaponTraceBakeBuildIssue[] issues)
        {
            RequestCount = requestCount < 0 ? 0 : requestCount;
            ArtifactCount = artifactCount < 0 ? 0 : artifactCount;
            CheckedFrameCount = checkedFrameCount < 0 ? 0 : checkedFrameCount;
            Issues = issues ?? Array.Empty<CharacterWeaponTraceBakeBuildIssue>();
        }

        public int RequestCount { get; }
        public int ArtifactCount { get; }
        public int CheckedFrameCount { get; }
        public CharacterWeaponTraceBakeBuildIssue[] Issues { get; }
        public bool Success => Issues.Length == 0;
    }

    /// <summary>
    /// LowFreqAlloc: immutable result for weapon trace resolution performed during config load, bake setup or hit-window preparation.
    /// </summary>
    public readonly struct CharacterResolvedWeaponTraceBinding
    {
        public CharacterResolvedWeaponTraceBinding(
            int traceId,
            int equipmentSlotId,
            int weaponId,
            int startSocketId,
            int endSocketId,
            float length,
            float radius,
            float proximalExtension,
            float proximalRadius,
            string sampleRule,
            CharacterWeaponTraceRuntimeBinding traceBinding,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            bool usedLegacyTraceProfileId)
        {
            TraceId = traceId;
            EquipmentSlotId = equipmentSlotId;
            WeaponId = weaponId;
            StartSocketId = startSocketId;
            EndSocketId = endSocketId;
            Length = length < 0f ? 0f : length;
            Radius = radius < 0f ? 0f : radius;
            ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
            ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
            SampleRule = sampleRule ?? string.Empty;
            TraceBinding = traceBinding;
            Attachment = attachment;
            UsedLegacyTraceProfileId = usedLegacyTraceProfileId;
        }

        public int TraceId { get; }
        public int EquipmentSlotId { get; }
        public int WeaponId { get; }
        public int StartSocketId { get; }
        public int EndSocketId { get; }
        public float Length { get; }
        public float Radius { get; }
        public float ProximalExtension { get; }
        public float ProximalRadius { get; }
        public string SampleRule { get; }
        public CharacterWeaponTraceRuntimeBinding TraceBinding { get; }
        public CharacterWeaponAttachmentRuntimeBinding Attachment { get; }
        public bool UsedLegacyTraceProfileId { get; }
        public bool IsValid => TraceBinding != null && (TraceId > 0);
    }

    /// <summary>
    /// LowFreqAlloc: carries a source-localized diagnostic for failed weapon trace resolution.
    /// </summary>
    public readonly struct CharacterWeaponTraceResolveIssue
    {
        public CharacterWeaponTraceResolveIssue(
            string code,
            string message,
            string sourcePath,
            string sourceObjectPath,
            string field,
            int equipmentSlotId,
            int traceId,
            int weaponId)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            SourceObjectPath = sourceObjectPath ?? string.Empty;
            Field = field ?? string.Empty;
            EquipmentSlotId = equipmentSlotId;
            TraceId = traceId;
            WeaponId = weaponId;
        }

        public string Code { get; }
        public string Message { get; }
        public string SourcePath { get; }
        public string SourceObjectPath { get; }
        public string Field { get; }
        public int EquipmentSlotId { get; }
        public int TraceId { get; }
        public int WeaponId { get; }
        public bool HasIssue => !string.IsNullOrEmpty(Code);
    }

    /// <summary>
    /// LowFreqAlloc: resolves weapon hit geometry from slot-first data, while retaining explicit traceProfileId as a legacy branch.
    /// </summary>
    public static class CharacterWeaponTraceBindingResolver
    {
        public const string IssuePackageMissing = "WeaponTraceResolvePackageMissing";
        public const string IssueEquipmentSlotMissing = "WeaponTraceResolveEquipmentSlotMissing";
        public const string IssueAttachmentMissing = "WeaponTraceResolveAttachmentMissing";
        public const string IssueTraceMissing = "WeaponTraceResolveTraceMissing";
        public const string IssueTraceMismatch = "WeaponTraceResolveAttachmentTraceMismatch";
        public const string IssueTraceAmbiguous = "WeaponTraceResolveAmbiguousTrace";

        public static bool TryResolve(
            CharacterImportedPackage package,
            int equipmentSlotId,
            int explicitTraceProfileId,
            out CharacterResolvedWeaponTraceBinding binding,
            out CharacterWeaponTraceResolveIssue issue)
        {
            binding = default;
            issue = default;
            if (package == null)
            {
                issue = CreateIssue(
                    IssuePackageMissing,
                    "Imported character package is required before resolving weapon trace geometry.",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    equipmentSlotId,
                    explicitTraceProfileId,
                    0);
                return false;
            }

            if ((explicitTraceProfileId > 0))
                return TryResolveExplicitTrace(package, equipmentSlotId, explicitTraceProfileId, out binding, out issue);

            if ((equipmentSlotId <= 0))
            {
                issue = CreateIssue(
                    IssueEquipmentSlotMissing,
                    "Slot-first weapon trace resolution requires equipmentSlotId. Explicit traceProfileId is only supported for legacy data.",
                    "actions",
                    "actions/hitTrace",
                    "equipmentSlotId",
                    equipmentSlotId,
                    0,
                    0);
                return false;
            }

            CharacterImportedGeometryBinding geometry = package.Geometry;
            CharacterWeaponAttachmentRuntimeBinding attachment = FindAttachmentForSlot(geometry, equipmentSlotId);
            if (attachment == null)
            {
                CharacterWeaponTraceRuntimeBinding fallbackTrace = FindUniqueTraceForSlot(
                    geometry,
                    equipmentSlotId,
                    out int fallbackCount);
                if (fallbackTrace != null && fallbackCount == 1)
                {
                    binding = CreateBinding(fallbackTrace, null, equipmentSlotId, false);
                    return true;
                }

                issue = fallbackCount > 1
                    ? CreateIssue(
                        IssueTraceAmbiguous,
                        "Multiple weapon traces match this equipment slot, but no weapon attachment selects one.",
                        "geometry/traces.json",
                        "geometry/traces/" + equipmentSlotId,
                        "equipSlot",
                        equipmentSlotId,
                        0,
                        0)
                    : CreateIssue(
                        IssueAttachmentMissing,
                        "No weapon attachment is bound to the requested equipment slot.",
                        "geometry/weapon_attachments.json",
                        "geometry/weaponAttachments/" + equipmentSlotId,
                        "equipSlot",
                        equipmentSlotId,
                        0,
                        0);
                return false;
            }

            if ((attachment.TraceId > 0))
            {
                CharacterWeaponTraceRuntimeBinding selectedTrace = CharacterWeaponTraceBakeCatalogBuilder.FindTraceBinding(package, attachment.TraceId);
                if (selectedTrace == null)
                {
                    issue = CreateIssue(
                        IssueTraceMissing,
                        "Weapon attachment references a trace that does not exist in geometry/traces.",
                        "geometry/weapon_attachments.json",
                        AttachmentObjectPath(attachment, equipmentSlotId),
                        "traceId",
                        equipmentSlotId,
                        attachment.TraceId,
                        attachment.WeaponId);
                    return false;
                }

                if (!TraceMatchesAttachment(selectedTrace, attachment, equipmentSlotId, out string mismatchField))
                {
                    issue = CreateIssue(
                        IssueTraceMismatch,
                        "Weapon attachment traceId points at trace geometry for a different weapon or equipment slot.",
                        "geometry/traces.json",
                        TraceObjectPath(selectedTrace, attachment.TraceId),
                        mismatchField,
                        equipmentSlotId,
                        selectedTrace.TraceId,
                        attachment.WeaponId);
                    return false;
                }

                binding = CreateBinding(selectedTrace, attachment, equipmentSlotId, false);
                return true;
            }

            CharacterWeaponTraceRuntimeBinding trace = FindUniqueTraceForAttachment(
                geometry,
                attachment,
                out int matchCount);
            if (trace != null && matchCount == 1)
            {
                binding = CreateBinding(trace, attachment, equipmentSlotId, false);
                return true;
            }

            issue = matchCount > 1
                ? CreateIssue(
                    IssueTraceAmbiguous,
                    "Multiple weapon traces match this weapon/slot. Set weapon_attachments.traceId to select the blade geometry explicitly.",
                    "geometry/traces.json",
                    "geometry/traces/" + attachment.WeaponId,
                    "traceId",
                    equipmentSlotId,
                    0,
                    attachment.WeaponId)
                : CreateIssue(
                    IssueTraceMissing,
                    "No weapon trace geometry matches this weapon attachment.",
                    "geometry/traces.json",
                    "geometry/traces/" + attachment.WeaponId,
                    "weaponId",
                    equipmentSlotId,
                    0,
                    attachment.WeaponId);
            return false;
        }

        private static bool TryResolveExplicitTrace(
            CharacterImportedPackage package,
            int equipmentSlotId,
            int explicitTraceProfileId,
            out CharacterResolvedWeaponTraceBinding binding,
            out CharacterWeaponTraceResolveIssue issue)
        {
            binding = default;
            issue = default;
            CharacterWeaponTraceRuntimeBinding trace = CharacterWeaponTraceBakeCatalogBuilder.FindTraceBinding(package, explicitTraceProfileId);
            if (trace == null)
            {
                issue = CreateIssue(
                    IssueTraceMissing,
                    "Legacy traceProfileId references a trace that does not exist in geometry/traces.",
                    "geometry/traces.json",
                    "geometry/traces/" + explicitTraceProfileId,
                    "traceId",
                    equipmentSlotId,
                    explicitTraceProfileId,
                    0);
                return false;
            }

            CharacterWeaponAttachmentRuntimeBinding attachment = FindAttachmentForTrace(package.Geometry, trace, equipmentSlotId);
            binding = CreateBinding(trace, attachment, equipmentSlotId, true);
            return true;
        }

        private static CharacterResolvedWeaponTraceBinding CreateBinding(
            CharacterWeaponTraceRuntimeBinding trace,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            int requestedEquipmentSlotId,
            bool usedLegacyTraceProfileId)
        {
            int equipmentSlotId = (requestedEquipmentSlotId > 0)
                ? requestedEquipmentSlotId
                : (attachment?.EquipSlot > 0)
                    ? attachment.EquipSlot
                    : trace?.EquipSlot ?? 0;
            int weaponId = (attachment?.WeaponId > 0)
                ? attachment.WeaponId
                : trace?.WeaponId ?? 0;
            int startSocketId = attachment?.TraceStartSocketId ?? trace?.SocketId ?? 0;
            int endSocketId = attachment?.TraceEndSocketId ?? trace?.SocketId ?? 0;
            float radius = trace?.Radius ?? 0f;
            string sampleRule = trace?.SampleRule ?? string.Empty;

            return new CharacterResolvedWeaponTraceBinding(
                trace?.TraceId ?? 0,
                equipmentSlotId,
                weaponId,
                startSocketId,
                endSocketId,
                ComputeTraceLength(trace),
                radius,
                trace?.ProximalExtension ?? 0f,
                trace?.ProximalRadius ?? 0f,
                sampleRule,
                trace,
                attachment,
                usedLegacyTraceProfileId);
        }

        private static CharacterWeaponTraceRuntimeBinding FindUniqueTraceForAttachment(
            CharacterImportedGeometryBinding geometry,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            out int matchCount)
        {
            matchCount = 0;
            CharacterWeaponTraceRuntimeBinding match = null;
            CharacterWeaponTraceRuntimeBinding[] traces = geometry?.WeaponTraces
                ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            int weaponId = attachment?.WeaponId ?? 0;
            int slotId = attachment?.EquipSlot ?? 0;
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (trace == null || (trace.TraceId <= 0))
                    continue;
                if ((weaponId > 0) &&
                    !(trace.WeaponId == weaponId))
                    continue;
                if (slotId > 0
                    && (trace.EquipSlot > 0)
                    && !(trace.EquipSlot == slotId))
                    continue;

                match = trace;
                matchCount++;
            }

            return matchCount == 1 ? match : null;
        }

        private static CharacterWeaponTraceRuntimeBinding FindUniqueTraceForSlot(
            CharacterImportedGeometryBinding geometry,
            int equipmentSlotId,
            out int matchCount)
        {
            matchCount = 0;
            CharacterWeaponTraceRuntimeBinding match = null;
            CharacterWeaponTraceRuntimeBinding[] traces = geometry?.WeaponTraces
                ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (trace == null || (trace.TraceId <= 0))
                    continue;
                if (!(trace.EquipSlot == equipmentSlotId))
                    continue;

                match = trace;
                matchCount++;
            }

            return matchCount == 1 ? match : null;
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachmentForSlot(
            CharacterImportedGeometryBinding geometry,
            int equipmentSlotId)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments = geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment != null && (attachment.EquipSlot == equipmentSlotId))
                    return attachment;
            }

            return null;
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachmentForTrace(
            CharacterImportedGeometryBinding geometry,
            CharacterWeaponTraceRuntimeBinding trace,
            int equipmentSlotId)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments = geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            CharacterWeaponAttachmentRuntimeBinding fallback = null;
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null)
                    continue;
                if ((attachment.TraceId == trace.TraceId))
                    return attachment;
                if (fallback == null
                    && ((equipmentSlotId <= 0)
                        || (attachment.EquipSlot == equipmentSlotId))
                    && ((trace.WeaponId <= 0)
                        || (attachment.WeaponId == trace.WeaponId)))
                {
                    fallback = attachment;
                }
            }

            return fallback;
        }

        private static bool TraceMatchesAttachment(
            CharacterWeaponTraceRuntimeBinding trace,
            CharacterWeaponAttachmentRuntimeBinding attachment,
            int equipmentSlotId,
            out string mismatchField)
        {
            mismatchField = string.Empty;
            if ((trace?.WeaponId > 0)
                && (attachment?.WeaponId > 0)
                && !(trace.WeaponId == attachment.WeaponId))
            {
                mismatchField = "weaponId";
                return false;
            }

            int expectedSlot = (equipmentSlotId > 0)
                ? equipmentSlotId
                : attachment?.EquipSlot ?? 0;
            if ((trace?.EquipSlot > 0)
                && expectedSlot > 0
                && !(trace.EquipSlot == expectedSlot))
            {
                mismatchField = "equipSlot";
                return false;
            }

            return true;
        }

        private static float ComputeTraceLength(CharacterWeaponTraceRuntimeBinding trace)
        {
            if (trace == null)
                return 0f;

            float x = trace.EndPose.Position.X - trace.StartPose.Position.X;
            float y = trace.EndPose.Position.Y - trace.StartPose.Position.Y;
            float z = trace.EndPose.Position.Z - trace.StartPose.Position.Z;
            return (float)Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private static CharacterWeaponTraceResolveIssue CreateIssue(
            string code,
            string message,
            string sourcePath,
            string sourceObjectPath,
            string field,
            int equipmentSlotId,
            int traceId,
            int weaponId)
        {
            return new CharacterWeaponTraceResolveIssue(
                code,
                message,
                sourcePath,
                sourceObjectPath,
                field,
                equipmentSlotId,
                traceId,
                weaponId);
        }

        private static string AttachmentObjectPath(CharacterWeaponAttachmentRuntimeBinding attachment, int fallbackSlotId)
        {
            string id = attachment?.WeaponLabel ?? fallbackSlotId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "geometry/weaponAttachments/" + id;
        }

        private static string TraceObjectPath(CharacterWeaponTraceRuntimeBinding trace, int fallbackTraceId)
        {
            string id = trace?.TraceLabel ?? fallbackTraceId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "geometry/traces/" + id;
        }
    }

    public static class CharacterWeaponTraceBakeCatalogBuilder
    {
        public const string IssuePackageMissing = "WeaponTraceBakePackageMissing";
        public const string IssueRequestMissingClip = "WeaponTraceBakeRequestMissingClip";
        public const string IssueRequestMissingTrace = "WeaponTraceBakeRequestMissingTrace";
        public const string IssueInvalidFrameRange = "WeaponTraceBakeInvalidFrameRange";
        public const string IssueTraceBindingMissing = "WeaponTraceBakeTraceBindingMissing";
        public const string IssueFrameMissing = "WeaponTraceBakeFrameMissing";

        public static CharacterWeaponTraceBakeCatalog Build(
            CharacterImportedPackage package,
            IReadOnlyList<CharacterWeaponTraceBakeRequest> requests,
            out CharacterWeaponTraceBakeBuildReport report)
        {
            var catalog = new CharacterWeaponTraceBakeCatalog();
            var issues = new List<CharacterWeaponTraceBakeBuildIssue>();
            if (package == null)
            {
                issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                    IssuePackageMissing,
                    "Imported character package is required before weapon trace bake.",
                    CharacterActionId.None,
                    string.Empty,
                    0,
                    -1,
                    "package_cache/import_report.json",
                    "package"));
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, issues.ToArray());
                return catalog;
            }

            CharacterWeaponTraceBakeRequest[] safeRequests = CopyRequests(requests);
            int artifactCount = 0;
            int checkedFrameCount = 0;
            for (int i = 0; i < safeRequests.Length; i++)
            {
                CharacterWeaponTraceBakeRequest request = safeRequests[i];
                if (!ValidateRequest(request, issues))
                    continue;

                CharacterWeaponTraceRuntimeBinding traceBinding = FindTraceBinding(package, request.TraceId);
                if (traceBinding == null)
                {
                    issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                        IssueTraceBindingMissing,
                        "Trace binding was not found in the imported character geometry.",
                        request.ActionId,
                        request.ClipKey,
                        request.TraceId,
                        -1,
                        "geometry/traces.json",
                        "geometry/traces/" + (request.TraceId)));
                    continue;
                }

                var options = new CharacterWeaponTraceBakeSampleOptions
                {
                    ProfileId = "character.weaponTraceBake."
                        + StableSuffix(request.ActionId.Value.ToString())
                        + "."
                        + StableSuffix(request.ClipKey)
                        + "."
                        + StableSuffix(traceBinding.TraceLabel),
                    SourceClipHash = string.IsNullOrWhiteSpace(request.SourceClipHash)
                        ? "generated:" + request.ActionId.Value
                        : request.SourceClipHash,
                    SkeletonProfileId = package.PackageId,
                    SkeletonProfileHash = CreateGeometryFingerprint(traceBinding),
                    MaxFrame = Math.Max(request.MaxFrame, request.ActiveEndFrame),
                    ActiveStartFrame = request.ActiveStartFrame,
                    ActiveEndFrame = request.ActiveEndFrame,
                };

                catalog.RegisterArtifact(
                    request.ClipKey,
                    request.TraceId,
                    CharacterWeaponTraceBakeArtifactBuilder.CreateAuthoredEndpointArtifact(
                        request.ClipKey,
                        request.TraceId,
                        request.TraceId,
                        traceBinding,
                        options));
                artifactCount++;
                for (int frame = request.ActiveStartFrame; frame <= request.ActiveEndFrame; frame++)
                {
                    checkedFrameCount++;
                    if (catalog.TryGetFrame(request.ClipKey, request.TraceId, frame, out _))
                        continue;

                    issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                        IssueFrameMissing,
                        "Baked weapon trace frame is missing for an active hit frame.",
                        request.ActionId,
                        request.ClipKey,
                        request.TraceId,
                        frame,
                        "generated/weapon_trace_bake_catalog.json",
                        "bakes/" + request.ActionId.Value + "/" + (request.TraceId)));
                }
            }

            report = new CharacterWeaponTraceBakeBuildReport(
                safeRequests.Length,
                artifactCount,
                checkedFrameCount,
                issues.ToArray());
            return catalog;
        }

        public static CharacterWeaponTraceRuntimeBinding FindTraceBinding(
            CharacterImportedPackage package,
            int traceId)
        {
            CharacterWeaponTraceRuntimeBinding[] traces = package?.Geometry?.WeaponTraces
                ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (trace != null && (trace.TraceId == traceId))
                    return trace;
            }

            return null;
        }

        public static int ResolveTraceIdForEquipmentSlot(
            CharacterImportedPackage package,
            int equipmentSlotId,
            int explicitTraceId)
        {
            return CharacterWeaponTraceBindingResolver.TryResolve(
                package,
                equipmentSlotId,
                explicitTraceId,
                out CharacterResolvedWeaponTraceBinding binding,
                out _)
                    ? binding.TraceId
                    : 0;
        }

        private static CharacterWeaponTraceBakeRequest[] CopyRequests(
            IReadOnlyList<CharacterWeaponTraceBakeRequest> requests)
        {
            if (requests == null || requests.Count == 0)
                return Array.Empty<CharacterWeaponTraceBakeRequest>();

            var copy = new CharacterWeaponTraceBakeRequest[requests.Count];
            for (int i = 0; i < requests.Count; i++)
                copy[i] = requests[i];
            return copy;
        }

        private static bool ValidateRequest(
            CharacterWeaponTraceBakeRequest request,
            List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            if (request == null)
                return false;

            bool valid = true;
            if (string.IsNullOrWhiteSpace(request.ClipKey))
            {
                issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                    IssueRequestMissingClip,
                    "Weapon trace bake request has no animation clip key.",
                    request.ActionId,
                    request.ClipKey,
                    request.TraceId,
                    -1,
                    "config/character_actions.json",
                    "actions/" + request.ActionId.Value));
                valid = false;
            }

            if ((request.TraceId <= 0))
            {
                issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                    IssueRequestMissingTrace,
                    "Weapon trace bake request has no trace id.",
                    request.ActionId,
                    request.ClipKey,
                    request.TraceId,
                    -1,
                    "geometry/traces.json",
                    "geometry/traces"));
                valid = false;
            }

            if (request.ActiveStartFrame < 0
                || request.ActiveEndFrame < request.ActiveStartFrame
                || request.MaxFrame < request.ActiveEndFrame)
            {
                issues.Add(new CharacterWeaponTraceBakeBuildIssue(
                    IssueInvalidFrameRange,
                    "Weapon trace bake request has an invalid active frame range.",
                    request.ActionId,
                    request.ClipKey,
                    request.TraceId,
                    request.ActiveStartFrame,
                    "config/character_actions.json",
                    "actions/" + request.ActionId.Value + "/combatTrack"));
                valid = false;
            }

            return valid;
        }

        private static int StablePositiveId(string value)
        {
            unchecked
            {
                int hash = 17;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                    hash = (hash * 31) + text[i];
                return hash & 0x7fffffff;
            }
        }

        private static string StableSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                chars[i] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars);
        }

        private static string CreateGeometryFingerprint(CharacterWeaponTraceRuntimeBinding traceBinding)
        {
            return "trace="
                + (traceBinding.TraceId)
                + "|weapon="
                + (traceBinding.WeaponId)
                + "|slot="
                + (traceBinding.EquipSlot)
                + "|start="
                + (traceBinding.StartLocatorPath ?? string.Empty)
                + "|end="
                + (traceBinding.EndLocatorPath ?? string.Empty)
                + "|proximalExtension="
                + traceBinding.ProximalExtension.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|proximalRadius="
                + traceBinding.ProximalRadius.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
