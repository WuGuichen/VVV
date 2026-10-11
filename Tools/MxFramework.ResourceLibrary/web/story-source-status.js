(function (global) {
  "use strict";

  const PROVIDER_ID = "storySource";
  const PACKET_EDIT_STATUSES = ["draft", "superseded", "rejected"];
  const CANON_TYPES = ["world", "location", "faction", "character_archetype", "enemy_origin", "power_rule", "symbol", "tone", "forbidden_content"];
  const CANON_STATUSES = ["draft", "canon", "deprecated", "rejected"];
  const OUTLINE_EDIT_STAGES = ["draft", "review", "superseded"];
  const REQUIRED_FIELDS = [
    "worldPremise",
    "coreEvent",
    "locationFunction",
    "civilizationOrigin",
    "playerRole",
    "enemyOrigins",
    "powerRules",
    "symbolMeanings",
    "toneTags",
    "visualDoNotUse"
  ];
  const FIELD_LABELS = {
    title: "标题",
    type: "类型",
    status: "状态",
    stage: "阶段",
    tags: "标签",
    relations: "关系",
    version: "版本",
    approvalStatus: "审批状态",
    targetConsumers: "消费方",
    canonRefs: "Canon 引用",
    outlineRefs: "大纲引用",
    reviewStatus: "评审状态",
    exportPath: "导出路径",
    outlinePath: "Markdown 路径",
    beats: "Beat Flow",
    conflicts: "冲突",
    openQuestions: "开放问题",
    worldPremise: "世界前提",
    coreEvent: "核心事件",
    locationFunction: "地点功能",
    civilizationOrigin: "文明来源",
    playerRole: "玩家身份",
    enemyOrigins: "敌人来源",
    powerRules: "力量规则",
    symbolMeanings: "符号语义",
    toneTags: "情绪标签",
    visualDoNotUse: "禁用设定"
  };

  function createModel(items, resourcesPayload, selectedKeys, packetDrafts, packetSaveState, canonDrafts, canonSaveState, outlineDrafts, outlineSaveState) {
    const storyItems = asArray(items).filter(item => item && item.providerId === PROVIDER_ID);
    const snapshot = extractSnapshot(resourcesPayload);
    const canonFacts = normalizeCanonFacts(snapshot.canonFacts, storyItems).sort(compareCanonFacts);
    const outlines = normalizeOutlines(snapshot.outlines, storyItems).sort(compareOutlines);
    const packets = normalizePackets(snapshot.packets, storyItems).sort(comparePackets);
    const selected = normalizeSelectedKeys(selectedKeys);
    const selectedCanon = findSelectedByKey(canonFacts, selected.canon);
    const selectedOutline = findSelectedByKey(outlines, selected.outline);
    const selectedPacket = findSelectedByKey(packets, selected.packet);

    canonFacts.forEach(fact => {
      fact.isSelected = Boolean(selectedCanon && selectedCanon.key === fact.key);
      fact.draft = createDraftForCanon(fact, canonDrafts && canonDrafts[fact.canonId]);
      fact.dirty = isDirty(createDraftForCanon(fact), fact.draft);
      fact.saveState = normalizeSaveState(canonSaveState, fact.canonId);
      fact.readiness = getCanonReadiness(fact.draft, fact.diagnostics);
      fact.isLinked = false;
    });
    outlines.forEach(outline => {
      outline.isSelected = Boolean(selectedOutline && selectedOutline.key === outline.key);
      outline.draft = createDraftForOutline(outline, outlineDrafts && outlineDrafts[outline.outlineId]);
      outline.dirty = isDirty(createDraftForOutline(outline), outline.draft);
      outline.saveState = normalizeSaveState(outlineSaveState, outline.outlineId);
      outline.approvalIssues = getOutlineApprovalIssues(outline.draft, canonFacts);
      outline.readiness = getOutlineReadiness(outline.draft, outline.approvalIssues, outline.diagnostics);
      outline.isLinked = false;
    });
    packets.forEach(packet => {
      packet.isSelected = Boolean(selectedPacket && selectedPacket.key === packet.key);
      packet.draft = createDraftForPacket(packet, packetDrafts && packetDrafts[packet.storySourceId]);
      packet.dirty = isDirty(createDraftForPacket(packet), packet.draft);
      packet.saveState = normalizeSaveState(packetSaveState, packet.storySourceId);
      packet.missingNow = getMissingRequiredFields(packet.draft);
      packet.derivationIssues = getPacketDerivationIssues(packet.draft, canonFacts, outlines);
      packet.readiness = getPacketReadiness(packet.draft, packet.missingNow, packet.derivationIssues, packet.diagnostics);
    });

    applyLinkHighlights(canonFacts, outlines, selectedOutline, selectedPacket);
    const selectedFallback = selectedPacket || selectedOutline || selectedCanon || null;
    const storyProgress = getStoryProgress(canonFacts, outlines, packets);
    return {
      providerId: PROVIDER_ID,
      items: storyItems,
      canonFacts,
      outlines,
      packets,
      selected: selectedFallback,
      selectedCanon,
      selectedOutline,
      selectedPacket,
      selectedKey: selectedFallback ? selectedFallback.key : "",
      selectedKeys: {
        canon: selectedCanon ? selectedCanon.key : "",
        outline: selectedOutline ? selectedOutline.key : "",
        packet: selectedPacket ? selectedPacket.key : ""
      },
      packetsHash: snapshot.packetsHash,
      progress: storyProgress,
      nextAction: getNextStoryAction(storyProgress, selectedPacket, selectedOutline),
      hasProviderItems: storyItems.length > 0,
      hasPackets: packets.length > 0,
      hasCanon: canonFacts.length > 0,
      hasOutlines: outlines.length > 0
    };
  }

  function render(model) {
    if (!model || (!model.hasProviderItems && !model.hasPackets && !model.hasCanon && !model.hasOutlines)) {
      return `
        <div class="story-empty-state">
          <h2>故事设定编辑器</h2>
          <p>当前资源集合没有读取到 StorySource 数据源。确认 StorySource/Projects/WGame 存在后刷新。</p>
        </div>`;
    }

    return `
      <div class="story-shell story-shell-v3">
        ${renderStoryHeader(model)}
        <section class="story-workbench story-workbench-v3" aria-label="StorySource story foundation">
          ${renderCanonColumn(model)}
          ${renderOutlineColumn(model)}
          ${renderPacketColumn(model)}
        </section>
      </div>`;
  }

  function renderStoryHeader(model) {
    const progress = model.progress || {};
    return `
      <section class="story-status-header story-editor-header">
        <div class="story-title-block">
          <span class="story-eyebrow">StorySource</span>
          <h2>故事设定编辑器</h2>
        </div>
        <div class="story-progress-strip" aria-label="故事设定进度">
          ${renderProgressMetric("正史", progress.canonReady, progress.canonTotal, "canon")}
          ${renderProgressMetric("大纲", progress.outlineReady, progress.outlineTotal, "outline")}
          ${renderProgressMetric("给美术", progress.packetReady, progress.packetTotal, "packet")}
        </div>
        <div class="story-next-action">
          <span>下一步</span>
          <strong>${escapeHtml(model.nextAction.title)}</strong>
          <small>${escapeHtml(model.nextAction.detail)}</small>
        </div>
      </section>`;
  }

  function renderProgressMetric(label, ready, total, tone) {
    return `
      <div class="story-progress-metric story-tone-${escapeHtml(tone)}">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(ready)}/${escapeHtml(total)}</strong>
      </div>`;
  }

  function renderCanonColumn(model) {
    const selected = model.selectedCanon || model.canonFacts[0] || null;
    return `
      <section class="story-column story-canon-column">
        ${renderColumnHeading("世界观记忆", `${model.canonFacts.length} 条`, "Canon facts 是正史事实源。")}
        <div class="story-mini-filters">
          <span>类型：${escapeHtml(formatTypeSummary(model.canonFacts))}</span>
          <span>状态：${escapeHtml(formatStatusSummary(model.canonFacts))}</span>
        </div>
        <div class="story-list story-canon-list">
          ${model.canonFacts.map(fact => renderCanonRow(fact)).join("") || emptyBlock("还没有世界观记忆。")}
        </div>
        ${renderCanonEditor(selected)}
      </section>`;
  }

  function renderCanonRow(fact) {
    const classes = ["story-row", "story-setting-row"];
    if (fact.isSelected) classes.push("selected");
    if (fact.isLinked) classes.push("linked");
    return `
      <button type="button" class="${classes.join(" ")}" data-story-canon-key="${escapeHtml(fact.key)}"${fact.isSelected ? " aria-current=\"true\"" : ""}>
        <span class="story-row-top">
          <strong>${escapeHtml(fact.title || fact.canonId)}</strong>
          ${renderStatusPill(formatCanonStatus(fact.status), fact.status)}
        </span>
        <span>${escapeHtml(formatCanonType(fact.type))}</span>
        <small>${escapeHtml(fact.summary || "未填写摘要")}</small>
      </button>`;
  }

  function renderCanonEditor(fact) {
    if (!fact) return emptyBlock("选择一条世界观记忆。");
    const draft = fact.draft || createDraftForCanon(fact);
    const busy = fact.saveState.status === "running";
    return `
      <div class="story-editor-card story-setting-editor story-save-${escapeHtml(fact.saveState.status || "idle")}">
        ${renderEditorTitle("Canon Fact", fact.canonId, fact.dirty, fact.readiness)}
        ${renderFieldGroup("基础信息", `
          <div class="story-form-grid">
            ${renderInput("data-story-canon-field=\"title\" data-story-canon-id=\"" + escapeHtml(fact.canonId) + "\"", "标题", draft.title, busy)}
            ${renderSelectField("data-story-canon-field=\"type\" data-story-canon-id=\"" + escapeHtml(fact.canonId) + "\"", "类型", draft.type, CANON_TYPES, formatCanonType, busy)}
            ${renderSelectField("data-story-canon-field=\"status\" data-story-canon-id=\"" + escapeHtml(fact.canonId) + "\"", "状态", draft.status, CANON_STATUSES, formatCanonStatus, busy)}
          </div>`)}
        ${renderFieldGroup("设定正文", `
          ${renderTextarea("data-story-canon-field=\"summary\" data-story-canon-id=\"" + escapeHtml(fact.canonId) + "\"", "摘要", draft.summary, 5, busy)}`)}
        ${renderFieldGroup("标签 / 关系", `
          ${renderLineListField("canon", fact.canonId, "tags", "标签", draft.tags, busy)}
          ${renderRelationField(fact, draft, busy)}`)}
        ${renderDiagnosticPanel("服务端诊断", fact.diagnostics)}
        ${renderActionBar([
          renderButton("重置", "secondary", `data-story-canon-action="reset" data-story-canon-id="${escapeHtml(fact.canonId)}"`, busy || !fact.dirty),
          renderButton(busy ? "保存中..." : "保存记忆", "", `data-story-canon-action="save" data-story-canon-id="${escapeHtml(fact.canonId)}"`, busy || !fact.dirty)
        ])}
        ${renderSaveMessage(fact.saveState)}
      </div>`;
  }

  function renderOutlineColumn(model) {
    const selected = model.selectedOutline || model.outlines[0] || null;
    return `
      <section class="story-column story-outline-column">
        ${renderColumnHeading("故事大纲", `${model.outlines.length} 条`, "把正史事实组织成可派生的 beat flow。")}
        <div class="story-list story-outline-list">
          ${model.outlines.map(outline => renderOutlineRow(outline)).join("") || emptyBlock("还没有故事大纲。")}
        </div>
        ${renderOutlineEditor(selected, model)}
      </section>`;
  }

  function renderOutlineRow(outline) {
    const classes = ["story-row", "story-setting-row"];
    if (outline.isSelected) classes.push("selected");
    if (outline.isLinked) classes.push("linked");
    return `
      <button type="button" class="${classes.join(" ")}" data-story-outline-key="${escapeHtml(outline.key)}"${outline.isSelected ? " aria-current=\"true\"" : ""}>
        <span class="story-row-top">
          <strong>${escapeHtml(outline.title || outline.outlineId)}</strong>
          ${renderStatusPill(formatOutlineStage(outline.stage), outline.stage)}
        </span>
        <span>${escapeHtml(outline.beats.length)} beat · ${escapeHtml(outline.outlineExists ? "Markdown 已连接" : "Markdown 缺失")}</span>
        <small>${escapeHtml(outline.openQuestions[0] || outline.outlinePath || "无开放问题")}</small>
      </button>`;
  }

  function renderOutlineEditor(outline, model) {
    if (!outline) return emptyBlock("选择一个故事大纲。");
    const draft = outline.draft || createDraftForOutline(outline);
    const busy = outline.saveState.status === "running";
    const issues = outline.approvalIssues || [];
    return `
      <div class="story-editor-card story-setting-editor story-save-${escapeHtml(outline.saveState.status || "idle")}">
        ${renderEditorTitle("Story Outline", outline.outlineId, outline.dirty, outline.readiness)}
        ${renderFieldGroup("基础信息", `
          <div class="story-form-grid">
            ${renderInput("data-story-outline-field=\"title\" data-story-outline-id=\"" + escapeHtml(outline.outlineId) + "\"", "标题", draft.title, busy)}
            ${renderSelectField("data-story-outline-field=\"stage\" data-story-outline-id=\"" + escapeHtml(outline.outlineId) + "\"", "阶段", draft.stage, optionsWithCurrent(draft.stage, OUTLINE_EDIT_STAGES), formatOutlineStage, busy)}
          </div>
          ${renderInput("data-story-outline-field=\"outlinePath\" data-story-outline-id=\"" + escapeHtml(outline.outlineId) + "\"", "Markdown 路径", draft.outlinePath, busy)}`)}
        ${renderFieldGroup("Canon 引用", renderLineListField("outline", outline.outlineId, "canonRefs", "Canon 引用", draft.canonRefs, busy, {
          values: model.canonFacts,
          idField: "canonId",
          statusField: "status",
          okValue: "canon",
          missingText: "未引用 Canon"
        }))}
        ${renderFieldGroup("Beat Flow", renderBeatRows(outline, draft, busy))}
        ${renderFieldGroup("冲突 / 开放问题", `
          ${renderLineListField("outline", outline.outlineId, "conflicts", "冲突", draft.conflicts, busy)}
          ${renderLineListField("outline", outline.outlineId, "openQuestions", "开放问题", draft.openQuestions, busy)}`)}
        ${renderDiagnosticPanel("审批阻塞", outline.diagnostics, issues)}
        ${renderActionBar([
          renderButton("重置", "secondary", `data-story-outline-action="reset" data-story-outline-id="${escapeHtml(outline.outlineId)}"`, busy || !outline.dirty),
          renderButton(busy ? "保存中..." : "保存大纲", "", `data-story-outline-action="save" data-story-outline-id="${escapeHtml(outline.outlineId)}"`, busy || !outline.dirty),
          renderButton("批准给输入包", "", `data-story-outline-action="approve" data-story-outline-id="${escapeHtml(outline.outlineId)}"`, busy || outline.dirty || issues.length > 0)
        ])}
        ${renderSaveMessage(outline.saveState)}
      </div>`;
  }

  function renderBeatRows(outline, draft, busy) {
    const beats = asArray(draft.beats);
    const rows = beats.map((beat, index) => `
      <div class="story-beat-row">
        <div class="story-beat-index">${escapeHtml(index + 1)}</div>
        ${renderBeatInput(outline, index, "slug", beat.slug || "", "slug", busy)}
        ${renderBeatInput(outline, index, "title", beat.title || "", "标题", busy)}
        ${renderBeatInput(outline, index, "summary", beat.summary || "", "摘要", busy)}
        <button type="button" class="secondary story-icon-button" data-story-outline-beat-action="remove" data-story-outline-id="${escapeHtml(outline.outlineId)}" data-story-outline-beat-index="${escapeHtml(index)}"${busy || beats.length <= 1 ? " disabled" : ""}>删</button>
      </div>`).join("");
    return `
      <div class="story-beat-editor">
        ${rows || `<div class="empty-block">还没有 beat。</div>`}
        <button type="button" class="secondary" data-story-outline-beat-action="add" data-story-outline-id="${escapeHtml(outline.outlineId)}"${busy ? " disabled" : ""}>新增 beat</button>
      </div>`;
  }

  function renderBeatInput(outline, index, field, value, label, busy) {
    return `
      <label class="story-beat-field story-beat-${escapeHtml(field)}">
        <span>${escapeHtml(label)}</span>
        <input data-story-outline-beat-field="${escapeHtml(field)}" data-story-outline-id="${escapeHtml(outline.outlineId)}" data-story-outline-beat-index="${escapeHtml(index)}" value="${escapeHtml(value)}"${busy ? " disabled" : ""}>
      </label>`;
  }

  function renderPacketColumn(model) {
    const selected = model.selectedPacket || model.packets[0] || null;
    return `
      <section class="story-column story-packet-column">
        ${renderColumnHeading("输入包派生", `${model.packets.length} 个`, "批准后 ArtSource 才能生产参考图。")}
        <div class="story-list story-packet-list">
          ${model.packets.map(packet => renderPacketRow(packet)).join("") || emptyBlock("还没有输入包。")}
        </div>
        ${renderPacketEditor(selected, model)}
      </section>`;
  }

  function renderPacketRow(packet) {
    const classes = ["story-row", "story-setting-row"];
    if (packet.isSelected) classes.push("selected");
    return `
      <button type="button" class="${classes.join(" ")}" data-story-packet-key="${escapeHtml(packet.key)}"${packet.isSelected ? " aria-current=\"true\"" : ""}>
        <span class="story-row-top">
          <strong>${escapeHtml(packet.storySourceId)}</strong>
          ${renderStatusPill(formatApprovalStatus(packet.approvalStatus), packet.approvalStatus)}
        </span>
        <span>${escapeHtml(packet.version || "未填版本")}</span>
        <small>${packet.missingRequiredFields.length > 0 ? `${packet.missingRequiredFields.length} 个字段未填` : "字段完整"}</small>
      </button>`;
  }

  function renderPacketEditor(packet, model) {
    if (!packet) return emptyBlock("选择一个 Story Input Packet。");
    const draft = packet.draft || createDraftForPacket(packet);
    const busy = packet.saveState.status === "running";
    const missing = packet.missingNow || [];
    const derivation = packet.derivationIssues || [];
    return `
      <div class="story-editor-card story-setting-editor story-save-${escapeHtml(packet.saveState.status || "idle")}">
        ${renderEditorTitle("Story Input Packet", packet.storySourceId, packet.dirty, packet.readiness)}
        ${renderFieldGroup("审批与交接", `
          <div class="story-form-grid">
            ${renderPacketInput(packet, "version", draft.version, busy)}
            ${renderPacketSelect(packet, "approvalStatus", draft.approvalStatus, optionsWithCurrent(draft.approvalStatus, PACKET_EDIT_STATUSES), formatApprovalStatus, busy)}
            ${renderPacketInput(packet, "reviewStatus", draft.reviewStatus, busy)}
            ${renderPacketInput(packet, "exportPath", draft.exportPath, busy)}
          </div>
          ${renderPacketArray(packet, "targetConsumers", draft.targetConsumers, busy)}`)}
        ${renderFieldGroup("派生引用", `
          ${renderLineListField("packet", packet.storySourceId, "canonRefs", "Canon 引用", draft.canonRefs, busy, {
            values: model.canonFacts,
            idField: "canonId",
            statusField: "status",
            okValue: "canon",
            missingText: "未引用 Canon"
          })}
          ${renderLineListField("packet", packet.storySourceId, "outlineRefs", "大纲引用", draft.outlineRefs, busy, {
            values: model.outlines,
            idField: "outlineId",
            statusField: "stage",
            okValue: "approved-for-packet",
            missingText: "未引用故事大纲"
          })}`)}
        ${renderFieldGroup("故事核心", `
          ${renderPacketText(packet, "worldPremise", draft.worldPremise, busy)}
          ${renderPacketText(packet, "coreEvent", draft.coreEvent, busy)}
          ${renderPacketText(packet, "playerRole", draft.playerRole, busy)}`)}
        ${renderFieldGroup("世界 / 地点", `
          ${renderPacketText(packet, "locationFunction", draft.locationFunction, busy)}
          ${renderPacketText(packet, "civilizationOrigin", draft.civilizationOrigin, busy)}
          ${renderPacketArray(packet, "enemyOrigins", draft.enemyOrigins, busy)}`)}
        ${renderFieldGroup("视觉约束", `
          ${renderPacketArray(packet, "powerRules", draft.powerRules, busy)}
          ${renderPacketArray(packet, "symbolMeanings", draft.symbolMeanings, busy)}
          ${renderPacketArray(packet, "toneTags", draft.toneTags, busy)}
          ${renderPacketArray(packet, "visualDoNotUse", draft.visualDoNotUse, busy)}`)}
        ${renderDiagnosticPanel("审批阻塞", packet.diagnostics, [...missing.map(field => `缺少字段：${FIELD_LABELS[field] || field}`), ...derivation])}
        ${renderActionBar([
          renderButton("重置", "secondary", `data-story-action="reset" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, busy || !packet.dirty),
          renderButton(busy ? "保存中..." : "保存输入包", "", `data-story-action="save" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, busy || !packet.dirty),
          renderButton("批准给美术", "", `data-story-action="approve" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, busy || missing.length > 0 || derivation.length > 0 || packet.dirty)
        ])}
        <details class="story-collapse story-tech-details">
          <summary>技术交接</summary>
          <dl class="story-detail-list">
            <div><dt>Packet</dt><dd>${escapeHtml(packet.packetPath || "(empty)")}</dd></div>
            <div><dt>Hash</dt><dd>${escapeHtml(model.packetsHash || "(empty)")}</dd></div>
            <div><dt>ArtSource</dt><dd>${escapeHtml(packet.artSourceTaskId || "未绑定")}</dd></div>
          </dl>
        </details>
        ${renderSaveMessage(packet.saveState)}
      </div>`;
  }

  function renderColumnHeading(title, count, description) {
    return `
      <div class="story-panel-heading">
        <div>
          <h3>${escapeHtml(title)}</h3>
          <p>${escapeHtml(description)}</p>
        </div>
        <span>${escapeHtml(count)}</span>
      </div>`;
  }

  function renderEditorTitle(kind, id, dirty, readiness) {
    return `
      <div class="story-editor-title story-editor-title-v3">
        <div>
          <span>${escapeHtml(kind)}</span>
          <strong>${escapeHtml(id)}</strong>
        </div>
        <div class="story-title-status">
          ${renderStatusPill(dirty ? "未保存" : "已同步", dirty ? "dirty" : "synced")}
          ${readiness ? renderStatusPill(readiness.label, readiness.tone) : ""}
        </div>
      </div>`;
  }

  function renderFieldGroup(title, content) {
    return `
      <section class="story-field-group">
        <h4>${escapeHtml(title)}</h4>
        <div class="story-field-group-body">${content}</div>
      </section>`;
  }

  function renderInput(attrs, label, value, busy) {
    return `<label><span>${escapeHtml(label)}</span><input ${attrs} value="${escapeHtml(value || "")}"${busy ? " disabled" : ""}></label>`;
  }

  function renderTextarea(attrs, label, value, rows, busy) {
    return `<label><span>${escapeHtml(label)}</span><textarea ${attrs} rows="${escapeHtml(rows || 3)}"${busy ? " disabled" : ""}>${escapeHtml(value || "")}</textarea></label>`;
  }

  function renderSelectField(attrs, label, value, options, formatter, busy) {
    return `<label><span>${escapeHtml(label)}</span>${renderSelect(attrs, value, options, formatter, busy)}</label>`;
  }

  function renderPacketInput(packet, field, value, busy) {
    return renderInput(`data-story-field="${escapeHtml(field)}" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, FIELD_LABELS[field] || field, value, busy);
  }

  function renderPacketSelect(packet, field, value, options, formatter, busy) {
    return renderSelectField(`data-story-field="${escapeHtml(field)}" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, FIELD_LABELS[field] || field, value, options, formatter, busy);
  }

  function renderPacketText(packet, field, value, busy) {
    return renderTextarea(`data-story-field="${escapeHtml(field)}" data-story-packet-id="${escapeHtml(packet.storySourceId)}"`, FIELD_LABELS[field] || field, value, 4, busy);
  }

  function renderPacketArray(packet, field, values, busy) {
    return renderLineListField("packet", packet.storySourceId, field, FIELD_LABELS[field] || field, values, busy);
  }

  function renderLineListField(kind, id, field, label, values, busy, options) {
    const attrs = getArrayFieldAttributes(kind, id, field);
    const normalized = asArray(values);
    return `
      <label class="story-token-field">
        <span>${escapeHtml(label)}</span>
        <div class="story-token-list">
          ${normalized.length ? normalized.map(value => renderToken(value, options)).join("") : `<small>${escapeHtml(options && options.missingText ? options.missingText : "未填写")}</small>`}
        </div>
        <textarea ${attrs} rows="2"${busy ? " disabled" : ""}>${escapeHtml(normalized.join("\n"))}</textarea>
      </label>`;
  }

  function getArrayFieldAttributes(kind, id, field) {
    if (kind === "canon") {
      return `data-story-canon-array-field="${escapeHtml(field)}" data-story-canon-id="${escapeHtml(id)}"`;
    }
    if (kind === "outline") {
      return `data-story-outline-array-field="${escapeHtml(field)}" data-story-outline-id="${escapeHtml(id)}"`;
    }
    return `data-story-array-field="${escapeHtml(field)}" data-story-packet-id="${escapeHtml(id)}"`;
  }

  function renderToken(value, options) {
    const status = resolveTokenStatus(value, options);
    return `<span class="story-token story-token-${escapeHtml(status.tone)}" title="${escapeHtml(status.title)}">${escapeHtml(value)}</span>`;
  }

  function resolveTokenStatus(value, options) {
    if (!options || !Array.isArray(options.values)) {
      return { tone: "neutral", title: value || "" };
    }
    const item = options.values.find(candidate => candidate && candidate[options.idField] === value);
    if (!item) return { tone: "missing", title: "未找到引用对象" };
    const current = item[options.statusField] || "";
    return {
      tone: current === options.okValue ? "ok" : "blocked",
      title: current ? `${options.statusField}: ${current}` : "状态未知"
    };
  }

  function renderRelationField(fact, draft, busy) {
    const relations = asArray(draft.relations);
    return `
      <label class="story-token-field">
        <span>关系</span>
        <div class="story-relation-list">
          ${relations.length ? relations.map(item => `
            <span class="story-relation-chip">
              <strong>${escapeHtml(item.relation || "")}</strong>
              <em>${escapeHtml(item.targetId || "")}</em>
            </span>`).join("") : `<small>未填写关系</small>`}
        </div>
        <textarea data-story-canon-relations-field="relations" data-story-canon-id="${escapeHtml(fact.canonId)}" rows="2" placeholder="relation | targetId"${busy ? " disabled" : ""}>${escapeHtml(formatRelations(relations))}</textarea>
      </label>`;
  }

  function renderDiagnosticPanel(title, providerDiagnostics, localIssues) {
    const diagnostics = [
      ...asArray(localIssues).map(message => ({ severity: "Warning", code: "LOCAL_STORY_HINT", message, sourceField: "" })),
      ...asArray(providerDiagnostics).map(normalizeDiagnostic)
    ].filter(item => item && (item.message || item.code));
    if (!diagnostics.length) {
      return `<p class="story-ok">当前对象没有阻塞项。</p>`;
    }
    return `
      <section class="story-diagnostic-panel">
        <div class="story-diagnostic-title">
          <strong>${escapeHtml(title)}</strong>
          <span>${escapeHtml(diagnostics.length)} 项</span>
        </div>
        <ul>
          ${diagnostics.map(diagnostic => `
            <li class="story-diagnostic-${escapeHtml(formatSeverityTone(diagnostic.severity))}">
              <span>${escapeHtml(diagnostic.sourceField ? (FIELD_LABELS[diagnostic.sourceField] || diagnostic.sourceField) : diagnostic.code || "提示")}</span>
              <p>${escapeHtml(diagnostic.message || diagnostic.code || "")}</p>
            </li>`).join("")}
        </ul>
      </section>`;
  }

  function normalizeDiagnostic(diagnostic) {
    if (!diagnostic) return null;
    return {
      severity: diagnostic.severity || diagnostic.Severity || "Info",
      code: diagnostic.code || diagnostic.Code || "",
      message: diagnostic.message || diagnostic.Message || "",
      sourceField: diagnostic.sourceField || diagnostic.SourceField || ""
    };
  }

  function renderActionBar(buttons) {
    return `<div class="story-action-row story-action-bar">${buttons.join("")}</div>`;
  }

  function renderButton(label, className, attrs, disabled) {
    return `<button type="button" class="${escapeHtml(className || "")}" ${attrs}${disabled ? " disabled" : ""}>${escapeHtml(label)}</button>`;
  }

  function renderStatusPill(label, tone) {
    return `<span class="story-status-pill story-status-${escapeHtml(normalizeTone(tone))}">${escapeHtml(label)}</span>`;
  }

  function renderSelect(attrs, value, options, formatter, busy) {
    return `<select ${attrs}${busy ? " disabled" : ""}>${asArray(options).map(option => `<option value="${escapeHtml(option)}"${option === value ? " selected" : ""}>${escapeHtml(formatter ? formatter(option) : option)}</option>`).join("")}</select>`;
  }

  function optionsWithCurrent(value, options) {
    const normalized = String(value || "").trim();
    const list = asArray(options);
    if (!normalized || list.includes(normalized)) return list;
    return [normalized, ...list];
  }

  function updateDraftField(model, drafts, packetId, field, value) {
    return updateDraft(model, drafts, packetId, "packets", "storySourceId", createDraftForPacket, draft => { draft[field] = value; });
  }

  function updateDraftArrayField(model, drafts, packetId, field, value) {
    return updateDraft(model, drafts, packetId, "packets", "storySourceId", createDraftForPacket, draft => { draft[field] = splitLines(value); });
  }

  function updateCanonDraftField(model, drafts, canonId, field, value) {
    return updateDraft(model, drafts, canonId, "canonFacts", "canonId", createDraftForCanon, draft => { draft[field] = value; });
  }

  function updateCanonDraftArrayField(model, drafts, canonId, field, value) {
    return updateDraft(model, drafts, canonId, "canonFacts", "canonId", createDraftForCanon, draft => { draft[field] = splitLines(value); });
  }

  function updateCanonDraftRelations(model, drafts, canonId, value) {
    return updateDraft(model, drafts, canonId, "canonFacts", "canonId", createDraftForCanon, draft => { draft.relations = parseRelations(value); });
  }

  function updateOutlineDraftField(model, drafts, outlineId, field, value) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => { draft[field] = value; });
  }

  function updateOutlineDraftArrayField(model, drafts, outlineId, field, value) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => { draft[field] = splitLines(value); });
  }

  function updateOutlineDraftBeats(model, drafts, outlineId, value) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => { draft.beats = parseBeats(value); });
  }

  function updateOutlineDraftBeatField(model, drafts, outlineId, index, field, value) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => {
      const beats = asArray(draft.beats).map(beat => Object.assign({ slug: "", title: "", summary: "" }, beat || {}));
      const safeIndex = Number(index);
      if (!Number.isInteger(safeIndex) || safeIndex < 0 || safeIndex >= beats.length) return;
      beats[safeIndex][field] = value;
      draft.beats = beats;
    });
  }

  function addOutlineDraftBeat(model, drafts, outlineId) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => {
      const beats = asArray(draft.beats).map(beat => Object.assign({ slug: "", title: "", summary: "" }, beat || {}));
      beats.push({ slug: `beat-${beats.length + 1}`, title: "", summary: "" });
      draft.beats = beats;
    });
  }

  function removeOutlineDraftBeat(model, drafts, outlineId, index) {
    return updateDraft(model, drafts, outlineId, "outlines", "outlineId", createDraftForOutline, draft => {
      const beats = asArray(draft.beats).map(beat => Object.assign({ slug: "", title: "", summary: "" }, beat || {}));
      const safeIndex = Number(index);
      if (!Number.isInteger(safeIndex) || safeIndex < 0 || safeIndex >= beats.length || beats.length <= 1) return;
      beats.splice(safeIndex, 1);
      draft.beats = beats;
    });
  }

  function updateDraft(model, drafts, id, collectionName, idField, factory, mutator) {
    const item = asArray(model && model[collectionName]).find(entry => entry[idField] === id || entry.key === id);
    if (!item) return drafts || {};
    const next = Object.assign({}, drafts || {});
    const draft = factory(item, next[item[idField]]);
    mutator(draft);
    next[item[idField]] = draft;
    return next;
  }

  function clearDraft(drafts, id) {
    const next = Object.assign({}, drafts || {});
    delete next[id];
    return next;
  }

  function buildUpdateRequest(model, drafts, packetId) {
    const packet = findById(model && model.packets, "storySourceId", packetId);
    if (!packet) return null;
    const draft = createDraftForPacket(packet, drafts && drafts[packet.storySourceId]);
    return Object.assign({}, draft, {
      storySourceId: packet.storySourceId,
      expectedHash: model.packetsHash || ""
    });
  }

  function buildApproveRequest(model, packetId) {
    const packet = findById(model && model.packets, "storySourceId", packetId);
    if (!packet) return null;
    return {
      storySourceId: packet.storySourceId,
      reviewStatus: "approved-for-art",
      expectedHash: model.packetsHash || ""
    };
  }

  function buildCanonUpdateRequest(model, drafts, canonId) {
    const fact = findById(model && model.canonFacts, "canonId", canonId);
    if (!fact) return null;
    return Object.assign({}, createDraftForCanon(fact, drafts && drafts[fact.canonId]), {
      canonId: fact.canonId,
      expectedHash: model.packetsHash || ""
    });
  }

  function buildOutlineUpdateRequest(model, drafts, outlineId) {
    const outline = findById(model && model.outlines, "outlineId", outlineId);
    if (!outline) return null;
    const draft = createDraftForOutline(outline, drafts && drafts[outline.outlineId]);
    return Object.assign({}, draft, {
      outlineId: outline.outlineId,
      beats: normalizeBeatRowsForRequest(draft.beats),
      expectedHash: model.packetsHash || ""
    });
  }

  function buildOutlineApproveRequest(model, outlineId) {
    const outline = findById(model && model.outlines, "outlineId", outlineId);
    if (!outline) return null;
    return {
      outlineId: outline.outlineId,
      expectedHash: model.packetsHash || ""
    };
  }

  function extractSnapshot(resourcesPayload) {
    const metadata = pick(resourcesPayload, "metadata", "Metadata") || {};
    return {
      packetsHash: metadata.storySourcePacketsHash || metadata.StorySourcePacketsHash || "",
      packets: asArray(parseSnapshot(metadata.storySourcePacketSnapshotJson || metadata.StorySourcePacketSnapshotJson || "").packets),
      canonFacts: asArray(parseSnapshot(metadata.storySourceCanonSnapshotJson || metadata.StorySourceCanonSnapshotJson || "").facts),
      outlines: asArray(parseSnapshot(metadata.storySourceOutlineSnapshotJson || metadata.StorySourceOutlineSnapshotJson || "").outlines)
    };
  }

  function parseSnapshot(raw) {
    if (!raw) return {};
    try {
      return JSON.parse(raw);
    } catch {
      return {};
    }
  }

  function normalizeCanonFacts(rawFacts, storyItems) {
    const itemById = new Map(storyItems.map(item => [metadata(item, "canonId") || item.displayName, item]));
    return asArray(rawFacts).map((fact, index) => {
      const canonId = fact.canonId || `canon.${index}`;
      return {
        key: `canon:${canonId}`,
        kind: "canon",
        canonId,
        type: fact.type || "world",
        title: fact.title || "",
        summary: fact.summary || "",
        status: fact.status || "draft",
        tags: asArray(fact.tags),
        relations: asArray(fact.relations),
        usedByPacketIds: asArray(fact.usedByPacketIds),
        usedByOutlineIds: asArray(fact.usedByOutlineIds),
        diagnostics: asArray(fact.diagnostics),
        item: itemById.get(canonId)
      };
    });
  }

  function normalizeOutlines(rawOutlines, storyItems) {
    const itemById = new Map(storyItems.map(item => [metadata(item, "outlineId") || item.displayName, item]));
    return asArray(rawOutlines).map((outline, index) => {
      const outlineId = outline.outlineId || `outline.${index}`;
      return {
        key: `outline:${outlineId}`,
        kind: "outline",
        outlineId,
        title: outline.title || "",
        stage: outline.stage || "draft",
        outlinePath: outline.outlinePath || "",
        outlineExists: Boolean(outline.outlineExists),
        canonRefs: asArray(outline.canonRefs),
        beats: asArray(outline.beats),
        conflicts: asArray(outline.conflicts),
        openQuestions: asArray(outline.openQuestions),
        targetConsumers: asArray(outline.targetConsumers),
        diagnostics: asArray(outline.diagnostics),
        item: itemById.get(outlineId)
      };
    });
  }

  function normalizePackets(rawPackets, storyItems) {
    const itemById = new Map(storyItems.map(item => [metadata(item, "storySourceId") || item.displayName, item]));
    return asArray(rawPackets).map((packet, index) => {
      const storySourceId = packet.storySourceId || `packet.${index}`;
      return {
        key: `story:${storySourceId}`,
        kind: "packet",
        storySourceId,
        version: packet.version || "",
        approvalStatus: packet.approvalStatus || "draft",
        targetConsumers: asArray(packet.targetConsumers),
        canonRefs: asArray(packet.canonRefs),
        outlineRefs: asArray(packet.outlineRefs),
        reviewStatus: packet.reviewStatus || "",
        packetPath: packet.packetPath || "",
        exportPath: packet.exportPath || "",
        artSourceTaskId: packet.artSourceTaskId || "",
        notes: packet.notes || "",
        schema: packet.schema || "",
        worldPremise: packet.worldPremise || "",
        coreEvent: packet.coreEvent || "",
        locationFunction: packet.locationFunction || "",
        civilizationOrigin: packet.civilizationOrigin || "",
        playerRole: packet.playerRole || "",
        enemyOrigins: asArray(packet.enemyOrigins),
        powerRules: asArray(packet.powerRules),
        symbolMeanings: asArray(packet.symbolMeanings),
        toneTags: asArray(packet.toneTags),
        visualDoNotUse: asArray(packet.visualDoNotUse),
        missingRequiredFields: asArray(packet.missingRequiredFields),
        diagnostics: asArray(packet.diagnostics),
        item: itemById.get(storySourceId)
      };
    });
  }

  function createDraftForCanon(fact, existing) {
    return Object.assign({
      type: fact.type || "world",
      title: fact.title || "",
      summary: fact.summary || "",
      status: fact.status || "draft",
      tags: fact.tags || [],
      relations: fact.relations || [],
      usedByPacketIds: fact.usedByPacketIds || [],
      usedByOutlineIds: fact.usedByOutlineIds || []
    }, existing || {});
  }

  function createDraftForOutline(outline, existing) {
    return Object.assign({
      title: outline.title || "",
      stage: outline.stage || "draft",
      outlinePath: outline.outlinePath || "",
      canonRefs: outline.canonRefs || [],
      beats: outline.beats || [],
      conflicts: outline.conflicts || [],
      openQuestions: outline.openQuestions || [],
      targetConsumers: outline.targetConsumers || []
    }, existing || {});
  }

  function createDraftForPacket(packet, existing) {
    return Object.assign({
      version: packet.version || "",
      approvalStatus: packet.approvalStatus || "draft",
      targetConsumers: packet.targetConsumers || [],
      canonRefs: packet.canonRefs || [],
      outlineRefs: packet.outlineRefs || [],
      reviewStatus: packet.reviewStatus || "",
      exportPath: packet.exportPath || "",
      worldPremise: packet.worldPremise || "",
      coreEvent: packet.coreEvent || "",
      locationFunction: packet.locationFunction || "",
      civilizationOrigin: packet.civilizationOrigin || "",
      playerRole: packet.playerRole || "",
      enemyOrigins: packet.enemyOrigins || [],
      powerRules: packet.powerRules || [],
      symbolMeanings: packet.symbolMeanings || [],
      toneTags: packet.toneTags || [],
      visualDoNotUse: packet.visualDoNotUse || []
    }, existing || {});
  }

  function applyLinkHighlights(canonFacts, outlines, selectedOutline, selectedPacket) {
    const canonRefs = new Set([
      ...asArray(selectedOutline && selectedOutline.draft && selectedOutline.draft.canonRefs),
      ...asArray(selectedPacket && selectedPacket.draft && selectedPacket.draft.canonRefs)
    ]);
    const outlineRefs = new Set(asArray(selectedPacket && selectedPacket.draft && selectedPacket.draft.outlineRefs));
    canonFacts.forEach(fact => {
      fact.isLinked = canonRefs.has(fact.canonId);
    });
    outlines.forEach(outline => {
      outline.isLinked = outlineRefs.has(outline.outlineId);
    });
  }

  function getStoryProgress(canonFacts, outlines, packets) {
    return {
      canonReady: countCanon(canonFacts),
      canonTotal: canonFacts.length,
      outlineReady: countApprovedOutlines(outlines),
      outlineTotal: outlines.length,
      packetReady: countApproved(packets),
      packetTotal: packets.length
    };
  }

  function getNextStoryAction(progress, selectedPacket, selectedOutline) {
    if ((progress.canonReady || 0) < (progress.canonTotal || 0)) {
      return { title: "确认正史事实", detail: "先把被大纲和输入包引用的 Canon 调整为正史。" };
    }
    if ((progress.outlineReady || 0) < (progress.outlineTotal || 0)) {
      return { title: "批准故事大纲", detail: selectedOutline ? `${selectedOutline.outlineId} 需要进入输入包派生。` : "选择一个故事大纲完成审批。" };
    }
    if (selectedPacket && selectedPacket.missingNow && selectedPacket.missingNow.length) {
      return { title: "补齐输入包字段", detail: `${selectedPacket.missingNow.length} 个字段会阻塞给美术审批。` };
    }
    if ((progress.packetReady || 0) < (progress.packetTotal || 0)) {
      return { title: "批准给美术", detail: selectedPacket ? `${selectedPacket.storySourceId} 可作为 ArtSource story gate 输入。` : "选择输入包后批准给美术。" };
    }
    return { title: "等待美术生产", detail: "StorySource 已交接给 ArtSource。继续在美术状态页推进任务。" };
  }

  function getCanonReadiness(draft, diagnostics) {
    if (hasBlockingDiagnostics(diagnostics)) return { label: "有诊断", tone: "blocked" };
    if (draft && draft.status === "canon") return { label: "正史", tone: "canon" };
    return { label: "草稿", tone: "draft" };
  }

  function getOutlineReadiness(draft, issues, diagnostics) {
    if (hasBlockingDiagnostics(diagnostics) || asArray(issues).length > 0) return { label: "阻塞", tone: "blocked" };
    if (draft && draft.stage === "approved-for-packet") return { label: "可派生", tone: "approved-for-packet" };
    return { label: formatOutlineStage(draft && draft.stage), tone: draft && draft.stage };
  }

  function getPacketReadiness(draft, missing, derivation, diagnostics) {
    if (hasBlockingDiagnostics(diagnostics) || asArray(missing).length > 0 || asArray(derivation).length > 0) return { label: "阻塞", tone: "blocked" };
    if (draft && draft.approvalStatus === "approved-for-art") return { label: "给美术", tone: "approved-for-art" };
    return { label: formatApprovalStatus(draft && draft.approvalStatus), tone: draft && draft.approvalStatus };
  }

  function hasBlockingDiagnostics(diagnostics) {
    return asArray(diagnostics).some(diagnostic => {
      const severity = String(diagnostic && (diagnostic.severity || diagnostic.Severity) || "").toLowerCase();
      return severity === "warning" || severity === "error";
    });
  }

  function getMissingRequiredFields(draft) {
    return REQUIRED_FIELDS.filter(field => {
      const value = draft ? draft[field] : null;
      return Array.isArray(value) ? value.length === 0 : !String(value || "").trim();
    });
  }

  function getPacketDerivationIssues(draft, canonFacts, outlines) {
    const issues = [];
    const canonById = new Map(asArray(canonFacts).map(fact => [fact.canonId, fact]));
    const outlineById = new Map(asArray(outlines).map(outline => [outline.outlineId, outline]));
    const canonRefs = asArray(draft && draft.canonRefs);
    const outlineRefs = asArray(draft && draft.outlineRefs);
    if (!asArray(draft && draft.targetConsumers).includes("artSource")) issues.push("输入包 targetConsumers 必须包含 artSource。");
    if (canonRefs.length === 0) issues.push("输入包必须引用 Canon 世界观记忆。");
    canonRefs.forEach(id => {
      const fact = canonById.get(id);
      if (!fact) issues.push(`缺少 Canon：${id}`);
      else if (fact.status !== "canon") issues.push(`Canon 未成为正史：${id}`);
    });
    if (outlineRefs.length === 0) issues.push("输入包必须引用已批准故事大纲。");
    outlineRefs.forEach(id => {
      const outline = outlineById.get(id);
      if (!outline) issues.push(`缺少大纲：${id}`);
      else if (outline.stage !== "approved-for-packet") issues.push(`大纲未批准给输入包：${id}`);
    });
    return issues;
  }

  function getOutlineApprovalIssues(draft, canonFacts) {
    const issues = [];
    const canonById = new Map(asArray(canonFacts).map(fact => [fact.canonId, fact]));
    const refs = asArray(draft && draft.canonRefs);
    if (refs.length === 0) issues.push("大纲必须引用 Canon。");
    refs.forEach(id => {
      const fact = canonById.get(id);
      if (!fact) issues.push(`缺少 Canon：${id}`);
      else if (fact.status !== "canon") issues.push(`Canon 未成为正史：${id}`);
    });
    if (asArray(draft && draft.beats).length === 0) issues.push("大纲至少需要一个 beat。");
    return issues;
  }

  function normalizeSelectedKeys(value) {
    if (typeof value === "string") {
      return { canon: value, outline: value, packet: value };
    }
    return Object.assign({ canon: "", outline: "", packet: "" }, value || {});
  }

  function findSelectedByKey(items, selectedKey) {
    const list = asArray(items);
    return list.find(item => item.key === selectedKey) || list[0] || null;
  }

  function findById(items, idField, id) {
    return asArray(items).find(item => item[idField] === id || item.key === id);
  }

  function isDirty(base, draft) {
    return JSON.stringify(base || {}) !== JSON.stringify(draft || {});
  }

  function normalizeSaveState(saveState, id) {
    if (!saveState || (saveState.storySourceId || saveState.canonId || saveState.outlineId) !== id) {
      return { status: "idle", message: "", error: "" };
    }
    return Object.assign({ status: "idle", message: "", error: "" }, saveState);
  }

  function compareCanonFacts(left, right) {
    return (left.type || "").localeCompare(right.type || "") || (left.canonId || "").localeCompare(right.canonId || "");
  }

  function compareOutlines(left, right) {
    return (left.stage || "").localeCompare(right.stage || "") || (left.outlineId || "").localeCompare(right.outlineId || "");
  }

  function comparePackets(left, right) {
    return (left.approvalStatus || "").localeCompare(right.approvalStatus || "") || (left.storySourceId || "").localeCompare(right.storySourceId || "");
  }

  function countApproved(packets) {
    return asArray(packets).filter(packet => packet.approvalStatus === "approved-for-art").length;
  }

  function countCanon(facts) {
    return asArray(facts).filter(fact => fact.status === "canon").length;
  }

  function countApprovedOutlines(outlines) {
    return asArray(outlines).filter(outline => outline.stage === "approved-for-packet").length;
  }

  function renderSaveMessage(saveState) {
    if (!saveState) return "";
    return `${saveState.message ? `<p class="story-save-message">${escapeHtml(saveState.message)}</p>` : ""}${saveState.error ? `<p class="story-error">${escapeHtml(saveState.error)}</p>` : ""}`;
  }

  function splitLines(value) {
    return String(value || "").split(/\r?\n/).map(item => item.trim()).filter(Boolean).filter((item, index, array) => array.indexOf(item) === index);
  }

  function parseRelations(value) {
    return splitLines(value).map(line => {
      const parts = line.split("|").map(part => part.trim());
      return { relation: parts[0] || "", targetId: parts[1] || "" };
    }).filter(item => item.relation && item.targetId);
  }

  function parseBeats(value) {
    return splitLines(value).map(line => {
      const parts = line.split("|").map(part => part.trim());
      return { slug: parts[0] || "", title: parts[1] || "", summary: parts.slice(2).join(" | ") || "" };
    }).filter(item => item.slug);
  }

  function normalizeBeatRowsForRequest(beats) {
    return asArray(beats).map(beat => ({
      slug: String(beat && beat.slug || "").trim(),
      title: String(beat && beat.title || "").trim(),
      summary: String(beat && beat.summary || "").trim()
    })).filter(beat => beat.slug || beat.title || beat.summary);
  }

  function formatRelations(relations) {
    return asArray(relations).map(item => `${item.relation || ""} | ${item.targetId || ""}`).join("\n");
  }

  function formatSeverityTone(value) {
    const severity = String(value || "").toLowerCase();
    if (severity === "error") return "error";
    if (severity === "warning") return "warn";
    return "info";
  }

  function formatApprovalStatus(value) {
    if (value === "approved-for-art") return "已批准给美术";
    if (value === "superseded") return "已废弃";
    if (value === "rejected") return "退回修改";
    return "草稿";
  }

  function formatCanonStatus(value) {
    if (value === "canon") return "正史";
    if (value === "deprecated") return "已废弃";
    if (value === "rejected") return "已拒绝";
    return "草稿";
  }

  function formatOutlineStage(value) {
    if (value === "approved-for-packet") return "已批准给输入包";
    if (value === "review") return "评审中";
    if (value === "superseded") return "已废弃";
    return "草稿";
  }

  function formatCanonType(value) {
    const labels = {
      world: "世界",
      location: "地点",
      faction: "势力",
      character_archetype: "角色类别",
      enemy_origin: "敌人来源",
      power_rule: "力量规则",
      symbol: "符号",
      tone: "情绪",
      forbidden_content: "禁用设定"
    };
    return labels[value] || value || "未分类";
  }

  function formatTypeSummary(facts) {
    const count = new Set(asArray(facts).map(fact => fact.type).filter(Boolean)).size;
    return `${count} 类`;
  }

  function formatStatusSummary(facts) {
    const draft = asArray(facts).filter(fact => fact.status !== "canon").length;
    return draft > 0 ? `${draft} 条待批准` : "全部正史";
  }

  function normalizeTone(tone) {
    const value = String(tone || "").toLowerCase();
    if (value === "canon" || value === "approved-for-art" || value === "approved-for-packet" || value === "synced") return "ok";
    if (value === "blocked" || value === "rejected") return "blocked";
    if (value === "dirty" || value === "review") return "warn";
    return "neutral";
  }

  function metadata(item, key) {
    const data = (item && item.metadata) || {};
    return data[key] || data[key && key[0] ? key[0].toUpperCase() + key.slice(1) : key] || "";
  }

  function pick(value, ...keys) {
    if (!value) return null;
    for (const key of keys) {
      if (Object.prototype.hasOwnProperty.call(value, key)) return value[key];
    }
    return null;
  }

  function asArray(value) {
    return Array.isArray(value) ? value : [];
  }

  function emptyBlock(text) {
    return `<div class="empty-block">${escapeHtml(text || "")}</div>`;
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  global.MxResourceStorySourceStatus = {
    createModel,
    render,
    updateDraftField,
    updateDraftArrayField,
    updateCanonDraftField,
    updateCanonDraftArrayField,
    updateCanonDraftRelations,
    updateOutlineDraftField,
    updateOutlineDraftArrayField,
    updateOutlineDraftBeats,
    updateOutlineDraftBeatField,
    addOutlineDraftBeat,
    removeOutlineDraftBeat,
    clearDraft,
    buildUpdateRequest,
    buildApproveRequest,
    buildCanonUpdateRequest,
    buildOutlineUpdateRequest,
    buildOutlineApproveRequest
  };
})(window);
