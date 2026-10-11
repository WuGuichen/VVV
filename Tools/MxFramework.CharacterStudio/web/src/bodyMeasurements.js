// Declared dimensions are reference guides. They do not rescale model, capsule or hurtbox data.

function getBodyMeasurementData(body = state.package?.geometry?.bodyProfile || {}) {
  const number = (value, fallback) => Number.isFinite(Number(value)) && value != null ? Number(value) : fallback;
  return { height: Math.max(0, number(body.heightMeters, 1.8)), radius: Math.max(0, number(body.radiusMeters, 0.35)),
    mass: Math.max(0, number(body.massKg, 0)) };
}

function formatBodyMeasurement(value) {
  return String(Number(Number(value).toFixed(3)));
}

function shouldShowBodyMeasurements() {
  return state.selectedPath === "geometry/body" && state.bodyMeasurementsVisible !== false;
}

function createBodyMeasurementGuides(THREE, body, createLabel = null) {
  const data = getBodyMeasurementData(body);
  const group = new THREE.Group();
  group.name = "body-dimension-guides";
  group.userData.dimensions = data;
  const rulerX = -data.radius - 0.26;
  const addLines = (name, points, color) => {
    const geometry = new THREE.BufferGeometry().setFromPoints(points.map(point => new THREE.Vector3(...point)));
    const line = new THREE.LineSegments(geometry, new THREE.LineBasicMaterial({ color, transparent: true, opacity: 0.9, depthTest: false, depthWrite: false }));
    line.name = name;
    line.renderOrder = 10;
    group.add(line);
  };
  const heightPoints = [[rulerX, 0, 0], [rulerX, data.height, 0],
    [rulerX - 0.08, 0, 0], [rulerX + 0.12, 0, 0],
    [rulerX - 0.08, data.height, 0], [rulerX + 0.12, data.height, 0]];
  const tickStep = Math.max(0.1, Math.ceil(data.height / 10) / 10);
  for (let index = 1; index < Math.ceil(data.height / tickStep); index++) {
    const y = index * tickStep;
    heightPoints.push([rulerX, y, 0], [rulerX + (index % 5 === 0 ? 0.07 : 0.035), y, 0]);
  }
  addLines("reference-height", heightPoints, 0x40d7ef);
  const radiusPoints = [[0, 0.015, 0], [data.radius, 0.015, 0],
    [0, 0.015, -0.05], [0, 0.015, 0.05], [data.radius, 0.015, -0.05], [data.radius, 0.015, 0.05]];
  for (let index = 0; index < 64; index++) {
    const a = index * Math.PI * 2 / 64, b = (index + 1) * Math.PI * 2 / 64;
    radiusPoints.push([Math.cos(a) * data.radius, 0.015, Math.sin(a) * data.radius],
      [Math.cos(b) * data.radius, 0.015, Math.sin(b) * data.radius]);
  }
  addLines("reference-radius", radiusPoints, 0xf5b544);
  addLines("ground-origin", [[-0.1, 0, 0], [0.1, 0, 0], [0, 0, -0.1], [0, 0, 0.1]], 0xb9c7d9);
  if (createLabel) {
    for (const [text, color, position] of [
      [`参考身高 ${formatBodyMeasurement(data.height)} m`, 0x40d7ef, [rulerX - 0.25, data.height * 0.62, 0]],
      [`参考半径 ${formatBodyMeasurement(data.radius)} m`, 0xf5b544, [data.radius * 0.5, 0.14, data.radius + 0.2]],
      ["地面 Y = 0", 0xb9c7d9, [rulerX, 0.07, 0.15]]
    ]) {
      const label = createLabel(THREE, [text], color);
      label.position.set(...position);
      label.scale.multiplyScalar(0.7);
      group.add(label);
    }
  }
  return group;
}

function renderBodyMeasurementLegend() {
  if (!shouldShowBodyMeasurements()) return "";
  const data = getBodyMeasurementData();
  const mass = data.mass > 0 ? `${formatBodyMeasurement(data.mass)} kg` : "使用游戏默认质量";
  return `<aside class="body-measurement-legend" aria-label="身体尺寸参考"><strong>尺寸参考</strong>
    <span class="body-height-legend">参考身高 ${formatBodyMeasurement(data.height)} m</span>
    <span class="body-radius-legend">参考半径 ${formatBodyMeasurement(data.radius)} m</span>
    <span>地面基准 Y = 0</span><span>质量：${mass}</span><small>标尺与参考圆不代表实际碰撞边界。</small></aside>`;
}

function renderBodyMeasurementSvg() {
  if (!shouldShowBodyMeasurements()) return "";
  const data = getBodyMeasurementData();
  const x = 50 - (data.radius + 0.26) * 38, top = 90 - data.height * 38, r = data.radius * 38;
  return `<g class="body-dimension-guides" pointer-events="none" fill="none" stroke-width="0.5">
    <path d="M${x},90 V${top} M${x - 2},90 H${x + 3} M${x - 2},${top} H${x + 3}" stroke="#40d7ef"/>
    <ellipse cx="50" cy="90" rx="${r}" ry="3" stroke="#f5b544"/>
    <path d="M50,90 H${50 + r} M50,88 V92 M${50 + r},88 V92" stroke="#f5b544"/>
    <text x="${x - 2}" y="${(top + 90) / 2}" text-anchor="end" fill="#40d7ef" font-size="2.8">${formatBodyMeasurement(data.height)} m</text>
    <text x="50" y="96" fill="#f5b544" font-size="2.8">r = ${formatBodyMeasurement(data.radius)} m</text>
    <text x="3" y="90" fill="#b9c7d9" font-size="2.8">Y = 0</text></g>`;
}

Object.assign(globalThis, { getBodyMeasurementData, formatBodyMeasurement, shouldShowBodyMeasurements,
  createBodyMeasurementGuides, renderBodyMeasurementLegend, renderBodyMeasurementSvg });
