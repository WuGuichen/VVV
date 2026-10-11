# Scene lighting first revision — 2026-09-25

The formal implementation, rebuild steps, resource estimates and scope are documented in [Scene light transport](../../Guides/SCENE_LIGHT_TRANSPORT.md). These are actual Unity captures, copied without image editing. No performance benchmark was run.

## Fixed-camera comparisons

Final full-base view:

![Full base](whole-base-final.png)

Hangar, identical camera/sun, indirect contribution disabled:

![Indirect off](hangar-indirect-off.png)

Same view with indirect contribution enabled:

![Indirect on](hangar-indirect-on.png)

Current warrior material and skeleton, placed temporarily in the hangar shadow. The camera remains fixed; the walk animation can change pose between captures. The validation actor was removed afterwards.

| Indirect off | Indirect on |
| --- | --- |
| ![Character off](character-in-shade-indirect-off.png) | ![Character on](character-in-shade.png) |

## Frame inputs and isolation

- [Normals for the three current characters](character-normals.png).
- [Camera motion, continuously rendered frame](camera-motion-live.png).
- [Skeletal animation with fixed camera/root](skeletal-motion-live.png).
- [Root translation with fixed camera/pose](translation-motion-live.png).
- [Unbound camera](unbound-camera-isolation.png): remains on normal legacy output while the bound camera has the motion diagnostic enabled.

Motion captures encode neutral 0.5 + vector × 16. Small changes are intentionally faint; the associated numerical nonzero-pixel checks are in the guide. These prove available motion input, not DLSS SDK integration or temporal-upscaling quality.

## Validation receipt

Runtime checks completed: sun/sky/camera invalidation rules; warm indirect coefficient readback in the shaded hangar; source fingerprint mutation/restoration; three current character normal outputs; animated-skin, translated-actor and moving-camera motion; unbound-camera isolation; disable/re-enable resource disposal; scene save/reopen with valid cache. Temporary actors/cameras and debug views were removed/restored.

Compilation used the repository's Unity refresh/compile/domain-reload workflow. The MCP connection can disconnect on domain reload; results were recovered from the validation job status and Console. A transport timeout is not recorded as an atomic tool pass.

Final verification:

| Check | Result / evidence |
| --- | --- |
| Unity script validation | `68ce3a317d104820b886333becf17c94`: passed; refresh complete; compilation started/finished; domain reload observed; Editor ready; Console errors 0; compilation errors/warnings empty |
| Focused EditMode tests | `e552160f091e4270a1505c6de7d32b05`: 17 passed, 0 failed, 0 skipped (10 transport cases + 7 fog cases) |
| Shader compilation | Capture, Resolve, EnvironmentStylizedLit, CharacterStylizedLit, CharacterStylizedLitLite, CharacterPaletteLegacy, PropStylizedLit, DistantMountainVertexColor: zero ShaderUtil messages |
| Compute compilation | SceneLighting.compute: zero ShaderUtil messages; Inject/Bounce/Project exercised by GPU analytical test; Reduce exercised by full scene |

The first version of the new wall fixture put the rear receiver in an object transform, while the production capture shader consumes already transformed world-space vertices. That fixture initially failed. The rear vertices were corrected to world space; the final test above passed. Production capture shader/data were unchanged by this fixture correction.

During final Edit Mode camera-switch cleanup, the validation command mistakenly destroyed the previous main camera. It was reconstructed through Unity with the recorded overview pose `(0,56,-80)`, 60-degree perspective, sky clear color `(0.52,0.65,0.78)`, HDR/MSAA, UI-layer exclusion and URP additional camera data. Clip planes are explicitly 0.1/1000; the original serialized camera file ID is replaced. Scene lighting was rebound. A fresh Play startup confirmed the three actors and normal runtime additions (`MxCameraUnityRig` and `StudioListener`); no base geometry/layout was changed by this recovery. Runtime automatic target switching was rechecked separately in unpaused Play, rather than relying on Editor update scheduling.

After recovery, unpaused runtime automatic rebinding returned true and history advanced from 4 to 6. The final saved scene has seven roots, Main Camera assigned, source fingerprint matching the cache, debug view 0 and no temporary validation objects. Console errors remained zero after the final capture.

