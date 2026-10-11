# MxFramework CharacterStudio

New character creation and duplication allocate a six-digit string identity from
`Authoring/CharacterPackages/character_id_catalog.json`; the display name is editable and does
not participate in package paths or ResourceKeys. Existing semantic IDs remain legacy-compatible.
Numeric characters also expose an optional `authoring_manifest.json` state for managed Blender,
reference-image, milestone, audit, and provenance files. These files are optional until adopted.

CharacterStudio is the external browser workstation for authoring Character Resource Packages. The C1 MVP is wired to the existing Authoring CLI/Core pipeline instead of reimplementing resource resolution, compilation, diagnostics, or Unity import logic in the UI.

## Character base attributes

`基础属性` edits character-owned HP, Attack, Defense, base cohesion and the three scatter/break recovery parameters. Source is the optional `config/character_attributes.json` schema 1.0, saved through the existing revision-gated package endpoint. Viewing a missing document displays defaults without creating a draft; the first edit and save creates the source. Mass and collision dimensions stay in `身体与受击`. Advanced `角色引用（只读）` shows authored references only, excluding hydrated weapon/animation results. Data ownership and runtime mappings are documented in [`CHARACTER_STUDIO_DATA_OWNERSHIP.md`](../../Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md).

The compiler exports HP/Attack/Defense identities 1/2/3 and preserves Stamina 920002. CharacterTest normalizes legacy imported HP at spawn, initializes scatter settings from the profile, and displays the configured maximum HP. Save and import apply to subsequently created runtime instances.

Checks: `npm run smoke:base-attributes`; Authoring tests selector `character-basic-attributes`.

The attribute workspace keeps the character visible while hiding geometry-editing controls and the binding dock. `显示辅助标记` temporarily reveals helpers without changing layer preferences; other workspaces restore the original layers. Recovery values show their units and simulation-time equivalents at 60Hz. Explicit `weapon.unarmed` slots are valid and require no model attachment; missing configured weapons remain errors. UI checks: `npm run smoke:attribute-workspace`.

## 身体与受击导航

左侧「身体与受击」打开三个入口：「整体身体」查看身体类型、尺寸与质量；「移动碰撞配置」编辑同一个身体源中的默认高度、半径与中心高度，默认对照移动胶囊代理与世界注册盒体；「部位与受击体」并列显示所有部位和当前部位的受击体。选择列表或直接点击预览中的受击体，会自动显示右侧属性、更新当前位置，并保持左侧分类高亮。「返回整体身体」可随时返回。

部位列表包含未配置受击体的部位；「查看全部受击体」也包含没有有效部位引用的条目。独立的「预览范围」只改变可见受击体，保留当前编辑对象，以便左右对照和对称编辑。切换列表保留镜头与列表滚动位置。身体工作区临时隐藏武器、挂点和绑定面板；原有图层和过滤偏好保留。导航路径不创建新数据，字段继续使用既有保存和撤销流程。

「部位与受击体」在列表旁提供「新增部位」和「添加受击体」，并提供「补齐人形受击体」模板入口。新增受击体优先使用当前所选部位的归属、默认命中区和骨骼绑定，缺少骨骼时使用定位点或部位空间；每次新增都使用现有包撤销事务。模板继续调用 `/api/character/humanoid-hurtboxes`，共享模型骨骼就绪与 busy 状态；失败保留在本工作区显示映射和重试，成功保留已有受击体并显示全部模板结果。

中间的「预览范围」是本工作区唯一的显示控制，支持仅选中、当前部位、全部。右侧不再重复显示旧范围按钮，保留聚焦、形状手柄、复制、删除、外扩/收缩和旋转归零。预览范围、创建和模板操作不改写其他工作区的图层及旧过滤偏好。

部位属性按部位信息、层级与骨骼绑定、默认受击规则、高级信息分组；受击体按所属部位与空间绑定、形状尺寸、局部位置与旋转、受击规则、高级信息分组。所属部位是命中归属，空间跟随是姿态父级，两者单独编辑。固定 ID 只读；改变受击体形状或归属不再自动改 ID，已有稳定引用继续保留。源层值原样只读展示（包括 `character.body` 和未记录状态），同时说明当前运行时注册到 `CharacterHurtbox`，不把单选项下拉框当成源字段值。检查：`npm run smoke:body-parts`；模板接口测试使用模拟响应验证前端成功/失败流程，不替代 SDK 模板生成验收。

检查：`npm run smoke:body-workspace`（导航事件、属性跳转、父级高亮、空部位与失效引用、胶囊几何和源数据保持不变）。该检查不代替浏览器实点与视觉验收。

「整体身体」优先显示「尺寸与质量」，随后为「身体结构」，根节点和物理引用收进可记忆展开状态的「高级绑定」。身体配置 ID 只读；`bodyScale` 保留原值并标为未启用、只读，因为当前预览和 CharacterTest 消费路径没有使用它做缩放。尺寸与质量字段附有中文说明和独立单位。

