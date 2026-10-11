// Presentation and entry points for the existing body-part/hurtbox authoring objects.

function createBodyWorkspaceItem(kind, options = {}) {
  if (!state.canWrite || !state.package || !["part", "collider"].includes(kind)) return false;
  const geometry = state.package.geometry || {};
  const selectedPart = getBodyWorkspaceSelection().part;
  const partId = options.partId || selectedPart?.partId || geometry.bodyParts?.[0]?.partId || "";
  if (kind === "collider" && !(geometry.bodyParts || []).some(part => part.partId === partId)) {
    state.message = "请先创建或选择一个身体部位，再添加受击体。";
    renderShellStatus();
    return false;
  }
  commitUndoTransaction();
  beginUndoTransaction(kind === "part" ? "新增身体部位" : "新增部位受击体", `body-create:${kind}`);
  state.package.geometry ||= {};
  state.package.geometry.bodyParts ||= [];
  state.package.geometry.colliders ||= [];
  const item = kind === "part" ? createBodyPartConfig(selectedPart?.partId || "") : createColliderConfig(partId);
  state.package.geometry[kind === "part" ? "bodyParts" : "colliders"].push(item);
  state.selectedPath = kind === "part" ? `geometry/body_parts/${item.partId}` : `geometry/colliders/${item.colliderId}`;
  state.bodyPartsPreviewScope = kind === "part" ? "part" : "selected";
  state.viewportMode = "character";
  state.contextDockCollapsed = true;
  state.activeInspectorTab = "properties";
  commitUndoTransaction();
  state.message = kind === "part" ? "已新增身体部位，可编辑名称并绑定骨骼。" : "已为所选部位新增受击体，可调整形状与位置。";
  render();
  return true;
}

