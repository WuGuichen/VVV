// Resource Library split module.
// Refs review workspace, bundle recommendations, draft apply, and undo.
// Loaded by index.html as a classic script; keep shared bindings global.
function renderReferencesWorkspace() {
  const model = getReferenceReviewModel();

  for (const button of el.referencesFilterButtons) {
    button.classList.toggle("active", button.dataset.refsFilter === state.referencesFilter);
  }

  if (el.referencesScopeSearch && el.referencesScopeSearch.value !== state.referencesSearch) {
    el.referencesScopeSearch.value = state.referencesSearch;
  }

  el.referencesReviewBar.innerHTML = renderReferenceReviewBar(model);
  el.referencesSummary.textContent = `${model.scopeLabel} · ${model.targets.length} 个资源`;
  el.referencesScopeHint.textContent = model.scopeHint;
  if (el.referencesScopeExplanation) {
    el.referencesScopeExplanation.innerHTML = renderReferencesScopeExplanation(model);
  }
  el.referencesGroupSummary.textContent = `${model.groupModels.length} 个引用者配置 · ${model.coReferencedCount} 个同一配置还会加载的资源`;
  el.referencesAdviceSummary.textContent = model.buckets.blocking.length > 0
    ? `${model.buckets.blocking.length} 个必须处理，${model.buckets.suggested.length} 个建议处理，${model.autoApplyCount} 个可自动应用`
    : model.buckets.suggested.length > 0
      ? `${model.buckets.suggested.length} 个建议处理，${model.autoApplyCount} 个可自动应用`
      : "当前没有阻塞建议";

  el.referencesTargetList.innerHTML = renderReferencesTargetList(model.targets);
  el.referencesContent.innerHTML = renderReferenceReviewGroups(model);
  el.referencesAdviceContent.innerHTML = renderReferenceRecommendationQueue(model);
}

function getReferenceReviewModel() {
  const targets = getReferencesWorkspaceTargetItems();
  const referenceRows = targets.flatMap(item => getReferenceRowsForItem(item));
  const groupModels = getReferenceGroupModels(referenceRows);
  const coReferencedCount = uniqueItems(groupModels.flatMap(group => group.displayRows.map(row => row.targetItem).filter(Boolean))).length;
  const recommendations = getBundleRecommendationRows(targets);
  const buckets = getReferenceIssueBuckets(recommendations);
  const visibleRecommendations = getVisibleReferenceRecommendations(recommendations);
  const scopeLabel = formatReferenceScopeLabel();
  const model = {
    targets,
    referenceRows,
    groupModels,
    coReferencedCount,
    recommendations,
    buckets,
    visibleRecommendations,
    autoApplyCount: visibleRecommendations.length,
    scopeLabel,
    scopeHint: formatReferenceScopeHint(targets),
    dirtyLabel: state.buildProfileDirty ? "Profile 草稿未保存" : "Profile 已保存",
    hasUndo: Boolean(state.referencesLastPatch && asArray(state.referencesLastPatch.entries).length > 0)
  };
  model.education = getReferenceEducationState(model);
  return model;
}

function renderReferenceReviewBar(model) {
  const canApplyVisible = model.autoApplyCount > 0;
  const education = model.education || getReferenceEducationState(model);
  return `
    <div class="refs-review-title${education.visible ? "" : " compact"}">
      <span>Refs 审查工作台</span>
      <strong>本页能回答什么</strong>
      <p>先看引用和 Bundle 结论；需要工程字段时再展开详情。</p>
      <small>当前范围：${escapeHtml(model.scopeLabel)}</small>
      ${renderReferencesIntro(model)}
    </div>
    <div class="refs-review-metrics">
      ${renderReferenceMetric("直接引用", model.referenceRows.length, "有多少配置直接用到当前检查资源。")}
      ${renderReferenceMetric("同组资源", model.coReferencedCount, "同一配置还会一起加载的资源，用来判断 Bundle 是否应该放在一起。")}
      ${renderReferenceMetric("必须处理", model.buckets.blocking.length, "构建或运行时可能缺资源，保存前应先处理。")}
      ${renderReferenceMetric("建议处理", model.buckets.suggested.length, "不是立刻阻塞，但会增加加载顺序或依赖风险。")}
      ${renderReferenceMetric("可应用", model.autoApplyCount, "可以一键写入 Profile 草稿，但不会自动保存。")}
      ${renderReferenceMetric("Profile", model.dirtyLabel, "草稿未保存时，需要去 Profile 显式保存才会落盘。")}
    </div>
    <div class="refs-review-actions">
      <button type="button" data-reference-action="apply-visible" class="primary-action"${canApplyVisible ? "" : " disabled"} title="${canApplyVisible ? "只应用当前筛选可见的必须处理/建议处理项，写入 Profile 草稿。" : "当前没有可自动应用的可见建议。"}">应用可见建议</button>
      <button type="button" data-reference-action="undo-last" class="secondary"${model.hasUndo ? "" : " disabled"} title="${model.hasUndo ? "撤销最近一次 Refs 建议应用。" : "还没有可撤销的 Refs 应用。"}">撤销本轮应用</button>
      <button type="button" data-reference-action="open-profile-save" class="secondary"${state.buildProfileDirty ? "" : " disabled"} title="${state.buildProfileDirty ? "进入 Profile 工作区保存草稿。" : "当前没有未保存 Profile 草稿。"}">去 Profile 保存</button>
      <button type="button" data-reference-action="open-graph" class="secondary">打开关系图</button>
      <button type="button" data-reference-action="toggle-education" class="secondary">${education.visible ? "隐藏说明" : "显示说明"}</button>
      <small>批量动作只改当前可见建议的 Profile 草稿，保存 Profile 前不会落盘。</small>
    </div>`;
}

function getReferenceEducationState(model = {}) {
  return {
    visible: state.referencesEducationVisible !== false,
    dirty: state.buildProfileDirty,
    cards: [
      {
        title: "谁在引用当前资源",
        text: "看中间的引用者配置卡，确认资源被哪个角色、UI 或运行时配置使用。"
      },
      {
        title: "同组资源 Bundle 是否合理",
        text: "看同一配置还会加载的资源，required 资源通常应该有清晰的 Profile 和 Bundle 归属。"
      },
      {
        title: "哪些建议可以安全写入草稿",
        text: "看右侧可执行清单。按钮只写前端草稿，不会保存 Profile，也不会构建 AssetBundle。"
      }
    ],
    scopeText: model.scopeHint || "这里决定本次检查哪些资源，不会修改任何配置。"
  };
}

