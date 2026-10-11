import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as THREE from "three";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const sampleRoot = path.join(repoRoot, "Tools/MxFramework.Authoring/samples/character-codex-flow-test");
const codexFlowTestSampleRoot = sampleRoot;
const packagepackage000003Root = path.join(repoRoot, "Authoring/CharacterPackages/character-000003");
const unityGeneratedRoot = path.join(repoRoot, "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test");
const required = [
  "manifest.json",
  "resource_catalog.json",
  "config/character_application.json",
  "config/character_actions.json",
  "geometry/body_geometry.json",
  "geometry/body_parts.json",
  "geometry/body_colliders.json",
  "geometry/sockets.json",
  "geometry/weapon_slots.json",
  "geometry/body_part_contact_points.json",
  "validation/last_report.json"
];
const requiredGenerated = [
  "config/unity_resource_catalog.json",
  "package_cache/import_report.json",
  "package_cache/unity_editor_import_receipt.json"
];

for (const relative of required) {
  const full = path.join(sampleRoot, relative);
  if (!fs.existsSync(full)) {
    console.error(`missing ${relative}`);
    process.exit(1);
  }
}
for (const relative of requiredGenerated) {
  const full = path.join(unityGeneratedRoot, relative);
  if (!fs.existsSync(full)) {
    console.error(`missing generated ${relative}`);
    process.exit(1);
  }
}

const manifest = readJson("manifest.json");
const resources = readJson("resource_catalog.json");
const colliders = readJson("geometry/body_colliders.json");
const sockets = readJson("geometry/sockets.json");
const weaponSlots = readJson("geometry/weapon_slots.json");
const applicationConfig = readJson("config/character_application.json");
const globalWeaponLibrary = readRepoJson("WeaponSource/global_weapon_library.json");
const contactPoints = readJson("geometry/body_part_contact_points.json");
const unityCatalog = readGeneratedJson("config/unity_resource_catalog.json");
const importReport = readGeneratedJson("package_cache/import_report.json");
const codexFlowTestColliders = readPackageJson(codexFlowTestSampleRoot, "geometry/body_colliders.json");
const codexFlowTestImportMaterials = readRepoJson("ArtSource/Characters/codex_flow_test/character_sources.json");
const commonCharacterSources = readRepoJson("ArtSource/Common/Characters/common_character_1/character_sources.json");
const commonWeaponSources = readRepoJson("ArtSource/Common/Weapons/common_weapon_1/weapon_sources.json");
const commonKiteShieldSources = readRepoJson("ArtSource/Common/Weapons/common_kite_shield/weapon_sources.json");
const commonTextureSources = readRepoJson("ArtSource/Common/Textures/common_body_albedo/resource_sources.json");
const resourceMapping = readRepoJson("Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json");
const codexFlowTestRuntimeCatalog = readRepoJson("Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/config/runtime_resource_catalog.json");
const codexFlowTestResourcePlan = readRepoJson("Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/config/character_resource_plan.json");
const codexFlowTestImportMaterialsState = readPackageJson(codexFlowTestSampleRoot, "config/import_materials_state.json");
const codexFlowTestResources = readPackageJson(codexFlowTestSampleRoot, "resource_catalog.json");
const codexFlowTestWeaponSlots = readPackageJson(codexFlowTestSampleRoot, "geometry/weapon_slots.json");
const package000003BodyPresence = readPackageJson(packagepackage000003Root, "geometry/body_presence.json");
const package000003BodyPartDamage = readPackageJson(packagepackage000003Root, "geometry/body_part_damage.json");
const appEntrySource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/app.js"), "utf8");
const stateModuleSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/state.js"), "utf8");
const sessionStateSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/sessionState.js"), "utf8");
globalThis.READINESS_STAGE_ORDER = [
  "CharacterPackage",
  "ResourceBinding",
  "AnimationBinding",
  "ReactionBinding",
  "ActionTimeline",
  "UnityImport",
  "RuntimeValidation",
  "AbilityRuntimeBinding"
];
const contextPanelsPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/contextPanels.js");
const contextPanelsSource = fs.readFileSync(contextPanelsPath, "utf8");
await import(pathToFileURL(contextPanelsPath).href);
const apiPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/api.js");
const apiSource = fs.readFileSync(apiPath, "utf8");
await import(pathToFileURL(apiPath).href);
const singleWeaponWorkbenchPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/singleWeaponWorkbench.js");
const singleWeaponWorkbenchSource = fs.readFileSync(singleWeaponWorkbenchPath, "utf8");
const singleWeaponWorkbench = await import(pathToFileURL(singleWeaponWorkbenchPath).href);
const viewport3dPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/viewport3d.js");
const viewport3d = await import(pathToFileURL(viewport3dPath).href);
const combinedWeaponWorkbenchPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/combinedWeaponWorkbench.js");
const combinedWeaponWorkbenchSource = fs.readFileSync(combinedWeaponWorkbenchPath, "utf8");
const combinedWeaponWorkbench = await import(pathToFileURL(combinedWeaponWorkbenchPath).href);
const reactionConfigurationWorkbenchPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/reactionConfigurationWorkbench.js");
const reactionConfigurationWorkbenchSource = fs.readFileSync(reactionConfigurationWorkbenchPath, "utf8");
const reactionConfigurationWorkbench = await import(pathToFileURL(reactionConfigurationWorkbenchPath).href);
const resourcePickerSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/resourcePicker.js"), "utf8");
const appSource = readCharacterStudioWebSources();
const buildImportMaterialSlotStatusesSource = appSource.slice(
  appSource.indexOf("function buildImportMaterialSlotStatuses"),
  appSource.indexOf("function renderImportMaterialSlotCards")
);
const renderContextDockSummarySource = appSource.slice(
  appSource.indexOf("function renderContextDockSummary"),
  appSource.indexOf("function getPrimaryReadinessIssue")
);
const indexSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/index.html"), "utf8");
const stylesSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/styles.css"), "utf8");
const editorServerSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs"), "utf8");
const editorServerBodyPresenceSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.BodyPresence.cs"), "utf8");
const editorServerBodyPartDamageSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterBodyPartDamageAuthoring.cs"), "utf8");
const bodyPartDamageValidatorSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterPackageValidation.BodyPartDamage.cs"), "utf8");
const bodyPartDamageTestsSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.BodyPartDamage.cs"), "utf8");
const editorServerGlobalWeaponLibrarySource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.GlobalWeaponLibrary.cs"), "utf8");
const editorServerAbilityChainSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.AbilityChain.cs"), "utf8");
const editorServerReadinessSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.Readiness.cs"), "utf8");
const editorServerReactionProfileSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterActionReactionProfile.cs"), "utf8");
const editorServerImportMaterialsSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.ImportMaterials.cs"), "utf8");
const editorServerCommonResourcesSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CommonResources.cs"), "utf8");
const editorServerCharacterActionOpenSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterActionOpen.cs"), "utf8");
const editorServerCharacterUnityImportEditorSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterUnityImportEditor.cs"), "utf8");
const characterActionAuthoringModelSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionAuthoringModel.cs"), "utf8");
const characterActionAuthoringWindowSourcesSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterActionAuthoringWindow.Sources.cs"), "utf8");
const characterTestActionConfigSource = fs.readFileSync(path.join(repoRoot, "Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionConfigSource.cs"), "utf8");
const importMaterialsDocumentSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterImportMaterialsDocument.cs"), "utf8");
const characterActionPreviewRuntimeSessionSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterAction/Timeline/Preview/CharacterActionPreviewRuntimeSession.cs"), "utf8");
const characterPackageImportRequestBridgeSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterPackageImportRequestBridge.cs"), "utf8");
const characterPackageImportCommandSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterPackageImportCommand.cs"), "utf8");
const characterImportedPackagePrefabBuilderSource = fs.readFileSync(path.join(repoRoot, "Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.cs"), "utf8");
const characterUnityImportBridgeSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterUnityImportBridge.cs"), "utf8");
const characterUnityImportGlbCompatibilitySource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterUnityImportGlbCompatibility.cs"), "utf8");
const characterResourcePlanCompilerSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterResourcePlanCompiler.cs"), "utf8");
const authoringResourceReferenceGraphSource = fs.readFileSync(path.join(repoRoot, "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/AuthoringResourceReferenceGraph.cs"), "utf8");
const codexFlowTestActionSet = readRepoJson("Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test/character_action_sets.json");
const codexFlowTestActionLibrary = readRepoJson("Assets/MxFrameworkGenerated/CharacterActions/Libraries/codex_flow_test/character_actions.json");
const commonCharacterActionLibrary = readRepoJson("Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json");
const codexFlowTestActionProfile = readRepoJson("Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test/character_action_profile.json");

assert(manifest.packageId === "codex_flow_test", "manifest packageId should be codex_flow_test");
assert(editorServerSource.includes("character-codex-flow-test"), "Authoring server should resolve default character templates to the codex flow test sample");
assert(editorServerSource.includes("RewriteCharacterTemplateImportMaterials"), "Character create should rewrite template import material resource keys");
assert(editorServerSource.includes('/api/character/duplicate') && editorServerSource.includes("DuplicateCharacterPackage"), "Authoring server should expose character duplication through the transactional package copy path.");
assert(editorServerSource.includes('/api/character/archive') && editorServerSource.includes("ArchiveCharacterPackage") && editorServerSource.includes("generatedOutputsRetained"), "Authoring server should archive active character sources while explicitly retaining generated outputs.");
assert(indexSource.includes('id="newCharacterButton"') && indexSource.includes('id="duplicateCharacterButton"') && indexSource.includes('id="archiveCharacterButton"'), "CharacterStudio toolbar should expose create, duplicate, and archive actions.");
assert(indexSource.includes('id="characterManagerOverlay"') && indexSource.includes('id="characterManagerArchiveConfirm"'), "CharacterStudio should use a dedicated lifecycle dialog with explicit archive confirmation.");
assert(indexSource.includes('id="migrateCharacterButton"'), "CharacterStudio should expose explicit character package schema migration.");
assert(indexSource.includes('id="reloadButton"') && indexSource.includes("刷新状态") && indexSource.includes('id="discardReloadButton"') && indexSource.includes("放弃改动并重载"), "CharacterStudio should separate non-destructive status refresh from explicit draft discard and reload.");
assert(appEntrySource.includes('import "./src/characterManager.js?v=character-manager-v2";'), "CharacterStudio app entry should load the character manager module.");
const sessionStateImportIndex = appEntrySource.search(/import "\.\/src\/sessionState\.js\?v=[^"]+";/);
const refreshActionWiringIndex = appEntrySource.indexOf('el.reloadButton.addEventListener("click", () => refreshExternalState())');
assert(sessionStateImportIndex >= 0 && refreshActionWiringIndex > sessionStateImportIndex
  && appEntrySource.includes('el.discardReloadButton.addEventListener("click", () => discardDraftAndReload())'),
  "CharacterStudio should load session preservation before wiring refresh actions.");
assert(sessionStateSource.includes("sessionStorage") && sessionStateSource.includes("localStorage") && sessionStateSource.includes('window.addEventListener("beforeunload"') && sessionStateSource.includes("restoreCharacterStudioDraftSnapshot"), "CharacterStudio should protect temporary drafts and restore editor UI across browser reloads.");
assert(/async function refreshExternalState[\s\S]*createCharacterStudioDraftSnapshot[\s\S]*loadPackageState[\s\S]*restoreCharacterStudioDraftSnapshot/.test(appSource), "CharacterStudio status refresh should reload external state only after capturing and then restoring the current draft.");
assert(/async function discardDraftAndReload[\s\S]*confirm[\s\S]*clearSavedCharacterStudioDraft[\s\S]*ignoreSessionDraft/.test(appSource), "CharacterStudio destructive reload should require confirmation and explicitly bypass the temporary draft.");
assert(/async function discardDraftAndReload[\s\S]*suspendCharacterStudioSessionPersistence[\s\S]*clearSavedCharacterStudioDraft[\s\S]*await loadAll\(\{ ignoreSessionDraft: true, uiSnapshot \}\)[\s\S]*clearSavedCharacterStudioDraft[\s\S]*resumeCharacterStudioSessionPersistence/.test(appSource), "CharacterStudio destructive reload should keep draft persistence suspended and clear the discarded draft again after loading disk state.");
assert(appSource.includes("/api/character/migration") && appSource.includes("CHARACTER_PACKAGE_WRITE_CONFLICT"), "CharacterStudio migration UI should use the shared optimistic conflict contract.");
assert(appSource.includes("expectedRevision") && appSource.includes("强制保存"), "CharacterStudio package save should preserve a revision and require explicit force after conflicts.");
assert(appSource.includes("function openCharacterManager") && appSource.includes("function submitCharacterManager") && appSource.includes("hasUnsavedCharacterManagerChanges"), "CharacterStudio lifecycle UI should block package switches when the current draft is dirty.");
assert(appSource.includes('endpoint = "/api/character/create"') && appSource.includes('endpoint = "/api/character/duplicate"') && appSource.includes('endpoint = "/api/character/archive"'), "CharacterStudio lifecycle dialog should call create, duplicate, and archive APIs.");
assert(stylesSource.includes(".character-manager-dialog") && stylesSource.includes(".character-manager-confirm"), "CharacterStudio should style the lifecycle dialog and destructive confirmation separately.");
assertStarterColliderAliases(codexFlowTestColliders, null, "codex flow test");
await assertCodexFlowTestPreviewBoneAliases();
const codexFlowTestBodyMaterial = codexFlowTestImportMaterials.items.find(item => item.id === "body-model");
const codexFlowTestBodySelection = codexFlowTestBodyMaterial?.source?.selection || {};
const codexFlowTestBodyIsPackageFile =
  codexFlowTestBodyMaterial?.source?.kind === "file" &&
  codexFlowTestBodyMaterial.source?.scope === "package" &&
  sourcePathOf(codexFlowTestBodyMaterial) === "character_00.fbx";
const codexFlowTestBodyIsPublicRef =
  codexFlowTestBodyMaterial?.source?.kind === "resourceRef" &&
  codexFlowTestBodyMaterial.source?.scope === "public" &&
  codexFlowTestBodySelection.sourceProviderId === "resourceMapping" &&
  codexFlowTestBodySelection.runtimeResourceKey === "character.common_character_1.model";
assert(codexFlowTestBodyMaterial?.presetId === "characterModel" && (codexFlowTestBodyIsPackageFile || codexFlowTestBodyIsPublicRef), "codex flow test source manifest should include either package-local character_00.fbx or a public character model resourceRef.");
if (codexFlowTestBodyIsPublicRef) {
  assert(codexFlowTestBodyMaterial.target?.resourceKey === "char.codex_flow_test.model.body", "codex flow test public body resourceRef should still bind through the local body alias resource key.");
} else {
  assert(codexFlowTestBodyMaterial.source?.scope === "package", "codex flow test body source should prefer package-local ArtSource files.");
  assert(sourcePathOf(codexFlowTestBodyMaterial) === "character_00.fbx", "codex flow test body source should be the human-authored FBX.");
}
assert(!codexFlowTestImportMaterials.items.some(item => /character_00.*\.glb$/i.test(sourcePathOf(item) || "")), "codex flow test source manifest should not expose generated body GLB artifacts.");
assert(codexFlowTestImportMaterials.items.some(item => item.id === "body-albedo" && item.presetId === "texture" && item.required === false), "codex flow test body texture should be optional source tracking.");
assert(!codexFlowTestImportMaterials.items.some(item => /weapon/i.test(item.presetId || item.id || item.bindTarget || "")), "codex flow test character source manifest should not own weapon source items.");
assert(commonCharacterSources.items?.some(item => item.target?.resourceKey === "character.common_character_1.model" && item.source?.scope === "public"), "common character source manifest should register a reusable character model key.");
assert(commonWeaponSources.items?.some(item => item.target?.resourceKey === "weapon.common_weapon_1.model" && item.source?.scope === "public"), "common weapon source manifest should register a reusable weapon model key.");
assert(commonWeaponSources.items?.some(item => item.target?.resourceKey === "weapon.common_weapon_1.model" && item.target?.importHints?.modelWrapperPose?.scale?.x === 1 && item.target?.importHints?.modelWrapperPose?.scale?.y === 1 && item.target?.importHints?.modelWrapperPose?.scale?.z === 1), "common weapon source manifest should keep unit conversion out of resource-level model wrapper scale.");
assert(commonKiteShieldSources.items?.some(item => item.target?.resourceKey === "weapon.common_kite_shield.model" && item.source?.scope === "public"), "common kite shield source manifest should register a reusable offHand weapon model key.");
assert(commonTextureSources.items?.some(item => item.target?.resourceKey === "texture.common_body_albedo" && item.target?.resourceKind === "Texture" && item.source?.scope === "public"), "common texture source manifest should register a reusable texture key.");
assert(codexFlowTestImportMaterialsState.items.some(item => item.id === "body-model" && item.lastImportedHash), "codex flow test import state should keep body import hash outside the source manifest.");
assert(!JSON.stringify(codexFlowTestImportMaterials).includes("lastImportedHash"), "codex flow test source manifest should not store import hashes.");
assert(!fs.existsSync(path.join(codexFlowTestSampleRoot, "config/import_materials.json")), "codex flow test should not retain the legacy import_materials.json standard path.");
const codexFlowTestBodyResource = codexFlowTestResources.entries.find(entry => entry.resourceKey === "char.codex_flow_test.model.body");
const codexFlowTestPreviewBodyArtifact = codexFlowTestResources.entries.find(entry => entry.resourceKey === "char.codex_flow_test.preview.body_mesh");
assert(codexFlowTestBodyResource?.usage === "characterModel" && codexFlowTestBodyResource.sourceFormat === "fbx" && codexFlowTestBodyResource.relativePath === "resources/models/character_00.fbx", "codex flow test body catalog entry should use the FBX as characterModel authority.");
assert(!(codexFlowTestBodyResource.tags || []).includes("converted-from-fbx"), "codex flow test body characterModel should not be tagged as an FBX-converted GLB.");
assert(codexFlowTestPreviewBodyArtifact?.usage === "previewMesh" && codexFlowTestPreviewBodyArtifact.sourceFormat === "glb", "codex flow test preview GLB should be separated as a previewMesh artifact.");
assert(codexFlowTestPreviewBodyArtifact?.relativePath === "previews/character_00.preview.glb", "codex flow test preview GLB should live under previews as an artifact.");
assert(codexFlowTestPreviewBodyArtifact?.importHints?.metadata?.sourceResourceKey === "char.codex_flow_test.model.body", "codex flow test preview artifact should point back to the FBX source resource.");
assert(codexFlowTestPreviewBodyArtifact?.importHints?.metadata?.artifactPath === "previews/character_00.preview.glb", "codex flow test preview artifact metadata should use the previews artifact path.");
const codexFlowTestMainHandSlot = codexFlowTestWeaponSlots.weaponSlots.find(slot => slot.slotId === "mainHand");
const codexFlowTestOffHandSlot = codexFlowTestWeaponSlots.weaponSlots.find(slot => slot.slotId === "offHand");
const codexFlowTestSwordDefinition = globalWeaponLibrary.singleWeaponConfigurations.find(definition => definition.singleWeaponConfigurationId === "sword");
const codexFlowTestShieldDefinition = globalWeaponLibrary.singleWeaponConfigurations.find(definition => definition.singleWeaponConfigurationId === "shield");
const codexFlowTestPublicWeaponProfileEntry = resourceMapping.entries?.find(entry => entry.resourceKey?.id === "weapon.common_weapon_1.model");
const codexFlowTestPublicShieldProfileEntry = resourceMapping.entries?.find(entry => entry.resourceKey?.id === "weapon.common_kite_shield.model");
assert(codexFlowTestMainHandSlot, "codex flow test mainHand character weapon slot should exist.");
assert(getDefinitionResourceKey(codexFlowTestSwordDefinition) === "weapon.common_weapon_1.model", "codex flow test sword definition should use the public weaponRef.");
assert(codexFlowTestSwordDefinition.weaponRef?.sourceProviderId === "resourceMapping", "codex flow test sword weaponRef should point at the global resource build profile.");
assert(codexFlowTestMainHandSlot.attachSocketId === "mainHand" && !codexFlowTestMainHandSlot.singleWeaponConfigurationId, "schema 5.0 character slot should own only Socket and slot pose, not a weapon reference.");
assert(codexFlowTestPublicWeaponProfileEntry?.labels?.includes("usage.weaponmodel"), "public default weapon should be registered as a weapon model in the production mapping Profile.");
assert(codexFlowTestPublicWeaponProfileEntry?.sourceLocator?.cachedPath, "public default weapon mapping entry should carry a cached Unity asset path for preview and prefab resolution.");
assert(codexFlowTestPublicWeaponProfileEntry?.sourceLocator?.unityGuid, "public default weapon mapping entry should carry a Unity GUID for prefab resolution.");
assert(/_weapon\.prefab$/i.test(codexFlowTestPublicWeaponProfileEntry?.sourceLocator?.cachedPath || ""), "public default weapon runtime path should point to the wrapper prefab, not the raw model asset.");
assert(codexFlowTestPublicWeaponProfileEntry?.deliveryIntent === "internal" && codexFlowTestPublicWeaponProfileEntry?.bundleIntent?.ruleId === "common.models", "public default weapon mapping entry should be internally delivered through the common models Bundle.");
assert(!codexFlowTestResources.entries.some(entry => entry.resourceKey === "weapon.common_weapon_1.model"), "codex flow test resource catalog should not copy the public default weapon into the character package.");
assert(!codexFlowTestResources.entries.some(entry => entry.resourceKey === "char.codex_flow_test.weapon.main_hand.model"), "codex flow test resource catalog should not retain the legacy package-local mainHand weapon.");
assert(!codexFlowTestResources.entries.some(entry => entry.resourceKey === "char.codex_flow_test.weapon.off_hand.model"), "codex flow test resource catalog should not retain the legacy package-local offHand weapon.");
const packagePreviewMaterialBindings = viewport3d.buildPackagePreviewMaterialBindings([
  {
    resourceKey: "char.preview.material.body",
    localId: "material.body",
    typeId: "material",
    provenance: { sourceFile: "Assets/Vendor/Materials/Body.mat" },
    dependencies: [{ resourceKey: "char.preview.texture.body" }, { resourceKey: "char.preview.texture.body_rim_mask" }]
  },
  {
    resourceKey: "char.preview.texture.body",
    localId: "texture.body",
    typeId: "texture",
    relativePath: "resources/textures/Body.png",
    hash: "sha256:body"
  },
  {
    resourceKey: "char.preview.texture.body_rim_mask",
    localId: "texture.body_rim_mask",
    typeId: "texture",
    relativePath: "resources/textures/Body_Rim_Mask.png",
    hash: "sha256:mask"
  },
  {
    resourceKey: "char.preview.material.sword_hair_01",
    localId: "material.sword_hair_01",
    typeId: "material",
    provenance: { sourceFile: "Assets/Vendor/Materials/Sword_Hair_01.material.json" },
    dependencies: [{ resourceKey: "char.preview.texture.sword_hair" }]
  },
  {
    resourceKey: "char.preview.texture.sword_hair",
    localId: "texture.sword_hair",
    typeId: "texture",
    relativePath: "resources/textures/Sword_Hair.png",
    hash: "sha256:hair"
  }
]);
assert(packagePreviewMaterialBindings.length === 2
  && packagePreviewMaterialBindings[0].normalizedName === "body"
  && packagePreviewMaterialBindings[0].textureResourceKey === "char.preview.texture.body",
"CharacterStudio should bind extracted material descriptors to their primary package textures for FBX preview.");
assert(viewport3d.normalizePreviewMaterialName("Sword_Hair_01.material.json") === "swordhair"
  && viewport3d.findPreviewMaterialBinding("Sword_Hair", packagePreviewMaterialBindings)?.textureResourceKey === "char.preview.texture.sword_hair",
"CharacterStudio should match vendor material descriptors whose file names carry numeric variant suffixes.");
const descriptorOnlyPreviewResources = [
  {
    resourceKey: "char.preview.material.descriptor_body",
    localId: "material.body",
    typeId: "material",
    relativePath: "resources/materials/Body/Body.material.json",
    dependencies: []
  },
  { resourceKey: "char.preview.texture.descriptor_color", typeId: "texture", relativePath: "resources/textures/Body_Color.png" },
  { resourceKey: "char.preview.texture.descriptor_normal", typeId: "texture", relativePath: "resources/textures/Body_Normal.png" },
  { resourceKey: "char.preview.texture.descriptor_mask", typeId: "texture", relativePath: "resources/textures/Body_Mask.png" }
];
const descriptorOnlyBindings = viewport3d.buildPackagePreviewMaterialBindings(descriptorOnlyPreviewResources);
assert(descriptorOnlyBindings.length === 1
  && descriptorOnlyBindings[0].descriptorRelativePath === "resources/materials/Body/Body.material.json"
  && descriptorOnlyBindings[0].textureResourceKey === "",
"CharacterStudio should retain a material descriptor for preview even when legacy catalog dependencies are empty.");
const loadedPreviewTextures = [];
const originalFetch = globalThis.fetch;
globalThis.fetch = async () => ({
  ok: true,
  json: async () => ({
    sourceAssetPath: "Body.mat",
    textures: [
      { property: "_BaseMap", resourceKey: "char.preview.texture.descriptor_color" },
      { property: "_WgNormalTex", resourceKey: "char.preview.texture.descriptor_normal" },
      { property: "_WgMaskTex", resourceKey: "char.preview.texture.descriptor_mask" }
    ]
  })
});
let descriptorLoadedBindings;
try {
  descriptorLoadedBindings = await globalThis.loadPackagePreviewMaterialBindings({
    SRGBColorSpace: "srgb",
    NoColorSpace: "linear",
    RepeatWrapping: "repeat",
    TextureLoader: class {
      load(url, resolve) {
        const texture = {
          url,
          repeat: { set(x, y) { this.x = x; this.y = y; } },
          offset: { set(x, y) { this.x = x; this.y = y; } }
        };
        loadedPreviewTextures.push(texture);
        resolve(texture);
      }
    }
  }, descriptorOnlyPreviewResources, relativePath => `/package/${relativePath}`);
} finally {
  globalThis.fetch = originalFetch;
}
assert(descriptorLoadedBindings[0].textureResourceKey === "char.preview.texture.descriptor_color"
  && descriptorLoadedBindings[0].normalTextureResourceKey === "char.preview.texture.descriptor_normal"
  && descriptorLoadedBindings[0].maskTextureResourceKey === "char.preview.texture.descriptor_mask"
  && descriptorLoadedBindings[0].texture?.colorSpace === "srgb"
  && descriptorLoadedBindings[0].normalTexture?.colorSpace === "linear"
  && descriptorLoadedBindings[0].maskTexture?.colorSpace === "linear"
  && loadedPreviewTextures.length === 3,
"CharacterStudio should resolve Color, Normal, and Mask textures from the material descriptor with the correct color-space semantics.");
const alphaClipSurface = viewport3d.resolvePackagePreviewSurfaceSettings({
  surfaceMode: "AlphaClip",
  alphaCutoff: 0.42,
  renderQueue: 2000
});
const explicitOpaqueSurface = viewport3d.resolvePackagePreviewSurfaceSettings({
  surfaceMode: "Opaque",
  renderQueue: 2450,
  keywords: ["_ALPHATEST_ON"]
});
assert(alphaClipSurface.surfaceMode === "AlphaClip"
  && alphaClipSurface.isCutout
  && !alphaClipSurface.isTransparent
  && Math.abs(alphaClipSurface.alphaCutoff - 0.42) < 0.000001
  && explicitOpaqueSurface.surfaceMode === "Opaque"
  && !explicitOpaqueSurface.isCutout,
  "CharacterStudio preview should treat the explicit surfaceMode/alphaCutoff contract as authoritative over legacy queue inference.");
