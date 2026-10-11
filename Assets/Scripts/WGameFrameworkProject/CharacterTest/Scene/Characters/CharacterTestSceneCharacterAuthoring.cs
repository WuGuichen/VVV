using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Config.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestSceneCharacterSpawnPose
    {
        [JsonConverter(typeof(CharacterTestSpawnPositionJsonConverter))]
        public Vector3 Position { get; set; }

        public float YawDegrees { get; set; }

        public CharacterTestSceneCharacterSpawnPose Normalize()
        {
            return new CharacterTestSceneCharacterSpawnPose
            {
                Position = Position,
                YawDegrees = IsFinite(YawDegrees) ? Mathf.Repeat(YawDegrees, 360f) : YawDegrees
            };
        }

        internal bool IsValid => IsFinite(Position.x) && IsFinite(Position.y)
            && IsFinite(Position.z) && IsFinite(YawDegrees);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Vector3's computed properties must never become part of the authoring wire format/hash.
    internal sealed class CharacterTestSpawnPositionJsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            JObject value = JObject.Load(reader);
            return new Vector3(ReadCoordinate(value, "x"), ReadCoordinate(value, "y"), ReadCoordinate(value, "z"));
        }

        private static float ReadCoordinate(JObject value, string name)
        {
            JToken coordinate = value[name];
            if (coordinate == null || (coordinate.Type != JTokenType.Integer && coordinate.Type != JTokenType.Float))
                throw new JsonSerializationException("Spawn position requires numeric " + name + ".");
            return coordinate.Value<float>();
        }
    }

    public sealed class CharacterTestSceneCharacterAuthoringDocument
    {
        public const string FormatV1 = "mx.character-test.scene-characters.v1";
        public const string FormatV2 = "mx.character-test.scene-characters.v2";
        public const string CurrentFormat = FormatV2;
        public const int LegacySchemaVersion = 1;
        public const int CurrentSchemaVersion = 2;

        public CharacterTestSceneCharacterAuthoringDocument()
        {
            Format = CurrentFormat;
            SchemaVersion = CurrentSchemaVersion;
            LevelId = string.Empty;
            ScenePath = string.Empty;
            RootPath = string.Empty;
            Characters = new List<CharacterTestSceneCharacterDefinition>();
        }

        public string Format { get; set; }

        public int SchemaVersion { get; set; }

        public string LevelId { get; set; }

        public string ScenePath { get; set; }

        public string RootPath { get; set; }

        public List<CharacterTestSceneCharacterDefinition> Characters { get; set; }

        public CharacterTestSceneCharacterAuthoringDocument Normalize()
        {
            var normalized = new CharacterTestSceneCharacterAuthoringDocument
            {
                Format = Format,
                SchemaVersion = SchemaVersion,
                LevelId = Trim(LevelId),
                ScenePath = NormalizePath(ScenePath),
                RootPath = NormalizePath(RootPath),
                Characters = new List<CharacterTestSceneCharacterDefinition>()
            };

            if (Characters != null)
            {
                for (int i = 0; i < Characters.Count; i++)
                {
                    if (Characters[i] != null)
                        normalized.Characters.Add(Characters[i].Normalize());
                }
            }

            normalized.Characters.Sort(CharacterTestSceneCharacterDefinitionComparer.Instance);
            return normalized;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizePath(string value)
        {
            return Trim(value).Replace('\\', '/');
        }
    }

    public sealed class CharacterTestSceneCharacterDefinition
    {
        public CharacterTestSceneCharacterDefinition()
        {
            Enabled = true;
            StableId = string.Empty;
            DisplayName = string.Empty;
            TransformPath = string.Empty;
            TransformRef = new LevelTransformReference();
            CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId;
            TeamId = 1;
            ControlOwner = CharacterTestCharacterControlOwner.None;
            RuntimeAiPlannerProfilePath = string.Empty;
            Group = string.Empty;
            Notes = string.Empty;
        }

        public bool Enabled { get; set; }

        public string StableId { get; set; }

        public string DisplayName { get; set; }

        public string TransformPath { get; set; }

        public LevelTransformReference TransformRef { get; set; }

        public CharacterTestSceneCharacterSpawnPose SpawnPose { get; set; }

        public int CharacterId { get; set; }

        public int TeamId { get; set; }

        public CharacterTestCharacterControlOwner ControlOwner { get; set; }

        public string RuntimeAiPlannerProfilePath { get; set; }

        public string Group { get; set; }

        public CharacterTestLevelBehaviorBinding BehaviorBinding { get; set; }
        public bool ShouldSerializeBehaviorBinding() => BehaviorBinding != null;

        public string Notes { get; set; }

        public bool ShouldSerializeTransformPath()
        {
            return false;
        }

        public CharacterTestSceneCharacterDefinition Normalize()
        {
            LevelTransformReference transformRef = TransformRef == null
                ? new LevelTransformReference()
                : TransformRef.Normalize();
            if (string.IsNullOrWhiteSpace(transformRef.Path) && !string.IsNullOrWhiteSpace(TransformPath))
                transformRef.Path = NormalizePath(TransformPath);

            return new CharacterTestSceneCharacterDefinition
            {
                Enabled = Enabled,
                StableId = Trim(StableId),
                DisplayName = Trim(DisplayName),
                TransformPath = transformRef.Path,
                TransformRef = transformRef,
                SpawnPose = SpawnPose?.Normalize(),
                CharacterId = CharacterId,
                TeamId = TeamId,
                ControlOwner = ControlOwner,
                RuntimeAiPlannerProfilePath = NormalizeRuntimeAiPlannerProfilePath(RuntimeAiPlannerProfilePath),
                Group = Trim(Group),
                BehaviorBinding = BehaviorBinding?.Clone(),
                Notes = Trim(Notes)
            };
        }

        internal static bool IsKnownControlOwner(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.None
                || owner == CharacterTestCharacterControlOwner.LocalPlayer
                || owner == CharacterTestCharacterControlOwner.RemotePlayer
                || owner == CharacterTestCharacterControlOwner.Scripted
                || owner == CharacterTestCharacterControlOwner.AiControlled;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizePath(string value)
        {
            return Trim(value).Replace('\\', '/');
        }

        private static string NormalizeRuntimeAiPlannerProfilePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(value);
        }
    }

    internal sealed class CharacterTestSceneCharacterDefinitionComparer :
        IComparer<CharacterTestSceneCharacterDefinition>
    {
        public static readonly CharacterTestSceneCharacterDefinitionComparer Instance =
            new CharacterTestSceneCharacterDefinitionComparer();

        private CharacterTestSceneCharacterDefinitionComparer()
        {
        }

        public int Compare(
            CharacterTestSceneCharacterDefinition left,
            CharacterTestSceneCharacterDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int group = string.CompareOrdinal(left.Group ?? string.Empty, right.Group ?? string.Empty);
            if (group != 0)
                return group;

            int stable = string.CompareOrdinal(left.StableId ?? string.Empty, right.StableId ?? string.Empty);
            if (stable != 0)
                return stable;

            return string.CompareOrdinal(
                left.TransformRef?.Path ?? left.TransformPath ?? string.Empty,
                right.TransformRef?.Path ?? right.TransformPath ?? string.Empty);
        }
    }

    [Serializable]
    public sealed class CharacterTestSceneCharacterCache
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _authoringJsonPath = "";
        [SerializeField] private string _authoringContentHash = "";
        [SerializeField] private string _authoringLastAppliedUtc = "";
        [SerializeField] private CharacterInstance[] _characters = Array.Empty<CharacterInstance>();

        private string[] _lastDiagnostics = Array.Empty<string>();

        public bool Enabled => _enabled;

        public string AuthoringJsonPath => _authoringJsonPath ?? string.Empty;

        public string AuthoringContentHash => _authoringContentHash ?? string.Empty;

        public string AuthoringLastAppliedUtc => _authoringLastAppliedUtc ?? string.Empty;

        public IReadOnlyList<CharacterInstance> Characters =>
            _characters ?? Array.Empty<CharacterInstance>();

        public int CharacterInstanceCount => _characters == null ? 0 : _characters.Length;

        public IReadOnlyList<string> LastDiagnostics => _lastDiagnostics;

        public void Configure(IEnumerable<CharacterInstance> characters)
        {
            _characters = ToArray(characters);
        }

        public void ConfigureFromAuthoring(
            IEnumerable<CharacterInstance> characters,
            string authoringJsonPath,
            string authoringContentHash,
            string authoringLastAppliedUtc)
        {
            Configure(characters);
            _authoringJsonPath = NormalizeAuthoringString(authoringJsonPath);
            _authoringContentHash = NormalizeAuthoringString(authoringContentHash);
            _authoringLastAppliedUtc = NormalizeAuthoringString(authoringLastAppliedUtc);
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            out string[] diagnostics)
        {
            return CollectCharacterSpawnSpecs(new CharacterTestCharacterDefinitionRegistry(), out diagnostics);
        }

        public CharacterTestCharacterSpawnSpec[] CollectCharacterSpawnSpecs(
            CharacterTestCharacterDefinitionRegistry characterRegistry,
            out string[] diagnostics)
        {
            var localDiagnostics = new List<string>();
            if (!_enabled || _characters == null || _characters.Length == 0)
            {
                diagnostics = Array.Empty<string>();
                _lastDiagnostics = diagnostics;
                return Array.Empty<CharacterTestCharacterSpawnSpec>();
            }

            var instances = new List<CharacterInstance>(_characters);
            instances.Sort(CharacterInstanceComparer.Instance);

            var specs = new List<CharacterTestCharacterSpawnSpec>();
            bool hasLocalPlayer = false;
            int aiCount = 0;
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterInstance instance = instances[i];
                if (instance == null || !instance.IsEnabled)
                    continue;

                string stableId = instance.StableId;
                if (!string.IsNullOrWhiteSpace(stableId) && !stableIds.Add(stableId))
                {
                    localDiagnostics.Add("scene-characters.duplicate-stable-id:" + stableId);
                    continue;
                }

                if (!instance.TryCreateSpawnSpec(
                        characterRegistry,
                        out CharacterTestCharacterSpawnSpec spec,
                        out string diagnostic))
                {
                    if (!string.IsNullOrEmpty(diagnostic))
                        localDiagnostics.Add(diagnostic);
                    continue;
                }

                if (spec.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer)
                    hasLocalPlayer = true;
                else if (spec.ControlOwner == CharacterTestCharacterControlOwner.AiControlled)
                    aiCount++;
                specs.Add(spec);
            }

            if (!hasLocalPlayer)
                localDiagnostics.Add("scene-characters.missing-local-player");
            if (aiCount == 0)
                localDiagnostics.Add("scene-characters.missing-ai-controlled");

            diagnostics = localDiagnostics.ToArray();
            _lastDiagnostics = diagnostics;
            return specs.ToArray();
        }

        private static T[] ToArray<T>(IEnumerable<T> values)
        {
            if (values == null)
                return Array.Empty<T>();

            var list = new List<T>(values);
            return list.ToArray();
        }

        private static string NormalizeAuthoringString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        public sealed class CharacterInstance
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private string _stableId = "";
            [SerializeField] private string _displayName = "";
            [SerializeField] private int _characterId =
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId;
            [SerializeField] private int _teamId = 1;
            [SerializeField] private CharacterTestCharacterControlOwner _controlOwner =
                CharacterTestCharacterControlOwner.None;
            [SerializeField] private string _runtimeAiPlannerProfilePath = "";
            [SerializeField] private Transform _transform;
            [SerializeField] private bool _hasSpawnPose;
            [SerializeField] private Vector3 _spawnPosition;
            [SerializeField] private float _spawnYawDegrees;
            [SerializeField] private string _group = "";
            [SerializeField] private CharacterTestLevelBehaviorBinding _behaviorBinding;
            [SerializeField] private string _notes = "";

            public CharacterInstance()
            {
            }

            public CharacterInstance(
                bool enabled,
                string stableId,
                string displayName,
                int characterId,
                int teamId,
                CharacterTestCharacterControlOwner controlOwner,
                Transform transform,
                string group,
                string notes,
                string runtimeAiPlannerProfilePath = "",
                bool hasSpawnPose = false,
                Vector3 spawnPosition = default,
                float spawnYawDegrees = 0f,
                CharacterTestLevelBehaviorBinding behaviorBinding = null)
            {
                _enabled = enabled;
                _stableId = stableId ?? string.Empty;
                _displayName = displayName ?? string.Empty;
                _characterId = characterId;
                _teamId = teamId;
                _controlOwner = controlOwner;
                _runtimeAiPlannerProfilePath = runtimeAiPlannerProfilePath ?? string.Empty;
                _transform = transform;
                _hasSpawnPose = hasSpawnPose;
                _spawnPosition = spawnPosition;
                _spawnYawDegrees = spawnYawDegrees;
                _group = group ?? string.Empty;
                _behaviorBinding = behaviorBinding?.Clone();
                _notes = notes ?? string.Empty;
            }

            public bool IsEnabled => _enabled;

            public string StableId => string.IsNullOrWhiteSpace(_stableId)
                ? string.Empty
                : _stableId.Trim();

            public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
                ? StableId
                : _displayName.Trim();

            public int CharacterId => _characterId;

            public int TeamId => _teamId;

            public CharacterTestCharacterControlOwner ControlOwner => _controlOwner;

            public string RuntimeAiPlannerProfilePath => string.IsNullOrWhiteSpace(_runtimeAiPlannerProfilePath)
                ? string.Empty
                : CharacterTestRuntimeAiPlannerProfileJson.NormalizeStreamingAssetsRelativePath(
                    _runtimeAiPlannerProfilePath);

            public Transform Transform => _transform;

            public bool HasSpawnPose => _hasSpawnPose;
            public Vector3 SpawnPosition => _spawnPosition;
            public float SpawnYawDegrees => _spawnYawDegrees;

            public CharacterTestLevelBehaviorBinding BehaviorBinding => _behaviorBinding?.Clone();
            public string Group => string.IsNullOrWhiteSpace(_group) ? string.Empty : _group.Trim();

            public string Notes => string.IsNullOrWhiteSpace(_notes) ? string.Empty : _notes.Trim();

            public bool TryCreateSpawnSpec(
                CharacterTestCharacterDefinitionRegistry characterRegistry,
                out CharacterTestCharacterSpawnSpec spec,
                out string diagnostic)
            {
                spec = default;
                diagnostic = string.Empty;
                if (!_enabled)
                    return false;
                if (!_hasSpawnPose && _transform == null)
                {
                    diagnostic = "scene-characters.missing-transform:" + StableId;
                    return false;
                }
                if (characterRegistry == null)
                {
                    diagnostic = "scene-characters.missing-character-registry";
                    return false;
                }
                if (!characterRegistry.TryGet(_characterId, out _))
                {
                    diagnostic = "scene-characters.character-not-found:" + _characterId;
                    return false;
                }
                if (_teamId <= 0)
                {
                    diagnostic = "scene-characters.invalid-team:" + StableId;
                    return false;
                }
                if (!CharacterTestSceneCharacterDefinition.IsKnownControlOwner(_controlOwner))
                {
                    diagnostic = "scene-characters.invalid-control-owner:" + StableId;
                    return false;
                }

                Vector3 position = _hasSpawnPose ? _spawnPosition : _transform.position;
                spec = new CharacterTestCharacterSpawnSpec(
                    _characterId,
                    _teamId,
                    position.x,
                    position.y,
                    position.z,
                    _controlOwner,
                    RuntimeAiPlannerProfilePath, instanceStableId: StableId);
                return true;
            }
        }

        private sealed class CharacterInstanceComparer : IComparer<CharacterInstance>
        {
            public static readonly CharacterInstanceComparer Instance = new CharacterInstanceComparer();

            private CharacterInstanceComparer()
            {
            }

            public int Compare(CharacterInstance left, CharacterInstance right)
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return -1;
                if (right == null)
                    return 1;

                int group = string.CompareOrdinal(left.Group, right.Group);
                if (group != 0)
                    return group;

                int stable = string.CompareOrdinal(left.StableId, right.StableId);
                if (stable != 0)
                    return stable;

                return left.CharacterId.CompareTo(right.CharacterId);
            }
        }
    }

    public static class CharacterTestSceneCharacterAuthoringAdapter
    {
        public const string DefaultLevelId = CharacterTestWorldObjectAuthoringAdapter.DefaultLevelId;
        public const string DefaultScenePath = CharacterTestWorldObjectAuthoringAdapter.DefaultScenePath;
        public const string DefaultDocumentPath = CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath;
        public const string CharactersModuleId = MxLevelAuthoringDocument.CharacterTestCharactersModuleId;
        public const int CharactersModuleSchemaVersion = 2;

        public const string MissingCharactersModule = "MissingCharactersModule";
        public const string InvalidCharactersModulePayload = "InvalidCharactersModulePayload";
        public const string MissingStableId = "MissingStableId";
        public const string DuplicateStableId = "DuplicateStableId";
        public const string MissingTransformPath = "MissingTransformPath";
        public const string TransformNotFound = "TransformNotFound";
        public const string InvalidCharacterId = "InvalidCharacterId";
        public const string CharacterDefinitionNotFound = "CharacterDefinitionNotFound";
        public const string InvalidTeamId = "InvalidTeamId";
        public const string InvalidSpawnPose = "InvalidSpawnPose";
        public const string InvalidControlOwner = "InvalidControlOwner";
        public const string RuntimeAiPlannerProfileRequiresAiControlled =
            "RuntimeAiPlannerProfileRequiresAiControlled";
        public const string MissingLocalPlayer = "MissingLocalPlayer";
        public const string MissingAiControlled = "MissingAiControlled";

        public static MxLevelAuthoringDocument CreateEnvelopeFromDocument(
            CharacterTestSceneCharacterAuthoringDocument document,
            Transform rootTransform = null,
            MxLevelAuthoringDocument existingEnvelope = null)
        {
            MxLevelAuthoringDocument envelope = existingEnvelope == null
                ? new MxLevelAuthoringDocument()
                : existingEnvelope.Normalize();
            return UpsertCharactersModule(envelope, document, rootTransform);
        }

        public static MxLevelAuthoringDocument UpsertCharactersModule(
            MxLevelAuthoringDocument envelope,
            CharacterTestSceneCharacterAuthoringDocument document,
            Transform rootTransform = null)
        {
            CharacterTestSceneCharacterAuthoringDocument normalized =
                document == null
                    ? new CharacterTestSceneCharacterAuthoringDocument()
                    : document.Normalize();
            envelope ??= new MxLevelAuthoringDocument();
            ApplyEnvelopeHeader(envelope, normalized, rootTransform);
            envelope.UpsertModulePayload(
                CharactersModuleId,
                normalized.SchemaVersion,
                normalized,
                "CharacterTest Characters");
            return envelope.Normalize();
        }

        public static bool TryExtractCharactersDocument(
            MxLevelAuthoringDocument envelope,
            out CharacterTestSceneCharacterAuthoringDocument document,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            document = null;
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (envelope == null)
            {
                issues.Add(Error(
                    InvalidCharactersModulePayload,
                    "document",
                    "Level authoring envelope is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            MxLevelAuthoringDocument normalized = envelope.Normalize();
            if (!normalized.TryGetModule(CharactersModuleId, out MxLevelAuthoringModuleDocument module))
            {
                issues.Add(Error(
                    MissingCharactersModule,
                    "modules",
                    "Level authoring envelope does not contain the CharacterTest characters module."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            CharacterTestSceneCharacterAuthoringDocument payload;
            try
            {
                payload = module.ReadPayload<CharacterTestSceneCharacterAuthoringDocument>();
            }
            catch (JsonException exception)
            {
                issues.Add(Error(InvalidCharactersModulePayload,
                    "modules." + CharactersModuleId + ".payload", exception.Message));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }
            if (payload == null)
            {
                issues.Add(Error(
                    InvalidCharactersModulePayload,
                    "modules." + CharactersModuleId + ".payload",
                    "CharacterTest characters module payload could not be decoded."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (!IsSupportedVersion(payload.Format, payload.SchemaVersion)
                || module.SchemaVersion != payload.SchemaVersion)
            {
                issues.Add(Error(LevelWorldObjectAuthoringValidator.UnsupportedSchemaVersion,
                    "modules." + CharactersModuleId, "Expected matching characters module/payload v1 or v2."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            document = payload.Normalize();
            if (string.IsNullOrWhiteSpace(document.LevelId))
                document.LevelId = normalized.LevelId;
            if (string.IsNullOrWhiteSpace(document.ScenePath))
                document.ScenePath = normalized.ScenePath;
            if (string.IsNullOrWhiteSpace(document.RootPath))
                document.RootPath = normalized.RootTransformRef?.Path ?? normalized.RootPath;
            report = LevelWorldObjectAuthoringValidationReport.Empty;
            return true;
        }

        public static CharacterTestSceneCharacterAuthoringDocument CreateDocumentFromCache(
            CharacterTestSceneCharacterCache cache,
            Transform root,
            string levelId = null,
            string scenePath = null)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var document = new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = string.IsNullOrWhiteSpace(levelId) ? DefaultLevelId : levelId.Trim(),
                ScenePath = string.IsNullOrWhiteSpace(scenePath) ? DefaultScenePath : scenePath.Trim().Replace('\\', '/'),
                RootPath = root.name,
                Characters = new List<CharacterTestSceneCharacterDefinition>()
            };

            IReadOnlyList<CharacterTestSceneCharacterCache.CharacterInstance> instances = cache.Characters;
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterTestSceneCharacterCache.CharacterInstance instance = instances[i];
                if (instance == null)
                    continue;

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, instance.Transform);
                document.Characters.Add(new CharacterTestSceneCharacterDefinition
                {
                    Enabled = instance.IsEnabled,
                    StableId = instance.StableId,
                    DisplayName = instance.DisplayName,
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    SpawnPose = instance.HasSpawnPose ? new CharacterTestSceneCharacterSpawnPose
                    {
                        Position = instance.SpawnPosition,
                        YawDegrees = instance.SpawnYawDegrees
                    } : null,
                    CharacterId = instance.CharacterId,
                    TeamId = instance.TeamId,
                    ControlOwner = instance.ControlOwner,
                    RuntimeAiPlannerProfilePath = instance.RuntimeAiPlannerProfilePath,
                    Group = instance.Group,
                    BehaviorBinding = instance.BehaviorBinding,
                    Notes = instance.Notes
                });
            }

            return document.Normalize();
        }

        public static CharacterTestSceneCharacterAuthoringDocument CreateDefaultDocument(
            string rootPath = "CharacterTestSceneRoot")
        {
            var document = new CharacterTestSceneCharacterAuthoringDocument
            {
                LevelId = DefaultLevelId,
                ScenePath = DefaultScenePath,
                RootPath = string.IsNullOrWhiteSpace(rootPath) ? "CharacterTestSceneRoot" : rootPath.Trim(),
                Characters = new List<CharacterTestSceneCharacterDefinition>
                {
                    CreateTemplateCharacter(
                        "player.local",
                        "Local Player",
                        "Spawns/Player[0]",
                        1,
                        CharacterTestCharacterControlOwner.LocalPlayer),
                    CreateTemplateCharacter(
                        "enemy.ai.01",
                        "AI Enemy",
                        "Spawns/Enemy_AI[0]",
                        2,
                        CharacterTestCharacterControlOwner.AiControlled)
                }
            };
            return document.Normalize();
        }

        public static bool ApplyEnvelopeToCache(
            MxLevelAuthoringDocument envelope,
            CharacterTestSceneCharacterCache cache,
            Transform root,
            string authoringJsonPath,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            if (!TryExtractCharactersDocument(envelope, out CharacterTestSceneCharacterAuthoringDocument document, out report))
                return false;

            MxLevelAuthoringValidationReport envelopeReport = MxLevelAuthoringValidator.Validate(envelope);
            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForCache(document, cache, root, new CharacterTestCharacterDefinitionRegistry());
            report = MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
            if (report.HasErrors)
                return false;

            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            bool built = TryBuildCache(
                document.Normalize(),
                root,
                new CharacterTestCharacterDefinitionRegistry(),
                out List<CharacterTestSceneCharacterCache.CharacterInstance> instances,
                out LevelWorldObjectAuthoringValidationReport buildReport);
            report = MergeReports(envelopeReport.ToWorldObjectReport(), buildReport);
            if (!built)
                return false;

            cache.ConfigureFromAuthoring(
                instances,
                authoringJsonPath,
                envelope == null ? string.Empty : MxLevelAuthoringJson.ComputeContentHash(envelope),
                DateTime.UtcNow.ToString("o"));
            return true;
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateEnvelopeForCache(
            MxLevelAuthoringDocument envelope,
            CharacterTestSceneCharacterCache cache,
            Transform root)
        {
            MxLevelAuthoringValidationReport envelopeReport = MxLevelAuthoringValidator.Validate(envelope);
            if (!TryExtractCharactersDocument(
                    envelope,
                    out CharacterTestSceneCharacterAuthoringDocument document,
                    out LevelWorldObjectAuthoringValidationReport extractReport))
            {
                return MergeReports(envelopeReport.ToWorldObjectReport(), extractReport);
            }

            LevelWorldObjectAuthoringValidationReport documentReport =
                ValidateDocumentForCache(document, cache, root, new CharacterTestCharacterDefinitionRegistry());
            return MergeReports(envelopeReport.ToWorldObjectReport(), documentReport);
        }

        public static LevelWorldObjectAuthoringValidationReport ValidateDocumentForCache(
            CharacterTestSceneCharacterAuthoringDocument document,
            CharacterTestSceneCharacterCache cache,
            Transform root,
            CharacterTestCharacterDefinitionRegistry characterRegistry)
        {
            TryBuildCache(
                document == null ? null : document.Normalize(),
                root,
                characterRegistry,
                out _,
                out LevelWorldObjectAuthoringValidationReport report);
            return report;
        }

        public static string ComputeContentHash(CharacterTestSceneCharacterAuthoringDocument document)
        {
            if (document == null)
                return string.Empty;

            string json = JsonConvert.SerializeObject(document.Normalize(), CreateJsonSettings());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool TryBuildCache(
            CharacterTestSceneCharacterAuthoringDocument normalized,
            Transform root,
            CharacterTestCharacterDefinitionRegistry characterRegistry,
            out List<CharacterTestSceneCharacterCache.CharacterInstance> instances,
            out LevelWorldObjectAuthoringValidationReport report)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            instances = new List<CharacterTestSceneCharacterCache.CharacterInstance>();
            if (normalized == null)
            {
                issues.Add(Error(
                    "MissingDocument",
                    string.Empty,
                    "CharacterTest characters authoring document is missing."));
                report = new LevelWorldObjectAuthoringValidationReport(issues);
                return false;
            }

            if (normalized.Format != CharacterTestSceneCharacterAuthoringDocument.FormatV1
                && normalized.Format != CharacterTestSceneCharacterAuthoringDocument.FormatV2)
            {
                issues.Add(Error(
                    LevelWorldObjectAuthoringValidator.UnsupportedFormat,
                    "format",
                    "Expected format " + CharacterTestSceneCharacterAuthoringDocument.CurrentFormat + "."));
            }
            if (!IsSupportedVersion(normalized.Format, normalized.SchemaVersion))
            {
                issues.Add(Error(
                    LevelWorldObjectAuthoringValidator.UnsupportedSchemaVersion,
                    "schemaVersion",
                    "Expected schema version " + CharacterTestSceneCharacterAuthoringDocument.CurrentSchemaVersion + "."));
            }
            if (string.IsNullOrWhiteSpace(normalized.LevelId))
                issues.Add(Error(LevelWorldObjectAuthoringValidator.MissingLevelId, "levelId", "Level id is required."));
            if (string.IsNullOrWhiteSpace(normalized.ScenePath))
                issues.Add(Error(LevelWorldObjectAuthoringValidator.MissingScenePath, "scenePath", "Scene path is required."));

            var stableIds = new Dictionary<string, int>(StringComparer.Ordinal);
            bool hasLocalPlayer = false;
            int aiCount = 0;
            IReadOnlyList<CharacterTestSceneCharacterDefinition> characters =
                normalized.Characters == null
                    ? Array.Empty<CharacterTestSceneCharacterDefinition>()
                    : normalized.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character = characters[i];
                if (character == null)
                    continue;

                string path = "characters[" + i + "]";
                ValidateCharacter(
                    character,
                    path,
                    root,
                    characterRegistry,
                    stableIds,
                    issues,
                    out Transform transform);
                if (character.Enabled
                    && character.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer)
                {
                    hasLocalPlayer = true;
                }
                else if (character.Enabled
                    && character.ControlOwner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    aiCount++;
                }

                instances.Add(new CharacterTestSceneCharacterCache.CharacterInstance(
                    character.Enabled,
                    character.StableId,
                    character.DisplayName,
                    character.CharacterId,
                    character.TeamId,
                    character.ControlOwner,
                    transform,
                    character.Group,
                    character.Notes,
                    character.RuntimeAiPlannerProfilePath,
                    character.SpawnPose != null,
                    character.SpawnPose?.Position ?? default,
                    character.SpawnPose?.YawDegrees ?? 0f, character.BehaviorBinding));
            }

            if (!hasLocalPlayer)
                issues.Add(Warning(MissingLocalPlayer, "characters", "No enabled LocalPlayer character is configured."));
            if (aiCount == 0)
                issues.Add(Warning(MissingAiControlled, "characters", "No enabled AiControlled character is configured."));

            report = new LevelWorldObjectAuthoringValidationReport(issues);
            return !report.HasErrors;
        }

        private static void ValidateCharacter(
            CharacterTestSceneCharacterDefinition character,
            string path,
            Transform root,
            CharacterTestCharacterDefinitionRegistry characterRegistry,
            Dictionary<string, int> stableIds,
            List<LevelWorldObjectAuthoringIssue> issues,
            out Transform transform)
        {
            transform = null;
            if (character == null)
                return;

            if (string.IsNullOrWhiteSpace(character.StableId))
            {
                issues.Add(Error(MissingStableId, path + ".stableId", "Stable id is required."));
            }
            else if (stableIds.TryGetValue(character.StableId, out int firstIndex))
            {
                issues.Add(Error(
                    DuplicateStableId,
                    path + ".stableId",
                    "Stable id duplicates characters[" + firstIndex + "]."));
            }
            else
            {
                stableIds.Add(character.StableId, ExtractIndex(path));
            }

            if (character.CharacterId <= 0)
            {
                issues.Add(Error(InvalidCharacterId, path + ".characterId", "Character id must be greater than 0."));
            }
            else if (characterRegistry == null || !characterRegistry.TryGet(character.CharacterId, out _))
            {
                issues.Add(Error(
                    CharacterDefinitionNotFound,
                    path + ".characterId",
                    "Character definition was not found: " + character.CharacterId + "."));
            }

            if (character.TeamId <= 0)
                issues.Add(Error(InvalidTeamId, path + ".teamId", "Team id must be greater than 0."));
            if (character.SpawnPose != null && !character.SpawnPose.IsValid)
                issues.Add(Error(InvalidSpawnPose, path + ".spawnPose", "Spawn pose must contain finite position and yaw."));
            if (!CharacterTestSceneCharacterDefinition.IsKnownControlOwner(character.ControlOwner))
            {
                issues.Add(Error(
                    InvalidControlOwner,
                    path + ".controlOwner",
                    "Control owner is not supported."));
            }

            LevelTransformReference transformRef = character.TransformRef == null
                ? new LevelTransformReference()
                : character.TransformRef.Normalize();
            if (transformRef.IsEmpty)
            {
                if (character.Enabled)
                    issues.Add(Error(MissingTransformPath, path + ".transformRef", "Transform reference is required."));
                return;
            }

            if (root != null
                && !CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(root, transformRef, out transform)
                && character.Enabled)
            {
                issues.Add(Error(
                    TransformNotFound,
                    path + ".transformRef",
                    "Transform was not found under CharacterTest scene root: " + transformRef.Path + "."));
            }
        }

        private static bool IsSupportedVersion(string format, int schemaVersion)
        {
            return (format == CharacterTestSceneCharacterAuthoringDocument.FormatV1
                    && schemaVersion == CharacterTestSceneCharacterAuthoringDocument.LegacySchemaVersion)
                || (format == CharacterTestSceneCharacterAuthoringDocument.FormatV2
                    && schemaVersion == CharacterTestSceneCharacterAuthoringDocument.CurrentSchemaVersion);
        }

        private static CharacterTestSceneCharacterDefinition CreateTemplateCharacter(
            string stableId,
            string displayName,
            string transformPath,
            int teamId,
            CharacterTestCharacterControlOwner owner)
        {
            return new CharacterTestSceneCharacterDefinition
            {
                Enabled = true,
                StableId = stableId,
                DisplayName = displayName,
                TransformRef = LevelTransformReference.FromPath(transformPath),
                TransformPath = transformPath,
                CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                TeamId = teamId,
                ControlOwner = owner,
                RuntimeAiPlannerProfilePath = string.Empty,
                Group = owner == CharacterTestCharacterControlOwner.LocalPlayer ? "Players" : "Enemies"
            };
        }

        private static void ApplyEnvelopeHeader(
            MxLevelAuthoringDocument envelope,
            CharacterTestSceneCharacterAuthoringDocument normalized,
            Transform rootTransform)
        {
            if (envelope == null)
                return;

            envelope.LevelId = string.IsNullOrWhiteSpace(normalized.LevelId)
                ? DefaultLevelId
                : normalized.LevelId;
            envelope.ScenePath = string.IsNullOrWhiteSpace(normalized.ScenePath)
                ? DefaultScenePath
                : normalized.ScenePath;
            LevelTransformReference rootRef = rootTransform == null
                ? LevelTransformReference.FromPath(normalized.RootPath)
                : LevelTransformReference.FromPath(rootTransform.name);
            envelope.RootPath = string.IsNullOrWhiteSpace(rootRef.Path) ? normalized.RootPath : rootRef.Path;
            envelope.RootTransformRef = rootRef;
        }

        private static JsonSerializerSettings CreateJsonSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        private static int ExtractIndex(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return -1;

            int start = path.IndexOf('[', StringComparison.Ordinal);
            int end = path.IndexOf(']', StringComparison.Ordinal);
            if (start < 0 || end <= start)
                return -1;
            return int.TryParse(path.Substring(start + 1, end - start - 1), out int value)
                ? value
                : -1;
        }

        private static LevelWorldObjectAuthoringValidationReport MergeReports(
            LevelWorldObjectAuthoringValidationReport left,
            LevelWorldObjectAuthoringValidationReport right)
        {
            var issues = new List<LevelWorldObjectAuthoringIssue>();
            if (left != null)
                issues.AddRange(left.Issues);
            if (right != null)
                issues.AddRange(right.Issues);
            return new LevelWorldObjectAuthoringValidationReport(issues);
        }

        private static LevelWorldObjectAuthoringIssue Error(string code, string path, string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Error,
                code,
                path,
                message);
        }

        private static LevelWorldObjectAuthoringIssue Warning(string code, string path, string message)
        {
            return new LevelWorldObjectAuthoringIssue(
                LevelWorldObjectAuthoringSeverity.Warning,
                code,
                path,
                message);
        }
    }
}
