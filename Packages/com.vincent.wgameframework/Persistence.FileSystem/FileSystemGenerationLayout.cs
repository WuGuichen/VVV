using System;
using System.Globalization;

namespace MxFramework.Persistence.FileSystem
{
    internal static class FileSystemGenerationLayout
    {
        private const string Prefix = "generation-";
        private const string Suffix = ".mxsave";
        private const int DigitCount = 20;
        private const int ExactLength = 11 + DigitCount + 7;

        public static string Format(SaveGameGeneration generation)
        {
            if (!generation.IsPublished)
            {
                throw new ArgumentException("A published generation is required.", nameof(generation));
            }

            return Prefix + generation.Value.ToString("D20", CultureInfo.InvariantCulture) + Suffix;
        }

        public static bool TryParse(
            string entryName,
            out SaveGameGeneration generation,
            out bool malformedReservedName)
        {
            generation = default;
            malformedReservedName = false;
            if (string.IsNullOrEmpty(entryName))
            {
                return false;
            }

            bool reserved = entryName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
            if (!reserved)
            {
                return false;
            }

            if (entryName.Length != ExactLength
                || !entryName.StartsWith(Prefix, StringComparison.Ordinal)
                || !entryName.EndsWith(Suffix, StringComparison.Ordinal))
            {
                malformedReservedName = true;
                return false;
            }

            int digitsStart = Prefix.Length;
            for (int index = 0; index < DigitCount; index++)
            {
                char value = entryName[digitsStart + index];
                if (value < '0' || value > '9')
                {
                    malformedReservedName = true;
                    return false;
                }
            }

            ulong parsed;
            if (!ulong.TryParse(
                    entryName.Substring(digitsStart, DigitCount),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsed)
                || parsed == 0)
            {
                malformedReservedName = true;
                return false;
            }

            generation = new SaveGameGeneration(parsed);
            return true;
        }

        public static bool TryFindHighest(
            FileSystemDirectoryEntry[] entries,
            SaveGameOperationPhase phase,
            out SaveGameGeneration highest,
            out SaveGameError error)
        {
            highest = SaveGameGeneration.None;
            if (entries == null)
            {
                error = new SaveGameError(
                    SaveGameErrorCode.IoFailure,
                    phase,
                    "generation",
                    "Generation entries are unavailable.");
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                SaveGameGeneration generation;
                bool malformedReservedName;
                if (TryParse(entries[index].Name, out generation, out malformedReservedName))
                {
                    if (entries[index].Kind != FileSystemEntryKind.File)
                    {
                        error = MalformedError(phase);
                        return false;
                    }

                    if (generation.Value > highest.Value)
                    {
                        highest = generation;
                    }

                    continue;
                }

                if (malformedReservedName)
                {
                    error = MalformedError(phase);
                    return false;
                }
            }

            error = SaveGameError.None;
            return true;
        }

        private static SaveGameError MalformedError(SaveGameOperationPhase phase)
        {
            return new SaveGameError(
                SaveGameErrorCode.InvalidEnvelope,
                phase,
                "generation",
                "Reserved generation filename is malformed.");
        }
    }
}
