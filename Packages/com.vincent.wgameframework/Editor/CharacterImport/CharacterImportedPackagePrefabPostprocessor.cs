using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    /// <summary>
    /// Immutable Editor context supplied to optional imported-character prefab postprocessors.
    /// </summary>
    public sealed class CharacterImportedPackagePrefabPostprocessContext
    {
        internal CharacterImportedPackagePrefabPostprocessContext(
            GameObject characterRoot,
            Transform modelRoot,
            Transform bodyModel,
            string importedPackageRoot,
            CharacterImportedPackage package)
        {
            CharacterRoot = characterRoot;
            ModelRoot = modelRoot;
            BodyModel = bodyModel;
            ImportedPackageRoot = importedPackageRoot ?? string.Empty;
            Package = package;
        }

        public GameObject CharacterRoot { get; }

        public Transform ModelRoot { get; }

        public Transform BodyModel { get; }

        public string ImportedPackageRoot { get; }

        public CharacterImportedPackage Package { get; }
    }

    /// <summary>
    /// Editor-only extension point for optional integrations that add presentation components to
    /// a generated character prefab before it is saved and mirrored to Runtime Resources.
    /// </summary>
    public interface ICharacterImportedPackagePrefabPostprocessor
    {
        int Order { get; }

        /// <remarks>AllocByDesign: called only during imported character prefab construction.</remarks>
        void Process(CharacterImportedPackagePrefabPostprocessContext context);
    }

    internal static class CharacterImportedPackagePrefabPostprocessorRunner
    {
        private static ICharacterImportedPackagePrefabPostprocessor[] s_postprocessors;

        /// <remarks>AllocByDesign: Editor-only prefab construction and optional adapter discovery.</remarks>
        internal static void Process(CharacterImportedPackagePrefabPostprocessContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ICharacterImportedPackagePrefabPostprocessor[] postprocessors =
                s_postprocessors ??= DiscoverPostprocessors();
            for (int i = 0; i < postprocessors.Length; i++)
            {
                ICharacterImportedPackagePrefabPostprocessor postprocessor = postprocessors[i];
                try
                {
                    postprocessor.Process(context);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Imported character prefab postprocessor failed: "
                        + postprocessor.GetType().FullName,
                        exception);
                }
            }
        }

        private static ICharacterImportedPackagePrefabPostprocessor[] DiscoverPostprocessors()
        {
            var result = new List<ICharacterImportedPackagePrefabPostprocessor>();
            TypeCache.TypeCollection types =
                TypeCache.GetTypesDerivedFrom<ICharacterImportedPackagePrefabPostprocessor>();
            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                    continue;

                if (Activator.CreateInstance(type) is ICharacterImportedPackagePrefabPostprocessor postprocessor)
                    result.Add(postprocessor);
            }

            result.Sort(ComparePostprocessors);
            return result.ToArray();
        }

        private static int ComparePostprocessors(
            ICharacterImportedPackagePrefabPostprocessor left,
            ICharacterImportedPackagePrefabPostprocessor right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0
                ? order
                : string.CompareOrdinal(left.GetType().FullName, right.GetType().FullName);
        }
    }
}
