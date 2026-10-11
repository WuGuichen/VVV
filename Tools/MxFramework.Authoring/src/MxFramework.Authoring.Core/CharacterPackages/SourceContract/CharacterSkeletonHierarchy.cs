using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 中立骨架中的单根骨骼。对应规范 v1.4 第 1.2 节 <c>bones[]</c>。
    /// </summary>
    /// <remarks>
    /// 规范澄清项：<c>humanoidSemantic</c> 的 Schema 允许 <c>null</c> 与字面量
    /// <c>"None"</c> 两种取值。两者在 <c>E-BONE-201</c> / <c>E-BONE-202</c> 判定中
    /// 语义等价（均为"未配置语义锚点"），因此此处统一映射为
    /// <see cref="CharacterHumanoidBone.None"/>，不再保留 null 与 None 的差异。
    /// </remarks>
    public sealed class CharacterSkeletonBone
    {
        public string BoneName { get; set; } = string.Empty;

        /// <summary>骨架树中的唯一路径。</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>父骨骼路径。根骨骼为空字符串。</summary>
        public string ParentPath { get; set; } = string.Empty;

        public CharacterHumanoidBone HumanoidSemantic { get; set; } = CharacterHumanoidBone.None;

        /// <summary>绑定姿态的世界矩阵，行主序 16 元素。布局约定见 <see cref="CharacterMatrix4x4"/>。</summary>
        public float[] BindPoseWorldMatrix { get; set; } = new float[CharacterMatrix4x4.ElementCount];

        public bool HasHumanoidSemantic => HumanoidSemantic != CharacterHumanoidBone.None;
    }

    /// <summary>
    /// 中立骨架文档。对应规范 v1.4 第 1.2 节 <c>skeleton_hierarchy.json</c>。
    /// </summary>
    public sealed class CharacterSkeletonHierarchy
    {
        public const string CurrentSchemaVersion = "1.0.0";

        public string SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string PackageId { get; set; } = string.Empty;

        /// <summary>该骨架所提取自的基础网格 FBX 的 SHA-256。</summary>
        public string SourceMeshSha256 { get; set; } = string.Empty;

        public List<CharacterSkeletonBone> Bones { get; set; } = new List<CharacterSkeletonBone>();
    }
}
