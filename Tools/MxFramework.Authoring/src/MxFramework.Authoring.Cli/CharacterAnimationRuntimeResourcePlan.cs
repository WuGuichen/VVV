using System;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static class CharacterAnimationRuntimeResourcePlanFormats
{
    /// <summary>正式角色包运行投影的 wire format。</summary>
    public const string RuntimePlan = "mx.animationResourcePlan.v2";

    public const string RuntimePlanSchemaVersion = "2.0";
}

internal static class CharacterAnimationRuntimeResourcePlanDiagnostics
{
    public const string Invalid = "CHARPKG_ANIMATION_RUNTIME_PLAN_INVALID";
}

/// <summary>
/// 正式角色包 <c>config/animation_resource_plan.json</c> 的运行用投影：恰好五个字段。
/// </summary>
/// <remarks>
/// 属性顺序即序列化顺序，不得调整。本类型只承载身份与语义哈希，
/// 不承载资源目录、加载分组、音频清单或诊断——那些保留在完整编译对象与显式诊断输出中。
/// </remarks>
internal sealed class CharacterAnimationRuntimeResourcePlanDocument
{
    public string Format { get; set; } = CharacterAnimationRuntimeResourcePlanFormats.RuntimePlan;
    public string SchemaVersion { get; set; } = CharacterAnimationRuntimeResourcePlanFormats.RuntimePlanSchemaVersion;
    public string PackageId { get; set; } = string.Empty;
    public string StableId { get; set; } = string.Empty;
    public string PlanHash { get; set; } = string.Empty;
}

/// <summary>
/// 把完整动画资源计划投影为运行用五字段文档。只做校验与复制，不重算任何语义哈希。
/// </summary>
internal static class CharacterAnimationRuntimeResourcePlanProjection
{
    /// <summary>`000008` 正文验收上限。</summary>
    internal const int MaxBytes = 512;

    private const string HashPrefix = "sha256:";

    /// <summary>
    /// 投影并序列化。失败时返回 false 并给出稳定诊断；调用方必须在写盘前处理。
    /// </summary>
    internal static bool TryProject(
        AnimationResourcePlanDocument source,
        JsonSerializerOptions options,
        out string json,
        out string diagnostic)
    {
        json = string.Empty;
        diagnostic = string.Empty;

        if (source == null)
        {
            diagnostic = "Animation resource plan source object is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.PackageId))
        {
            diagnostic = "Animation resource plan source is missing packageId.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.StableId))
        {
            diagnostic = "Animation resource plan source is missing stableId.";
            return false;
        }

        if (!IsLowercaseSha256Hash(source.PlanHash))
        {
            diagnostic = "Animation resource plan source planHash must be 'sha256:' followed by 64 lowercase hex characters; actual='" + source.PlanHash + "'.";
            return false;
        }

        var projection = new CharacterAnimationRuntimeResourcePlanDocument
        {
            PackageId = source.PackageId,
            StableId = source.StableId,
            PlanHash = source.PlanHash
        };

        string serialized = JsonSerializer.Serialize(projection, options);
        if (!serialized.EndsWith("\n", StringComparison.Ordinal))
            serialized += "\n";

        int bytes = System.Text.Encoding.UTF8.GetByteCount(serialized);
        if (bytes > MaxBytes)
        {
            diagnostic = "Animation runtime resource plan body exceeds " + MaxBytes + " bytes (actual " + bytes
                + "); identity and hash must not be truncated to fit.";
            return false;
        }

        json = serialized;
        return true;
    }

    internal static bool IsLowercaseSha256Hash(string value)
    {
        if (value == null || value.Length != HashPrefix.Length + 64 || !value.StartsWith(HashPrefix, StringComparison.Ordinal))
            return false;

        for (int i = HashPrefix.Length; i < value.Length; i++)
        {
            char character = value[i];
            bool isLowercaseHex = (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f');
            if (!isLowercaseHex)
                return false;
        }

        return true;
    }
}