This receipt covers this rendering change, not unrelated CharacterAction/RuntimeCatalog tests or the entire project. No commit or push was performed.

## Follow-up: first three cost optimizations

Scope: opt-in motion input, camera-independent world resource lifetime, and a compact runtime sun occluder mesh. Depth/normal prepass, shadow quality, stable-frame update-pass recording and capsule spatial filtering are unchanged. No timing or FPS comparison was performed.

- Default GI requests depth/normals only. An explicit `RequireMotionVectors` consumer request or motion debug view 3 enables the rendering session's motion request. Original renderer skinned-motion settings are restored when the request ends; independent URP TAA/motion-blur requests remain supported by URP.
- A/B/null/back camera changes retain the same provider and native coefficient buffer. Live current-scene verification kept world update counters at 1/1 while camera history advanced from 2 to 11. Default, explicit and debug motion transitions were tested on a current actor, including restoring its original skin-motion setting. Temporary test cameras were created and removed within a guarded cleanup block.
- The existing `Occluders.asset` was regenerated in place from unsplit source triangles with exact position welding: 145,361 vertices, 677,832 indices. Future full builds use the same independent runtime-mesh generator. A valid existing cache can use **Rebuild Sun Occluders Only**.
- Three 256x256 sun-direction comparisons found zero silhouette changes and maximum normalized depth difference 0.000291645527. An initial diagnostic tolerance of 0.0002 flagged three pixels; the comparison is not bit-identical because the old mesh contained sampling subdivisions. The maximum difference is below the existing runtime visibility bias of 0.001. The comparison did not change that bias or simplify the source surfaces.
- Transport binary SHA-256 stayed `7A2EA63FBD9388362B23FD16329301CA567803E640CFC7A2E09C2687A8302DF1`. SceneLighting.asset and the occluder metadata were byte-identical; mesh GUID/file ID and scene references were preserved. The scene's only serialization change in this optimization is `RequireMotionVectors: 0`.
- Final Unity validation `6ef793f4f4dc4338a38c02950cefaa41`: refresh complete, compilation started/finished, domain reload observed, Editor ready, zero Console errors, no compilation errors/warnings in that validation job.
- Focused test job `7b57725595ca41b0ac41ffa912003d9b`: **19 passed, 0 failed, 0 skipped**, including opt-in input requirements, camera retarget/null/resume/disposal contracts and the real compact mesh's shared positions/attribute layout.
- Final [optimized scene capture](optimized-base.png) was inspected after exiting Play: Main Camera assigned, motion request off, debug view 0, valid source fingerprint, no temporary validation camera and zero Console errors.

## Follow-up: stable recording, capsule tiles and transient sun depth

Scope: stable cameras do not record world-update work; out-of-field/zero-contribution pixels skip unnecessary queries; capsules use per-camera candidate tiles; sun depth is a graph-transient raster attachment. Actor discovery, authoring fingerprint scheduling, depth/normal prepass and shadow quality are unchanged. No time or FPS benchmark was performed.

The world path now uses a separate sun raster pass and compute pass, with explicit resource dependencies. Only inputs whose update pass executed are marked applied. Camera data is copied into pass-specific property blocks, and each active capsule camera gets its own transient candidate buffer. The persistent sun visibility image remains camera-independent. Graph resource pools may retain physical storage beyond a transient handle's logical lifetime; scene ownership no longer includes a persistent sun depth RT.

Verification:

- Unity validation `255d6294a6754efdba5d1cc2b291a56a` passed: refresh complete, compilation started/finished, domain reload observed, Editor ready, Console errors 0. Two existing empty-switch warnings were reported in CharacterTestAuthoredContentIds.cs.
- Focused job `1856259ba44c408abd762d33cbd64c35`: **26 passed, 0 failed, 0 skipped**. Added cases cover the aggregate fade-error bound, projected bounds in both Y conventions, near-plane/behind-camera behavior, orthographic screen edges, GPU tile-edge overlap, the highest mask bit and empty candidate frames.
- Six alternating A/B camera renders with different target sizes added no world update pass or sun depth handle. Intensity and sky changes each added one compute update and no sun raster/depth handle. Sun rotation added one compute update, one visibility raster and one transient depth handle; the following stable camera added none. Disable/re-enable released owned resources and initialized a fresh cache exactly once.
- A temporary in-memory copy of the pre-optimization resolve shader supplied the full-traversal reference. Three fixed-camera raster fixtures used a 634x358 target (including partial edge tiles), 3/32 capsules, two perspective views and one orthographic view. Maximum differences relative to unoccluded irradiance were 0.271003%, 0.280899% and 0.285987%, all below the 1% analytical budget. Reference occlusion reached 19.0–21.6%, so this was not an empty-input comparison. The temporary shader and cameras were removed and all scene/look references restored.
- A manual validation cleanup initially invoked Main Camera.Render directly on the Editor backbuffer and produced a 2506x1038 versus 2560x1440 final-blit/overlay attachment mismatch. That cleanup was changed to return rendering to Unity's normal camera loop. After clearing those recorded fixture errors, continuous unpaused Play with the three current characters produced zero Console errors, zero Resolve/Compute shader messages, and stable world/depth recording counters of 1/1.
- The scene and transport binary remain unchanged by this batch. The runtime exposes 44,279,696 estimated persistent bytes; transient depth and per-camera mask storage are described in the guide. These byte counts describe resource ownership, not a measured reduction in total GPU residency.
- The [final batch-two capture](optimized-batch2-base.png) was inspected after leaving Play. Normal view and Main Camera were restored, the source fingerprint matched, temporary cameras were absent, and Console errors remained zero.

## Follow-up: remove the old receiver-lighting path

Scope: EnvironmentStylizedLit and the three character shaders that consume Scene Lighting (full baked, Lite and palette-format CharacterPaletteLegacy). Their material input formats remain supported, but all now use the common direct/indirect diffuse response. Disabled/missing/out-of-field cache no longer selects the former SH/hemisphere/value-floor/fog treatment. Without a cache, only indirect contribution is absent; direct light and the retained material/effect features still render.

Removed old diffuse/value/fog calculations and obsolete Lite vertex outputs. Character additional-light diffuse uses the common response, with full/legacy profile specular retained. Kept hair/specular/MatCap, edge/rim, emission, procedural detail, weather, status/hit, dissolve and LOD features. Retired controls are hidden in the existing material inspectors; serialized keys and CBUFFER layouts remain for compatibility. Shared frame inputs were extracted from the fog include so the new consumers no longer depend on fog for time/weather declarations.

Evidence:

- Unity validation `05b8f9af6db941478121b2af7ef6f856`: refresh completed, compilation started/finished, domain reload observed, Editor ready, Console errors 0, compilation errors/warnings empty.
- Test job `04f1c5413b5b46abb9e4faaa53210643`: **36 passed, 0 failed, 0 skipped** (lighting/fog plus shader-family pass coverage). The added GPU fixture returns (0.4,0.8,1.2) for no-cache direct light despite stale Look globals, returns zero with zero light, and preserves direct-only results outside the field.
- [Unified base lighting](unified-lighting-base.png) and [cache disabled: direct-only base](unified-direct-only-base.png) were captured from the same camera. The latter no longer restores the original blue ambient/fog treatment.
- [Current three characters](unified-lighting-characters.png) rendered with full baked and palette-format shaders. [Lite palette/face rendering](unified-lighting-lite-character.png) was checked using temporary material copies on the current palette actor; originals were restored afterwards.
- Four changed shaders and the prop shader using the shared frame declarations reported zero ShaderUtil messages after compilation. The source audit found no calls to old three-band/SH/hemisphere/diffuse-compression/fog fallback in the four changed forward paths.
- Initial shader compilation exposed that weather/time globals had been declared in the removed fog include. They were moved to the guarded shared frame-input include; the final compilation and GPU tests above passed.

This removal does not rename palette-format shaders or remove helpers still used by unrelated standalone prop/distant-mountain/VFX families. The initial legacy-camera screenshots earlier in this document are historical evidence, not the current no-cache behavior. No scene layout, model source, material assets or transport data was rebuilt.