默认显示可关闭的高度标尺、半径参考圆与地面 `Y = 0`；3D 和 SVG 降级视图都提供对应尺寸信息。辅助线使用 `heightMeters` / `radiusMeters` 的声明尺寸，不替代默认胶囊，也不修改模型、武器或部位受击体。质量只更新说明，不改变模型外观。辅助线开关保留镜头，更新时释放旧辅助几何与文字纹理。`npm run smoke:body-overview` 检查分组、只读字段、单位、折叠状态、米制几何、源数据隔离与辅助线开关。

「移动碰撞配置」保留 `geometry/body/capsule` 导航路径和 `bodyProfile.defaultCapsule` 数据身份。青色胶囊表示默认站立移动代理，紫色盒体表示注册到物理世界的动态阻挡体及代理包围盒，两者默认同时显示；注册盒体可独立隐藏。「显示源胶囊参考」可额外叠加橙色源形状。源尺寸无法组成胶囊时明确提示并不绘制该参考，不在预览中擅自撑大高度。右侧对照配置值与站立参数，分别说明代理和注册体的用途。X/Z 只读保留在「高级源信息」；非零偏移的已有 Authoring 警告继续有效。中心高度为非正数时使用自动初值，随后受站立范围限制；半径过大时游戏缩小半径。蹲伏、动作和运行中覆盖不在默认站立预览范围。

当前移动求解按目标形状分支：AABB 障碍使用角色包围盒扫掠；OBB 障碍使用胶囊代理 cast；球体/胶囊目标使用水平圆形判定与高度重叠。注册体是 AABB 不等于所有移动分支都按盒体求解，存在胶囊代理也不等于所有分支都已统一为胶囊检测。界面提供可展开的分支说明，并明确提示这一边界。

「底部对齐地面」仅写入中心 Y，使用已有包撤销事务；已对齐时不重复写入。尺寸编辑、源参考和注册盒体开关可刷新预览而保留模型与镜头。`npm run smoke:movement-collision` 对照 `scripts/fixtures/movement-collision-runtime.json` 中 2026-09-13 从 Unity 读取的 8 组站立尺寸结果，并验证代理/注册体/源形状的区分、分支说明、源值保留、只读偏移、无效胶囊处理、对齐撤销和独立视图开关。8 组样本只验证尺寸与中心计算，不验证移动扫掠，也不能证明不同障碍的碰撞行为已经统一。

## Launch

Authoring server is required for save, compile, source model import, and Unity import. The one-command launch scripts run the server, check the local environment, and open CharacterStudio in the browser.

Package load responses include a package `revision` and a global-library revision. Save and the top-level `迁移` action use optimistic revision gates; Character Application 6.0 → 7.0 and Global Weapon Library 4.0 → 5.0 migration checks both revisions, backs up both files, promotes character-local combinations into the public library, creates character bindings, and rolls both files back on failure. Legacy ability IDs and slot bindings become semantic entries; non-empty removal lists require manual resolution. Conflicts remain editable drafts and block compile/import. Future schemas are never downgraded.

The top-level `刷新状态` action reloads external readiness, resource-plan, Unity/import, package and library state while restoring the current in-memory draft, undo history, selection, workbench, viewport camera, folds and scroll position. If a disk revision changed, the local draft remains authoritative in the editor and the conflict is reported instead of silently replacing it. `放弃改动并重载` is the only top-level destructive reload and requires confirmation. Unsaved package, global-library and source-manifest drafts are mirrored to `sessionStorage` for recovery after a browser reload; UI position is stored separately in `localStorage`. Neither store is a versioned authoring asset, and a successful save or explicit discard clears the temporary draft.

## Humanoid eleven-volume Hurtbox template

The package sidebar exposes **一键添加人形受击体（11 体）** after the character model preview is ready. The template contains head, chest, pelvis/lower torso, and left/right upper arm, forearm, thigh and calf capsules. Hands and feet are endpoint bones, covered approximately by the forearm/calf volumes. Radii use skeletal proportions, not mesh fitting; inspect and adjust coverage before saving.

Generation uses reference-pose bone frames captured before preview posing/animation, in metres and the same scale-free parent coordinates as the existing collider editor. `POST /api/character/humanoid-hurtboxes` delegates to the Unity-independent `CharacterHumanoidHurtboxTemplate` in Authoring Core and returns draft additions only. Concrete existing part bindings take precedence; known `bone.<semantic>` aliases and exact common Humanoid/Mixamo/project names are supported. Missing, ambiguous, duplicate or degenerate bindings reject the whole addition and expose editable bone mappings. An unknown geometry schema remains read-only.

New geometry receives stable `humanoid11.<partId>` collider IDs and distinct `hit.humanoid11.<partId>` hit zones, with damage/impact overrides of 1. Existing colliders on the same template `partId` retain their shapes and rules. Repeated generation only fills missing template parts; it does not reduce an existing character to eleven volumes or delete custom geometry. The 32-collider limit is checked before applying additions. The entire insertion uses one existing Undo/Redo transaction and the normal revision-gated package save, compile and Unity import/bake workflow. No runtime collision, hit-count or damage policy is changed.

Focused Core regression check: `dotnet run --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests -- humanoid-hurtboxes` from the repository root.

## Body Presence (M0)

