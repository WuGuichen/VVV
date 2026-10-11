using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Platform.Unity
{
    /// <summary>
    /// Captures Unity host environment facts once for no-allocation reads.
    /// </summary>
    public sealed class UnityPlatformEnvironmentProvider : IPlatformEnvironmentProvider
    {
        private readonly PlatformEnvironment _current;

        /// <summary>
        /// Captures the current Unity environment.
        /// </summary>
        /// <remarks>LowFreqAlloc during composition. <see cref="Current"/> is NoAlloc.</remarks>
        public UnityPlatformEnvironmentProvider()
            : this(
                Application.platform,
                Application.isEditor,
                Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
        }

        internal UnityPlatformEnvironmentProvider(
            RuntimePlatform runtimePlatform,
            bool isEditor,
            bool isHeadless)
        {
            _current = new PlatformEnvironment(
                MapFamily(runtimePlatform),
                isEditor,
                isHeadless,
                runtimePlatform.ToString());
        }

        /// <inheritdoc />
        public PlatformEnvironment Current => _current;

        /// <summary>
        /// Maps a Unity runtime platform to the stable framework family.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public static PlatformFamily MapFamily(RuntimePlatform runtimePlatform)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsServer:
                case RuntimePlatform.WSAPlayerARM:
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerX86:
                    return PlatformFamily.Windows;

                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXServer:
                    return PlatformFamily.MacOS;

                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxServer:
                case RuntimePlatform.EmbeddedLinuxArm64:
                case RuntimePlatform.EmbeddedLinuxX64:
                    return PlatformFamily.Linux;

                case RuntimePlatform.Android:
                    return PlatformFamily.Android;

                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS:
                case RuntimePlatform.VisionOS:
                    return PlatformFamily.IOS;

                case RuntimePlatform.WebGLPlayer:
                    return PlatformFamily.Web;

                case RuntimePlatform.PS4:
                case RuntimePlatform.PS5:
                case RuntimePlatform.XboxOne:
                case RuntimePlatform.GameCoreXboxOne:
                case RuntimePlatform.GameCoreXboxSeries:
                case RuntimePlatform.Switch:
                case RuntimePlatform.Switch2:
                    return PlatformFamily.Console;

                default:
                    return PlatformFamily.Unknown;
            }
        }
    }
}
