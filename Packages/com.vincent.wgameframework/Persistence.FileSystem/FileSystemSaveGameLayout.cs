using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MxFramework.Persistence.FileSystem
{
    internal static class FileSystemSaveGameLayout
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly char[] HexDigits = "0123456789abcdef".ToCharArray();

        public static bool TryNormalizeRoot(
            string value,
            IFileSystemBackend backend,
            out string rootPath,
            out SaveGameError error)
        {
            try
            {
                rootPath = Path.GetFullPath(value);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                rootPath = string.Empty;
                error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem location is invalid.");
                return false;
            }

            if (!Path.IsPathFullyQualified(rootPath))
            {
                rootPath = string.Empty;
                error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem location must be absolute.");
                return false;
            }

            return TryValidateContainedPath(rootPath, rootPath, backend, out error);
        }

        public static bool TryGetProfileDirectory(
            string rootPath,
            SaveGameProfileId profile,
            IFileSystemBackend backend,
            out string path,
            out SaveGameError error)
        {
            if (!profile.IsValid)
            {
                path = string.Empty;
                error = Error(SaveGameErrorCode.InvalidProfile, "profile", "Profile id is invalid.");
                return false;
            }

            string profiles = Path.Combine(rootPath, "profiles");
            if (!TryValidateContainedPath(rootPath, profiles, backend, out error))
            {
                path = string.Empty;
                return false;
            }

            path = Path.Combine(profiles, "p-" + EncodeIdentifier(profile.Value));
            return TryValidateContainedPath(rootPath, path, backend, out error);
        }

        public static bool TryGetSlotDirectory(
            string rootPath,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            IFileSystemBackend backend,
            out string path,
            out SaveGameError error)
        {
            string profilePath;
            if (!TryGetProfileDirectory(rootPath, profile, backend, out profilePath, out error))
            {
                path = string.Empty;
                return false;
            }

            if (!slot.IsValid)
            {
                path = string.Empty;
                error = Error(SaveGameErrorCode.InvalidSlot, "slot", "Slot id is invalid.");
                return false;
            }

            string slots = Path.Combine(profilePath, "slots");
            if (!TryValidateContainedPath(rootPath, slots, backend, out error))
            {
                path = string.Empty;
                return false;
            }

            path = Path.Combine(slots, "s-" + EncodeIdentifier(slot.Value));
            return TryValidateContainedPath(rootPath, path, backend, out error);
        }

        public static string EncodeIdentifier(string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value);
            var characters = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                byte valueByte = bytes[index];
                characters[index * 2] = HexDigits[valueByte >> 4];
                characters[index * 2 + 1] = HexDigits[valueByte & 0x0f];
            }

            return new string(characters);
        }

        public static bool TryDecodeProfileSegment(string segment, out SaveGameProfileId profile)
        {
            string value;
            if (TryDecodeSegment(segment, "p-", out value)
                && SaveGameProfileId.TryCreate(value, out profile))
            {
                return true;
            }

            profile = default;
            return false;
        }

        public static bool TryDecodeSlotSegment(string segment, out SaveGameSlotId slot)
        {
            string value;
            if (TryDecodeSegment(segment, "s-", out value)
                && SaveGameSlotId.TryCreate(value, out slot))
            {
                return true;
            }

            slot = default;
            return false;
        }

        public static bool TryValidateContainedPath(
            string rootPath,
            string candidatePath,
            IFileSystemBackend backend,
            out SaveGameError error)
        {
            string root;
            string candidate;
            try
            {
                root = Path.GetFullPath(rootPath);
                candidate = Path.GetFullPath(candidatePath);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem path is invalid.");
                return false;
            }

            StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string boundedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            bool isRoot = string.Equals(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                comparison);
            if (!isRoot && !candidate.StartsWith(boundedRoot, comparison))
            {
                error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem path escapes the configured root.");
                return false;
            }

            try
            {
                if (!ValidateComponent(root, backend))
                {
                    error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem root contains a reparse point.");
                    return false;
                }

                if (!isRoot)
                {
                    string relative = candidate.Substring(boundedRoot.Length);
                    string current = root;
                    string[] components = relative.Split(
                        new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries);
                    for (int index = 0; index < components.Length; index++)
                    {
                        current = Path.Combine(current, components[index]);
                        if (!ValidateComponent(current, backend))
                        {
                            error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem path contains a reparse point.");
                            return false;
                        }
                    }
                }
            }
            catch (Exception)
            {
                error = Error(SaveGameErrorCode.InvalidLocation, "location", "Filesystem path could not be inspected safely.");
                return false;
            }

            error = SaveGameError.None;
            return true;
        }

        private static bool ValidateComponent(string path, IFileSystemBackend backend)
        {
            FileSystemEntryStatus status = backend.GetStatus(path);
            return status.Kind == FileSystemEntryKind.Missing
                || status.Kind == FileSystemEntryKind.Directory;
        }

        private static bool TryDecodeSegment(string segment, string prefix, out string value)
        {
            value = string.Empty;
            if (segment == null
                || !segment.StartsWith(prefix, StringComparison.Ordinal)
                || segment.Length == prefix.Length
                || ((segment.Length - prefix.Length) & 1) != 0)
            {
                return false;
            }

            int byteCount = (segment.Length - prefix.Length) / 2;
            var bytes = new byte[byteCount];
            for (int index = 0; index < byteCount; index++)
            {
                int high = DecodeLowerHex(segment[prefix.Length + index * 2]);
                int low = DecodeLowerHex(segment[prefix.Length + index * 2 + 1]);
                if (high < 0 || low < 0)
                {
                    return false;
                }

                bytes[index] = (byte)((high << 4) | low);
            }

            try
            {
                value = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }

            return string.Equals(prefix + EncodeIdentifier(value), segment, StringComparison.Ordinal);
        }

        private static int DecodeLowerHex(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            return -1;
        }

        private static SaveGameError Error(SaveGameErrorCode code, string path, string message)
        {
            return new SaveGameError(code, SaveGameOperationPhase.Validation, path, message);
        }
    }
}
