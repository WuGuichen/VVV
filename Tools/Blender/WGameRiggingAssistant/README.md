# WGame Rigging Assistant

Repository authority for the Blender-side WGame character binding workflow.

## Install

Existing Base Skin validation reuses one Custom-mask snapshot per component pass
(rigid Head adoption, explicit Head checks, automatic Head boundary checks). Distal
hierarchy checks build a sparse per-group influence index once instead of scanning
every Base vertex for every bone. These are per-call snapshots, not persistent
weight-result caches; editing weights is reflected on the next validation. The
checks and acceptance thresholds are unchanged. A 218-component character snapshot
measured 33.72 s before and 1.24 s after under the same cProfile run, with identical
complete result dictionaries; this is a fixture measurement, not a universal budget.

Zero-weight auto repair now plans three stages in one transaction: existing
topology neighbors first, bounded spatial sampling for disconnected pieces second,
then topology neighbors using the staged new seeds. Connected propagation follows
the configured edge-step limit inside the same Custom component and has no centimeter
cutoff. The spatial distance limit still applies across disconnected islands. Plans
are combined before writes, and existing nonzero weights remain unchanged.

**Across Fragments: Fill Zero Weights By Spatial Neighbors** complements topology
repair. It samples up to four original valid, normalized Custom vertices in the
same mesh within an explicit world-space distance (default 3 cm). Only donors
sharing the nearest donor's dominant bone are blended, capped at four influences
and normalized. Base-only donors, rigid masks and existing nonzero target weights
are excluded. Donors are frozen before repair so newly filled points cannot spread
weights beyond the distance limit. The operator backs up the Blend, verifies writes,
and reports remaining points; geometric proximity is not semantic certainty.

Whole-chain viewing preserves the original shaded material at zero weight and
blends heat colors at up to 55% opacity on affected regions. The current chain is
drawn independently: bright solid segments pass the viewport depth test; pale dashed
segments lie behind the model surface. Numbered bone labels match the sidebar rows.
Other rig bones and unrelated rigid/diagnostic overlays are suppressed during this
view and restored on exit. These depth cues complement orbiting the model; the
weight values and mesh materials are not modified.

Attachment picking is a toggle: click an unmarked Custom island to bind/mark it;
click an already marked green island to remove its rigid record and rigid mask.
Unmarking preserves existing Deform weights and other authoring masks. Hover text
states which action the next click will perform. There is no separate erase mode.
Esc/right-click exits; a session with changes finishes as an Undoable operation.

All registered rigid attachment regions have an independent persistent green
overview, enabled by default. Clearing hover feedback or binding a different
attachment does not remove earlier marks. The panel and viewport show region and
unique-vertex counts. **Hide/Show All Green Marks** only toggles visualization;
binding records and weights remain unchanged. Green means registered, not a claim
that weight validation passed. Records that no longer match current Custom islands
are counted as unresolved instead of coloring unrelated vertex indices.

The top **Rigid Attachments** box offers **Click Attachment And Auto Bind**:
hover a Custom island to preview an orange surface highlight and target-bone connector,
then click to immediately write rigid 1.0 weights to that island only. The highlight
turns green on success; viewport and sidebar messages persist, including invalid-hit
reasons. Picking remains active for the next attachment until Esc/right-click exits; normal
viewport orbit/zoom remains available. The target bone is highlighted and the result
shows the changed vertex count. Change the target dropdown and use **Rebind Last
Attachment** to correct it without reselecting vertices. **Bind Selected Attachment**
supports the existing Edit Mode selection workflow. Every commit requires a Blend
backup; other islands, Base weights and non-deform authoring masks are preserved.
These shortcuts apply weights immediately and do not rerun all Custom initial weights.
The original low-level mark-only operator remains for existing callers.

**View Whole Custom Chain Weights** at the top of WGame Rig draws a read-only
surface heatmap of the sum of weights for all bones in the selected scanned chain,
across participating meshes. Transparent=0, green=0.5, red=1; totals above one are counted
before display clamping. The panel shows nonzero vertex counts and positive weight
ranges for each bone. Zero coverage outside this chain is not an error, and totals
alone do not certify deformation quality. No temporary vertex groups are created.
Previous/next chain refreshes the snapshot; refresh after pose/weight changes.
Only original topology and Armature modifiers are supported for reliable mapping.
Exit removes the overlay and restores the previous editing mode and selection.

The top-of-panel **Temporarily Edit Bones (Remember Current State)** button switches
from the current object/mode into WGame armature Edit Mode. It becomes **Return To
Previous Editing State**, restoring the active/selected objects, mesh component
selection and visibility, active vertex group, and rig bone selection. Combined
Weight Paint + Pose contexts are restored. The return record is session-local,
not a saved Blend workflow checkpoint. Bone edits themselves are retained: changing
base bones invalidates base acceptance, while changing custom bones invalidates
custom completion. Returning does not generate or overwrite any weights.

While editing a WGame armature, **Bone Editing Shortcuts → Create Detached Child
From Active Bone** creates one Deform bone parented to the selected active bone
with Connected disabled. It starts beside the parent's tail at half its length,
inherits its roll and bone collections, and becomes the only selected active bone.
Use G to position it and F2 to rename it. Blender generates a unique `extra_` name;
the parent and existing weights are unchanged. The operation supports Undo and
invalidates Custom Bind completion; refresh scanned custom chains before weighting.

Finger-bone generation now reuses the checked five-finger topology analysis for
each hand independently. It partitions surface vertices by normalized geodesic
distance to the identified tips, orders the main fingers relative to the thumb,
and fits four center samples per branch. Joint fractions remain initial authoring
estimates, not anatomical joint detection. Each segment is sampled for surface-side
and branch-ownership checks; crossing into a neighboring finger rejects the plan.
Both hands must pass before any existing finger bone is removed. Generation requires
a saved Blend backup, preserves non-finger bones and mesh data, and restores the
previous armature data if commit validation fails. Nonstandard finger names or
non-finger children attached to finger bones require explicit manual handling.

