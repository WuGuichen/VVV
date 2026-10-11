using System;

namespace MxFramework.Authoring;

/// <summary>
/// 角色包资源的导出用途分类：决定某个源条目是否进入 Unity / runtime 导入投影。
/// </summary>
/// <remarks>
/// 只表达导出用途，**不修改源对象**：源 catalog、制作 API、资源浏览与诊断继续看到完整条目集合，
/// 由调用方基于本分类派生 Unity/runtime 子集。
///
/// 判定条件刻意收窄为两条同时成立，不按文件名、目录或 <c>typeId</c> 猜测：
/// <list type="bullet">
/// <item><c>usage</c> 明确为 <c>previewMesh</c>；</item>
/// <item>有效源格式为 GLB（比较大小写不敏感，接受 <c>glb</c> 与 <c>.glb</c> 两种写法）。</item>
/// </list>
/// <c>typeId=model</c> 是合法输入（预览 GLB 的实际 <c>typeId</c> 就是 <c>model</c>）；
/// <c>previewMesh</c> 的 FBX/GLTF、<c>previewThumbnail</c>、正式 characterModel/weaponModel
/// 均不匹配本分类，保持原有导出行为。
/// </remarks>
public static class CharacterPackageResourceExportPolicy
{
    /// <summary>分类名：仅制作侧使用的专用预览 GLB。</summary>
    public const string AuthoringPreviewGlbCategory = "AuthoringPreviewGlb";

    /// <summary>触发分类的资源用途值。</summary>
    public const string PreviewMeshUsage = "previewMesh";

    /// <summary>规范化后的 GLB 源格式。</summary>
    public const string GlbSourceFormat = "glb";

    /// <summary>
    /// 诊断码：被标为专用预览的 GLB 同时被运行用途实际引用。
    /// </summary>
    public const string RuntimeReferenceDiagnosticCode = "CHAR_PREVIEW_GLB_RUNTIME_REFERENCE";

    /// <summary>
    /// 诊断码：专用预览 GLB 的外部依赖预检未通过（容器非法 / 依赖缺失 / 越界或绝对 URI）。
    /// </summary>
    public const string DependencyDiagnosticCode = "CHAR_PREVIEW_GLB_DEPENDENCY_INVALID";

    /// <summary>按源条目判定。使用有效源格式（显式 <c>sourceFormat</c> 优先，否则取扩展名）。</summary>
    public static bool IsAuthoringPreviewGlb(CharacterPackageResourceEntry entry)
    {
        if (entry == null)
            return false;

        return IsAuthoringPreviewGlb(entry.Usage, CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry));
    }

    /// <summary>按用途与有效源格式判定。任一为空或格式非 GLB 即返回 false。</summary>
    public static bool IsAuthoringPreviewGlb(string usage, string effectiveSourceFormat)
    {
        if (!string.Equals((usage ?? string.Empty).Trim(), PreviewMeshUsage, StringComparison.Ordinal))
            return false;

        return string.Equals(
            NormalizeSourceFormat(effectiveSourceFormat),
            GlbSourceFormat,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>规范化为不带前导点的源格式串。</summary>
    public static string NormalizeSourceFormat(string value)
    {
        string format = (value ?? string.Empty).Trim();
        return format.StartsWith(".", StringComparison.Ordinal) ? format.Substring(1) : format;
    }
}
