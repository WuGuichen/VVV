namespace MxFramework.Authoring
{
    /// <summary>
    /// 碰撞体校验输入 DTO，对应规范 v1.4 第 3.2 节 <c>RawColliderDto</c>。
    /// </summary>
    /// <remarks>
    /// 与既有持久化 DTO <see cref="CharacterBodyColliderProfile"/> 的关键差异是
    /// <see cref="Size"/> / <see cref="Radius"/> / <see cref="Height"/> 均为可空：
    /// 校验器必须能区分"未声明"与"声明为零"，否则球体/胶囊体会因为
    /// <c>size</c> 缺省为零向量而误触发 <c>E-GEO-303</c>。
    /// </remarks>
    public sealed class RawColliderDto
    {
        public string ColliderId { get; set; } = string.Empty;

        public string PartId { get; set; } = string.Empty;

        public string HitZoneId { get; set; } = string.Empty;

        public CharacterColliderShape Shape { get; set; } = CharacterColliderShape.Unknown;

        public CharacterAuthoringPose LocalPose { get; set; } = new CharacterAuthoringPose();

        /// <summary>遗留全长尺寸。未声明时为 null，此时跳过 <c>E-GEO-303</c> 审查。</summary>
        public CharacterAuthoringVector3 Size { get; set; }

        public float? Radius { get; set; }

        public float? Height { get; set; }
    }
}
