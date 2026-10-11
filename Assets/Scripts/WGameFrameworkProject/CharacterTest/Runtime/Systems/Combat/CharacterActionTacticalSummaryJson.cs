using System;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Strict versioned encoding for derived measurement files. Missing fields never become valid defaults.</summary>
    /// <remarks>LowFreqAlloc.</remarks>
    internal static class CharacterActionTacticalSummaryJson
    {
        private const string Schema = "character-action-nominal-summary-v1";

        internal static string Write(CharacterActionSpatialSummary summary)
        {
            CharacterActionReferenceDummy dummy = summary.Measurement.ReferenceDummy;
            var intervals = new JArray();
            foreach (CharacterActionSpatialInterval interval in summary.Intervals)
                intervals.Add(new JArray(interval.FirstHitMillimeters, interval.LastHitMillimeters));
            return new JObject {
                ["schema"] = Schema, ["actionId"] = summary.ActionId.Value,
                ["distanceMetric"] = CharacterActionRangeProber.UsesHurtboxSurfaceMetric(summary.ProberVersion) ? "HurtboxSurfaceClearance" : "RootPlanarDistance",
                ["actionContentHash"] = summary.ActionContentHash, ["referenceDummyHash"] = summary.ReferenceDummyHash,
                ["proberVersion"] = summary.ProberVersion, ["stepMillimeters"] = summary.Measurement.StepMillimeters,
                ["searchMinMillimeters"] = summary.Measurement.SearchMinMillimeters,
                ["searchMaxMillimeters"] = summary.Measurement.SearchMaxMillimeters,
                ["dummy"] = new JObject { ["version"] = dummy.Version, ["heightMillimeters"] = dummy.HeightMillimeters,
                    ["radiusMillimeters"] = dummy.RadiusMillimeters, ["centerXMillimeters"] = dummy.CenterXMillimeters,
                    ["centerYMillimeters"] = dummy.CenterYMillimeters, ["centerZMillimeters"] = dummy.CenterZMillimeters },
                ["status"] = (int)summary.ProberStatus, ["intervals"] = intervals,
                ["optimalMillimeters"] = summary.VerifiedOptimalMillimeters.HasValue ? new JValue(summary.VerifiedOptimalMillimeters.Value) : JValue.CreateNull(),
                ["lowerOpen"] = summary.IsLowerBoundOpen, ["upperOpen"] = summary.IsUpperBoundOpen,
                ["sectorCenterMilliDegrees"] = summary.Sector.CenterMilliDegrees,
                ["sectorToleranceMilliDegrees"] = summary.Sector.ToleranceMilliDegrees, ["sectorSource"] = (int)summary.Sector.Source
            }.ToString(Formatting.Indented);
        }

        internal static CharacterActionSpatialSummary Read(string json)
        {
            JObject root = JObject.Parse(json);
            if (Text(root, "schema") != Schema) throw new InvalidDataException("Unsupported summary schema.");
            if (CharacterActionRangeProber.UsesHurtboxSurfaceMetric(Text(root, "proberVersion"))
                && Text(root, "distanceMetric") != "HurtboxSurfaceClearance")
                throw new InvalidDataException("Current summaries require Hurtbox surface clearance units.");
            var dummyJson = Required(root, "dummy") as JObject ?? throw new InvalidDataException("Dummy must be an object.");
            var dummy = new CharacterActionReferenceDummy(Int(dummyJson, "version"), Int(dummyJson, "heightMillimeters"),
                Int(dummyJson, "radiusMillimeters"), Int(dummyJson, "centerXMillimeters"), Int(dummyJson, "centerYMillimeters"), Int(dummyJson, "centerZMillimeters"));
            var measurement = new CharacterActionSpatialMeasurement(Text(root, "proberVersion"), Int(root, "stepMillimeters"),
                Int(root, "searchMinMillimeters"), Int(root, "searchMaxMillimeters"), dummy);
            var array = Required(root, "intervals") as JArray ?? throw new InvalidDataException("Intervals must be an array.");
            if (array.Count > CharacterActionRangeProber.MaximumGridSamples) throw new InvalidDataException("Too many intervals.");
            var intervals = new CharacterActionSpatialInterval[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JArray pair) || pair.Count != 2) throw new InvalidDataException("Malformed interval.");
                intervals[i] = new CharacterActionSpatialInterval(IntValue(pair[0]), IntValue(pair[1]));
            }
            JToken optimalToken = Required(root, "optimalMillimeters");
            int? optimal = optimalToken.Type == JTokenType.Null ? (int?)null : IntValue(optimalToken);
            return new CharacterActionSpatialSummary(new CharacterActionId(Int(root, "actionId")), Text(root, "actionContentHash"),
                Text(root, "referenceDummyHash"), measurement, (CharacterActionProberStatus)Int(root, "status"), intervals, optimal,
                Bool(root, "lowerOpen"), Bool(root, "upperOpen"), new CharacterActionTacticalSector(Int(root, "sectorCenterMilliDegrees"),
                    Int(root, "sectorToleranceMilliDegrees"), (CharacterActionSectorSource)Int(root, "sectorSource")));
        }

        private static JToken Required(JObject obj, string name) => obj[name] ?? throw new InvalidDataException("Missing summary field: " + name);
        private static int Int(JObject obj, string name) => IntValue(Required(obj, name));
        private static int IntValue(JToken token)
        {
            if (token.Type != JTokenType.Integer) throw new InvalidDataException("Expected an integer measurement value.");
            return checked((int)token.Value<long>());
        }
        private static string Text(JObject obj, string name)
        {
            JToken value = Required(obj, name);
            if (value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Invalid string: " + name);
            return value.Value<string>();
        }
        private static bool Bool(JObject obj, string name)
        {
            JToken value = Required(obj, name);
            if (value.Type != JTokenType.Boolean) throw new InvalidDataException("Invalid boolean: " + name);
            return value.Value<bool>();
        }
    }
}
