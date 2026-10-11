using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Config.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestGuidanceAuthoringDocument
    {
        internal const int CurrentSchemaVersion = 1;

        public CharacterTestGuidanceAuthoringDocument()
        {
            Enabled = true;
            Steps = new List<CharacterTestGuidanceStepAuthoring>();
        }

        public bool Enabled { get; set; }
        public List<CharacterTestGuidanceStepAuthoring> Steps { get; set; }

        internal CharacterTestGuidanceAuthoringDocument Normalize()
        {
            var normalized = new CharacterTestGuidanceAuthoringDocument
            {
                Enabled = Enabled,
                Steps = new List<CharacterTestGuidanceStepAuthoring>()
            };
            if (Steps == null)
                return normalized;

            for (int i = 0; i < Steps.Count; i++)
            {
                if (Steps[i] != null)
                    normalized.Steps.Add(Steps[i].Normalize());
            }
            return normalized;
        }

        internal static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class CharacterTestGuidanceStepAuthoring
    {
        public CharacterTestGuidanceStepAuthoring()
        {
            StableId = string.Empty;
            Enabled = true;
            InputActionId = string.Empty;
            Content = new CharacterTestGuidanceContentAuthoring();
            Completion = new CharacterTestGuidanceCompletionAuthoring();
        }

        public string StableId { get; set; }
        public bool Enabled { get; set; }
        public string InputActionId { get; set; }
        public double AutoAdvanceSeconds { get; set; }
        public CharacterTestGuidanceContentAuthoring Content { get; set; }
        public CharacterTestGuidanceCompletionAuthoring Completion { get; set; }

        internal CharacterTestGuidanceStepAuthoring Normalize()
        {
            return new CharacterTestGuidanceStepAuthoring
            {
                StableId = CharacterTestGuidanceAuthoringDocument.Trim(StableId),
                Enabled = Enabled,
                InputActionId = CharacterTestGuidanceAuthoringDocument.Trim(InputActionId),
                AutoAdvanceSeconds = AutoAdvanceSeconds,
                Content = (Content ?? new CharacterTestGuidanceContentAuthoring()).Normalize(),
                Completion = (Completion ?? new CharacterTestGuidanceCompletionAuthoring()).Normalize()
            };
        }
    }

    internal sealed class CharacterTestGuidanceTextAuthoring
    {
        public CharacterTestGuidanceTextAuthoring()
        {
            Key = string.Empty;
            Fallback = string.Empty;
        }

        public CharacterTestGuidanceTextAuthoring(string key, string fallback)
        {
            Key = key ?? string.Empty;
            Fallback = fallback ?? string.Empty;
        }

        public string Key { get; set; }
        public string Fallback { get; set; }

        internal CharacterTestGuidanceTextAuthoring Normalize()
        {
            return new CharacterTestGuidanceTextAuthoring(
                CharacterTestGuidanceAuthoringDocument.Trim(Key),
                CharacterTestGuidanceAuthoringDocument.Trim(Fallback));
        }
    }

    internal sealed class CharacterTestGuidanceContentAuthoring
    {
        public CharacterTestGuidanceContentAuthoring()
        {
            Title = new CharacterTestGuidanceTextAuthoring();
            ActionInstruction = new CharacterTestGuidanceTextAuthoring();
            Focus = new CharacterTestGuidanceTextAuthoring();
            WaitingStatus = new CharacterTestGuidanceTextAuthoring();
            CompletedStatus = new CharacterTestGuidanceTextAuthoring();
            SuccessNotice = new CharacterTestGuidanceTextAuthoring();
        }

        public CharacterTestGuidanceTextAuthoring Title { get; set; }
        public CharacterTestGuidanceTextAuthoring ActionInstruction { get; set; }
        public CharacterTestGuidanceTextAuthoring Focus { get; set; }
        public CharacterTestGuidanceTextAuthoring WaitingStatus { get; set; }
        public CharacterTestGuidanceTextAuthoring CompletedStatus { get; set; }
        public CharacterTestGuidanceTextAuthoring SuccessNotice { get; set; }

        internal CharacterTestGuidanceContentAuthoring Normalize()
        {
            return new CharacterTestGuidanceContentAuthoring
            {
                Title = (Title ?? new CharacterTestGuidanceTextAuthoring()).Normalize(),
                ActionInstruction = (ActionInstruction ?? new CharacterTestGuidanceTextAuthoring()).Normalize(),
                Focus = (Focus ?? new CharacterTestGuidanceTextAuthoring()).Normalize(),
                WaitingStatus = (WaitingStatus ?? new CharacterTestGuidanceTextAuthoring()).Normalize(),
                CompletedStatus = (CompletedStatus ?? new CharacterTestGuidanceTextAuthoring()).Normalize(),
                SuccessNotice = (SuccessNotice ?? new CharacterTestGuidanceTextAuthoring()).Normalize()
            };
        }
    }

    internal sealed class CharacterTestGuidanceCompletionAuthoring
    {
        public CharacterTestGuidanceCompletionAuthoring()
        {
            TypeId = string.Empty;
            SchemaVersion = 1;
            Payload = new JObject();
        }

        public string TypeId { get; set; }
        public int SchemaVersion { get; set; }
        public JObject Payload { get; set; }

        internal CharacterTestGuidanceCompletionAuthoring Normalize()
        {
            return new CharacterTestGuidanceCompletionAuthoring
            {
                TypeId = CharacterTestGuidanceAuthoringDocument.Trim(TypeId),
                SchemaVersion = SchemaVersion,
                Payload = Payload == null ? new JObject() : (JObject)Payload.DeepClone()
            };
        }
    }

    internal enum CharacterTestGuidanceAuthoringSeverity : byte
    {
        Warning = 0,
        Error = 1
    }

    internal sealed class CharacterTestGuidanceAuthoringIssue
    {
        internal CharacterTestGuidanceAuthoringIssue(
            CharacterTestGuidanceAuthoringSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal CharacterTestGuidanceAuthoringSeverity Severity { get; }
        internal string Code { get; }
        internal string Path { get; }
        internal string Message { get; }
    }

    internal sealed class CharacterTestGuidanceAuthoringReport
    {
        internal static readonly CharacterTestGuidanceAuthoringReport Empty =
            new CharacterTestGuidanceAuthoringReport(Array.Empty<CharacterTestGuidanceAuthoringIssue>());

        internal CharacterTestGuidanceAuthoringReport(
            IReadOnlyList<CharacterTestGuidanceAuthoringIssue> issues)
        {
            var copy = new List<CharacterTestGuidanceAuthoringIssue>();
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < (issues?.Count ?? 0); i++)
            {
                CharacterTestGuidanceAuthoringIssue issue = issues[i];
                if (issue == null)
                    continue;
                copy.Add(issue);
                if (issue.Severity == CharacterTestGuidanceAuthoringSeverity.Error)
                    errors++;
                else
                    warnings++;
            }
            Issues = copy;
            ErrorCount = errors;
            WarningCount = warnings;
        }

        internal IReadOnlyList<CharacterTestGuidanceAuthoringIssue> Issues { get; }
        internal int ErrorCount { get; }
        internal int WarningCount { get; }
        internal bool HasErrors => ErrorCount > 0;
    }

    internal sealed class CharacterTestGuidanceStepPreset
    {
        private readonly JObject _completionPayload;

        internal CharacterTestGuidanceStepPreset(
            string presetId,
            string displayName,
            string stableIdSeed,
            string inputActionId,
            double autoAdvanceSeconds,
            string completionTypeId,
            JObject completionPayload,
            string title,
            string actionInstruction,
            string focus,
            string waitingStatus,
            string completedStatus,
            string successNotice)
        {
            PresetId = presetId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            StableIdSeed = stableIdSeed ?? string.Empty;
            InputActionId = inputActionId ?? string.Empty;
            AutoAdvanceSeconds = autoAdvanceSeconds;
            CompletionTypeId = completionTypeId ?? string.Empty;
            _completionPayload = completionPayload == null
                ? new JObject()
                : (JObject)completionPayload.DeepClone();
            Title = title ?? string.Empty;
            ActionInstruction = actionInstruction ?? string.Empty;
            Focus = focus ?? string.Empty;
            WaitingStatus = waitingStatus ?? string.Empty;
            CompletedStatus = completedStatus ?? string.Empty;
            SuccessNotice = successNotice ?? string.Empty;
        }

        internal string PresetId { get; }
        internal string DisplayName { get; }
        internal string StableIdSeed { get; }
        internal string InputActionId { get; }
        internal double AutoAdvanceSeconds { get; }
        internal string CompletionTypeId { get; }
        internal string Title { get; }
        internal string ActionInstruction { get; }
        internal string Focus { get; }
        internal string WaitingStatus { get; }
        internal string CompletedStatus { get; }
        internal string SuccessNotice { get; }

        internal JObject CreateCompletionPayload() => (JObject)_completionPayload.DeepClone();
    }

    internal static class CharacterTestGuidanceStepPresetCatalog
    {
        internal const string BasicMovement = "basic-movement";
        internal const string JumpAndLand = "jump-and-land";
        internal const string CameraModeSwitch = "camera-mode-switch";
        internal const string TargetLock = "target-lock";
        internal const string TargetUnlock = "target-unlock";
        internal const string BasicAttackHit = "basic-attack-hit";

        private static readonly CharacterTestGuidanceStepPreset[] PresetsInternal =
        {
            new CharacterTestGuidanceStepPreset(
                BasicMovement,
                "基础移动与停步",
                "basic-movement",
                "Gameplay/Move",
                2d,
                CharacterTestGuidanceCompletionRegistry.MoveDistanceAndStop,
                CharacterTestGuidanceCompletionRegistry.CreateDefaultPayload(
                    CharacterTestGuidanceCompletionRegistry.MoveDistanceAndStop),
                "基础移动与停步",
                "使用移动键前进后停下",
                "达到目标距离，随后速度归零并保持落地",
                "等待达到目标距离并停下",
                "已完成：移动与停步已确认，即将继续",
                "移动距离与停步状态均已确认"),
            new CharacterTestGuidanceStepPreset(
                JumpAndLand,
                "跳跃与落地",
                "jump-and-land",
                "Gameplay/Jump",
                2d,
                CharacterTestGuidanceCompletionRegistry.JumpAndLand,
                new JObject(),
                "跳跃与落地",
                "跳起并落回地面",
                "角色先离地，再重新落地",
                "等待一次完整的离地与落地",
                "已完成：离地与落地已确认，即将继续",
                "离地与再次落地均已确认"),
            new CharacterTestGuidanceStepPreset(
                CameraModeSwitch,
                "切换相机模式",
                "camera-mode-switch",
                "Gameplay/CameraModeNext",
                2d,
                CharacterTestGuidanceCompletionRegistry.CameraModeChanged,
                new JObject(),
                "切换相机模式",
                "切换一次相机模式",
                "等待运行时确认相机模式已经改变",
                "等待相机模式切换事件",
                "已完成：相机模式切换已确认，即将继续",
                "相机模式切换事件已确认"),
            new CharacterTestGuidanceStepPreset(
                TargetLock,
                "锁定目标",
                "target-lock",
                "Gameplay/TargetLock",
                2d,
                CharacterTestGuidanceCompletionRegistry.TargetLockState,
                TargetLockPayload(true),
                "锁定目标",
                "锁定一个可用目标",
                "锁定标记、角色朝向与镜头跟随",
                "等待锁定目标",
                "已完成：目标锁定已确认，即将继续",
                "目标锁定已确认"),
            new CharacterTestGuidanceStepPreset(
                TargetUnlock,
                "取消锁定",
                "target-unlock",
                "Gameplay/TargetLock",
                2d,
                CharacterTestGuidanceCompletionRegistry.TargetLockState,
                TargetLockPayload(false),
                "取消锁定",
                "再次执行目标锁定操作以取消锁定",
                "锁定标记消失，镜头恢复自由跟随",
                "等待取消目标锁定",
                "已完成：目标锁定已取消，即将继续",
                "目标锁定取消已确认"),
            new CharacterTestGuidanceStepPreset(
                BasicAttackHit,
                "普通攻击命中",
                "basic-attack-hit",
                "Gameplay/AttackPrimary",
                0d,
                CharacterTestGuidanceCompletionRegistry.BasicAttackDamage,
                CharacterTestGuidanceCompletionRegistry.CreateDefaultPayload(
                    CharacterTestGuidanceCompletionRegistry.BasicAttackDamage),
                "普通攻击命中",
                "用普通攻击命中训练目标",
                "本地玩家造成匹配的普通攻击伤害，且目标生命值下降",
                "等待普通攻击对训练目标造成伤害",
                "已完成：普通攻击造成了真实伤害，本轮测试完成",
                "普通攻击命中并造成真实伤害")
        };

        internal static IReadOnlyList<CharacterTestGuidanceStepPreset> Presets => PresetsInternal;

        internal static bool TryGet(string presetId, out CharacterTestGuidanceStepPreset preset)
        {
            for (int i = 0; i < PresetsInternal.Length; i++)
            {
                if (string.Equals(
                        PresetsInternal[i].PresetId,
                        presetId,
                        StringComparison.Ordinal))
                {
                    preset = PresetsInternal[i];
                    return true;
                }
            }
            preset = null;
            return false;
        }

        private static JObject TargetLockPayload(bool expectedLocked)
        {
            return new JObject { ["expectedLocked"] = expectedLocked };
        }
    }

    internal static class CharacterTestGuidanceCompletionRegistry
    {
        internal const string MoveDistanceAndStop = "MoveDistanceAndStop";
        internal const string JumpAndLand = "JumpAndLand";
        internal const string CameraModeChanged = "CameraModeChanged";
        internal const string TargetLockState = "TargetLockState";
        internal const string BasicAttackDamage = "BasicAttackDamage";

        private static readonly string[] TypeIdsInternal =
        {
            MoveDistanceAndStop,
            JumpAndLand,
            CameraModeChanged,
            TargetLockState,
            BasicAttackDamage
        };

        internal static IReadOnlyList<string> TypeIds => TypeIdsInternal;

        internal static bool IsRegistered(string typeId)
        {
            for (int i = 0; i < TypeIdsInternal.Length; i++)
            {
                if (string.Equals(TypeIdsInternal[i], typeId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static ICharacterTestGuidanceCompletionObserver CreateObserver(
            CharacterTestGuidanceStepDefinition step)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            switch (step.CompletionTypeId)
            {
                case MoveDistanceAndStop:
                    return new CharacterTestGuidanceMoveDistanceAndStopObserver(
                        step.CompletionPayload);
                case JumpAndLand:
                    return new CharacterTestGuidanceJumpAndLandObserver();
                case CameraModeChanged:
                    return new CharacterTestGuidanceCameraModeChangedObserver();
                case TargetLockState:
                    return new CharacterTestGuidanceTargetLockStateObserver(
                        step.CompletionPayload);
                case BasicAttackDamage:
                    return new CharacterTestGuidanceBasicAttackDamageObserver(
                        step.CompletionPayload);
                default:
                    throw new InvalidOperationException(
                        "Guidance completion condition is not registered: "
                        + step.CompletionTypeId);
            }
        }

        internal static string CatalogHash => CharacterTestGuidanceCanonicalJson.ComputeHash(
            "v1\n"
            + MoveDistanceAndStop + ":1\n"
            + JumpAndLand + ":1\n"
            + CameraModeChanged + ":1\n"
            + TargetLockState + ":1\n"
            + BasicAttackDamage + ":1\n");

        internal static JObject CreateDefaultPayload(string typeId)
        {
            switch (typeId)
            {
                case MoveDistanceAndStop:
                    return new JObject
                    {
                        ["minimumDistanceMillimeters"] = 2000,
                        ["maximumStopSpeedMillimetersPerSecond"] = 150,
                        ["requireGrounded"] = true
                    };
                case TargetLockState:
                    return new JObject { ["expectedLocked"] = true };
                case BasicAttackDamage:
                    return new JObject
                    {
                        ["actionId"] = CharacterTestCharacterActionConfigSource.LightAttackActionId.Value,
                        ["minimumFinalDamage"] = 1
                    };
                default:
                    return new JObject();
            }
        }

        internal static JObject NormalizePayload(string typeId, JObject payload)
        {
            payload ??= new JObject();
            switch (typeId)
            {
                case MoveDistanceAndStop:
                    return new JObject
                    {
                        ["minimumDistanceMillimeters"] = ReadInt(payload, "minimumDistanceMillimeters", 2000),
                        ["maximumStopSpeedMillimetersPerSecond"] = ReadInt(payload, "maximumStopSpeedMillimetersPerSecond", 150),
                        ["requireGrounded"] = ReadBool(payload, "requireGrounded", true)
                    };
                case TargetLockState:
                    return new JObject
                    {
                        ["expectedLocked"] = ReadBool(payload, "expectedLocked", true)
                    };
                case BasicAttackDamage:
                    return new JObject
                    {
                        ["actionId"] = ReadInt(
                            payload,
                            "actionId",
                            CharacterTestCharacterActionConfigSource.LightAttackActionId.Value),
                        ["minimumFinalDamage"] = ReadInt(payload, "minimumFinalDamage", 1)
                    };
                default:
                    return new JObject();
            }
        }

        internal static void Validate(
            CharacterTestGuidanceCompletionAuthoring completion,
            string path,
            List<CharacterTestGuidanceAuthoringIssue> issues)
        {
            if (completion == null)
            {
                Error(issues, "MissingCompletion", path, "A completion condition is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(completion.TypeId))
            {
                Error(issues, "MissingCompletionType", path + ".typeId",
                    "A registered completion condition must be selected.");
                return;
            }
            if (completion.SchemaVersion != 1)
            {
                Error(issues, "UnknownCompletionSchema", path + ".schemaVersion", "Only completion schema v1 is supported.");
                return;
            }
            JObject payload = completion.Payload ?? new JObject();
            switch (completion.TypeId)
            {
                case MoveDistanceAndStop:
                    ValidateFields(payload, path + ".payload", issues,
                        "minimumDistanceMillimeters", "maximumStopSpeedMillimetersPerSecond", "requireGrounded");
                    RequireInt(payload, "minimumDistanceMillimeters", 1, path, issues);
                    RequireInt(payload, "maximumStopSpeedMillimetersPerSecond", 0, path, issues);
                    RequireBool(payload, "requireGrounded", path, issues);
                    break;
                case JumpAndLand:
                case CameraModeChanged:
                    ValidateFields(payload, path + ".payload", issues);
                    break;
                case TargetLockState:
                    ValidateFields(payload, path + ".payload", issues, "expectedLocked");
                    RequireBool(payload, "expectedLocked", path, issues);
                    break;
                case BasicAttackDamage:
                    ValidateFields(payload, path + ".payload", issues, "actionId", "minimumFinalDamage");
                    RequireInt(payload, "actionId", 1, path, issues);
                    RequireInt(payload, "minimumFinalDamage", 1, path, issues);
                    break;
                default:
                    Error(issues, "UnknownCompletionType", path + ".typeId", "Completion condition is not registered.");
                    break;
            }
        }

        private static int ReadInt(JObject payload, string name, int fallback)
        {
            return payload.TryGetValue(name, StringComparison.Ordinal, out JToken token)
                && token.Type == JTokenType.Integer
                    ? token.Value<int>()
                    : fallback;
        }

        private static bool ReadBool(JObject payload, string name, bool fallback)
        {
            return payload.TryGetValue(name, StringComparison.Ordinal, out JToken token)
                && token.Type == JTokenType.Boolean
                    ? token.Value<bool>()
                    : fallback;
        }

        private static void RequireInt(
            JObject payload,
            string name,
            int minimum,
            string path,
            List<CharacterTestGuidanceAuthoringIssue> issues)
        {
            if (!payload.TryGetValue(name, StringComparison.Ordinal, out JToken token)
                || token.Type != JTokenType.Integer
                || token.Value<long>() < minimum
                || token.Value<long>() > int.MaxValue)
            {
                Error(issues, "InvalidIntegerParameter", path + ".payload." + name,
                    name + " must be an integer greater than or equal to " + minimum + ".");
            }
        }

        private static void RequireBool(
            JObject payload,
            string name,
            string path,
            List<CharacterTestGuidanceAuthoringIssue> issues)
        {
            if (!payload.TryGetValue(name, StringComparison.Ordinal, out JToken token)
                || token.Type != JTokenType.Boolean)
            {
                Error(issues, "InvalidBooleanParameter", path + ".payload." + name,
                    name + " must be a boolean.");
            }
        }

        private static void ValidateFields(
            JObject payload,
            string path,
            List<CharacterTestGuidanceAuthoringIssue> issues,
            params string[] allowed)
        {
            var allowedSet = new HashSet<string>(allowed ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (JProperty property in payload.Properties())
            {
                if (!allowedSet.Contains(property.Name))
                    Error(issues, "UnknownCompletionParameter", path + "." + property.Name,
                        "Completion parameter is not part of the typed condition.");
            }
        }

        private static void Error(
            List<CharacterTestGuidanceAuthoringIssue> issues,
            string code,
            string path,
            string message)
        {
            issues.Add(new CharacterTestGuidanceAuthoringIssue(
                CharacterTestGuidanceAuthoringSeverity.Error, code, path, message));
        }
    }

    internal static class CharacterTestGuidanceAuthoringValidator
    {
        internal static CharacterTestGuidanceAuthoringReport Validate(
            CharacterTestGuidanceAuthoringDocument document)
        {
            var issues = new List<CharacterTestGuidanceAuthoringIssue>();
            if (document == null)
            {
                Error(issues, "MissingDocument", "$", "Guidance authoring document is missing.");
                return new CharacterTestGuidanceAuthoringReport(issues);
            }

            CharacterTestGuidanceAuthoringDocument normalized = document.Normalize();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            int enabledCount = 0;
            for (int i = 0; i < normalized.Steps.Count; i++)
            {
                CharacterTestGuidanceStepAuthoring step = normalized.Steps[i];
                string path = "steps[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (string.IsNullOrEmpty(step.StableId))
                    Error(issues, "MissingStableId", path + ".stableId", "Stable ID is required.");
                else if (!stableIds.Add(step.StableId))
                    Error(issues, "DuplicateStableId", path + ".stableId", "Stable ID must be unique in this module.");
                if (step.Enabled)
                    enabledCount++;
                if (string.IsNullOrWhiteSpace(step.InputActionId))
                {
                    Error(issues, "MissingInputActionId", path + ".inputActionId",
                        "A formal Gameplay input action is required.");
                }
                else if (!IsGameplayActionId(step.InputActionId))
                    Error(issues, "InvalidInputActionId", path + ".inputActionId", "Input action must use Gameplay/Action form.");
                if (double.IsNaN(step.AutoAdvanceSeconds)
                    || double.IsInfinity(step.AutoAdvanceSeconds)
                    || step.AutoAdvanceSeconds < 0d
                    || step.AutoAdvanceSeconds > float.MaxValue)
                {
                    Error(issues, "InvalidAutoAdvanceSeconds", path + ".autoAdvanceSeconds",
                        "Auto advance seconds must be finite, non-negative, and representable at runtime.");
                }
                if (!string.IsNullOrEmpty(step.StableId)
                    && !CharacterTestGuidanceAuthoringAdapter.IsValidStableId(step.StableId))
                {
                    Error(issues, "InvalidStableId", path + ".stableId",
                        "Stable ID must use lowercase letters, digits, and hyphens, and start with a letter or digit.");
                }
                ValidateText(
                    step.Content?.Title,
                    path + ".content.title",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "title"),
                    issues);
                ValidateText(
                    step.Content?.ActionInstruction,
                    path + ".content.actionInstruction",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "action"),
                    issues);
                ValidateText(
                    step.Content?.Focus,
                    path + ".content.focus",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "focus"),
                    issues);
                ValidateText(
                    step.Content?.WaitingStatus,
                    path + ".content.waitingStatus",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "waiting"),
                    issues);
                ValidateText(
                    step.Content?.CompletedStatus,
                    path + ".content.completedStatus",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "completed"),
                    issues);
                ValidateText(
                    step.Content?.SuccessNotice,
                    path + ".content.successNotice",
                    CharacterTestGuidanceAuthoringAdapter.CreateManagedTextKey(step.StableId, "success"),
                    issues);
                CharacterTestGuidanceCompletionRegistry.Validate(step.Completion, path + ".completion", issues);
            }

            if (normalized.Enabled && enabledCount == 0)
                Error(issues, "EmptyEnabledSequence", "steps", "An enabled guidance module needs at least one enabled step.");

            AddSuspiciousOrderWarnings(normalized, issues);
            return new CharacterTestGuidanceAuthoringReport(issues);
        }

        private static bool IsGameplayActionId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)
                || !actionId.StartsWith("Gameplay/", StringComparison.Ordinal))
                return false;
            int slash = actionId.IndexOf('/');
            return slash > 0 && slash == actionId.LastIndexOf('/') && slash < actionId.Length - 1;
        }

        private static void ValidateText(
            CharacterTestGuidanceTextAuthoring text,
            string path,
            string expectedKey,
            List<CharacterTestGuidanceAuthoringIssue> issues)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.Key))
                Error(issues, "MissingLocalizedTextKey", path + ".key", "Localization key is required.");
            else if (!string.Equals(text.Key, expectedKey, StringComparison.Ordinal))
                Error(issues, "UnexpectedLocalizedTextKey", path + ".key",
                    "Localization key is system-managed and must be '" + expectedKey + "'.");
            if (text == null || string.IsNullOrWhiteSpace(text.Fallback))
                Error(issues, "MissingLocalizedTextFallback", path + ".fallback", "Fallback text is required.");
        }

        private static void AddSuspiciousOrderWarnings(
            CharacterTestGuidanceAuthoringDocument document,
            List<CharacterTestGuidanceAuthoringIssue> issues)
        {
            bool hasLock = false;
            bool? lastLockExpectation = null;
            for (int i = 0; i < document.Steps.Count; i++)
            {
                CharacterTestGuidanceStepAuthoring step = document.Steps[i];
                if (!step.Enabled
                    || !string.Equals(step.Completion.TypeId,
                        CharacterTestGuidanceCompletionRegistry.TargetLockState,
                        StringComparison.Ordinal))
                    continue;
                bool expected = step.Completion.Payload?.Value<bool?>("expectedLocked") ?? false;
                if (!expected && !hasLock)
                {
                    Warning(issues, "TargetUnlockBeforeLock", "steps[" + i + "].completion",
                        "Target unlock appears before any enabled lock step. This is allowed but may be unintentional.");
                }
                if (lastLockExpectation.HasValue && lastLockExpectation.Value == expected)
                {
                    Warning(issues, "RepeatedTargetLockExpectation", "steps[" + i + "].completion",
                        "Consecutive target-lock conditions expect the same state.");
                }
                hasLock |= expected;
                lastLockExpectation = expected;
            }
        }

        private static void Error(
            List<CharacterTestGuidanceAuthoringIssue> issues,
            string code,
            string path,
            string message)
        {
            issues.Add(new CharacterTestGuidanceAuthoringIssue(
                CharacterTestGuidanceAuthoringSeverity.Error, code, path, message));
        }

        private static void Warning(
            List<CharacterTestGuidanceAuthoringIssue> issues,
            string code,
            string path,
            string message)
        {
            issues.Add(new CharacterTestGuidanceAuthoringIssue(
                CharacterTestGuidanceAuthoringSeverity.Warning, code, path, message));
        }
    }

    internal sealed class CharacterTestGuidanceStepDefinition
    {
        internal CharacterTestGuidanceStepDefinition(
            int authoringIndex,
            CharacterTestGuidanceStepAuthoring source)
        {
            AuthoringIndex = authoringIndex;
            StableId = source.StableId;
            InputActionId = source.InputActionId;
            AutoAdvanceSeconds = (float)source.AutoAdvanceSeconds;
            Content = source.Content.Normalize();
            CompletionTypeId = source.Completion.TypeId;
            CompletionSchemaVersion = source.Completion.SchemaVersion;
            CompletionPayload = CharacterTestGuidanceCompletionRegistry.NormalizePayload(
                source.Completion.TypeId,
                source.Completion.Payload);
        }

        internal int AuthoringIndex { get; }
        internal string StableId { get; }
        internal string InputActionId { get; }
        internal float AutoAdvanceSeconds { get; }
        internal CharacterTestGuidanceContentAuthoring Content { get; }
        internal string CompletionTypeId { get; }
        internal int CompletionSchemaVersion { get; }
        internal JObject CompletionPayload { get; }
    }

    internal sealed class CharacterTestGuidancePlan
    {
        internal CharacterTestGuidancePlan(
            bool enabled,
            IReadOnlyList<CharacterTestGuidanceStepDefinition> steps,
            string sourceHash,
            string catalogHash,
            string canonicalModuleJson)
        {
            Enabled = enabled;
            Steps = steps ?? Array.Empty<CharacterTestGuidanceStepDefinition>();
            SourceHash = sourceHash ?? string.Empty;
            CompletionCatalogHash = catalogHash ?? string.Empty;
            CanonicalModuleJson = canonicalModuleJson ?? string.Empty;
        }

        internal bool Enabled { get; }
        internal IReadOnlyList<CharacterTestGuidanceStepDefinition> Steps { get; }
        internal int StepCount => Steps.Count;
        internal string SourceHash { get; }
        internal string CompletionCatalogHash { get; }
        internal string CanonicalModuleJson { get; }
    }

    internal static class CharacterTestGuidanceCompiler
    {
        internal static bool TryCompile(
            CharacterTestGuidanceAuthoringDocument document,
            out CharacterTestGuidancePlan plan,
            out CharacterTestGuidanceAuthoringReport report)
        {
            report = CharacterTestGuidanceAuthoringValidator.Validate(document);
            if (report.HasErrors)
            {
                plan = null;
                return false;
            }

            CharacterTestGuidanceAuthoringDocument normalized = document.Normalize();
            string canonical = CharacterTestGuidanceCanonicalJson.SerializeModule(normalized);
            var steps = new List<CharacterTestGuidanceStepDefinition>();
            if (normalized.Enabled)
            {
                for (int i = 0; i < normalized.Steps.Count; i++)
                {
                    if (normalized.Steps[i].Enabled)
                        steps.Add(new CharacterTestGuidanceStepDefinition(i, normalized.Steps[i]));
                }
            }
            plan = new CharacterTestGuidancePlan(
                normalized.Enabled,
                steps,
                CharacterTestGuidanceCanonicalJson.ComputeHash(canonical),
                CharacterTestGuidanceCompletionRegistry.CatalogHash,
                canonical);
            return true;
        }
    }

    internal static class CharacterTestGuidanceCanonicalJson
    {
        internal static string SerializeModule(CharacterTestGuidanceAuthoringDocument document)
        {
            CharacterTestGuidanceAuthoringDocument normalized = document.Normalize();
            var steps = new JArray();
            for (int i = 0; i < normalized.Steps.Count; i++)
            {
                CharacterTestGuidanceStepAuthoring step = normalized.Steps[i];
                steps.Add(new JObject
                {
                    ["stableId"] = step.StableId,
                    ["enabled"] = step.Enabled,
                    ["inputActionId"] = step.InputActionId,
                    ["autoAdvanceSeconds"] = step.AutoAdvanceSeconds,
                    ["content"] = Content(step.Content),
                    ["completion"] = new JObject
                    {
                        ["typeId"] = step.Completion.TypeId,
                        ["schemaVersion"] = step.Completion.SchemaVersion,
                        ["payload"] = CharacterTestGuidanceCompletionRegistry.NormalizePayload(
                            step.Completion.TypeId,
                            step.Completion.Payload)
                    }
                });
            }

            var module = new JObject
            {
                ["moduleId"] = CharacterTestGuidanceAuthoringAdapter.ModuleId,
                ["schemaVersion"] = CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion,
                ["displayName"] = CharacterTestGuidanceAuthoringAdapter.DisplayName,
                ["payload"] = new JObject
                {
                    ["enabled"] = normalized.Enabled,
                    ["steps"] = steps
                }
            };
            return module.ToString(Formatting.None).Replace("\r\n", "\n");
        }

        internal static string ComputeHash(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static JObject Content(CharacterTestGuidanceContentAuthoring content)
        {
            content = content.Normalize();
            return new JObject
            {
                ["title"] = Text(content.Title),
                ["actionInstruction"] = Text(content.ActionInstruction),
                ["focus"] = Text(content.Focus),
                ["waitingStatus"] = Text(content.WaitingStatus),
                ["completedStatus"] = Text(content.CompletedStatus),
                ["successNotice"] = Text(content.SuccessNotice)
            };
        }

        private static JObject Text(CharacterTestGuidanceTextAuthoring text)
        {
            text = text.Normalize();
            return new JObject { ["key"] = text.Key, ["fallback"] = text.Fallback };
        }
    }

    internal static class CharacterTestGuidanceAuthoringAdapter
    {
        internal const string ModuleId = "characterTest.guidance";
        internal const int ModuleSchemaVersion = 1;
        internal const string DisplayName = "CharacterTest Guidance";
        internal const string DefaultScenePath = "Assets/Scenes/CharacterTestScene.unity";
        internal const string DefaultDocumentPath =
            "Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json";
        internal const string DefaultLocalizationPath =
            "Assets/StreamingAssets/MxFramework/Localization/character_test.tsv";
        internal const string SourceLocaleId = "zh-CN";
        internal const string TranslationLocaleId = "en-US";
        internal const string ManagedTextKeyPrefix = "ui.character_test.guidance.";

        internal static bool TryExtract(
            MxLevelAuthoringDocument envelope,
            out CharacterTestGuidanceAuthoringDocument document,
            out CharacterTestGuidanceRuntimeDiagnostic diagnostic)
        {
            document = null;
            if (envelope == null || !envelope.TryGetModule(ModuleId, out MxLevelAuthoringModuleDocument module))
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.ModuleMissing,
                    "The characterTest.guidance module is missing.");
                return false;
            }
            if (module.SchemaVersion != ModuleSchemaVersion)
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.ModuleSchemaUnsupported,
                    "Guidance module schema is not supported.");
                return false;
            }
            try
            {
                document = module.ReadPayload<CharacterTestGuidanceAuthoringDocument>();
                if (document == null)
                    throw new JsonException("Payload resolved to null.");
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.None;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.ModuleInvalid,
                    "Guidance payload cannot be read: " + exception.Message);
                return false;
            }
        }

        internal static MxLevelAuthoringDocument Upsert(
            MxLevelAuthoringDocument envelope,
            CharacterTestGuidanceAuthoringDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            envelope ??= new MxLevelAuthoringDocument();
            envelope = envelope.Normalize();
            envelope.UpsertModulePayload(
                ModuleId,
                ModuleSchemaVersion,
                document.Normalize(),
                DisplayName);
            return envelope.Normalize();
        }

        internal static CharacterTestGuidanceAuthoringDocument CreateEmptyDocument()
        {
            return new CharacterTestGuidanceAuthoringDocument { Enabled = false };
        }

        internal static CharacterTestGuidanceAuthoringDocument CreateSixStepReferenceDocument()
        {
            var document = new CharacterTestGuidanceAuthoringDocument { Enabled = true };
            for (int i = 0; i < CharacterTestGuidanceStepPresetCatalog.Presets.Count; i++)
            {
                CharacterTestGuidanceStepPreset preset =
                    CharacterTestGuidanceStepPresetCatalog.Presets[i];
                document.Steps.Add(CreateStepFromPreset(preset.PresetId, preset.StableIdSeed));
            }
            return document.Normalize();
        }

        internal static CharacterTestGuidanceStepAuthoring CreateBlankStep(string stableId)
        {
            var step = new CharacterTestGuidanceStepAuthoring
            {
                StableId = CharacterTestGuidanceAuthoringDocument.Trim(stableId),
                Enabled = true,
                InputActionId = string.Empty,
                AutoAdvanceSeconds = 2d,
                Content = new CharacterTestGuidanceContentAuthoring(),
                Completion = new CharacterTestGuidanceCompletionAuthoring
                {
                    TypeId = string.Empty,
                    SchemaVersion = 1,
                    Payload = new JObject()
                }
            };
            AssignManagedTextKeys(step);
            return step;
        }

        internal static CharacterTestGuidanceStepAuthoring CreateStepFromPreset(
            string presetId,
            string stableId)
        {
            if (!CharacterTestGuidanceStepPresetCatalog.TryGet(presetId, out CharacterTestGuidanceStepPreset preset))
                throw new ArgumentException("Guidance step preset is not registered.", nameof(presetId));
            CharacterTestGuidanceStepAuthoring step = CreateStep(
                stableId,
                preset.InputActionId,
                preset.AutoAdvanceSeconds,
                preset.CompletionTypeId,
                preset.Title,
                preset.ActionInstruction,
                preset.Focus,
                preset.WaitingStatus,
                preset.CompletedStatus,
                preset.SuccessNotice);
            step.Completion.Payload = preset.CreateCompletionPayload();
            return step;
        }

        internal static void ChangeCompletionType(
            CharacterTestGuidanceStepAuthoring step,
            string typeId)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));
            if (!CharacterTestGuidanceCompletionRegistry.IsRegistered(typeId))
                throw new ArgumentException("Guidance completion condition is not registered.", nameof(typeId));
            if (string.Equals(step.Completion?.TypeId, typeId, StringComparison.Ordinal)
                && step.Completion.SchemaVersion == 1)
            {
                return;
            }
            step.Completion = new CharacterTestGuidanceCompletionAuthoring
            {
                TypeId = typeId,
                SchemaVersion = 1,
                Payload = CharacterTestGuidanceCompletionRegistry.CreateDefaultPayload(typeId)
            };
        }

        private static CharacterTestGuidanceStepAuthoring CreateStep(
            string stableId,
            string actionId,
            double autoAdvanceSeconds,
            string completionType,
            string title,
            string instruction,
            string focus,
            string waiting,
            string completed,
            string notice)
        {
            var step = new CharacterTestGuidanceStepAuthoring
            {
                StableId = stableId,
                Enabled = true,
                InputActionId = actionId,
                AutoAdvanceSeconds = autoAdvanceSeconds,
                Content = new CharacterTestGuidanceContentAuthoring
                {
                    Title = new CharacterTestGuidanceTextAuthoring(string.Empty, title),
                    ActionInstruction = new CharacterTestGuidanceTextAuthoring(string.Empty, instruction),
                    Focus = new CharacterTestGuidanceTextAuthoring(string.Empty, focus),
                    WaitingStatus = new CharacterTestGuidanceTextAuthoring(string.Empty, waiting),
                    CompletedStatus = new CharacterTestGuidanceTextAuthoring(string.Empty, completed),
                    SuccessNotice = new CharacterTestGuidanceTextAuthoring(string.Empty, notice)
                },
                Completion = new CharacterTestGuidanceCompletionAuthoring
                {
                    TypeId = completionType,
                    SchemaVersion = 1,
                    Payload = CharacterTestGuidanceCompletionRegistry.CreateDefaultPayload(completionType)
                }
            };
            AssignManagedTextKeys(step);
            return step;
        }

        internal static void ChangeStableId(
            CharacterTestGuidanceStepAuthoring step,
            string stableId)
        {
            if (step == null)
                return;
            step.StableId = CharacterTestGuidanceAuthoringDocument.Trim(stableId);
            AssignManagedTextKeys(step);
        }

        internal static void AssignManagedTextKeys(CharacterTestGuidanceStepAuthoring step)
        {
            if (step == null)
                return;
            step.Content ??= new CharacterTestGuidanceContentAuthoring();
            step.Content.Title ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.ActionInstruction ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.Focus ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.WaitingStatus ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.CompletedStatus ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.SuccessNotice ??= new CharacterTestGuidanceTextAuthoring();
            step.Content.Title.Key = CreateManagedTextKey(step.StableId, "title");
            step.Content.ActionInstruction.Key = CreateManagedTextKey(step.StableId, "action");
            step.Content.Focus.Key = CreateManagedTextKey(step.StableId, "focus");
            step.Content.WaitingStatus.Key = CreateManagedTextKey(step.StableId, "waiting");
            step.Content.CompletedStatus.Key = CreateManagedTextKey(step.StableId, "completed");
            step.Content.SuccessNotice.Key = CreateManagedTextKey(step.StableId, "success");
        }

        internal static string CreateManagedTextKey(string stableId, string slot)
        {
            string normalizedId = CharacterTestGuidanceAuthoringDocument.Trim(stableId)
                .Replace('-', '_');
            return ManagedTextKeyPrefix + normalizedId + "." + (slot ?? string.Empty);
        }

        internal static bool IsManagedTextKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)
                || !key.StartsWith(ManagedTextKeyPrefix, StringComparison.Ordinal))
                return false;
            return key.EndsWith(".title", StringComparison.Ordinal)
                || key.EndsWith(".action", StringComparison.Ordinal)
                || key.EndsWith(".focus", StringComparison.Ordinal)
                || key.EndsWith(".waiting", StringComparison.Ordinal)
                || key.EndsWith(".completed", StringComparison.Ordinal)
                || key.EndsWith(".success", StringComparison.Ordinal);
        }

        internal static bool IsValidStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            for (int i = 0; i < stableId.Length; i++)
            {
                char value = stableId[i];
                bool alphaNumeric = value >= 'a' && value <= 'z'
                    || value >= '0' && value <= '9';
                if (!alphaNumeric && value != '-')
                    return false;
                if (value == '-' && (i == 0 || i == stableId.Length - 1))
                    return false;
            }
            return true;
        }
    }
}
