using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring
{
    public sealed class CharacterUnityImportArtifactInfo
    {
        public string SourceHash { get; set; } = string.Empty;
        public string ArtifactHash { get; set; } = string.Empty;
        public string ArtifactStatus { get; set; } = string.Empty;
        public string ValidationStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Transforms { get; set; } = new List<string>();
    }

    public static class CharacterUnityImportGlbCompatibility
    {
        private const uint GlbMagic = 0x46546C67;
        private const uint GlbVersion2 = 2;
        private const uint JsonChunkType = 0x4E4F534A;
        public const string StripSkinnedColor0Transform = "stripSkinnedColor0";

        public static CharacterUnityImportArtifactInfo AnalyzeResourceArtifact(
            string sourcePath,
            string targetPath,
            byte[] bytes,
            string sourceHash)
        {
            CharacterUnityImportWriteInput input = new CharacterUnityImportWriteInput
            {
                Kind = CharacterAuthoringCompilerWriteKinds.ResourceFile,
                SourcePath = sourcePath ?? string.Empty,
                TargetPath = targetPath ?? string.Empty
            };
            byte[] output;
            CharacterUnityImportArtifactInfo info;
            BuildResourceArtifact(input, bytes, sourceHash, out output, out info);
            return info;
        }

        internal static bool BuildResourceArtifact(
            CharacterUnityImportWriteInput input,
            byte[] bytes,
            out byte[] output,
            out CharacterUnityImportArtifactInfo info)
        {
            string sourceHash = CharacterPackageHashUtility.NormalizeSha256(input != null ? input.ContentHash : string.Empty);
            if (string.IsNullOrWhiteSpace(sourceHash) && bytes != null)
                sourceHash = CharacterPackageHashUtility.ComputeSha256(bytes);
            return BuildResourceArtifact(input, bytes, sourceHash, out output, out info);
        }

        private static bool BuildResourceArtifact(
            CharacterUnityImportWriteInput input,
            byte[] bytes,
            string sourceHash,
            out byte[] output,
            out CharacterUnityImportArtifactInfo info)
        {
            output = bytes;
            sourceHash = CharacterPackageHashUtility.NormalizeSha256(sourceHash);
            info = new CharacterUnityImportArtifactInfo
            {
                SourceHash = sourceHash,
                ArtifactHash = sourceHash,
                ArtifactStatus = "SourceUnchanged",
                ValidationStatus = "NotRequired",
                Message = "source bytes are used as the Unity import artifact."
            };

            if (input == null || bytes == null || !ShouldScan(input))
                return false;

            try
            {
                int removedAttributeCount;
                string validationError;
                if (!TryStripSkinnedVertexColorAttributes(bytes, out output, out removedAttributeCount, out validationError) || removedAttributeCount <= 0)
                {
                    if (!string.IsNullOrWhiteSpace(validationError))
                    {
                        info.ArtifactStatus = "Failed";
                        info.ValidationStatus = "Failed";
                        info.Message = validationError;
                    }
                    return false;
                }

                info.ArtifactHash = CharacterPackageHashUtility.ComputeSha256(output);
                info.ArtifactStatus = "Generated";
                info.ValidationStatus = "Passed";
                info.Transforms.Add(StripSkinnedColor0Transform);
                info.Message = "applied GLB Unity compatibility: removed " + removedAttributeCount.ToString() + " skinned COLOR_0 attribute reference(s).";
                return true;
            }
            catch (Exception ex)
            {
                output = bytes;
                info.ArtifactStatus = "Failed";
                info.ValidationStatus = "Failed";
                info.Message = "GLB compatibility artifact generation failed: " + ex.Message;
                return false;
            }
        }

        private static bool ShouldScan(CharacterUnityImportWriteInput input)
        {
            return string.Equals(input.Kind, CharacterAuthoringCompilerWriteKinds.ResourceFile, StringComparison.Ordinal)
                && (HasGlbExtension(input.SourcePath) || HasGlbExtension(input.TargetPath) || HasGlbExtension(input.SourceFilePath));
        }

        private static bool HasGlbExtension(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryStripSkinnedVertexColorAttributes(byte[] bytes, out byte[] output, out int removedAttributeCount, out string validationError)
        {
            output = bytes;
            removedAttributeCount = 0;
            validationError = string.Empty;

            if (bytes.Length < 20)
                return false;

            if (ReadUInt32(bytes, 0) != GlbMagic || ReadUInt32(bytes, 4) != GlbVersion2)
                return false;

            int declaredLength = checked((int)ReadUInt32(bytes, 8));
            if (declaredLength > bytes.Length)
                return false;

            List<GlbChunk> chunks = ReadChunks(bytes, declaredLength);
            int jsonChunkIndex = FindJsonChunk(chunks);
            if (jsonChunkIndex < 0)
                return false;

            string json = DecodeJsonChunk(chunks[jsonChunkIndex].Data);
            JsonNode rootNode = JsonNode.Parse(json);
            JsonObject root = rootNode as JsonObject;
            if (root == null)
                return false;

            string invariantSignature = BuildGlbInvariantSignature(root);
            removedAttributeCount = StripSkinnedColorAttributes(root);
            if (removedAttributeCount <= 0)
                return false;

            if (!string.Equals(invariantSignature, BuildGlbInvariantSignature(root), StringComparison.Ordinal))
            {
                validationError = "GLB compatibility transform changed scene, nodes, skins or animations.";
                output = bytes;
                return false;
            }

            byte[] jsonBytes = EncodeJsonChunk(root.ToJsonString());
            chunks[jsonChunkIndex] = new GlbChunk(JsonChunkType, jsonBytes);
            output = BuildGlb(chunks);
            return true;
        }

        private static string BuildGlbInvariantSignature(JsonObject root)
        {
            var builder = new StringBuilder();
            AppendInvariant(builder, root, "scene");
            AppendInvariant(builder, root, "scenes");
            AppendInvariant(builder, root, "nodes");
            AppendInvariant(builder, root, "skins");
            AppendInvariant(builder, root, "animations");
            return builder.ToString();
        }

        private static void AppendInvariant(StringBuilder builder, JsonObject root, string propertyName)
        {
            builder.Append(propertyName).Append('=');
            JsonNode node = root[propertyName];
            if (node != null)
                builder.Append(node.ToJsonString());
            builder.Append('\n');
        }

        private static List<GlbChunk> ReadChunks(byte[] bytes, int declaredLength)
        {
            var chunks = new List<GlbChunk>();
            int offset = 12;
            while (offset + 8 <= declaredLength)
            {
                int chunkLength = checked((int)ReadUInt32(bytes, offset));
                uint chunkType = ReadUInt32(bytes, offset + 4);
                offset += 8;

                if (chunkLength < 0 || offset + chunkLength > declaredLength)
                    throw new InvalidOperationException("Invalid GLB chunk length.");

                var data = new byte[chunkLength];
                Buffer.BlockCopy(bytes, offset, data, 0, chunkLength);
                chunks.Add(new GlbChunk(chunkType, data));
                offset += chunkLength;
            }

            if (offset != declaredLength)
                throw new InvalidOperationException("Invalid GLB chunk alignment.");

            return chunks;
        }

        private static int FindJsonChunk(List<GlbChunk> chunks)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].Type == JsonChunkType)
                    return i;
            }

            return -1;
        }

        private static string DecodeJsonChunk(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            return json.TrimEnd('\0', ' ', '\t', '\r', '\n');
        }

        private static byte[] EncodeJsonChunk(string json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            int paddedLength = Align4(raw.Length);
            var padded = new byte[paddedLength];
            Buffer.BlockCopy(raw, 0, padded, 0, raw.Length);
            for (int i = raw.Length; i < padded.Length; i++)
                padded[i] = 0x20;
            return padded;
        }

        private static int StripSkinnedColorAttributes(JsonObject root)
        {
            JsonArray meshes = root["meshes"] as JsonArray;
            if (meshes == null)
                return 0;

            int removed = 0;
            for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                JsonObject mesh = meshes[meshIndex] as JsonObject;
                JsonArray primitives = mesh != null ? mesh["primitives"] as JsonArray : null;
                if (primitives == null)
                    continue;

                for (int primitiveIndex = 0; primitiveIndex < primitives.Count; primitiveIndex++)
                {
                    JsonObject primitive = primitives[primitiveIndex] as JsonObject;
                    JsonObject attributes = primitive != null ? primitive["attributes"] as JsonObject : null;
                    if (attributes == null || !IsSkinned(attributes) || !attributes.ContainsKey("COLOR_0"))
                        continue;

                    if (attributes.Remove("COLOR_0"))
                        removed++;
                }
            }

            return removed;
        }

        private static bool IsSkinned(JsonObject attributes)
        {
            return attributes.ContainsKey("JOINTS_0") && attributes.ContainsKey("WEIGHTS_0");
        }

        private static byte[] BuildGlb(List<GlbChunk> chunks)
        {
            int totalLength = 12;
            for (int i = 0; i < chunks.Count; i++)
                totalLength = checked(totalLength + 8 + chunks[i].Data.Length);

            var output = new byte[totalLength];
            WriteUInt32(output, 0, GlbMagic);
            WriteUInt32(output, 4, GlbVersion2);
            WriteUInt32(output, 8, checked((uint)totalLength));

            int offset = 12;
            for (int i = 0; i < chunks.Count; i++)
            {
                GlbChunk chunk = chunks[i];
                WriteUInt32(output, offset, checked((uint)chunk.Data.Length));
                WriteUInt32(output, offset + 4, chunk.Type);
                offset += 8;
                Buffer.BlockCopy(chunk.Data, 0, output, offset, chunk.Data.Length);
                offset += chunk.Data.Length;
            }

            return output;
        }

        private static int Align4(int value)
        {
            return (value + 3) & ~3;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private readonly struct GlbChunk
        {
            public GlbChunk(uint type, byte[] data)
            {
                Type = type;
                Data = data;
            }

            public uint Type { get; }
            public byte[] Data { get; }
        }
    }
}
