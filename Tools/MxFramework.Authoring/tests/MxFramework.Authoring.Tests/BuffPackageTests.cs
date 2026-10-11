using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;
using CliProgram = MxFramework.Authoring.Cli.Program;

namespace MxFramework.Authoring.Tests;

internal static class BuffPackageTests
{
    public static void RunAll()
    {
        BuffAuthoringCompiler_GuardAuraSample_CompilesRuntimeCatalogs();
        BuffAuthoringCompiler_LocalizedPresentationAndAutoDescription();
        BuffAutoDescriptionFormatter_LocalizedTemplates();
        BuffAuthoringCompiler_Types_BlockAndNormalize();
        BuffAuthoringCompiler_InvalidDefinitions_BlockValidation();
        BuffAuthoringDiffBuilder_ChangedFieldAndCleanRevert();
        BuffPackageCommands_Compile_WritesArtifacts();
        BuffPackageCommands_ImportUnityDryRun_ReportsArtifacts();
        BuffEditorServer_Options_ReturnsDesignerMetadata();
        BuffEditorServer_DescriptionPreview_ReturnsLocalizedDraft();
        BuffEditorServer_ProjectPackage_DiscoverLoadValidateCompile();
        BuffEditorServer_Validate_ReturnsMissingBadgeMetadataWarnings();
        BuffEditorServer_Validate_ReturnsUndeclaredNameKeyWarning();
        BuffEditorServer_Validate_ReturnsAuthoringDiff();
        BuffEditorServer_Save_BlocksInvalidDocumentWithoutWriting();
        BuffEditorServer_CompileDraftDoesNotWriteGeneratedFiles();
        BuffEditorServer_Compile_ReturnsGeneratedDiffWithoutWriting();
        BuffEditorServer_ImportUsesSavedDocumentOnly();
        BuffEditorServer_Save_NormalizesLegacySourceWithoutSemanticDiff();
        BuffEditorServer_Save_ValidDocumentWritesSource();
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void BuffAuthoringCompiler_GuardAuraSample_CompilesRuntimeCatalogs()
    {
        string packagePath = FindSamplePath("buff-character-test");
        string documentPath = BuffAuthoringCompiler.ResolveDocumentPath(packagePath);
        BuffAuthoringDocument document = JsonSerializer.Deserialize<BuffAuthoringDocument>(
            File.ReadAllText(documentPath),
            JsonOptions);

        BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(document);

        Require(!result.ValidationReport.HasBlockingIssues, "Guard Aura buff package should validate.");
        Require(result.BuffDefinitions.Definitions.Count == 4, "Guard Aura sample should compile Guard Aura, on-hit, rooted, and Stasis buff definitions.");
        BuffDefinitionConfig buff = result.BuffDefinitions.Definitions[0];
        Require(buff.BuffId == 601008, "Guard Aura buff id should compile.");
        Require(buff.StableId == "buff.character_test.guard_aura.defense", "Guard Aura stable id should compile.");
        Require(buff.Types == null || buff.Types.Count == 0, "Runtime buff definitions should not serialize authoring-only types.");
        Require(buff.DefaultDurationFrames == 2, "Guard Aura lease duration should compile.");
        Require(buff.MaxStackCount == 1, "Guard Aura max stack should compile.");
        Require(buff.ApplyStackCount == 1, "Guard Aura apply stack should compile.");
        Require(buff.StackPolicy == "RefreshDuration", "Guard Aura stack policy should compile.");
        Require(buff.Effects.Count == 1, "Guard Aura should compile one effect.");
        Require(buff.Effects[0].Kind == "AttributeModifier", "Guard Aura effect kind should compile.");
        Require(buff.Effects[0].AttributeId == 3, "Guard Aura defense attribute id should compile.");
        Require(buff.Effects[0].AddValue == 5, "Guard Aura defense bonus should compile.");
        BuffDefinitionConfig onHitBuff = result.BuffDefinitions.Definitions[1];
        Require(onHitBuff.BuffId == 601009, "On-hit defense break buff id should compile.");
        Require(onHitBuff.StableId == "buff.character_test.on_hit.defense_break", "On-hit defense break stable id should compile.");
        Require(onHitBuff.DefaultDurationFrames == 300, "On-hit defense break duration should compile.");
        Require(onHitBuff.Effects.Count == 1, "On-hit defense break should compile one effect.");
        Require(onHitBuff.Effects[0].Kind == "AttributeModifier", "On-hit defense break effect kind should compile.");
        Require(onHitBuff.Effects[0].AttributeId == 3, "On-hit defense break defense attribute id should compile.");
        Require(onHitBuff.Effects[0].AddValue == -3, "On-hit defense break penalty should compile.");
        BuffDefinitionConfig rootedBuff = result.BuffDefinitions.Definitions[2];
        Require(rootedBuff.BuffId == 601010, "Rooted buff id should compile.");
        Require(rootedBuff.StableId == "buff.character_test.area_confinement.rooted", "Rooted stable id should compile.");
        Require(rootedBuff.DefaultDurationFrames == 2, "Rooted duration should compile.");
        Require(rootedBuff.Effects.Count == 1, "Rooted should compile one effect.");
        Require(rootedBuff.Effects[0].Kind == "Status", "Rooted status effect kind should compile.");
        Require(rootedBuff.Effects[0].StatusId == 901010, "Rooted status id should compile.");
        BuffDefinitionConfig stasisBuff = result.BuffDefinitions.Definitions[3];
        Require(stasisBuff.BuffId == 601011, "Stasis buff id should compile.");
        Require(stasisBuff.StableId == "buff.character_test.stasis.action_stop", "Stasis stable id should compile.");
        Require(stasisBuff.DisplayName == "Stasis", "Stasis display name fallback should compile.");
        Require(stasisBuff.DefaultDurationFrames == 90, "Stasis duration should compile.");
        Require(stasisBuff.MaxStackCount == 1, "Stasis max stack should compile.");
        Require(stasisBuff.StackPolicy == "RefreshDuration", "Stasis stack policy should compile.");
        Require(stasisBuff.Effects.Count == 1, "Stasis should compile one effect.");
        Require(stasisBuff.Effects[0].Kind == "Status", "Stasis status effect kind should compile.");
        Require(stasisBuff.Effects[0].StatusId == 901011, "Stasis status id should compile.");

        Require(result.AuraBuffDefinitions.Definitions.Count == 1, "Guard Aura sample should compile one aura definition.");
        AuraBuffDefinitionConfig aura = result.AuraBuffDefinitions.Definitions[0];
        Require(aura.AuraId == 501008, "Guard Aura aura id should compile.");
        Require(aura.BuffId == 601008, "Guard Aura aura buff id should compile.");
        Require(Math.Abs(aura.RadiusMeters - 4f) < 0.0001f, "Guard Aura radius should compile.");
        Require(aura.LayerMask == 256, "Guard Aura dynamic character mask should compile.");
        Require(aura.RelationFilter == "SameTeam", "Guard Aura relation should compile.");
        Require(aura.IncludeCaster, "Guard Aura should include caster.");

        Require(result.BuffPresentationDefinitions.Definitions.Count == 4, "Guard Aura sample should compile buff presentation definitions.");
        BuffPresentationDefinitionConfig guardPresentation =
            result.BuffPresentationDefinitions.Definitions[0];
        Require(guardPresentation.BuffId == 601008, "Guard Aura presentation buff id should compile.");
        Require(guardPresentation.NameKey == "buff.character_test.guard_aura.defense.name", "Guard Aura presentation name key should compile.");
        Require(guardPresentation.ShortName == "DEF+", "Guard Aura presentation short name should compile.");
        Require(guardPresentation.DescriptionMode == "Auto", "Guard Aura presentation should compile Auto description mode.");
        Require(guardPresentation.Description == "防御+5，瞬时，不可叠加", "Guard Aura presentation should compile generated auto description.");
        Require(guardPresentation.Polarity == "Buff", "Guard Aura presentation polarity should be inferred from buff types.");
        Require(guardPresentation.ShowOnPlayerHud, "Guard Aura should show on player HUD.");
        Require(guardPresentation.ShowOnTargetHud, "Guard Aura should show on target HUD.");
        Require(guardPresentation.ShowOnHeadPad, "Guard Aura should show on head pad.");
        Require(guardPresentation.EffectSummary == "Defense(3) +5", "Guard Aura presentation should summarize effects.");
        BuffPresentationDefinitionConfig onHitPresentation =
            result.BuffPresentationDefinitions.Definitions[1];
        Require(onHitPresentation.BuffId == 601009, "On-hit defense break presentation buff id should compile.");
        Require(onHitPresentation.NameKey == "buff.character_test.on_hit.defense_break.name", "On-hit defense break presentation name key should compile.");
        Require(onHitPresentation.ShortName == "DEF-", "On-hit defense break presentation short name should compile.");
        Require(onHitPresentation.DescriptionMode == "Auto", "On-hit defense break presentation should compile Auto description mode.");
        Require(onHitPresentation.Description == "防御-3，持续5秒，不可叠加", "On-hit defense break should compile generated auto description.");
        Require(onHitPresentation.Polarity == "Debuff", "On-hit defense break presentation polarity should compile.");
        Require(!onHitPresentation.ShowOnPlayerHud, "On-hit defense break should not show on player HUD by default.");
        Require(onHitPresentation.ShowOnTargetHud, "On-hit defense break should show on target HUD.");
        Require(onHitPresentation.ShowOnHeadPad, "On-hit defense break should show on head pad.");
        Require(onHitPresentation.EffectSummary == "Defense(3) -3", "On-hit defense break should summarize effects.");
        BuffPresentationDefinitionConfig rootedPresentation =
            result.BuffPresentationDefinitions.Definitions[2];
        Require(rootedPresentation.BuffId == 601010, "Rooted presentation buff id should compile.");
        Require(rootedPresentation.NameKey == "buff.character_test.area_confinement.rooted.name", "Rooted presentation name key should compile.");
        Require(rootedPresentation.ShortName == "ROOT", "Rooted presentation short name should compile.");
        Require(rootedPresentation.DescriptionMode == "Manual", "Rooted presentation should compile Manual description mode.");
        Require(rootedPresentation.Description == "Active movement locked; rotation and external motion remain available.", "Rooted manual description should compile.");
        Require(rootedPresentation.Polarity == "Debuff", "Rooted presentation polarity should compile.");
        Require(!rootedPresentation.ShowOnPlayerHud, "Rooted should not show on player HUD by default.");
        Require(rootedPresentation.ShowOnTargetHud, "Rooted should show on target HUD.");
        Require(rootedPresentation.ShowOnHeadPad, "Rooted should show on head pad.");
        Require(rootedPresentation.EffectSummary == "Status(901010)", "Rooted presentation should summarize effects.");
        BuffPresentationDefinitionConfig stasisPresentation =
            result.BuffPresentationDefinitions.Definitions[3];
        Require(stasisPresentation.BuffId == 601011, "Stasis presentation buff id should compile.");
        Require(stasisPresentation.NameKey == "buff.control.stasis.name", "Stasis presentation name key should compile.");
        Require(stasisPresentation.ShortName == "STOP", "Stasis presentation short name should compile.");
        Require(stasisPresentation.DescriptionMode == "Manual", "Stasis presentation should compile Manual description mode.");
        Require(stasisPresentation.Description == "Self-driven action, movement, and facing pause; external motion remains active.", "Stasis manual description should compile.");
        Require(stasisPresentation.Polarity == "Debuff", "Stasis presentation polarity should compile.");
        Require(stasisPresentation.ShowOnPlayerHud, "Stasis should show on player HUD.");
        Require(stasisPresentation.ShowOnTargetHud, "Stasis should show on target HUD.");
        Require(stasisPresentation.ShowOnHeadPad, "Stasis should show on head pad.");
        Require(stasisPresentation.EffectSummary == "Status(901011)", "Stasis presentation should summarize effects.");
    }

    private static void BuffAuthoringCompiler_LocalizedPresentationAndAutoDescription()
    {
        BuffAuthoringDocument autoDocument = CreateValidDocument("Auto Guard");
        BuffDefinitionConfig autoBuff = autoDocument.Buffs[0];
        autoBuff.Presentation.DescriptionMode = "Auto";
        autoBuff.Presentation.Description = "Legacy manual description.";

        BuffAuthoringCompileResult autoResult = BuffAuthoringCompiler.Compile(autoDocument);
        BuffPresentationDefinitionConfig autoPresentation = autoResult.BuffPresentationDefinitions.Definitions[0];
        Require(!autoResult.ValidationReport.HasBlockingIssues, "Auto description document should validate.");
        Require(autoPresentation.NameKey == "buff.test.guard.name", "Generated presentation should include nameKey.");
        Require(autoPresentation.DisplayName == "Auto Guard", "Generated presentation should keep displayName fallback.");
        Require(autoPresentation.DescriptionMode == "Auto", "Generated presentation should include Auto descriptionMode.");
        Require(autoPresentation.Description == "防御+2，持续0.2秒，不可叠加", "Auto description should be generated from Buff config.");
        Require(autoPresentation.Description != "Legacy manual description.", "Auto description should not copy legacy manual text.");
        string buffJson = JsonSerializer.Serialize(autoResult.BuffDefinitions, JsonOptions);
        Require(!buffJson.Contains("descriptionMode", StringComparison.Ordinal), "Buff definitions should not gain presentation description schema.");
        string presentationJson = JsonSerializer.Serialize(autoResult.BuffPresentationDefinitions, JsonOptions);
        Require(presentationJson.Contains("\"nameKey\"", StringComparison.Ordinal), "Presentation definitions should serialize nameKey.");
        Require(presentationJson.Contains("\"descriptionMode\": \"Auto\"", StringComparison.Ordinal), "Presentation definitions should serialize descriptionMode.");

        string english = BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "en-US", 60);
        Require(english == "Defense +2, lasts 0.2s, not stackable", "English auto description should format attribute, duration, and stacking.");

        autoBuff.Effects[0].AddValue = -34;
        autoBuff.DefaultDurationFrames = 540;
        Require(
            BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "en-US", 60)
                == "Defense -34, lasts 9s, not stackable",
            "English auto description should format negative values and whole seconds.");