CharacterStudio displays the optional `geometry/body_presence.json` as Missing, Current, Future, or Invalid and lists the current stable core volumes. Current is saved only through `POST /api/character/body-presence/save` with the package revision returned by `GET /api/character/body-presence`; Future/Invalid payloads remain raw read-only, and `/api/character/save` preserves the independent file. This M0 surface is authoring/diagnostic only and does not enable playable pushing.

## Body Part Damage (LCC-M2)

CharacterStudio displays the optional `geometry/body_part_damage.json` through the existing T1 service. A Current document supports definition create/edit/delete, BodyPart member mapping, every T1 threshold/scale field, validation, Undo/Redo, and draft-only up/down reordering. Current requires at least one part-state definition: deleting the last card may remain an editable draft, but the T1 typed validation blocks save and the returned issue is displayed. Reordering is an editing aid: `POST /api/character/body-part-damage/save` validates and reopens the document in canonical `partStateId` order, so user-defined list order is not a persisted contract. A BodyPart already owned by another definition is visibly disabled instead of being silently duplicated.

The body-part document has independent dirty/revision state but no second formal writer. Both the dedicated save action and top-level package save route body-part changes through the T1 revision-gated transaction; `/api/character/save` preserves the independently persisted file. Ordinary `刷新状态` and browser reload restore the draft, Undo/Redo history, selection, folds and scroll position. Only confirmed `放弃改动并重载` clears the draft. Missing state requires explicit creation, while Future/Invalid payloads show the exact raw content read-only and expose no create/edit/save control.

## Weapon Configuration 7.0

The normative contract is [`Docs/CHARACTER_WEAPON_CONFIGURATION.md`](../../Docs/CHARACTER_WEAPON_CONFIGURATION.md). Global Library 6.0 contains full single-weapon records, public combined configurations, animation, frozen compatibility ability data, reaction configurations, and public action-role defaults. Character Application 8.0 stores public-combination bindings plus optional per-field behavior and whole-action overrides. Profile and ActionSet ids are compiler-generated; action slots select references from the Common or current-character source library.

Ability authoring is currently frozen as experimental compatibility. Normal CharacterStudio navigation, combined-weapon editing, overview, readiness, and next-step guidance expose only weapons, animation, reaction, public action defaults, and character overrides. Existing ability data remains compiler/runtime compatible and is visible only from the read-only advanced compatibility diagnostic; new combinations do not author new ability data.

The primary navigation is **绑定 / 单一武器 / 组合武器 / 原材料 / 运行验证**. Global animation and reaction records are selected, created and quick-edited inside the combined-weapon workbench instead of occupying duplicate primary tabs. Compiler-backed animation resolution, reaction generation, resource planning and readiness are grouped as secondary views under `运行验证`. Frozen historical ability data is available only in the read-only advanced compatibility diagnostic. Weapon, animation, reaction, action and preview surfaces share `activeCombinedWeaponConfigurationId`.
- `GET /api/character/weapon-configurations?package=<relative-path>` is the read-only compiler-backed snapshot for slots, EquipmentState, animation, action, ability, resources, and diagnostics. Other panels consume this identity instead of deriving their own loadout.

Recommended shared entry for all external editors:

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

Windows:

```bat
Tools\MxFramework.EditorHub\start-editor-hub.bat
```

Open the Hub and click `打开角色编辑器`.

macOS:

```bash
Tools/MxFramework.CharacterStudio/start-character-studio.sh
```

Windows:

```bat
Tools\MxFramework.CharacterStudio\start-character-studio.bat
```

macOS Finder can also double-click:

```text
Tools/MxFramework.CharacterStudio/start-character-studio.command
```

Defaults:

- port: `4873`
- package: `Tools/MxFramework.Authoring/samples/character-codex-flow-test`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.CharacterStudio/web/`

Project-owned character packages are discovered and created under
`Authoring/CharacterPackages/`. Built-in packages under
`Tools/MxFramework.Authoring/samples/` remain available as templates and examples;
licensed project characters should not be stored as framework samples.

Optional port and package override:

```bash
Tools/MxFramework.CharacterStudio/start-character-studio.sh 4874 Tools/MxFramework.Authoring/samples/character-slime
```

```bat
Tools\MxFramework.CharacterStudio\start-character-studio.bat 4874 Tools\MxFramework.Authoring\samples\character-slime
```

The launch scripts check:

- repository root and Authoring CLI project;
- .NET 9+ SDK in `PATH`;
- selected character package and `manifest.json`;
- port availability, with a friendly message if the server is already running;
- optional CharacterStudio npm dependencies for Three.js GLB/FBX preview and optional FBX conversion tooling.

Install the browser 3D / FBX dependency once when model preview or FBX conversion is needed:

```bash
npm --prefix Tools/MxFramework.CharacterStudio install
```

Manual fallback from the repository root:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  editor serve --root . --port 4873 --package Tools/MxFramework.Authoring/samples/character-codex-flow-test
```

Open:

```text
http://127.0.0.1:4873/Tools/MxFramework.CharacterStudio/web/
```

The root `index.html` redirects to `web/` for static repo servers.

## Scope

