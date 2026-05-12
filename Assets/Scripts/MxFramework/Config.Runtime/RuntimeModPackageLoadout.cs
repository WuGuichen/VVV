using System;
using System.Collections.Generic;

namespace MxFramework.Config.Runtime
{
    /// <summary>
    /// Persisted mod package enable-state profile.
    /// </summary>
    public sealed class RuntimeModPackageLoadout
    {
        public const string ExpectedFormat = "mx.modLoadout.v1";

        public RuntimeModPackageLoadout(
            string profileId,
            IReadOnlyList<string> enabledPackageKeys,
            string displayName = "",
            string format = ExpectedFormat,
            string updatedUtc = "")
        {
            Format = string.IsNullOrWhiteSpace(format) ? ExpectedFormat : format;
            ProfileId = profileId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EnabledPackageKeys = enabledPackageKeys ?? Array.Empty<string>();
            UpdatedUtc = updatedUtc ?? string.Empty;
        }

        public string Format { get; }
        public string ProfileId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> EnabledPackageKeys { get; }
        public string UpdatedUtc { get; }

        public RuntimeModPackageLoadout WithUpdatedUtcNow()
        {
            return new RuntimeModPackageLoadout(
                profileId: ProfileId,
                enabledPackageKeys: EnabledPackageKeys,
                displayName: DisplayName,
                format: Format,
                updatedUtc: DateTime.UtcNow.ToString("O"));
        }
    }
}
