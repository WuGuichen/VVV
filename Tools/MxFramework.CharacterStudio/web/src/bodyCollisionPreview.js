// Display adapter for CharacterTest default standing movement, not a source writer or validator.
// Authority: CreatePhysicsProfile -> CharacterTestCharacterPhysicsComponent ->
// CharacterTestCollisionProfileResolverSystem.ResolveProfile/ClampCapsuleDimensions -> GetCharacterHalfExtents.
// The same half-extents feed both the registered AABB and CombatMotionCapsuleProxy.FromHalfExtents.
// Neither primitive alone describes every movement sweep branch.
// Keep parity with scripts/fixtures/movement-collision-runtime.json when those consumers change.

function movementMetersToMillimeters(value) {
  const meters = Math.fround(Number(value));
  // Match the live consumer's promoted multiplication; another float32 rounding here changes half-mm results.
  const scaled = (Number.isFinite(meters) ? meters : 0) * 1000;
  if (!Number.isFinite(scaled)) return 0;
  if (scaled <= -2147483648) return -2147483648;
  if (scaled >= 2147483647) return 2147483647;
  return Math.sign(scaled) * Math.floor(Math.abs(scaled) + 0.5);
}

function getMovementCollisionPreview(body = state.package?.geometry?.bodyProfile || {}) {
  const finite = value => Number.isFinite(Number(value)) && value != null ? Number(value) : 0;
  const capsule = body.defaultCapsule || {};
  const source = { height: finite(capsule.height), radius: finite(capsule.radius),
    center: { x: finite(capsule.center?.x), y: finite(capsule.center?.y), z: finite(capsule.center?.z) } };
  const heightInput = Math.fround(source.height > 0 ? source.height : finite(body.heightMeters));
  const radiusInput = Math.fround(source.radius > 0 ? source.radius : finite(body.radiusMeters));
  const centerInput = source.center.y > 0 ? Math.fround(source.center.y) : Math.fround(heightInput * 0.5);
  const quantizedHeight = movementMetersToMillimeters(heightInput);
  const quantizedRadius = movementMetersToMillimeters(radiusInput);
  const quantizedCenter = movementMetersToMillimeters(centerInput);
  const physicsHeight = quantizedHeight > 0 ? quantizedHeight : 1860;
  const physicsRadius = quantizedRadius > 0 ? quantizedRadius : 340;
  const physicsCenter = quantizedCenter > 0 ? quantizedCenter : Math.floor(physicsHeight / 2);
  const heightMm = Math.max(20, physicsHeight);
  const radiusMm = Math.min(Math.max(10, physicsRadius), Math.floor(heightMm / 2));
  const centerMm = Math.max(radiusMm, Math.min(heightMm - radiusMm, physicsCenter > 0 ? physicsCenter : Math.floor(heightMm / 2)));
  const halfHeightMm = Math.floor(heightMm / 2);
  const effective = { height: halfHeightMm * 2 / 1000, radius: radiusMm / 1000,
    width: radiusMm * 2 / 1000, depth: radiusMm * 2 / 1000, center: { x: 0, y: centerMm / 1000, z: 0 },
    bottom: (centerMm - halfHeightMm) / 1000, top: (centerMm + halfHeightMm) / 1000,
    standingHeightMm: heightMm, radiusMm, centerMm, boxHeightMm: halfHeightMm * 2 };
  const notes = [];
  if (source.height <= 0 || source.radius <= 0) notes.push("尺寸未设置为正数时，游戏先取整体身体的参考尺寸，再按默认值处理。");
  if (source.center.y <= 0) notes.push("中心高度为 0 或负数，游戏使用高度的一半作为初始中心，再应用位置限制。");
  if (radiusMm < Math.max(10, physicsRadius)) notes.push("半径超过高度的一半，站立生效半径已缩小。");
  if (centerMm !== physicsCenter) notes.push("中心高度超出站立有效范围，游戏已限制在半径与高度减半径之间。");
  if (source.center.x !== 0 || source.center.z !== 0) notes.push("源中心 X / Z 为非零值，当前游戏忽略这两个偏移；原值继续保留。");
  const sourceCapsuleValid = source.height > 0 && source.radius > 0 && source.height >= source.radius * 2;
  if (!sourceCapsuleValid) notes.push("源尺寸无法组成有效胶囊，源胶囊参考不绘制；处理后的移动代理与注册盒体仍分别显示。");
  const proxy = { radius: effective.radius, height: effective.height, center: { ...effective.center },
    segmentStartY: effective.bottom + effective.radius, segmentEndY: effective.top - effective.radius };
  const registeredBounds = { width: effective.width, height: effective.height, depth: effective.depth,
    center: { ...effective.center }, bottom: effective.bottom, top: effective.top };
  return { source, effective, proxy, registeredBounds, sourceCapsuleValid, notes };
}

