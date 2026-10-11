using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Runtime
{
    public static class RuntimeSaveStateJson
    {
        public const int MaximumJsonDepth = 64;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        public static string SaveToJson(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                throw new ArgumentNullException(nameof(saveState));
            }

            return JsonConvert.SerializeObject(saveState, Settings);
        }

        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromJson(string json)
        {
            return LoadFromJson(json, RuntimeSaveState.CurrentSchemaVersion, null);
        }

        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromJson(
            string json,
            int targetSchemaVersion,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json is null or empty.",
                    -1,
                    targetSchemaVersion));
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json is not a valid object: " + ex.Message,
                    -1,
                    targetSchemaVersion,
                    ex));
            }

            return LoadFromRoot(root, targetSchemaVersion, migrationPipeline);
        }

        /// <summary>Serializes one Runtime SaveState into bounded UTF-8 JSON bytes.</summary>
        /// <remarks>
        /// AllocByDesign. The returned byte array is newly owned by the caller. The maximum depth must be in
        /// the inclusive range 1..64; Runtime persistence uses 16.
        /// </remarks>
        public static RuntimeSaveStateResult<byte[]> SaveToUtf8(
            RuntimeSaveState saveState,
            int maxByteCount,
            int maxDepth)
        {
            ValidateBounds(maxByteCount, maxDepth);
            if (saveState == null)
            {
                throw new ArgumentNullException(nameof(saveState));
            }

            try
            {
                using (var output = new MemoryStream(Math.Min(maxByteCount, 4096)))
                using (var limitedOutput = new ByteLimitedWriteStream(output, maxByteCount))
                using (var textWriter = new StreamWriter(limitedOutput, StrictUtf8, 1024, true))
                using (var jsonWriter = new DepthLimitedJsonTextWriter(textWriter, maxDepth))
                {
                    jsonWriter.Formatting = Settings.Formatting;
                    JsonSerializer.Create(Settings).Serialize(jsonWriter, saveState);
                    jsonWriter.Flush();
                    textWriter.Flush();
                    return RuntimeSaveStateResult<byte[]>.Succeeded(output.ToArray());
                }
            }
            catch (JsonOutputLimitException ex)
            {
                return LimitFailure<byte[]>(ex.Message);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<byte[]>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state could not be serialized as UTF-8 JSON: " + ex.Message,
                    saveState.SchemaVersion,
                    saveState.SchemaVersion,
                    ex));
            }
        }

        /// <summary>Strictly parses bounded UTF-8 bytes as one Runtime SaveState object.</summary>
        /// <remarks>
        /// AllocByDesign. Rejects invalid UTF-8, duplicate properties, trailing tokens, non-object roots,
        /// byte-limit overflow, and structural depth overflow. Custom-state PayloadJson remains an opaque string.
        /// </remarks>
        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromUtf8(
            byte[] utf8Json,
            int maxByteCount,
            int maxDepth)
        {
            return LoadFromUtf8(
                utf8Json,
                maxByteCount,
                maxDepth,
                RuntimeSaveState.CurrentSchemaVersion,
                null);
        }

        /// <summary>Strictly parses and optionally migrates bounded UTF-8 Runtime SaveState JSON.</summary>
        /// <remarks>AllocByDesign. The caller retains ownership of the input byte array.</remarks>
        public static RuntimeSaveStateResult<RuntimeSaveState> LoadFromUtf8(
            byte[] utf8Json,
            int maxByteCount,
            int maxDepth,
            int targetSchemaVersion,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {
            ValidateBounds(maxByteCount, maxDepth);
            if (utf8Json == null || utf8Json.Length == 0)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save-state UTF-8 JSON is null or empty.",
                    -1,
                    targetSchemaVersion));
            }

            if (utf8Json.Length > maxByteCount)
            {
                return LimitFailure<RuntimeSaveState>("Save-state UTF-8 JSON exceeds the configured byte limit.");
            }

            string json;
            try
            {
                json = StrictUtf8.GetString(utf8Json);
            }
            catch (DecoderFallbackException ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save-state JSON is not valid UTF-8.",
                    -1,
                    targetSchemaVersion,
                    ex));
            }

            RuntimeSaveStateError depthError = ValidateStructuralDepth(json, maxDepth);
            if (depthError != null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(depthError);
            }

            JObject root;
            try
            {
                using (var textReader = new StringReader(json))
                using (var reader = new JsonTextReader(textReader))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.MaxDepth = maxDepth;
                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    {
                        return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                            RuntimeSaveStateErrorCode.InvalidDocument,
                            "$",
                            "Save-state JSON root must be one object.",
                            -1,
                            targetSchemaVersion));
                    }

                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load
                    });

                    if (reader.Read())
                    {
                        return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                            RuntimeSaveStateErrorCode.InvalidDocument,
                            "$",
                            "Save-state JSON contains a trailing token.",
                            -1,
                            targetSchemaVersion));
                    }
                }
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save-state UTF-8 JSON is not one strict object: " + ex.Message,
                    -1,
                    targetSchemaVersion,
                    ex));
            }

            return LoadFromRoot(root, targetSchemaVersion, migrationPipeline);
        }

        private static RuntimeSaveStateResult<RuntimeSaveState> LoadFromRoot(
            JObject root,
            int targetSchemaVersion,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {

            JToken schemaVersionToken = root["schemaVersion"];
            if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state document is missing an integer schemaVersion.",
                    -1,
                    targetSchemaVersion));
            }

            int schemaVersion;
            try
            {
                schemaVersion = schemaVersionToken.Value<int>();
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state schemaVersion is outside the supported integer range.",
                    -1,
                    targetSchemaVersion,
                    ex));
            }
            if (schemaVersion > targetSchemaVersion)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.schemaVersion",
                    "Save state schema version is newer than this runtime can load.",
                    schemaVersion,
                    targetSchemaVersion));
            }

            RuntimeSaveState saveState;
            try
            {
                saveState = root.ToObject<RuntimeSaveState>(JsonSerializer.Create(Settings));
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json could not be deserialized: " + ex.Message,
                    schemaVersion,
                    targetSchemaVersion,
                    ex));
            }

            RuntimeSaveStateResult<RuntimeSaveState> validation = Validate(saveState, targetSchemaVersion);
            if (!validation.Success)
            {
                return validation;
            }

            if (saveState.SchemaVersion == targetSchemaVersion)
            {
                return validation;
            }

            if (migrationPipeline == null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.MissingMigration,
                    "$.schemaVersion",
                    "Save state document requires migration, but no migration pipeline was provided.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            return migrationPipeline.Migrate(saveState, targetSchemaVersion);
        }

        private static void ValidateBounds(int maxByteCount, int maxDepth)
        {
            if (maxByteCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxByteCount));
            }

            if (maxDepth <= 0 || maxDepth > MaximumJsonDepth)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            }
        }

        private static RuntimeSaveStateError ValidateStructuralDepth(string json, int maxDepth)
        {
            int index = 0;
            bool depthExceeded = false;
            SkipJsonWhitespace(json, ref index);
            if (!TryReadJsonValue(json, ref index, 0, maxDepth, ref depthExceeded))
            {
                return depthExceeded
                    ? new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.DocumentLimitExceeded,
                        "$",
                        "Save-state JSON exceeds the configured structural depth limit.")
                    : StrictSyntaxFailure();
            }

            SkipJsonWhitespace(json, ref index);
            return index == json.Length ? null : StrictSyntaxFailure();
        }

        private static bool TryReadJsonValue(
            string json,
            ref int index,
            int depth,
            int maxDepth,
            ref bool depthExceeded)
        {
            SkipJsonWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return false;
            }

            char character = json[index];
            if (character == '{')
            {
                return TryReadJsonObject(json, ref index, depth + 1, maxDepth, ref depthExceeded);
            }

            if (character == '[')
            {
                return TryReadJsonArray(json, ref index, depth + 1, maxDepth, ref depthExceeded);
            }

            if (character == '"')
            {
                return TryReadJsonString(json, ref index);
            }

            if (character == 't')
            {
                return TryReadJsonLiteral(json, ref index, "true");
            }

            if (character == 'f')
            {
                return TryReadJsonLiteral(json, ref index, "false");
            }

            if (character == 'n')
            {
                return TryReadJsonLiteral(json, ref index, "null");
            }

            return character == '-' || IsJsonDigit(character)
                ? TryReadJsonNumber(json, ref index)
                : false;
        }

        private static bool TryReadJsonObject(
            string json,
            ref int index,
            int depth,
            int maxDepth,
            ref bool depthExceeded)
        {
            if (depth > maxDepth)
            {
                depthExceeded = true;
                return false;
            }

            index++;
            SkipJsonWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TryReadJsonString(json, ref index))
                {
                    return false;
                }

                SkipJsonWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':') ||
                    !TryReadJsonValue(json, ref index, depth, maxDepth, ref depthExceeded))
                {
                    return false;
                }

                SkipJsonWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipJsonWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}')
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadJsonArray(
            string json,
            ref int index,
            int depth,
            int maxDepth,
            ref bool depthExceeded)
        {
            if (depth > maxDepth)
            {
                depthExceeded = true;
                return false;
            }

            index++;
            SkipJsonWhitespace(json, ref index);
            if (TryConsume(json, ref index, ']'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TryReadJsonValue(json, ref index, depth, maxDepth, ref depthExceeded))
                {
                    return false;
                }

                SkipJsonWhitespace(json, ref index);
                if (TryConsume(json, ref index, ']'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipJsonWhitespace(json, ref index);
                if (index >= json.Length || json[index] == ']')
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadJsonString(string json, ref int index)
        {
            if (!TryConsume(json, ref index, '"'))
            {
                return false;
            }

            while (index < json.Length)
            {
                char character = json[index++];
                if (character == '"')
                {
                    return true;
                }

                if (character < 0x20)
                {
                    return false;
                }

                if (character != '\\')
                {
                    continue;
                }

                if (index >= json.Length)
                {
                    return false;
                }

                char escape = json[index++];
                if (escape == 'u')
                {
                    if (index + 4 > json.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        if (!IsJsonHex(json[index++]))
                        {
                            return false;
                        }
                    }
                }
                else if (escape != '"' && escape != '\\' && escape != '/' &&
                    escape != 'b' && escape != 'f' && escape != 'n' && escape != 'r' && escape != 't')
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadJsonNumber(string json, ref int index)
        {
            if (TryConsume(json, ref index, '-') && index >= json.Length)
            {
                return false;
            }

            if (TryConsume(json, ref index, '0'))
            {
                if (index < json.Length && IsJsonDigit(json[index]))
                {
                    return false;
                }
            }
            else
            {
                if (index >= json.Length || json[index] < '1' || json[index] > '9')
                {
                    return false;
                }

                do
                {
                    index++;
                }
                while (index < json.Length && IsJsonDigit(json[index]));
            }

            if (TryConsume(json, ref index, '.'))
            {
                if (index >= json.Length || !IsJsonDigit(json[index]))
                {
                    return false;
                }

                do
                {
                    index++;
                }
                while (index < json.Length && IsJsonDigit(json[index]));
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                if (index >= json.Length || !IsJsonDigit(json[index]))
                {
                    return false;
                }

                do
                {
                    index++;
                }
                while (index < json.Length && IsJsonDigit(json[index]));
            }

            return true;
        }

        private static bool TryReadJsonLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length)
            {
                return false;
            }

            for (int i = 0; i < literal.Length; i++)
            {
                if (json[index + i] != literal[i])
                {
                    return false;
                }
            }

            index += literal.Length;
            return true;
        }

        private static void SkipJsonWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char character = json[index];
                if (character != ' ' && character != '\t' && character != '\r' && character != '\n')
                {
                    return;
                }

                index++;
            }
        }

        private static bool TryConsume(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private static bool IsJsonDigit(char character)
        {
            return character >= '0' && character <= '9';
        }

        private static bool IsJsonHex(char character)
        {
            return IsJsonDigit(character) ||
                (character >= 'a' && character <= 'f') ||
                (character >= 'A' && character <= 'F');
        }

        private static RuntimeSaveStateError StrictSyntaxFailure()
        {
            return new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                "$",
                "Save-state JSON does not use strict JSON syntax.");
        }

        private static RuntimeSaveStateResult<T> LimitFailure<T>(string message)
        {
            return RuntimeSaveStateResult<T>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.DocumentLimitExceeded,
                "$",
                message));
        }

        private sealed class DepthLimitedJsonTextWriter : JsonTextWriter
        {
            private readonly int _maxDepth;

            public DepthLimitedJsonTextWriter(TextWriter textWriter, int maxDepth)
                : base(textWriter)
            {
                _maxDepth = maxDepth;
                CloseOutput = false;
            }

            public override void WriteStartObject()
            {
                EnsureDepthAvailable();
                base.WriteStartObject();
            }

            public override void WriteStartArray()
            {
                EnsureDepthAvailable();
                base.WriteStartArray();
            }

            private void EnsureDepthAvailable()
            {
                if (Top >= _maxDepth)
                {
                    throw new JsonOutputLimitException(
                        "Save-state JSON exceeds the configured structural depth limit.");
                }
            }
        }

        private sealed class ByteLimitedWriteStream : Stream
        {
            private readonly Stream _output;
            private readonly int _maxByteCount;
            private int _writtenByteCount;

            public ByteLimitedWriteStream(Stream output, int maxByteCount)
            {
                _output = output;
                _maxByteCount = maxByteCount;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _writtenByteCount;

            public override long Position
            {
                get => _writtenByteCount;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                _output.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count > _maxByteCount - _writtenByteCount)
                {
                    throw new JsonOutputLimitException(
                        "Save-state UTF-8 JSON exceeds the configured byte limit.");
                }

                _output.Write(buffer, offset, count);
                _writtenByteCount += count;
            }
        }

        private sealed class JsonOutputLimitException : Exception
        {
            public JsonOutputLimitException(string message)
                : base(message)
            {
            }
        }

        private static RuntimeSaveStateResult<RuntimeSaveState> Validate(RuntimeSaveState saveState, int targetSchemaVersion)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state json parsed to null.",
                    -1,
                    targetSchemaVersion));
            }

            if (saveState.SchemaVersion < 0)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state schema version cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            if (saveState.Frame < 0L)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.frame",
                    "Save state frame cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }
    }
}
