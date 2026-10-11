# 战术卡描述查询

## 程序集读取与 Unity 编译互不占用

`generated`、目录和临时装配预览先捕获主 DLL 及其完整托管依赖闭包，再用独立、可回收的 `AssemblyLoadContext.LoadFromStream` 读取内存映像。不会将 `Library/ScriptAssemblies` 的 DLL 交给路径加载器，也不会留下源文件句柄或映像映射；此保证不依赖 GC 何时完成卸载。框架程序集只共享当前 .NET 运行时的程序集。

每次请求重新读取依赖，按程序集名称、兼容版本、公钥和区域信息校验身份，并复读校验全部字节哈希和时间戳。源文件变化、缺失、被独占写入或处于半写状态时有界重试，失败后报告 `ASSEMBLY_SNAPSHOT_UNAVAILABLE`，不返回上次缓存。捕获结束后依赖只从该次快照解析，因此之后的编译替换不会使一次预览混用新旧依赖。`generated.assemblySnapshot` 输出加载模式、闭包指纹、数量、捕获时间和主 DLL 哈希；依赖变化也会改变生成指纹。

依赖查找覆盖项目 ScriptAssemblies、对应 Unity 版本的 Managed / BuildPipeline，以及项目 PackageCache；托管映像按元数据身份校验。版本绑定遵循 [.NET AssemblyLoadContext 的等于或高于请求版本规则](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext#versioning-rules)。未知动态依赖明确失败，不回退到项目磁盘路径加载。

回归入口为 Authoring 测试程序参数 `tactical-card-assembly-isolation`。它仅在临时目录构建夹具，验证上下文存活期间主 DLL 和依赖可独占写入、覆盖、重命名和删除，延迟加载仍读取同次快照，新请求读取新版依赖，并拒绝缺失、错误身份、独占写入和半写输出。真实项目验收使用零写入的 ReadWrite 打开检查，并检查宿主进程没有映射任何主项目 ScriptAssemblies 映像；不以“调用了 Unload”替代释放证据。

在项目根目录运行（CLI 只需构建一次，查询不会构建、刷新 Unity 或切换 Play）：

```powershell
./Tools/MxFramework.Authoring/scripts/card-query.ps1 -Name '错拍再攻 · 测试' -Layer live
./Tools/MxFramework.Authoring/scripts/card-query.ps1 -Contains '错拍再攻' -Layer disk
./Tools/MxFramework.Authoring/scripts/card-query.ps1 -Id '<stableId 或 recordId>'
./Tools/MxFramework.Authoring/scripts/card-query.ps1 -InstanceId '<instanceId>' -SaveJson '<已解码玩家数据.json>'
```

`disk` 直接读取发布目录索引、author 工作区索引及测试卡 JSON，输出身份、版本、绑定和静态短描述。author 文件必须满足索引 hash/identity/revision；玩家卡必须精确匹配发布 revision/contentHash，缺失时报告，不替换为最新卡。`-SaveJson` 接受已解码的 tactical-player payload，不接受存档 envelope，不写玩家数据。同名或多个来源匹配时输出 `ambiguous` 和候选，选择明确 recordId。测试卡的 BaseKey 是来源标识，不冒充其 stableId。

`generated` 使用现有编译运行时中的描述投影器和实际 TSV provider 生成 `generated.description`，不以静态短描述替代。不会启动角色测量；未准备距离/时长显示原有未知参数，`measured=false`。动作名从实际动作目录解析，歧义时报告，不猜测。源比 DLL 新时输出 `generated-assembly-may-be-stale`；输出 DLL moduleId、TSV hash 和 source/bindings/locale/runtime fingerprint。没有跨查询缓存，修改任何输入后下次查询重新读取。可用 `-RuntimeAssembly '<独立验证 DLL>'` 明确选择独立 CPU 编译结果，它不代表现场 Editor 已加载该版本。`-SnapshotJson` 可读取外部保存的只读 configured-card JSON 快照，不保存或修改原卡。

`live` 向 Editor 内的只读 owner 发起一次请求，读取当前**可见卡**已绑定的 DTO、实际 Description、动作名和参数。最多扫描 100 ms / 10000 UI 节点，客户端最多等待 2 秒；返回旧缓存如实标记 `live-ui-cache`，不会刷新或重新准备。未显示的卡、未加载 owner、无法确认的身份分别报告 not-found/unavailable/null。新 owner 需在正常编译后加载；不能为查询停止 Play。离线模式无需连接 Editor。三种模式不会修改场景、卡片、执行规则、存档或发布物。

原生同源 EditorHub GET 接口：`/api/tactical-card/query?name=<URL编码名称>&layer=disk|generated|live&locale=zh-CN`。已有服务要在正常升级后运行新 CLI 版本；查询不会重启服务。

公共组合显示使用箭头和真实条件路径。`generated.description` 已包含换行与缩进，Web 使用 `pre-wrap` 即可；可选 `generated.descriptionFlow` 提供同源显示树和排版行，详见 `Docs/Interfaces/TacticalCard.DescriptionFlow.md`。它不属于玩法存档 schema。验证用 `-RuntimeAssembly` 指定独立新 DLL，不能将此结果冒充当前 Editor 已加载版本。

构建一次：`dotnet build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli -c Release`。运行中的服务若占用输出，请先构建到独立目录并通过 `-ToolDll` 指定，不强行重启。JSON 是机器接口，候选和 provenance 始终保留。

## EditorHub 战术卡目录

`/Tools/MxFramework.EditorHub/web/combat-design.html#cards` 展示当前索引中的正式版本、作者草稿和配置测试副本。默认显示正式与草稿，测试副本需切换来源筛选。此目录不扫描玩家存档；不会将模板、测试副本或临时绑定当作玩家持有实例。意图目录仍是模板组件目录，不能用整套战术模板充当基础意图。

只读 GET 接口：

- `/api/tactical-card/catalog?locale=zh-CN`：每次重新读取权威索引，以稳定身份、版本、完整字符串 hash 和工作区区分同名记录。支持当前 v3/v4 作者索引；hash、identity、revision 或文件失效时保留明确的失效索引行和诊断，不制造有效内容。旧 Recipe 保留静态说明，不伪造同源投影。
- `/api/tactical-card/preview?key=…&role=…&bindings=…&sourceFingerprint=…`：仅在请求内替换角色上下文及已声明动作槽，再调用原有共享投影器。角色目录和动作目录是作者数据，不证明玩家持有、武器兼容或可执行。没有角色测量，实际费用及未知距离继续显示未知。清空绑定后恢复未装配说明；源指纹改变则拒绝旧预览。
- `/api/tactical-card/source?key=…`：按精确目录身份重新解析源路径，只返回该索引记录的源码；不接受任意文件路径。

前端只展示投影器的 `description`、`descriptionDetails` 与结构化参数 DTO，不另写描述算法。嵌套移动参数、等待、环绕、面向目标等参数保留游戏 DTO 的显示值。组件和机制筛选通过已存在的 `CombatDesignCatalogProjection` 固定解析器匹配保存源；不能匹配的条目仍完整显示，并标记尚未分类。

卡面复用 `TacticalPlayerCardFace.xml` 的 378×528 几何、`behavior_card_base.png` 和原类型图标。当前卡片的默认矢量徽章沿用 `CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter` 的八边形及菱形坐标、`CharacterTestFairyGuiPlayerCardPresentation` 的颜色；Web SVG 是该徽章的静态表示。卡面短摘要可省略长文，详情与复制入口始终保留完整生成说明。Web 字体依赖本机中文字体，未声称像素级一致。

浏览器回归脚本：`Tools/MxFramework.EditorHub/scripts/verify-tactical-cards.cjs <origin> <evidence-directory>`。设置 `PLAYWRIGHT_MODULE` 指向已安装的 `playwright-core`，`BROWSER_PATH` 指向本机 Edge/Chrome。它按实时接口计数核对目录、逐一打开正式详情、反复筛选、临时绑定、来源链接、刷新和窄屏；仅调用只读接口，不启动或刷新 Unity。
