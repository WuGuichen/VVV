# Audio FMOD M3：Settings + Bank Validation

> 状态：Implemented / Bank Assets Pending
> 日期：2026-05-11
> 前置任务：`AUDIO_FMOD_M2_BACKEND.md`
> 实现边界：新增 Editor-only FMOD setup validator 和菜单；当前机器未发现 FMOD Studio、`.fspro` 或 `.bank`，因此不能自动生成真实 bank。

## 已实现

- 新增 `MxFramework.Audio.FMOD.Editor` Editor-only asmdef。
- 新增 `FmodAudioSetupValidator`。
- 新增菜单：
  - `MxFramework/Audio/Validate FMOD Setup`
  - `MxFramework/Audio/Refresh FMOD Banks`
- 校验内容：
  - FMOD Settings 是否存在。
  - `SourceProjectPath` / `SourceBankPath` 是否缺失。
  - runtime bank root 是否存在。
  - 是否存在 `.bank`。
  - 是否存在 `Master.bank`。
  - 是否存在 `Master.strings.bank`。
  - `BankLoadType=Specified` 时 `BanksToLoad` 是否为空。
  - `BankLoadType=All` 时 FMOD bank cache 是否为空并提示 refresh。
- 新增 `FmodAudioSetupValidatorTests`，用临时 bank 文件覆盖缺失和通过路径。

## 当前阻塞

当前工程没有发现：

- FMOD Studio app / CLI。
- `.fspro` 工程。
- `.bank` 或 `.strings.bank`。

所以不能由本地自动生成可播放 bank。真实出声验收仍需要一组 FMOD Studio 导出的 bank：

```text
Master.bank
Master.strings.bank
SFX.bank 或其他包含 Demo event 的业务 bank
```

## 下一步

用户放入 bank 后：

1. 配置 FMOD Settings 的 Source Type / Source Bank Path。
2. 执行 `MxFramework/Audio/Refresh FMOD Banks`。
3. 执行 `MxFramework/Audio/Validate FMOD Setup`。
4. 在 `FmodAudioDemoRunner` 填入真实 `event:/...` 或 guid。
5. 跑 PlayMode 出声 smoke：one-shot、loop start/stop、parameter、bus volume。
