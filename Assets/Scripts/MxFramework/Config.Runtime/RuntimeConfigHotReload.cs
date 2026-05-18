using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public sealed class RuntimeConfigHotReloadRequest
    {
        public RuntimeConfigHotReloadRequest(string path, string sourceName = null, string expectedContentHash = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config hot reload path cannot be empty.", nameof(path));

            Path = path;
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? path : sourceName;
            ExpectedContentHash = expectedContentHash ?? string.Empty;
        }

        public string Path { get; }
        public string SourceName { get; }
        public string ExpectedContentHash { get; }
    }

    public sealed class RuntimeConfigHotReloadResult
    {
        public RuntimeConfigHotReloadResult(
            string sourceName,
            string sourceId,
            string contentHash,
            long durationMilliseconds,
            bool success,
            IConfigProvider provider,
            ConfigChangeSet changeSet,
            IReadOnlyList<string> changedTables,
            IReadOnlyList<string> errors)
        {
            SourceName = sourceName ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            DurationMilliseconds = durationMilliseconds < 0L ? 0L : durationMilliseconds;
            Success = success;
            Provider = provider;
            ChangeSet = changeSet ?? new ConfigChangeSet();
            ChangedTables = changedTables != null ? new List<string>(changedTables) : new List<string>();
            Errors = errors != null ? new List<string>(errors) : new List<string>();
        }

        public string SourceName { get; }
        public string SourceId { get; }
        public string ContentHash { get; }
        public long DurationMilliseconds { get; }
        public bool Success { get; }
        public IConfigProvider Provider { get; }
        public ConfigChangeSet ChangeSet { get; }
        public IReadOnlyList<string> ChangedTables { get; }
        public IReadOnlyList<string> Errors { get; }

        public string ErrorSummary
        {
            get
            {
                if (Errors.Count == 0)
                    return string.Empty;

                var builder = new StringBuilder();
                for (int i = 0; i < Errors.Count; i++)
                {
                    if (i > 0)
                        builder.Append('\n');
                    builder.Append(Errors[i]);
                }

                return builder.ToString();
            }
        }
    }

    public sealed class RuntimeConfigPatchHotReloadService
    {
        private readonly IConfigProvider _baseProvider;
        private readonly Func<string, string> _readText;

        public RuntimeConfigPatchHotReloadService(
            IConfigProvider baseProvider,
            Func<string, string> readText = null)
        {
            _baseProvider = baseProvider ?? new MemoryConfigProvider();
            _readText = readText ?? File.ReadAllText;
        }

        public RuntimeConfigHotReloadResult Reload(RuntimeConfigHotReloadRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var stopwatch = Stopwatch.StartNew();
            try
            {
                string json = _readText(request.Path);
                string hash = ComputeSha256(json);
                if (!string.IsNullOrEmpty(request.ExpectedContentHash)
                    && !string.Equals(request.ExpectedContentHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    return Failed(request, string.Empty, hash, stopwatch, "Content hash did not match expected version.");
                }

                RuntimeConfigPatchBundle bundle = RuntimeConfigPatchJsonLoader.Load(json);
                ConfigPatchMergeResult<BasicBuffConfig> buffResult = RuntimeConfigPatchMerger.Merge(
                    BasicBuffConfig.CreateSchema(),
                    _baseProvider.GetAllConfigs<BasicBuffConfig>(),
                    bundle.BuffPatches);
                ConfigPatchMergeResult<BasicModifierConfig> modifierResult = RuntimeConfigPatchMerger.Merge(
                    BasicModifierConfig.CreateSchema(),
                    _baseProvider.GetAllConfigs<BasicModifierConfig>(),
                    bundle.ModifierPatches);

                var changes = new ConfigChangeSet();
                CopyChanges(buffResult.ChangeSet, changes);
                CopyChanges(modifierResult.ChangeSet, changes);

                var registry = new ConfigRegistry();
                registry.RegisterProvider<BasicBuffConfig>(buffResult.Table);
                registry.RegisterProvider<BasicModifierConfig>(modifierResult.Table);

                return new RuntimeConfigHotReloadResult(
                    request.SourceName,
                    bundle.SourceId,
                    hash,
                    stopwatch.ElapsedMilliseconds,
                    success: true,
                    provider: registry,
                    changeSet: changes,
                    changedTables: CreateChangedTableList(changes),
                    errors: Array.Empty<string>());
            }
            catch (Exception exception)
            {
                return Failed(request, string.Empty, string.Empty, stopwatch, exception.Message);
            }
        }

        private static RuntimeConfigHotReloadResult Failed(
            RuntimeConfigHotReloadRequest request,
            string sourceId,
            string contentHash,
            Stopwatch stopwatch,
            string error)
        {
            return new RuntimeConfigHotReloadResult(
                request.SourceName,
                sourceId,
                contentHash,
                stopwatch.ElapsedMilliseconds,
                success: false,
                provider: null,
                changeSet: new ConfigChangeSet(),
                changedTables: Array.Empty<string>(),
                errors: new[] { error ?? "Unknown config hot reload error." });
        }

        private static void CopyChanges(ConfigChangeSet source, ConfigChangeSet destination)
        {
            if (source == null || destination == null)
                return;

            for (int i = 0; i < source.Changes.Count; i++)
                destination.Add(source.Changes[i]);
        }

        private static IReadOnlyList<string> CreateChangedTableList(ConfigChangeSet changes)
        {
            var tables = new HashSet<string>(StringComparer.Ordinal);
            if (changes != null)
            {
                for (int i = 0; i < changes.Changes.Count; i++)
                {
                    ConfigRowChange change = changes.Changes[i];
                    if (change.ChangeKind != ConfigMergeChangeKind.Noop && change.ConfigType != null)
                        tables.Add(change.ConfigType.Name);
                }
            }

            var ordered = new List<string>(tables);
            ordered.Sort(StringComparer.Ordinal);
            return ordered;
        }

        private static string ComputeSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    public sealed class RuntimeConfigHotReloadPoller
    {
        private string _lastPath = string.Empty;
        private long _lastWriteTicks;
        private long _lastLength;

        public bool TryCreateReloadRequest(
            string path,
            string sourceName,
            out RuntimeConfigHotReloadRequest request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var file = new FileInfo(path);
            if (!file.Exists)
                return false;

            long writeTicks = file.LastWriteTimeUtc.Ticks;
            long length = file.Length;
            bool changed = !string.Equals(_lastPath, file.FullName, StringComparison.Ordinal)
                || _lastWriteTicks != writeTicks
                || _lastLength != length;

            _lastPath = file.FullName;
            _lastWriteTicks = writeTicks;
            _lastLength = length;

            if (!changed)
                return false;

            request = new RuntimeConfigHotReloadRequest(file.FullName, sourceName);
            return true;
        }
    }
}
