using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Modifiers;
using UnityEngine;

namespace MxFramework.Preview
{
    /// <summary>
    /// IPreviewWorld implementation that drives runtime <see cref="MxPreviewSceneTarget"/>
    /// objects generated from <see cref="MxPreviewSceneTargetConfig"/> scene configs.
    /// Falls back to <see cref="DummyPreviewWorld"/>
    /// when no scene targets are found.
    ///
    /// Uses <see cref="RuntimeConfigPatchJsonLoader"/> + <see cref="RuntimeConfigPatchMerger"/>
    /// for Runtime Patch v1 integration. Pure framework code, no WGame dependencies.
    /// </summary>
    public sealed class ScenePreviewWorld : IPreviewWorld, IRuntimePreviewModeSource, IRuntimePreviewConfigMetadataSource, IRuntimePreviewFailureSource
    {
        // Attribute IDs matching framework convention
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;

        private readonly Dictionary<string, MxPreviewSceneTarget> _targets = new Dictionary<string, MxPreviewSceneTarget>(StringComparer.Ordinal);
        private readonly Dictionary<string, MxPreviewSceneTarget> _casters = new Dictionary<string, MxPreviewSceneTarget>(StringComparer.Ordinal);
        private readonly List<MxPreviewSceneTarget> _generatedTargets = new List<MxPreviewSceneTarget>();
        private IConfigProvider _configRegistry;
        private ConfigBuffFactory<BasicBuffConfig> _buffFactory;
        private ConfigModifierFactory<BasicModifierConfig> _modifierFactory;
        private bool _hasPatch;
        private string _patchSourceInfo = "";
        private string _patchError = "";
        private RuntimePreviewConfigMetadata _configMetadata = new RuntimePreviewConfigMetadata();
        private readonly IBuffFactory _fallbackBuffFactory;
        private string _lastFailureReason = string.Empty;
        private string _lastFailureMessage = string.Empty;

        // Fallback dummy world when no scene targets are found
        private DummyPreviewWorld _fallback;
        private bool _usingFallback;
        private string _fallbackReason = "";

        private readonly PreviewLogBuffer _logs;

        /// <summary>
        /// Current preview mode: "scene" when scene targets are active, "dummy" when falling back.
        /// </summary>
        public string PreviewMode => _usingFallback ? "dummy" : "scene";

        /// <summary>
        /// Human-readable fallback reason (empty when in scene mode).
        /// </summary>
        public string FallbackReason => _fallbackReason;

        public RuntimePreviewConfigMetadata CurrentConfigMetadata => _configMetadata.Clone();
        public string LastFailureReason => _lastFailureReason;
        public string LastFailureMessage => _lastFailureMessage;

        public ScenePreviewWorld(PreviewLogBuffer logs = null, IBuffFactory fallbackBuffFactory = null)
        {
            _logs = logs ?? new PreviewLogBuffer();
            _fallbackBuffFactory = fallbackBuffFactory;
            DiscoverSceneTargets();
        }