Hand neutral analysis separates four main finger endpoints from the lateral thumb
branch. ROI and endpoint thresholds use hand-bone length and measured hand surface
reach, not character height. Both hands report their own failures. Ambiguous main
branches or thumb candidates block automatic neutralization rather than forcing
five results. In the hand-neutral panel, L/R **Show Candidates** highlights mesh
vertices. Select exactly one thumb-tip vertex in Edit Mode and use **Set Thumb Tip**
to supply a checked hint, or **Clear Hint** to resume automatic selection. Hints are
bound to mesh geometry/connectivity and must be renewed after mesh edits. Candidate
inspection only changes selection; analysis never straightens geometry or creates bones.

Major joint defaults now fit local body cross-sections for upright T/A poses.
The confirmed Base Skin vertex scope is preferred to avoid Custom Bind garments
and accessories; missing sections fall back to the main formal mesh. Arm markers
follow measured lateral reach and local height/depth; knees and ankles follow leg
sections rather than a fixed outward spread. Sparse/unsupported arm poses retain
the proportional fallback. These are initial estimates, not anatomical detection:
joint heights and hand proportions still need author review, especially with bulky
clothes, bent limbs, or unusual proportions. Negative Y remains the forward axis.
Recreating guides preserves confirmed or manually moved pairs and refreshes only
untouched generated defaults (including unchanged legacy defaults).
In Object Mode, left-click a guide circle or its label to select the guide and
synchronize the current sidebar item without reframing the view. With mirroring
enabled, clicking a right-side marker selects its left-side controller. With
mirroring disabled, either side can be selected independently. Use G to move the
selected guide. Clicks outside the markers pass through to normal Blender tools;
modified clicks and Edit/Pose Mode retain Blender's existing behavior.

```powershell
& Tools/Blender/WGameRiggingAssistant/install.ps1 -BlenderVersion 5.2
```

The installer copies the repository source into the Blender user add-on directory as
`wgame_rigging_assistant.py`. The AppData copy is deployment state, not source authority.

For numeric character creation and optional DCC manifest adoption, start the local
MxFramework Authoring Server and keep the default endpoint `http://127.0.0.1:4873`, or
change the endpoint in the add-on panel.

The unrigged WGame route enforces numeric identity, model preflight, bind-pose lock, and
connected-component scanning. Scope selection uses an exclusion workflow: paint only vertices
that require Custom Bind, optionally fill or clear a complete connected island as a shortcut,
hide the marked geometry while reviewing the rest, and then confirm once to assign every unmarked
vertex to Base Skin. One connected island may therefore contain both scopes.

Model preflight, component scanning, joint-guide/skeleton inputs, and export use the same formal
mesh exclusion rules: `cs_`, topology/backup suffixes, and the explicit per-object
`wgame_rig_exclude_from_binding` flag. The preflight panel lists all participants and exclusions;
selection does not narrow the audit. A toggle excludes/includes the active mesh and invalidates
prior preparation. Backup-name exclusions remain in force regardless of the toggle.

Preflight blocks empty surfaces, invalid coordinates/transforms, unapplied rotation/scale,
remaining armature binding, zero-length edges, degenerate faces, and duplicate vertex-set faces.
Boundary edges, edges shared by more than two faces, inconsistent adjacent winding, disconnected
islands, loose geometry, near-coincident vertices, UV/material absence, and non-reserved vertex
groups are reported for review. Edge/face/vertex issues have a selection locator; stored IDs cannot
be used after the geometry changes. Near-coincidence uses a reported scale-relative world-space
tolerance and never welds geometry automatically. Self-intersection, hidden internal faces, and
semantic accidental connections are not automatically resolved by this scan.

The preflight report offers **Repair Zero-Length Edges And Degenerate Faces**. It re-scans current
formal meshes, stages independent mesh copies, welds only vertices connected by edges at the same
near-zero threshold, and deletes degenerate faces. Unconnected near-coincident vertices are not
merged. Differing endpoint vertex-group masks/weights, Shape Keys, or existing Armature binding
require manual handling. A timestamped Blend backup must be successfully written before replacing
the live meshes. The action supports Undo, invalidates prior preparation, and reruns preflight;
it does not fill holes or automatically accept the remaining warnings.

**Merge Near Vertices Within The Same Island** uses the current preflight world-space tolerance
and the original edge-connected islands. It never merges across islands or objects, skips different
endpoint vertex-group weights/masks, and limits every merged vertex to the retained representative's
distance (no transitive merge beyond tolerance). It removes collapsed affected faces and rejects
candidate groups that would increase abnormal shared-edge incidence are skipped; a final check rejects
staged results that still increase edges shared by more than two faces. The same backup, staging, Undo,
and re-preflight transaction applies. Same-island membership does not imply shared clothing/body
semantics; this is an explicit author action, not an automatic preflight repair.

A data pass is distinct from author confirmation. Before locking a pose, the author must confirm
topology exceptions, pose/orientation/limb spacing, both hands, and mixed body/clothing regions.
Warnings additionally require a written exception or retention note. This acknowledges inspection;
it does not label every component or replace the subsequent Base/Custom masks.
Fingerprints include full connectivity, vertex coordinates and world transforms rather than only
counts/bounds. Pre-binding geometry changes invalidate preflight, pose confirmation, and downstream
binding acceptance; stage entry points also recheck current geometry. Component fingerprints track
connectivity without treating ordinary weight edits as topology changes. Existing legacy scan hashes
must be refreshed by the existing component rescan/recovery path.

`WGame.CustomBindMask` is the persisted binary vertex mask. Membership at weight `1.0` means
Custom Bind; absence means the vertex becomes a Base Skin candidate when the scope is confirmed.
This reserved non-deform group is authoring metadata, is preserved by base skinning, and is removed
from the staged FBX export so it never becomes a Unity bone influence. Older Blend files that only
store component-level `CUSTOM_BIND` scopes are materialized into the mask without changing their
selected vertices. An object-level authority-version marker distinguishes an intentionally empty
new mask from an uninitialized or pre-existing empty group, so upgrading cannot silently erase old
component classifications; the marker is also stripped from the staged FBX export.