const paletteBindingV4 = {
  format: "mx.characterPaletteBinding.v5",
  schemaVersion: "5.0",
  enabled: true,
  colorAtlasResourceKey: "wgame.character.color_palette.shared",
  addressingMode: "colorUvSurfaceProfileVertexAlpha",
  atlasWidth: 256,
  atlasHeight: 256,
  surfaceProfileSource: "paletteDataAlpha16",
  surfaceProfileCount: 16,
  cullMode: "Off"
};
const surfacePaletteBinding = viewport3d.normalizeSurfacePaletteBinding(paletteBindingV4);
assert(surfacePaletteBinding?.atlasWidth === 256
  && surfacePaletteBinding.atlasHeight === 256
  && surfacePaletteBinding.surfaceProfileSource === "paletteDataAlpha16"
  && surfacePaletteBinding.cullMode === "Off"
  && viewport3d.normalizeSurfacePaletteBinding({ ...paletteBindingV4, enabled: false }) === null
  && viewport3d.normalizeSurfacePaletteBinding({ ...paletteBindingV4, surfaceProfileSource: "paletteAlpha" }) === null
  && viewport3d.normalizeSurfacePaletteBinding({ ...paletteBindingV4, format: "mx.characterPaletteBinding.v3" }) === null,
"CharacterStudio should normalize only v4 Color/Profile bindings and reject legacy Palette-Alpha authority.");
const paletteGeometry = new THREE.BufferGeometry();
paletteGeometry.setAttribute("position", new THREE.Float32BufferAttribute([
  0, 0, 0,
  1, 0, 0,
  0, 1, 0
], 3));
paletteGeometry.setAttribute("uv1", new THREE.Float32BufferAttribute([
  0.5 / 256, 1.5 / 256,
  7.5 / 256, 16.5 / 256,
  13.5 / 256, 42.5 / 256
], 2));
paletteGeometry.setAttribute("color", new THREE.Float32BufferAttribute([
  1, 0, 0, 200 / 255,
  1, 0, 0, 200 / 255,
  1, 0, 0, 200 / 255
], 4));
const paletteTexture = new THREE.Texture();
const paletteMesh = new THREE.Mesh(paletteGeometry, new THREE.MeshBasicMaterial({ color: 0xffffff }));
paletteMesh.name = "PaletteProfileSmokeMesh";
const paletteRoot = new THREE.Group();
paletteRoot.add(paletteMesh);
const paletteApply = viewport3d.applySurfacePalettePreviewMaterials(THREE, paletteRoot, {
  ...surfacePaletteBinding,
  catalogHash: "sha256:test",
  catalog: {
    surfaceProfiles: Array.from({ length: 16 }, (_, index) => ({
      index,
      profileId: `profile_${index}`,
      roughness: 0.9 - index * 0.03,
      metallic: index >= 12 ? 0.8 : 0,
      specular: 0.04 + index * 0.02,
      rim: 0.1 + index * 0.02
    }))
  },
  texture: paletteTexture
});
const appliedPaletteUv = paletteMesh.geometry.getAttribute("uv");
const paletteShaderProbe = {
  vertexShader: "void main() {\n}",
  fragmentShader: "void main() {\n#include <color_fragment>\n#include <map_fragment>\n#include <roughnessmap_fragment>\n#include <metalnessmap_fragment>\n#include <emissivemap_fragment>\n}"
};
paletteMesh.material.onBeforeCompile(paletteShaderProbe);
assert(paletteApply.appliedCount === 1
  && paletteApply.missingPaletteUvCount === 0
  && Math.abs(appliedPaletteUv.getX(1) - 7.5 / 256) < 0.000001
  && Math.abs(appliedPaletteUv.getY(1) - 16.5 / 256) < 0.000001
  && paletteMesh.material.isMeshStandardMaterial
  && paletteMesh.material.map === paletteTexture
  && !paletteMesh.material.vertexColors
  && paletteMesh.material.userData.paletteSurfaceProfileSource === "paletteDataAlpha16"
  && paletteShaderProbe.vertexShader.includes("attribute vec4 wgPaletteData")
  && paletteShaderProbe.fragmentShader.includes("wgSurfaceProfile")
  && paletteShaderProbe.fragmentShader.includes("roughnessFactor = wgProfileRoughness")
  && paletteShaderProbe.fragmentShader.includes("metalnessFactor = wgProfileMetallic")
  && paletteShaderProbe.fragmentShader.includes("totalEmissiveRadiance += diffuseColor.rgb"),
"CharacterStudio should preserve full FBX uv1.xy and derive one of 16 Surface Profiles from PaletteData.A.");
paletteMesh.material.dispose();
paletteMesh.geometry.dispose();
paletteTexture.dispose();
const sourceIdleCandidates = viewport3d.getPackagePreviewAnimationCandidates({
  animationInventory: {
    entries: [
      { packageRelativePath: "source/unity/animations/Normal/S_Idle.fbx", clips: [{ name: "S_Idle", isLooping: true, suggestedRole: "locomotion.idle" }] },
      { packageRelativePath: "source/unity/animations/Special/S_Sp_Idle.fbx", clips: [{ name: "S_Sp_Idle", isLooping: true, suggestedRole: "locomotion.idle" }] }
    ]
  }
}, "source_idle");
assert(sourceIdleCandidates[0]?.clipName === "S_Sp_Idle",
  "CharacterStudio should prefer the package's sword-specific authored idle FBX for the default preview.");
const previewQuaternion = { clone: () => ({}) };
const locatorPreviewRoot = { name: "preview", parent: null };
const previewSkeletonRoot = { name: "root", parent: locatorPreviewRoot, isBone: true, quaternion: previewQuaternion };
const previewWeaponLocator = { name: "add_weapon_r", parent: previewSkeletonRoot, isBone: false, quaternion: previewQuaternion };
locatorPreviewRoot.traverse = callback => { callback(previewSkeletonRoot); callback(previewWeaponLocator); };
const previewPoseRecords = viewport3d.collectBoneRecords(locatorPreviewRoot, ["root/add_weapon_r"]);
assert(previewPoseRecords.some(record => record.path === "root/add_weapon_r" && record.alias === "locator.add_weapon_r"),
  "CharacterStudio should track authored non-bone weapon locators so mounted models follow package animation.");
const socketPoseNodePaths = viewport3d.getSocketPoseNodePaths([{
  locatorPath: "root/add_weapon_r",
  bonePath: "root/pelvis/hand_r",
  localPose: { parentPath: "root/add_weapon_r" }
}]);
assert(socketPoseNodePaths.length === 2
  && socketPoseNodePaths[0] === "root/add_weapon_r"
  && socketPoseNodePaths[1] === "root/pelvis/hand_r",
"CharacterStudio should load explicit weapon locator nodes before fallback hand bones and de-duplicate pose paths.");
const wrapperSourceRoot = {
  position: { x: 4, y: 5, z: 6, set(x, y, z) { this.x = x; this.y = y; this.z = z; } },
  quaternion: { x: 0.5, y: 0.5, z: 0.5, w: 0.5, identity() { this.x = 0; this.y = 0; this.z = 0; this.w = 1; } },
  scale: { x: 0.01, y: 0.01, z: 0.01 },
  updateMatrix() { this.matrixUpdated = true; },
  updateMatrixWorld(force) { this.matrixWorldUpdated = force; }
};
const wrapperPreviewRoot = { children: [wrapperSourceRoot], userData: {} };
assert(viewport3d.normalizePreviewSourceRootForUnityWrapper(wrapperPreviewRoot) === wrapperSourceRoot
  && wrapperSourceRoot.position.x === 0
  && wrapperSourceRoot.position.y === 0
  && wrapperSourceRoot.position.z === 0
  && wrapperSourceRoot.quaternion.w === 1
  && wrapperSourceRoot.scale.x === 0.01
  && wrapperPreviewRoot.userData.unityWrapperSourceRootNormalized === true,
"CharacterStudio should mirror Unity wrapper prefabs by clearing only the raw model root position/rotation while preserving imported scale.");
assert(appSource.includes('`weapon_${attachment.weaponId}_model`, { inheritScale: false, poseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER }'),
  "CharacterStudio should convert global Unity-authored weapon modelPose values consistently in the character viewport.");
const embeddedWeaponPackage = {
  resourceCatalog: {
    entries: [
      {
        resourceKey: "char.preview.model.body",
        typeId: "model",
        usage: "characterModel",
        importHints: {
          metadata: {
            hiddenRendererNodePaths: "root/Body_Full;Face/Face_01Re",
            embeddedWeaponNodePath: "root/add_weapon_r/Weapon_Sword",
            rendererMaterialBindingsJson: JSON.stringify({
              bindings: [
                {
                  rendererNodePath: "root/Body_Full",
                  materialSlotIndex: 0,
                  materialResourceKey: "char.preview.material.body",
                  sourceMaterialName: "Body"
                }
              ]
            })
          }
        },
        dependencies: [{ resourceKey: "char.preview.material.weapon_sword" }]
      },
      {
        resourceKey: "char.preview.material.weapon_sword",
        localId: "material.weapon_sword",
        typeId: "material",
        provenance: { sourceFile: "Assets/Vendor/Materials/Weapon_Sword.mat" }
      }
    ]
  }
};
assert(viewport3d.hasEmbeddedWeaponSource(embeddedWeaponPackage),
  "CharacterStudio should distinguish a weapon mesh embedded in the character source model from a configured framework weapon.");
const embeddedBodyResource = embeddedWeaponPackage.resourceCatalog.entries[0];
assert(viewport3d.getEmbeddedWeaponNodePaths(embeddedBodyResource).join("|") === "root/add_weapon_r/Weapon_Sword",
  "CharacterStudio should read explicit embedded weapon node paths from model import metadata.");
assert(viewport3d.getHiddenRendererNodePaths(embeddedBodyResource).join("|") === "root/Body_Full|Face/Face_01Re",
  "CharacterStudio should read source Prefab renderer visibility from model import metadata.");
assert(viewport3d.getPreviewHiddenNodePaths(embeddedBodyResource, false).join("|") === "root/Body_Full|Face/Face_01Re"
  && viewport3d.getPreviewHiddenNodePaths(embeddedBodyResource, true).includes("root/add_weapon_r/Weapon_Sword"),
  "CharacterStudio should always hide inactive source variants and hide the embedded weapon only when a framework weapon is configured.");
const embeddedWeaponNode = { name: "Weapon_Sword", visible: true, children: [] };
const embeddedWeaponParent = { name: "add_weapon_r", visible: true, children: [embeddedWeaponNode] };
const inactiveBodyVariant = { name: "Body_Full", visible: true, children: [] };
const inactiveFaceVariant = { name: "Face_01Re", visible: true, children: [] };
const skeletonRoot = {
  name: "root",
  visible: true,
  children: [embeddedWeaponParent, inactiveBodyVariant, inactiveFaceVariant]
};
const unrelatedWeaponNode = { name: "Weapon_Sword", visible: true, children: [] };
const unrelatedRoot = { name: "preview_only", visible: true, children: [unrelatedWeaponNode] };
const previewRoot = {
  name: "CombatGirl_Sword",
  visible: true,
  children: [skeletonRoot, unrelatedRoot],
  userData: {},
  traverse(visitor) {
    const visit = node => {
      visitor(node);
      for (const child of node.children || []) {
        child.parent = node;
        visit(child);
      }
    };
    visit(this);
  }
};
const hiddenSourceVariants = viewport3d.hidePreviewModelNodes(
  previewRoot,
  viewport3d.getPreviewHiddenNodePaths(embeddedBodyResource, false));
assert(hiddenSourceVariants.join("|") === "root/Body_Full|root/Face_01Re"
  && inactiveBodyVariant.visible === false
  && inactiveFaceVariant.visible === false
  && embeddedWeaponNode.visible === true,
"CharacterStudio should reproduce source Prefab renderer visibility without hiding its embedded weapon by default.");
const rendererMaterialBindings = viewport3d.getRendererMaterialBindings(embeddedBodyResource);
assert(rendererMaterialBindings.length === 1
  && rendererMaterialBindings[0].materialResourceKey === "char.preview.material.body"
  && viewport3d.findPreviewRendererMaterialBinding(previewRoot, inactiveBodyVariant, 0, rendererMaterialBindings)?.sourceMaterialName === "Body",
"CharacterStudio should resolve source Prefab renderer-slot material bindings independently of FBX material names.");
const sourceNameOnlyBindings = viewport3d.getRendererMaterialBindings({
  importHints: {
    metadata: {
      rendererMaterialBindingsJson: JSON.stringify({
        bindingMode: "sourceMaterialName",
        bindings: [{
          rendererNodePath: "",
          materialSlotIndex: -1,
          materialResourceKey: "char.preview.material.eye",
          sourceMaterialName: "Eye"
        }]
      })
    }
  }
});
assert(sourceNameOnlyBindings.length === 1
  && sourceNameOnlyBindings[0].materialSlotIndex === -1
  && viewport3d.findPreviewRendererMaterialBinding(previewRoot, inactiveBodyVariant, 3, sourceNameOnlyBindings, "Eye")?.materialResourceKey === "char.preview.material.eye",
"CharacterStudio should resolve pre-Unity source-material-name bindings without requiring a Renderer path or fixed slot index.");
const hiddenEmbeddedWeaponNodes = viewport3d.hideEmbeddedPreviewModelNodes(
  previewRoot,
  viewport3d.getEmbeddedWeaponNodePaths(embeddedBodyResource));
assert(hiddenEmbeddedWeaponNodes.join("|") === "root/add_weapon_r/Weapon_Sword"
  && embeddedWeaponNode.visible === false
  && unrelatedWeaponNode.visible === true,
"CharacterStudio should hide only the explicitly declared source weapon node when a framework weapon is attached.");

const missingCombinedWeaponIssue = {
  severity: "Error",
  code: "COMBINED_WEAPON_CONFIGURATION_MISSING",
  message: "At least one explicit combined weapon configuration is required.",
  sourcePath: "config/character_application.json",
  sourceObjectPath: "combinedWeaponConfigurations"
};
const invalidDefaultCombinedWeaponIssue = {
  severity: "Error",
  code: "COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID",
  message: "defaultCombinedWeaponConfigurationId must reference exactly one existing combination.",
  sourcePath: "config/character_application.json",
  sourceObjectPath: "defaultCombinedWeaponConfigurationId"
};
const missingAnimationIssue = {
  severity: "Error",
  code: "ANIMATION_AUTHORING_MISSING",
  message: "Animation authoring configuration is missing.",
  sourcePath: "config/animation_authoring.json"
};
const missingPreviewIssue = {
  severity: "Warning",
  code: "CHARPKG_MISSING_PREVIEW_RESOURCE",
  message: "No preview resource is registered.",
  sourcePath: "resource_catalog.json"
};
const missingReactionIssue = {
  severity: "Warning",
  code: "GLOBAL_REACTION_CONFIGURATION_NOT_FOUND",
  message: "No global reaction configuration is selected.",
  sourcePath: "config/character_application.json",
  sourceObjectPath: "reactionConfigurationId"
};
const duplicatedReadinessSnapshot = {
  blockers: [missingCombinedWeaponIssue, invalidDefaultCombinedWeaponIssue, missingAnimationIssue, missingCombinedWeaponIssue],
  warnings: [missingPreviewIssue, missingReactionIssue, missingPreviewIssue],
  stages: [
    { id: "CharacterPackage", blockers: [missingCombinedWeaponIssue, invalidDefaultCombinedWeaponIssue], warnings: [missingPreviewIssue] },
    { id: "ResourceBinding", blockers: [missingCombinedWeaponIssue, missingCombinedWeaponIssue], warnings: [missingPreviewIssue, missingPreviewIssue] },
    { id: "AnimationBinding", blockers: [missingAnimationIssue], warnings: [] },
    { id: "ReactionBinding", blockers: [], warnings: [missingReactionIssue] },
    { id: "RuntimeValidation", blockers: [missingCombinedWeaponIssue], warnings: [missingPreviewIssue] }
  ]
};
const canonicalBlockers = globalThis.collectWorkflowIssueRecords(duplicatedReadinessSnapshot, "blocker");
const canonicalWarnings = globalThis.collectWorkflowIssueRecords(duplicatedReadinessSnapshot, "warning");
const combinedWeaponRecord = canonicalBlockers.find(record => record.code === "COMBINED_WEAPON_CONFIGURATION_MISSING");
assert(canonicalBlockers.length === 2
  && canonicalWarnings.length === 2
  && !canonicalBlockers.some(record => record.code === "COMBINED_WEAPON_CONFIGURATION_DEFAULT_INVALID"),
"CharacterStudio should count canonical readiness causes once and suppress an invalid-default cascade when the combined-weapon list is empty.");
assert(combinedWeaponRecord?.stageId === "ActionTimeline",
  "Missing combined-weapon configuration should route to the weapon/action authoring stage instead of source binding.");
const importCascadeSnapshot = {
  blockers: [],
  warnings: [],
  stages: [
    { id: "UnityImport", blockers: [{ severity: "Error", code: "UNITY_EDITOR_IMPORT_RECEIPT_ACCEPTANCE_INVALID", message: "Receipt is invalid.", sourcePath: "validation" }], warnings: [] },
    { id: "RuntimeValidation", blockers: [{ severity: "Error", code: "RUNTIME_VALIDATION_IMPORT_FAILED", message: "Runtime waits for import.", sourcePath: "" }], warnings: [] }
  ]
};
const canonicalImportBlockers = globalThis.collectWorkflowIssueRecords(importCascadeSnapshot, "blocker");
assert(canonicalImportBlockers.length === 1
  && canonicalImportBlockers[0].code === "UNITY_EDITOR_IMPORT_RECEIPT_ACCEPTANCE_INVALID",
"CharacterStudio should count an invalid Unity receipt as the root blocker and suppress the dependent RuntimeValidation cascade.");
const combinedWeaponDescription = globalThis.describeWorkflowIssue(combinedWeaponRecord, "blocker");
assert(combinedWeaponDescription.primaryLabel === "去组合武器"
  && combinedWeaponDescription.title === "需要创建组合武器配置",
"Missing combined-weapon configuration should expose a localized and actionable next step.");
const codexFlowTestEquipmentInitial = codexFlowTestResourcePlan.equipmentInitial?.resources || [];
assert(codexFlowTestEquipmentInitial.some(resource => resource.resourceKey === "weapon.common_weapon_1.model" && resource.packageId === "common"), "codex flow test resource plan should preload the public mainHand weapon from the common package.");
assert(!codexFlowTestEquipmentInitial.some(resource => resource.resourceKey === "weapon.common_kite_shield.model"), "the two accepted sword/empty and empty/empty combinations should not preload an unused shield.");
assert(!codexFlowTestEquipmentInitial.some(resource => resource.resourceKey === "char.codex_flow_test.weapon.main_hand.model"), "codex flow test resource plan should not preload a legacy package-local mainHand weapon.");
assert(!codexFlowTestEquipmentInitial.some(resource => resource.resourceKey === "char.codex_flow_test.weapon.off_hand.model"), "codex flow test resource plan should not preload a legacy package-local offHand weapon.");
const codexFlowTestPublicWeaponRuntimeEntry = codexFlowTestRuntimeCatalog.entries?.find(entry => entry.id === "weapon.common_weapon_1.model");
const codexFlowTestPublicShieldRuntimeEntry = codexFlowTestRuntimeCatalog.entries?.find(entry => entry.id === "weapon.common_kite_shield.model");
assert(codexFlowTestPublicWeaponRuntimeEntry?.packageId === "common" && codexFlowTestPublicWeaponRuntimeEntry?.providerData?.sourceProviderId === "resourceMapping", "codex flow test runtime catalog should include the public mainHand weapon from the global profile.");
assert(codexFlowTestPublicWeaponRuntimeEntry?.providerData?.modelWrapperPose && codexFlowTestPublicWeaponRuntimeEntry?.providerData?.unityAssetGuid, "codex flow test runtime catalog should preserve public weapon wrapper pose and Unity guid metadata.");
assert(/_weapon\.prefab$/i.test(codexFlowTestPublicWeaponRuntimeEntry?.providerData?.generatedAssetPath || ""), "codex flow test runtime catalog should load the public weapon wrapper prefab.");
assert(/common_weapon_1\.fbx$/i.test(codexFlowTestPublicWeaponRuntimeEntry?.providerData?.rawGeneratedAssetPath || ""), "codex flow test runtime catalog should retain raw public weapon model metadata for diagnostics.");
assert(!codexFlowTestPublicShieldRuntimeEntry, "the character runtime catalog should omit the unused public shield while the global profile continues to own it.");
assert(codexFlowTestPublicShieldProfileEntry?.labels?.includes("usage.weaponmodel") && codexFlowTestPublicShieldProfileEntry?.deliveryIntent === "internal", "public offHand weapon should be registered as an internal weapon model in the production mapping Profile.");
assert(!codexFlowTestRuntimeCatalog.entries?.some(entry => entry.id === "char.codex_flow_test.weapon.main_hand.model"), "codex flow test runtime catalog should not retain the legacy package-local mainHand weapon.");
assert(codexFlowTestOffHandSlot && getDefinitionResourceKey(codexFlowTestShieldDefinition) === "weapon.common_kite_shield.model", "codex flow test shield definition should use the public weaponRef.");
assert(codexFlowTestShieldDefinition.weaponRef?.sourceProviderId === "resourceMapping", "codex flow test shield weaponRef should point at the global resource build profile.");
assert(codexFlowTestOffHandSlot.attachSocketId === "offHand" && !codexFlowTestOffHandSlot.singleWeaponConfigurationId, "schema 5.0 offHand slot should not persist weapon-owned preview fields.");
assert(!codexFlowTestImportMaterials.items.some(item => item.id === "locomotion-animation"), "codex flow test import materials should not treat Locomotion BlendTree as a source file");
assert(codexFlowTestImportMaterials.items.some(item => item.id === "body-albedo" && item.presetId === "texture"), "codex flow test import materials should include body texture");
assert(Array.isArray(resources.entries) && resources.entries.length > 0, "resource catalog should have entries");
assert(Array.isArray(colliders.colliders) && colliders.colliders.length > 0, "colliders should exist");
assert(Array.isArray(sockets.sockets) && sockets.sockets.some(s => s.socketId === "mainHand"), "mainHand socket should exist");
assert(Array.isArray(weaponSlots.weaponSlots) && weaponSlots.weaponSlots.some(slot => slot.slotId === "mainHand"), "mainHand character weapon slot should exist");
assert(globalWeaponLibrary.singleWeaponConfigurations.some(weapon => weapon.singleWeaponConfigurationId === "sword" && weapon.traceProfiles?.some(trace => trace.traceId === "trace.iron_sword.blade")), "sword should own its weapon-local trace");
assert(Array.isArray(contactPoints.contactPoints) && contactPoints.contactPoints.some(point => point.stableId === "contact.foot_l.sole"), "body part contact point source should exist");
assert(unityCatalog.packageId === manifest.packageId, "Unity resource catalog packageId should match manifest");
assert(Array.isArray(unityCatalog.entries) && unityCatalog.entries.length > 0, "Unity resource catalog should have entries");
assert(importReport.packageId === manifest.packageId, "Unity import report packageId should match manifest");
assert(Array.isArray(importReport.operations) && importReport.operations.some(op => op.kind === "unityResourceCatalog"), "Unity import report should include unityResourceCatalog operation");
assert(appSource.includes("RESOURCE_FIELD_SPECS"), "CharacterStudio should expose ResourceFieldSpec-driven selection");
assertCharacterStudioWebModules();
assert(appSource.includes('if (parentKind === "ModelRoot") return true;')
  && appSource.includes('if (parentKind === "Bone") return Boolean(String(socket?.bonePath || "").trim());')
  && appSource.includes('if (parentKind === "Locator") return Boolean(String(socket?.locatorPath || "").trim());')
  && appSource.includes('if (parentKind === "BodyPart") return Boolean(String(socket?.parentPartId || "").trim());'),
  "CharacterStudio socket diagnostics should accept explicit ModelRoot bindings while typed bindings remain fail-closed.");
assert(appSource.includes("Stable ModelRoot binding: follows the character root and ignores bone animation."),
  "CharacterStudio should identify stable ModelRoot socket bindings directly in the Socket Inspector.");
