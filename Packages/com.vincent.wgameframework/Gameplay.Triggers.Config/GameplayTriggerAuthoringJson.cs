using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MxFramework.Gameplay.Triggers.Config
{
    /// <summary>Stable failure categories returned by the Trigger authoring JSON reader.</summary>
    public enum GameplayTriggerAuthoringJsonErrorCode
    {
        None = 0,
        EmptyInput = 1,
        InvalidJson = 2,
        RootMustBeObject = 3,
        DuplicateProperty = 4,
        UnknownProperty = 5,
        MissingProperty = 6,
        NullProperty = 7,
        InvalidValue = 8
    }

    /// <summary>First deterministic failure produced while reading Trigger authoring JSON.</summary>
    public readonly struct GameplayTriggerAuthoringJsonDiagnostic
    {
        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerAuthoringJsonDiagnostic(
            GameplayTriggerAuthoringJsonErrorCode code,
            string path,
            string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <remarks>NoAlloc.</remarks>
        public GameplayTriggerAuthoringJsonErrorCode Code { get; }

        /// <remarks>NoAlloc.</remarks>
        public string Path { get; }

        /// <remarks>NoAlloc.</remarks>
        public string Message { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool Succeeded => Code == GameplayTriggerAuthoringJsonErrorCode.None;
    }

    /// <summary>Strict canonical JSON boundary for immutable Trigger authoring documents.</summary>
    public static class GameplayTriggerAuthoringJson
    {
        private static readonly string[] RootProperties =
        {
            "schemaId",
            "schemaVersion",
            "sources",
            "conditions",
            "definitions",
            "bindings"
        };

        private static readonly string[] SourceProperties =
        {
            "sourceDefinitionId",
            "sourceKind",
            "signalOrdering",
            "thresholdDirection",
            "enterThreshold",
            "exitThreshold",
            "initialSamplePolicy",
            "departurePolicy"
        };

        private static readonly string[] Fix64Properties =
        {
            "kind",
            "rawValue",
            "invariantDecimal"
        };

        private static readonly string[] ConditionProperties =
        {
            "conditionId",
            "root"
        };

        private static readonly string[] ConditionNodeProperties =
        {
            "operation",
            "operand",
            "comparison",
            "children"
        };

        private static readonly string[] DefinitionProperties =
        {
            "definitionId",
            "definitionVersion",
            "sourceDefinitionId",
            "routes"
        };

        private static readonly string[] RouteProperties =
        {
            "routeId",
            "occurrenceMask",
            "conditionId",
            "actions",
            "stateScope",
            "maxActivations",
            "cooldownFrames",
            "presenceLimit",
            "stayIntervalFrames",
            "executionOrder",
            "exclusiveGroupId",
            "winnerPriority",
            "arbitrationScope",
            "maxStateKeys",
            "allowRepeatingCycle",
            "minIntervalFrames"
        };

        private static readonly string[] ActionProperties =
        {
            "actionKindId",
            "targetSelector",
            "targetSlotId",
            "payload0",
            "payload1",
            "payload2"
        };

        private static readonly string[] BindingProperties =
        {
            "bindingId",
            "definitionId",
            "sourceBindingId",
            "ownerIndex",
            "ownerGeneration",
            "initiallyEnabled",
            "explicitTargets"
        };

        private static readonly string[] TargetProperties =
        {
            "slotId",
            "domainId",
            "value",
            "generation"
        };

        private static readonly JsonSerializerSettings ReaderSettings =
            CreateReaderSettings();

        /// <summary>Serializes a canonical copy without mutating caller-owned arrays.</summary>
        /// <remarks>AllocByDesign. Copies sortable arrays and builds a canonical JSON token tree.</remarks>
        public static string Serialize(
            GameplayTriggerAuthoringDocument document,
            bool indented = true)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            GameplayTriggerAuthoringDocument canonical = CreateCanonicalCopy(document);
            JObject root = WriteDocument(canonical);
            return root.ToString(indented ? Formatting.Indented : Formatting.None);
        }

        /// <summary>Strictly reads one complete canonicalizable Trigger authoring document.</summary>
        /// <remarks>AllocByDesign. Parses, validates shape, materializes immutable DTOs and canonicalizes arrays.</remarks>
        public static bool TryDeserialize(
            string json,
            out GameplayTriggerAuthoringDocument document,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            document = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.EmptyInput,
                    "$",
                    "Trigger authoring JSON is empty.");
                return false;
            }

            try
            {
                JToken token;
                using (var stringReader = new System.IO.StringReader(json))
                using (var reader = new JsonTextReader(stringReader)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal
                })
                {
                    token = JToken.Load(
                        reader,
                        new JsonLoadSettings
                        {
                            CommentHandling = CommentHandling.Load,
                            DuplicatePropertyNameHandling =
                                DuplicatePropertyNameHandling.Error,
                            LineInfoHandling = LineInfoHandling.Load
                        });
                    if (reader.Read())
                    {
                        diagnostic = Failure(
                            GameplayTriggerAuthoringJsonErrorCode.InvalidJson,
                            "$",
                            "Trigger authoring JSON contains trailing content.");
                        return false;
                    }
                }

                if (ContainsComment(token))
                {
                    diagnostic = Failure(
                        GameplayTriggerAuthoringJsonErrorCode.InvalidJson,
                        "$",
                        "Comments are not allowed in Trigger authoring JSON.");
                    return false;
                }

                if (token is not JObject root)
                {
                    diagnostic = Failure(
                        GameplayTriggerAuthoringJsonErrorCode.RootMustBeObject,
                        "$",
                        "Trigger authoring JSON root must be an object.");
                    return false;
                }

                if (!ValidateShape(root, out diagnostic))
                    return false;

                GameplayTriggerAuthoringDocument parsed =
                    root.ToObject<GameplayTriggerAuthoringDocument>(
                        JsonSerializer.Create(ReaderSettings));
                if (parsed == null)
                {
                    diagnostic = Failure(
                        GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                        "$",
                        "Trigger authoring JSON could not be materialized.");
                    return false;
                }

                document = CreateCanonicalCopy(parsed);
                diagnostic = default;
                return true;
            }
            catch (JsonReaderException exception)
            {
                GameplayTriggerAuthoringJsonErrorCode code =
                    exception.Message.IndexOf(
                        "already exists",
                        StringComparison.OrdinalIgnoreCase) >= 0
                        ? GameplayTriggerAuthoringJsonErrorCode.DuplicateProperty
                        : GameplayTriggerAuthoringJsonErrorCode.InvalidJson;
                diagnostic = Failure(code, "$", exception.Message);
                return false;
            }
            catch (JsonSerializationException exception)
            {
                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    string.IsNullOrWhiteSpace(exception.Path)
                        ? "$"
                        : "$." + exception.Path,
                    exception.Message);
                return false;
            }
            catch (ArgumentException exception)
            {
                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    "$",
                    exception.Message);
                return false;
            }
        }

        private static bool ValidateShape(
            JObject root,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            if (!ValidateObject(root, "$", RootProperties, out diagnostic))
                return false;

            if (!ValidateRows(
                    root["sources"],
                    "$.sources",
                    SourceProperties,
                    ValidateSource,
                    out diagnostic)
                || !ValidateRows(
                    root["conditions"],
                    "$.conditions",
                    ConditionProperties,
                    ValidateCondition,
                    out diagnostic)
                || !ValidateRows(
                    root["definitions"],
                    "$.definitions",
                    DefinitionProperties,
                    ValidateDefinition,
                    out diagnostic)
                || !ValidateRows(
                    root["bindings"],
                    "$.bindings",
                    BindingProperties,
                    ValidateBinding,
                    out diagnostic))
            {
                return false;
            }

            diagnostic = default;
            return true;
        }

        private static bool ValidateSource(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return ValidateObject(row, path, SourceProperties, out diagnostic)
                && ValidateRequiredObject(
                    row["enterThreshold"],
                    path + ".enterThreshold",
                    Fix64Properties,
                    out diagnostic)
                && ValidateRequiredObject(
                    row["exitThreshold"],
                    path + ".exitThreshold",
                    Fix64Properties,
                    out diagnostic);
        }

        private static bool ValidateCondition(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return ValidateObject(row, path, ConditionProperties, out diagnostic)
                && ValidateConditionNode(row["root"], path + ".root", 0, out diagnostic);
        }

        private static bool ValidateConditionNode(
            JToken token,
            string path,
            int depth,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            if (depth > 256)
            {
                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    path,
                    "Condition JSON nesting exceeds the authoring reader limit.");
                return false;
            }

            if (token is not JObject node)
            {
                diagnostic = Failure(
                    token == null || token.Type == JTokenType.Null
                        ? GameplayTriggerAuthoringJsonErrorCode.NullProperty
                        : GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    path,
                    "Condition root and children must be objects.");
                return false;
            }
            if (!ValidateObject(
                    node,
                    path,
                    ConditionNodeProperties,
                    out diagnostic))
            {
                return false;
            }

            if (node["children"] is not JArray children)
            {
                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    path + ".children",
                    "Condition children must be an array.");
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (!ValidateConditionNode(
                        children[i],
                        path + ".children[" + i + "]",
                        depth + 1,
                        out diagnostic))
                {
                    return false;
                }
            }

            diagnostic = default;
            return true;
        }

        private static bool ValidateDefinition(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return ValidateObject(row, path, DefinitionProperties, out diagnostic)
                && ValidateRows(
                    row["routes"],
                    path + ".routes",
                    RouteProperties,
                    ValidateRoute,
                    out diagnostic);
        }

        private static bool ValidateRoute(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return ValidateObject(row, path, RouteProperties, out diagnostic)
                && ValidateRows(
                    row["actions"],
                    path + ".actions",
                    ActionProperties,
                    null,
                    out diagnostic);
        }

        private static bool ValidateBinding(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            return ValidateObject(row, path, BindingProperties, out diagnostic)
                && ValidateRows(
                    row["explicitTargets"],
                    path + ".explicitTargets",
                    TargetProperties,
                    null,
                    out diagnostic);
        }

        private delegate bool RowValidator(
            JObject row,
            string path,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic);

        private static bool ValidateRows(
            JToken token,
            string path,
            string[] properties,
            RowValidator additionalValidation,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            if (token is not JArray rows)
            {
                diagnostic = Failure(
                    token == null || token.Type == JTokenType.Null
                        ? GameplayTriggerAuthoringJsonErrorCode.NullProperty
                        : GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    path,
                    "Required collection must be an array.");
                return false;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                string rowPath = path + "[" + i + "]";
                if (rows[i] is not JObject row)
                {
                    diagnostic = Failure(
                        rows[i] == null || rows[i].Type == JTokenType.Null
                            ? GameplayTriggerAuthoringJsonErrorCode.NullProperty
                            : GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                        rowPath,
                        "Collection rows must be objects.");
                    return false;
                }

                if (!ValidateObject(row, rowPath, properties, out diagnostic)
                    || (additionalValidation != null
                        && !additionalValidation(row, rowPath, out diagnostic)))
                {
                    return false;
                }
            }

            diagnostic = default;
            return true;
        }

        private static bool ValidateRequiredObject(
            JToken token,
            string path,
            string[] properties,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            if (token is not JObject value)
            {
                diagnostic = Failure(
                    token == null || token.Type == JTokenType.Null
                        ? GameplayTriggerAuthoringJsonErrorCode.NullProperty
                        : GameplayTriggerAuthoringJsonErrorCode.InvalidValue,
                    path,
                    "Required value must be an object.");
                return false;
            }

            return ValidateObject(value, path, properties, out diagnostic);
        }

        private static bool ValidateObject(
            JObject value,
            string path,
            string[] requiredProperties,
            out GameplayTriggerAuthoringJsonDiagnostic diagnostic)
        {
            for (int i = 0; i < requiredProperties.Length; i++)
            {
                string propertyName = requiredProperties[i];
                if (!value.TryGetValue(
                        propertyName,
                        StringComparison.Ordinal,
                        out JToken token))
                {
                    diagnostic = Failure(
                        GameplayTriggerAuthoringJsonErrorCode.MissingProperty,
                        path + "." + propertyName,
                        "Required property is missing.");
                    return false;
                }

                if (token == null || token.Type == JTokenType.Null)
                {
                    diagnostic = Failure(
                        GameplayTriggerAuthoringJsonErrorCode.NullProperty,
                        path + "." + propertyName,
                        "Required property cannot be null.");
                    return false;
                }
            }

            foreach (JProperty property in value.Properties())
            {
                bool known = false;
                for (int i = 0; i < requiredProperties.Length; i++)
                {
                    if (string.Equals(
                            property.Name,
                            requiredProperties[i],
                            StringComparison.Ordinal))
                    {
                        known = true;
                        break;
                    }
                }

                if (known)
                    continue;

                diagnostic = Failure(
                    GameplayTriggerAuthoringJsonErrorCode.UnknownProperty,
                    path + "." + property.Name,
                    "Unknown property is not allowed.");
                return false;
            }

            diagnostic = default;
            return true;
        }

        private static GameplayTriggerAuthoringDocument CreateCanonicalCopy(
            GameplayTriggerAuthoringDocument document)
        {
            GameplayTriggerSourceAuthoring[] sources =
                CopyAndSort(document.Sources, CompareSources, "sources");
            GameplayTriggerConditionAuthoring[] conditions =
                CopyAndSort(document.Conditions, CompareConditions, "conditions");
            GameplayTriggerDefinitionAuthoring[] definitions =
                CopyAndSort(document.Definitions, CompareDefinitions, "definitions");
            GameplayTriggerBindingAuthoring[] bindings =
                CopyAndSort(document.Bindings, CompareBindings, "bindings");

            for (int i = 0; i < definitions.Length; i++)
            {
                GameplayTriggerDefinitionAuthoring definition = definitions[i]
                    ?? throw new ArgumentException("definitions contains a null row.");
                GameplayTriggerRouteAuthoring[] routes =
                    CopyAndSort(definition.Routes, CompareRoutes, "definitions.routes");
                definitions[i] = new GameplayTriggerDefinitionAuthoring(
                    definition.DefinitionId,
                    definition.DefinitionVersion,
                    definition.SourceDefinitionId,
                    routes);
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                GameplayTriggerBindingAuthoring binding = bindings[i]
                    ?? throw new ArgumentException("bindings contains a null row.");
                GameplayTriggerBindingTargetAuthoring[] targets =
                    CopyAndSort(
                        binding.ExplicitTargets,
                        CompareTargets,
                        "bindings.explicitTargets");
                bindings[i] = new GameplayTriggerBindingAuthoring(
                    binding.BindingId,
                    binding.DefinitionId,
                    binding.SourceBindingId,
                    binding.OwnerIndex,
                    binding.OwnerGeneration,
                    binding.InitiallyEnabled,
                    targets);
            }

            return new GameplayTriggerAuthoringDocument(
                document.SchemaId,
                document.SchemaVersion,
                sources,
                conditions,
                definitions,
                bindings);
        }

        private static T[] CopyAndSort<T>(
            T[] rows,
            Comparison<T> comparison,
            string collectionName)
        {
            if (rows == null)
                throw new ArgumentException(collectionName + " cannot be null.");

            T[] copy = (T[])rows.Clone();
            Array.Sort(copy, comparison);
            return copy;
        }

        private static int CompareSources(
            GameplayTriggerSourceAuthoring left,
            GameplayTriggerSourceAuthoring right) =>
            left == null
                ? right == null ? 0 : -1
                : right == null
                    ? 1
                    : left.SourceDefinitionId.CompareTo(right.SourceDefinitionId);

        private static int CompareConditions(
            GameplayTriggerConditionAuthoring left,
            GameplayTriggerConditionAuthoring right) =>
            left == null
                ? right == null ? 0 : -1
                : right == null
                    ? 1
                    : left.ConditionId.CompareTo(right.ConditionId);

        private static int CompareDefinitions(
            GameplayTriggerDefinitionAuthoring left,
            GameplayTriggerDefinitionAuthoring right) =>
            left == null
                ? right == null ? 0 : -1
                : right == null
                    ? 1
                    : left.DefinitionId.CompareTo(right.DefinitionId);

        private static int CompareRoutes(
            GameplayTriggerRouteAuthoring left,
            GameplayTriggerRouteAuthoring right) =>
            left == null
                ? right == null ? 0 : -1
                : right == null
                    ? 1
                    : left.RouteId.CompareTo(right.RouteId);

        private static int CompareBindings(
            GameplayTriggerBindingAuthoring left,
            GameplayTriggerBindingAuthoring right) =>
            left == null
                ? right == null ? 0 : -1
                : right == null
                    ? 1
                    : left.BindingId.CompareTo(right.BindingId);

        private static int CompareTargets(
            GameplayTriggerBindingTargetAuthoring left,
            GameplayTriggerBindingTargetAuthoring right) =>
            left.SlotId.CompareTo(right.SlotId);

        private static JObject WriteDocument(GameplayTriggerAuthoringDocument document)
        {
            var sources = new JArray();
            for (int i = 0; i < document.Sources.Length; i++)
                sources.Add(WriteSource(RequireRow(document.Sources[i], "sources", i)));

            var conditions = new JArray();
            for (int i = 0; i < document.Conditions.Length; i++)
                conditions.Add(WriteCondition(RequireRow(document.Conditions[i], "conditions", i)));

            var definitions = new JArray();
            for (int i = 0; i < document.Definitions.Length; i++)
                definitions.Add(WriteDefinition(RequireRow(document.Definitions[i], "definitions", i)));

            var bindings = new JArray();
            for (int i = 0; i < document.Bindings.Length; i++)
                bindings.Add(WriteBinding(RequireRow(document.Bindings[i], "bindings", i)));

            return new JObject
            {
                ["schemaId"] = document.SchemaId ?? string.Empty,
                ["schemaVersion"] = document.SchemaVersion,
                ["sources"] = sources,
                ["conditions"] = conditions,
                ["definitions"] = definitions,
                ["bindings"] = bindings
            };
        }

        private static JObject WriteSource(GameplayTriggerSourceAuthoring source)
        {
            return new JObject
            {
                ["sourceDefinitionId"] = source.SourceDefinitionId,
                ["sourceKind"] = source.SourceKind.ToString(),
                ["signalOrdering"] = source.SignalOrdering.ToString(),
                ["thresholdDirection"] = source.ThresholdDirection.ToString(),
                ["enterThreshold"] = WriteFix64(source.EnterThreshold),
                ["exitThreshold"] = WriteFix64(source.ExitThreshold),
                ["initialSamplePolicy"] = source.InitialSamplePolicy.ToString(),
                ["departurePolicy"] = source.DeparturePolicy.ToString()
            };
        }

        private static JObject WriteFix64(GameplayTriggerFix64Authoring value)
        {
            return new JObject
            {
                ["kind"] = value.Kind.ToString(),
                ["rawValue"] = value.RawValue,
                ["invariantDecimal"] = value.InvariantDecimal ?? string.Empty
            };
        }

        private static JObject WriteCondition(GameplayTriggerConditionAuthoring condition)
        {
            return new JObject
            {
                ["conditionId"] = condition.ConditionId,
                ["root"] = WriteConditionNode(condition.Root, "$.conditions.root", 0)
            };
        }

        private static JObject WriteConditionNode(
            GameplayTriggerConditionNodeAuthoring node,
            string path,
            int depth)
        {
            if (node == null)
                throw new ArgumentException(path + " cannot be null.");
            if (depth > 256)
                throw new ArgumentException(path + " exceeds the authoring writer depth limit.");

            GameplayTriggerConditionNodeAuthoring[] childRows =
                node.Children
                ?? throw new ArgumentException(path + ".children cannot be null.");
            var children = new JArray();
            for (int i = 0; i < childRows.Length; i++)
            {
                children.Add(
                    WriteConditionNode(
                        childRows[i],
                        path + ".children[" + i + "]",
                        depth + 1));
            }

            return new JObject
            {
                ["operation"] = node.Operation.ToString(),
                ["operand"] = node.Operand,
                ["comparison"] = node.Comparison.ToString(),
                ["children"] = children
            };
        }

        private static JObject WriteDefinition(
            GameplayTriggerDefinitionAuthoring definition)
        {
            GameplayTriggerRouteAuthoring[] routeRows =
                definition.Routes
                ?? throw new ArgumentException("definition routes cannot be null.");
            var routes = new JArray();
            for (int i = 0; i < routeRows.Length; i++)
                routes.Add(WriteRoute(RequireRow(routeRows[i], "routes", i)));

            return new JObject
            {
                ["definitionId"] = definition.DefinitionId,
                ["definitionVersion"] = definition.DefinitionVersion,
                ["sourceDefinitionId"] = definition.SourceDefinitionId,
                ["routes"] = routes
            };
        }

        private static JObject WriteRoute(GameplayTriggerRouteAuthoring route)
        {
            GameplayTriggerActionAuthoring[] actionRows =
                route.Actions
                ?? throw new ArgumentException("route actions cannot be null.");
            var actions = new JArray();
            for (int i = 0; i < actionRows.Length; i++)
            {
                GameplayTriggerActionAuthoring action = actionRows[i];
                actions.Add(
                    new JObject
                    {
                        ["actionKindId"] = action.ActionKindId,
                        ["targetSelector"] = action.TargetSelector.ToString(),
                        ["targetSlotId"] = action.TargetSlotId,
                        ["payload0"] = action.Payload0,
                        ["payload1"] = action.Payload1,
                        ["payload2"] = action.Payload2
                    });
            }

            return new JObject
            {
                ["routeId"] = route.RouteId,
                ["occurrenceMask"] = route.OccurrenceMask.ToString(),
                ["conditionId"] = route.ConditionId,
                ["actions"] = actions,
                ["stateScope"] = route.StateScope.ToString(),
                ["maxActivations"] = route.MaxActivations,
                ["cooldownFrames"] = route.CooldownFrames,
                ["presenceLimit"] = route.PresenceLimit.ToString(),
                ["stayIntervalFrames"] = route.StayIntervalFrames,
                ["executionOrder"] = route.ExecutionOrder,
                ["exclusiveGroupId"] = route.ExclusiveGroupId,
                ["winnerPriority"] = route.WinnerPriority,
                ["arbitrationScope"] = route.ArbitrationScope.ToString(),
                ["maxStateKeys"] = route.MaxStateKeys,
                ["allowRepeatingCycle"] = route.AllowRepeatingCycle,
                ["minIntervalFrames"] = route.MinIntervalFrames
            };
        }

        private static JObject WriteBinding(GameplayTriggerBindingAuthoring binding)
        {
            GameplayTriggerBindingTargetAuthoring[] targetRows =
                binding.ExplicitTargets
                ?? throw new ArgumentException("binding explicit targets cannot be null.");
            var targets = new JArray();
            for (int i = 0; i < targetRows.Length; i++)
            {
                GameplayTriggerBindingTargetAuthoring target = targetRows[i];
                targets.Add(
                    new JObject
                    {
                        ["slotId"] = target.SlotId,
                        ["domainId"] = target.DomainId,
                        ["value"] = target.Value,
                        ["generation"] = target.Generation
                    });
            }

            return new JObject
            {
                ["bindingId"] = binding.BindingId,
                ["definitionId"] = binding.DefinitionId,
                ["sourceBindingId"] = binding.SourceBindingId,
                ["ownerIndex"] = binding.OwnerIndex,
                ["ownerGeneration"] = binding.OwnerGeneration,
                ["initiallyEnabled"] = binding.InitiallyEnabled,
                ["explicitTargets"] = targets
            };
        }

        private static T RequireRow<T>(T row, string collectionName, int index)
            where T : class
        {
            return row
                ?? throw new ArgumentException(
                    collectionName + "[" + index + "] cannot be null.");
        }

        private static bool ContainsComment(JToken token)
        {
            if (token == null)
                return false;
            if (token.Type == JTokenType.Comment)
                return true;

            foreach (JToken child in token.Children())
            {
                if (ContainsComment(child))
                    return true;
            }

            return false;
        }

        private static JsonSerializerSettings CreateReaderSettings()
        {
            var converter = new StringEnumConverter
            {
                AllowIntegerValues = false
            };
            var settings = new JsonSerializerSettings
            {
                Culture = System.Globalization.CultureInfo.InvariantCulture,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            settings.Converters.Add(converter);
            return settings;
        }

        private static GameplayTriggerAuthoringJsonDiagnostic Failure(
            GameplayTriggerAuthoringJsonErrorCode code,
            string path,
            string message) =>
            new GameplayTriggerAuthoringJsonDiagnostic(code, path, message);
    }
}
