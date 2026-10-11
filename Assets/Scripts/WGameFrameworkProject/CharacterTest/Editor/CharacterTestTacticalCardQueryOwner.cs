using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using FairyGUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>On-demand read of existing visible card DTOs. Never prepares, saves, refreshes or changes Play.</summary>
    [InitializeOnLoad]
    internal static class CharacterTestTacticalCardQueryOwner
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MxFramework/TacticalCardQuery"));
        private static double _nextPoll, _nextHeartbeat;
        static CharacterTestTacticalCardQueryOwner() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.25;
            try
            {
                if (now >= _nextHeartbeat)
                {
                    _nextHeartbeat = now + 2;
                    Directory.CreateDirectory(Folder);
                    Atomic(Path.Combine(Folder, "owner.json"), new JObject {
                        ["schema"] = "mx.tactical-card.query-owner.v1", ["updatedUtc"] = DateTime.UtcNow.ToString("O"),
                        ["playing"] = EditorApplication.isPlaying, ["compiling"] = EditorApplication.isCompiling });
                }
                string requests = Path.Combine(Folder, "requests");
                if (!Directory.Exists(requests) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                foreach (string path in Directory.EnumerateFiles(requests, "*.json").Take(4))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (!Guid.TryParseExact(id, "N", out _)) continue;
                    JObject response;
                    try
                    {
                        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Query request exceeds 16 KiB.");
                        var request = JObject.Parse(File.ReadAllText(path));
                        if ((string)request["id"] != id || (string)request["schema"] != "mx.tactical-card.query-request.v1")
                            throw new InvalidDataException("Invalid query request identity.");
                        if (!DateTime.TryParse((string)request["createdUtc"], out var created) || DateTime.UtcNow - created.ToUniversalTime() > TimeSpan.FromSeconds(10))
                            response = new JObject { ["status"] = "expired" };
                        else response = ReadVisible((JObject)request["options"]);
                    }
                    catch (Exception error) { response = new JObject { ["status"] = "error", ["message"] = error.Message }; }
                    response["schema"] = "mx.tactical-card.query.v1"; response["operationId"] = id;
                    Atomic(Path.Combine(Folder, "responses", id + ".json"), response);
                    File.Delete(path);
                }
            }
            catch (IOException) { /* A bounded client request reports its own timeout. */ }
            catch (UnauthorizedAccessException) { }
        }
        internal static JObject ReadVisible(JObject options)
        {
            string Q(string name)
            {
                string value = (string)(options[name] ?? options[name.Substring(0, 1).ToUpperInvariant() + name.Substring(1)]);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            string name = Q("name"), contains = Q("contains"), id = Q("id"), instance = Q("instanceId");
            if (new[] { name, contains, id, instance }.Count(s => !string.IsNullOrWhiteSpace(s)) != 1)
                throw new ArgumentException("Exactly one selector is required.");
            var root = typeof(GRoot).GetField("_inst", Members)?.GetValue(null) as GComponent;
            var cards = new JArray();
            if (root == null) return new JObject { ["status"] = "not-found", ["candidates"] = cards };
            var pending = new Stack<GObject>(); pending.Push(root);
            var seen = new HashSet<string>(); var timer = Stopwatch.StartNew(); int visited = 0;
            while (pending.Count > 0 && visited++ < 10000 && timer.ElapsedMilliseconds < 100)
            {
                var item = pending.Pop();
                if (!item.visible) continue;
                if (item is GComponent component)
                {
                    var title = component.GetChild("name")?.asTextField;
                    if (title != null && component.GetChild("behaviorHints") != null)
                    {
                        var bridge = Get(component.onRollOver, "_bridge");
                        var callback = bridge == null ? null : Get(bridge, "_callback0") as Delegate;
                        foreach (var handler in callback?.GetInvocationList() ?? Array.Empty<Delegate>())
                        {
                            if (handler.Target?.GetType().Name != "CharacterTestFairyGuiCardUiPreview") continue;
                            object data = Get(handler.Target, "_data"); if (data == null) continue;
                            string recordId = Get(data, "Id") as string, token = Get(data, "Token") as string;
                            string stableId = null, playerInstanceId = null; int? revision = null;
                            int at = token?.IndexOf('@') ?? -1, colon = token?.IndexOf(':', Math.Max(0, at)) ?? -1;
                            if (at > 0 && colon > at && int.TryParse(token.Substring(at + 1, colon - at - 1), out int parsed))
                            { stableId = token.Substring(0, at); revision = parsed; int slash = token.IndexOf('/', colon); if (slash >= 0) playerInstanceId = token.Substring(slash + 1); }
                            bool match = name != null ? title.text == name : contains != null ? title.text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0
                                : id != null ? recordId == id || stableId == id : playerInstanceId == instance;
                            if (!match || !seen.Add(recordId + "/" + token)) continue;
                            var card = new JObject { ["title"] = title.text, ["recordId"] = recordId, ["stableId"] = stableId,
                                ["instanceId"] = playerInstanceId, ["revision"] = revision, ["sourceLayer"] = "live-ui-cache",
                                ["description"] = Get(data, "Description") as string, ["descriptionDetails"] = Get(data, "DescriptionDetails") as string,
                                ["unboundDescription"] = Get(data, "UnboundDescription") as string, ["token"] = token,
                                ["prepared"] = Convert.ToBoolean(Get(data, "Prepared")), ["dirty"] = Convert.ToBoolean(Get(data, "Dirty")),
                                ["runtimeModuleId"] = data.GetType().Assembly.ManifestModule.ModuleVersionId.ToString(),
                                ["frameSeconds"] = Convert.ToDouble(Get(data, "FrameSeconds")), ["cache"] = "Existing visible DTO; not regenerated."
                            };
                            var bindings = new JArray();
                            foreach (object action in Get(data, "Actions") as IEnumerable ?? Array.Empty<object>())
                                bindings.Add(Values(action, "Path", "Label", "ActionId", "ActionName"));
                            card["bindings"] = bindings;
                            object flow = Get(data, "DescriptionFlow");
                            card["descriptionFlow"] = flow == null ? null : flow.GetType().GetMethod("ToJson", Members)?.Invoke(flow, null) as JObject;
                            object signature = Get(data, "TextSignature");
                            card["cachedLocale"] = Get(signature, "_locale")?.ToString();
                            card["cachedProviderRevision"] = Get(signature, "_revision") == null ? JValue.CreateNull() : JToken.FromObject(Get(signature, "_revision"));
                            var parameters = new JArray();
                            foreach (object segment in Get(data, "Segments") as IEnumerable ?? Array.Empty<object>())
                            {
                                var value = Values(segment, "Text", "BlockId", "NodeId");
                                foreach (string kind in new[] { "Action", "Arrival", "Retreat", "Orbit", "KeepDistance", "FacingStep" })
                                    if (Get(segment, kind) is object hint) value[kind] = Values(hint, "DurationText", "RangeText", "FacingText", "ApproachText", "ActionName");
                                parameters.Add(value);
                            }
                            card["parameters"] = parameters;
                            cards.Add(card);
                        }
                    }
                    for (int index = component.numChildren - 1; index >= 0; index--) pending.Push(component.GetChildAt(index));
                }
            }
            bool truncated = pending.Count > 0;
            return new JObject { ["status"] = truncated ? "scan-limit" : cards.Count == 1 ? "found" : cards.Count == 0 ? "not-found" : "ambiguous",
                ["candidates"] = cards, ["scanTruncated"] = truncated, ["elapsedMs"] = timer.ElapsedMilliseconds,
                ["playing"] = EditorApplication.isPlaying, ["message"] = "Reads visible existing card DTOs only; unavailable identities remain null." };
        }
        private static object Get(object target, string name) => target?.GetType().GetField(name, Members)?.GetValue(target)
            ?? target?.GetType().GetProperty(name, Members)?.GetValue(target);
        private static JObject Values(object value, params string[] fields)
        { var result = new JObject(); foreach (string field in fields) { var item = Get(value, field); result[field] = item == null ? JValue.CreateNull() : JToken.FromObject(item); } return result; }
        private static void Atomic(string path, JObject data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, data.ToString(Formatting.None));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }
}
