# Audio Workbench

项目级 Audio Cue 创作面。通过 EditorHub 启动 Authoring server 后访问：

`/Tools/MxFramework.AudioWorkbench/web/?package=Tools/MxFramework.Authoring/samples/character-codex-flow-test`

工作台保存 `Assets/Config/MxFramework/Audio/audio_semantic_manifest.json` 和 Authoring-only generation Recipe；发布生成 Global Runtime Audio Catalog 和 StreamingAssets mirror。接触音效 Recipe 使用“碰撞体 × 受体 × 接触过程”的结构化物理配方，Footstep、Landing、Object Collision、Weapon Hit 可直接选择双方材质、质量、接触面积、表面、构造、强度、速度和角度，无需手写英文。Authoring AI Assist 会把配方投影为英文 brief，代理 4080 SFX Host，并提供候选波形、剪辑、A/B 混合与一键 FMOD 应用。生成和应用不会自动批准，最终仍需 Unity/FMОD 人工审听。Resource Manager 的 Audio 页仅负责只读总览与深链。完整流程见 `Docs/AUDIO_WORKBENCH_GUIDE.md`。

界面使用六个共享状态的一级工作区：项目总览、Cue 制作、候选实验室、Basis 与响应、材质覆盖、游戏预览与发布。项目总览只展示 `runtimeActivation` 权威状态和下一批阻断任务；矩阵只负责公共 Pair 覆盖，不再承担整套音频创作导航。旧 `?mode=pairs` 深链继续兼容，并映射到 `?screen=matrix`。

## 公共材质矩阵批量制作

全量物理材质 Pair 使用现有 Authoring server 与 wg200 SFX Host 串行制作。脚本不会自动采用候选、批准审听或发布 Runtime Catalog：

```bash
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py plan
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py prepare
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py calibrate
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py generate --scope impact --calibration-approved
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py generate --scope sweep --calibration-approved
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py generate --scope specialized --calibration-approved
python3 Tools/MxFramework.AudioWorkbench/scripts/physical_matrix_pipeline.py status
```

- `prepare` 原子保存 78 个实体材质 Pair 与 12 个材质 × 空气 Pair；Auto 仍为 fallback-only，Air × Air 保持静音。
- `calibrate` 通过受控 Managed Event 管线物化八个校准 Basis，保存可追溯 Recipe，并在 RTX/CUDA/固定 model revision 预检后为每层生成三个候选。
- 已完成且 Recipe hash 一致的 Job 会被复用，脚本可安全断点续跑。
- `generate` 必须显式声明校准已通过，并可按 Impact、Sweep、专属动作或全部范围串行继续；服务存在冲突 Job 时预检会拒绝启动。
- 校准结果必须在 Workbench 完成人工选择；候选技术指标不能替代主观审听。
- 新校准 Recipe 使用 `NaturalDecay`：界面配置的是材质相关的最大生成窗口，Host 自动裁到自然结束；候选会展示原始画布、最大窗口、最终成品和尾音能量。`ReverbLike` 作为软质量警告保留在主候选区，可试听、Keep、本地裁切和混合比较；“一键微调到门槛并勾选”会寻找最短尾音渐隐并交给 Host 重新渲染、复检，只有复检为 `Ready` 才解锁并进入选择集。
- 尾音落在 `-30 dB` 到 `-18 dB`、且没有削波或静音等硬错误时，Host 会在候选末端自动施加 60–120ms 余弦渐隐并重新执行自然衰减检测。修复成功的候选以 `Ready` 返回，同时保留原始尾音能量和自动渐隐参数；高于 `-18 dB` 的明显持续尾音仍保持 `ReverbLike`。
