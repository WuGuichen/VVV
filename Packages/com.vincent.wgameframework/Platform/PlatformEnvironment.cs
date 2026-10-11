using System;

namespace MxFramework.Platform
{
    /// <summary>
    /// Identifies a normalized host platform family.
    /// </summary>
    public enum PlatformFamily
    {
        Unknown = 0,
        Windows = 1,
        MacOS = 2,
        Linux = 3,
        Android = 4,
        IOS = 5,
        Web = 6,
        Console = 7
    }

    /// <summary>
    /// Contains normalized, non-authoritative host environment facts.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected.</remarks>
    public readonly struct PlatformEnvironment : IEquatable<PlatformEnvironment>
    {
        private readonly string _runtimeName;

        /// <summary>
        /// Creates an environment fact value.
        /// </summary>
        /// <remarks>LowFreqAlloc only when the caller creates the runtime-name string.</remarks>
        public PlatformEnvironment(
            PlatformFamily family,
            bool isEditor,
            bool isHeadless,
            string runtimeName = "")
        {
            Family = family;
            IsEditor = isEditor;
            IsHeadless = isHeadless;
            _runtimeName = runtimeName ?? string.Empty;
        }

        public PlatformFamily Family { get; }
        public bool IsEditor { get; }
        public bool IsHeadless { get; }

        /// <summary>
        /// Gets a diagnostics-only runtime name that must not drive rules, ordering, hashing, or SaveState compatibility.
        /// </summary>
        public string RuntimeName => _runtimeName ?? string.Empty;

        public bool Equals(PlatformEnvironment other)
        {
            return Family == other.Family
                && IsEditor == other.IsEditor
                && IsHeadless == other.IsHeadless
                && string.Equals(RuntimeName, other.RuntimeName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformEnvironment other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Family;
                hashCode = (hashCode * 397) ^ IsEditor.GetHashCode();
                hashCode = (hashCode * 397) ^ IsHeadless.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(RuntimeName);
                return hashCode;
            }
        }

        public static bool operator ==(PlatformEnvironment left, PlatformEnvironment right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformEnvironment left, PlatformEnvironment right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Supplies the current normalized host environment.
    /// </summary>
    public interface IPlatformEnvironmentProvider
    {
        /// <summary>
        /// Gets the current environment facts.
        /// </summary>
        /// <remarks>NoAlloc after provider initialization.</remarks>
        PlatformEnvironment Current { get; }
    }
}
