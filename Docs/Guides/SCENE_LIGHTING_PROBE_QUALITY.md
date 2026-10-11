# SceneLighting probe coverage, preview and quality

The implementation is integrated in the main project as a scoped working-tree change. The original scene, 4 m spacing, current GI cache and user defaults remain unchanged. Quality is explicit opt-in and does not trigger an automatic bake.

## Implemented behavior

- Sampling coverage can be authored independently of static capture geometry. The shared planner supplies preview and builder bounds, origin, spacing and count. Capture far distance covers the selected geometry even when a probe lies outside its bounds.
- Preview reads actual Transport.bytes positions and validity. It displays cached bounds, boundary validity and the filter pipeline; height slices use coordinate-scaled floating-point tolerance. Height, distance, valid/invalid and draw-cap filters precede point selection and directional distance statistics. Missing or stale caches are indicated without baking.
- Explicit local refinement volumes add half-spacing bricks with shared probe IDs and deterministic base-cell lookup. Surface and medium interpolation use the topology and blend back to the base lattice near brick boundaries. This is user-authored local refinement; automatic adaptive-density selection is not implemented.
- Quality-enabled captures use an exact triangle BVH over the capture source. Near/interior probes try bounded relocation relative to local spacing. Accepted final positions are freshly captured for transport, sky and distances. Unrecoverable solid-interior probes remain invalid. The candidate directions do not guarantee recovery in every complex mesh interior.
- Dilation uses directly valid neighbors only, constrained by bidirectional conservative distance and exact-geometry segment checks. The GPU coefficient pass reads immutable source coefficients. It is skipped when the cache has no dilation links.
- v2 stores topology, final positions, clearance/relocation diagnostics, nearest distances and dilation rows. v1 remains readable; unknown versions, truncation and invalid indices are rejected. Source, spacing, coverage and quality fingerprint changes invalidate the cache.
- Surface leakage suppression combines probe visibility with receiver tangent-plane support. Nearest directional distance constrains that support, rather than cutting every surface ray at a cone-wide minimum; the latter produced angular dark islands in the owned fixture and was corrected. Medium sampling retains the conservative minimum-distance gate.

Static capture membership remains the explicit source/root contract. Tactical geometry uses BaseFacility_Exterior; BasicVegetation is excluded by the source policy rather than treating every Static object as capture geometry. Runtime performs no scene traversal or CPU raycasts for this feature.

## Native validation on 2026-10-02

The initial native compilation rebuilt 12 assemblies with no compiler errors; its validation wrapper later timed out on Console warning classification. The final incremental validation job 0882eea284b749e1922c69c1091dcfa8 passed after cleanup: refresh completed, compilation started and finished, domain reload observed, Editor ready and Console errors zero. It found no further assemblies requiring compilation.

Thirteen focused EditMode tests are covered by two native runner receipts: the first run passed 12 and exposed an overly strict floating-point vector assertion; the corrected geometry test then passed 1/1 in the native runner. Tests include coverage/cache invalidation, slice boundaries, refinement indexing/boundaries, version and malformed topology rejection, wall/doorway geometry checks, and actual final-position recapture. No Player build was run.

The actual SceneRendering panel and SceneView verified cached preview, draw cap, invalid-only filtering, native mouse point selection, planned grid, stale-cache and no-cache indicators. Stale/no-cache checks used read-only configuration clones. Four owned variants rendered wall corner, doorway, indoor/outdoor and thin wall views. Generated cache assets and images are retained in the external evidence directory; all owned Unity assets, helper scripts and additive scenes were removed after validation. The original scene is active and clean, non-Play, with 744 vegetation instances; original Profiler settings were restored.

## Recorded cost and limitations

The owned fixture uses indirect lighting only, with a zero-intensity directional sun, and produces intentionally dark stress views. It is representative diagnostic evidence, not a proof that all real-world leaks are eliminated. The four views were replayed from the existing owned caches after the surface-support correction; no additional bake was needed.

