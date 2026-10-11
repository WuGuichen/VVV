using System;
using Newtonsoft.Json;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxCompositionLayerClipboardData
    {
        [JsonProperty("layer")] public VfxCompositionLayerView Layer;
        [JsonProperty("localElement", NullValueHandling = NullValueHandling.Ignore)] public VfxSourceElementView LocalElement;
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("curationHash")] public string CurationHash = string.Empty;
        [JsonProperty("elementHash")] public string ElementHash = string.Empty;
    }

    /// <summary>Serializes a composition Layer and its optional draft-private Element for cross-draft paste.</summary>
    /// <remarks>Editor-only, AllocByDesign.</remarks>
    internal static class VfxCompositionLayerClipboard
    {
        internal const string SchemaVersion = "mx.vfx.composition-layer-clipboard.v1";
        internal const int MaximumTextLength = 4 * 1024 * 1024;
        private const string TextPrefix = "MXVFX_LAYER_CLIPBOARD_V1\n";
        private static readonly JsonSerializerSettings ClipboardJsonSettings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MaxDepth = 64
        };

        [Serializable]
        private sealed class Payload
        {
            [JsonProperty("schemaVersion")] public string Schema = string.Empty;
            [JsonProperty("data")] public VfxCompositionLayerClipboardData Data;
        }

        internal static string Serialize(
            VfxCompositionLayerView layer,
            VfxSourceElementView localElement,
            VfxSourceElementView resolvedElement,
            string sourceIndexHash,
            string curationHash)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (resolvedElement == null) throw new ArgumentNullException(nameof(resolvedElement));
            var data = new VfxCompositionLayerClipboardData
            {
                Layer = layer,
                LocalElement = localElement,
                SourceIndexHash = sourceIndexHash ?? string.Empty,
                CurationHash = curationHash ?? string.Empty,
                ElementHash = HashElement(resolvedElement)
            };
            return TextPrefix + JsonConvert.SerializeObject(
                new Payload { Schema = SchemaVersion, Data = data },
                Formatting.None,
                ClipboardJsonSettings);
        }

        internal static bool TryDeserialize(string text, out VfxCompositionLayerClipboardData data, out string message)
        {
            data = null;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(TextPrefix, StringComparison.Ordinal))
            {
                message = "剪贴板中没有 VFX Layer。";
                return false;
            }
            if (text.Length > MaximumTextLength)
            {
                message = "剪贴板中的 VFX Layer 超过 4 MB 安全上限。";
                return false;
            }

            try
            {
                Payload payload = JsonConvert.DeserializeObject<Payload>(
                    text.Substring(TextPrefix.Length),
                    ClipboardJsonSettings);
                if (payload == null
                    || !string.Equals(payload.Schema, SchemaVersion, StringComparison.Ordinal)
                    || payload.Data?.Layer == null)
                {
                    message = "剪贴板内容不是受支持的 VFX Layer。";
                    return false;
                }

                data = payload.Data;
                if (string.IsNullOrWhiteSpace(data.Layer.LayerId)
                    || string.IsNullOrWhiteSpace(data.Layer.ElementId)
                    || string.IsNullOrWhiteSpace(data.SourceIndexHash)
                    || string.IsNullOrWhiteSpace(data.CurationHash)
                    || string.IsNullOrWhiteSpace(data.ElementHash))
                {
                    data = null;
                    message = "剪贴板 VFX Layer 缺少身份或依赖证据。";
                    return false;
                }
                if (data.LocalElement != null
                    && !string.Equals(data.LocalElement.ElementId, data.Layer.ElementId, StringComparison.Ordinal))
                {
                    data = null;
                    message = "剪贴板中的私有 Element 与 Layer 引用不一致。";
                    return false;
                }
                return true;
            }
            catch (JsonException)
            {
                data = null;
                message = "剪贴板内容不是有效的 VFX Layer 数据。";
                return false;
            }
        }

        internal static string HashElement(VfxSourceElementView element)
        {
            if (element == null) return string.Empty;
            string json = JsonConvert.SerializeObject(element, Formatting.None, ClipboardJsonSettings);
            return VfxSourceElementCurationService.Hash(json);
        }

        internal static bool TryValidateEvidence(
            VfxCompositionLayerClipboardData data,
            VfxSourceElementCatalog catalog,
            out VfxSourceElementView resolvedElement,
            out string message)
        {
            resolvedElement = null;
            message = string.Empty;
            if (data?.Layer == null || catalog == null)
            {
                message = "剪贴板 VFX Layer 或原材料目录不可用。";
                return false;
            }
            if (!string.Equals(data.SourceIndexHash, catalog.Index?.IndexHash, StringComparison.Ordinal)
                || !string.Equals(data.CurationHash, catalog.Curation?.CurationHash, StringComparison.Ordinal))
            {
                message = "复制后原材料索引或策展目录已经变化；请重新复制该 Layer，或先完成依赖重映射。";
                return false;
            }

            resolvedElement = data.LocalElement ?? catalog.Find(data.Layer.ElementId);
            if (resolvedElement == null)
            {
                message = "当前草稿无法解析被复制 Layer 的 Element：" + data.Layer.ElementId;
                return false;
            }
            if (!string.Equals(data.ElementHash, HashElement(resolvedElement), StringComparison.Ordinal))
            {
                resolvedElement = null;
                message = "被复制 Layer 的 Element 内容已经变化；请重新复制，避免静默绑定到不同成员。";
                return false;
            }
            return true;
        }
    }
}
