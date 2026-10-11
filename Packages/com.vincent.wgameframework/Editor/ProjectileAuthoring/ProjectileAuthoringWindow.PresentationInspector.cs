using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.ProjectileAuthoring
{
    public sealed partial class ProjectileAuthoringWindow
    {
        private void RenderPresentationInspector()
        {
            if (presentationInspectorRoot == null) return;
            presentationInspectorRoot.Clear();
            ProjectileDefinitionListItem definition = SelectedDefinition();
            if (definition == null)
            {
                presentationInspectorRoot.Add(new HelpBox("当前条目没有可编辑的投射物定义表现。", HelpBoxMessageType.Info));
                return;
            }
            BuildPresentationInspector(presentationInspectorRoot, definition);
        }

        private void BuildPresentationInspector(VisualElement root, ProjectileDefinitionListItem item)
        {
            ProjectilePresentationBindingListItem binding = workspace.bindings?.FirstOrDefault(value => value.projectileDefinitionId == item.definitionId);
            Foldout section = Section(root, "definition-presentation-section", "生命周期 VFX", true);
            if (binding == null)
                section.Add(new HelpBox("尚未配置表现绑定。游戏逻辑仍然有效；设置任意槽位时会创建 v2 绑定。", HelpBoxMessageType.Info));
            if (binding?.legacyV1 == true)
            {
                section.Add(new HelpBox("旧版 V1 prefabResource 只读映射到飞行主体。编辑全部七个槽位前需要显式迁移。", HelpBoxMessageType.Warning));
                section.Add(new Button(() => CommitPresentationCommand("MigratePresentationBindingV1ToV2")) { name = "presentation-migrate-v1", text = "将表现绑定从 v1 迁移到 v2" });
            }
            bool editable = binding?.legacyV1 != true;

            var bindingHeader = new VisualElement { name = "presentation-binding-summary" };
            bindingHeader.AddToClassList("pa-vfx-binding-summary");
            bindingHeader.Add(new Label("表现绑定") { name = "presentation-binding-summary-title" });
            var validate = new Button(() => ValidatePresentationProductionGate(binding))
            {
                name = "presentation-production-gate",
                text = "校验"
            };
            validate.AddToClassList("pa-vfx-quiet-button");
            bindingHeader.Add(validate);
            section.Add(bindingHeader);
            ProjectilePresentationSlotsView slots = binding?.detail?.slots;
            switch (selectedPresentationSlotKind)
            {
                case "spawn": AddPresentationSlot(section, "spawn", "出生", "投射物原点", slots?.spawn, editable); break;
                case "trail": AddPresentationSlot(section, "trail", "拖尾", "投射物变换", slots?.trail, editable); break;
                case "directImpact": AddPresentationSlot(section, "directImpact", "直接命中", "命中点", slots?.directImpact, editable); break;
                case "areaImpact": AddPresentationSlot(section, "areaImpact", "范围命中", "范围脉冲中心", slots?.areaImpact, editable); break;
                case "blockedImpact": AddPresentationSlot(section, "blockedImpact", "格挡命中", "命中点", slots?.blockedImpact, editable); break;
                case "expire": AddPresentationSlot(section, "expire", "过期", "最终位置", slots?.expire, editable); break;
                case "exitPolicy":
                    ProjectilePresentationExitPolicyView exit = binding?.detail?.exitPolicy;
                    Foldout exitSection = Section(section, "presentation-exit-policy", "退出策略", true);
                    AddPresentationExit(exitSection, "有效命中", "acceptedImpact", exit?.acceptedImpact ?? "Complete", "presentation-exit-accepted", editable);
                    AddPresentationExit(exitSection, "格挡命中", "blockedImpact", exit?.blockedImpact ?? "Dissipate", "presentation-exit-blocked", editable);
                    AddPresentationExit(exitSection, "自然过期", "expiration", exit?.expiration ?? "Complete", "presentation-exit-expiration", editable);
                    AddPresentationExit(exitSection, "外部移除", "externalRemoval", exit?.externalRemoval ?? "Dissipate", "presentation-exit-external", editable);
                    AddReadOnly(exitSection, "运行时重置", LocalizeUiValue(exit?.runtimeReset ?? "Immediate"), "presentation-exit-reset");
                    break;
                default: AddPresentationSlot(section, "flightBody", "飞行主体", "投射物变换", slots?.flightBody, editable); break;
            }
        }

        private void AddPresentationSlot(VisualElement root, string kind, string title, string anchor, ProjectilePresentationSlotView slot, bool editable)
        {
            Foldout section = Section(root, "presentation-slot-" + kind, title, true);
            section.AddToClassList("pa-vfx-slot-card");
            string serializedKey = SerializeResourceKey(slot?.resource);
            ProjectileAuthoringVfxTransientView snapshot = PresentationVfxBridge.Resolve(serializedKey);
            snapshot.ContractDiagnostic = ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(kind, snapshot.Role, snapshot.Anchor, snapshot.Space, snapshot.Lifecycle);
            snapshot.HasSemanticSuggestion = !string.IsNullOrWhiteSpace(serializedKey)
                && !string.IsNullOrWhiteSpace(snapshot.ContractDiagnostic);

            bool persistent = kind == "flightBody" || kind == "trail";
            bool ready = !string.IsNullOrWhiteSpace(serializedKey)
                && !string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath)
                && !snapshot.IsCandidate
                && !snapshot.IsStale;
            var slotHeader = new VisualElement { name = "presentation-slot-" + kind + "-summary" };
            slotHeader.AddToClassList("pa-vfx-slot-summary");
            var lifecycleBadge = new Label(persistent ? "持续" : "单次");
            lifecycleBadge.AddToClassList("pa-vfx-badge");
            slotHeader.Add(lifecycleBadge);
            var anchorLabel = new Label(anchor);
            anchorLabel.AddToClassList("pa-vfx-anchor");
            slotHeader.Add(anchorLabel);
            var readiness = new Label(string.IsNullOrWhiteSpace(serializedKey)
                ? "未绑定"
                : ready ? "● 生产可用" : "● 需要处理")
            {
                name = "presentation-slot-" + kind + "-status"
            };
            readiness.AddToClassList(ready ? "pa-vfx-ready" : "pa-vfx-warning");
            slotHeader.Add(readiness);
            section.Add(slotHeader);

            var resourceCard = new VisualElement { name = "presentation-slot-" + kind + "-resource-card" };
            resourceCard.AddToClassList("pa-vfx-resource-card");
            string displayName = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? "未选择 VFX" : snapshot.DisplayName;
            var display = new Label(displayName) { name = "presentation-slot-" + kind + "-display-name" };
            display.AddToClassList("pa-vfx-resource-title");
            resourceCard.Add(display);

            var resourceKey = new Label(string.IsNullOrWhiteSpace(serializedKey) ? "尚未绑定 ResourceKey" : serializedKey)
            {
                name = "presentation-slot-" + kind + "-resource"
            };
            resourceKey.tooltip = serializedKey;
            resourceKey.AddToClassList("pa-vfx-resource-key");
            resourceCard.Add(resourceKey);

            if (snapshot.Thumbnail != null)
            {
                var thumbnail = new Image { name = "presentation-slot-" + kind + "-thumbnail", image = snapshot.Thumbnail, scaleMode = ScaleMode.ScaleAndCrop };
                thumbnail.AddToClassList("pa-vfx-thumbnail");
                resourceCard.Add(thumbnail);
            }

            var prefabRow = new VisualElement { name = "presentation-slot-" + kind + "-prefab-row" };
            prefabRow.AddToClassList("pa-vfx-prefab-row");
            var prefabPath = new Label(string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath)
                ? "未解析到 Unity Prefab"
                : snapshot.ActualPrefabPath)
            {
                name = "presentation-slot-" + kind + "-resolved-prefab"
            };
            prefabPath.tooltip = snapshot.ActualPrefabPath;
            prefabPath.AddToClassList("pa-vfx-prefab-path");
            prefabRow.Add(prefabPath);
            var locate = new Button(() => PingPresentationPrefab(snapshot.ActualPrefabPath))
            {
                name = "presentation-slot-" + kind + "-locate",
                text = "定位"
            };
            locate.SetEnabled(!string.IsNullOrWhiteSpace(snapshot.ActualPrefabPath));
            locate.AddToClassList("pa-vfx-quiet-button");
            prefabRow.Add(locate);
            resourceCard.Add(prefabRow);
            section.Add(resourceCard);

            if (!string.IsNullOrWhiteSpace(snapshot.Diagnostic))
                section.Add(new HelpBox(snapshot.Diagnostic, HelpBoxMessageType.Warning));
            if (snapshot.HasSemanticSuggestion)
                section.Add(new HelpBox(
                    "语义建议：" + snapshot.ContractDiagnostic + " 仅供创作与 Agent 判断，不影响选择或生产可用性。",
                    HelpBoxMessageType.Info));
            if (snapshot.IsCandidate)
                section.Add(new HelpBox("PROJAUTH016：当前只有本地候选资源覆盖，该覆盖不会持久化。", HelpBoxMessageType.Warning));
            if (snapshot.IsStale)
                section.Add(new HelpBox("PROJAUTH017：已发布绑定或候选资源回执已过期。", HelpBoxMessageType.Warning));

            string[] orientations = PresentationOrientations(kind);
            int orientationIndex = Math.Max(0, Array.IndexOf(orientations, slot?.orientation ?? orientations[0]));
            var orientation = new PopupField<string>("朝向", orientations.ToList(), orientationIndex) { name = "presentation-slot-" + kind + "-orientation" };
            orientation.formatListItemCallback = LocalizeUiValue;
            orientation.formatSelectedValueCallback = LocalizeUiValue;
            orientation.SetEnabled(editable && orientations.Length > 1);
            orientation.AddToClassList("pa-vfx-parameter");
            section.Add(orientation);
            ProjectileVector3View offset = slot?.localOffset ?? new ProjectileVector3View();
            var localOffset = new Vector3Field("局部偏移") { name = "presentation-slot-" + kind + "-offset" };
            localOffset.SetValueWithoutNotify(new Vector3(offset.x, offset.y, offset.z));
            localOffset.SetEnabled(editable);
            localOffset.AddToClassList("pa-vfx-parameter");
            section.Add(localOffset);

            var lifetime = new SliderInt("技能合同长度（帧）", 1, 600)
            {
                name = "presentation-slot-" + kind + "-lifetime",
                showInputField = true
            };
            lifetime.SetValueWithoutNotify(ResolveVfxContractFrames(slot));
            lifetime.SetEnabled(editable && !persistent);

            Foldout advanced = null;
            if (!persistent)
            {
                advanced = new Foldout
                {
                    name = "presentation-slot-" + kind + "-advanced",
                    text = "高级参数",
                    value = false
                };
                advanced.AddToClassList("pa-vfx-advanced");
                advanced.Add(lifetime);
                advanced.Add(new HelpBox(
                    "应用后会同步 VFX 定义默认长度、命中表现合同和当前投射物槽位；草稿 VFX 的原始播放速度不会改变。",
                    HelpBoxMessageType.Info));
                section.Add(advanced);
            }

            var actions = new VisualElement { name = "presentation-slot-" + kind + "-actions" };
            actions.AddToClassList("pa-vfx-actions");
            Button choose = null;
            choose = new Button(() =>
            {
                Rect world = choose.worldBound;
                Rect screen = new Rect(position.x + world.x, position.y + world.y, world.width, world.height);
                CharacterActionVfxPickerWindow.ShowPicker(screen, serializedKey, ExpectedPresentationRole(kind), option =>
                    SelectPresentationResource(kind, new ProjectileResourceKeyView { id = option.ResourceKey.Id, typeId = option.ResourceKey.TypeId, variant = option.ResourceKey.Variant, packageId = option.ResourceKey.PackageId }, option.IsCandidate, orientation.value, localOffset.value, persistent ? 0 : lifetime.value),
                    option => ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(kind, option),
                    option => ProjectileAuthoringVfxSelectionBridge.PickerDiagnostic(kind, serializedKey, option));
            }) { name = "presentation-slot-" + kind + "-choose", text = "选择 VFX" };
            choose.SetEnabled(editable);
            choose.AddToClassList("pa-vfx-primary-button");
            actions.Add(choose);
            var apply = new Button(() =>
            {
                ProjectileResourceKeyView resource = slot?.resource;
                if (resource == null || string.IsNullOrWhiteSpace(resource.id)) return;
                CommitPresentationSlot(kind, resource.id, resource.typeId, resource.variant, resource.packageId, orientation.value, localOffset.value, persistent ? 0 : lifetime.value, false);
            }) { name = "presentation-slot-" + kind + "-apply", text = "应用参数" };
            apply.SetEnabled(editable && slot?.resource != null && !string.IsNullOrWhiteSpace(slot.resource.id));
            actions.Add(apply);
            var clear = new Button(() => CommitPresentationCommand("ClearPresentationSlot", kind)) { name = "presentation-slot-" + kind + "-clear", text = "清空" };
            clear.SetEnabled(editable && slot?.resource != null);
            actions.Add(clear);
            actions.Add(new Button(VfxAuthoringResourceLibraryWindow.Open) { name = "presentation-slot-" + kind + "-workbench", text = "打开工作台" });
            section.Add(actions);
            AddFieldProblems(section, "presentationBinding.slots." + kind);
        }

        private void AddPresentationSlot(VisualElement root, string kind, string title, string anchor, ProjectilePresentationSlotView slot)
            => AddPresentationSlot(root, kind, title, anchor, slot, false);

        private void AddPresentationExit(VisualElement parent, string label, string kind, string value, string name, bool editable)
        {
            var field = new PopupField<string>(label, new List<string> { "Complete", "Dissipate" }, value == "Dissipate" ? 1 : 0) { name = name };
            field.formatListItemCallback = LocalizeUiValue;
            field.formatSelectedValueCallback = LocalizeUiValue;
            field.RegisterValueChangedCallback(evt => CommitPresentationCommand("EditPresentationExitPolicy", null, kind, evt.newValue));
            field.SetEnabled(editable);
            parent.Add(field);
            AddFieldProblems(parent, "presentationBinding.exitPolicy." + kind);
        }

        private void CommitPresentationSlot(string kind, string id, string typeId, string variant, string packageId, string orientation, Vector3 offset, int lifetime, bool synchronizeVfxContractLifetime = false)
        {
            CommitPresentationCommand("SetPresentationSlot", kind, null, null,
                new ProjectileResourceKeyView { id = id ?? string.Empty, typeId = string.IsNullOrWhiteSpace(typeId) ? "GameObject" : typeId, variant = variant ?? string.Empty, packageId = packageId ?? string.Empty },
                orientation, new ProjectileVector3View { x = offset.x, y = offset.y, z = offset.z }, lifetime,
                synchronizeVfxContractLifetime);
        }

        private static int ResolveVfxContractFrames(ProjectilePresentationSlotView slot)
        {
            if ((slot?.lifetimeFramesOverride ?? 0) > 0)
                return Mathf.Clamp(slot.lifetimeFramesOverride, 1, 600);
            ProjectileResourceKeyView source = slot?.resource;
            if (source == null || string.IsNullOrWhiteSpace(source.id))
                return 30;
            var key = new ResourceKey(
                source.id,
                string.IsNullOrWhiteSpace(source.typeId) ? ResourceTypeIds.GameObject : source.typeId,
                source.variant ?? string.Empty,
                source.packageId ?? string.Empty);
            VfxConsumerContract contract = VfxConsumerContractResolver.Find(key);
            return Mathf.Clamp(contract?.ActiveFrames ?? 30, 1, 600);
        }

        private IProjectileAuthoringVfxSelectionBridge PresentationVfxBridge => presentationVfxBridge ??= new ProjectileAuthoringVfxSelectionBridge();
        private IProjectileAuthoringVfxSelectionBridge presentationVfxBridge;

        private void SelectPresentationResource(string kind, ProjectileResourceKeyView resource, bool candidate, string orientation, Vector3 offset, int lifetime)
        {
            if (candidate)
            {
                ProjectileDefinitionListItem definition = workspace?.definitions?.FirstOrDefault(
                    value => string.Equals(value.stableId, session.SelectedStableId, StringComparison.Ordinal));
                RenderLifecycleCards(definition);
                RenderRuleInspector();
                return;
            }
            PresentationVfxBridge.SelectFormal(resource);
            CommitPresentationSlot(kind, resource.id, resource.typeId, resource.variant, resource.packageId, orientation, offset, lifetime, false);
        }

        private void CommitPresentationCommand(string kind, string slotKind = null, string exitKind = null, string exitMode = null, ProjectileResourceKeyView resource = null, string orientation = null, ProjectileVector3View offset = null, int lifetime = 0, bool synchronizeVfxContractLifetime = false)
        {
            if (workspace == null || service == null)
            {
                status.text = "VFX 应用失败：投射物工作区尚未就绪。";
                ShowError(status.text);
                return;
            }
            if (service.IsBusy)
            {
                status.text = "VFX 应用等待中：投射物工作区正在处理其他操作。";
                return;
            }
            if (CurrentIsGeneratedOnly)
            {
                status.text = "VFX 应用失败：仅生成条目没有可写入的源定义。";
                ShowError(status.text);
                return;
            }

            ProjectileDefinitionListItem definition = SelectedDefinition();
            if (definition == null || string.IsNullOrWhiteSpace(definition.stableId))
            {
                status.text = "VFX 应用失败：找不到当前请求所属的投射物定义。";
                ShowError(status.text);
                return;
            }

            string definitionStableId = definition.stableId;
            status.text = "正在将 VFX 应用到投射物定义：" + definitionStableId;
            PreviewMutation(new ProjectileAuthoringMutationRequestView
            {
                kind = kind,
                sourceStableId = definitionStableId,
                stableId = definitionStableId,
                definitionStableId = definitionStableId,
                workspaceToken = workspace.workspaceHash,
                planOnly = true,
                presentationSlotKind = slotKind,
                presentationResource = resource,
                presentationOrientation = orientation,
                presentationLocalOffset = offset,
                presentationLifetimeFramesOverride = lifetime,
                synchronizeVfxContractLifetime = synchronizeVfxContractLifetime,
                presentationExitKind = exitKind,
                presentationExitMode = exitMode
            });
        }

        public void ClearPresentationSlotForTests(string slotKind) => CommitPresentationCommand("ClearPresentationSlot", slotKind);

        internal bool ValidatePresentationProductionGateForTests()
        {
            ProjectileDefinitionListItem definition = workspace?.definitions?.FirstOrDefault(
                value => string.Equals(value.stableId, session.SelectedStableId, StringComparison.Ordinal));
            ProjectilePresentationBindingListItem binding = workspace?.bindings?.FirstOrDefault(
                value => value.projectileDefinitionId == definition?.definitionId);
            return ValidatePresentationProductionGate(binding);
        }

        internal void SetPresentationVfxBridgeForTests(IProjectileAuthoringVfxSelectionBridge value) => presentationVfxBridge = value;

        internal void SelectPresentationResourceForTests(string slotKind, ProjectileResourceKeyView resource, bool candidate)
            => SelectPresentationResource(slotKind, resource, candidate, PresentationOrientations(slotKind)[0], Vector3.zero, 0);

        private static string[] PresentationOrientations(string kind)
        {
            switch (kind)
            {
                case "spawn": case "expire": return new[] { "ProjectileForward", "WorldUp" };
                case "directImpact": return new[] { "SurfaceNormal", "ProjectileForward" };
                case "areaImpact": return new[] { "ImpactNormalOrWorldUp", "WorldUp" };
                case "blockedImpact": return new[] { "SurfaceNormal" };
                default: return new[] { "ProjectileForward" };
            }
        }

        private static string ExpectedPresentationRole(string kind) => kind == "flightBody" ? "Flight" : kind == "trail" ? "Trail" : kind == "spawn" ? "Spawn" : kind == "expire" ? "Expire" : "Impact";

        private static string SerializeResourceKey(ProjectileResourceKeyView value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.id)) return string.Empty;
            string result = value.id + ":" + (string.IsNullOrWhiteSpace(value.typeId) ? "GameObject" : value.typeId);
            if (!string.IsNullOrWhiteSpace(value.variant)) result += "#" + value.variant;
            if (!string.IsNullOrWhiteSpace(value.packageId)) result += "@" + value.packageId;
            return result;
        }

        private static void PingPresentationPrefab(string assetPath)
        {
            UnityEngine.Object asset = string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private bool ValidatePresentationProductionGate(ProjectilePresentationBindingListItem binding)
        {
            var diagnostics = new List<ProjectileAuthoringProblemView>();
            if (binding?.legacyV1 == true)
                AddProductionProblem(diagnostics, "PROJAUTH013", "presentationBinding.prefabResource", "Legacy Presentation Binding v1 cannot be published to Production/Player.");
            AddProductionSlotProblem(diagnostics, "spawn", binding?.detail?.slots?.spawn);
            AddProductionSlotProblem(diagnostics, "flightBody", binding?.detail?.slots?.flightBody);
            AddProductionSlotProblem(diagnostics, "trail", binding?.detail?.slots?.trail);
            AddProductionSlotProblem(diagnostics, "directImpact", binding?.detail?.slots?.directImpact);
            AddProductionSlotProblem(diagnostics, "areaImpact", binding?.detail?.slots?.areaImpact);
            AddProductionSlotProblem(diagnostics, "blockedImpact", binding?.detail?.slots?.blockedImpact);
            AddProductionSlotProblem(diagnostics, "expire", binding?.detail?.slots?.expire);
            transientProblems = diagnostics.ToArray();
            RenderProblems();
            return diagnostics.Count == 0;
        }

        private void AddProductionSlotProblem(List<ProjectileAuthoringProblemView> diagnostics, string kind, ProjectilePresentationSlotView slot)
        {
            string serializedKey = SerializeResourceKey(slot?.resource);
            if (string.IsNullOrWhiteSpace(serializedKey)) return;
            ProjectileAuthoringVfxTransientView view = PresentationVfxBridge.Resolve(serializedKey);
            string path = "presentationBinding.slots." + kind + ".resource";
            var findings = new List<ProjectileAuthoringVfxProductionGateFinding>();
            ProjectileAuthoringVfxProductionGate.EvaluateSlot(kind, view, findings);
            for (int i = 0; i < findings.Count; i++)
                AddProductionProblem(diagnostics, findings[i].Code, path, findings[i].Message);
        }

        private void AddProductionProblem(List<ProjectileAuthoringProblemView> diagnostics, string code, string fieldPath, string message)
        {
            diagnostics.Add(new ProjectileAuthoringProblemView
            {
                code = code,
                severity = "Error",
                message = message,
                objectPath = "definition:" + session.SelectedStableId,
                fieldPath = fieldPath,
                relatedStableIds = new[] { session.SelectedStableId }
            });
        }
    }
}
