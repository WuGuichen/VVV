using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MxFramework.Persistence.FileSystem
{
    internal sealed class FileSystemSlotWriterLeaseCoordinator
    {
        private readonly object _gate = new object();
        private readonly HashSet<string> _activeSlots;

        public FileSystemSlotWriterLeaseCoordinator()
        {
            _activeSlots = new HashSet<string>(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);
        }

        public bool TryAcquire(string slotPath, out FileSystemSlotWriterLease lease)
        {
            string normalized;
            if (!TryNormalize(slotPath, out normalized))
            {
                lease = null;
                return false;
            }

            lock (_gate)
            {
                if (!_activeSlots.Add(normalized))
                {
                    lease = null;
                    return false;
                }
            }

            lease = new FileSystemSlotWriterLease(this, normalized);
            return true;
        }

        public bool WaitAcquire(
            string slotPath,
            CancellationToken cancellationToken,
            out FileSystemSlotWriterLease lease)
        {
            return WaitAcquire(slotPath, null, cancellationToken, out lease);
        }

        public bool WaitAcquire(
            string slotPath,
            FileSystemPublicationGate publicationGate,
            CancellationToken cancellationToken,
            out FileSystemSlotWriterLease lease)
        {
            string normalized;
            if (!TryNormalize(slotPath, out normalized))
            {
                lease = null;
                return false;
            }

            using (cancellationToken.Register(PulseWaiters, this))
            {
                lock (_gate)
                {
                    while (_activeSlots.Contains(normalized))
                    {
                        if (IsCancellationRequested(publicationGate, cancellationToken))
                        {
                            lease = null;
                            return false;
                        }

                        Monitor.Wait(_gate, 25);
                    }

                    if (IsCancellationRequested(publicationGate, cancellationToken))
                    {
                        lease = null;
                        return false;
                    }

                    _activeSlots.Add(normalized);
                }
            }

            lease = new FileSystemSlotWriterLease(this, normalized);
            return true;
        }

        private static bool IsCancellationRequested(
            FileSystemPublicationGate publicationGate,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (publicationGate != null) publicationGate.RequestCancel();
                return true;
            }

            return publicationGate != null && publicationGate.IsCancellationRequested;
        }

        internal void Release(string normalizedSlotPath)
        {
            lock (_gate)
            {
                _activeSlots.Remove(normalizedSlotPath);
                Monitor.PulseAll(_gate);
            }
        }

        private static void PulseWaiters(object state)
        {
            var coordinator = (FileSystemSlotWriterLeaseCoordinator)state;
            lock (coordinator._gate)
            {
                Monitor.PulseAll(coordinator._gate);
            }
        }

        private static bool TryNormalize(string slotPath, out string normalized)
        {
            if (string.IsNullOrEmpty(slotPath))
            {
                normalized = string.Empty;
                return false;
            }

            try
            {
                normalized = Path.GetFullPath(slotPath);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                normalized = string.Empty;
                return false;
            }
        }
    }

    internal sealed class FileSystemSlotWriterLease : IDisposable
    {
        private FileSystemSlotWriterLeaseCoordinator _owner;
        private readonly string _normalizedSlotPath;

        public FileSystemSlotWriterLease(
            FileSystemSlotWriterLeaseCoordinator owner,
            string normalizedSlotPath)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _normalizedSlotPath = normalizedSlotPath ?? throw new ArgumentNullException(nameof(normalizedSlotPath));
        }

        public void Dispose()
        {
            FileSystemSlotWriterLeaseCoordinator owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null) owner.Release(_normalizedSlotPath);
        }
    }
}