        private void DiscoverSceneTargets()
        {
            DestroyGeneratedTargets();

            var found = GameObject.FindObjectsByType<MxPreviewSceneTarget>(
                FindObjectsSortMode.None);

            if (found != null && found.Length > 0)
            {
                RegisterTargets(found, "scene runtime target");
                return;
            }

            var configs = GameObject.FindObjectsByType<MxPreviewSceneTargetConfig>(
                FindObjectsSortMode.None);

            if (configs != null && configs.Length > 0)
            {
                var created = new List<MxPreviewSceneTarget>();
                for (int i = 0; i < configs.Length; i++)
                {
                    MxPreviewSceneTargetConfig config = configs[i];
                    if (config == null || !config.ShouldCreateRuntimeTarget)
                        continue;

                    MxPreviewSceneTarget target = config.CreateRuntimeTarget();
                    _generatedTargets.Add(target);
                    created.Add(target);
                    Log("info", $"ScenePreviewWorld: generated runtime target '{target.TargetId}' from config");
                }

                if (created.Count > 0)
                {
                    RegisterTargets(created.ToArray(), "generated from config");
                    return;
                }
            }

            MxPreviewSceneTargetProfile profile = MxPreviewSceneTargetProfile.LoadDefault();
            if (profile != null && profile.Enabled)
            {
                var created = new List<MxPreviewSceneTarget>();

                MxPreviewSceneTargetDefinition[] definitions = profile.Targets;
                for (int i = 0; i < definitions.Length; i++)
                {
                    MxPreviewSceneTargetDefinition definition = definitions[i];
                    if (definition == null || !definition.ShouldCreateRuntimeTarget)
                        continue;

                    MxPreviewSceneTarget target = definition.CreateRuntimeTarget(null);
                    _generatedTargets.Add(target);
                    created.Add(target);
                    Log("info", $"ScenePreviewWorld: generated runtime target '{target.TargetId}' from profile");
                }

                if (created.Count > 0)
                {
                    RegisterTargets(created.ToArray(), "generated from profile");
                    return;
                }
            }

            if (_fallback == null)
                _fallback = new DummyPreviewWorld(_fallbackBuffFactory);

            _usingFallback = true;
            _fallbackReason = "未找到场景目标配置，已使用 dummy world";
            _fallback.GetOrCreateTarget("TestTarget");
            _fallback.GetOrCreateCaster("TestCaster");
            Log("info", "ScenePreviewWorld: no scene target configs found, falling back to dummy");
        }

        private void RegisterTargets(MxPreviewSceneTarget[] found, string source)
        {
            if (found == null || found.Length == 0)
            {
                _usingFallback = true;
                _fallbackReason = "未找到场景目标，已使用 dummy world";
                _fallback = new DummyPreviewWorld(_fallbackBuffFactory);
                _fallback.GetOrCreateTarget("TestTarget");
                _fallback.GetOrCreateCaster("TestCaster");
                Log("info", "ScenePreviewWorld: no scene targets found, falling back to dummy");
                return;
            }

            _usingFallback = false;
            _fallbackReason = "";

            // Group targets and casters by TargetId
            for (int i = 0; i < found.Length; i++)
            {
                MxPreviewSceneTarget t = found[i];
                string id = t.TargetId;
                _targets[id] = t;
                _casters[id] = t;
                Log("info", $"ScenePreviewWorld: found {source} '{id}' " +
                    $"HP={t.InitialHp} ATK={t.InitialAttack} DEF={t.InitialDefense}");
            }
        }

        private void DestroyGeneratedTargets()
        {
            for (int i = 0; i < _generatedTargets.Count; i++)
            {
                MxPreviewSceneTarget target = _generatedTargets[i];
                if (target == null)
                    continue;

                GameObject go = target.gameObject;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(go);
                else
                    UnityEngine.Object.DestroyImmediate(go);
            }
            _generatedTargets.Clear();
        }

        // ====== IPreviewWorld ======

        public IBuffTarget GetOrCreateTarget(string targetId)
        {
            if (_usingFallback)
                return EnsureFallbackWorld().GetOrCreateTarget(targetId);

            if (string.IsNullOrEmpty(targetId)) targetId = "TestTarget";
            if (_targets.TryGetValue(targetId, out var t))
                return t;

            Log("warn", $"ScenePreviewWorld: target '{targetId}' not found in scene, falling back");
            _usingFallback = true;
            _fallbackReason = $"Target '{targetId}' not in scene, fell back to dummy";
            return EnsureFallbackWorld().GetOrCreateTarget(targetId);
        }

        public IBuffTarget GetOrCreateCaster(string casterId)
        {
            if (_usingFallback)
                return EnsureFallbackWorld().GetOrCreateCaster(casterId);

            if (string.IsNullOrEmpty(casterId)) casterId = "TestCaster";
            if (_casters.TryGetValue(casterId, out var c))
                return c;

            Log("warn", $"ScenePreviewWorld: caster '{casterId}' not found in scene, falling back");
            _usingFallback = true;
            _fallbackReason = $"Caster '{casterId}' not in scene, fell back to dummy";
            return EnsureFallbackWorld().GetOrCreateCaster(casterId);
        }