The recommended selection flow is subtractive: use the component picker to make one connected
island current, click **Start/Continue Trimming Current Island**, and remove the vertices that do
not need Custom Bind. When that island has no existing mask, starting trim first fills the complete
island. Left-drag removes visible vertices and Ctrl+left-drag restores them. Re-entering an already
partially trimmed island preserves the current mask instead of filling it again, and the trim brush
is restricted to that island so it cannot change neighboring components.

The selection panel provides two viewport tools:

- **Island trim brush (recommended)**: left-drag subtracts visible vertices from the current
  island's Custom Bind mask and Ctrl+left-drag restores them. Entering trim clears Blender's
  ordinary orange mesh selection so it cannot obscure the scope preview. Every visible vertex in
  the current island then has an explicit state marker: a large pink point means Custom Bind and a
  blue point means it has been subtracted into the Base Skin candidate range. Both use a dark
  outline for contrast on light and dark surfaces. After trimming divides the remaining pink mask
  into multiple edge-connected regions, Shift+left-clicking any visible pink vertex removes that
  complete pink sub-island in one Undoable action. Flood fill follows only pink vertices and mesh
  edges inside the current original component; blue vertices cut connectivity, and other components
  cannot be changed. The inverse shortcut is Ctrl+Shift+left-click on a visible blue vertex: it
  follows only edge-connected blue vertices and restores that complete blue sub-island to pink.
  Pink vertices cut the blue flood fill, so adjacent excluded regions remain independent. Both
  operations participate in the brush session's Undo/Redo history.
- Island trim starts with the current original component isolated: other connected islands in the
  same Mesh and all other participant Mesh objects are temporarily hidden, so they cannot obscure
  the target or win the visibility ray test. Press `H` during trim to toggle the surrounding model
  context back on, and press it again to return to target-only isolation. Exiting trim restores the
  exact object, vertex, edge, and polygon visibility captured on entry, including content the author
  had intentionally hidden beforehand.
- **Free vertex brush (advanced)**: left-drag adds visible surface vertices to Custom Bind and
  Ctrl+left-drag erases them across the active mesh.
- In either brush mode, press `B` for an X-ray rectangle and left-drag to commit.
  Island trim subtracts; Ctrl+drag restores. Free brush adds; Ctrl+drag subtracts.
  Rectangle selection includes occluded front/back vertices, skips explicitly hidden
  vertices, and retains the current component restriction in island/semantic trim.
  Esc/right-click cancels the rectangle without changing the mask or leaving the brush.
  Each completed rectangle is one session Undo/Redo operation. X-ray rectangles do not
  change Blender's viewport X-ray setting or ordinary mesh selection.
- Both brush modes use `[` and `]` to change the radius, use the nearest visible mesh surface, and
  do not paint through to back-facing geometry. Each completed stroke is an Undo step; middle mouse
  and the wheel remain available for navigation. Enter, right-click, or Escape exits. The pink mask
  preview is depth-tested against the scene, so vertices on the back side do not show through the
  visible surface; the brush cursor itself remains in the foreground. Brush hit testing uses a
  view-depth tolerance derived from the vertex's local edge spacing, matching the visible marker at
  silhouettes and hard folds without opening selection through to a genuinely occluded back side.
  While either brush remains active, Ctrl+Z uses the brush session's exact mask history to undo the
  previous stroke or pink-sub-island removal, and Ctrl+Shift+Z reapplies it. This avoids Blender's
  global Undo shortcut being swallowed by the modal operator. After the brush exits, the completed
  session is also one ordinary Blender Undo step.
- **Component picker**: left-click immediately fills the visible connected component into
  `WGame.CustomBindMask`, makes it current, and highlights it without changing the view. Clicking
  an already-complete Custom island only focuses it. Shift+left-click remains the inverse toggle
  for filling or clearing a complete island. Press `H` while the picker is active to toggle all
  marked Custom Bind geometry between hidden and visible, making it quick to remove completed
  islands from the review view. The shortcut restores Edit Mode after changing mesh visibility;
  when hiding, it advances to the next pending visible component when one exists. Hidden Custom
  Bind geometry is excluded from picking.


Component scope selection supports Ctrl+Z / Ctrl+Shift+Z while the component
picker is active, including ordinary focus, island fill/toggle and H visibility.
Each action restores the exact mask, component index, selection, visibility and
workflow fields. Esc/right-click/Enter exits and retains changes; a changed
session becomes one ordinary Blender Undo step. Exiting an unchanged session
does not add a step. The scope buttons (focus/navigation, mark/unmark, hide/show,
confirm/reopen/reset) and completed brush sessions also restore Scene metadata
alongside mesh data in Blender 5.2 Edit Mode. Undo/Redo discards derived display
caches. Multi-object Edit Mode reads/writes the BMesh of every editing mesh.

The regression uses a separate windowed Blender and actual window events:
`blender --factory-startup --enable-event-simulate --python
Tools/Blender/WGameRiggingAssistant/test_component_scope_undo_ui.py`.
Set `WGAME_SCOPE_TEST_OUTPUT` for its JSON/PNG output directory. This is a
synthetic scene test; never run it inside the production authoring session.

- **Base Skin**: unmasked vertices receive initial weights from the accepted WGame Humanoid and finger
  skeleton.
- **Custom Bind**: masked vertices are excluded from base skinning. Step 3A can deterministically
  generate an initial weight field from explicitly refreshed custom bone chains; Step 3B remains
  the manual Weight Paint authority and Step 3C remains the final validator.

