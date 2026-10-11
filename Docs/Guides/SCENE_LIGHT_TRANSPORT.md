# Scene light transport

> Status: Current

Status: integrated revision, updated 2026-09-26. TacticalCardDemoScene's full-base cache has now been explicitly rebuilt after repairing two newly added hangar material slots. The slots use the manifest's WGame baked environment material; Unity's default Lit is not accepted as a fallback. The new source fingerprint is valid, GI/fog preview and scene-build preflight pass, and existing cache asset identity is preserved. The rebuild contains 38670 surface regions and 7448 probes. No performance benchmark was performed.

## Ownership and data flow

### 查看探针布置

在 Level Editor 的 **场景渲染 → 探针布置预览** 开启 **在 Scene 视图显示探针**，再选择显示来源：

- **构建计划**：橙色框为实际纳入捕获的几何范围，青色框／点为待构建网格；有效性尚未知。范围沿用 Builder 的静态根与显式源选择、三角形细分和退化剔除，不按 Static 标志或 Layer 自动排除。
- **已构建缓存**：从绑定的 `Transport.bytes` 读取真实位置，绿色／红色对应保存的有效／无效标记。源 fingerprint 或构建间距不匹配时明确显示过期；查看旧缓存不会重建它。

默认启用世界高度切片、Scene 视图焦点附近 24 单位范围与 512 点上限，最大上限 2000。取消切片／局部筛选可看全域，超过上限时对匹配结果均匀抽取。使用 **定位网格范围** 调整视图；点击点或输入 ID 查看其精确位置、valid 标记、方向分箱的距离均值／二阶矩／标准差和链接数量。方向线只画至两格长度，面板数值保留实际统计值；代表方向不是原始射线，保存的统计也不是运行时 RGB。

构建间距默认仍为 **4**，修改通过现有 Undo／场景 Save 流程保存作者参数，不会自动烘焙。修改后计划使用新间距，旧缓存仍显示自己的实际间距和位置；只有显式 **构建光传输** 才生成新数据。网格最小端为 `bounds.min - spacing`，各轴点数为 `ceil((bounds.size + 2*spacing)/spacing)+1`，因此最大端外扩可能多于一格。预览同时显示预计数量；超过 120000 点只画范围并提示既有构建上限。

开启时及约每两秒核对作者源和缓存，CPU payload 仅在依赖或元数据变化时重新解码。内存网格修改、尚未被 AssetDatabase 捕捉的改动后可使用 **刷新范围和缓存状态**。切换离开此工作区会释放预览数据和 SceneView 订阅。预览不创建场景物件、不写光传输资产，Play Mode 中作者预览关闭。

Airborne dust uses this same scene composition. In Level Editor's scene rendering
workspace, use **空气微尘** to enable and tune density (particle count), world-space
extent, size, opacity, drift and scattering. **自动扫描缺失参数** fills the explicit
`SceneDust` shader binding without enabling the effect. Changes use the existing
Undo, preview and scene Save flow. Dust can run while GI and height fog are off.
When GI is available it borrows the existing probe moments and static sun visibility;
it does not rebuild transport or allocate a second lighting cache. Surface and medium
sampling share spatial visibility calculations but retain different angular responses.
See the scene dust contract in [Rendering](../Interfaces/Rendering.md).

