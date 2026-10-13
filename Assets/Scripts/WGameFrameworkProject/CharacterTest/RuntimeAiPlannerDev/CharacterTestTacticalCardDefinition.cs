using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestTacticalCardApproach
    {
        OptimalRange = 0,
        None = 1
    }

    /// <summary>Immutable, versioned source for the single primary attack card.</summary>
    /// <remarks>Construction and JSON operations allocate and belong to compilation preparation.</remarks>
    internal sealed class CharacterTestTacticalCardDefinition
    {
        public const int SchemaVersion = 1;
        private static readonly string[] Fields =
        {
            "version", "name", "behavior", "target", "slot", "approach", "facing",
            "eligibility", "completion", "exit"
        };

        // This schema is a flat object of primitive values. Validate its JSON lexical grammar
        // before Json.NET decoding, which otherwise accepts JavaScript extensions and normalizes numbers.
        // Quoted strings consume escapes atomically; commas/braces inside them are ordinary text.
        private const string JsonWhitespace = @"[ \t\r\n]*";
        private const string JsonStringToken = @"""(?:[^""\\\x00-\x1f]|\\(?:[""\\/bfnrt]|u[0-9A-Fa-f]{4}))*""";
        private const string JsonPrimitive = "(?:" + JsonStringToken
            + @"|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)";
        private const string JsonMember = JsonStringToken + JsonWhitespace + ":" + JsonWhitespace + JsonPrimitive;
        private static readonly Regex SourceJsonSyntax = new Regex(
            @"\A" + JsonWhitespace + @"\{" + JsonWhitespace + "(?:" + JsonMember
            + "(?:" + JsonWhitespace + "," + JsonWhitespace + JsonMember + ")*)?"
            + JsonWhitespace + @"\}" + JsonWhitespace + @"\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public string Name { get; }
        public CharacterTestTacticalCardApproach Approach { get; }
        public string SemanticFingerprint { get; }

        public CharacterTestTacticalCardDefinition(string name, CharacterTestTacticalCardApproach approach)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A card name is required.", nameof(name));
            if (approach != CharacterTestTacticalCardApproach.OptimalRange
                && approach != CharacterTestTacticalCardApproach.None)
                throw new ArgumentOutOfRangeException(nameof(approach));
            Name = name;
            Approach = approach;
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(Serialize(includeName: false)));
                SemanticFingerprint = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public string Write() => Serialize(includeName: true);

        public static bool TryRead(string json, out CharacterTestTacticalCardDefinition definition, out string issue)
        {
            definition = null;
            issue = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return Reject("$", "JSON object is required", out issue);
            try
            {
                if (!SourceJsonSyntax.IsMatch(json))
                    return Reject("$", "Expected strict JSON: a flat object with double-quoted fields and primitive values", out issue);
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 4 })
                {
                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                        return Reject("$", "Expected an object", out issue);
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    bool ended = false;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject)
                        {
                            ended = true;
                            break;
                        }
                        if (reader.TokenType != JsonToken.PropertyName)
                            return Reject("$", "Expected a property", out issue);
                        string field = (string)reader.Value;
                        if (Array.IndexOf(Fields, field) < 0)
                            return Reject(field, "Unknown field", out issue);
                        if (values.ContainsKey(field))
                            return Reject(field, "Duplicate field", out issue);
                        if (!reader.Read())
                            return Reject(field, "Missing value", out issue);
                        if (field == "version")
                        {
                            if (reader.TokenType != JsonToken.Integer
                                || !string.Equals(Convert.ToString(reader.Value, CultureInfo.InvariantCulture),
                                    SchemaVersion.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                                return Reject(field, "Unsupported integer schema version", out issue);
                        }
                        else if (reader.TokenType != JsonToken.String)
                            return Reject(field, "Expected a string", out issue);
                        values.Add(field, Convert.ToString(reader.Value, CultureInfo.InvariantCulture));
                    }
                    if (!ended || reader.Read())
                        return Reject("$", "Incomplete object or trailing content", out issue);
                    foreach (string field in Fields)
                    {
                        if (!values.ContainsKey(field))
                            return Reject(field, "Required field is missing", out issue);
                    }
                    if (string.IsNullOrWhiteSpace(values["name"]))
                        return Reject("name", "A card name is required", out issue);
                    foreach (string field in Fields)
                    {
                        string expected = FixedValue(field);
                        if (expected != null && !string.Equals(values[field], expected, StringComparison.Ordinal))
                            return Reject(field, "Unsupported value; expected " + expected, out issue);
                    }
                    CharacterTestTacticalCardApproach approach;
                    if (values["approach"] == "optimalRange")
                        approach = CharacterTestTacticalCardApproach.OptimalRange;
                    else if (values["approach"] == "none")
                        approach = CharacterTestTacticalCardApproach.None;
                    else
                        return Reject("approach", "Unsupported approach", out issue);
                    definition = new CharacterTestTacticalCardDefinition(values["name"], approach);
                    return true;
                }
            }
            catch (JsonException exception)
            {
                return Reject("$", exception.Message, out issue);
            }
            catch (RegexMatchTimeoutException)
            {
                return Reject("$", "JSON lexical validation budget exceeded", out issue);
            }
        }

        private string Serialize(bool includeName)
        {
            using (var output = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(output) { Formatting = Formatting.None })
            {
                writer.WriteStartObject();
                foreach (string field in Fields)
                {
                    if (!includeName && field == "name")
                        continue;
                    writer.WritePropertyName(field);
                    if (field == "version")
                        writer.WriteValue(SchemaVersion);
                    else if (field == "name")
                        writer.WriteValue(Name);
                    else if (field == "approach")
                        writer.WriteValue(Approach == CharacterTestTacticalCardApproach.OptimalRange ? "optimalRange" : "none");
                    else
                        writer.WriteValue(FixedValue(field));
                }
                writer.WriteEndObject();
                writer.Flush();
                return output.ToString();
            }
        }

        private static string FixedValue(string field)
        {
            switch (field)
            {
                case "behavior": return "singlePrimaryAttack";
                case "target": return "currentCombatTarget";
                case "slot": return "lightAttackOpener";
                case "facing": return "entireCard";
                case "eligibility": return "gateAndActionEntry";
                case "completion": return "fullActionDuration";
                case "exit": return "none";
                default: return null;
            }
        }

        private static bool Reject(string field, string reason, out string issue)
        {
            issue = "TacticalCard.Source." + field + ": " + reason;
            return false;
        }
    }
}