assert(indexSource.includes('./app.js?v=palette-render-face-v1') && indexSource.includes('./styles.css?v=rgb565-color-palette-v3') && appEntrySource.includes('import "./src/api.js?v=rgb565-color-palette-v3";') && appEntrySource.includes('import "./src/surfacePaletteWorkbench.js?v=palette-render-face-v1";') && appEntrySource.includes('import "./src/sessionState.js?v=body-part-damage-m2-v2";') && appEntrySource.includes('import "./src/contextPanels.js?v=body-part-damage-m2-v2";') && appEntrySource.includes('import "./src/state.js?v=body-part-damage-m2-v2";') && appEntrySource.includes('import "./src/inspector.js?v=body-part-damage-m2-v2";') && appEntrySource.includes('import "./src/combinedWeaponWorkbench.js?v=character-action-slots-v1";') && appEntrySource.includes('import "./src/importMaterialsPanel.js?v=alpha-clip-v1";') && appEntrySource.includes('import "./src/viewport3d.js?v=rgb565-color-palette-v3";'), "CharacterStudio should cache-bust the Palette render-face editor while preserving current module versions.");
assert(indexSource.includes('id="hurtboxCoverageButton"') && appSource.includes('addHurtboxCoverageHeatmap') && appSource.includes('falseNegativeRate') && appSource.includes('firstHitDepthP95Meters'), "CharacterStudio should expose six-direction animated Hurtbox coverage heatmaps and quantitative diagnostics.");
assert(appSource.includes("手动编辑受击体") && appSource.includes('data-collider-edit-action="show-selected"') && appSource.includes('data-collider-edit-action="expand"') && appSource.includes("handleViewportTransformHotkeys") && appSource.includes("focusSelectedViewportTransformTarget"), "CharacterStudio should expose focused per-collider manual editing with local gizmo hotkeys and centimeter margin controls.");
assert(appSource.includes("start.colliderSize.x * sx") && appSource.includes("start.colliderSize.y * sy") && appSource.includes("start.colliderSize.z * sz"), "CharacterStudio Box gizmo scaling should update authoritative Box size instead of persisting a second pose scale.");
assert(appSource.includes("自动拟合只负责提供手动编辑起点") && appSource.includes("载入为手动编辑草稿"), "CharacterStudio should present automatic Hurtbox fitting as an initial draft, not a final authoring result.");
assert(stylesSource.includes(".collider-manual-editor") && stylesSource.includes(".collider-manual-actions"), "CharacterStudio should visually group manual Hurtbox editing actions.");
assert(contextPanelsSource.includes('["4.0", "5.0", "6.0", "7.0", "8.0"].includes(schemaVersion)') && contextPanelsSource.includes("renderWeaponConfigurationPanel4();"), "schema 4.0 through 8.0 should route to the current combined-weapon workbench instead of the legacy summary form.");
assert(indexSource.includes('data-context-tab="validation"') && indexSource.includes('data-validation-tab="animation"') && indexSource.includes('data-validation-tab="ability"') && indexSource.includes('data-validation-tab="reaction"') && indexSource.includes('data-validation-tab="resources"') && indexSource.includes('data-validation-tab="readiness"'), "CharacterStudio should consolidate generated animation, ability, reaction, resource, and readiness views under runtime validation.");
assert(!indexSource.includes('data-context-tab="abilities"') && !indexSource.includes('data-context-tab="animation"') && !indexSource.includes('data-context-tab="resourcePlan"') && !appEntrySource.includes('["animationLibrary", "动画配置"]') && !appEntrySource.includes('["abilityLibrary", "能力配置"]') && !appEntrySource.includes('["reactionLibrary", "受击配置"]'), "CharacterStudio should not expose duplicate global configuration or generated-result primary tabs.");
assert(singleWeaponWorkbenchSource.includes("配置编辑") && singleWeaponWorkbenchSource.includes("配置总览") && singleWeaponWorkbenchSource.includes("single-weapon-editor-layout") && singleWeaponWorkbenchSource.includes("single-weapon-overview-table"), "single-weapon configurations should use list/detail editing plus a separate management overview.");
assert(singleWeaponWorkbenchSource.includes("武器自身变换") && singleWeaponWorkbenchSource.includes("攻击轨迹") && singleWeaponWorkbenchSource.includes("战斗基础参数") && singleWeaponWorkbenchSource.includes("资源策略") && singleWeaponWorkbenchSource.includes("基础 Modifier"), "single-weapon detail should expose weapon-local transform, complete traces, and every base configuration group as structured fields.");
assert(resourcePickerSource.includes("角色武器槽位") && resourcePickerSource.includes("slotPose") && combinedWeaponWorkbenchSource.includes("角色槽位") && !combinedWeaponWorkbenchSource.includes("attachmentId"), "character binding should own semantic slot transforms while combinations contain no attachment authoring.");
assert(combinedWeaponWorkbenchSource.includes("配置编辑") && combinedWeaponWorkbenchSource.includes("配置总览") && combinedWeaponWorkbenchSource.includes("single-weapon-editor-layout") && combinedWeaponWorkbenchSource.includes("combined-weapon-overview-table"), "combined-weapon configurations should use the same list/detail and overview workbench model as single weapons.");
assert(combinedWeaponWorkbenchSource.includes("公共主手 / 副手配置") && combinedWeaponWorkbenchSource.includes("设置公共行为默认值") && combinedWeaponWorkbenchSource.includes("当前角色覆盖") && combinedWeaponWorkbenchSource.includes("Compiler / Runtime 投影"), "combined-weapon detail should separate public weapon and behavior defaults from character overrides and generated state.");
assert(combinedWeaponWorkbenchSource.includes('data-global-quick-action="newRebind"') && appEntrySource.includes('action === "newRebind"'), "combined-weapon behavior selectors should retain global configuration creation after duplicate primary tabs are removed.");
assert(combinedWeaponWorkbenchSource.includes("打开 Animation Editor") && combinedWeaponWorkbenchSource.includes("只读 JSON 审计") && !combinedWeaponWorkbenchSource.includes('data-global-config-type="animation" data-global-quick-field="configurationJson"'), "combined animation should use a readable summary, Animation Editor handoff, and read-only JSON audit.");
assert(combinedWeaponWorkbenchSource.includes("Humanoid 重定向") && combinedWeaponWorkbenchSource.includes("renderAnimationConfigurationOption") && combinedWeaponWorkbenchSource.includes("allowRetargeting"), "animation selectors should explain native versus reusable Humanoid-retargeted configurations.");
assert(combinedWeaponWorkbenchSource.includes("最小运行已就绪") && combinedWeaponWorkbenchSource.includes("可选增强") && combinedWeaponWorkbenchSource.includes("生命周期也会走各自的安全降级"), "reaction summary should distinguish minimum runtime requirements from optional presentation enhancements.");
assert(contextPanelsSource.includes('stageId === "UnityImport" ? "待重新导入" : "待继续"') && contextPanelsSource.includes("源包已更新，待重新导入") && !contextPanelsSource.includes('label: stale ? "验收凭据已过期"'), "deferred Unity import should read as a normal resync state instead of a generic problem.");
assert(appEntrySource.includes('import "./src/contextPanels.js?v=body-part-damage-m2-v2";'), "CharacterStudio should cache-bust the current context-panel integration.");
assert(reactionConfigurationWorkbenchSource.includes("语义矩阵") && reactionConfigurationWorkbenchSource.includes("自定义槽位") && reactionConfigurationWorkbenchSource.includes("ActionRole") && reactionConfigurationWorkbenchSource.includes("openGlobalReactionSlotPicker"), "global reactions should use a semantic matrix, preserve custom slots, expose ActionRole state, and reuse resource selection.");
assert(combinedWeaponWorkbenchSource.includes("combined-recipe-navigator") && combinedWeaponWorkbenchSource.includes("设置公共行为默认值") && combinedWeaponWorkbenchSource.includes("公共默认 CharacterAction / ActionSet") && combinedWeaponWorkbenchSource.includes('action === "jump"'), "combined-weapon editing should guide users through public recipe defaults, character overrides, and Runtime with direct navigation.");
assert(stylesSource.includes("context-tab-combinedWeapons > .tree-panel") && stylesSource.includes("combined-detail-hero") && stylesSource.includes("combined-behavior-picker-grid"), "combined-weapon editing should use a focused full-width layout and clear visual hierarchy.");
assert(reactionConfigurationWorkbenchSource.includes("reaction-slot-groups") && reactionConfigurationWorkbenchSource.includes("身份与语义") && reactionConfigurationWorkbenchSource.includes("Action 与时序") && reactionConfigurationWorkbenchSource.includes("冲击与资源"), "reaction slot editing should group fields by authoring intent rather than expose one flat form.");
assert(!appSource.includes("data-single-weapon-select") && !appSource.includes("baseConfigurationJson"), "single-weapon editing should not retain the current-item dropdown or writable base JSON editor.");
assert(singleWeaponWorkbenchSource.includes("activateSingleWeaponForEditing") && singleWeaponWorkbenchSource.includes("SINGLE_WEAPON_LAST_EDITED_STORAGE_KEY") && !singleWeaponWorkbenchSource.includes('data-single-weapon-action="preview"'), "single-weapon editing should automatically activate its 3D workspace instead of requiring a preview button.");
const preferredSingleWeaponId = singleWeaponWorkbench.selectPreferredSingleWeaponConfigurationId([
  { singleWeaponConfigurationId: "weapon.unarmed", weaponRef: null },
  { singleWeaponConfigurationId: "unbound", weaponRef: null },
  { singleWeaponConfigurationId: "bound", weaponRef: { runtimeResourceKey: "weapon.bound" } }
], "", "missing", "weapon.unarmed");
assert(preferredSingleWeaponId === "bound", "single-weapon automatic selection should prefer the first model-bound editable record over unarmed and unbound records.");
assert(singleWeaponWorkbench.selectPreferredSingleWeaponConfigurationId([
  { singleWeaponConfigurationId: "weapon.unarmed" },
  { singleWeaponConfigurationId: "unbound" }
], "", "missing", "") === "unbound", "single-weapon automatic selection should fall back to an editable unbound draft before unarmed.");
const zeroLengthTraceFrame = viewport3d.computeTraceSpatialFrame({ x: 1, y: 2, z: 3 }, { x: 1, y: 2, z: 3 });
assert(zeroLengthTraceFrame.axis.y === 1 && zeroLengthTraceFrame.perpendicular.x === 1, "zero-length traces should use weapon-local +Y and +X fallback axes.");
assert(Math.abs(viewport3d.projectTraceScalarValue("radius", { x: 0.25, y: 0.5, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }) - 0.25) < 0.000001, "trace radius handles should project onto the stable radial axis.");
assert(Math.abs(viewport3d.projectTraceScalarValue("proximalExtension", { x: 0, y: -0.4, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }) - 0.4) < 0.000001, "trace proximal extension handles should project behind the trace start.");
assert(editorServerSource.includes("/api/weapon-library/reference-index") && editorServerGlobalWeaponLibrarySource.includes("BuildGlobalConfigurationReferenceIndex") && editorServerGlobalWeaponLibrarySource.includes("CharacterCount") && editorServerGlobalWeaponLibrarySource.includes("CombinationCount"), "Authoring should expose a one-scan global configuration reference index with distinct counts.");

const workbenchBaseline = [{ singleWeaponConfigurationId: "sword", displayName: "剑", category: "OneHandMelee", tags: [] }];
const workbenchModified = [{ ...workbenchBaseline[0], displayName: "长剑" }];
assert(singleWeaponWorkbench.getSingleWeaponRecordStatus(workbenchBaseline[0], workbenchBaseline) === "clean" && singleWeaponWorkbench.getSingleWeaponRecordStatus(workbenchModified[0], workbenchBaseline) === "modified", "single-weapon workbench should derive per-record dirty state from the loaded baseline.");
const workbenchDraftSummary = singleWeaponWorkbench.getSingleWeaponDraftSummary([
  workbenchModified[0],
  { singleWeaponConfigurationId: "shield", displayName: "盾" }
], [...workbenchBaseline, { singleWeaponConfigurationId: "removed", displayName: "Removed" }]);
assert(workbenchDraftSummary.added === 1 && workbenchDraftSummary.modified === 1 && workbenchDraftSummary.deleted === 1, "single-weapon workbench should report added, modified, and deleted records separately.");
const combinedWorkbenchBaseline = [{
  combinedWeaponConfigurationId: "sword_empty",
  displayName: "剑 / 空手",
  slots: [
    { slotId: "mainHand", singleWeaponConfigurationId: "sword", attachmentId: "mount.sword.main" },
    { slotId: "offHand", singleWeaponConfigurationId: "weapon.unarmed" }
  ],
  animationConfigurationId: "animation.sword_empty",
  abilityConfigurationId: "ability.sword_empty",
  reactionConfigurationId: "reaction.default",
  actionConfiguration: { actions: [{ actionRole: "primary", actionId: "light_attack" }], commandSequences: [], abilityActionBindings: [], reactionActionBindings: [] }
}];
const combinedWorkbenchModified = [{ ...combinedWorkbenchBaseline[0], displayName: "长剑 / 空手" }];
assert(combinedWeaponWorkbench.getCombinedWeaponRecordStatus(combinedWorkbenchBaseline[0], combinedWorkbenchBaseline) === "clean" && combinedWeaponWorkbench.getCombinedWeaponRecordStatus(combinedWorkbenchModified[0], combinedWorkbenchBaseline) === "modified", "combined-weapon workbench should derive per-record dirty state from the loaded character baseline.");
const combinedDraftSummary = combinedWeaponWorkbench.getCombinedWeaponDraftSummary([
  combinedWorkbenchModified[0],
  { ...combinedWorkbenchBaseline[0], combinedWeaponConfigurationId: "dual_sword" }
], [...combinedWorkbenchBaseline, { ...combinedWorkbenchBaseline[0], combinedWeaponConfigurationId: "removed" }]);
assert(combinedDraftSummary.added === 1 && combinedDraftSummary.modified === 1 && combinedDraftSummary.deleted === 1, "combined-weapon workbench should report added, modified, and deleted recipes separately.");
assert(combinedWeaponWorkbench.getCombinedWeaponCompleteness(combinedWorkbenchBaseline[0]).complete, "a complete public combined recipe should include both slots, animation, reaction, and at least one action reference.");
const previousStudioState = globalThis.state;
globalThis.state = {
  package: {
  animationAuthoring: {
    skeletonProfileId: "skeleton.combat_girl_sword.humanoid",
    avatarProfileId: "avatar.combat_girl_sword.humanoid"
  },
  geometry: { bodyProfile: { bodyKind: "Skeletal" } },
  resourceCatalog: { entries: [] }
  }
};
const codexAnimationConfiguration = globalWeaponLibrary.animationConfigurations.find(item =>
  item.animationConfigurationId === "animation.codex_flow_test.profile.locomotion");
const humanoidCompatibility = combinedWeaponWorkbench.getAnimationConfigurationCompatibility(codexAnimationConfiguration);
assert(humanoidCompatibility.kind === "retarget" && humanoidCompatibility.compatible,
  "Combat Girl should be allowed to select Codex Flow Test animation through Humanoid retargeting.");
globalThis.state.package.animationAuthoring.skeletonProfileId = "skeleton.generic.creature";
globalThis.state.package.animationAuthoring.avatarProfileId = "avatar.generic.creature";
const genericSkeletalCompatibility = combinedWeaponWorkbench.getAnimationConfigurationCompatibility(codexAnimationConfiguration);
assert(genericSkeletalCompatibility.kind === "incompatible" && !genericSkeletalCompatibility.compatible,
  "a Generic skeletal character should not be presented as Humanoid-compatible only because it is skeletal.");
globalThis.state = previousStudioState;
const incompleteCombined = { ...combinedWorkbenchBaseline[0], abilityConfigurationId: "", actionConfiguration: { actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: [] } };
assert(!combinedWeaponWorkbench.getCombinedWeaponCompleteness(incompleteCombined).complete && combinedWeaponWorkbench.getCombinedWeaponReadiness(incompleteCombined, null, []).state === "draft", "incomplete combined recipes should remain editable drafts.");
const frozenCopy = JSON.parse(JSON.stringify({ ...combinedWorkbenchBaseline[0], actionConfiguration: { actions: [{ actionRole: "ability.evade" }, { actionRole: "reaction.death" }], commandSequences: [], abilityActionBindings: [{ actionRole: "ability.evade", abilityId: 1 }], reactionActionBindings: [{ actionRole: "reaction.death" }] } }));
combinedWeaponWorkbench.stripFrozenAbilityDataFromCombinedCopy(frozenCopy);
assert(frozenCopy.abilityConfigurationId === "" && frozenCopy.actionConfiguration.abilityActionBindings.length === 0 && frozenCopy.actionConfiguration.actions.length === 1 && frozenCopy.actionConfiguration.actions[0].actionRole === "reaction.death", "copying a public combined recipe should strip frozen ability references and ability-only default actions while retaining reaction actions.");
const effectiveCombinedOverride = { ...combinedWorkbenchBaseline[0], animationConfigurationId: "animation.character.override" };
assert(combinedWeaponWorkbench.getCombinedWeaponReadiness(effectiveCombinedOverride, { generationStatus: "Generated", diagnostics: [] }, combinedWorkbenchBaseline, combinedWorkbenchBaseline[0]).state === "ready", "a saved character override should use effective completeness without marking the public base as modified.");
const sortedCombined = combinedWeaponWorkbench.filterAndSortCombinedWeaponConfigurations([
  combinedWorkbenchBaseline[0],
  { ...combinedWorkbenchBaseline[0], combinedWeaponConfigurationId: "shield", displayName: "盾" }
], { query: "剑", readiness: "all", defaultState: "all", sort: "id" }, [], combinedWorkbenchBaseline, "sword_empty");
assert(sortedCombined.length === 1 && sortedCombined[0].combinedWeaponConfigurationId === "sword_empty", "combined-weapon workbench search should cover the visible recipe identity.");
const groupedReaction = reactionConfigurationWorkbench.groupReactionSlots([
  { slotId: "death", trigger: "Death" },
  { slotId: "normal.high", trigger: "Damage", pressureBand: "NormalHigh" },
  { slotId: "designer.special", trigger: "Custom" }
]);
assert(groupedReaction.known.get("death").length === 1 && groupedReaction.known.get("normal.high").length === 1 && groupedReaction.custom.length === 1, "reaction semantic grouping should retain unmatched custom slots.");
assert(reactionConfigurationWorkbench.resolveReactionActionRole({ actions: [{ actionId: "action.hit" }], reactionActionBindings: [{ actionRole: "reaction.hit", actionId: "action.hit" }] }, "reaction.hit").state === "resolved", "reaction ActionRole should resolve through the current effective combined action configuration.");
const minimumReactionCoverage = combinedWeaponWorkbench.getReactionBindingCoverage(
  { defaultSlotId: "hit_default", slots: [
    { slotId: "death", actionRole: "reaction.death", required: true },
    { slotId: "hit_default", actionRole: "reaction.hit_default", required: false },
    { slotId: "hit_high", actionRole: "reaction.hit_high", required: false }
  ] },
  { actions: [{ actionId: "death" }, { actionId: "hit" }], reactionActionBindings: [
    { actionRole: "reaction.death", actionId: "death" },
    { actionRole: "reaction.hit_default", actionId: "hit" }
  ] });
assert(minimumReactionCoverage.minimumReady && minimumReactionCoverage.requiredResolved === 2 && minimumReactionCoverage.optionalResolved === 0, "reaction coverage should accept required death plus the default hit while leaving specialized hits optional.");
const strongHighTemplate = reactionConfigurationWorkbench.getReactionSemanticTemplate("strong.high");
assert(strongHighTemplate.pressureBand === "strong.high" && strongHighTemplate.actionRole === "reaction.hit.strong.high", "semantic-matrix creation should prefill the selected impact strength and height instead of creating an unrelated custom slot.");
const presetCreation = reactionConfigurationWorkbench.createReactionPresetSlot([], "normal.mid");
assert(presetCreation.state === "created" && presetCreation.slot.slotId === "hit.normal.mid" && presetCreation.slot.trigger === "Damage" && presetCreation.slot.actionRole === "reaction.hit.normal.mid", "standard reaction matrix entries should create their fixed stable ID and semantic fields without asking for an ID.");
const existingPreset = reactionConfigurationWorkbench.createReactionPresetSlot([presetCreation.slot], "normal.mid");
assert(existingPreset.state === "existing" && existingPreset.slot === presetCreation.slot, "selecting an existing standard preset should focus the existing slot instead of creating a suffixed duplicate.");
const conflictingPreset = reactionConfigurationWorkbench.createReactionPresetSlot([{ ...presetCreation.slot, trigger: "Death" }], "normal.mid");
assert(conflictingPreset.state === "conflict" && conflictingPreset.conflicts.includes("trigger"), "fixed preset ID collisions with incompatible semantics should produce a stable conflict result.");
assert(reactionConfigurationWorkbench.getReactionSlotPresetState({ slotId: "hitHigh", trigger: "Hit" }).kind === "custom", "legacy reaction IDs should not receive compatibility behavior or implicit canonical mapping.");
assert(reactionConfigurationWorkbenchSource.includes("if (semantic) {") && reactionConfigurationWorkbenchSource.includes("createReactionPresetSlot(item.slots, semanticKey)"), "semantic preset creation should bypass the custom ID prompt.");
const mergedWorkbenchReferences = singleWeaponWorkbench.mergeSingleWeaponReferenceIndex(
  { entries: [{ configurationId: "sword", references: [
    { packageRelative: "character.current", combinedWeaponConfigurationId: "old", slotId: "mainHand" },
    { packageRelative: "character.other", combinedWeaponConfigurationId: "other", slotId: "mainHand" }
  ] }] },
  "character.current",
  { combinedWeaponConfigurations: [{ combinedWeaponConfigurationId: "dual", slots: [
    { slotId: "mainHand", singleWeaponConfigurationId: "sword" },
    { slotId: "offHand", singleWeaponConfigurationId: "sword" }
  ] }] },
  ["sword"],
  true,
  true);
