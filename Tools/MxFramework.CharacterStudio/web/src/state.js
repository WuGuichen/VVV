const DEFAULT_PACKAGE = "Authoring/CharacterPackages/character-000001";
const CHARACTER_ACTION_HANDOFF = {
  generatedRoot: "Assets/MxFrameworkGenerated/CharacterActions",
  commonActionLibrarySourceDirectory: "CharacterActionSource/Libraries/Common"
};
const READINESS_STAGE_ORDER = [
  "CharacterPackage",
  "ResourceBinding",
  "AnimationBinding",
  "ReactionBinding",
  "ActionTimeline",
  "UnityImport",
  "RuntimeValidation",
  "AbilityRuntimeBinding"
];
const LAYERS = { colliders: true, sockets: true, traces: true, weapons: true, contactPoints: true, engagementAnchors: true };
const UNITY_GLTF_PREVIEW_POSE_ADAPTER = "unityGltfPreviewMirrorX";
const UNDO_HISTORY_LIMIT = 50;

const DEFAULT_ANIMATION_PROFILE_ID = "anim_profile.default";
const ANIMATION_PROFILE_SLOTS = [
  { slotId: "locomotion", displayName: "移动动画", purpose: "待机 / 行走 / 奔跑移动动画组", resourceHint: "locomotion", required: true }
];
const LEGACY_ANIMATION_PROFILE_SLOT_IDS = new Set(["base", "combat"]);
const DEFAULT_REACTION_PROFILE_ID = "reaction_profile.default";
const DEFAULT_DEATH_REACTION_ANIMATION_RESOURCE_KEY = "art.character.skeleton.animation.damage_ko_mid_2";
const REACTION_PROFILE_SLOTS = [
  { slotId: "cohesionBreak", displayName: "破防反应", trigger: "CohesionBreak", pressureBand: "Broken", reactionGroupId: "", minImpactForce: null, maxImpactForce: null, actionSuffix: "cohesion_break_react", durationFrames: 18, priority: 120, resourceHint: "damage_stun", required: true },
  { slotId: "hit.normal.high", displayName: "普通 · 上段", trigger: "Damage", pressureBand: "normal.high", reactionGroupId: "reaction.height.high", minImpactForce: null, maxImpactForce: 59, actionSuffix: "hit_normal_high_react", durationFrames: 12, priority: 90, resourceHint: "damage_high", required: false },
  { slotId: "hit.normal.mid", displayName: "普通 · 中段", trigger: "Damage", pressureBand: "normal.mid", reactionGroupId: "reaction.height.mid", minImpactForce: null, maxImpactForce: 59, actionSuffix: "hit_normal_mid_react", durationFrames: 12, priority: 89, resourceHint: "damage_mid", required: false },
  { slotId: "hit.normal.low", displayName: "普通 · 下段", trigger: "Damage", pressureBand: "normal.low", reactionGroupId: "reaction.height.low", minImpactForce: null, maxImpactForce: 59, actionSuffix: "hit_normal_low_react", durationFrames: 12, priority: 88, resourceHint: "damage_low", required: false },
  { slotId: "hit.strong.high", displayName: "强冲击 · 上段", trigger: "Damage", pressureBand: "strong.high", reactionGroupId: "reaction.height.high", minImpactForce: 60, maxImpactForce: null, actionSuffix: "hit_strong_high_react", durationFrames: 18, priority: 110, resourceHint: "damage_strong_head", required: false },
  { slotId: "hit.strong.mid", displayName: "强冲击 · 中段", trigger: "Damage", pressureBand: "strong.mid", reactionGroupId: "reaction.height.mid", minImpactForce: 60, maxImpactForce: null, actionSuffix: "hit_strong_mid_react", durationFrames: 18, priority: 109, resourceHint: "damage_strong_chest", required: false },
  { slotId: "hit.strong.low", displayName: "强冲击 · 下段", trigger: "Damage", pressureBand: "strong.low", reactionGroupId: "reaction.height.low", minImpactForce: 60, maxImpactForce: null, actionSuffix: "hit_strong_low_react", durationFrames: 18, priority: 108, resourceHint: "damage_strong_foot", required: false },
  { slotId: "death", displayName: "死亡", trigger: "Death", pressureBand: "", actionSuffix: "death_react", durationFrames: 60, priority: 200, resourceHint: "damage_ko_mid_2", defaultResourceKey: DEFAULT_DEATH_REACTION_ANIMATION_RESOURCE_KEY, required: false }
];
const RESOURCE_BINDING_ROLES = {
  body: { label: "身体模型", title: "绑定角色身体模型资源" },
  mainHand: { label: "主手资源", title: "绑定主手资源" },
  offHand: { label: "副手资源", title: "绑定副手资源" },
  preview: { label: "预览资源", title: "只预览资源，不写入角色字段" }
};

