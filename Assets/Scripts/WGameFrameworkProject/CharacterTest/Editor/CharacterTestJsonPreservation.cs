using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditor.Compilation;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Maintains explicit linker roots for the project's runtime JSON contracts.</summary>
    internal static class CharacterTestJsonPreservation
    {
        internal const string OutputPath = "Assets/Scripts/WGameFrameworkProject/CharacterTest/link.xml";

        [MenuItem("MxFramework/Build/Windows64/Regenerate Runtime JSON Preservation")]
        internal static void Regenerate()
        {
            XmlDocument document = CreateDocument();
            using (var writer = XmlWriter.Create(OutputPath, new XmlWriterSettings { Indent = true }))
                document.Save(writer);
            AssetDatabase.ImportAsset(OutputPath);
            UnityEngine.Debug.Log("Runtime JSON linker contracts: " + document.SelectNodes("/linker/assembly/type").Count);
        }

        internal static void RequireCurrent(string outputRoot)
        {
            XmlDocument expected = CreateDocument();
            XmlDocument saved = new XmlDocument(); saved.Load(OutputPath);
            string Key(XmlNode node) => node.ParentNode.Attributes["fullname"].Value + "|" + node.Attributes["fullname"].Value;
            var existing = new HashSet<string>(saved.SelectNodes("/linker/assembly/type").Cast<XmlNode>()
                .Where(node => node.Attributes["preserve"]?.Value == "all").Select(Key), StringComparer.Ordinal);
            string[] missing = expected.SelectNodes("/linker/assembly/type").Cast<XmlNode>().Select(Key).Where(key => !existing.Contains(key)).ToArray();
            var receipt = new Newtonsoft.Json.Linq.JObject { ["linkXml"] = OutputPath,
                ["expectedTypeCount"] = expected.SelectNodes("/linker/assembly/type").Count,
                ["preservedTypeCount"] = existing.Count, ["missingTypes"] = new Newtonsoft.Json.Linq.JArray(missing),
                ["scope"] = "Concrete project/framework runtime generic JSON entry points, reachable DTO members and concrete converters; dynamic unknown types require explicit roots." };
            File.WriteAllText(Path.Combine(outputRoot, "CharacterTestJsonPreservation.json"), receipt.ToString());
            if (missing.Length > 0) throw new UnityEditor.Build.BuildFailedException("Regenerate runtime JSON preservation before building: " + string.Join(", ", missing));
        }

        internal static XmlDocument CreateDocument()
        {
            var players = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .Where(a => a.name.StartsWith("MxFramework.", StringComparison.Ordinal)).ToArray();
            var names = new HashSet<string>(players.Select(a => a.name), StringComparer.Ordinal);
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Where(a => names.Contains(a.GetName().Name))
                .SelectMany(a => a.GetTypes()).ToArray();
            var roots = new HashSet<string>(StringComparer.Ordinal);
            var pattern = new Regex(@"\b(?:DeserializeObject|ToObject|Deserialize)\s*<\s*([\w.]+)");
            foreach (string source in players.SelectMany(a => a.sourceFiles).Distinct())
                foreach (Match match in pattern.Matches(File.ReadAllText(source))) roots.Add(match.Groups[1].Value);

            var pending = new Queue<Type>(types.Where(t => roots.Contains(t.Name) || roots.Contains(t.FullName)
                || (!t.IsAbstract && typeof(JsonConverter).IsAssignableFrom(t))));
            var preserved = new HashSet<Type>();
            var resolver = new DefaultContractResolver();
            while (pending.Count > 0)
            {
                Type type = pending.Dequeue();
                if (type == null || type.ContainsGenericParameters) continue;
                if (type.HasElementType) { pending.Enqueue(type.GetElementType()); continue; }
                if (type.IsGenericType)
                    foreach (Type argument in type.GetGenericArguments()) pending.Enqueue(argument);
                if (!names.Contains(type.Assembly.GetName().Name) || !preserved.Add(type)) continue;
                if (type.IsEnum || typeof(JsonConverter).IsAssignableFrom(type)) continue;
                JsonContract contract = resolver.ResolveContract(type);
                if (contract.Converter != null) pending.Enqueue(contract.Converter.GetType());
                if (contract is JsonObjectContract objectContract)
                    foreach (JsonProperty property in objectContract.Properties.Where(p => !p.Ignored))
                    {
                        pending.Enqueue(property.PropertyType);
                        if (property.Converter != null) pending.Enqueue(property.Converter.GetType());
                        if (property.ItemConverter != null) pending.Enqueue(property.ItemConverter.GetType());
                    }
            }

            var document = new XmlDocument();
            document.AppendChild(document.CreateComment(" Generated by CharacterTestJsonPreservation. Runtime JSON payloads and converters only; regenerate after adding concrete JSON entry points. "));
            XmlElement linker = document.CreateElement("linker");
            document.AppendChild(linker);
            foreach (var group in preserved.Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t).Distinct()
                         .GroupBy(t => t.Assembly.GetName().Name).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                XmlElement assembly = document.CreateElement("assembly");
                assembly.SetAttribute("fullname", group.Key);
                linker.AppendChild(assembly);
                foreach (Type type in group.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    XmlElement item = document.CreateElement("type");
                    item.SetAttribute("fullname", type.FullName.Replace('+', '/'));
                    item.SetAttribute("preserve", "all");
                    assembly.AppendChild(item);
                }
            }
            return document;
        }
    }
}
