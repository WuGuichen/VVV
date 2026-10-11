using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisValidationStaticSourceKind
    {
        Unknown = 0,
        SingleStatic = 1
    }

    internal enum CharacterTestGenesisValidationResetPolicy
    {
        Unknown = 0,
        SessionRebuild = 1
    }

    internal enum CharacterTestGenesisValidationAuthorityStatus
    {
        RuntimeAuthority = 0,
        HarnessOnly = 1,
        Unsupported = 2
    }

    internal enum CharacterTestGenesisValidationSceneReadiness
    {
        SceneRunnable = 0,
        NotRunnable = 1
    }

    internal enum CharacterTestGenesisValidationCacheHealth
    {
        NotBuilt = 0,
        Fresh = 1,
        Stale = 2,
        Inconsistent = 3
    }

    internal sealed class CharacterTestGenesisValidationAuthoringDocument
    {
        internal const int CurrentSchemaVersion = 1;

        public CharacterTestGenesisValidationAuthoringDocument()
        {
            SchemaVersion = CurrentSchemaVersion;
            ScenePlan = new CharacterTestGenesisValidationScenePlan();
            Groups = new List<CharacterTestGenesisValidationGroup>();
            Stations = new List<CharacterTestGenesisValidationStation>();
            LoadErrors = new List<string>();
        }

        public int SchemaVersion { get; set; }
        public CharacterTestGenesisValidationScenePlan ScenePlan { get; set; }
        public List<CharacterTestGenesisValidationGroup> Groups { get; set; }
        public List<CharacterTestGenesisValidationStation> Stations { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        [JsonIgnore]
        public List<string> LoadErrors { get; set; }

        public CharacterTestGenesisValidationAuthoringDocument Normalize()
        {
            var result = new CharacterTestGenesisValidationAuthoringDocument
            {
                SchemaVersion = SchemaVersion,
                ScenePlan = (ScenePlan ?? new CharacterTestGenesisValidationScenePlan()).Normalize(),
                Groups = new List<CharacterTestGenesisValidationGroup>(),
                Stations = new List<CharacterTestGenesisValidationStation>(),
                ExtensionData = CloneExtensionData(ExtensionData),
                LoadErrors = LoadErrors == null
                    ? new List<string>()
                    : new List<string>(LoadErrors)
            };

            if (Groups != null)
            {
                for (int i = 0; i < Groups.Count; i++)
                {
                    if (Groups[i] != null)
                        result.Groups.Add(Groups[i].Normalize());
                }
            }

            result.Groups.Sort(CharacterTestGenesisValidationGroupComparer.Instance);
            var groupOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < result.Groups.Count; i++)
            {
                if (!groupOrder.ContainsKey(result.Groups[i].StableId))
                    groupOrder.Add(result.Groups[i].StableId, i);
            }

            if (Stations != null)
            {
                for (int i = 0; i < Stations.Count; i++)
                {
                    if (Stations[i] != null)
                        result.Stations.Add(Stations[i].Normalize());
                }
            }

            result.Stations.Sort(new CharacterTestGenesisValidationStationComparer(groupOrder));
            return result;
        }

        internal static IDictionary<string, JToken> CloneExtensionData(
            IDictionary<string, JToken> source)
        {
            if (source == null || source.Count == 0)
                return null;

            var result = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> pair in source)
                result[pair.Key] = pair.Value?.DeepClone();
            return result;
        }

        internal static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class CharacterTestGenesisValidationGroup
    {
        public CharacterTestGenesisValidationGroup()
        {
            StableId = string.Empty;
            DisplayName = string.Empty;
        }

        public string StableId { get; set; }
        public string DisplayName { get; set; }
        public int SortOrder { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationGroup Normalize()
        {
            return new CharacterTestGenesisValidationGroup
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                DisplayName = CharacterTestGenesisValidationAuthoringDocument.Trim(DisplayName),
                SortOrder = SortOrder,
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationStation
    {
        public CharacterTestGenesisValidationStation()
        {
            StableId = string.Empty;
            DisplayName = string.Empty;
            Enabled = true;
            GroupId = string.Empty;
            Intent = string.Empty;
            CanonTrace = new CharacterTestGenesisValidationCanonTrace();
            Bindings = new CharacterTestGenesisValidationBindings();
            SpatialProgram = new CharacterTestGenesisValidationStationSpatialProgram();
            Scenario = new CharacterTestGenesisValidationScenario();
            Observation = new CharacterTestGenesisValidationObservation();
            ResetPolicy = CharacterTestGenesisValidationResetPolicy.SessionRebuild;
            Notes = string.Empty;
        }

        public string StableId { get; set; }
        public string DisplayName { get; set; }
        public bool Enabled { get; set; }
        public string GroupId { get; set; }
        public int SortOrder { get; set; }
        public string Intent { get; set; }
        public CharacterTestGenesisValidationCanonTrace CanonTrace { get; set; }
        public CharacterTestGenesisValidationBindings Bindings { get; set; }
        public CharacterTestGenesisValidationStationSpatialProgram SpatialProgram { get; set; }
        public CharacterTestGenesisValidationScenario Scenario { get; set; }
        public CharacterTestGenesisValidationObservation Observation { get; set; }
        public CharacterTestGenesisValidationResetPolicy ResetPolicy { get; set; }
        public string Notes { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationStation Normalize()
        {
            return new CharacterTestGenesisValidationStation
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                DisplayName = CharacterTestGenesisValidationAuthoringDocument.Trim(DisplayName),
                Enabled = Enabled,
                GroupId = CharacterTestGenesisValidationAuthoringDocument.Trim(GroupId),
                SortOrder = SortOrder,
                Intent = CharacterTestGenesisValidationAuthoringDocument.Trim(Intent),
                CanonTrace = (CanonTrace ?? new CharacterTestGenesisValidationCanonTrace()).Normalize(),
                Bindings = (Bindings ?? new CharacterTestGenesisValidationBindings()).Normalize(),
                SpatialProgram = (SpatialProgram ?? new CharacterTestGenesisValidationStationSpatialProgram()).Normalize(),
                Scenario = (Scenario ?? new CharacterTestGenesisValidationScenario()).Normalize(),
                Observation = (Observation ?? new CharacterTestGenesisValidationObservation()).Normalize(),
                ResetPolicy = ResetPolicy,
                Notes = CharacterTestGenesisValidationAuthoringDocument.Trim(Notes),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationCanonTrace
    {
        public CharacterTestGenesisValidationCanonTrace()
        {
            SourcePath = string.Empty;
            SectionTitle = string.Empty;
            DesignNote = string.Empty;
        }

        public string SourcePath { get; set; }
        public string SectionTitle { get; set; }
        public string DesignNote { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationCanonTrace Normalize()
        {
            return new CharacterTestGenesisValidationCanonTrace
            {
                SourcePath = CharacterTestGenesisValidationAuthoringDocument.Trim(SourcePath).Replace('\\', '/'),
                SectionTitle = CharacterTestGenesisValidationAuthoringDocument.Trim(SectionTitle),
                DesignNote = CharacterTestGenesisValidationAuthoringDocument.Trim(DesignNote),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationBindings
    {
        public CharacterTestGenesisValidationCharacterRef SubjectCharacter { get; set; }
        public CharacterTestGenesisValidationMovableObjectRef TargetMovableObject { get; set; }
        public CharacterTestGenesisValidationStaticColliderRef TargetStaticCollider { get; set; }
        public List<CharacterTestGenesisValidationStaticColliderRef> SampleStaticColliders { get; set; }
        public CharacterTestGenesisValidationPointRef StartPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ResetPoint { get; set; }
        public CharacterTestGenesisValidationRegionRef ObservationRegion { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationBindings Normalize()
        {
            var samples = new List<CharacterTestGenesisValidationStaticColliderRef>();
            if (SampleStaticColliders != null)
            {
                for (int i = 0; i < SampleStaticColliders.Count; i++)
                {
                    if (SampleStaticColliders[i] != null)
                        samples.Add(SampleStaticColliders[i].Normalize());
                }
            }

            return new CharacterTestGenesisValidationBindings
            {
                SubjectCharacter = SubjectCharacter?.Normalize(),
                TargetMovableObject = TargetMovableObject?.Normalize(),
                TargetStaticCollider = TargetStaticCollider?.Normalize(),
                SampleStaticColliders = samples,
                StartPoint = StartPoint?.Normalize(),
                ResetPoint = ResetPoint?.Normalize(),
                ObservationRegion = ObservationRegion?.Normalize(),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationCharacterRef
    {
        public string StableId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationCharacterRef Normalize() =>
            new CharacterTestGenesisValidationCharacterRef
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
    }

    internal sealed class CharacterTestGenesisValidationMovableObjectRef
    {
        public string InstanceStableId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationMovableObjectRef Normalize() =>
            new CharacterTestGenesisValidationMovableObjectRef
            {
                InstanceStableId = CharacterTestGenesisValidationAuthoringDocument.Trim(InstanceStableId),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
    }

    internal sealed class CharacterTestGenesisValidationPointRef
    {
        public string StableId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationPointRef Normalize() =>
            new CharacterTestGenesisValidationPointRef
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
    }

    internal sealed class CharacterTestGenesisValidationRegionRef
    {
        public string StableId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationRegionRef Normalize() =>
            new CharacterTestGenesisValidationRegionRef
            {
                StableId = CharacterTestGenesisValidationAuthoringDocument.Trim(StableId),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
    }

    internal sealed class CharacterTestGenesisValidationStaticColliderRef
    {
        public CharacterTestGenesisValidationStaticSourceKind SourceKind { get; set; }
        public int BodyId { get; set; }
        public int ColliderId { get; set; }
        public int ExpectedDefinitionId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationStaticColliderRef Normalize()
        {
            return new CharacterTestGenesisValidationStaticColliderRef
            {
                SourceKind = SourceKind,
                BodyId = Math.Max(0, BodyId),
                ColliderId = ColliderId > 0 ? ColliderId : 1,
                ExpectedDefinitionId = Math.Max(0, ExpectedDefinitionId),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationScenario
    {
        public CharacterTestGenesisValidationScenario()
        {
            TypeId = string.Empty;
            SchemaVersion = 1;
            Payload = new JObject();
        }

        public string TypeId { get; set; }
        public int SchemaVersion { get; set; }
        public JObject Payload { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationScenario Normalize()
        {
            return new CharacterTestGenesisValidationScenario
            {
                TypeId = CharacterTestGenesisValidationAuthoringDocument.Trim(TypeId),
                SchemaVersion = SchemaVersion,
                Payload = Payload == null ? new JObject() : (JObject)Payload.DeepClone(),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationObservation
    {
        public CharacterTestGenesisValidationObservation()
        {
            BodyPrompt = string.Empty;
            RequiredSignalIds = new List<string>();
            Criteria = new List<JObject>();
            EvidenceNote = string.Empty;
        }

        public string BodyPrompt { get; set; }
        public List<string> RequiredSignalIds { get; set; }
        public List<JObject> Criteria { get; set; }
        public string EvidenceNote { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public CharacterTestGenesisValidationObservation Normalize()
        {
            var signals = new List<string>();
            if (RequiredSignalIds != null)
            {
                for (int i = 0; i < RequiredSignalIds.Count; i++)
                {
                    string signal = CharacterTestGenesisValidationAuthoringDocument.Trim(RequiredSignalIds[i]);
                    if (!string.IsNullOrEmpty(signal) && !signals.Contains(signal))
                        signals.Add(signal);
                }
            }

            var criteria = new List<JObject>();
            if (Criteria != null)
            {
                for (int i = 0; i < Criteria.Count; i++)
                {
                    if (Criteria[i] != null)
                        criteria.Add((JObject)Criteria[i].DeepClone());
                }
            }

            return new CharacterTestGenesisValidationObservation
            {
                BodyPrompt = CharacterTestGenesisValidationAuthoringDocument.Trim(BodyPrompt),
                RequiredSignalIds = signals,
                Criteria = criteria,
                EvidenceNote = CharacterTestGenesisValidationAuthoringDocument.Trim(EvidenceNote),
                ExtensionData = CharacterTestGenesisValidationAuthoringDocument.CloneExtensionData(ExtensionData)
            };
        }
    }

    internal class CharacterTestGenesisValidationScenarioPayloadV1
    {
        public string RequestProfileId { get; set; } = string.Empty;
        public int DurationFrames { get; set; } = 60;
        public int ObservationFrames { get; set; } = 30;
        public List<string> ComparisonOrder { get; set; } = new List<string>();

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    internal sealed class CharacterTestGenesisValidationSurfaceContactPayloadV1 :
        CharacterTestGenesisValidationScenarioPayloadV1
    {
        public bool PreserveComparisonOrder { get; set; } = true;
    }

    internal sealed class CharacterTestGenesisValidationBoundaryBodyPayloadV1 :
        CharacterTestGenesisValidationScenarioPayloadV1
    {
        public int ApproachDirectionMilli { get; set; } = 1000;
    }

    internal sealed class CharacterTestGenesisValidationMovableObjectPayloadV1 :
        CharacterTestGenesisValidationScenarioPayloadV1
    {
        public int PushDirectionMilli { get; set; } = 1000;
    }

    internal sealed class CharacterTestGenesisValidationPotencyDebugPayloadV1 :
        CharacterTestGenesisValidationScenarioPayloadV1
    {
        public string InfluenceProfileId { get; set; } = string.Empty;
    }

    internal enum CharacterTestGenesisValidationInspectorFieldKind
    {
        String = 0,
        PositiveInteger = 1,
        Boolean = 2,
        OrderedStringList = 3
    }

    internal sealed class CharacterTestGenesisValidationInspectorField
    {
        public CharacterTestGenesisValidationInspectorField(
            string propertyName,
            string displayName,
            CharacterTestGenesisValidationInspectorFieldKind kind)
        {
            PropertyName = propertyName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
        }

        public string PropertyName { get; }
        public string DisplayName { get; }
        public CharacterTestGenesisValidationInspectorFieldKind Kind { get; }
    }

    internal enum CharacterTestGenesisValidationBindingValueKind
    {
        Character = 0,
        MovableObject = 1,
        SingleStatic = 2,
        OrderedSingleStaticList = 3,
        Point = 4,
        Region = 5
    }

    internal sealed class CharacterTestGenesisValidationBindingSlot
    {
        public CharacterTestGenesisValidationBindingSlot(
            string slotName,
            CharacterTestGenesisValidationBindingValueKind valueKind,
            bool required)
        {
            SlotName = slotName ?? string.Empty;
            ValueKind = valueKind;
            Required = required;
        }

        public string SlotName { get; }
        public CharacterTestGenesisValidationBindingValueKind ValueKind { get; }
        public bool Required { get; }
    }

    internal interface ICharacterTestGenesisValidationAuthoringScenarioDescriptor
    {
        string TypeId { get; }
        int SchemaVersion { get; }
        string DisplayName { get; }
        IReadOnlyList<string> AllowedSignalIds { get; }
        IReadOnlyList<CharacterTestGenesisValidationInspectorField> InspectorFields { get; }
        IReadOnlyList<CharacterTestGenesisValidationBindingSlot> BindingSlots { get; }
        CharacterTestGenesisValidationScenario CreateDefaultScenario();
        bool TryNormalizeAndValidate(
            CharacterTestGenesisValidationScenario scenario,
            CharacterTestGenesisValidationBindings bindings,
            CharacterTestGenesisValidationObservation observation,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path);
    }

    internal sealed class CharacterTestGenesisValidationAuthoringScenarioDescriptor :
        ICharacterTestGenesisValidationAuthoringScenarioDescriptor
    {
        private readonly CharacterTestGenesisValidationBindingSlot[] _bindingSlots;
        private readonly string[] _allowedSignals;
        private readonly CharacterTestGenesisValidationInspectorField[] _inspectorFields;
        private readonly Type _payloadType;

        public CharacterTestGenesisValidationAuthoringScenarioDescriptor(
            string typeId,
            string displayName,
            Type payloadType,
            string[] requiredBindings,
            string[] allowedSignals,
            CharacterTestGenesisValidationInspectorField[] inspectorFields)
        {
            TypeId = typeId;
            DisplayName = displayName;
            _payloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
            _bindingSlots = CreateBindingSlots(requiredBindings);
            _allowedSignals = allowedSignals ?? Array.Empty<string>();
            _inspectorFields = inspectorFields ?? Array.Empty<CharacterTestGenesisValidationInspectorField>();
        }

        public string TypeId { get; }
        public int SchemaVersion => 1;
        public string DisplayName { get; }
        public IReadOnlyList<string> AllowedSignalIds => _allowedSignals;
        public IReadOnlyList<CharacterTestGenesisValidationInspectorField> InspectorFields =>
            _inspectorFields;
        public IReadOnlyList<CharacterTestGenesisValidationBindingSlot> BindingSlots =>
            _bindingSlots;

        public CharacterTestGenesisValidationScenario CreateDefaultScenario()
        {
            return new CharacterTestGenesisValidationScenario
            {
                TypeId = TypeId,
                SchemaVersion = SchemaVersion,
                Payload = JObject.FromObject(Activator.CreateInstance(_payloadType))
            };
        }

        public bool TryNormalizeAndValidate(
            CharacterTestGenesisValidationScenario scenario,
            CharacterTestGenesisValidationBindings bindings,
            CharacterTestGenesisValidationObservation observation,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path)
        {
            bool valid = true;
            if (scenario.SchemaVersion != SchemaVersion)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "UnknownScenarioVersion",
                    path + ".scenario.schemaVersion",
                    "Scenario schema version is not registered."));
                return false;
            }

            CharacterTestGenesisValidationScenarioPayloadV1 payload;
            try
            {
                payload = (CharacterTestGenesisValidationScenarioPayloadV1)
                    (scenario.Payload ?? new JObject()).ToObject(_payloadType);
            }
            catch (Exception exception)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "InvalidScenarioPayload",
                    path + ".scenario.payload",
                    exception.Message));
                return false;
            }

            if (payload == null)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "InvalidScenarioPayload",
                    path + ".scenario.payload",
                    "Typed scenario payload is missing."));
                valid = false;
            }
            else
            {
                payload.RequestProfileId = CharacterTestGenesisValidationAuthoringDocument.Trim(
                    payload.RequestProfileId);
                payload.ComparisonOrder ??= new List<string>();
                scenario.Payload = JObject.FromObject(payload);
                for (int i = 0; i < _inspectorFields.Length; i++)
                {
                    CharacterTestGenesisValidationInspectorField field = _inspectorFields[i];
                    if (field.Kind != CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger
                        || scenario.Payload.Value<int?>(field.PropertyName) > 0)
                        continue;
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "InvalidScenarioPayload",
                        path + ".scenario.payload." + field.PropertyName,
                        field.DisplayName + " must be positive."));
                    valid = false;
                }
            }

            for (int i = 0; i < _bindingSlots.Length; i++)
            {
                if (HasBinding(bindings, _bindingSlots[i].SlotName))
                    continue;

                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "MissingRequiredBinding",
                    path + ".bindings." + _bindingSlots[i].SlotName,
                    "Required typed binding is missing."));
                valid = false;
            }

            if (observation?.RequiredSignalIds != null)
            {
                for (int i = 0; i < observation.RequiredSignalIds.Count; i++)
                {
                    if (Array.IndexOf(_allowedSignals, observation.RequiredSignalIds[i]) >= 0)
                        continue;

                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "UnknownObservationSignal",
                        path + ".observation.requiredSignalIds[" + i + "]",
                        "Signal is not part of the Authoring descriptor vocabulary."));
                    valid = false;
                }
            }

            return valid;
        }

        private static CharacterTestGenesisValidationBindingSlot[] CreateBindingSlots(
            string[] requiredBindings)
        {
            if (requiredBindings == null || requiredBindings.Length == 0)
                return Array.Empty<CharacterTestGenesisValidationBindingSlot>();
            var result = new CharacterTestGenesisValidationBindingSlot[requiredBindings.Length];
            for (int i = 0; i < requiredBindings.Length; i++)
            {
                result[i] = new CharacterTestGenesisValidationBindingSlot(
                    requiredBindings[i],
                    ResolveBindingValueKind(requiredBindings[i]),
                    true);
            }
            return result;
        }

        private static CharacterTestGenesisValidationBindingValueKind ResolveBindingValueKind(
            string name)
        {
            switch (name)
            {
                case "subjectCharacter":
                    return CharacterTestGenesisValidationBindingValueKind.Character;
                case "targetMovableObject":
                    return CharacterTestGenesisValidationBindingValueKind.MovableObject;
                case "targetStaticCollider":
                    return CharacterTestGenesisValidationBindingValueKind.SingleStatic;
                case "sampleStaticColliders":
                    return CharacterTestGenesisValidationBindingValueKind.OrderedSingleStaticList;
                case "startPoint":
                case "resetPoint":
                    return CharacterTestGenesisValidationBindingValueKind.Point;
                case "observationRegion":
                    return CharacterTestGenesisValidationBindingValueKind.Region;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown binding slot.");
            }
        }

        private static bool HasBinding(CharacterTestGenesisValidationBindings bindings, string name)
        {
            if (bindings == null)
                return false;

            switch (name)
            {
                case "subjectCharacter":
                    return !string.IsNullOrWhiteSpace(bindings.SubjectCharacter?.StableId);
                case "targetMovableObject":
                    return !string.IsNullOrWhiteSpace(bindings.TargetMovableObject?.InstanceStableId);
                case "targetStaticCollider":
                    return bindings.TargetStaticCollider != null;
                case "sampleStaticColliders":
                    return bindings.SampleStaticColliders != null && bindings.SampleStaticColliders.Count > 0;
                case "startPoint":
                    return !string.IsNullOrWhiteSpace(bindings.StartPoint?.StableId);
                case "resetPoint":
                    return !string.IsNullOrWhiteSpace(bindings.ResetPoint?.StableId);
                default:
                    return false;
            }
        }
    }

    internal static class CharacterTestGenesisValidationAuthoringScenarioRegistry
    {
        internal const string SurfaceContactResponse = "SurfaceContactResponse";
        internal const string BoundaryBodyResponse = "BoundaryBodyResponse";
        internal const string MovableNaturalObjectResponse = "MovableNaturalObjectResponse";
        internal const string PotencyReceptionDebug = "PotencyReceptionDebug";

        private static readonly List<ICharacterTestGenesisValidationAuthoringScenarioDescriptor>
            Descriptors = new List<ICharacterTestGenesisValidationAuthoringScenarioDescriptor>
            {
                new CharacterTestGenesisValidationAuthoringScenarioDescriptor(
                    SurfaceContactResponse,
                    "表面接触响应",
                    typeof(CharacterTestGenesisValidationSurfaceContactPayloadV1),
                    new[] { "subjectCharacter", "startPoint", "resetPoint", "sampleStaticColliders" },
                    new[] { "contact-state", "motion-response", "surface-comparison" },
                    CreateInspectorFields(new CharacterTestGenesisValidationInspectorField(
                        "PreserveComparisonOrder",
                        "保持比较顺序",
                        CharacterTestGenesisValidationInspectorFieldKind.Boolean))),
                new CharacterTestGenesisValidationAuthoringScenarioDescriptor(
                    BoundaryBodyResponse,
                    "边界身体响应",
                    typeof(CharacterTestGenesisValidationBoundaryBodyPayloadV1),
                    new[] { "subjectCharacter", "startPoint", "resetPoint", "targetStaticCollider" },
                    new[] { "contact-state", "motion-response", "grounding-state" },
                    CreateInspectorFields(new CharacterTestGenesisValidationInspectorField(
                        "ApproachDirectionMilli",
                        "接近方向量化值",
                        CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger))),
                new CharacterTestGenesisValidationAuthoringScenarioDescriptor(
                    MovableNaturalObjectResponse,
                    "可动自然物响应",
                    typeof(CharacterTestGenesisValidationMovableObjectPayloadV1),
                    new[] { "subjectCharacter", "startPoint", "resetPoint", "targetMovableObject" },
                    new[] { "contact-state", "motion-response", "movable-state" },
                    CreateInspectorFields(new CharacterTestGenesisValidationInspectorField(
                        "PushDirectionMilli",
                        "推动方向量化值",
                        CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger))),
                new CharacterTestGenesisValidationAuthoringScenarioDescriptor(
                    PotencyReceptionDebug,
                    "三势受势调试",
                    typeof(CharacterTestGenesisValidationPotencyDebugPayloadV1),
                    new[] { "targetMovableObject" },
                    new[] { "potency-state", "command-result" },
                    CreateInspectorFields(new CharacterTestGenesisValidationInspectorField(
                        "InfluenceProfileId",
                        "受势 Profile",
                        CharacterTestGenesisValidationInspectorFieldKind.String)))
            };

        public static IReadOnlyList<ICharacterTestGenesisValidationAuthoringScenarioDescriptor> All =>
            Descriptors;

        public static bool TryGet(
            string typeId,
            int schemaVersion,
            out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor)
        {
            for (int i = 0; i < Descriptors.Count; i++)
            {
                if (Descriptors[i].SchemaVersion == schemaVersion
                    && string.Equals(Descriptors[i].TypeId, typeId, StringComparison.Ordinal))
                {
                    descriptor = Descriptors[i];
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        private static CharacterTestGenesisValidationInspectorField[] CreateInspectorFields(
            CharacterTestGenesisValidationInspectorField specificField)
        {
            return new[]
            {
                new CharacterTestGenesisValidationInspectorField(
                    "RequestProfileId",
                    "请求 Profile",
                    CharacterTestGenesisValidationInspectorFieldKind.String),
                new CharacterTestGenesisValidationInspectorField(
                    "DurationFrames",
                    "持续帧",
                    CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger),
                new CharacterTestGenesisValidationInspectorField(
                    "ObservationFrames",
                    "观察帧",
                    CharacterTestGenesisValidationInspectorFieldKind.PositiveInteger),
                new CharacterTestGenesisValidationInspectorField(
                    "ComparisonOrder",
                    "比较顺序",
                    CharacterTestGenesisValidationInspectorFieldKind.OrderedStringList),
                specificField
            };
        }
    }

    internal sealed class CharacterTestGenesisValidationOwnerCatalog
    {
        private readonly HashSet<string> _characters = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _movables = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _points = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _regions = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<long, List<int>> _singleStatics = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, int> _expandedStaticCounts = new Dictionary<long, int>();

        public static CharacterTestGenesisValidationOwnerCatalog Build(
            CharacterTestSceneCharacterAuthoringDocument characters,
            LevelWorldObjectAuthoringDocument worldObjects,
            IReadOnlyList<CharacterTestWorldObjectSceneSource.StaticColliderInstance> expandedStatics = null)
        {
            var catalog = new CharacterTestGenesisValidationOwnerCatalog();
            CharacterTestSceneCharacterAuthoringDocument normalizedCharacters = characters?.Normalize();
            if (normalizedCharacters?.Characters != null)
            {
                for (int i = 0; i < normalizedCharacters.Characters.Count; i++)
                {
                    CharacterTestSceneCharacterDefinition item = normalizedCharacters.Characters[i];
                    if (item.Enabled && !string.IsNullOrWhiteSpace(item.StableId))
                        catalog._characters.Add(item.StableId);
                }
            }

            LevelWorldObjectAuthoringDocument normalizedWorld = worldObjects?.Normalize();
            if (normalizedWorld != null)
            {
                AddStableIds(normalizedWorld.MovableInstances, catalog._movables);
                AddStableIds(normalizedWorld.Points, catalog._points);
                AddStableIds(normalizedWorld.Regions, catalog._regions);
                for (int i = 0; i < normalizedWorld.SingleStaticColliders.Count; i++)
                {
                    LevelWorldObjectInstanceDefinition item = normalizedWorld.SingleStaticColliders[i];
                    if (!item.Enabled)
                        continue;

                    long key = MakeStaticKey(item.BodyId, item.ColliderId > 0 ? item.ColliderId : 1);
                    if (!catalog._singleStatics.TryGetValue(key, out List<int> definitions))
                    {
                        definitions = new List<int>();
                        catalog._singleStatics.Add(key, definitions);
                    }
                    definitions.Add(item.DefinitionId);
                }
            }

            if (expandedStatics != null)
            {
                for (int i = 0; i < expandedStatics.Count; i++)
                {
                    CharacterTestWorldObjectSceneSource.StaticColliderInstance item = expandedStatics[i];
                    if (item == null || !item.IsEnabled)
                        continue;

                    long key = MakeStaticKey(item.BodyId, item.ColliderId > 0 ? item.ColliderId : 1);
                    catalog._expandedStaticCounts.TryGetValue(key, out int count);
                    catalog._expandedStaticCounts[key] = count + 1;
                }
            }
            else
            {
                foreach (KeyValuePair<long, List<int>> pair in catalog._singleStatics)
                    catalog._expandedStaticCounts[pair.Key] = pair.Value.Count;
            }

            return catalog;
        }

        public bool ContainsCharacter(string id) => _characters.Contains(id ?? string.Empty);
        public bool ContainsMovable(string id) => _movables.Contains(id ?? string.Empty);
        public bool ContainsPoint(string id) => _points.Contains(id ?? string.Empty);
        public bool ContainsRegion(string id) => _regions.Contains(id ?? string.Empty);

        public bool TryResolveSingleStatic(
            CharacterTestGenesisValidationStaticColliderRef reference,
            out string errorCode)
        {
            if (reference == null)
            {
                errorCode = "MissingStaticColliderRef";
                return false;
            }
            if (reference.SourceKind != CharacterTestGenesisValidationStaticSourceKind.SingleStatic)
            {
                errorCode = "UnsupportedStaticSourceKind";
                return false;
            }
            if (reference.BodyId <= 0 || reference.ExpectedDefinitionId <= 0)
            {
                errorCode = "InvalidStaticColliderRef";
                return false;
            }

            long key = MakeStaticKey(reference.BodyId, reference.ColliderId > 0 ? reference.ColliderId : 1);
            if (!_singleStatics.TryGetValue(key, out List<int> definitions) || definitions.Count != 1)
            {
                errorCode = definitions == null ? "StaticColliderNotFound" : "AmbiguousSingleStaticCollider";
                return false;
            }
            if (definitions[0] != reference.ExpectedDefinitionId)
            {
                errorCode = "StaticColliderDefinitionMismatch";
                return false;
            }
            if (_expandedStaticCounts.TryGetValue(key, out int count) && count != 1)
            {
                errorCode = "ExpandedStaticColliderKeyConflict";
                return false;
            }

            errorCode = string.Empty;
            return true;
        }

        private static void AddStableIds(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> values,
            HashSet<string> output)
        {
            if (values == null)
                return;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Enabled && !string.IsNullOrWhiteSpace(values[i].InstanceStableId))
                    output.Add(values[i].InstanceStableId);
            }
        }

        private static void AddStableIds(
            IReadOnlyList<LevelWorldObjectPointDefinition> values,
            HashSet<string> output)
        {
            if (values == null)
                return;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Enabled && !string.IsNullOrWhiteSpace(values[i].StableId))
                    output.Add(values[i].StableId);
            }
        }

        private static void AddStableIds(
            IReadOnlyList<LevelWorldObjectRegionDefinition> values,
            HashSet<string> output)
        {
            if (values == null)
                return;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Enabled && !string.IsNullOrWhiteSpace(values[i].StableId))
                    output.Add(values[i].StableId);
            }
        }

        private static long MakeStaticKey(int bodyId, int colliderId)
        {
            return ((long)bodyId << 32) | (uint)colliderId;
        }
    }

    internal sealed class CharacterTestGenesisValidationAuthoringIssue
    {
        private CharacterTestGenesisValidationAuthoringIssue(
            LevelWorldObjectAuthoringSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code;
            Path = path;
            Message = message;
        }

        public LevelWorldObjectAuthoringSeverity Severity { get; }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }

        public static CharacterTestGenesisValidationAuthoringIssue Error(
            string code,
            string path,
            string message)
        {
            return new CharacterTestGenesisValidationAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }

        public static CharacterTestGenesisValidationAuthoringIssue Warning(
            string code,
            string path,
            string message)
        {
            return new CharacterTestGenesisValidationAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Warning,
                code,
                path,
                message);
        }
    }

    internal sealed class CharacterTestGenesisValidationAuthoringReport
    {
        public CharacterTestGenesisValidationAuthoringReport(
            IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            Issues = issues ?? Array.Empty<CharacterTestGenesisValidationAuthoringIssue>();
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == LevelWorldObjectAuthoringSeverity.Error)
                    errors++;
                else if (Issues[i].Severity == LevelWorldObjectAuthoringSeverity.Warning)
                    warnings++;
            }
            ErrorCount = errors;
            WarningCount = warnings;
        }

        public IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> Issues { get; }
        public int ErrorCount { get; }
        public int WarningCount { get; }
        public bool HasErrors => ErrorCount > 0;
    }

    internal static class CharacterTestGenesisValidationAuthoringValidator
    {
        public static CharacterTestGenesisValidationAuthoringReport Validate(
            CharacterTestGenesisValidationAuthoringDocument document,
            CharacterTestGenesisValidationOwnerCatalog owners)
        {
            var issues = new List<CharacterTestGenesisValidationAuthoringIssue>();
            if (document == null)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "MissingDocument", "document", "Validation document is missing."));
                return new CharacterTestGenesisValidationAuthoringReport(issues);
            }
            if (document.SchemaVersion != CharacterTestGenesisValidationAuthoringDocument.CurrentSchemaVersion)
            {
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                    "UnknownSchemaVersion", "schemaVersion", "Validation schema version is not supported."));
                return new CharacterTestGenesisValidationAuthoringReport(issues);
            }

            if (document.LoadErrors != null && document.LoadErrors.Count > 0)
            {
                for (int i = 0; i < document.LoadErrors.Count; i++)
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(
                        "AuthoringPayloadReadFailed",
                        "document",
                        document.LoadErrors[i]));
                }
                return new CharacterTestGenesisValidationAuthoringReport(issues);
            }

            CharacterTestGenesisValidationAuthoringDocument normalized = document.Normalize();
            owners ??= CharacterTestGenesisValidationOwnerCatalog.Build(null, null);
            var groups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < normalized.Groups.Count; i++)
            {
                string path = "groups[" + i + "]";
                string id = normalized.Groups[i].StableId;
                if (string.IsNullOrEmpty(id))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("MissingGroupStableId", path + ".stableId", "Group StableId is required."));
                else if (!groups.Add(id))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("DuplicateGroupStableId", path + ".stableId", "Group StableId must be unique."));
            }

            Dictionary<string, CharacterTestGenesisValidationZone> zones =
                CharacterTestGenesisValidationSceneDesignValidator.ValidateScenePlan(
                    normalized.ScenePlan,
                    groups,
                    owners,
                    issues);

            var stations = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < normalized.Stations.Count; i++)
            {
                CharacterTestGenesisValidationStation station = normalized.Stations[i];
                string path = "stations[" + i + "]";
                if (string.IsNullOrEmpty(station.StableId))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("MissingStationStableId", path + ".stableId", "Station StableId is required."));
                else if (!stations.Add(station.StableId))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("DuplicateStationStableId", path + ".stableId", "Station StableId must be unique."));
                if (!groups.Contains(station.GroupId))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("MissingStationGroup", path + ".groupId", "Station GroupId must resolve to one root group."));
                if (station.ResetPolicy != CharacterTestGenesisValidationResetPolicy.SessionRebuild)
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("UnsupportedResetPolicy", path + ".resetPolicy", "Schema v1 only supports SessionRebuild."));
                if (string.IsNullOrWhiteSpace(station.Observation.BodyPrompt))
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("MissingBodyPrompt", path + ".observation.bodyPrompt", "BodyPrompt is required."));

                if (!CharacterTestGenesisValidationAuthoringScenarioRegistry.TryGet(
                        station.Scenario.TypeId,
                        station.Scenario.SchemaVersion,
                        out ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor))
                {
                    issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error("UnknownScenarioType", path + ".scenario", "Authoring scenario descriptor is not registered."));
                }
                else
                {
                    descriptor.TryNormalizeAndValidate(
                        station.Scenario,
                        station.Bindings,
                        station.Observation,
                        issues,
                        path);
                }

                ValidateBindings(station.Bindings, owners, issues, path + ".bindings");
                CharacterTestGenesisValidationSceneDesignValidator.ValidateStationSpatialProgram(
                    station,
                    zones,
                    owners,
                    issues,
                    path + ".spatialProgram");
            }

            return new CharacterTestGenesisValidationAuthoringReport(issues);
        }

        private static void ValidateBindings(
            CharacterTestGenesisValidationBindings bindings,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path)
        {
            if (bindings == null)
                return;
            if (bindings.SubjectCharacter != null && !owners.ContainsCharacter(bindings.SubjectCharacter.StableId))
                AddMissing("CharacterRefNotFound", path + ".subjectCharacter", issues);
            if (bindings.TargetMovableObject != null && !owners.ContainsMovable(bindings.TargetMovableObject.InstanceStableId))
                AddMissing("MovableObjectRefNotFound", path + ".targetMovableObject", issues);
            if (bindings.StartPoint != null && !owners.ContainsPoint(bindings.StartPoint.StableId))
                AddMissing("PointRefNotFound", path + ".startPoint", issues);
            if (bindings.ResetPoint != null && !owners.ContainsPoint(bindings.ResetPoint.StableId))
                AddMissing("PointRefNotFound", path + ".resetPoint", issues);
            if (bindings.ObservationRegion != null && !owners.ContainsRegion(bindings.ObservationRegion.StableId))
                AddMissing("RegionRefNotFound", path + ".observationRegion", issues);
            ValidateStatic(bindings.TargetStaticCollider, owners, issues, path + ".targetStaticCollider");
            if (bindings.SampleStaticColliders != null)
            {
                for (int i = 0; i < bindings.SampleStaticColliders.Count; i++)
                    ValidateStatic(bindings.SampleStaticColliders[i], owners, issues, path + ".sampleStaticColliders[" + i + "]");
            }
        }

        private static void ValidateStatic(
            CharacterTestGenesisValidationStaticColliderRef reference,
            CharacterTestGenesisValidationOwnerCatalog owners,
            List<CharacterTestGenesisValidationAuthoringIssue> issues,
            string path)
        {
            if (reference == null)
                return;
            if (!owners.TryResolveSingleStatic(reference, out string code))
                issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(code, path, "Static collider reference cannot be resolved uniquely in the owner catalog."));
        }

        private static void AddMissing(
            string code,
            string path,
            List<CharacterTestGenesisValidationAuthoringIssue> issues)
        {
            issues.Add(CharacterTestGenesisValidationAuthoringIssue.Error(code, path, "Typed owner identity cannot be resolved."));
        }
    }

    internal static class CharacterTestGenesisValidationCapabilityResolver
    {
        public static CharacterTestGenesisValidationAuthorityStatus ResolveAuthority(
            CharacterTestGenesisValidationStation station)
        {
            return station != null && station.Enabled
                ? CharacterTestGenesisValidationAuthorityStatus.HarnessOnly
                : CharacterTestGenesisValidationAuthorityStatus.Unsupported;
        }

        public static CharacterTestGenesisValidationSceneReadiness ResolveSceneReadiness(
            CharacterTestGenesisValidationStation station)
        {
            return CharacterTestGenesisValidationSceneReadiness.NotRunnable;
        }

        public static CharacterTestGenesisValidationCacheHealth ResolveCacheHealth()
        {
            return CharacterTestGenesisValidationCacheHealth.NotBuilt;
        }
    }

    internal sealed class CharacterTestGenesisValidationGroupComparer :
        IComparer<CharacterTestGenesisValidationGroup>
    {
        public static readonly CharacterTestGenesisValidationGroupComparer Instance =
            new CharacterTestGenesisValidationGroupComparer();

        public int Compare(
            CharacterTestGenesisValidationGroup left,
            CharacterTestGenesisValidationGroup right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(left.StableId, right.StableId);
        }
    }

    internal sealed class CharacterTestGenesisValidationStationComparer :
        IComparer<CharacterTestGenesisValidationStation>
    {
        private readonly IReadOnlyDictionary<string, int> _groupOrder;

        public CharacterTestGenesisValidationStationComparer(
            IReadOnlyDictionary<string, int> groupOrder)
        {
            _groupOrder = groupOrder;
        }

        public int Compare(
            CharacterTestGenesisValidationStation left,
            CharacterTestGenesisValidationStation right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int leftGroup = ResolveGroup(left.GroupId);
            int rightGroup = ResolveGroup(right.GroupId);
            int group = leftGroup.CompareTo(rightGroup);
            if (group != 0) return group;
            int order = left.SortOrder.CompareTo(right.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(left.StableId, right.StableId);
        }

        private int ResolveGroup(string id)
        {
            return _groupOrder != null && _groupOrder.TryGetValue(id ?? string.Empty, out int order)
                ? order
                : int.MaxValue;
        }
    }

    internal static class CharacterTestGenesisValidationAuthoringAdapter
    {
        public const string ModuleId = "characterTest.genesisValidation";
        public const int ModuleSchemaVersion = 1;
        public const string DisplayName = "CharacterTest Genesis Validation";

        public static CharacterTestGenesisValidationAuthoringDocument Extract(
            MxLevelAuthoringDocument envelope)
        {
            if (envelope != null
                && envelope.TryGetModule(ModuleId, out MxLevelAuthoringModuleDocument module))
            {
                if (module.SchemaVersion != ModuleSchemaVersion)
                    return CreateUnsupportedDocument();
                try
                {
                    CharacterTestGenesisValidationAuthoringDocument payload =
                        module.ReadPayload<CharacterTestGenesisValidationAuthoringDocument>();
                    return payload ?? CreateUnreadableDocument(
                        "Validation payload could not be read.");
                }
                catch (Exception exception)
                {
                    return CreateUnreadableDocument(
                        "Validation payload contains an unsupported value: " + exception.Message);
                }
            }

            return CreateDefaultDocument();
        }

        public static MxLevelAuthoringDocument Upsert(
            MxLevelAuthoringDocument envelope,
            CharacterTestGenesisValidationAuthoringDocument document)
        {
            envelope ??= new MxLevelAuthoringDocument();
            envelope = envelope.Normalize();
            envelope.UpsertModulePayload(
                ModuleId,
                ModuleSchemaVersion,
                document?.Normalize() ?? CreateDefaultDocument(),
                DisplayName);
            return envelope.Normalize();
        }

        public static CharacterTestGenesisValidationAuthoringDocument CreateDefaultDocument()
        {
            return new CharacterTestGenesisValidationAuthoringDocument
            {
                Groups = new List<CharacterTestGenesisValidationGroup>
                {
                    new CharacterTestGenesisValidationGroup { StableId = "surface-materials", DisplayName = "表面材料", SortOrder = 10 },
                    new CharacterTestGenesisValidationGroup { StableId = "boundary-forms", DisplayName = "边界形态", SortOrder = 20 },
                    new CharacterTestGenesisValidationGroup { StableId = "movable-natural-objects", DisplayName = "可动自然物", SortOrder = 30 },
                    new CharacterTestGenesisValidationGroup { StableId = "later-candidates", DisplayName = "后续候选", SortOrder = 40 }
                }
            }.Normalize();
        }

        private static CharacterTestGenesisValidationAuthoringDocument CreateUnsupportedDocument()
        {
            return new CharacterTestGenesisValidationAuthoringDocument
            {
                SchemaVersion = -1
            };
        }

        private static CharacterTestGenesisValidationAuthoringDocument CreateUnreadableDocument(
            string message)
        {
            var document = new CharacterTestGenesisValidationAuthoringDocument();
            document.LoadErrors.Add(string.IsNullOrWhiteSpace(message)
                ? "Validation payload could not be read."
                : message.Trim());
            return document;
        }
    }
}