const IMPORT_MATERIAL_PRESETS = [
  { id: "characterModel", label: "角色模型", bindTarget: "body" },
  { id: "modelPreview", label: "预览模型", bindTarget: "" },
  { id: "animationClipGroup", label: "动画组", bindTarget: "" },
  { id: "texture", label: "贴图", bindTarget: "" },
  { id: "faceDetailTexture", label: "角色面部细节贴图", bindTarget: "faceDetail" },
  { id: "material", label: "材质", bindTarget: "" },
  { id: "audioCue", label: "音频 Cue", bindTarget: "" },
  { id: "vfxCue", label: "VFX", bindTarget: "" },
  { id: "config", label: "配置", bindTarget: "" }
];

const IMPORT_MATERIAL_TEMPLATES = [
  {
    id: "humanoid-basic",
    label: "基础人形",
    description: "身体模型和可选身体贴图。移动动画由 AnimationEditor 配置为 BlendTree。",
    items: [
      { id: "body-model", label: "身体模型", sourcePath: "body.fbx", presetId: "characterModel", bindTarget: "body", required: true },
      { id: "body-albedo", label: "身体贴图", sourcePath: "body_albedo.png", presetId: "texture", bindTarget: "", required: false },
      { id: "face-detail", label: "角色面部细节贴图", sourcePath: "FaceDetail/face_detail_rgba.png", presetId: "faceDetailTexture", bindTarget: "faceDetail", required: false }
    ]
  },
  { id: "custom-empty", label: "自定义", description: "从空清单开始。", items: [] }
];

const IMPORT_MATERIAL_SLOT_DEFINITIONS = [
  { key: "body", id: "body-model", label: "身体模型", presetId: "characterModel", bindTarget: "body", required: true, placeholder: "body.fbx" },
  { key: "bodyTexture", id: "body-albedo", label: "身体贴图", presetId: "texture", bindTarget: "", required: false, placeholder: "body_albedo.png" },
  { key: "faceDetail", id: "face-detail", label: "角色面部细节贴图", presetId: "faceDetailTexture", bindTarget: "faceDetail", required: false, placeholder: "FaceDetail/face_detail_rgba.png" }
];

const MODEL_USAGE_OPTIONS = [
  { value: "characterModel", label: "身体模型" },
  { value: "weaponModel", label: "武器模型" },
  { value: "previewMesh", label: "预览网格" }
];

const RESOURCE_KIND_LABELS = {
  Model: "Model",
  Animation: "Animation",
  Texture: "Texture",
  Material: "Material",
  AvatarMask: "AvatarMask",
  Vfx: "VFX",
  Audio: "Audio",
  Config: "Config",
  Generated: "Generated"
};
const PROVIDER_LABELS = {
  runtimeCatalog: "Runtime Catalog",
  characterPackage: "角色包",
  resourceMapping: "全局资源 Profile",
  unityAssetDatabase: "Unity Asset Database",
  unityProjectAssets: "Unity 项目资源",
  generatedAssets: "生成资源",
  characterAction: "CharacterAction 配置",
  fmod: "FMOD"
};