Dust validation (2026-09-28): enabled and saved on `CharacterTestScene`; scene diff
contains only the 15 dust configuration lines. Unity refresh/compilation/domain
reload completed with Editor ready and zero Console errors. The focused dust,
scene-lighting and fog suites passed 35/35; after the final shared fog extraction,
the fog suite passed 8/8 again. Same-camera near/overview captures exercised the
procedural pass; near-camera fading and a 2.5-pixel radius cap limit oversized grains.
Two consecutive renders to an explicit 1280x720 target increased the dust draw counter by two while world
lighting updates and sun-visibility updates stayed at one and persistent lighting
storage stayed at 26,778,336 bytes. These are functional counters, not GPU timings.
Direct Editor `Camera.Render()` to the backbuffer encountered a final-post/UI
attachment-size mismatch; the same error reproduced with dust disabled. Explicit
render-target captures with dust enabled reported no rendering errors. Those manual
backbuffer probe errors are separate from the successful script-validation receipt.
The URP/quality asset remains `MxFrameworkURPAsset` and the only renderer feature
remains `MxRenderingPipelineFeature`. The separate repository stock-Lit audit reports
two unchanged materials: `CharacterPaletteLookDevGround.mat` and generated
`CharacterPreview_148C8C8C.mat`; neither belongs to this dust change.

`CharacterTestScene` now has its own transport cache. Its GameManager explicitly selects 32 existing static-source MeshRenderers (ground, material samples, fixed stairs and ramps) from the mixed test-course hierarchy; moving natural objects, grid lines and validation markers are excluded. The source selection is serialized on GameManager, not duplicated as render geometry. This build contains 1463 surface regions and 5445 probes; `Transport.bytes` is 7,847,076 bytes. The original whole-root path remains the default for the base scene, and the cache binary format is unchanged. The cache and scene references are now saved; subsequent authoring changes continue through Level Editor Save.

The scene's unique `GameManager` serializes `MxSceneRenderingConfiguration`: static root, sun, target camera, shared Look, transport data, shaders, renderer feature and independent GI/fog switches. A disposable `MxSceneRenderingSession` registers the existing lighting and fog providers. It ticks before gameplay's early return, so menus and paused play still render. An Editor-only preview manager creates the same session without starting gameplay and keeps it alive when Level Editor closes. Entering Play releases preview before GameManager takes ownership; disabling/unloading releases GPU resources. World transport buffers belong to the lighting provider; per-camera irradiance textures belong to RenderGraph. Other cameras cannot inherit the previous camera's lighting bindings.

Editor preview retains one validated CPU decode of the transport across Play scene reloads. It compares dependency hashes of the transport and data assets before reuse, and drops the copy when the settled Edit-mode scene changes or GI is disabled. The runtime GameManager continues to decode its own data, and each preview/runtime session allocates and releases its own GPU buffers. Authoring changes trigger a source-fingerprint check through Unity object/project change events; a ten-second fallback catches changes made outside those events. Build validation still computes the full fingerprint independently before accepting enabled GI. A cache hit skips binary parsing; it does not skip source validity or first-frame world-radiance updates.

Atmosphere is composed independently through the GameManager configuration, bound to the same rendering feature and Main Camera. It composites height fog after scene lighting/transparents and before post-processing, independently of the GI switch or cache validity. The saved density is 0.18, base height 0, height falloff 0.12 and distance 180; color, density, height and focus-clear controls are editable in Level Editor without rebaking transport. Noise is disabled to avoid a moving screen-space grain pattern.

Pipeline height fog suppresses old per-material height/focus fog only on the bound camera and opted-in Scene View. The mountains retain their authored distance haze, while their separate valley-height haze is suppressed under the shared height fog. Turning the fog switch off restores the relevant material behavior; it does not restore the removed legacy lighting paths in the four unified lighting shaders. Camera globals reset the override so UI, reflection and other non-targeted cameras do not inherit it. This uses the framework's existing v1 height-fog composite, not a new volumetric ray-marching system.

The Editor builder groups geometric surface patches, rasterizes five hemicube faces for patch transfer and six faces for each spatial probe, and records visibility independently of light color. It retains the strongest 24 fine links and up to eight coarse spatial/normal clusters. Dropped energy is reported, never renormalized into artificial light. Coarse radiance is an area-weighted reduction of child radiance.

The offline capture mesh and runtime sun occluder mesh have separate purposes. Offline captures use subdivided, per-patch-ID vertices. The runtime mesh uses original triangles and exact-position welding, with no patch IDs, normals or UV streams. Welding has no distance tolerance, so nearby distinct surfaces and thin gaps remain distinct.