Base skinning never clears the whole mesh. It rewrites deform weights only on Base Skin vertices,
preserves Custom Bind vertices and non-bone vertex groups, and supports every mesh object recorded
by the current component scan. The accepted base skeleton is snapshotted so later manual bone
editing can add arbitrary custom hierarchies without silently changing the Humanoid rest skeleton.
Manual edits to Base Skin weights do not require regenerating Base Skin. The permanent Base Skin
panel provides **Normalize Base Weights Using Existing Ratios** followed by **Validate Existing Base
Skin (No Recalculation)**. Normalization touches only existing deform assignments on Base Skin
vertices, preserves their bone-to-bone ratios, trims to the strongest four when necessary, and
never modifies Custom Bind or non-deform groups. Zero-weight vertices are reported rather than
inventing semantics; they must be repaired deliberately before no-recalculation validation can
pass. Regeneration remains reserved for authors who intentionally want the solver to replace their
manual Base Skin edits.
After normalization, **Conservatively Repair Base Validation Failures** provides the deliberate
repair path: zero-weight vertices copy a normalized blend only from topology seeds in the same
component and Base Skin scope within six edges, while existing nonzero vertices are rewritten only
when the authoritative joint limiter changes a weight by more than the validator's `0.001`
tolerance. Excess Shoulder, UpperArm, or distal-child weight returns toward its parent/support
chain; accepted manual vertices remain untouched. The action creates a Blend backup, is Undoable,
and immediately runs no-recalculation validation.
Base validation reports are rendered as a structured multi-line result in the narrow N-panel rather
than one truncated diagnostic string. The panel leads with the exact nonzero failures, then lists
weight integrity, torso coverage, Head, shoulder/arm, elbow/knee, and hierarchy checks separately;
failed gates use error icons while zero-valued gates remain visibly passed. A failed manual-weight
validation directs the author back through normalize and no-recalculation validation instead of
unconditionally telling them to regenerate Base Skin.
After a WGame skeleton already exists, its main validation route keeps a permanent **Continue
Adjusting Custom Selection** entry beside the Base/Custom vertex summary. It preserves the current
mask and reopens component picking, island trimming, and final scope confirmation, so a missed
detached component can be added without resetting the workflow. Reopening invalidates prior base
skin acceptance because newly added Custom vertices must be excluded when Base Skin is rerun.
The manual-binding route is split into three persistent UI tabs. **Base Preparation (1-2)** contains
scope adjustment, skeleton/finger acceptance, Base Skin generation and manual-weight repair, and
per-vertex anatomical semantics. **Custom Binding (3)** contains custom-bone authoring, custom-chain
initial weights, Custom Bind Weight Paint, and the 3C global validation. **Validation And Publish
(4+)** contains deformation-pose checks, the complete bone-weight browser, final FBX/Unity publish,
and no Step-3 editing controls. Passing Base Skin automatically opens the Custom Binding tab;
passing 3C automatically opens Validation And Publish. Invalidating the base scope, skeleton, or
weights returns to Base Preparation. Opening a later tab before its prerequisite passes shows only
the blocking reason rather than duplicating content from the earlier tabs.

Base weights are anatomy-partitioned rather than solved against every bone globally. Six authority
surfaces are selected from the connected components nearest the center, head, left/right arm, and
left/right leg chains. Authority vertices use chain-local candidate bones, wrist/ankle direction
gates, and hand-side finger-chain selection; influences below one percent are removed and the
remaining maximum-four weights are normalized. Other Base Skin components copy the nearest
authority-surface weights, preventing spatially adjacent detached clothing from choosing unrelated
bones independently. Re-running the solver is deterministic and still leaves every Custom Bind
assignment unchanged.

Non-authority Base Skin topology islands receive a component-level semantic pass before transfer.
Strongly single-region islands are locked to one anatomical region; a top-attached or substantially
Head-classified island remains Head even when lower vertices cross the neck or shoulder planes.
Only genuinely mixed components retain per-vertex regional transfer. Locked detached Head islands
are rigidly assigned to `head.x`, while the authority head surface keeps its Neck-to-Head skin
transition. Optional persisted per-vertex anatomical-region overrides are honored when present.
The Base Skin panel exposes the selected region through both a continuous whole-component picker
and the same subtractive brush used by Custom Bind. The picker remains the fast path for marking an
entire detached hair island as Head. The brush starts from the current component, fills its eligible
AUTO vertices with the selected region, and then supports visible-surface trimming, colored/blue
state markers, pink/blue sub-island operations, target isolation, context toggle, and session-local
Undo/Redo. Vertices already assigned to another explicit region are protected from a different
region's trim session; clear or edit that region first when reassignment is intentional.
Overrides are stored in mutually exclusive non-deform groups named `WGame.BaseRegion.<REGION>`.
An authority marker distinguishes migrated legacy component JSON from an intentionally empty new
mask. A component-level `base_region` value remains only as a compatibility cache when every Base
Skin vertex in that component has the same explicit region; partial or multi-region components do
not pretend to have one whole-island override.
Persisted overrides are drawn continuously as colored vertex overlays with region, component, and
vertex-count labels. Panel totals are exact vertex counts. Whole-component and bulk clear actions
update the same masks, and `AUTO` removes explicit ownership from the affected vertices. Weight
changes occur only after rerunning Base Skin. Overlay geometry and labels are cached across ordinary
viewport redraws, so orbiting or zooming does not rescan every semantic group and vertex. The cache
is invalidated only by mask/scope/topology edits or object transforms. A semantic trim session
temporarily suspends the global overlay and uses its own focused two-state preview, restoring the
global overlay on exit.
Explicit per-vertex overrides take precedence over authority-surface transfer and automatic
anatomical partitioning. In particular, every Base Skin vertex explicitly assigned `HEAD` is
rewritten as exactly `head.x = 1.0`; neighboring AUTO vertices in the same connected component keep
their normal inference. A full-component single-region mask preserves the previous authority
selection behavior. The Base Skin report independently checks all explicit HEAD vertices and
refuses completion when the `HEAD override mismatch` count is nonzero. Region authoring groups and
their authority marker are removed from the staged FBX export.

Region ownership is established before distance weighting. Head influence begins at Head Head,
Neck begins at the authored neck-root/Neck Head, central chest vertices remain Center, and lateral
clavicle/shoulder-cap vertices use dedicated left/right Shoulder regions before transitioning to
Arm. Upper-arm influence begins only on the outward side of the shoulder joint. Surface-transfer KD
trees are kept per anatomical region, so detached head, neck, chest, and shoulder components cannot
copy weights across those boundaries. Missing chains in minimal test rigs fall back to the closest
existing region without weakening the full Humanoid boundaries.