function renderMovementDetectionExplanation() {
  return `<p class="field-help movement-detection-notice">当前检测方式随障碍类型变化，胶囊代理不代表所有分支都使用统一胶囊检测。</p>
    <details class="movement-detection-details"><summary>按障碍类型查看检测方式</summary><ul>
      <li>轴对齐盒体障碍：使用角色包围盒进行盒体扫掠。</li>
      <li>旋转盒体障碍：使用胶囊代理进行扫掠。</li>
      <li>球体或胶囊障碍：水平圆形判定结合高度重叠。</li>
    </ul></details>`;
}

function formatMovementCollisionNumber(value) {
  return String(Number(Number(value).toFixed(6)));
}

function renderMovementCollisionInspectorSummary() {
  const view = getMovementCollisionPreview();
  const f = formatMovementCollisionNumber;
  const rows = [["高度", view.source.height, view.effective.height], ["半径", view.source.radius, view.effective.radius],
    ["中心高度", view.source.center.y, view.effective.center.y]];
  return `<section class="movement-collision-summary" data-movement-collision-summary>
    <strong>默认站立：代理与注册体</strong>
    <p class="movement-proxy-legend">移动胶囊代理 · 半径 ${f(view.proxy.radius)} m · 高 ${f(view.proxy.height)} m</p>
    <p class="movement-box-legend">注册盒体 / 包围盒 · 宽 ${f(view.registeredBounds.width)} × 高 ${f(view.registeredBounds.height)} × 深 ${f(view.registeredBounds.depth)} m</p>
    <div class="movement-collision-height"><span>底部距地 <b>${f(view.effective.bottom)} m</b></span><span>顶部高度 <b>${f(view.effective.top)} m</b></span></div>
    <table><thead><tr><th>字段（m）</th><th>配置值</th><th>站立参数</th></tr></thead><tbody>${rows.map(([name, source, effective]) =>
      `<tr${Math.abs(source - effective) > 0.0000001 ? ' class="has-difference"' : ""}><th>${name}</th><td>${f(source)}</td><td>${f(effective)}</td></tr>`).join("")}</tbody></table>
    <p class="field-help">站立参数按毫米精度计算，代理与注册体共享这些尺寸和中心。蹲伏、动作和运行中状态可另行覆盖。</p>
    ${renderMovementDetectionExplanation()}
    ${view.notes.map(note => `<p class="movement-collision-note">${escapeHtml(note)}</p>`).join("")}</section>`;
}

function refreshMovementCollisionInspectorSummary() {
  const summary = el.inspector?.querySelector("[data-movement-collision-summary]");
  if (summary) summary.outerHTML = renderMovementCollisionInspectorSummary();
  const button = el.inspector?.querySelector("[data-movement-align-ground]");
  if (button) {
    const aligned = Math.abs(getMovementCollisionPreview().effective.bottom) < 0.0000001;
    button.disabled = !state.canWrite || aligned;
    button.textContent = aligned ? "底部已对齐地面" : "底部对齐地面";
  }
}

function alignMovementCollisionToGround() {
  const body = state.package?.geometry?.bodyProfile;
  if (!state.canWrite || !body || state.selectedPath !== "geometry/body/capsule") return false;
  const view = getMovementCollisionPreview(body);
  if (Math.abs(view.effective.bottom) < 0.0000001) return false;
  commitUndoTransaction();
  beginUndoTransaction("移动碰撞底部对齐地面", "body-movement:align-ground");
  if (!body.defaultCapsule) body.defaultCapsule = {};
  if (!body.defaultCapsule.center) body.defaultCapsule.center = {};
  body.defaultCapsule.center.y = view.effective.height / 2;
  commitUndoTransaction();
  state.message = "已将默认站立代理与注册体的底部对齐地面。";
  renderShellStatus();
  renderInspector();
  if (!globalThis.refreshMovementCollisionPreview?.()) renderViewport();
  globalThis.scheduleCharacterStudioSessionPersistence?.();
  return true;
}