## Height fog restored as an independent scene effect

The scene now saves GI and height fog bindings on its unique GameManager. Level Editor's **场景渲染** module exposes independent switches, Main Camera/renderer-feature/shader references and the saved fog density 0.18, base height 0, falloff 0.12 and distance 180. It reuses the framework's full-screen height-fog composite after scene color/transparents and before post-processing. Fog is independent of GI cache availability. Noise is off; fog color is converted from authored sRGB to linear.

The published base source changed during the Level Editor migration, initially invalidating the retained cache. On 2026-09-26 the user initiated a full rebuild. It exposed two new material slots in `Arch_Hangar_MainBay` that had inherited Unity's default Lit because the generated module only overrode the original five slots. Both slots now reference the manifest's WGame baked material; the module diff contains only those two new references. A fresh full-base build completed with 38670 surface regions and 7448 probes. Its fingerprint and scene-build preflight pass and GI/fog preview is active. A final audit also replaced the disabled collision-ground renderer's default Lit reference with the project's existing WGame ground material, retaining its disabled state and collider settings. All scene renderers, including inactive/disabled ones, now have zero default URP Lit assignments. The base-layout prefab hash is unchanged; the cache was updated in place and the narrowly changed scene was saved through the coordinated save path. Console errors after validation: 0.

`CharacterTestScene` has a separate new GI cache compiled from 32 explicitly selected existing MeshRenderers: 1463 surface regions, 5445 probes and a 7,847,076-byte transport payload. Moving test objects, grid lines and validation markers were excluded without reparenting or duplicating scene meshes. Its source fingerprint, payload validation and scene-build preflight pass; Edit and Play sessions both register GI and fog, and the preview resumes after Play. The cache assets and GameManager scene references have been saved. No frame-time benchmark was run.

Skin and dark-side follow-up: the baked, Lite and palette shaders now apply the same skin diffuse chroma response (default light-color influence 0.25). The shared evening Look raises existing sky/indirect illumination and reduces its extra blue shadow tint; the character fabric response no longer clips evaluated dark-cloth lighting to a fixed body ceiling. The focused GPU/pass suite passed 12/12, including diffuse luminance preservation, zero-light output, non-skin identity, neutral-light identity, HDR, additivity across light contributions and dark cloth responding to increased illumination. All three character shaders reported zero compiler messages. Baked and palette characters were inspected in the active scene, and a temporary Lite material was rendered and restored.

The final [`skin-readable-dark-side.png`](skin-readable-dark-side.png) uses a linear viewport readback verified against Windows screen pixels. The default MCP Scene View screenshot path applied an extra sRGB conversion and appeared too pale; those earlier captures are not used to judge this brightness revision. The skin response adds no texture sample or render pass. Current scene and both transport payload hashes remained unchanged; only the shared Look asset was saved for the new illumination values.

## Curved-receiver GI black-patch correction (2026-09-26)

At a fixed Scene View in TacticalCardDemoScene, black patches appeared on the character's chin, fringe, sleeves and nearby spheres. Reducing direct shadow strength did not remove them. A temporary irradiance view reproduced them, and a diagnostic color for zero total probe weight matched the patches. Removing tangent rejection temporarily removed the patches, confirming empty interpolation support rather than a material-color issue.

The production correction retains moment visibility, invalid-probe rejection and the tangent gate for ambiguous bins. It smoothly admits back-hemisphere probes only when the receiver is before a conservative lower bound on all 16 captured obstruction distances. Existing cache format, world radiance, Look values and scene bindings are unchanged. There is no new texture, pass, probe search or illumination floor.