The shoulder-to-upper-arm boundary uses the lateral Shoulder bone axis rather than the sloped
UpperArm axis. Vertices on the torso side of the UpperArm head plane receive no UpperArm influence;
on the outer side, a smooth root zone caps UpperArm weight and redistributes the remainder to
Shoulder/UpperChest before allowing full arm ownership farther down the limb. The final Base Skin
report rejects any medial UpperArm influence or weight above that root-zone cap, including weights
copied to non-authority components.

Elbow and knee boundaries use a narrow bidirectional joint band along the parent limb axis. This
preserves a natural parent/child blend immediately around the bend while forcing Forearm to zero
farther into UpperArm and LowerLeg to zero farther into Thigh. Within the band, a smooth directional
cap moves excess child weight directly back to its parent bone. Direct authority solving, regional
surface transfer, and final validation all apply the same rule; the Base Skin report separately
exposes out-of-band elbow/knee crossings and child-root cap violations.

The same distal-to-parent cascade covers Shoulder-to-UpperChest, Hand-to-Forearm, every three-bone
finger chain to Hand, Foot-to-LowerLeg, and Toes-to-Foot. Each joint uses a bone-length and
character-scale band suited to its size; finger bands are millimeter-scale while wrist, ankle, and
shoulder bands are wider. Corrections run from the most distal phalanx or toe toward the torso so
weight removed from one child is rechecked when it reaches the parent. Final diagnostics report
out-of-band Shoulder, Hand, Finger, Foot, and Toes influence separately from transition-cap errors.

The v2.2 base skeleton maps Unity Humanoid Chest to `spine_02.x` and UpperChest to
`upper_chest.x`. The authored Neck guide is the neck root: UpperChest spans from the existing Chest
guide to that neck root, Neck starts there and ends at a derived head-base point, and Head continues
to the existing head-center target. Both Shoulder bones start at the midpoint of the left/right
shoulder guides at 35% of the center-to-joint path, remain horizontal, and are offset children of
UpperChest. Existing v1 rigs and the intermediate v2.0/v2.1 layouts expose one confirmed migration action that creates a backup
and repairs or creates the hierarchy and mapping. Because this changes the skeleton, prior acceptance is invalidated;
the author inspects and accepts the new base skeleton before running Base Skin as a separate explicit step. The
UpperChest migration never creates weights or runs binding validation.

After base skinning, the panel provides guarded entry points to isolate Custom Bind vertices, edit
the shared WGame armature, generate deterministic initial custom weights, paint only the custom
vertex mask, restore visibility, and run one global validation across all participating meshes.
The assistant does not generate accessory, cape, chain, hair, eye, jaw, cloth-physics, or
spring-bone structures; those are authored manually.
Entering custom Weight Paint forces the chosen deform bone visible and selected, enables Armature
In Front, and draws a thick orange bone axis plus a labelled screen marker without moving the
current 3D view. Switching or exiting the custom weight session updates or clears the marker.
Existing vertex, edge, and face visibility is preserved across entry and exit; only currently
visible Custom Bind vertices are selected into the paint mask, so intentionally hidden components
do not reappear or receive brush edits.
The custom-binding panel presents explicit **3A Custom Bones and Initial Weights**, **3B per-bone
Weight Paint**, and **3C global validation** states. Enter/finish controls are mutually enabled by
the active mode, and a successful global validation immediately points to the step-4 pose previews
instead of relying on scrolling past a raw diagnostic line.

Step 3A persists refreshed custom-chain records on the armature under
`wgame_rigging_assistant.custom_weight_chains.v1`. Every Deform bone outside the accepted base
skeleton snapshot is grouped by parent/child connectivity; a single bone is a valid one-bone chain.
Each record contains a stable root-based ID, topological bone order, connected base parent, enabled
state, influence radius, optional blend-step override, and the custom-bone structure fingerprint.
The default radius is 75% of the chain's median bone length, clamped to 2.5%-12% of character
height. Explicit refresh preserves settings for chains with the same root name. Adding, deleting,
renaming, reparenting, moving, resizing, or toggling Deform state makes the record stale and blocks
generation until the author refreshes it; settings are never silently reset during drawing or
viewport interaction.

Every scanned linear chain exposes a target bone count from 1 to 12. Applying the count resamples
the complete Rest-Pose polyline by arc length, preserving the root head, terminal tail, base parent,
connected hierarchy, Deform state, and nearest source rolls rather than merely appending or deleting
the distal end. Breast chains are resized as a mirrored pair and always use dedicated names
`breast_01..N.l/r`. Other chains are renamed deterministically as
`custom_<semantic>_<chainOrdinal>_<segmentOrdinal>.<side>`; the semantic defaults to the accepted
base-parent name and can be overridden per chain. Refresh performs the same canonical rename, using
a two-phase temporary-name transaction so swaps cannot trigger Blender's automatic `.001` suffixes.
Matching vertex groups, object/pose constraint subtargets, active weight selections, chain records,
and structure fingerprints are synchronized. A conflicting target name or an external child or
constraint on a bone that would be deleted blocks the operation. Structural changes create a Blend
backup, invalidate Custom acceptance, require initial-weight regeneration, and back up then clear an
enabled character-cloth document whose transform paths are now stale. After a count change, the
armature returns directly to Edit Mode with every bone in the adjusted current-side chain selected
and its root active, allowing immediate joint/roll refinement before Finish Custom Bone Editing.
While the armature remains in Edit Mode, selecting any scanned custom bone synchronizes the 3A
current-chain panel to the chain containing the active Edit Bone; multiple selections use the active
bone, and selecting a base bone leaves the current custom-chain choice unchanged.

The initial-weight solver recomputes edge-connected sub-islands inside the exact
`WGame.CustomBindMask`; Base vertices split Custom connectivity. Sub-islands now define only the
topology boundary and Base-transition domain. Every Custom vertex independently compares its
point-to-bone-segment distance against every enabled chain's own radius and retains the nearest one
or two eligible chains. Consequently one long connected skirt, cape, or hair island may use many
different chains across its surface while each individual vertex still sees at most two chains.
The former island-wide two-chain cap and 1.5x second-chain gate no longer suppress locally closest
bones. For compatibility and complete initial coverage, a vertex with no local in-radius candidate
may fall back to the island-level conservative match; a wholly unmatched island keeps every
existing weight and is listed with its component, vertex count, nearest candidate distance, and
radius.

