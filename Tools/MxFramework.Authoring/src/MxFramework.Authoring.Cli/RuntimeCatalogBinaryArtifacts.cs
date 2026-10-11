using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MxFramework.Authoring;

[assembly: InternalsVisibleTo("MxFramework.Authoring.Tests")]

namespace MxFramework.Authoring.Cli;

internal static class RuntimeCatalogBinaryArtifacts
{
    private const int Magic = 0x4D584342; // MXCB
    private const int CurrentFormatVersion = 1;
    private const long Fix64Scale = 1000000L;

    private static class Kinds
    {
        public const string AbilityDefinitions = "mxframework.gameplay.ability.definitions";
        public const string AbilityEffectTimelines = "mxframework.gameplay.ability.effect-timelines";
        public const string GameplayBuffDefinitions = "mxframework.gameplay.buff.definitions";
        public const string GameplayBuffPresentationDefinitions = "mxframework.gameplay.buff.presentations";
        public const string CombatGameplayAuraBuffDefinitions = "mxframework.combat.gameplay.aura-buffs";
        public const string ProjectileDefinitions = "mxframework.combat.projectile.definitions";
        public const string ProjectileSpawnProfiles = "mxframework.combat.projectile.spawn-profiles";
        public const string ProjectilePresentationBindings = "mxframework.combat.projectile.presentation-bindings";
        public const string VfxDefinitions = "mxframework.vfx.definitions";
        public const string VfxTriggerMaps = "mxframework.vfx.trigger-maps";
    }