        autoBuff.Effects[0].AddValue = 2;
        autoBuff.DefaultDurationFrames = 2;
        Require(
            BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "zh-CN", 60)
                == "防御+2，瞬时，不可叠加",
            "Auto description should format instant buffs.");

        autoBuff.DefaultDurationFrames = 60;
        autoBuff.StackPolicy = "Stack";
        autoBuff.MaxStackCount = 3;
        Require(
            BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "zh-CN", 60)
                == "防御+2，持续1秒，最多3层",
            "Auto description should format stackable buffs.");

        autoBuff.StackPolicy = "RefreshDuration";
        autoBuff.MaxStackCount = 1;
        autoBuff.IsPermanent = true;
        Require(
            BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "zh-CN", 60)
                == "防御+2，永久，不可叠加",
            "Auto description should format permanent buffs.");

        autoBuff.IsPermanent = false;
        autoBuff.Effects[0].Kind = "Status";
        autoBuff.Effects[0].StatusId = 88;
        Require(
            BuffAuthoringCompiler.FormatAutoDescription(autoBuff, "zh-CN", 60)
                == "状态 88，持续1秒，不可叠加",
            "Auto description should format Status fallback.");

        BuffAuthoringDocument manualDocument = CreateValidDocument("Manual Guard");
        manualDocument.Buffs[0].Presentation.DescriptionMode = "Manual";
        manualDocument.Buffs[0].Presentation.Description = "Manual guard description.";
        BuffPresentationDefinitionConfig manualPresentation =
            BuffAuthoringCompiler.Compile(manualDocument).BuffPresentationDefinitions.Definitions[0];
        Require(manualPresentation.DescriptionMode == "Manual", "Manual description mode should compile.");
        Require(manualPresentation.Description == "Manual guard description.", "Manual mode should preserve handwritten description.");

        BuffAuthoringDocument missingMetadata = CreateValidDocument("Missing Metadata");
        missingMetadata.Buffs[0].Presentation.NameKey = "";
        missingMetadata.Buffs[0].Presentation.IconResourceKey = "";
        BuffAuthoringValidationReport missingReport = BuffAuthoringCompiler.Validate(missingMetadata);
        Require(!missingReport.HasBlockingIssues, "Missing badge metadata should not block validation.");
        Require(missingReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_NAME_KEY_MISSING"), "Missing nameKey should warn.");
        Require(missingReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_ICON_MISSING"), "Missing iconResourceKey should warn.");

        BuffAuthoringDocument invalidMode = CreateValidDocument("Invalid Mode");
        invalidMode.Buffs[0].Presentation.DescriptionMode = "GeneratedMaybe";
        BuffAuthoringValidationReport invalidModeReport = BuffAuthoringCompiler.Validate(invalidMode);
        Require(invalidModeReport.HasBlockingIssues, "Invalid descriptionMode should block validation.");
        Require(invalidModeReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_DESCRIPTION_MODE_INVALID"), "Invalid descriptionMode should be diagnosed.");
    }

    private static void BuffAuthoringCompiler_Types_BlockAndNormalize()
    {
        BuffAuthoringDocument missing = CreateValidDocument("Missing Types");
        missing.Buffs[0].Types.Clear();
        BuffAuthoringValidationReport missingReport = BuffAuthoringCompiler.Validate(missing);
        Require(missingReport.HasBlockingIssues, "Missing Buff types should block validation.");
        Require(missingReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_TYPES_REQUIRED"), "Missing Buff types should be diagnosed.");

        BuffAuthoringDocument unknown = CreateValidDocument("Unknown Types");
        unknown.Buffs[0].Types.Clear();
        unknown.Buffs[0].Types.Add("mystery");
        BuffAuthoringValidationReport unknownReport = BuffAuthoringCompiler.Validate(unknown);
        Require(unknownReport.HasBlockingIssues, "Unknown Buff types should block validation.");
        Require(unknownReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_TYPE_INVALID"), "Unknown Buff type should be diagnosed.");

        BuffAuthoringDocument ordered = CreateValidDocument("Ordered Types");
        ordered.Buffs[0].Types.Clear();
        ordered.Buffs[0].Types.Add("damageReduction");
        ordered.Buffs[0].Types.Add("empower");
        ordered.Buffs[0].Types.Add("empower");
        BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(ordered);
        Require(!result.ValidationReport.HasBlockingIssues, "Normalized Buff types should validate.");
        Require(ordered.Buffs[0].Types.Count == 2, "Buff types should be deduplicated.");
        Require(ordered.Buffs[0].Types[0] == "empower", "Buff types should be sorted by fixed vocabulary.");
        Require(ordered.Buffs[0].Types[1] == "damageReduction", "Buff types should keep fixed vocabulary order.");
        Require(result.BuffPresentationDefinitions.Definitions[0].Polarity == "Buff", "Positive Buff types should compile to Buff polarity.");
        string buffJson = JsonSerializer.Serialize(result.BuffDefinitions, JsonOptions);
        Require(!buffJson.Contains("\"types\"", StringComparison.Ordinal), "Runtime buff definitions should not serialize authoring types.");
    }

    private static void BuffAutoDescriptionFormatter_LocalizedTemplates()
    {
        BuffAuthoringDocument document = CreateValidDocument("Template Guard");
        BuffDefinitionConfig buff = document.Buffs[0];
        buff.Presentation.DescriptionMode = "Auto";
        buff.Effects[0].AddValue = -34;
        buff.DefaultDurationFrames = 540;

        var texts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["buff.attribute.3"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "防御",
                ["en-US"] = "Defense"
            },
            ["buff.auto_description.effect.attribute_modifier"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "{attribute}{value}",
                ["en-US"] = "{attribute} {value}"
            },
            ["buff.auto_description.duration.timed"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "持续{seconds}秒",
                ["en-US"] = "lasts {seconds}s"
            },
            ["buff.auto_description.stack.single"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "不可叠加",
                ["en-US"] = "not stackable"
            },
            ["buff.auto_description.stack.stacking"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "最多{maxStack}层",
                ["en-US"] = "up to {maxStack} stacks"
            },
            ["buff.auto_description.duration.instant"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "瞬时",
                ["en-US"] = "instant"
            },
            ["buff.auto_description.duration.permanent"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "永久",
                ["en-US"] = "permanent"
            },
            ["buff.auto_description.separator"] = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zh-CN"] = "，",
                ["en-US"] = ", "
            }
        };

        bool Resolver(string key, string localeId, out string text)
        {
            text = string.Empty;
            return texts.TryGetValue(key, out System.Collections.Generic.Dictionary<string, string> table)
                && table.TryGetValue(localeId, out text);
        }

        Require(
            BuffAutoDescriptionFormatter.Format(buff, "zh-CN", 60, Resolver)
                == "防御-34，持续9秒，不可叠加",
            "Localized template formatter should format zh-CN negative value duration.");
        Require(
            BuffAutoDescriptionFormatter.Format(buff, "en-US", 60, Resolver)
                == "Defense -34, lasts 9s, not stackable",
            "Localized template formatter should format en-US negative value duration.");

        buff.DefaultDurationFrames = 60;
        buff.StackPolicy = "Stack";
        buff.MaxStackCount = 3;
        Require(
            BuffAutoDescriptionFormatter.Format(buff, "zh-CN", 60, Resolver)
                == "防御-34，持续1秒，最多3层",
            "Localized template formatter should format stackable buffs.");

        buff.StackPolicy = "RefreshDuration";
        buff.MaxStackCount = 1;
        buff.Effects[0].Kind = "Status";
        buff.Effects[0].StatusId = 88;
        Require(
            BuffAutoDescriptionFormatter.Format(buff, "en-US", 60, Resolver)
                == "Status 88, lasts 1s, not stackable",
            "Localized template formatter should fallback missing status labels.");
    }

    private static void BuffAuthoringCompiler_InvalidDefinitions_BlockValidation()
    {
        var document = new BuffAuthoringDocument
        {
            PackageId = "invalid.buff"
        };
        document.Buffs.Add(new BuffDefinitionConfig
        {
            BuffId = 7,
            StableId = "",
            DefaultDurationFrames = 0,
            MaxStackCount = 0,
            ApplyStackCount = 2,
            StackPolicy = "RefreshForever",
            Presentation = new BuffPresentationConfig
            {
                DescriptionMode = "GeneratedMaybe"
            }
        });
        document.Buffs[0].Types.Add("mystery");
        document.Buffs[0].Effects.Add(new BuffEffectConfig
        {
            EffectId = 0,
            Kind = "MissingKind"
        });
        document.Buffs[0].Effects.Add(new BuffEffectConfig
        {
            EffectId = 1,
            Kind = "AttributeModifier",
            AttributeId = 0,
            AddValue = 5
        });
        document.Buffs[0].Effects.Add(new BuffEffectConfig
        {
            EffectId = 1,
            Kind = "Status",
            StatusId = 0
        });
        document.Buffs.Add(new BuffDefinitionConfig
        {
            BuffId = 7,
            StableId = "buff.duplicate",
            DefaultDurationFrames = 1,
            MaxStackCount = 1,
            ApplyStackCount = 2,
            Types = new() { "empower" }
        });
        document.Buffs.Add(new BuffDefinitionConfig
        {
            BuffId = 8,
            StableId = "buff.duplicate",
            DefaultDurationFrames = 1,
            Types = new() { "weaken" }
        });
        document.Auras.Add(new AuraBuffDefinitionConfig
        {
            AuraId = 10,
            BuffId = 999,
            RadiusMeters = 0f,
            LayerMask = -1,
            RelationFilter = "Allies"
        });
        document.Auras.Add(new AuraBuffDefinitionConfig
        {
            AuraId = 10,
            BuffId = 7,
            RadiusMeters = 1f,
            LayerMask = 1,
            RelationFilter = "Any"
        });

        BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);

        Require(report.HasBlockingIssues, "Invalid buff package should block validation.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_STABLE_ID_REQUIRED"), "Missing buff stable id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_ID_DUPLICATE"), "Duplicate buff id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_STABLE_ID_DUPLICATE"), "Duplicate buff stable id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_DURATION_INVALID"), "Invalid buff duration should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_MAX_STACK_INVALID"), "Invalid max stack should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_APPLY_STACK_INVALID"), "Invalid apply stack should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_STACK_POLICY_INVALID"), "Invalid stack policy should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_TYPE_INVALID"), "Invalid Buff type should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_DESCRIPTION_MODE_INVALID"), "Invalid presentation description mode should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_EFFECT_ID_INVALID"), "Invalid effect id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_EFFECT_KIND_INVALID"), "Invalid effect kind should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_EFFECT_ID_DUPLICATE"), "Duplicate effect id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_EFFECT_ATTRIBUTE_INVALID"), "Invalid attribute modifier should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_EFFECT_STATUS_INVALID"), "Invalid status effect should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_AURA_ID_DUPLICATE"), "Duplicate aura id should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_AURA_BUFF_UNKNOWN"), "Unknown aura buff should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_AURA_RADIUS_INVALID"), "Invalid aura radius should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_AURA_LAYER_MASK_INVALID"), "Invalid aura layer mask should be diagnosed.");
        Require(report.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_AURA_RELATION_INVALID"), "Invalid aura relation should be diagnosed.");
    }

    private static void BuffAuthoringDiffBuilder_ChangedFieldAndCleanRevert()
    {
        BuffAuthoringDocument saved = CreateValidDocument("Saved Guard");
        BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");

        BuffAuthoringDiffReport diff = BuffAuthoringDiffBuilder.BuildAuthoringDiff(saved, draft);

        Require(diff.HasChanges, "Changed draft should produce authoring diff.");
        Require(diff.ChangeCount == 1, "Display name change should be one authoring diff entry.");
        Require(diff.Groups.Exists(group => group.Changes.Exists(change =>
            change.Path == "buffs[0].displayName" &&
            change.BeforeValue == "Saved Guard" &&
            change.AfterValue == "Draft Guard")), "Authoring diff should include changed displayName.");

        draft.Buffs[0].DisplayName = "Saved Guard";
        BuffAuthoringDiffReport clean = BuffAuthoringDiffBuilder.BuildAuthoringDiff(saved, draft);
        Require(!clean.HasChanges, "Reverted draft should produce clean authoring diff.");
        Require(clean.ChangeCount == 0, "Clean authoring diff should have zero changes.");
    }

    private static void BuffPackageCommands_Compile_WritesArtifacts()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "mx-authoring-buff-" + Guid.NewGuid().ToString("N"));
        try
        {
            int exit = BuffPackageCommands.Dispatch(new[]
            {
                "buff",
                "compile",
                "--package",
                FindSamplePath("buff-character-test"),
                "--out",
                outDir
            }, CliProgram.CreateJsonOptions());

            Require(exit == CliProgram.ExitReady, "Buff compile command should return ready.");
            Require(File.Exists(Path.Combine(outDir, "buff_compile_result.json")), "Buff compile should write compile result.");
            Require(File.Exists(Path.Combine(outDir, "buff_definitions.json")), "Buff compile should write buff definitions.");
            Require(File.Exists(Path.Combine(outDir, "buff_definitions.bytes")), "Buff compile should write buff definition bytes.");
            Require(File.Exists(Path.Combine(outDir, "aura_buff_definitions.json")), "Buff compile should write aura definitions.");
            Require(File.Exists(Path.Combine(outDir, "aura_buff_definitions.bytes")), "Buff compile should write aura definition bytes.");
            Require(File.Exists(Path.Combine(outDir, "buff_presentation_definitions.json")), "Buff compile should write presentation definitions.");
            Require(File.Exists(Path.Combine(outDir, "buff_presentation_definitions.bytes")), "Buff compile should write presentation definition bytes.");
            string buffJson = File.ReadAllText(Path.Combine(outDir, "buff_definitions.json"));
            Require(buffJson.Contains("buff.character_test.guard_aura.defense", StringComparison.Ordinal), "Buff output should contain Guard Aura stable id.");
            Require(buffJson.Contains("\"addValue\": 5", StringComparison.Ordinal), "Buff output should contain Defense bonus.");
            Require(buffJson.Contains("buff.character_test.on_hit.defense_break", StringComparison.Ordinal), "Buff output should contain on-hit defense break stable id.");
            Require(buffJson.Contains("\"addValue\": -3", StringComparison.Ordinal), "Buff output should contain on-hit Defense penalty.");
            Require(!buffJson.Contains("\"presentation\"", StringComparison.Ordinal), "Runtime buff definitions should not embed presentation config.");
            string auraJson = File.ReadAllText(Path.Combine(outDir, "aura_buff_definitions.json"));
            Require(auraJson.Contains("\"auraId\": 501008", StringComparison.Ordinal), "Aura output should contain Guard Aura id.");
            Require(auraJson.Contains("\"layerMask\": 256", StringComparison.Ordinal), "Aura output should contain dynamic character mask.");
            string presentationJson = File.ReadAllText(Path.Combine(outDir, "buff_presentation_definitions.json"));
            Require(presentationJson.Contains("\"shortName\": \"DEF+\"", StringComparison.Ordinal), "Presentation output should contain Guard Aura short name.");
            Require(presentationJson.Contains("\"polarity\": \"Buff\"", StringComparison.Ordinal), "Presentation output should contain inferred Buff polarity.");
            Require(presentationJson.Contains("\"polarity\": \"Debuff\"", StringComparison.Ordinal), "Presentation output should contain debuff polarity.");
            Require(presentationJson.Contains("\"effectSummary\": \"Defense(3) -3\"", StringComparison.Ordinal), "Presentation output should contain generated effect summary.");
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    private static void BuffPackageCommands_ImportUnityDryRun_ReportsArtifacts()
    {
        string reportDir = Path.Combine(Path.GetTempPath(), "mx-authoring-buff-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            int exit = BuffPackageCommands.Dispatch(new[]
            {
                "buff",
                "import-unity",
                "--package",
                FindSamplePath("buff-character-test"),
                "--project-root",
                FindRepoRoot(),
                "--dry-run",
                "--report-out",
                reportDir
            }, CliProgram.CreateJsonOptions());

            Require(exit == CliProgram.ExitReady, "Buff import-unity dry-run should return ready.");
            string reportPath = Path.Combine(reportDir, "buff_import_report.json");
            Require(File.Exists(reportPath), "Buff import-unity dry-run should write a report.");
            string reportJson = File.ReadAllText(reportPath);
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json", StringComparison.Ordinal), "Buff import-unity report should include buff definitions.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.bytes", StringComparison.Ordinal), "Buff import-unity report should include buff definition bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.json", StringComparison.Ordinal), "Buff import-unity report should include presentation definitions.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_presentation_definitions.bytes", StringComparison.Ordinal), "Buff import-unity report should include presentation definition bytes.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.json", StringComparison.Ordinal), "Buff import-unity report should include aura definitions.");
            Require(reportJson.Contains("Assets/MxFrameworkGenerated/Combat/Auras/aura_buff_definitions.bytes", StringComparison.Ordinal), "Buff import-unity report should include aura definition bytes.");
        }
        finally
        {
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir, recursive: true);
        }
    }

    private static void BuffEditorServer_Options_ReturnsDesignerMetadata()
    {
        EditorServer.BuffAuthoringOptionsResponse options = EditorServer.ReadBuffAuthoringOptions();

        Require(options.DurationFrameRate == 60, "Buff designer options should expose 60fps duration conversion.");
        Require(options.InstantFrameRateBase == 30, "Buff designer options should expose 30fps instant baseline.");
        Require(options.InstantFrames == 2, "Buff designer options should expose 2-frame instant duration at 60fps.");
        Require(Array.Exists(options.DurationKinds, item => item.Id == "instant"), "Buff designer options should expose instant duration kind.");
        Require(Array.Exists(options.DurationKinds, item => item.Id == "duration"), "Buff designer options should expose duration kind.");
        Require(Array.Exists(options.DurationKinds, item => item.Id == "permanent"), "Buff designer options should expose permanent duration kind.");
        Require(Array.Exists(options.StackingModes, item => item.Id == "single"), "Buff designer options should expose single stacking mode.");
        Require(Array.Exists(options.StackingModes, item => item.Id == "stacking"), "Buff designer options should expose stacking mode.");
        Require(Array.Exists(options.DescriptionModes, item => item.Id == "Auto"), "Buff designer options should expose Auto description mode.");
        Require(Array.Exists(options.DescriptionModes, item => item.Id == "Manual"), "Buff designer options should expose Manual description mode.");
        Require(Array.Exists(options.BuffTypes, item => item.Id == "damageReduction" && item.Label == "减伤"), "Buff designer options should expose damage reduction Buff type.");
        Require(Array.Exists(options.Locales, item => item.Id == "zh-CN"), "Buff designer options should expose zh-CN locale.");
        Require(Array.Exists(options.Locales, item => item.Id == "en-US"), "Buff designer options should expose en-US locale.");
        Require(Array.Exists(options.Attributes, item => item.AttributeId == 3 && item.DisplayName == "Defense"), "Buff designer options should expose Defense attribute.");
        Require(Array.Exists(options.StackPolicies, item => item.Id == "RefreshDuration"), "Buff designer options should expose RefreshDuration stack policy.");
        Require(Array.Exists(options.Relations, item => item.Id == "SameTeam"), "Buff designer options should expose SameTeam relation.");
        Require(Array.Exists(options.AuraLayerMasks, item => item.Value == 256), "Buff designer options should expose CharacterTest aura layer preset.");
        Require(Array.Exists(options.Fields, item => item.Field == "displayName" && item.Label == "Fallback 显示名称"), "Buff designer options should expose fallback display name field label.");
        Require(Array.Exists(options.Fields, item => item.Field == "nameKey" && item.Label == "名称多语言 Key"), "Buff designer options should expose nameKey field label.");
        Require(Array.Exists(options.Fields, item => item.Field == "descriptionMode" && item.Label == "描述来源"), "Buff designer options should expose descriptionMode field label.");
        Require(Array.Exists(options.Fields, item => item.Field == "types" && item.Label == "Buff 类型"), "Buff designer options should expose Buff type field label.");
        Require(Array.Exists(options.Fields, item => item.Field == "durationKind" && item.Label == "时间类型"), "Buff designer options should expose duration kind field label.");

        BuffAuthoringDocument instantDocument = CreateValidDocument("Instant Guard");
        instantDocument.Buffs[0].DefaultDurationFrames = options.InstantFrames;
        instantDocument.Buffs[0].IsPermanent = false;
        BuffAuthoringCompileResult compileResult = BuffAuthoringCompiler.Compile(instantDocument);
        Require(!compileResult.ValidationReport.HasBlockingIssues, "Instant duration mapping should remain valid Buff v2 authoring.");
        string buffJson = JsonSerializer.Serialize(compileResult.BuffDefinitions, JsonOptions);
        Require(buffJson.Contains("\"defaultDurationFrames\": 2", StringComparison.Ordinal), "Instant duration mapping should compile to defaultDurationFrames.");
        Require(buffJson.Contains("\"isPermanent\": false", StringComparison.Ordinal), "Instant duration mapping should compile to non-permanent runtime config.");
            Require(!buffJson.Contains("durationKind", StringComparison.Ordinal), "Instant duration kind should not add a runtime schema field.");
            Require(!buffJson.Contains("\"types\"", StringComparison.Ordinal), "Buff types should not add a runtime definition schema field.");

        BuffAuthoringDocument singleStackDocument = CreateValidDocument("Single Guard");
        singleStackDocument.Buffs[0].StackPolicy = "RefreshDuration";
        singleStackDocument.Buffs[0].MaxStackCount = 1;
        singleStackDocument.Buffs[0].ApplyStackCount = 1;
        BuffAuthoringCompileResult singleStackResult = BuffAuthoringCompiler.Compile(singleStackDocument);
        Require(!singleStackResult.ValidationReport.HasBlockingIssues, "Single stacking mapping should remain valid Buff v2 authoring.");
        BuffDefinitionConfig singleStackBuff = singleStackResult.BuffDefinitions.Definitions[0];
        Require(singleStackBuff.StackPolicy == "RefreshDuration", "Single stacking mapping should compile to RefreshDuration.");
        Require(singleStackBuff.MaxStackCount == 1, "Single stacking mapping should compile maxStackCount=1.");
        Require(singleStackBuff.ApplyStackCount == 1, "Single stacking mapping should compile applyStackCount=1.");

        BuffAuthoringDocument stackingDocument = CreateValidDocument("Stacking Guard");
        stackingDocument.Buffs[0].StackPolicy = "Stack";
        stackingDocument.Buffs[0].MaxStackCount = 3;
        stackingDocument.Buffs[0].ApplyStackCount = 1;
        BuffAuthoringCompileResult stackingResult = BuffAuthoringCompiler.Compile(stackingDocument);
        Require(!stackingResult.ValidationReport.HasBlockingIssues, "Stacking mapping should remain valid Buff v2 authoring.");
        BuffDefinitionConfig stackingBuff = stackingResult.BuffDefinitions.Definitions[0];
        Require(stackingBuff.StackPolicy == "Stack", "Stacking mapping should compile to Stack.");
        Require(stackingBuff.MaxStackCount == 3, "Stacking mapping should compile edited maxStackCount.");
        Require(stackingBuff.ApplyStackCount == 1, "Stacking mapping should keep applyStackCount=1.");
        string stackingJson = JsonSerializer.Serialize(stackingResult.BuffDefinitions, JsonOptions);
        Require(!stackingJson.Contains("stackingMode", StringComparison.Ordinal), "Stacking mode should not add a runtime schema field.");
    }

    private static void BuffEditorServer_DescriptionPreview_ReturnsLocalizedDraft()
    {
        string root = CreateTempRoot();
        try
        {
            WriteCharacterTestLocalization(root);
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");
            draft.Buffs[0].Presentation.DescriptionMode = "Auto";
            draft.Buffs[0].Presentation.Description = "Manual text should not appear.";
            draft.Buffs[0].Effects[0].AddValue = -34;
            draft.Buffs[0].DefaultDurationFrames = 540;
            string documentPath = Path.Combine(root, package, "config", "buff_authoring.json");
            string before = File.ReadAllText(documentPath);

            EditorServer.BuffAuthoringDescriptionPreviewResponse zh =
                EditorServer.PreviewBuffAutoDescriptions(
                    root,
                    new EditorServer.BuffAuthoringDescriptionPreviewRequest
                    {
                        Package = package,
                        Locale = "zh-CN",
                        Document = draft
                    },
                    JsonOptions);
            EditorServer.BuffAuthoringDescriptionPreviewResponse en =
                EditorServer.PreviewBuffAutoDescriptions(
                    root,
                    new EditorServer.BuffAuthoringDescriptionPreviewRequest
                    {
                        Package = package,
                        Locale = "en-US",
                        Document = draft
                    },
                    JsonOptions);

            Require(zh.Success, "Description preview should succeed.");
            Require(zh.Locale == "zh-CN", "Description preview should keep requested zh-CN locale.");
            Require(Array.Exists(zh.Locales, item => item.Id == "zh-CN"), "Description preview should expose zh-CN locale.");
            Require(Array.Exists(zh.Locales, item => item.Id == "en-US"), "Description preview should expose en-US locale.");
            Require(zh.Buffs.Length == 1, "Description preview should return one Buff preview.");
            Require(zh.Buffs[0].Description == "防御-34，持续9秒，不可叠加", "Description preview should use zh-CN templates.");
            Require(en.Buffs[0].Description == "Defense -34, lasts 9s, not stackable", "Description preview should use en-US templates.");
            Require(!zh.Buffs[0].Description.Contains("Manual text", StringComparison.Ordinal), "Auto description preview should not use manual description text.");
            Require(File.ReadAllText(documentPath) == before, "Description preview should not write source authoring JSON.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Validate_ReturnsMissingBadgeMetadataWarnings()
    {
        string root = CreateTempRoot();
        try
        {
            BuffAuthoringDocument document = CreateValidDocument("Missing Badge Metadata");
            document.Buffs[0].Presentation.NameKey = "";
            document.Buffs[0].Presentation.IconResourceKey = "";
            string package = WriteProjectBuffPackage(root, "guard", document);

            EditorServer.BuffAuthoringValidationResponse response =
                EditorServer.ValidateBuffAuthoringPackage(root, package, document, JsonOptions);

            Require(response.Success, "Missing badge metadata warnings should not block validation.");
            Require(!response.ValidationReport.HasBlockingIssues, "Warnings should not set blocking state.");
            Require(response.ValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_NAME_KEY_MISSING"), "Missing nameKey should be returned by server validation.");
            Require(response.ValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Code == "BUFF_PRESENTATION_ICON_MISSING"), "Missing iconResourceKey should be returned by server validation.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Validate_ReturnsUndeclaredNameKeyWarning()
    {
        string root = CreateTempRoot();
        try
        {
            WriteCharacterTestLocalization(root);
            BuffAuthoringDocument document = CreateValidDocument("Missing Localized Name");
            document.Buffs[0].Presentation.NameKey = "buff.test.guard.missing.name";
            string package = WriteProjectBuffPackage(root, "guard", document);

            EditorServer.BuffAuthoringValidationResponse response =
                EditorServer.ValidateBuffAuthoringPackage(root, package, document, JsonOptions);

            Require(response.Success, "Undeclared nameKey warning should not block validation.");
            Require(!response.ValidationReport.HasBlockingIssues, "Undeclared nameKey warning should not set blocking state.");
            Require(
                response.ValidationReport.Diagnostics.Exists(diagnostic =>
                    diagnostic.Code == "BUFF_PRESENTATION_NAME_KEY_UNDECLARED"
                    && diagnostic.Path == "buffs[0].presentation.nameKey"),
                "Server validation should warn when nameKey is absent from Localization TSV.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_ProjectPackage_DiscoverLoadValidateCompile()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));

            var packages = EditorServer.ListBuffAuthoringPackages(root, JsonOptions);
            Require(packages.Exists(item => item.Relative == package), "Buff package discovery should include project package.");

            EditorServer.BuffAuthoringPackageState state =
                EditorServer.ReadBuffAuthoringState(root, package, JsonOptions);
            Require(state.Success, "Buff load should succeed.");
            Require(state.PackageId == "test.guard", "Buff load should expose package id.");
            Require(state.Document.Buffs.Count == 1, "Buff load should expose authoring document.");
            Require(!string.IsNullOrWhiteSpace(state.SavedDocumentHash), "Buff load should expose saved document hash.");
            Require(!string.IsNullOrWhiteSpace(state.DraftRevision), "Buff load should expose draft revision.");
            Require(!state.ValidationReport.HasBlockingIssues, "Valid project Buff package should validate.");
            Require(!state.AuthoringDiff.HasChanges, "Freshly loaded Buff package should have clean authoring diff.");
            Require(!state.HasSourceChanges, "Freshly loaded normalized Buff package should not need a source rewrite.");
            Require(state.ImportPlan.Length == 3, "Buff load should expose generated import plan.");

            EditorServer.BuffAuthoringValidationResponse validation =
                EditorServer.ValidateBuffAuthoringPackage(root, package, state.Document, JsonOptions);
            Require(validation.Success, "Draft validation should succeed.");
            Require(!validation.HasSourceChanges, "Clean draft validation should not need a source rewrite.");

            EditorServer.BuffAuthoringCompileResponse compile =
                EditorServer.CompileBuffAuthoringPackage(root, package, state.Document, JsonOptions);
            Require(compile.Success, "Draft compile should succeed.");
            Require(compile.CompileResult.BuffDefinitions.Definitions.Count == 1, "Compile should produce one Buff definition.");
            Require(compile.CompileResult.AuraBuffDefinitions.Definitions.Count == 1, "Compile should produce one Aura definition.");
            Require(compile.GeneratedDiff.HasChanges, "Compile should report generated diff when generated files are missing.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Validate_ReturnsAuthoringDiff()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");

            EditorServer.BuffAuthoringValidationResponse response =
                EditorServer.ValidateBuffAuthoringPackage(root, package, draft, JsonOptions);

            Require(response.Success, "Changed valid Buff draft should validate.");
            Require(!string.IsNullOrWhiteSpace(response.SavedDocumentHash), "Validation should return saved document hash.");
            Require(!string.IsNullOrWhiteSpace(response.DraftRevision), "Validation should return draft revision.");
            Require(response.AuthoringDiff.HasChanges, "Validation should return authoring diff.");
            Require(response.AuthoringDiff.Groups.Exists(group => group.Changes.Exists(change =>
                change.Path == "buffs[0].displayName" &&
                change.BeforeValue == "Saved Guard" &&
                change.AfterValue == "Draft Guard")), "Validation authoring diff should include displayName change.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Save_BlocksInvalidDocumentWithoutWriting()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            string documentPath = Path.Combine(root, package, "config", "buff_authoring.json");
            string before = File.ReadAllText(documentPath);
            BuffAuthoringDocument invalid = CreateValidDocument("Invalid Guard");
            invalid.Buffs[0].DefaultDurationFrames = 0;

            EditorServer.BuffAuthoringSaveResponse response =
                EditorServer.SaveBuffAuthoringPackage(root, package, invalid, JsonOptions);

            Require(!response.Success, "Invalid Buff save should fail.");
            Require(!response.Saved, "Invalid Buff save should not write.");
            Require(response.ValidationReport.HasBlockingIssues, "Invalid Buff save should return blocking diagnostics.");
            Require(response.AuthoringDiff.HasChanges, "Invalid Buff save should still return authoring diff.");
            Require(response.ValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Path == "buffs[0].defaultDurationFrames"), "Invalid Buff save should return field-mappable diagnostic path.");
            Require(File.ReadAllText(documentPath) == before, "Invalid Buff save should preserve source file.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_CompileDraftDoesNotWriteGeneratedFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");

            EditorServer.BuffAuthoringCompileResponse response =
                EditorServer.CompileBuffAuthoringPackage(root, package, draft, JsonOptions);

            Require(response.Success, "Draft Buff compile should succeed.");
            Require(response.GeneratedDiff.HasChanges, "Draft compile should report generated diff when generated files are missing.");
            Require(!File.Exists(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Gameplay", "Buffs", "buff_definitions.json")), "Draft compile should not write buff_definitions.");
            Require(!File.Exists(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Gameplay", "Buffs", "buff_presentation_definitions.json")), "Draft compile should not write buff presentation definitions.");
            Require(!File.Exists(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Combat", "Auras", "aura_buff_definitions.json")), "Draft compile should not write aura definitions.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Compile_ReturnsGeneratedDiffWithoutWriting()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringCompileResult current = BuffAuthoringCompiler.Compile(CreateValidDocument("Saved Guard"));
            WriteGeneratedBuffCatalogs(root, current);
            string generatedPath = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Gameplay", "Buffs", "buff_definitions.json");
            string before = File.ReadAllText(generatedPath);

            BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");
            EditorServer.BuffAuthoringCompileResponse response =
                EditorServer.CompileBuffAuthoringPackage(root, package, draft, JsonOptions);

            Require(response.Success, "Generated diff compile should succeed.");
            Require(response.GeneratedDiff.HasChanges, "Compile should report generated semantic diff.");
            Require(response.GeneratedDiff.Groups.Exists(group => group.ArtifactKind == "BuffDefinitions" && group.Changes.Exists(change =>
                change.Path == "definitions[0].displayName" &&
                change.BeforeValue == "Saved Guard" &&
                change.AfterValue == "Draft Guard")), "Generated diff should include runtime buff displayName change.");
            Require(File.ReadAllText(generatedPath) == before, "Compile generated diff should not write generated files.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_ImportUsesSavedDocumentOnly()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringDocument draft = CreateValidDocument("Draft Guard");
            EditorServer.CompileBuffAuthoringPackage(root, package, draft, JsonOptions);

            EditorServer.BuffAuthoringImportResponse response =
                EditorServer.ImportSavedBuffAuthoringPackage(root, package, JsonOptions);

            Require(response.Success, "Saved Buff import should succeed.");
            Require(response.Imported, "Saved Buff import should write generated files.");
            string generated = File.ReadAllText(Path.Combine(root, "Assets", "MxFrameworkGenerated", "Gameplay", "Buffs", "buff_definitions.json"));
            Require(generated.Contains("Saved Guard", StringComparison.Ordinal), "Import should use saved document.");
            Require(!generated.Contains("Draft Guard", StringComparison.Ordinal), "Import should not use unsaved draft compile data.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Save_ValidDocumentWritesSource()
    {
        string root = CreateTempRoot();
        try
        {
            string package = WriteProjectBuffPackage(root, "guard", CreateValidDocument("Saved Guard"));
            BuffAuthoringDocument updated = CreateValidDocument("Updated Guard");
            updated.Buffs[0].Presentation.Polarity = "Debuff";

            EditorServer.BuffAuthoringSaveResponse response =
                EditorServer.SaveBuffAuthoringPackage(root, package, updated, JsonOptions);

            Require(response.Success, "Valid Buff save should succeed.");
            Require(response.Saved, "Valid Buff save should write source.");
            Require(!string.IsNullOrWhiteSpace(response.SavedDocumentHash), "Valid Buff save should return saved document hash.");
            Require(!string.IsNullOrWhiteSpace(response.DraftRevision), "Valid Buff save should return draft revision.");
            Require(!response.AuthoringDiff.HasChanges, "Valid Buff save should return clean authoring diff.");
            string source = File.ReadAllText(Path.Combine(root, package, "config", "buff_authoring.json"));
            Require(source.Contains("Updated Guard", StringComparison.Ordinal), "Valid Buff save should persist source document.");
            Require(source.Contains("\"types\"", StringComparison.Ordinal), "Valid Buff save should persist Buff types.");
            Require(!source.Contains("\"polarity\"", StringComparison.Ordinal), "Valid Buff save should remove legacy source presentation polarity.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void BuffEditorServer_Save_NormalizesLegacySourceWithoutSemanticDiff()
    {
        string root = CreateTempRoot();
        try
        {
            BuffAuthoringDocument legacy = CreateValidDocument("Legacy Guard");
            legacy.Buffs[0].Presentation.Polarity = "Aura";
            string package = WriteProjectBuffPackage(root, "guard", legacy);
            string sourcePath = Path.Combine(root, package, "config", "buff_authoring.json");
            Require(File.ReadAllText(sourcePath).Contains("\"polarity\"", StringComparison.Ordinal), "Legacy source fixture should contain presentation polarity.");

            EditorServer.BuffAuthoringPackageState state =
                EditorServer.ReadBuffAuthoringState(root, package, JsonOptions);

            Require(state.Success, "Legacy Buff load should succeed.");
            Require(!state.ValidationReport.HasBlockingIssues, "Legacy Buff source should normalize to a valid draft.");
            Require(!state.AuthoringDiff.HasChanges, "Legacy source normalization should not create semantic authoring diff.");
            Require(state.HasSourceChanges, "Legacy source normalization should report a pending source rewrite.");
            Require(state.Document.Buffs[0].Presentation.Polarity == null, "Legacy polarity should be removed from the normalized draft.");

            EditorServer.BuffAuthoringSaveResponse response =
                EditorServer.SaveBuffAuthoringPackage(root, package, state.Document, JsonOptions);

            Require(response.Success, "Normalized legacy Buff save should succeed.");
            Require(response.Saved, "Normalized legacy Buff save should write source.");
            Require(!response.HasSourceChanges, "Saved normalized legacy Buff should be clean.");
            Require(!File.ReadAllText(sourcePath).Contains("\"polarity\"", StringComparison.Ordinal), "Saved legacy Buff source should remove presentation polarity.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string FindSamplePath(string sampleName)
    {
        string root = FindRepoRoot();
        return Path.Combine(root, "Tools", "MxFramework.Authoring", "samples", sampleName);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Tools")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-authoring-buff-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string WriteProjectBuffPackage(string root, string packageName, BuffAuthoringDocument document)
    {
        string relative = Path.Combine("Authoring", "BuffPackages", packageName).Replace('\\', '/');
        string configDir = Path.Combine(root, relative, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(
            Path.Combine(configDir, "buff_authoring.json"),
            JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine);
        return relative;
    }

    private static void WriteCharacterTestLocalization(string root)
    {
        string directory = Path.Combine(root, "Assets", "StreamingAssets", "MxFramework", "Localization");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "character_test.tsv"),
            string.Join(
                Environment.NewLine,
                new[]
                {
                    "key\tzh-CN\ten-US",
                    "locale.zh-CN.label\t简体中文\tChinese (Simplified)",
                    "locale.en-US.label\tEnglish\tEnglish",
                    "buff.test.guard.name\t测试守护\tTest Guard",
                    "buff.attribute.1\t生命\tHP",
                    "buff.attribute.2\t攻击\tAttack",
                    "buff.attribute.3\t防御\tDefense",
                    "buff.attribute.4\t韧性\tPosture",
                    "buff.auto_description.effect.attribute_modifier\t{attribute}{value}\t{attribute} {value}",
                    "buff.auto_description.effect.status\t{status}\t{status}",
                    "buff.auto_description.effect.unknown\t效果 {effectId}\tEffect {effectId}",
                    "buff.auto_description.effect.none\t无效果\tNo effect",
                    "buff.auto_description.duration.instant\t瞬时\tinstant",
                    "buff.auto_description.duration.timed\t持续{seconds}秒\tlasts {seconds}s",
                    "buff.auto_description.duration.permanent\t永久\tpermanent",
                    "buff.auto_description.stack.single\t不可叠加\tnot stackable",
                    "buff.auto_description.stack.stacking\t最多{maxStack}层\tup to {maxStack} stacks",
                    "buff.auto_description.separator\t，\t,{space}"
                })
            + Environment.NewLine);
    }

    private static void WriteGeneratedBuffCatalogs(string root, BuffAuthoringCompileResult result)
    {
        string buffDir = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Gameplay", "Buffs");
        string auraDir = Path.Combine(root, "Assets", "MxFrameworkGenerated", "Combat", "Auras");
        Directory.CreateDirectory(buffDir);
        Directory.CreateDirectory(auraDir);
        File.WriteAllText(Path.Combine(buffDir, "buff_definitions.json"), JsonSerializer.Serialize(result.BuffDefinitions, JsonOptions) + Environment.NewLine);
        File.WriteAllText(Path.Combine(buffDir, "buff_presentation_definitions.json"), JsonSerializer.Serialize(result.BuffPresentationDefinitions, JsonOptions) + Environment.NewLine);
        File.WriteAllText(Path.Combine(auraDir, "aura_buff_definitions.json"), JsonSerializer.Serialize(result.AuraBuffDefinitions, JsonOptions) + Environment.NewLine);
    }

    private static BuffAuthoringDocument CreateValidDocument(string displayName)
    {
        var document = new BuffAuthoringDocument
        {
            SchemaVersion = "1",
            PackageId = "test.guard"
        };
        var buff = new BuffDefinitionConfig
        {
            BuffId = 700001,
            StableId = "buff.test.guard",
            DisplayName = displayName,
            Types = new() { "empower", "damageReduction" },
            DefaultDurationFrames = 12,
            MaxStackCount = 2,
            ApplyStackCount = 1,
            StackPolicy = "RefreshDuration",
            IsPermanent = false,
            Presentation = new BuffPresentationConfig
            {
                NameKey = "buff.test.guard.name",
                ShortName = "GRD",
                DescriptionMode = "Manual",
                Description = "Test guard buff.",
                IconResourceKey = "icon.buff.test.guard",
                Priority = 100,
                ShowOnPlayerHud = true,
                ShowOnTargetHud = true,
                ShowOnHeadPad = false
            }
        };
        buff.Effects.Add(new BuffEffectConfig
        {
            EffectId = 1,
            Kind = "AttributeModifier",
            AttributeId = 3,
            AddValue = 2
        });
        document.Buffs.Add(buff);
        document.Auras.Add(new AuraBuffDefinitionConfig
        {
            AuraId = 710001,
            BuffId = 700001,
            RadiusMeters = 3f,
            LayerMask = 256,
            RelationFilter = "SameTeam",
            IncludeCaster = true
        });
        return document;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
