using System.IO;
using UnityEditor;

namespace MxFramework.Tests.Resources
{
    internal static class ResourcesProviderTestFixture
    {
        private const string TempRoot = "Assets/Temp";
        private const string Root = "Assets/Temp/MxFrameworkResourcesProviderTests";
        private const string ResourceDirectory = Root + "/Resources/MxFramework/ResourcesProviderTests";
        private const string ResourceFilePath = ResourceDirectory + "/resource_demo_text.txt";

        public const string DemoAddress = "MxFramework/ResourcesProviderTests/resource_demo_text";
        public const string MissingAddress = "MxFramework/ResourcesProviderTests/missing";
        public const string DemoText = "MxFramework resources demo text";

        public static void Create()
        {
            Delete();
            Directory.CreateDirectory(ResourceDirectory);
            File.WriteAllText(ResourceFilePath, DemoText + "\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void Delete()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
            DeleteFile(Root + ".meta");
            DeleteDirectoryIfEmpty(TempRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void DeleteDirectoryIfEmpty(string path)
        {
            if (!Directory.Exists(path) || Directory.GetFileSystemEntries(path).Length != 0)
                return;

            Directory.Delete(path);
            DeleteFile(path + ".meta");
        }
    }
}
