using System;

namespace MxFramework.Persistence.FileSystem
{
    /// <summary>
    /// Records the filesystem primitives observed by one provider-root capability probe.
    /// </summary>
    /// <remarks>
    /// NoAlloc when copied or inspected. Evidence is limited to the configured root and backend at probe time; it is
    /// not a universal power-loss or crash-durability guarantee for other volumes, platforms, or providers.
    /// </remarks>
    public readonly struct FileSystemCapabilityEvidence : IEquatable<FileSystemCapabilityEvidence>
    {
        internal FileSystemCapabilityEvidence(
            bool createNewFile,
            bool sameDirectoryMove,
            bool nonOverwritingMove,
            bool durableFileFlush,
            bool exclusiveWriterLock)
        {
            CreateNewFile = createNewFile;
            SameDirectoryMove = sameDirectoryMove;
            NonOverwritingMove = nonOverwritingMove;
            DurableFileFlush = durableFileFlush;
            ExclusiveWriterLock = exclusiveWriterLock;
        }

        public bool CreateNewFile { get; }
        public bool SameDirectoryMove { get; }
        public bool NonOverwritingMove { get; }
        public bool DurableFileFlush { get; }
        public bool ExclusiveWriterLock { get; }

        /// <summary>Gets whether every primitive required by the immutable-generation provider was observed.</summary>
        public bool HasRequiredPublicationPrimitives =>
            CreateNewFile
            && SameDirectoryMove
            && NonOverwritingMove
            && DurableFileFlush
            && ExclusiveWriterLock;

        public bool Equals(FileSystemCapabilityEvidence other)
        {
            return CreateNewFile == other.CreateNewFile
                && SameDirectoryMove == other.SameDirectoryMove
                && NonOverwritingMove == other.NonOverwritingMove
                && DurableFileFlush == other.DurableFileFlush
                && ExclusiveWriterLock == other.ExclusiveWriterLock;
        }

        public override bool Equals(object obj) =>
            obj is FileSystemCapabilityEvidence other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CreateNewFile.GetHashCode();
                hash = (hash * 397) ^ SameDirectoryMove.GetHashCode();
                hash = (hash * 397) ^ NonOverwritingMove.GetHashCode();
                hash = (hash * 397) ^ DurableFileFlush.GetHashCode();
                hash = (hash * 397) ^ ExclusiveWriterLock.GetHashCode();
                return hash;
            }
        }
    }
}
