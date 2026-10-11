namespace MxFramework.Authoring
{
    /// <summary>
    /// 中立骨架的 Humanoid 语义标注。取值集合来自规范 v1.4 第 1.2 节
    /// <c>skeleton_hierarchy.json</c> 的 <c>humanoidSemantic</c> 枚举。
    /// </summary>
    /// <remarks>
    /// 已知缺口：规范 v1.4 第 1.2 节只列出 8 个取值，缺少下肢、手、脚等语义，
    /// 无法支撑第 7.1 节 <c>E-BONE-201</c>（Humanoid 语义备用锚点）与
    /// <c>E-BONE-202</c>（路径与语义冲突）的完整判定。此处严格按规范落地，
    /// 扩展取值须先由规范确认（见待确认项）。
    /// </remarks>
    public enum CharacterHumanoidBone
    {
        None = 0,
        Hips = 1,
        Spine = 2,
        Chest = 3,
        Neck = 4,
        Head = 5,
        LeftUpperArm = 6,
        RightUpperArm = 7
    }
}
