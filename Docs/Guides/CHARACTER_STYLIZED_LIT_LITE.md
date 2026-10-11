# Character Stylized Lit Lite

独立对照 Shader：`WGame/CharacterStylizedLitLite`。

此 Shader 属于历史色板对照方案，输入合同与 `WGame/CharacterPaletteLegacy` 对应；不能直接用于烘焙网格。新角色主路径为仅烘焙的 `WGame/CharacterStylizedLit`。下文历史性能记录中的“原版”指拆分前的色板路径。

## 替换对比

1. 复制现有角色材质，保留原材质作为对照。
2. 将副本的 Shader 切换到 `WGame > CharacterStylizedLitLite`。
3. 使用 Palette 的角色保持 `Palette Enabled` 开启，检查共享色板纹理仍已绑定；有 FaceDetail 的角色保持对应开关与纹理。
4. 确认 `Shared Material MatCap` 使用 Shader 默认的 `CharacterLiteMatCap`；若材质中显式保存了空值，可手动绑定 `Assets/Art/WGame/Shaders/Character/CharacterLiteMatCap.asset`。
5. 将副本临时赋给角色 Renderer，比较常用游戏距离和近景。`MatCap Strength` 控制反射亮带强度，`MatCap in Shadow` 控制其在阴影中的保留比例。

原 Shader、正式材质和角色绑定不因新增此 Shader 而改变。生成材质在重新导入时可能被生成器还原，长期采用前需要单独调整材质选择流程。

## 设计与取舍

- 复用现有色板寻址、16 个 Surface Profile、FaceDetail、AO、发光、状态染色和受击参数。
- 色板采样、Profile 解码、固定明暗颜色、环境补光和雾在顶点阶段计算。角色输入应遵循现有逐面一致的色板地址和 Profile 约定。
- 明暗边界、一个统一的平滑主光高光、主光阴影和 MatCap 坐标逐像素计算。相比第一版顶点高光，平滑曲面的明暗和高光不再仅由顶点插值决定。
- 无 FaceDetail 时，预计算暗、中、亮三种颜色并逐像素混合；有 FaceDetail 时，先合成面部颜色再计算明暗。材质共用一套光照，高光宽度和强度由原 Surface Profile 提供。
- 金属和晶体使用一张共享、中性的 128×128 RHalf MatCap，按色板颜色染色，不固定成金色或蓝色；皮肤、头发、织物和皮革不采样 MatCap。该近似使用观察空间法线，会随相机改变反射位置，不代表真实环境反射。
- MatCap 由 Unity Editor 生成并通过 ShaderImporter 设为默认纹理：在像素中心将 x/y 映射到 [-1,1]，亮度为 `saturate(1-abs(x-0.42)*2.6)^2*0.78 + saturate(y*0.72+0.38)^2*0.42`；线性采样、Clamp、无 mip。需要更换外观时可以直接替换该纹理。
- 主光阴影保持逐像素接收；附加灯支持 Forward/Forward+ 的逐像素漫反射补光，不计算附加灯镜面高光。
- 不计算旧版程序化织物、皮革、木纹、拉丝细节，也不执行旧版材质专属色相/亮度重建。保留这些旧属性仅用于材质切换兼容，Inspector 隐藏了未使用的细节和 Rim Power 参数。
- 顶点环境光与雾仍依赖网格密度。共享 MatCap、单高光和预计算颜色是外观近似，不承诺与原版 CharacterStylizedLit 逐像素一致。
- 溶解默认不编入常态变体。需要溶解时，先勾选 `Enable Dissolve`（`_WG_LITE_DISSOLVE_ON`），然后驱动原有 `Dissolve Progress`。运行时通过代码创建材质时，应同时设置 keyword；Player 构建须有引用该变体的材质以避免裁剪。
- Forward、ShadowCaster、DepthOnly 共享材质常量布局与溶解规则；保留 LOD CrossFade 和实例化变体。

## 2026-09-05 验证

- Unity refresh/compile、domain reload、Editor ready 完成，Console error 为 0。
- 色板、FaceDetail、溶解、Forward+ 与主光级联阴影的代表性变体可编译，三个 Pass 的 SetPass 成功。
- 已加载 `000004_character_preview.prefab` 到临时 Prefab 编辑场景，使用烘焙网格生成单灯对照并卸载，未保存或修改正式绑定。预览位于本地 `Library/CharacterLiteReview/before.png` 和 `after.png`。
- 当前逐像素版的代表性变体和三个 Pass 已验证。第一版顶点着色的性能数据不再代表当前版本。
- RTX 4080 / D3D11，2048×2048 ARGBHalf、32 次全覆盖绘制、1 像素同步读回，预热后轮换测量 9 次取中位数：织物旧 Lite 1.313 ms / 当前版 1.815 ms / 基础 URP Lit 1.511 ms；抛光金属旧 Lite 1.327 ms / 当前版 1.978 ms / URP Lit 1.560 ms。均无阴影、附加灯、FaceDetail 和溶解；当前版金属启用 MatCap。
- 当前版本改善逐像素表现，但在该测试中约为 PBR 的 1.20–1.27 倍，尚未达到低于 PBR 的目标。同步负载包含 CPU 提交与 GPU 等待，样本存在波动，不是单角色或纯 GPU 时间；不能外推实际场景倍率。