At runtime a scene-wide static visibility texture supplies sun visibility even outside the current camera. Its raster geometry is the compiled static mesh. Sun rotation invalidates this texture; sun color/intensity and sky changes invalidate radiance only. Camera motion does not invalidate either world cache. Directly illuminated patch radiance plus one transport step supplies two diffuse surface interactions before probe reconstruction. This is indirect diffuse transport, not a real-time reflection or general dynamic-geometry GI system.

Probes store first-order directional radiance and six-face coarse depth moments. Reconstruction combines neighboring probes with depth-moment visibility, rejects invalid interior probes, and applies analytic capsule occlusion near dynamic actors. Depth/normal-guided half-resolution camera reconstruction is shared by scene and character materials. Source AO affects micro-occlusion, not another whole-frame darkening operation.

The receiver tangent plane is a conservative fallback for ambiguous visibility bins, not a physical occluder. A curved receiver can have all valid neighboring probes behind that plane (for example, a downward-facing surface above a floor). Format v1 captures 16 equally weighted distances per visibility bin; `mean - sqrt(15 * variance)` is a lower bound on their minimum. When the receiver lies before this bound with the existing distance margin, reconstruction smoothly admits the otherwise rejected probe. Mixed floor/sky bins retain tangent rejection unless the segment is before even that conservative bound. This fixes empty-support black patches without adding ambient light, changing radiance, allocating another texture or rebuilding the cache. The correction adds scalar work to the existing eight-probe gather. Finite capture resolution remains an approximation; it is not an exact visibility guarantee for unsampled thin geometry.

## Build and edit

Emission transport uses `linear BaseColor × SurfaceData.B × EmissionIntensityProperty` before area averaging. The optional scalar-property mapping is exposed in the Level Editor's advanced material mappings. Empty mappings and materials without that property retain 1× emission; mapped values must be finite and non-negative. The mapping and the current scalar value are fingerprinted, including unsaved material edits. This affects emitted radiance, not albedo/diffuse fractions, and does not require a binary format change. The project scanner maps WGame's `_WgEmissionIntensity`; the generic rendering compiler contains no WGame property names. Changing that intensity requires rebuilding transport, even if the emission texture is unchanged. Bloom remains a separate screen effect.

Open **MxFramework > Level Editor > 场景渲染** in the active scene. Its unique GameManager owns the bindings. The advanced foldout exposes the explicit material property mapping and 16 diffuse fractions from the project's surface-profile authority. Solid-color material mappings are supported separately; the framework does not hardcode WGame shader properties. No gameplay level JSON is changed by rendering adjustments.

Use **构建光传输** in Level Editor. Building runs across Editor updates and can be cancelled; switching or closing the owning scene cancels it. The output folder is `Assets/Config/MxFramework/Rendering/SceneLighting/<scene>/`. The scene's Data reference is adopted after a successful build and remains unsaved until Level Editor's coordinated Save. Ordinary Save never starts a build.

**仅重建太阳遮挡** upgrades the runtime mesh of an already valid cache without rebaking transport. It preserves the existing mesh asset identity and transport bytes. A source mismatch requires a full build instead.

For this project, open `Assets/Scenes/TacticalCardDemoScene.unity` and use the Level Editor module. GI and height fog have independent switches; `TacticalEveningLook` remains a shared asset and the panel warns about cross-scene effects. The debug selector is preview-only: 0 for normal rendering, 1 for irradiance, 2 for normals and 3 for motion (neutral 0.5 plus motion multiplied by 16). It does not write the shared Look. `IndirectStrength=0` is useful for a fixed-camera comparison while preserving the new direct-light response.

