using System.Globalization;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static AudioWorkbenchSaveResponse SaveAudioWorkbenchDraft(
        string rootPath,
        string packageRelative,
        AudioWorkbenchManifestRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request?.expectedHash ?? string.Empty);
        if (File.Exists(manifestPath) && !string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchSaveResponse.Conflict(currentHash, expectedHash);
        string generationPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        string currentGenerationHash = ComputeOptionalFileHash(generationPath);
        if (request?.generationManifest != null)
        {
            string expectedGenerationHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedGenerationHash ?? string.Empty);
            if (File.Exists(generationPath) && !string.Equals(currentGenerationHash, expectedGenerationHash, StringComparison.OrdinalIgnoreCase))
                return AudioWorkbenchSaveResponse.GenerationConflict(currentGenerationHash, expectedGenerationHash);
        }
        string physicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        string currentPhysicalHash = ComputeOptionalFileHash(physicalPath);
        if (request?.physicalManifest != null)
        {
            string expectedPhysicalHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedPhysicalHash ?? string.Empty);
            if (File.Exists(physicalPath) && !string.Equals(currentPhysicalHash, expectedPhysicalHash, StringComparison.OrdinalIgnoreCase))
                return AudioWorkbenchSaveResponse.PhysicalConflict(currentPhysicalHash, expectedPhysicalHash);
        }

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId),
            jsonOptions);
        AudioSemanticManifestDocument manifest = AudioAuthoringManifestV2.Upgrade(request?.manifest, snapshot);
        AudioSemanticManifestDocument previousManifest = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions),
            snapshot);
        manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        AudioAuthoringManifestV2.Normalize(manifest, snapshot);
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        AddAudioPreviewEvidenceDiagnostics(rootPath, manifest, validation, jsonOptions);
        AddAudioAdoptionDiagnostics(rootPath, manifest, validation, jsonOptions);
        AddAudioCueDeletionDiagnostics(rootPath, packageRelative, previousManifest, manifest, validation, jsonOptions);
        if (validation.Diagnostics.Exists(item => item != null && string.Equals(item.Code, "AUTH_AUDIO_CUE_DELETE_BLOCKED", StringComparison.Ordinal)))
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_CUE_DELETE_BLOCKED",
                message = "永久删除被阻止：Cue 必须已归档且全项目零引用。",
                currentHash = currentHash,
                manifest = manifest,
                validation = validation
            };
        }
        AudioGenerationManifestDocument generationManifest = null;
        if (request?.generationManifest != null)
        {
            AudioGenerationManifestDocument previousGeneration = UpgradeGenerationManifest(
                ReadOptionalJsonFile<AudioGenerationManifestDocument>(rootPath, generationPath, jsonOptions));
            generationManifest = UpgradeGenerationManifest(request.generationManifest);
            RecordRecipeHistory(previousGeneration, generationManifest);
            generationManifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
        PhysicalAudioAuthoringManifestDocument physicalManifest = null;
        PhysicalAudioValidationReport physicalValidation = null;
        if (request?.physicalManifest != null)
        {
            physicalManifest = PhysicalAudioAuthoringManifest.Upgrade(request.physicalManifest);
            PhysicalAudioAuthoringManifestDocument previousPhysicalManifest = File.Exists(physicalPath)
                ? ReadPhysicalAudioManifest(rootPath, jsonOptions)
                : null;
            PhysicalAudioAuthoringManifest.RetireRemovedIds(previousPhysicalManifest, physicalManifest);
            physicalManifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            physicalValidation = PhysicalAudioAuthoringManifest.Validate(physicalManifest, manifest);
            AddPhysicalAudioAdoptionDiagnostics(
                physicalManifest,
                manifest,
                generationManifest ?? ReadGenerationManifest(rootPath, jsonOptions),
                physicalValidation);
            PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(
                physicalManifest,
                manifest,
                snapshot,
                physicalValidation);
        }
        var draftWrites = new List<AudioPublicationWrite>
        {
            AudioPublicationWrite.Create(manifestPath, manifest, jsonOptions)
        };
        if (generationManifest != null)
            draftWrites.Add(AudioPublicationWrite.Create(generationPath, generationManifest, jsonOptions));
        if (physicalManifest != null)
            draftWrites.Add(AudioPublicationWrite.Create(physicalPath, physicalManifest, jsonOptions));
        try
        {
            WriteAudioPublicationAtomically(draftWrites);
        }
        catch (Exception exception)
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 500,
                code = "AUTH_AUDIO_SAVE_WRITE_FAILED",
                message = "音频创作草稿原子写入失败：" + exception.Message,
                currentHash = ComputeOptionalFileHash(manifestPath),
                generationManifestHash = ComputeOptionalFileHash(generationPath),
                physicalManifestHash = ComputeOptionalFileHash(physicalPath),
                manifest = manifest,
                validation = validation,
                generationManifest = generationManifest,
                physicalManifest = physicalManifest,
                physicalValidation = physicalValidation
            };
        }
        InvalidateAuthoringResourceCache(rootPath);
        return new AudioWorkbenchSaveResponse
        {
            success = true,
            httpStatus = 200,
            message = validation.HasErrors ? "草稿已保存；发布门禁仍有待处理项。" : "音频创作草稿已保存。",
            currentHash = ComputeOptionalFileHash(manifestPath),
            generationManifestHash = ComputeOptionalFileHash(generationPath),
            generationManifest = generationManifest,
            physicalManifestHash = ComputeOptionalFileHash(physicalPath),
            physicalManifest = physicalManifest,
            physicalValidation = physicalValidation,
            manifest = manifest,
            validation = validation
        };
    }

    internal static AudioWorkbenchSaveResponse BulkApproveAllAudioCues(
        string rootPath,
        string packageRelative,
        AudioWorkbenchBulkApprovalRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request?.expectedHash ?? string.Empty);
        if (!File.Exists(manifestPath) || !string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchSaveResponse.Conflict(currentHash, expectedHash);

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId),
            jsonOptions);
        if (snapshot == null || snapshot.CacheStale)
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_BULK_APPROVAL_SNAPSHOT_STALE",
                message = "全部批准需要当前 FMOD snapshot。请先刷新 FMOD Snapshot。",
                currentHash = currentHash
            };
        }

        AudioSemanticManifestDocument manifest = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions),
            snapshot);
        PhysicalAudioAuthoringManifestDocument physicalManifest = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        AudioGenerationManifestDocument generationManifest = ReadGenerationManifest(rootPath, jsonOptions);
        string snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot);
        string reviewedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        string batchId = "bulk-" + Guid.NewGuid().ToString("N");
        int approvedCount = 0;

        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = manifest.Cues[i];
            if (cue == null || string.Equals(cue.Lifecycle, AudioCueLifecycles.Archived, StringComparison.Ordinal))
                continue;
            AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(manifest, cue.EventDefinitionId);
            if (audioEvent == null
                || AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid) == null)
            {
                return new AudioWorkbenchSaveResponse
                {
                    success = false,
                    httpStatus = 400,
                    code = "AUTH_AUDIO_BULK_APPROVAL_CUE_INVALID",
                    message = "全部批准被阻止：Cue 无法解析当前 FMOD Event：" + (cue.AudioCueId ?? "<null>"),
                    currentHash = currentHash,
                    manifest = manifest
                };
            }

            PhysicalAudioBasisSoundAuthoring basis = physicalManifest?.BasisSounds?.Find(item =>
                item != null && string.Equals(item.AudioCueId, cue.AudioCueId, StringComparison.Ordinal));
            AudioAdoptionEventStateV3 eventState = generationManifest?.EventStates?.Find(item =>
                item != null && string.Equals(item.EventDefinitionId, cue.EventDefinitionId, StringComparison.Ordinal));
            string appliedSetId = !string.IsNullOrWhiteSpace(basis?.AdoptionSetId)
                ? basis.AdoptionSetId
                : eventState?.ActiveSetId ?? string.Empty;
            AudioAdoptionSetV3 appliedSet = generationManifest?.AdoptionSets?.Find(item =>
                item != null && string.Equals(item.SetId, appliedSetId, StringComparison.Ordinal));
            cue.Lifecycle = AudioCueLifecycles.Active;
            cue.AuditionEvidence = new AudioCueAuditionEvidence
            {
                Status = AudioSemanticAuditionStatuses.Approved,
                ReviewMode = AudioCueAuditionReviewModes.BulkOperatorApproval,
                PreviewRequestId = batchId + "-" + i.ToString(CultureInfo.InvariantCulture),
                EventBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, audioEvent),
                SnapshotHash = snapshotHash,
                AppliedSetId = appliedSetId,
                BankHash = appliedSet?.BankHash ?? string.Empty,
                AudioBasisId = basis?.Id ?? string.Empty,
                RuntimeBasisId = basis?.RuntimeBasisId ?? 0,
                PhysicalManifestHash = ComputeOptionalFileHash(ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath)),
                ReviewedAtUtc = reviewedAtUtc
            };
            approvedCount++;
        }

        manifest.UpdatedAtUtc = reviewedAtUtc;
        AudioAuthoringManifestV2.Normalize(manifest, snapshot);
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        AddAudioAdoptionDiagnostics(rootPath, manifest, validation, jsonOptions);
        try
        {
            WriteAudioPublicationAtomically(new List<AudioPublicationWrite>
            {
                AudioPublicationWrite.Create(manifestPath, manifest, jsonOptions)
            });
        }
        catch (Exception exception)
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 500,
                code = "AUTH_AUDIO_BULK_APPROVAL_WRITE_FAILED",
                message = "全部批准写入失败：" + exception.Message,
                currentHash = ComputeOptionalFileHash(manifestPath),
                manifest = manifest,
                validation = validation
            };
        }

        InvalidateAuthoringResourceCache(rootPath);
        return new AudioWorkbenchSaveResponse
        {
            success = true,
            httpStatus = 200,
            message = "已批量批准 " + approvedCount.ToString(CultureInfo.InvariantCulture) + " 个 Cue。",
            currentHash = ComputeOptionalFileHash(manifestPath),
            generationManifestHash = ComputeOptionalFileHash(ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath)),
            physicalManifestHash = ComputeOptionalFileHash(ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath)),
            manifest = manifest,
            validation = validation
        };
    }

    internal static AudioWorkbenchSaveResponse DirectAdoptCurrentAudioForAllBasis(
        string rootPath,
        string packageRelative,
        AudioWorkbenchDirectAdoptionRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        string generationPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        string physicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string currentGenerationHash = ComputeOptionalFileHash(generationPath);
        string currentPhysicalHash = ComputeOptionalFileHash(physicalPath);
        if (!File.Exists(manifestPath)
            || !string.Equals(currentHash, CharacterPackageHashUtility.NormalizeSha256(request?.expectedHash ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchSaveResponse.Conflict(currentHash, request?.expectedHash);
        if (!File.Exists(generationPath)
            || !string.Equals(currentGenerationHash, CharacterPackageHashUtility.NormalizeSha256(request?.expectedGenerationHash ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchSaveResponse.GenerationConflict(currentGenerationHash, request?.expectedGenerationHash);
        if (!File.Exists(physicalPath)
            || !string.Equals(currentPhysicalHash, CharacterPackageHashUtility.NormalizeSha256(request?.expectedPhysicalHash ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchSaveResponse.PhysicalConflict(currentPhysicalHash, request?.expectedPhysicalHash);

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId),
            jsonOptions);
        if (snapshot == null || snapshot.CacheStale)
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_DIRECT_ADOPTION_SNAPSHOT_STALE",
                message = "直接采用需要当前 FMOD snapshot。请先刷新 FMOD Snapshot。",
                currentHash = currentHash
            };
        }

        string buildRoot = ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop");
        string mirrorRoot = ResolveAudioBridgePath(rootPath, "Assets/StreamingAssets");
        string bankHash = ComputeBankSetHash(buildRoot);
        string mirrorHash = ComputeBankSetHash(mirrorRoot);
        if (string.IsNullOrWhiteSpace(bankHash) || !string.Equals(bankHash, mirrorHash, StringComparison.OrdinalIgnoreCase))
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_DIRECT_ADOPTION_BANK_MISMATCH",
                message = "直接采用被阻止：FMOD Build 与 StreamingAssets Bank 不一致。",
                currentHash = currentHash
            };
        }

        AudioSemanticManifestDocument manifest = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions),
            snapshot);
        AudioGenerationManifestDocument generationManifest = ReadGenerationManifest(rootPath, jsonOptions);
        PhysicalAudioAuthoringManifestDocument physicalManifest = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        string snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot);
        string changedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        int adoptedCount = 0;

        for (int i = 0; i < physicalManifest.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring basis = physicalManifest.BasisSounds[i];
            if (basis == null || !string.Equals(basis.Lifecycle, PhysicalAudioLifecycles.Active, StringComparison.Ordinal))
                continue;
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(manifest, basis.AudioCueId);
            AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(manifest, cue?.EventDefinitionId);
            if (cue == null || audioEvent == null
                || AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid) == null)
            {
                return new AudioWorkbenchSaveResponse
                {
                    success = false,
                    httpStatus = 400,
                    code = "AUTH_AUDIO_DIRECT_ADOPTION_BASIS_INVALID",
                    message = "直接采用被阻止：Basis 无法解析当前 FMOD Event：" + (basis.Id ?? "<null>"),
                    currentHash = currentHash
                };
            }

            List<FmodAudioAdoptionSource> sources = ReadOriginalEventSources(rootPath, audioEvent.FmodEventGuid);
            if (sources.Count == 0 && !string.IsNullOrWhiteSpace(cue.SourcePreviewPath))
            {
                string sourcePath = ResolveSafePath(rootPath, cue.SourcePreviewPath);
                if (File.Exists(sourcePath))
                {
                    string sourceHash = CharacterPackageHashUtility.ComputeFileSha256(sourcePath);
                    sources.Add(new FmodAudioAdoptionSource
                    {
                        Layer = "body",
                        Take = 1,
                        ProjectRelativePath = cue.SourcePreviewPath,
                        Sha256 = sourceHash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                            ? sourceHash.Substring(7)
                            : sourceHash
                    });
                }
            }
            string setId = "original-direct-" + ComputeTextSha256(audioEvent.Id).Substring(7, 12);
            AudioAdoptionSetV3 set = generationManifest.AdoptionSets.Find(item =>
                item != null && string.Equals(item.SetId, setId, StringComparison.Ordinal));
            if (set == null)
            {
                set = new AudioAdoptionSetV3 { SetId = setId };
                generationManifest.AdoptionSets.Add(set);
            }
            for (int setIndex = 0; setIndex < generationManifest.AdoptionSets.Count; setIndex++)
            {
                AudioAdoptionSetV3 other = generationManifest.AdoptionSets[setIndex];
                if (other != null
                    && !ReferenceEquals(other, set)
                    && string.Equals(other.EventDefinitionId, audioEvent.Id, StringComparison.Ordinal)
                    && string.Equals(other.Status, "Applied", StringComparison.Ordinal))
                    other.Status = "Superseded";
            }
            set.AudioCueId = cue.AudioCueId;
            set.EventDefinitionId = audioEvent.Id;
            set.BindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, audioEvent);
            set.SourceType = sources.Count == 0 ? "CurrentEventState" : "OriginalBaseline";
            set.Status = "Applied";
            set.Layout = sources.Count == 0
                ? "EmptyEvent"
                : sources.Count == 1 ? "SingleInstrument" : "MultiInstrument";
            set.Sources = sources;
            set.Randomization = new List<FmodAudioAdoptionRandomization>();
            set.LayerMix = new List<FmodAudioAdoptionLayerMix>();
            set.BankHash = bankHash;
            set.SnapshotHash = snapshotHash;
            if (string.IsNullOrWhiteSpace(set.CreatedAtUtc))
                set.CreatedAtUtc = changedAtUtc;
            set.AppliedAtUtc = changedAtUtc;
            set.SetHash = string.Empty;
            set.SetHash = ComputeTextSha256(JsonSerializer.Serialize(set, jsonOptions));

            AudioAdoptionEventStateV3 eventState = generationManifest.EventStates.Find(item =>
                item != null && string.Equals(item.EventDefinitionId, audioEvent.Id, StringComparison.Ordinal));
            if (eventState == null)
            {
                eventState = new AudioAdoptionEventStateV3 { EventDefinitionId = audioEvent.Id };
                generationManifest.EventStates.Add(eventState);
            }
            eventState.ActiveSetId = setId;
            basis.AdoptionSetId = setId;
            cue.Lifecycle = AudioCueLifecycles.Active;
            cue.AuditionEvidence = new AudioCueAuditionEvidence
            {
                Status = AudioSemanticAuditionStatuses.Approved,
                ReviewMode = AudioCueAuditionReviewModes.BulkOperatorApproval,
                PreviewRequestId = "direct-adopt-" + Guid.NewGuid().ToString("N"),
                EventBindingHash = set.BindingHash,
                SnapshotHash = snapshotHash,
                AppliedSetId = setId,
                BankHash = bankHash,
                AudioBasisId = basis.Id,
                RuntimeBasisId = basis.RuntimeBasisId,
                PhysicalManifestHash = currentPhysicalHash,
                ReviewedAtUtc = changedAtUtc
            };
            adoptedCount++;
        }

        for (int i = 0; i < generationManifest.AdoptionSets.Count; i++)
        {
            AudioAdoptionSetV3 applied = generationManifest.AdoptionSets[i];
            if (applied == null || !string.Equals(applied.Status, "Applied", StringComparison.Ordinal))
                continue;
            applied.BankHash = bankHash;
            applied.SnapshotHash = snapshotHash;
        }
        manifest.UpdatedAtUtc = changedAtUtc;
        generationManifest.UpdatedAtUtc = changedAtUtc;
        physicalManifest.UpdatedAtUtc = changedAtUtc;
        try
        {
            WriteAudioPublicationAtomically(new List<AudioPublicationWrite>
            {
                AudioPublicationWrite.Create(manifestPath, manifest, jsonOptions),
                AudioPublicationWrite.Create(generationPath, generationManifest, jsonOptions),
                AudioPublicationWrite.Create(physicalPath, physicalManifest, jsonOptions)
            });
        }
        catch (Exception exception)
        {
            return new AudioWorkbenchSaveResponse
            {
                success = false,
                httpStatus = 500,
                code = "AUTH_AUDIO_DIRECT_ADOPTION_WRITE_FAILED",
                message = "直接采用写入失败：" + exception.Message,
                currentHash = ComputeOptionalFileHash(manifestPath),
                generationManifestHash = ComputeOptionalFileHash(generationPath),
                physicalManifestHash = ComputeOptionalFileHash(physicalPath)
            };
        }

        InvalidateAuthoringResourceCache(rootPath);
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        PhysicalAudioValidationReport physicalValidation = PhysicalAudioAuthoringManifest.Validate(physicalManifest, manifest);
        AddPhysicalAudioAdoptionDiagnostics(physicalManifest, manifest, generationManifest, physicalValidation);
        AddPhysicalAudioAuditionDiagnostics(physicalManifest, manifest, physicalValidation);
        return new AudioWorkbenchSaveResponse
        {
            success = true,
            httpStatus = 200,
            message = "已直接采用并绑定 " + adoptedCount.ToString(CultureInfo.InvariantCulture) + " 个 Active Basis 的当前 FMOD Event。",
            currentHash = ComputeOptionalFileHash(manifestPath),
            generationManifestHash = ComputeOptionalFileHash(generationPath),
            physicalManifestHash = ComputeOptionalFileHash(physicalPath),
            manifest = manifest,
            generationManifest = generationManifest,
            physicalManifest = physicalManifest,
            validation = validation,
            physicalValidation = physicalValidation
        };
    }

    internal static AudioWorkbenchPublishResponse PublishAudioWorkbench(
        string rootPath,
        string packageRelative,
        AudioWorkbenchPublishRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request?.expectedHash ?? string.Empty);
        if (!File.Exists(manifestPath) || !string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchPublishResponse.Conflict(currentHash, expectedHash);
        string generationPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        string currentGenerationHash = ComputeOptionalFileHash(generationPath);
        if (!File.Exists(generationPath))
            return AudioWorkbenchPublishResponse.GenerationMissing();
        string expectedGenerationHash = CharacterPackageHashUtility.NormalizeSha256(request?.expectedGenerationHash ?? string.Empty);
        if (!string.Equals(currentGenerationHash, expectedGenerationHash, StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchPublishResponse.GenerationConflict(currentGenerationHash, expectedGenerationHash);
        string physicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        string currentPhysicalHash = ComputeOptionalFileHash(physicalPath);
        if (!File.Exists(physicalPath))
            return AudioWorkbenchPublishResponse.PhysicalMissing();
        string expectedPhysicalHash = CharacterPackageHashUtility.NormalizeSha256(request?.expectedPhysicalHash ?? string.Empty);
        if (!string.Equals(currentPhysicalHash, expectedPhysicalHash, StringComparison.OrdinalIgnoreCase))
            return AudioWorkbenchPublishResponse.PhysicalConflict(currentPhysicalHash, expectedPhysicalHash);

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        AudioSemanticManifestDocument manifest = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions),
            snapshot);
        bool developmentPublishAll = request?.developmentPublishAll == true;
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        if (!developmentPublishAll)
        {
            AddAudioPreviewEvidenceDiagnostics(rootPath, manifest, validation, jsonOptions);
            AddAudioAdoptionDiagnostics(rootPath, manifest, validation, jsonOptions);
        }
        PhysicalAudioAuthoringManifestDocument physicalManifest = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        PhysicalAudioValidationReport physicalValidation = PhysicalAudioAuthoringManifest.Validate(physicalManifest, manifest);
        if (!developmentPublishAll)
        {
            AddPhysicalAudioAdoptionDiagnostics(
                physicalManifest,
                manifest,
                ReadGenerationManifest(rootPath, jsonOptions),
                physicalValidation);
            AddPhysicalAudioAuditionDiagnostics(
                physicalManifest,
                manifest,
                physicalValidation);
        }
        PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(
            physicalManifest,
            manifest,
            snapshot,
            physicalValidation);
        PhysicalAudioCoverageReport physicalCoverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(physicalManifest);
        if (snapshot == null || snapshot.CacheStale)
        {
            return new AudioWorkbenchPublishResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_PUBLISH_BLOCKED",
                message = "发布被阻止：FMOD snapshot 缺失或已过期。",
                validation = validation
            };
        }
        if (!developmentPublishAll && validation.HasErrors)
        {
            return new AudioWorkbenchPublishResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_PUBLISH_BLOCKED",
                message = "正式发布被阻止：请先解决 Cue/Event、FMOD snapshot 或运行时目录的技术错误。",
                validation = validation
            };
        }
        if (!developmentPublishAll
            && (physicalValidation?.HasErrors == true
                || (physicalCoverage != null
                    && (physicalCoverage.MissingCellCount > 0
                        || physicalCoverage.AmbiguousCellCount > 0))))
        {
            return new AudioWorkbenchPublishResponse
            {
                success = false,
                httpStatus = 400,
                code = "AUTH_AUDIO_PHYSICAL_PUBLISH_BLOCKED",
                message = "发布被阻止：物理音频目录存在无效引用、覆盖空洞或歧义。",
                validation = validation,
                physicalValidation = physicalValidation
            };
        }

        AudioSemanticManifestDocument catalogManifest = manifest;
        if (developmentPublishAll)
        {
            catalogManifest = CreateDevelopmentPublishManifest(
                manifest,
                snapshot,
                jsonOptions,
                out string invalidCueId);
            if (catalogManifest == null)
            {
                return new AudioWorkbenchPublishResponse
                {
                    success = false,
                    httpStatus = 400,
                    code = "AUTH_AUDIO_DEVELOPMENT_PUBLISH_CUE_INVALID",
                    message = "开发发布被阻止：Cue 无法解析当前 FMOD Event 或默认参数：" + invalidCueId,
                    validation = validation,
                    physicalValidation = physicalValidation
                };
            }
        }

        string snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot);
        AudioRuntimeCatalogDocument catalog = AudioAuthoringManifestV2.CompileRuntimeCatalog(catalogManifest, currentHash, snapshotHash);
        string globalCatalogHash = CharacterPackageHashUtility.ComputeTextSha256(JsonSerializer.Serialize(catalog, jsonOptions));
        PhysicalAudioRuntimeCatalogDocument physicalCatalog =
            PhysicalAudioAuthoringManifest.CompileRuntimeCatalog(physicalManifest, catalogManifest, currentPhysicalHash, globalCatalogHash);
        string generatedPath = ResolveAudioBridgePath(rootPath, AudioGeneratedCatalogRelativePath);
        string streamingPath = ResolveAudioBridgePath(rootPath, AudioStreamingCatalogRelativePath);
        int syncedSetCount = 0;
        int outOfSyncSetCount = 0;
        try
        {
            var writes = new List<AudioPublicationWrite>
            {
                AudioPublicationWrite.Create(generatedPath, catalog, jsonOptions),
                AudioPublicationWrite.Create(streamingPath, catalog, jsonOptions)
            };
            writes.Add(AudioPublicationWrite.Create(
                ResolveAudioBridgePath(rootPath, AudioPhysicalGeneratedCatalogRelativePath),
                physicalCatalog,
                jsonOptions));
            writes.Add(AudioPublicationWrite.Create(
                ResolveAudioBridgePath(rootPath, AudioPhysicalStreamingCatalogRelativePath),
                physicalCatalog,
                jsonOptions));
            AudioGenerationManifestDocument generationManifest = ReadGenerationManifest(rootPath, jsonOptions);
            string gameBankHash = ReadCurrentGameBankHash(rootPath);
            string publishedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            bool generationChanged = false;
            foreach (AudioAdoptionEventStateV3 eventState in generationManifest.EventStates)
            {
                if (string.IsNullOrWhiteSpace(eventState.ActiveSetId))
                    continue;
                AudioAdoptionSetV3 set = generationManifest.AdoptionSets.Find(item =>
                    string.Equals(item.SetId, eventState.ActiveSetId, StringComparison.Ordinal));
                if (set == null || !string.Equals(set.Status, "Applied", StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(gameBankHash)
                    || string.IsNullOrWhiteSpace(set.BankHash)
                    || !string.Equals(set.BankHash, gameBankHash, StringComparison.OrdinalIgnoreCase))
                {
                    outOfSyncSetCount++;
                    continue;
                }
                syncedSetCount++;
                if (string.Equals(eventState.PublishedSetId, set.SetId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(set.PublishedAtUtc))
                    continue;
                set.PublishedAtUtc = publishedAt;
                eventState.PublishedSetId = set.SetId;
                generationChanged = true;
            }
            if (generationChanged)
            {
                generationManifest.UpdatedAtUtc = publishedAt;
                writes.Add(AudioPublicationWrite.Create(generationPath, generationManifest, jsonOptions));
            }
            if (TryBuildAudioPackageSubset(rootPath, packageRelative, packageId, catalog, globalCatalogHash, jsonOptions, out string subsetPath, out AudioPackageCueSubsetDocument subset))
                writes.Add(AudioPublicationWrite.Create(subsetPath, subset, jsonOptions));
            WriteAudioPublicationAtomically(writes);
        }
        catch (Exception exception)
        {
            return new AudioWorkbenchPublishResponse
            {
                success = false,
                httpStatus = 500,
                code = "AUTH_AUDIO_PUBLISH_WRITE_FAILED",
                message = "运行时目录写入失败：" + exception.Message,
                validation = validation
            };
        }

        return new AudioWorkbenchPublishResponse
        {
            success = true,
            httpStatus = 200,
            message = "已一键同步到游戏：发布 "
                + catalog.Cues.Count.ToString(CultureInfo.InvariantCulture)
                + " 个 Cue，确认 "
                + syncedSetCount.ToString(CultureInfo.InvariantCulture)
                + " 个 Applied 版本与游戏 Bank 一致"
                + (outOfSyncSetCount > 0
                    ? "；另有 " + outOfSyncSetCount.ToString(CultureInfo.InvariantCulture) + " 个版本的 Bank 尚未同步。"
                    : "。"),
            catalog = catalog,
            catalogHash = ComputeOptionalFileHash(generatedPath),
            generationManifestHash = ComputeOptionalFileHash(generationPath),
            syncedSetCount = syncedSetCount,
            outOfSyncSetCount = outOfSyncSetCount,
            validation = validation,
            physicalCatalog = physicalCatalog,
            physicalCatalogHash = ComputeOptionalFileHash(ResolveAudioBridgePath(rootPath, AudioPhysicalGeneratedCatalogRelativePath)),
            physicalValidation = physicalValidation
        };
    }

    private static AudioSemanticManifestDocument CreateDevelopmentPublishManifest(
        AudioSemanticManifestDocument manifest,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        JsonSerializerOptions jsonOptions,
        out string invalidCueId)
    {
        invalidCueId = string.Empty;
        AudioSemanticManifestDocument result = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(
            JsonSerializer.Serialize(manifest, jsonOptions),
            jsonOptions);
        if (result == null)
            return null;
        for (int i = 0; i < result.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = result.Cues[i];
            if (cue == null || string.Equals(cue.Lifecycle, AudioCueLifecycles.Archived, StringComparison.Ordinal))
                continue;
            AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(result, cue.EventDefinitionId);
            if (audioEvent == null
                || AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid) == null)
            {
                invalidCueId = cue?.AudioCueId ?? "<null>";
                return null;
            }
            cue.Lifecycle = AudioCueLifecycles.Active;
        }
        // Development publication may relax readiness gates, but cannot drop authored defaults.
        AuthoringResourceDiagnostic missingDefault = FindActiveCueDefaultParameterError(result, snapshot);
        if (missingDefault != null)
        {
            invalidCueId = missingDefault.RuntimeResourceKey;
            return null;
        }
        return result;
    }

    private static AuthoringResourceDiagnostic FindActiveCueDefaultParameterError(
        AudioSemanticManifestDocument manifest, AuthoringFmodAudioLibrarySnapshotDocument snapshot)
    {
        return AudioAuthoringManifestV2.Validate(manifest, snapshot).Diagnostics.Find(item =>
            item.Code == AuthoringResourceDiagnosticCodes.AudioCueDefaultParameterMissing
            && AudioAuthoringManifestV2.FindCue(manifest, item.RuntimeResourceKey)?.Lifecycle == AudioCueLifecycles.Active);
    }

    private static bool TryBuildAudioPackageSubset(
        string rootPath,
        string packageRelative,
        string packageId,
        AudioRuntimeCatalogDocument catalog,
        string globalCatalogHash,
        JsonSerializerOptions jsonOptions,
        out string outputPath,
        out AudioPackageCueSubsetDocument subset)
    {
        outputPath = string.Empty;
        subset = null;
        if (string.IsNullOrWhiteSpace(packageRelative))
            return false;
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        if (!Directory.Exists(packagePath))
            return false;
        outputPath = Path.Combine(packagePath, "config", "audio_cue_manifest.json");
        subset = new AudioPackageCueSubsetDocument
        {
            PackageId = packageId,
            GlobalCatalogHash = globalCatalogHash ?? string.Empty,
            Cues = new List<AudioPackageCueSubsetEntry>()
        };
        for (int i = 0; i < catalog.Cues.Count; i++)
        {
            AudioRuntimeCueDefinition cue = catalog.Cues[i];
            if (FindAudioSemanticReferences(rootPath, packageRelative, cue.AudioCueId, jsonOptions).Count == 0)
                continue;
            subset.Cues.Add(new AudioPackageCueSubsetEntry
            {
                AudioCueId = cue.AudioCueId,
                RuntimeCueId = cue.Id,
                RuntimeEventId = cue.EventId,
                EventDefinitionId = cue.EventDefinitionId
            });
        }
        return true;
    }

    private static void WriteAudioPublicationAtomically(List<AudioPublicationWrite> writes)
    {
        string transactionId = Guid.NewGuid().ToString("N");
        var committed = new List<AudioPublicationWrite>();
        try
        {
            for (int i = 0; i < writes.Count; i++)
            {
                AudioPublicationWrite write = writes[i];
                Directory.CreateDirectory(Path.GetDirectoryName(write.path));
                write.tempPath = write.path + ".tmp." + transactionId;
                write.backupPath = write.path + ".bak." + transactionId;
                File.WriteAllText(write.tempPath, write.json);
                if (File.Exists(write.path))
                    File.Copy(write.path, write.backupPath, overwrite: true);
            }
            for (int i = 0; i < writes.Count; i++)
            {
                AudioPublicationWrite write = writes[i];
                File.Move(write.tempPath, write.path, overwrite: true);
                committed.Add(write);
            }
        }
        catch
        {
            for (int i = committed.Count - 1; i >= 0; i--)
            {
                AudioPublicationWrite write = committed[i];
                if (File.Exists(write.backupPath))
                    File.Move(write.backupPath, write.path, overwrite: true);
                else if (File.Exists(write.path))
                    File.Delete(write.path);
            }
            throw;
        }
        finally
        {
            for (int i = 0; i < writes.Count; i++)
            {
                if (File.Exists(writes[i].tempPath))
                    File.Delete(writes[i].tempPath);
                if (File.Exists(writes[i].backupPath))
                    File.Delete(writes[i].backupPath);
            }
        }
    }

    private static void AddAudioCueDeletionDiagnostics(
        string rootPath,
        string packageRelative,
        AudioSemanticManifestDocument previous,
        AudioSemanticManifestDocument next,
        AudioSemanticManifestValidationReport validation,
        JsonSerializerOptions jsonOptions)
    {
        if (previous?.Cues == null || next?.Cues == null || validation == null)
            return;
        next.IdAllocation ??= new AudioIdAllocationState();
        next.IdAllocation.RetiredCueIds ??= new List<int>();
        for (int i = 0; i < previous.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition removed = previous.Cues[i];
            if (removed == null || AudioAuthoringManifestV2.FindCue(next, removed.AudioCueId) != null)
                continue;
            int referenceCount = FindAudioSemanticReferences(rootPath, packageRelative, removed.AudioCueId, jsonOptions).Count;
            if (!string.Equals(removed.Lifecycle, AudioCueLifecycles.Archived, StringComparison.Ordinal) || referenceCount > 0)
            {
                validation.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Code = "AUTH_AUDIO_CUE_DELETE_BLOCKED",
                    RuntimeResourceKey = removed.AudioCueId,
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    SourceConfigKind = "audioSemanticManifest",
                    SourceField = "cues",
                    Message = "Cue deletion requires Archived lifecycle and zero project references. references=" + referenceCount.ToString(CultureInfo.InvariantCulture),
                    SuggestedFix = "先归档 Cue，清理全部引用并保存，再执行永久删除。"
                });
                validation.HasErrors = true;
                continue;
            }
            if (!next.IdAllocation.RetiredCueIds.Contains(removed.RuntimeCueId))
                next.IdAllocation.RetiredCueIds.Add(removed.RuntimeCueId);
        }
    }

}
