using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionMechanicTuningAuthoringStore
    {
        public static string DefaultAbilityAuthoringPath =>
            CharacterActionProjectileAuthoringStore.DefaultAbilityAuthoringPath;
        public const string DefaultGeneratedMechanicTuningsPath =
            "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";
        public const string DefaultPlayerMirrorMechanicTuningsPath =
            "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Abilities/character_test_mechanic_tunings.json";
        public const string ArcMineTuningStableId = "mechanic_tuning.character_test.arc_mine";
        public const string ArcMineGameplayRequestId = "character_test.arc_mine.deploy";
        public const string ArcMineAbilityStableId = "ability.character_test.arc_mine";
        public const string ArcMineSourceStableId = "mechanic.character_test.arc_mine.trap";
        public const string RuneTurretTuningStableId = "mechanic_tuning.character_test.rune_turret";
        public const string RuneTurretGameplayRequestId = "character_test.rune_turret.deploy";
        public const string RuneTurretAbilityStableId = "ability.character_test.rune_turret";
        public const string RuneTurretSourceStableId = "mechanic.character_test.rune_turret.turret";

        private const int DefaultArcMineDeployRangeMeters = 8;
        private const int DefaultArcMineFallbackForwardMeters = 5;
        private const int DefaultArcMineFuseDelayFrames = 60;
        private const int DefaultArcMineDurationFrames = 180;
        private const int DefaultArcMineTriggerRadiusCentimeters = 125;
        private const int DefaultArcMineBlastRadiusCentimeters = 225;
        private const int DefaultArcMineMaxBlastTargets = 8;
        private const int DefaultArcMineDeployVfxDefinitionId = 12041;
        private const int DefaultArcMineTriggerVfxDefinitionId = 12042;
        private const int DefaultArcMineFuseVfxDefinitionId = 12043;
        private const int DefaultRuneTurretDeployRangeMeters = 8;
        private const int DefaultRuneTurretFallbackForwardMeters = 5;
        private const int DefaultRuneTurretWarmupFrames = 18;
        private const int DefaultRuneTurretDurationFrames = 360;
        private const int DefaultRuneTurretScanRadiusCentimeters = 1000;
        private const int DefaultRuneTurretScanIntervalFrames = 12;
        private const int DefaultRuneTurretFireIntervalFrames = 45;
        private const int DefaultRuneTurretMaxShots = 8;
        private const int DefaultRuneTurretMuzzleHeightCentimeters = 100;
        private const int DefaultRuneTurretDeployVfxDefinitionId = 12045;
        private const int DefaultRuneTurretIdleVfxDefinitionId = 12046;
        private const int DefaultRuneTurretFireVfxDefinitionId = 12047;
        private const int DefaultRuneTurretExpireVfxDefinitionId = 12050;

        private readonly CharacterActionMechanicTuningAuthoringStoreOptions _options;
        private readonly CharacterActionMechanicTuningAuthoringRecord _arcMine;
        private readonly CharacterActionMechanicTuningAuthoringRecord _runeTurret;
        private readonly string[] _diagnostics;

        private CharacterActionMechanicTuningAuthoringStore(
            CharacterActionMechanicTuningAuthoringStoreOptions options,
            CharacterActionMechanicTuningAuthoringRecord arcMine,
            CharacterActionMechanicTuningAuthoringRecord runeTurret,
            string[] diagnostics)
        {
            _options = options ?? CharacterActionMechanicTuningAuthoringStoreOptions.Default;
            _arcMine = arcMine;
            _runeTurret = runeTurret;
            _diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Diagnostics => _diagnostics;

        public static CharacterActionMechanicTuningAuthoringStore LoadDefault()
        {
            return Load(CharacterActionMechanicTuningAuthoringStoreOptions.Default);
        }

        public static CharacterActionMechanicTuningAuthoringStore Load(
            CharacterActionMechanicTuningAuthoringStoreOptions options)
        {
            options ??= CharacterActionMechanicTuningAuthoringStoreOptions.Default;
            var diagnostics = new List<string>();
            JObject sourceRoot = TryReadJson(options.AbilityAuthoringPath, diagnostics, "ability_authoring.json");
            CharacterActionMechanicTuningAuthoringRecord arcMine = null;
            CharacterActionMechanicTuningAuthoringRecord runeTurret = null;
            if (sourceRoot != null)
            {
                JObject tuning = FindTuning(sourceRoot["characterTestMechanicTunings"] as JArray, ArcMineTuningStableId);
                if (tuning != null)
                    arcMine = CharacterActionMechanicTuningAuthoringRecord.FromDraft(
                        CharacterActionMechanicTuningAuthoringDraft.FromJson(
                            tuning,
                            options.AbilityAuthoringPath,
                            hasSource: true));
                else
                    diagnostics.Add("缺少触发地雷数值源: " + ArcMineTuningStableId);

                JObject runeTuning = FindTuning(
                    sourceRoot["characterTestMechanicTunings"] as JArray,
                    RuneTurretTuningStableId);
                if (runeTuning != null)
                    runeTurret = CharacterActionMechanicTuningAuthoringRecord.FromDraft(
                        CharacterActionMechanicTuningAuthoringDraft.FromJson(
                            runeTuning,
                            options.AbilityAuthoringPath,
                            hasSource: true));
                else
                    diagnostics.Add("缺少符文炮台数值源: " + RuneTurretTuningStableId);
            }

            return new CharacterActionMechanicTuningAuthoringStore(
                options,
                arcMine,
                runeTurret,
                diagnostics.ToArray());
        }

        public CharacterActionMechanicTuningAuthoringStore WithDraftOverrides(
            IReadOnlyDictionary<string, CharacterActionMechanicTuningAuthoringDraft> overrides)
        {
            if (overrides == null || overrides.Count == 0)
                return this;

            CharacterActionMechanicTuningAuthoringRecord arcMine = _arcMine;
            CharacterActionMechanicTuningAuthoringRecord runeTurret = _runeTurret;
            if (arcMine != null
                && overrides.TryGetValue(ArcMineTuningStableId, out CharacterActionMechanicTuningAuthoringDraft arcDraft)
                && arcDraft != null)
            {
                arcMine = CharacterActionMechanicTuningAuthoringRecord.FromDraft(arcDraft.Clone());
            }

            if (runeTurret != null
                && overrides.TryGetValue(RuneTurretTuningStableId, out CharacterActionMechanicTuningAuthoringDraft runeDraft)
                && runeDraft != null)
            {
                runeTurret = CharacterActionMechanicTuningAuthoringRecord.FromDraft(runeDraft.Clone());
            }

            return new CharacterActionMechanicTuningAuthoringStore(
                _options,
                arcMine,
                runeTurret,
                CopyDiagnostics());
        }

        public bool TryGetArcMine(out CharacterActionMechanicTuningAuthoringRecord record)
        {
            record = _arcMine;
            return record != null;
        }

        public bool TryGetRuneTurret(out CharacterActionMechanicTuningAuthoringRecord record)
        {
            record = _runeTurret;
            return record != null;
        }

        public CharacterActionMechanicTuningAuthoringEditResult ValidateDraft(
            CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (draft == null)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("玩法数值草稿缺失。");

            if (string.Equals(draft.StableId, ArcMineTuningStableId, StringComparison.Ordinal))
            {
                if (_arcMine == null || !_arcMine.HasSource)
                    return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                        "缺少触发地雷数值源。");
                return ValidateArcMineDraft(draft);
            }

            if (string.Equals(draft.StableId, RuneTurretTuningStableId, StringComparison.Ordinal))
            {
                if (_runeTurret == null || !_runeTurret.HasSource)
                    return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                        "缺少符文炮台数值源。");
                return ValidateRuneTurretDraft(draft);
            }

            return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                "不支持的玩法数值: " + (draft.StableId ?? string.Empty));
        }

        private CharacterActionMechanicTuningAuthoringEditResult ValidateKnownDraft(
            CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (draft == null)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "玩法数值草稿缺失。");
            if (string.Equals(draft.StableId, ArcMineTuningStableId, StringComparison.Ordinal))
                return ValidateArcMineDraft(draft);
            if (string.Equals(draft.StableId, RuneTurretTuningStableId, StringComparison.Ordinal))
                return ValidateRuneTurretDraft(draft);

            return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                "不支持的玩法数值: " + (draft.StableId ?? string.Empty));
        }

        public CharacterActionMechanicTuningAuthoringEditResult UpdateTunings(
            IReadOnlyList<CharacterActionMechanicTuningAuthoringDraft> drafts)
        {
            if (drafts == null || drafts.Count == 0)
                return CharacterActionMechanicTuningAuthoringEditResult.SuccessResult(
                    "没有需要保存的玩法数值修改。",
                    null);

            JObject sourceRoot = TryReadJson(_options.AbilityAuthoringPath, null, string.Empty);
            if (sourceRoot == null)
            {
                return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "无法读取能力配置源: " + _options.AbilityAuthoringPath);
            }

            JArray tunings = sourceRoot["characterTestMechanicTunings"] as JArray;
            if (tunings == null)
            {
                return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "能力配置源缺少 characterTestMechanicTunings。");
            }

            for (int i = 0; i < drafts.Count; i++)
            {
                CharacterActionMechanicTuningAuthoringDraft draft = drafts[i]?.Clone();
                CharacterActionMechanicTuningAuthoringEditResult validation = ValidateKnownDraft(draft);
                if (!validation.Success)
                    return validation;

                JObject tuning = FindTuning(tunings, draft.StableId);
                if (tuning == null)
                {
                    return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                        "缺少玩法数值源: " + draft.StableId);
                }

                ApplyKnownDraft(tuning, draft);
            }

            JObject generatedRoot = BuildGeneratedMechanicTuningsRoot(sourceRoot);
            try
            {
                WriteJson(_options.AbilityAuthoringPath, sourceRoot);
                WriteJson(_options.GeneratedMechanicTuningsPath, generatedRoot);
                if (!string.IsNullOrWhiteSpace(_options.PlayerMirrorMechanicTuningsPath))
                    WriteJson(_options.PlayerMirrorMechanicTuningsPath, generatedRoot);

                ImportAssetIfNeeded(_options.AbilityAuthoringPath, _options.RefreshAssetDatabase);
                ImportAssetIfNeeded(_options.GeneratedMechanicTuningsPath, _options.RefreshAssetDatabase);
                ImportAssetIfNeeded(_options.PlayerMirrorMechanicTuningsPath, _options.RefreshAssetDatabase);
            }
            catch (Exception ex)
            {
                return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "保存玩法数值配置失败: " + ex.Message);
            }

            return CharacterActionMechanicTuningAuthoringEditResult.SuccessResult(
                "玩法数值配置已保存，并已同步 generated JSON。",
                sourceRoot);
        }

        private string[] CopyDiagnostics()
        {
            var diagnostics = new string[_diagnostics.Length];
            Array.Copy(_diagnostics, diagnostics, _diagnostics.Length);
            return diagnostics;
        }

        private static CharacterActionMechanicTuningAuthoringEditResult ValidateArcMineDraft(
            CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (draft == null)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("触发地雷数值草稿缺失。");
            if (!string.Equals(draft.StableId, ArcMineTuningStableId, StringComparison.Ordinal))
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("不支持的玩法数值: " + draft.StableId);
            if (draft.DeployRangeMeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("部署锁定范围必须大于 0。");
            if (draft.FallbackForwardMeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("无锁前方距离必须大于 0。");
            if (draft.FuseDelayFrames <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("引信延迟必须大于 0。");
            if (draft.DurationFrames <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("持续时间必须大于 0。");
            if (draft.TriggerRadiusCentimeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("触发半径必须大于 0。");
            if (draft.BlastRadiusCentimeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("爆发半径必须大于 0。");
            if (draft.MaxBlastTargets <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("爆发最大目标数必须大于 0。");
            if (draft.DeployVfxDefinitionId <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("部署 VFX 定义 ID 必须大于 0。");
            if (draft.FuseVfxDefinitionId < 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("寮曚俊 VFX 瀹氫箟 ID 涓嶈兘灏忎簬 0銆?");
            if (draft.TriggerVfxDefinitionId <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("触发 VFX 定义 ID 必须大于 0。");

            return CharacterActionMechanicTuningAuthoringEditResult.SuccessResult("玩法数值草稿有效。", null);
        }

        private static CharacterActionMechanicTuningAuthoringEditResult ValidateRuneTurretDraft(
            CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (draft == null)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("符文炮台数值草稿缺失。");
            if (!string.Equals(draft.StableId, RuneTurretTuningStableId, StringComparison.Ordinal))
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("不支持的玩法数值: " + draft.StableId);
            if (draft.DeployRangeMeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("投放锁定范围必须大于 0。");
            if (draft.FallbackForwardMeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("无锁投放距离必须大于 0。");
            if (draft.WarmupFrames < 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("启动延迟不能小于 0。");
            if (draft.DurationFrames <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("炮台存在时间必须大于 0。");
            if (draft.ScanRadiusCentimeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("索敌半径必须大于 0。");
            if (draft.ScanIntervalFrames <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("空闲扫描间隔必须大于 0。");
            if (draft.FireIntervalFrames <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("开火间隔必须大于 0。");
            if (draft.MaxShots <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("最多开火次数必须大于 0。");
            if (draft.MuzzleHeightCentimeters <= 0)
                return CharacterActionMechanicTuningAuthoringEditResult.Failure("炮口高度必须大于 0。");
            if (draft.DeployVfxDefinitionId <= 0
                || draft.IdleVfxDefinitionId <= 0
                || draft.FireVfxDefinitionId <= 0
                || draft.ExpireVfxDefinitionId <= 0)
            {
                return CharacterActionMechanicTuningAuthoringEditResult.Failure(
                    "符文炮台 VFX 定义 ID 必须大于 0。");
            }

            return CharacterActionMechanicTuningAuthoringEditResult.SuccessResult("玩法数值草稿有效。", null);
        }

        private static JObject BuildGeneratedMechanicTuningsRoot(JObject sourceRoot)
        {
            var root = new JObject
            {
                ["schemaVersion"] = "1",
                ["packageId"] = sourceRoot?["packageId"]?.Value<string>() ?? string.Empty,
                ["tunings"] = sourceRoot?["characterTestMechanicTunings"] is JArray tunings
                    ? (JArray)tunings.DeepClone()
                    : new JArray(),
            };
            return root;
        }

        private static void ApplyArcMineDraft(JObject tuning, CharacterActionMechanicTuningAuthoringDraft draft)
        {
            tuning["stableId"] = string.IsNullOrWhiteSpace(draft.StableId) ? ArcMineTuningStableId : draft.StableId;
            tuning["abilityStableId"] = string.IsNullOrWhiteSpace(draft.AbilityStableId)
                ? ArcMineAbilityStableId
                : draft.AbilityStableId;
            tuning["sourceStableId"] = string.IsNullOrWhiteSpace(draft.SourceStableId)
                ? ArcMineSourceStableId
                : draft.SourceStableId;
            tuning["deployRangeMeters"] = draft.DeployRangeMeters;
            tuning["fallbackForwardMeters"] = draft.FallbackForwardMeters;
            tuning.Remove("armDelayFrames");
            tuning["fuseDelayFrames"] = draft.FuseDelayFrames;
            tuning["durationFrames"] = draft.DurationFrames;
            tuning["triggerRadiusCentimeters"] = draft.TriggerRadiusCentimeters;
            tuning["blastRadiusCentimeters"] = draft.BlastRadiusCentimeters;
            tuning["maxBlastTargets"] = draft.MaxBlastTargets;
            tuning["deployVfxDefinitionId"] = draft.DeployVfxDefinitionId;
            tuning["fuseVfxDefinitionId"] = draft.FuseVfxDefinitionId;
            tuning["triggerVfxDefinitionId"] = draft.TriggerVfxDefinitionId;
        }

        private static void ApplyKnownDraft(JObject tuning, CharacterActionMechanicTuningAuthoringDraft draft)
        {
            if (string.Equals(draft.StableId, RuneTurretTuningStableId, StringComparison.Ordinal))
            {
                ApplyRuneTurretDraft(tuning, draft);
                return;
            }

            ApplyArcMineDraft(tuning, draft);
        }

        private static void ApplyRuneTurretDraft(JObject tuning, CharacterActionMechanicTuningAuthoringDraft draft)
        {
            tuning["stableId"] = string.IsNullOrWhiteSpace(draft.StableId) ? RuneTurretTuningStableId : draft.StableId;
            tuning["abilityStableId"] = string.IsNullOrWhiteSpace(draft.AbilityStableId)
                ? RuneTurretAbilityStableId
                : draft.AbilityStableId;
            tuning["sourceStableId"] = string.IsNullOrWhiteSpace(draft.SourceStableId)
                ? RuneTurretSourceStableId
                : draft.SourceStableId;
            tuning["deployRangeMeters"] = draft.DeployRangeMeters;
            tuning["fallbackForwardMeters"] = draft.FallbackForwardMeters;
            tuning["warmupFrames"] = draft.WarmupFrames;
            tuning["durationFrames"] = draft.DurationFrames;
            tuning["scanRadiusCentimeters"] = draft.ScanRadiusCentimeters;
            tuning["scanIntervalFrames"] = draft.ScanIntervalFrames;
            tuning["fireIntervalFrames"] = draft.FireIntervalFrames;
            tuning["maxShots"] = draft.MaxShots;
            tuning["muzzleHeightCentimeters"] = draft.MuzzleHeightCentimeters;
            tuning["deployVfxDefinitionId"] = draft.DeployVfxDefinitionId;
            tuning["idleVfxDefinitionId"] = draft.IdleVfxDefinitionId;
            tuning["fireVfxDefinitionId"] = draft.FireVfxDefinitionId;
            tuning["expireVfxDefinitionId"] = draft.ExpireVfxDefinitionId;
        }

        private static JObject FindTuning(JArray tunings, string stableId)
        {
            for (int i = 0; tunings != null && i < tunings.Count; i++)
            {
                if (tunings[i] is JObject fields
                    && string.Equals(fields["stableId"]?.Value<string>(), stableId, StringComparison.Ordinal))
                {
                    return fields;
                }
            }

            return null;
        }

        private static JObject TryReadJson(string path, List<string> diagnostics, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (diagnostics != null)
                    diagnostics.Add("Missing " + (string.IsNullOrWhiteSpace(label) ? "JSON" : label) + ": " + path);
                return null;
            }

            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                if (diagnostics != null)
                    diagnostics.Add("Failed to read " + label + ": " + ex.Message);
                return null;
            }
        }

        private static void WriteJson(string path, JObject root)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                JsonConvert.SerializeObject(root ?? new JObject(), CharacterActionAuthoringJsonService.JsonSettings)
                + Environment.NewLine);
        }

        private static void ImportAssetIfNeeded(string path, bool refreshAssetDatabase)
        {
            if (!refreshAssetDatabase || string.IsNullOrWhiteSpace(path))
                return;

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || string.Equals(normalized, "Assets", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
            }
        }

        private static int ReadInt(JObject fields, string name, int fallback)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static string ReadString(JObject fields, string name, string fallback)
        {
            JToken token = fields?[name];
            return token == null ? fallback : token.Value<string>() ?? fallback;
        }

        public sealed class CharacterActionMechanicTuningAuthoringRecord
        {
            private CharacterActionMechanicTuningAuthoringRecord(
                CharacterActionMechanicTuningAuthoringDraft draft)
            {
                Draft = draft?.Clone() ?? CharacterActionMechanicTuningAuthoringDraft.CreateDefaultArcMine();
            }

            public string StableId => Draft.StableId;
            public string AbilityStableId => Draft.AbilityStableId;
            public string SourceStableId => Draft.SourceStableId;
            public int DeployRangeMeters => Draft.DeployRangeMeters;
            public int FallbackForwardMeters => Draft.FallbackForwardMeters;
            public int FuseDelayFrames => Draft.FuseDelayFrames;
            public int ArmDelayFrames => Draft.FuseDelayFrames;
            public int DurationFrames => Draft.DurationFrames;
            public int TriggerRadiusCentimeters => Draft.TriggerRadiusCentimeters;
            public int BlastRadiusCentimeters => Draft.BlastRadiusCentimeters;
            public int MaxBlastTargets => Draft.MaxBlastTargets;
            public int WarmupFrames => Draft.WarmupFrames;
            public int ScanRadiusCentimeters => Draft.ScanRadiusCentimeters;
            public int ScanIntervalFrames => Draft.ScanIntervalFrames;
            public int FireIntervalFrames => Draft.FireIntervalFrames;
            public int MaxShots => Draft.MaxShots;
            public int MuzzleHeightCentimeters => Draft.MuzzleHeightCentimeters;
            public int DeployVfxDefinitionId => Draft.DeployVfxDefinitionId;
            public int IdleVfxDefinitionId => Draft.IdleVfxDefinitionId;
            public int FireVfxDefinitionId => Draft.FireVfxDefinitionId;
            public int FuseVfxDefinitionId => Draft.FuseVfxDefinitionId;
            public int TriggerVfxDefinitionId => Draft.TriggerVfxDefinitionId;
            public int ExpireVfxDefinitionId => Draft.ExpireVfxDefinitionId;
            public string SourcePath => Draft.SourcePath;
            public bool HasSource => Draft.HasSource;
            public CharacterActionMechanicTuningAuthoringDraft Draft { get; }

            public string Summary =>
                string.Equals(StableId, RuneTurretTuningStableId, StringComparison.Ordinal)
                    ? "前方 " + FallbackForwardMeters + "m / " + WarmupFrames
                        + "f 后激活 / 索敌 " + FormatMeters(ScanRadiusCentimeters)
                        + "m / 每 " + FireIntervalFrames + "f 开火 / 最多 " + MaxShots + " 发"
                    : "前方 " + FallbackForwardMeters + "m / 入圈后 " + FuseDelayFrames
                        + "f 爆炸 / 存在 " + DurationFrames
                        + "f / 触发 " + FormatMeters(TriggerRadiusCentimeters)
                        + "m / 爆炸 " + FormatMeters(BlastRadiusCentimeters) + "m";

            public CharacterActionMechanicTuningAuthoringDraft CreateDraft()
            {
                return Draft.Clone();
            }

            public static CharacterActionMechanicTuningAuthoringRecord FromDraft(
                CharacterActionMechanicTuningAuthoringDraft draft)
            {
                return new CharacterActionMechanicTuningAuthoringRecord(draft);
            }
        }

        public sealed class CharacterActionMechanicTuningAuthoringDraft
        {
            public string StableId { get; set; } = ArcMineTuningStableId;
            public string AbilityStableId { get; set; } = ArcMineAbilityStableId;
            public string SourceStableId { get; set; } = ArcMineSourceStableId;
            public int DeployRangeMeters { get; set; } = DefaultArcMineDeployRangeMeters;
            public int FallbackForwardMeters { get; set; } = DefaultArcMineFallbackForwardMeters;
            public int FuseDelayFrames { get; set; } = DefaultArcMineFuseDelayFrames;
            public int ArmDelayFrames
            {
                get => FuseDelayFrames;
                set => FuseDelayFrames = value;
            }
            public int DurationFrames { get; set; } = DefaultArcMineDurationFrames;
            public int TriggerRadiusCentimeters { get; set; } = DefaultArcMineTriggerRadiusCentimeters;
            public int BlastRadiusCentimeters { get; set; } = DefaultArcMineBlastRadiusCentimeters;
            public int MaxBlastTargets { get; set; } = DefaultArcMineMaxBlastTargets;
            public int WarmupFrames { get; set; } = DefaultRuneTurretWarmupFrames;
            public int ScanRadiusCentimeters { get; set; } = DefaultRuneTurretScanRadiusCentimeters;
            public int ScanIntervalFrames { get; set; } = DefaultRuneTurretScanIntervalFrames;
            public int FireIntervalFrames { get; set; } = DefaultRuneTurretFireIntervalFrames;
            public int MaxShots { get; set; } = DefaultRuneTurretMaxShots;
            public int MuzzleHeightCentimeters { get; set; } = DefaultRuneTurretMuzzleHeightCentimeters;
            public int DeployVfxDefinitionId { get; set; } = DefaultArcMineDeployVfxDefinitionId;
            public int IdleVfxDefinitionId { get; set; } = DefaultRuneTurretIdleVfxDefinitionId;
            public int FireVfxDefinitionId { get; set; } = DefaultRuneTurretFireVfxDefinitionId;
            public int FuseVfxDefinitionId { get; set; } = DefaultArcMineFuseVfxDefinitionId;
            public int TriggerVfxDefinitionId { get; set; } = DefaultArcMineTriggerVfxDefinitionId;
            public int ExpireVfxDefinitionId { get; set; } = DefaultRuneTurretExpireVfxDefinitionId;
            public string SourcePath { get; set; } = string.Empty;
            public bool HasSource { get; set; }

            public static CharacterActionMechanicTuningAuthoringDraft CreateDefaultArcMine()
            {
                return new CharacterActionMechanicTuningAuthoringDraft
                {
                    StableId = ArcMineTuningStableId,
                    AbilityStableId = ArcMineAbilityStableId,
                    SourceStableId = ArcMineSourceStableId,
                    HasSource = false,
                };
            }

            public static CharacterActionMechanicTuningAuthoringDraft CreateDefaultRuneTurret()
            {
                return new CharacterActionMechanicTuningAuthoringDraft
                {
                    StableId = RuneTurretTuningStableId,
                    AbilityStableId = RuneTurretAbilityStableId,
                    SourceStableId = RuneTurretSourceStableId,
                    DeployRangeMeters = DefaultRuneTurretDeployRangeMeters,
                    FallbackForwardMeters = DefaultRuneTurretFallbackForwardMeters,
                    WarmupFrames = DefaultRuneTurretWarmupFrames,
                    DurationFrames = DefaultRuneTurretDurationFrames,
                    ScanRadiusCentimeters = DefaultRuneTurretScanRadiusCentimeters,
                    ScanIntervalFrames = DefaultRuneTurretScanIntervalFrames,
                    FireIntervalFrames = DefaultRuneTurretFireIntervalFrames,
                    MaxShots = DefaultRuneTurretMaxShots,
                    MuzzleHeightCentimeters = DefaultRuneTurretMuzzleHeightCentimeters,
                    DeployVfxDefinitionId = DefaultRuneTurretDeployVfxDefinitionId,
                    IdleVfxDefinitionId = DefaultRuneTurretIdleVfxDefinitionId,
                    FireVfxDefinitionId = DefaultRuneTurretFireVfxDefinitionId,
                    ExpireVfxDefinitionId = DefaultRuneTurretExpireVfxDefinitionId,
                    HasSource = false,
                };
            }

            public static CharacterActionMechanicTuningAuthoringDraft FromJson(
                JObject fields,
                string sourcePath,
                bool hasSource)
            {
                string stableId = ReadString(fields, "stableId", ArcMineTuningStableId);
                bool isRuneTurret = string.Equals(stableId, RuneTurretTuningStableId, StringComparison.Ordinal);
                return new CharacterActionMechanicTuningAuthoringDraft
                {
                    StableId = stableId,
                    AbilityStableId = ReadString(
                        fields,
                        "abilityStableId",
                        isRuneTurret ? RuneTurretAbilityStableId : ArcMineAbilityStableId),
                    SourceStableId = ReadString(
                        fields,
                        "sourceStableId",
                        isRuneTurret ? RuneTurretSourceStableId : ArcMineSourceStableId),
                    DeployRangeMeters = ReadInt(
                        fields,
                        "deployRangeMeters",
                        isRuneTurret ? DefaultRuneTurretDeployRangeMeters : DefaultArcMineDeployRangeMeters),
                    FallbackForwardMeters = ReadInt(
                        fields,
                        "fallbackForwardMeters",
                        isRuneTurret ? DefaultRuneTurretFallbackForwardMeters : DefaultArcMineFallbackForwardMeters),
                    FuseDelayFrames = ReadInt(
                        fields,
                        "fuseDelayFrames",
                        ReadInt(fields, "armDelayFrames", DefaultArcMineFuseDelayFrames)),
                    WarmupFrames = ReadInt(fields, "warmupFrames", DefaultRuneTurretWarmupFrames),
                    DurationFrames = ReadInt(
                        fields,
                        "durationFrames",
                        isRuneTurret ? DefaultRuneTurretDurationFrames : DefaultArcMineDurationFrames),
                    TriggerRadiusCentimeters = ReadInt(
                        fields,
                        "triggerRadiusCentimeters",
                        DefaultArcMineTriggerRadiusCentimeters),
                    BlastRadiusCentimeters = ReadInt(
                        fields,
                        "blastRadiusCentimeters",
                        DefaultArcMineBlastRadiusCentimeters),
                    MaxBlastTargets = ReadInt(fields, "maxBlastTargets", DefaultArcMineMaxBlastTargets),
                    ScanRadiusCentimeters = ReadInt(
                        fields,
                        "scanRadiusCentimeters",
                        DefaultRuneTurretScanRadiusCentimeters),
                    ScanIntervalFrames = ReadInt(
                        fields,
                        "scanIntervalFrames",
                        DefaultRuneTurretScanIntervalFrames),
                    FireIntervalFrames = ReadInt(
                        fields,
                        "fireIntervalFrames",
                        DefaultRuneTurretFireIntervalFrames),
                    MaxShots = ReadInt(fields, "maxShots", DefaultRuneTurretMaxShots),
                    MuzzleHeightCentimeters = ReadInt(
                        fields,
                        "muzzleHeightCentimeters",
                        DefaultRuneTurretMuzzleHeightCentimeters),
                    DeployVfxDefinitionId = ReadInt(
                        fields,
                        "deployVfxDefinitionId",
                        isRuneTurret ? DefaultRuneTurretDeployVfxDefinitionId : DefaultArcMineDeployVfxDefinitionId),
                    IdleVfxDefinitionId = ReadInt(
                        fields,
                        "idleVfxDefinitionId",
                        DefaultRuneTurretIdleVfxDefinitionId),
                    FireVfxDefinitionId = ReadInt(
                        fields,
                        "fireVfxDefinitionId",
                        DefaultRuneTurretFireVfxDefinitionId),
                    FuseVfxDefinitionId = ReadInt(
                        fields,
                        "fuseVfxDefinitionId",
                        0),
                    TriggerVfxDefinitionId = ReadInt(
                        fields,
                        "triggerVfxDefinitionId",
                        DefaultArcMineTriggerVfxDefinitionId),
                    ExpireVfxDefinitionId = ReadInt(
                        fields,
                        "expireVfxDefinitionId",
                        DefaultRuneTurretExpireVfxDefinitionId),
                    SourcePath = sourcePath ?? string.Empty,
                    HasSource = hasSource,
                };
            }

            public CharacterActionMechanicTuningAuthoringDraft Clone()
            {
                return new CharacterActionMechanicTuningAuthoringDraft
                {
                    StableId = StableId ?? string.Empty,
                    AbilityStableId = AbilityStableId ?? string.Empty,
                    SourceStableId = SourceStableId ?? string.Empty,
                    DeployRangeMeters = DeployRangeMeters,
                    FallbackForwardMeters = FallbackForwardMeters,
                    FuseDelayFrames = FuseDelayFrames,
                    DurationFrames = DurationFrames,
                    TriggerRadiusCentimeters = TriggerRadiusCentimeters,
                    BlastRadiusCentimeters = BlastRadiusCentimeters,
                    MaxBlastTargets = MaxBlastTargets,
                    WarmupFrames = WarmupFrames,
                    ScanRadiusCentimeters = ScanRadiusCentimeters,
                    ScanIntervalFrames = ScanIntervalFrames,
                    FireIntervalFrames = FireIntervalFrames,
                    MaxShots = MaxShots,
                    MuzzleHeightCentimeters = MuzzleHeightCentimeters,
                    DeployVfxDefinitionId = DeployVfxDefinitionId,
                    IdleVfxDefinitionId = IdleVfxDefinitionId,
                    FireVfxDefinitionId = FireVfxDefinitionId,
                    FuseVfxDefinitionId = FuseVfxDefinitionId,
                    TriggerVfxDefinitionId = TriggerVfxDefinitionId,
                    ExpireVfxDefinitionId = ExpireVfxDefinitionId,
                    SourcePath = SourcePath ?? string.Empty,
                    HasSource = HasSource,
                };
            }
        }

        public sealed class CharacterActionMechanicTuningAuthoringEditResult
        {
            private CharacterActionMechanicTuningAuthoringEditResult(
                bool success,
                string message,
                JObject sourceRoot)
            {
                Success = success;
                Message = message ?? string.Empty;
                SourceRoot = sourceRoot == null ? null : (JObject)sourceRoot.DeepClone();
            }

            public bool Success { get; }
            public string Message { get; }
            public JObject SourceRoot { get; }

            public static CharacterActionMechanicTuningAuthoringEditResult SuccessResult(
                string message,
                JObject sourceRoot)
            {
                return new CharacterActionMechanicTuningAuthoringEditResult(true, message, sourceRoot);
            }

            public static CharacterActionMechanicTuningAuthoringEditResult Failure(string message)
            {
                return new CharacterActionMechanicTuningAuthoringEditResult(false, message, null);
            }
        }

        public static string FormatMeters(int centimeters)
        {
            return (centimeters / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CharacterActionMechanicTuningAuthoringStoreOptions
    {
        public CharacterActionMechanicTuningAuthoringStoreOptions(
            string abilityAuthoringPath,
            string generatedMechanicTuningsPath,
            string playerMirrorMechanicTuningsPath,
            bool refreshAssetDatabase = true)
        {
            AbilityAuthoringPath = abilityAuthoringPath ?? string.Empty;
            GeneratedMechanicTuningsPath = generatedMechanicTuningsPath ?? string.Empty;
            PlayerMirrorMechanicTuningsPath = playerMirrorMechanicTuningsPath ?? string.Empty;
            RefreshAssetDatabase = refreshAssetDatabase;
        }

        public string AbilityAuthoringPath { get; }
        public string GeneratedMechanicTuningsPath { get; }
        public string PlayerMirrorMechanicTuningsPath { get; }
        public bool RefreshAssetDatabase { get; }

        public static CharacterActionMechanicTuningAuthoringStoreOptions Default =>
            new CharacterActionMechanicTuningAuthoringStoreOptions(
                CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath,
                CharacterActionMechanicTuningAuthoringStore.DefaultGeneratedMechanicTuningsPath,
                CharacterActionMechanicTuningAuthoringStore.DefaultPlayerMirrorMechanicTuningsPath);
    }
}
