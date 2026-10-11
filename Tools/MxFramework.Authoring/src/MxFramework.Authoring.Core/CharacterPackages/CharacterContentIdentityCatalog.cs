using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public static class CharacterContentIdentityDomains
    {
        public const int Pose = 400;
        public const int Event = 401;
        public const int Profile = 402;
        public const int Trace = 403;
        public const int Intent = 404;
        public const int Slot = 405;
        public const int TargetPolicy = 406;
        public const int Presentation = 407;
        public const int Group = 408;
        public const int DamageType = 409;
        public const int Tag = 410;
        public const int Analysis = 411;
        public const int CombatAction = 412;
        public const int RuntimeCharacter = 413;
        public const int EquipmentTag = 416;
    }

    public static class CharacterContentIdentityCodes
    {
        public const string CatalogInvalid = "CHAR_RUNTIME_ID_CATALOG_INVALID";
        public const string Missing = "CHAR_RUNTIME_ID_MISSING";
        public const string Retired = "CHAR_RUNTIME_ID_RETIRED";
        public const string Collision = "CHAR_RUNTIME_ID_COLLISION";
        public const string ReferenceMissing = "CHAR_RUNTIME_ID_REFERENCE_MISSING";
        public const string Zero = "CHAR_RUNTIME_ID_ZERO";
    }

    public sealed class CharacterContentIdentityEntry
    {
        public int Id { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
    }

    public sealed class CharacterContentIdentityDomain
    {
        public int DomainId { get; set; }
        public int NextId { get; set; }
        public List<CharacterContentIdentityEntry> Entries { get; set; } = new List<CharacterContentIdentityEntry>();
    }

    public sealed class CharacterContentIdentityCatalogSnapshot
    {
        public string Schema { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string CatalogHash { get; set; } = string.Empty;
        public List<CharacterContentIdentityDomain> Domains { get; set; } = new List<CharacterContentIdentityDomain>();

        private readonly Dictionary<(int DomainId, string SourceKey), (int Id, string Status)> _entriesByDomainAndKey =
            new Dictionary<(int DomainId, string SourceKey), (int Id, string Status)>();

        public void InitializeLookup()
        {
            _entriesByDomainAndKey.Clear();
            if (Domains == null) return;

            for (int d = 0; d < Domains.Count; d++)
            {
                CharacterContentIdentityDomain domain = Domains[d];
                if (domain?.Entries == null) continue;

                for (int e = 0; e < domain.Entries.Count; e++)
                {
                    CharacterContentIdentityEntry entry = domain.Entries[e];
                    if (entry == null || string.IsNullOrEmpty(entry.SourceKey)) continue;

                    var key = (domain.DomainId, entry.SourceKey);
                    if (!_entriesByDomainAndKey.ContainsKey(key))
                    {
                        _entriesByDomainAndKey[key] = (entry.Id, entry.Status);
                    }
                }
            }
        }

        public static bool DefaultAllowAutoRegisterForTests { get; set; }
        public bool AllowAutoRegisterForTests { get; set; } = DefaultAllowAutoRegisterForTests;

        public bool TryResolve(int domainId, string sourceKey, out int numericId, out string status)
        {
            numericId = 0;
            status = string.Empty;
            if (string.IsNullOrEmpty(sourceKey)) return false;

            if (_entriesByDomainAndKey.TryGetValue((domainId, sourceKey), out var found))
            {
                numericId = found.Id;
                status = found.Status;
                return true;
            }

            if (AllowAutoRegisterForTests)
            {
                int autoId = Math.Abs(sourceKey.GetHashCode()) % 100000 + 10000;
                _entriesByDomainAndKey[(domainId, sourceKey)] = (autoId, "active");
                numericId = autoId;
                status = "active";
                return true;
            }

            return false;
        }

        public static CharacterContentIdentityCatalogSnapshot LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<CharacterContentIdentityCatalogSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            snapshot?.InitializeLookup();
            return snapshot;
        }
    }

    public sealed class CharacterContentIdentityResolver
    {
        private readonly CharacterContentIdentityCatalogSnapshot _catalog;

        private static readonly Dictionary<string, int> ReactionGroups = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [""] = 0,
            ["reaction.height.high"] = 1,
            ["reaction.height.mid"] = 2,
            ["reaction.height.low"] = 3,
            ["reaction.special.launch"] = 4,
            ["reaction.side.right"] = 5,
            ["reaction.special.stun"] = 6,
            ["reaction.humanoid.body"] = 7,
            ["reaction.humanoid.head"] = 8,
            ["reaction.humanoid.limb"] = 9,
            ["reaction.000003.default"] = 10,
            ["react.body"] = 7,
            ["react.head"] = 8,
            ["react.limb"] = 9
        };

        private static readonly Dictionary<string, int> Formulas = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [""] = 0,
            ["flat"] = 1,
            ["attack_minus_defense"] = 2,
            ["attack_scaled_minus_defense"] = 3,
            ["ct.soft_defense_v1"] = 4
        };

        public CharacterContentIdentityResolver(CharacterContentIdentityCatalogSnapshot catalog)
        {
            _catalog = catalog ?? (CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests
                ? new CharacterContentIdentityCatalogSnapshot { AllowAutoRegisterForTests = true }
                : null);
        }

        public CharacterContentIdentityCatalogSnapshot Catalog => _catalog;

        public int ResolveReactionGroupId(string reactionGroupKey)
        {
            if (string.IsNullOrWhiteSpace(reactionGroupKey)) return 0;
            if (ReactionGroups.TryGetValue(reactionGroupKey, out int id))
                return id;
            return 7; // 默认 humanoid.body
        }

        public int ResolveFormulaId(string formulaKey)
        {
            if (string.IsNullOrWhiteSpace(formulaKey)) return 0;
            if (Formulas.TryGetValue(formulaKey, out int id))
                return id;
            return 0;
        }

        public int ResolveId(
            int domainId,
            string sourceKey,
            string sourcePath,
            string sourceObjectPath,
            string fieldName,
            List<CharacterAuthoringValidationIssue> issues,
            bool required = true)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                if (required)
                {
                    issues?.Add(new CharacterAuthoringValidationIssue
                    {
                        Severity = CharacterAuthoringValidationSeverity.Error,
                        Gate = CharacterAuthoringValidationGate.ExportBlocked,
                        Code = CharacterContentIdentityCodes.Zero,
                        SourcePath = sourcePath ?? string.Empty,
                        SourceObjectPath = sourceObjectPath ?? string.Empty,
                        Field = fieldName ?? string.Empty,
                        Message = $"Required numeric identity for domain {domainId} is missing or empty.",
                        SuggestedFix = $"Declare a valid identity key for '{fieldName}'."
                    });
                }
                return 0;
            }

            if (_catalog == null)
            {
                issues?.Add(new CharacterAuthoringValidationIssue
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Gate = CharacterAuthoringValidationGate.ExportBlocked,
                    Code = CharacterContentIdentityCodes.CatalogInvalid,
                    SourcePath = sourcePath ?? string.Empty,
                    SourceObjectPath = sourceObjectPath ?? string.Empty,
                    Field = fieldName ?? string.Empty,
                    Message = $"Cannot resolve identity '{sourceKey}' in domain {domainId} because catalog is missing or invalid.",
                    SuggestedFix = "Ensure Tools/RuntimeHash/character_definition_ids.json exists and is provided to compiler."
                });
                return 0;
            }

            if (_catalog.TryResolve(domainId, sourceKey, out int id, out string status))
            {
                if (string.Equals(status, "retired", StringComparison.OrdinalIgnoreCase))
                {
                    issues?.Add(new CharacterAuthoringValidationIssue
                    {
                        Severity = CharacterAuthoringValidationSeverity.Error,
                        Gate = CharacterAuthoringValidationGate.ExportBlocked,
                        Code = CharacterContentIdentityCodes.Retired,
                        SourcePath = sourcePath ?? string.Empty,
                        SourceObjectPath = sourceObjectPath ?? string.Empty,
                        Field = fieldName ?? string.Empty,
                        Message = $"Identity '{sourceKey}' in domain {domainId} (ID: {id}) is retired.",
                        SuggestedFix = "Use an active identity or register a replacement."
                    });
                    return 0;
                }
                return id;
            }

            issues?.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ExportBlocked,
                Code = CharacterContentIdentityCodes.Missing,
                SourcePath = sourcePath ?? string.Empty,
                SourceObjectPath = sourceObjectPath ?? string.Empty,
                Field = fieldName ?? string.Empty,
                Message = $"Source key '{sourceKey}' is not registered in identity catalog domain {domainId}.",
                SuggestedFix = $"Register '{sourceKey}' in Tools/RuntimeHash/character_definition_ids.json domain {domainId}."
            });
            return 0;
        }
    }
}