function createMovementCollisionPreviewGeometry(THREE, body, showSource = false, poseAdapter = "", showRegistered = true) {
  const view = getMovementCollisionPreview(body);
  const group = new THREE.Group();
  group.name = "movement-collision-preview";
  const proxyShape = new THREE.CapsuleGeometry(view.proxy.radius, Math.max(0, view.proxy.height - view.proxy.radius * 2), 8, 24);
  const proxyMesh = new THREE.Mesh(proxyShape, new THREE.MeshBasicMaterial({ color: 0x40d7ef, transparent: true, opacity: 0.16, depthWrite: false }));
  proxyMesh.name = "movement-capsule-proxy";
  proxyMesh.position.y = view.proxy.center.y;
  proxyMesh.userData.objectPath = "geometry/body/capsule";
  proxyMesh.add(new THREE.LineSegments(new THREE.WireframeGeometry(proxyShape), new THREE.LineBasicMaterial({ color: 0x40d7ef, transparent: true, opacity: 0.5, depthWrite: false })));
  group.add(proxyMesh);
  if (showRegistered) {
    const shape = new THREE.BoxGeometry(view.registeredBounds.width, view.registeredBounds.height, view.registeredBounds.depth);
    const mesh = new THREE.Mesh(shape, new THREE.MeshBasicMaterial({ color: 0xb89af6, transparent: true, opacity: 0.025, depthWrite: false }));
    mesh.name = "movement-standing-box";
    mesh.position.y = view.registeredBounds.center.y;
    mesh.add(new THREE.LineSegments(new THREE.EdgesGeometry(shape), new THREE.LineBasicMaterial({ color: 0xb89af6, depthTest: false, depthWrite: false })));
    group.add(mesh);
  }
  const groundSize = Math.max(view.effective.radius + 0.12, 0.25);
  const points = [[-groundSize, 0, -groundSize], [groundSize, 0, -groundSize], [groundSize, 0, groundSize], [-groundSize, 0, groundSize]];
  const ground = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(points.map(p => new THREE.Vector3(...p))),
    new THREE.LineBasicMaterial({ color: 0xb9c7d9, transparent: true, opacity: 0.7 }));
  ground.name = "movement-ground-reference";
  group.add(ground);
  if (showSource && view.sourceCapsuleValid) {
    const capsule = new THREE.CapsuleGeometry(view.source.radius, view.source.height - view.source.radius * 2, 8, 24);
    const ghost = new THREE.LineSegments(new THREE.WireframeGeometry(capsule), new THREE.LineBasicMaterial({ color: 0xffb24c, transparent: true, opacity: 0.65, depthTest: false }));
    capsule.dispose();
    ghost.name = "movement-source-capsule";
    setObjectLocalPose(THREE, ghost, { position: view.source.center }, { poseAdapter });
    group.add(ghost);
  }
  return group;
}

function renderMovementCollisionLegend() {
  if (state.selectedPath !== "geometry/body/capsule") return "";
  const view = getMovementCollisionPreview();
  const f = formatMovementCollisionNumber;
  return `<aside class="movement-collision-legend"><strong>默认站立预览</strong><span class="movement-proxy-legend">青色：移动胶囊代理</span>
    ${state.movementRegisteredBoundsVisible !== false ? '<span class="movement-box-legend">紫色：注册阻挡盒体 / 包围盒</span>' : ""}
    ${state.movementSourceCapsuleVisible ? `<span class="movement-source-legend">${view.sourceCapsuleValid ? "橙色：源胶囊参考" : "源胶囊尺寸无效，参考未绘制"}</span>` : ""}
    <span>灰色：地面基准 Y = 0</span><span>代理半径 ${f(view.proxy.radius)} m · 高 ${f(view.proxy.height)} m</span>
    <span>共同底部距地 ${f(view.effective.bottom)} m</span><small>检测方式随障碍类型变化。</small></aside>`;
}

function renderMovementCollisionSvg() {
  if (state.selectedPath !== "geometry/body/capsule") return "";
  const view = getMovementCollisionPreview();
  const e = view.effective, s = view.source;
  const x = 50 - e.radius * 38, y = 90 - e.top * 38;
  let result = `<g><rect data-movement-primitive="capsule-proxy" data-object-path="geometry/body/capsule" x="${x}" y="${y}" width="${e.width * 38}" height="${e.height * 38}" rx="${view.proxy.radius * 38}" fill="rgba(64,215,239,0.12)" stroke="#40d7ef"/>
    <path d="M${x - 5},90 H${x + e.width * 38 + 5}" stroke="#b9c7d9"/></g>`;
  if (state.movementRegisteredBoundsVisible !== false) {
    result += `<rect data-movement-primitive="registered-box" x="${x}" y="${y}" width="${e.width * 38}" height="${e.height * 38}" fill="none" stroke="#b89af6" pointer-events="none"/>`;
  }
  if (state.movementSourceCapsuleVisible && view.sourceCapsuleValid) {
    result += `<rect data-movement-primitive="source-capsule" x="${50 + (s.center.x - s.radius) * 38}" y="${90 - (s.center.y + s.height / 2) * 38}" width="${s.radius * 76}" height="${s.height * 38}" rx="${s.radius * 38}" fill="none" stroke="#ffb24c" stroke-dasharray="1.5 1" pointer-events="none"/>`;
  }
  return result;
}

Object.assign(globalThis, { movementMetersToMillimeters, getMovementCollisionPreview, formatMovementCollisionNumber,
  renderMovementCollisionInspectorSummary, renderMovementDetectionExplanation, refreshMovementCollisionInspectorSummary, alignMovementCollisionToGround,
  createMovementCollisionPreviewGeometry, renderMovementCollisionLegend, renderMovementCollisionSvg });