        public void Reset(bool reloadBase)
        {
            ClearRuntimeConfig();

            if (_usingFallback)
            {
                _fallback?.Reset(reloadBase);
                RecreateFallbackWorld(_fallbackBuffFactory);
                return;
            }

            if (reloadBase)
            {
                // Re-discover targets (in case scene changed)
                _targets.Clear();
                _casters.Clear();
                DiscoverSceneTargets();
            }
            else
            {
                // Just reset state of existing targets
                foreach (var kv in _targets)
                {
                    if (kv.Value.ResetOnPreviewRun)
                        kv.Value.ResetState();
                }
            }
            Log("info", $"ScenePreviewWorld: reset completed (reloadBase={reloadBase})");
        }

        public void Tick(int frames)
        {
            if (_usingFallback)
            {
                EnsureFallbackWorld().Tick(frames);
                return;
            }

            if (frames <= 0) return;
            float dt = 1f / 60f;
            for (int f = 0; f < frames; f++)
            {
                foreach (var kv in _targets)
                {
                    MxPreviewSceneTarget t = kv.Value;
                    t.Buffs.TickAll(dt);

                    ModifierContext ctx = ModifierContext.Get();
                    ctx.Target = t.Store;
                    ctx.Buffs = t.Buffs;
                    ctx.Counters = t.Modifiers.Counters;
                    t.Modifiers.UpdateAll(dt, ctx);
                    ModifierContext.Push(ctx);
                }
            }
        }

        public IReadOnlyList<BuffSnapshot> SnapshotBuffs(string targetId)
        {
            if (_usingFallback)
                return EnsureFallbackWorld().SnapshotBuffs(targetId);

            if (string.IsNullOrEmpty(targetId) || !_targets.TryGetValue(targetId, out var t))
                return Array.Empty<BuffSnapshot>();

            return t.SnapshotBuffs();
        }

        public IReadOnlyList<AttributeChange> SnapshotAttributeChanges(string targetId)
        {
            if (_usingFallback)
                return EnsureFallbackWorld().SnapshotAttributeChanges(targetId);

            if (string.IsNullOrEmpty(targetId) || !_targets.TryGetValue(targetId, out var t))
                return Array.Empty<AttributeChange>();

            return t.DrainAttributeChanges();
        }

        public IReadOnlyList<DamageTick> DrainDamageTicks()
        {
            if (_usingFallback)
                return EnsureFallbackWorld().DrainDamageTicks();

            var all = new List<DamageTick>();
            foreach (var kv in _targets)
                all.AddRange(kv.Value.DrainDamageTicks());
            return all;
        }

        public IReadOnlyList<StatusChange> DrainStatusChanges()
        {
            if (_usingFallback)
                return EnsureFallbackWorld().DrainStatusChanges();

            var all = new List<StatusChange>();
            foreach (var kv in _targets)
                all.AddRange(kv.Value.DrainStatusChanges());
            return all;
        }

