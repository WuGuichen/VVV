// Browser source-preview playback for the read-only Resource Library surface.
function playSelectedAudioPreview() {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;
  const preview = getAudioPreviewInfo(row);
  if (!preview.url) {
    audioPreviewState = {
      resourceKey: row.item.key,
      status: "blocked",
      message: preview.message,
      url: ""
    };
    renderAudioWorkspace();
    return;
  }

  if (!audioPreviewElement) {
    audioPreviewElement = new Audio();
    audioPreviewElement.preload = "auto";
  }

  audioPreviewElement.pause();
  audioPreviewElement.currentTime = 0;
  audioPreviewElement.src = preview.url;
  audioPreviewElement.onended = () => {
    audioPreviewState = {
      resourceKey: row.item.key,
      status: "idle",
      message: "试听结束",
      url: preview.url
    };
    renderAudioWorkspace();
  };
  audioPreviewElement.onerror = () => {
    audioPreviewState = {
      resourceKey: row.item.key,
      status: "error",
      message: "源文件无法播放；可能路径不存在、格式不被浏览器支持，或需要重新导出 preview artifact。",
      url: preview.url
    };
    renderAudioWorkspace();
  };

  audioPreviewState = {
    resourceKey: row.item.key,
    status: "playing",
    message: "正在试听源文件",
    url: preview.url
  };
  renderAudioWorkspace();
  audioPreviewElement.play().catch(error => {
    audioPreviewState = {
      resourceKey: row.item.key,
      status: "error",
      message: `浏览器拒绝播放：${String(error?.message || error)}`,
      url: preview.url
    };
    renderAudioWorkspace();
  });
}

function stopAudioPreview(message) {
  if (audioPreviewElement) {
    audioPreviewElement.pause();
    audioPreviewElement.currentTime = 0;
  }
  audioPreviewState = {
    resourceKey: audioPreviewState.resourceKey,
    status: "idle",
    message: message || "已停止试听",
    url: audioPreviewState.url
  };
  renderAudioWorkspace();
}
