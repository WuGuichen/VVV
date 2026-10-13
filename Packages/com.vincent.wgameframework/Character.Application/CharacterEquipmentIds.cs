using System;

namespace MxFramework.CharacterApplication
{
    /// <summary>Common equipment slot ids used by Character.Application configs and bridges.</summary>
    public static class CharacterEquipmentSlotIds
    {
        public const int MainHand = 1;
        public const string MainHandLabel = "mainHand";
        public const int OffHand = 2;
        public const string OffHandLabel = "offHand";
        public const int TwoHand = 3;
        public const string TwoHandLabel = "twoHand";
        public const int Head = 4;
        public const string HeadLabel = "head";
        public const int Body = 5;
        public const string BodyLabel = "body";
        public const int Back = 6;
        public const string BackLabel = "back";
        public const int Accessory = 7;
        public const string AccessoryLabel = "accessory";
        public const int NaturalWeapon = 8;
        public const string NaturalWeaponLabel = "naturalWeapon";
    }

    /// <summary>Common weapon tag ids used by equipment state match rules.</summary>
    public static class CharacterWeaponTagIds
    {
        public const string Blade = "blade";
        public const string OneHand = "one_hand";
        public const string Shield = "shield";
        public const string Guard = "guard";
    }

    /// <summary>Helpers for building slot-scoped equipment match rule tokens.</summary>
    public static class EquipmentMatchRuleToken
    {
        /// <summary>Creates a numeric slot-scoped match token such as "1:blade".</summary>
        /// <remarks>Allocation: LowFreqAlloc. Use while building configs, not inside simulation ticks.</remarks>
        public static string Create(int slotId, string token)
        {
            if ((slotId <= 0))
                throw new ArgumentException("Equipment match slot id cannot be null or empty.", nameof(slotId));
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException("Equipment match token cannot be null or empty.", nameof(token));

            return slotId + ":" + token;
        }
    }
}
