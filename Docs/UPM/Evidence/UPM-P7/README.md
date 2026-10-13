# UPM-P7 Consumer Evidence Gate

> Status: Current
>
> Candidate: `50036e76a0eb4ff9ca248965a548949173faffaa`
>
> Canonical candidate package tree: `05095ca820b4d819dba8ada303c994105e0193913cf7007b1c6406e2e524d72e`
>
> Unity: `6000.5.0f1`

P7 在干净 detached worktree 与短外部运行根中完成固定八行矩阵：Local/PinnedGit × base/FairyGUI/Unity Transport/VFX Graph。两条 base 行均为 `18/18`，六条 optional 行均为 `19/19`；schema、精确 full-name 集合、artifact SHA-256、Consumer/ProjectSettings/package mutation guard、log policy、resolved full SHA 和 candidate equality 全部通过，matrix 自动计算 `promotable=true`。

## 版本化证据包

本目录由 `Tools/Harness/build_upm_p7_evidence_bundle.py` 从外部原始运行根 `C:\WgP7O50036` 生成：

- `profiles/*.json`：八份规范化 profile report；
- `matrix.json`：schema v1 matrix；
- `p7-receipt-candidate.json`：E3 receipt candidate；
- `reports.sha256`：上述 10 份 JSON 的原始字节 SHA-256。

matrix 与 receipt 引用的 report 已全部随仓库保存，因此可以只用本目录复核二者。NUnit XML、Unity 日志摘要等 profile payload artifacts 仍保存在外部运行根；生成器在规范化之前已验证这些原始 artifact 的哈希。版本化 JSON 不包含用户名、凭据、license/serial 或绝对运行路径。

生成与复核命令：

```powershell
python Tools/Harness/build_upm_p7_evidence_bundle.py <external-root> <bundle-root>
python Tools/Harness/validate_upm_consumer_evidence.py matrix <bundle-root>/matrix.json --artifact-root <bundle-root>
python Tools/Harness/validate_upm_consumer_evidence.py receipt-candidate <bundle-root>/p7-receipt-candidate.json --artifact-root <bundle-root>
```

本次版本化 raw SHA-256：

- `matrix.json`：`ebb588d40046a1eef3098114f8e5eacab0f8ec341ef9826adb4df481777c970d`；
- `p7-receipt-candidate.json`：`d7da2e50f42449cb6228b4e32f9f8d4bc9220f334008664bd46c43854f747481`；
- `reports.sha256`：10/10 文件从仓库目录重新计算一致。

## 同候选验证

- Harness Python tests：`37/37`；
- package contract tests：`9/9`；
- repository EditMode：`162/162`；
- repository PlayMode：`21/21`；
- Unity refresh、compile、domain reload、Editor ready 与 Console 0 error：通过；
- strict E0：0 blocking failure，3 个 legal readiness gaps；
- legacy active package path：0；
- `git diff --check`、Unity `.meta` pairing、Docs health 与 touched-file code standards：通过。

该 receipt 仍明确 `releaseReady=false`。P8 尚需 owner 决定许可证与 Third Party Notices，并完成 tag、PinnedGit Player build/run、适用 optional Player、N-1 upgrade/migration、public API compatibility 和最终 preview publication；P7 的 `promotable=true` 不等于已经发布。