assert(mergedWorkbenchReferences.sword.referenceCount === 3 && mergedWorkbenchReferences.sword.characterCount === 2 && mergedWorkbenchReferences.sword.combinationCount === 2 && mergedWorkbenchReferences.sword.references.filter(item => item.draft).length === 2, "single-weapon workbench should replace the active package's saved references with its dual-wield draft.");
const pinnedUnarmed = singleWeaponWorkbench.filterAndSortSingleWeaponConfigurations([
  { singleWeaponConfigurationId: "sword", displayName: "剑", category: "OneHandMelee", weaponRef: {} },
  { singleWeaponConfigurationId: "weapon.unarmed", displayName: "空手", category: "Unarmed", weaponRef: null }
], { query: "missing", resource: "bound" }, {}, []);
assert(pinnedUnarmed.length === 1 && pinnedUnarmed[0].singleWeaponConfigurationId === "weapon.unarmed", "weapon.unarmed should remain pinned when list filters hide ordinary records.");
const unknownIsNotUnreferenced = singleWeaponWorkbench.filterAndSortSingleWeaponConfigurations([
  { singleWeaponConfigurationId: "sword", displayName: "剑", category: "OneHandMelee", weaponRef: {} }
], { reference: "unreferenced" }, { sword: { known: false, referenceCount: 0 } }, []);
assert(unknownIsNotUnreferenced.length === 0, "unknown reference state should never be presented as zero references.");
assert(appSource.includes("data-auto-fit-weapon") && appSource.includes("data-open-resource-manager-resource") && appSource.includes("openResourceManagerForPublicResource"), "CharacterStudio should keep local weapon model helpers but delegate public resource edits to Resource Manager.");
assert(indexSource.includes('value="contactPoint"') && indexSource.includes('data-layer="contactPoints"'), "CharacterStudio should expose body part contact point creation and layer controls.");
assert(indexSource.includes('id="hurtboxDisplayModeSelect"') && indexSource.includes('id="hurtboxPartFilterSelect"'), "CharacterStudio should expose Hurtbox scope and body-part filters.");
assert(appSource.includes("getVisibleHurtboxColliders") && appSource.includes("syncHurtboxDisplayPartToSelection") && appEntrySource.includes("hurtboxDisplayModeSelect"), "CharacterStudio should filter Hurtbox preview by selected body part and keep the filter synchronized with selection.");
assert(appSource.includes('state.hurtboxDisplayMode === "all"') && appEntrySource.includes("六向覆盖热图需要先将受击体显示范围切换为“全部”"), "CharacterStudio should prevent whole-character coverage review from running on a filtered Hurtbox subset.");
assert(appSource.includes("bodyPartContactPoints") && appSource.includes("body_part_contact_points.json"), "CharacterStudio should load and save body part contact point source data.");
assert(appSource.includes("addContactPointMeshes") && appSource.includes("createContactPointConfig") && appSource.includes("commitContactPointTransformTarget"), "CharacterStudio viewport should render and transform body part contact point spheres.");
assert(appSource.includes("CONTACT_ACTIVATION_MODE_OPTIONS") && appSource.includes("radiusMeters"), "CharacterStudio inspector should expose body part contact point fields.");
assert(appSource.includes("PresentationAudio") && appSource.includes("PresentationOnly"), "CharacterStudio should mark contact points as presentation-only audio points.");
assert(appSource.includes("data-apply-model-scale") && appSource.includes("getModelScaleDiagnostic") && appSource.includes("applyRecommendedModelScale"), "CharacterStudio should expose FBX unit scale recommendations for public models.");
assert(appSource.includes("MODEL_WRAPPER_SCALE_PRESETS") && appSource.includes("data-apply-model-scale-preset") && appSource.includes("applyModelWrapperScalePreset"), "CharacterStudio should expose one-click overall model scale presets.");
assert(!appSource.includes("pendingCommonResourceSaves") && !appSource.includes("flushCommonResourceSaves") && !appSource.includes("saveCommonResourceImportHints"), "CharacterStudio should not own public resource import-hint save queues.");
assert(appSource.includes("handleResourceManagerReturnMessage") && appSource.includes("mx-resource-manager-selection") && appSource.includes("openResourceManagerForActivePicker"), "CharacterStudio should delegate public resource lifecycle work to Resource Manager and accept returned field bindings.");
assert(stylesSource.includes(".field-section-modelTransform .field-grid") && stylesSource.includes("repeat(3, minmax(74px, 1fr))"), "CharacterStudio should use a compact dedicated layout for model transform fields.");
assert(!/[\u95c2\u9227\u951f\u5a75\u6fde\u95b8\u7ec2\u6fe0\u599e\u7f02\u67db\u95bb\ufffd]/.test(appSource + stylesSource + indexSource), "CharacterStudio web sources should not contain common mojibake strings.");
assert(!/\?\/(?:button|option|div|span)/.test(appSource + indexSource), "CharacterStudio web templates should not contain corrupted closing tags.");
assert(editorServerCommonResourcesSource.includes("/api/authoring/common-resources/save") && editorServerCommonResourcesSource.includes("SaveCommonResourceManifestImportHints") && editorServerCommonResourcesSource.includes("importHints"), "Authoring server should save public resource wrapper import hints.");
assert(editorServerCommonResourcesSource.includes("UnitScaleFactor") && editorServerCommonResourcesSource.includes("modelScaleDiagnostic") && editorServerCommonResourcesSource.includes("UnityConvertsUnits"), "Authoring server should detect FBX unit scale diagnostics for common resources.");
assert(appSource.includes("/api/authoring/resources?package="), "CharacterStudio should read the Authoring Resource Manager API");
assert(appSource.includes("/api/authoring/resources/resource-plan"), "CharacterStudio should read the Authoring resource plan API");
assert(appSource.includes("/api/authoring/resources/pick"), "CharacterStudio should query server-side picker candidates");
assert(appSource.includes("/api/authoring/resources/resolve-selection"), "CharacterStudio should resolve resource selections through the Authoring API");
assert(!appSource.includes("/api/character/resources?package="), "CharacterStudio should not read the legacy character resource library API");
assert(!appSource.includes("/api/character/resource-plan?package="), "CharacterStudio should not read the legacy character resource plan API");
assert(!appSource.includes("/api/character/import-model") && !appSource.includes("function importModel("), "CharacterStudio should not expose the legacy single-file model import flow");
assert(!indexSource.includes("modelImportRole") && !indexSource.includes("modelFileInput") && !indexSource.includes("modelImportButton"), "CharacterStudio should not render legacy single-file model import controls");
assert(indexSource.includes("resourceBindingRole") && appSource.includes("RESOURCE_BINDING_ROLES") && appSource.includes("getResourceBindingRole"), "CharacterStudio should use a resource binding target selector instead of model import roles");
assert(!appSource.includes("MODEL_IMPORT_ROLES") && !appSource.includes("getModelImportRole") && !stylesSource.includes("legacy-action"), "CharacterStudio should not keep legacy model import role code or styles");
assert(appSource.includes("resourcePickerOpen") && appSource.includes("openResourcePicker"), "CharacterStudio should open resources through a field-scoped picker");
assert(appSource.includes("openResourcePickerForField") && appSource.includes('data-picker-action=\"openResourcePicker\"'), "CharacterStudio inspector resource fields should open picker on demand");
assert(appSource.includes("createResourceSelectionRef") && appSource.includes("resourceStableId") && appSource.includes("runtimeResourceKey"), "CharacterStudio should create the new ResourceSelectionRef shape");
assert(appSource.includes("sourceProviderId") && appSource.includes("providerResourceKey") && appSource.includes("packageResourceKey"), "CharacterStudio selection should keep provider-local identity separate from runtime keys");
assert(appSource.includes('slotId: "mainHand"') && appSource.includes('slotId: "offHand"'), "CharacterStudio resource field specs should use server-compatible slotId compatibility filters");
assert(!appSource.includes("compatibilityFilter: { equipSlot"), "CharacterStudio should not use obsolete equipSlot compatibility filters");
assert(appSource.includes("Animation.Clip") && appSource.includes("animationClip") && appSource.includes("AnimationWarmup"), "CharacterStudio should define an animation clip ResourceFieldSpec for future animation fields");
assert(/animationClip:\s*{[\s\S]*?fieldKey:\s*"Animation\.Clip"[\s\S]*?acceptedUsages:\s*\["animationClip", "animationClipGroup"\]/.test(appSource), "CharacterStudio source clip fields should still accept direct clips and clip groups");
assert(/animationProfileSlot:\s*{[\s\S]*?fieldKey:\s*"Animation\.ProfileSlot"[\s\S]*?acceptedUsages:\s*\["animationClipGroup"\][\s\S]*?preloadPolicy:\s*"AnimationWarmup"/.test(appSource), "CharacterStudio profile slots should only accept animation clip groups");
assert(appSource.includes("...RESOURCE_FIELD_SPECS.animationProfileSlot") && appSource.includes("RESOURCE_FIELD_SPECS.animationProfileSlot.compatibilityFilter"), "CharacterStudio profile slot picker should use the animationProfileSlot ResourceFieldSpec");
assert(appSource.includes("unityProjectAssets") && appSource.includes("UnityEditorOnlyAsset") && appSource.includes("PackageResource"), "CharacterStudio animation picker should accept Unity project and package animation assets");
assert(indexSource.includes("animationConfigPanel") && appSource.includes("renderAnimationConfigPanel") && appSource.includes("openAnimationSlotPicker"), "CharacterStudio should expose an animation profile picker panel");
assert(appSource.includes("Tools/MxFramework.AnimationEditor/web/") && appSource.includes("打开 AnimationEditor"), "CharacterStudio should link to the standalone Animation Editor");
assert(appSource.includes("animationProfiles") && appSource.includes("resourceSelection") && appSource.includes("applyResourceSelectionToAnimationSlot"), "CharacterStudio should persist animation slot ResourceSelectionRef data");
assert(indexSource.includes('data-validation-tab="reaction"') && indexSource.includes('id="reactionProfilePanel"') && indexSource.includes("受击结果（只读）"), "CharacterStudio should label the reaction result tab as read-only");
assert(appSource.includes("usesCompilerOwnedReactionProjection") && appSource.includes("renderCompilerOwnedReactionProjectionPanel") && appSource.includes("只读视图 · 不可编辑") && appSource.includes("不提供编辑、保存或重新生成操作"), "Schema 8 reaction UI should explicitly identify the compiler projection as non-editable");
assert(appSource.includes("REACTION_PROFILE_SLOTS") && appSource.includes('slotId: "cohesionBreak"') && appSource.includes('slotId: "hit.normal.high"') && appSource.includes('slotId: "hit.strong.mid"'), "CharacterStudio should define the fixed reaction semantic slot IDs");
assert(stateModuleSource.includes("DEFAULT_DEATH_REACTION_ANIMATION_RESOURCE_KEY") && stateModuleSource.includes("art.character.skeleton.animation.damage_ko_mid_2") && stateModuleSource.includes("defaultResourceKey: DEFAULT_DEATH_REACTION_ANIMATION_RESOURCE_KEY"), "CharacterStudio death reaction slot should default to the KO death animation resource.");
assert(stateModuleSource.includes("DEFAULT_REACTION_PROFILE_ID,") && stateModuleSource.includes("REACTION_PROFILE_SLOTS,") && !stateModuleSource.includes("LEGACY_REACTION_PROFILE_SLOT_IDS"), "CharacterStudio should export fixed reaction presets without a legacy ID compatibility map.");
assert(!combinedWeaponWorkbenchSource.includes('renderCombinedBehaviorSelector("ability"') && !combinedWeaponWorkbenchSource.includes('renderCombinedGlobalQuickEditor("ability"'), "combined-weapon editing should not expose frozen ability selection or quick editing.");
assert(!appSource.includes('node("workflow/abilities"') && !contextPanelsSource.includes('return "去能力绑定"'), "normal CharacterStudio workflow should not expose frozen ability authoring or route ActionTimeline to ability binding.");
assert(appSource.includes("reaction.height.high") && appSource.includes("minImpactForce: 60") && appSource.includes("maxImpactForce: 59"), "CharacterStudio reaction slots should carry height and impact split rules");
assert(appSource.includes("/api/character-action/reaction-profile/save") && editorServerReactionProfileSource.includes("CHARACTER_ACTION_REACTION_PROFILE_COMPILER_OWNED"), "legacy reaction saves should remain available while Schema 8 direct projection writes are rejected");
assert(/reactionAnimationSlot:\s*{[\s\S]*?fieldKey:\s*"Reaction\.AnimationSlot"[\s\S]*?acceptedUsages:\s*\["animationClip"\]/.test(appSource), "CharacterStudio reaction slots should only accept concrete animation clips");
assert(appSource.includes("openReactionSlotPicker") && appSource.includes("applyResourceSelectionToReactionSlot") && appSource.includes("clearReactionSlotSelection"), "CharacterStudio should persist reaction slot ResourceSelectionRef data");
assert(appSource.includes("getGeneratedReactionProfileStableId") && appSource.includes("getGeneratedReactionActionId") && appSource.includes("renderReactionProfilePanel"), "CharacterStudio should render generated reaction profile/action ids");
assert(stateModuleSource.includes("selectedReactionSlotId"), "CharacterStudio reaction panel should keep an explicit selected reaction slot for the detail view.");
assert(appSource.includes("reaction-workbench") && appSource.includes("reaction-matrix") && appSource.includes("reaction-slot-tile") && appSource.includes("reaction-detail"), "CharacterStudio reaction panel should render a matrix plus detail layout.");
assert(indexSource.includes("reaction-config-grid reaction-profile-panel") && !indexSource.includes("animation-config-grid reaction-profile-panel"), "CharacterStudio reaction panel should not inherit the animation slot panel height clamp.");
assert(appSource.includes("CONTEXT_EDITOR_TABS") && appSource.includes("context-editor-expanded") && stylesSource.includes(".workspace.context-expanded.context-editor-expanded .context-dock"), "CharacterStudio editor tabs should share a dedicated full-height context layout.");
assert(stylesSource.includes(".workspace.context-expanded.context-editor-expanded .viewport") && stylesSource.includes(".workspace.context-expanded.context-editor-expanded .context-panel.active") && !stylesSource.includes("context-tab-reactions"), "CharacterStudio editor tabs should not use a reaction-only context layout.");
assert(stylesSource.includes(".workspace.context-expanded.context-editor-expanded .import-materials-panel") && stylesSource.includes(".workspace.context-expanded.context-editor-expanded .readiness-flow"), "CharacterStudio editor tab content roots should share the full-height layout.");
assert(!stylesSource.includes("max-height: 230px") && !stylesSource.includes("max-height: 260px") && !stylesSource.includes("46vh, 410px"), "CharacterStudio context tab content containers should not keep per-tab height clamps.");
const saveReactionProfileConfigSource = appSource.slice(
  appSource.indexOf("async function saveReactionProfileConfig"),
  appSource.indexOf("async function readStaticPackage")
);
assert(
  appSource.includes("当前武器 ActionSet 绑定的受击动作规则")
    && appSource.includes("绑定目标")
    && appSource.includes("当前武器")
    && appSource.includes("当前配置未声明武器/装备态绑定")
    && appSource.includes("造成破聚时")
    && appSource.includes("本次命中触发破聚")
    && appSource.includes("破聚后命中")
    && appSource.includes("预留规则")
    && appSource.includes("Trigger")
    && appSource.includes("未选动画")
    && appSource.includes("已选动画")
    && appSource.includes("未保存")
    && appSource.includes("已保存待 Unity 导入")
    && appSource.includes("已生成")
    && appSource.includes("普通 · 上段")
    && appSource.includes("强冲击"),
  "CharacterStudio reaction panel should expose hit-reaction rule authoring labels.");
const obsoleteCohesionBreakReactionLabel = "破聚" + "反应";
assert(!appSource.includes(obsoleteCohesionBreakReactionLabel), "CharacterStudio reaction panel should not present cohesion break as a separate reaction concept.");
assert(appSource.includes("reaction-binding-status") && stylesSource.includes(".reaction-binding-status"), "CharacterStudio reaction panel should show an ActionSet binding target strip.");
assert(appSource.includes("getCurrentReactionActionSetContext") && appSource.includes("buildReactionActionSetCandidates") && appSource.includes("containsActionSetToken"), "CharacterStudio reaction panel should resolve the current weapon ActionSet context.");
assert(saveReactionProfileConfigSource.includes("getCurrentReactionActionSetContext") && saveReactionProfileConfigSource.includes("loadResourceLibraryAndPlan") && !saveReactionProfileConfigSource.includes("actionSets[0]"), "CharacterStudio reaction profile save should use the current ActionSet context and refresh characterAction resources.");
assert(appSource.includes("getReactionSlotTreeLabel") && appSource.includes("已选") && appSource.includes("未选择"), "CharacterStudio package tree should show Chinese reaction slot summaries.");
assert(stylesSource.includes(".reaction-workbench") && stylesSource.includes("grid-template-columns: minmax(0, 1fr) minmax(300px, 360px)") && stylesSource.includes(".reaction-detail-actions"), "CharacterStudio reaction panel should have dedicated matrix/detail styles.");
assert(appSource.includes("getCharacterActionPackageSourceFolder") && appSource.includes("Profiles/${packageSegment}") && appSource.includes("ActionSets/${packageSegment}"), "CharacterStudio CharacterAction handoff paths should be package-scoped");
assert(appSource.includes('characterAction: "CharacterAction 配置"'), "CharacterStudio should label characterAction authoring resources in Resource Manager surfaces");
assert(appSource.includes("animationGroups") && appSource.includes("renderAnimationGroupCard") && appSource.includes("AnimationEditor 分组"), "CharacterStudio should show animation groups as read-only AnimationEditor references");
assert(appSource.includes("renderAnimationProfileReferenceContext") && appSource.includes("Animation Profile 引用"), "CharacterStudio should show animation profiles as read-only references");
assert(appSource.includes("animationGroupId") && appSource.includes("renderAnimationEditorReferenceRow") && appSource.includes("打开 AnimationEditor"), "CharacterStudio should keep profile group references and hand off source editing to AnimationEditor");
assert(!appSource.includes("data-animation-add-group") && !appSource.includes("data-animation-add-clip") && !appSource.includes("data-animation-add-blend") && !appSource.includes("data-animation-remove-group"), "CharacterStudio should not expose animation group/clip/blend authoring actions");
assert(!appSource.includes("function addAnimationGroup(") && !appSource.includes("function createManualAnimationGroup(") && !appSource.includes("function removeAnimationGroup("), "CharacterStudio should not keep AnimationGroup source mutation functions");
assert(!appSource.includes("function addAnimationGroupClip(") && !appSource.includes("function removeAnimationGroupClip(") && !appSource.includes("function addAnimationGroupBlendSpace("), "CharacterStudio should not keep Clip/Blend source mutation functions");
assert(appSource.includes('if (["animationProfile", "animationGroup", "animationClip", "animationBlendSpace", "animationBlendPoint"].includes(kind)) return [];'), "CharacterStudio should keep animation source metadata read-only in the inspector");
assert(appSource.includes("mergeResourcePickerRows") && appSource.includes("getResourcePickerDedupeKey"), "CharacterStudio resource picker should merge duplicate provider projections");
assert(appSource.includes("CharacterStudio 只绑定已有引用。") && appSource.includes("没有找到适配当前字段的资源"), "CharacterStudio resource picker should explain that incompatible candidates are hidden");
assert(appSource.includes("delete attachment.previewResourceSelection") && appSource.includes("delete attachment.previewResourceKey"), "CharacterStudio should clear legacy weapon preview fields when writing weaponRef.");
assert(!appSource.includes('node("resources", "resources"'), "CharacterStudio should not expose the full resource browser in the package tree");
assert(editorServerSource.includes("/api/authoring/resources/pick"), "Authoring server should expose picker query API");
assert(appSource.includes("collectResourceReferences") && appSource.includes("weaponRef") && appSource.includes("dependencies") && appSource.includes("preview.thumbnailResourceKey"), "CharacterStudio reference collection should cover character, weaponRef, dependency, and preview references");
assert(indexSource.includes("resourcePickerOverlay"), "CharacterStudio should render the resource picker as an on-demand dialog");
assert(!indexSource.includes("modelResourceList"), "CharacterStudio should not keep a full resource library list in the main viewport");
assert(appSource.includes("SpawnCritical") && appSource.includes("EquipmentInitial") && appSource.includes("AnimationWarmup"), "CharacterStudio should render resource plan groups");
assert(stylesSource.includes("resource-plan-grid"), "CharacterStudio should style the resource plan preview");
assert(indexSource.includes('data-context-tab="materials"') && indexSource.includes("importMaterialsPanel") && indexSource.includes("character_sources.json 源清单编辑器"), "CharacterStudio should expose a character source manifest editor context tab");
assert(appSource.includes("/api/character/import-materials?package=") && appSource.includes("/api/character/import-materials/save") && appSource.includes("/api/character/import-materials/sync"), "CharacterStudio should read, save, and sync the character source manifest through the import-materials compatibility APIs");
assert(appSource.includes("/api/character/import-materials/source-candidates"), "CharacterStudio should query project-scoped import material source candidates");
assert(appSource.includes("unityImportTask: null") && appSource.includes("startUnityImportTask") && appSource.includes("finishUnityImportTask"), "CharacterStudio should track visible Unity import task state");
assert(appSource.includes("renderUnityImportTaskPanel") && appSource.includes("buildUnityImportTaskSteps") && appSource.includes("Unity 导入任务"), "CharacterStudio should render a visible Unity import progress panel");
assert(appSource.includes("buildUnityImportAcceptance") && appSource.includes("缺少 Unity Editor 验收") && appSource.includes("等待 Unity Editor"), "Unity import task should render a final acceptance verdict from Unity Editor bridge status");
assert(appSource.includes("生成 Unity 导入产物") && appSource.includes("导入 Unity AssetDatabase") && appSource.includes("校验 Humanoid Avatar Source") && appSource.includes("在 Unity Editor 中重建角色 Prefab") && appSource.includes("烘焙武器命中轨迹") && appSource.includes("刷新 CharacterAction Profile / ActionSet"), "Unity import task steps should call out artifact, AssetDatabase, Avatar source, Prefab, Weapon trace bake, and CharacterAction synchronization");
assert(appSource.includes("classifyUnityImportOperation") && appSource.includes("summarizeUnityImportOperations") && appSource.includes("renderUnityImportUpToDateDetails"), "Unity import report rendering should classify changed, up-to-date, and problem operations");
assert(appSource.includes("Unity 导出已是最新") && appSource.includes("没有变更或问题操作"), "Skipped-only Unity import reports should render as up-to-date instead of noisy warnings");
assert(appSource.includes("getUnityEditorCharacterActionStepStatus") && appSource.includes("characterActionProfileReady") && appSource.includes("characterActionActionSetReady") && appSource.includes('return "error";'), "CharacterAction post-import step should use Unity Editor bridge profile/action-set status and fail when profile/action set is missing after completion");
assert(appSource.includes("getUnityEditorAvatarSourceStepStatus") && appSource.includes("avatarSourceHumanoidStatus") && appSource.includes("avatarSourceAssetPath") && appSource.includes("Unity Editor bridge 没有报告 Humanoid Avatar Source 状态"), "CharacterStudio Unity import should surface Humanoid Avatar source status separately from GLB importer status");
assert(appSource.includes("getUnityEditorWeaponTraceBakeStepStatus") && appSource.includes("weaponTraceBakeStatus") && appSource.includes("weaponTraceBakeIssueCount") && appSource.includes("Unity Editor bridge 没有报告武器轨迹烘焙状态"), "CharacterStudio Unity import should surface Weapon trace bake as an explicit Editor bridge acceptance stage.");
const partialEditorFailure = {
  status: "failed",
  artifactHash: "sha256:artifact",
  prefabPath: "Assets/MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview.prefab",
  avatarSourceStatus: "Valid",
  avatarSourceHumanoidStatus: "ValidHuman",
  avatarSourceAssetPath: "Assets/MxFrameworkGenerated/CharacterPackages/000001/resources/models/000001_lod0.fbx",
  weaponTraceBakeStatus: "Ready",
  weaponTraceBakeIssueCount: 0,
  socketBakeStatus: "Ready",
  socketBakeIssueCount: 0,
  hurtboxBakeStatus: "Failed",
  hurtboxBakeIssueCount: 2,
  characterActionProfileReady: true,
  characterActionActionSetReady: true
};
assert(globalThis.getUnityEditorAssetDatabaseStepStatus(partialEditorFailure, true) === "ok", "A later bake failure must not repaint successful AssetDatabase import as failed.");
assert(globalThis.getUnityEditorAvatarSourceStepStatus(partialEditorFailure, true) === "ok", "A later bake failure must not repaint a ValidHuman Avatar as failed.");
assert(globalThis.getUnityEditorRebuildStepStatus(partialEditorFailure, true) === "ok", "A produced Prefab must remain visible when spawn acceptance later fails.");
assert(globalThis.getUnityEditorWeaponTraceBakeStepStatus(partialEditorFailure, true) === "ok", "A later hurtbox failure must not repaint a Ready weapon trace bake as failed.");
assert(globalThis.getUnityEditorSocketBakeStepStatus(partialEditorFailure, true) === "ok", "A later hurtbox failure must not repaint a Ready socket bake as failed.");
assert(globalThis.getUnityEditorHurtboxBakeStepStatus(partialEditorFailure, true) === "error", "The actual failed hurtbox stage must remain failed.");
assert(globalThis.getUnityEditorCharacterActionStepStatus(partialEditorFailure, true) === "ok", "A later hurtbox failure must not repaint ready CharacterAction outputs as failed.");
assert(appSource.includes("getUnityEditorDefaultWeaponValidation") && appSource.includes("defaultWeaponResourceIds") && appSource.includes("getExpectedDefaultWeaponResourceKeys") && appSource.includes("weaponAttachmentCount"), "Unity import acceptance should validate default weapons from weaponRef-derived attachment resources instead of legacy preview keys");
assert(appSource.includes("getUnityArtifactStepDetail") && appSource.includes("artifactTransformCount") && appSource.includes("artifactHash"), "Unity import task should surface artifact hash/transform metadata separately from source writes");
assert(!appSource.includes('prefabEvidence || prefabReadiness ? "ok" : (commandSucceeded ? "warn" : "error")'), "Prefab step should not warn just because import report lacks a prefab operation");
assert(appSource.includes("readJsonResponseOrText") && appSource.includes("命令输出"), "Unity import failures should preserve command output for diagnosis");
assert(stylesSource.includes("#importButton.is-running") && stylesSource.includes(".unity-import-task") && stylesSource.includes(".unity-import-step") && stylesSource.includes(".unity-import-acceptance"), "CharacterStudio should style the Unity import running button, acceptance verdict, and task checklist");
assert(stylesSource.includes(".unity-import-up-to-date") && stylesSource.includes(".unity-import-operation-empty"), "CharacterStudio should collapse skipped Unity import operations under an up-to-date details block");
assert(characterActionPreviewRuntimeSessionSource.includes("BuildPreviewBindingKey(binding)") && characterActionPreviewRuntimeSessionSource.includes("GeometryBindingHash") && characterActionPreviewRuntimeSessionSource.includes("ResourceMappingHash") && characterActionPreviewRuntimeSessionSource.includes("WritePlanHash"), "CharacterAction preview should invalidate cached preview instances when imported package hashes change");
assert(characterActionPreviewRuntimeSessionSource.includes("ComposePreviewWeaponPose")
  && characterActionPreviewRuntimeSessionSource.includes("attachment.SlotPose")
  && characterActionPreviewRuntimeSessionSource.includes("attachment.WeaponModelPose")
  && characterActionPreviewRuntimeSessionSource.includes("slotPosition + slotRotation * Vector3.Scale(slotScale, weaponPosition)"),
"CharacterAction preview should mount weapons with the same slot-pose then weapon-model-pose composition as runtime spawning.");
assert(editorServerCharacterUnityImportEditorSource.includes("/api/character/import-unity/editor-status") && editorServerCharacterUnityImportEditorSource.includes("rebuild_request.json"), "Authoring server should expose Unity Editor prefab rebuild request status");
assert(editorServerSource.includes("BuildAuthoringCliProcessStartInfo") && editorServerSource.includes("Environment.ProcessPath") && editorServerSource.includes("typeof(EditorServer).Assembly.Location"), "CharacterStudio import should reuse the running Authoring CLI binary instead of rebuilding the locked server executable");
assert(editorServerSource.includes('startInfo.ArgumentList.Add("--no-build")'), "Fallback CharacterStudio import command should never rebuild the running authoring executable");
assert(characterPackageImportRequestBridgeSource.includes("[InitializeOnLoad]") && characterPackageImportRequestBridgeSource.includes("CharacterPackageImportCommand.SynchronizeImportedAssetsWithReport") && characterPackageImportRequestBridgeSource.includes("MountedWeaponCount") && characterPackageImportRequestBridgeSource.includes("BodyMaterialBindingStatus"), "Unity Editor should consume CharacterStudio import rebuild requests and write structured prefab/profile status");
assert(characterPackageImportCommandSource.includes("internal static CharacterImportedPackagePrefabBuildResult SynchronizeImportedAssetsWithReport") && characterPackageImportCommandSource.includes("CharacterImportedPackagePrefabBuilder.SynchronizeRuntimeEntryPointsWithReport"), "Character package import command should expose the reported Editor prefab sync path to the bridge");
assert(characterImportedPackagePrefabBuilderSource.includes("CharacterImportedPackagePrefabBuildResult") && characterImportedPackagePrefabBuilderSource.includes("GeometryBindingHash") && characterImportedPackagePrefabBuilderSource.includes("DefaultWeaponResourceIds") && characterImportedPackagePrefabBuilderSource.includes("AvatarSourceHumanoidStatus") && characterImportedPackagePrefabBuilderSource.includes("BodyMaterialBindingStatus"), "Prefab builder should report hashes, Avatar source, material binding, and default weapon bindings after rebuild");
assert(editorServerCharacterUnityImportEditorSource.includes("geometryBindingHash") && editorServerCharacterUnityImportEditorSource.includes("avatarSourceHumanoidStatus") && editorServerCharacterUnityImportEditorSource.includes("mountedWeaponCount") && editorServerCharacterUnityImportEditorSource.includes("bodyMaterialBindingStatus") && editorServerCharacterUnityImportEditorSource.includes("characterActionProfileReady"), "Authoring server should forward structured Unity Editor avatar/prefab/profile/material rebuild status");
assert(characterUnityImportBridgeSource.includes("ArtifactHash") && characterUnityImportBridgeSource.includes("ArtifactTransforms"), "Unity import bridge report should distinguish source and artifact hashes");
assert(characterUnityImportGlbCompatibilitySource.includes("stripSkinnedColor0") && characterUnityImportGlbCompatibilitySource.includes("BuildGlbInvariantSignature"), "GLB Unity compatibility artifacts should strip skinned COLOR_0 while validating scene/node/skin/animation invariants");
assert(appSource.includes("pollUnityImportEditorRebuild") && appSource.includes("/api/character/import-unity/editor-status") && appSource.includes("editorRebuildStatus"), "CharacterStudio Unity import should poll Unity Editor prefab rebuild status");
assert(indexSource.includes('id="unityBuildTargetSelect"') && appSource.includes("state.unityImportBuildTarget") && appSource.includes("buildTarget: state.unityImportBuildTarget"), "CharacterStudio Unity import should require and send an explicit target platform");
assert(sessionStateSource.includes('mx.characterStudio.preferences.v1')
  && sessionStateSource.includes("packageRelative: String(state.packageRelative")
  && sessionStateSource.includes("unityImportBuildTarget: String(state.unityImportBuildTarget")
  && appEntrySource.includes("restoreCharacterStudioPreferences?.()")
  && appEntrySource.includes("persistCharacterStudioPreferences?.()")
  && sessionStateSource.includes('url.searchParams.set("package"')
  && sessionStateSource.includes('url.searchParams.set("buildTarget"')
  && sessionStateSource.includes("window.history.replaceState"),
"CharacterStudio should remember the selected package and Unity import platform across refreshes while allowing URL parameters to override local preferences.");
assert(editorServerSource.includes('startInfo.ArgumentList.Add("--build-target")') && editorServerSource.includes("request.buildTarget"), "Authoring server should forward the explicit CharacterStudio Unity import target platform");
assert(appSource.includes("getUnityEditorBodyMaterialBindingDetail") && appSource.includes("bodyMaterialBindingStatus") && appSource.includes("bodyMaterialRendererCount"), "CharacterStudio Unity import Prefab step should surface body material binding status.");
assert(appSource.includes("const attempts = options.attempts || 90") && appSource.includes('status: "timeout"') && appSource.includes('value === "timeout"'), "CharacterStudio Unity import should wait long enough for Editor rebuilds and avoid leaving timed-out prefab rebuilds stuck as Running");
assert(appSource.includes("IMPORT_MATERIAL_TEMPLATES") && appSource.includes("appendPastedImportMaterialPaths") && appSource.includes("syncImportMaterials"), "CharacterStudio source manifest editor should support template, pasted-path, and sync workflows");
assert(appSource.includes("IMPORT_MATERIAL_SLOT_DEFINITIONS") && appSource.includes('key: "body"') && appSource.includes('key: "bodyTexture"') && appSource.includes('id: "body-albedo"') && !appSource.includes('key: "mainHand"') && !appSource.includes('key: "offHand"') && !appSource.includes('key: "locomotion"'), "CharacterStudio source manifest editor should expose source-file slot cards for body and body texture only");
assert(appSource.includes("renderDefaultWeaponReferenceCards") && appSource.includes("组合武器资源归属") && appSource.includes("openSingleWeaponConfiguration"), "CharacterStudio should route combination weapon resources to the global single-weapon editor instead of character source-file slots");
assert(appSource.includes('picker?.kind === "singleWeapon"') && appSource.includes("single.weaponRef = clone(selectionRef)") && appSource.includes("state.weaponLibraryDirty = true"), "CharacterStudio resource picker should write weaponRef only through a global single-weapon configuration.");
assert(appSource.includes("applyWeaponAttachmentSelection") && appSource.includes("getSingleWeaponConfiguration"), "legacy attachment selection helpers should delegate ownership to the referenced global single-weapon configuration.");
assert(!/picker\.fieldPath === "weaponRef" \|\| picker\.fieldPath === "previewResourceKey"/.test(appSource), "CharacterStudio resource picker should not treat legacy previewResourceKey as an editable weapon authority.");
assert(/function clearDefaultWeaponReference[\s\S]*clearWeaponAttachmentSelection/.test(appSource), "CharacterStudio explicit clear should remove weaponRef and legacy preview fallbacks.");
assert(characterResourcePlanCompilerSource.includes("SingleWeaponConfiguration") && characterResourcePlanCompilerSource.includes("return definition.WeaponRef.PackageResourceKey"), "Authoring resource plan should read weaponRef from referenced global single-weapon configurations only.");
assert(authoringResourceReferenceGraphSource.includes("GetWeaponDefinitionReferenceKey") && authoringResourceReferenceGraphSource.includes("SingleWeaponConfiguration"), "Authoring reference graph should use global single-weapon configurations as weapon resource authority.");
assert(appSource.includes("bodyPublicModel") && appSource.includes("bodyPublicTexture") && appSource.includes("openImportMaterialResourceRefPicker") && appSource.includes("choosePublicSourceResource"), "CharacterStudio source cards should support public character model and texture resourceRef selection.");
assert(appSource.includes("function getActiveResourcePickerRequest") && appSource.includes("getActiveResourcePickerRequest,"), "CharacterStudio resource picker modules should expose the active picker request after app.js split.");
assert(/openImportMaterialResourceRefPicker[\s\S]*await loadCommonResources\(\)/.test(appSource), "CharacterStudio public model picker should refresh public reusable resources before opening.");
assert(/bodyPublicModel:\s*{[\s\S]*?acceptedBindingKinds:\s*\["ResourceManagerAsset", "UnityEditorOnlyAsset"\][\s\S]*?allowIncompatibleWithWarning:\s*true/.test(appSource), "CharacterStudio should allow saving public body resourceRef selections before Unity import, with warnings.");
assert(/bodyPublicTexture:\s*{[\s\S]*?acceptedKinds:\s*\["Texture"\][\s\S]*?acceptedUsages:\s*\["texture"\][\s\S]*?allowIncompatibleWithWarning:\s*true/.test(appSource), "CharacterStudio should allow selecting public texture resourceRefs from source manifest cards.");
assert(appSource.includes("getCommonResourcePickerRows") && appSource.includes("normalizeCommonResourcePickerItem") && appSource.includes("公共资源 API 不可用"), "CharacterStudio public model picker should list ArtSource/Common manifest resources and explain unavailable common-resource APIs.");
assert(appSource.includes("normalizeResourcePickerStableSegment") && appSource.includes("buildCommonResourceStableId") && appSource.includes("inferCommonResourceKindFromUsage") && appSource.includes("global.profile."), "CharacterStudio public resource picker should build kind-aware global profile stable ids locally.");
assert(appSource.includes("shouldSkipResourcePickerResolve") && appSource.includes('request?.kind === "importMaterialResourceRef"') && appSource.includes("allowIncompatibleWithWarning") && appSource.includes('row?.selection?.tone !== "blocked"') && appSource.includes("raw?.manifestPath"), "CharacterStudio public manifest selections should not be blocked by runtime-ready resolver checks when the local field spec allows warning selections.");
assert(appSource.includes("findPreviewModelResourceByKey") && appSource.includes("buildCommonPreviewModelResource") && appSource.includes("buildPreviewModelUrl") && appSource.includes("commonResources/"), "CharacterStudio viewport should resolve public common model resources for preview instead of treating them as missing package catalog entries.");
assert(appSource.includes("function getCommonResourceUnitySync") && appSource.includes("resourceMapping") && appSource.includes("RuntimeReady") && appSource.includes("usesUnityWrapperPrefab"), "CharacterStudio weapon data should resolve public weapon Unity status through common resource wrapper prefab metadata instead of package-local Unity catalog entries.");
assert(appSource.includes("weaponMaterialBindingStatus") && appSource.includes("weaponMaterialTemplateId") && appSource.includes("weaponMaterialRendererCount"), "CharacterStudio should surface public weapon wrapper material path, template, texture source, and renderer count diagnostics.");
assert(indexSource.includes("resourcePickerDebug") && appSource.includes("renderResourcePickerDebug") && stylesSource.includes(".resource-picker-debug"), "CharacterStudio public model picker should render visible diagnostics instead of silently showing an empty modal.");
assert(/mainHand:\s*{[\s\S]*?acceptedProviderIds:\s*\["runtimeCatalog", "resourceMapping"\][\s\S]*?acceptedBindingKinds:\s*\["ResourceManagerAsset", "UnityEditorOnlyAsset"\][\s\S]*?allowIncompatibleWithWarning:\s*true/.test(appSource), "CharacterStudio main-hand picker should select registered public/runtime weapon resources, including not-yet-imported public entries with warnings.");
assert(/offHand:\s*{[\s\S]*?acceptedProviderIds:\s*\["runtimeCatalog", "resourceMapping"\][\s\S]*?acceptedBindingKinds:\s*\["ResourceManagerAsset", "UnityEditorOnlyAsset"\][\s\S]*?allowIncompatibleWithWarning:\s*true/.test(appSource), "CharacterStudio off-hand picker should select registered public/runtime weapon resources, including not-yet-imported public entries with warnings.");
assert(appSource.includes("renderImportMaterialSlotCards") && appSource.includes("import-material-slot-card") && appSource.includes("renderImportMaterialsAdvancedList") && appSource.includes("renderImportMaterialsBatchTools") && appSource.includes("renderImportMaterialsManifestDetails"), "CharacterStudio source manifest editor should use slot cards, batch validation, manifest details, and an advanced manifest fallback");
assert(appSource.includes("sourceManifestExists") && appSource.includes("legacyDocumentExists") && appSource.includes("renderImportMaterialsManifestState") && appSource.includes("先填写下方卡片"), "CharacterStudio source manifest editor should distinguish missing, empty, legacy, and populated source manifests while keeping edit slots visible");
assert(!appSource.includes("shouldRenderCommittedSourceManifestItems"), "CharacterStudio source manifest editor should not reference the removed committed-only slot rendering guard");
assert(buildImportMaterialSlotStatusesSource.includes("IMPORT_MATERIAL_SLOT_DEFINITIONS.map") && buildImportMaterialSlotStatusesSource.includes("createImportMaterialSlotItem") && appSource.includes("可选空缺") && appSource.includes("必填缺失"), "CharacterStudio source manifest slot cards should always render fixed edit slots while only real selections become manifest items.");
assert(appSource.includes("renderBodyFbxTextureDiagnostic") && appSource.includes("embeddedTextureDetected") && appSource.includes("externalTextureRefs") && appSource.includes("noTextureEvidence"), "CharacterStudio body source card should explain FBX texture-source diagnostics.");
assert(appSource.includes("renderImportMaterialSourceThumbnail") && appSource.includes("getImportMaterialSourceThumbnailUrl") && appSource.includes("/api/character/import-materials/source-image"), "CharacterStudio image source entries should render safe server-backed thumbnails.");
assert(appSource.includes("renderImportMaterialSourcePicker") && appSource.includes('data-import-material-action="openSourcePicker"') && appSource.includes('data-import-material-action="selectSourceCandidate"'), "CharacterStudio source manifest editor should fill source files through a candidate picker");
assert(appSource.includes("data-import-material-source-root") && appSource.includes("getImportMaterialSourceRootOptions"), "CharacterStudio source picker should expose package/public candidate roots");
assert(!appSource.includes("data-import-material-slot-field") && /renderImportMaterialSlotCard[\s\S]*<code[\s\S]*openSourcePicker[\s\S]*syncSlot/.test(appSource), "CharacterStudio slot cards should show source files as controlled selections instead of free-text inputs");
assert(appSource.includes("matchImportMaterialSourceCandidate") && appSource.includes("importMaterialUnrecognizedPaths"), "CharacterStudio pasted paths should be validated against source candidates before editing character_sources.json");
assert(appSource.includes("inferImportMaterialSlotKey") && appSource.includes("ensureImportMaterialSlotItem") && appSource.includes('data-import-material-action="appendPasted"'), "CharacterStudio source manifest editor should smart-classify validated pasted character source paths into slots");
assert(/renderImportMaterialsAdvancedList[\s\S]*data-import-material-field="resourceKey"/.test(appSource), "CharacterStudio should keep resourceKey editing inside the advanced manifest fields only");
assert(appSource.includes("body_albedo.png") && appSource.includes("bodyTexture") && appSource.includes("可选来源未配置，不会保存或同步。") && !appSource.includes("locomotion.fbx"), "CharacterStudio import material primary flow should promote body texture, keep optional empty slots non-blocking, and avoid locomotion path examples");
assert(appSource.includes('slotId: "locomotion"') && appSource.includes("LEGACY_ANIMATION_PROFILE_SLOT_IDS"), "CharacterStudio should keep Locomotion as the only built-in animation profile slot");
assert(!appSource.includes('slotId: "base"') && !appSource.includes('slotId: "combat"') && !appSource.includes("combat-animation"), "CharacterStudio should not recreate legacy base/combat animation slots or checklist rows");
assert(!appSource.includes('id: "locomotion-animation"') && !appSource.includes('slot: "locomotion", required: true'), "CharacterStudio import material templates should not treat Locomotion BlendTree as a source file");
assert(!editorServerImportMaterialsSource.includes("combat-animation") && !editorServerImportMaterialsSource.includes('locomotion-animation'), "Authoring server import material templates should not default combat or locomotion source files");
assert(editorServerImportMaterialsSource.includes("/api/character/import-materials/source-candidates") && editorServerImportMaterialsSource.includes("ReadCharacterImportMaterialSourceCandidates"), "Authoring server should expose read-only import material source candidates");
assert(editorServerImportMaterialsSource.includes("sourceManifestExists") && editorServerImportMaterialsSource.includes("legacyDocumentExists"), "Authoring server import materials API should report source manifest and legacy document presence separately");
assert(editorServerImportMaterialsSource.includes("fbxTextureDiagnostic") && editorServerImportMaterialsSource.includes("BuildFbxTextureDiagnostic") && editorServerImportMaterialsSource.includes("embeddedTextureDetected") && editorServerImportMaterialsSource.includes("externalTextureRefs"), "Authoring server import materials API should report FBX texture diagnostics as status data.");
assert(editorServerImportMaterialsSource.includes("BuildImportMaterialsIntegrityReceipt")
  && editorServerImportMaterialsSource.includes("ReconcileCharacterImportMaterialGraph")
  && editorServerImportMaterialsSource.includes("MATERIAL_DESCRIPTOR_MISSING")
  && editorServerImportMaterialsSource.includes("DEPENDENCY_GRAPH_INCOMPLETE")
  && editorServerImportMaterialsSource.includes("RENDERER_BINDING_INCOMPLETE"),
"Authoring server should audit and repair the raw model-material-texture graph with stable blocking diagnostics.");
assert(importMaterialsDocumentSource.includes('SchemaVersion { get; set; } = "2.0"')
  && importMaterialsDocumentSource.includes("CharacterImportMaterialsIntegrityReceipt")
  && importMaterialsDocumentSource.includes("CharacterImportMaterialBindingReceipt"),
"Authoring core should persist a v2 raw-material state receipt with material-level evidence.");
assert(appSource.includes("原材料导入回执")
  && appSource.includes("完整性阻塞")
  && appSource.includes("integrityReceipt")
  && appSource.includes("blockingIssueCount")
  && appSource.includes("INTEGRITY_RECEIPT_UNAVAILABLE")
  && appSource.includes("缺少回执不能视为原材料导入成功"),
"CharacterStudio should surface raw-material integrity blockers instead of reporting file-copy success alone.");
assert(editorServerImportMaterialsSource.includes("/api/character/import-materials/source-image") && editorServerImportMaterialsSource.includes("ServeCharacterImportMaterialSourceImage") && editorServerImportMaterialsSource.includes("IsImportMaterialBrowserImagePath"), "Authoring server should expose a scoped read-only source image endpoint for thumbnails.");
assert(editorServerCommonResourcesSource.includes("/api/authoring/common-resources") && editorServerCommonResourcesSource.includes("ArtSource/Common") && editorServerCommonResourcesSource.includes("ResolveResourceMappingProfilePath") && editorServerCommonResourcesSource.includes("ApplyCommonResourceProducerSnapshot") && editorServerCommonResourcesSource.includes("common.textures") && editorServerCommonResourcesSource.includes("resourceKind"), "Authoring server should expose public reusable resource sync through the production mapping Aggregator.");
assert(editorServerCommonResourcesSource.includes("bindTarget") && editorServerCommonResourcesSource.includes("weaponMaterialBindingStatus") && editorServerCommonResourcesSource.includes("weaponMaterialRendererCount"), "Authoring server common resources API should carry weapon albedo bind targets and wrapper material diagnostics.");
assert(editorServerImportMaterialsSource.includes("UpsertResourceRefAlias") && editorServerImportMaterialsSource.includes("resourceSelectionAlias"), "Authoring import material sync should turn public resourceRefs into local package alias entries.");
assert(appSource.includes("校验公共复用资源") && appSource.includes("getCommonResourcesUnityImportStepDetail"), "CharacterStudio Unity import task should show public reusable resource validation as a separate stage.");
assert(appSource.includes("importMaterialsDirty") && appSource.includes("importMaterialsSelectedIds"), "CharacterStudio should track checklist dirty and selected row state separately");
assert(appSource.includes("hasUnsavedImportMaterialsChanges") && appSource.includes("markImportMaterialsDirty") && appSource.includes("源清单") && appSource.includes("dirtyParts"), "CharacterStudio shell should surface unsaved character_sources.json and global weapon-library changes instead of reporting a clean package.");
assert(/async function savePackage[\s\S]*packageWasDirty[\s\S]*sourceManifestWasDirty[\s\S]*saveImportMaterialsIfDirty/.test(appSource), "CharacterStudio top-level Save should flush dirty character_sources.json edits as well as package JSON edits.");
assert(appSource.includes("CHARACTER_PACKAGE_VALIDATION_BLOCKED") && appSource.includes("详细内容已放到“诊断”") && appSource.includes("summarizeApiFailure"), "CharacterStudio save failures should show a compact message and route structured validation details to Diagnostics.");
assert(stylesSource.includes("#subtitle") && stylesSource.includes("text-overflow: ellipsis") && stylesSource.includes("white-space: nowrap"), "CharacterStudio shell feedback should stay single-line instead of flooding the workspace with raw server logs.");
assert(/async function compilePackage[\s\S]*saveImportMaterialsIfDirty[\s\S]*\/api\/character\/compile/.test(appSource) && /async function importUnity[\s\S]*saveImportMaterialsIfDirty[\s\S]*isImportBlocked/.test(appSource), "CharacterStudio compile and Unity import should not silently run with unsaved source manifest edits.");
assert(appSource.includes("Unity 导入前请先保存 package JSON 和角色源清单改动。"), "CharacterStudio Unity Import should prompt for saving package/source manifest edits before import.");
assert(stylesSource.includes(".import-materials-panel") && stylesSource.includes(".import-materials-statusbar") && stylesSource.includes(".import-materials-editor-title") && stylesSource.includes(".import-material-slot-grid") && stylesSource.includes(".import-material-slot-hint") && stylesSource.includes(".import-material-source-thumbnail") && stylesSource.includes(".import-materials-extra-list") && stylesSource.includes(".import-materials-batch") && stylesSource.includes(".import-materials-advanced") && stylesSource.includes(".import-materials-row"), "CharacterStudio should style the source manifest panel, slot cards, texture hints, image thumbnails, compact extras, and advanced manifest table");
assert(stylesSource.includes(".import-materials-integrity") && stylesSource.includes(".import-materials-integrity-diagnostic"), "CharacterStudio should style the persistent raw-material integrity receipt and diagnostics.");
assert(editorServerImportMaterialsSource.includes("/api/character/import-materials") && editorServerImportMaterialsSource.includes("SyncCharacterImportMaterials"), "Authoring server should expose import materials APIs");
assert(importMaterialsDocumentSource.includes("CharacterSourceManifestDocument") && importMaterialsDocumentSource.includes("CharacterImportMaterialsStateDocument"), "Authoring core should split source manifests from import state");
assert(editorServerImportMaterialsSource.includes("CharacterSourceManifestFileName") && editorServerImportMaterialsSource.includes("ImportMaterialsStateRelativePath"), "Authoring server should read source manifests and write import state separately");
assert(appSource.includes("character_sources.json") && appSource.includes("源清单文件") && appSource.includes("sourceManifestRelative") && appSource.includes("inferImportMaterialSourceScope"), "CharacterStudio should surface character_sources.json and package/public source scopes");
assert(indexSource.includes("readinessFlow"), "CharacterStudio should expose the in-game readiness panel");
assert(appSource.includes("/api/character/readiness?package=") && appSource.includes("renderReadinessPanel") && appSource.includes("AbilityRuntimeBinding"), "CharacterStudio should read and render character readiness API stages");
assert(appSource.includes("metrics?.editorRequestId") && appSource.includes("/api/character/import-unity/editor-status?id="), "CharacterStudio should restore persisted Unity Editor bridge status when readiness is reloaded");
assert(appSource.includes("readiness: state.readiness"), "CharacterStudio copy report should include readiness diagnostics");
assert(stylesSource.includes(".readiness-stage-list") && stylesSource.includes(".readiness-stage-row"), "CharacterStudio should style readiness stages as a compact list");
const getUnityImportEvidenceView = globalThis.getUnityImportEvidenceView;
const shouldOfferDeferredUnityImport = globalThis.shouldOfferDeferredUnityImport;
assert(typeof getUnityImportEvidenceView === "function", "CharacterStudio should expose its Unity import evidence presentation as a pure smoke-testable helper.");
assert(typeof shouldOfferDeferredUnityImport === "function", "CharacterStudio should expose its Deferred Unity import decision as a pure smoke-testable helper.");
const currentSessionEvidence = getUnityImportEvidenceView({ status: "Ready", metrics: { evidenceSource: "currentUnitySession", editorStatus: "completed", receiptStatus: "Current" } }, {});
const pendingSessionEvidence = getUnityImportEvidenceView({ status: "Deferred", metrics: { evidenceSource: "currentUnitySession", editorStatus: "pending", receiptStatus: "Current" } }, {});
const runningSessionEvidence = getUnityImportEvidenceView({ status: "Deferred", metrics: { evidenceSource: "currentUnitySession", editorStatus: "running", receiptStatus: "Current" } }, {});
const failedSessionEvidence = getUnityImportEvidenceView({ status: "Blocked", metrics: { evidenceSource: "currentUnitySession", editorStatus: "failed", receiptStatus: "Current" } }, {});
const persistentReceiptEvidence = getUnityImportEvidenceView({ status: "Ready", metrics: { evidenceSource: "persistentReceipt", receiptStatus: "Current", receiptPath: "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/package_cache/unity_editor_import_receipt.json" } }, {});
const deferredEvidence = getUnityImportEvidenceView({ status: "Deferred", metrics: { evidenceSource: "none", receiptStatus: "Missing" } }, {});
const blockedEvidence = getUnityImportEvidenceView({ status: "Blocked", metrics: { evidenceSource: "none", receiptStatus: "Invalid" } }, {});
assert(currentSessionEvidence.label === "当前 Unity 会话" && currentSessionEvidence.tone === "ok" && currentSessionEvidence.statusLabel === "completed", "A completed matching editor status should be presented as accepted current Unity session evidence.");
assert(pendingSessionEvidence.label === "当前 Unity 会话（验收中）" && pendingSessionEvidence.tone === "pending" && pendingSessionEvidence.statusLabel === "pending" && pendingSessionEvidence.detail.includes("已排队"), "A pending current Unity session should remain visibly queued instead of claiming acceptance.");
assert(runningSessionEvidence.label === "当前 Unity 会话（验收中）" && runningSessionEvidence.tone === "pending" && runningSessionEvidence.statusLabel === "running" && runningSessionEvidence.detail.includes("正在验收"), "A running current Unity session should remain visibly in progress instead of claiming acceptance.");
assert(failedSessionEvidence.label === "当前 Unity 会话（验收失败）" && failedSessionEvidence.tone === "error" && failedSessionEvidence.statusLabel === "failed" && failedSessionEvidence.detail.includes("优先于旧的持久验收凭据"), "A failed current Unity session should be blocked and must not present an older receipt as current acceptance.");
assert(persistentReceiptEvidence.label === "持久验收凭据" && persistentReceiptEvidence.tone === "ok" && persistentReceiptEvidence.receiptPath.endsWith("unity_editor_import_receipt.json"), "A current receipt should be presented as persistent acceptance evidence with its tracked path.");
assert(deferredEvidence.label === "尚未执行 Unity 导入" && deferredEvidence.tone === "pending" && deferredEvidence.detail.includes("不是角色内容错误"), "Missing Temp status and receipt should be presented as pending import work, not invalid character content.");
assert(blockedEvidence.label === "验收凭据无效" && blockedEvidence.tone === "error", "Invalid receipts should remain an explicit blocked acceptance state.");
assert(shouldOfferDeferredUnityImport({ status: "Deferred" }) && !shouldOfferDeferredUnityImport({ status: "Ready" }) && !shouldOfferDeferredUnityImport({ status: "Blocked" }) && !shouldOfferDeferredUnityImport({ status: "Deferred" }, { hasBlockers: true }) && !shouldOfferDeferredUnityImport({ status: "Deferred" }, { importBlocked: true }), "Only a non-blocked Deferred Unity stage should offer Unity import as the next action.");
assert(/if \(blockerRecords\.length\)[\s\S]*shouldOfferDeferredUnityImport[\s\S]*action: "import"[\s\S]*if \(warningRecords\.length\)/.test(contextPanelsSource), "Deferred Unity evidence should choose Unity import before non-blocking warning guidance while preserving hard blockers.");
assert(/el\.importButton\.disabled = unityImportRunning \|\| !state\.apiAvailable \|\| !state\.package \|\| anyDirty \|\| buildTargetMissing \|\| isImportBlocked\(\);/.test(contextPanelsSource), "Missing Unity acceptance evidence alone should not disable the Unity import button once a target platform is selected.");
assert(contextPanelsSource.includes("验收来源") && contextPanelsSource.includes("receiptStatus") && stylesSource.includes(".unity-import-evidence"), "CharacterStudio should render the Unity acceptance source and receipt status as a dedicated readiness summary.");
assert(indexSource.includes('id="workflowBar"') && appSource.includes("function buildWorkflowView") && appSource.includes("renderWorkflowBar"), "CharacterStudio should expose a top workflow bar derived from readiness state");
assert(appSource.includes("源资产") && appSource.includes("资源绑定") && appSource.includes("Unity 导入") && appSource.includes("入游戏验证"), "CharacterStudio workflow bar should name the current character creation stages");
assert(appSource.includes("选择角色源文件") && appSource.includes("同步角色源文件") && appSource.includes("没有阻塞项。") && appSource.includes("在 Resource Manager 中管理/修复"), "CharacterStudio primary authoring labels should be Simplified Chinese first.");
assert(!appSource.includes("Choose source file") && !appSource.includes("Sync changed sources") && !appSource.includes("No blockers.") && !appSource.includes(">Open Resource Manager<"), "CharacterStudio primary authoring labels should not regress to English actions.");
assert(appSource.includes('selectedPath: "overview"') && appSource.includes("renderOverviewInspector") && appSource.includes("buildTechnicalTree") && appSource.includes("高级 / 调试树") && appSource.includes("Debug：Manifest JSON"), "CharacterStudio should default to a workflow overview and move raw Manifest JSON behind a debug foldout.");
assert(appSource.includes("workflow-issue-card") && appSource.includes("describeWorkflowIssue"), "CharacterStudio should render readiness warnings as actionable issue cards instead of a naked code line.");
assert(appSource.includes('data-workflow-action="') && appEntrySource.includes("handleWorkflowActionClick"), "CharacterStudio workflow bar should route primary next actions through the app entry");
assert(/function openWorkflowInspectorTab[\s\S]*contextDockCollapsed = true[\s\S]*scrollIntoView[\s\S]*workflow-target-flash/.test(appEntrySource), "workflow inspector actions should leave hidden weapon-focus mode, reveal the inspector, and visibly focus the requested status panel");
assert(/function renderImportStatus[\s\S]*readinessStage\?\.blockers[\s\S]*当前导入诊断/.test(appSource), "Unity import inspector should show persisted readiness blockers even after the immediate request result is gone");
assert(!renderContextDockSummarySource.includes("getPrimaryReadinessIssue") && !renderContextDockSummarySource.includes("blockers /") && !renderContextDockSummarySource.includes("warnings"), "CharacterStudio context dock summary should not render the global primary readiness issue");
assert(editorServerSource.includes("/api/character/readiness"), "Authoring server should expose the character readiness API");
assert(indexSource.includes("contextDock") && indexSource.includes('data-context-tab=\"validation\"') && indexSource.includes('data-validation-tab=\"readiness\"') && appSource.includes("activeContextTab"), "CharacterStudio should route authoring and consolidated runtime validation through the context dock");
assert(indexSource.includes("高级兼容诊断") && indexSource.includes("实验性能力兼容诊断"), "CharacterStudio should expose frozen ability data only as an advanced compatibility diagnostic");
assert(editorServerSource.includes("/api/character/ability-chain") && editorServerAbilityChainSource.includes("ReadCharacterAbilityChain") && editorServerAbilityChainSource.includes("BuildAbilityScenario"), "Authoring server should expose a compiled character ability-chain snapshot API");
assert(editorServerReadinessSource.includes("ReadAbilityActionBindings(rootPath, snapshot.PackageId)") && editorServerReadinessSource.includes("actionSetRoot = Path.Combine(actionSetRoot, packageId)"), "CharacterAction readiness should scope numeric ability bindings to the selected package");
assert(appSource.includes("/api/character/ability-chain?package=") && appSource.includes("data-load-frozen-ability-diagnostics") && appSource.includes("Frozen / Experimental Compatibility"), "CharacterStudio should load the ability chain only from the advanced read-only compatibility diagnostic");
assert(editorServerSource.includes("/api/character/weapon-configurations") && editorServerSource.includes("/api/weapon-library/state") && appSource.includes("singleWeaponConfigurationPanel") && appSource.includes("combinedWeaponConfigurationPanel") && appSource.includes("setWeaponConfigurationSlot"), "CharacterStudio should expose the global weapon library together with character combined-weapon bindings and overrides");
assert(manifest.packageSchemaVersion === "8.0" && applicationConfig.schemaVersion === "8.0", "codex flow test should use Character Application schema 8.0.");
assert(globalWeaponLibrary.schemaVersion === "6.0" && globalWeaponLibrary.combinedWeaponConfigurations.length > 0 && globalWeaponLibrary.animationConfigurations.length > 0 && globalWeaponLibrary.abilityConfigurations.length === 5 && globalWeaponLibrary.reactionConfigurations.length > 0, "global library 6.0 should store public combined defaults plus all reusable behavior collections.");
assert(!("abilityLoadouts" in applicationConfig) && !("animationProfiles" in applicationConfig) && !("reactionProfiles" in applicationConfig) && !("combinedWeaponConfigurations" in applicationConfig), "schema 8.0 character root should persist neither legacy behavior arrays nor copied public combinations.");
assert(applicationConfig.combinedWeaponConfigurationBindings.every(binding => globalWeaponLibrary.combinedWeaponConfigurations.some(item => item.combinedWeaponConfigurationId === binding.combinedWeaponConfigurationId && item.animationConfigurationId && item.reactionConfigurationId && item.actionConfiguration?.actions?.some(action => action.actionRole && action.actionId))), "every character binding should resolve to a complete public combined-weapon default.");
assert(globalWeaponLibrary.schemaVersion === "6.0", "sample should use Global Weapon Library schema 6.0.");
assert(globalWeaponLibrary.abilityConfigurations.every(item => item.abilities.some(ability => ability.abilityId === 900001 && ability.displayName === "闪避" && ability.executionMode === "ActionOnly")), "every sample ability configuration should expose the semantic ActionOnly dodge ability.");
assert(contextPanelsSource.includes("实验性能力兼容诊断") && contextPanelsSource.includes("不能创建、修改、复制或绑定能力") && contextPanelsSource.includes("data-load-frozen-ability-diagnostics"), "schema 7.0 ability authoring should remain frozen behind a read-only diagnostic.");
assert(!combinedWeaponWorkbenchSource.match(/renderCombinedBehaviorSectionHumanized[\s\S]*?renderCombinedBehaviorPicker\("ability"/), "the current combined weapon workflow should not render an ability selector.");
assert(editorServerSource.includes("/api/weapon-library/references") && appSource.includes("copyRebind") && appSource.includes("globalAnimationConfigurationPanel") && appSource.includes("globalAbilityConfigurationPanel") && appSource.includes("globalReactionConfigurationPanel"), "CharacterStudio should expose reference scans, shared quick editing and copy-rebind for all global behavior configurations.");
assert(appSource.includes("当前有效授予") && appSource.includes("槽位输入") && appSource.includes("角色动作") && appSource.includes("运行时实现"), "CharacterStudio should present the ability chain as four explicit data stages");
assert(!appSource.includes("inferGeneratedAbilityLoadouts") && !appSource.includes("CreateAbilityDefinition"), "CharacterStudio should not infer synthetic ability ids or require the separate AbilityDefinition runtime path");
assert(appSource.includes("CharacterTest 的 1～9 试玩技能栏不属于这套角色授予数据") && appSource.includes("data-ability-action"), "CharacterStudio should distinguish the CharacterTest hotbar and preserve CharacterAction handoff actions");
assert(appSource.includes("abilityChain: getAbilityChainView()"), "CharacterStudio copy report should include the selected compiled ability-chain scenario");
assert(appSource.includes("/api/character-action/open") && appSource.includes("pollAbilityActionOpenStatus"), "CharacterStudio ability handoff should request Unity action authoring and poll open status");
assert(appSource.includes("abilityActionOpenRequests") && appSource.includes("已复制 fallback 位置"), "CharacterStudio ability handoff should expose pending/opened/failed button states with fallback");
assert(combinedWeaponWorkbenchSource.includes('["primary", "普通攻击 1"]')
  && combinedWeaponWorkbenchSource.includes('["primary.combo2", "连段 2"]')
  && combinedWeaponWorkbenchSource.includes('["primary.combo3", "连段 3"]')
  && combinedWeaponWorkbenchSource.includes('["heavy", "重击"]')
  && combinedWeaponWorkbenchSource.includes('["evade", "闪避"]')
  && !combinedWeaponWorkbenchSource.includes('["ability.evade", "闪避"]'),
  "CharacterStudio should expose the actual core CharacterAction slots, including the non-ability evade role.");
assert(combinedWeaponWorkbenchSource.includes("角色专属")
  && combinedWeaponWorkbenchSource.includes("公共动作")
  && combinedWeaponWorkbenchSource.includes("复制为角色专属并编辑")
  && combinedWeaponWorkbenchSource.includes("只读 JSON 审计"),
  "CharacterStudio should use grouped action choices, Common copy-on-edit, and read-only JSON audit instead of raw ActionSet editing.");
assert(!appSource.includes('data-private-action-json')
  && !appSource.includes('data-weapon-config-field="actionSetId"')
  && !appSource.includes("actionConfiguration.actionSetId"),
  "CharacterStudio must not retain legacy editable ActionSet ids or raw action-configuration JSON fallbacks.");
assert(stylesSource.includes(".context-dock") && stylesSource.includes(".context-dock.is-collapsed .context-dock-body"), "CharacterStudio context dock should default to a compact collapsed state");
assert(stylesSource.includes(".workspace.context-expanded .viewport") && stylesSource.includes("clamp(180px, 24vh, 260px)"), "Expanded context dock should compact the 3D viewport on desktop");
assert(stylesSource.includes(".workspace.context-expanded .context-dock-body") && stylesSource.includes("min(52vh, 460px)"), "Expanded context dock should give more room to the active context panel");
assert(!indexSource.includes('class=\"readiness-panel\" open') && !indexSource.includes('id=\"readinessPanel\"'), "CharacterStudio readiness should not render as a default-open middle-column details panel");
assert(indexSource.includes("inspectorTabs") && indexSource.includes('data-inspector-tab=\"diagnostics\"') && appSource.includes("activeInspectorTab"), "CharacterStudio should consolidate properties, diagnostics, and Unity import into inspector tabs");
assert(editorServerCharacterActionOpenSource.includes("/api/character-action/open") && editorServerCharacterActionOpenSource.includes("open_request.json"), "Authoring server should expose a Temp-backed CharacterAction open bridge");
assert(editorServerCharacterActionOpenSource.includes("request.profileSourceDirectory") && editorServerCharacterActionOpenSource.includes("request.actionSetSourceDirectory") && editorServerCharacterActionOpenSource.includes("request.actionLibrarySourceDirectory"), "Authoring server CharacterAction open request should require the selected Profile, ActionSet, and Library locations instead of a fixed character default.");
assert(characterActionAuthoringModelSource.includes('StableId = "codex_flow_test.default"') && characterActionAuthoringModelSource.includes('ReactionProfileId = "reaction.codex_flow_test.default"'), "CharacterAction editor default model should create codex_flow_test profile/action set ids");
assert(!characterActionAuthoringModelSource.includes('StableId = "character_test.default"') && !characterActionAuthoringModelSource.includes("reaction.character_test.katana.default"), "CharacterAction editor default model should not create character_test profile/action set ids");
assert(characterActionAuthoringWindowSourcesSource.includes("DefaultCharacterActionProfileSourceDirectory") && characterActionAuthoringWindowSourcesSource.includes("Profiles/codex_flow_test") && characterActionAuthoringWindowSourcesSource.includes("FindPreferredSourceIndex"), "Unity CharacterAction window should prefer codex_flow_test over sorted discovered sources");
assert(characterTestActionConfigSource.includes('ActionSetStableId = "codex_flow_test.sword_empty"') && characterTestActionConfigSource.includes("ActionPackageSegment"), "CharacterTest should retain the Codex fixture identity only as compatibility data while external discovery loads all generated Profiles and ActionSets.");
assert(!fs.existsSync(path.join(repoRoot, "Assets/MxFrameworkGenerated/CharacterActions/Profiles/character_test")) && !fs.existsSync(path.join(repoRoot, "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/character_test")), "Generated character_test CharacterAction file sources should be removed");
const codexFlowActionSetIds = (codexFlowTestActionSet.actionSets || []).map(item => item.stableId);
assert(codexFlowActionSetIds.length === 2 && ["empty_empty", "sword_empty"].every(id => codexFlowActionSetIds.includes(`codex_flow_test.${id}`)), "codex_flow_test should generate one CharacterAction ActionSet for each of the two accepted combinations");
assert(codexFlowTestActionSet.actionSets?.every(item => item.equipmentStateStableId && item.reactionProfileId === `reaction.${item.stableId}`), "generated ActionSets should bind exact equipment states and combination-private reaction profiles");
assert(codexFlowTestActionSet.actionSets?.every(item => item.commandBindings?.some(binding => binding.intentId === "intent.dodge" && binding.actionId.startsWith(`${item.stableId}.`))), "every generated weapon configuration ActionSet should bind its complete ability configuration to private actions");
const privateActionIds = new Set((codexFlowTestActionLibrary.actions || []).map(item => item.stableId));
const commonActionIds = new Set((commonCharacterActionLibrary.actions || []).map(item => item.stableId));
assert(codexFlowTestActionSet.actionSets?.every(item => item.abilityBindings?.every(binding => privateActionIds.has(binding.actionId) || commonActionIds.has(binding.actionId))), "every ability binding should resolve through the Profile's package-private or Common CharacterAction libraries");
assert(codexFlowTestActionLibrary.actions?.every(item => ["Dodge", "Skill", "Reaction"].includes(item.category)), "generated private actions should use valid CharacterActionCategory names");
assert(codexFlowTestActionProfile.profiles?.[0]?.actionLibraryIds?.includes("codex_flow_test") && codexFlowTestActionProfile.profiles?.[0]?.actionLibraryIds?.includes("Common"), "the generated profile should load both package-private and Common CharacterAction libraries");
assert(indexSource.includes("viewWeaponConfigButton"), "CharacterStudio should expose a focused weapon view button");
assert(indexSource.includes("returnCharacterViewButton"), "CharacterStudio should expose a return-to-character button");
assert(appSource.includes('viewportMode: "character"') && appSource.includes("focusedWeaponAttachmentId"), "CharacterStudio should keep explicit viewport mode and attachment identity state");
assert(appSource.includes("weapon.attachment.") && appSource.includes("singleWeaponConfigurationId") && appSource.includes("weaponSlots"), "CharacterStudio should derive distinct runtime attachments from combination slot and single-weapon identity");
assert(appSource.includes("function buildWeaponVisualizationItems") && appSource.includes("window.CharacterStudioTest") && appSource.includes("buildWeaponVisualizationItems"), "CharacterStudio should expose a shared weapon visualization resolver");
assert(appSource.includes('buildWeaponAttachmentForSlot(configuredSlot, pkg, activeConfiguration?.combinedWeaponConfigurationId || "configuration")'), "CharacterStudio weapon overlay actions should use the same configuration-qualified attachment identity as the active preview.");
assert(appSource.includes("const activeAttachments = getActiveWeaponAttachments();") && appSource.includes("getWeaponAttachmentIdentity(attachment) === attachmentId"), "CharacterStudio should resolve focused weapon actions from compiler-derived active attachments.");
assert(appSource.includes("attachments: activeAttachments") && !appSource.includes("attachments: (geometry.weaponAttachments || []).filter"), "schema 5.0 character preview should render compiler-derived active attachments instead of the retired character attachment source list");
assert(appSource.includes("const lineSpace = object.parent || content;") && appSource.includes("lineSpace.worldToLocal(start);") && appSource.includes("lineSpace.worldToLocal(end);"), "nested weapon-local trace lines should convert endpoints into the line parent space instead of applying the weapon transform twice");
assert(appSource.includes('!(activeTab === "singleWeapons" && singleWeaponPreview)') && !appSource.includes('activeTab !== "singleWeapons"'), "single-weapon editing should use the full-height authoring workspace while only its 3D preview keeps the viewport visible");
assert(!appSource.includes("const LOADOUTS") && !appSource.includes("activeLoadout") && !indexSource.includes("loadoutTabs") && !appSource.includes("data-loadout"), "CharacterStudio preview should not expose legacy hard-coded loadout tabs");
assert(appSource.includes("function getDefaultWeaponSlotList") && appSource.includes("function getDefaultWeaponSlotSet") && appSource.includes("getActiveWeaponConfiguration"), "CharacterStudio preview should derive active weapon slots from the global weapon configuration");
assert(appSource.includes("addFocusedWeaponViewportContent") && appSource.includes("renderWeaponFocusOverlayHtml"), "CharacterStudio should render a focused weapon viewport");
const focusedWeaponViewportStart = appSource.indexOf("async function addFocusedWeaponViewportContent");
const focusedWeaponViewportEnd = appSource.indexOf("function getWeaponVisualizationItemForWeapon", focusedWeaponViewportStart);
assert(focusedWeaponViewportStart >= 0 && focusedWeaponViewportEnd > focusedWeaponViewportStart, "smoke should locate addFocusedWeaponViewportContent");
const focusedWeaponViewportBody = appSource.slice(focusedWeaponViewportStart, focusedWeaponViewportEnd);
assert(focusedWeaponViewportBody.includes("definition?.traceProfiles || []")
  && focusedWeaponViewportBody.includes("objectPathPrefix: `${item.weaponPath}/traceProfiles`")
  && focusedWeaponViewportBody.includes("enableSpatialEditing: true"),
  "Focused single-weapon viewport should render every authoritative weapon-local trace under the weapon model-pose root.");
assert(appSource.includes('transformKind: "traceScalar"') && appSource.includes("commitTraceScalarTransformTarget") && appSource.includes("updateTraceVolumeObject"), "single-weapon 3D editing should expose trace radius, proximal extension, and proximal radius handles with live volume feedback.");
assert(appSource.includes("if (!options.enableSpatialEditing) continue;"), "character-level previews should omit single-weapon spatial-authoring volumes and scalar handles.");
assert(appSource.includes("addSingleWeaponPoseCalibrationGuide") && appSource.includes("referenceGuidePose.scale = { x: 1, y: 1, z: 1 }") && appSource.includes("single_weapon_pose_calibration_guide") && appSource.includes("握持原点 / 护手面") && appSource.includes("+Y 长度") && appSource.includes("+Z 正面朝向"), "single-weapon calibration should inherit the reference weapon position/rotation, retain meter scale, and expose an explicit +Z front direction.");
assert(appSource.includes("new THREE.Vector3(0, y, -halfWidth), new THREE.Vector3(0, y, halfWidth)"), "single-weapon calibration ticks should extend along local Z after the requested 90-degree rotation.");
assert(appSource.includes("index % 5 === 0 ? 0.18 : 0.09"), "single-weapon calibration ticks should use the widened major and minor extents.");
assert(appSource.includes("getSingleWeaponPoseReferenceDefinition") && appSource.includes("data-single-weapon-pose-reference") && appSource.includes("wireframe = true"), "single-weapon 3D editing should allow a model-bound weapon to be selected as a translucent pose reference.");
assert(stylesSource.includes(".weapon-pose-guide-controls"), "the pose-reference selector should have a compact visible control surface in the weapon overlay.");
assert(appSource.includes("updateTraceVolumeObject(THREE, object, volume, object.parent || content)") && appSource.includes("const coordinateSpace = target.object.parent || viewportLiveContent"), "nested weapon Trace volumes and scalar handles should calculate and commit in their own parent space instead of applying the weapon transform twice.");
assert(appSource.includes("requestViewportUpdate") && appSource.includes("refreshViewportLiveData"), "CharacterStudio should route inspector edits through viewport update requests");
assert(stylesSource.includes(".workspace.weapon-focus-mode.context-expanded.context-tab-singleWeapons .context-dock") && stylesSource.includes("scene above, structured"), "single-weapon focus should keep the structured configuration dock below the 3D scene.");
assert(indexSource.includes("weaponTransformMoveButton") && indexSource.includes("weaponTransformRotateButton") && indexSource.includes("weaponTransformScaleButton"), "Focused weapon viewport should expose transform mode buttons");
assert(indexSource.includes("symmetryEditToggleButton") && stylesSource.includes(".symmetry-toggle.active"), "CharacterStudio should expose a visible symmetry edit toggle in the viewport toolbar");
assert(indexSource.includes("undoButton") && indexSource.includes("redoButton"), "CharacterStudio should expose visible undo and redo buttons");
assert(appSource.includes("TransformControls.js") && appSource.includes("registerViewportTransformTarget") && appSource.includes("commitWeaponTransformTarget"), "Focused weapon viewport should use TransformControls for direct transform editing");
assert(appSource.includes("undoStack: []") && appSource.includes("redoStack: []") && appSource.includes("activeUndoTransaction") && appSource.includes("cleanPackageJson"), "CharacterStudio should keep undo/redo state");
assert(appSource.includes("handleUndoRedoKeydown") && appSource.includes("redoLastEdit") && indexSource.includes("Ctrl+Z"), "CharacterStudio should wire undo/redo keyboard shortcuts");
assert(appSource.includes("beginUndoTransaction") && appSource.includes("commitUndoTransaction") && appSource.includes("undoLastEdit") && appSource.includes("refreshDirtyFromCleanSnapshot"), "CharacterStudio should provide shared undo transaction APIs");
assert(appSource.includes("symmetryEditEnabled: false") && appSource.includes("function toggleSymmetryEdit") && appSource.includes("function applySymmetryEdit"), "CharacterStudio should keep symmetry edit state and writeback logic");
assert(appSource.includes("mirrorPairSocketId") && appSource.includes("function findMirrorSocket") && appSource.includes("function mirrorSideToken"), "Symmetry edit should resolve explicit socket mirror pairs and left/right token pairs");
assert(viewport3d.mirrorSideToken("Dragon15_root/Dragon15_r_wing_00") === "Dragon15_root/Dragon15_l_wing_00", "Symmetry should mirror an _r_ side token without corrupting Dragon15_root into Dragon15_loot.");
assert(viewport3d.mirrorSideToken("Dragon15_root/Dragon15_l_wing_00") === "Dragon15_root/Dragon15_r_wing_00", "Symmetry should mirror an _l_ side token while preserving the skeleton root name.");
assert(indexSource.includes('id="colliderSymmetrySyncButton"') && appSource.includes("function synchronizeVisibleColliderSymmetryFromLeft"), "CharacterStudio should expose an explicit visible-collider left-to-right world-space sync action.");
assert(viewport3d.isLeftSideMirrorSource({ colliderId: "hurt.left_wing_membrane.obb.0" }) && !viewport3d.isLeftSideMirrorSource({ colliderId: "hurt.right_wing_membrane.obb.0" }), "Collider batch symmetry should choose only canonical left-side sources.");
const symmetryContentMatrix = new THREE.Matrix4().compose(
  new THREE.Vector3(3, -2, 1),
  new THREE.Quaternion().setFromEuler(new THREE.Euler(0.1, -0.2, 0.05)),
  new THREE.Vector3(1, 1, 1));
const symmetrySourceInContent = new THREE.Matrix4().compose(
  new THREE.Vector3(2, 1, -3),
  new THREE.Quaternion().setFromEuler(new THREE.Euler(0.2, 0.3, -0.4)),
  new THREE.Vector3(1, 1, 1));
const symmetrySourceWorld = new THREE.Matrix4().multiplyMatrices(symmetryContentMatrix, symmetrySourceInContent);
const symmetryTargetParentInContent = new THREE.Matrix4().compose(
  new THREE.Vector3(-1.5, 0.7, 0.2),
  new THREE.Quaternion().setFromEuler(new THREE.Euler(-0.35, 0.25, 0.45)),
  new THREE.Vector3(1, 1, 1));
const symmetryTargetParentWorld = new THREE.Matrix4().multiplyMatrices(symmetryContentMatrix, symmetryTargetParentInContent);
const symmetryTargetPose = viewport3d.computeMirroredLocalPoseAcrossContentX(
  THREE, symmetrySourceWorld, symmetryContentMatrix, symmetryTargetParentWorld, "");
const symmetryTargetLocal = new THREE.Matrix4().compose(
  new THREE.Vector3(symmetryTargetPose.position.x, symmetryTargetPose.position.y, symmetryTargetPose.position.z),
  new THREE.Quaternion(symmetryTargetPose.rotation.x, symmetryTargetPose.rotation.y, symmetryTargetPose.rotation.z, symmetryTargetPose.rotation.w),
  new THREE.Vector3(symmetryTargetPose.scale.x, symmetryTargetPose.scale.y, symmetryTargetPose.scale.z));
const symmetryActualInContent = new THREE.Matrix4().multiplyMatrices(symmetryTargetParentInContent, symmetryTargetLocal);
const symmetryReflection = new THREE.Matrix4().makeScale(-1, 1, 1);
const symmetryExpectedInContent = new THREE.Matrix4().copy(symmetryReflection).multiply(symmetrySourceInContent).multiply(symmetryReflection);
assert(symmetryActualInContent.elements.every((value, index) => Math.abs(value - symmetryExpectedInContent.elements[index]) < 0.00001), "Collider symmetry should mirror the source world transform across character X before converting into the paired bone local space.");
assert(appSource.includes('if (target.kind === "weapon") return path === "localGripPose"') && appSource.includes("function mirrorWeaponGripEdit"), "Weapon symmetry edit should only mirror grip pose fields, not weapon identity or resources");
assert(appSource.includes("function findMirrorCollider") && appSource.includes('path === "radius"') && appSource.includes('path.startsWith("size.")'), "Collider symmetry edit should mirror left/right pose and dimensions");
assert(appSource.includes("function createMirroredContactPointConfig") && appSource.includes("findMirroredBodyPartId"), "Contact audio point creation should support symmetry edit pair creation");
assert(appSource.includes('path === "motionSocketId"') && appSource.includes('path === "activationMode"') && appSource.includes('path === "usage"') && appSource.includes("mirrorContactPointEdit(target.value, path)"), "Contact audio point symmetry edit should mirror socket binding and presentation fields");
assert(appSource.includes("viewportLiveContactPointBindings") && appSource.includes("registerViewportContactPointBinding"), "Contact audio point symmetry edits should live-refresh paired viewport spheres.");
assert(!appSource.includes("updateContactPointBakedMotionAnchors"), "Contact audio point preview must not run a separate baked animation clock.");
assert(appSource.includes("expectedStableId") && appSource.includes("expectedPartId") && appSource.includes("score += 8"), "Contact audio point mirror matching should prefer explicit mirrored stable id and part id.");
assert(!appSource.includes('target.kind === "resource" && path.startsWith("importHints.modelWrapperPose")'), "Model wrapper pose edits should not force full viewport rebuilds");
assert(appSource.includes("withWeaponModelWrapperFields") && appSource.includes("getInspectorInputTarget") && appSource.includes("isWeaponModelWrapperField"), "Weapon inspector should edit the bound model wrapper pose in the same weapon context");
assert(appSource.includes("addColliderMeshes(THREE, content, pickables, colliders, bodyBoneRecords = [], registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER)") && appSource.includes('if (target.kind === "collider") return `${path}#localPose`;'), "Collider selections should support viewport TransformControls on localPose");
assert(appSource.includes("wireframe: true") && appSource.includes("material.wireframe = !selected"), "Collider debug meshes should keep the legacy wireframe preview style");
assert(appSource.includes("inheritScale: options.inheritScale !== false") && appSource.includes("{ inheritScale: false, poseAdapter }"), "Collider debug meshes should follow bone position/rotation without inheriting tiny imported bone scale");
assert(appSource.includes("createPoseAnchor(THREE, content, gripPose, bodyBoneRecords, socketsById") && appSource.includes("weapon_${attachment.weaponId}_grip`, { inheritScale: false, poseAdapter }"), "Weapon preview meshes should follow hand bones without inheriting imported bone scale");
assert(appSource.includes('return target.kind === "socket" || target.kind === "collider" || target.kind === "contactPoint";'), "Character viewport transform controls should be available for sockets, colliders, and contact points");
assert(appSource.includes("applyColliderSizeFromTransform") && appSource.includes('shape !== "sphere" && shape !== "capsule" && shape !== "box"'), "Sphere, capsule, and Box collider scaling should write shape dimensions instead of only localPose scale");
assert(appSource.includes("collider.radius = roundTransformNumber") && appSource.includes("collider.height = roundTransformNumber") && appSource.includes("pose.scale.x = 1"), "Collider scale Gizmo should bake radius/height and reset localPose scale");
const previousColliderScaleState = globalThis.state;
globalThis.state = { weaponTransformMode: "scale" };
const boxScaleCollider = { shape: "Box", size: { x: 2, y: 3, z: 4 } };
const boxScaleTarget = {
  transformKind: "collider",
  collider: boxScaleCollider,
  transformStart: globalThis.captureViewportTransformStart({ collider: boxScaleCollider })
};
assert(globalThis.applyColliderSizeFromTransform(boxScaleTarget, { scale: { x: 1.5, y: 0.5, z: 2 } })
  && boxScaleCollider.size.x === 3
  && boxScaleCollider.size.y === 1.5
  && boxScaleCollider.size.z === 8,
  "Box collider scale Gizmo should update authoritative per-axis dimensions.");
globalThis.state = previousColliderScaleState;
const boxPositiveFace = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Box", size: { x: 2, y: 3, z: 4 } },
  { kind: "face", axis: "x", sign: 1 },
  0.5,
  false);
assert(boxPositiveFace.size.x === 2.5
  && boxPositiveFace.centerShift.x === 0.25
  && boxPositiveFace.centerShift.y === 0
  && boxPositiveFace.centerShift.z === 0,
  "Box positive-face drag should keep the opposite face fixed by moving size and center together.");
const boxNegativeFace = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Box", size: { x: 2, y: 3, z: 4 } },
  { kind: "face", axis: "z", sign: -1 },
  0.4,
  false);
assert(boxNegativeFace.size.z === 4.4 && Math.abs(boxNegativeFace.centerShift.z + 0.2) < 0.000001,
  "Box negative-face drag should move the center toward the dragged negative face.");
const boxSymmetricFace = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Box", size: { x: 2, y: 3, z: 4 } },
  { kind: "face", axis: "y", sign: 1 },
  0.25,
  true);
assert(boxSymmetricFace.size.y === 3.5
  && boxSymmetricFace.centerShift.x === 0
  && boxSymmetricFace.centerShift.y === 0
  && boxSymmetricFace.centerShift.z === 0,
  "Alt Box drag should resize symmetrically without moving the center.");
const clampedBoxFace = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Box", size: { x: 0.004, y: 1, z: 1 } },
  { kind: "face", axis: "x", sign: 1 },
  -5,
  false);
assert(clampedBoxFace.size.x === 0.001 && Math.abs(clampedBoxFace.centerShift.x + 0.0015) < 0.000001,
  "Box face drag should clamp at one millimeter while preserving the fixed opposite face.");
const halfTurn = Math.sin(Math.PI / 4);
const rotatedShift = viewport3d.rotateHurtboxCenterShift(
  { x: 0.25, y: 0, z: 0 },
  { x: 0, y: halfTurn, z: 0, w: Math.cos(Math.PI / 4) });
assert(Math.abs(rotatedShift.x) < 0.000001 && Math.abs(rotatedShift.z + 0.25) < 0.000001,
  "Fixed-face center motion should rotate with the collider local pose before writing parent-local position.");
const capsuleHeight = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Capsule", radius: 0.5, height: 2 },
  { kind: "height", axis: "y", sign: -1 },
  0.4,
  false);
assert(capsuleHeight.height === 2.4 && Math.abs(capsuleHeight.centerShift.y + 0.2) < 0.000001,
  "Capsule end drag should keep the opposite end fixed.");
const capsuleRadius = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Capsule", radius: 0.5, height: 1.1 },
  { kind: "radius", axis: "x", sign: 1 },
  0.2,
  false);
assert(capsuleRadius.radius === 0.7 && capsuleRadius.height === 1.401,
  "Capsule radius drag should keep the center fixed and raise height to at least two radii plus one millimeter.");
const capsuleHeightClamp = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Capsule", radius: 0.5, height: 2 },
  { kind: "height", axis: "y", sign: 1 },
  -20,
  false);
assert(capsuleHeightClamp.height === 1.001,
  "Capsule height handles should never cross the two-radii plus one-millimeter minimum.");
const sphereRadius = viewport3d.computeHurtboxShapeHandleEdit(
  { shape: "Sphere", radius: 0.4 },
  { kind: "radius", axis: "z", sign: -1 },
  0.3,
  false);
assert(sphereRadius.radius === 0.7
  && sphereRadius.centerShift.x === 0
  && sphereRadius.centerShift.y === 0
  && sphereRadius.centerShift.z === 0,
  "Sphere handles should update one radius scalar without moving the center.");
const shapeWriteCollider = {
  shape: "Box",
  size: { x: 1, y: 1, z: 1 },
  localPose: { scale: { x: 3, y: 4, z: 5 } }
};
viewport3d.applyHurtboxShapeHandleEdit(shapeWriteCollider, boxPositiveFace);
assert(shapeWriteCollider.size.x === 2.5
  && shapeWriteCollider.localPose.scale.x === 1
  && shapeWriteCollider.localPose.scale.y === 1
  && shapeWriteCollider.localPose.scale.z === 1,
  "Dedicated shape handles should only persist authoritative dimensions and unit localPose scale.");
const cancelledCollider = {
  colliderId: "hurt.cancel.box.0",
  shape: "Box",
  size: { x: 4, y: 5, z: 6 },
  localPose: { position: { x: 7, y: 8, z: 9 }, scale: { x: 1, y: 1, z: 1 } }
};
const cancelledSnapshot = JSON.parse(JSON.stringify(cancelledCollider));
cancelledCollider.size.x = 99;
cancelledCollider.localPose.position.y = -99;
viewport3d.restoreColliderFromSnapshot({ collider: cancelledCollider }, cancelledSnapshot);
assert(JSON.stringify(cancelledCollider) === JSON.stringify(cancelledSnapshot),
  "Cancelled dedicated-handle drags should restore the collider snapshot byte-for-byte at the document level.");
assert(viewport3d.getHurtboxShapeHandleDescriptors({ shape: "Box" }).length === 6
  && viewport3d.getHurtboxShapeHandleDescriptors({ shape: "Capsule" }).length === 6
  && viewport3d.getHurtboxShapeHandleDescriptors({ shape: "Sphere" }).length === 6,
  "Each supported Hurtbox shape should expose only its six dedicated size handles.");
assert(appSource.includes("createHurtboxShapeHandleController")
  && appSource.includes("usesDedicatedSizeHandles")
  && appSource.includes("pointercancel")
  && appSource.includes('String(event.key || "") !== "Escape"')
  && appSource.includes("beginUndoTransaction(`调整受击体尺寸")
  && appSource.includes("commitUndoTransaction()"),
  "CharacterStudio should route collider R mode through cancellable dedicated shape handles and one drag transaction.");
assert(appSource.includes("if (hurtboxShapeHandles.pointerDown(event)) return;")
  && appSource.includes("const colliderHits = []")
  && appSource.includes("colliderHits[index]")
  && appSource.includes("side: THREE.DoubleSide")
  && !appSource.includes("find(item => findObjectPath(item.object) === currentPath)"),
  "Viewport picking should prioritize dedicated handles, then the nearest double-sided collider, and cycle stable overlapping collider hits.");
assert(stylesSource.includes(".hurtbox-size-edit-overlay")
  && appSource.includes("对侧固定")
  && appSource.includes("中心对称"),
  "Hurtbox size drags should expose live dimensions, delta, and fixed-opposite or symmetric feedback.");
assert(indexSource.includes('id="hurtboxBindingReferenceButton"')
  && stateModuleSource.includes("hurtboxBindingReferenceVisible: true")
  && sessionStateSource.includes('"hurtboxBindingReferenceVisible"'),
  "CharacterStudio should expose and persist a session-only Hurtbox binding-reference toggle that defaults on.");
assert(appSource.includes("const workspaceAvailable = state.viewportMode === \"character\" && state.layers.colliders && colliders.length > 0")
  && appSource.includes("绑定参考已开启；选择一个受击体即可显示参考")
  && !appSource.includes("state.layers.colliders && Boolean(selectedCollider)"),
  "The binding-reference toggle should remain interactive before collider selection and only gate the rendered reference on selection.");
assert(appSource.includes("createHurtboxBindingReferenceController")
  && appSource.includes("HurtboxBindingAdjacentBones")
  && appSource.includes("HurtboxBindingLocalPoseOffset")
  && appSource.includes("HurtboxBindingBoneStartCore")
  && appSource.includes("HurtboxBindingBoneEnd")
  && appSource.includes("binding-start-label")
  && appSource.includes("binding-end-label")
  && stylesSource.includes(".hurtbox-binding-endpoint-label")
  && appSource.includes("骨骼起点")
  && appSource.includes("骨骼终点")
  && !appSource.includes("HurtboxBindingReferenceShape")
  && !appSource.includes("HurtboxBindingEffectiveAxes"),
  "Selected Hurtboxes should show the bound bone, reduced adjacent context, and offset connector without a reference collider or effective-axis gizmo.");
assert(stylesSource.includes(".hurtbox-binding-reference-overlay")
  && stylesSource.includes(".binding-reference-legend")
  && appSource.includes("host.dataset.hurtboxBindingReference")
  && appSource.includes("无法解析骨骼"),
  "Hurtbox binding references should expose structured visible resolution evidence and fail closed when a bone cannot resolve.");
assert(appSource.includes("selectHurtboxBindingReferenceNeighbors")
  && appSource.includes('role: "primaryChild"')
  && appSource.includes("骨链方向"),
  "Hurtbox binding references should reduce dense skeleton branches to the parent and one labeled primary child.");

const referenceParent = new THREE.Matrix4().compose(
  new THREE.Vector3(3, 4, 5),
  new THREE.Quaternion().setFromEuler(new THREE.Euler(0.2, -0.35, 0.12)),
  new THREE.Vector3(2, 0.75, 1.5));
const referencePose = {
  position: { x: 0.4, y: -0.2, z: 0.7 },
  rotation: new THREE.Quaternion().setFromEuler(new THREE.Euler(-0.15, 0.28, 0.42)),
  scale: { x: 1, y: 1, z: 1 }
};
const referenceMatrices = viewport3d.computeHurtboxBindingReferenceMatrices(THREE, referenceParent, referencePose, "");
const expectedLocal = new THREE.Matrix4().compose(
  new THREE.Vector3(referencePose.position.x, referencePose.position.y, referencePose.position.z),
  new THREE.Quaternion(referencePose.rotation.x, referencePose.rotation.y, referencePose.rotation.z, referencePose.rotation.w),
  new THREE.Vector3(1, 1, 1));
const expectedFinal = referenceParent.clone().multiply(expectedLocal);
assert(referenceMatrices.referenceMatrix.equals(referenceParent)
  && referenceMatrices.finalMatrix.elements.every((value, index) => Math.abs(value - expectedFinal.elements[index]) < 0.000001),
  "Hurtbox binding reference matrices should preserve the effective parent frame and compose parent x localPose under rotated non-unit parents.");
const adaptedReferenceMatrices = viewport3d.computeHurtboxBindingReferenceMatrices(
  THREE,
  new THREE.Matrix4(),
  { position: { x: 1, y: 2, z: 3 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, scale: { x: 1, y: 1, z: 1 } },
  "unityGltfPreviewMirrorX");
const adaptedPosition = new THREE.Vector3().setFromMatrixPosition(adaptedReferenceMatrices.finalMatrix);
assert(adaptedPosition.x === -1 && adaptedPosition.y === 2 && adaptedPosition.z === 3,
  "Hurtbox binding reference composition should use the same Unity-to-GLB pose adapter as the final collider preview.");
const resolvedBinding = viewport3d.getHurtboxBindingReferenceStatus(
  { localPose: { parentKind: "Bone", parentPath: "bone.head" } },
  { bone: {}, record: { name: "Head", path: "Armature/Spine/Head" } });
const unresolvedBinding = viewport3d.getHurtboxBindingReferenceStatus(
  { localPose: { parentKind: "Bone", parentPath: "bone.missing" } },
  null);
assert(resolvedBinding.resolved && resolvedBinding.resolvedPath === "Armature/Spine/Head"
  && !unresolvedBinding.resolved && unresolvedBinding.reason.includes("bone.missing"),
  "Hurtbox binding reference status should report the exact resolved bone path without falling back unresolved Bone bindings to ModelRoot.");
const bindingParentBone = new THREE.Bone();
bindingParentBone.name = "neck";
const bindingBone = new THREE.Bone();
bindingBone.name = "head";
bindingBone.position.set(0, 1, 0);
bindingParentBone.add(bindingBone);
const chainChild = new THREE.Bone();
chainChild.name = "face_chain";
chainChild.position.set(0.1, 1, 0);
bindingBone.add(chainChild);
const longSideBranch = new THREE.Bone();
longSideBranch.name = "horn_long";
longSideBranch.position.set(5, 0, 0);
bindingBone.add(longSideBranch);
bindingParentBone.updateMatrixWorld(true);
const selectedBindingNeighbors = viewport3d.selectHurtboxBindingReferenceNeighbors(THREE, bindingBone);
assert(selectedBindingNeighbors.parent === bindingParentBone
  && selectedBindingNeighbors.primaryChild === chainChild,
  "Hurtbox binding references should select chain continuity over a longer side branch and never render every direct child.");
const currentBodyPresenceState = globalThis.normalizeBodyPresenceApiState({
  state: "Current",
  writeEligible: true,
  packageRevision: "sha256:current",
  document: package000003BodyPresence
});
const futureBodyPresenceState = globalThis.normalizeBodyPresenceApiState({
  state: "UnsupportedSchema",
  writeEligible: false,
  rawPayload: "{ future: true }",
  packageRevision: "sha256:future"
});
assert(currentBodyPresenceState?.state === "Current"
  && currentBodyPresenceState.writeEligible
  && currentBodyPresenceState.document?.volumes?.length === 3,
  "CharacterStudio should project the dedicated Current Body Presence API state and all three Dragon14 volumes.");
assert(futureBodyPresenceState?.state === "UnsupportedSchema"
  && !futureBodyPresenceState.writeEligible
  && futureBodyPresenceState.rawPayload === "{ future: true }",
  "CharacterStudio should preserve the dedicated future-schema API state as read-only raw content.");
assert(package000003BodyPresence.format === "mx.characterBodyPresence.v2"
  && package000003BodyPresence.schemaVersion === "2.0"
  && package000003BodyPresence.volumes.map(volume => volume.volumeId).join("|")
    === "presence.000003.core.neck|presence.000003.core.tail_root|presence.000003.core.torso"
  && package000003BodyPresence.animatedHurtboxSources.map(source => source.colliderId).sort().join("|")
    === "hurt.head.obb.0|hurt.tail_mid.capsule.0|hurt.tail_tip.capsule.0",
  "Dragon14 Body Presence v2 should expose the three core volumes and three stable animated Hurtbox references.");
assert(appSource.includes('node("geometry/body_presence", "bodyPresence"')
  && appSource.includes("Body Presence · Current")
  && appSource.includes("Body Presence · Missing")
  && appSource.includes("Future / UnsupportedSchema")
  && appSource.includes("Body Presence 当前为只读状态，未发送保存请求。")
  && appSource.includes("/api/character/body-presence/save?package=")
  && appSource.includes("expectedRevision"),
  "CharacterStudio should display all Body Presence states and save only Current through the dedicated revision-gated API.");
assert(appSource.includes("bodyPresenceAnimatedSource")
  && appSource.includes("animatedHurtboxColliderOptions")
  && appSource.includes("添加 Animated Hurtbox source")
  && appSource.includes("只引用现有 Hurtbox；shape/part/bone/track 仍由 collider 权威持有。"),
  "CharacterStudio should edit fixed v2 animated Hurtbox references without copying collider geometry.");
assert(apiSource.includes("snapshot?.source !== \"dedicatedApi\"")
  && apiSource.includes('snapshot.state !== "Current"')
  && !apiSource.includes("bodyPresenceDirty"),
  "CharacterStudio should fail closed for fallback/future/invalid Body Presence without adding it to the whole-package draft writer.");
assert(editorServerSource.includes("TryHandleCharacterBodyPresenceApi")
  && editorServerSource.includes("PreservePersistedBodyPresence(persisted, package)")
  && editorServerBodyPresenceSource.includes('path == "/api/character/body-presence"')
  && editorServerBodyPresenceSource.includes('path != "/api/character/body-presence/save"')
  && editorServerBodyPresenceSource.includes("private static void PreservePersistedBodyPresence"),
  "Authoring service should expose the dedicated Body Presence API and keep whole-package save from becoming a second writer.");
const currentBodyPartDamageState = globalThis.normalizeBodyPartDamageApiState({
  state: "Current",
  writeEligible: true,
  packageRevision: "sha256:body-part-current",
  contentHash: "sha256:000003-body-part-damage",
  document: package000003BodyPartDamage
});
const futureBodyPartDamageState = globalThis.normalizeBodyPartDamageApiState({
  state: "Future",
  writeEligible: false,
  rawPayload: "{\n  \"format\": \"mx.characterBodyPartDamage.v2\",\n  \"future\": true\n}\n",
  packageRevision: "sha256:body-part-future"
});
assert(currentBodyPartDamageState?.state === "Current"
  && currentBodyPartDamageState.writeEligible
  && currentBodyPartDamageState.document?.partStates?.length === 4,
  "CharacterStudio should project Dragon14's four Current body-part-damage definitions from the T1 service response.");
assert(futureBodyPartDamageState?.state === "Future"
  && !futureBodyPartDamageState.writeEligible
  && futureBodyPartDamageState.rawPayload.includes("mx.characterBodyPartDamage.v2"),
  "CharacterStudio should keep future body-part-damage payloads raw and read-only.");
assert(package000003BodyPartDamage.format === "mx.characterBodyPartDamage.v1"
  && package000003BodyPartDamage.schemaVersion === "1.0"
  && package000003BodyPartDamage.partStates.map(item => item.partStateId).join("|")
    === "partstate.000003.head|partstate.000003.left_wing|partstate.000003.right_wing|partstate.000003.tail"
  && package000003BodyPartDamage.partStates.flatMap(item => item.memberPartIds).length === 10
  && package000003BodyPartDamage.partStates.slice(0, 3).every(item => item.severable !== true)
  && package000003BodyPartDamage.partStates[3].severable === true
  && package000003BodyPartDamage.partStates[3].severRequiredTag === "action.000003.tail.sever"
  && package000003BodyPartDamage.partStates[3].presentationEventId === "event.000003.tail.severed",
  "Dragon14 should retain four canonical groups while enabling severance only for the tail.");
const previousBodyPartDamageSmokeState = globalThis.state;
globalThis.state = {
  bodyPartDamage: JSON.parse(JSON.stringify(currentBodyPartDamageState)),
  bodyPartDamageDirty: false,
  cleanBodyPartDamageJson: globalThis.serializeBodyPartDamageDocument(currentBodyPartDamageState)
};
assert(!globalThis.refreshBodyPartDamageDirtyFromCleanSnapshot(),
  "A reopened body-part-damage document should start clean.");
globalThis.state.bodyPartDamage.document.partStates.reverse();
assert(globalThis.refreshBodyPartDamageDirtyFromCleanSnapshot(),
  "Draft-only body-part reorder should participate in independent dirty tracking until canonical save/reopen.");
globalThis.state = previousBodyPartDamageSmokeState;
assert(appSource.includes('node("geometry/body_part_damage", "bodyPartDamage"')
  && appSource.includes('data-body-part-damage-action="create"')
  && appSource.includes('data-body-part-damage-action="add"')
  && appSource.includes('data-body-part-damage-action="delete"')
  && appSource.includes('data-body-part-damage-action="move-up"')
  && appSource.includes('data-body-part-damage-action="move-down"')
  && appSource.includes("保存后会按 <code>partStateId</code> 规范化排序")
  && !appSource.includes("partStateOrder"),
  "CharacterStudio should create/edit/delete and draft-reorder definitions while explicitly leaving persistent order to T1 canonical partStateId sorting.");
assert(["memberPartIds", "damageZoneId", "woundCapacity", "openDamageScaleMilli",
  "focusConsumeTag", "maxIntegrity", "integrityDamageScaleMilli", "breakReactionId",
  "severable", "severRequiredTag", "presentationEventId",
  "authoredPriorityDeltaOpen", "authoredPriorityDeltaBroken"]
  .every(fieldName => appSource.includes(`field(\"${fieldName}\"`)
    || appSource.includes(`tagsField(\"${fieldName}\"`)),
  "CharacterStudio should expose the M2 fields plus the minimal optional sever extension without a second schema.");
assert(appSource.includes("只有帧开始已 Broken 且收到匹配 typed fact")
  && appSource.includes("不从 damage tag 猜测")
  && appSource.includes("一次性 Severed presentation 事件身份"),
  "CharacterStudio should describe the fixed typed sever policy without adding a user-authored policy graph.");
assert(appSource.includes("bodyPartDamageMemberOptions")
  && appSource.includes("已属于 ${owner}")
  && appSource.includes("disabled: Boolean(option.disabled)")
  && appSource.includes('option.disabled ? " disabled" : ""')
  && appSource.includes('option.disabled ? " is-disabled" : ""'),
  "Member mapping should preserve disabled option metadata so BodyParts owned by another state are visibly unavailable.");
assert(bodyPartDamageValidatorSource.includes('BodyPartDamageEmptyDefinitions = "CHARBPD016_EMPTY_DEFINITIONS"')
  && bodyPartDamageValidatorSource.includes("definitions.Count == 0")
  && bodyPartDamageTestsSource.includes("Current zero-definition body-part-damage must fail closed")
  && appSource.includes("Current 文档至少需要 1 个 part state")
  && appSource.includes("state.bodyPartDamageValidation?.issues")
  && appSource.includes('String(issue?.code || issue?.gate || "Validation")')
  && apiSource.includes("state.bodyPartDamageValidation = error?.validation || null"),
  "Current body-part damage should require one definition; T1 save should return a typed blocker that CharacterStudio renders without preventing draft deletion.");
assert(apiSource.includes("/api/character/body-part-damage?package=")
  && apiSource.includes("/api/character/body-part-damage/save?package=")
  && apiSource.includes("saveBodyPartDamageDocument")
  && apiSource.includes("savePackage(options = {})")
  && apiSource.includes("geometry/body_part_damage.json")
  && editorServerSource.includes("TryHandleCharacterBodyPartDamageApi")
  && editorServerBodyPartDamageSource.includes("PreservePersistedBodyPartDamage")
  && editorServerBodyPartDamageSource.includes("WriteBodyPartDamageJsonAtomic"),
  "CharacterStudio should save body_part_damage only through the existing T1 revision-gated transaction while whole-package save preserves that file.");
assert(appSource.includes("future format / schema")
  && appSource.includes("不提供创建、编辑或保存入口")
  && sessionStateSource.includes("bodyPartDamage: state.bodyPartDamageDirty ? clone(state.bodyPartDamage) : null")
  && sessionStateSource.includes("bodyPartDamageDirty: Boolean(state.bodyPartDamageDirty)")
  && sessionStateSource.includes("部位伤害文档")
  && contextPanelsSource.includes("beforeBodyPartDamage")
  && contextPanelsSource.includes("afterBodyPartDamage")
  && contextPanelsSource.includes("|| bodyPartDamageDirty || weaponLibraryDirty")
  && apiSource.includes("部位伤害草稿"),
  "Future raw safety, independent Undo/Redo, refresh recovery, explicit discard, and compile/import dirty gates should cover body-part damage.");
assert(stylesSource.includes(".body-part-damage-list")
  && stylesSource.includes("@media (max-width: 900px)")
  && stylesSource.includes(".choice.is-disabled"),
  "Body-part damage should have standard and narrow layouts plus a visible disabled-member state.");
assert(appSource.includes("COMMON_HIT_ZONE_IDS") && appSource.includes("hitZoneField(\"hitZoneId\"") && !appSource.includes("datalistField(\"hitZoneId\""), "Collider hit zone should be selected from finite authoring options instead of free text");
assert(appSource.includes('node("geometry/damage_zones", "damageZones"')
  && appSource.includes('tagsField("hitZoneIds", "Hit Zones", hitZoneOptions()')
  && appSource.includes('data-damage-zone-action="enable"')
  && appSource.includes('data-damage-zone-action="delete"'),
"CharacterStudio should provide explicit DamageZone enable, membership editing, add, and delete flows.");
assert(appSource.includes("DamageZone 文档只读")
  && appSource.includes("damageZonesRawPayload")
  && appSource.includes("normalizeDamageZonesDocumentState")
  && appSource.includes("UnsupportedSchema")
  && appSource.includes("getDamageZoneItems"),
"CharacterStudio should preserve unsupported or invalid DamageZone documents as lossless read-only payloads.");
assert(appSource.includes('node("geometry/engagement_anchors", "engagementAnchors"')
  && appSource.includes("data-engagement-anchor-action=\"enable\"")
  && appSource.includes("data-engagement-anchor-action=\"add\"")
  && appSource.includes("data-engagement-anchor-action=\"duplicate\"")
  && appSource.includes("data-engagement-anchor-action=\"move-up\"")
  && appSource.includes("data-engagement-anchor-action=\"move-down\"")
  && appSource.includes("data-engagement-anchor-action=\"delete\"")
  && appSource.includes('field("zoneId", { label: "Damage Zone", type: "select", options: approachableDamageZoneOptions()')
  && appSource.includes('field("referenceFrame", { label: "Reference Frame", type: "select", options: ["ModelRoot"]'),
"CharacterStudio should provide explicit EngagementAnchor enable, binding, add, copy, ordering, and delete flows with a ModelRoot-only v1 frame.");
assert(appSource.includes("接战锚点文档只读")
  && appSource.includes("engagementAnchorsRawPayload")
  && appSource.includes("normalizeEngagementAnchorsDocumentState")
  && appSource.includes("getEngagementAnchorItems"),
"CharacterStudio should preserve unsupported or invalid EngagementAnchor documents as lossless read-only payloads.");
assert(indexSource.includes('data-layer="engagementAnchors"')
  && appSource.includes("addEngagementAnchorMeshes")
  && appSource.includes('transformKind: "engagementAnchor"')
  && appSource.includes("commitEngagementAnchorTransformTarget"),
"CharacterStudio should visualize and directly move or yaw-edit ModelRoot engagement anchors in the 3D viewport.");
assert(sessionStateSource.includes("createCharacterStudioDraftSnapshot")
  && appSource.includes("beginUndoTransaction(`接战锚点 ${action}`")
  && appSource.includes("refreshDirtyFromCleanSnapshot()"),
"EngagementAnchor edits should inherit package draft, undo, and browser-reload preservation.");
assert(sessionStateSource.includes("package: state.dirty ? clone(state.package) : null")
  && sessionStateSource.includes("undoStack: documentHistoryDirty ? clone(state.undoStack || []) : []"),
"DamageZone edits should inherit package draft and undo preservation across refresh/reload.");
assert(appSource.includes("hitZoneField(\"defaultHitZoneId\"") && appSource.includes("reactionGroupField(\"reactionGroupId\""), "Body part hit zone and reaction group fields should use select controls");
assert(appSource.includes("const spec = selectField(path, label, bonePathOptions()") && appSource.includes("const spec = selectField(path, label, locatorPathOptions()"), "Bone and locator reference fields should use dropdown options");
assert(appSource.includes("function colliderShapeFields") && appSource.includes('if (shape === "Sphere")') && appSource.includes('if (shape === "Box")'), "Collider inspector should show dimension fields from the shape field, not collider names");
assert(appSource.includes("normalizeColliderShapeDimensions(target.value)") && appSource.includes("path === \"shape\"") && appSource.includes("collider.height = Math.max"), "Collider shape changes should repair missing shape-specific dimensions");
assert(appSource.includes('identityField("colliderId"') && appSource.includes("colliderIdSuggestions(value)"), "Collider inspector should expose editable collider ids");
assert(appSource.includes("normalizeColliderIdAfterEdit") && appSource.includes("buildColliderAutoId(collider.partId, collider.shape)") && appSource.includes("hasColliderShapeSuffix"), "Collider ids should auto-align generated names with part id and shape");
assert(appSource.includes("getMutableSocketPose") && appSource.includes("addSocketMeshes(THREE, content, pickables, sockets, bodyBoneRecords = [], registerSelectionVisual = null, registerTransformTarget = null, poseAdapter = UNITY_GLTF_PREVIEW_POSE_ADAPTER)"), "Socket preview meshes should bind editable localPose data");
assert(appSource.includes('field("abstractBoneId"') && appSource.includes("getAbstractBoneLocalDiagnostics") && appSource.includes("CHARPKG_REQUIRED_ABSTRACT_BONE_MISSING"), "Socket Inspector should edit and locally validate abstract-bone semantics.");
assert(appSource.includes("ABSTRACT_BONE_ID_OPTIONS") && appSource.includes("AbstractBone_${semantic}"), "CharacterStudio should expose core abstract-bone ids and semantic viewport markers.");
assert(appSource.includes("getSocketBoneParentCandidates") && appSource.includes("socket?.bonePath") && appSource.includes("getSocketBoneParentCandidates(socket)"), "Socket preview should resolve bone parents with Unity prefab-builder priority");
assert(appSource.includes("`socket_${socket.socketId}_parent`, getSocketBoneParentCandidates(socket), { inheritScale: false, poseAdapter }"), "Socket preview meshes should not inherit imported bone scale");
assert(appSource.includes("function findBoneRecordForPose") && appSource.includes("bonePathCandidates"), "CharacterStudio should resolve bone poses from ordered candidates");
assert(appSource.includes("UNITY_GLTF_PREVIEW_POSE_ADAPTER") && appSource.includes("convertUnityPoseToPreviewPose") && appSource.includes("readObjectPoseForAdapter"), "CharacterStudio should convert Unity-authored poses into Three.js GLB preview space and back");
assert(appSource.includes("position.x = -position.x") && appSource.includes("quaternion.set(quaternion.x, -quaternion.y, -quaternion.z, quaternion.w)"), "Unity/GLB preview pose conversion should mirror X position and Y/Z rotation channels");
assert(appSource.includes("poseAdapter: UNITY_GLTF_PREVIEW_POSE_ADAPTER"), "Geometry transform targets should carry the Unity-to-GLB preview pose adapter");
assert(appSource.includes("FBXLoader.js") && appSource.includes("FBXLoader: fbxModule.FBXLoader"), "CharacterStudio should load Three.js FBXLoader for direct FBX preview");
assert(appSource.includes('../../node_modules/three/build/three.module.js'), "CharacterStudio split viewport module should resolve Three.js from the package root node_modules");
assert(!appSource.includes('import("../node_modules/three/build/three.module.js")'), "CharacterStudio split viewport module should not resolve Three.js under web/node_modules");
assert(appSource.includes("function getPreviewModelFormat") && appSource.includes('cleanPath.endsWith(".fbx")') && appSource.includes('cleanPath.endsWith(".glb")'), "CharacterStudio should recognize FBX and GLB/GLTF preview model formats");
assert(appSource.includes("function resolvePreviewModelRelativePaths") && appSource.includes("buildSiblingPreviewModelPaths") && appSource.includes("findCatalogPreviewModelPaths"), "CharacterStudio body preview should resolve direct FBX previews and sibling preview artifacts");
assert(appSource.includes("function shouldPreferSourceFbxPreview") && appSource.includes("buildSourceFbxPreviewPaths(resource, directPath)") && appSource.includes("tags.includes(\"converted-from-fbx\")"), "CharacterStudio should prefer source FBX previews for resources converted from FBX before falling back to generated GLB");
assert(appSource.includes("function addPreviewModelResource") && appSource.includes("function loadPreviewModel") && appSource.includes('format === "fbx"'), "CharacterStudio should load preview models through a format-dispatching loader");
assert(appSource.includes("function getDefaultPoseAdapterForPreviewFormat") && appSource.includes('format === "gltf" || format === "fbx" ? UNITY_GLTF_PREVIEW_POSE_ADAPTER : ""'), "FBX and glTF previews should apply the Unity-authored MirrorX pose adapter");
assert(appSource.includes("fitHeightMeters: geometry.bodyProfile?.heightMeters") && appSource.includes("function applyPreviewModelUnitScale") && appSource.includes('format !== "fbx"') && appSource.includes("previewUnitWrapper.userData.previewUnitScale"), "CharacterStudio should normalize oversized FBX body previews through an internal preview unit wrapper");
assert(appSource.includes("formatSink: format => { viewportPoseAdapter = getDefaultPoseAdapterForPreviewFormat(format); }") && appSource.includes("addColliderMeshes(THREE, content, pickables, visibleColliders, bodyBoneRecords, registerSelectionVisual, registerTransformTarget, viewportPoseAdapter)"), "CharacterStudio should render filtered sockets/colliders in the active model preview coordinate space");
assert(appSource.includes('if (target.kind === "socket") return `${path}#localPose`;') && !appSource.includes('state.viewportMode !== "weapon" || !entry?.key'), "Socket selections should support viewport TransformControls outside weapon mode");
assert(appSource.includes("isViewportTransformHandleActive(transformControls)") && appSource.includes("transformControls?.dragging || transformControls?.axis"), "Viewport selection should ignore pointerdown events already captured by transform gizmos");
const transformCommitStart = appSource.indexOf("function commitWeaponTransformTarget");
const transformCommitEnd = appSource.indexOf("function setWeaponTransformPick", transformCommitStart);
const transformCommitBody = appSource.slice(transformCommitStart, transformCommitEnd);
assert(transformCommitStart >= 0 && transformCommitEnd > transformCommitStart, "smoke should locate commitWeaponTransformTarget");
assert(transformCommitBody.includes("refreshViewportLiveData();"), "TransformControls commits should refresh linked viewport pose bindings");
assert(transformCommitBody.includes("readObjectPoseForAdapter(THREE, target.object, target.poseAdapter)"), "TransformControls commits should write back preview-space transforms through the pose adapter");
assert(transformCommitBody.includes('applySymmetryEdit(findTarget(target.objectPath || ""), posePath'), "TransformControls commits should apply symmetry edit writeback when enabled");
assert(appSource.includes("beginUndoTransaction(`Gizmo ${target.label}`") && appSource.includes("commitWeaponTransformTarget(THREE, target, { live: true })"), "Gizmo drags should create one transaction while live object changes only update the active target");
assert(appSource.includes("未绑定攻击轨迹"), "CharacterStudio should report weapons without trace bindings as a visible authoring hint");
const normalizeWeaponPreviewPoseStart = appSource.indexOf("function normalizeWeaponPreviewPose");
const normalizeWeaponPreviewPoseEnd = appSource.indexOf("function renderWeaponFocusOverlayHtml", normalizeWeaponPreviewPoseStart);
assert(normalizeWeaponPreviewPoseStart >= 0 && normalizeWeaponPreviewPoseEnd > normalizeWeaponPreviewPoseStart, "smoke should locate normalizeWeaponPreviewPose");
const normalizeWeaponPreviewPoseBody = appSource.slice(normalizeWeaponPreviewPoseStart, normalizeWeaponPreviewPoseEnd);
assert(normalizeWeaponPreviewPoseBody.includes('previewPose.parentKind === "Socket"')
  && normalizeWeaponPreviewPoseBody.includes("previewPose.parentPath === weapon?.attachSocketId")
  && normalizeWeaponPreviewPoseBody.includes("previewPose.parentPath === weapon?.equipSlot")
  && normalizeWeaponPreviewPoseBody.includes('previewPose.parentKind = "ModelRoot";')
  && normalizeWeaponPreviewPoseBody.includes('previewPose.parentPath = "";'),
  "Focused weapon trace normalization should convert only the current weapon socket into weapon-local ModelRoot space.");
assert(appSource.includes("Unity 使用 wrapper Prefab；CharacterStudio 同步归零源模型根姿态并应用同一份 modelWrapperPose。"),
  "Public weapon wrapper hint should explain the Unity prefab versus CharacterStudio raw-model preview equivalence.");
assert(appSource.includes("武器挂接只选择攻击轨迹几何；动作窗口只启用或禁用命中检测。")
  && appSource.includes('renderWeaponDataValue("攻击轨迹"'),
  "CharacterStudio weapon focus overlay should expose attachment, trace geometry, and action-window responsibility boundaries.");

const bonePickStart = appSource.indexOf("function applyBonePick");
const bonePickEnd = appSource.indexOf("function getBoneValueForField", bonePickStart);
const bonePickBody = appSource.slice(bonePickStart, bonePickEnd);
assert(bonePickStart >= 0 && bonePickEnd > bonePickStart, "smoke should locate applyBonePick");
assert(bonePickBody.includes("applySymmetryEdit(target, fieldPath)"), "Bone picker edits should also apply symmetry writeback when enabled");
assert(appSource.includes('data-bone-picker-search')
  && appSource.includes('data-bone-picker-zoom="in"')
  && appSource.includes('data-bone-picker-view="reset"')
  && appSource.includes('canvas.addEventListener("pointermove"')
  && appSource.includes("offsetZ * sinYaw")
  && appSource.includes('class="bone-picker-results"')
  && appSource.includes("const width = 640;"),
  "Bone picker should provide a large searchable, zoomable graph with a selectable results list.");
const layoutBonePickerStart = appSource.indexOf("function layoutBonePicker");
const layoutBonePickerEnd = appSource.indexOf("function hasBonePosition", layoutBonePickerStart);
const layoutBonePickerBody = appSource.slice(layoutBonePickerStart, layoutBonePickerEnd);
assert(layoutBonePickerStart >= 0 && layoutBonePickerEnd > layoutBonePickerStart, "smoke should locate the bone-picker layouts");
assert(!layoutBonePickerBody.includes("slice(0,"), "Bone picker graph and tree layouts should include every available bone without count truncation.");
assert(appSource.includes("const searchResults = bones.filter(matchesSearch);") && !appSource.includes("bones.filter(matchesSearch).slice(0,"), "Bone picker results should include every matching bone without count truncation.");
assert(stylesSource.includes("position: fixed;")
  && stylesSource.includes("z-index: 10000;")
  && stylesSource.includes("width: calc(100vw - 20px);")
  && stylesSource.includes("height: calc(100vh - 20px);")
  && stylesSource.includes(".bone-picker-canvas")
  && stylesSource.includes(".bone-picker-result-list"),
  "Bone picker should render as a viewport-sized modal workspace instead of a narrow inspector popover.");
assert(sessionStateSource.includes('"bonePickerSearchQuery"') && sessionStateSource.includes('"bonePickerZoom"') && sessionStateSource.includes('"bonePickerYaw"') && sessionStateSource.includes('"bonePickerPitch"'), "Bone picker search, zoom, and 3D view preferences should survive CharacterStudio refreshes.");
assert(appSource.includes("acceptedAsAuthored: true")
  && appSource.includes("body_colliders.json 是唯一权威")
  && appSource.includes("检测和额外确认不参与门禁"),
  "Saved authoritative Hurtbox authoring should enter Unity bake directly; optional confirmation and coverage diagnostics must not gate import.");

const commitStart = appSource.indexOf("function commitInspectorField");
const commitEnd = appSource.indexOf("function readInspectorInputValue", commitStart);
const commitBody = appSource.slice(commitStart, commitEnd);
assert(commitStart >= 0 && commitEnd > commitStart, "smoke should locate commitInspectorField");
assert(commitBody.includes("beginInspectorUndoTransaction(target, path)") && commitBody.includes("updateUndoTransactionAfter();"), "commitInspectorField should update an inspector undo transaction");
assert(commitBody.includes("applySymmetryEdit(target, input.dataset.field)"), "Inspector field commits should apply symmetry edit writeback when enabled");
assert(commitBody.includes("requestViewportUpdate(target, input.dataset.field)"), "commitInspectorField should request viewport updates");
assert(!commitBody.includes("\n  renderViewport();"), "commitInspectorField should not unconditionally rebuild the 3D viewport");

const mainHandSlot = weaponSlots.weaponSlots.find(slot => slot.slotId === "mainHand");
const swordDefinition = globalWeaponLibrary.singleWeaponConfigurations.find(definition => definition.singleWeaponConfigurationId === "sword");
const swordTrace = swordDefinition.traceProfiles.find(trace => trace.traceId === "trace.iron_sword.blade");
assert(mainHandSlot?.attachSocketId === "mainHand", "mainHand character slot should bind to mainHand socket");
assert(getDefinitionResourceKey(swordDefinition) === "weapon.common_weapon_1.model", "sword single-weapon configuration should bind the public common weapon resource");
assert(sockets.sockets.some(socket => socket.socketId === mainHandSlot.attachSocketId), "mainHand character slot Socket should resolve");
assert(swordDefinition.modelPose?.parentKind === "ModelRoot", "single weapon model pose should remain weapon-local");
assert(swordTrace?.startPose?.parentKind === "ModelRoot" && swordTrace?.endPose?.parentKind === "ModelRoot", "single weapon trace endpoints should remain weapon-local");
const offHandSlot = weaponSlots.weaponSlots.find(slot => slot.slotId === "offHand");
assert(offHandSlot?.attachSocketId === "offHand", "offHand character slot should exist");
assert(getDefinitionResourceKey(codexFlowTestShieldDefinition) === "weapon.common_kite_shield.model", "shield single-weapon configuration should bind the public common shield resource");
assert((codexFlowTestShieldDefinition.traceProfiles || []).length === 0, "offHand shield sample should intentionally have no weapon trace for visualization warning coverage");

const modelResources = resources.entries.filter(entry => entry.typeId === "model" && entry.resourceKey);
for (const resource of modelResources) {
  const unityEntry = unityCatalog.entries.find(entry =>
    entry.id === resource.resourceKey
    || entry.providerData?.packageResourceKey === resource.resourceKey
    || entry.providerData?.stableId === resource.stableId);
  assert(unityEntry, `Unity catalog should include ${resource.resourceKey}`);
  assert(unityEntry.unityAssetPath || unityEntry.providerData?.unityAssetPath, `${resource.resourceKey} should expose Unity asset path`);
  assert(unityEntry.importerKind || unityEntry.providerData?.importerKind, `${resource.resourceKey} should expose importer kind`);
  assert(unityEntry.importStatus || unityEntry.providerData?.importStatus, `${resource.resourceKey} should expose import status`);
}

const fallbackPlan = {
  spawnCritical: resources.entries.filter(entry => entry.usage === "characterModel").map(entry => entry.resourceKey),
  equipmentInitial: codexFlowTestResourcePlan.equipmentInitial?.resources?.map(resource => resource.resourceKey) || [],
  animationWarmup: codexFlowTestResourcePlan.animationWarmup?.resources?.map(resource => resource.resourceKey) || [],
  uiDeferred: resources.entries.filter(entry => entry.usage === "previewThumbnail").map(entry => entry.resourceKey)
};
assert(fallbackPlan.spawnCritical.length > 0, "fallback resource plan should include SpawnCritical resources");
assert(fallbackPlan.equipmentInitial.length > 0, "fallback resource plan should include EquipmentInitial resources");
assert(Array.isArray(codexFlowTestResourcePlan.animationWarmup?.resources), "fallback resource plan should preserve the AnimationWarmup group even when the current sample has no warmup resources");
assert(fallbackPlan.uiDeferred.length > 0, "fallback resource plan should include UiDeferred resources");

console.log("CharacterStudio smoke ok");

function readJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(sampleRoot, relative), "utf8"));
}

