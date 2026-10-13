using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum CharacterTacticalRewardState { Pending = 1, Claimed = 2 }
    internal enum CharacterTacticalRewardClaimStatus { Claimed, AlreadyClaimed, Busy, Rejected, SaveFailed }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalRewardRecipe
    {
        public const string FileName = "tactical_slice_rewards.json";
        public string Schema { get; set; } = "mx.character-test.tactical-fixed-rewards.v1";
        public string RecipeId { get; set; }
        public int Revision { get; set; }
        public CharacterTacticalContentGrantRequest[] Entries { get; set; }
    }

    internal sealed class CharacterTacticalRewardLine
    {
        public CharacterTacticalContentGrantRequest Grant { get; set; }
        public string Label { get; set; }
        public string ContentFingerprint { get; set; }
        public string RequestFingerprint { get; set; }
    }

    internal sealed class CharacterTacticalRewardGrantReceipt
    {
        public long RequestId { get; set; }
        public CharacterTacticalContentKind Kind { get; set; }
        public int ItemInstanceId { get; set; }
        public ulong CardInstanceId { get; set; }
    }

    /// <summary>Frozen result entitlement. Status/receipts change only in the same durable commit as player inventory.</summary>
    internal sealed class CharacterTacticalRewardBundle
    {
        public string Schema { get; set; } = "mx.character-test.battle-rewards.v1";
        public string BattleId { get; set; }
        public string RecipeId { get; set; }
        public int RecipeRevision { get; set; }
        public int OwnerId { get; set; }
        public int CharacterItemInstanceId { get; set; }
        public string CharacterStableId { get; set; }
        public string CharacterDisplayName { get; set; }
        public CharacterTacticalRewardLine[] Lines { get; set; }
        public string Fingerprint { get; set; }
        public CharacterTacticalRewardState State { get; set; } = CharacterTacticalRewardState.Pending;
        public string ClaimedAtUtc { get; set; }
        public CharacterTacticalRewardGrantReceipt[] Receipts { get; set; }
    }

    internal sealed class CharacterTacticalRewardBatch
    {
        internal CharacterTacticalPlayerState Player { get; set; }
        internal CharacterTacticalRewardGrantReceipt[] Receipts { get; set; }
    }

    internal sealed class CharacterTacticalRewardClaimResult
    {
        internal CharacterTacticalRewardClaimStatus Status { get; }
        internal string Issue { get; }
        internal CharacterTacticalBattleSettlement Settlement { get; }
        internal bool Success => Status == CharacterTacticalRewardClaimStatus.Claimed || Status == CharacterTacticalRewardClaimStatus.AlreadyClaimed;
        internal CharacterTacticalRewardClaimResult(CharacterTacticalRewardClaimStatus status, string issue = "", CharacterTacticalBattleSettlement settlement = null)
        { Status = status; Issue = issue; Settlement = settlement; }
    }

    internal static class CharacterTacticalRewardRules
    {
        internal const string ProgressSchema = "mx.character-test.tactical-slice-progress.v2";
        internal static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        internal static string Digest(string text)
        {
            var hash = RuntimeHashDigest.ComputeContentDigest(text);
            return hash.Word0.ToString("x16") + hash.Word1.ToString("x16") + hash.Word2.ToString("x16") + hash.Word3.ToString("x16");
        }
        // Request identities are fixed when the entitlement is created, never regenerated during a retry.
        internal static long RequestId(string battleId, int index)
        {
            ulong value = RuntimeHashDigest.ComputeContentDigest("battle-reward-v1:" + battleId + ":" + index).Word0;
            return (long)(value & 0x3fffffffffffffffUL) + 0x4000000000000000L;
        }
        internal static string Fingerprint(CharacterTacticalRewardBundle value) => Digest(JsonConvert.SerializeObject(new {
            value.Schema, value.BattleId, value.RecipeId, value.RecipeRevision, value.OwnerId,
            value.CharacterItemInstanceId, value.CharacterStableId, value.CharacterDisplayName, value.Lines
        }));

        /// <remarks>LowFreqAlloc. Validate stored intent independently of current authoring; missing definitions cannot reroll it.</remarks>
        internal static void Validate(CharacterTacticalBattleSettlement result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.BattleId)) throw new JsonSerializationException("TACTICAL_SETTLEMENT_INVALID");
            var value = result.Rewards;
            if (value == null) return; // Old settlements and defeat/draw never receive retroactive rewards.
            if (result.Draw || result.WinningTeamId != result.PlayerTeamId || result.PlayerTeamId <= 0
                || value.Schema != "mx.character-test.battle-rewards.v1" || value.BattleId != result.BattleId
                || string.IsNullOrWhiteSpace(value.RecipeId) || value.RecipeRevision <= 0 || value.OwnerId <= 0
                || value.CharacterItemInstanceId <= 0 || string.IsNullOrWhiteSpace(value.CharacterStableId)
                || string.IsNullOrWhiteSpace(value.CharacterDisplayName) || value.Lines == null || value.Lines.Length == 0
                || value.Fingerprint != Fingerprint(value)
                || !Enum.IsDefined(typeof(CharacterTacticalRewardState), value.State))
                throw new JsonSerializationException("TACTICAL_REWARD_MANIFEST_INVALID");
            var requests = new HashSet<long>();
            foreach (var line in value.Lines)
            {
                var grant = line?.Grant;
                if (grant == null || grant.RequestId <= 0 || !requests.Add(grant.RequestId) || grant.OwnerId != value.OwnerId
                    || grant.ExpectedPlayerRevision != 0 || grant.ExpectedCharacterRevision != 0 || grant.Frame != 0
                    || string.IsNullOrWhiteSpace(line.Label) || line.ContentFingerprint?.Length != 64 || line.RequestFingerprint?.Length != 64
                    || !Enum.IsDefined(typeof(CharacterTacticalContentKind), grant.Kind) || grant.Kind == CharacterTacticalContentKind.Unspecified
                    || ((grant.Kind == CharacterTacticalContentKind.MainCard || grant.Kind == CharacterTacticalContentKind.Action)
                        ? grant.CharacterItemInstanceId != value.CharacterItemInstanceId : grant.CharacterItemInstanceId != 0))
                    throw new JsonSerializationException("TACTICAL_REWARD_LINE_INVALID");
            }
            if (value.State == CharacterTacticalRewardState.Pending)
            {
                if (value.Receipts != null || value.ClaimedAtUtc != null) throw new JsonSerializationException("TACTICAL_REWARD_PENDING_INVALID");
                return;
            }
            if (!DateTimeOffset.TryParse(value.ClaimedAtUtc, out _) || value.Receipts?.Length != value.Lines.Length)
                throw new JsonSerializationException("TACTICAL_REWARD_RECEIPTS_INVALID");
            for (int index = 0; index < value.Lines.Length; index++)
            {
                var receipt = value.Receipts[index]; var grant = value.Lines[index].Grant;
                if (receipt == null || receipt.RequestId != grant.RequestId || receipt.Kind != grant.Kind || receipt.ItemInstanceId < 0
                    || (grant.Kind == CharacterTacticalContentKind.MainCard) != (receipt.CardInstanceId != 0)
                    || ((grant.Kind == CharacterTacticalContentKind.Accessory || grant.Kind == CharacterTacticalContentKind.Character)
                        != (receipt.ItemInstanceId > 0)))
                    throw new JsonSerializationException("TACTICAL_REWARD_RECEIPTS_INVALID");
            }
        }

        internal static void Merge(List<CharacterTacticalBattleSettlement> results, CharacterTacticalBattleSettlement incoming)
        {
            Validate(incoming);
            var existing = results.Find(value => value.BattleId == incoming.BattleId);
            if (existing == null)
            {
                if (incoming.Rewards?.State == CharacterTacticalRewardState.Claimed)
                    throw new InvalidOperationException("TACTICAL_REWARD_CLAIM_REQUIRES_TRANSACTION");
                results.Add(Copy(incoming)); return;
            }
            if (existing.WinningTeamId != incoming.WinningTeamId || existing.Draw != incoming.Draw
                || existing.PlayerTeamId != incoming.PlayerTeamId || existing.Rewards?.Fingerprint != incoming.Rewards?.Fingerprint)
                throw new InvalidOperationException("TACTICAL_SETTLEMENT_CONFLICT");
            // An old terminal callback must never downgrade a durable Claimed result.
        }
    }
}
