using System;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestNetworkPanelPreferences
    {
        private const string Prefix = "MxFramework.CharacterTest.NetworkPanel.";
        private const string LaunchModeKey = Prefix + "launchMode";
        private const string BindAddressKey = Prefix + "bindAddress";
        private const string RemoteAddressKey = Prefix + "remoteAddress";
        private const string PortKey = Prefix + "port";
        private const string RoomNameKey = Prefix + "roomName";
        private const string DisplayNameKey = Prefix + "displayName";
        private const string AutoSyncProbeAfterEnterGameKey = Prefix + "autoSyncProbeAfterEnterGame";

        public static CharacterTestNetworkLaunchMode LoadLaunchMode(
            CharacterTestNetworkLaunchMode fallback)
        {
            int value = PlayerPrefs.GetInt(LaunchModeKey, (int)fallback);
            return Enum.IsDefined(typeof(CharacterTestNetworkLaunchMode), value)
                ? (CharacterTestNetworkLaunchMode)value
                : fallback;
        }

        public static CharacterTestNetworkSessionOptions Apply(
            CharacterTestNetworkSessionOptions options)
        {
            options = options?.Clone() ?? new CharacterTestNetworkSessionOptions();
            options.LaunchMode = LoadLaunchMode(options.LaunchMode);
            options.BindAddress = LoadString(BindAddressKey, options.BindAddress);
            options.RemoteAddress = LoadString(RemoteAddressKey, options.RemoteAddress);
            options.Port = LoadPort(options.Port);
            string roomName = LoadString(RoomNameKey, options.RoomName);
            options.RoomName = roomName;
            options.RoomId = roomName;
            options.DisplayName = LoadString(DisplayNameKey, options.DisplayName);
            return options;
        }

        public static void Save(CharacterTestNetworkSessionOptions options)
        {
            if (options == null)
                return;

            SaveLaunchMode(options.LaunchMode, saveNow: false);
            if (options.Kind == CharacterTestNetworkSessionKind.Host)
                SaveString(BindAddressKey, options.BindAddress);
            if (options.Kind == CharacterTestNetworkSessionKind.Join)
                SaveString(RemoteAddressKey, options.RemoteAddress);
            if (options.Port > 0)
                PlayerPrefs.SetInt(PortKey, options.Port);
            SaveString(RoomNameKey, string.IsNullOrWhiteSpace(options.RoomName) ? options.RoomId : options.RoomName);
            SaveString(DisplayNameKey, options.DisplayName);
            PlayerPrefs.Save();
        }

        public static bool LoadAutoSyncProbeAfterEnterGame(bool fallback = false)
        {
            return PlayerPrefs.GetInt(AutoSyncProbeAfterEnterGameKey, fallback ? 1 : 0) != 0;
        }

        public static void SaveAutoSyncProbeAfterEnterGame(bool enabled)
        {
            PlayerPrefs.SetInt(AutoSyncProbeAfterEnterGameKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SaveLaunchMode(CharacterTestNetworkLaunchMode launchMode)
        {
            SaveLaunchMode(launchMode, saveNow: true);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(LaunchModeKey);
            PlayerPrefs.DeleteKey(BindAddressKey);
            PlayerPrefs.DeleteKey(RemoteAddressKey);
            PlayerPrefs.DeleteKey(PortKey);
            PlayerPrefs.DeleteKey(RoomNameKey);
            PlayerPrefs.DeleteKey(DisplayNameKey);
            PlayerPrefs.DeleteKey(AutoSyncProbeAfterEnterGameKey);
            PlayerPrefs.Save();
        }

        private static void SaveLaunchMode(CharacterTestNetworkLaunchMode launchMode, bool saveNow)
        {
            PlayerPrefs.SetInt(LaunchModeKey, (int)launchMode);
            if (saveNow)
                PlayerPrefs.Save();
        }

        private static string LoadString(string key, string fallback)
        {
            string value = PlayerPrefs.GetString(key, fallback ?? string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static ushort LoadPort(ushort fallback)
        {
            int value = PlayerPrefs.GetInt(PortKey, fallback);
            return value > 0 && value <= ushort.MaxValue ? (ushort)value : fallback;
        }

        private static void SaveString(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                PlayerPrefs.SetString(key, value.Trim());
        }
    }
}