- package discovery plus workflow navigation for the Codex Flow Test Character Resource Package;
- character lifecycle controls in the toolbar: `新建` calls `/api/character/create`, `复制` calls `/api/character/duplicate`, and `归档` calls `/api/character/archive`. Create/duplicate require a globally unique `packageId`, create the Authoring package and canonical `ArtSource/Characters/<packageId>/` source root as one rollback unit, rewrite package-local identities, and reset inherited import history. Archive moves both active roots into timestamped Archive directories, protects the built-in Humanoid/Creature templates, and deliberately retains Unity Generated / CharacterAction outputs for separate cleanup;
- read-only resource, geometry, validation, and import report browsing;
- Body Presence state and volume browsing, with independent Current save and fail-closed Future/Invalid read-only handling;
- Body Part Damage definition/member/threshold editing through the T1 service, with independent draft recovery, draft-only reorder and fail-closed Future/Invalid raw handling;
- Chinese-first workstation labels and actions;
- top workflow bar with `源资产` / `资源绑定` / `动画` / `受击` / `动作` / `Unity 导入` / `入游戏验证` stages, package readiness, blocker / warning counts, and one derived next action; frozen ability compatibility never becomes the normal next step;
- left-side workflow navigation by default, with the full technical debug tree kept under `高级 / 调试树`;
- right-side `当前流程` overview by default; raw Manifest JSON remains available only as an explicit Debug view;
- readiness issues rendered as compact Chinese problem cards, with stable diagnostic codes preserved as secondary technical evidence;
- source manifest editing for character-owned body / texture files plus field-driven resource pickers for public body, texture, and default weapon references;
- Three.js viewport for package FBX/GLB resources, including `mx.characterPaletteBinding.v5`: FBX `uv1 / PaletteUV.xy` samples the complete 256×256 RGB565 Color Palette v3 while `PaletteData.A` selects one of 16 Surface Profiles; the global-palette workbench edits the character's front-only, double-sided, or back-only render-face mode and refreshes the preview immediately; an enabled character-owned 512 Direct RGBA FaceDetail texture samples `uv2 / FaceDetailUV` directly. CharacterStudio preserves FBX vertex RGBA, builds the Color Palette preview DataTexture, previews the package FaceDetail resource and emits Usage v2;
- isolated single-weapon viewport mode, opened from the single-weapon workbench, which edits the weapon model pose and weapon-local traces without depending on a character; the role viewport separately composes those with character slots;
- SVG viewport fallback when `node_modules/three` has not been installed;
- inspector draft edits for colliders and sockets; the binding surface edits character slot position/rotation, while the single-weapon workbench edits weapon model position/rotation/scale and traces;
- save through `/api/character/save`, which validates with the Authoring Core gate before writing package JSON;
- compile diagnostics through `/api/character/compile`;
- Unity import bridge through `/api/character/import-unity`.
- in-game readiness through `/api/character/readiness`, shown in the `入游戏状态` panel with blockers, warnings, source paths, report references, tool handoff buttons, and a Unity acceptance source of `当前 Unity 会话` or `持久验收凭据`. When neither source exists for the current hash, the panel shows Deferred and keeps Unity import available instead of reporting invalid character content.
- read-only historical ability-chain inspection through `GET /api/character/ability-chain?package=<relative-path>` in `运行验证 → 高级兼容诊断`. It is not a normal editing surface and never affects Ready or next-step guidance.
- C2-C minimum Unity sync visibility: the workstation reads the source package, the latest Unity import report, and `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_resource_catalog.json` when present. Field resource pickers show Unity sync status, Unity asset path, GUID, importer kind, main object type, latest import operation, and diagnostics without mutating Unity assets.

Static file preview still opens the sample package, but save, compile, and import require `editor serve`.

## Interface language

CharacterStudio visible workstation labels, actions, empty states, and diagnostics are Simplified Chinese first. Product/tool names and technical identities such as `Unity`, `Runtime`, `Resource Manager`, `AnimationEditor`, `CharacterAction`, `Prefab`, `Avatar`, resource keys, stable IDs, paths, enum values, and raw JSON stay in their original form.

## Web Source Layout

CharacterStudio is still a native browser app loaded through:

```html
<script type="module" src="./app.js"></script>
```

`web/app.js` is now only the entry point: it imports the split modules, initializes DOM references, wires top-level events, and starts `loadAll()`. Feature code lives under `web/src/`:

| Module | Responsibility |
| --- | --- |
| `state.js` | Shared constants, mutable UI state, DOM reference object, and viewport runtime handles. |
| `api.js` | Package loading, save/compile/import calls, Unity import polling, and shared HTTP helpers. |
| `importMaterialsPanel.js` | Source manifest/import-materials panel, source candidates, slot cards, advanced list, save/sync actions. |
| `resourcePicker.js` | Field-scoped resource picker, `ResourceFieldSpec` handling, selection refs, resource library normalization. |
| `singleWeaponWorkbench.js` | Global single-weapon list/detail editing, structured base fields, management overview, draft state, reference merging, and protected CRUD. |
| `contextPanels.js` | Binding, animation handoff, resource plan, readiness, ability binding, and Unity import task panels. |
| `viewport3d.js` | Three.js runtime, direct FBX/GLB preview, immutable Palette Profile/Atlas material projection, sockets, colliders, weapons, traces, transform controls, symmetry editing. |
| `inspector.js` | Package tree, inspector fields, bone picker, diagnostics, and field commit behavior. |
| `utils.js` | Small shared helpers such as HTML escaping, cloning, nested-path access, and import blocking checks. |

