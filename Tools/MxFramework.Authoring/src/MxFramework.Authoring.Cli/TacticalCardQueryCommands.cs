using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class TacticalCardQueryCommands
{
    internal static TacticalCardQueryOptions Options(string[] args) => new()
    {
        Name = Program.GetOption(args, "--name", null), Contains = Program.GetOption(args, "--contains", null),
        Id = Program.GetOption(args, "--id", null), InstanceId = Program.GetOption(args, "--instance-id", null),
        SaveJson = Program.GetOption(args, "--save-json", null), Locale = Program.GetOption(args, "--locale", "zh-CN"),
        SnapshotJson = Program.GetOption(args, "--snapshot-json", null), RuntimeAssembly = Program.GetOption(args, "--runtime-assembly", null),
        Layer = Program.GetOption(args, "--layer", "generated")
    };
    internal static int Dispatch(string[] args, JsonSerializerOptions json)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        string root = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        try
        {
            if (args[1] != "query") throw new ArgumentException("tactical-card query --name|--contains|--id|--instance-id; --layer disk|generated|live");
            Console.WriteLine(JsonSerializer.Serialize(Query(root, Options(args)), json));
            return Program.ExitReady;
        }
        catch (Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { schema = "mx.tactical-card.query.v1", status = "error", message = (error.InnerException ?? error).Message }, json));
            return Program.ExitToolError;
        }
    }
    internal static object Query(string root, TacticalCardQueryOptions options)
    {
        options.Validate(); var timer = Stopwatch.StartNew();
        if (options.Layer == "live") return Live(root, options);
        var selector = new TacticalCardQuery(root); var records = selector.Find(options);
        if (records.Length != 1) return new { schema = "mx.tactical-card.query.v1", status = records.Length == 0 ? "not-found" : "ambiguous",
            layer = "disk", elapsedMs = timer.ElapsedMilliseconds, candidates = records, diagnostics = selector.Diagnostics,
            message = "No record was silently selected. Use its record ID, instance ID or an exact name." };
        var record = records[0];
        object generated = null;
        if (options.Layer == "generated")
        {
            using var projector = new RuntimeProjector(root, options.RuntimeAssembly);
            generated = projector.Project(record, options.Locale);
        }
        return new { schema = "mx.tactical-card.query.v1", status = "found", layer = options.Layer, elapsedMs = timer.ElapsedMilliseconds,
            record, generated, diagnostics = selector.Diagnostics, cache = "none: source, binding, locale and assembly are observed for this query" };
    }
    private static object Live(string root, TacticalCardQueryOptions options)
    {
        string folder = Path.Combine(Path.GetFullPath(root), "Temp/MxFramework/TacticalCardQuery");
        string owner = Path.Combine(folder, "owner.json");
        if (!File.Exists(owner)) return new { status = "live-unavailable", message = "Read-only query owner has not loaded. No Editor refresh or Play change was requested." };
        var state = TacticalCardQuery.Read(owner);
        if (!DateTimeOffset.TryParse(TacticalCardQuery.Text(state, "updatedUtc"), out var timestamp) || DateTimeOffset.UtcNow - timestamp > TimeSpan.FromSeconds(8))
            return new { status = "live-unavailable", message = "Editor query owner heartbeat is stale. Use --layer disk or generated." };
        string id = Guid.NewGuid().ToString("N");
        string requests = Path.Combine(folder, "requests"); Directory.CreateDirectory(requests);
        string request = Path.Combine(requests, id + ".json"), response = Path.Combine(folder, "responses", id + ".json");
        File.WriteAllText(request + ".tmp", JsonSerializer.Serialize(new { schema = "mx.tactical-card.query-request.v1", id, options, createdUtc = DateTime.UtcNow.ToString("O") }));
        File.Move(request + ".tmp", request);
        var timeout = Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 2000)
        { if (File.Exists(response)) return TacticalCardQuery.Read(response); Thread.Sleep(25); }
        return new { status = "live-timeout", operationId = id, message = "Editor did not answer within 2 seconds. No automatic retry, refresh or Play change." };
    }

    /// <summary>Calls the existing pure projector from compiled game code, without Unity Editor or a scene.</summary>
    internal sealed class RuntimeProjector : IDisposable
    {
        private readonly string _root, _runtimePath;
        private readonly Dictionary<string, string> _assemblies = new(StringComparer.OrdinalIgnoreCase);
        private readonly Assembly _runtime;
        private readonly TacticalCardAssemblyLoadContext _context;
        private readonly JsonElement[] _actions;
        private readonly string _runtimeHash, _actionFingerprint;
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal RuntimeProjector(string root, string runtimeAssembly = null)
        {
            _root = Path.GetFullPath(root);
            _runtimePath = runtimeAssembly == null ? Path.Combine(_root, "Library/ScriptAssemblies/MxFramework.Game.CharacterTest.dll") : Path.GetFullPath(runtimeAssembly);
            if (!File.Exists(_runtimePath)) throw new InvalidOperationException("Compiled CharacterTest runtime is unavailable; --layer disk remains usable. Query never compiles Unity.");
            AddDirectory(Path.Combine(_root, "Library/ScriptAssemblies"));
            string version = File.ReadAllLines(Path.Combine(_root, "ProjectSettings/ProjectVersion.txt")).First(line => line.StartsWith("m_EditorVersion:")).Split(':')[1].Trim();
            string managed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity/Hub/Editor", version, "Editor/Data/Managed");
            if (Directory.Exists(managed)) foreach (string file in Directory.EnumerateFiles(managed, "*.dll", SearchOption.AllDirectories)) _assemblies.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            string buildTools = Path.Combine(Path.GetDirectoryName(managed), "Tools/BuildPipeline");
            if (Directory.Exists(buildTools)) foreach (string file in Directory.EnumerateFiles(buildTools, "*.dll", SearchOption.AllDirectories)) _assemblies.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            string packages = Path.Combine(_root, "Library/PackageCache");
            if (Directory.Exists(packages)) foreach (string file in Directory.EnumerateFiles(packages, "*.dll", SearchOption.AllDirectories)) _assemblies.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            _actions = TacticalCardDirectoryProjection.Actions(_root).Select(a => JsonSerializer.SerializeToElement(a)).ToArray();
            _actionFingerprint = TacticalCardQuery.Hash(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_actions)));
            _context = new TacticalCardAssemblyLoadContext(_runtimePath, _assemblies, "Newtonsoft.Json");
            _runtime = _context.MainAssembly;
            _runtimeHash = _context.RuntimeHash;
        }
        private void AddDirectory(string path)
        { foreach (string file in Directory.GetFiles(path, "*.dll")) _assemblies[Path.GetFileNameWithoutExtension(file)] = file; }
        private Type Type(string name) => _runtime.GetType("MxFramework.Game.CharacterTest." + name, true);
        private object Call(Type type, string method, params object[] args) => type.GetMethods(Members).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
        internal object Project(TacticalCardQueryRecord record, string locale)
        {
            if (record.TacticalSource.ValueKind != JsonValueKind.Object) return new { status = "source-unavailable", message = record.SourceIssue };
            object source = ReadSource(record);
            var projection = Type("CharacterTestTacticalCardPresentationProjection");
            object document = Call(projection, "Document", record.RecordId ?? record.StableId, record.Title, source);
            object data = Activator.CreateInstance(Type("CharacterTestCardUiPreviewData"), true);
            Set(data, "Id", record.RecordId ?? record.StableId); Set(data, "Name", record.Title);
            var metadata = record.SourceLayer == "disk-author" ? record.Raw : TacticalCardQuery.Field(record.Raw, "metadata");
            Set(data, "IconKey", TacticalCardQuery.Text(metadata, "iconKey"));
            object inputs = Call(Type("CharacterTestTacticalCardInputs"), "Unbound", 1);
            var names = new Dictionary<string, string>(); var issues = new List<string>();
            foreach (var pair in record.Bindings)
            {
                string numeric = pair.Value.Split('.', ':').Last();
                var exact = _actions.Where(a => TacticalCardQuery.Text(a, "id") == pair.Value).ToArray();
                var matches = (exact.Length > 0 ? exact : _actions.Where(a => TacticalCardQuery.Text(a, "actionId") == numeric))
                    .Select(a => TacticalCardQuery.Text(a, "name")).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
                if (matches.Length == 1) names[(Convert.ToBoolean(Get(source, "IsLegacy")) ? "preview-legacy/" : "") + pair.Key] = matches[0];
                else issues.Add("Action name unresolved or ambiguous at " + pair.Key + ": " + pair.Value);
            }
            string localePath = Path.Combine(_root, "Assets/StreamingAssets/MxFramework/Localization/character_test.tsv");
            object localization = Call(Type("CharacterTestLocalizationRuntime"), "Create", localePath, null);
            object provider = localization.GetType().GetProperty("TextProvider").GetValue(localization);
            var setLocale = provider.GetType().GetMethod("SetLocale");
            setLocale.Invoke(provider, new[] { Activator.CreateInstance(setLocale.GetParameters()[0].ParameterType, new object[] { locale }) });
            string resolvedLocale = Get(provider, "CurrentLocale")?.ToString();
            // Spatial preparation needs a role's measured geometry. Do not trigger measurements, fabricate inputs,
            // or label this read-only disk projection executable. The normal unprepared path still resolves names.
            double frameSeconds = 1d / Convert.ToInt32(Type("CharacterTestDeterministicUnits").GetField("DefaultTickRate", Members).GetRawConstantValue());
            object preparation = Get(source, "InteractionPreparation");
            if (preparation != null)
            {
                // Fixed preparations have no DSL events. Reuse the game's existing effect-based
                // description authority, rather than compiling their intentionally empty timeline.
                object description = Call(Type("CharacterTestTacticalDslDescription"), "GenerateInteractionPreparation", preparation);
                string text = RequireDescriptionText(Get(description, "Text") as string);
                var warnings = ((System.Collections.IEnumerable)Get(description, "Warnings")).Cast<string>().ToArray();
                Set(data, "Description", text); Set(data, "UnboundDescription", text);
                Set(data, "DescriptionDetails", Get(description, "Details") + "\n" + string.Join("\n", warnings));
                Set(data, "Hints", Get(description, "Hints")); Set(data, "Segments", Get(description, "Segments"));
                Set(data, "DescriptionFlow", Get(description, "DescriptionFlow"));
                if (data.GetType().GetField("UtilityFace", Members) != null)
                    Set(data, "UtilityFace", Get(description, "UtilityFace"));
                Set(data, "HasStructuredDescription", Get(description, "HasStructuredDescription")); Set(data, "FrameSeconds", frameSeconds);
                issues.AddRange(warnings);
                if (resolvedLocale != "zh-CN") issues.Add("Fixed preparation descriptions currently use the game's zh-CN source text; no translation was inferred.");
            }
            else Call(projection, "Project", source, document, data, inputs,
                "CLI disk projection: role measurement is not opened; use --layer live for already-prepared parameters.", names, provider, frameSeconds, issues);
            string sourceFolder = Path.Combine(_root, "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev");
            var sourceFiles = Directory.GetFiles(sourceFolder, "CharacterTestTactical*.cs").Concat(new[] {
                Path.Combine(sourceFolder, "CharacterTestRuntimeAiPlannerDevSceneController.CardUiPreview.cs"),
                Path.Combine(_root, "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/Runtime/CharacterTestUiText.cs"),
                Path.Combine(_root, "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/Runtime/CharacterTestLocalizationRuntime.cs") });
            var newer = sourceFiles.Where(File.Exists)
                .Where(path => File.GetLastWriteTimeUtc(path) > _context.RuntimeLastWriteUtc).ToArray();
            var segments = new List<object>();
            foreach (object segment in (System.Collections.IEnumerable)Get(data, "Segments"))
            {
                var values = new Dictionary<string, object> { ["text"] = Get(segment, "Text"), ["blockId"] = Get(segment, "BlockId") };
                foreach (string kind in new[] { "Action", "Arrival", "Retreat", "Orbit", "KeepDistance", "FacingStep", "WaitOpportunity", "FacingTarget" })
                    if (Get(segment, kind) is object hint) values[kind] = DisplayValues(hint);
                segments.Add(values);
            }
            object flow = Get(data, "DescriptionFlow");
            JsonElement? flowJson = flow == null ? null : TacticalCardQuery.Parse(flow.GetType().GetMethod("ToJson", Members).Invoke(flow, null).ToString());
            object utility = Get(data, "UtilityFace");
            JsonElement? utilityJson = utility == null ? null : TacticalCardQuery.Parse(utility.GetType().GetMethod("ToJson", Members).Invoke(utility, null).ToString());
            return new { status = newer.Length == 0 ? "generated-unmeasured" : "generated-assembly-may-be-stale", description = Get(data, "Description"), descriptionFlow = flowJson,
                descriptionDetails = Get(data, "DescriptionDetails"), unboundDescription = Get(data, "UnboundDescription"), utilityFace = utilityJson,
                parameters = segments, parameterContract = "game-card-hints.v1", measured = false, executable = false,
                descriptionBasis = preparation == null ? "game-card-presentation-projection" : "game-fixed-interaction-preparation",
                sourceLayer = record.SourceLayer, runtimeAssembly = _runtimePath, moduleId = _runtime.ManifestModule.ModuleVersionId,
                assemblySnapshot = new { mode = "verified-memory-streams", fingerprint = _context.SnapshotHash,
                    assemblyCount = _context.AssemblyCount, capturedUtc = _context.CapturedUtc, runtimeHash = _runtimeHash },
                sourcesNewerThanRuntime = newer, locale = preparation == null ? resolvedLocale : "zh-CN", requestedLocale = locale,
                providerRevision = Get(provider, "Revision"), localeHash = TacticalCardQuery.Hash(File.ReadAllBytes(localePath)),
                fingerprint = TacticalCardQuery.Hash(System.Text.Encoding.UTF8.GetBytes(record.SourceFingerprint + JsonSerializer.Serialize(record.Bindings) + record.Owner + resolvedLocale
                    + TacticalCardQuery.Hash(File.ReadAllBytes(localePath)) + _runtimeHash + _context.SnapshotHash + _actionFingerprint)), diagnostics = issues };
        }
        internal static string RequireDescriptionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.Any(char.IsLetterOrDigit))
                throw new InvalidDataException("Game fixed preparation description is empty or contains only punctuation; no generated text was inferred.");
            return text;
        }
        // The game parser validates the saved source. Use its real payload and enum discriminants,
        // following TacticalDeployment.CreateDefinition; names and editor components are not types.
        internal object ProjectType(TacticalCardQueryRecord record) => ProjectType(ReadSource(record), record);
        private object ReadSource(TacticalCardQueryRecord record)
        {
            if (record.TacticalSource.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(record.SourceIssue ?? "Card tactical source unavailable.");
            var json = _context.LoadFromAssemblyName(new AssemblyName("Newtonsoft.Json")).GetType("Newtonsoft.Json.Linq.JObject", true);
            var token = json.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { record.TacticalSource.GetRawText() });
            return Call(Type("CharacterTestTacticalPublishedSource"), "Read", token);
        }
        private object ProjectType(object source, TacticalCardQueryRecord record)
        {
            var property = source.GetType().GetProperty("InteractionPreparation", Members)
                ?? throw new MissingMemberException("Runtime card type contract unavailable; upgrade the compiled runtime.");
            object preparation = property.GetValue(source);
            object kind = Enum.Parse(Type("TacticalCard.TacticalCardKind"), preparation == null ? "Action" : "Utility");
            object payloadKind = preparation == null ? null : Get(preparation, "Kind");
            if (preparation != null && (payloadKind == null || !payloadKind.GetType().IsEnum
                || !Enum.IsDefined(payloadKind.GetType(), payloadKind)))
                throw new InvalidDataException("Runtime utility payload discriminator unavailable.");
            return new { kind = kind.ToString(), kindValue = Convert.ToInt32(kind),
                utilityPayloadKind = payloadKind?.ToString(), utilityPayloadKindValue = payloadKind == null ? (int?)null : Convert.ToInt32(payloadKind),
                status = record.SourceLayer == "disk-published" ? "published-source" : "source-preview",
                basis = "game-validated-source: TacticalDeployment.CreateDefinition / InteractionPreparation.Kind",
                moduleId = _runtime.ManifestModule.ModuleVersionId };
        }
        private static object Get(object value, string name) => value.GetType().GetField(name, Members)?.GetValue(value) ?? value.GetType().GetProperty(name, Members)?.GetValue(value);
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, Members).SetValue(value, data);
        // Serialize the game's immutable display DTOs, including nested movement multipliers.
        // No text formatting or independent gameplay formulas are introduced here.
        private static object DisplayValues(object value)
        {
            var result = new Dictionary<string, object>();
            foreach (var property in value.GetType().GetProperties(Members).Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod != null && !p.GetMethod.IsStatic))
            {
                object item = property.GetValue(value);
                if (item == null || item is string || item is bool || item is int || item is float || item is double || item is long) result[property.Name] = item;
                else if (item.GetType().IsEnum) result[property.Name] = item.ToString();
                else if (property.Name == "Movement") result[property.Name] = DisplayValues(item);
            }
            return result;
        }
        public void Dispose() => _context.Dispose();
    }
}