const PLAN_GROUPS = [
  {
    key: "spawnCritical",
    internalLabel: "SpawnCritical",
    defaultFailurePolicy: "FailSpawn",
    categoryKey: "required",
    categoryLabel: "必需",
    authorLabel: "Spawn 核心资源",
    authorSummary: "Spawn 开始前必须可用。",
    consequence: "缺失时阻塞 Spawn。"
  },
  {
    key: "equipmentInitial",
    internalLabel: "EquipmentInitial",
    defaultFailurePolicy: "UseFallbackEquipment",
    categoryKey: "required",
    categoryLabel: "必需",
    authorLabel: "初始装备",
    authorSummary: "首帧武器表现需要这些资源。",
    consequence: "缺失时使用装备兜底表现。"
  },
  {
    key: "animationWarmup",
    internalLabel: "AnimationWarmup",
    defaultFailurePolicy: "UseFallbackPose",
    categoryKey: "warmup",
    categoryLabel: "建议预热",
    authorLabel: "动画预热",
    authorSummary: "建议预热以减少首次播放卡顿。",
    consequence: "缺失时可退回默认姿势。"
  },
  {
    key: "vfxWarmup",
    internalLabel: "VfxWarmup",
    defaultFailurePolicy: "SkipEffect",
    categoryKey: "warmup",
    categoryLabel: "建议预热",
    authorLabel: "VFX 预热",
    authorSummary: "建议预热以保证特效播放稳定。",
    consequence: "缺失时跳过该特效。"
  },
  {
    key: "audio",
    internalLabel: "Audio",
    defaultFailurePolicy: "MuteMissingCue",
    categoryKey: "optional",
    categoryLabel: "可选",
    authorLabel: "音频 Cue",
    authorSummary: "Spawn 时可选，可稍后加载。",
    consequence: "缺失时该 Cue 静音。"
  },
  {
    key: "uiDeferred",
    internalLabel: "UiDeferred",
    defaultFailurePolicy: "ShowPlaceholder",
    categoryKey: "deferred",
    categoryLabel: "延迟",
    authorLabel: "UI 延迟资源",
    authorSummary: "Spawn 后或 UI 请求时加载。",
    consequence: "缺失时显示占位。"
  }
];

const PLAN_CATEGORIES = [
  { key: "required", label: "必需", description: "保证 Spawn 正确性的资源。" },
  { key: "warmup", label: "建议预热", description: "用于减少卡顿和首帧弹出。" },
  { key: "optional", label: "可选", description: "缺失时不阻塞 Spawn。" },
  { key: "deferred", label: "延迟", description: "通常按需稍后加载。" }
];