        public bool ApplyBuff(string buffId, string casterId, string targetId, int stack, long? durationOverrideMs)
        {
            ClearFailure();
            if (_usingFallback)
            {
                bool fallbackApplied = EnsureFallbackWorld().ApplyBuff(buffId, casterId, targetId, stack, durationOverrideMs);
                if (!fallbackApplied && _fallback is IRuntimePreviewFailureSource failure)
                    SetFailure(failure.LastFailureReason, failure.LastFailureMessage);
                return fallbackApplied;
            }

            if (!_hasPatch)
            {
                SetFailure("missing_runtime_patch", "No Runtime Patch v1 config source is loaded; call preview.loadPatch before applyBuff.");
                Log("error", "ScenePreviewWorld: no patch loaded, cannot apply buff");
                return false;
            }

            if (!int.TryParse(buffId, out int id))
            {
                SetFailure("invalid_buff_id", $"buffId '{buffId}' is not a numeric runtime buff id.");
                Log("error", $"ScenePreviewWorld: invalid buffId '{buffId}'");
                return false;
            }

            if (string.IsNullOrEmpty(targetId)) targetId = "TestTarget";
            if (!_targets.TryGetValue(targetId, out var target))
            {
                SetFailure("missing_target", $"Preview target '{targetId}' was not found in scene preview world.");
                Log("error", $"ScenePreviewWorld: target '{targetId}' not found");
                return false;
            }

            // Create the buff from config factory
            if (_buffFactory == null || !_buffFactory.TryCreate(id, out IBuff buff))
            {
                SetFailure("unknown_buff_or_config", $"Buff '{buffId}' was not found in the current preview config registry.");
                Log("error", $"ScenePreviewWorld: buff {buffId} not found in config registry");
                return false;
            }

            // If the buff config has a modifierId, create and add the modifier too
            if (buff is ConfiguredBuff cBuff && cBuff.Config is BasicBuffConfig buffCfg && buffCfg.ModifierId > 0)
            {
                if (_modifierFactory != null && _modifierFactory.TryCreate(buffCfg.ModifierId, out IModifier modifier))
                {
                    target.Modifiers.AddModifier(modifier);
                    Log("info", $"ScenePreviewWorld: created modifier {buffCfg.ModifierId} for buff {buffId}");

                    // Apply the modifier immediately so it takes effect
                    ModifierContext ctx = ModifierContext.Get();
                    ctx.Target = target.Store;
                    ctx.Buffs = target.Buffs;
                    ctx.Counters = target.Modifiers.Counters;
                    modifier.Apply(ctx);
                    ModifierContext.Push(ctx);
                }
                else
                {
                    Log("warn", $"ScenePreviewWorld: modifier {buffCfg.ModifierId} not found in config registry");
                }
            }

            // Add buff to the target's pipeline
            target.Buffs.AddBuff(buff, target);

            // Handle stack override and duration override
            if (stack > 1)
                buff.AddLayer(stack - 1);

            Log("info", $"ScenePreviewWorld: applied buff {buffId} -> {targetId} (stack={stack})");
            return true;
        }

        public void LoadPreviewPatch(string sourceJson)
        {
            RuntimeConfigPatchBundle bundle = null;
            RuntimePreviewConfigMetadata attemptedMetadata = null;
            try
            {
                string json = ExtractRuntimePatchJson(sourceJson);
                bundle = RuntimeConfigPatchJsonLoader.Load(json);
                attemptedMetadata = CreateAttemptedMetadata(bundle);

                // Get base configs
                IConfigProvider baseRegistry = CreateBaseRegistry();

                // Merge base + patch
                IReadOnlyCollection<BasicBuffConfig> baseBuffs = baseRegistry.GetAllConfigs<BasicBuffConfig>();
                IReadOnlyCollection<BasicModifierConfig> baseMods = baseRegistry.GetAllConfigs<BasicModifierConfig>();

                var mergedBuffResult = RuntimeConfigPatchMerger.Merge(
                    BasicBuffConfig.CreateSchema(),
                    baseBuffs,
                    bundle.BuffPatches);

                var mergedModResult = RuntimeConfigPatchMerger.Merge(
                    BasicModifierConfig.CreateSchema(),
                    baseMods,
                    bundle.ModifierPatches);

                // Build registry
                var registry = new ConfigRegistry();
                registry.RegisterProvider<BasicModifierConfig>(mergedModResult.Table);
                registry.RegisterProvider<BasicBuffConfig>(mergedBuffResult.Table);

                List<string> validationErrors = ValidateMergedRegistry(registry, mergedBuffResult.Table, mergedModResult.Table);
                if (validationErrors.Count > 0)
                {
                    attemptedMetadata.FailedConfigIds.AddRange(CollectPatchEntryIds(bundle));
                    attemptedMetadata.MergeWarnings.AddRange(validationErrors);
                    _configMetadata = MergeFailureIntoCurrentMetadata(attemptedMetadata);
                    _patchError = validationErrors[0];
                    Log("error", $"ScenePreviewWorld: patch rejected source={bundle.SourceId} reason={validationErrors[0]}");
                    throw new InvalidOperationException(validationErrors[0]);
                }

                _configRegistry = registry;

                // Create factories
                _buffFactory = new ConfigBuffFactory<BasicBuffConfig>(_configRegistry);
                _modifierFactory = new ConfigModifierFactory<BasicModifierConfig>(_configRegistry);
                _hasPatch = true;
                _patchSourceInfo = bundle.SourceId;
                _patchError = "";
                _configMetadata = CreateMergedMetadata(bundle, mergedBuffResult.ChangeSet, mergedModResult.ChangeSet);

                if (_usingFallback)
                    RecreateFallbackWorld(_buffFactory);

                // Log change set
                foreach (var c in mergedBuffResult.ChangeSet.Changes)
                    Log("info", $"Change: Buff id={c.Id} {c.ChangeKind} source={c.SourceId}");
                foreach (var c in mergedModResult.ChangeSet.Changes)
                    Log("info", $"Change: Modifier id={c.Id} {c.ChangeKind} source={c.SourceId}");

                Log("info", $"ScenePreviewWorld: patch loaded source={bundle.SourceId} layer={bundle.Layer} changed={_configMetadata.ChangedConfigIds.Count} failed={_configMetadata.FailedConfigIds.Count}");
            }
            catch (RuntimeConfigPatchParseException ex)
            {
                _patchError = $"Patch parse failed: {ex.Message}";
                Log("error", $"ScenePreviewWorld: {_patchError}");
                throw;
            }
            catch (Exception ex)
            {
                _patchError = $"Patch load failed: {ex.Message}";
                Log("error", $"ScenePreviewWorld: {_patchError}");
                if (attemptedMetadata != null && attemptedMetadata.FailedConfigIds.Count == 0 && bundle != null)
                {
                    attemptedMetadata.FailedConfigIds.AddRange(CollectPatchEntryIds(bundle));
                    _configMetadata = MergeFailureIntoCurrentMetadata(attemptedMetadata);
                }
                throw;
            }
        }

