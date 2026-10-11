# UPM-P6 Embedded Package 路径迁移

> Status: Current preview migration
>
> Applies to: `0.8.0-preview.1` source layout cutover

UPM-P6 只改变 package 在仓库中的 canonical subpath，不改变 package ID、程序集名、GUID、公共 API 或运行时语义。

## Consumer manifest

Local checkout before:

```json
"com.vincent.wgameframework": "file:../../WGameFramework/Assets/Scripts/MxFramework"
```

Local checkout after:

```json
"com.vincent.wgameframework": "file:../../WGameFramework/Packages/com.vincent.wgameframework"
```

Pinned Git before:

```json
"com.vincent.wgameframework": "git+<repository>.git?path=/Assets/Scripts/MxFramework#<full-40-character-sha>"
```

Pinned Git after:

```json
"com.vincent.wgameframework": "git+<repository>.git?path=/Packages/com.vincent.wgameframework#<full-40-character-sha>"
```

`?path=` 必须位于 `#revision` 前；新的 subpath 只能和包含 P6 cutover 的候选 SHA 一起使用。

## 升级步骤

1. 提交并推送包含新 package root 的框架候选，记录完整 SHA。
2. 提交 Consumer 当前改动；不要在脏 Consumer 上删除生成状态。
3. 修改 `Packages/manifest.json` 的 local `file:` 或 Git `?path=`。
4. 删除该 Consumer 的 `Packages/packages-lock.json` 和 `Library/` 仅在 Package Manager 保留旧解析或路径时执行；它们是可重建状态，不要删除 Consumer 正式资产。
5. 重新打开 Unity，确认 lock 中 `com.vincent.wgameframework` 的 source、version、revision/path 与请求一致。
6. 确认代表资源从 `Packages/com.vincent.wgameframework/...` 加载，并运行 base 与所用 optional profile smoke。

## 验证与回退

- P5 immutable base fixture 已在旧 Git subpath 上通过 `14/14`；P6 Local fixture 已在新 embedded root 上通过 `14/14`，三个 optional profile 各 `15/15`。
- 移动回执证明 package 内 3,204/3,204 tracked 文件在纯移动瞬间字节一致，所有内部 `.meta`、GUID 和 117 个 asmdef identity 保持不变。
- 若升级失败，Consumer manifest 回退到旧 subpath，同时把 revision 回退到 P5 完整 SHA；不能把旧 subpath 与 P6 SHA 或新 subpath 与 P5 SHA 混用。
- 框架仓库整体回退点为 P6 实施前提交 `32f7fd0d195651def1b9655fa0913397191aefba`。完整回退同时恢复旧 root sibling meta、locator fallback、runner/fixture 和文档，禁止只恢复目录。
