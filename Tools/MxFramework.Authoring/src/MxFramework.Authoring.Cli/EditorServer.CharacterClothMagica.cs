using System.Net;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterClothOpenRequestRelativePath =
        "Temp/MxFrameworkAuthoring/CharacterCloth/open_request.json";

    private static bool TryHandleCharacterClothMagicaApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path != "/api/character/cloth-magica/open" || context.Request.HttpMethod != "POST")
            return false;
        string packageRelative = ResolvePackageRelative(context, rootPath, defaultPackage);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string requestId = Guid.NewGuid().ToString("N");
        var payload = new
        {
            requestId,
            package = packageRelative.Replace('\\', '/'),
            packageId = package.Manifest.PackageId,
            createdUtc = DateTimeOffset.UtcNow.ToString("O")
        };
        string requestPath = ResolveSafePath(rootPath, CharacterClothOpenRequestRelativePath);
        WriteJsonFileAtomic(requestPath, payload, jsonOptions);
        WriteJson(context.Response, new
        {
            status = "queued",
            requestId,
            package = packageRelative,
            packageId = package.Manifest.PackageId,
            requestPath = CharacterClothOpenRequestRelativePath
        }, jsonOptions, 202);
        return true;
    }

    private static object ReadCharacterClothMagicaState(
        string rootPath,
        string packageRelative,
        string packageId)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string configPath = Path.Combine(packagePath, "config", "character_cloth_magica.json");
        int clothCount = 0;
        int colliderCount = 0;
        string schemaVersion = string.Empty;
        string backend = string.Empty;
        bool enabled = false;
        string[] presets = Array.Empty<string>();
        if (File.Exists(configPath))
        {
            try
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(configPath));
                JsonElement root = json.RootElement;
                schemaVersion = root.TryGetProperty("schemaVersion", out JsonElement schema) ? schema.GetString() ?? string.Empty : string.Empty;
                backend = root.TryGetProperty("backend", out JsonElement backendValue) ? backendValue.GetString() ?? string.Empty : string.Empty;
                enabled = root.TryGetProperty("enabled", out JsonElement enabledValue) && enabledValue.ValueKind == JsonValueKind.True;
                var presetList = new List<string>();
                if (root.TryGetProperty("cloths", out JsonElement cloths) && cloths.ValueKind == JsonValueKind.Array)
                {
                    clothCount = cloths.GetArrayLength();
                    foreach (JsonElement cloth in cloths.EnumerateArray())
                    {
                        if (cloth.TryGetProperty("colliders", out JsonElement colliders) && colliders.ValueKind == JsonValueKind.Array)
                            colliderCount += colliders.GetArrayLength();
                        if (cloth.TryGetProperty("presetAssetPath", out JsonElement preset))
                        {
                            string value = preset.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(value)) presetList.Add(Path.GetFileNameWithoutExtension(value));
                        }
                    }
                }
                presets = presetList.Distinct(StringComparer.Ordinal).ToArray();
            }
            catch (JsonException) { }
        }

        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId);
        string generatedConfig = Path.Combine(generatedRoot, "config", "character_cloth_magica.json");
        string receiptPath = Path.Combine(generatedRoot, "package_cache", "unity_editor_import_receipt.json");
        bool receiptReady = false;
        if (File.Exists(receiptPath))
        {
            try
            {
                using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
                receiptReady = receipt.RootElement.TryGetProperty("ready", out JsonElement ready) && ready.ValueKind == JsonValueKind.True;
            }
            catch (JsonException) { }
        }
        bool configured = File.Exists(configPath);
        bool supportedSchema = schemaVersion == "1.0" || schemaVersion == "1.1";
        string status = !configured ? "NotConfigured"
            : !supportedSchema || backend != "MagicaCloth2" ? "Blocked"
            : !enabled ? "Disabled"
            : !File.Exists(generatedConfig) || !receiptReady ? "UnityPending"
            : "Ready";
        return new
        {
            configured,
            enabled,
            schemaVersion,
            backend,
            clothCount,
            colliderCount,
            presets,
            generatedConfigExists = File.Exists(generatedConfig),
            receiptReady,
            status,
            configPath = ToProjectRelativePath(rootPath, configPath)
        };
    }
}
