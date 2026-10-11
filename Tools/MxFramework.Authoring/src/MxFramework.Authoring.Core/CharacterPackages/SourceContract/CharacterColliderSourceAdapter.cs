namespace MxFramework.Authoring
{
    /// <summary>
    /// 把既有持久化碰撞体 DTO 适配为第 3.2 节校验输入 DTO。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 适配的核心难点是"遗留 <c>size</c> 是否被声明"。既有
    /// <see cref="CharacterBodyColliderProfile.Size"/> 是非空引用类型，
    /// 当 JSON 中缺少 <c>size</c> 键时反序列化结果是零向量，无法与
    /// "显式声明 size=(0,0,0)" 区分。若不做区分，球体/胶囊体会因为
    /// 零向量与权威几何的偏差而误触发 <c>E-GEO-303</c>。
    /// </para>
    /// <para>
    /// 因此本适配器默认采用"非全零即视为已声明"的推断，并允许调用方
    /// 通过 <c>hasDeclaredLegacySize</c> 显式覆盖。需要精确语义时，
    /// 应改由源契约在反序列化阶段直接产出 <see cref="RawColliderDto"/>。
    /// </para>
    /// </remarks>
    public static class CharacterColliderSourceAdapter
    {
        /// <summary>
        /// 推断遗留 <c>size</c> 是否已声明：非空且三个分量不同时为零。
        /// </summary>
        public static bool InferLegacySizeDeclared(CharacterAuthoringVector3 size)
        {
            return size != null && (size.X != 0f || size.Y != 0f || size.Z != 0f);
        }

        /// <summary>
        /// 转换为校验输入 DTO。
        /// </summary>
        /// <param name="profile">既有持久化碰撞体。</param>
        /// <param name="hasDeclaredLegacySize">
        /// 遗留 <c>size</c> 是否已声明。传 null 时按
        /// <see cref="InferLegacySizeDeclared"/> 推断。
        /// </param>
        public static RawColliderDto ToRawColliderDto(
            CharacterBodyColliderProfile profile,
            bool? hasDeclaredLegacySize = null)
        {
            if (profile == null)
            {
                return null;
            }

            bool sizeDeclared = hasDeclaredLegacySize ?? InferLegacySizeDeclared(profile.Size);

            return new RawColliderDto
            {
                ColliderId = profile.ColliderId,
                PartId = profile.PartId,
                HitZoneId = profile.HitZoneId,
                Shape = profile.Shape,
                LocalPose = CharacterAuthoringPose.FromLocalPose(profile.LocalPose),
                Size = sizeDeclared
                    ? new CharacterAuthoringVector3(profile.Size.X, profile.Size.Y, profile.Size.Z)
                    : null,
                Radius = profile.Radius,
                Height = profile.Height
            };
        }

        /// <summary>
        /// 校验并规范化单个既有碰撞体，返回规范化几何。
        /// </summary>
        public static CanonicalColliderGeometry ValidateAndNormalize(
            CharacterBodyColliderProfile profile,
            string objectPath,
            bool? hasDeclaredLegacySize = null)
        {
            RawColliderDto dto = ToRawColliderDto(profile, hasDeclaredLegacySize);
            return CharacterColliderValidatorAndNormalizer.ValidateAndNormalize(dto, objectPath);
        }
    }
}