The split is structural. It does not introduce a bundler or framework, and it keeps `window.CharacterStudioTest` available for smoke tests. When adding new CharacterStudio behavior, prefer the existing module that owns that surface instead of growing `app.js` again.

## 来源检查与同步

「原材料与源文件」首先显示角色模型、身体贴图和面部细节卡片；路径、制作记录、完整回执和公共库汇总收进下方详情。实际阻塞和缺失仍明确显示，不能因路径已填写而把 `Missing` 改成正常的“已配置”。卡片区分“已选择，待检查”“文件缺失”“待同步”“已同步到资源包”和“待 Unity 导入”。检查身份包含来源路径、目录、公共引用和目标声明，来源改变后不复用旧来源的成功或失败结论。

「保存来源配置」只保存本页来源选择；「检查来源」读取已保存来源的 SDK 检查结果，保留当前来源草稿。新选择尚未保存时保持“待检查”，不伪装成已检查。保存期间产生的新编辑继续保留为草稿。「同步到角色资源包」可先保存来源配置，但不自动保存角色数值、几何、武器库或部位伤害草稿；若自动保存期间又修改了来源，会停止后续同步并保留新选择。Unity 导入仍是后续独立步骤。

同步不再调用会替换整包和清空历史的 `loadPackageState`。前端读取同步前后的服务器包，将这段变化合入当前草稿、干净基线以及每条 Undo/Redo/进行中的包快照；有身份的资源数组按稳定键合并，新同步资源不会因撤销一条属性编辑而丢失。武器库与部位伤害草稿不被替换。独立文档只读取得前后版本见证，只有证实磁盘内容未变化才更新当前草稿和历史中的包版本；外部改动或缺少见证时保留旧版本并提示核对。检查/保存/同步响应只应用于发起操作的包实例，避免切换角色后串写界面。

同一字段双方都修改时保留草稿值，显示冲突路径并保持旧包版本，沿用现有保存冲突流程；不会把冲突当成已经解决。服务器 409 中的部分同步结果与完整性诊断也会保留。同步后状态读取失败时保留原编辑包与历史，不把不完整结果标为已接收。贴图语义、材质表面模式、Alpha cutoff 与资源用途在保存准备过程中保持完整。

检查：`npm run smoke:source-workspace`。覆盖来源草稿检查、迟到的保存响应、切换角色、同步增量合并、Undo/Redo、同字段冲突、部分 409、后续读取失败、材质声明和卡片顺序；接口使用模拟响应验证前端行为，不代替真实同步与浏览器视觉验收。

## Public reusable resources

CharacterStudio treats character-owned source files and public reusable resources as different layers.

- Character-owned source files live under `ArtSource/Characters/<characterId>/character_sources.json`. The source manifest should contain the body FBX, optional body textures, and other character-specific sources only.
- Public reusable resources live under `ArtSource/Common/Characters/<resourceId>/character_sources.json`, `ArtSource/Common/Weapons/<weaponId>/weapon_sources.json`, or generic resource manifests such as `ArtSource/Common/Textures/<resourceId>/resource_sources.json`.
- Resource Manager scans those common manifests, copies source files to `Assets/MxFrameworkGenerated/Common/...`, saves public import hints, and registers stable keys in `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json`. CharacterStudio opens Resource Manager for those lifecycle operations and only stores the returned resource reference on the active character field.
- Public character model keys use `character.<id>.model`; public weapon model keys use `weapon.<id>.model`; generic resources use keys such as `texture.<id>`.
- Source cards can choose either a package-local source file or a public resourceRef. Public body and texture selections are saved as `source.kind=resourceRef` in `character_sources.json`; the character package later gets a local alias instead of a copied public asset.
- Main-hand and off-hand choices are not source-file import slots. In Global Library 6.0, public/runtime resource identity, weapon pose and complete trace belong to the global single-weapon configuration; `geometry/weapon_slots.json` records only each character slot's Socket and local position/rotation. Every combination explicitly chooses one single weapon for each slot, including `weapon.unarmed`.

Because `viewport3d.js` is one directory deeper than the old monolithic `app.js`, Three.js dynamic imports must resolve from the package root:

```js
import("../../node_modules/three/build/three.module.js")
```

Do not change those imports back to `../node_modules/...`; that path points at `web/node_modules`, which does not exist.

The `原材料` tab edits `ArtSource/Characters/<characterId>/character_sources.json`. It saves only character-owned source choices such as body FBX and material textures, then syncs those choices into the package catalog and package-local `config/import_materials_state.json`. Texture sources use `Color / Normal / Mask` semantics and a material slot such as `body` or `eye`. A slot's Color target may explicitly declare `materialSurfaceMode = AlphaClip` and `alphaCutoff = 0..1`; otherwise the material remains `Opaque`. Sync groups them into package material descriptors, writes `model --usesMaterial--> material --usesTexture--> texture` dependencies, and declares FBX source-material-name bindings. Selecting a public weapon writes the global profile key to the active weapon definition's `weaponRef` and does not copy the model into the character package. CharacterStudio does not keep a full resource library browser visible in the role-editing surface; field pickers open only for the active body, texture, or weapon definition. When a public resource is missing, pending, or needs wrapper / import-hint edits, CharacterStudio opens Resource Manager instead of repairing the resource in place.

