using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 武器插槽的校验视图。与 <c>weapon_slots.json</c> 的实际 DTO 解耦，
    /// 只依赖第 1.5 节 Schema 要求的字段。
    /// </summary>
    public sealed class CharacterWeaponSlotView
    {
        public string SlotId { get; set; } = string.Empty;

        public string AttachSocketId { get; set; } = string.Empty;

        public List<string> AllowedWeaponTypes { get; set; } = new List<string>();

        public string DefaultWeaponId { get; set; }
    }

    /// <summary>
    /// 武器插槽语义门禁。对应规范 v1.4 第 1.5 节与第 7.1 节 <c>E-SLOT-301</c>。
    /// </summary>
    /// <remarks>
    /// <para>门禁内容：</para>
    /// <list type="bullet">
    /// <item><description><c>E-SLOT-301</c>：<c>attachSocketId</c> 在 <c>sockets.json</c> 中不存在（悬空引用）。</description></item>
    /// <item><description><c>slotId</c> 唯一性（修订矩阵"补齐语义门禁：资源键与槽位 ID 唯一性校验"）。</description></item>
    /// <item><description><c>allowedWeaponTypes</c> 至少一项且取值必须落在第 1.5 节枚举内。</description></item>
    /// </list>
    /// </remarks>
    public static class CharacterWeaponSlotSemanticValidator
    {
        /// <summary>第 1.5 节 <c>allowedWeaponTypes</c> 的合法取值。</summary>
        public static readonly string[] AllowedWeaponTypes = { "SwordSingle", "GreatSword", "Shield", "Bow", "Dagger" };

        /// <summary>校验并返回全部问题（不抛异常）。</summary>
        public static IReadOnlyList<CharacterAuthoringValidationIssue> Validate(
            IReadOnlyList<CharacterWeaponSlotView> slots,
            IReadOnlyCollection<string> availableSocketIds,
            string sourcePath)
        {
            var issues = new List<CharacterAuthoringValidationIssue>();
            if (slots == null)
            {
                return issues;
            }

            var sockets = new HashSet<string>(StringComparer.Ordinal);
            if (availableSocketIds != null)
            {
                foreach (string socketId in availableSocketIds)
                {
                    if (!string.IsNullOrEmpty(socketId))
                    {
                        sockets.Add(socketId);
                    }
                }
            }

            var seenSlotIds = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < slots.Count; i++)
            {
                CharacterWeaponSlotView slot = slots[i];
                string path = "weaponSlots[" + i + "]";

                if (slot == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(slot.SlotId))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.WeaponSlotSocketDangling,
                        sourcePath,
                        path + ".slotId",
                        "slotId",
                        "slotId is required."));
                }
                else if (seenSlotIds.TryGetValue(slot.SlotId, out int firstIndex))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.WeaponSlotSocketDangling,
                        sourcePath,
                        path + ".slotId",
                        "slotId",
                        $"Duplicate slotId '{slot.SlotId}' (first declared at weaponSlots[{firstIndex}])."));
                }
                else
                {
                    seenSlotIds.Add(slot.SlotId, i);
                }

                if (string.IsNullOrWhiteSpace(slot.AttachSocketId))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.WeaponSlotSocketDangling,
                        sourcePath,
                        path + ".attachSocketId",
                        "attachSocketId",
                        "attachSocketId is required."));
                }
                else if (!sockets.Contains(slot.AttachSocketId))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.WeaponSlotSocketDangling,
                        sourcePath,
                        path + ".attachSocketId",
                        "attachSocketId",
                        $"attachSocketId '{slot.AttachSocketId}' does not exist in sockets.json."));
                }

                if (slot.AllowedWeaponTypes == null || slot.AllowedWeaponTypes.Count == 0)
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.WeaponSlotSocketDangling,
                        sourcePath,
                        path + ".allowedWeaponTypes",
                        "allowedWeaponTypes",
                        "allowedWeaponTypes must declare at least one weapon type."));
                }
                else
                {
                    for (int t = 0; t < slot.AllowedWeaponTypes.Count; t++)
                    {
                        string weaponType = slot.AllowedWeaponTypes[t];
                        if (Array.IndexOf(AllowedWeaponTypes, weaponType) < 0)
                        {
                            issues.Add(CharacterGateCodes.CreateIssue(
                                CharacterGateCodes.WeaponSlotSocketDangling,
                                sourcePath,
                                path + ".allowedWeaponTypes[" + t + "]",
                                "allowedWeaponTypes",
                                $"Unknown weapon type '{weaponType}'. Expected one of: {string.Join(", ", AllowedWeaponTypes)}."));
                        }
                    }
                }
            }

            return issues;
        }

        /// <summary>校验并在首个违规处抛出诊断异常。</summary>
        public static void AssertValid(
            IReadOnlyList<CharacterWeaponSlotView> slots,
            IReadOnlyCollection<string> availableSocketIds,
            string sourcePath)
        {
            IReadOnlyList<CharacterAuthoringValidationIssue> issues = Validate(slots, availableSocketIds, sourcePath);
            if (issues.Count == 0)
            {
                return;
            }

            CharacterAuthoringValidationIssue issue = issues[0];
            throw new CharacterDiagnosticException(
                issue.SourceObjectPath,
                issue.Message,
                issue.Code,
                sourcePath,
                issue.Field);
        }
    }
}
