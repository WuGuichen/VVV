using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using static MxFramework.Authoring.CombatDesignCatalogProjection;

namespace MxFramework.Authoring
{
    /// <summary>Read-only mechanism navigation from provenance-bound design annotations and fixed source extractors.</summary>
    /// <remarks>LowFreqAlloc explicit queries. Finite reviewed formatters do not infer arbitrary gameplay or design semantics.</remarks>
    public sealed class CombatDesignMechanismProjection
    {
        private readonly string _root;
        public CombatDesignMechanismProjection(string projectRoot) { _root = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot))); }

        /// <summary>Regenerates source excerpts and actual catalog relationships without compiling or writing authoring data.</summary>
        public object Query()
        {
            var reader = new CombatDesignSourceReader(_root);
            JsonElement metadata = reader.Json(MetadataPath), contracts = F(metadata, "reviewedSourceContracts");
            JsonElement catalog = JsonSerializer.SerializeToElement(new CombatDesignCatalogProjection(_root).Query());
            foreach (JsonElement read in A(catalog, "readSet")) reader.MergeHash(S(read, "path"), S(read, "hash"));
            var entries = A(catalog, "entries"); var mechanisms = new List<object>();
            foreach (JsonElement annotation in A(metadata, "mechanismAnnotations"))
            {
                string key = S(annotation, "navigationKey"); var sources = new List<object>(); bool contractCurrent = true, annotationCurrent = true;
                foreach (JsonElement source in A(annotation, "sourceContracts"))
                {
                    string path = source.GetString(), expected = S(contracts, path); bool current = Observe(reader, path, expected);
                    sources.Add(D("path", path, "observedHash", reader.Hashes.TryGetValue(path, out string hash) ? hash : null, "reviewedHash", expected, "current", current)); contractCurrent &= current;
                }
                foreach (JsonElement provenance in A(annotation, "provenance")) annotationCurrent &= Observe(reader, S(provenance, "path"), S(provenance, "reviewedHash"));
                var rules = new List<object>();
                foreach (JsonElement rule in A(metadata, "rules").Where(r => S(r, "mechanism") == key).OrderBy(r => S(r, "key"), StringComparer.Ordinal))
                {
                    string path = S(rule, "source"), selector = S(rule, "selector"); string[] lines = Lines(reader, path);
                    int start = Array.FindIndex(lines, line => line.Contains(selector));
                    string excerpt = start < 0 ? "" : string.Join("\n", lines.Skip(start).Take(Math.Max(1, Math.Min(120, N(rule, "lines", 20)))));
                    bool parsed = start >= 0 && Observe(reader, path, S(contracts, path)) && A(rule, "requiredTokens").All(t => excerpt.Contains(t.GetString()));
                    if (!parsed) reader.Diagnostic("UNPARSED_OR_STALE_FIXED_RULE", key + ":" + S(rule, "key"));
                    rules.Add(D("key", S(rule, "key"), "title", S(rule, "title"), "parsed", parsed, "text", parsed ? S(rule, "format") : "规则模板或源指纹不匹配；停止自动解释，保留当前原文。", "origin", S(rule, "origin"), "source", path, "line", start + 1, "excerpt", excerpt, "observedHash", reader.Hashes.TryGetValue(path, out string hash) ? hash : null));
                }
                var excerpts = A(annotation, "excerpts").Select(excerpt => { string path = S(excerpt, "path"), selector = S(excerpt, "selector"); string[] lines = Lines(reader, path); int start = Array.FindIndex(lines, l => l.Contains(selector)); return D("path", path, "selector", selector, "line", start + 1, "kind", S(excerpt, "kind"), "text", start < 0 ? "未找到来源段落" : string.Join("\n", lines.Skip(start).Take(key == "ready" ? 7 : selector == "中毒的具体边界" ? 7 : 1))); }).ToArray();
                var related = new List<object>();
                var configuration = new List<object>();
                if (key == "stamina")
                {
                    const string producer = "Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCombatEvadeRuntime.cs";
                    const string configPath = "Assets/MxFrameworkGenerated/Abilities/character_test_mechanic_tunings.json";
                    try
                    {
                        Match stableId = Regex.Match(reader.Text(producer), @"const\s+string\s+\w+\s*=\s*""(mechanic_tuning\.[^""]+)""\s*;");
                        JsonElement config = reader.Json(configPath);
                        JsonElement tuning = A(config, "tunings").FirstOrDefault(t => S(t, "stableId") == stableId.Groups[1].Value);
                        if (stableId.Success && tuning.ValueKind == JsonValueKind.Object) configuration.Add(D("source", configPath, "sourceHash", reader.Hashes[configPath], "producer", producer, "facts", tuning, "scope", "generated config observed; current actor/scene loading not verified"));
                        else reader.Diagnostic("UNPARSED_CURRENT_TUNING", configPath);
                    }
                    catch (Exception failure) when (CombatDesignSourceReader.ReadError(failure)) { reader.Diagnostic("CONFIGURATION_UNAVAILABLE", failure.Message); }
                }
                // Actual functional resolver -> observed action -> Unhittable event. Never match titles.
                if (contractCurrent && (key == "evade" || key == "stamina"))
                {
                    const string flagsPath = "Packages/com.vincent.wgameframework/Combat/Hit/HitTargetStateFlags.cs";
                    string flags = reader.Text(flagsPath); Match bit = Regex.Match(flags, @"\bUnhittable\s*=\s*1\s*<<\s*(\d+)");
                    int bitIndex = bit.Success ? int.Parse(bit.Groups[1].Value) : -1;
                    int mask = bitIndex >= 0 && bitIndex < 31 ? 1 << bitIndex : 0;
                    foreach (JsonElement entry in entries.Where(e => S(e, "kind") == "shared-source"))
                    {
                        var matches = A(entry, "slots").Where(slot => (S(slot, "resolver") == "stepForward" || S(slot, "resolver") == "stepBack") && F(slot, "action").ValueKind == JsonValueKind.Object &&
                            (key == "stamina" || (mask != 0 && A(F(F(F(slot, "action"), "raw"), "combatTrack"), "events").Any(evt => S(evt, "kind") == "SetHitTargetState" && (N(evt, "targetStateMask") & mask) != 0)))).ToArray();
                        if (matches.Length > 0) related.Add(D("entryKey", S(entry, "entryKey"), "id", S(entry, "id"), "name", S(entry, "name"), "role", O(F(entry, "role")), "revision", O(F(entry, "revision")), "slots", matches, "usedBy", O(F(entry, "usedBy")), "basis", key == "evade" ? "functional resolver -> action catalog -> actual SetHitTargetState Unhittable mask" : "functional resolver -> reviewed evade admission contract; actor preparation not proven"));
                    }
                }
                mechanisms.Add(D("navigationKey", key, "name", S(annotation, "name"), "purpose", annotationCurrent ? S(annotation, "purpose") : "设计注释来源陈旧，待复核", "reviewedContractCurrent", contractCurrent,
                    "annotation", D("origin", S(annotation, "origin"), "current", annotationCurrent, "provenance", O(F(annotation, "provenance"))), "response", D("text", annotationCurrent ? S(annotation, "playerResponse") : "设计注释陈旧，待复核", "kind", "agent-reviewed-design-annotation"), "rules", rules, "configurationFacts", configuration, "sources", sources, "excerpts", excerpts, "relationships", related,
                    "lifecycle", D("design", annotationCurrent ? "来源绑定的 Agent 设计注释" : "注释陈旧", "code", contractCurrent ? "登记代码指纹相符" : "陈旧/待复核", "configuration", "only source facts; scene loading not verified", "enabled", "not-inferred", "validation", "source-inspection-only", "play", "not-run")));
            }
            return reader.Seal(D("schema", "mx.combat-design.mechanism-projection.v2", "mechanisms", mechanisms, "catalogProjectionHash", S(catalog, "projectionHash"), "permissions", D("readOnly", true, "templateRuleOverrides", false, "playerRuleOverrides", false), "coverage", "Five design navigation annotations; finite fingerprint-bound code extractors; actual functional evade relationships within current indexed catalog. Other relationships explicitly unregistered; no all-code semantic inference or runtime receipt."));
        }
        private static bool Observe(CombatDesignSourceReader reader, string path, string expected)
        {
            try { reader.Text(path); return expected.Length > 0 && reader.Hashes[path] == expected; }
            catch (Exception error) when (CombatDesignSourceReader.ReadError(error)) { reader.Diagnostic("SOURCE_UNAVAILABLE", path + ":" + error.Message); return false; }
        }
        private static string[] Lines(CombatDesignSourceReader reader, string path)
        {
            try { return reader.Text(path).Replace("\r\n", "\n").Split('\n'); }
            catch (Exception error) when (CombatDesignSourceReader.ReadError(error)) { reader.Diagnostic("SOURCE_UNAVAILABLE", path + ":" + error.Message); return Array.Empty<string>(); }
        }
    }
}