The panel renders a persistent `原材料导入回执`; file-copy/hash status and relationship integrity are separate. Each material evidence row shows the resolved `Opaque` or `AlphaClip` mode and Alpha cutoff. Missing texture semantics, material descriptors, catalog dependency edges, source-material bindings, or unsupported surface declarations add stable diagnostics and block Compile / Unity Import even when every source row is `Clean`. A frontend connected to an older Authoring service that returns no receipt fails closed with `INTEGRITY_RECEIPT_UNAVAILABLE` instead of treating the missing evidence as `NotApplicable`. Successful reconciliation is also logged and remains visible after reload. Raw-material readiness proves source material names and declared surface modes only; the Unity import receipt remains responsible for exact Renderer node/slot coverage and generated `Material` assets. CharacterStudio's direct FBX preview reads the material descriptor first, so Color, Normal, and Mask textures remain resolvable even while repairing a legacy catalog whose material dependency list was empty.

Public weapon textures and materials belong to the common weapon resource lifecycle. A public `ArtSource/Common/Weapons/<weaponId>/weapon_sources.json` can declare an optional texture item with `target.bindTarget = "weapon.albedo"`; the global single-weapon configuration keeps the `weaponRef`, and the role package never copies public weapon materials. Unity sync writes common weapon textures under `Assets/MxFrameworkGenerated/Common/Weapons/<weaponId>/textures/`, builds `materials/<weaponId>_Weapon.mat` with the `wgame.prop_stylized_lit` template, and assigns it to the public wrapper prefab. A palette-only public weapon may instead declare root-level `paletteMode = "sharedWGameAtlas"`; Unity then selects `wgame.prop_palette_stylized_lit`, binds the project shared Palette Atlas, and applies the holder character's immutable Palette Profile. The common resource diagnostics surface material path, template id, texture/Profile source, fallback, and renderer count.

CharacterStudio targets four separate layers: Resource Manager import correction, the global single-weapon library, character combined weapon configurations, and character-local semantic slots. Resource Manager owns resource creation, replacement, synchronization and import hints; CharacterStudio owns the global single-weapon configuration and character combination editors. Changing a combination slot never deletes the referenced public resource, global single weapon, animation, dependency graph, or character slot. Deleting a global single weapon is blocked while any role combination references it.

The `单一武器` surface is a two-view configuration workbench rather than a current-item dropdown. `配置编辑` is 3D-first: opening the tab restores the last valid editable weapon, or selects the first model-bound non-unarmed record, and switching the left-side list immediately replaces the isolated weapon scene. The scene stays above the complete structured identity / slot / resource / model-pose / presentation / combat / resource-policy / trace / modifier editor. Model pose, every Trace endpoint, radius, proximal extension and proximal radius are editable with synchronized scene handles and fields. A visible calibration rig marks the grip origin/guard plane, local `+Y` length axis, local `+Z` front and 0.1 m ticks; authors can also select any model-bound single weapon as a translucent wireframe pose reference. Both guides are editor-only UI state and never enter the library schema or character/runtime output. `配置总览` remains a full-width read-only management table with filtering, sorting, expansion, edit/copy/delete actions, draft state, and cross-character reference counts. `weapon.unarmed` is pinned and fully read-only, and selecting it leaves the model scene instead of showing an empty viewport. New and copied records require their stable ID before creation; the ID is locked immediately afterward. Switching records preserves the in-memory global-library draft and per-weapon camera, while `保存全局库` still commits the whole library through one revision.

The workbench reads `GET /api/weapon-library/reference-index?configurationType=singleWeapon` once for list and overview counts, then replaces the currently open character package's saved references with its in-memory combination draft. A dual-wield combination therefore reports one combination and two slot references. If the batch index is unavailable, reference state remains unknown and deletion is blocked; the existing per-ID reference endpoint can refresh the selected record before retrying.

The `组合武器` surface uses the same two-view workbench model. Entering `单一武器` or `组合武器` switches the center area into a focused full-width authoring layout; the package tree and generic inspector return when the user switches back to `绑定`. `配置编辑` lists the public combined-weapon library, marks the current character's bound/default roles, and guides the selected public recipe through four directly navigable stages: combination content, shared behavior defaults and character overrides, action defaults and overrides, and Runtime projection. Animation is a structured read-only summary with a Global Library Animation Editor handoff. Reaction opens a full-width sub-workbench with a global configuration list, death/cohesion and impact-height semantic matrix, preserved custom slots, structured intent groups, Resource Manager selection, and ActionRole resolution. Clicking an empty known semantic cell creates its fixed preset ID (`death`, `cohesionBreak`, or `hit.<normal|strong>.<high|mid|low>`) without an ID prompt. Existing presets are selected instead of duplicated; conflicting preset semantics are repaired explicitly. Non-standard IDs receive no legacy recognition or conversion and remain ordinary custom Slots. An invalid default Slot falls back to the first available Slot and is shown as an explicit repair warning; deleting the default requires choosing a new one explicitly. Raw JSON remains read-only audit data except for the advanced public-action and character-action-override editors. `配置总览` compares public recipes and their current-character role state in one table. CharacterStudio requires an explicit global-library save before opening Animation Editor and only auto-refreshes the editor result when no local global-library draft exists.