function readPackageJson(root, relative) {
  return JSON.parse(fs.readFileSync(path.join(root, relative), "utf8"));
}

function readRepoJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(repoRoot, relative), "utf8"));
}

function getAttachmentResourceKey(attachment) {
  return getDefinitionResourceKey(globalWeaponLibrary.singleWeaponConfigurations.find(definition => definition.singleWeaponConfigurationId === attachment?.singleWeaponConfigurationId));
}

function getDefinitionResourceKey(definition) {
  return firstNonEmpty(
    definition?.weaponRef?.runtimeResourceKey,
    definition?.weaponRef?.packageResourceKey,
    definition?.weaponRef?.providerResourceKey);
}

function firstNonEmpty(...values) {
  for (const value of values) {
    if (value == null) continue;
    const text = String(value).trim();
    if (text) return text;
  }
  return "";
}

function readCharacterStudioWebSources() {
  const webRoot = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web");
  const sources = [
    fs.readFileSync(path.join(webRoot, "app.js"), "utf8")
  ];
  const srcRoot = path.join(webRoot, "src");
  if (fs.existsSync(srcRoot)) {
    const sourceFiles = fs.readdirSync(srcRoot)
      .filter(file => file.endsWith(".js"))
      .sort((left, right) => left.localeCompare(right, "en"));
    for (const file of sourceFiles) {
      sources.push(fs.readFileSync(path.join(srcRoot, file), "utf8"));
    }
  }
  return sources.join("\n");
}