    public static byte[] WriteAbilityDefinitions(AbilityRuntimeCatalogDocument document)
    {
        List<AbilityDefinitionConfig> values = document?.Abilities ?? new List<AbilityDefinitionConfig>();
        return WriteCatalog(Kinds.AbilityDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteAbilityDefinition(writer, values[i]);
        });
    }

    public static byte[] WriteAbilityEffectTimelines(AbilityEffectTimelineCatalogDocument document)
    {
        List<AbilityEffectTimelineConfig> values = document?.Timelines ?? new List<AbilityEffectTimelineConfig>();
        return WriteCatalog(Kinds.AbilityEffectTimelines, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteAbilityEffectTimeline(writer, values[i]);
        });
    }

    public static byte[] WriteBuffDefinitions(BuffDefinitionCatalogDocument document)
    {
        List<BuffDefinitionConfig> values = document?.Definitions ?? new List<BuffDefinitionConfig>();
        return WriteCatalog(Kinds.GameplayBuffDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteBuffDefinition(writer, values[i]);
        });
    }

    public static byte[] WriteBuffPresentations(BuffPresentationDefinitionCatalogDocument document)
    {
        List<BuffPresentationDefinitionConfig> values =
            document?.Definitions ?? new List<BuffPresentationDefinitionConfig>();
        return WriteCatalog(Kinds.GameplayBuffPresentationDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteBuffPresentation(writer, values[i]);
        });
    }

    public static byte[] WriteAuraBuffDefinitions(AuraBuffDefinitionCatalogDocument document)
    {
        List<AuraBuffDefinitionConfig> values = document?.Definitions ?? new List<AuraBuffDefinitionConfig>();
        return WriteCatalog(Kinds.CombatGameplayAuraBuffDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteAuraBuffDefinition(writer, values[i]);
        });
    }

    public static byte[] WriteVfxDefinitions(AbilityVfxDefinitionCatalogDocument document)
    {
        List<VfxDefinitionConfig> values = document?.Definitions ?? new List<VfxDefinitionConfig>();
        return WriteCatalog(Kinds.VfxDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteVfxDefinition(writer, values[i]);
        });
    }

    public static byte[] WriteVfxTriggerMaps(AbilityVfxTriggerMapDocument document)
    {
        List<VfxTriggerMapConfig> values = document?.Mappings ?? new List<VfxTriggerMapConfig>();
        return WriteCatalog(Kinds.VfxTriggerMaps, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteVfxTriggerMap(writer, values[i]);
        });
    }

    public static byte[] WriteProjectileDefinitions(AbilityProjectileDefinitionCatalogDocument document)
    {
        List<ProjectileDefinitionConfig> values = document?.Definitions ?? new List<ProjectileDefinitionConfig>();
        return WriteCatalog(Kinds.ProjectileDefinitions, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteProjectileDefinition(writer, values[i]);
        });
    }

    public static byte[] WriteProjectileSpawnProfiles(AbilityProjectileSpawnProfileCatalogDocument document)
    {
        List<ProjectileSpawnProfileConfig> values = document?.Profiles ?? new List<ProjectileSpawnProfileConfig>();
        return WriteCatalog(Kinds.ProjectileSpawnProfiles, Schema(document), Package(document), values.Count, writer =>
        {
            for (int i = 0; i < values.Count; i++)
                WriteProjectileSpawnProfile(writer, values[i]);
        });
    }

    public static byte[] WriteProjectilePresentationBindings(AbilityProjectilePresentationBindingDocument document)
    {
        List<ProjectilePresentationBindingConfig> values =
            document?.Bindings ?? new List<ProjectilePresentationBindingConfig>();
        ProjectilePresentationBindingConfig[] ordered = values.ToArray();
        Array.Sort(ordered, static (left, right) => (left?.ProjectileDefinitionId ?? 0).CompareTo(right?.ProjectileDefinitionId ?? 0));
        string schema = Schema(document);
        if (!string.Equals(schema, "1", StringComparison.Ordinal) && !string.Equals(schema, "2", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported projectile presentation schema: " + schema + ".");
        return WriteCatalog(Kinds.ProjectilePresentationBindings, schema, Package(document), ordered.Length, writer =>
        {
            for (int i = 0; i < ordered.Length; i++)
            {
                if (string.Equals(schema, "1", StringComparison.Ordinal)) WriteLegacyProjectilePresentationBinding(writer, ordered[i]);
                else WriteProjectilePresentationBindingV2(writer, ordered[i]);
            }
        });
    }

    internal static bool ValidateProjectileArtifact(byte[] content, string expectedKind, string expectedSchema, int expectedCount)
    {
        if (content == null || content.Length == 0)
            return false;
        try
        {
            using var stream = new MemoryStream(content, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != CurrentFormatVersion)
                return false;
            return string.Equals(reader.ReadString(), expectedKind, StringComparison.Ordinal)
                && string.Equals(reader.ReadString(), expectedSchema, StringComparison.Ordinal)
                && reader.ReadString() != null
                && reader.ReadInt32() == expectedCount
                && stream.Position + sizeof(long) <= stream.Length;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    internal static string ProjectileDefinitionsKind => Kinds.ProjectileDefinitions;
    internal static string ProjectileSpawnProfilesKind => Kinds.ProjectileSpawnProfiles;
    internal static string ProjectilePresentationBindingsKind => Kinds.ProjectilePresentationBindings;

    private static byte[] WriteCatalog(
        string catalogKind,
        string schemaVersion,
        string sourcePackageId,
        int entryCount,
        Action<BinaryWriter> writePayload)
    {
        byte[] payload;
        using (var payloadStream = new MemoryStream())
        {
            using (var payloadWriter = new BinaryWriter(payloadStream))
            {
                writePayload(payloadWriter);
                payloadWriter.Flush();
                payload = payloadStream.ToArray();
            }
        }

        long stableHash = ComputeStableHash(payload, 0, payload.Length);
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(CurrentFormatVersion);
                writer.Write(catalogKind ?? string.Empty);
                writer.Write(schemaVersion ?? string.Empty);
                writer.Write(sourcePackageId ?? string.Empty);
                writer.Write(entryCount < 0 ? 0 : entryCount);
                writer.Write(stableHash);
                writer.Write(payload);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }

    private static void WriteAbilityDefinition(BinaryWriter writer, AbilityDefinitionConfig ability)
    {
        ability ??= new AbilityDefinitionConfig();
        writer.Write(ability.AbilityId);
        writer.Write(ability.AbilityStableId ?? string.Empty);
        writer.Write(ability.DisplayName ?? string.Empty);
        writer.Write(ability.Input?.InputIntentId ?? 0);
        writer.Write(ability.Input?.InputIntentLabel ?? string.Empty);
        writer.Write(ability.Input?.SlotId ?? 0);
        writer.Write(ability.Input?.SlotLabel ?? string.Empty);
        writer.Write(ability.Cooldown?.Frames ?? 0L);
        writer.Write(ability.Cooldown?.GroupId ?? 0);
        writer.Write(ability.Cooldown?.GroupLabel ?? string.Empty);
        WriteAbilityCosts(writer, ability.Costs);
        writer.Write(ability.TargetPolicyId);
        writer.Write(ability.TargetPolicyLabel ?? string.Empty);
        writer.Write(ability.ActionBinding?.ActionId ?? 0);
        writer.Write(ability.ActionBinding?.ActionIntentId ?? 0);
        writer.Write(ability.ActionBinding?.ActionIntentLabel ?? string.Empty);
        writer.Write(ability.Damage != null);
        if (ability.Damage != null)
            WriteHitDamage(writer, ability.Damage, includeSourceScales: true);
        writer.Write(ability.PresentationProfileId);
        writer.Write(ability.PresentationProfileLabel ?? string.Empty);
        writer.Write(ability.EffectTimelineId);
        writer.Write(ability.EffectTimelineLabel ?? string.Empty);
    }

    private static void WriteAbilityCosts(BinaryWriter writer, List<AbilityCostConfig> costs)
    {
        int count = costs?.Count ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            AbilityCostConfig cost = costs[i] ?? new AbilityCostConfig();
            writer.Write(cost.AttributeId);
            writer.Write(cost.Amount);
        }
    }

    private static void WriteAbilityEffectTimeline(BinaryWriter writer, AbilityEffectTimelineConfig timeline)
    {
        timeline ??= new AbilityEffectTimelineConfig();
        writer.Write(timeline.TimelineId);
        writer.Write(timeline.TimelineLabel ?? string.Empty);
        writer.Write(timeline.DurationFrames);
        int count = timeline.Events?.Count ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
            WriteAbilityEffectTimelineEvent(writer, timeline.Events[i]);
    }

    private static void WriteAbilityEffectTimelineEvent(BinaryWriter writer, AbilityEffectTimelineEventConfig evt)
    {
        evt ??= new AbilityEffectTimelineEventConfig();
        writer.Write(evt.EventId);
        writer.Write(evt.EventLabel ?? string.Empty);
        writer.Write(ParseEnum(evt.TrackKind, 1, AbilityEffectTimelineTrackKind));
        writer.Write(ParseEnum(evt.EventKind, 103, AbilityEffectTimelineEventKind));
        writer.Write(evt.Frame);
        writer.Write(evt.EndFrame < 0 ? evt.Frame : evt.EndFrame);
        writer.Write(ParseEnum(evt.ShapeKind, 0, AbilityEffectTimelineShapeKind));
        writer.Write(ParseEnum(evt.AnchorMode, 0, AbilityEffectTimelineAnchorMode));
        WriteVector(writer, evt.LocalOffset, ToTimelineRaw);
        WriteVector(writer, evt.LocalPointB, ToTimelineRaw);
        writer.Write(ToTimelineRaw(evt.Radius));
        writer.Write(evt.LayerMask);
        writer.Write(evt.TargetTimelineId);
        writer.Write(evt.TargetTimelineLabel ?? string.Empty);
        writer.Write(evt.RequestId);
        writer.Write(evt.RequestLabel ?? string.Empty);
        writer.Write(evt.EffectId);
        writer.Write(evt.PresentationProfileId);
        writer.Write(evt.PresentationProfileLabel ?? string.Empty);
        writer.Write(evt.RequiresOwner);
        writer.Write(evt.RequiresSource);
    }

    private static void WriteBuffDefinition(BinaryWriter writer, BuffDefinitionConfig definition)
    {
        definition ??= new BuffDefinitionConfig();
        writer.Write(definition.BuffId);
        writer.Write(definition.StableId ?? string.Empty);
        writer.Write(definition.DefaultDurationFrames);
        writer.Write(definition.MaxStackCount);
        writer.Write(ParseEnum(definition.StackPolicy, 0, BuffStackPolicy));
        int count = definition.Effects?.Count ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
            WriteBuffEffect(writer, definition.Effects[i]);
        writer.Write(definition.ApplyStackCount);
        writer.Write(definition.IsPermanent);
    }

    private static void WriteBuffEffect(BinaryWriter writer, BuffEffectConfig effect)
    {
        effect ??= new BuffEffectConfig();
        writer.Write(effect.EffectId);
        writer.Write(ParseEnum(effect.Kind, 0, BuffEffectKind));
        writer.Write(effect.AttributeId);
        writer.Write(effect.AddValue);
        writer.Write(effect.StatusId);
    }

    private static void WriteBuffPresentation(BinaryWriter writer, BuffPresentationDefinitionConfig definition)
    {
        definition ??= new BuffPresentationDefinitionConfig();
        writer.Write(definition.BuffId);
        writer.Write(definition.DisplayName ?? string.Empty);
        writer.Write(definition.NameKey ?? string.Empty);
        writer.Write(definition.ShortName ?? string.Empty);
        writer.Write(definition.Description ?? string.Empty);
        writer.Write(string.IsNullOrWhiteSpace(definition.DescriptionMode) ? "Manual" : definition.DescriptionMode);
        writer.Write(definition.IconResourceKey ?? string.Empty);
        writer.Write(ParseEnum(definition.Polarity, 0, BuffPresentationPolarity));
        writer.Write(definition.Priority);
        writer.Write(definition.ShowOnPlayerHud);
        writer.Write(definition.ShowOnTargetHud);
        writer.Write(definition.ShowOnHeadPad);
        writer.Write(definition.EffectSummary ?? string.Empty);
    }

    private static void WriteAuraBuffDefinition(BinaryWriter writer, AuraBuffDefinitionConfig definition)
    {
        definition ??= new AuraBuffDefinitionConfig();
        writer.Write(definition.AuraId);
        writer.Write(definition.BuffId);
        writer.Write(ToAuraRaw(definition.RadiusMeters));
        writer.Write(definition.LayerMask);
        writer.Write(ParseEnum(definition.RelationFilter, 0, GameplayRelationFilter));
        writer.Write(definition.IncludeCaster);
    }

    private static void WriteVfxDefinition(BinaryWriter writer, VfxDefinitionConfig definition)
    {
        definition ??= new VfxDefinitionConfig();
        writer.Write(definition.DefinitionId);
        writer.Write(definition.StableId ?? string.Empty);
        WriteResourceKey(writer, definition.PrefabResource);
        writer.Write(definition.DefaultLifetimeFrames);
        writer.Write(definition.DefinitionHash ?? string.Empty);
    }

    private static void WriteVfxTriggerMap(BinaryWriter writer, VfxTriggerMapConfig mapping)
    {
        mapping ??= new VfxTriggerMapConfig();
        writer.Write(ResolveVfxSourceDomain(mapping));
        writer.Write(mapping.TriggerId);
        writer.Write(mapping.DefinitionId);
        writer.Write(mapping.LifetimeFramesOverride);
    }

    private static void WriteProjectileDefinition(BinaryWriter writer, ProjectileDefinitionConfig definition)
    {
        definition ??= new ProjectileDefinitionConfig();
        writer.Write(definition.DefinitionId);
        writer.Write(definition.StableId ?? string.Empty);
        writer.Write(definition.PhysicalEventsEnabled);
        WriteProjectileMotion(writer, definition.Motion);
        WriteProjectileCollision(writer, definition.Collision);
        WriteProjectileImpact(writer, definition.Impact);
    }

    private static void WriteProjectileMotion(BinaryWriter writer, ProjectileMotionConfig motion)
    {
        motion ??= new ProjectileMotionConfig();
        writer.Write(ParseEnum(motion.Kind, 0, ProjectileMotionKind));
        writer.Write(ToProjectileRaw(motion.SpeedPerFrame));
        writer.Write(motion.LifetimeFrames);
        writer.Write(ToProjectileRaw(motion.ArcHeight));
        writer.Write(ParseEnum(motion.SpeedProfile?.Kind, 0, ProjectileSpeedProfileKind));
        writer.Write(ToProjectileRaw(motion.LateralAmplitude));
        writer.Write(motion.LateralCycles);
        WriteVector(writer, motion.BezierControlOffsetA, ToProjectileRaw);
        WriteVector(writer, motion.BezierControlOffsetB, ToProjectileRaw);
        writer.Write(ToProjectileRaw(motion.Homing?.TurnRatePerFrame ?? 1f));
        writer.Write(ToProjectileRaw(motion.GravityPerFrameSquared));
        writer.Write(ToProjectileRaw(motion.MaxLaunchAngleTangent));
    }

    private static void WriteProjectileCollision(BinaryWriter writer, ProjectileCollisionConfig collision)
    {
        collision ??= new ProjectileCollisionConfig();
        writer.Write(ToProjectileRaw(collision.Radius));
        writer.Write(collision.LayerMask);
        writer.Write(collision.StopOnFirstAcceptedImpact);
        writer.Write(collision.StopOnBlockedImpact);
        writer.Write(collision.MaxAcceptedImpacts);
        writer.Write(ParseEnum(collision.HitSameTargetPolicy, 0, ProjectileHitSameTargetPolicy));
    }

    private static void WriteProjectileImpact(BinaryWriter writer, ProjectileImpactConfig impact)
    {
        impact ??= new ProjectileImpactConfig();
        int policyKind = ParseEnum(impact.PolicyKind, 0, ProjectileImpactPolicyKind);
        int effectiveStableEventId = impact.StableEventId > 0
            ? impact.StableEventId
            : impact.HitEvent?.StableEventId ?? 0;
        string effectiveStableEventLabel = !string.IsNullOrWhiteSpace(impact.StableEventLabel)
            ? impact.StableEventLabel
            : impact.HitEvent?.StableEventLabel ?? string.Empty;
        CombatHitDamageConfig inheritedDamage = default;
        byte[] hitEventBytes = null;
        CombatHitDamageConfig hitDamage;
        using (var hitStream = new MemoryStream())
        {
            using (var hitWriter = new BinaryWriter(hitStream))
            {
                WriteHitEvent(hitWriter, impact.HitEvent, effectiveStableEventId, effectiveStableEventLabel, inheritedDamage, out hitDamage);
                hitWriter.Flush();
                hitEventBytes = hitStream.ToArray();
            }
        }

        writer.Write(impact.PresentationProfileId);
        writer.Write(impact.PresentationProfileLabel ?? string.Empty);
        writer.Write(effectiveStableEventId);
        writer.Write(effectiveStableEventLabel);
        writer.Write(policyKind);
        writer.Write(ToProjectileRaw(impact.AreaRadius));
        writer.Write(impact.AreaLayerMask);
        writer.Write(impact.TriggerOnExpire);
        writer.Write(hitEventBytes);
        CombatHitDamageConfig inheritedAreaDamage =
            impact.AreaHitEvent != null || policyKind == 1 ? hitDamage : default;
        WriteHitEvent(writer, impact.AreaHitEvent, effectiveStableEventId, effectiveStableEventLabel, inheritedAreaDamage, out _);
        writer.Write(impact.EffectTimelineId);
        writer.Write(impact.EffectTimelineLabel ?? string.Empty);
    }

    private static void WriteHitEvent(
        BinaryWriter writer,
        CombatHitEventConfig hitEvent,
        int defaultStableEventId,
        string defaultStableEventLabel,
        CombatHitDamageConfig inheritedDamage,
        out CombatHitDamageConfig writtenDamage)
    {
        CombatHitDamageConfig damage = hitEvent?.Damage ?? inheritedDamage;
        writer.Write(ResolveTargetTypes(hitEvent?.TargetTypes));
        writer.Write(ParseEnum(hitEvent?.EnvironmentHitPolicy, 0, CombatEnvironmentHitPolicy));
        writer.Write(hitEvent?.StableEventId > 0 ? hitEvent.StableEventId : defaultStableEventId);
        writer.Write(!string.IsNullOrWhiteSpace(hitEvent?.StableEventLabel)
            ? hitEvent.StableEventLabel
            : defaultStableEventLabel ?? string.Empty);
        writer.Write(Math.Max(1, hitEvent?.MaxAcceptedHits ?? 1));
        writer.Write(ParseEnum(hitEvent?.HitSameTargetPolicy, 0, CombatHitSameTargetPolicy));
        writer.Write(damage != null);
        if (damage != null)
            WriteHitDamage(writer, damage, includeSourceScales: true);
        WriteOnHitBuffs(writer, hitEvent?.OnHitBuffs);
        writtenDamage = damage;
    }

    private static void WriteHitDamage(BinaryWriter writer, CombatHitDamageConfig damage, bool includeSourceScales)
    {
        writer.Write(damage?.DamageTypeId ?? 0);
        writer.Write(damage?.DamageTypeLabel ?? string.Empty);
        writer.Write(damage?.BaseDamage ?? 0);
        writer.Write(damage?.ImpactPower ?? 0);
        WriteIntList(writer, damage?.Tags);
        WriteStringList(writer, damage?.TagLabels);
        writer.Write(damage?.AnalysisCategory ?? 0);
        writer.Write(damage?.AnalysisCategoryLabel ?? string.Empty);
        writer.Write(damage?.LaunchPower ?? 0);
        if (includeSourceScales)
        {
            List<CombatDamageAttributeScaleConfig> scales = damage?.SourceAttributeScales;
            writer.Write(scales?.Count ?? 0);
            for (int i = 0; scales != null && i < scales.Count; i++)
            {
                writer.Write(scales[i]?.AttributeId ?? 0);
                writer.Write(scales[i]?.Scale ?? 0f);
            }
        }
    }

    private static void WriteOnHitBuffs(BinaryWriter writer, List<CombatHitBuffApplicationConfig> buffs)
    {
        int count = buffs?.Count ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
            writer.Write(buffs[i]?.BuffId ?? 0);
    }

    private static void WriteProjectileSpawnProfile(BinaryWriter writer, ProjectileSpawnProfileConfig profile)
    {
        profile ??= new ProjectileSpawnProfileConfig();
        writer.Write(profile.ProfileId);
        writer.Write(profile.StableId ?? string.Empty);
        writer.Write(profile.ProjectileDefinitionId);
        writer.Write(profile.ProjectileStableId ?? string.Empty);
        writer.Write(ParseEnum(profile.Origin?.Kind, 0, ProjectileSpawnOriginKind));
        writer.Write(profile.Origin?.SocketId ?? 0);
        WriteVector(writer, profile.Origin?.LocalOffset, ToProjectileRaw);
        WriteVector(writer, profile.Origin?.FallbackRootOffset, ToProjectileRaw);
        writer.Write(profile.Origin?.SocketLabel ?? string.Empty);
        writer.Write(ParseEnum(profile.Aim?.Kind, 0, ProjectileAimKind));
        writer.Write(ToProjectileRaw(profile.Aim?.TargetHeightOffset ?? 0f));
        writer.Write(ParseEnum(profile.Aim?.FallbackKind, 0, ProjectileAimKind));
        writer.Write(ToProjectileRaw(profile.Aim?.MaxRange ?? 0f));
    }

    private static void WriteLegacyProjectilePresentationBinding(BinaryWriter writer, ProjectilePresentationBindingConfig binding)
    {
        binding ??= new ProjectilePresentationBindingConfig();
        AbilityResourceKeyConfig resource = binding.PrefabResource ?? binding.Slots?.FlightBody?.Resource;
        if (resource == null || string.IsNullOrWhiteSpace(resource.Id))
            throw new InvalidDataException("Legacy projectile presentation binding requires prefabResource.");
        writer.Write(binding.ProjectileDefinitionId);
        WriteResourceKey(writer, resource);
        writer.Write(binding.ViewNamePrefix ?? "ProjectileVFX");
    }

    private static void WriteProjectilePresentationBindingV2(
        BinaryWriter writer,
        ProjectilePresentationBindingConfig binding)
    {
        binding ??= new ProjectilePresentationBindingConfig();
        writer.Write(binding.ProjectileDefinitionId);
        writer.Write(binding.ViewNamePrefix ?? "ProjectileVFX");
        ProjectilePresentationSlotSetConfig slots = binding.Slots ?? new ProjectilePresentationSlotSetConfig();
        int count = CountPresentationSlots(slots);
        writer.Write(count);
        WritePresentationSlot(writer, 0, slots.Spawn, true);
        WritePresentationSlot(writer, 1, slots.FlightBody, false);
        WritePresentationSlot(writer, 2, slots.Trail, false);
        WritePresentationSlot(writer, 3, slots.DirectImpact, true);
        WritePresentationSlot(writer, 4, slots.AreaImpact, true);
        WritePresentationSlot(writer, 5, slots.BlockedImpact, true);
        WritePresentationSlot(writer, 6, slots.Expire, true);
        ProjectilePresentationExitPolicyConfig exit = binding.ExitPolicy ?? new ProjectilePresentationExitPolicyConfig();
        writer.Write(ParsePresentationExit(exit.AcceptedImpact));
        writer.Write(ParsePresentationExit(exit.BlockedImpact));
        writer.Write(ParsePresentationExit(exit.Expiration));
        writer.Write(ParsePresentationExit(exit.ExternalRemoval));
        writer.Write((byte)2);
    }

    private static int CountPresentationSlots(ProjectilePresentationSlotSetConfig slots)
    {
        int count = 0;
        if (slots.Spawn != null) count++;
        if (slots.FlightBody != null) count++;
        if (slots.Trail != null) count++;
        if (slots.DirectImpact != null) count++;
        if (slots.AreaImpact != null) count++;
        if (slots.BlockedImpact != null) count++;
        if (slots.Expire != null) count++;
        return count;
    }

    private static void WritePresentationSlot(BinaryWriter writer, byte kind, ProjectilePresentationSlotConfig slot, bool oneShot)
    {
        if (slot == null)
            return;
        writer.Write(kind);
        WriteResourceKey(writer, slot.Resource);
        writer.Write(ParsePresentationOrientation(slot.Orientation));
        WriteVector(writer, slot.LocalOffset, ToProjectileRaw);
        writer.Write(slot.LifetimeFramesOverride);
        writer.Write(oneShot ? slot.VfxDefinitionId : 0);
    }

    private static byte ParsePresentationOrientation(string value)
    {
        if (string.Equals(value, "ProjectileForward", StringComparison.Ordinal)) return 0;
        if (string.Equals(value, "WorldUp", StringComparison.Ordinal)) return 1;
        if (string.Equals(value, "SurfaceNormal", StringComparison.Ordinal)) return 2;
        if (string.Equals(value, "ImpactNormalOrWorldUp", StringComparison.Ordinal)) return 3;
        throw new InvalidDataException("Unknown projectile presentation orientation: " + (value ?? string.Empty));
    }

    private static byte ParsePresentationExit(string value)
    {
        if (string.Equals(value, "Complete", StringComparison.Ordinal)) return 0;
        if (string.Equals(value, "Dissipate", StringComparison.Ordinal)) return 1;
        throw new InvalidDataException("Unknown projectile presentation exit mode: " + (value ?? string.Empty));
    }

    private static void WriteResourceKey(BinaryWriter writer, AbilityResourceKeyConfig key)
    {
        writer.Write(key?.Id ?? string.Empty);
        writer.Write(key?.TypeId ?? string.Empty);
        writer.Write(key?.Variant ?? string.Empty);
        writer.Write(key?.PackageId ?? string.Empty);
    }

    private static void WriteVector(
        BinaryWriter writer,
        ProjectileVector3Config vector,
        Func<float, long> toRaw)
    {
        WriteRawVector(
            writer,
            toRaw(vector?.X ?? 0f),
            toRaw(vector?.Y ?? 0f),
            toRaw(vector?.Z ?? 0f));
    }

    private static void WriteRawVector(BinaryWriter writer, long x, long y, long z)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
    }

    private static void WriteStringList(BinaryWriter writer, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            writer.Write(0);
            return;
        }

        var normalized = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                normalized.Add(value);
        }

        int count = normalized.Count;
        writer.Write(count);
        for (int i = 0; i < count; i++)
            writer.Write(normalized[i]);
    }

    private static void WriteIntList(BinaryWriter writer, List<int> values)
    {
        int count = values?.Count ?? 0;
        writer.Write(count);
        for (int i = 0; i < count; i++)
            writer.Write(values[i]);
    }

    private static long ToAuraRaw(float value)
    {
        return checked((long)((decimal)value * Fix64Scale));
    }

    private static long ToTimelineRaw(float value)
    {
        return checked((long)Math.Round(value * Fix64Scale, MidpointRounding.AwayFromZero));
    }

    private static long ToProjectileRaw(float value)
    {
        return checked((long)Math.Round(value * Fix64Scale));
    }

    private static int ResolveTargetTypes(List<string> values)
    {
        if (values == null)
            return 1;

        int result = 0;
        for (int i = 0; i < values.Count; i++)
            result |= ParseEnum(values[i], 0, CombatHitTargetTypes);

        return result;
    }

    private static int ResolveVfxSourceDomain(VfxTriggerMapConfig mapping)
    {
        if (mapping.SourceDomainId > 0)
            return mapping.SourceDomainId;
        return ParseEnum(mapping.SourceDomain, 0, VfxSourceDomains);
    }

    private static string Schema(AbilityRuntimeCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityRuntimeCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityEffectTimelineCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityEffectTimelineCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(BuffDefinitionCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(BuffDefinitionCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(BuffPresentationDefinitionCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(BuffPresentationDefinitionCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AuraBuffDefinitionCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AuraBuffDefinitionCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityVfxDefinitionCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityVfxDefinitionCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityVfxTriggerMapDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityVfxTriggerMapDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityProjectileDefinitionCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityProjectileDefinitionCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityProjectileSpawnProfileCatalogDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityProjectileSpawnProfileCatalogDocument document) => document?.PackageId ?? string.Empty;
    private static string Schema(AbilityProjectilePresentationBindingDocument document) => document?.SchemaVersion ?? "1";
    private static string Package(AbilityProjectilePresentationBindingDocument document) => document?.PackageId ?? string.Empty;

    private static long ComputeStableHash(byte[] bytes, int startIndex, int count)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            int end = startIndex + count;
            for (int i = startIndex; i < end; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return (long)hash;
        }
    }

    private static int ParseEnum(string value, int fallback, IReadOnlyDictionary<string, int> map)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return map.TryGetValue(value, out int parsed) ? parsed : fallback;
    }

    private static readonly IReadOnlyDictionary<string, int> AbilityEffectTimelineTrackKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Combat", 0 },
            { "Gameplay", 1 },
            { "Presentation", 2 }
        };

    private static readonly IReadOnlyDictionary<string, int> AbilityEffectTimelineEventKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "AreaPulse", 0 },
            { "ShapeQueryWindow", 1 },
            { "ApplyEffect", 100 },
            { "SpawnProjectile", 101 },
            { "StartEffectTimeline", 102 },
            { "EmitGameplayEvent", 103 },
            { "SpawnVfx", 200 },
            { "PlayAudioCue", 201 },
            { "CameraImpulse", 202 },
            { "UiFeedback", 203 }
        };

    private static readonly IReadOnlyDictionary<string, int> AbilityEffectTimelineShapeKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "None", 0 },
            { "Sphere", 1 },
            { "Capsule", 2 }
        };

    private static readonly IReadOnlyDictionary<string, int> AbilityEffectTimelineAnchorMode =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "CapturedAnchor", 0 },
            { "OwnerCurrent", 1 },
            { "TriggerPoint", 2 }
        };

    private static readonly IReadOnlyDictionary<string, int> BuffStackPolicy =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "RefreshDuration", 0 },
            { "Replace", 1 },
            { "Stack", 2 }
        };

    private static readonly IReadOnlyDictionary<string, int> BuffEffectKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "None", 0 },
            { "AttributeModifier", 1 },
            { "Status", 2 }
        };

    private static readonly IReadOnlyDictionary<string, int> BuffPresentationPolarity =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Neutral", 0 },
            { "Buff", 1 },
            { "Debuff", 2 },
            { "Aura", 3 }
        };

    private static readonly IReadOnlyDictionary<string, int> GameplayRelationFilter =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Any", 0 },
            { "Self", 1 },
            { "SameTeam", 2 },
            { "Enemy", 3 }
        };

    private static readonly IReadOnlyDictionary<string, int> VfxSourceDomains =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Manual", 1 },
            { "Gameplay", 10 },
            { "CombatAction", 20 },
            { "CombatHit", 21 },
            { "ActionPresentation", 30 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileMotionKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Linear", 0 },
            { "ArcToTarget", 1 },
            { "BoomerangReturn", 2 },
            { "SwayForward", 3 },
            { "HomingToTarget", 4 },
            { "BezierToTarget", 5 },
            { "BallisticArcToTarget", 6 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileSpeedProfileKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Constant", 0 },
            { "EaseInQuadratic", 1 },
            { "EaseOutQuadratic", 2 },
            { "EaseInOutSmoothstep", 3 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileHitSameTargetPolicy =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "AllowRepeat", 0 },
            { "OncePerProjectile", 1 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileImpactPolicyKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "DirectHit", 0 },
            { "AreaPulseOnImpact", 1 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileSpawnOriginKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Socket", 0 }
        };

    private static readonly IReadOnlyDictionary<string, int> ProjectileAimKind =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "CasterForwardRange", 0 },
            { "LockedTargetCenter", 1 },
            { "LockedTargetOrCasterForwardRange", 2 }
        };

        private static readonly IReadOnlyDictionary<string, int> CombatHitTargetTypes =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "None", 0 },
            { "HostileCharacter", 1 << 0 },
            { "FriendlyCharacter", 1 << 1 },
            { "Self", 1 << 2 },
            { "ThirdPartyCharacter", 1 << 3 },
            { "NeutralCharacter", 1 << 3 },
            { "DamageableObject", 1 << 4 },
            { "WorldWall", 1 << 5 },
            { "WorldGround", 1 << 6 },
            { "WorldStatic", 1 << 7 }
        };

    private static readonly IReadOnlyDictionary<string, int> CombatEnvironmentHitPolicy =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "None", 0 },
            { "BlockedPresentation", 1 }
        };

    private static readonly IReadOnlyDictionary<string, int> CombatHitSameTargetPolicy =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "AllowRepeat", 0 },
            { "OncePerEvent", 1 }
        };
}
