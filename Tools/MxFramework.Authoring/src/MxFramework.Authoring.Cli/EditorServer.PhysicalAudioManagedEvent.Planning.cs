using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;


internal static partial class EditorServer
{
    /// <remarks>LowFreqAlloc. Reads Authoring manifests and builds a deterministic, hash-guarded FMOD mutation plan.</remarks>
    internal static ManagedPhysicalEventOperation BuildManagedPhysicalEventOperation(
        string rootPath,
        string packageRelative,
        ManagedPhysicalEventApiRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new ManagedPhysicalEventApiRequest();
        string semanticPath = ResolveAudioSemanticManifestPath(rootPath);
        string physicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        string currentSemanticHash = ComputeOptionalFileHash(semanticPath);
        string currentPhysicalHash = ComputeOptionalFileHash(physicalPath);
        RequireExpectedHash(
            currentSemanticHash,
            request.expectedSemanticHash,
            "AUTH_AUDIO_MANAGED_SEMANTIC_CONFLICT",
            "audio_semantic_manifest.json 已变化，请刷新后重试。");
        RequireExpectedHash(
            currentPhysicalHash,
            request.expectedPhysicalHash,
            "AUTH_AUDIO_MANAGED_PHYSICAL_CONFLICT",
            "audio_physical_manifest.json 已变化，请刷新后重试。");

        RequireSafeManagedIdentifier(request.familyId, "familyId");
        RequireSafeManagedIdentifier(request.basisId, "basisId");
        PhysicalAudioAuthoringManifestDocument physical = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        PhysicalAudioFamilyAuthoring family = PhysicalAudioAuthoringManifest.FindFamily(physical, request.familyId);
        PhysicalAudioBasisSoundAuthoring basis = PhysicalAudioAuthoringManifest.FindBasis(physical, request.basisId);
        if (family == null)
            throw ManagedPhysicalEventException.Invalid("AUTH_AUDIO_MANAGED_FAMILY_MISSING", "指定的 Physical Audio Family 不存在。");
        PhysicalAudioMaterialPairAuthoring materialPair = string.IsNullOrWhiteSpace(request.materialPairId)
            ? null
            : PhysicalAudioAuthoringManifest.FindMaterialPair(physical, request.materialPairId);
        if (!string.IsNullOrWhiteSpace(request.materialPairId) && materialPair == null)
            throw ManagedPhysicalEventException.Invalid("AUTH_AUDIO_MANAGED_PAIR_MISSING", "指定的公共 Material Pair 不存在。");
        string variantKind = string.IsNullOrWhiteSpace(request.variantKind)
            ? family.VariantKind
            : request.variantKind;
        if (!string.Equals(variantKind, family.VariantKind, StringComparison.Ordinal))
            throw ManagedPhysicalEventException.Invalid("AUTH_AUDIO_MANAGED_VARIANT_MISMATCH", "动作 Variant 与 Physical Audio Family 不匹配。");
        if (basis == null && request.createBasis)
            basis = CreateManagedPhysicalBasis(physical, family, materialPair, variantKind, request.basisId, request.displayName);
        if (basis == null || !string.Equals(basis.FamilyId, family.Id, StringComparison.Ordinal))
            throw ManagedPhysicalEventException.Invalid("AUTH_AUDIO_MANAGED_BASIS_MISSING", "指定的 Basis 不存在或不属于该 Family。");
        if (materialPair != null
            && (!string.Equals(basis.MaterialPairId, materialPair.Id, StringComparison.Ordinal)
                || !string.Equals(basis.VariantKind, variantKind, StringComparison.Ordinal)))
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_PAIR_VARIANT_CONFLICT",
                "现有 Basis 已绑定其他公共 Pair 或 Variant；拒绝静默改绑。",
                409);

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            snapshotPath,
            jsonOptions);
        AudioSemanticManifestDocument semantic = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, semanticPath, jsonOptions),
            snapshot);

        AudioEventAuthoringDefinition template = ResolveManagedPhysicalEventTemplate(
            semantic,
            physical,
            family,
            request.templateEventDefinitionId);
        if (template == null)
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_TEMPLATE_MISSING",
                "找不到可用于复制 Bus、3D 和距离设置的 Event Definition 模板。");
        AuthoringFmodAudioLibraryEvent templateSnapshot = AudioSemanticManifestValidator.FindEvent(
            snapshot,
            template.FmodEventPath,
            template.FmodEventGuid);
        if (templateSnapshot == null)
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_TEMPLATE_STALE",
                "模板 Event 不在当前 FMOD snapshot 中，请先刷新 Snapshot。",
                409);
        if (templateSnapshot.IsLoop)
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_TEMPLATE_LOOP_UNSUPPORTED",
                "Physical Basis Sound 第一阶段只允许 OneShot Event，不能使用 Loop 模板。");

        AudioBusAuthoringDefinition bus = semantic.Buses.Find(item => item != null
            && string.Equals(item.Id, template.BusId, StringComparison.Ordinal));
        if (bus == null || !IsSafeFmodLookupPath(bus.FmodBusPath, "bus:/"))
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_BUS_INVALID",
                "模板 Event 的 Bus binding 不存在或不是安全的 FMOD bus path。");
        string bankName = templateSnapshot.Banks?.Find(item => string.Equals(item, "Master", StringComparison.Ordinal))
            ?? templateSnapshot.Banks?.FirstOrDefault();
        if (!IsSafeFmodBankName(bankName))
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_BANK_INVALID",
                "模板 Event 没有可安全复用的 FMOD Bank。");

        string eventPath = BuildManagedPhysicalEventPath(family.Id, basis.Id);
        string eventDefinitionId = BuildManagedPhysicalSemanticId("audio.event.generated.physical", family.Id, basis.Id);
        string cueId = BuildManagedPhysicalSemanticId("audio.cue.generated.physical", family.Id, basis.Id);
        AudioCueAuthoringDefinition cue = ResolveManagedPhysicalCue(semantic, basis, cueId, eventPath);
        AudioEventAuthoringDefinition audioEvent = cue == null
            ? null
            : AudioAuthoringManifestV2.FindEventDefinition(semantic, cue.EventDefinitionId);
        AudioEventAuthoringDefinition idCollision = AudioAuthoringManifestV2.FindEventDefinition(semantic, eventDefinitionId);
        AudioEventAuthoringDefinition pathCollision = semantic.EventDefinitions.Find(item => item != null
            && string.Equals(item.FmodEventPath, eventPath, StringComparison.Ordinal));
        AuthoringFmodAudioLibraryEvent snapshotPathCollision = AudioSemanticManifestValidator.FindEvent(
            snapshot,
            eventPath,
            string.Empty);

        if (audioEvent != null && !string.Equals(audioEvent.FmodEventPath, eventPath, StringComparison.Ordinal))
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_EXTERNAL_BINDING_REJECTED",
                "Basis 已绑定到受控命名空间之外的 Event；Managed 创建不会覆盖外部 Event。",
                409);
        if (idCollision != null && audioEvent != idCollision)
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_EVENT_ID_COLLISION",
                "派生的 Event Definition ID 已被其他 binding 使用。",
                409);
        if (pathCollision != null && audioEvent != pathCollision)
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_EVENT_PATH_COLLISION",
                "受控 FMOD Event path 已被其他 Event Definition 使用。",
                409);
        if (snapshotPathCollision != null
            && (audioEvent == null
                || !IsGuid(audioEvent.FmodEventGuid)
                || !string.Equals(snapshotPathCollision.Guid, audioEvent.FmodEventGuid, StringComparison.OrdinalIgnoreCase)))
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_SNAPSHOT_PATH_COLLISION",
                "受控 FMOD Event path 已存在，但没有当前 Semantic GUID 所有权；拒绝接管既存 Event。",
                409);

        string displayName = NormalizeManagedDisplayName(request.displayName, basis.DisplayName, basis.Id);
        if (audioEvent == null)
        {
            audioEvent = new AudioEventAuthoringDefinition
            {
                Id = eventDefinitionId,
                RuntimeEventId = AudioAuthoringManifestV2.AllocateEventId(semantic),
                FmodEventPath = eventPath
            };
            semantic.EventDefinitions.Add(audioEvent);
        }
        ApplyManagedPhysicalEventTemplate(audioEvent, template, displayName, family.Id, basis.Id);

        if (cue == null)
        {
            cue = new AudioCueAuthoringDefinition
            {
                AudioCueId = cueId,
                RuntimeCueId = AudioAuthoringManifestV2.AllocateCueId(semantic),
                Lifecycle = AudioCueLifecycles.Draft
            };
            semantic.Cues.Add(cue);
        }
        ApplyManagedPhysicalCueTemplate(cue, audioEvent, displayName, family, basis);
        basis.AudioCueId = cue.AudioCueId;
        basis.AuditionBindingHash = string.Empty;
        physical.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        AudioAuthoringManifestV2.Normalize(semantic, null);

        var fmodRequest = new FmodManagedPhysicalEventRequestV1
        {
            RequestId = Guid.NewGuid().ToString("N"),
            FamilyId = family.Id,
            BasisId = basis.Id,
            EventPath = eventPath,
            ExpectedEventGuid = audioEvent.FmodEventGuid,
            BankName = bankName,
            BankPath = "bank:/" + bankName,
            BusPath = bus.FmodBusPath,
            Is3D = templateSnapshot.Is3D,
            MinDistance = templateSnapshot.MinDistance,
            MaxDistance = templateSnapshot.MaxDistance,
            CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        fmodRequest.RequestHash = ComputeObjectSha256(fmodRequest, jsonOptions);
        return new ManagedPhysicalEventOperation
        {
            SemanticPath = semanticPath,
            PhysicalPath = physicalPath,
            SnapshotPath = snapshotPath,
            ExpectedSemanticHash = currentSemanticHash,
            ExpectedPhysicalHash = currentPhysicalHash,
            SemanticManifest = semantic,
            PhysicalManifest = physical,
            Cue = cue,
            AudioEvent = audioEvent,
            Basis = basis,
            FmodRequest = fmodRequest
        };
    }


    private static AudioEventAuthoringDefinition ResolveManagedPhysicalEventTemplate(
        AudioSemanticManifestDocument semantic,
        PhysicalAudioAuthoringManifestDocument physical,
        PhysicalAudioFamilyAuthoring family,
        string requestedTemplateId)
    {
        if (!string.IsNullOrWhiteSpace(requestedTemplateId))
            return AudioAuthoringManifestV2.FindEventDefinition(semantic, requestedTemplateId.Trim());
        PhysicalAudioBasisSoundAuthoring fallback = PhysicalAudioAuthoringManifest.FindBasis(
            physical,
            family.FallbackBasisId);
        AudioCueAuthoringDefinition fallbackCue = fallback == null
            ? null
            : AudioAuthoringManifestV2.FindCue(semantic, fallback.AudioCueId);
        if (fallbackCue != null)
            return AudioAuthoringManifestV2.FindEventDefinition(semantic, fallbackCue.EventDefinitionId);
        for (int i = 0; i < physical.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring candidate = physical.BasisSounds[i];
            if (candidate == null || !string.Equals(candidate.FamilyId, family.Id, StringComparison.Ordinal))
                continue;
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semantic, candidate.AudioCueId);
            AudioEventAuthoringDefinition audioEvent = cue == null
                ? null
                : AudioAuthoringManifestV2.FindEventDefinition(semantic, cue.EventDefinitionId);
            if (audioEvent != null)
                return audioEvent;
        }
        return null;
    }

    private static PhysicalAudioBasisSoundAuthoring CreateManagedPhysicalBasis(
        PhysicalAudioAuthoringManifestDocument manifest,
        PhysicalAudioFamilyAuthoring family,
        PhysicalAudioMaterialPairAuthoring materialPair,
        string variantKind,
        string basisId,
        string displayName)
    {
        int runtimeBasisId = AllocateManagedPhysicalBasisId(manifest);
        PhysicalAudioBasisSoundAuthoring fallback = PhysicalAudioAuthoringManifest.FindBasis(
            manifest,
            family.FallbackBasisId);
        var basis = new PhysicalAudioBasisSoundAuthoring
        {
            Id = basisId,
            RuntimeBasisId = runtimeBasisId,
            DisplayName = NormalizeManagedDisplayName(displayName, basisId),
            FamilyId = family.Id,
            MaterialPairId = materialPair?.Id ?? string.Empty,
            VariantKind = variantKind ?? family.VariantKind,
            AudioCueId = string.Empty,
            AdoptionSetId = string.Empty,
            Impactor = CloneManagedBodyAnchor(fallback?.Impactor),
            Receiver = CloneManagedBodyAnchor(fallback?.Receiver),
            RelativeSpeed = CloneManagedRange(fallback?.RelativeSpeed),
            Intensity = CloneManagedRange(fallback?.Intensity),
            AngleDegrees = fallback?.AngleDegrees == null
                ? new PhysicalAudioRangeAnchor { Min = 0f, Max = 180f, Anchor = 90f }
                : CloneManagedRange(fallback.AngleDegrees),
            Priority = fallback?.Priority ?? 0,
            Lifecycle = PhysicalAudioLifecycles.Draft,
            Metadata = new Dictionary<string, string>
            {
                ["managedPhysical"] = "true",
                ["managedPhysicalFamilyId"] = family.Id
            }
        };
        if (materialPair != null)
        {
            basis.SideA = CloneManagedBodyAnchor(basis.Impactor);
            basis.SideA.MaterialId = materialPair.MaterialAId;
            basis.SideB = CloneManagedBodyAnchor(basis.Receiver);
            basis.SideB.MaterialId = materialPair.MaterialBId;
            basis.Impactor = CloneManagedBodyAnchor(basis.SideA);
            basis.Receiver = CloneManagedBodyAnchor(basis.SideB);
            basis.Metadata["managedPhysicalMaterialPairId"] = materialPair.Id;
            basis.Metadata["managedPhysicalVariantKind"] = basis.VariantKind;
        }
        manifest.BasisSounds.Add(basis);
        return basis;
    }

    private static int AllocateManagedPhysicalBasisId(PhysicalAudioAuthoringManifestDocument manifest)
    {
        manifest.IdAllocation ??= new PhysicalAudioIdAllocationState();
        manifest.IdAllocation.RetiredBasisIds ??= new List<int>();
        int value = manifest.IdAllocation.NextBasisId <= 0
            ? 530001
            : manifest.IdAllocation.NextBasisId;
        while (manifest.IdAllocation.RetiredBasisIds.Contains(value)
            || manifest.BasisSounds.Exists(item => item != null && item.RuntimeBasisId == value))
            value++;
        if (value > AudioAuthoringManifestV2.MaximumRuntimeId)
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_BASIS_ID_EXHAUSTED",
                "Physical Audio Basis runtime ID 空间已耗尽。");
        manifest.IdAllocation.NextBasisId = value + 1;
        return value;
    }

    private static PhysicalAudioBodyAnchorAuthoring CloneManagedBodyAnchor(PhysicalAudioBodyAnchorAuthoring source) =>
        source == null
            ? new PhysicalAudioBodyAnchorAuthoring
            {
                MaterialId = "audio.material.generic_solid",
                Mass = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f },
                ContactArea = new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f },
                Surface = "Normal",
                Construction = "Solid"
            }
            : new PhysicalAudioBodyAnchorAuthoring
            {
                MaterialId = source.MaterialId,
                Mass = CloneManagedRange(source.Mass),
                ContactArea = CloneManagedRange(source.ContactArea),
                Surface = source.Surface,
                Construction = source.Construction
            };

    private static PhysicalAudioRangeAnchor CloneManagedRange(PhysicalAudioRangeAnchor source) =>
        source == null
            ? new PhysicalAudioRangeAnchor { Min = 0f, Max = 1f, Anchor = 0.5f }
            : new PhysicalAudioRangeAnchor
            {
                Min = source.Min,
                Max = source.Max,
                Anchor = source.Anchor
            };

    private static AudioCueAuthoringDefinition ResolveManagedPhysicalCue(
        AudioSemanticManifestDocument semantic,
        PhysicalAudioBasisSoundAuthoring basis,
        string deterministicCueId,
        string expectedEventPath)
    {
        AudioCueAuthoringDefinition cue = string.IsNullOrWhiteSpace(basis.AudioCueId)
            ? AudioAuthoringManifestV2.FindCue(semantic, deterministicCueId)
            : AudioAuthoringManifestV2.FindCue(semantic, basis.AudioCueId);
        if (!string.IsNullOrWhiteSpace(basis.AudioCueId) && cue == null)
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_BASIS_CUE_MISSING",
                "Basis 已声明 Audio Cue，但该 Cue 不存在；拒绝静默改绑。",
                409);
        if (cue == null)
            return null;
        AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(
            semantic,
            cue.EventDefinitionId);
        if (audioEvent == null || !string.Equals(audioEvent.FmodEventPath, expectedEventPath, StringComparison.Ordinal))
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_EXTERNAL_BINDING_REJECTED",
                "现有 Cue 未绑定到该 Basis 的受控 FMOD Event path。",
                409);
        string markedBasis = cue.Metadata != null
            && cue.Metadata.TryGetValue("managedPhysicalBasisId", out string value)
                ? value
                : string.Empty;
        if (!string.IsNullOrEmpty(markedBasis) && !string.Equals(markedBasis, basis.Id, StringComparison.Ordinal))
            throw ManagedPhysicalEventException.Conflict(
                "AUTH_AUDIO_MANAGED_CUE_OWNERSHIP_MISMATCH",
                "现有 Cue 已由其他 Basis 管理。",
                409);
        return cue;
    }

    private static void ApplyManagedPhysicalEventTemplate(
        AudioEventAuthoringDefinition target,
        AudioEventAuthoringDefinition template,
        string displayName,
        string familyId,
        string basisId)
    {
        target.DisplayName = displayName;
        target.FmodEventPath = BuildManagedPhysicalEventPath(familyId, basisId);
        target.BusId = template.BusId;
        target.Kind = "Event";
        target.Is3D = template.Is3D;
        target.IsLoop = false;
        target.MinDistance = template.MinDistance;
        target.MaxDistance = template.MaxDistance;
        target.Metadata ??= new Dictionary<string, string>();
        target.Metadata["managedPhysical"] = "true";
        target.Metadata["managedPhysicalFamilyId"] = familyId;
        target.Metadata["managedPhysicalBasisId"] = basisId;
    }

    private static void ApplyManagedPhysicalCueTemplate(
        AudioCueAuthoringDefinition cue,
        AudioEventAuthoringDefinition audioEvent,
        string displayName,
        PhysicalAudioFamilyAuthoring family,
        PhysicalAudioBasisSoundAuthoring basis)
    {
        cue.DisplayName = displayName;
        cue.Purpose = "物理交互 Basis Sound：" + displayName;
        cue.Context = string.IsNullOrWhiteSpace(family.DisplayName) ? family.InteractionKind : family.DisplayName;
        cue.EventDefinitionId = audioEvent.Id;
        cue.PlayMode = "OneShot";
        cue.MissingPolicy = "Mute";
        cue.Tags = new List<string> { "physical", "basis", "managed", family.InteractionKind };
        cue.AuditionEvidence = new AudioCueAuditionEvidence { Status = AudioSemanticAuditionStatuses.Needed };
        cue.Metadata ??= new Dictionary<string, string>();
        cue.Metadata["managedPhysical"] = "true";
        cue.Metadata["managedPhysicalFamilyId"] = family.Id;
        cue.Metadata["managedPhysicalBasisId"] = basis.Id;
    }

    internal static string BuildManagedPhysicalEventPath(string familyId, string basisId)
    {
        RequireSafeManagedIdentifier(familyId, "familyId");
        RequireSafeManagedIdentifier(basisId, "basisId");
        return ManagedPhysicalEventPathPrefix + familyId + "/" + basisId;
    }

    internal static bool IsManagedPhysicalEventPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.StartsWith(ManagedPhysicalEventPathPrefix, StringComparison.Ordinal))
            return false;
        string suffix = path.Substring(ManagedPhysicalEventPathPrefix.Length);
        string[] parts = suffix.Split('/');
        return parts.Length == 2
            && IsSafeManagedIdentifier(parts[0])
            && IsSafeManagedIdentifier(parts[1]);
    }

    private static string BuildManagedPhysicalSemanticId(string prefix, string familyId, string basisId)
    {
        string value = prefix + "." + familyId + "." + basisId;
        if (value.Length > 240)
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_ID_TOO_LONG",
                "Family/Basis ID 组合过长，无法创建稳定 Semantic ID。");
        return value;
    }

    private static void RequireSafeManagedIdentifier(string value, string field)
    {
        if (!IsSafeManagedIdentifier(value))
            throw ManagedPhysicalEventException.Invalid(
                "AUTH_AUDIO_MANAGED_ID_INVALID",
                field + " 只允许 1-96 个 ASCII 字母、数字、点、下划线或连字符，且必须以字母或数字开头。");
    }

    internal static bool IsSafeManagedIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96 || !IsAsciiAlphaNumeric(value[0]))
            return false;
        for (int i = 1; i < value.Length; i++)
        {
            char c = value[i];
            if (!IsAsciiAlphaNumeric(c) && c != '.' && c != '_' && c != '-')
                return false;
        }
        return true;
    }

    private static bool IsAsciiAlphaNumeric(char c) =>
        (c >= 'a' && c <= 'z')
        || (c >= 'A' && c <= 'Z')
        || (c >= '0' && c <= '9');

    private static bool IsSafeFmodLookupPath(string path, string prefix)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        for (int i = prefix.Length; i < path.Length; i++)
        {
            char c = path[i];
            if (!IsAsciiAlphaNumeric(c) && c != '/' && c != ' ' && c != '.' && c != '_' && c != '-')
                return false;
        }
        return true;
    }

    private static bool IsSafeFmodBankName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
            return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!IsAsciiAlphaNumeric(c) && c != ' ' && c != '.' && c != '_' && c != '-')
                return false;
        }
        return true;
    }

    private static string NormalizeManagedDisplayName(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i]?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            var builder = new StringBuilder(Math.Min(value.Length, 120));
            for (int p = 0; p < value.Length && builder.Length < 120; p++)
                if (!char.IsControl(value[p])) builder.Append(value[p]);
            if (builder.Length > 0)
                return builder.ToString();
        }
        return "Managed Physical Basis";
    }

    private static bool IsGuid(string value) => Guid.TryParse(value?.Trim('{', '}'), out _);

    private static void RequireExpectedHash(string current, string expected, string code, string message)
    {
        string normalized = CharacterPackageHashUtility.NormalizeSha256(expected ?? string.Empty);
        if (!string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase))
            throw ManagedPhysicalEventException.Conflict(code, message, 409);
    }

    private static void RequireCurrentHash(string path, string expected, string code)
    {
        string current = ComputeOptionalFileHash(path);
        if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            throw ManagedPhysicalEventException.Conflict(
                code,
                "创建期间 manifest 已被其他操作修改，拒绝提交并回滚 FMOD。",
                409);
    }
}