function assertCharacterStudioWebModules() {
  const expectedModules = [
    "api.js",
    "characterManager.js",
    "combinedWeaponWorkbench.js",
    "contextPanels.js",
    "importMaterialsPanel.js",
    "inspector.js",
    "resourcePicker.js",
    "singleWeaponWorkbench.js",
    "state.js",
    "utils.js",
    "viewport3d.js"
  ];
  for (const moduleName of expectedModules) {
    const full = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src", moduleName);
    assert(fs.existsSync(full), `CharacterStudio split module should exist: ${moduleName}`);
  }
}

async function assertCodexFlowTestPreviewBoneAliases() {
  const viewport3dPath = path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/viewport3d.js");
  await import(pathToFileURL(viewport3dPath).href);

  const findBoneRecordForValue = globalThis.findBoneRecordForValue;
  const findPreviewBoneBySemantic = globalThis.findPreviewBoneBySemantic;
  const getPreviewBoneSemanticKey = globalThis.getPreviewBoneSemanticKey;
  const getSemanticBoneKey = globalThis.getSemanticBoneKey;
  const getSemanticFrameBoneKey = globalThis.getSemanticFrameBoneKey;
  const isSegmentBoneSemanticKey = globalThis.isSegmentBoneSemanticKey;
  const getSemanticBoneRecordRank = globalThis.getSemanticBoneRecordRank;
  const getWeaponAttachmentIdentity = globalThis.getWeaponAttachmentIdentity;
  assert(typeof findBoneRecordForValue === "function", "CharacterStudio viewport should expose bone record resolver for smoke coverage.");
  assert(typeof findPreviewBoneBySemantic === "function", "CharacterStudio viewport should expose preview semantic resolver for smoke coverage.");
  assert(typeof getPreviewBoneSemanticKey === "function", "CharacterStudio viewport should expose preview semantic key resolver for smoke coverage.");
  assert(typeof getSemanticBoneKey === "function", "CharacterStudio viewport should expose authoring semantic key resolver for smoke coverage.");
  assert(typeof getSemanticFrameBoneKey === "function", "CharacterStudio viewport should expose semantic frame resolver for smoke coverage.");
  assert(typeof isSegmentBoneSemanticKey === "function", "CharacterStudio viewport should expose segment semantic key classification for smoke coverage.");
  assert(typeof getSemanticBoneRecordRank === "function", "CharacterStudio viewport should expose semantic bone ranking for smoke coverage.");
  assert(typeof getWeaponAttachmentIdentity === "function", "CharacterStudio split modules should expose deterministic runtime attachment identity helpers before the preview renders.");

  const records = [
    mockBoneRecord("Hips", "Armature/Hips"),
    mockBoneRecord("Spine", "Armature/Hips/Spine"),
    mockBoneRecord("Spine1", "Armature/Hips/Spine/Spine1"),
    mockBoneRecord("Spine2", "Armature/Hips/Spine/Spine1/Spine2"),
    mockBoneRecord("Head", "Armature/Hips/Spine/Spine1/Spine2/Head"),
    mockBoneRecord("RightShoulder", "Armature/Hips/Spine/Spine1/Spine2/RightShoulder"),
    mockBoneRecord("RightArm", "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm"),
    mockBoneRecord("RightForeArm", "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm"),
    mockBoneRecord("RightHand", "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand"),
    mockBoneRecord("LeftShoulder", "Armature/Hips/Spine/Spine1/Spine2/LeftShoulder"),
    mockBoneRecord("LeftArm", "Armature/Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm"),
    mockBoneRecord("LeftForeArm", "Armature/Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm/LeftForeArm"),
    mockBoneRecord("LeftHand", "Armature/Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm/LeftForeArm/LeftHand"),
    mockBoneRecord("RightUpLeg", "Armature/Hips/RightUpLeg"),
    mockBoneRecord("RightLeg", "Armature/Hips/RightUpLeg/RightLeg"),
    mockBoneRecord("RightFoot", "Armature/Hips/RightUpLeg/RightLeg/RightFoot"),
    mockBoneRecord("LeftUpLeg", "Armature/Hips/LeftUpLeg"),
    mockBoneRecord("LeftLeg", "Armature/Hips/LeftUpLeg/LeftLeg"),
    mockBoneRecord("LeftFoot", "Armature/Hips/LeftUpLeg/LeftLeg/LeftFoot")
  ];

  assertResolvedBone(records, "bone.upper_arm.R", "RightArm");
  assertResolvedBone(records, "bone.upper_arm.L", "LeftArm");
  assertResolvedBone(records, "bone.forearm.R", "RightForeArm");
  assertResolvedBone(records, "bone.forearm.L", "LeftForeArm");
  assertResolvedBone(records, "bone.thigh.R", "RightUpLeg");
  assertResolvedBone(records, "bone.thigh.L", "LeftUpLeg");
  assertResolvedBone(records, "bone.shin.R", "RightLeg");
  assertResolvedBone(records, "bone.shin.L", "LeftLeg");
  assertResolvedBone(records, "bone.hand.R", "RightHand");
  assertResolvedBone(records, "bone.hand.L", "LeftHand");
  assertResolvedBone(records, "bone.foot.R", "RightFoot");
  assertResolvedBone(records, "bone.foot.L", "LeftFoot");
  assertResolvedBone(records, "bone.head", "Head");
  assertResolvedBone(records, "bone.spine", "Spine");

  assert(getSemanticBoneKey("bone.upper_arm.R") === "right_upper_arm", "authoring alias bone.upper_arm.R should resolve to the right upper-arm semantic.");
  assert(getSemanticBoneKey("bone.forearm.R") === "right_forearm", "authoring alias bone.forearm.R should resolve to the right forearm semantic.");
  assert(getSemanticBoneKey("bone.thigh.R") === "right_thigh", "authoring alias bone.thigh.R should resolve to the right thigh semantic.");
  assert(getSemanticBoneKey("bone.shin.R") === "right_shin", "authoring alias bone.shin.R should resolve to the right shin semantic.");
  assert(getPreviewBoneSemanticKey(records.find(record => record.name === "RightShoulder")) === "right_upper_arm", "FBX RightShoulder should resolve to the same upper-arm semantic.");
  assert(getPreviewBoneSemanticKey(records.find(record => record.name === "RightArm")) === "right_upper_arm", "FBX RightArm should resolve to the same upper-arm semantic.");
  assert(getPreviewBoneSemanticKey(records.find(record => record.name === "RightUpLeg")) === "right_thigh", "FBX RightUpLeg should resolve to the same thigh semantic.");
  assert(getPreviewBoneSemanticKey(records.find(record => record.name === "RightLeg")) === "right_shin", "FBX RightLeg should resolve to the same shin semantic.");
  assert(findPreviewBoneBySemantic(records, "right_leg")?.name === "RightUpLeg", "broad preview right_leg should prefer the upper-leg record instead of the shin fallback.");
  assert(getSemanticBoneRecordRank(records.find(record => record.name === "RightArm"), "right_upper_arm") > getSemanticBoneRecordRank(records.find(record => record.name === "RightShoulder"), "right_upper_arm"), "upper-arm semantic aliases should prefer RightArm over the shoulder helper bone when both exist.");
  globalThis.UNITY_GLTF_PREVIEW_POSE_ADAPTER = "unityGltfPreviewMirrorX";
  const rightUpperArmRecord = records.find(record => record.name === "RightArm");
  assert(isSegmentBoneSemanticKey("right_upper_arm") && isSegmentBoneSemanticKey("right_hand"), "CharacterStudio semantic frame should include limb and hand records.");
  assert(!isSegmentBoneSemanticKey("right_leg"), "CharacterStudio semantic frame should not use broad leg fallback keys.");
  assert(getSemanticFrameBoneKey({ parentPath: "bone.upper_arm.R" }, rightUpperArmRecord, { poseAdapter: "" }) === "right_upper_arm", "FBX body preview should use semantic limb frames for upper-arm authoring poses.");
  assert(getSemanticFrameBoneKey({ parentPath: "bone.upper_arm.R" }, rightUpperArmRecord, { poseAdapter: globalThis.UNITY_GLTF_PREVIEW_POSE_ADAPTER }) === "", "GLTF body preview should keep the existing raw pose adapter path.");
}