function getBodyPartEditorFields(target, fields) {
  const part = target.kind === "part";
  const common = {
    partId: [part ? "部位 ID" : "所属部位", part ? "bodyPartAdvanced" : "hurtboxBinding"],
    displayName: ["部位名称", "bodyPartInfo"], partKind: ["部位类型", "bodyPartInfo"],
    parentPartId: ["上级部位", "bodyPartBinding"], bonePath: ["绑定骨骼", "bodyPartBinding"], locatorId: ["定位点", "bodyPartBinding"],
    defaultHitZoneId: ["默认命中区", "bodyPartRules"], reactionGroupId: ["受击反应组", "bodyPartRules"], tags: ["标签", "bodyPartAdvanced"],
    colliderId: ["受击体 ID", "hurtboxAdvanced"], shape: ["形状", "hurtboxShape"], hitZoneId: ["命中区", "hurtboxRules"],
    "localPose.parentKind": ["跟随空间", "hurtboxBinding"], "localPose.parentPath": ["跟随目标", "hurtboxBinding"],
    "localPose.position.x": ["位置 X", "hurtboxTransform"], "localPose.position.y": ["位置 Y", "hurtboxTransform"], "localPose.position.z": ["位置 Z", "hurtboxTransform"],
    "localPose.eulerHint.x": ["旋转 X", "hurtboxTransform"], "localPose.eulerHint.y": ["旋转 Y", "hurtboxTransform"], "localPose.eulerHint.z": ["旋转 Z", "hurtboxTransform"],
    "size.x": ["宽度", "hurtboxShape"], "size.y": ["高度", "hurtboxShape"], "size.z": ["深度", "hurtboxShape"],
    radius: ["半径", "hurtboxShape"], height: ["胶囊总高度", "hurtboxShape"], priority: ["查询优先级", "hurtboxAdvanced"],
    isWeakPoint: ["弱点", "hurtboxRules"], damageMultiplierOverride: ["伤害倍率", "hurtboxRules"],
    impactPowerScaleOverride: ["冲击倍率", "hurtboxRules"], materialStableId: ["表面材质引用", "hurtboxAdvanced"],
    physicsLayer: ["源层记录", "hurtboxAdvanced"]
  };
  const enumLabels = { Bone: "骨骼", Primitive: "基础几何", Virtual: "逻辑部位", Unknown: "未指定",
    Capsule: "胶囊", Box: "盒体", Sphere: "球体", ModelRoot: "模型根", SkeletonRoot: "骨骼根",
    Locator: "定位点", BodyPart: "身体部位", Socket: "挂点", WorldPreview: "预览世界", None: "无", Yes: "是", No: "否" };
  const order = part ? ["bodyPartInfo", "bodyPartBinding", "bodyPartRules", "bodyPartAdvanced"]
    : ["hurtboxBinding", "hurtboxShape", "hurtboxTransform", "hurtboxRules", "hurtboxAdvanced"];
  return fields.map(original => {
    const spec = { ...original };
    const presentation = common[spec.path];
    if (presentation) [spec.label, spec.group] = presentation;
    if (Array.isArray(spec.options)) spec.options = spec.options.map(option => {
      const entry = typeof option === "object" && option !== null ? { ...option } : { value: option, label: String(option) };
      if (["partKind", "shape", "localPose.parentKind", "isWeakPoint"].includes(spec.path)) entry.label = enumLabels[entry.label] || enumLabels[entry.value] || entry.label;
      else if (entry.value === "" && entry.label === "None") entry.label = "无";
      return entry;
    });
    if ((part && spec.path === "partId") || spec.path === "colliderId") {
      spec.readOnly = true;
      spec.help = "固定引用标识；编辑名称、形状或归属时保持不变。";
    }
    if (spec.path === "displayName") spec.help = "用于列表和当前对象标题显示。";
    if (spec.path === "partKind") spec.help = "描述部位结构，不自动创建受击体。";
    if (spec.path === "bonePath") spec.help = "部位的骨骼绑定；受击体也可以明确选择自己的跟随目标。";
    if (spec.path === "locatorId") spec.help = "可选的模型定位点。";
    if (spec.path === "defaultHitZoneId") spec.help = "新增受击体时使用此命中区作为初始值。";
    if (spec.path === "reactionGroupId") spec.help = "该部位使用的受击反应组引用。";
    if (spec.path === "tags") spec.help = "附加部位标签。";
    if (spec.path === "hitZoneId") spec.help = "此受击体命中时使用的命中区。";
    if (spec.path === "localPose.parentKind") spec.help = "决定局部位置与旋转相对哪个坐标空间。";
    if (spec.path === "localPose.parentPath") spec.help = "实际跟随的骨骼、定位点或部位，与受击归属分别配置。";
    if (spec.path === "height") spec.help = "包含两端半球的总高度，必须不小于直径。";
    if (["damageMultiplierOverride", "impactPowerScaleOverride"].includes(spec.path)) { spec.unit = "×"; spec.help = "1 表示不额外放大或缩小。"; }
    if (spec.path === "physicsLayer") {
      spec.type = spec.dataType = "text"; spec.options = null; spec.readOnly = true;
      spec.emptyText = "未记录";
      spec.help = "保留源记录。当前运行时固定将角色受击几何注册到角色受击层（CharacterHurtbox）。";
    }
    if (spec.path === "parentPartId" || (!part && spec.path === "partId")) {
      spec.options = (state.package?.geometry?.bodyParts || []).map(item => ({ value: item.partId,
        label: item.displayName ? `${item.displayName} · ${item.partId}` : item.partId,
        disabled: part && item.partId === target.value?.partId }));
      if (part) spec.options.unshift({ value: "", label: "无上级部位" });
      const current = target.value?.[spec.path] || "";
      if (current && !spec.options.some(option => option.value === current)) spec.options.push({ value: current, label: `未找到部位 · ${current}`, disabled: true });
      spec.help = part ? "身体部位的逻辑层级。" : "决定受击归属；空间跟随由下方的跟随空间和目标决定。";
    }
    return spec;
  }).sort((left, right) => order.indexOf(left.group) - order.indexOf(right.group));
}

function bindBodyPartInspectorActions() {
  el.inspector.querySelector("[data-body-part-advanced]")?.addEventListener("toggle", event => {
    state.bodyPartAdvancedOpen = event.target.open;
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
}

Object.assign(globalThis, { createBodyWorkspaceItem, getBodyPartEditorFields, bindBodyPartInspectorActions });