const RESOURCE_FIELD_SPECS = {
  body: {
    fieldKey: "Character.Model",
    editorKind: "CharacterStudio",
    displayName: "角色身体模型",
    acceptedKinds: ["Model"],
    acceptedUsages: ["characterModel"],
    acceptedProviderIds: ["runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["ResourceManagerAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: false,
    preloadPolicy: "SpawnCritical",
    outputKind: "RuntimeResourceKey"
  },
  bodyPublicModel: {
    fieldKey: "Character.Source.Body.resourceRef",
    editorKind: "CharacterStudio",
    displayName: "公共角色模型",
    acceptedKinds: ["Model"],
    acceptedUsages: ["characterModel"],
    acceptedProviderIds: ["runtimeCatalog", "resourceMapping"],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "SpawnCritical",
    outputKind: "ResourceSelectionRef"
  },
  bodyPublicTexture: {
    fieldKey: "Character.Source.BodyTexture.resourceRef",
    editorKind: "CharacterStudio",
    displayName: "公共身体贴图",
    acceptedKinds: ["Texture"],
    acceptedUsages: ["texture"],
    acceptedProviderIds: ["runtimeCatalog", "resourceMapping"],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "None",
    outputKind: "ResourceSelectionRef"
  },
  publicSourceResource: {
    fieldKey: "Character.Source.resourceRef",
    editorKind: "CharacterStudio",
    displayName: "公共源资源",
    acceptedKinds: ["Model", "Texture", "Material", "Vfx", "Audio", "Config", "Generated"],
    acceptedUsages: [],
    acceptedProviderIds: ["runtimeCatalog", "resourceMapping"],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "None",
    outputKind: "ResourceSelectionRef"
  },
  mainHand: {
    fieldKey: "Equipment.MainHand.weaponRef",
    editorKind: "CharacterStudio",
    displayName: "主手默认武器",
    acceptedKinds: ["Model"],
    acceptedUsages: ["weaponModel"],
    acceptedProviderIds: ["runtimeCatalog", "resourceMapping"],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    compatibilityFilter: { slotId: "mainHand" },
    preloadPolicy: "EquipmentInitial",
    outputKind: "ResourceSelectionRef"
  },
  offHand: {
    fieldKey: "Equipment.OffHand.weaponRef",
    editorKind: "CharacterStudio",
    displayName: "副手默认武器",
    acceptedKinds: ["Model"],
    acceptedUsages: ["weaponModel"],
    acceptedProviderIds: ["runtimeCatalog", "resourceMapping"],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset"],
    requireRuntimeLoadable: true,
    requireUnityImported: true,
    allowIncompatibleWithWarning: true,
    compatibilityFilter: { slotId: "offHand" },
    preloadPolicy: "EquipmentInitial",
    outputKind: "ResourceSelectionRef"
  },
  animationClip: {
    fieldKey: "Animation.Clip",
    editorKind: "CharacterStudio",
    displayName: "动画片段",
    acceptedKinds: ["Animation"],
    acceptedUsages: ["animationClip", "animationClipGroup"],
    acceptedProviderIds: ["unityProjectAssets", "unityAssetDatabase", "runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["UnityEditorOnlyAsset", "UnityAsset", "ResourceManagerAsset", "PackageResource"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "AnimationWarmup",
    outputKind: "ResourceSelectionRef"
  },
  animationProfileSlot: {
    fieldKey: "Animation.ProfileSlot",
    editorKind: "CharacterStudio",
    displayName: "动画 Profile 槽位",
    acceptedKinds: ["Animation"],
    acceptedUsages: ["animationClipGroup"],
    acceptedProviderIds: ["unityProjectAssets", "unityAssetDatabase", "runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["UnityEditorOnlyAsset", "UnityAsset", "ResourceManagerAsset", "PackageResource"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "AnimationWarmup",
    outputKind: "ResourceSelectionRef"
  },
  reactionAnimationSlot: {
    fieldKey: "Reaction.AnimationSlot",
    editorKind: "CharacterStudio",
    displayName: "受击动画槽位",
    acceptedKinds: ["Animation"],
    acceptedUsages: ["animationClip"],
    acceptedProviderIds: ["unityProjectAssets", "unityAssetDatabase", "runtimeCatalog", "characterPackage"],
    acceptedBindingKinds: ["UnityEditorOnlyAsset", "UnityAsset", "ResourceManagerAsset", "PackageResource"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "AnimationWarmup",
    outputKind: "ResourceSelectionRef"
  },
  preview: {
    fieldKey: "CharacterStudio.Selection",
    editorKind: "CharacterStudio",
    displayName: "资源预览",
    acceptedKinds: ["Model", "Animation", "Texture", "Material", "Vfx", "Audio", "Config", "Generated"],
    acceptedUsages: [],
    acceptedProviderIds: [],
    acceptedBindingKinds: ["ResourceManagerAsset", "UnityEditorOnlyAsset", "AudioEventDefinition", "AudioCue", "GeneratedPreviewOnly"],
    requireRuntimeLoadable: false,
    requireUnityImported: false,
    allowIncompatibleWithWarning: true,
    preloadPolicy: "None",
    outputKind: "ResourceSelectionRef"
  }
};

const FIELD_GROUP_LABELS = {
  resource: "资源身份",
  modelTransform: "模型变换",
  base: "基础",
  binding: "绑定",
  animation: "动画",
  animationGroup: "动画组",
  animationClip: "动画片段",
  animationBlend: "2D Blend",
  animationSlot: "动画槽位",
  reaction: "受击",
  reactionSlot: "受击槽位",
  selection: "选择",
  capability: "能力",
  wound: "伤口轴",
  structure: "结构轴",
  combat: "战斗",
  usage: "用途",
  localPose: "本地姿态",
  poseParent: "姿态父级",
  shape: "形状",
  contact: "接触点",
  trace: "轨迹"
};

const BODY_PART_KIND_OPTIONS = ["Unknown", "Bone", "Primitive", "Virtual"];
const BODY_KIND_OPTIONS = ["Unknown", "Skeletal", "Primitive", "Compound"];
const POSE_PARENT_KIND_OPTIONS = ["ModelRoot", "SkeletonRoot", "Bone", "Locator", "BodyPart", "Socket", "WorldPreview"];
const SOCKET_USAGE_OPTIONS = ["Unknown", "Weapon", "Vfx", "Camera", "Ui", "Gameplay"];
const ABSTRACT_BONE_ID_OPTIONS = [
  "root", "body_center", "head", "mouth", "left_hand", "right_hand",
  "left_foot", "right_foot", "tail_base", "tail_tip", "left_wing", "right_wing"
];
const SOCKET_HANDEDNESS_OPTIONS = [
  { value: "None", label: "无" },
  { value: "Left", label: "左手" },
  { value: "Right", label: "右手" },
  { value: "Both", label: "双手" }
];
const SOCKET_SIDE_OPTIONS = [
  { value: "Center", label: "中间" },
  { value: "Left", label: "左侧" },
  { value: "Right", label: "右侧" },
  { value: "Front", label: "前方" },
  { value: "Back", label: "后方" }
];
const EQUIP_SLOT_OPTIONS = [
  { value: "mainHand", label: "主手" },
  { value: "offHand", label: "副手" },
  { value: "twoHand", label: "双手" },
  { value: "naturalWeapon", label: "天然武器" }
];
const TRACE_SAMPLE_RULE_OPTIONS = ["LineSegment", "CapsuleSweep", "FixedSamples"];
const CONTACT_ACTIVATION_MODE_OPTIONS = ["Disabled", "Always", "ActionWindow", "ExplicitState", "MotionState"];
const CONTACT_MOTION_MODE_OPTIONS = ["Static", "BakedSocket"];
const COMMON_BODY_PART_IDS = ["root", "torso", "head", "right_hand", "left_hand", "right_leg", "left_leg", "main_hand", "off_hand"];
const COMMON_HIT_ZONE_IDS = ["hit.root", "hit.torso", "hit.head", "hit.right_hand", "hit.left_hand", "hit.right_leg", "hit.left_leg"];
const COMMON_SOCKET_IDS = ["mainHand", "offHand", "back", "headVfx", "camera", "uiAnchor"];
const COMMON_TAGS = [
  "body", "core", "critical", "hand", "leg", "main", "offhand", "stow",
  "weapon", "vfx", "weakPoint", "characterstudio-bind", "characterstudio-import", "converted-from-fbx"
];
const COMMON_ACTION_KEYS = ["primary", "secondary", "guard", "punch", "slash", "shield_guard"];
const COMMON_REACTION_GROUPS = ["reaction.humanoid.body", "reaction.humanoid.head", "reaction.humanoid.limb", "react.body"];

const PREVIEW_POSES = [
  { id: "bind", label: "绑定姿势" },
  { id: "guard", label: "防御姿势" },
  { id: "attack", label: "攻击姿势" },
  { id: "inspect", label: "检查姿势" }
];

const PREVIEW_MOTIONS = [
  { id: "none", label: "静态" },
  { id: "source_idle", label: "原包待机" },
  { id: "breath", label: "呼吸" },
  { id: "weapon_sway", label: "武器摆动" },
  { id: "walk_cycle", label: "行走循环" }
];

const KIND_LABELS = {
  tacticalDefaults: "战术",
  baseAttributes: "属性",
  overview: "流程",
  workflow: "阶段",
  manifest: "Debug",
  resources: "资源",
  resource: "资源",
  config: "配置",
  gameplayCamera: "游戏镜头",
  animationConfig: "动画配置",
  animationGroups: "动画组",
  animationGroup: "动画组",
  animationProfile: "动画 Profile",
  animationClip: "片段",
  animationBlendSpace: "Blend",
  animationBlendPoint: "Blend 点",
  animationSlot: "动画槽位",
  reactionConfig: "受击配置",
  reactionProfile: "受击 Profile",
  reactionSlot: "受击槽位",
  body: "身体",
  bodyPartsOverview: "身体与受击",
  part: "部位",
  collider: "受击体 Hurtbox",
  bodyPresence: "Body Presence",
  bodyPresenceVolume: "Presence Volume",
  bodyPresenceAnimatedSource: "Animated Hurtbox Source",
  bodyPartDamage: "部位伤害",
  bodyPartDamageDefinition: "部位状态",
  engagementAnchors: "接战锚点",
  engagementAnchor: "接战锚点",
  contactPoint: "接触点",
  socket: "挂点",
  weapon: "武器",
  trace: "轨迹",
  validation: "校验",
  issue: "问题"
};
const state = {
  packages: [],
  packageRelative: new URLSearchParams(window.location.search).get("package") || DEFAULT_PACKAGE,
  package: null,
  packageRevision: "",
  packageWriteConflict: null,
  bodyPresence: null,
  bodyPresenceSaving: false,
  bodyPartDamage: null,
  bodyPartDamageSaving: false,
  bodyPartDamageDirty: false,
  cleanBodyPartDamageJson: "",
  bodyPartDamageValidation: null,
  gameplayCameraPreviewMode: "normal",
  externalRefreshConflict: null,
  migrationPlan: null,
  validation: null,
  compileResult: null,
  importResult: null,
  unityImportTask: null,
  unityImportBuildTarget: new URLSearchParams(window.location.search).get("buildTarget") || "",
  readiness: null,
  clothMagica: null,
  abilityChain: null,
  combinedWeaponConfigurationsSnapshot: null,
  activeCombinedWeaponConfigurationId: "",
  combinedWeaponWorkspaceView: "edit",
  combinedWeaponSearchQuery: "",
  combinedWeaponReadinessFilter: "all",
  combinedWeaponDefaultFilter: "all",
  combinedWeaponSort: "id",
  combinedWeaponExpandedOverviewId: "",
  combinedWeaponCreationDialog: null,
  combinedWeaponSubWorkspace: "",
  activeReactionConfigurationWorkbenchId: "",
  activeReactionSlotId: "",
  reactionConfigurationSearchQuery: "",
  reactionConfigurationCreationDialog: null,
  reactionSlotCreationDialog: null,
  reactionConfigurationLibraryBaseline: [],
  reactionReferenceSnapshots: {},
  animationConfigurationLibraryBaseline: [],
  combinedWeaponConfigurationLibraryBaseline: [],
  weaponLibrary: null,
  weaponLibraryRevision: "",
  weaponLibraryDirty: false,
  weaponLibraryWriteConflict: null,
  activeSingleWeaponConfigurationId: "weapon.unarmed",
  singleWeaponPoseGuideVisible: true,
  singleWeaponPoseReferenceId: "sword",
  singleWeaponWorkspaceView: "edit",
  singleWeaponSearchQuery: "",
  singleWeaponCategoryFilter: "all",
  singleWeaponResourceFilter: "all",
  singleWeaponReferenceFilter: "all",
  singleWeaponSort: "id",
  singleWeaponLibraryBaseline: [],
  singleWeaponReferenceIndex: null,
  singleWeaponReferenceIndexLoaded: false,
  singleWeaponReferenceIndexLoading: false,
  singleWeaponReferenceIndexError: "",
  singleWeaponReferenceDetailId: "",
  singleWeaponExpandedOverviewId: "",
  singleWeaponCreationDialog: null,
  activeAnimationConfigurationId: "",
  activeAbilityConfigurationId: "",
  activeReactionConfigurationId: "",
  globalConfigurationReferences: {},
  characterActionSets: null,
  characterActionCatalog: null,
  characterActionOpenRequests: {},
  characterReactionProfiles: null,
  abilityActionOpenRequests: {},
  unityResourceCatalog: null,
  unityResourceCatalogPath: "",
  weaponTraceBakeCatalog: null,
  weaponTraceBakeCatalogPath: "",
  resourceLibrary: null,
  resourcePlan: null,
  commonResources: null,
  pendingWeaponAutoFitKey: "",
  importMaterials: null,
  importMaterialsDirty: false,
  importMaterialsSelectedIds: new Set(),
  importMaterialSourceCandidates: null,
  importMaterialSourcePicker: null,
  importMaterialSourcePickerSearch: "",
  importMaterialSourcePickerType: "",
  importMaterialUnrecognizedPaths: [],
  authoringSources: null,
  selectedPath: "overview",
  layers: { ...LAYERS },
  dirty: false,
  canWrite: false,
  apiAvailable: false,
  message: "",
  characterManagerMode: "",
  characterManagerSubmitting: false,
  characterIdCatalog: null,
  userSelectedPackage: Boolean(new URLSearchParams(window.location.search).get("package")),
  previewBones: [],
  previewBoneKey: "",
  activeBoneFieldPath: "",
  highlightedBoneValue: "",
  bonePickerOpen: false,
  bonePickerSearchQuery: "",
  bonePickerZoom: 1,
  bonePickerYaw: 0,
  bonePickerPitch: 0,
  previewPose: "bind",
  previewMotion: "source_idle",
  hurtboxCoverageVisible: false,
  hurtboxDisplayMode: "part",
  hurtboxDisplayPartId: "",
  hurtboxBindingReferenceVisible: true,
  viewportCameraState: null,
  viewportCameraStates: { character: null, weapons: {} },
  viewportMode: "character",
  attributePreviewOverlays: false,
  bodyPartsPreviewScope: "part",
  bodyPartAdvancedOpen: false,
  importMaterialsOperation: null,
  sourceSyncConflictPaths: [],
  bodyMeasurementsVisible: true,
  bodyAdvancedBindingsOpen: false,
  movementSourceCapsuleVisible: false,
  movementRegisteredBoundsVisible: true,
  movementSourceDetailsOpen: false,
  focusedWeaponAttachmentId: "",
  previousCharacterSelection: "",
  weaponTransformMode: "translate",
  weaponTransformKey: "",
  symmetryEditEnabled: false,
  contextDockCollapsed: true,
  activeContextTab: "binding",
  activeRuntimeValidationTab: "readiness",
  activeInspectorTab: "properties",
  selectedReactionSlotId: "",
  skipNextCameraRemember: false,
  treeCollapsed: false,
  resourcePickerOpen: false,
  resourcePickerField: null,
  resourcePickerQuery: null,
  resourcePickerLoading: false,
  resourcePickerSearchQuery: "",
  resourcePickerUsageFilter: "",
  resourcePickerProviderFilter: "",
  resourceManagerDelegation: null,
  undoStack: [],
  redoStack: [],
  activeUndoTransaction: null,
  cleanPackageJson: ""
};

const el = {};
let threeRuntimePromise = null;
let viewportRenderId = 0;
let viewportCleanup = null;
let viewportSelectionSync = null;
let viewportLivePoseBindings = [];
let viewportLiveContactPointBindings = [];
let viewportRefreshOverlay = null;
let viewportLiveContent = null;
let viewportLiveThree = null;
let viewportTransformControls = null;
let viewportTransformTargets = new Map();
let viewportSyncTransformControl = null;

Object.assign(globalThis, {
  DEFAULT_PACKAGE,
  CHARACTER_ACTION_HANDOFF,
  READINESS_STAGE_ORDER,
  LAYERS,
  UNITY_GLTF_PREVIEW_POSE_ADAPTER,
  UNDO_HISTORY_LIMIT,
  DEFAULT_ANIMATION_PROFILE_ID,
  ANIMATION_PROFILE_SLOTS,
  LEGACY_ANIMATION_PROFILE_SLOT_IDS,
  DEFAULT_REACTION_PROFILE_ID,
  DEFAULT_DEATH_REACTION_ANIMATION_RESOURCE_KEY,
  REACTION_PROFILE_SLOTS,
  RESOURCE_BINDING_ROLES,
  IMPORT_MATERIAL_PRESETS,
  IMPORT_MATERIAL_TEMPLATES,
  IMPORT_MATERIAL_SLOT_DEFINITIONS,
  MODEL_USAGE_OPTIONS,
  RESOURCE_KIND_LABELS,
  PROVIDER_LABELS,
  PLAN_GROUPS,
  PLAN_CATEGORIES,
  RESOURCE_FIELD_SPECS,
  FIELD_GROUP_LABELS,
  BODY_PART_KIND_OPTIONS,
  BODY_KIND_OPTIONS,
  POSE_PARENT_KIND_OPTIONS,
  SOCKET_USAGE_OPTIONS,
  ABSTRACT_BONE_ID_OPTIONS,
  SOCKET_HANDEDNESS_OPTIONS,
  SOCKET_SIDE_OPTIONS,
  EQUIP_SLOT_OPTIONS,
  TRACE_SAMPLE_RULE_OPTIONS,
  CONTACT_ACTIVATION_MODE_OPTIONS,
  CONTACT_MOTION_MODE_OPTIONS,
  COMMON_BODY_PART_IDS,
  COMMON_HIT_ZONE_IDS,
  COMMON_SOCKET_IDS,
  COMMON_TAGS,
  COMMON_ACTION_KEYS,
  COMMON_REACTION_GROUPS,
  PREVIEW_POSES,
  PREVIEW_MOTIONS,
  KIND_LABELS,
  state,
  el
});

Object.defineProperty(state, "activeWeaponConfigurationId", {
  get: () => state.activeCombinedWeaponConfigurationId,
  set: value => { state.activeCombinedWeaponConfigurationId = value || ""; },
  enumerable: false
});
Object.defineProperty(state, "weaponConfigurationsSnapshot", {
  get: () => state.combinedWeaponConfigurationsSnapshot,
  set: value => { state.combinedWeaponConfigurationsSnapshot = value; },
  enumerable: false
});

Object.defineProperties(globalThis, {
  threeRuntimePromise: { get: () => threeRuntimePromise, set: value => { threeRuntimePromise = value; }, configurable: true },
  viewportRenderId: { get: () => viewportRenderId, set: value => { viewportRenderId = value; }, configurable: true },
  viewportCleanup: { get: () => viewportCleanup, set: value => { viewportCleanup = value; }, configurable: true },
  viewportSelectionSync: { get: () => viewportSelectionSync, set: value => { viewportSelectionSync = value; }, configurable: true },
  viewportLivePoseBindings: { get: () => viewportLivePoseBindings, set: value => { viewportLivePoseBindings = value; }, configurable: true },
  viewportLiveContactPointBindings: { get: () => viewportLiveContactPointBindings, set: value => { viewportLiveContactPointBindings = value; }, configurable: true },
  viewportRefreshOverlay: { get: () => viewportRefreshOverlay, set: value => { viewportRefreshOverlay = value; }, configurable: true },
  viewportLiveContent: { get: () => viewportLiveContent, set: value => { viewportLiveContent = value; }, configurable: true },
  viewportLiveThree: { get: () => viewportLiveThree, set: value => { viewportLiveThree = value; }, configurable: true },
  viewportTransformControls: { get: () => viewportTransformControls, set: value => { viewportTransformControls = value; }, configurable: true },
  viewportTransformTargets: { get: () => viewportTransformTargets, set: value => { viewportTransformTargets = value; }, configurable: true },
  viewportSyncTransformControl: { get: () => viewportSyncTransformControl, set: value => { viewportSyncTransformControl = value; }, configurable: true }
});

export { state, el };