| Variant | Probes | Bricks | Relocated / dilated | Bake seconds | Payload bytes | Persistent GPU buffer bytes | Camera.Render CPU mean ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| v1 legacy | 180 | 0 | 0 / 0 | 3.468 | 234016 | 17061052 | 1.492 |
| v2 quality | 180 | 0 | 3 / 0 | 3.005 | 308240 | 17131140 | 1.439 |
| v2 refined | 377 | 18 | 18 / 0 | 4.496 | 618840 | 17484144 | 1.630 |
| v2 controlled dilation | 144 | 0 | 0 / 3 | 2.723 | 243828 | 17062216 | 1.470 |

These are one Editor run on RTX 4080 / Direct3D11, 480x360, 8 warmup and 32 sampled renders per view, averaged across four views. CPU timing includes Camera.Render and is not isolated shader CPU cost; persistent bytes count provider-owned buffers, not total application VRAM. Controlled dilation uses different coverage and is not an equivalent performance comparison. Each replay reports one world update/pass across repeated camera renders.

GPU timestamp sampling returned zero blocks despite supported/enabled GPU profiling. GPU milliseconds remain unverified; zero samples must not be reported as zero cost. No speedup claim or general performance guarantee is made from these samples.

The final evidence manifest records the 18 source/document files plus three Unity-generated source .meta files, original per-file hashes, final hashes and reverse-apply check. No commit/push, formal cache bake, scene save or management-owner change was performed.

## Automatic two-level layout and Scene range editing

Enable probe quality and **自动选择加密格** in Level Editor > 场景渲染 > 探针布置预览. No manual boxes are needed: the shared planner uses exact selected static geometry, receiver normal/clearance changes, axis occlusion, baked NavMesh activity clearance and configured height fog/dust demand. Optional activity boxes add stable authored demand. Current camera, moving actor poses and frame brightness are never layout inputs. Scores are geometric heuristics, not measured irradiance error or proof that every valid probe is necessary.

The layout keeps the complete base lattice and adds only half-spacing bricks. Exclusion boxes override both manual and automatic refinement; they **do not delete coarse probes or remove interpolation coverage**. Manual boxes have priority. Manual refinement can be disabled while retaining its definitions for comparison. Automatic candidates are ordered by descending importance and stable cell ID; cost counts only new shared point IDs. By default automatic selection uses 35% of the budget left after the base lattice and manual overrides. Candidates that do not fit are skipped deterministically. In automatic mode, an over-budget base lattice is coarsened until complete coverage fits; the actual spacing and omitted refinement count are reported. Legacy manual-only over-budget behavior remains unchanged.

Total coverage is white/cyan, manual boxes purple, exclusions red, activity boxes yellow; orange boxes show read-only automatic selection (display capped, actual selection unaffected). Enable **在 Scene 拖拽范围** and select **编辑范围**. Move the XYZ handle or drag a bounds face to resize. The selected center/size fields synchronize during dragging and all fields synchronize at release. Undo/redo restores scene-owned definitions and refreshes planning. Probe dot clicks only inspect probes and are disabled while editing a range. Editing never starts a bake automatically. Explicit automatic builds write a timestamped independent cache directory, then adopt the result without saving the scene.

The automatic algorithm version, parameters, exclusions, activity/medium inputs and authored navigation fingerprint participate in cache identity. Disabled automatic mode with legacy manual inputs retains the exact v2 fingerprint. Transport remains v2 indexed half-spacing bricks; existing payloads remain readable. Data additionally records requested base spacing so a budget-coarsened cache can validate its original authoring input. NavMesh demand is conservative triangle-bounds occupancy, not a proof of gameplay reachability; an absent baked NavMesh simply adds no navigation demand. Fog demand is conservative and independent of camera focus clearing. Multi-level refinement, radiance-error feedback, arbitrary sparse probe clouds, redundancy pruning and per-frame actor-based redistribution remain outside this step.