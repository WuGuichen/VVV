using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Config.Runtime
{
    public static class RuntimeModDiagnosticSnapshotJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        public static string SaveToJson(RuntimeModDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (!string.Equals(snapshot.Format, RuntimeModDiagnosticSnapshot.ExpectedFormat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Snapshot format mismatch. expected='{RuntimeModDiagnosticSnapshot.ExpectedFormat}', actual='{snapshot.Format ?? "(null)"}'.");
            }

            return JsonConvert.SerializeObject(snapshot, Settings);
        }

        public static RuntimeModDiagnosticSnapshot LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Snapshot json is null or empty.");

            RuntimeModDiagnosticSnapshot snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<RuntimeModDiagnosticSnapshot>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse snapshot json: {ex.Message}", ex);
            }

            if (snapshot == null)
                throw new InvalidOperationException("Snapshot json parsed to null.");

            if (!string.Equals(snapshot.Format, RuntimeModDiagnosticSnapshot.ExpectedFormat, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Snapshot format mismatch. expected='{RuntimeModDiagnosticSnapshot.ExpectedFormat}', actual='{snapshot.Format ?? "(null)"}'.");
            }

            return snapshot;
        }
    }
}
