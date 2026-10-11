(function (global) {
  "use strict";

  const PROVIDER_ID = "artSource";
  const TASK_STATUSES = ["blocked", "active", "waiting", "ready", "review", "completed"];
  const TASK_PRIORITIES = ["critical", "high", "medium", "low"];
  const WORKSTREAM_PHASES = ["story", "reference", "source", "export", "unity-import", "review", "legacy", "handoff", "art"];
  const TASK_CODES = {
    missing: "ART_SOURCE_TASKS_MISSING",
    invalid: "ART_SOURCE_TASKS_JSON_INVALID",
    workstreamMissing: "ART_SOURCE_TASK_WORKSTREAM_MISSING",
    assetMissing: "ART_SOURCE_TASK_ASSET_MISSING",
    previewMissing: "ART_SOURCE_PREVIEW_MISSING"
  };

  function createDefaultFilters() {
    return {
      search: "",
      workstream: "all",
      taskStatus: "all",
      priority: "all",
      blocked: "all",
      family: "all",
      reviewStatus: "all"
    };
  }

  function createDefaultCreateDraft() {
    return {
      kind: "task",
      workstreamId: "",
      taskId: "",
      title: "",
      phase: "source",
      status: "active",
      priority: "medium",
      summary: "",
      blockedBy: "",
      nextAction: "",
      assetIds: [],
      assetSearch: "",
      previewPath: "",
      reviewRefsText: ""
    };
  }

  function createModel(items, filters, selectedKey, resourcesPayload, taskDrafts, taskSaveState, createDraft, createState, uiPanels, graphZoom) {
    const currentFilters = Object.assign(createDefaultFilters(), filters || {});
    const artItems = asArray(items)
      .filter(isArtSourceItem)
      .map(normalizeArtItem)
      .sort(compareArtItems);
    const itemByAssetId = new Map(artItems.map(item => [item.assetId, item]));
    const snapshot = extractTaskSnapshot(resourcesPayload);
    const workstreams = normalizeWorkstreams(snapshot.workstreams);
    const tasks = normalizeTasks(snapshot.tasks, workstreams, itemByAssetId).sort(compareTasks);
    tasks.forEach(task => applyTaskDraft(task, taskDrafts));
    annotateTaskBlockers(tasks);
    const filteredTasks = tasks.filter(task => matchesTaskFilters(task, currentFilters));
    const selected = findSelectedTask(tasks, filteredTasks, selectedKey);
    const normalizedTaskSaveState = normalizeTaskSaveState(taskSaveState);
    tasks.forEach(task => {
      task.isSelected = Boolean(selected && task.key === selected.key);
      task.candidateAssets = buildCandidateAssets(artItems, task);
      task.saveState = normalizedTaskSaveState.taskId === task.taskId ? normalizedTaskSaveState : { status: "idle", taskId: "", error: "", message: "" };
      task.uiPanels = normalizeUiPanels(uiPanels);
    });
    const workstreamViews = workstreams
      .map(workstream => ({
        ...workstream,
        isSelected: Boolean(selected && selected.workstreamId === workstream.workstreamId),
        allTasks: tasks.filter(task => task.workstreamId === workstream.workstreamId),
        tasks: filteredTasks.filter(task => task.workstreamId === workstream.workstreamId)
      }))
      .filter(workstream => workstream.allTasks.length > 0 || currentFilters.workstream === "all" || currentFilters.workstream === workstream.workstreamId);
    const model = {
      providerId: PROVIDER_ID,
      filters: currentFilters,
      allItems: artItems,
      itemByAssetId,
      allTasks: tasks,
      filteredTasks,
      selected,
      selectedKey: selected ? selected.key : "",
      selectedWorkstreamId: selected ? selected.workstreamId : "",
      workstreams: workstreamViews,
      graphZoom: normalizeGraphZoom(graphZoom),
      dependencyGraph: buildDependencyGraph(tasks, selected),
      options: buildFilterOptions(artItems, workstreams, tasks),
      tasksHash: snapshot.tasksHash,
      taskSaveState: normalizedTaskSaveState,
      createDraft: normalizeCreateDraft(createDraft, workstreams, artItems),
      createState: normalizeCreateState(createState),
      uiPanels: normalizeUiPanels(uiPanels),
      hasProviderItems: artItems.length > 0,
      hasTasks: tasks.length > 0,
      snapshot
    };
    modelPostProcessCreateModel(model);
    return model;
  }

  function modelPostProcessCreateModel(model) {
    if (!model) return;
    model.createDraft.candidateAssets = buildCreateCandidateAssets(model.allItems, model.createDraft);
    model.createDraft.validationMessage = getCreateValidationMessage(model, model.createDraft);
    model.createDraft.canSave = !model.createDraft.validationMessage;
    model.createDraft.saveState = model.createState || { status: "idle", message: "" };
  }

  function render(model, imagePreview) {
    if (!model || (!model.hasProviderItems && !model.hasTasks)) {
      return `
        <div class="art-empty-state">
          <h2>ArtSource 多进程工作台</h2>
          <p>当前资源集合没有读取到 artSource 数据源。确认 Authoring 服务已加载 ArtSource/Projects/WGame/art_manifest.json 和 art_tasks.json 后刷新。</p>
        </div>`;
    }
    return `
      <div class="art-shell">
        <section class="art-status-header" aria-label="ArtSource 看板说明">
          <div>
            <h2>ArtSource 多进程美术工作台</h2>
            <p>同时跟踪故事输入、参考图、源文件、Unity 交接、评审和旧参考等并行进程；本页只写 art_tasks.json，不写 manifest、不导入 Unity、不保存 Profile。</p>
          </div>
          <div class="art-readonly-badge">任务写入门禁</div>
        </section>
        ${renderDependencyGraph(model.dependencyGraph, model.graphZoom, Boolean(model.uiPanels && model.uiPanels.graph))}
        <section class="art-main-grid art-task-workbench" aria-label="ArtSource 多进程任务和预览">
          ${renderPreviewStage(model.selected)}
          ${renderActionPanel(model.selected)}
        </section>
        ${renderCreatePanel(model)}
        ${renderFilters(model)}
        ${renderImagePreviewLightbox(imagePreview)}
      </div>`;
  }

  function renderDependencyGraph(graph, zoom, isOpen) {
    const nodes = asArray(graph && graph.nodes);
    if (nodes.length === 0) return "";
    const edges = asArray(graph && graph.edges);
    const selectedNode = nodes.find(node => node.isSelected) || nodes[0];
    const height = graph.height || 240;
    const graphZoom = normalizeGraphZoom(zoom);
    const zoomPercent = Math.round(graphZoom * 100);
    const contentWidth = Math.round(1000 * graphZoom);
    const contentHeight = Math.round(height * graphZoom);
    const canZoomOut = graphZoom > 0.7;
    const canZoomIn = graphZoom < 1.8;
    return `
      <details class="art-dependency-graph" data-art-panel="graph"${isOpen ? " open" : ""} style="--art-graph-open-height: ${escapeHtml(contentHeight + 98)}px;" aria-label="流程依赖图">
        <summary class="art-dependency-summary">
          <span class="art-dependency-summary-content">
            <span>流程依赖图</span>
            <small>
              ${edges.length} 条依赖 · 当前：${escapeHtml(selectedNode ? selectedNode.workstreamTitle : "无选中流程")} ·
              <span class="art-graph-state-closed">展开查看前后关系</span>
              <span class="art-graph-state-open">收起后查看预览</span>
            </small>
          </span>
        </summary>
        <div class="art-dependency-body">
          <div class="art-dependency-heading">
          <div>
            <h3>前后依赖</h3>
            <p>箭头来自 blockedBy 中能匹配到的任务；没有箭头但仍阻塞的是外部输入门禁。按住图中空白处拖动可移动视图。</p>
          </div>
          <div class="art-graph-toolbar" aria-label="流程依赖图缩放">
            <span>${edges.length} 条依赖</span>
            <button type="button" class="secondary" data-art-action="art-graph-zoom-out" aria-label="缩小流程依赖图"${canZoomOut ? "" : " disabled"}>-</button>
            <strong>${escapeHtml(zoomPercent)}%</strong>
            <button type="button" class="secondary" data-art-action="art-graph-zoom-in" aria-label="放大流程依赖图"${canZoomIn ? "" : " disabled"}>+</button>
            <button type="button" class="secondary" data-art-action="art-graph-zoom-reset">重置</button>
          </div>
          </div>
          <div class="art-graph-canvas" data-art-graph-canvas style="--art-graph-height: ${escapeHtml(height)}px;">
            <div class="art-graph-content" style="width: ${escapeHtml(contentWidth)}px; height: ${escapeHtml(contentHeight)}px;">
              <div class="art-graph-scale" style="--art-graph-zoom: ${escapeHtml(graphZoom)}; --art-graph-height: ${escapeHtml(height)}px;">
                <svg class="art-graph-lines" viewBox="0 0 1000 ${escapeHtml(height)}" aria-hidden="true" focusable="false">
                  <defs>
                    <marker id="artGraphArrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="5" markerHeight="5" orient="auto-start-reverse">
                      <path d="M 0 0 L 10 5 L 0 10 z"></path>
                    </marker>
                  </defs>
                  ${edges.map(edge => `
                    <path class="${edge.isSelected ? "selected" : ""}" d="M ${escapeHtml(edge.fromX)} ${escapeHtml(edge.fromY)} C ${escapeHtml(edge.midX)} ${escapeHtml(edge.fromY)}, ${escapeHtml(edge.midX)} ${escapeHtml(edge.toY)}, ${escapeHtml(edge.toX)} ${escapeHtml(edge.toY)}"></path>
                  `).join("")}
                </svg>
                <div class="art-graph-nodes">
                  ${nodes.map(node => `
                    <button type="button"
                      class="art-graph-node art-tone-${escapeHtml(node.tone)}${node.isSelected ? " selected" : ""}${node.hasIncoming ? " has-incoming" : ""}${node.hasOutgoing ? " has-outgoing" : ""}"
                      data-art-workstream-key="${escapeHtml(node.workstreamId)}"
                      style="left: ${escapeHtml(node.left)}%; top: ${escapeHtml(node.top)}%;"
                      ${node.isSelected ? `aria-current="true"` : ""}
                      aria-label="选择流程：${escapeHtml(node.workstreamTitle)}">
                      <span class="art-graph-phase">${escapeHtml(node.phaseLabel)}</span>
                      <strong>${escapeHtml(node.workstreamTitle)}</strong>
                      <small>${escapeHtml(node.title)}</small>
                      <span class="art-graph-meta">${escapeHtml(node.statusLabel)}${node.externalBlocker ? " · 外部输入" : ""}</span>
                    </button>
                  `).join("")}
                </div>
              </div>
            </div>
          </div>
        </div>
      </details>`;
  }

  function renderCreatePanel(model) {
    const draft = model.createDraft || createDefaultCreateDraft();
    const saveState = draft.saveState || { status: "idle", message: "" };
    const busy = saveState.status === "running";
    const forcedOpen = saveState.status === "running" || saveState.status === "error" || saveState.status === "conflict";
    const isOpen = forcedOpen || Boolean(model.uiPanels && model.uiPanels.create);
    const statusMessage = saveState.message || (isOpen ? draft.validationMessage : "") || "需要新增进程或任务时再展开。";
    return `
      <details class="art-create-panel" data-art-panel="create"${isOpen ? " open" : ""}>
        <summary>
          <span>创建入口</span>
          <small>${escapeHtml(statusMessage)}</small>
        </summary>
        <div class="art-create-body art-save-${escapeHtml(saveState.status || "idle")}">
          <div class="art-create-intro">
            <strong>创建美术进程 / 任务</strong>
            <span>只写 art_tasks.json；创建后再在右侧推进状态。</span>
          </div>
          <div class="art-segmented art-create-kind" role="group" aria-label="创建类型">
            <button type="button" class="${draft.kind === "workstream" ? "selected" : ""}" data-art-action="art-create-set-kind" data-art-create-kind="workstream"${busy ? " disabled" : ""}>新建进程</button>
            <button type="button" class="${draft.kind === "task" ? "selected" : ""}" data-art-action="art-create-set-kind" data-art-create-kind="task"${busy ? " disabled" : ""}>新建任务</button>
          </div>
          ${draft.kind === "workstream" ? renderCreateWorkstreamForm(draft, busy) : renderCreateTaskForm(model, draft, busy)}
          ${saveState.status === "error" || saveState.status === "conflict" ? `<p class="art-editor-error">${escapeHtml(saveState.error || saveState.message)}</p>` : ""}
          <div class="art-editor-actions">
            <button type="button" class="secondary" data-art-action="art-create-reset"${busy ? " disabled" : ""}>重置创建表单</button>
            <button type="button" data-art-action="art-create-save"${busy || !draft.canSave ? " disabled" : ""}>${busy ? "创建中..." : draft.kind === "workstream" ? "创建进程" : "创建任务"}</button>
          </div>
        </div>
      </details>`;
  }

  function renderCreateWorkstreamForm(draft, busy) {
    return `
      <div class="art-create-grid">
        <label class="art-editor-field">
          <span>进程 id</span>
          <input data-art-create-field="workstreamId" value="${escapeHtml(draft.workstreamId)}" placeholder="reference.shared_law_world"${busy ? " disabled" : ""}>
        </label>
        <label class="art-editor-field">
          <span>进程标题</span>
          <input data-art-create-field="title" value="${escapeHtml(draft.title)}" placeholder="共享法则世界参考图生产"${busy ? " disabled" : ""}>
        </label>
        <label class="art-editor-field">
          <span>阶段</span>
          <select data-art-create-field="phase"${busy ? " disabled" : ""}>
            ${WORKSTREAM_PHASES.map(phase => `<option value="${escapeHtml(phase)}"${draft.phase === phase ? " selected" : ""}>${escapeHtml(formatPhase(phase))}</option>`).join("")}
          </select>
        </label>
        <label class="art-editor-field">
          <span>状态</span>
          <select data-art-create-field="status"${busy ? " disabled" : ""}>
            ${TASK_STATUSES.map(status => `<option value="${escapeHtml(status)}"${draft.status === status ? " selected" : ""}>${escapeHtml(formatStatus(status))}</option>`).join("")}
          </select>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>摘要</span>
          <textarea rows="2" data-art-create-field="summary" placeholder="这条进程要解决什么问题"${busy ? " disabled" : ""}>${escapeHtml(draft.summary)}</textarea>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>下一动作</span>
          <textarea rows="2" data-art-create-field="nextAction" placeholder="创建后执行者第一步做什么"${busy ? " disabled" : ""}>${escapeHtml(draft.nextAction)}</textarea>
        </label>
      </div>`;
  }

  function renderCreateTaskForm(model, draft, busy) {
    return `
      <div class="art-create-grid">
        <label class="art-editor-field">
          <span>所属进程</span>
          <select data-art-create-field="workstreamId"${busy || model.workstreams.length === 0 ? " disabled" : ""}>
            ${model.workstreams.length === 0 ? `<option value="">先创建进程</option>` : model.workstreams.map(workstream => `<option value="${escapeHtml(workstream.workstreamId)}"${draft.workstreamId === workstream.workstreamId ? " selected" : ""}>${escapeHtml(workstream.title)} · ${escapeHtml(workstream.workstreamId)}</option>`).join("")}
          </select>
        </label>
        <label class="art-editor-field">
          <span>任务 id</span>
          <input data-art-create-field="taskId" value="${escapeHtml(draft.taskId)}" placeholder="reference.shared_law_world.mood_key_art"${busy ? " disabled" : ""}>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>任务标题</span>
          <input data-art-create-field="title" value="${escapeHtml(draft.title)}" placeholder="生成共享法则世界 mood key art 参考图"${busy ? " disabled" : ""}>
        </label>
        <label class="art-editor-field">
          <span>状态</span>
          <select data-art-create-field="status"${busy ? " disabled" : ""}>
            ${TASK_STATUSES.map(status => `<option value="${escapeHtml(status)}"${draft.status === status ? " selected" : ""}>${escapeHtml(formatStatus(status))}</option>`).join("")}
          </select>
        </label>
        <label class="art-editor-field">
          <span>优先级</span>
          <select data-art-create-field="priority"${busy ? " disabled" : ""}>
            ${TASK_PRIORITIES.map(priority => `<option value="${escapeHtml(priority)}"${draft.priority === priority ? " selected" : ""}>${escapeHtml(formatPriority(priority))}</option>`).join("")}
          </select>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>阻塞原因</span>
          <textarea rows="2" data-art-create-field="blockedBy" placeholder="无阻塞则留空"${busy ? " disabled" : ""}>${escapeHtml(draft.blockedBy)}</textarea>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>下一动作</span>
          <textarea rows="2" data-art-create-field="nextAction" placeholder="写给执行者的下一步"${busy ? " disabled" : ""}>${escapeHtml(draft.nextAction)}</textarea>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>任务预览路径</span>
          <input data-art-create-field="previewPath" value="${escapeHtml(draft.previewPath)}" placeholder="可选，只填真实存在的 PNG/JPG/WebP 路径"${busy ? " disabled" : ""}>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>评审/契约引用</span>
          <textarea rows="2" data-art-create-field="reviewRefsText" placeholder="可选，每行或逗号分隔一个文档路径"${busy ? " disabled" : ""}>${escapeHtml(draft.reviewRefsText)}</textarea>
        </label>
        <label class="art-editor-field art-create-wide">
          <span>绑定资产</span>
          <input type="search" data-art-create-field="assetSearch" value="${escapeHtml(draft.assetSearch)}" placeholder="搜索 assetId / 资产家族 / 评审状态"${busy ? " disabled" : ""}>
        </label>
      </div>
      <div class="art-asset-picker art-create-assets">
        ${draft.candidateAssets.length === 0 ? `<p class="muted">没有匹配资产；可以先创建横向任务，之后再绑定资产。</p>` : draft.candidateAssets.map(asset => `
          <label class="art-asset-option">
            <input type="checkbox"
              data-art-create-asset-toggle="${escapeHtml(asset.assetId)}"
              ${draft.assetIds.includes(asset.assetId) ? " checked" : ""}
              ${busy ? " disabled" : ""}>
            <span>${escapeHtml(asset.assetId)}</span>
            <small>${escapeHtml(asset.family)} · ${escapeHtml(asset.reviewStatus || "无评审状态")}</small>
          </label>`).join("")}
      </div>`;
  }

  function renderFilters(model) {
    const filters = model.filters;
    const activeCount = countActiveFilters(filters);
    const isOpen = Boolean(model.uiPanels && model.uiPanels.filters);
    return `
      <details class="art-filter-panel" data-art-panel="filters"${isOpen ? " open" : ""} aria-label="ArtSource 筛选">
        <summary>
          <span>筛选</span>
          <small>${activeCount > 0 ? `${activeCount} 个条件已启用` : "默认显示全部任务"}</small>
        </summary>
        <div class="art-filter-body">
          <label class="art-filter-field art-filter-search">
            <span>搜索</span>
            <input type="search" data-art-filter="search" value="${escapeHtml(filters.search)}" placeholder="任务、assetId、路径、resourceKey...">
          </label>
          ${renderSelect("workstream", "进程", model.options.workstream, filters.workstream)}
          ${renderSelect("taskStatus", "任务状态", model.options.taskStatus, filters.taskStatus)}
          ${renderSelect("priority", "优先级", model.options.priority, filters.priority)}
          <label class="art-filter-field">
            <span>阻塞</span>
            <select data-art-filter="blocked">
              ${[
                ["all", "全部"],
                ["blocked", "有阻塞"],
                ["unblocked", "无阻塞"]
              ].map(([value, label]) => `<option value="${escapeHtml(value)}"${filters.blocked === value ? " selected" : ""}>${escapeHtml(label)}</option>`).join("")}
            </select>
          </label>
          ${renderSelect("family", "资产家族", model.options.family, filters.family)}
          ${renderSelect("reviewStatus", "评审状态", model.options.reviewStatus, filters.reviewStatus)}
          <button type="button" class="secondary" data-art-action="clear-filters">清除筛选</button>
        </div>
      </details>`;
  }

  function renderSelect(key, label, options, selected) {
    return `
      <label class="art-filter-field">
        <span>${escapeHtml(label)}</span>
        <select data-art-filter="${escapeHtml(key)}">
          ${options.map(([value, text]) => `<option value="${escapeHtml(value)}"${value === selected ? " selected" : ""}>${escapeHtml(text)}</option>`).join("")}
        </select>
      </label>`;
  }

  function renderPreviewStage(task) {
    if (!task) {
      return `
        <section class="art-preview-stage">
          <div class="art-preview-empty">
            <h3>没有匹配任务</h3>
            <p>调整筛选或刷新 ArtSource 数据源。</p>
          </div>
        </section>`;
    }

    return `
      <section class="art-preview-stage" aria-label="ArtSource 任务预览">
        <div class="art-preview-frame">
          ${renderTaskPreviewContent(task)}
        </div>
        <div class="art-preview-caption">
          <div>
            <h3>${escapeHtml(task.title || task.taskId)}</h3>
            <p>${escapeHtml(task.taskId)} · ${escapeHtml(task.workstreamTitle)}</p>
          </div>
          <span class="art-detail-lane">${escapeHtml(task.statusLabel)}</span>
        </div>
        <p class="art-preview-note">${escapeHtml(task.assets.length > 1 ? "预览拼板来自任务绑定资产的真实预览图；没有真实图的资产显示缺预览。" : task.previewNote)}</p>
      </section>`;
  }

  function renderTaskPreviewContent(task) {
    if (task.assets.length > 1) {
      return `
        <div class="art-contact-sheet">
          ${task.assets.map(asset => `
            <button type="button" class="art-contact-tile" data-art-resource-key="${escapeHtml(asset.key)}">
              ${asset.hasPreview
                ? `<img src="${escapeHtml(asset.previewUrl)}" alt="${escapeHtml(asset.assetId)} 预览" data-art-preview-url="${escapeHtml(asset.previewUrl)}" data-art-preview-title="${escapeHtml(asset.assetId)}" data-art-preview-note="${escapeHtml(asset.previewNote || asset.previewPath || "ArtSource 真实预览图。")}">`
                : `<span class="art-contact-missing">无预览</span>`}
              <small>${escapeHtml(asset.assetId)}</small>
            </button>`).join("")}
        </div>`;
    }

    const preview = getTaskPreview(task);
    if (preview.url) {
      return `
        <button type="button" class="art-preview-image-button" data-art-preview-url="${escapeHtml(preview.url)}" data-art-preview-title="${escapeHtml(task.title || task.taskId)}" data-art-preview-note="${escapeHtml(preview.path || task.previewNote || "ArtSource 真实预览图。")}" aria-label="放大查看预览图：${escapeHtml(task.title || task.taskId)}">
          <img src="${escapeHtml(preview.url)}" alt="${escapeHtml(task.title || task.taskId)} 预览">
          <span>点击放大查看</span>
        </button>`;
    }

    return `
      <div class="art-preview-missing">
        <strong>${escapeHtml(getMissingPreviewTitle(task))}</strong>
        <span>${escapeHtml(getMissingPreviewText(task))}</span>
      </div>`;
  }

  function renderImagePreviewLightbox(imagePreview) {
    const preview = normalizeImagePreview(imagePreview);
    if (!preview.url) return "";
    return `
      <div class="art-image-lightbox" role="dialog" aria-modal="true" aria-label="图像放大预览">
        <button type="button" class="art-image-lightbox-backdrop" data-art-action="art-image-preview-close" aria-label="关闭图像预览"></button>
        <section class="art-image-lightbox-panel">
          <header>
            <div>
              <span>图像预览</span>
              <h3>${escapeHtml(preview.title || "ArtSource 预览图")}</h3>
            </div>
            <button type="button" class="secondary" data-art-action="art-image-preview-close">关闭</button>
          </header>
          <div class="art-image-lightbox-viewport">
            <img src="${escapeHtml(preview.url)}" alt="${escapeHtml(preview.title || "ArtSource 预览图")}">
          </div>
          ${preview.note ? `<p>${escapeHtml(preview.note)}</p>` : ""}
        </section>
      </div>`;
  }

  function getMissingPreviewTitle(task) {
    if (!task || !task.isBlocked) return "缺少真实预览";
    if (task.blockerKind === "task") return "等待前置任务";
    if (task.blockerKind === "external") return "等待外部输入";
    return "任务已阻塞";
  }

  function getMissingPreviewText(task) {
    if (!task || !task.isBlocked) return "当前任务没有真实 PNG/JPG/WebP 预览。";
    if (task.blockerKind === "task") return task.blockedSummary || "前置任务尚未完成。";
    if (task.blockerKind === "external") return task.externalBlocker || task.blockedSummary || "外部输入尚未交付。";
    return task.blockedSummary || task.blockedBy || "当前任务仍有阻塞。";
  }

  function renderActionPanel(task) {
    if (!task) {
      return `
        <aside class="art-detail-panel art-action-panel">
          <h3>本条下一动作</h3>
          <p class="muted">选择一个进程任务查看路径、阻塞原因和下一步动作。</p>
        </aside>`;
    }

    return `
      <aside class="art-detail-panel art-action-panel" aria-label="ArtSource 本条下一动作">
        <section class="art-primary-action art-tone-${escapeHtml(task.tone)}">
          <span>${escapeHtml(task.workstreamTitle)}</span>
          <h3>本条下一动作</h3>
          <p>${escapeHtml(task.draftNextAction || task.nextAction || task.workstreamNextAction || "查看任务绑定资产并决定后续处理。")}</p>
          <div class="art-primary-buttons">
            ${renderPrimaryActionButtons(task)}
          </div>
        </section>
        ${renderTaskEditor(task)}
        ${renderTaskDetailSections(task)}
      </aside>`;
  }

  function renderPrimaryActionButtons(task) {
    const preview = getTaskPreview(task);
    const reviewUrl = task.reviewRefs.length > 0 ? toStaticUrl(task.reviewRefs[0]) : "";
    const sourceUrl = task.assets[0] ? task.assets[0].sourceUrl : "";
    return [
      reviewUrl ? `<button type="button" data-art-open-url="${escapeHtml(reviewUrl)}">打开评审/契约</button>` : "",
      preview.url ? `<button type="button" class="secondary" data-art-preview-url="${escapeHtml(preview.url)}" data-art-preview-title="${escapeHtml(task.title || task.taskId)}" data-art-preview-note="${escapeHtml(preview.path || task.previewNote || "ArtSource 真实预览图。")}">放大预览图</button>` : "",
      !reviewUrl && sourceUrl ? `<button type="button" class="secondary" data-art-open-url="${escapeHtml(sourceUrl)}">打开源文件</button>` : ""
    ].filter(Boolean).join("");
  }

  function renderTaskEditor(task) {
    const saveState = task.saveState || { status: "idle", message: "" };
    const busy = saveState.status === "running";
    const saveMessage = saveState.message || (task.isDirty ? "有未保存任务草稿。" : "当前任务与 art_tasks.json 一致。");
    const editOpen = Boolean(task.uiPanels && task.uiPanels.taskEdit) || saveState.status === "error" || saveState.status === "conflict";
    const assetsOpen = Boolean(task.uiPanels && task.uiPanels.taskAssets) || Boolean(task.assetSearch);
    return `
      <section class="art-task-editor art-save-${escapeHtml(saveState.status || "idle")}" aria-label="ArtSource 任务推进">
        <div class="art-editor-heading">
          <div>
            <h4>任务推进</h4>
            <p>${escapeHtml(saveMessage)}</p>
          </div>
          <span>${escapeHtml(task.isDirty ? "草稿" : "已保存")}</span>
        </div>
        <div class="art-segmented" role="group" aria-label="任务状态">
          ${TASK_STATUSES.map(status => `
            <button type="button"
              class="${task.draftStatus === status ? "selected" : ""}"
              data-art-action="art-task-set-status"
              data-art-task-id="${escapeHtml(task.taskId)}"
              data-art-task-value="${escapeHtml(status)}"
              ${busy ? " disabled" : ""}>${escapeHtml(formatStatus(status))}</button>`).join("")}
        </div>
        <div class="art-editor-compact-row">
          <label class="art-editor-field">
            <span>优先级</span>
            <select data-art-task-edit-field="priority" data-art-task-id="${escapeHtml(task.taskId)}"${busy ? " disabled" : ""}>
              ${TASK_PRIORITIES.map(priority => `<option value="${escapeHtml(priority)}"${task.draftPriority === priority ? " selected" : ""}>${escapeHtml(formatPriority(priority))}</option>`).join("")}
            </select>
          </label>
          <div class="art-editor-actions art-editor-actions-compact">
            <button type="button" class="secondary" data-art-action="art-task-reset" data-art-task-id="${escapeHtml(task.taskId)}"${busy || !task.isDirty ? " disabled" : ""}>重置</button>
            <button type="button" data-art-action="art-task-save" data-art-task-id="${escapeHtml(task.taskId)}"${busy || !task.isDirty ? " disabled" : ""}>${busy ? "保存中..." : "保存"}</button>
          </div>
        </div>
        <details class="art-collapse-panel" data-art-panel="taskEdit"${editOpen ? " open" : ""}>
          <summary>
            <span>编辑阻塞与下一动作</span>
            <small>${escapeHtml(task.draftBlockedBy ? "有阻塞" : "无阻塞")}</small>
          </summary>
          ${editOpen ? `<div class="art-collapse-body">
            <label class="art-editor-field">
              <span>阻塞原因</span>
              <textarea rows="2" data-art-task-edit-field="blockedBy" data-art-task-id="${escapeHtml(task.taskId)}" placeholder="无阻塞则留空"${busy ? " disabled" : ""}>${escapeHtml(task.draftBlockedBy)}</textarea>
            </label>
            <label class="art-editor-field">
              <span>下一动作</span>
              <textarea rows="3" data-art-task-edit-field="nextAction" data-art-task-id="${escapeHtml(task.taskId)}" placeholder="写给执行者的下一步"${busy ? " disabled" : ""}>${escapeHtml(task.draftNextAction)}</textarea>
            </label>
            <div class="art-editor-shortcuts">
              <button type="button" class="secondary" data-art-action="art-task-quick-unblock" data-art-task-id="${escapeHtml(task.taskId)}"${busy ? " disabled" : ""}>解除阻塞</button>
              <button type="button" class="secondary" data-art-action="art-task-quick-review" data-art-task-id="${escapeHtml(task.taskId)}"${busy ? " disabled" : ""}>送审</button>
              <button type="button" class="secondary" data-art-action="art-task-quick-complete" data-art-task-id="${escapeHtml(task.taskId)}"${busy ? " disabled" : ""}>标记完成</button>
            </div>
          </div>` : ""}
        </details>
        <details class="art-collapse-panel" data-art-panel="taskAssets"${assetsOpen ? " open" : ""}>
          <summary>
            <span>绑定资产</span>
            <small>${escapeHtml(`${task.draftAssetIds.length} 个已绑定`)}</small>
          </summary>
          ${assetsOpen ? `<div class="art-collapse-body">
            <label class="art-editor-field">
              <span>搜索资产</span>
              <input type="search" data-art-task-edit-field="assetSearch" data-art-task-id="${escapeHtml(task.taskId)}" value="${escapeHtml(task.assetSearch)}" placeholder="搜索 assetId / 资产家族 / 评审状态"${busy ? " disabled" : ""}>
            </label>
            <div class="art-asset-picker">
              ${task.candidateAssets.length === 0 ? `<p class="muted">没有匹配资产。</p>` : task.candidateAssets.map(asset => `
                <label class="art-asset-option">
                  <input type="checkbox"
                    data-art-task-asset-toggle="${escapeHtml(asset.assetId)}"
                    data-art-task-id="${escapeHtml(task.taskId)}"
                    ${task.draftAssetIds.includes(asset.assetId) ? " checked" : ""}
                    ${busy ? " disabled" : ""}>
                  <span>${escapeHtml(asset.assetId)}</span>
                  <small>${escapeHtml(asset.family)} · ${escapeHtml(asset.reviewStatus || "无评审状态")}</small>
                </label>`).join("")}
            </div>
          </div>` : ""}
        </details>
        ${saveState.status === "error" || saveState.status === "conflict" ? `<p class="art-editor-error">${escapeHtml(saveState.error || saveState.message)}</p>` : ""}
      </section>`;
  }

  function renderTaskDetailSections(task) {
    const detailsOpen = Boolean(task.uiPanels && task.uiPanels.taskDetails);
    return `
      <details class="art-collapse-panel art-task-details-panel" data-art-panel="taskDetails"${detailsOpen ? " open" : ""}>
        <summary>
          <span>任务详情、引用与诊断</span>
          <small>${escapeHtml(`${task.assets.length} 个资产 · ${task.reviewRefs.length} 个引用 · ${task.diagnostics.length} 条诊断`)}</small>
        </summary>
        ${detailsOpen ? `<div class="art-collapse-body">
          <dl class="art-detail-list">
            ${renderDetailRow("任务", task.taskId, task.title)}
            ${renderDetailRow("进程", task.workstreamId, task.workstreamTitle)}
            ${renderDetailRow("状态", task.status, task.statusLabel)}
            ${renderDetailRow("优先级", task.priority, task.priorityLabel)}
            ${renderDetailRow("阻塞", task.blockedBy, task.blockedBy ? "有阻塞" : "无阻塞")}
            ${renderDetailRow("资产", task.assetIds.join(", "), `${task.assets.length} 个绑定资产`)}
          </dl>
          ${renderAssetList(task)}
          ${renderReviewRefs(task)}
          <section class="art-diagnostic-list">
            <h4>阻塞 / 诊断</h4>
            ${task.diagnostics.length === 0
              ? `<p class="muted">当前任务没有 ArtSource 数据源诊断。</p>`
              : `<ul>${task.diagnostics.map(renderDiagnostic).join("")}</ul>`}
          </section>
          <div class="art-detail-actions">
            <button type="button" class="secondary" data-art-action="open-browse-art">在资源浏览查看 artSource</button>
            ${task.assets[0] ? `<button type="button" class="secondary" data-art-action="open-debug" data-art-resource-key="${escapeHtml(task.assets[0].key)}">查看首个资产诊断</button>` : ""}
          </div>
        </div>` : ""}
      </details>`;
  }

  function renderTaskThumbnail(task, size) {
    const preview = getTaskPreview(task);
    if (preview.url) {
      return `<span class="art-card-thumb art-card-thumb-${escapeHtml(size)}"><img src="${escapeHtml(preview.url)}" alt=""></span>`;
    }
    return `<span class="art-card-thumb art-card-thumb-${escapeHtml(size)} art-card-thumb-missing"><span>${task.assets.length > 1 ? task.assets.length + "项" : "无预览"}</span></span>`;
  }

  function renderAssetList(task) {
    if (task.assets.length === 0) {
      return `
        <section class="art-linked-assets">
          <h4>绑定资产</h4>
          <p class="muted">这是横向流程任务，没有绑定具体 asset。</p>
        </section>`;
    }
    return `
      <section class="art-linked-assets">
        <h4>绑定资产</h4>
        <div class="art-linked-asset-list">
          ${task.assets.map(asset => `
            <button type="button" class="art-linked-asset" data-art-resource-key="${escapeHtml(asset.key)}">
              <span>${escapeHtml(asset.assetId)}</span>
              <small>${escapeHtml(asset.family)} · ${escapeHtml(asset.reviewStatus)} · ${asset.hasPreview ? "有预览" : "缺预览"}</small>
            </button>`).join("")}
        </div>
      </section>`;
  }

  function renderReviewRefs(task) {
    if (task.reviewRefs.length === 0) return "";
    return `
      <section class="art-linked-assets">
        <h4>评审 / 契约</h4>
        <div class="art-linked-asset-list">
          ${task.reviewRefs.map(ref => `
            <button type="button" class="art-linked-asset" data-art-open-url="${escapeHtml(toStaticUrl(ref))}">
              <span>${escapeHtml(ref)}</span>
              <small>只读打开</small>
            </button>`).join("")}
        </div>
      </section>`;
  }

  function renderDetailRow(label, value, state) {
    return `
      <div>
        <dt>${escapeHtml(label)}</dt>
        <dd>
          <code>${escapeHtml(value || "(empty)")}</code>
          <span>${escapeHtml(state || "")}</span>
        </dd>
      </div>`;
  }

  function renderDiagnostic(diagnostic) {
    const code = pick(diagnostic, "code") || "ART_SOURCE_INFO";
    const message = pick(diagnostic, "message") || pick(diagnostic, "suggestedFix") || "";
    return `<li><strong>${escapeHtml(code)}</strong><span>${escapeHtml(message)}</span></li>`;
  }

  function extractTaskSnapshot(resourcesPayload) {
    const metadata = pick(resourcesPayload, "metadata", "Metadata") || {};
    const raw = stringValue(pick(metadata, "artSourceTaskSnapshotJson", "ArtSourceTaskSnapshotJson"));
    const tasksHash = stringValue(pick(metadata, "artSourceTasksHash", "ArtSourceTasksHash"));
    if (!raw) return { tasksHash, workstreams: [], tasks: [] };
    try {
      const parsed = JSON.parse(raw);
      return {
        schemaVersion: pick(parsed, "schemaVersion") || 0,
        projectId: stringValue(pick(parsed, "projectId")),
        updated: stringValue(pick(parsed, "updated")),
        tasksHash,
        workstreams: asArray(pick(parsed, "workstreams")),
        tasks: asArray(pick(parsed, "tasks"))
      };
    } catch {
      return { tasksHash, workstreams: [], tasks: [] };
    }
  }

  function normalizeWorkstreams(rawWorkstreams) {
    return asArray(rawWorkstreams)
      .map((raw, index) => {
        const status = stringValue(pick(raw, "status")) || "unknown";
        return {
          workstreamId: stringValue(pick(raw, "workstreamId")) || `workstream-${index}`,
          title: stringValue(pick(raw, "title")) || stringValue(pick(raw, "workstreamId")) || `进程 ${index + 1}`,
          phase: stringValue(pick(raw, "phase")) || "art",
          status,
          statusLabel: formatStatus(status),
          tone: toneForStatus(status),
          summary: stringValue(pick(raw, "summary")),
          nextAction: stringValue(pick(raw, "nextAction")),
          sortOrder: numericValue(pick(raw, "sortOrder"), index)
        };
      })
      .sort((left, right) => left.sortOrder - right.sortOrder || compareText(left.title, right.title));
  }

  function normalizeTasks(rawTasks, workstreams, itemByAssetId) {
    const workstreamById = new Map(workstreams.map(workstream => [workstream.workstreamId, workstream]));
    return asArray(rawTasks).map((raw, index) => {
      const taskId = stringValue(pick(raw, "taskId")) || `task-${index}`;
      const workstreamId = stringValue(pick(raw, "workstreamId"));
      const workstream = workstreamById.get(workstreamId) || {
        workstreamId,
        title: workstreamId || "未分配进程",
        nextAction: "",
        sortOrder: 999,
        tone: "warn"
      };
      const assetIds = asArray(pick(raw, "assetIds")).map(stringValue).filter(Boolean);
      const assets = assetIds.map(assetId => itemByAssetId.get(assetId)).filter(Boolean);
      const status = stringValue(pick(raw, "status")) || "unknown";
      const priority = stringValue(pick(raw, "priority")) || "medium";
      const blockedBy = stringValue(pick(raw, "blockedBy"));
      const previewPath = stringValue(pick(raw, "previewPath"));
      const previewUrl = toBoolean(pick(raw, "previewExists")) ? stringValue(pick(raw, "previewUrl")) : "";
      const task = {
        key: `task:${taskId}`,
        taskId,
        workstreamId,
        workstreamTitle: workstream.title,
        workstreamPhase: workstream.phase || "art",
        workstreamNextAction: workstream.nextAction,
        workstreamSortOrder: workstream.sortOrder,
        title: stringValue(pick(raw, "title")) || taskId,
        status,
        statusLabel: formatStatus(status),
        priority,
        priorityLabel: formatPriority(priority),
        tone: toneForStatus(status),
        assetIds,
        assets,
        blockedBy,
        isBlocked: status.toLowerCase() === "blocked" || Boolean(blockedBy),
        nextAction: stringValue(pick(raw, "nextAction")),
        previewPath,
        previewUrl,
        previewExists: Boolean(previewUrl),
        previewNote: previewPath ? "任务级预览图。" : "",
        reviewRefs: asArray(pick(raw, "reviewRefs")).map(stringValue).filter(Boolean),
        diagnostics: assets.flatMap(asset => asset.diagnostics),
        raw
      };
      task.searchText = buildTaskSearchText(task);
      return task;
    });
  }

  function applyTaskDraft(task, taskDrafts) {
    const draft = Object.assign(createBaseDraft(task), pick(taskDrafts, task.taskId) || {});
    draft.status = normalizeTaskStatus(draft.status, task.status);
    draft.priority = normalizeTaskPriority(draft.priority, task.priority);
    draft.assetIds = normalizeAssetIds(draft.assetIds);
    draft.assetSearch = stringValue(draft.assetSearch);

    task.draft = draft;
    task.draftStatus = draft.status;
    task.draftStatusLabel = formatStatus(draft.status);
    task.draftPriority = draft.priority;
    task.draftPriorityLabel = formatPriority(draft.priority);
    task.draftBlockedBy = stringValue(draft.blockedBy);
    task.draftNextAction = stringValue(draft.nextAction);
    task.draftAssetIds = draft.assetIds;
    task.assetSearch = draft.assetSearch;
    task.isDirty = isDraftDirty(task, draft);
  }

  function annotateTaskBlockers(tasks) {
    const graphTasks = asArray(tasks);
    const byTaskId = new Map(graphTasks.map(task => [task.taskId, task]));
    const byWorkstreamId = new Map(graphTasks.map(task => [task.workstreamId, task]));
    graphTasks.forEach(task => {
      const dependencyText = getEffectiveBlockedBy(task);
      const dependencies = findDependencyTasks(dependencyText, task, byTaskId, byWorkstreamId);
      task.dependencyTaskIds = dependencies.map(dependency => dependency.taskId);
      task.dependencyTaskTitles = dependencies.map(dependency => dependency.title || dependency.taskId);
      task.externalBlocker = dependencyText && dependencies.length === 0 ? dependencyText : "";
      task.blockerKind = dependencies.length > 0 ? "task" : task.externalBlocker ? "external" : "";
      task.blockedSummary = task.externalBlocker || task.dependencyTaskTitles.join("、") || dependencyText;
      task.isBlocked = getEffectiveStatus(task) === "blocked" || Boolean(dependencyText);
    });
  }

  function getEffectiveStatus(task) {
    if (!task) return "";
    if (task.draft) return stringValue(task.draftStatus || task.status);
    return stringValue(task.status);
  }

  function getEffectiveBlockedBy(task) {
    if (!task) return "";
    if (task.draft) return stringValue(task.draftBlockedBy);
    return stringValue(task.blockedBy);
  }

  function createBaseDraft(task) {
    return {
      status: task.status || "waiting",
      priority: task.priority || "medium",
      blockedBy: task.blockedBy || "",
      nextAction: task.nextAction || "",
      assetIds: task.assetIds ? task.assetIds.slice() : [],
      assetSearch: ""
    };
  }

  function buildCandidateAssets(artItems, task) {
    const query = normalizeSearch(task.assetSearch);
    const selected = new Set(task.draftAssetIds || []);
    return asArray(artItems)
      .filter(asset => {
        if (selected.has(asset.assetId)) return true;
        if (!query) return true;
        return normalizeSearch([
          asset.assetId,
          asset.family,
          asset.stage,
          asset.reviewStatus,
          asset.resourceKey,
          asset.sourcePath
        ].join(" ")).includes(query);
      })
      .slice(0, 80);
  }

  function buildCreateCandidateAssets(artItems, draft) {
    const query = normalizeSearch(draft.assetSearch);
    const selected = new Set(draft.assetIds || []);
    return asArray(artItems)
      .filter(asset => {
        if (selected.has(asset.assetId)) return true;
        if (!query) return true;
        return normalizeSearch([
          asset.assetId,
          asset.family,
          asset.stage,
          asset.reviewStatus,
          asset.resourceKey,
          asset.sourcePath
        ].join(" ")).includes(query);
      })
      .slice(0, 80);
  }

  function normalizeCreateDraft(value, workstreams, artItems) {
    const base = createDefaultCreateDraft();
    const draft = Object.assign(base, pickObject(value));
    draft.kind = draft.kind === "workstream" ? "workstream" : "task";
    draft.workstreamId = stringValue(draft.workstreamId).trim();
    draft.taskId = stringValue(draft.taskId).trim();
    draft.title = stringValue(draft.title);
    draft.phase = WORKSTREAM_PHASES.includes(stringValue(draft.phase)) ? stringValue(draft.phase) : "art";
    draft.status = normalizeTaskStatus(draft.status, "active");
    draft.priority = normalizeTaskPriority(draft.priority, "medium");
    draft.summary = stringValue(draft.summary);
    draft.blockedBy = stringValue(draft.blockedBy);
    draft.nextAction = stringValue(draft.nextAction);
    draft.assetIds = normalizeAssetIds(draft.assetIds);
    draft.assetSearch = stringValue(draft.assetSearch);
    draft.previewPath = stringValue(draft.previewPath);
    draft.reviewRefsText = stringValue(draft.reviewRefsText);
    if (draft.kind === "task" && !draft.workstreamId) {
      draft.workstreamId = asArray(workstreams)[0]?.workstreamId || "";
    }
    const knownAssets = new Set(asArray(artItems).map(asset => asset.assetId));
    draft.assetIds = draft.assetIds.filter(assetId => knownAssets.has(assetId));
    return draft;
  }

  function normalizeCreateState(value) {
    return {
      status: stringValue(pick(value, "status")) || "idle",
      kind: stringValue(pick(value, "kind")),
      taskId: stringValue(pick(value, "taskId")),
      workstreamId: stringValue(pick(value, "workstreamId")),
      error: stringValue(pick(value, "error")),
      message: stringValue(pick(value, "message"))
    };
  }

  function normalizeUiPanels(value) {
    return {
      create: toBoolean(pick(value, "create")),
      filters: toBoolean(pick(value, "filters")),
      taskEdit: toBoolean(pick(value, "taskEdit")),
      taskAssets: toBoolean(pick(value, "taskAssets")),
      taskDetails: toBoolean(pick(value, "taskDetails"))
    };
  }

  function countActiveFilters(filters) {
    const current = Object.assign(createDefaultFilters(), filters || {});
    let count = 0;
    if (stringValue(current.search).trim()) count += 1;
    ["workstream", "taskStatus", "priority", "blocked", "family", "reviewStatus"].forEach(key => {
      if (stringValue(current[key]) && current[key] !== "all") count += 1;
    });
    return count;
  }

  function updateCreateDraftField(model, draftValue, field, value) {
    const draft = normalizeCreateDraft(draftValue, asArray(pick(model, "workstreams")), asArray(pick(model, "allItems")));
    if (field === "kind") {
      draft.kind = value === "workstream" ? "workstream" : "task";
      if (draft.kind === "task" && !draft.workstreamId) draft.workstreamId = asArray(pick(model, "workstreams"))[0]?.workstreamId || "";
    }
    if (field === "workstreamId") draft.workstreamId = stringValue(value).trim();
    if (field === "taskId") draft.taskId = stringValue(value).trim();
    if (field === "title") draft.title = stringValue(value);
    if (field === "phase") draft.phase = WORKSTREAM_PHASES.includes(stringValue(value)) ? stringValue(value) : draft.phase;
    if (field === "status") draft.status = normalizeTaskStatus(value, draft.status);
    if (field === "priority") draft.priority = normalizeTaskPriority(value, draft.priority);
    if (field === "summary") draft.summary = stringValue(value);
    if (field === "blockedBy") draft.blockedBy = stringValue(value);
    if (field === "nextAction") draft.nextAction = stringValue(value);
    if (field === "assetSearch") draft.assetSearch = stringValue(value);
    if (field === "previewPath") draft.previewPath = stringValue(value);
    if (field === "reviewRefsText") draft.reviewRefsText = stringValue(value);
    return draft;
  }

  function updateCreateDraftAsset(model, draftValue, assetId, checked) {
    const draft = normalizeCreateDraft(draftValue, asArray(pick(model, "workstreams")), asArray(pick(model, "allItems")));
    const values = normalizeAssetIds(draft.assetIds);
    const exists = values.includes(assetId);
    if (checked && assetId && !exists) values.push(assetId);
    if (!checked && exists) values.splice(values.indexOf(assetId), 1);
    draft.assetIds = values;
    return draft;
  }

  function buildCreateRequest(model, draftValue) {
    const draft = normalizeCreateDraft(draftValue, asArray(pick(model, "workstreams")), asArray(pick(model, "allItems")));
    const validation = getCreateValidationMessage(model, draft);
    if (validation) return null;
    const request = {
      kind: draft.kind,
      workstreamId: draft.workstreamId,
      title: draft.title.trim(),
      status: draft.status,
      nextAction: draft.nextAction.trim(),
      expectedHash: stringValue(pick(model, "tasksHash"))
    };
    if (draft.kind === "workstream") {
      request.phase = draft.phase;
      request.summary = draft.summary.trim();
      return request;
    }

    request.taskId = draft.taskId;
    request.priority = draft.priority;
    request.blockedBy = draft.blockedBy.trim();
    request.assetIds = normalizeAssetIds(draft.assetIds);
    request.previewPath = draft.previewPath.trim();
    request.reviewRefs = splitMultiValue(draft.reviewRefsText);
    return request;
  }

  function clearCreateDraft(previousDraft, payload) {
    const next = createDefaultCreateDraft();
    if (pick(payload, "kind") === "workstream") {
      next.kind = "task";
      next.workstreamId = stringValue(pick(payload, "workstreamId"));
    } else if (pick(previousDraft, "kind") === "task") {
      next.kind = "task";
      next.workstreamId = stringValue(pick(previousDraft, "workstreamId"));
    }
    return next;
  }

  function getCreateValidationMessage(model, draftValue) {
    const draft = normalizeCreateDraft(draftValue, asArray(pick(model, "workstreams")), asArray(pick(model, "allItems")));
    const taskIds = new Set(asArray(pick(model, "allTasks")).map(task => task.taskId));
    const workstreamIds = new Set(asArray(pick(model, "workstreams")).map(workstream => workstream.workstreamId));
    if (draft.kind === "workstream") {
      if (!isValidId(draft.workstreamId)) return "进程 id 只能使用 a-z、0-9、.、_、-。";
      if (workstreamIds.has(draft.workstreamId)) return "进程 id 已存在。";
      if (!draft.title.trim()) return "必须填写进程标题。";
      return "";
    }

    if (!isValidId(draft.taskId)) return "任务 id 只能使用 a-z、0-9、.、_、-。";
    if (taskIds.has(draft.taskId)) return "任务 id 已存在。";
    if (!draft.workstreamId || !workstreamIds.has(draft.workstreamId)) return "必须选择已有进程。";
    if (!draft.title.trim()) return "必须填写任务标题。";
    return "";
  }

  function updateTaskDraftField(model, drafts, taskId, field, value) {
    const task = findTaskById(model, taskId);
    if (!task) return Object.assign({}, drafts || {});

    const next = Object.assign({}, drafts || {});
    const draft = Object.assign(createBaseDraft(task), pick(next, task.taskId) || {});
    if (field === "status") draft.status = normalizeTaskStatus(value, draft.status);
    if (field === "priority") draft.priority = normalizeTaskPriority(value, draft.priority);
    if (field === "blockedBy") draft.blockedBy = stringValue(value);
    if (field === "nextAction") draft.nextAction = stringValue(value);
    if (field === "assetSearch") draft.assetSearch = stringValue(value);
    next[task.taskId] = normalizeDraftForStorage(task, draft);
    return next;
  }

  function updateTaskDraftAsset(model, drafts, taskId, assetId, checked) {
    const task = findTaskById(model, taskId);
    if (!task || !assetId) return Object.assign({}, drafts || {});

    const next = Object.assign({}, drafts || {});
    const draft = Object.assign(createBaseDraft(task), pick(next, task.taskId) || {});
    const values = normalizeAssetIds(draft.assetIds);
    const exists = values.includes(assetId);
    if (checked && !exists) values.push(assetId);
    if (!checked && exists) values.splice(values.indexOf(assetId), 1);
    draft.assetIds = values;
    next[task.taskId] = normalizeDraftForStorage(task, draft);
    return next;
  }

  function applyTaskQuickAction(model, drafts, taskId, action) {
    const task = findTaskById(model, taskId);
    if (!task) return Object.assign({}, drafts || {});

    const next = Object.assign({}, drafts || {});
    const draft = Object.assign(createBaseDraft(task), pick(next, task.taskId) || {});
    if (action === "art-task-quick-unblock") {
      draft.status = "active";
      draft.blockedBy = "";
    }
    if (action === "art-task-quick-review") {
      draft.status = "review";
      draft.blockedBy = "";
      if (!stringValue(draft.nextAction)) draft.nextAction = "执行评审并记录结论。";
    }
    if (action === "art-task-quick-complete") {
      draft.status = "completed";
      draft.blockedBy = "";
      if (!stringValue(draft.nextAction)) draft.nextAction = "任务已完成，保留记录供后续追溯。";
    }
    next[task.taskId] = normalizeDraftForStorage(task, draft);
    return next;
  }

  function buildTaskUpdateRequest(model, drafts, taskId) {
    const task = findTaskById(model, taskId);
    if (!task || !model || !model.tasksHash) return null;

    const draft = Object.assign(createBaseDraft(task), pick(drafts, task.taskId) || {});
    const normalized = normalizeDraftForStorage(task, draft);
    if (!isDraftDirty(task, normalized)) return null;

    return {
      taskId: task.taskId,
      status: normalized.status,
      priority: normalized.priority,
      blockedBy: normalized.blockedBy,
      nextAction: normalized.nextAction,
      assetIds: normalized.assetIds,
      expectedHash: model.tasksHash
    };
  }

  function clearTaskDraft(drafts, taskId) {
    const next = Object.assign({}, drafts || {});
    if (taskId) delete next[taskId];
    return next;
  }

  function normalizeDraftForStorage(task, draft) {
    return {
      status: normalizeTaskStatus(draft.status, task.status),
      priority: normalizeTaskPriority(draft.priority, task.priority),
      blockedBy: stringValue(draft.blockedBy),
      nextAction: stringValue(draft.nextAction),
      assetIds: normalizeAssetIds(draft.assetIds),
      assetSearch: stringValue(draft.assetSearch)
    };
  }

  function isDraftDirty(task, draft) {
    const base = createBaseDraft(task);
    return draft.status !== base.status ||
      draft.priority !== base.priority ||
      stringValue(draft.blockedBy) !== stringValue(base.blockedBy) ||
      stringValue(draft.nextAction) !== stringValue(base.nextAction) ||
      !arraysEqual(normalizeAssetIds(draft.assetIds), normalizeAssetIds(base.assetIds));
  }

  function findTaskById(model, taskId) {
    return asArray(pick(model, "allTasks")).find(task => task.taskId === taskId) || null;
  }

  function normalizeTaskSaveState(value) {
    return {
      status: stringValue(pick(value, "status")) || "idle",
      taskId: stringValue(pick(value, "taskId")),
      error: stringValue(pick(value, "error")),
      message: stringValue(pick(value, "message"))
    };
  }

  function normalizeArtItem(item) {
    const metadata = pick(item, "metadata") || {};
    const diagnostics = asArray(pick(item, "allDiagnostics")).length > 0
      ? asArray(pick(item, "allDiagnostics"))
      : asArray(pick(item, "diagnostics"));
    const raw = {
      key: stringValue(pick(item, "key", "resourceId", "stableId")),
      assetId: firstString(pick(metadata, "assetId"), pick(item, "canonicalIdentity"), pick(item, "displayName")),
      displayName: stringValue(pick(item, "displayName")) || firstString(pick(metadata, "assetId"), pick(item, "stableId")),
      family: firstString(pick(metadata, "family"), pick(item, "kind")),
      stage: firstString(pick(metadata, "stage"), pick(item, "usage")),
      sourcePath: firstString(pick(metadata, "sourcePath"), pick(item, "sourcePath")),
      exportPath: stringValue(pick(metadata, "exportPath")),
      unityTargetPath: firstString(pick(metadata, "unityTargetPath"), pick(item, "unityAssetPath")),
      resourceKey: firstString(pick(metadata, "resourceKey"), pick(item, "resourceKey")),
      reviewStatus: firstString(pick(metadata, "reviewStatus"), pick(item, "importStatus")),
      previewPath: stringValue(pick(metadata, "previewPath")),
      previewUrl: stringValue(pick(metadata, "previewUrl")),
      previewExists: toBoolean(pick(metadata, "previewExists")),
      previewRole: stringValue(pick(metadata, "previewRole")),
      previewNote: stringValue(pick(metadata, "previewNote")),
      sourceExists: toBoolean(pick(metadata, "sourceExists")),
      workstreamIds: splitCsv(pick(metadata, "workstreamIds")),
      taskIds: splitCsv(pick(metadata, "taskIds")),
      activeTaskCount: numericValue(pick(metadata, "activeTaskCount"), 0),
      blockedTaskCount: numericValue(pick(metadata, "blockedTaskCount"), 0),
      diagnostics,
      providerId: firstString(pick(item, "providerId"), pick(item, "sourceProviderId")),
      runtimeAvailability: stringValue(pick(item, "runtimeAvailability"))
    };
    raw.hasPreview = Boolean(raw.previewExists && raw.previewUrl);
    raw.sourceUrl = toStaticUrl(raw.sourcePath);
    raw.searchText = [
      raw.assetId,
      raw.displayName,
      raw.family,
      raw.stage,
      raw.sourcePath,
      raw.exportPath,
      raw.unityTargetPath,
      raw.resourceKey,
      raw.reviewStatus,
      raw.previewPath,
      raw.previewRole,
      raw.previewNote,
      raw.workstreamIds.join(" "),
      raw.taskIds.join(" "),
      raw.diagnostics.map(diagnostic => `${pick(diagnostic, "code")} ${pick(diagnostic, "message")}`).join(" ")
    ].join(" ").toLowerCase();
    return raw;
  }

  function getTaskPreview(task) {
    if (!task) return { path: "", url: "" };
    if (task.previewUrl) return { path: task.previewPath, url: task.previewUrl };
    const firstPreview = task.assets.find(asset => asset.hasPreview);
    return firstPreview ? { path: firstPreview.previewPath, url: firstPreview.previewUrl } : { path: "", url: "" };
  }

  function buildTaskSearchText(task) {
    return [
      task.taskId,
      task.title,
      task.workstreamId,
      task.workstreamTitle,
      task.status,
      task.priority,
      task.blockedBy,
      task.nextAction,
      task.previewPath,
      task.assetIds.join(" "),
      task.reviewRefs.join(" "),
      task.assets.map(asset => asset.searchText).join(" ")
    ].join(" ").toLowerCase();
  }

  function matchesTaskFilters(task, filters) {
    const search = String(filters.search || "").trim().toLowerCase();
    if (filters.workstream !== "all" && task.workstreamId !== filters.workstream) return false;
    if (filters.taskStatus !== "all" && task.status !== filters.taskStatus) return false;
    if (filters.priority !== "all" && task.priority !== filters.priority) return false;
    if (filters.blocked === "blocked" && !task.isBlocked) return false;
    if (filters.blocked === "unblocked" && task.isBlocked) return false;
    if (filters.family !== "all" && !task.assets.some(asset => asset.family === filters.family)) return false;
    if (filters.reviewStatus !== "all" && !task.assets.some(asset => asset.reviewStatus === filters.reviewStatus)) return false;
    if (search && !task.searchText.includes(search)) return false;
    return true;
  }

  function findSelectedTask(tasks, filteredTasks, selectedKey) {
    const key = stringValue(selectedKey);
    return filteredTasks.find(task => task.key === key)
      || tasks.find(task => task.key === key)
      || filteredTasks.find(task => task.assets.some(asset => asset.key === key || asset.assetId === key))
      || tasks.find(task => task.assets.some(asset => asset.key === key || asset.assetId === key))
      || filteredTasks[0]
      || tasks[0]
      || null;
  }

  function buildDependencyGraph(tasks, selectedTask) {
    const graphTasks = asArray(tasks);
    const byTaskId = new Map(graphTasks.map(task => [task.taskId, task]));
    const byWorkstreamId = new Map(graphTasks.map(task => [task.workstreamId, task]));
    const incomingByTaskId = new Map(graphTasks.map(task => [task.taskId, []]));
    const outgoingByTaskId = new Map(graphTasks.map(task => [task.taskId, []]));
    const externalBlockers = new Map();
    const edges = [];
    graphTasks.forEach(task => {
      const dependencyText = getEffectiveBlockedBy(task);
      const dependencies = findDependencyTasks(dependencyText, task, byTaskId, byWorkstreamId);
      dependencies.forEach(dependency => {
        const edge = { from: dependency.taskId, to: task.taskId };
        edges.push(edge);
        incomingByTaskId.get(task.taskId)?.push(edge);
        outgoingByTaskId.get(dependency.taskId)?.push(edge);
      });
      if (dependencyText && dependencies.length === 0) externalBlockers.set(task.taskId, dependencyText);
    });

    const levels = new Map(graphTasks.map(task => [task.taskId, 0]));
    for (let pass = 0; pass < graphTasks.length; pass += 1) {
      let changed = false;
      edges.forEach(edge => {
        const nextLevel = (levels.get(edge.from) || 0) + 1;
        if ((levels.get(edge.to) || 0) < nextLevel) {
          levels.set(edge.to, nextLevel);
          changed = true;
        }
      });
      if (!changed) break;
    }

    const groups = new Map();
    graphTasks
      .slice()
      .sort(compareTasks)
      .forEach(task => {
        const level = levels.get(task.taskId) || 0;
        if (!groups.has(level)) groups.set(level, []);
        groups.get(level).push(task);
      });
    const levelValues = Array.from(groups.keys()).sort((left, right) => left - right);
    const maxRows = Math.max(1, ...Array.from(groups.values()).map(group => group.length));
    const height = Math.max(230, maxRows * 88 + 50);
    const xByLevel = new Map();
    levelValues.forEach((level, index) => {
      const x = levelValues.length === 1 ? 500 : 110 + index * (780 / Math.max(1, levelValues.length - 1));
      xByLevel.set(level, Math.round(x));
    });

    const positions = new Map();
    levelValues.forEach(level => {
      const group = groups.get(level) || [];
      group.forEach((task, index) => {
        const y = group.length === 1 ? height / 2 : 42 + index * ((height - 84) / Math.max(1, group.length - 1));
        positions.set(task.taskId, {
          x: xByLevel.get(level) || 500,
          y: Math.round(y)
        });
      });
    });

    const selectedId = selectedTask ? selectedTask.taskId : "";
    const nodes = graphTasks.map(task => {
      const position = positions.get(task.taskId) || { x: 500, y: height / 2 };
      return {
        taskId: task.taskId,
        workstreamId: task.workstreamId,
        workstreamTitle: task.workstreamTitle,
        title: task.title || task.taskId,
        statusLabel: task.draftStatusLabel || task.statusLabel,
        phaseLabel: formatPhase(task.workstreamPhase),
        tone: task.tone,
        externalBlocker: externalBlockers.get(task.taskId) || "",
        hasIncoming: (incomingByTaskId.get(task.taskId) || []).length > 0,
        hasOutgoing: (outgoingByTaskId.get(task.taskId) || []).length > 0,
        isSelected: task.taskId === selectedId,
        left: Math.round((position.x / 1000) * 1000) / 10,
        top: Math.round((position.y / height) * 1000) / 10,
        x: position.x,
        y: position.y
      };
    });
    const nodeByTaskId = new Map(nodes.map(node => [node.taskId, node]));
    const graphEdges = edges
      .map(edge => {
        const from = nodeByTaskId.get(edge.from);
        const to = nodeByTaskId.get(edge.to);
        if (!from || !to) return null;
        const fromX = from.x + 88;
        const toX = to.x - 88;
        const midX = (fromX + toX) / 2;
        return {
          fromX: Math.round(fromX),
          fromY: Math.round(from.y),
          toX: Math.round(toX),
          toY: Math.round(to.y),
          midX: Math.round(midX),
          isSelected: from.taskId === selectedId || to.taskId === selectedId
        };
      })
      .filter(Boolean);
    return { nodes, edges: graphEdges, height };
  }

  function findDependencyTasks(dependencyText, task, byTaskId, byWorkstreamId) {
    const text = stringValue(dependencyText);
    if (!text) return [];
    const dependencies = [];
    byTaskId.forEach((candidate, taskId) => {
      if (candidate.taskId !== task.taskId && text.includes(taskId)) dependencies.push(candidate);
    });
    byWorkstreamId.forEach((candidate, workstreamId) => {
      if (candidate.taskId !== task.taskId && text.includes(workstreamId) && !dependencies.includes(candidate)) {
        dependencies.push(candidate);
      }
    });
    return dependencies.sort(compareTasks);
  }

  function buildFilterOptions(artItems, workstreams, tasks) {
    return {
      workstream: [["all", "全部"], ...workstreams.map(workstream => [workstream.workstreamId, workstream.title])],
      taskStatus: buildValueOptions(tasks, "status"),
      priority: buildValueOptions(tasks, "priority"),
      family: buildValueOptions(artItems, "family"),
      reviewStatus: buildValueOptions(artItems, "reviewStatus")
    };
  }

  function buildValueOptions(items, key) {
    const values = Array.from(new Set(items.map(item => item[key]).filter(Boolean))).sort(compareText);
    return [["all", "全部"], ...values.map(value => [value, formatOptionValue(key, value)])];
  }

  function formatOptionValue(key, value) {
    if (key === "status") return formatStatus(value);
    if (key === "priority") return formatPriority(value);
    return value;
  }

  function compareTasks(left, right) {
    const stream = left.workstreamSortOrder - right.workstreamSortOrder;
    if (stream !== 0) return stream;
    const status = statusRank(left.status) - statusRank(right.status);
    if (status !== 0) return status;
    const priority = priorityRank(left.priority) - priorityRank(right.priority);
    if (priority !== 0) return priority;
    return compareText(left.title || left.taskId, right.title || right.taskId);
  }

  function compareArtItems(left, right) {
    const family = compareText(left.family, right.family);
    if (family !== 0) return family;
    return compareText(left.assetId || left.displayName, right.assetId || right.displayName);
  }

  function statusRank(status) {
    const value = String(status || "").toLowerCase();
    if (value === "blocked") return 0;
    if (value === "active") return 1;
    if (value === "waiting") return 2;
    if (value === "ready") return 3;
    if (value === "review") return 4;
    if (value === "done" || value === "completed") return 5;
    return 6;
  }

  function priorityRank(priority) {
    const value = String(priority || "").toLowerCase();
    if (value === "critical") return 0;
    if (value === "high") return 1;
    if (value === "medium") return 2;
    if (value === "low") return 3;
    return 4;
  }

  function toneForStatus(status) {
    const value = String(status || "").toLowerCase();
    if (value === "blocked") return "warn";
    if (value === "active") return "ok";
    if (value === "waiting") return "info";
    if (value === "ready") return "ok";
    if (value === "review") return "info";
    if (value === "done" || value === "completed") return "neutral";
    return "neutral";
  }

  function formatStatus(status) {
    const value = String(status || "").toLowerCase();
    if (value === "blocked") return "阻塞";
    if (value === "active") return "进行中";
    if (value === "waiting") return "等待";
    if (value === "ready") return "就绪";
    if (value === "review") return "评审中";
    if (value === "done" || value === "completed") return "已完成";
    return status || "未知";
  }

  function formatPriority(priority) {
    const value = String(priority || "").toLowerCase();
    if (value === "critical") return "最高";
    if (value === "high") return "高";
    if (value === "medium") return "中";
    if (value === "low") return "低";
    return priority || "中";
  }

  function formatPhase(phase) {
    const value = String(phase || "").toLowerCase();
    if (value === "story") return "故事";
    if (value === "reference") return "参考图";
    if (value === "source") return "源文件";
    if (value === "export") return "导出";
    if (value === "unity-import") return "Unity 导入";
    if (value === "review") return "评审";
    if (value === "legacy") return "旧参考";
    if (value === "handoff") return "交接";
    return phase || "美术";
  }

  function isValidId(value) {
    const text = String(value || "").trim();
    if (!text) return false;
    return /^[a-z0-9._-]+$/.test(text);
  }

  function isArtSourceItem(item) {
    const metadata = pick(item, "metadata") || {};
    const providerId = firstString(pick(item, "providerId"), pick(item, "sourceProviderId"));
    return providerId === PROVIDER_ID || Boolean(pick(metadata, "artSourceManifestPath"));
  }

  function toStaticUrl(path) {
    const value = String(path || "").trim();
    if (!value) return "";
    return "/" + value.replace(/\\/g, "/").replace(/^\/+/, "").split("/").map(encodeURIComponent).join("/");
  }

  function compareText(left, right) {
    return String(left || "").localeCompare(String(right || ""), "zh-Hans-CN");
  }

  function asArray(value) {
    return Array.isArray(value) ? value : [];
  }

  function pickObject(value) {
    return value && typeof value === "object" ? value : {};
  }

  function splitCsv(value) {
    return String(value || "").split(",").map(part => part.trim()).filter(Boolean);
  }

  function pick(object, ...keys) {
    if (!object) return undefined;
    for (const key of keys) {
      if (Object.prototype.hasOwnProperty.call(object, key) && object[key] !== undefined && object[key] !== null) {
        return object[key];
      }
    }
    return undefined;
  }

  function firstString(...values) {
    for (const value of values) {
      const text = stringValue(value);
      if (text) return text;
    }
    return "";
  }

  function stringValue(value) {
    if (value === undefined || value === null) return "";
    return String(value);
  }

  function numericValue(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
  }

  function toBoolean(value) {
    if (typeof value === "boolean") return value;
    return String(value || "").toLowerCase() === "true";
  }

  function normalizeSearch(value) {
    return String(value || "").trim().toLowerCase();
  }

  function normalizeImagePreview(value) {
    return {
      url: stringValue(pick(value, "url")),
      title: stringValue(pick(value, "title")),
      note: stringValue(pick(value, "note"))
    };
  }

  function normalizeTaskStatus(value, fallback) {
    const status = String(value || "").trim().toLowerCase();
    return TASK_STATUSES.includes(status) ? status : normalizeTaskStatus(fallback, "waiting");
  }

  function normalizeTaskPriority(value, fallback) {
    const priority = String(value || "").trim().toLowerCase();
    return TASK_PRIORITIES.includes(priority) ? priority : normalizeTaskPriority(fallback, "medium");
  }

  function normalizeAssetIds(values) {
    const result = [];
    const seen = new Set();
    asArray(values).forEach(value => {
      const text = String(value || "").trim();
      if (!text || seen.has(text)) return;
      seen.add(text);
      result.push(text);
    });
    return result;
  }

  function splitMultiValue(value) {
    const text = String(value || "");
    return normalizeAssetIds(text.split(/[\n,]/g));
  }

  function arraysEqual(left, right) {
    const leftValues = asArray(left);
    const rightValues = asArray(right);
    if (leftValues.length !== rightValues.length) return false;
    for (let i = 0; i < leftValues.length; i += 1) {
      if (leftValues[i] !== rightValues[i]) return false;
    }
    return true;
  }

  function normalizeGraphZoom(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return 1;
    return Math.min(1.8, Math.max(0.7, Math.round(number * 10) / 10));
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  global.MxResourceArtSourceStatus = {
    createDefaultFilters,
    createDefaultCreateDraft,
    createModel,
    render,
    updateCreateDraftField,
    updateCreateDraftAsset,
    buildCreateRequest,
    clearCreateDraft,
    getCreateValidationMessage,
    updateTaskDraftField,
    updateTaskDraftAsset,
    applyTaskQuickAction,
    buildTaskUpdateRequest,
    clearTaskDraft,
    TASK_CODES
  };
})(window);