function renderReferencesIntro(model) {
  const education = model.education || getReferenceEducationState(model);
  if (!education.visible) return "";
  return `
    <div class="refs-intro-grid">
      ${education.cards.map(card => `
        <div>
          <strong>${escapeHtml(card.title)}</strong>
          <span>${escapeHtml(card.text)}</span>
        </div>`).join("")}
    </div>`;
}

function renderReferenceMetric(label, value, help) {
  const helpText = state.referencesEducationVisible === false ? "" : `<small>${escapeHtml(help)}</small>`;
  return `<div class="metric references-metric"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong>${helpText}</div>`;
}

function renderReferencesScopeExplanation(model) {
  const education = model.education || getReferenceEducationState(model);
  if (!education.visible) return "";
  const dirty = state.buildProfileDirty
    ? `<div class="references-draft-callout"><strong>Profile 草稿未保存</strong><span>Refs 的建议只写草稿。确认后请点“去 Profile 保存”。</span><button type="button" data-reference-action="open-profile-save">去 Profile 保存</button></div>`
    : "";
  return `
    <div class="refs-inline-help">
      <strong>审查范围决定本次看哪些资源</strong>
      <span>这里的搜索和筛选只影响本页显示、建议和“应用可见建议”，不会修改任何配置。</span>
      <em>${escapeHtml(education.scopeText)}</em>
    </div>
    ${dirty}`;
}

function getReferencesWorkspaceTargetItems() {
  const selected = getSelectedItem();
  let targets = [];
  if (state.referencesFilter === "current") targets = selected ? [selected] : [];
  if (state.referencesFilter === "checked") {
    const checked = getCheckedItems();
    targets = checked.length > 0 ? checked : selected ? [selected] : [];
  }
  if (targets.length === 0 && state.referencesFilter !== "current" && state.referencesFilter !== "checked") {
    const items = getReferencesScopeItems();
    if (state.referencesFilter === "required") {
      targets = items.filter(item => getReferenceRowsForItem(item).some(row => row.required));
    } else if (state.referencesFilter === "missingProfile") {
      targets = items.filter(item => getBundleRecommendationRows([item]).some(row => row.type === "missingProfile"));
    } else if (state.referencesFilter === "splitBundle") {
      targets = items.filter(item => getBundleRecommendationRows([item]).some(row => row.type === "splitBundle"));
    } else if (state.referencesFilter === "issues") {
      targets = items.filter(item => getBundleRecommendationRows([item]).some(row => row.severity === "blocking" || row.severity === "suggested"));
    }
  }
  return filterReferencesTargetsBySearch(targets);
}

