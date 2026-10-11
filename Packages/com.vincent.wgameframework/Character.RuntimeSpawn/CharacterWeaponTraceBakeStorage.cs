using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>Lossless, versioned weapon/contact storage. All IO and decoding belongs to load/authoring boundaries.</summary>
    public static class CharacterWeaponTraceBakeStorage
    {
        public const string Format = "mx.character.weaponTraceBakeCatalog.v3";
        public const string WeaponPath = "generated/weapon_trace_bake_catalog.bytes";
        public const string ContactPath = "generated/contact_motion_bake_catalog.bytes";
        private const uint Magic = 0x3242574d;
        private const int MaxPayload = 64 * 1024 * 1024;
        private const int MaxRecords = 1000000;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>LowFreqAlloc: selects actual attack windows and preserves their explicit previous endpoints.</summary>
        public static CharacterWeaponTraceBakeCatalog SelectWeaponWindows(CharacterWeaponTraceBakeCatalog source,
            IReadOnlyList<CharacterWeaponTraceBakeRequest> requests)
        {
            var result = new CharacterWeaponTraceBakeCatalog();
            foreach (var pair in source.EnumerateArtifacts())
            {
                var selected = new List<MxAnimationBakedWeaponTraceFrame>();
                foreach (var frame in pair.Value.WeaponTraceFrames)
                {
                    for (int i = 0; requests != null && i < requests.Count; i++)
                    {
                        var request = requests[i];
                        if (request.ClipKey == pair.Key.ClipKey && request.TraceId == pair.Key.TraceId
                            && frame.LocalFrame >= request.ActiveStartFrame && frame.LocalFrame <= request.ActiveEndFrame)
                        { selected.Add(frame); break; }
                    }
                }
                if (selected.Count > 0)
                    result.RegisterArtifact(pair.Key.ClipKey, pair.Key.TraceId,
                        new MxAnimationBakeArtifact(pair.Value.Profile, selected));
            }
            return result;
        }

        /// <summary>LowFreqAlloc: encodes signed 64-bit values without quantization changes, using delta/varint and Deflate.</summary>
        public static byte[] Encode(string packageId, CharacterWeaponTraceBakeCatalog catalog, bool contacts)
        {
            var artifacts = new List<KeyValuePair<CharacterWeaponTraceBakeKey, MxAnimationBakeArtifact>>();
            foreach (var pair in catalog.EnumerateArtifacts())
                if ((contacts ? pair.Value.SocketFrames.Count : pair.Value.WeaponTraceFrames.Count) > 0) artifacts.Add(pair);
            artifacts.Sort((a, b) => { int c = string.CompareOrdinal(a.Key.ClipKey, b.Key.ClipKey);
                return c != 0 ? c : a.Key.TraceId.CompareTo(b.Key.TraceId); });
            byte[] raw;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                Text(writer, packageId);
                Unsigned(writer, (ulong)artifacts.Count);
                foreach (var pair in artifacts)
                {
                    Text(writer, pair.Key.ClipKey); Unsigned(writer, checked((ulong)pair.Key.TraceId)); Profile(writer, pair.Value.Profile);
                    Text(writer, contacts
                        ? new MxAnimationBakeArtifact(pair.Value.Profile, socketFrames: pair.Value.SocketFrames).ArtifactHash
                        : new MxAnimationBakeArtifact(pair.Value.Profile, pair.Value.WeaponTraceFrames).ArtifactHash);
                    if (contacts) WriteContacts(writer, pair.Value.SocketFrames);
                    else WriteWeapon(writer, pair.Value.WeaponTraceFrames);
                }
                writer.Flush(); raw = stream.ToArray();
            }
            if (raw.Length > MaxPayload) throw new InvalidDataException("Bake payload exceeds the format limit.");
            byte[] digest = Digest(raw);
            byte[] compressed;
            using (var buffer = new MemoryStream())
            {
                using (var zip = new DeflateStream(buffer, CompressionLevel.Optimal, true)) zip.Write(raw, 0, raw.Length);
                compressed = buffer.ToArray();
            }
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Utf8, true))
                { writer.Write(Magic); writer.Write((byte)3); writer.Write(contacts); writer.Write(raw.Length); writer.Write(digest);
                    writer.Write(compressed.Length); writer.Write(compressed); }
                return output.ToArray();
            }
        }

        /// <summary>LowFreqAlloc: validates identity, version, bounds and SHA-256 before constructing indexed tracks.</summary>
        public static CharacterWeaponTraceBakeCatalog Decode(byte[] bytes, string packageId, bool contacts,
            out CharacterWeaponTraceBakeBuildReport report)
        {
            try
            {
                if (bytes == null || bytes.Length < 46 || bytes.Length > MaxPayload) throw new InvalidDataException("Invalid bake length.");
                byte[] raw;
                using (var input = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(input, Utf8, true))
                {
                    if (reader.ReadUInt32() != Magic || reader.ReadByte() != 3 || reader.ReadBoolean() != contacts)
                        throw new InvalidDataException("Bake version or data kind mismatch.");
                    int length = reader.ReadInt32();
                    if (length < 1 || length > MaxPayload) throw new InvalidDataException("Invalid decoded bake length.");
                    byte[] expected = reader.ReadBytes(32);
                    if (reader.ReadInt32() != input.Length - input.Position) throw new InvalidDataException("Encoded bake length mismatch.");
                    raw = new byte[length];
                    using (var zip = new DeflateStream(input, CompressionMode.Decompress, true))
                    {
                        int offset = 0;
                        while (offset < raw.Length) { int count = zip.Read(raw, offset, raw.Length - offset);
                            if (count == 0) throw new InvalidDataException("Truncated bake payload."); offset += count; }
                        if (zip.ReadByte() != -1) throw new InvalidDataException("Decoded bake length mismatch.");
                    }
                    byte[] actual = Digest(raw);
                    for (int i = 0; i < 32; i++) if (expected[i] != actual[i]) throw new InvalidDataException("Bake checksum mismatch.");
                }
                var catalog = new CharacterWeaponTraceBakeCatalog(); int records = 0;
                using (var input = new MemoryStream(raw, false))
                using (var reader = new BinaryReader(input, Utf8))
                {
                    if (!string.Equals(Text(reader), packageId ?? string.Empty, StringComparison.Ordinal))
                        throw new InvalidDataException("Bake package identity mismatch.");
                    int count = Count(reader, 20000);
                    for (int i = 0; i < count; i++)
                    {
                        string clip = Text(reader); int trace = PositiveIdentity(reader, "trace id"); var profile = Profile(reader); string hash = Text(reader);
                        if (string.IsNullOrEmpty(clip) || catalog.TryGetArtifact(clip, trace, out _))
                            throw new InvalidDataException("Missing or duplicated bake identity.");
                        var weapons = contacts ? null : ReadWeapon(reader, ref records);
                        var sockets = contacts ? ReadContacts(reader, ref records) : null;
                        catalog.RegisterArtifact(clip, trace, new MxAnimationBakeArtifact(profile, weapons,
                            artifactHash: hash, socketFrames: sockets));
                    }
                    if (input.Position != input.Length) throw new InvalidDataException("Trailing bake payload.");
                }
                report = new CharacterWeaponTraceBakeBuildReport(catalog.Count, catalog.Count, records, null);
                return catalog;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException)
            {
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, new[] {
                    new CharacterWeaponTraceBakeBuildIssue(CharacterWeaponTraceBakeCatalogJson.IssueCatalogInvalid,
                        ex.Message, CharacterActionId.None, string.Empty, 0, -1,
                        contacts ? ContactPath : WeaponPath, "binary") });
                return new CharacterWeaponTraceBakeCatalog();
            }
        }

        /// <summary>LowFreqAlloc: legacy JSON is accepted only when no v3 index is present; corrupt v3 never falls back.</summary>
        public static CharacterWeaponTraceBakeCatalog Load(string packageRoot, string packageId, bool contacts,
            out CharacterWeaponTraceBakeBuildReport report)
        {
            string indexPath = Path.Combine(packageRoot, CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath);
            try
            {
                string json = File.ReadAllText(indexPath);
                var index = JObject.Parse(json);
                if ((string)index["format"] != Format)
                    return CharacterWeaponTraceBakeCatalogJson.Load(json, indexPath, out report);
                var descriptor = index[contacts ? "contacts" : "weapons"] as JObject;
                string relative = contacts ? ContactPath : WeaponPath;
                if ((string)index["packageId"] != packageId || descriptor == null || (string)descriptor["path"] != relative)
                    throw new InvalidDataException("Bake index identity or path mismatch.");
                byte[] data = File.ReadAllBytes(Path.Combine(packageRoot, relative));
                if ((int?)descriptor["size"] != data.Length || (string)descriptor["sha256"] != Hash(data))
                    throw new InvalidDataException("Bake index checksum mismatch.");
                return Decode(data, packageId, contacts, out report);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException || ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                report = new CharacterWeaponTraceBakeBuildReport(0, 0, 0, new[] {
                    new CharacterWeaponTraceBakeBuildIssue(CharacterWeaponTraceBakeCatalogJson.IssueCatalogInvalid,
                        ex.Message, CharacterActionId.None, string.Empty, 0, -1, indexPath, "index") });
                return new CharacterWeaponTraceBakeCatalog();
            }
        }

        /// <summary>LowFreqAlloc: generates independent payloads and a small index, publishing the index last.</summary>
        public static void Write(string packageRoot, string packageId, CharacterWeaponTraceBakeCatalog weapons,
            CharacterWeaponTraceBakeCatalog contacts)
        {
            byte[] weaponBytes = Encode(packageId, weapons, false), contactBytes = Encode(packageId, contacts, true);
            var artifacts = new JArray();
            var ordered = new List<KeyValuePair<CharacterWeaponTraceBakeKey, MxAnimationBakeArtifact>>(weapons.EnumerateArtifacts());
            ordered.Sort((a, b) => { int c = string.CompareOrdinal(a.Key.ClipKey, b.Key.ClipKey);
                return c != 0 ? c : a.Key.TraceId.CompareTo(b.Key.TraceId); });
            foreach (var pair in ordered) artifacts.Add(new JObject {
                ["clipKey"] = pair.Key.ClipKey, ["traceId"] = pair.Key.TraceId,
                ["frameCount"] = pair.Value.WeaponTraceFrames.Count, ["artifactHash"] = pair.Value.ArtifactHash });
            var index = new JObject { ["format"] = Format, ["packageId"] = packageId,
                ["sampleTickRate"] = 60, ["quantizationScale"] = 1000000,
                ["weapons"] = Descriptor(WeaponPath, weaponBytes), ["contacts"] = Descriptor(ContactPath, contactBytes),
                ["artifacts"] = artifacts };
            Directory.CreateDirectory(Path.Combine(packageRoot, "generated"));
            AtomicWrite(Path.Combine(packageRoot, WeaponPath), weaponBytes);
            AtomicWrite(Path.Combine(packageRoot, ContactPath), contactBytes);
            AtomicWrite(Path.Combine(packageRoot, CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath),
                Utf8.GetBytes(index.ToString(Formatting.Indented) + Environment.NewLine));
        }

        private static JObject Descriptor(string path, byte[] bytes) => new JObject { ["path"] = path, ["size"] = bytes.Length, ["sha256"] = Hash(bytes) };
        private static void AtomicWrite(string path, byte[] bytes)
        {
            if (File.Exists(path) && new FileInfo(path).Length == bytes.Length && Hash(File.ReadAllBytes(path)) == Hash(bytes)) return;
            string temp = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temp, bytes);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static byte[] Digest(byte[] value) { using (var sha = SHA256.Create()) return sha.ComputeHash(value); }
        private static string Hash(byte[] value) => BitConverter.ToString(Digest(value)).Replace("-", string.Empty).ToLowerInvariant();
        private static void Unsigned(BinaryWriter w, ulong value) { while (value >= 128) { w.Write((byte)(value | 128)); value >>= 7; } w.Write((byte)value); }
        private static ulong Unsigned(BinaryReader r)
        {
            ulong value = 0;
            for (int shift = 0; shift < 70; shift += 7) { byte b = r.ReadByte(); if (shift == 63 && b > 1) throw new InvalidDataException("Varint overflow.");
                value |= (ulong)(b & 127) << shift; if ((b & 128) == 0) return value; }
            throw new InvalidDataException("Invalid varint.");
        }
        private static void Signed(BinaryWriter w, long n) => Unsigned(w, unchecked((ulong)((n << 1) ^ (n >> 63))));
        private static long Signed(BinaryReader r) { ulong n = Unsigned(r); return unchecked((long)(n >> 1) ^ -((long)n & 1)); }
        private static int Count(BinaryReader r, int max) { ulong n = Unsigned(r); if (n > (ulong)max) throw new InvalidDataException("Bake count exceeds bounds."); return (int)n; }
        private static int PositiveIdentity(BinaryReader r, string name)
        {
            int value = Count(r, int.MaxValue);
            if (value <= 0)
                throw new InvalidDataException("Invalid " + name + ".");
            return value;
        }
        private static void Text(BinaryWriter w, string value) { byte[] data = Utf8.GetBytes(value ?? string.Empty); Unsigned(w, (ulong)data.Length); w.Write(data); }
        private static string Text(BinaryReader r) { int count = Count(r, 1024 * 1024); byte[] bytes = r.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return Utf8.GetString(bytes); }
        private static void Vector(BinaryWriter w, MxAnimationBakedVector3 value, ref MxAnimationBakedVector3 previous)
        { Signed(w, unchecked(value.X - previous.X)); Signed(w, unchecked(value.Y - previous.Y)); Signed(w, unchecked(value.Z - previous.Z)); previous = value; }
        private static MxAnimationBakedVector3 Vector(BinaryReader r, ref MxAnimationBakedVector3 previous)
        { previous = new MxAnimationBakedVector3(unchecked(previous.X + Signed(r)), unchecked(previous.Y + Signed(r)), unchecked(previous.Z + Signed(r))); return previous; }
        private static void Profile(BinaryWriter w, MxAnimationBakeProfile p)
        {
            Text(w,p.ProfileId); Text(w,p.SourceClipKey.Id); Text(w,p.SourceClipKey.TypeId); Text(w,p.SourceClipKey.Variant); Text(w,p.SourceClipKey.PackageId);
            Text(w,p.SourceClipHash); Text(w,p.SkeletonProfileId); Text(w,p.SkeletonProfileHash); w.Write(p.SampleTickRate); w.Write(p.QuantizationScale);
            w.Write((int)p.CoordinateSpace); w.Write((int)p.RoundingPolicy); Text(w,p.ImportSettingsFingerprint); Text(w,p.ProfileHash);
        }
        private static MxAnimationBakeProfile Profile(BinaryReader r)
        {
            string id=Text(r); var key=new ResourceKey(Text(r),Text(r),Text(r),Text(r)); string source=Text(r),rig=Text(r),rigHash=Text(r);
            int rate=r.ReadInt32(),scale=r.ReadInt32(),space=r.ReadInt32(),round=r.ReadInt32();
            if(rate<=0 || scale<=0 || space<0 || space>1 || round<0 || round>2) throw new InvalidDataException("Invalid bake profile.");
            return new MxAnimationBakeProfile(id,key,source,rig,rigHash,rate,scale,(MxAnimationBakeCoordinateSpace)space,(MxAnimationBakeRoundingPolicy)round,Text(r),Text(r));
        }
        private static void WriteWeapon(BinaryWriter w, IReadOnlyList<MxAnimationBakedWeaponTraceFrame> frames)
        {
            Unsigned(w,(ulong)frames.Count); MxAnimationBakedVector3 root=default,tip=default; int previousFrame=-1;
            foreach(var f in frames) {
                if(f.LocalFrame<=previousFrame) throw new InvalidDataException("Weapon frames must be unique and ordered.");
                Unsigned(w,(ulong)f.LocalFrame); Unsigned(w,(ulong)f.TraceId); Unsigned(w,(ulong)f.SocketId);
                bool same=f.RootPrev.Equals(root)&&f.TipPrev.Equals(tip); w.Write(same);
                if(!same){Vector(w,f.RootPrev,ref root);Vector(w,f.TipPrev,ref tip);}
                Vector(w,f.RootNow,ref root);Vector(w,f.TipNow,ref tip);previousFrame=f.LocalFrame;
            }
        }
        private static List<MxAnimationBakedWeaponTraceFrame> ReadWeapon(BinaryReader r, ref int total)
        {
            int count=Count(r,MaxRecords-total);total+=count;var frames=new List<MxAnimationBakedWeaponTraceFrame>(count);
            MxAnimationBakedVector3 root=default,tip=default;int previousFrame=-1;
            for(int i=0;i<count;i++){int frame=Count(r,MaxRecords);if(frame<=previousFrame)throw new InvalidDataException("Unordered weapon frame.");
                int id=PositiveIdentity(r, "trace id");int socket=PositiveIdentity(r, "socket id");
                if(!r.ReadBoolean()){Vector(r,ref root);Vector(r,ref tip);}var rp=root;var tp=tip;
                frames.Add(new MxAnimationBakedWeaponTraceFrame(frame,id,socket,rp,tp,Vector(r,ref root),Vector(r,ref tip)));previousFrame=frame;}
            return frames;
        }
        private static void WriteContacts(BinaryWriter w, IReadOnlyList<MxAnimationBakedSocketFrame> frames)
        {
            var tracks=new SortedDictionary<int,List<MxAnimationBakedSocketFrame>>();
            foreach(var f in frames){if(!tracks.TryGetValue(f.SocketId,out var track)){track=new List<MxAnimationBakedSocketFrame>();tracks.Add(f.SocketId,track);}track.Add(f);}
            Unsigned(w,(ulong)tracks.Count);
            foreach(var pair in tracks){Unsigned(w,(ulong)pair.Key);Unsigned(w,(ulong)pair.Value.Count);MxAnimationBakedVector3 position=default,delta=default;
                string path=string.Empty;foreach(var f in pair.Value){Unsigned(w,(ulong)f.LocalFrame);bool same=f.SocketPath==path;w.Write(same);if(!same){Text(w,f.SocketPath);path=f.SocketPath;}
                    Vector(w,f.Position,ref position);Vector(w,f.DeltaPosition,ref delta);}}
        }
        private static List<MxAnimationBakedSocketFrame> ReadContacts(BinaryReader r, ref int total)
        {
            int tracks=Count(r,20000);var frames=new List<MxAnimationBakedSocketFrame>();var ids=new HashSet<int>();
            for(int i=0;i<tracks;i++){int socket=PositiveIdentity(r, "socket id");if(!ids.Add(socket))throw new InvalidDataException("Duplicate socket.");int count=Count(r,MaxRecords-total);total+=count;
                MxAnimationBakedVector3 position=default,delta=default;string path=string.Empty;int previousFrame=-1;
                for(int j=0;j<count;j++){int frame=Count(r,MaxRecords);if(frame<=previousFrame)throw new InvalidDataException("Unordered socket frame.");previousFrame=frame;
                    if(!r.ReadBoolean())path=Text(r);frames.Add(new MxAnimationBakedSocketFrame(frame,socket,path,Vector(r,ref position),Vector(r,ref delta)));}}
            return frames;
        }
    }
}