Disconnected hard accessories are an explicit exception to flexible multi-chain weighting. Step
3A exposes a **Rigid Attachment Island** box: in Edit Mode, selecting any vertex of a Custom
sub-island is enough to expand the operation to the complete island. **Auto From Attachment Point**
finds the island point closest to another Custom surface, first inherits that surface point's
dominant existing custom Deform influence, and otherwise chooses the closest enabled custom bone
segment. **Bind To Current Bone 1.0** uses the selected 3A/active Deform bone. The authoritative
binary membership is persisted in the non-Deform metadata group `WGame.RigidBindMask`; per-island
target records are stored on the armature under
`wgame_rigging_assistant.rigid_bind_attachments.v1`.
**Create Dedicated Rigid Bone And Mark** creates a timestamped Blend backup, places one Deform bone
at the island bounds, parents it without connection to the selected or automatically resolved
attachment bone, marks it as excluded from flexible custom-chain discovery, and records the complete
island against that bone. As with every skeleton-structure change, an enabled character-cloth
document is backed up and cleared because its transform contract may be stale.
**Show Only All Rigid Vertices** works across every participant Mesh: it temporarily reveals and
selects all `WGame.RigidBindMask` members in vertex Edit Mode while hiding every non-rigid mesh
element. **Restore Context** exits that isolation and restores the prior Custom-only isolation when
one was active, otherwise it restores all participant vertices. These controls change only temporary
visibility and selection; they never edit membership, weights, or attachment targets.

During generation a valid rigid island bypasses local/island multi-chain matching, Base boundary
blending, inverse-distance distribution, and four-slot sharing. Every vertex is cleared of previous
Deform assignments and receives exactly `{targetBone: 1.0}`. A missing or non-Deform target leaves
the island unchanged and is reported as an invalid rigid record. Step 3C verifies that every
recorded vertex remains in the rigid mask and has one and only one Deform influence at weight
`1.0`; neighbor zero-weight repair excludes rigid-mask vertices so it cannot smear flexible cape
weights back into a jewel. Clearing a rigid mark removes only metadata and mask membership, not the
current Deform weights. Both authoring-only rigid metadata surfaces are stripped before FBX export
and explicitly rejected by round-trip audit if they leak.

Matched detached sub-islands use inverse-cubed distance to all participating bone segments, retain
the strongest four Deform influences, and normalize every vertex. Matched sub-islands connected to
Base Skin sample the current normalized Deform assignments of directly adjacent Base vertices.
Those real boundary samples propagate only through Custom edges for the configured topological
blend width (global default four edges; per-chain value `0` inherits the global setting). When two
chains match, the largest effective width is used. Base vertices remain read-only samples, while
the first Custom boundary ring is Custom-dominant. The configurable minimum Custom share defaults
to 65%; deeper rings interpolate from that minimum to 100% using
`minimum + (1-minimum) * smoothstep(d/steps)`. With four steps the default Custom shares are 65%,
70.5%, 82.5%, 94.5%, then 100%. At and beyond the final step the result is pure Custom. Multiple
boundary samples are combined by inverse topological distance, so traversal order cannot choose the
result. Existing boundary weights may come from base or other established Deform bones and are not
guessed from names.

The Base/Custom merge obeys Unity's four-influence contract while keeping both sides in the
transition: Custom share below one third reserves 3 Base + 1 Custom slots, the middle third uses
2 + 2, and the final third uses 1 + 3. Missing influences return their unused slots to the other
side; the retained values are normalized again. With the default 65% boundary minimum, the first
ring uses 2 + 2 and subsequent rings normally use 1 Base + 3 Custom, so Base anchors continuity
without taking ownership of the Custom island. The confirmed **Generate All Custom Initial
Weights** action creates a Blend backup and one Undo step, clears and rebuilds Deform assignments
only on successfully matched Custom sub-islands, and leaves unmatched Custom vertices, every Base
vertex, non-Deform groups, the mask, and the armature unchanged. Its structured report includes
chain state, detached/connected and per-vertex dual-chain counts, matched and unmatched coverage, shared-band
vertices, local/fallback vertex counts, multi-chain islands, the maximum distinct chains used in one
island, post-generation zero/over-four/unnormalized counts, exact per-chain vertex coverage, and
backup path.
This is deliberately only the starting point for 3B; generation never declares 3C acceptance.

The current-chain panel also provides **Activate And Automatically Display Bone Weights**. It
chooses the participant Mesh with the largest nonzero group for the current bone, enters Blender's
standard Weight Paint heatmap, frames the weighted region, and identifies the active bone with the
existing viewport marker. It then enables vertex-paint masking with an empty selection, so ordinary
brush strokes cannot change any vertex during this preview; actual painting still starts only from
Step 3B. Previous/Next Bone follows the chain's stored topological order without skipping
zero-weight bones. A weighted bone opens the protected Weight Paint heatmap; a bone with no nonzero
assignment still opens Pose Mode, becomes visible and selected, and is framed in the viewport
without creating an empty vertex group. While automatic preview is active, changing to the
previous or next custom chain immediately refreshes the heatmap for that chain. A chain with no
nonzero weights therefore remains fully navigable and reports zero weight rather than inventing weights. Exit Weight
Preview returns to Object Mode without modifying any assignments. Start 3B remains a separate
action, so the author must leave a manual custom-paint session before activating this inspection
mode.