The shared evening Look now uses indirect strength 3.5, sky `(0.65, 0.68, 0.74)`, equator `(0.52, 0.53, 0.56)`, ground `(0.40, 0.36, 0.31)` and shadow tint `(0.94, 0.96, 1)` to improve shadow readability using the existing transport. These changes update radiance without rebuilding geometry or adding a second ambient-light term. Skin diffuse additionally limits illumination chroma through the material's **Skin Light Color Influence** (default 0.25), while retaining linear diffuse luminance. Missing/stale GI remains disabled and is still a build diagnostic; these Look settings do not manufacture an unoccluded fallback.

The model authority remains `ArtSource/Environment/Base/Blender_Work/base_work.blend`. Follow [base scene assembly](ENVIRONMENT_BASE_SCENE_ASSEMBLY.md): Blender publish/sync, Unity refresh and **Update Base Module Prefabs**, then rebuild transport after model/material/layout changes. The builder reads the resulting Unity placements; it never recreates the base layout. No Blender source edit was required for this revision.

The binary header contains magic, format version, fine/total node counts, edge count and probe/link counts. Nodes, CSR rows/edges, probe coefficients, links and depth moments follow in little-endian order. Loading checks exact byte size, dimensions, indices and energy bounds before GPU upload. Binary version changes require an explicit rebuild.

Changing mesh content, placement, albedo/surface texture content or diffuse mappings invalidates the fingerprint. The Editor reports stale data and disables the new indirect lighting until rebuilt. Sun and Look changes do not rebuild geometry. Missing/invalid data is not an accepted high-quality result, although the base renderer continues working.

Look also exposes opt-in `SpecularStrength` and `LightRimStrength` (both default zero). The existing camera resolve publishes these material-response scalars in `_MxSceneSurfaceHighlights.xy`; shaders must first check `_MxSceneLightingEnabled`. They affect the surface's main-light response, not diffuse transport injection, so changing them does not rebuild or recompute the world cache. The baked WGame environment consumer uses existing Surface Profile constants and the already sampled main-light visibility; it adds neither texture reads nor a pass. Base-scene tuning and the default-preserving behavior are documented in the [assembly guide](ENVIRONMENT_BASE_SCENE_ASSEMBLY.md).

Authoring fingerprint checks run in Edit Mode. Play-mode static batching replaces imported meshes with transient combined meshes and must not be interpreted as an authoring edit. A scene-build callback rejects missing/stale active lighting data before a player build. Moving static environment during Play remains outside this revision's supported scope. Assembly reload is locked only while the cancellable offline build owns temporary capture resources.

## RenderGraph extension

`IMxRenderGraphPass` is an optional exclusive path on `IMxRenderPass`. It declares URP input requirements and records its own graph from `ContextContainer` and the existing configure context. The wrapper does not also call legacy Execute/fullscreen paths for these passes. Existing fog and legacy providers retain their existing path.

Lighting inputs are compared before recording. Stable cameras import only the buffers needed to resolve lighting and record no world-update pass. Changed sun color/sky records a compute pass; changed sun direction also records a separate visibility raster pass. The sun's RFloat visibility texture is persistent, while its 2048-square depth attachment is created only for that raster pass and owned by RenderGraph. Input values are captured in pass data; the cache becomes valid only when the compute pass executes, never merely when it is recorded. The sun projection basis depends only on its direction, so an unrelated roll cannot desynchronize capture and lookup.

Camera resolve declares depth/normal/buffer reads and writes a transient HDR output. Capsule candidates use a separate compute pass and per-camera transient buffer. Material property blocks copy camera-specific constants/arrays and bind the candidate handle during execution. World-private resources are not advertised as cross-provider SharedRT entries. Graph transient resources can remain in Unity's allocation pool after their logical lifetime; this is distinct from scene-owned persistent memory.

Before probe lookup, pixels outside the material's world-field bounds return zero irradiance with valid eye depth for reconstruction. Pixels with no irradiance, no capsules or zero capsule strength skip capsule evaluation. Active capsules use conservative projected influence bounds and a 32-bit mask per 16x16 half-resolution tile. At each pixel only the candidate bits are evaluated.

