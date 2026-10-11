using UnityEditor;

namespace MxFramework.Editor
{
    [InitializeOnLoad]
    internal static class FrameworkManagerBuiltInTools
    {
        static FrameworkManagerBuiltInTools()
        {
            Register("framework.modules", "模块概览", "Framework", "查看框架模块 asmdef 和基础验证状态。", "内置", 10, "MxFramework/Framework Manager", FrameworkManager.Open);
            Register("framework.config-workbench", "配置工作台", "Config", "查看配置源、字段、引用、健康报告和提交前检查。", "内置", 20, string.Empty, FrameworkManager.OpenConfigWorkbench);
            Register("resources.sample-player-catalog", "构建示例资源 Catalog", "Resources", "生成框架示例 ResourceCatalog，并复用资源校验入口。", "菜单", 30, "MxFramework/Samples/Build Player Resource Catalog");
            Register("character.import-package", "导入 Character Resource Package", "Character", "调用 Authoring CLI / Unity Import Bridge 导入角色资源包。", "菜单", 40, "MxFramework/Character/Import Character Package...");
            Register("character.reimport-package", "重复导入上次角色包", "Character", "使用上次路径重新执行角色资源包导入。", "菜单", 41, "MxFramework/Character/Reimport Last Character Package");
            Register("animation.workstation", "MxAnimation Workstation", "Animation", "打开动画 registry 工作台、事件时间线和导出检查入口。", "菜单", 50, "MxFramework/MxAnimation/Workstation");
            Register("animation.bake-selected", "Bake Selected Animation Clip", "Animation", "从选中的 AnimationClip 生成 bake 报告 artifact。", "菜单", 51, "MxFramework/MxAnimation/Bake Selected Animation Clip MVP");
            Register("animation.timeline-preview", "Timeline Scrubber Preview", "Animation", "打开 MxAnimation 时间线 scrubber 预览工具。", "菜单", 52, "MxFramework/MxAnimation/Timeline Scrubber Preview MVP");
        }

        private static void Register(
            string id,
            string displayName,
            string group,
            string description,
            string status,
            int sortOrder,
            string menuPath,
            System.Action openAction = null)
        {
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                id,
                displayName,
                group,
                description,
                status,
                menuPath,
                sortOrder,
                openAction ?? (() => EditorApplication.ExecuteMenuItem(menuPath))));
        }
    }
}
