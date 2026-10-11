// Compiled locally by analyze_package_native_size.ps1 against the installed DIA SDK.
// This is an analysis tool, not part of any Unity Player assembly.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using DiaInterop;

public static class PackageNativeSizeAnalysis
{
    private sealed class Section
    {
        public string name;
        public uint virtualAddress, rawOffset, rawBytes;
    }
    private struct Edge
    {
        public long position;
        public int owner, delta;
        public Edge(long p, int o, int d) { position = p; owner = o; delta = d; }
    }

    public static void Analyze(string binary, string pdb, string output)
    {
        var sections = new List<Section>();
        Guid signature = Guid.Empty;
        uint age = 0;
        using (var reader = new BinaryReader(File.OpenRead(binary)))
        {
            reader.BaseStream.Position = 0x3c;
            reader.BaseStream.Position = reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x4550) throw new InvalidDataException("Not a PE image.");
            reader.ReadUInt16();
            ushort count = reader.ReadUInt16();
            reader.BaseStream.Position += 12;
            ushort optionalBytes = reader.ReadUInt16();
            reader.ReadUInt16();
            long optional = reader.BaseStream.Position;
            if (reader.ReadUInt16() != 0x20b) throw new InvalidDataException("Expected PE32+.");
            reader.BaseStream.Position = optional + 112 + 6 * 8;
            uint debugRva = reader.ReadUInt32(), debugBytes = reader.ReadUInt32();
            reader.BaseStream.Position = optional + optionalBytes;
            for (int i = 0; i < count; i++)
            {
                string name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
                reader.ReadUInt32();
                var section = new Section { name = name, virtualAddress = reader.ReadUInt32(),
                    rawBytes = reader.ReadUInt32(), rawOffset = reader.ReadUInt32() };
                sections.Add(section);
                reader.BaseStream.Position += 16;
            }
            var debugSection = sections.First(s => debugRva >= s.virtualAddress && debugRva < s.virtualAddress + s.rawBytes);
            long debugOffset = debugSection.rawOffset + debugRva - debugSection.virtualAddress;
            for (int i = 0; i < debugBytes / 28; i++)
            {
                reader.BaseStream.Position = debugOffset + i * 28 + 12;
                uint type = reader.ReadUInt32();
                reader.BaseStream.Position += 8;
                uint pointer = reader.ReadUInt32();
                if (type != 2) continue;
                reader.BaseStream.Position = pointer;
                if (reader.ReadUInt32() != 0x53445352) continue;
                signature = new Guid(reader.ReadBytes(16)); age = reader.ReadUInt32(); break;
            }
        }
        if (signature == Guid.Empty) throw new InvalidDataException("PE CodeView identity missing.");
        var source = (IDiaDataSource)new DiaSourceClass();
        source.loadAndValidateDataFromPdb(pdb, ref signature, 0, age);
        IDiaSession session; source.openSession(out session);
        IDiaEnumTables tables; session.getEnumTables(out tables);
        var ownerNames = new List<string>();
        var ownerIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var modules = new Dictionary<uint, int>();
        var edges = new List<Edge>();
        long contributionCount = 0;
        foreach (IDiaTable table in tables)
        {
            if (table.name != "Sections") continue;
            var contributions = (IDiaEnumSectionContribs)table;
            contributions.Reset();
            while (true)
            {
                IDiaSectionContrib contribution; uint fetched;
                contributions.Next(1, out contribution, out fetched);
                if (fetched == 0) break;
                try
                {
                    uint index = contribution.addressSection;
                    if (index == 0 || index > sections.Count || contribution.length == 0) continue;
                    Section section = sections[(int)index - 1];
                    long begin = contribution.addressOffset;
                    long end = Math.Min((long)section.rawBytes, begin + contribution.length);
                    if (end <= begin) continue; // BSS/virtual-only data is not file storage.
                    int owner;
                    uint moduleId = contribution.compilandId;
                    if (!modules.TryGetValue(moduleId, out owner))
                    {
                        var compiland = contribution.compiland;
                        IDiaEnumSourceFiles files; session.findFile(compiland, null, 0, out files);
                        var paths = new List<string>();
                        foreach (IDiaSourceFile file in files)
                        {
                            paths.Add(file.fileName.Replace('\\', '/'));
                            Marshal.ReleaseComObject(file);
                        }
                        string name = Classify(paths, compiland.name);
                        if (!ownerIds.TryGetValue(name, out owner))
                        { owner = ownerNames.Count; ownerNames.Add(name); ownerIds.Add(name, owner); }
                        modules.Add(moduleId, owner);
                        Marshal.ReleaseComObject(files); Marshal.ReleaseComObject(compiland);
                    }
                    edges.Add(new Edge(section.rawOffset + begin, owner, 1));
                    edges.Add(new Edge(section.rawOffset + end, owner, -1));
                    contributionCount++;
                    if (contributionCount % 100000 == 0) Console.WriteLine("Native contributions read: " + contributionCount);
                }
                finally { Marshal.ReleaseComObject(contribution); }
            }
        }
        edges.Sort((a, b) => a.position.CompareTo(b.position));
        var active = new Dictionary<int, int>();
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        long previous = 0, covered = 0;
        for (int i = 0; i < edges.Count;)
        {
            long position = edges[i].position;
            if (position > previous && active.Count > 0)
            {
                string owner = active.Count == 1 ? ownerNames[active.Keys.First()] : "Shared / folded across modules";
                long size = position - previous;
                if (!sizes.ContainsKey(owner)) sizes[owner] = 0;
                sizes[owner] += size; covered += size;
            }
            while (i < edges.Count && edges[i].position == position)
            {
                Edge edge = edges[i++]; int count;
                active.TryGetValue(edge.owner, out count); count += edge.delta;
                if (count == 0) active.Remove(edge.owner); else active[edge.owner] = count;
            }
            previous = position;
        }
        long total = new FileInfo(binary).Length;
        if (covered > total || active.Count != 0) throw new InvalidDataException("Native contribution accounting failed.");
        sizes["Headers / alignment / unattributed"] = total - covered;
        string hash;
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(binary))
            hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        var result = new { status = "Verified", method = "DIA section contributions clipped to PE file-backed ranges; overlapping owners counted once as shared",
            binaryBytes = total, binarySha256 = hash, pdbGuid = signature.ToString(), pdbAge = age,
            contributionCount = contributionCount, moduleCount = modules.Count,
            groups = sizes.OrderByDescending(p => p.Value).Select(p => new { name = p.Key, bytes = p.Value }).ToArray(),
            sections = sections.Select(s => new { name = s.name, bytes = s.rawBytes }).ToArray() };
        File.WriteAllText(output, new JavaScriptSerializer().Serialize(result), new UTF8Encoding(false));
    }

    private static string Classify(List<string> files, string module)
    {
        string generated = files.FirstOrDefault(p => p.IndexOf("/il2cppOutput/", StringComparison.OrdinalIgnoreCase) >= 0
            && (p.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".c", StringComparison.OrdinalIgnoreCase)));
        if (generated != null)
        {
            string name = Path.GetFileNameWithoutExtension(generated);
            if (name.StartsWith("Generic", StringComparison.Ordinal)) return "Shared generic code";
            if (name.StartsWith("Il2Cpp", StringComparison.Ordinal)) return "IL2CPP registration / generated tables";
            name = Regex.Replace(name, @"__\d+$", "");
            name = Regex.Replace(name, @"_CodeGen$", "");
            return name;
        }
        if (files.Any(p => p.IndexOf("/libil2cpp/", StringComparison.OrdinalIgnoreCase) >= 0)) return "IL2CPP runtime";
        if (files.Any(p => p.IndexOf("/external/", StringComparison.OrdinalIgnoreCase) >= 0)) return "IL2CPP native dependencies";
        return "Native libraries / other";
    }
}
