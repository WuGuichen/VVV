# Fixed directional triangle shadows

> Status: Guide
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxRenderingPipelineFeature.cs`

## Disabled-camera integer texture binding (2026-10-11)

`_MxDirectionalShadowDynamicDepth` remains `Texture2D<uint>`; the dynamic raster
stores float-depth bits in an `R32_UInt` texture. `MxRenderingPipelineFeature`
now owns a transient 1x1 `R32_UInt` RenderTexture fallback with random-write
enabled, after checking `Render` and `LoadStore` format support. This follows
the existing dynamic-atlas allocation path. A `Texture2D` fallback is invalid
on Metal platforms where Unity rejects `R32_UInt` for `Sample` usage; the
receiver uses integer `Load`, which does not require filtered sampling.
It binds the fallback at creation and at the start of each camera, resets the
shadow enable/data-present state, and declares the fallback read in RenderGraph.
The targeted shadow provider then replaces it with the actual atlas. Fallback
contents are not read while the shadow enable/data-present flags are zero.
The owning RTHandle releases the RenderTexture on feature disposal.

This addresses the missing compatible binding when no shadow provider runs:
Metal can reject Unity's default sRGB color texture for the integer parameter
even when sampling is disabled. Changing the receiver to `float` would break the
existing integer-depth contract. No authored textures or scene assets change.
Native evidence on 2026-10-11 (Unity 6000.6.3f1, macOS Metal) confirms that
`Render`/`LoadStore` support is true and the bound fallback is `R32_UInt`.
CharacterTestScene entered Play and displayed its startup menu; the user
confirmed that it runs. Resource bootstrap and missing-UI-package errors were
separately resolved by restoring 177 existing Unity Git LFS assets (about
129 MiB); these had been pointer text imported as `DefaultAsset`. Restoring
the original LFS contents is local checkout recovery, not a new asset revision.

The script validator transport disconnected during domain reload. Its persisted
evidence records refresh completion, compilation start/finish with no errors,
and the reload; a subsequent native Editor check confirms ready state and
Console error count 0. This is fallback validation, not a successful unified
tool response. The evidence covers disabled-shadow binding and scene startup;
active dynamic-atlas rendering and other GPU platforms are not newly accepted.
The separate scene Trigger cache hash and filesystem writer-lock warnings
remain outside this correction.

## Fixed-ground grass coverage

The format-3 implementation keeps the existing RFloat centre/peripheral textures and
records an explicitly authored horizontal receiver plane with six-bit coverage.
It retains the original centre depth rounded to the remaining mantissa bits for
non-plane receivers. At the current 197.75534 m depth span the rounding bound is
about 0.378 mm. A near occluder's coverage is not used as general transparency for
arbitrary receiver heights.

First activation uses `CharacterTestDirectionalShadowAuthoring.BakeWithPlaneCoverage(owner,
planeY, planeTolerance, resolution, false)` after confirming the actual ground.
Both static regions must be rebaked; a legacy peripheral texture cannot be reused.
Subsequent ordinary Bake preserves the selected plane. Bias changes invalidate
authoring validation and need baking. Legacy format-2 assets retain raw decoding;
unknown encodings are rejected. A failed in-place asset commit is left invalid,
not silently exposed as a mixed raw/packed field.

The 8x8 coverage work and one reusable ulong sample-mask array exist only during
baking. Runtime still point-loads at most four depth texels per static region,
then decodes coverage on the selected plane or depth elsewhere. The existing
static/dynamic union and direct-light-only application remain. There is no extra
runtime texture or mesh pass. Bit-exact RFloat asset/GPU round-trip, grass-region
A/B, non-plane and overlap checks must pass in the owning Editor before visual or
performance acceptance is claimed; offline area preservation is not visual approval.

Native acceptance on 2026-10-09 (Unity 6000.6.3f1, D3D11): four focused tests passed,
including persisted RFloat GPU bit preservation; 48 production-HLSL checks passed
for legacy decoding, non-plane/curved/sloped receivers, bias mismatch and overlap.
The TacticalCardDemoScene centre (4096) and periphery (2048) were fully rebaked
for the visible OuterConcrete plane at Y=-0.015, tolerance 0.001 and bias 0.015.
Same-view grass screenshots show substantially fewer disconnected bead-like
shadow fragments. Static texture storage remains 80 MiB and the tap count is unchanged.
Raised decals and other heights retain depth comparison. This validates an Editor
SceneView sample, not runtime animation, other GPU backends or a performance gain.



The original backend was implemented and validated in WGameFramework on 2026-10-02. Cast-mode corrections were integrated on 2026-10-03; the receiver/caching optimization described below is integrated and has offline plus focused native acceptance. This guide covers the new direct-light visibility path, its scene adoption and the limits of the evidence.

## Scene detail override (2026-10-08)

TacticalCardDemoScene now persists `DirectionalShadowCenterScale = 0.8` and `DirectionalShadowCenterResolution = 4096` on its scene rendering configuration. The centered detail receiver bounds changed from 80 x 60 m to 64 x 48 m in XZ; center `(0, 8.922486, 0)` and height 18.4649734 m are unchanged. The actual center RFloat texture is 4096 x 4096; peripheral remains 2048 x 2048 and dynamic remains 1024 x 1024. Static logical texture storage is now 80 MiB (previously 52 MiB). Bias 0.015, filter radius 0 and transition width 1 remain unchanged.

These are scene-specific authoring inputs. Resolution 0 retains the existing budget split, and the default scale 1 retains the original region; shared battlefield geometry/constants are not changed. Ordinary shadow baking honors the override. `CharacterTestDirectionalShadowAuthoring.Bake(owner, budget, true)` requests a center-only rebuild: the baker first verifies that current sources/light/full coverage still match the existing field, then reuses its peripheral texture, axes, common depth slab and blend width. Stale peripheral input is rejected, not silently reused. The actual center resolution extends the fingerprint only when it differs from the legacy split. Changing either input requires an explicit shadow bake; it never rebuilds GI.

The native center-only rebuild completed in about 10.2 seconds. Source validation passed and the allocated/imported texture is 4096 squared. Scene disk changes are exactly the two configuration fields; the peripheral depth and scene-GI asset hashes are unchanged. Native compilation completed with zero compiler/Console errors and observed domain reload/readiness; the validation wrapper timed out, so the recovered job evidence supplies those results. The scene was saved clean; a later release snapshot was dirty and was preserved without another save or discard. This is configuration/resource validation, not a new visual or performance benchmark.

## Caster admission and material response corrections (2026-10-08)

Dynamic triangle admission, candidate-pixel counting and rasterization now consume the same per-update submesh admission snapshot. Only nonempty material slots admitted by `MxDirectionalShadowCoverage.CanCast` contribute triangles or issue CountCoverage/Raster dispatches. Coverage values and property-block overrides are resolved before counting and retained for that update; material/queue/pass changes are observed by the next update. The complete update is still counted before any raster dispatch, with the existing 262144-triangle and 8M candidate-pixel rejection limits. Alpha-tested admitted triangles retain conservative candidate-box counting; this change does not discard partially covered silhouettes or guarantee a larger supported actor population.

Ordinary static MeshRenderer sources and dynamic sources require an opaque/cutout material with an existing, enabled `ShadowCaster` pass. Renderer Cast Shadows Off remains authoritative. The baker applies the same admission to its source fingerprint, depth bounds and raster loop, including direct callers. `MxShadowBakeSource` keeps its existing constructors and adds an explicit-coverage overload. The sole project exception is scene-owned `WGame/FoliageMossLit` vegetation: its existing red-channel mask / vertex-alpha reference coverage is used despite the shader intentionally lacking a mesh ShadowCaster pass. The vegetation type's `CastFixedDirectionalShadow` remains its independent authoring switch; this does not inherit the prefab renderer's Off state. Explicitly disabling the material ShadowCaster pass or using a transparent queue rejects even this exception. Other shaders without a ShadowCaster pass are not implicitly admitted as vegetation.

The bake fingerprint is now v6 and records explicit coverage. Existing v5 caches require an explicit Edit-mode rebake and source validation before acceptance; no bake or scene/asset rewrite is performed by this source change. Grass remains reference-pose static, with visible wind unchanged.

The extra material-facing changes introduced in commit `7622be300` were narrowed out: additional-light specular retains its previous lobe, hair restores its half-Lambert facing, and the two shared palette direct-highlight factors apply sun visibility without the additional N·L factor. Direct sunlight, its specular/rim terms and the incoming GI/environment/studio response remain separate; final color is not multiplied by shadow visibility. Later skin changes and character normal/bias sampling are retained. This restores these specific response factors when visibility is one, not a claim of whole-frame pixel identity with an older shader revision. Visual preference for the restored lobes needs native comparison.

This correction is verified with offline compilation and a focused dispatch/admission harness; native Editor/shader import, focused NUnit execution, a static rebake, feature on/off visual comparison and Player/platform behavior must be checked by the coordinating LOD owner. Older native/timing evidence below applies to its stated source revision and is not new acceptance for this change. No URP performance claim is made.


## Scene adoption

烘焙角色 `WGame/CharacterStylizedLit` 不把局部三角面的平面外推到相邻 PCF 采样点；阴影查询点沿平滑法线偏移已有的 `DirectionalShadowBias` 世界距离，再执行深度比较。前者消除曲面平面外推产生的碎斑，后者减少平滑表面显露底层多边形投影的块面。偏移只用于启用的固定方向阴影查询，不改变几何、其它光照位置或贴图。角色保留静态／动态投影、深度偏移和四点过滤。其它接收材质保持默认平面补偿，地面路径不变。无需重烘焙角色贴图。与普通阴影偏移一样，过大的值会削弱近距离接触阴影；此次沿用场景原值，不提高全局偏移。

`Assets/Scenes/TacticalCardDemoScene.unity` enables persistent static visibility and dynamic triangle coverage at 1024². The fixed light is `Directional Light`. The saved bias is 0.015 m and the filter radius remains 0 m. Radius zero now enables the minimum 2x2 bilinear PCF footprint, replacing the unfiltered hard-shadow path. The bilinear weights vary continuously across texel boundaries; point depths are compared before filtering. Static and dynamic visibility are combined on each shared ray before weighting, preserving overlapping silhouettes without multiplying attenuation. The coarser active field supplies the sampling lattice; thin details can soften. Positive radius can widen the footprint while retaining four rays. Receiver culling includes the kernel extent and depth-texel rounding. No additional render texture, temporal history or higher map resolution is introduced. These are implementation choices, not measured performance results; final appearance is left to user inspection.

Persistent assets live under `Assets/Config/MxFramework/Rendering/DirectionalShadows/TacticalCardDemoScene/`: `DirectionalShadows.asset`, `StaticDepth.asset` (center) and `PeripheralDepth.asset`. The fixed two-region bake was saved on 2026-10-04 at 01:29 +08. Its inputs contain 1222 source entries and 354443 triangles: the configured static root and every authored vegetation instance in its reference pose. Wind remains enabled in the visible vegetation shader. The shadow is intentionally fixed.

Bake with the scene rendering authoring panel or **Tools → MxFramework → Rendering → Bake Fixed Directional Shadows**. Baking is explicit, runs in Edit mode and persists actual geometry/alpha depth; it is not a startup capture. The ordinary scene-GI startup visibility cache is a separate existing system.

## PCF integration (2026-10-03)

After visual feedback that the initial 3x3 tent result was too soft, the base kernel was narrowed to 2x2 bilinear comparison filtering. The narrowed version completed Unity compilation/domain reload and the six shader-family error checks at 2026-10-03 23:26 +08; native Console errors were 0, static source validation passed and radius remained 0. Evidence: `C:/Users/WuGC/Documents/Codex/2026-10-03/shadow-pcf-narrow/final-validation.json`. At radius 0, an isolated step transitions across one reference texel instead of roughly two; static/dynamic union and receiver-plane correction are retained. This is continuous fractional-coordinate weighting, not the earlier equal-weight four-corner offsets.

The requested 2x2 bilinear comparison PCF is enabled in the existing receiver helper, including saved configurations with radius 0. Sampling stays on a stable light-space lattice instead of translating four hard silhouettes around the receiver. The kernel is normalized, preserves fully lit/shadowed regions, and fades entering/leaving taps to zero at lattice transitions. The source depth textures remain point-loaded (including the R32UInt dynamic texture); raw depth values are never interpolated. There are at most four depth comparisons per active field and one dynamic-status read per receiver query.

Validation of the initial 3x3 implementation completed at 2026-10-03 23:06 +08: the persisted Unity compilation job records refresh, compilation start/finish with no errors and domain reload; the ready Editor reports 0 native Console errors and no shader errors for the six checked WGame shader families. The unified validation tool disconnected during reload, so native/persisted evidence supplied the fallback. Static source validation passes, radius remains 0 and scene dirty state is false. Evidence: `C:/Users/WuGC/Documents/Codex/2026-10-03/shadow-pcf/final-validation.json`. Visual acceptance and timing were not performed for this PCF change.

This increases receiver shading cost without changing compute raster resolution or the 8M candidate-pixel budget. Static bake metadata must be refreshed after shader dependency changes, even though the depth image is unchanged. Validation for this step is script/shader import and Console checks; visual comparison and quality judgment belong to the user. Earlier crash/transition and timing results below apply to the preceding backend and are not a PCF performance or stability claim.

## Coverage fitting before the region split (2026-10-04)

Static coverage now fits authored receiver bounds. The baker compares the original orthographic basis, projected box-edge orientations, and bounded affine receiver-plane projections. The latter straighten a plane's oblique footprint without changing which points lie on the same light ray. Axes are normalized and perpendicular to the fixed sun; their positive determinant must be at least 0.5. Comparisons use physical light-plane area (UV rectangle area / determinant), not raw UV area. The common depth slab still includes all static sources and actor headroom. A 0.1 m coordinate border, texel guards and grid alignment keep the receiver boundary inside the sampled field. This is one fitted atlas, not sparse paging; remaining interior holes still occupy texels.

Dynamic coverage is fitted after conservative receiver clipping. Large partly relevant casters contribute only their intersecting light-ray region plus the PCF safety border. Offscreen casters remain eligible when they can shadow a visible 3D receiver. Invalid/unknown scopes remain unbounded, and late cameras still request a conservative rebuild. Width and height are fitted independently; sixteenth-octave buckets (at most about 4.4% immediate growth slack), 20% shrink hysteresis and texel-snapped origins reduce rescaling during small pose changes. No hot-path collections are added. Compute formats, atlas dimensions, 8M candidate-pixel and 262144-triangle limits are unchanged.

In the current scene, static physical footprint changed from 29732.86 to 15413.93 square metres: about 48.2% less coverage area, or 1.929x texel density. The axes changed, so raw XY dimensions must not be compared without the determinant. Serialized RFloat payload inspection found 5385009 / 16777216 nonempty texels before (32.10%) and 10388092 after (61.92%). This measures depth occupancy, not the percentage of pixels visibly in shadow. Texture memory remains 64 MiB.

The current single-caster Editor preview fits approximately 1.919 x 2.282 light-coordinate units. Against the old square-fit rule in the same new projection, its rectangle area is 15.2% smaller. This is not a claim that every dynamic scene gains the same amount: empty scope, actor placement, pose envelopes and hysteresis affect the result. Current candidate work is about 5.03M, below the unchanged 8M limit. The denser projection can increase triangle candidate work, so this change is a precision/utilization improvement, not a GPU-time benchmark.

Fifteen focused shadow regressions passed, including receiver/light-ray coverage, grazing-light conditioning, rectangular fit and grid alignment, grow/shrink hysteresis, filter-border clipping and unbounded-scope retention. Unity compilation completed with no compiler errors; the native Editor Console had 0 errors after rebaking. The unified MCP validator disconnected on domain reload, so persisted compilation evidence and native Console counts were used. No Player build or visual acceptance was performed. Local evidence is under `C:/Users/WuGC/Documents/Codex/2026-10-04/shadow-coverage/`. Source fingerprint v4 requires rebaking older assets; this scene has been rebaked through the Editor.

## Fixed center and periphery (2026-10-04)

The user selected a fixed battlefield center. CharacterTest uses `TacticalBattlefieldLayout` (80 x 60 m around world XZ origin) intersected with full receiver coverage, retaining the full receiver height. This is authoring-time partitioning, not a camera-following cascade. Framework baking accepts explicit center/full bounds and stays independent of game-specific dimensions.

The original 4096² texture budget is redistributed into 3072² center and 2048² peripheral textures. Total payload is 54525952 bytes (52 MiB), down from 64 MiB. The peripheral map covers the whole authored receiver region and supplies the fallback; its central overlap is intentional. Both maps use the same bounded-shear light basis and depth range. Every caster contributes to both maps through projection/raster clipping, so surrounding objects can cast into the center. A padded one-metre light-space band blends visibility at the center boundary. Most pixels sample only one region, and the coarse peripheral grid does not set the filter spacing for the center interior. Dynamic occlusion is combined on each ray within either region before filtering; the band blends the resulting visibility instead of multiplying shadows. Receiver-plane derivatives are computed in common light coordinates before branching.

Data format v2 stores the two texture references, both rectangles, center bounds, texture-budget side and transition width. Fingerprint v5 includes region selection and allocation policy. Rebuild old format-v1/fingerprint-v4 assets. `Depth.width` is now the center size; validation/rebakes must use `TextureBudgetResolution` for the budget, not that width. The original single-region Initialize overload explicitly aliases one texture into both regions and counts that storage once.

The current center is `(0, 8.922486, 0)` with size `(80, 18.4649734, 60)`. Its fitted light-plane footprint is about 4669.68 m²; the full peripheral footprint is 15459.25 m². Center texel density is about 1.86x the preceding single-4096² coverage-fit version, while total texture memory is lower. Twenty-one focused shadow regressions passed on the newly loaded format-v2 assembly, including budget allocation, center fingerprint changes, two-texture accounting/validation and full-quality receiver coverage before the transition band. The six checked WGame shader families have no reported shader errors, Unity native Console errors are 0, authoring validation passes and scene dirty state remains false. No Player build, GPU timing or visual seam acceptance is claimed; the user inspects appearance. Local evidence: `C:/Users/WuGC/Documents/Codex/2026-10-04/shadow-regions/`.

## Runtime path and lighting boundary

`MxDirectionalShadowProvider` records clear, coverage-count and triangle-raster Compute dispatches before opaque shading. It emits no mesh shadow draw. Play mode uses native position buffers from `GetVertexBuffer` only when the returned resource has the Raw target. Edit-mode skinned previews always use an owned CPU pose snapshot and do not request or borrow the renderer's native skin buffer, including after Play/domain transitions. CPU skinning on the observed D3D11 Editor returned a Vertex-only buffer despite requesting Raw; this resource must not be bound as a `ByteAddressBuffer`. Editor previews and incompatible runtime skinned casters reuse a CPU `BakeMesh(true)` snapshot and upload positions into an owned Raw buffer. The upload uses a 12-byte vertex stride and the renderer's local-to-world matrix; the native skinned path uses root-bone space. Rigid fallback positions are uploaded once. Both paths validate byte-address ranges. Indices and UV/alpha attributes are cached separately, so dynamic source meshes require **Read/Write Enabled**. Preview and build validation report this requirement explicitly. All active eligible scene casters are considered, including those outside the target camera frustum.

The current scene admits three skinned models and two rigid weapons. The repository's shader/coverage and debug-surface exclusions belong to `CharacterTestDirectionalShadowPolicy`; the framework receives a generic coverage resolver and caster filter. Transparent surfaces and materials without a shadow-caster pass are excluded.

Projection is light-space XY **plus depth**, not a floor XZ opacity mask. Receivers inside the captured depth range use their world position/depth, so the same field can shade three-dimensional surfaces. Outside that range, the receiver returns unshadowed visibility. Static and dynamic nearest-depth visibility is combined per ray before the 2x2 weighted filter. It is never multiplied twice. RFloat/positive normalized float bits in R32UInt preserve the common depth comparison; world-space bias and receiver-plane derivatives address depth-texel/PCF slope acne.

Only the main direct term consumes the new visibility. `WgSceneDiffuse` retains its existing normal-dependent indirect tint without using the new occlusion to dim GI. Prop palette direct diffuse/material response and traditional direct portions are attenuated once; their authored fill, weather, status tint, outlines and emission remain separate. The legacy path retains its prior behavior when this feature is disabled. Existing GI transport and its own occlusion mechanisms are not rewritten or presented as this new shadow solution.

While a valid runtime session owns this path, the configured sun uses `LightShadows.None`. URP's shadow-variant stripping can retain a shadow keyword and an empty compatibility shadow pass; the final runtime probe found `m_CreateEmptyShadowmap=True` and no regular main-light shadowmap target. That empty setup does not render casters. The provider never writes Renderer.shadowCastingMode. Authored Off is excluded from baking and every dynamic frame; On, TwoSided and ShadowsOnly remain eligible. Vertex-buffer targets, offscreen updates and the sun's prior shadow setting are restored on disposal/invalidation. Instanced vegetation uses its independent CastFixedDirectionalShadow flag, because its visible URP submission/prefab may intentionally use Off while fixed custom shadows remain enabled. Edit-mode previews do not persist these transient ownership changes. Ordinary URP/custom shaders need to opt into the receiver helper to receive this field; it does not automatically rewrite every shader.

## Buffer-correction validation before PCF (2026-10-03)

On Unity 6000.6.3f1 / Core and URP 17.6.0 / D3D11, process 106840 completed a 30-minute monitored session from 20:40:28 to 21:10:30 +08 without a new NVIDIA error or Unity fatal dialog. The session included compilation/domain reloads and two Play-to-Edit cycles. The Play spans recorded 5519 and 7970 shadow builds respectively, each with five active casters, native Raw buffers and an empty shadow diagnostic. After each return/reload, the Editor preview used one owned CPU pose, zero native skin-buffer acquisitions and valid static data. A post-transition depth readback contained 92057 written texels.

The seven focused `DirectionalShadowRegressionTests` passed. The compile observer recorded refresh completion, compilation start/finish with no errors, and a domain reload. The unified MCP validator lost its transport session and its Console adapter also misclassified pre-existing compiler warnings; the fallback used the persisted compilation record and Unity's native Console counts (0 errors). This is not reported as a successful unified-tool response.

A subsequent complete Editor restart (new process 125940, started 21:14:12 +08) retained fingerprint `06402ce5c4209882faaf581c3a3c9f3d`, passed authoring validation, rendered the owned preview pose and had 0 native Console errors. The scene file and static depth texture remained byte-identical to the pre-fix backup; the bake metadata fingerprint and four model importer Read/Write settings changed. The imported models are characters 000001, 000004, 000009 and common weapon 000001. Importer serialization also advanced to the current Unity schema through the Editor.

The detailed local evidence is `C:/Users/WuGC/Documents/Codex/2026-10-03/shadow-code-isolation/final-repair-validation.json`. No Player build was run. This bounded observation supports the corrected Editor pose path; the earlier 20:29 device reset remains recorded, and the result does not prove that every possible device-loss cause has been eliminated.

## Animated bounds and work limits

The imported `Renderer.bounds` was insufficient for a real attack pose. The provider builds each bone's influenced-vertex envelope once in bind space. Each frame it reads bone matrices, transforms envelopes whose matrices changed, and unions the cached envelopes to conservatively enclose normalized positive linear-blend skinning. This controls atlas coverage and validation; the envelope is **not** rendered as a shadow. Runtime silhouettes still come from actual current-pose triangles and alpha. Compatible native Raw buffers in Play mode avoid CPU re-skinning. The compatibility path performs CPU skinning after receiver culling and reuses its mesh/list/buffer; it has an additional CPU and upload cost that the pass execution timer does not include.

## 2026-10-03 corrective changes

The first correction (actual Raw-target guard plus GPU-recording removal) did not eliminate the crash: process 241592 hit another NVIDIA event 153 / Unity device-loss dialog at 20:29:48 +08 after Play exit and script reload. Edit-mode native-buffer borrowing was then removed entirely. Treat this ownership change as a separately validated path; the earlier failure remains counterevidence against claiming the Raw guard or timing removal alone solved all device resets.

The crash investigation confirmed an unsupported buffer binding: the CPU-skinned buffer was D3D11 dynamic usage, Vertex-only bind flags, and no raw-view flag, while the compute shader expected a raw shader resource. The provider now checks actual resource targets and supplies owned compatible storage. An earlier GPU-timing-only causal conclusion was withdrawn after another failure with timing disabled; stopping GPU queries alone was not a sufficient fix. The sampler now uses a separate CPU-only marker and does not start GPU recording. `GpuMilliseconds == 0` means unmeasured, not zero cost. Historical GPU measurements below describe the earlier implementation and are not performance acceptance for the CPU fallback.

Material coverage now matches [Unity property-block precedence](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Renderer.SetPropertyBlock.html): a per-material block replaces the whole per-renderer block. Missing properties fall back to the material. This applies to alpha, cutoff, dissolve, textures and UV transforms; default framework cutouts now honor texture scale/offset too. Bake fingerprint v5 includes fixed center/periphery bounds and allocation/blend policy and distinguishes subassets by GUID/local file ID and detects procedural mesh content changes. Built-in fallback textures use canonical identities, and equivalent procedural materials use shader identity plus resolved coverage; process-local EntityIds are excluded. Other transient inputs must be saved as assets before baking. Older bakes require explicit regeneration.

`DirectionalShadowRegressionTests` covers whole-block precedence, default UV transforms, same-file subasset collisions, in-place procedural mesh edits, equivalent recreated materials, Editor-owned CPU pose selection/layout/space/update/reuse, and unreadable-mesh rejection. Live validation and its duration must be reported separately from compilation and these focused regressions; a short healthy run cannot by itself prove that every possible device reset is eliminated.

The current three models have no blend shapes or Unity vertex-cloth component. Such displacement sources require a validated additional bound contract and are rejected by this version rather than silently approximated. Magica's current bone-driven poses in this scene matched the GPU buffer references used in validation.

Count every admitted triangle before rasterizing any source. Limits are 262144 triangles and 8388608 candidate bounding-box pixels for each complete atlas build. Overflow, invalid/nonfinite positions or an out-of-bound pose disable dynamic visibility for that frame; a truncated silhouette is not accepted. A 65535-group dispatch split is only a D3D11 dimensional limit. These limits bound work, not a universal GPU-time or TDR guarantee.

The normal Play path builds one world atlas per frame for the union of permitted receiver cameras. Main-camera/SceneView consumers rebind it. A late or moved camera can trigger one additional unculled rebuild in that frame; the existing per-build limits apply separately, so the diagnostic name ComputedFrames counts builds, not necessarily distinct Time.frameCount values. Non-target cameras receive disabled globals; UI overlay cameras also reset globals for their own scope. The emergency `DynamicRasterValidationApproved` switch defaults true after the 2026-10-03 integration and focused native acceptance. Full 1024 dynamic clear/count/raster work is restored for normal use. The switch remains available for emergency suspension; the persistent static field is independent.

## Invalidation and supported changes

Editor source/coverage/layout/fixed-direction changes invalidate the saved fingerprint; explicit rebaking is required. Build scene processing rejects stale data. Runtime direction mismatch suspends the path and restores ownership. The authored static geometry/coverage/layout is an immutable runtime contract: code changing it must call `SetDirectionalShadowSourceValidation(false, reason)` rather than continuing to use the old asset. Changing any captured static casting flag (including an authored Off becoming On) or the captured vegetation flag disables the old static field until the original flags are restored or the asset is rebaked. Dynamic transforms, current skin pose and material/property-block coverage are sampled at runtime; added/deleted/replaced dynamic mesh sources refresh every 0.5 s. Cached topology, UVs and skin-weight envelopes assume mesh data is immutable in place; invalidate/rebind when changing those inputs.

## Historical receiver influence and CPU caching acceptance (2026-10-03)

The following records the earlier implementation and its validation. Its all-caster rectangle and hard-filtering statements were superseded by the coverage fitting and PCF sections above; the recorded results are not acceptance evidence for those later changes.

CullDirectionalShadowsToReceivers defaults true. Each permitted camera's six actual culling-frustum planes are clipped by the shared light-depth slab and projected to conservative light-space XY bounds. This is a volume of three-dimensional visible receivers, not a floor-only mask. Casters are rejected only when their projected bounds cannot overlap any receiver region, including a texel/filter/bounds margin. Height, offscreen casters and procedural receivers are retained by the same-ray test. There is no aggressive depth-order pruning. Invalid, degenerate, empty, stereo or incomplete camera scopes retain all casters. The project Editor adapter supplies every SceneView; an absent/failing adapter also retains all. A new or moved late camera triggers the full rebuild described above.

The dynamic atlas rectangle still uses all eligible candidate bounds before influence culling. Resolution, texel scale, hard filtering and static depth format remain unchanged. Culling skips rejected pose acquisitions/imports, triangle counting, alpha sampling and raster work. The full 4 MiB dynamic clear remains, and Unity offscreen skinning is still enabled for supported skinned sources: this does not claim to eliminate their engine skinning work. No new draw or approximate silhouette is introduced.

Model position layout, immutable index/UV attributes and rigid position-buffer wrappers are cached with shared-mesh identity/validity checks. Skinned position wrappers are acquired anew for each build, with a conservative same-frame-camera rebuild retaining already imported wrappers. Bone envelopes are re-transformed only for an exactly changed matrix; current Renderer.bounds and all cached envelopes are still unioned. In-place mesh topology/UV/skin-binding mutation continues to require session invalidation/rebinding. Shader property layout is cached by Shader identity and cleared on Editor project changes/domain reload. Material values, textures, UV scale/offset and keywords are read once per build epoch per shared material. Live object/submesh property blocks are overlaid on each use, with submesh precedence; removed overrides return to the current shared values. Consecutive equal coverage states skip repeated Compute parameter binding. No material hash is assumed to be a reliable version signal.

Offline evidence: all four affected assemblies compile with Unity's existing response files and references redirected to workspace outputs; 420000 seeded candidate points yield 69575 valid orthographic/perspective same-ray tests with zero false culls (139166 checks total). Extracted production coverage/cache logic matches the previous policy in 4500 seeded opaque/cutout/foliage/Lite combinations (27000 checks), including alpha/cutoff/dissolve/texture/keyword/UV changes and object/submesh override removal. The latter uses managed Material/MPB/CommandBuffer stubs and proves logic parity only. Neither test runs the Editor or measures GPU time. Focused native acceptance subsequently passed: 2400 actual Material/MPB cases across four repository shaders, 4800 policy/overlay comparisons; four GPU influence stages at 1024 (offscreen caster retained, disjoint caster culled, auxiliary receiver-camera union, incomplete-camera fallback); unchanged atlas rectangle and zero visible-receiver depth differences versus unculled coverage; camera GPU globals 1/1/0/1 with one late-camera conservative rebuild followed by reuse; eight cast-mode/static-invalidation stages at 1024; and 90 actual full-scene frame advances/builds with five casters, no rejection and no camera fallback. Rigid pose-buffer acquisitions were zero after initial acquisition, while current skinned buffers were acquired three times per normal build. This is focused acceptance, not a Player-build or performance benchmark.

CandidateCasters/CulledCasters, PoseBufferAcquisitions, BoneEnvelopeRebuilds, ReceiverRegionRebuilds, ConservativeCameraRebuilds, MaterialCoverageReads, CoverageOverrideReads and CoverageBindingChanges expose work counters. Storage stays at the existing formats/resolutions; counting fewer inputs is not a measured GPU speedup. No new timing or equal-quality URP comparison is claimed.

## Validation and performance evidence

Target: Unity 6000.6.3f1, URP 17.6, Windows D3D11, RTX 4080. Native compilation and final Console error counts passed. The general script validator repeatedly lost its MCP session during its refresh; native ready/type-loading/Console checks and the actual render tests supplied the completion evidence.

- 64² Compute fixtures: **11/11**, including triangle coverage, red-channel versus texture alpha, vertex alpha, dissolve, nearest overlapping depth, single and aggregate candidate-budget rejection, NaN, bounds and triangle-budget rejection.
- GPU lighting fixtures: **6/6**. GI lit/shadowed values both `(0.6, 0.7, 0.8)`; direct occlusion reaches zero; a Prop half-visible composed value remains `0.94`; legacy GI tint remains `(0.2, 0.4, 0.6)` in its tested shadow case.
- Actual camera test: main → SceneView → non-target → main in the same frame. Three enabled bindings, no extra compute, identical atlas rectangle, GPU enabled states `1/1/0/1`. Probes ran at individual camera completion, before a later UI overlay reset; probing after the whole camera stack would misidentify the overlay's disabled state as the main camera state.
- Continuous camera reuse: 180 frame advances / 180 computes / 667 bindings. The accepted post-bound sample also had 119 frame advances / 119 computes / 446 bindings.
- A real card selection and confirmation both returned `None`. The accepted finite action run completed 60 frame advances, sampled six different GPU poses per model, included 52 samples outside the target camera frustum, and produced no dynamic rejection. Across 65 position samples/model/snapshot, CPU-reference versus GPU world-space maximum errors were `5.61e-7`, `6.34e-7` and `1.91e-6` m. In that earlier backend, `BakeMesh` and pose readback were validation-only. The corrected backend now uses `BakeMesh` in Editor previews and when runtime native buffers are incompatible; it still performs no production pose readback.
- Historical 2026-10-02 soft-shadow/filter-radius 0.04 m sample at 1024² post-bound sample, main + SceneView enabled: 30 warmup frames, **120 measured frames**. Owned-pass GPU mean **0.870769 ms**, P95 **2.258944 ms**, max **2.701312 ms**; maximum candidates **437806**. Owned-pass CPU marker mean **0.073266 ms** measures pass execution/command recording, not all RenderGraph preparation, source refresh or bone-envelope CPU work. Receiver fragment sampling and whole-frame GPU time are not included in this marker.

The earlier single-camera sample before the bounds fix averaged 0.781235 ms at 1024². Do not substitute it for the final result or interpret differences between short Editor samples as a scaling law. No equal-quality URP A/B or Player build benchmark was completed; this evidence does not show that Compute is necessarily faster. The original unbounded implementation was active when a D3D11 fatal device-removal/TDR occurred before recovery; its sole causality was not isolated from driver/other workload factors. The listed bounded-backend checks completed at that time. A subsequent 2026-10-03 D3D11 device reset/removal occurred after source reload; its cause remains unisolated. One authorized recovery and the focused cast-mode fixtures subsequently completed. The full dynamic backend was subsequently restored after the focused 2026-10-03 acceptance described above. OS timeout/protection settings were not changed.

Logical GPU storage: static depth **64 MiB**; current provider-owned textures/index/attribute/status buffers **9480648 bytes** (~9.04 MiB), of which the dynamic depth atlas is 4 MiB. Static texture CPU readability is disabled. The serialized static texture file is 134218786 bytes; disk bytes are not GPU storage or runtime performance. Unity's existing skin buffers, GI resources and other scene allocations are additional. Atlas precision still depends on world coverage, resolution and filtering; finite sampling can lose thin features.

Optimization evidence is in shadow-priority-optimization/offline-compile/results.json, offline-math/results.json, offline-coverage/results.json, native-coverage-results.json, native-influence-results.json, native-camera-results.json, native-casting-results.json and native-full-scene-results.json. An asynchronous influence-fixture timeout is preserved separately; the passing native influence evidence uses a synchronous controlled fixture and is not represented as that failed scheduling attempt. Final post-reload state is in optimization-editor-release.json.

Original evidence is retained in the task workspace: `shadow-small-fixture-results.json`, `shadow-lighting-fixture-results.json`, `shadow-camera-fixture-results.json`, `shadow-battle-animation-results.json`, `shadow-accepted-1024-timings.csv`, its completion status, and `shadow-final-acceptance.json`. The earlier failed action/bounds evidence is retained separately. Actual captures are under `Captures/DirectionalShadowValidation/`; the final accepted view is captured without the MCP screenshot helper's `EditorApplication.Step()` behavior. That helper explained the earlier paused Editor, not a second GPU failure.

Saved scene changes are limited to the eight shadow configuration fields. New HUD work, camera focus-height changes and other prior working-tree edits are preserved. No commit, whole-project test run or Player build was performed.

## Player shadow quality setting

CharacterTest Settings > Graphics exposes “Shadow Quality” with Default / Low / Medium / High, persisted through the existing settings profile and Apply/Cancel flow. Default preserves the scene's authored dynamic resolution (currently 1024). Low, Medium and High use 256², 512² and 1024² dynamic R32_UInt targets: 0.25, 1 and 4 MiB respectively. During a resize, the old and replacement targets coexist briefly until allocation succeeds; steady state owns one dynamic target. This changes dynamic clear/raster area, not the triangle budget, 8M candidate-pixel cap or receiver texture-fetch count. Actual GPU cost must be measured separately.

Static center/peripheral data and grass plane coverage remain at their baked quality (current scene: 4096² / 2048²). Switching settings never rebakes, raises static precision, changes GI, enables disabled casting, or adjusts the existing edge filter. No runtime Off option is exposed because the backend also owns baked/static visibility and the URP main-light shadow handoff. The old generic URP main-light atlas preference remains compatible in saved profiles; this formal CharacterTest row now controls the custom backend.
