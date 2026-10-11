using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.ProjectileAuthoring
{
    public sealed partial class ProjectileAuthoringWindow
    {
        private static readonly string[] MotionKinds =
        {
            "Linear", "ArcToTarget", "BoomerangReturn", "SwayForward", "HomingToTarget", "BezierToTarget", "BallisticArcToTarget"
        };

        private static readonly string[] SpeedProfileKinds =
        {
            "Constant", "EaseInQuadratic", "EaseOutQuadratic", "EaseInOutSmoothstep"
        };

        private static readonly string[] AimKinds =
        {
            "CasterForwardRange", "LockedTargetCenter", "LockedTargetOrCasterForwardRange"
        };

        private static readonly string[] OriginKinds =
        {
            "Socket"
        };

        private const float DefaultArcHeight = 1f;
        private const float DefaultSwayAmplitude = .5f;
        private const int DefaultSwayCycles = 1;
        private const float DefaultBallisticGravity = .01f;
        private const float DefaultBallisticAngle = 45f;
        private const float DefaultHomingTurnRate = 1f;
        private const float DefaultAreaRadius = 1f;

        private static readonly string[] TargetTypes =
        {
            "None", "HostileCharacter", "FriendlyCharacter", "Self", "ThirdPartyCharacter", "DamageableObject", "WorldWall", "WorldGround", "WorldStatic"
        };

        private void RenderRuleInspector()
        {
            if (ruleInspectorRoot == null)
                return;

            ruleInspectorRoot.Clear();
            RenderPresentationInspector();
            RenderDiagnosticsInspector();
            if (workspace == null || CurrentIsGeneratedOnly || string.IsNullOrEmpty(session.SelectedStableId))
                return;

            if (CurrentIsRequest)
            {
                ProjectileRequestVariantListItem request = workspace.requests?.FirstOrDefault(value => value.stableId == session.SelectedStableId);
                if (request?.detail == null)
                {
                    ruleInspectorRoot.Add(new HelpBox("请求变体的投影详情不可用。请重新加载或查看问题列表。", HelpBoxMessageType.Warning));
                    return;
                }
                BuildRequestInspector(ruleInspectorRoot, request);
                return;
            }

            ProjectileDefinitionListItem definition = workspace.definitions?.FirstOrDefault(value => value.stableId == session.SelectedStableId);
            if (definition?.detail == null)
            {
                ruleInspectorRoot.Add(new HelpBox("投射物定义的投影详情不可用。请重新加载或查看问题列表。", HelpBoxMessageType.Warning));
                return;
            }
            BuildDefinitionInspector(ruleInspectorRoot, definition);
        }

        private void BuildDefinitionInspector(VisualElement root, ProjectileDefinitionListItem item)
        {
            ProjectileDefinitionDetailView detail = item.detail;
            Foldout identity = Section(root, "definition-identity-section", "标识", true);
            AddReadOnly(identity, "定义 ID（源权威）", detail.definitionId.ToString(CultureInfo.InvariantCulture), "definition-id-readonly");
            AddReadOnly(identity, "稳定 ID（请使用重命名）", detail.stableId, "definition-stable-id-readonly");
            AddToggle(identity, "物理事件", "physicalEventsEnabled", detail.physicalEventsEnabled, "definition-physical-events");

            ProjectileMotionView motion = detail.motion ?? new ProjectileMotionView();
            Foldout motionSection = Section(root, "definition-motion-section", "运动", true);
            AddPopup(motionSection, "运动类型", "motion.kind", motion.kind, MotionKinds, "motion-kind", value => BuildMotionKindEdits(motion, value));
            AddFloat(motionSection, "每帧速度", "motion.speedPerFrame", motion.speedPerFrame, "motion-speed");
            AddInt(motionSection, "生命周期（帧）", "motion.lifetimeFrames", motion.lifetimeFrames, "motion-lifetime");
            AddPopup(motionSection, "速度曲线", "motion.speedProfile.kind", motion.speedProfile?.kind, SpeedProfileKinds, "motion-speed-profile");
            if (motion.kind == "ArcToTarget")
                AddFloat(motionSection, "弧线高度", "motion.arcHeight", motion.arcHeight, "motion-arc-height");
            if (motion.kind == "BallisticArcToTarget")
            {
                AddFloat(motionSection, "每帧平方重力", "motion.gravityPerFrameSquared", motion.gravityPerFrameSquared, "motion-gravity");
                AddFloat(motionSection, "最大发射角", "motion.maxLaunchAngleDegrees", motion.maxLaunchAngleDegrees, "motion-max-launch-angle");
                AddReadOnly(motionSection, "派生正切值（编译器）", motion.maxLaunchAngleTangent.ToString("R", CultureInfo.InvariantCulture), "motion-derived-tangent");
            }
            if (motion.kind == "SwayForward")
            {
                AddFloat(motionSection, "横向振幅", "motion.lateralAmplitude", motion.lateralAmplitude, "motion-lateral-amplitude");
                AddInt(motionSection, "横向周期数", "motion.lateralCycles", motion.lateralCycles, "motion-lateral-cycles");
            }
            if (motion.kind == "BezierToTarget")
            {
                AddVector(motionSection, "控制点偏移 A", "motion.bezierControlOffsetA", motion.bezierControlOffsetA, "motion-bezier-a");
                AddVector(motionSection, "控制点偏移 B", "motion.bezierControlOffsetB", motion.bezierControlOffsetB, "motion-bezier-b");
            }
            if (motion.kind == "HomingToTarget")
                AddFloat(motionSection, "每帧转向率", "motion.homing.turnRatePerFrame", motion.homing?.turnRatePerFrame ?? 1f, "motion-homing-turn-rate");

            ProjectileCollisionView collision = detail.collision ?? new ProjectileCollisionView();
            Foldout collisionSection = Section(root, "definition-collision-section", "碰撞", true);
            AddFloat(collisionSection, "半径", "collision.radius", collision.radius, "collision-radius");
            AddReadOnly(collisionSection, "原始层遮罩（由目标类型派生）", collision.layerMask.ToString(CultureInfo.InvariantCulture), "collision-layer-mask-readonly");
            AddToggle(collisionSection, "首次有效命中后停止", "collision.stopOnFirstAcceptedImpact", collision.stopOnFirstAcceptedImpact, "collision-stop-first");
            AddToggle(collisionSection, "格挡命中后停止", "collision.stopOnBlockedImpact", collision.stopOnBlockedImpact, "collision-stop-blocked");
            AddInt(collisionSection, "最大有效命中数", "collision.maxAcceptedImpacts", collision.maxAcceptedImpacts, "collision-max-impacts");
            AddPopup(collisionSection, "同一目标", "collision.hitSameTargetPolicy", collision.hitSameTargetPolicy, new[] { "AllowRepeat", "OncePerProjectile" }, "collision-same-target");

            ProjectileImpactView impact = detail.impact ?? new ProjectileImpactView();
            Foldout impactSection = Section(root, "definition-impact-section", "命中", true);
            AddPopup(impactSection, "策略", "impact.policyKind", impact.policyKind, new[] { "DirectHit", "AreaPulseOnImpact" }, "impact-policy", value => BuildImpactPolicyEdits(impact, value));
            if (impact.policyKind == "AreaPulseOnImpact")
            {
                AddFloat(impactSection, "范围半径", "impact.areaRadius", impact.areaRadius, "impact-area-radius");
                AddToggle(impactSection, "过期时触发", "impact.triggerOnExpire", impact.triggerOnExpire, "impact-trigger-expire");
                AddReadOnly(impactSection, "原始范围层遮罩（由范围目标派生）", impact.areaLayerMask.ToString(CultureInfo.InvariantCulture), "impact-area-mask-readonly");
            }

            BuildHitEvent(root, "impact.hitEvent", "直接命中", impact.hitEvent, "direct-hit");
            if (impact.policyKind == "AreaPulseOnImpact")
                BuildHitEvent(root, "impact.areaHitEvent", "范围命中", impact.areaHitEvent, "area-hit");

            Foldout timeline = Section(root, "definition-effect-timeline-section", "效果时间轴引用", false);
            AddText(timeline, "时间轴 ID", "impact.effectTimelineId", impact.effectTimelineId, "impact-effect-timeline");
            timeline.Add(new HelpBox("这里只编辑稳定引用；时间轴内容仍由对应的时间轴创作工具负责。", HelpBoxMessageType.Info));

            Foldout compatibility = Section(root, "definition-compatibility-section", "兼容性", false);
            AddReadOnly(compatibility, "旧版 presentationProfileId", impact.presentationProfileId, "impact-presentation-profile-readonly");
            compatibility.Add(new HelpBox("旧版 gameplay presentationProfileId 以及编译器派生的遮罩和正切值均为只读。", HelpBoxMessageType.Info));

            if (item.shared)
            {
                root.Insert(0, new HelpBox(
                    "共享定义：编辑会影响 " + (item.requestIds?.Length ?? 0) + " 个请求。首次提交字段组时需要确认准确的引用影响。",
                    HelpBoxMessageType.Warning));
            }
        }

        private void BuildHitEvent(VisualElement root, string path, string title, CombatHitEventView hit, string namePrefix)
        {
            hit ??= new CombatHitEventView();
            Foldout section = Section(root, namePrefix + "-section", title, title == "直接命中");
            Foldout targeting = Section(section, namePrefix + "-targeting-section", "目标", true);
            AddTargetTypes(targeting, path + ".targetTypes", hit.targetTypes, namePrefix + "-targets");
            AddPopup(targeting, "环境命中策略", path + ".environmentHitPolicy", hit.environmentHitPolicy, new[] { "None", "BlockedPresentation" }, namePrefix + "-environment");
            AddText(targeting, "稳定事件 ID", path + ".stableEventId", hit.stableEventId, namePrefix + "-stable-event");
            AddInt(targeting, "最大有效命中数", path + ".maxAcceptedHits", hit.maxAcceptedHits, namePrefix + "-max-hits");
            AddPopup(targeting, "同一目标", path + ".hitSameTargetPolicy", hit.hitSameTargetPolicy, new[] { "AllowRepeat", "OncePerEvent" }, namePrefix + "-same-target");

            CombatHitDamageView damage = hit.damage ?? new CombatHitDamageView();
            Foldout damageSection = Section(section, namePrefix + "-damage-section", "伤害", true);
            AddText(damageSection, "伤害类型", path + ".damage.damageTypeId", damage.damageTypeId, namePrefix + "-damage-type");
            AddInt(damageSection, "基础伤害", path + ".damage.baseDamage", damage.baseDamage, namePrefix + "-base-damage");
            AddInt(damageSection, "冲击力", path + ".damage.impactPower", damage.impactPower, namePrefix + "-impact-power");
            AddInt(damageSection, "击飞力", path + ".damage.launchPower", damage.launchPower, namePrefix + "-launch-power");
            AddText(damageSection, "分析分类", path + ".damage.analysisCategory", damage.analysisCategory, namePrefix + "-analysis-category");
            AddStringList(damageSection, "伤害标签", path + ".damage.tags", damage.tags, namePrefix + "-damage-tags");
            Foldout buffs = Section(section, namePrefix + "-buffs-section", "增益效果", false);
            AddIntList(buffs, "命中时施加的增益", path + ".onHitBuffs", (hit.onHitBuffs ?? Array.Empty<CombatHitBuffView>()).Select(value => value.buffId).ToArray(), namePrefix + "-buffs");
        }

        private void BuildRequestInspector(VisualElement root, ProjectileRequestVariantListItem item)
        {
            ProjectileRequestDetailView detail = item.detail;
            Foldout definition = Section(root, "request-definition-section", "定义", true);
            string[] definitionIds = (workspace.definitions ?? Array.Empty<ProjectileDefinitionListItem>()).Select(value => value.stableId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            AddPopup(definition, "定义引用", "projectileStableId", detail.projectileStableId, definitionIds, "request-definition-reference");

            ProjectileSpawnOriginView origin = detail.origin ?? new ProjectileSpawnOriginView();
            Foldout originSection = Section(root, "request-origin-section", "出生点", true);
            AddPopup(originSection, "出生点类型", "origin.kind", string.IsNullOrEmpty(origin.kind) ? "Socket" : origin.kind, OriginKinds, "request-origin-kind");
            BuildSocketEditor(originSection, origin.socketId);
            AddVector(originSection, "局部偏移", "origin.localOffset", origin.localOffset, "request-local-offset");
            AddVector(originSection, "备用根节点偏移", "origin.fallbackRootOffset", origin.fallbackRootOffset, "request-fallback-offset");

            ProjectileAimView aim = detail.aim ?? new ProjectileAimView();
            Foldout aimSection = Section(root, "request-aim-section", "瞄准", true);
            AddPopup(aimSection, "瞄准类型", "aim.kind", aim.kind, AimKinds, "request-aim-kind");
            AddFloat(aimSection, "最大距离", "aim.maxRange", aim.maxRange, "request-aim-max-range");
            if (aim.kind != "CasterForwardRange")
            {
                AddFloat(aimSection, "目标高度偏移", "aim.targetHeightOffset", aim.targetHeightOffset, "request-target-height");
            }
            AddPopup(aimSection, "备用瞄准类型", "aim.fallbackKind", string.IsNullOrEmpty(aim.fallbackKind) ? "CasterForwardRange" : aim.fallbackKind, AimKinds, "request-aim-fallback");
        }

        private void BuildSocketEditor(VisualElement parent, string socketId)
        {
            string[] known = (workspace.knownSocketIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (known.Length > 0)
            {
                string controlled = known.Contains(socketId) ? socketId : known[0];
                AddPopup(parent, "已知挂点", "origin.socketId", controlled, known, "request-known-socket");
            }
            else
            {
                parent.Add(new HelpBox("没有可用的 KnownSocketIds 权威数据，无法选择受控挂点候选项。", HelpBoxMessageType.Warning));
            }

            AddText(parent, "高级 / 旧版挂点文本", "origin.socketId", socketId, "request-socket-advanced");
            if (!string.IsNullOrWhiteSpace(socketId) && !known.Contains(socketId))
                parent.Add(new HelpBox("当前挂点不在 KnownSocketIds 中。该值会作为兼容文本保留，并继续显示诊断。", HelpBoxMessageType.Warning));
        }

        private Foldout Section(VisualElement parent, string name, string text, bool expanded)
        {
            var foldout = new Foldout { name = name, text = text, value = expanded };
            foldout.AddToClassList("pa-inspector-section");
            parent.Add(foldout);
            return foldout;
        }

        private void AddReadOnly(VisualElement parent, string label, string value, string name)
        {
            var field = new TextField(label) { name = name, value = value ?? string.Empty };
            field.SetEnabled(false);
            parent.Add(field);
        }

        private void AddText(VisualElement parent, string label, string path, string value, string name)
        {
            var field = new TextField(label) { name = name };
            field.SetValueWithoutNotify(value ?? string.Empty);
            string committed = value ?? string.Empty;
            Action commit = () =>
            {
                string next = field.value ?? string.Empty;
                if (string.Equals(next, committed, StringComparison.Ordinal)) return;
                committed = next;
                CommitFieldEdit(StringEdit(path, next));
            };
            field.RegisterCallback<FocusOutEvent>(_ => commit());
            field.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter) commit(); });
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void AddFloat(VisualElement parent, string label, string path, float value, string name)
        {
            var field = new FloatField(label) { name = name };
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => CommitFieldEdit(new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Float", floatValue = evt.newValue }));
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void AddInt(VisualElement parent, string label, string path, int value, string name)
        {
            var field = new IntegerField(label) { name = name };
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => CommitFieldEdit(new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Int", intValue = evt.newValue }));
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void AddToggle(VisualElement parent, string label, string path, bool value, string name)
        {
            var field = new Toggle(label) { name = name };
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => CommitFieldEdit(new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Bool", boolValue = evt.newValue }));
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void AddPopup(VisualElement parent, string label, string path, string value, IEnumerable<string> choices, string name, Func<string, ProjectileAuthoringFieldEditView[]> buildEdits = null)
        {
            var values = (choices ?? Array.Empty<string>()).Where(item => !string.IsNullOrEmpty(item)).Distinct().ToList();
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Insert(0, value);
            if (values.Count == 0) values.Add(string.Empty);
            var field = new PopupField<string>(label, values, Math.Max(0, values.IndexOf(value ?? string.Empty))) { name = name };
            field.formatListItemCallback = LocalizeUiValue;
            field.formatSelectedValueCallback = LocalizeUiValue;
            field.userData = new PopupCommitBinding(path, buildEdits);
            field.RegisterValueChangedCallback(evt => CommitPopupValue(field, evt.newValue));
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void CommitPopupValue(PopupField<string> field, string value)
        {
            PopupCommitBinding binding = field?.userData as PopupCommitBinding;
            ProjectileAuthoringFieldEditView[] edits = binding?.BuildEdits?.Invoke(value)
                ?? new[] { EnumEdit(binding?.FieldPath ?? string.Empty, value) };
            CommitFieldEdit(edits);
        }

        private static ProjectileAuthoringFieldEditView[] BuildMotionKindEdits(ProjectileMotionView motion, string targetKind)
        {
            motion ??= new ProjectileMotionView();
            var edits = new List<ProjectileAuthoringFieldEditView> { EnumEdit("motion.kind", targetKind) };
            switch (targetKind)
            {
                case "ArcToTarget":
                    edits.Add(FloatEdit("motion.arcHeight", motion.arcHeight > 0f ? motion.arcHeight : DefaultArcHeight));
                    break;
                case "SwayForward":
                    edits.Add(FloatEdit("motion.lateralAmplitude", motion.lateralAmplitude > 0f ? motion.lateralAmplitude : DefaultSwayAmplitude));
                    edits.Add(IntEdit("motion.lateralCycles", motion.lateralCycles > 0 && motion.lateralCycles <= 1000 ? motion.lateralCycles : DefaultSwayCycles));
                    break;
                case "BezierToTarget":
                    ProjectileVector3View a = motion.bezierControlOffsetA ?? new ProjectileVector3View();
                    ProjectileVector3View b = motion.bezierControlOffsetB ?? new ProjectileVector3View();
                    if (IsZero(a) && IsZero(b))
                    {
                        a = new ProjectileVector3View { y = 1f };
                        b = new ProjectileVector3View { y = 1f, z = 1f };
                    }
                    edits.Add(VectorEdit("motion.bezierControlOffsetA", a));
                    edits.Add(VectorEdit("motion.bezierControlOffsetB", b));
                    break;
                case "BallisticArcToTarget":
                    edits.Add(FloatEdit("motion.gravityPerFrameSquared", motion.gravityPerFrameSquared > 0f ? motion.gravityPerFrameSquared : DefaultBallisticGravity));
                    edits.Add(FloatEdit("motion.maxLaunchAngleDegrees", motion.maxLaunchAngleDegrees > 0f && motion.maxLaunchAngleDegrees < 90f ? motion.maxLaunchAngleDegrees : DefaultBallisticAngle));
                    break;
                case "HomingToTarget":
                    float turnRate = motion.homing?.turnRatePerFrame ?? 0f;
                    edits.Add(FloatEdit("motion.homing.turnRatePerFrame", turnRate > 0f && turnRate <= 1f ? turnRate : DefaultHomingTurnRate));
                    break;
            }
            return edits.ToArray();
        }

        private static ProjectileAuthoringFieldEditView[] BuildImpactPolicyEdits(ProjectileImpactView impact, string targetPolicy)
        {
            impact ??= new ProjectileImpactView();
            var edits = new List<ProjectileAuthoringFieldEditView> { EnumEdit("impact.policyKind", targetPolicy) };
            if (!string.Equals(targetPolicy, "AreaPulseOnImpact", StringComparison.Ordinal)) return edits.ToArray();

            CombatHitEventView direct = impact.hitEvent ?? new CombatHitEventView();
            CombatHitEventView area = impact.areaHitEvent;
            CombatHitEventView inherited = area ?? direct;
            string[] targets = NonEmpty(area?.targetTypes) ? area.targetTypes : NonEmpty(direct.targetTypes) ? direct.targetTypes : new[] { "HostileCharacter" };
            string environment = ValidEnvironment(inherited?.environmentHitPolicy) ? inherited.environmentHitPolicy : ValidEnvironment(direct.environmentHitPolicy) ? direct.environmentHitPolicy : "None";
            string sameTarget = ValidHitRepeat(inherited?.hitSameTargetPolicy) ? inherited.hitSameTargetPolicy : ValidHitRepeat(direct.hitSameTargetPolicy) ? direct.hitSameTargetPolicy : "AllowRepeat";
            string stableEventId = !string.IsNullOrWhiteSpace(inherited?.stableEventId) ? inherited.stableEventId : !string.IsNullOrWhiteSpace(direct.stableEventId) ? direct.stableEventId + ".area" : "projectile.area";
            int maxHits = inherited?.maxAcceptedHits > 0 ? inherited.maxAcceptedHits : direct.maxAcceptedHits > 0 ? direct.maxAcceptedHits : 1;
            CombatHitDamageView damage = ValidDamage(area?.damage) ? area.damage : ValidDamage(direct.damage) ? direct.damage : new CombatHitDamageView { damageTypeId = "Default" };
            int[] buffs = ((area?.onHitBuffs?.Length ?? 0) > 0 ? area.onHitBuffs : direct.onHitBuffs ?? Array.Empty<CombatHitBuffView>())
                .Where(value => value != null && value.buffId > 0).Select(value => value.buffId).Distinct().ToArray();

            edits.Add(FloatEdit("impact.areaRadius", impact.areaRadius > 0f ? impact.areaRadius : DefaultAreaRadius));
            edits.Add(BoolEdit("impact.triggerOnExpire", impact.triggerOnExpire));
            edits.Add(StringListEdit("impact.areaHitEvent.targetTypes", targets));
            edits.Add(EnumEdit("impact.areaHitEvent.environmentHitPolicy", environment));
            edits.Add(StringEdit("impact.areaHitEvent.stableEventId", stableEventId));
            edits.Add(IntEdit("impact.areaHitEvent.maxAcceptedHits", maxHits));
            edits.Add(EnumEdit("impact.areaHitEvent.hitSameTargetPolicy", sameTarget));
            edits.Add(StringEdit("impact.areaHitEvent.damage.damageTypeId", string.IsNullOrWhiteSpace(damage.damageTypeId) ? "Default" : damage.damageTypeId));
            edits.Add(IntEdit("impact.areaHitEvent.damage.baseDamage", Math.Max(0, damage.baseDamage)));
            edits.Add(IntEdit("impact.areaHitEvent.damage.impactPower", Math.Max(0, damage.impactPower)));
            edits.Add(IntEdit("impact.areaHitEvent.damage.launchPower", Math.Max(0, damage.launchPower)));
            edits.Add(StringListEdit("impact.areaHitEvent.damage.tags", (damage.tags ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))));
            edits.Add(StringEdit("impact.areaHitEvent.damage.analysisCategory", damage.analysisCategory ?? string.Empty));
            edits.Add(IntListEdit("impact.areaHitEvent.onHitBuffs", buffs));
            return edits.ToArray();
        }

        private static bool NonEmpty(string[] values) => values != null && values.Length > 0;
        private static bool ValidEnvironment(string value) => value == "None" || value == "BlockedPresentation";
        private static bool ValidHitRepeat(string value) => value == "AllowRepeat" || value == "OncePerEvent";
        private static bool ValidDamage(CombatHitDamageView value) => value != null && !string.IsNullOrWhiteSpace(value.damageTypeId) && value.baseDamage >= 0 && value.impactPower >= 0 && value.launchPower >= 0;
        private static bool IsZero(ProjectileVector3View value) => value == null || (Math.Abs(value.x) < .0001f && Math.Abs(value.y) < .0001f && Math.Abs(value.z) < .0001f);

        private void AddVector(VisualElement parent, string label, string path, ProjectileVector3View value, string name)
        {
            value ??= new ProjectileVector3View();
            var field = new Vector3Field(label) { name = name };
            field.SetValueWithoutNotify(new UnityEngine.Vector3(value.x, value.y, value.z));
            field.RegisterValueChangedCallback(evt => CommitFieldEdit(new ProjectileAuthoringFieldEditView
            {
                fieldPath = path, valueKind = "Vector", vectorValue = new ProjectileVector3View { x = evt.newValue.x, y = evt.newValue.y, z = evt.newValue.z }
            }));
            parent.Add(field);
            AddFieldProblems(parent, path);
        }

        private void AddTargetTypes(VisualElement parent, string path, string[] current, string name)
        {
            var box = new VisualElement { name = name };
            box.Add(new Label("目标类型"));
            var values = new List<string>(current ?? Array.Empty<string>());
            bool hadLegacyNeutral = values.Remove("NeutralCharacter");
            if (hadLegacyNeutral && !values.Contains("ThirdPartyCharacter")) values.Add("ThirdPartyCharacter");
            if (hadLegacyNeutral) box.Add(new HelpBox("旧版 NeutralCharacter 会按 ThirdPartyCharacter 显示和保存。", HelpBoxMessageType.Info));
            foreach (string target in TargetTypes)
            {
                var toggle = new Toggle(LocalizeUiValue(target)); toggle.SetValueWithoutNotify(values.Contains(target));
                toggle.RegisterValueChangedCallback(evt =>
                {
                    var next = new List<string>(values);
                    if (evt.newValue && !next.Contains(target)) next.Add(target);
                    if (!evt.newValue) next.Remove(target);
                    CommitFieldEdit(StringListEdit(path, next));
                });
                box.Add(toggle);
            }
            string[] unknown = values.Where(value => !TargetTypes.Contains(value)).ToArray();
            if (unknown.Length > 0) box.Add(new HelpBox("以下未知目标值会保留，直到明确修改此列表：" + string.Join(", ", unknown), HelpBoxMessageType.Warning));
            parent.Add(box);
            AddFieldProblems(parent, path);
        }

        private void AddStringList(VisualElement parent, string label, string path, string[] current, string name)
        {
            string[] values = current ?? Array.Empty<string>();
            var box = new VisualElement { name = name }; box.Add(new Label(label));
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                var row = new VisualElement(); row.AddToClassList("pa-row"); row.Add(new Label(values[i]));
                row.Add(new Button(() => MoveStringList(path, values, index, -1)) { text = "↑" });
                row.Add(new Button(() => MoveStringList(path, values, index, 1)) { text = "↓" });
                row.Add(new Button(() => CommitFieldEdit(StringListEdit(path, values.Where((_, valueIndex) => valueIndex != index)))) { text = "移除" });
                box.Add(row);
            }
            var input = new TextField { name = name + "-input" };
            box.Add(input); box.Add(new Button(() => { string text = input.value?.Trim(); if (!string.IsNullOrEmpty(text)) CommitFieldEdit(StringListEdit(path, values.Concat(new[] { text }))); }) { text = "添加", name = name + "-add" });
            parent.Add(box);
            AddFieldProblems(parent, path);
        }

        private void AddIntList(VisualElement parent, string label, string path, int[] current, string name)
        {
            int[] values = current ?? Array.Empty<int>();
            var box = new VisualElement { name = name }; box.Add(new Label(label));
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                var row = new VisualElement(); row.AddToClassList("pa-row"); row.Add(new Label(values[i].ToString(CultureInfo.InvariantCulture)));
                row.Add(new Button(() => MoveIntList(path, values, index, -1)) { text = "↑" });
                row.Add(new Button(() => MoveIntList(path, values, index, 1)) { text = "↓" });
                row.Add(new Button(() => CommitFieldEdit(IntListEdit(path, values.Where((_, valueIndex) => valueIndex != index)))) { text = "移除" });
                box.Add(row);
            }
            var input = new IntegerField { name = name + "-input" };
            box.Add(input); box.Add(new Button(() => CommitFieldEdit(IntListEdit(path, values.Concat(new[] { input.value })))) { text = "添加", name = name + "-add" });
            parent.Add(box);
            AddFieldProblems(parent, path);
        }

        private void MoveStringList(string path, string[] values, int index, int direction)
        {
            int target = index + direction; if (target < 0 || target >= values.Length) return;
            string[] next = values.ToArray(); (next[index], next[target]) = (next[target], next[index]); CommitFieldEdit(StringListEdit(path, next));
        }

        private void MoveIntList(string path, int[] values, int index, int direction)
        {
            int target = index + direction; if (target < 0 || target >= values.Length) return;
            int[] next = values.ToArray(); (next[index], next[target]) = (next[target], next[index]); CommitFieldEdit(IntListEdit(path, next));
        }

        private void AddFieldProblems(VisualElement parent, string path)
        {
            foreach (ProjectileAuthoringProblemView problem in AllProblems().Where(value => string.Equals(value.fieldPath, path, StringComparison.Ordinal) || (value.fieldPath?.EndsWith("." + path, StringComparison.Ordinal) ?? false)))
                parent.Add(new HelpBox(problem.code + ": " + problem.message, string.Equals(problem.severity, "Error", StringComparison.OrdinalIgnoreCase) ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
        }

        private void CommitFieldEdit(params ProjectileAuthoringFieldEditView[] edits)
        {
            if (workspace == null || service == null || service.IsBusy || CurrentIsGeneratedOnly || edits == null || edits.Length == 0)
                return;
            var request = new ProjectileAuthoringMutationRequestView
            {
                kind = CurrentIsRequest ? "EditRequestFields" : "EditDefinitionFields",
                sourceStableId = session.SelectedStableId,
                stableId = session.SelectedStableId,
                definitionStableId = CurrentIsRequest ? workspace.requests?.FirstOrDefault(value => value.stableId == session.SelectedStableId)?.definitionStableId : session.SelectedStableId,
                workspaceToken = workspace.workspaceHash,
                planOnly = true,
                fieldEdits = edits
            };
            PreviewMutation(request);
        }

        public void CommitFieldForTests(string path, float value)
            => CommitFieldEdit(new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Float", floatValue = value });

        public void CommitFieldForTests(ProjectileAuthoringFieldEditView edit)
            => CommitFieldEdit(edit);

        public void CommitPopupForTests(string controlName, string value)
        {
            PopupField<string> field = ruleInspectorRoot?.Q<PopupField<string>>(controlName);
            if (field == null || !field.choices.Contains(value)) throw new InvalidOperationException("The requested popup control or value is unavailable: " + controlName + " = " + value);
            field.SetValueWithoutNotify(value);
            CommitPopupValue(field, value);
        }

        private static ProjectileAuthoringFieldEditView StringEdit(string path, string value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "String", stringValue = value ?? string.Empty };
        private static ProjectileAuthoringFieldEditView EnumEdit(string path, string value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Enum", stringValue = value ?? string.Empty };
        private static ProjectileAuthoringFieldEditView FloatEdit(string path, float value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Float", floatValue = value };
        private static ProjectileAuthoringFieldEditView IntEdit(string path, int value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Int", intValue = value };
        private static ProjectileAuthoringFieldEditView BoolEdit(string path, bool value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Bool", boolValue = value };
        private static ProjectileAuthoringFieldEditView VectorEdit(string path, ProjectileVector3View value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Vector", vectorValue = new ProjectileVector3View { x = value?.x ?? 0f, y = value?.y ?? 0f, z = value?.z ?? 0f } };
        private static ProjectileAuthoringFieldEditView StringListEdit(string path, IEnumerable<string> values) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "StringList", stringValues = (values ?? Array.Empty<string>()).ToArray() };
        private static ProjectileAuthoringFieldEditView IntListEdit(string path, IEnumerable<int> values) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "IntList", intValues = (values ?? Array.Empty<int>()).ToArray() };

        private sealed class PopupCommitBinding
        {
            public PopupCommitBinding(string fieldPath, Func<string, ProjectileAuthoringFieldEditView[]> buildEdits) { FieldPath = fieldPath ?? string.Empty; BuildEdits = buildEdits; }
            public string FieldPath { get; }
            public Func<string, ProjectileAuthoringFieldEditView[]> BuildEdits { get; }
        }
    }
}