New and copied public combined recipes use an ID/name dialog. A new recipe starts with two explicit `weapon.unarmed` slots and empty behavior/action defaults, then creates a binding for the current character. A copied recipe retains the shared behavior references and deep-copies and rewrites its public ActionSet, Action, and Timeline identities; character overrides are not copied. Saving writes the global library before the character binding when both are dirty, under both optimistic revisions. Incomplete recipes remain editable drafts but continue to block Compile and Unity Import.

The `动画` panel is the current public combination's quick editor for its default global animation configuration. The global record contains complete Set / Layer / Group / Clip / BlendTree / Warmup and locomotion data. A character may select a different global animation configuration through its binding override; an empty override inherits the public selection. Quick edits modify the shared record and show affected combinations, while “复制为新配置并重绑当前组合” changes only the selected public combination default.

Character-owned model scale and art-offset correction live on `resource_catalog.json` as `importHints.modelWrapperPose`. Public weapon scale and art-offset correction live on the public `weapon_sources.json` / global profile provider data and must be edited in Resource Manager. CharacterStudio applies both as wrapper GameObjects in the 3D preview, but public weapon wrapper controls open Resource Manager instead of saving resource-level changes from the character surface.

For public FBX weapons, Unity ModelImporter `Convert Units` owns source-unit conversion. CharacterStudio may apply a temporary preview unit scale when loading raw FBX in the browser, but that preview scale is not saved into `modelWrapperPose`. The public weapon wrapper prefab used by Unity/runtime should have root scale `1` unless the author intentionally applies an art-scale correction.

The role viewport deterministically derives one runtime attachment identity for each active `combination + slot + weapon`. Use the ownership links to edit the character slot in `绑定` or the weapon model pose/trace in `单一武器`; the combined view is read-only for both. Dual-wielding reuses one weapon pose and trace, while main/off differences come only from the two character slots. No off-hand weapon pose is synthesized or mirrored.

`.fbx` body input is accepted as the Humanoid authority source. CharacterStudio can preview FBX directly, while GLB remains an optional generated preview/import artifact rather than a user-maintained character body source. `导入 Unity` is a separate step that writes compiled package outputs, public resource references, Prefab status, and CharacterAction synchronization data into the Unity project.

The visible workstation intentionally separates actions by layer. The top workflow bar derives the next primary action from unsaved changes, readiness issues, and Unity import task state; the older toolbar buttons remain as direct shortcuts for authors who already know the target operation.

- `导入源资源` writes imported model files into the Character source package resource catalog and can bind them to the current field.
- `选择已有资源` opens a field-scoped picker; it does not expose the full resource library as a permanent character-editing panel.
- `Prefab 重建预检` runs the Character compile gate and checks the generated inputs used by the Unity prefab path.
- `Unity 导入` writes the compiled package outputs and Unity resource catalog under `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`.
- `Unity 导入` requires an explicit Unity target platform. CharacterStudio forwards that selection to Resource Mapping and does not infer a platform from the authoring host.
- `入游戏状态` shows whether the current package is blocked by resources, animation, reaction, actions, Unity import, weapon trace bake, or Runtime validation. Deferred/frozen ability compatibility never blocks the normal flow.
- `高级兼容诊断` may display historical ability projection read-only. It does not offer create/edit/copy/bind operations and is not evidence that a formal ability authoring workflow exists.
- `查看/复制报告` copies validation, readiness, the selected compiled ability-chain scenario, compile, import, and Unity sync JSON for review.

`Unity 导入` 是分阶段同步，不是 AssetBundle 构建：

