using System;

namespace MxFramework.Platform
{
    /// <summary>
    /// Supplies non-authoritative UTC wall-clock metadata.
    /// </summary>
    /// <remarks>
    /// Implementations must return DateTimeKind.Utc. Values may move backward and must not drive ordering,
    /// generations, replay, hashing, deterministic simulation, or SaveState conflict resolution.
    /// </remarks>
    public interface IWallClock
    {
        /// <summary>
        /// Gets the current UTC wall-clock value.
        /// </summary>
        /// <remarks>NoAlloc after provider initialization.</remarks>
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// Supplies UTC wall-clock metadata from the BCL system clock.
    /// </summary>
    public sealed class SystemUtcWallClock : IWallClock
    {
        /// <remarks>NoAlloc.</remarks>
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Validates wall-clock values before an application or persistence boundary publishes metadata.
    /// </summary>
    public static class PlatformUtcClock
    {
        /// <summary>
        /// Reads a UTC value and fails closed for null providers, provider exceptions, Local, or Unspecified values.
        /// </summary>
        /// <remarks>
        /// NoAlloc on the successful path for a stable provider. Adapter failure diagnostics are LowFreqAlloc.
        /// Invalid values are not converted, relabeled, or replaced by another clock.
        /// </remarks>
        public static bool TryReadUtc(
            IWallClock wallClock,
            out DateTime utcNow,
            out PlatformError error)
        {
            utcNow = default;

            if (wallClock == null)
            {
                error = new PlatformError(
                    PlatformErrorCode.InvalidArgument,
                    "Wall clock provider must not be null.");
                return false;
            }

            DateTime candidate;
            try
            {
                candidate = wallClock.UtcNow;
            }
            catch (Exception exception)
            {
                error = new PlatformError(
                    PlatformErrorCode.AdapterFailure,
                    "Wall clock provider failed.",
                    exception);
                return false;
            }

            if (candidate.Kind != DateTimeKind.Utc)
            {
                error = new PlatformError(
                    PlatformErrorCode.AdapterFailure,
                    "Wall clock provider returned a non-UTC value.");
                return false;
            }

            utcNow = candidate;
            error = PlatformError.None;
            return true;
        }
    }
}