The previous infinite capsule tail is smoothly faded over R to 1.25R. With strength s, radius r and active count N, R squared is r squared times s times N / 0.01. At the start of the fade, each capsule's maximum attenuation is already at most 0.01/N; summing all omissions bounds the change in the product's occlusion factor by 0.01. Thus the difference is at most 1% of **unoccluded** indirect irradiance, not 1% relative to a nearly black shaded result. Near-plane crossing falls back to full-screen candidates, offscreen bounds are clipped conservatively, and stereo also falls back to full coverage. Depth/normal prepass and actor-discovery policy are unchanged.

## Frame inputs and DLSS boundary

EnvironmentStylizedLit, CharacterStylizedLit, CharacterStylizedLitLite and CharacterPaletteLegacy consume one shared diffuse response, including their baked, palette and solid-color input modes. These shaders plus PropStylizedLit and DistantMountainVertexColor have DepthNormals and MotionVectors passes. The latter two keep their separate forward-lighting response. Motion uses current and previous non-jittered camera matrices, previous object matrices and the engine's previous skinned vertex stream. Auxiliary passes preserve dissolve and LOD clipping. Camera cuts/rebinding increment the scene history generation and request URP history reset; scene recreation starts a new history owner.

The four Scene Lighting consumers no longer execute their former diffuse-lighting fallback. `WgSceneDiffuse` always evaluates the shared direct-light response; it samples indirect irradiance only for a valid in-bounds field with a positive indirect strength. Without an enabled provider, direct strength 1 and band strength 0.25 are explicit defaults, so another camera's stale Look values cannot control the material. With an enabled provider but an out-of-bounds surface, current Look direct styling still applies and indirect contribution is zero. Missing/stale cache diagnostics remain a failure of full lighting acceptance; direct-only rendering does not conceal them.

Removed receiver calculations include old palette three-band/value lifting, SH/hemisphere/skin-view fill, direct AO, per-material legacy diffuse compression, duplicate light tint and height/focus fog. Lite no longer interpolates precomputed ambient/mid/light colors or fog. Additional character lights use the same direct diffuse response, while full/legacy variants retain profile specular highlights. Hair/material specular, MatCap, edge/rim effects, authored emission, procedural surface detail, weather, hit/status effects, dissolve and LOD clipping remain material features. This does not claim a replacement of all stylized material effects with a physical BRDF.

Shader names, asset GUIDs, input formats and material CBUFFER layouts are preserved. `CharacterPaletteLegacy` names the supported palette input format, not an old lighting path. Retired lighting property keys remain hidden for importer/serialized-material compatibility and are not evaluated; material inspectors expose only active surface/effect controls and direct lighting-style edits to the shared Look asset. `WGameFrameGlobals.hlsl` now owns time/weather/subject declarations independently of fog. Shared fog and palette helpers used by other shader families are not deleted; standalone prop, distant mountain and VFX families are outside this specific fallback removal.

GI requests only depth and normals. `场景渲染 → 高级绑定与材质映射 → 请求运动矢量` defaults off; enable it only while an external consumer needs that input. Motion debug view 3 requests it automatically. Disabling the request restores any skinned motion settings overridden by the session; it does not force off a renderer's pre-existing setting. URP TAA/object motion blur can independently request their own motion input. A borrowed Motion handle is optional and must be checked before use.

Changing `TargetCamera`, including a temporary null target, retains the existing provider and its world buffers. Only camera targeting/history changes. Inspector camera/preview/motion changes follow this lightweight path as well. Disabling/unloading, changing world resource references, or explicit **Rebind Lighting** still releases/recreates the world resources as appropriate. Regenerated assets use explicit Rebind so changed data cannot reuse stale GPU content.

