using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    // Persisted protocol values. Never renumber or reuse removed codes.
    public enum CharacterTestBehaviorFailureCode
    {
        None = 0,
        ActionCommitRejected = 1,
        EntryConditionRejected = 2,
        CharacterActionUnknown = 3,
        AbilityRuntimeMissing = 4,
        AbilityRejected = 5,
        AbilityActionUnknown = 6,
        MoveTargetInvalid = 7,
        WaitInvalid = 8,
        PrimaryActionInvalid = 9,
        JumpRejected = 10,
        CommandRejected = 11,
        OrchestrationCommandRejected = 12,
        OrchestrationTerminateRejected = 13,
        // Action-owner receipt codes use a disjoint domain, offset 10000.
        ActionReceiptDomain = 10000,
        ActionOwnerDomain = 20000
    }

    internal static class AiStateHashArray
    {
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerStateArray;
        internal const int SchemaVersion = 2;
        internal const int Values = 1;
        internal const int ObjectValue = 2;

        internal static void Add(RuntimeHashAccumulator hash, int key, int[] values, int elementValueKey, int presenceKey)
        {
            hash.AddBool(presenceKey, values != null);
            hash.BeginArray(key, values?.Length ?? 0);
            for (int i = 0; values != null && i < values.Length; i++)
            { hash.BeginElement(i); hash.AddInt(elementValueKey, values[i]); hash.EndElement(); }
            hash.EndArray();
        }

        internal static void Add(RuntimeHashAccumulator hash, int key, bool[] values, int elementValueKey, int presenceKey)
        {
            hash.AddBool(presenceKey, values != null);
            hash.BeginArray(key, values?.Length ?? 0);
            for (int i = 0; values != null && i < values.Length; i++)
            { hash.BeginElement(i); hash.AddBool(elementValueKey, values[i]); hash.EndElement(); }
            hash.EndArray();
        }
    }

    internal static class CharacterTestBehaviorFailureCodes
    {
        internal static bool IsValid(int value, int detailId = 0)
        {
            if (value >= (int)CharacterTestBehaviorFailureCode.ActionOwnerDomain)
            {
                try { _ = new CharacterTestActionRejection(value - (int)CharacterTestBehaviorFailureCode.ActionOwnerDomain, detailId); return true; }
                catch (ArgumentOutOfRangeException) { return false; }
            }
            return detailId == 0 && (value >= 0 && value <= 13 || value >= 10001 && value <= 10015);
        }

        internal static string Display(CharacterTestBehaviorFailureCode reason)
        {
            switch (reason)
            {
                case CharacterTestBehaviorFailureCode.None: return string.Empty;
                case CharacterTestBehaviorFailureCode.ActionCommitRejected: return "ActionCommitRejected";
                case CharacterTestBehaviorFailureCode.EntryConditionRejected: return "BehaviorRecipe.EntryConditionRejected";
                case CharacterTestBehaviorFailureCode.CharacterActionUnknown: return "BehaviorRecipe.CharacterActionUnknown";
                case CharacterTestBehaviorFailureCode.AbilityRuntimeMissing: return "BehaviorRecipe.AbilityRuntimeMissing";
                case CharacterTestBehaviorFailureCode.AbilityRejected: return "BehaviorRecipe.AbilityRejected";
                case CharacterTestBehaviorFailureCode.AbilityActionUnknown: return "BehaviorRecipe.AbilityActionUnknown";
                case CharacterTestBehaviorFailureCode.MoveTargetInvalid: return "BehaviorRecipe.MoveTargetInvalid";
                case CharacterTestBehaviorFailureCode.WaitInvalid: return "BehaviorRecipe.WaitInvalid";
                case CharacterTestBehaviorFailureCode.PrimaryActionInvalid: return "BehaviorRecipe.PrimaryActionInvalid";
                case CharacterTestBehaviorFailureCode.JumpRejected: return "BehaviorRecipe.JumpRejected";
                case CharacterTestBehaviorFailureCode.CommandRejected: return "BehaviorExecution.CommandRejected";
                case CharacterTestBehaviorFailureCode.OrchestrationCommandRejected: return "BehaviorRecipe.OrchestrationCommandRejected";
                case CharacterTestBehaviorFailureCode.OrchestrationTerminateRejected: return "BehaviorRecipe.OrchestrationTerminateRejected";
                default: return string.Empty;
            }
        }
    }

    internal static class AiPreparedSha256
    {
        // Called only by immutable content construction/import, never by state writers.
        internal static RuntimeHashDigest Parse(string hex)
        {
            if (hex == null || hex.Length != 64)
                throw new ArgumentException("A complete SHA-256 fingerprint is required.", nameof(hex));
            return new RuntimeHashDigest(Word(hex, 0), Word(hex, 16), Word(hex, 32), Word(hex, 48));
        }

        private static ulong Word(string hex, int start)
        {
            ulong result = 0;
            for (int i = 0; i < 8; i++)
                result |= (ulong)((Nibble(hex[start + i * 2]) << 4) | Nibble(hex[start + i * 2 + 1])) << (i * 8);
            return result;
        }

        private static int Nibble(char c) => c >= '0' && c <= '9' ? c - '0'
            : c >= 'a' && c <= 'f' ? c - 'a' + 10
            : c >= 'A' && c <= 'F' ? c - 'A' + 10
            : throw new ArgumentException("A SHA-256 fingerprint contains an invalid hexadecimal digit.");
    }
}