- The new GPU fixture renders the actual production resolve shader. Before the correction, the open-space case returned red 0 instead of the expected 0.2. After correction, job `6babee9a7a1e4bf6ba1c7b910efa6b29` passed **4/4** cases: unobstructed back-hemisphere support, an opaque wall, an equal wall/sky mixture and a bin with one near obstruction plus fifteen sky samples. Each case sweeps 21 receiver positions/normals and checks RGB and preserved depth. The obstructed cases remain black rather than admitting the bright rear probes.
- Unity script validation `8fc8a96a7a81421ca30653cff8a46f26` confirmed refresh, compilation started/finished, domain reload, Editor ready and Console errors 0. The final resolve shader imported successfully and reported zero ShaderUtil messages.
- Fixed-view linear viewport captures: [before](blackspot-before.png), [after](blackspot-fixed.png). The final frame has GI enabled, normal preview mode, the original soft sun shadows and a clean scene. No temporary diagnostic shader remains assigned.
- The scene, shared Look and both transport payloads are byte-identical to this correction's baseline. No cache rebuild, model/material migration, player build or performance benchmark was performed. Thin geometry between the finite visibility samples remains outside an exact-occlusion guarantee.

At the fixed Main Camera immediately before and after the component migration, the captured PNGs were byte-identical (SHA-256 `E49739AEC65083E58B45D8FE46469C112A2E32956C1FB2DCC0CA594D320CA01D`); the versioned image is [`height-fog-final-base.png`](height-fog-final-base.png). This comparison predates the later base-source republish and therefore does not claim that the currently stale GI cache is accepted.

## Editor Play-return preview reuse (2026-09-26)

`CharacterTestRuntimeAiLabScene` has GI enabled with a 19,254,612-byte transport payload, 24,384 surface regions and 7,448 probes. A live Editor diagnostic before this change took 167 ms to read/decode the payload and 169 ms for a manual lighting rebind; the same source-fingerprint check took 17 ms. These are single-call diagnostics, not frame-time or FPS measurements.

The Editor preview now keeps one validated CPU payload across Unity's Play scene reload. It compares the data/transport asset dependency hashes before reuse and clears the copy when the settled Edit-mode scene changes or GI is disabled. Runtime GameManager decoding and per-session GPU ownership are unchanged. Authoring changes trigger source validation via object/project changes, with a ten-second fallback for edits outside those notifications instead of an unconditional two-second full-scene scan.

An initial Play-return check exposed that `sceneClosed` also fires during the Play boundary and was clearing the CPU copy. Cleanup now waits until the active Edit-mode scene actually differs. The final Play→Edit check retained the exact same decoded payload object, restored active GI with no source issue, and left the scene clean. A manual warm rebind took 2 ms in this Editor session. Unity script validation `bc7f94b998bb4aa59fcd7ac9177bd69c` confirmed refresh, compile start/finish, domain reload, Editor ready and zero Console errors. First-frame sun/world GPU update still occurs after each new session, so these checks do not claim a measured end-to-end Play-exit duration.

The bound camera marks pipeline height fog before opaque rendering, so old per-material height/focus fog is skipped. Mountains keep authored distance haze but skip their separate valley-height term while pipeline height fog is active. The camera-global reset prevents the override from leaking to another camera using the framework renderer. UI cameras are not targeted. The v1 effect uses opaque depth, so depthless transparents inherit the underlying depth; it is height fog, not volumetric ray marching.

- Unity validation `1bbd71f163b24ef6abef2fafe43206b7`: passed with refresh, compilation, domain reload and Editor readiness confirmed, Console errors 0, no compilation errors/warnings in that job.
- Focused tests `707774559b9341e78411b27a8664db7a`: **37 passed, 0 failed, 0 skipped**, including new camera targeting, material-override/composite ordering, opt-in Scene View preview, target switching/null, disabled state and disposal checks.
- Real renderer checks: an unbound camera did not execute the fog pass; disabling GI still executed fog; disabling/re-enabling the fog component released and recreated its owned material.
- [Final base fog capture](height-fog-final-base.png) preserves the authored mountain distance atmosphere and adds a restrained ground-height veil. The first stronger-density capture is not the final tuning.
- Scene integration migrates the former Scene Lighting and Height Fog bindings into GameManager and removes their empty nodes. Environment geometry, materials and the transport cache were not rebuilt. The old receiver-lighting branches remain removed.
- [Current characters with height fog](height-fog-characters.png) were inspected in unpaused Play. Fog continued executing after the runtime renderer setup, with no Console errors. Main Camera bindings were restored, the temporary camera was removed, Play was exited and the scene saved with the fog enabled.