`MxSceneLightingFrameData` exposes borrowed Irradiance, SceneColor, Depth, Normals and Motion handles in the current camera graph. SceneColor is published at BeforeRenderingPostProcessing/order -100; a consumer must record after this producer and before post-processing or UI composition. Camera, ColorDescriptor, ColorSpace, PreExposure (1: this provider applies no pre-exposure), and HistoryGeneration state the input contract. Handles must not be retained across cameras or frames; history identity includes the camera and scene owner, not just the generation number. World-space/transparent UI already rendered into camera color is not removed by this hook. A future adapter must select its actual UI composition boundary accordingly.

No unused export textures are allocated. Character/environment/VFX/UI remain separate authored renderers and layers, with no speculative control-mask textures. No DLSS SDK, neural-rendering model, vendor runtime, or promised hardware compatibility is included. Actual DLSS integration requires its own acceptance.

## Validation and estimates

Correctness tests cover binary round-trip, truncation, invalid indices/weights and real-scene energy retention. Real-frame acceptance must additionally check sun/sky changes, camera motion, character contact, wall leakage, resolution changes and resource lifetime.

Estimate storage from actual buffers: nodes 80 bytes, CSR rows 8 bytes, edges 8 bytes, probes 64 bytes, probe links 32 bytes, depth moments 96 x 8 bytes per probe, projected coefficients 48 bytes per probe, two radiance buffers 16 bytes per node. The persistent static visibility target is 2048 x 2048 RFloat. Its depth attachment is graph-transient on visibility-update frames. Each camera resolve uses half-width x half-height RGBA16F; active capsule candidates add four bytes per 16x16 tile of that target. Backend alignment and transient URP input buffers are additional.

No GPU benchmark or frame-time claim is part of this task. Update counters are functional cache-invalidation diagnostics, not timing measurements.

## Current scene acceptance

Formal assets: `Assets/Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/` and `SceneLighting/TacticalEveningLook.asset`, all generated through Unity with their metadata.

| Data | Actual generated amount |
| --- | ---: |
| Fine surface patches | 38,669 |
| Fine + coarse nodes | 50,712 |
| Transfer links | 978,978 (940,309 fine; 38,669 coarse reduction) |
| Spatial probes | 7,448; 7,404 valid |
| Probe links | 217,856 |
| Retained sampled transfer | 95.77537% |
| Transport binary | 22,907,948 bytes |
| Static occluder mesh | 145,361 vertices / 225,944 triangles; 32-bit indices |

GPU cache buffers plus the persistent 2048-square sun visibility target estimate **44,279,696 bytes (42.23 MiB)**. The compact position-only occluder mesh adds **4,455,660 bytes (4.25 MiB)** of vertex/index storage, excluding CPU copies and backend overhead. At 2560x1440, the half-resolution RGBA16F resolve adds **7,372,800 bytes (7.03 MiB)** per rendered camera. Together these explicitly counted resources are approximately **53.51 MiB**, plus a **14,400-byte** per-camera capsule mask when needed. A visibility-update frame additionally needs the transient depth attachment (approximately 16 MiB for four-byte depth storage); Unity may retain its physical allocation in the graph pool. URP shadows, depth/normals/motion, color buffers, driver alignment and temporary offline-build allocations are additional. The initial unoptimized mesh had 712,653 vertices / 237,551 triangles; transport node/link data is unchanged by the mesh upgrade.

Stable lighting frames reuse all world radiance and sun visibility without recording their update passes. Camera reconstruction still runs: up to eight probe candidates per in-field half-resolution pixel, only its tile's capsule candidates, and four depth/normal-guided samples per shaded full-resolution pixel. Active capsule frames also generate tile masks. Updating light color/sky dispatches 38,669 injection threads, two reductions of 12,043 coarse nodes, 38,669 bounce threads and 7,448 projection threads: 108,872 logical invocations / 1,705 groups of 64 including padding, with 1,235,503 link gathers. A changed sun direction additionally rasterizes the 225,944-triangle static mesh. These are structural estimates, not measured GPU times or FPS claims. Depth/normal prepass remains in place.