        /// <summary>
        /// Check whether ScenePreviewWorld found any targets without forcing discovery.
        /// </summary>
        public bool HasSceneTargets => !_usingFallback && _targets.Count > 0;

        // ====== Private ======

        private static IConfigProvider CreateBaseRegistry()
        {
            var registry = new ConfigRegistry();
            var modTable = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
            var buffTable = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            registry.RegisterProvider<BasicModifierConfig>(modTable);
            registry.RegisterProvider<BasicBuffConfig>(buffTable);
            return registry;
        }

        private void ClearRuntimeConfig()
        {
            _configRegistry = null;
            _buffFactory = null;
            _modifierFactory = null;
            _hasPatch = false;
            _patchSourceInfo = "";
            _patchError = "";
            _configMetadata = new RuntimePreviewConfigMetadata();
            ClearFailure();
        }

        private static string ExtractRuntimePatchJson(string sourceJson)
        {
            if (string.IsNullOrWhiteSpace(sourceJson))
                throw new RuntimeConfigPatchParseException("Runtime Patch v1 source is empty.");

            JsonValue root;
            try
            {
                root = PreviewJson.Parse(sourceJson);
            }
            catch (Exception ex)
            {
                throw new RuntimeConfigPatchParseException($"Invalid preview patch params JSON: {ex.Message}", ex);
            }

            if (root == null || root.Kind != JsonKind.Object)
                throw new RuntimeConfigPatchParseException("Runtime Patch v1 source must be a JSON object.");

            if (root.GetField("format") != null)
                return sourceJson;

            string rawSource = root.GetString("rawSource");
            if (!string.IsNullOrWhiteSpace(rawSource))
                return rawSource;

            throw new RuntimeConfigPatchParseException("Missing Runtime Patch v1 payload. Expected direct 'format' or loadPatch 'rawSource'.");
        }

        private static List<string> ValidateMergedRegistry(
            ConfigRegistry registry,
            ConfigTable<BasicBuffConfig> buffTable,
            ConfigTable<BasicModifierConfig> modifierTable)
        {
            var errors = new List<string>();
            AppendValidationErrors(errors, modifierTable.Validate(registry), "BasicModifierConfig");
            AppendValidationErrors(errors, buffTable.Validate(registry), "BasicBuffConfig");
            return errors;
        }

