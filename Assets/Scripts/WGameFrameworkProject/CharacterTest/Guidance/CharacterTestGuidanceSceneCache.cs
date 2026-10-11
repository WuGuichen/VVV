using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGuidanceRuntimeDiagnosticCode : byte
    {
        None = 0,
        AuthorDisabled = 1,
        ModuleMissing = 2,
        ModuleSchemaUnsupported = 3,
        ModuleInvalid = 4,
        CacheMissing = 5,
        CacheSchemaUnsupported = 6,
        CacheSourceHashMismatch = 7,
        CompletionCatalogHashMismatch = 8
    }

    internal readonly struct CharacterTestGuidanceRuntimeDiagnostic
    {
        private CharacterTestGuidanceRuntimeDiagnostic(
            CharacterTestGuidanceRuntimeDiagnosticCode code,
            string message,
            bool isError)
        {
            Code = code;
            Message = message ?? string.Empty;
            IsError = isError;
        }

        internal static CharacterTestGuidanceRuntimeDiagnostic None => default;
        internal CharacterTestGuidanceRuntimeDiagnosticCode Code { get; }
        internal string Message { get; }
        internal bool IsError { get; }
        internal bool IsDisabled => Code == CharacterTestGuidanceRuntimeDiagnosticCode.AuthorDisabled;

        internal static CharacterTestGuidanceRuntimeDiagnostic Error(
            CharacterTestGuidanceRuntimeDiagnosticCode code,
            string message)
        {
            return new CharacterTestGuidanceRuntimeDiagnostic(code, message, true);
        }

        internal static CharacterTestGuidanceRuntimeDiagnostic Disabled(string message)
        {
            return new CharacterTestGuidanceRuntimeDiagnostic(
                CharacterTestGuidanceRuntimeDiagnosticCode.AuthorDisabled,
                message,
                false);
        }
    }

    /// <summary>
    /// Scene-owned immutable startup input for the CharacterTest guidance sequence.
    /// The Level Editor is the only writer; runtime never reads Assets/Config directly.
    /// </summary>
    [Serializable]
    internal sealed class CharacterTestGuidanceSceneCache
    {
        internal const int CurrentCacheVersion = 1;

        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _moduleSchemaVersion =
            CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion;
        [SerializeField] private int _cacheVersion = CurrentCacheVersion;
        [SerializeField] private int _stepCount;
        [SerializeField] private string _guidanceModuleSourceHash = string.Empty;
        [SerializeField] private string _completionCatalogHash = string.Empty;
        [SerializeField, TextArea(4, 18)]
        private string _canonicalGuidanceModuleJson = string.Empty;

        internal bool Enabled => _enabled;
        internal int ModuleSchemaVersion => _moduleSchemaVersion;
        internal int CacheVersion => _cacheVersion;
        internal int StepCount => _stepCount;
        internal string GuidanceModuleSourceHash => _guidanceModuleSourceHash ?? string.Empty;
        internal string CompletionCatalogHash => _completionCatalogHash ?? string.Empty;
        internal string CanonicalGuidanceModuleJson => _canonicalGuidanceModuleJson ?? string.Empty;

        internal bool HasSerializedInput =>
            !string.IsNullOrWhiteSpace(CanonicalGuidanceModuleJson)
            && !string.IsNullOrWhiteSpace(GuidanceModuleSourceHash);

        internal void Configure(CharacterTestGuidancePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            _enabled = plan.Enabled;
            _moduleSchemaVersion = CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion;
            _cacheVersion = CurrentCacheVersion;
            _stepCount = plan.StepCount;
            _guidanceModuleSourceHash = plan.SourceHash;
            _completionCatalogHash = plan.CompletionCatalogHash;
            _canonicalGuidanceModuleJson = plan.CanonicalModuleJson;
        }

        internal CharacterTestGuidanceSceneCache Clone()
        {
            return new CharacterTestGuidanceSceneCache
            {
                _enabled = _enabled,
                _moduleSchemaVersion = _moduleSchemaVersion,
                _cacheVersion = _cacheVersion,
                _stepCount = _stepCount,
                _guidanceModuleSourceHash = GuidanceModuleSourceHash,
                _completionCatalogHash = CompletionCatalogHash,
                _canonicalGuidanceModuleJson = CanonicalGuidanceModuleJson
            };
        }

        internal bool TryCompilePlan(
            out CharacterTestGuidancePlan plan,
            out CharacterTestGuidanceRuntimeDiagnostic diagnostic)
        {
            plan = null;
            if (_cacheVersion != CurrentCacheVersion
                || _moduleSchemaVersion != CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion)
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.CacheSchemaUnsupported,
                    "Guidance scene cache schema is not supported.");
                return false;
            }
            if (!HasSerializedInput)
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.CacheMissing,
                    "Guidance scene cache has no compiled module input.");
                return false;
            }
            if (!string.Equals(
                    CompletionCatalogHash,
                    CharacterTestGuidanceCompletionRegistry.CatalogHash,
                    StringComparison.Ordinal))
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.CompletionCatalogHashMismatch,
                    "Guidance completion catalog hash does not match this player build.");
                return false;
            }
            string actualHash = CharacterTestGuidanceCanonicalJson.ComputeHash(
                CanonicalGuidanceModuleJson);
            if (!string.Equals(actualHash, GuidanceModuleSourceHash, StringComparison.Ordinal))
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.CacheSourceHashMismatch,
                    "Guidance scene cache source hash does not match its canonical module JSON.");
                return false;
            }

            try
            {
                JObject module = JObject.Parse(CanonicalGuidanceModuleJson);
                if (!string.Equals(
                        module.Value<string>("moduleId"),
                        CharacterTestGuidanceAuthoringAdapter.ModuleId,
                        StringComparison.Ordinal)
                    || module.Value<int?>("schemaVersion")
                        != CharacterTestGuidanceAuthoringAdapter.ModuleSchemaVersion)
                {
                    diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                        CharacterTestGuidanceRuntimeDiagnosticCode.ModuleSchemaUnsupported,
                        "Guidance cache contains a different module identity or schema.");
                    return false;
                }
                CharacterTestGuidanceAuthoringDocument document =
                    module["payload"]?.ToObject<CharacterTestGuidanceAuthoringDocument>();
                if (!CharacterTestGuidanceCompiler.TryCompile(
                        document,
                        out plan,
                        out CharacterTestGuidanceAuthoringReport report))
                {
                    diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                        CharacterTestGuidanceRuntimeDiagnosticCode.ModuleInvalid,
                        "Guidance cache payload is invalid. Errors=" + report.ErrorCount + ".");
                    return false;
                }
                if (!string.Equals(plan.SourceHash, GuidanceModuleSourceHash, StringComparison.Ordinal)
                    || plan.StepCount != _stepCount
                    || plan.Enabled != _enabled)
                {
                    plan = null;
                    diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                        CharacterTestGuidanceRuntimeDiagnosticCode.CacheSourceHashMismatch,
                        "Guidance scene cache metadata does not match the compiled payload.");
                    return false;
                }
                if (!plan.Enabled)
                {
                    diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Disabled(
                        "Guidance was disabled by level authoring.");
                    return false;
                }
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.None;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = CharacterTestGuidanceRuntimeDiagnostic.Error(
                    CharacterTestGuidanceRuntimeDiagnosticCode.ModuleInvalid,
                    "Guidance scene cache cannot be read: " + exception.Message);
                plan = null;
                return false;
            }
        }
    }
}
