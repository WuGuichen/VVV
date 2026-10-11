namespace MxFramework.Authoring
{
    /// <summary>
    /// 源契约姿态 DTO，对应规范 v1.4 第 3.1 节 <c>CharacterAuthoringPose</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与既有持久化 DTO <see cref="CharacterAuthoringLocalPose"/> 的区别有两点，
    /// 这两点差异是规范刻意的设计，不能合并：
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <see cref="EulerHint"/> 为可空。规范明确其"仅供创作展示，反序列化允许读取，
    /// 但在规范化几何求值时彻底丢弃"。可空才能区分"未声明"与"声明为零向量"。
    /// </description></item>
    /// <item><description>
    /// <see cref="HumanoidSemantic"/> 承载中立骨架的语义锚点，用于第 7.1 节
    /// <c>E-BONE-201</c> / <c>E-BONE-202</c> 的判定。
    /// </description></item>
    /// </list>
    /// <para>
    /// <see cref="ParentKind"/> 保持为字符串而非枚举：源契约以 JSON 面向外部工具，
    /// 未知取值必须能原样透传以便诊断，不能在校验前被反序列化吞掉。
    /// </para>
    /// </remarks>
    public sealed class CharacterAuthoringPose
    {
        public string ParentKind { get; set; } = "Bone";

        public string ParentPath { get; set; } = string.Empty;

        public CharacterHumanoidBone HumanoidSemantic { get; set; } = CharacterHumanoidBone.None;

        public CharacterAuthoringVector3 Position { get; set; } = new CharacterAuthoringVector3(0f, 0f, 0f);

        public CharacterAuthoringQuaternion Rotation { get; set; } = new CharacterAuthoringQuaternion(0f, 0f, 0f, 1f);

        public CharacterAuthoringVector3 Scale { get; set; } = new CharacterAuthoringVector3(1f, 1f, 1f);

        /// <summary>
        /// 仅供创作展示的欧拉角提示。规范化几何求值时必须彻底丢弃，不得参与任何计算。
        /// </summary>
        public CharacterAuthoringVector3 EulerHint { get; set; }

        /// <summary>是否声明了欧拉角提示。</summary>
        public bool HasEulerHint => EulerHint != null;

        /// <summary>从既有持久化姿态转换。欧拉角提示按值拷贝，仅作展示用途。</summary>
        public static CharacterAuthoringPose FromLocalPose(CharacterAuthoringLocalPose pose)
        {
            if (pose == null)
            {
                return null;
            }

            return new CharacterAuthoringPose
            {
                ParentKind = pose.ParentKind.ToString(),
                ParentPath = pose.ParentPath ?? string.Empty,
                HumanoidSemantic = CharacterHumanoidBone.None,
                Position = new CharacterAuthoringVector3(pose.Position.X, pose.Position.Y, pose.Position.Z),
                Rotation = new CharacterAuthoringQuaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W),
                Scale = new CharacterAuthoringVector3(pose.Scale.X, pose.Scale.Y, pose.Scale.Z),
                EulerHint = new CharacterAuthoringVector3(pose.EulerHint.X, pose.EulerHint.Y, pose.EulerHint.Z)
            };
        }
    }
}