        private static void AppendValidationErrors(
            List<string> errors,
            ConfigTableValidationReport report,
            string tableName)
        {
            if (report == null || !report.HasErrors)
                return;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ConfigTableValidationIssue issue = report.Issues[i];
                if (issue.Severity != ConfigValidationSeverity.Error)
                    continue;

                errors.Add($"{tableName}:{issue.RowId} {issue.FieldName} {issue.Message}");
            }
        }

        private static RuntimePreviewConfigMetadata CreateAttemptedMetadata(RuntimeConfigPatchBundle bundle)
        {
            var metadata = new RuntimePreviewConfigMetadata
            {
                SourceId = bundle.SourceId,
                Layer = bundle.Layer.ToString(),
            };
            metadata.LoadedPatchIds.Add(bundle.SourceId);
            return metadata;
        }

        private static RuntimePreviewConfigMetadata CreateMergedMetadata(
            RuntimeConfigPatchBundle bundle,
            ConfigChangeSet buffChanges,
            ConfigChangeSet modifierChanges)
        {
            RuntimePreviewConfigMetadata metadata = CreateAttemptedMetadata(bundle);
            AppendChangedIds(metadata, modifierChanges);
            AppendChangedIds(metadata, buffChanges);
            return metadata;
        }

        private static void AppendChangedIds(RuntimePreviewConfigMetadata metadata, ConfigChangeSet changeSet)
        {
            if (changeSet == null)
                return;

            for (int i = 0; i < changeSet.Changes.Count; i++)
            {
                ConfigRowChange change = changeSet.Changes[i];
                string typeName = change.ConfigType != null ? change.ConfigType.Name : "Config";
                string id = typeName + ":" + change.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!metadata.ChangedConfigIds.Contains(id))
                    metadata.ChangedConfigIds.Add(id);
            }
        }

        private static List<string> CollectPatchEntryIds(RuntimeConfigPatchBundle bundle)
        {
            var ids = new List<string>();
            if (bundle == null)
                return ids;

            for (int i = 0; i < bundle.ModifierPatches.Count; i++)
                ids.Add("BasicModifierConfig:" + bundle.ModifierPatches[i].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int i = 0; i < bundle.BuffPatches.Count; i++)
                ids.Add("BasicBuffConfig:" + bundle.BuffPatches[i].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return ids;
        }

        private RuntimePreviewConfigMetadata MergeFailureIntoCurrentMetadata(RuntimePreviewConfigMetadata attempted)
        {
            RuntimePreviewConfigMetadata metadata = _configMetadata != null ? _configMetadata.Clone() : new RuntimePreviewConfigMetadata();
            if (attempted == null)
                return metadata;

            for (int i = 0; i < attempted.FailedConfigIds.Count; i++)
            {
                string id = attempted.FailedConfigIds[i];
                if (!metadata.FailedConfigIds.Contains(id))
                    metadata.FailedConfigIds.Add(id);
            }

            for (int i = 0; i < attempted.MergeWarnings.Count; i++)
            {
                string warning = attempted.MergeWarnings[i];
                if (!metadata.MergeWarnings.Contains(warning))
                    metadata.MergeWarnings.Add(warning);
            }

            return metadata;
        }

        private void Log(string level, string message)
        {
            _logs.Append(level, message);
        }

        private void ClearFailure()
        {
            _lastFailureReason = string.Empty;
            _lastFailureMessage = string.Empty;
        }

        private void SetFailure(string reason, string message)
        {
            _lastFailureReason = reason ?? string.Empty;
            _lastFailureMessage = message ?? string.Empty;
        }

        private DummyPreviewWorld EnsureFallbackWorld()
        {
            if (_fallback == null)
            {
                _fallback = new DummyPreviewWorld(_buffFactory ?? _fallbackBuffFactory);
                _fallback.GetOrCreateTarget("TestTarget");
                _fallback.GetOrCreateCaster("TestCaster");
            }

            return _fallback;
        }

        private void RecreateFallbackWorld(IBuffFactory factory)
        {
            _fallback = new DummyPreviewWorld(factory ?? _fallbackBuffFactory);
            _fallback.GetOrCreateTarget("TestTarget");
            _fallback.GetOrCreateCaster("TestCaster");
        }
    }
}
