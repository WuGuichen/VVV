using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>单根骨骼的重绑定裁决。</summary>
    public enum CharacterBoneRebindVerdict
    {
        /// <summary>骨架未变化（Δ_B 在容差内），姿态原样保留。</summary>
        NotRequired = 0,

        /// <summary>Δ_B 超容差，需人工审查；候选姿态仅供评估，不得自动应用。</summary>
        ManualReviewRequired = 1
    }

    /// <summary>单根骨骼的重绑定明细。</summary>
    public sealed class CharacterBoneRebindEntry
    {
        public CharacterBoneRebindEntry(
            string bonePath,
            float deltaB,
            CharacterAuthoringPose authoredPose,
            CharacterAuthoringPose candidatePose,
            CharacterBoneRebindVerdict verdict,
            string reason)
        {
            BonePath = bonePath ?? string.Empty;
            DeltaB = deltaB;
            AuthoredPose = authoredPose;
            CandidatePose = candidatePose;
            Verdict = verdict;
            Reason = reason ?? string.Empty;
        }

        public string BonePath { get; }

        /// <summary>Δ_B：旧/新骨架绑定矩阵的逐元素最大绝对偏差。</summary>
        public float DeltaB { get; }

        /// <summary>创作时记录的姿态。</summary>
        public CharacterAuthoringPose AuthoredPose { get; }

        /// <summary>
        /// 候选姿态 <c>B_new⁻¹ · B_old · P_old</c>。
        /// 仅在 <see cref="CharacterBoneRebindVerdict.ManualReviewRequired"/> 时作为审查参考，
        /// 严禁自动写入。
        /// </summary>
        public CharacterAuthoringPose CandidatePose { get; }

        public CharacterBoneRebindVerdict Verdict { get; }

        public string Reason { get; }
    }

    /// <summary>
    /// 重绑定候选报告。对应规范 v1.4 第 4.1 节 <c>RebindCandidateReport</c>。
    /// </summary>
    public sealed class CharacterRebindCandidateReport
    {
        public string PackageId { get; set; } = string.Empty;

        /// <summary>创作时锁定的骨架快照 ID。</summary>
        public string AuthoredSkeletonSnapshotId { get; set; } = string.Empty;

        /// <summary>Δ_B 判定容差。</summary>
        public float Tolerance { get; set; } = CharacterBoneRebindSolver.DeltaBTolerance;

        /// <summary>旧骨架快照是否丢失。</summary>
        public bool SnapshotMissing { get; set; }

        public List<CharacterBoneRebindEntry> Entries { get; set; } = new List<CharacterBoneRebindEntry>();

        public int ManualReviewCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Verdict == CharacterBoneRebindVerdict.ManualReviewRequired)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int TransparentCount => Entries.Count - ManualReviewCount;

        /// <summary>是否需要人工审查。为 true 时禁止自动发布。</summary>
        public bool RequiresManualReview => SnapshotMissing || ManualReviewCount > 0;

        /// <summary>需人工审查的骨骼路径列表。</summary>
        public IReadOnlyList<string> ManualReviewBones
        {
            get
            {
                var bones = new List<string>();
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Verdict == CharacterBoneRebindVerdict.ManualReviewRequired)
                    {
                        bones.Add(Entries[i].BonePath);
                    }
                }

                return bones;
            }
        }
    }

    /// <summary>
    /// 骨骼双锚点重绑定求解。对应规范 v1.4 第 4.1 节。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 规范澄清项（两处表述的调和）：第 4.1 节同时给出
    /// "Δ_B ≤ 1e-4 时自动完成透明重绑定（P_new = P_old）"与公式
    /// <c>P_new = B_new⁻¹ · B_old · P_old</c>。二者并非同一件事：
    /// 前者描述的是<b>骨架未变</b>时无需改动姿态；后者是把姿态从旧骨骼坐标系
    /// 搬到新骨骼坐标系的<b>候选变换</b>。本实现的裁决是：
    /// </para>
    /// <list type="bullet">
    /// <item><description>Δ_B ≤ 容差 → <see cref="CharacterBoneRebindVerdict.NotRequired"/>，
    /// 姿态保持 <c>P_old</c> 不变。</description></item>
    /// <item><description>Δ_B &gt; 容差 → <see cref="CharacterBoneRebindVerdict.ManualReviewRequired"/>，
    /// 报告中给出候选姿态供人工评估，<b>不自动应用</b>（规范明确"严禁猜测"）。</description></item>
    /// </list>
    /// <para>该调和方式需规范确认。</para>
    /// <para>
    /// 额外防御：若候选姿态分解出的缩放不接近单位值，说明旧/新绑定矩阵之间存在
    /// 非均匀缩放或剪切，此时同样转人工——因为第 3.2 节要求姿态缩放严格为
    /// <c>(1,1,1)</c>，静默产出非单位缩放会直接违反该约束。
    /// </para>
    /// </remarks>
    public static class CharacterBoneRebindSolver
    {
        /// <summary>Δ_B 容差。规范第 4.1 节取 1e-4。</summary>
        public const float DeltaBTolerance = 1e-4f;

        /// <summary>候选姿态缩放的允许偏差。</summary>
        public const float UnitScaleTolerance = 1e-3f;

        /// <summary>计算 Δ_B：两个绑定矩阵的逐元素最大绝对偏差。</summary>
        public static float ComputeDeltaB(CharacterMatrix4x4 oldBindPose, CharacterMatrix4x4 newBindPose)
        {
            return CharacterMatrix4x4.MaxAbsoluteDeviation(oldBindPose, newBindPose);
        }

        /// <summary>
        /// 计算候选姿态 <c>P_new = B_new⁻¹ · B_old · P_old</c>。
        /// </summary>
        public static CharacterAuthoringPose ComputeCandidatePose(
            CharacterAuthoringPose oldPose,
            CharacterMatrix4x4 oldBindPose,
            CharacterMatrix4x4 newBindPose,
            string objectPath)
        {
            if (oldPose == null)
            {
                throw new ArgumentNullException(nameof(oldPose));
            }

            if (oldBindPose == null)
            {
                throw new ArgumentNullException(nameof(oldBindPose));
            }

            if (newBindPose == null)
            {
                throw new ArgumentNullException(nameof(newBindPose));
            }

            CharacterMatrix4x4 poseMatrix = CharacterMatrix4x4.FromPose(oldPose);
            CharacterMatrix4x4 inverseNew = newBindPose.Inverse(objectPath);

            // 结合律：先算 (B_new⁻¹ · B_old)，再右乘 P_old。
            CharacterMatrix4x4 rebase = CharacterMatrix4x4.Multiply(inverseNew, oldBindPose);
            CharacterMatrix4x4 rebased = CharacterMatrix4x4.Multiply(rebase, poseMatrix);

            return rebased.ToPose(objectPath);
        }

        /// <summary>
        /// 对单根骨骼裁决重绑定。
        /// </summary>
        /// <param name="bonePath">骨骼路径，用于报告与诊断定位。</param>
        /// <param name="oldPose">创作时记录的局部姿态。</param>
        /// <param name="oldBindPose">旧骨架的绑定矩阵。</param>
        /// <param name="newBindPose">新骨架的绑定矩阵。</param>
        /// <param name="objectPath">诊断定位路径。</param>
        public static CharacterBoneRebindEntry RebindBone(
            string bonePath,
            CharacterAuthoringPose oldPose,
            CharacterMatrix4x4 oldBindPose,
            CharacterMatrix4x4 newBindPose,
            string objectPath)
        {
            float deltaB = ComputeDeltaB(oldBindPose, newBindPose);

            if (deltaB <= DeltaBTolerance)
            {
                return new CharacterBoneRebindEntry(
                    bonePath,
                    deltaB,
                    oldPose,
                    oldPose,
                    CharacterBoneRebindVerdict.NotRequired,
                    "Skeleton binding is unchanged within tolerance; pose kept as authored.");
            }

            CharacterAuthoringPose candidate = ComputeCandidatePose(oldPose, oldBindPose, newBindPose, objectPath);

            if (!IsUnitScale(candidate.Scale))
            {
                return new CharacterBoneRebindEntry(
                    bonePath,
                    deltaB,
                    oldPose,
                    candidate,
                    CharacterBoneRebindVerdict.ManualReviewRequired,
                    $"Candidate pose scale is not unit ({candidate.Scale.X}, {candidate.Scale.Y}, {candidate.Scale.Z}); " +
                    "the old/new bind matrices imply non-uniform scale or shear.");
            }

            return new CharacterBoneRebindEntry(
                bonePath,
                deltaB,
                oldPose,
                candidate,
                CharacterBoneRebindVerdict.ManualReviewRequired,
                $"Skeleton binding changed beyond tolerance (Δ_B = {deltaB} > {DeltaBTolerance}); manual review required.");
        }

        /// <summary>
        /// 构建重绑定候选报告。
        /// </summary>
        /// <param name="packageId">包标识。</param>
        /// <param name="authoredSkeletonSnapshotId">创作时锁定的骨架快照 ID。</param>
        /// <param name="entries">各骨骼的裁决明细。</param>
        /// <param name="snapshotMissing">
        /// 旧骨架快照是否丢失。为 true 时报告整体转人工（规范："旧快照丢失，立即中断"）。
        /// </param>
        public static CharacterRebindCandidateReport BuildReport(
            string packageId,
            string authoredSkeletonSnapshotId,
            IEnumerable<CharacterBoneRebindEntry> entries,
            bool snapshotMissing)
        {
            var report = new CharacterRebindCandidateReport
            {
                PackageId = packageId ?? string.Empty,
                AuthoredSkeletonSnapshotId = authoredSkeletonSnapshotId ?? string.Empty,
                SnapshotMissing = snapshotMissing
            };

            if (entries != null)
            {
                report.Entries.AddRange(entries);
            }

            return report;
        }

        /// <summary>
        /// 断言可自动重绑定；存在待人工项时抛出，避免静默发布。
        /// </summary>
        public static void AssertNoManualReview(CharacterRebindCandidateReport report, string objectPath)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (!report.RequiresManualReview)
            {
                return;
            }

            string reason = report.SnapshotMissing
                ? "authored skeleton snapshot is missing"
                : "bones changed beyond tolerance: " + string.Join(", ", report.ManualReviewBones);

            throw new CharacterDiagnosticException(
                objectPath,
                $"Bone rebind requires manual review ({reason}). Automatic rebinding is prohibited.",
                CharacterGateCodes.BonePathSemanticConflict,
                field: "authoredSkeletonSnapshotId");
        }

        private static bool IsUnitScale(CharacterAuthoringVector3 scale)
        {
            if (scale == null)
            {
                return false;
            }

            return MathF.Abs(scale.X - 1f) <= UnitScaleTolerance &&
                   MathF.Abs(scale.Y - 1f) <= UnitScaleTolerance &&
                   MathF.Abs(scale.Z - 1f) <= UnitScaleTolerance;
        }
    }
}