function getReferencesScopeItems() {
  const items = getBrowserItems();
  if (!state.referencesBundleRuleId) return items;
  return items.filter(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule") === state.referencesBundleRuleId);
}

function formatReferencesFilterLabel(filter) {
  const labels = {
    current: "当前资源",
    checked: "已勾选资源",
    issues: "有引用问题",
    missingProfile: "缺 Profile",
    splitBundle: "Bundle 不一致",
    required: "必需引用"
  };
  return labels[filter] || "当前资源";
}

function formatReferenceScopeLabel() {
  const bundle = state.referencesBundleRuleId ? ` · Bundle ${state.referencesBundleRuleId}` : "";
  const search = state.referencesSearch.trim() ? ` · 搜索 "${state.referencesSearch.trim()}"` : "";
  return `${formatReferencesFilterLabel(state.referencesFilter)}${bundle}${search}`;
}

function formatReferenceScopeHint(targets) {
  if (state.referencesBundleRuleId) {
    return `当前只检查 Bundle ${state.referencesBundleRuleId} 的成员；从顶栏进入 Refs 可回到全部范围。`;
  }
  if (state.referencesFilter === "checked") return `当前检查 ${targets.length} 个已勾选资源；未勾选时回退到当前资源。`;
  if (state.referencesFilter === "current") return targets.length > 0 ? "当前只检查选中资源及其同组引用。" : "请先在 Browse 中选择一个资源。";
  return "当前检查全局资源范围，筛选只影响本页可见建议和批量应用。";
}

function filterReferencesTargetsBySearch(targets) {
  const query = state.referencesSearch.trim().toLowerCase();
  if (!query) return targets;
  return targets.filter(item => itemMatchesReferenceSearch(item, query));
}

function itemMatchesReferenceSearch(item, query) {
  const entry = findDraftBuildProfileEntryForItem(item);
  const values = [
    formatResourceDisplayName(item),
    item?.key,
    item?.resourceKey,
    item?.runtimeResourceKey,
    item?.providerResourceKey,
    item?.packageResourceKey,
    item?.kind,
    item?.usage,
    item?.sourcePath,
    item?.unityAssetPath,
    formatProfileStatusLabel(getBuildProfileStatus(item)),
    pick(entry, "bundleRule"),
    pick(entry, "deliveryMode")
  ];
  return values.some(value => stringValue(value).toLowerCase().includes(query));
}

function renderReferencesEmptyState(title, body, actionLabel = "", action = "") {
  return `
    <div class="empty references-empty-state">
      <strong>${escapeHtml(title)}</strong>
      <span>${escapeHtml(body)}</span>
      ${actionLabel && action ? `<button type="button" data-reference-action="${escapeHtml(action)}">${escapeHtml(actionLabel)}</button>` : ""}
    </div>`;
}

function renderReferencesTargetList(targets) {
  if (targets.length === 0) {
    if (state.referencesFilter === "current") {
      return renderReferencesEmptyState("先去 Browse 选择资源", "选择一个资源后，这里会显示它被哪些配置引用，以及 Bundle 分配是否需要调整。", "去 Browse 选择", "open-browse");
    }
    if (state.referencesBundleRuleId) {
      return renderReferencesEmptyState(`Bundle ${state.referencesBundleRuleId} 下没有匹配资源`, "可以清空搜索、切换审查范围，或从顶栏进入 Refs 回到全部范围。");
    }
    return renderReferencesEmptyState("当前筛选没有匹配资源", "清空搜索或切换审查范围，可以扩大本次检查。");
  }
  return targets.map(item => {
    const active = selectionMatchesItem(item, state.selectedResourceKey);
    const directReferences = getReferenceRowsForItem(item);
    const recommendations = getBundleRecommendationRows([item]);
    const blocking = recommendations.filter(row => row.severity === "blocking").length;
    const suggested = recommendations.filter(row => row.severity === "suggested").length;
    const entry = findDraftBuildProfileEntryForItem(item);
    const deliveryState = getDeliveryEntryState(item, entry);
    return `
      <button type="button" class="references-target-row${active ? " active" : ""}" data-resource-key="${escapeHtml(item.key)}">
        <strong>${escapeHtml(formatResourceDisplayName(item))}</strong>
        <span>${escapeHtml(item.kind || "-")} · ${escapeHtml(formatProfileStatusLabel(getBuildProfileStatus(item)))} · ${escapeHtml(deliveryState.label)}</span>
        <small>${escapeHtml(getReferenceTargetReason(item, { blocking, suggested, directReferences }))}</small>
        <div>
          ${smallBadge(`引用 ${directReferences.length}`, directReferences.length > 0 ? "info" : "muted")}
          ${smallBadge(`必须 ${blocking}`, blocking > 0 ? "error" : "muted")}
          ${smallBadge(`建议 ${suggested}`, suggested > 0 ? "warn" : "muted")}
        </div>
      </button>`;
  }).join("");
}

function getReferenceTargetReason(item, context = {}) {
  if (context.blocking > 0) return "为什么在这里：有必须处理项，可能影响构建或运行时加载。";
  if (context.suggested > 0) return "为什么在这里：有建议处理项，Bundle 分配可以更稳。";
  if (state.referencesBundleRuleId) return `为什么在这里：属于当前 Bundle ${state.referencesBundleRuleId} 的检查范围。`;
  if (state.referencesFilter === "current") return "为什么在这里：这是 Browse 当前选中的资源。";
  if (state.referencesFilter === "checked") return "为什么在这里：来自 Browse 勾选的批量检查范围。";
  if (state.referencesFilter === "missingProfile") return "为什么在这里：运行时必需引用还没有加入 Profile。";
  if (state.referencesFilter === "splitBundle") return "为什么在这里：同一配置的必需资源分散在多个 Bundle。";
  if (state.referencesFilter === "required") return "为什么在这里：至少有一个运行时必需引用。";
  if (state.referencesFilter === "issues") return "为什么在这里：当前筛选只显示需要处理的资源。";
  if (asArray(context.directReferences).length > 0) return "为什么在这里：Authoring 引用图发现它被配置使用。";
  return "为什么在这里：当前范围内的资源。";
}

function renderReferenceReviewGroups(model) {
  if (model.referenceRows.length === 0) {
    return renderReferencesEmptyState("当前范围没有引用记录", "Authoring 引用图没有发现其它配置引用这些资源。可以切换到“运行时必需”或清空搜索扩大范围。", "去 Browse 选择", "open-browse");
  }
  return model.groupModels.map(group => {
    return `
      <article class="reference-group-card">
        <div class="reference-group-head">
          <div>
            <span>引用者配置</span>
            <strong>${escapeHtml(formatReferenceUserLabel(group.sourceKind, "sourceConfig"))}</strong>
            <small>${escapeHtml(group.sourceStableId ? `配置 ID：${group.sourceStableId}` : "配置 ID：未提供")}</small>
            <p>这个配置会在运行时或预加载阶段用到下面这些资源；required 资源的 Profile 和 Bundle 归属需要先看清楚。</p>
          </div>
          <div class="reference-group-badges">
            ${smallBadge(`当前检查资源 ${group.directCount}`, "info")}
            ${smallBadge(`同一配置还会加载 ${group.displayRows.length}`, group.displayRows.length > 0 ? "ok" : "muted")}
            ${smallBadge(`必需 ${group.requiredCount}`, group.requiredCount > 0 ? "warn" : "muted")}
            ${smallBadge(`可选 ${group.optionalCount}`, group.optionalCount > 0 ? "muted" : "muted")}
          </div>
        </div>
        <div class="reference-group-summary">
          <div>
            <span>加载策略</span>
            <strong>${escapeHtml(group.preloadPolicies.map(policy => formatReferenceUserLabel(policy, "preloadPolicy")).join(" / ") || "-")}</strong>
          </div>
          <div>
            <span>当前检查资源</span>
            <strong>${escapeHtml(formatReferenceItemList(group.directItems))}</strong>
          </div>
          <div>
            <span>同一配置还会加载</span>
            <strong>${escapeHtml(formatReferenceItemList(group.coItems))}</strong>
          </div>
        </div>
        <div class="reference-edge-list">
          ${group.displayRows.map(row => renderReferenceEdgeRow(row, { direct: group.directRowKeys.has(getReferenceRowIdentity(row)) })).join("")}
        </div>
      </article>`;
  }).join("");
}

function getReferenceGroupModels(referenceRows) {
  return groupReferenceRows(referenceRows).map(group => {
    const coRows = getCoReferencedResourceRows(group.rows);
    const displayRows = coRows.length > 0 ? coRows : group.rows;
    const directRowKeys = new Set(group.rows.map(getReferenceRowIdentity));
    const directItems = uniqueItems(group.rows.map(row => row.targetItem).filter(Boolean));
    const coItems = uniqueItems(displayRows.map(row => row.targetItem).filter(Boolean));
    return {
      sourceKey: group.sourceKey,
      sourceKind: group.label,
      sourceStableId: group.sourceStableId,
      directCount: group.rows.length,
      displayRows,
      directRowKeys,
      directItems,
      coItems,
      requiredCount: displayRows.filter(row => row.required).length,
      optionalCount: displayRows.filter(row => !row.required).length,
      preloadPolicies: uniqueStrings(displayRows.map(row => row.preloadPolicy || "-"))
    };
  });
}

function renderReferenceEdgeRow(row, options = {}) {
  const item = row.targetItem;
  const entry = item ? findDraftBuildProfileEntryForItem(item) : null;
  const deliveryState = item ? getDeliveryEntryState(item, entry) : { label: "无法映射", tone: "warn" };
  const bundle = entry ? pick(entry, "bundleRule") || "-" : "-";
  const profile = item ? formatProfileStatusLabel(getBuildProfileStatus(item)) : "无法映射到资源库项";
  return `
    <div class="reference-edge-row${row.required ? " required" : ""}">
      <div class="reference-edge-main">
        <strong>${escapeHtml(item ? formatResourceDisplayName(item) : row.targetLabel)}</strong>
        <small>${escapeHtml(row.targetResourceKey || row.targetStableId || "-")}</small>
        ${renderReferenceStatusMeaning(row, { item, entry, deliveryState, bundle, profile })}
      </div>
      <div class="reference-edge-badges">
        ${smallBadge(options.direct ? "当前检查资源" : "同一配置还会加载", options.direct ? "info" : "muted")}
        ${smallBadge(row.required ? "必需" : "可选", row.required ? "warn" : "muted")}
        ${smallBadge(formatReferenceUserLabel(row.preloadPolicy || "无 preload", "preloadPolicy"), row.required ? "warn" : "muted")}
        ${smallBadge(profile, toneForBuildProfileStatus(item ? getBuildProfileStatus(item) : "notInProfile"))}
        ${smallBadge(deliveryState.label, deliveryState.tone)}
      </div>
      <div class="reference-edge-bundle">
        <span>Bundle</span>
        <strong>${escapeHtml(bundle)}</strong>
      </div>
      <details class="reference-tech-details">
        <summary>工程详情</summary>
        ${renderKeyValueList([
          ["sourceStableId", row.sourceStableId || "-"],
          ["sourceField", row.sourceField || "-"],
          ["sourceConsumerKind", row.sourceConsumerKind || "-"],
          ["bindingKind", row.bindingKind || "-"],
          ["targetStableId", row.targetStableId || "-"],
          ["targetResourceId", row.targetResourceId || "-"]
        ])}
      </details>
    </div>`;
}

function formatReferenceUserLabel(value, kind = "") {
  const text = stringValue(value) || "-";
  if (kind === "sourceConfig") {
    const map = {
      character: "角色配置",
      characterPackage: "角色包配置",
      resourcePlan: "资源计划配置",
      resourceMapping: "生产资源映射",
      unknown: "未知引用配置"
    };
    return map[text] || text;
  }
  if (kind === "preloadPolicy") {
    const normalized = text.toLowerCase();
    if (!text || text === "-" || normalized === "none" || text === "无 preload") return "不预加载";
    if (normalized.includes("warm")) return `${text}（预热加载）`;
    if (normalized.includes("spawn")) return `${text}（生成前需要）`;
    if (normalized.includes("defer")) return `${text}（延后加载）`;
  }
  return text;
}

function renderReferenceStatusMeaning(row, context = {}) {
  if (state.referencesEducationVisible === false) return "";
  const item = context.item || row.targetItem;
  if (!item) {
    return `<p class="reference-status-meaning">为什么要关注：引用图里有目标，但资源库没有对应项，不能自动写入 Profile 草稿。</p>`;
  }
  const assignment = getBundleAssignmentState(item);
  const reasons = [];
  reasons.push(row.required
    ? "运行时必需：缺失时可能导致生成、显示或预加载失败。"
    : "可选引用：通常允许降级，但仍要确认是否符合预期。");
  if (!assignment.entry) {
    reasons.push("还未加入 Profile，因此不会进入 Bundle Plan。");
  } else if (assignment.invalidInternalBundle) {
    reasons.push(assignment.missingBundleRule ? "Profile 指向的 Bundle 规则不存在。" : "已加入 Profile，但 internal 资源还没有 Bundle。");
  } else if (assignment.bundleRule) {
    reasons.push(`当前会进入 Bundle ${assignment.bundleRule}。`);
  } else if (["external", "editorOnly", "excluded"].includes(assignment.deliveryMode)) {
    reasons.push(`当前是 ${assignment.deliveryMode}，不会进入本地 AssetBundle。`);
  } else {
    reasons.push("当前 Profile 状态可接受。");
  }
  return `<p class="reference-status-meaning">为什么要关注：${escapeHtml(reasons.join(" "))}</p>`;
}

function renderReferenceRecommendationQueue(model) {
  const rows = asArray(model.recommendations);
  if (rows.length === 0) {
    return renderReferencesEmptyState("当前范围没有可显示的 Bundle 分配意见", "没有发现需要自动写入 Profile 草稿的建议。可以继续去 Build 检查产物状态。", "去 Build 检查", "open-build");
  }
  const groups = [
    ["blocking", "必须处理"],
    ["suggested", "建议处理"],
    ["accepted", "可接受"],
    ["info", "信息"]
  ];
  const clearState = model.buckets.blocking.length === 0 && model.buckets.suggested.length === 0
    ? `<div class="references-ok-callout"><strong>当前范围没有必须处理项</strong><span>Profile / Bundle 状态没有发现阻塞建议。可以继续去 Build 检查 Bundle Plan 和生成产物。</span><button type="button" data-reference-action="open-build">去 Build 检查</button></div>`
    : "";
  return clearState + groups.map(([severity, label]) => {
    const groupRows = rows.filter(row => row.severity === severity);
    if (groupRows.length === 0) return "";
    return `
      <section class="recommendation-group ${severity}">
        <h3>${escapeHtml(label)} <span>${groupRows.length}</span></h3>
        <p class="recommendation-group-help">${escapeHtml(getRecommendationGroupExplanation(severity))}</p>
        ${groupRows.map(renderBundleRecommendationRow).join("")}
      </section>`;
  }).join("");
}

function renderBundleRecommendationRow(row) {
  const canApply = Boolean(row.canApply && row.recommendedBundleId);
  const disabledReason = getRecommendationDisabledReason(row);
  return `
    <article class="recommendation-row ${escapeHtml(row.severity)}">
      <div>
        <strong>${escapeHtml(row.title)}</strong>
        <p>${escapeHtml(row.message)}</p>
        ${row.detail ? `<small>${escapeHtml(row.detail)}</small>` : ""}
        <div class="recommendation-explain-grid">
          <div>
            <span>问题原因</span>
            <strong>${escapeHtml(getRecommendationReasonText(row))}</strong>
          </div>
          <div>
            <span>影响资源</span>
            <strong>${escapeHtml(formatRecommendationAffectedItems(row))}</strong>
          </div>
          <div>
            <span>当前分配</span>
            <strong>${escapeHtml(formatRecommendationCurrentBundle(row))}</strong>
          </div>
          <div>
            <span>推荐分配</span>
            <strong>${escapeHtml(row.recommendedBundleId || "无推荐 Bundle")}</strong>
          </div>
        </div>
        ${renderRecommendationSafetyText(row, canApply, disabledReason)}
      </div>
      <div class="recommendation-row-actions">
        <button type="button" data-recommendation-action="apply" data-recommendation-id="${escapeHtml(row.id)}"${canApply ? "" : " disabled"} title="${escapeHtml(row.applyHint || "当前建议不能自动应用。")}">
          ${escapeHtml(row.actionLabel || "写入推荐 Bundle 草稿")}
        </button>
        <small>${escapeHtml(canApply ? "只写 Profile 草稿，保存前不会落盘" : disabledReason)}</small>
      </div>
    </article>`;
}

function getRecommendationGroupExplanation(severity) {
  const explanations = {
    blocking: "这些项可能导致构建计划缺资源或运行时加载失败，保存 Profile 前优先处理。",
    suggested: "这些项不一定立刻阻塞，但同一配置的必需资源分散时，后续加载顺序更容易出问题。",
    accepted: "这些项当前判断为可接受，展示出来是为了让你确认为什么不需要自动修复。",
    info: "这些项只是背景信息或无法自动映射，通常不生成草稿动作。"
  };
  return explanations[severity] || "按当前引用和 Profile 状态给出的说明。";
}

function getRecommendationReasonText(row) {
  const reasons = {
    missingProfile: "运行时必需资源还没有加入 Profile。",
    missingBundle: "Profile 指向的 Bundle 规则不存在。",
    unassignedBundle: "internal 运行时资源还没有 Bundle。",
    splitBundle: "同一引用者配置的必需资源分散在多个 Bundle。",
    validBundle: "Profile 和 Bundle 归属可接受。",
    externalConfig: "资源被配置引用，但当前不会进入本地 AssetBundle。",
    noReferences: "当前引用图没有发现其它配置引用它。",
    unmapped: "引用目标无法映射到资源库项。"
  };
  return reasons[row.type] || row.title || "根据引用关系生成的 Bundle 建议。";
}

function formatRecommendationAffectedItems(row) {
  const items = uniqueItems(row.targetItems);
  if (items.length === 0) return "无可自动处理资源";
  const names = items.map(formatResourceDisplayName).filter(Boolean);
  return names.length <= 2 ? names.join(", ") : `${names.slice(0, 2).join(", ")} +${names.length - 2}`;
}

function renderRecommendationSafetyText(row, canApply, disabledReason) {
  if (state.referencesEducationVisible === false) return "";
  const text = canApply
    ? "安全性：只会把这条建议涉及的资源写入前端 Profile 草稿；不会保存 Profile、不会构建 AssetBundle、不会写 StreamingAssets。"
    : `安全性：不会自动处理。原因：${disabledReason || "当前建议只供查看。"}`;
  return `<p class="recommendation-safety">${escapeHtml(text)}</p>`;
}

function getReferenceIssueBuckets(recommendations) {
  const buckets = { blocking: [], suggested: [], accepted: [], info: [] };
  for (const row of asArray(recommendations)) {
    const key = buckets[row.severity] ? row.severity : "info";
    buckets[key].push(row);
  }
  return buckets;
}

function getVisibleReferenceRecommendations(recommendations) {
  return asArray(recommendations).filter(row =>
    (row.severity === "blocking" || row.severity === "suggested")
    && row.canApply
    && row.recommendedBundleId
  );
}

function formatReferenceItemList(items) {
  const names = uniqueItems(items).map(formatResourceDisplayName).filter(Boolean);
  if (names.length === 0) return "-";
  return names.length <= 3 ? names.join(", ") : `${names.slice(0, 3).join(", ")} +${names.length - 3}`;
}

function formatRecommendationCurrentBundle(row) {
  const bundles = uniqueStrings(uniqueItems(row.targetItems).map(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule") || "未分配"));
  if (bundles.length === 0) return "-";
  return bundles.length <= 2 ? bundles.join(", ") : `${bundles.slice(0, 2).join(", ")} +${bundles.length - 2}`;
}

function getRecommendationDisabledReason(row) {
  if (!row.recommendedBundleId) return "没有可用推荐 Bundle";
  if (row.severity === "accepted") return "当前状态可接受，不需要写入草稿";
  if (row.severity === "info") return "信息项不生成 Profile 草稿";
  if (!row.canApply) return row.applyHint || "当前建议不能自动应用";
  return "";
}

function getReferenceRowsForItem(item) {
  if (!item) return [];
  const edges = uniqueRows([
    ...getReferencesForItem(item),
    ...asArray(pick(getCurrentDetail(item), "references"))
  ]);
  return edges.map(edge => normalizeReferenceRow(edge, item));
}

function getCoReferencedResourceRows(referenceRows) {
  const rows = [];
  const seen = new Set();
  const edges = getAllReferenceEdges();
  for (const reference of asArray(referenceRows)) {
    const sourceKey = reference.sourceKey;
    if (!sourceKey) continue;
    for (const edge of edges) {
      const row = normalizeReferenceRow(edge, null);
      if (row.sourceKey !== sourceKey) continue;
      const key = `${sourceKey}::${row.targetStableId}::${row.targetResourceKey}::${row.sourceField}`;
      if (seen.has(key)) continue;
      seen.add(key);
      rows.push(row);
    }
  }
  return rows;
}

function getReferenceRowIdentity(row) {
  return [
    row.sourceKey || "",
    row.sourceField || "",
    row.targetStableId || "",
    row.targetResourceKey || "",
    row.targetResourceId || ""
  ].join("::");
}

function getBundleRecommendationRows(targets) {
  const rows = [];
  const seen = new Set();
  const targetItems = asArray(targets).filter(Boolean);
  const referenceRows = targetItems.flatMap(item => getReferenceRowsForItem(item));

  for (const item of targetItems) {
    const directRows = getReferenceRowsForItem(item);
    if (directRows.length === 0) {
      addRecommendation(rows, seen, buildInfoRecommendation(item, "noReferences", "没有引用关系", "Authoring reference graph 没有发现其它配置引用这个资源。"));
      continue;
    }
    for (const reference of directRows) {
      const assignment = getBundleAssignmentState(item);
      if (reference.required && isRuntimeReadyCandidate(item) && !assignment.entry) {
        addRecommendation(rows, seen, buildBundleRecommendation({
          type: "missingProfile",
          severity: "blocking",
          sourceKey: reference.sourceKey,
          title: "必需引用未加入 Profile",
          message: `${formatResourceDisplayName(item)} 被 ${reference.sourceLabel} 必需引用，但还没有 Profile entry。`,
          targetItems: [item],
          recommendedBundleId: getRecommendedBundleId([item]),
          applyHint: "把资源加入推荐 Bundle 的 Profile 草稿。"
        }));
      } else if (reference.required && assignment.entry && assignment.invalidInternalBundle) {
        addRecommendation(rows, seen, buildBundleRecommendation({
          type: assignment.missingBundleRule ? "missingBundle" : "unassignedBundle",
          severity: "blocking",
          sourceKey: reference.sourceKey,
          title: assignment.missingBundleRule ? "Bundle 规则缺失" : "必需引用缺少 Bundle",
          message: `${formatResourceDisplayName(item)} 是 internal runtime 资源，但当前 Bundle 归属无效。`,
          targetItems: [item],
          recommendedBundleId: getRecommendedBundleId([item]),
          applyHint: "把资源改入推荐 Bundle 的 Profile 草稿。"
        }));
      } else {
        addRecommendation(rows, seen, buildAcceptedRecommendation(item, reference, assignment));
      }
    }
  }

  for (const group of groupReferenceRows(referenceRows)) {
    const coRows = getCoReferencedResourceRows(group.rows);
    const groupRows = coRows.length > 0 ? coRows : group.rows;
    for (const row of groupRows) {
      const item = row.targetItem;
      if (!item) {
        addRecommendation(rows, seen, buildUnmappedReferenceRecommendation(row));
        continue;
      }
      if (!row.required || !isRuntimeReadyCandidate(item)) continue;
      const assignment = getBundleAssignmentState(item);
      if (!assignment.entry) {
        addRecommendation(rows, seen, buildBundleRecommendation({
          type: "missingProfile",
          severity: "blocking",
          sourceKey: row.sourceKey,
          title: "同组必需资源未加入 Profile",
          message: `${formatResourceDisplayName(item)} 与当前检查资源被 ${row.sourceLabel} 共同必需引用，但还没有 Profile entry。`,
          targetItems: [item],
          recommendedBundleId: getRecommendedBundleId([item], row),
          applyHint: "把同组 required 资源加入推荐 Bundle 的 Profile 草稿。"
        }));
      } else if (assignment.invalidInternalBundle) {
        addRecommendation(rows, seen, buildBundleRecommendation({
          type: assignment.missingBundleRule ? "missingBundle" : "unassignedBundle",
          severity: "blocking",
          sourceKey: row.sourceKey,
          title: assignment.missingBundleRule ? "同组资源 Bundle 规则缺失" : "同组必需资源缺少 Bundle",
          message: `${formatResourceDisplayName(item)} 与当前检查资源被 ${row.sourceLabel} 共同必需引用，但 internal Bundle 归属无效。`,
          targetItems: [item],
          recommendedBundleId: getRecommendedBundleId([item], row),
          applyHint: "把同组 required 资源改入推荐 Bundle 的 Profile 草稿。"
        }));
      }
    }
    const requiredItems = uniqueItems(groupRows.filter(row => row.required).map(row => row.targetItem).filter(Boolean));
    const assignable = requiredItems.filter(canAssignItemToInternalBundle);
    const bundleIds = Array.from(new Set(assignable.map(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule")).filter(id => findBundleRuleById(id))));
    if (assignable.length > 1 && bundleIds.length > 1) {
      const recommendedBundleId = getRecommendedBundleId(assignable, group.rows[0]);
      const targetItems = assignable.filter(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule") !== recommendedBundleId);
      addRecommendation(rows, seen, buildBundleRecommendation({
        type: "splitBundle",
        severity: "suggested",
        sourceKey: group.sourceKey,
        title: "同源必需引用分散在多个 Bundle",
        message: `${group.label} 的 required runtime 资源分布在 ${bundleIds.length} 个 Bundle，建议合并以降低加载顺序风险。`,
        detail: `当前 Bundle：${bundleIds.join(", ")}`,
        targetItems,
        recommendedBundleId,
        actionLabel: "应用本组草稿建议",
        applyHint: "把本组可分配资源改入推荐 Bundle 的 Profile 草稿。"
      }));
    }
  }

  return sortBundleRecommendations(rows);
}

function buildUnmappedReferenceRecommendation(row) {
  return {
    id: ["info", "unmapped", row.sourceKey, row.targetStableId || row.targetResourceKey || row.targetResourceId].join("::"),
    type: "unmapped",
    severity: "info",
    title: "引用目标无法映射到资源库项",
    message: `${row.sourceLabel} 引用了 ${row.targetLabel}，但当前 Resource Manager 没有找到对应资源项。`,
    detail: row.sourceField || "",
    targetItems: [],
    recommendedBundleId: "",
    canApply: false,
    actionLabel: "无法自动处理",
    applyHint: "缺少可映射资源项，不能生成 Profile 草稿。"
  };
}

function buildBundleRecommendation(options) {
  const targetItems = uniqueItems(options.targetItems);
  const canApply = Boolean(options.recommendedBundleId) && targetItems.some(canAssignItemToInternalBundle);
  return {
    id: [
      options.type,
      options.sourceKey || "global",
      options.recommendedBundleId || "no-bundle",
      targetItems.map(item => item.key).join("|")
    ].join("::"),
    type: options.type,
    severity: options.severity,
    title: options.title,
    message: options.message,
    detail: options.detail || "",
    targetItems,
    recommendedBundleId: options.recommendedBundleId || "",
    canApply,
    actionLabel: options.actionLabel || "写入推荐 Bundle 草稿",
    applyHint: options.applyHint || ""
  };
}

function buildAcceptedRecommendation(item, reference, assignment) {
  const external = assignment.entry && ["external", "editorOnly", "excluded"].includes(assignment.deliveryMode);
  return {
    id: ["accepted", reference.sourceKey, item.key, assignment.bundleRule || assignment.deliveryMode || "none"].join("::"),
    type: external ? "externalConfig" : "validBundle",
    severity: "accepted",
    title: external ? "配置引用可接受" : "Bundle 归属可接受",
    message: external
      ? `${formatResourceDisplayName(item)} 当前是 ${assignment.deliveryMode}，不会进入本地 AssetBundle。`
      : `${formatResourceDisplayName(item)} 的 Profile / Bundle 状态没有发现阻塞问题。`,
    detail: reference.sourceLabel,
    targetItems: [item],
    recommendedBundleId: assignment.bundleRule || "",
    canApply: false,
    actionLabel: "已处理"
  };
}

function buildInfoRecommendation(item, type, title, message) {
  return {
    id: ["info", type, item.key].join("::"),
    type,
    severity: "info",
    title,
    message,
    detail: formatResourceDisplayName(item),
    targetItems: [item],
    recommendedBundleId: "",
    canApply: false,
    actionLabel: "无需操作"
  };
}

function addRecommendation(rows, seen, row) {
  if (!row || seen.has(row.id)) return;
  seen.add(row.id);
  rows.push(row);
}

function sortBundleRecommendations(rows) {
  const order = { blocking: 0, suggested: 1, accepted: 2, info: 3 };
  return rows.sort((a, b) => (order[a.severity] ?? 9) - (order[b.severity] ?? 9) || compareText(a.title, b.title));
}

function getBundleAssignmentState(item) {
  const entry = findDraftBuildProfileEntryForItem(item);
  const deliveryMode = pick(entry, "deliveryMode") || "";
  const override = pick(entry, "overrideIntent") || "forbid";
  const bundleRule = pick(entry, "bundleRule") || "";
  const internal = Boolean(entry) && (deliveryMode === "" || deliveryMode === "internal");
  const missingBundleRule = internal && bundleRule && !findBundleRuleById(bundleRule);
  const unassignedInternalBundle = internal && !bundleRule;
  return {
    entry,
    deliveryMode: deliveryMode || "notInProfile",
    override,
    bundleRule,
    internal,
    missingBundleRule,
    unassignedInternalBundle,
    invalidInternalBundle: missingBundleRule || unassignedInternalBundle
  };
}

function getRecommendedBundleId(items, referenceRow = null) {
  if (state.selectedBundleRuleId && findBundleRuleById(state.selectedBundleRuleId)) return state.selectedBundleRuleId;
  const selected = getSelectedItem();
  const selectedBundle = selected ? pick(findDraftBuildProfileEntryForItem(selected), "bundleRule") : "";
  if (selectedBundle && findBundleRuleById(selectedBundle)) return selectedBundle;
  const itemBundle = asArray(items)
    .map(item => pick(findDraftBuildProfileEntryForItem(item), "bundleRule"))
    .find(bundleRule => findBundleRuleById(bundleRule));
  if (itemBundle) return itemBundle;
  if (referenceRow) {
    const sourceSegment = normalizeBundleRuleId(stringValue(pick(referenceRow, "sourceConfigKind")) || "references");
    const matching = getBuildProfileBundleRules().find(rule => String(rule.id || "").includes(sourceSegment));
    if (matching) return matching.id;
  }
  return getBuildProfileBundleRules()[0]?.id || "";
}

function applyBundleRecommendationDraft(recommendationId) {
  const recommendation = getReferenceReviewModel().recommendations.find(row => row.id === recommendationId);
  if (!recommendation) {
    state.lastActionMessage = "引用建议已过期，请刷新后重试。";
    renderReferencesWorkspace();
    return;
  }
  applyReferenceRecommendationsDraft([recommendation], "已按引用建议更新 1 条建议");
}

function applyVisibleReferenceRecommendations() {
  const model = getReferenceReviewModel();
  const recommendations = getVisibleReferenceRecommendations(model.recommendations);
  applyReferenceRecommendationsDraft(recommendations, `已应用 ${recommendations.length} 条可见引用建议`);
}

function applyReferenceRecommendationsDraft(recommendations, label) {
  const rows = getVisibleReferenceRecommendations(recommendations);
  if (rows.length === 0) {
    state.lastActionMessage = "当前没有可自动应用的可见引用建议。";
    renderReferencesWorkspace();
    return;
  }
  const profile = ensureBuildProfileDraft();
  const assignments = new Map();
  const conflicts = new Set();
  for (const recommendation of rows) {
    if (!recommendation.recommendedBundleId || !findBundleRuleById(recommendation.recommendedBundleId)) continue;
    for (const item of uniqueItems(recommendation.targetItems).filter(canAssignItemToInternalBundle)) {
      const key = getReferencePatchItemKey(item);
      const existing = assignments.get(key);
      if (existing && existing.bundleRuleId !== recommendation.recommendedBundleId) {
        conflicts.add(key);
        continue;
      }
      if (!existing) assignments.set(key, { item, bundleRuleId: recommendation.recommendedBundleId });
    }
  }

  const patchEntries = [];
  let changed = 0;
  let unchanged = 0;
  for (const [key, assignment] of assignments) {
    if (conflicts.has(key)) continue;
    let entry = findDraftBuildProfileEntryForItem(assignment.item);
    if (entry && !isManualProfileEntry(entry)) {
      unchanged++;
      continue;
    }
    const existedBefore = Boolean(entry);
    const before = existedBefore ? structuredCloneCompat(entry) : null;
    if (!entry) {
      entry = buildDraftBuildProfileEntryForItem(assignment.item);
      profile.entries.push(entry);
    }
    entry.bundleRule = assignment.bundleRuleId;
    entry.deliveryMode = "internal";
    const after = structuredCloneCompat(entry);
    if (stableJson(before || null) === stableJson(after)) {
      unchanged++;
      continue;
    }
    patchEntries.push({
      item: buildReferencePatchItemSnapshot(assignment.item),
      existedBefore,
      before,
      after
    });
    changed++;
    state.selectedBundleRuleId = assignment.bundleRuleId;
  }

  if (changed === 0) {
    state.lastActionMessage = unchanged > 0 ? "可见引用建议已经在草稿中，无需重复应用。" : "当前建议没有可自动加入 internal Bundle 的资源。";
    renderReferencesWorkspace();
    return;
  }
  state.referencesLastPatch = {
    createdAt: Date.now(),
    entries: patchEntries
  };
  markBuildProfileDirty();
  const conflictSuffix = conflicts.size > 0 ? `；${conflicts.size} 个资源存在推荐 Bundle 冲突，已跳过` : "";
  state.lastActionMessage = `${label}，影响 ${changed} 个资源；Profile 草稿未保存${conflictSuffix}。`;
  render();
}

function undoLastReferenceRecommendationDraft() {
  const patch = state.referencesLastPatch;
  const entries = asArray(patch?.entries);
  if (entries.length === 0) {
    state.lastActionMessage = "没有可撤销的 Refs 应用。";
    renderReferencesWorkspace();
    return;
  }
  const profile = ensureBuildProfileDraft();
  let reverted = 0;
  let skipped = 0;
  for (const patchEntry of entries) {
    const index = profile.entries.findIndex(entry => profileEntryMatchesItem(entry, patchEntry.item));
    if (index < 0) {
      skipped++;
      continue;
    }
    const current = profile.entries[index];
    if (stableJson(current) !== stableJson(patchEntry.after)) {
      skipped++;
      continue;
    }
    if (patchEntry.existedBefore) {
      profile.entries[index] = structuredCloneCompat(patchEntry.before);
    } else {
      profile.entries.splice(index, 1);
    }
    reverted++;
  }
  if (reverted > 0) markBuildProfileDirty();
  state.referencesLastPatch = null;
  state.lastActionMessage = `已撤销本轮 Refs 应用 ${reverted} 个资源${skipped > 0 ? `；${skipped} 个资源已有后续手动修改，已保留` : ""}。`;
  render();
}

function buildReferencePatchItemSnapshot(item) {
  return {
    key: item?.key || "",
    resourceKey: item?.resourceKey || "",
    runtimeResourceKey: item?.runtimeResourceKey || "",
    providerResourceKey: item?.providerResourceKey || "",
    packageResourceKey: item?.packageResourceKey || "",
    stableId: item?.stableId || "",
    unityGuid: item?.unityGuid || "",
    unityAssetPath: item?.unityAssetPath || ""
  };
}

function getReferencePatchItemKey(item) {
  const snapshot = buildReferencePatchItemSnapshot(item);
  return snapshot.key || snapshot.resourceKey || snapshot.runtimeResourceKey || snapshot.providerResourceKey || snapshot.stableId || stableJson(snapshot);
}

function handleReferencesActionClick(event) {
  const referenceButton = event.target.closest("button[data-reference-action]");
  if (referenceButton && !referenceButton.disabled) {
    const action = referenceButton.dataset.referenceAction;
    if (action === "apply-visible") applyVisibleReferenceRecommendations();
    if (action === "undo-last") undoLastReferenceRecommendationDraft();
    if (action === "open-profile-save") {
      setWorkspace("profile");
      render();
    }
    if (action === "toggle-education") {
      state.referencesEducationVisible = !state.referencesEducationVisible;
      saveReferenceEducationPreference(state.referencesEducationVisible);
      renderReferencesWorkspace();
    }
    if (action === "open-browse") {
      setWorkspace("browse");
      render();
    }
    if (action === "open-build") {
      setWorkspace("build");
      render();
    }
    if (action === "open-graph") {
      state.graphScope = modelHasReferenceIssues(getReferenceReviewModel()) ? "issues" : "current";
      state.graphSelectedNodeId = "";
      setWorkspace("graph");
      render();
    }
    return;
  }
  const button = event.target.closest("button[data-recommendation-action]");
  if (!button || button.disabled) return;
  if (button.dataset.recommendationAction === "apply") {
    applyBundleRecommendationDraft(button.dataset.recommendationId || "");
  }
}

function modelHasReferenceIssues(model) {
  return asArray(model?.buckets?.blocking).length > 0 || asArray(model?.buckets?.suggested).length > 0;
}

function normalizeReferenceRow(edge, fallbackTargetItem) {
  const sourceConfigKind = stringValue(pick(edge, "sourceConfigKind", "sourceKind", "sourceType")) || "unknown";
  const sourceConsumerKind = stringValue(pick(edge, "sourceConsumerKind", "consumerKind")) || "";
  const sourceStableId = stringValue(pick(edge, "sourceStableId", "sourceConfigStableId", "sourceId")) || "";
  const sourceField = stringValue(pick(edge, "sourceField", "field", "fieldPath")) || "";
  const targetStableId = stringValue(pick(edge, "targetLibraryItemStableId", "targetStableId", "targetResourceStableId")) || "";
  const targetResourceKey = stringValue(pick(edge, "targetResourceKey", "resourceKey", "targetProviderResourceKey", "targetRuntimeResourceKey")) || "";
  const targetResourceId = stringValue(pick(edge, "targetResourceId")) || "";
  const mappedTargetItem = findItemForReferenceTarget(targetStableId, targetResourceKey, targetResourceId);
  const targetItem = mappedTargetItem || fallbackTargetItem || null;
  const sourceKey = [sourceConfigKind, sourceStableId || "(missing)"].join("::");
  return {
    raw: edge,
    sourceConsumerKind,
    sourceConfigKind,
    sourceStableId,
    sourceField,
    sourceKey,
    sourceLabel: `${sourceConfigKind}${sourceStableId ? ` · ${sourceStableId}` : ""}`,
    targetStableId,
    targetResourceKey,
    targetResourceId,
    targetItem,
    targetLabel: targetResourceKey || targetStableId || targetResourceId || "(unknown target)",
    bindingKind: stringValue(pick(edge, "bindingKind")) || "",
    required: pick(edge, "isRequiredAtRuntime") === true || pick(edge, "required") === true,
    preloadPolicy: stringValue(pick(edge, "preloadPolicy")) || ""
  };
}

function groupReferenceRows(rows) {
  const groups = new Map();
  for (const row of asArray(rows)) {
    const key = row.sourceKey || "unknown::unknown";
    if (!groups.has(key)) {
      groups.set(key, {
        sourceKey: key,
        label: row.sourceConfigKind || "unknown",
        sourceStableId: row.sourceStableId || "",
        rows: []
      });
    }
    groups.get(key).rows.push(row);
  }
  return Array.from(groups.values()).sort((a, b) => compareText(a.label, b.label) || compareText(a.sourceStableId, b.sourceStableId));
}

function countReferenceGroups(rows) {
  return groupReferenceRows(rows).length;
}

function getAllReferenceEdges() {
  const graph = pick(state.resourcesPayload, "referenceGraph") || {};
  return asArray(pick(graph, "edges"));
}

function findItemForReferenceTarget(stableId, resourceKey, resourceId = "") {
  return getBrowserItems().find(item => matchesItem(item, stableId, resourceKey, resourceId)) || null;
}

function uniqueItems(items) {
  const map = new Map();
  for (const item of asArray(items)) {
    if (!item) continue;
    map.set(item.key || item.stableId || item.resourceKey || formatResourceDisplayName(item), item);
  }
  return Array.from(map.values());
}
