using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Prefab Descriptor")]
    public sealed class CharacterRuntimePrefabDescriptor : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string _packageId = string.Empty;
        [SerializeField] private string _characterResourceId = string.Empty;
        [SerializeField] private string _resourceVariant = "default";

        public int SchemaVersion => _schemaVersion;
        public string PackageId => _packageId;
        public string CharacterResourceId => _characterResourceId;
        public string ResourceVariant => _resourceVariant;

        /// <summary>
        /// LowFreqAlloc: fills descriptor metadata while exporting a runtime character prefab.
        /// </summary>
        public void Configure(string packageId, string characterResourceId, string resourceVariant)
        {
            _schemaVersion = CurrentSchemaVersion;
            _packageId = packageId ?? string.Empty;
            _characterResourceId = characterResourceId ?? string.Empty;
            _resourceVariant = string.IsNullOrWhiteSpace(resourceVariant) ? "default" : resourceVariant;
        }
    }
}