The Final tab uses a data-driven deformation-acceptance library instead of the former five cramped
fixed buttons. Nine presets cover shoulder/arm range, elbow/forearm/wrist motion, torso/neck/head,
deep squat, asymmetric lunge, different left/right hand shapes, every recorded custom chain, an
asymmetric cross-side test, and a whole-body stress pose. Shoulder elevation and elbow flexion use
the generated WGame bones' local X bending axes; forward arm motion uses the mirrored local Z axes,
avoiding the former preset's sideways/twisting result. Every preset starts again from the exact pose
captured before preview, supports 25%-100% intensity and Both/Left/Right filtering, selects the bones
it actually moved, and records expected/applied operations, missing bones, and coverage counts by
body region. By default it recenters and zooms every 3D viewport to the complete participant-mesh
bounds after applying a pose, while preserving the current viewing angle; this can be disabled in
the Final tab. Previous/Next Action reapplies the neighboring preset immediately. Custom-chain
preview reads the persisted enabled chain records and applies small alternating rotations to every
segment; when records are unavailable it safely falls back to all Deform bones outside the base
snapshot. Restore Bind Pose restores the exact captured matrices and clears the report. An optional
fine-tune panel exposes live torso, shoulder/arm, finger, hip/leg/foot, and secondary sliders; slider
editing is calculated independently from the bind pose and does not stack unpredictably on a
preset. No preview writes keyframes or changes Rest Pose.

The Humanoid skeleton step exposes **Auto Generate Female Breast Bones** before
Base Skin. It estimates symmetric `breast_01/02.l/r` chains from the confirmed
Base Skin chest-front geometry and parents them to `upper_chest.x`. Creation
requires a successful Blend backup, preserves existing bones/weights, rejects
partial name collisions, and is a no-op when a recognized complete pair exists.
Positions are author-adjustable estimates. This optional button does not assume
every character needs breast bones and does not mark the base skeleton accepted.
It enables the existing breast weighting pass for the next Base Skin execution;
already skinned characters can use the existing breast-weight button explicitly.

Custom-bone inspection recognizes an optional mirrored female-breast layout as two two-bone Deform
chains parented to `upper_chest.x`. Detection verifies chest-relative placement, connected segment
continuity, chain depth, left/right mirror error, and bone-length scale rather than relying on names.
The panel reports both chains and the custom bone selectors label their four semantic roles while
retaining the actual bone names. A guarded action can rename the recognized chains to
`breast_01/02.l/r`; it creates a backup, rejects occupied targets, and synchronizes matching vertex
groups, constraints, and active bone selections. Detection itself never mutates the rig.

Recognized breast chains also expose automatic initial weighting, enabled by default after a
high-confidence detection, plus an explicit refresh action. The solver only considers the Center authority body's
Base Skin vertices, requires existing Chest/UpperChest support, separates sides at the rig midline,
and uses the chain-derived forward direction and a character-scaled local envelope. It scales at
most two existing torso supports and adds the two same-side breast segments, preserving non-deform
groups, Custom Bind vertices, detached clothing/components, four-influence limits, and normalized
sums. Rerunning Base Skin deterministically rebuilds the breast weights instead of clearing them.
Step 3C can isolate zero-weight Custom Bind vertices across all participating meshes. The operator
hides every other mesh element, selects the zero-weight vertices in Edit Mode, and reports the exact
count. Restoring all custom vertices keeps the Base Skin range hidden so repair work can continue
without visual clutter.
While custom Weight Paint is active, the panel exposes a WGame-owned 3D bone picker that does not
depend on Blender keymap shortcuts. Clicking a visible deform-bone axis synchronizes the active Pose
Bone, same-named vertex group, panel selection, and orange locator, then returns immediately to
painting. The entry temporarily disables Lock Object Modes and restores the user's original value
on exit.
Custom Weight Paint preserves the author's Blender Auto Normalize preference. Blender's Armature
modifier interprets deform weights relatively, while Unity's bone-weight data contract requires a
sum of one. The global repair action therefore multiplies every existing weight on a vertex by the
same factor: relative deformation is preserved, sub-one and over-one sums become one, zero-weight
vertices stay untouched, and no new bone semantics are invented. A single-group vertex becomes one
numerically but deforms identically in Blender; only its Weight Paint color changes.
Step 3C also offers conservative zero-weight repair. It copies normalized deform weights only from
topological neighbors that are inside the same connected component **and** the Custom Bind mask,
within a configurable one-to-six edge steps. It keeps the strongest four influences and normalizes
the result. The search never crosses into Base Skin vertices; custom regions with no weighted seed
are skipped, preventing nearby or differently scoped geometry from sharing weights.
It can also normalize every nonzero Custom Bind deform assignment in one confirmed Undoable pass,
preserving each vertex's existing bone ratio while scaling the sum to one. Zero-weight vertices,
non-deform groups, and Base Skin vertices are untouched; stale saved Weight Paint session flags are
cleared when the mesh is no longer actually in Weight Paint mode.

Legacy six-role component assignments and single-bone groups are not migrated. The explicit reset
operation clears only their workflow metadata and keeps meshes, armatures, modifiers, bones, and
vertex groups intact.

Before finger-bone generation, `Analyze Hand Neutral` performs a read-only five-branch check on both
hands using weighted surface distance from the wrist. `Auto-Neutralize Both Hands` saves a rolling
Blend backup, preserves the palm and thumb, and straightens the four main-finger center paths with a
smooth, topology-partitioned deformation. It is intentionally available only before finger bones or
base-skeleton acceptance; the resulting silhouette remains an explicit author review point.
The branch analysis counts all spatially separated eligible tips and rejects both fewer and more
than five; it never truncates an ambiguous six-branch result into a five-finger success.

Finger generation uses the same checked topology endpoints as neutral analysis.
Both hands are fitted independently; old planar clustering and mirrored-left generation
are no longer used. Finger authoring resolves the formal configured/largest mesh and
excludes `__TOPO_BACKUP` helper meshes. Surface-side and branch-ownership checks are
sampled safeguards, not proof of a watertight volume or exact anatomical joint placement.
Finger confirmation and base-skeleton acceptance check the exact 30 names, each side's Hand parent,
three-segment hierarchy and connected joints, finite nonzero bone lengths, Deform flags, and all
30 Humanoid mappings. Invalid structure clears acceptance and downstream binding results.
The shared Mesh/rig resolver applies the same rule at publish time: a stale configured helper or an
actively selected topology backup is ignored, the largest formal character Mesh is recovered even
before an Armature modifier exists, and the corrected Mesh name is persisted before Palette and FBX
validation.

