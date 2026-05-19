# MxFramework Authoring

独立 Authoring Core / CLI 工具骨架，用于验证外部主创编辑器的核心能力可以脱离 Unity 编译和运行。

## 项目结构

```text
Tools/MxFramework.Authoring/
  src/MxFramework.Authoring.Core/   # .NET Standard 2.1 纯 C# Core
  src/MxFramework.Authoring.Cli/    # authoring CLI
  tests/MxFramework.Authoring.Tests/# 无第三方依赖的测试控制台
  samples/buff-preview/             # Buff Preview ModPackage 样例
  samples/character-iron-vanguard/   # Character Resource Package 人形样例
  samples/character-slime/           # Character Resource Package 非人形样例
  samples/project-manifest/         # Project Authoring Manifest 样例
```

## 约束

- `MxFramework.Authoring.Core` 不引用 `UnityEngine` / `UnityEditor` / WGame 项目程序集。
- Patch / Mod 包导出格式使用 JSON。
- CLI 和测试当前目标运行时为 `net9.0`，Core 仍保持 `.NET Standard 2.1`。

## 常用命令

```bash
dotnet restore Tools/MxFramework.Authoring/MxFramework.Authoring.sln
dotnet build Tools/MxFramework.Authoring/MxFramework.Authoring.sln --no-restore /nr:false -m:1 -v:minimal
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- workflow context --workflow buff.create --step type-fields
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- manifest export
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- manifest inspect --manifest Tools/MxFramework.Authoring/samples/project-manifest/project-authoring-manifest.json
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character inspect --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character validate --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- validate --package Tools/MxFramework.Authoring/samples/buff-preview
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- merge-preview --package Tools/MxFramework.Authoring/samples/buff-preview
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- report --package Tools/MxFramework.Authoring/samples/buff-preview --out Tools/MxFramework.Authoring/samples/buff-preview/reports
```

## 当前范围

当前只是 Authoring Core / CLI v0：

- 内置 Buff Workflow。
- 内置 Buff Schema。
- ModPackage / PatchDocument 基础模型。
- Patch validate / merge-preview / report。
- Report bundle 写文件：`mod.json`、`validation_report.json`、`validation_report.txt`、`merge_preview.json`、`report_index.json`。
- 步骤级 AI context。
- Project Authoring Manifest export / inspect。
- Character Resource Package C0 契约：manifest、package-local resource catalog、body geometry、collider、socket、weapon attachment、trace、validation issue DTO。
- Character Resource Package CLI：`character inspect` / `character validate` / `character schema`。
- Iron Vanguard 人形样例和 Training Slime 非人形样例。

不包含外部 UI、角色资源导入、Authoring Compiler、Unity Bridge 导出和 Runtime Spawn。