Initial functional evidence on Unity 6000.5.0f1 / URP 17.5.0 / D3D11 / RTX 4080 (follow-up optimization checks below):

- Focused transport and existing fog tests: 17 passed, none skipped. Includes binary/energy contracts, real generated data, two-patch GPU analytical result 0.5625, and raster visibility selecting the near occluder over a receiver behind it.
- Camera-only changes kept world counters at 1/1. From a clean cache: sun intensity changed 1/1 to 2/1; sky to 3/1; sun rotation to 4/2. Restoring authored lights produced 5/3. Final Look-only changes also leave the world cache intact.
- At probe 2475 near the shaded hangar, position (-53.23, 3.85, -17.54), switching off only the sun changed the RGB constant coefficient from (0.027527, 0.025961, 0.027246) to (0.010541, 0.013692, 0.020399). The warm reflected contribution is (0.016986, 0.012269, 0.006848).
- Current three character materials have valid normal output. A temporary copy of the actual warrior skeleton was tested at the hangar using a humanoid walk clip, then removed. In continuous unpaused rendering with a fixed camera, skeletal motion changed 4,839 diagnostic pixels and root-only translation changed 4,680, using an absolute RG deviation threshold of 0.002 from neutral 0.5. These counts demonstrate nonzero input, not motion-quality scores. Camera motion also produced spatially varying environment vectors. A paused Editor/manual Camera.Render capture is insufficient motion evidence.
- The character changes from sunlit to shaded at the hangar edge; indirect off/on captures show shared shaded-ground and character dark-part response. The screen-space gather's behind-surface rejection removed a probe-grid artifact. First-order probes and finite directional depth bins remain approximations; narrow gaps and thin-wall cases require scene-specific inspection after authoring changes.
- A changed diffuse mapping changed the authoring fingerprint; restoring it matched the formal cache. Play-mode static batching did not invalidate it. Scene save/reopen recovered a valid cache at 1/1 with exactly two owned sun textures.
- Disabling the component reported zero owned cache bytes and removed both sun textures; re-enabling restored 61,056,912 bytes. The explicitly unbound camera rendered its normal legacy output while the bound camera used motion diagnostics. Camera switching increments/reset history and rebinds the provider.
- Unity refresh, compilation, domain reload, Editor readiness and zero Console errors were checked. Shader/Compute compile status is included in the final validation receipt. No player build, DLSS SDK acceptance or performance benchmark was run.

Captures and the final receipt are in [Docs/Validation/SceneLighting](../Validation/SceneLighting/README.md). The base was not regenerated; temporary validation actors/cameras were removed. Original unrelated work remains uncommitted.

After the motion/camera/occluder optimization, the focused suite passes 19/19 cases. Live A/B/null/back camera switching preserves the same native world buffer and 1/1 update counters; explicit/debug motion requests restore the original actor settings when disabled. The compact mesh preserves asset identity and the exact transport binary. Three-direction raster checks, compile job IDs and the scoped scene diff are recorded in the validation receipt. No time-saving estimate or performance benchmark was added.

After the scheduling/capsule/transient-depth optimization, the focused suite passes 26/26 cases. Stable A/B camera renders add no world passes or depth handles; intensity/sky changes add compute only, and sun rotation adds exactly one visibility raster/depth handle. Full-traversal versus tiled raster comparisons in two perspective views and an orthographic view stayed within the 1% bound, with measured maximum image differences below 0.29% of unoccluded irradiance in those fixtures. These are correctness comparisons, not runtime performance measurements.

After removing the four consumers' legacy lighting branches, 36/36 focused transport/fog/shader-family tests pass. A GPU response fixture checks no-cache direct light despite stale globals, zero illumination without lights, values above 1 in linear HDR, and no indirect sampling outside the field. Base and current-character captures, including the Lite palette variant, are linked in the validation receipt. The scene, material assets and transport payload were not migrated or rebaked for this removal.