1. 保存并检查当前 package 与源清单草稿。
2. 调用 `/api/character/import-unity`，由 Authoring 服务运行 `character import-unity`。
3. 将角色包生成文件写入 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`。
4. 检查 `Assets/MxFrameworkGenerated/Common/...` 和 `global_resource_mapping_profile.v2.json` 中的公共可复用资源状态。
5. 请求 Unity Editor bridge 通过 `AssetDatabase` 导入资产。
6. 验证主体 Humanoid Avatar source。
7. 重建生成的角色 preview Prefab，并按默认武器配置验收主副手实例、确定性 runtime attachment、角色槽位姿态、武器自身姿态、资源 key 和空手槽位。
8. 按 CharacterAction Profile 同时加载角色包与 `Common` Action Library，合并 Registry；任一 Library 或跨 Library 引用失败时立即阻止。
9. 烘焙 weapon hit trace catalog，并验收请求数、artifact 数、攻击有效帧、武器 Trace frame、装备槽位 Socket frame 和 issue 状态；不允许静态胶囊 fallback。
10. 刷新生成的 CharacterAction Profile / ActionSet 数据，并验收它们可被动作配置编辑器发现。
11. 全部门禁成功后原子写入可版本管理的 `package_cache/unity_editor_import_receipt.json`；失败、pending 和 running 详情仍只保留在 Temp 会话状态中。
12. 重新加载 CharacterStudio readiness、resource plan、Unity catalog、editor rebuild status 和 receipt evidence。

该按钮的主要排查入口：

- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/package_cache/import_report.json`：Authoring 文件写入报告。
- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/package_cache/unity_editor_import_receipt.json`：当前 Compiler hash 对应的确定性 Unity 成功验收凭据，可提交；只保存成功状态。
- `Temp/MxFrameworkAuthoring/CharacterImport/status`：当前机器/会话的 pending、running、failed 和详细错误 IPC，不提交。
- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/runtime_resource_catalog.json`：runtime resource entries。
- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/character_resource_plan.json`：warmup 和 equipment 资源计划。
- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/generated/weapon_trace_bake_catalog.json`：生成的武器轨迹烘焙 catalog。
- `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/prefabs/<packageId>_character_preview.prefab`：生成的角色 Prefab。
- `Assets/MxFrameworkGenerated/CharacterActions/Profiles/<packageId>/character_action_profile.json` 和 `Assets/MxFrameworkGenerated/CharacterActions/ActionSets/<packageId>/character_action_sets.json`：CharacterAction 发现入口。
- `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json`：公共可复用资源 key，例如 `weapon.common_weapon_1.model`、`weapon.common_kite_shield.model`。

`import_report.json` 里的 `Skipped` 是干净的“文件已是最新”写入结果。它不能证明 Unity 已重建 Prefab、刷新 CharacterAction profile 或完成 weapon trace bake；这些阶段要看 Unity import task checklist、当前 editor rebuild status、持久 receipt 和 readiness panel。Unity Import 面板顶部的验收摘要只在 Authoring 写入、公共资源、AssetDatabase、Avatar、Prefab、双 Action Library Registry、weapon trace bake、默认武器和 CharacterAction 阶段都通过时显示 `Ready`。Temp 为空但当前 hash receipt 有效时仍显示 Ready；Temp 和 receipt 都不存在或 receipt 已过期时显示 `Deferred / 待执行 Unity 导入`，导入按钮保持可用；当前匹配会话失败、receipt 损坏/路径非法/关键产物缺失、Avatar invalid、默认武器资源不匹配、Library/Registry 失败、攻击有效帧/Trace/Socket frame 缺失、weapon trace bake issue 或 Profile/ActionSet missing 才会显示 `Blocked / Failed`。生成的 Prefab 不应序列化项目动画 clip。默认武器写成 `CharacterDefaultEquipmentRuntimeBinder` 资源引用，由 ResourceManager / preview runtime 实例化，不要求把公共武器 mesh 作为角色 Prefab 子物体持久化。

当前样例状态：`character-codex-flow-test` / `codex_flow_test` 是维护中的 Character Application 8.0 / Global Weapon Library 6.0 验证样本。全局库保存 `codex_flow_test.sword_empty`（剑 / 空手）和 `codex_flow_test.empty_empty`（空手 / 空手）两个公共组合；角色只保存对应 bindings，并以 `codex_flow_test.sword_empty` 为默认。Profile / ActionSet 由编译器生成，动作正文来自正式 Common / 角色动作源。能力 Authoring 已冻结且不作为完成条件。剑的武器局部 Trace 来自全局单一武器配置，角色 `mainHand` slot 只提供 Socket 与槽位姿态。

If clicking a generated preview Prefab logs repeated `Build asset version error` / `SourceAssetDB has modification time ... while content on disk has modification time ...`, Unity is seeing a stale AssetDatabase record for a file that was regenerated on disk. This is not a character package data error. Reimporting the affected Prefab clears the symptom; the long-term fix is for the Prefab rebuild stage to force-import both the preview Prefab and its runtime mirror after writing them.

If the Unity catalog is missing, resource cards show `未读取Catalog` or `未入Catalog`; this means the source package can still be edited, but Unity import has not produced a usable sync ledger for that resource yet.

FBX conversion requires the CharacterStudio npm dependencies:

```bash
npm --prefix Tools/MxFramework.CharacterStudio install
```

Set `MXFRAMEWORK_FBX2GLTF=/absolute/path/to/FBX2glTF` to use a custom converter binary.

## Smoke

```bash
npm --prefix Tools/MxFramework.CharacterStudio run smoke
```

The smoke script verifies the committed sample package shape, workflow bar presence, Chinese-first primary labels, advanced debug tree behavior, resource binding behavior, focused weapon viewport entry points, public mainHand/offHand weapon chains, and the absence of package-local default weapon fallback in `codex_flow_test`.

Equivalent CLI commands behind the bridge:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character validate --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --check-files

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character compile --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --out Temp/MxFrameworkAuthoring/CharacterStudio/compile --check-files

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character import-unity --package Tools/MxFramework.Authoring/samples/character-codex-flow-test --project-root . --unity-root Assets/MxFrameworkGenerated/CharacterPackages --check-files
```
