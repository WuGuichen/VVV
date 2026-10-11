using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabSpawnSeverity
    {
        Ready,
        Warning,
        Error
    }

    internal readonly struct CharacterTestRuntimeAiLabSpawnSlotValidation
    {
        public CharacterTestRuntimeAiLabSpawnSlotValidation(
            int teamId,
            int slotIndex,
            bool enabled,
            Vector2 position,
            CharacterTestRuntimeAiLabSpawnSeverity severity,
            string message)
        {
            TeamId = teamId;
            SlotIndex = slotIndex;
            Enabled = enabled;
            Position = position;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public int TeamId { get; }
        public int SlotIndex { get; }
        public bool Enabled { get; }
        public Vector2 Position { get; }
        public CharacterTestRuntimeAiLabSpawnSeverity Severity { get; }
        public string Message { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabSpawnValidationResult
    {
        private readonly CharacterTestRuntimeAiLabSpawnSlotValidation[] _slots;

        public CharacterTestRuntimeAiLabSpawnValidationResult(
            bool canStart,
            bool hasWarnings,
            string summary,
            CharacterTestRuntimeAiLabSpawnSlotValidation[] slots)
        {
            CanStart = canStart;
            HasWarnings = hasWarnings;
            Summary = summary ?? string.Empty;
            _slots = slots ?? Array.Empty<CharacterTestRuntimeAiLabSpawnSlotValidation>();
        }

        public bool CanStart { get; }
        public bool HasWarnings { get; }
        public string Summary { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabSpawnSlotValidation> Slots => _slots;

        public bool TryGetSlot(
            int teamId,
            int slotIndex,
            out CharacterTestRuntimeAiLabSpawnSlotValidation slot)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                CharacterTestRuntimeAiLabSpawnSlotValidation candidate = _slots[i];
                if (candidate.TeamId == teamId && candidate.SlotIndex == slotIndex)
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = default;
            return false;
        }
    }

    internal static class CharacterTestRuntimeAiLabSpawnValidator
    {
        public const float DefaultSnapSize = 1f;
        public const float ArenaHalfWidth = 32f;
        public const float ArenaHalfDepth = 22f;

        private const float MinEnabledSlotDistance = 1.4f;

        public static Vector2 Snap(Vector2 position, float snapSize)
        {
            Vector2 clamped = ClampToArena(position);
            if (snapSize <= 0.0001f)
                return clamped;

            return new Vector2(
                Mathf.Round(clamped.x / snapSize) * snapSize,
                Mathf.Round(clamped.y / snapSize) * snapSize);
        }

        public static Vector2 ClampToArena(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, -ArenaHalfWidth + 1f, ArenaHalfWidth - 1f),
                Mathf.Clamp(position.y, -ArenaHalfDepth + 1f, ArenaHalfDepth - 1f));
        }

        public static CharacterTestRuntimeAiLabSpawnValidationResult Validate(
            CharacterTestArenaMatchConfig config)
        {
            if (config == null)
            {
                return new CharacterTestRuntimeAiLabSpawnValidationResult(
                    false,
                    false,
                    "Spawn setup unavailable.",
                    Array.Empty<CharacterTestRuntimeAiLabSpawnSlotValidation>());
            }

            var slots = new List<CharacterTestRuntimeAiLabSpawnSlotValidation>(
                CharacterTestArenaMatchConfig.SlotsPerTeam * 2);
            int errors = 0;
            int warnings = 0;
            int team1Enabled = 0;
            int team2Enabled = 0;

            ValidateTeam(
                config,
                CharacterTestArenaMatchConfig.Team1Id,
                slots,
                ref team1Enabled,
                ref errors,
                ref warnings);
            ValidateTeam(
                config,
                CharacterTestArenaMatchConfig.Team2Id,
                slots,
                ref team2Enabled,
                ref errors,
                ref warnings);
            ValidateEnabledSpacing(slots, ref errors);

            if (team1Enabled <= 0 || team2Enabled <= 0)
                errors++;

            string summary;
            if (errors > 0)
            {
                summary = "Spawn setup blocked: "
                    + errors.ToString(CultureInfo.InvariantCulture)
                    + " error(s).";
            }
            else if (warnings > 0)
            {
                summary = "Spawn setup valid with "
                    + warnings.ToString(CultureInfo.InvariantCulture)
                    + " warning(s).";
            }
            else
            {
                summary = "Spawn setup valid.";
            }

            return new CharacterTestRuntimeAiLabSpawnValidationResult(
                errors == 0,
                warnings > 0,
                summary,
                slots.ToArray());
        }

        private static void ValidateTeam(
            CharacterTestArenaMatchConfig config,
            int teamId,
            List<CharacterTestRuntimeAiLabSpawnSlotValidation> slots,
            ref int enabledCount,
            ref int errors,
            ref int warnings)
        {
            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
            {
                CharacterTestArenaSlotConfig slotConfig = config.TryGetSlot(teamId, i);
                bool enabled = slotConfig != null && slotConfig.Enabled;
                config.TryGetSpawnPosition(teamId, i, out Vector2 position);
                CharacterTestRuntimeAiLabSpawnSeverity severity =
                    CharacterTestRuntimeAiLabSpawnSeverity.Ready;
                string message = enabled ? "OK" : "Disabled";

                if (enabled)
                {
                    enabledCount++;
                    if (IsOutsideArena(position))
                    {
                        severity = CharacterTestRuntimeAiLabSpawnSeverity.Error;
                        message = "Outside arena bounds.";
                        errors++;
                    }
                    else if (IsInsideBlockingFixture(position))
                    {
                        severity = CharacterTestRuntimeAiLabSpawnSeverity.Error;
                        message = "Inside wall or blocking fixture.";
                        errors++;
                    }
                    else if (IsInsideHazardZone(position))
                    {
                        severity = CharacterTestRuntimeAiLabSpawnSeverity.Warning;
                        message = "Starts inside hazard training zone.";
                        warnings++;
                    }
                }

                slots.Add(new CharacterTestRuntimeAiLabSpawnSlotValidation(
                    teamId,
                    i,
                    enabled,
                    position,
                    severity,
                    message));
            }
        }

        private static void ValidateEnabledSpacing(
            List<CharacterTestRuntimeAiLabSpawnSlotValidation> slots,
            ref int errors)
        {
            float minSqr = MinEnabledSlotDistance * MinEnabledSlotDistance;
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestRuntimeAiLabSpawnSlotValidation a = slots[i];
                if (!a.Enabled || a.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error)
                    continue;

                for (int j = i + 1; j < slots.Count; j++)
                {
                    CharacterTestRuntimeAiLabSpawnSlotValidation b = slots[j];
                    if (!b.Enabled || b.Severity == CharacterTestRuntimeAiLabSpawnSeverity.Error)
                        continue;

                    if ((a.Position - b.Position).sqrMagnitude >= minSqr)
                        continue;

                    errors++;
                    slots[i] = new CharacterTestRuntimeAiLabSpawnSlotValidation(
                        a.TeamId,
                        a.SlotIndex,
                        a.Enabled,
                        a.Position,
                        CharacterTestRuntimeAiLabSpawnSeverity.Error,
                        "Too close to another enabled spawn.");
                    slots[j] = new CharacterTestRuntimeAiLabSpawnSlotValidation(
                        b.TeamId,
                        b.SlotIndex,
                        b.Enabled,
                        b.Position,
                        CharacterTestRuntimeAiLabSpawnSeverity.Error,
                        "Too close to another enabled spawn.");
                }
            }
        }

        private static bool IsOutsideArena(Vector2 position)
        {
            return position.x < -ArenaHalfWidth
                || position.x > ArenaHalfWidth
                || position.y < -ArenaHalfDepth
                || position.y > ArenaHalfDepth;
        }

        private static bool IsInsideBlockingFixture(Vector2 position)
        {
            return IsInsideRect(position, 17f, 14f, 13f, 1f)
                || IsInsideRect(position, -22f, 1.8f, 2f, 2f)
                || IsInsideRect(position, -16f, -2.2f, 2f, 2f)
                || IsInsideRect(position, 17f, 2.25f, 13.5f, 1f)
                || IsInsideRect(position, 17f, -2.25f, 13.5f, 1f);
        }

        private static bool IsInsideHazardZone(Vector2 position)
        {
            return IsInsideRect(position, -17f, -14f, 16f, 9f);
        }

        private static bool IsInsideRect(
            Vector2 position,
            float centerX,
            float centerZ,
            float width,
            float depth)
        {
            return Mathf.Abs(position.x - centerX) <= width * 0.5f
                && Mathf.Abs(position.y - centerZ) <= depth * 0.5f;
        }
    }
}
