using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxCompositionMigrationReceiptView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.composition-migration-receipt.v1";
        [JsonProperty("draftId")] public string DraftId = string.Empty;
        [JsonProperty("sourceSchemaVersion")] public string SourceSchemaVersion = string.Empty;
        [JsonProperty("targetSchemaVersion")] public string TargetSchemaVersion = VfxCompositionDraftView.CurrentSchemaVersion;
        [JsonProperty("sourceBytesHash")] public string SourceBytesHash = string.Empty;
        [JsonProperty("targetDraftHash")] public string TargetDraftHash = string.Empty;
        [JsonProperty("backupPath")] public string BackupPath = string.Empty;
        [JsonProperty("convertedLayers")] public List<string> ConvertedLayers = new List<string>();
        [JsonProperty("blockedLayers")] public List<string> BlockedLayers = new List<string>();
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
    }

    /// <summary>Performs byte-backed, all-or-nothing v2 to v3 composition migration.</summary>
    /// <remarks>Editor-only, AllocByDesign. It never rewrites Unity serialized assets.</remarks>
    internal sealed class VfxCompositionV3MigrationService
    {
        internal const string ReceiptRoot = "Temp/MxFramework/VfxSemantic/Migrations";

        public bool TryMigrateFile(
            string path,
            VfxSourceElementCatalog catalog,
            out VfxCompositionDraftView migrated,
            out VfxCompositionMigrationReceiptView receipt,
            out string message)
        {
            migrated = null;
            receipt = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                message = "v2 草稿文件不存在。";
                return false;
            }

            byte[] sourceBytes;
            JObject sourceObject;
            try
            {
                sourceBytes = File.ReadAllBytes(path);
                sourceObject = JObject.Parse(Encoding.UTF8.GetString(sourceBytes));
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "v2 草稿读取失败：" + exception.Message;
                return false;
            }
            if (sourceObject.Value<string>("schemaVersion") != VfxCompositionDraftView.PreviousSchemaVersion)
            {
                message = "只支持从 mx.vfx.composition-draft.v2 迁移。";
                return false;
            }

            VfxCompositionDraftView draft = sourceObject.ToObject<VfxCompositionDraftView>(JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            if (draft == null)
            {
                message = "v2 草稿内容为空。";
                return false;
            }
            receipt = new VfxCompositionMigrationReceiptView
            {
                DraftId = draft.DraftId,
                SourceSchemaVersion = draft.SchemaVersion,
                SourceBytesHash = VfxCanonicalJson.HashUtf8(Convert.ToBase64String(sourceBytes))
            };

            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                if (VfxCompositionDraftService.HasNonDefaultLegacyMultipliers(layer))
                    receipt.BlockedLayers.Add(layer.LayerId);
            }
            if (receipt.BlockedLayers.Count > 0)
            {
                message = "迁移已阻断：以下 Layer 含有非默认 v2 粗倍率，必须人工转换为语义属性：" + string.Join("、", receipt.BlockedLayers);
                return false;
            }

            draft.SchemaVersion = VfxCompositionDraftView.CurrentSchemaVersion;
            draft.FramesPerSecond = 60;
            VfxSemanticPropertyRegistryView registry = new VfxSemanticPropertyRegistryService().Load(out _);
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out _);
            draft.Dependencies = new VfxDraftDependencySetView
            {
                CatalogHash = draft.CatalogHash,
                SourceIndexHash = draft.SourceIndexHash,
                CurationHash = draft.CurationHash,
                PropertyRegistryHash = registry?.RegistryHash ?? string.Empty,
                ProfileManifestHash = manifest?.ManifestHash ?? string.Empty
            };
            draft.PhaseDurationOverrides = ReadLegacyPhaseOverrides(sourceObject["phaseDurationOverrides"] as JObject);

            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                layer.StartFrame = Math.Max(0, (int)Math.Round(layer.StartOffsetSeconds * 60f, MidpointRounding.AwayFromZero));
                layer.StartOffsetSeconds = 0f;
                VfxSourceElementView element = catalog?.Find(layer.ElementId);
                int estimated = EstimateSourceFrames(catalog, element);
                layer.EstimatedSourceFrames = estimated;
                VfxTimeSegmentsView segments = SuggestSegments(estimated, element?.Lifecycle == "Loop");
                layer.Timing = new VfxLayerTimingView
                {
                    RetimeMode = nameof(VfxRetimeMode.ExtendBody),
                    SourceSegments = segments,
                    TargetArtisticFrames = segments.ArtisticFrames
                };
                layer.Source = new VfxLayerSourceReferenceView
                {
                    Kind = string.IsNullOrWhiteSpace(layer.PrimitiveId) ? "Element" : "Primitive",
                    StableId = string.IsNullOrWhiteSpace(layer.PrimitiveId) ? layer.ElementId : layer.PrimitiveId
                };
                layer.Transform = new VfxLayerTransformView
                {
                    Position = Copy(layer.LocalPosition, 0f),
                    RotationEuler = Copy(layer.LocalEulerAngles, 0f),
                    Scale = Copy(layer.LocalScale, 1f)
                };
                layer.ProfileDependencies = new List<VfxProfileDependencyView>();
                layer.SemanticOverrides = new List<VfxSemanticOverrideView>();
                layer.MemberParticipation = new List<VfxMemberParticipationView>();
                layer.MemberOverrides = new List<VfxMemberSemanticOverrideView>();
                receipt.ConvertedLayers.Add(layer.LayerId);
            }
            VfxCompositionDraftService.NormalizeV3(draft, forSave: true);
            string targetJson = JsonConvert.SerializeObject(draft, Formatting.Indented) + Environment.NewLine;
            VfxCompositionDraftView verification = JsonConvert.DeserializeObject<VfxCompositionDraftView>(targetJson, VfxCompositionDraftService.JsonSettings);
            if (verification == null || verification.SchemaVersion != VfxCompositionDraftView.CurrentSchemaVersion)
            {
                message = "v3 迁移结果 roundtrip 验证失败。";
                return false;
            }

            using var transaction = new VfxCompositionMigrationFileTransaction(
                path,
                ReceiptRoot,
                draft.DraftId,
                receipt.SourceBytesHash);
            receipt.BackupPath = transaction.BackupPath;
            receipt.TargetDraftHash = VfxCanonicalJson.Hash(draft);
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            if (!transaction.Publish(sourceBytes, targetJson, receipt, out message))
                return false;
            migrated = draft;
            message = "v2 草稿已事务迁移为 v3；原始字节与迁移回执位于 " + ReceiptRoot + "。";
            return true;
        }

        private static VfxCompositionPhaseDurationOverridesView ReadLegacyPhaseOverrides(JObject source)
        {
            var result = new VfxCompositionPhaseDurationOverridesView();
            if (source == null) return result;
            result.BuildFrames = NullableFrame(source.Value<int?>("buildFrames"));
            result.RebuildFrames = NullableFrame(source.Value<int?>("rebuildFrames"));
            result.LoopFrames = NullableFrame(source.Value<int?>("loopFrames"));
            result.CompleteFrames = NullableFrame(source.Value<int?>("completeFrames"));
            result.DissipateFrames = NullableFrame(source.Value<int?>("dissipateFrames"));
            return result;
        }

        private static int? NullableFrame(int? value) => value.HasValue && value.Value >= 0 ? value : null;

        private static int EstimateSourceFrames(VfxSourceElementCatalog catalog, VfxSourceElementView element)
        {
            if (catalog == null || element == null) return 60;
            IReadOnlyList<VfxSourceComponentView> members = catalog.Members(element);
            float duration = members.Count == 0 ? 1f : members.Max(value => Math.Max(0.05f, value.DurationSeconds));
            return Math.Max(1, (int)Math.Ceiling(duration * 60f));
        }

        private static VfxTimeSegmentsView SuggestSegments(int totalFrames, bool loop)
        {
            int entry = Math.Min(12, Math.Max(0, totalFrames / 6));
            int exit = loop ? Math.Min(18, Math.Max(0, totalFrames / 5)) : 0;
            int body = Math.Max(1, totalFrames - entry - exit);
            return new VfxTimeSegmentsView
            {
                EntryFrames = entry,
                BodyFrames = body,
                ExitFrames = exit,
                CleanupFrames = Math.Min(30, Math.Max(0, totalFrames / 3)),
                MinimumBodyFrames = 1,
                CycleFrames = loop ? Math.Max(1, body) : 0
            };
        }

        private static List<float> Copy(List<float> source, float fallback)
        {
            return source != null && source.Count == 3
                ? new List<float> { source[0], source[1], source[2] }
                : new List<float> { fallback, fallback, fallback };
        }

    }

    internal sealed class VfxCompositionMigrationFileTransaction : IDisposable
    {
        private readonly string _sourcePath;
        private readonly string _sourceTemporaryPath;
        private readonly string _sourceReplaceBackupPath;
        private readonly string _backupTemporaryPath;
        private readonly string _receiptTemporaryPath;
        private readonly Action<string, byte[]> _backupWriter;
        private readonly Action<string, string> _receiptWriter;
        private readonly Action<string, string, string> _sourceReplacer;
        private readonly Func<string, bool> _sourceValidator;
        private readonly List<string> _createdDirectories;
        private bool _sourceReplaceAttempted;
        private bool _committed;

        internal VfxCompositionMigrationFileTransaction(
            string sourcePath,
            string receiptRoot,
            string draftId,
            string sourceBytesHash,
            Action<string, byte[]> backupWriter = null,
            Action<string, string> receiptWriter = null,
            Action<string, string, string> sourceReplacer = null,
            Func<string, bool> sourceValidator = null)
        {
            _sourcePath = sourcePath;
            string evidenceToken = VfxCompositionDraftService.FileToken(draftId)
                + "." + HashToken(sourceBytesHash)
                + "." + Guid.NewGuid().ToString("N");
            _createdDirectories = FindMissingDirectories(receiptRoot);
            BackupPath = receiptRoot + "/" + evidenceToken + ".v2.bytes";
            ReceiptPath = receiptRoot + "/" + evidenceToken + ".receipt.json";
            _backupTemporaryPath = receiptRoot + "/." + evidenceToken + ".v2.bytes.tmp";
            _receiptTemporaryPath = receiptRoot + "/." + evidenceToken + ".receipt.json.tmp";
            string sourceToken = Guid.NewGuid().ToString("N");
            _sourceTemporaryPath = sourcePath + "." + sourceToken + ".v3.tmp";
            _sourceReplaceBackupPath = sourcePath + "." + sourceToken + ".v2.replace-backup";
            _backupWriter = backupWriter ?? File.WriteAllBytes;
            _receiptWriter = receiptWriter ?? WriteText;
            _sourceReplacer = sourceReplacer ?? ReplaceSource;
            _sourceValidator = sourceValidator ?? (_ => true);
        }

        internal string BackupPath { get; }
        internal string ReceiptPath { get; }

        internal bool Publish(
            byte[] sourceBytes,
            string targetJson,
            VfxCompositionMigrationReceiptView receipt,
            out string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BackupPath) ?? string.Empty);
                Prepare(sourceBytes, targetJson, receipt);
                File.Move(_backupTemporaryPath, BackupPath);
                _sourceReplaceAttempted = true;
                _sourceReplacer(_sourceTemporaryPath, _sourcePath, _sourceReplaceBackupPath);
                ValidateSourceAndBackup(sourceBytes, receipt);
                if (!_sourceValidator(_sourcePath))
                    throw new InvalidDataException("Injected v3 source validation rejected the replaced draft.");
                File.Move(_receiptTemporaryPath, ReceiptPath);
                ValidateCommittedEvidence(sourceBytes, receipt);
                _committed = true;
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    RollBack(sourceBytes);
                    message = "v3 migration publish failed and restored the original draft: " + exception.Message;
                }
                catch (Exception rollbackException)
                {
                    message = "v3 migration publish failed: " + exception.Message
                        + " Migration rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        private void Prepare(
            byte[] sourceBytes,
            string targetJson,
            VfxCompositionMigrationReceiptView receipt)
        {
            _backupWriter(_backupTemporaryPath, sourceBytes);
            if (!File.ReadAllBytes(_backupTemporaryPath).SequenceEqual(sourceBytes)
                || !string.Equals(HashSourceBytes(File.ReadAllBytes(_backupTemporaryPath)), receipt.SourceBytesHash, StringComparison.Ordinal))
                throw new InvalidDataException("Migration backup temp bytes/hash did not match the source draft.");

            File.WriteAllText(_sourceTemporaryPath, targetJson, new UTF8Encoding(false));
            VfxCompositionDraftView target = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                File.ReadAllText(_sourceTemporaryPath),
                VfxCompositionDraftService.JsonSettings);
            if (target == null
                || target.SchemaVersion != VfxCompositionDraftView.CurrentSchemaVersion
                || !string.Equals(VfxCanonicalJson.Hash(target), receipt.TargetDraftHash, StringComparison.Ordinal))
                throw new InvalidDataException("Migration v3 temp roundtrip/hash validation failed.");

            string receiptJson = JsonConvert.SerializeObject(receipt, Formatting.Indented) + Environment.NewLine;
            _receiptWriter(_receiptTemporaryPath, receiptJson);
            VfxCompositionMigrationReceiptView roundTrip = JsonConvert.DeserializeObject<VfxCompositionMigrationReceiptView>(
                File.ReadAllText(_receiptTemporaryPath));
            if (roundTrip == null
                || !string.Equals(roundTrip.ReceiptHash, VfxCanonicalJson.Hash(roundTrip, "receiptHash"), StringComparison.Ordinal)
                || !string.Equals(roundTrip.BackupPath, BackupPath, StringComparison.Ordinal)
                || !string.Equals(roundTrip.SourceBytesHash, receipt.SourceBytesHash, StringComparison.Ordinal)
                || !string.Equals(roundTrip.TargetDraftHash, receipt.TargetDraftHash, StringComparison.Ordinal))
                throw new InvalidDataException("Migration receipt temp roundtrip/hash validation failed.");
        }

        private void ValidateSourceAndBackup(byte[] sourceBytes, VfxCompositionMigrationReceiptView receipt)
        {
            VfxCompositionDraftView migrated = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                File.ReadAllText(_sourcePath),
                VfxCompositionDraftService.JsonSettings);
            if (migrated == null
                || migrated.SchemaVersion != VfxCompositionDraftView.CurrentSchemaVersion
                || !string.Equals(VfxCanonicalJson.Hash(migrated), receipt.TargetDraftHash, StringComparison.Ordinal))
                throw new InvalidDataException("Replaced v3 source roundtrip/hash validation failed.");
            byte[] backup = File.ReadAllBytes(BackupPath);
            if (!backup.SequenceEqual(sourceBytes)
                || !string.Equals(HashSourceBytes(backup), receipt.SourceBytesHash, StringComparison.Ordinal))
                throw new InvalidDataException("Committed migration backup bytes/hash validation failed.");
        }

        private void ValidateCommittedEvidence(byte[] sourceBytes, VfxCompositionMigrationReceiptView receipt)
        {
            ValidateSourceAndBackup(sourceBytes, receipt);
            VfxCompositionMigrationReceiptView sealedReceipt = JsonConvert.DeserializeObject<VfxCompositionMigrationReceiptView>(
                File.ReadAllText(ReceiptPath));
            VfxCompositionDraftView migrated = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                File.ReadAllText(_sourcePath),
                VfxCompositionDraftService.JsonSettings);
            if (sealedReceipt == null
                || migrated == null
                || !string.Equals(sealedReceipt.ReceiptHash, VfxCanonicalJson.Hash(sealedReceipt, "receiptHash"), StringComparison.Ordinal)
                || !string.Equals(sealedReceipt.BackupPath, BackupPath, StringComparison.Ordinal)
                || !string.Equals(sealedReceipt.SourceBytesHash, HashSourceBytes(File.ReadAllBytes(BackupPath)), StringComparison.Ordinal)
                || !string.Equals(sealedReceipt.TargetDraftHash, VfxCanonicalJson.Hash(migrated), StringComparison.Ordinal))
                throw new InvalidDataException("Committed migration receipt/backup cross-validation failed.");
        }

        private void RollBack(byte[] sourceBytes)
        {
            var failures = new List<Exception>();
            try
            {
                bool restored = false;
                if (_sourceReplaceAttempted && File.Exists(_sourceReplaceBackupPath) && File.Exists(_sourcePath))
                {
                    try
                    {
                        File.Replace(_sourceReplaceBackupPath, _sourcePath, null, true);
                        restored = true;
                    }
                    catch (Exception)
                    {
                        // Fall back to the captured source bytes below.
                    }
                }
                if (!restored)
                    File.WriteAllBytes(_sourcePath, sourceBytes);
                if (!File.ReadAllBytes(_sourcePath).SequenceEqual(sourceBytes))
                    throw new IOException("Restored v2 source bytes did not match the original draft.");
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            TryCleanup(BackupPath, failures);
            TryCleanup(ReceiptPath, failures);
            TryCleanup(_backupTemporaryPath, failures);
            TryCleanup(_receiptTemporaryPath, failures);
            TryCleanup(_sourceTemporaryPath, failures);
            TryCleanup(_sourceReplaceBackupPath, failures);
            try { DeleteCreatedEmptyDirectories(); }
            catch (Exception exception) { failures.Add(exception); }
            _sourceReplaceAttempted = false;
            if (failures.Count > 0)
                throw new AggregateException("Migration rollback or cleanup did not complete.", failures);
        }

        private static void ReplaceSource(string temporaryPath, string sourcePath, string backupPath)
        {
            File.Replace(temporaryPath, sourcePath, backupPath, true);
        }

        private static void WriteText(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static string HashSourceBytes(byte[] bytes)
        {
            return VfxCanonicalJson.HashUtf8(Convert.ToBase64String(bytes));
        }

        private static string HashToken(string hash)
        {
            string value = new string((hash ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
            return value.Length > 12 ? value.Substring(value.Length - 12) : value;
        }

        private static List<string> FindMissingDirectories(string directory)
        {
            var result = new List<string>();
            string current = directory;
            while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
            {
                result.Add(current);
                current = Path.GetDirectoryName(current);
            }
            return result;
        }

        private void DeleteCreatedEmptyDirectories()
        {
            for (int i = 0; i < _createdDirectories.Count; i++)
            {
                string directory = _createdDirectories[i];
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                    Directory.Delete(directory);
            }
        }

        private static void TryCleanup(string path, ICollection<Exception> failures)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        public void Dispose()
        {
            if (File.Exists(_backupTemporaryPath)) File.Delete(_backupTemporaryPath);
            if (File.Exists(_receiptTemporaryPath)) File.Delete(_receiptTemporaryPath);
            if (File.Exists(_sourceTemporaryPath)) File.Delete(_sourceTemporaryPath);
            if (File.Exists(_sourceReplaceBackupPath)) File.Delete(_sourceReplaceBackupPath);
            if (!_committed) DeleteCreatedEmptyDirectories();
        }
    }
}