Optional managed-source buttons can save/adopt the rolling work Blend, M0-M3 milestones, Final
Blender authority, model-sheet captures, and a stable model audit. Auto-Rig Pro remains an optional
control/diagnostic integration rather than the authority for the WGame base skin workflow.

An external Blend outside `ArtSource/Characters/<id>` exposes `Apply For ID And Adopt As Art Source`
in every workflow route, including an already-generated WGame rig. It locates the repository from
the installed deployment receipt, starts the local Authoring Server when necessary, atomically
allocates the six-digit ID, creates both the versioned ArtSource root and Character package from the
selected Humanoid/Creature schema template, and saves the open file as
`Blender_Work/<id>_work.blend`. The Work Blend is adopted into `authoring_manifest.json`; the current
Character Color Profile is saved into the package, Palette meshes receive a v5 binding, Palette
package/export paths are initialized, and obsolete `PaletteSlot`/`MaterialClass.*` attributes are
removed from the adopted Work copy. The source manifest owns `Final_Source/<id>_LOD0.fbx`; that file
is created only after full binding validation by the normal publish-to-Unity action.

After full binding validation, `Publish FBX And Update Unity` provides the guarded end-to-end path.
Base generation, no-recalculation validation, final binding validation, and pre-publish validation
share the same current-weight anatomical checks. Final validation and publishing do not trust saved
completion flags: HEAD overrides, joint boundaries, the base-skeleton snapshot, scope validity,
weight integrity, and rigid attachments are checked again. These checks never rewrite weights or
adopt new HEAD metadata. A failed check returns the workflow to the relevant editing stage before
server startup or formal file writes.
It validates Palette mesh data, writes a staged Final Blend, exports a staged Unity FBX with the
repository exporter, round-trips that FBX through a clean Blender process, and only then replaces
the formal Blend/FBX/audit while backing up their previous versions. It writes Palette Usage v2
against the new FBX hash, force-syncs the `body-model` source into the character package, requests
Authoring-to-Unity import, and waits for the Unity Editor Prefab/Avatar/receipt rebuild. A missing
localhost Authoring Server is started in the background before formal files are written; remote
servers are never launched automatically. Any failure is reported with its pipeline stage, and
temporary staging files are removed.
Palette channel checks now run on source meshes before staging, on evaluated meshes before FBX
export, and on every mesh read back from the staged FBX before formal replacement or Unity sync.
Diagnostics identify the object, mesh, stage, missing UV/color layer, actual layer order/counts,
or loose vertex indices. Evaluated failures list active modifiers without claiming which modifier
caused the loss. `PaletteUV` must occupy UV1 and `PaletteData` must be the first RGBA color layer;
the existing single-PaletteUV export normalization remains supported. Publish passes an explicit
`--require-palette` flag to export and audit so complete channel loss cannot bypass detection.
Audit mesh reports include channel counts. Blender checks do not replace the final Unity importer
validation; Unity-specific channel changes can still fail at that later stage.
One WGame skeleton may export multiple bound Mesh objects without joining the authoring geometry.
The checked participant set must match the formal bound meshes; Palette validation and Usage v2
include that whole set. The FBX auditor checks weights and the Armature modifier separately on every
mesh, while required torso-bone coverage is aggregated across the character. Audit schema 1.1 adds
`meshReports` with per-mesh evidence; top-level geometry counts are totals. Legacy single-mesh
transform fields remain populated for one mesh and are null for multiple meshes.
The standalone exporter ignores bound `__TOPO_BACKUP`, `_TOPO_BACKUP`, `__BACKUP`, and `cs_` helper
meshes just like the in-process resolver. Export and round-trip Blender subprocesses use an explicit
Python failure exit code and must produce their declared FBX/audit outputs; a script exception is
therefore reported at its real stage instead of surfacing later as a missing audit JSON.
Authoring model sync, Unity file sync, and asynchronous Unity rebuild failures preserve the server
error/code/message, process exit code, report directory/status, structured issues/diagnostics, and
the actionable stderr/stdout tail. The popup starts with the concrete cause while the panel report
retains the bounded multi-line evidence for follow-up diagnosis.

The FBX round-trip audit records a deterministic hierarchy hash from every exported bone name,
parent, and Deform flag. Publishing compares it with the previous formal audit. When the hierarchy
changes, the publish transaction backs up `config/character_cloth_magica.json` beside the previous
Blend/FBX/audit and atomically resets the package Cloth document to schema 1.1 with `enabled=false`
and an empty `cloths` array before package sync and Unity import. Material-, texture-, or weight-only
exports whose hierarchy hash is unchanged preserve the existing Cloth authoring. Unity then imports
without stale Root paths; Cloth Authoring can initialize a new model-derived draft explicitly.


### Nearby finger tip detection (2.19.13)

Main digit candidates that are close in straight-line distance now receive a
surface-distance check before deduplication. Two tips on different digits may
connect through the palm even when their gap is narrower than the spatial
threshold. The finite surface path must be at least twice that threshold;
nearby vertices on the same tip cap remain duplicates, and disconnected tips
are not promoted. Existing wrist/length filters, thumb hint guards and fitted
bone surface/branch validation still apply. This analysis does not use the
CustomBindMask and does not alter mesh geometry or hand guides.

Failure reports remain visible in wrapped panel labels with the failing side
and next action. Missing main digits require that side's Show Candidates and
inspection of its four main tips/hand placement; Set Thumb Tip only addresses
a missing thumb. A missing thumb can be resolved by selecting exactly one
thumb tip vertex in Edit Mode, setting that side's hint, and generating again.

Regression: run a separate background Blender with
`--factory-startup --python-exit-code 1 --python
Tools/Blender/WGameRiggingAssistant/test_finger_tip_separation.py --
fixture.json result.json [baseline-addon.py]`. The exported JSON fixture
contains vertices, edges, faces, mesh_matrix, rig_matrix, all bones, mapping,
mask IDs and thumb hints. The test verifies both hands, actual 30-bone
generation and backup, repeat determinism, preservation of nonfinger bones,
geometry/masks/hints, and rejected-fit rollback. Never run scene-building tests
inside the user's authoring session.
