using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestActionCompatibility
    {
        internal readonly bool CanSelect;
        internal readonly string Reason, Diagnostic, Guidance;
        internal CharacterTestActionCompatibility(bool canSelect, string reason, string diagnostic = "", string guidance = "")
        { CanSelect = canSelect; Reason = reason ?? ""; Diagnostic = diagnostic ?? ""; Guidance = guidance ?? ""; }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly Dictionary<int, CharacterTestActionCompatibility> _actionBrowserCompatibility = new();
        private string _actionBrowserContext;
        internal static string AuthoringActionKey(int actionId) => "action:" + actionId.ToString(CultureInfo.InvariantCulture);

        internal static int ResolveAuthoringActionId(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (value.StartsWith("action:", StringComparison.Ordinal))
                return int.TryParse(value.Substring(7), NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0 ? id : 0;
            return CharacterTacticalServices.Player?.Catalog.Action(value)?.ActionId ?? 0;
        }

        internal void RefreshActionBrowser()
        {
            CharacterTestActionBrowserCatalog.Get(refresh: true);
            _actionBrowserCompatibility.Clear(); _actionBrowserContext = null;
            RefreshTacticalPreview();
        }

        internal void RequestActionBrowserSummary(int actionId, bool batch)
            => StartRuntimeSummary(actionId, batch);

        internal CharacterTestActionCompatibility EvaluateBrowserAction(int id, bool force = false)
        {
            var catalog = CharacterTestActionBrowserCatalog.Get();
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            var live = _context?.Slice?.CharacterRuntime?.ActionRuntime;
            CharacterTacticalActionEligibility eligibility;
            try { eligibility = CharacterTacticalActionEligibility.Load(); }
            catch (Exception error) when (error is System.IO.IOException || error is Newtonsoft.Json.JsonException || error is ArgumentException)
            { return new CharacterTestActionCompatibility(false, "角色分类范围配置有误", error.Message, "修复角色的可拥有动作分类配置后刷新。"); }
            string context = TacticalPreviewBindingContextKey + "|" + catalog.Fingerprint + "|"
                + CharacterTestTacticalPreparationCatalog.Revision + "|" + (live?.Registry?.GetHashCode() ?? 0) + "|" + eligibility.GetHashCode();
            if (_actionBrowserContext != context) { _actionBrowserContext = context; _actionBrowserCompatibility.Clear(); }
            if (!force && _actionBrowserCompatibility.TryGetValue(id, out var cached)) return cached;
            var entry = catalog.Actions.FirstOrDefault(a => a.ActionId == id);
            CharacterTestActionCompatibility result;
            if (entry == null) result = new CharacterTestActionCompatibility(false, "动作已不存在", guidance: "点击“刷新目录”，重新选择动作。");
            else if (entry.Issues.Length > 0) result = new CharacterTestActionCompatibility(false, "目录数据有误", string.Join("\n", entry.Issues), "修复动作目录中的问题：\n" + string.Join("\n", entry.Issues));
            else if (entry.Category != CharacterActionCategory.BasicAttack && entry.Category != CharacterActionCategory.Skill
                && entry.Category != CharacterActionCategory.Dodge && entry.Category != CharacterActionCategory.Guard)
                result = new CharacterTestActionCompatibility(false, "暂不支持此类型", "Action:UnsupportedCategory", "当前类型为“" + entry.TypeLabel + "”。战术动作事件目前支持攻击、技能、闪避和防御，请选择受支持的动作。");
            else if (character <= 0) result = new CharacterTestActionCompatibility(false, "未选择测试角色", guidance: "回到实验设置，先选择测试角色。");
            else
            {
                try
                {
                    var role = CharacterTestTacticalPreparationCatalog.Role(character, live);
                    if (!eligibility.Allows(role.Source.Identity.CharacterStableId, entry))
                    {
                        result = new CharacterTestActionCompatibility(false, "不在角色允许的分类内", "Action:CategoryNotAllowed",
                            "角色的可拥有范围由独立分类规则决定；请检查该角色允许的武器分类和动作类型。基础行为方案不会授予战术动作。");
                        _actionBrowserCompatibility[id] = result;
                        return result;
                    }
                    bool success = CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                        CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                        new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                        out _, out string issue, id,
                        CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.FunctionalActions
                        | (entry.Category == CharacterActionCategory.Guard ? CharacterTestTacticalInputRequirements.None : CharacterTestTacticalInputRequirements.SpatialSummary));
                    result = success ? new CharacterTestActionCompatibility(true, "当前角色可使用") : BrowserActionIssue(issue);
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is System.IO.IOException)
                { result = new CharacterTestActionCompatibility(false, "资源或角色配置不可用", error.Message, "检查角色与动作的资源导入结果，修复后重新进入测试。悬停详情可查看原始诊断。"); }
            }
            if (!result.CanSelect && result.Diagnostic.StartsWith("ActionSummary:", StringComparison.Ordinal))
            {
                string failed = RuntimeSummaryFailure(id);
                if (!string.IsNullOrEmpty(failed)) result = BrowserActionIssue("ActionSummary:" + failed);
            }
            _actionBrowserCompatibility[id] = result;
            return result;
        }

        private static CharacterTestActionCompatibility BrowserActionIssue(string issue)
        {
            if (CharacterTestTacticalSummaryDiagnostics.TryDescribe(issue, out string reason, out string guidance))
                return new CharacterTestActionCompatibility(false, reason, issue, guidance);
            (string reason, string guidance) message = issue switch {
                "Action:ExplicitSelectionRequired" => ("尚未选择战术动作", "请为战术卡明确选择动作。基础行为方案中的攻击、闪避映射不会自动填入战术卡。"),
                "Action:FiniteDurationRequired" => ("动作没有有限时长", "在动作编排中设置有效的有限持续帧数；循环或无限持续动作目前不能用于此处。"),
                "Action:PhaseOutsideDuration" => ("动作阶段超出时长", "在动作编排中调整阶段范围，使各阶段位于动作持续帧数之内。"),
                "Action:SingleStartupRequired" => ("存在多个前摇阶段", "此执行器要求单个前摇阶段，请调整动作阶段或选择其它动作。"),
                "Action:SingleActiveRequired" => ("存在多个生效阶段", "此执行器要求单个生效阶段，多段生效动作暂不支持。"),
                "Action:SingleRecoveryRequired" => ("存在多个后摇阶段", "此执行器最多支持一个后摇阶段，请调整动作阶段或选择其它动作。"),
                "Action:UnsupportedPhaseKind" => ("动作阶段类型不支持", "此处支持前摇、生效与后摇阶段；循环、引导等阶段暂不支持。"),
                "Action:ContiguousStartupActiveRequired" => ("前摇与生效阶段不连续", "前摇必须从第 0 帧开始，生效阶段紧接前摇；在动作编排中补齐或调整。"),
                "Action:ContiguousCompletionRequired" => ("结束阶段不连续", "后摇应紧接生效阶段，最后一个阶段应覆盖动作的最后一帧。"),
                "Action:DirectHitRequired" => ("缺少直接命中事件", "当前战术攻击需要直接命中配置。仅动画、投射物或增益技能不能通过这项检查；请选其它动作或补齐符合设计的命中配置。"),
                "Action:DirectHitOutsideActive" => ("命中超出生效阶段", "在动作编排中将直接命中事件放入生效阶段范围。"),
                "ActionSlot:SelectedTestActionUnavailable" => ("动作未加载到测试中", "检查动作库是否已导入运行目录，更新生成资源并重新进入测试。无需将动作加入基础行为方案。"),
                "ActionSlot:MissingCurrentActionSet" => ("当前角色没有动作方案", "先配置角色当前装备所使用的动作方案，再重新进入测试。"),
                "CurrentInputsMissing" => ("测试角色尚未就绪", "先在实验设置选择并加载测试角色，再打开动作库。"),
                "ActionSummary:HurtboxSurfaceMeasurementRequired" => ("摘要测量方式不匹配", "使用当前角色及标准测量规格重新准备战术摘要。"),
                "ActionSummary:ClosedContinuousVerifiedRangeRequired" => ("摘要没有有效距离区间", "需要已验证、闭合且连续的命中距离区间。检查命中轨迹和测量结果，重新准备摘要；单纯刷新目录不能修复。"),
                "ActionSummary:OptimalOutsideRange" => ("最佳距离超出有效范围", "检查战术测量结果并重新准备摘要，最佳距离必须在有效命中区间内。"),
                "ActionSummary:CentredFacingSectorRequired" => ("摘要朝向范围不符合要求", "当前执行器要求以正前方为中心的有效朝向范围，请检查命中配置和测量结果。"),
                _ => ("执行校验未通过", "检查角色装配、动作资源及武器轨迹。悬停详情可查看原始诊断。")
            };
            if (issue?.StartsWith("ActionSummary:", StringComparison.Ordinal) == true && message.reason == "执行校验未通过")
            {
                if (issue.Contains("SummaryLegacyFormatRequiresRemeasure"))
                    message = ("旧格式摘要需要重新测量", "点击“重建所选摘要”或“全量重建摘要”。旧 JSON 不再作为有效缓存读取。");
                else if (issue.Contains("Stale") || issue.Contains("Changed"))
                    message = ("战术摘要已过期", "角色、装备或动作数据已变化。使用当前配置重新准备此动作的战术摘要后刷新目录。");
                else if (issue.Contains("NoHit"))
                    message = ("测量未发现有效命中", "检查命中轨迹、装备和测量靶配置，修复后重新准备战术摘要。");
                else if (issue.Contains("Discontinuous") || issue.Contains("OpenBounded"))
                    message = ("命中距离区间不支持", "当前执行器要求闭合且连续的命中距离区间，请检查测量结果并选择符合要求的动作。");
                else if (issue.Contains("Missing") || issue.Contains("Unavailable"))
                    message = ("战术摘要不可用", "在战术摘要准备工具中，为当前角色、装备和此动作准备摘要；若准备失败，按工具中的资源诊断修复。");
                else message = ("战术摘要不符合要求", "查看战术摘要准备工具中的测量结果与诊断，修复后重新准备。");
            }
            return new CharacterTestActionCompatibility(false, message.reason, issue, message.guidance);
        }
    }
}