function mockBoneRecord(name, bonePath) {
  return {
    name,
    path: bonePath,
    alias: `bone.${name}`,
    depth: Math.max(0, String(bonePath || "").split("/").length - 1)
  };
}

function assertResolvedBone(records, alias, expectedName) {
  const record = globalThis.findBoneRecordForValue(records, alias);
  assert(record?.name === expectedName, `CharacterStudio should resolve ${alias} to ${expectedName}, got ${record?.name || "none"}.`);
}

function readGeneratedJson(relative) {
  return JSON.parse(fs.readFileSync(path.join(unityGeneratedRoot, relative), "utf8"));
}

function sourcePathOf(item) {
  return item?.sourcePath || item?.source?.path || "";
}

function assertStarterColliderAliases(collidersDocument, partsDocument, label) {
  const allowed = new Set([
    "bone.hips",
    "bone.spine",
    "bone.head",
    "bone.upper_arm.L",
    "bone.forearm.L",
    "bone.hand.L",
    "bone.upper_arm.R",
    "bone.forearm.R",
    "bone.hand.R",
    "bone.thigh.L",
    "bone.shin.L",
    "bone.foot.L",
    "bone.thigh.R",
    "bone.shin.R",
    "bone.foot.R",
    "Spine1",
    "Head",
    "LeftArm",
    "LeftForeArm",
    "LeftHand",
    "RightArm",
    "RightForeArm",
    "RightHand",
    "LeftUpLeg",
    "LeftLeg",
    "LeftFoot",
    "RightUpLeg",
    "RightLeg",
    "RightFoot"
  ]);
  const legacy = new Set([
    "bone.Head",
    "bone.RightHand",
    "bone.LeftHand",
    "bone.RightLeg",
    "bone.LeftLeg",
    "bone.Chest",
    "bone.Spine",
    "bone.spine002"
  ]);
  assert(Array.isArray(collidersDocument.colliders) && collidersDocument.colliders.length === 10, `${label} should include 10 torso/head/arms/legs collider defaults`);
  for (const collider of collidersDocument.colliders) {
    const parentPath = collider.localPose?.parentPath || "";
    assert(collider.colliderId !== "foot_l_box" && collider.colliderId !== "foot_r_box", `${label} should not include standalone foot colliders`);
    assert(collider.hitZoneId !== "hit.left_foot" && collider.hitZoneId !== "hit.right_foot", `${label} collider ${collider.colliderId} should not use standalone foot hit zones`);
    assert(collider.localPose?.parentKind === "Bone", `${label} collider ${collider.colliderId} should use Bone parentKind`);
    assert(allowed.has(parentPath), `${label} collider ${collider.colliderId} should use starter bone alias, got ${parentPath}`);
    assert(!legacy.has(parentPath), `${label} collider ${collider.colliderId} should not use legacy bone alias ${parentPath}`);
    assert(Math.abs(collider.localPose?.position?.y || 0) < 0.75, `${label} collider ${collider.colliderId} should use bone-local offsets`);
  }
  if (!partsDocument) return;
  assert(Array.isArray(partsDocument.bodyParts) && partsDocument.bodyParts.length >= 12, `${label} should expose starter body parts`);
  for (const part of partsDocument.bodyParts) {
    assert(part.partKind === "Bone", `${label} part ${part.partId} should be bone-bound`);
    assert(allowed.has(part.locatorId), `${label} part ${part.partId} should use starter locator alias, got ${part.locatorId}`);
    assert(!legacy.has(part.locatorId), `${label} part ${part.partId} should not use legacy locator alias ${part.locatorId}`);
    if (part.partId === "foot_l") assert(part.defaultHitZoneId === "hit.left_leg", `${label} left foot part should default to left leg hit zone`);
    if (part.partId === "foot_r") assert(part.defaultHitZoneId === "hit.right_leg", `${label} right foot part should default to right leg hit zone`);
    assert(part.defaultHitZoneId !== "hit.left_foot" && part.defaultHitZoneId !== "hit.right_foot", `${label} should not expose default foot hit zones`);
  }
}

function assert(condition, message) {
  if (!condition) {
    console.error(message);
    process.exit(1);
  }
}
