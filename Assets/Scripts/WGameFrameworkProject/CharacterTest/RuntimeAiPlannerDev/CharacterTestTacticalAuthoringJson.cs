using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Strict versioned source codec. Readable authoring errors survive round trips.</summary>
    /// <remarks>LowFreqAlloc. Not a persistence owner or a runtime JSON interpreter.</remarks>
    internal static class CharacterTestTacticalAuthoringJson
    {
        private static readonly Regex NumberToken = new Regex(@"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 128
        };

        internal static CharacterTestTacticalAuthoringSource Read(string json)
            => ReadCore(json, false);
        internal static CharacterTestTacticalAuthoringSource ReadSharedTrial(string json)
            => ReadCore(json, true);
        private static CharacterTestTacticalAuthoringSource ReadCore(string json, bool allowTrial)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024)
                throw new JsonSerializationException("Tactical source must contain at most 1 MiB of JSON.");
            ValidateLexicalJson(json);
            JObject token;
            using (var reader = new JsonTextReader(new StringReader(json))
            { DateParseHandling = DateParseHandling.None, MaxDepth = 128 })
            {
                token = JObject.Load(reader, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (reader.Read()) throw new JsonSerializationException("Trailing JSON content.");
            }
            ValidateTokenTypes(token);
            var source = token.ToObject<CharacterTestTacticalAuthoringSource>(JsonSerializer.Create(Settings));
            if (source == null || !source.IsSupportedVersion
                && !(allowTrial && (source.Schema == CharacterTestTacticalAuthoringSource.EventSetTrialSchema
                    || source.Schema == CharacterTestTacticalAuthoringSource.EventSetSchema) && source.Version == 1))
                throw new JsonSerializationException("Unsupported tactical source schema or version.");
            if (source.HasPreparationVersion != (source.InteractionPreparation != null)) throw new JsonSerializationException("v5 requires a fixed interaction preparation contract.");
            if (source.InteractionPreparation != null && (source.PositionRecall != null || source.ExecutionInput != null)) throw new JsonSerializationException("Preparation is an exclusive mechanism.");
            if (source.PositionRecall != null && source.ExecutionInput != null) throw new JsonSerializationException("Recall and WorldPoint are exclusive mechanisms.");
            if (source.PositionRecall != null || source.ExecutionInput != null || source.InteractionPreparation != null)
            {
                source.PositionRecall?.Freeze();
                source.InteractionPreparation?.Freeze();
                source.ExecutionInput?.Freeze();
                if (!source.HasTimingVersion || source.Slots.Count != 0 || source.PredicateBindings.Count != 0
                    || source.ActivationConditionRoot != null || source.Timelines.Count != 1
                    || source.Timelines[0] == null || source.Timelines[0].Events == null || source.Timelines[0].EventBlocks == null
                    || source.Timelines[0].TimelineId != source.EntryTimelineId
                    || source.Timelines[0].EntryConditionRoot != null || source.Timelines[0].Events.Count != 0
                    || source.Timelines[0].EventBlocks.Count != 0)
                    throw new JsonSerializationException("WorldPoint source must be an exclusive v4 point contract with one empty entry timeline.");
            }
            ValidateKnownEnums(source);
            return source;
        }

        internal static bool TryRead(string json, out CharacterTestTacticalAuthoringSource source, out string issue)
        {
            source = null;
            issue = string.Empty;
            try { source = Read(json); return true; }
            catch (JsonException error) { issue = error.Message; return false; }
            catch (ArgumentException error) { issue = error.Message; return false; }
            catch (OverflowException error) { issue = error.Message; return false; }
            catch (RegexMatchTimeoutException error) { issue = error.Message; return false; }
        }

        internal static string Write(CharacterTestTacticalAuthoringSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return JsonConvert.SerializeObject(source, Settings);
        }

        internal static string SemanticFingerprint(CharacterTestTacticalAuthoringSource source)
        {
            JToken token = JToken.Parse(Write(source));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Canonical(token).ToString(Formatting.None)))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JToken Canonical(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                    if (property.Name != "name" && property.Name != "title" && property.Name != "description")
                        result.Add(property.Name, Canonical(property.Value));
                return result;
            }
            if (token is JArray array)
            {
                var result = new JArray();
                foreach (JToken item in array) result.Add(Canonical(item));
                return result;
            }
            return token.DeepClone();
        }

        private static void ValidateTokenTypes(JToken token)
        {
            if (token.Type == JTokenType.Comment || token.Type == JTokenType.Undefined)
                throw new JsonSerializationException("Non-standard JSON token at " + token.Path);
            if (token is JValue number && (number.Type == JTokenType.Float))
            {
                double value = number.Value<double>();
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new JsonSerializationException("Non-finite number at " + token.Path);
            }
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    var value = property.Value;
                    if (value.Type == JTokenType.Null) continue;
                    string name = property.Name;
                    bool conditionNode = obj.Property("nodeId") != null && obj.Property("sourceActionEventId") == null;
                    bool integer = name == "lifetimeFrames" || name == "tractionSpeedMillimetersPerSecond" || name == "tractionAccelerationMillimetersPerSecondSquared" || name == "rangeMillimeters" || name == "arrivalMillimeters" || name == "maximumFrames" || name == "speedMilli" || name == "version" || name == "frame" || name == "offsetFrames" || name == "minimumFrames" || name == "durationFrames" || name == "preparationTimeoutFrames" || name == "completionTimeoutFrames" || name == "maximumGuardFrames"
                        || name == "strategy" || name == "entityId" || name == "side"
                        || name == "priority" || name == "order" || name == "maxExecutionsPerPass"
                        || name == "cooldownFrames" || name == "maxLoopRepeats" || name == "subject" || name == "fact"
                        || name == "operator" || name == "signal" || name == "allowedTargetPhases"
                        || name == "requiredTargetFlags" || name == "forbiddenTargetFlags"
                        || (name == "kind" && conditionNode);
                    bool text = name.EndsWith("Id", StringComparison.Ordinal) || name == "schema"
                        || name == "name" || name == "title" || name == "description" || name == "resolver"
                        || (name == "operation" && obj.Parent is JProperty contextProperty && contextProperty.Name == "positionRecall")
                        || name == "mechanism" || name == "metric" || name == "rangeCenter" || name == "startPolicy" || name == "anchor" || name == "space" || name == "targetKind" || name == "distanceSource" || name == "movementQuery" || name == "timingKind" || name == "completion"
                        || name == "facingSource" || name == "target" && obj.Property("strategy") == null
                        || (name == "kind" && !conditionNode);
                    // The existing fact DTO keeps numeric AbilityId/CharacterActionId fields.
                    if (name == "entityId" || name == "abilityId" || name == "characterActionId" || name == "target" && obj.Property("strategy") != null) { text = false; integer = true; }
                    if (integer && value.Type != JTokenType.Integer
                        || text && value.Type != JTokenType.String
                        || (name == "requireReachable" || name == "boolValue" || name == "requireLivingTarget" || name == "continueToActionOnPreparationTimeout" || name == "passiveWait") && value.Type != JTokenType.Boolean
                        || (name == "facingToleranceDegrees" || name == "ratio" || name == "speed" || name == "value" || name == "maxValue" || name == "minimum" || name == "maximum" || name == "radialWeight" || name == "x" || name == "z")
                            && value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                        throw new JsonSerializationException("Wrong JSON value type at " + value.Path);
                    ValidateTokenTypes(value);
                }
            }
            else if (token is JArray array)
                foreach (JToken item in array) ValidateTokenTypes(item);
        }

        internal static void ValidateLexicalJson(string json)
        {
            char previous = '\0';
            for (int i = 0; i < json.Length;)
            {
                char current = json[i];
                if (current == ' ' || current == '\t' || current == '\r' || current == '\n') { i++; continue; }
                if ("{}[]:,".IndexOf(current) >= 0)
                {
                    if ((current == '}' || current == ']') && previous == ',')
                        throw new JsonSerializationException("Trailing commas are not JSON.");
                    previous = current; i++; continue;
                }
                if (current == '"')
                {
                    bool ended = false;
                    for (i++; i < json.Length; i++)
                    {
                        if (json[i] < 0x20) throw new JsonSerializationException("Unescaped control character in JSON string.");
                        if (json[i] == '\\') { i++; continue; }
                        if (json[i] == '"') { i++; ended = true; break; }
                    }
                    if (!ended) throw new JsonSerializationException("Unterminated JSON string.");
                }
                else
                {
                    int start = i;
                    while (i < json.Length && "{}[]:, \t\r\n".IndexOf(json[i]) < 0) i++;
                    string token = json.Substring(start, i - start);
                    if (token != "true" && token != "false" && token != "null" && !NumberToken.IsMatch(token))
                        throw new JsonSerializationException("Non-standard JSON token at character " + start);
                }
                previous = 'v';
            }
        }

        private static void ValidateKnownEnums(CharacterTestTacticalAuthoringSource source)
        {
            // Retain the retired opportunity identifier for loading and removing old conditions.
            // Authoring additions and compilation reject it; never silently turn it into an unconditional pass.
            foreach (var binding in source.PredicateBindings)
                if (binding != null && binding.Kind != "RootDistanceRange" && binding.Kind != "TargetFacing" && binding.Kind != "BoundActionOpportunity" && binding.Kind != "IntentDistanceRange" && binding.Kind != "MovementArrived" && binding.Kind != "TurnAligned")
                    throw new JsonSerializationException("Unknown predicate binding kind; preserve the original source read-only.");
            CheckConditionEnums(source.ActivationConditionRoot);
            foreach (var timeline in source.Timelines)
            {
                if (timeline == null || timeline.Events == null) continue;
                if (!source.HasTimingVersion && (timeline.EventBlocks.Any(b => b?.Timing != null)
                    || timeline.Events.Any(e => e?.Start?.Kind == "Ratio" || e?.End?.Kind == "Ratio"
                        || e?.Start?.TargetKind == "Builtin" || e?.End?.TargetKind == "Builtin")))
                    throw new JsonSerializationException("时间关系与固有锚点需要 source v4。");
                if (!source.HasBlocksVersion && (timeline.EventBlocks?.Count > 0
                    || timeline.Events.Any(e => e?.Start?.Space == "Block" || e?.End?.Space == "Block"
                        || e?.Start?.TargetKind == "Block" || e?.End?.TargetKind == "Block")))
                    throw new JsonSerializationException("Event blocks require tactical source v3.");
                if (timeline.EventBlocks != null)
                    foreach (var block in timeline.EventBlocks) CheckFrameEnum(block?.Start);
                CheckConditionEnums(timeline.EntryConditionRoot);
                foreach (var evt in timeline.Events)
                {
                    if (evt == null) continue;
                    if (evt.TimingKind != "Instant" && evt.TimingKind != "Duration")
                        throw new JsonSerializationException("Unknown event timing kind.");
                    string kind = evt.Operation?.Kind;
                    if (!string.IsNullOrEmpty(kind) && kind != "CharacterAction" && kind != "ApproachOptimalRange"
                        && kind != "Movement" && kind != "TurnToTarget" && kind != "WaitForCondition" && kind != "ReturnToPlanner"
                        && !(source.HasTimingVersion && (kind == "LoopRegion" || kind == "EnterTimeline" || kind == "CompleteBlock"))
                        && !((source.Schema == CharacterTestTacticalAuthoringSource.EventSetTrialSchema
                            || source.Schema == CharacterTestTacticalAuthoringSource.EventSetSchema || source.HasBlocksVersion) && kind == "RetreatFromTarget"))
                        throw new JsonSerializationException("Unknown tactical operation kind; preserve the original source read-only.");
                    CheckFrameEnum(evt.Start); CheckFrameEnum(evt.End); CheckConditionEnums(evt.ConditionRoot);
                }
            }
        }

        private static void CheckFrameEnum(CharacterTestTacticalFrameReference frame)
        {
            if (frame == null) return;
            if (frame.Kind != "Absolute" && frame.Kind != "Anchor" && frame.Kind != "Ratio") throw new JsonSerializationException("Unknown frame reference kind.");
            if (frame.Space != "Timeline" && frame.Space != "Block" || frame.TargetKind != "Event" && frame.TargetKind != "Block" && frame.TargetKind != "Builtin")
                throw new JsonSerializationException("Unknown frame coordinate or target kind.");
            if (!string.IsNullOrEmpty(frame.Anchor) && frame.Anchor != "Start" && frame.Anchor != "End"
                && frame.Anchor != "ActionStartupEnd" && frame.Anchor != "ActionEnd" && frame.Anchor != "Minimum" && frame.Anchor != "SimulationEnd") throw new JsonSerializationException("Unknown anchor kind.");
        }

        private static void CheckConditionEnums(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null) return;
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind), node.Kind)
                || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind), node.Subject)
                || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal), node.Signal)
                || node.Predicate != null && (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionFact), node.Predicate.Fact)
                    || !Enum.IsDefined(typeof(CharacterTestRuntimeAiLabCardConditionOperator), node.Predicate.Operator)))
                throw new JsonSerializationException("Unknown condition enum value.");
            foreach (var child in node.Children) CheckConditionEnums(child);
        }
    }
}
