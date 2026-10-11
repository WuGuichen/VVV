using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct PresentationLodHandle
    {
        internal PresentationLodHandle(int slot, int generation) { Slot = slot; Generation = generation; }
        internal int Slot { get; }
        internal int Generation { get; }
        internal bool IsValid => Generation > 0;
    }

    /// <summary>Shared immutable observation. Unknown geometry never authorizes a reduction.</summary>
    internal readonly struct PresentationLodObservation
    {
        internal PresentationLodObservation(bool known, bool visible, bool shadowNeedsPose,
            bool projected, float screenX, float screenY, float depth, float distanceSquared,
            float screenHeight = float.PositiveInfinity, bool poseShadowNeedsPose = true)
        {
            Known = known; Visible = visible; ShadowNeedsPose = shadowNeedsPose;
            Projected = projected; ScreenX = screenX; ScreenY = screenY; Depth = depth;
            DistanceSquared = distanceSquared;
            ScreenHeight = screenHeight; PoseShadowNeedsPose = poseShadowNeedsPose;
        }
        internal bool Known { get; }
        internal bool Visible { get; }
        internal bool ShadowNeedsPose { get; }
        internal bool Projected { get; }
        internal float ScreenX { get; }
        internal float ScreenY { get; }
        internal float Depth { get; }
        internal float DistanceSquared { get; }
        internal float ScreenHeight { get; }
        internal bool PoseShadowNeedsPose { get; }
    }

    internal interface IPresentationLodObserver
    {
        PresentationLodObservation Observe(int subjectId, int cameraId);
    }

    internal readonly struct PresentationLodCounters
    {
        internal PresentationLodCounters(int subjects, int cameras, long requests, long samples,
            long hits, long invalidations, long scheduled, long transitions, long conservativeRestores,
            int cachedObservations, int deadlines, int subjectCapacity)
        {
            Subjects = subjects; Cameras = cameras; Requests = requests; Samples = samples;
            CacheHits = hits; Invalidations = invalidations; ScheduledEvaluations = scheduled;
            Transitions = transitions; ConservativeRestores = conservativeRestores;
            CachedObservations = cachedObservations; Deadlines = deadlines; SubjectCapacity = subjectCapacity;
        }
        internal int Subjects { get; }
        internal int Cameras { get; }
        internal long Requests { get; }
        internal long Samples { get; }
        internal long CacheHits { get; }
        internal long Invalidations { get; }
        internal long ScheduledEvaluations { get; }
        internal long Transitions { get; }
        internal long ConservativeRestores { get; }
        internal int CachedObservations { get; }
        internal int Deadlines { get; }
        internal int SubjectCapacity { get; }
    }

    /// <summary>
    /// Session-local, engine-free observation cache and indexed deadline scheduler.
    /// Consumers request observations by handle; there is no object-by-camera frame scan.
    /// </summary>
    internal sealed class PresentationLodCoordinator
    {
        private sealed class Subject
        {
            internal int Id, Generation, Revision, HeapIndex = -1;
            internal bool Active, HasCloth, Locked, PoseProtected, Suspended;
            internal double FeedbackUntil, HiddenSince = -1, Due;
            internal Action<bool> ApplyCloth;
            internal readonly List<CachedObservation> Cache = new List<CachedObservation>(2);
        }
        private readonly struct CameraVersion
        {
            internal CameraVersion(int id, long revision, long cacheVersion) { Id = id; Revision = revision; CacheVersion = cacheVersion; }
            internal int Id { get; }
            internal long Revision { get; }
            internal long CacheVersion { get; }
        }
        private struct CachedObservation
        {
            internal int CameraId, SubjectRevision;
            internal long CameraRevision;
            internal PresentationLodObservation Value;
        }
        private readonly IPresentationLodObserver _observer;
        private readonly List<Subject> _subjects = new List<Subject>(32);
        private readonly Stack<int> _free = new Stack<int>();
        private readonly List<Subject> _heap = new List<Subject>(32);
        private readonly List<Subject> _suspended = new List<Subject>(8);
        private readonly List<CameraVersion> _cameras = new List<CameraVersion>(2);
        private long _requests, _samples, _hits, _invalidations, _scheduled, _transitions, _restores;
        private int _active, _cacheCount;
        private long _cameraVersion;
        internal double SampleInterval { get; }
        internal double HiddenDelay { get; }

        internal PresentationLodCoordinator(IPresentationLodObserver observer,
            double sampleInterval = 0.25, double hiddenDelay = 0.75)
        {
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
            if (!(sampleInterval > 0) || double.IsInfinity(sampleInterval)) throw new ArgumentOutOfRangeException(nameof(sampleInterval));
            if (!(hiddenDelay >= 0) || double.IsInfinity(hiddenDelay)) throw new ArgumentOutOfRangeException(nameof(hiddenDelay));
            SampleInterval = sampleInterval; HiddenDelay = hiddenDelay;
        }

        /// <remarks>LowFreqAlloc on registration/capacity growth. Callback only applies actual state transitions.</remarks>
        internal PresentationLodHandle Register(int subjectId, bool hasCloth, Action<bool> applyCloth, double now)
        {
            int slot;
            Subject subject;
            if (_free.Count > 0) { slot = _free.Pop(); subject = _subjects[slot]; }
            else { slot = _subjects.Count; subject = new Subject(); _subjects.Add(subject); }
            subject.Id = subjectId; subject.Generation = Next(subject.Generation);
            subject.Active = true; subject.HasCloth = hasCloth; subject.ApplyCloth = applyCloth;
            subject.Revision = 1; subject.Locked = false; subject.PoseProtected = false; subject.FeedbackUntil = 0;
            subject.HiddenSince = -1; subject.Suspended = false; subject.Cache.Clear();
            _active++;
            if (hasCloth) Schedule(subject, now);
            return new PresentationLodHandle(slot, subject.Generation);
        }

        internal bool IsRegistered(PresentationLodHandle handle) => Resolve(handle) != null;

        /// <remarks>LowFreqAlloc when the recycled-slot stack grows. Removal never revives a released/destroyed consumer.</remarks>
        internal bool Unregister(PresentationLodHandle handle)
        {
            Subject subject = Resolve(handle);
            if (subject == null) return false;
            RemoveDeadline(subject); _suspended.Remove(subject);
            subject.Active = false; subject.ApplyCloth = null; _cacheCount -= subject.Cache.Count; subject.Cache.Clear();
            _active--; _free.Push(handle.Slot);
            return true;
        }

        /// <summary>Called by existing transform/anchor/geometry sync, not by a separate scene poll.</summary>
        internal void Invalidate(PresentationLodHandle handle, double now)
        {
            Subject subject = Resolve(handle);
            if (subject == null) return;
            subject.Revision = Next(subject.Revision); _invalidations++;
            Restore(subject);
            if (subject.HasCloth && IsRegistered(handle)) Schedule(subject, now);
        }

        /// <summary>Pose-dependent bounds become stale without cancelling offscreen hysteresis on every animation frame.</summary>
        internal void InvalidateObservation(PresentationLodHandle handle)
        {
            Subject subject = Resolve(handle);
            if (subject == null) return;
            subject.Revision = Next(subject.Revision); _invalidations++;
        }

        internal void SetImportance(PresentationLodHandle handle, bool locked, double feedbackUntil, double now)
        {
            Subject subject = Resolve(handle);
            if (subject == null) return;
            bool changed = subject.Locked != locked || feedbackUntil > subject.FeedbackUntil;
            subject.Locked = locked; subject.FeedbackUntil = Math.Max(subject.FeedbackUntil, feedbackUntil);
            if (!changed) return;
            if (locked || subject.FeedbackUntil > now) { subject.HiddenSince = -1; Restore(subject); }
            if (subject.HasCloth && IsRegistered(handle)) Schedule(subject, now);
        }

        internal void SetPoseProtection(PresentationLodHandle handle, bool needed, double now)
        {
            Subject subject = Resolve(handle);
            if (subject == null || subject.PoseProtected == needed) return;
            subject.PoseProtected = needed;
            if (needed) { subject.HiddenSince = -1; Restore(subject); }
            if (subject.HasCloth && IsRegistered(handle)) Schedule(subject, now);
        }

        /// <remarks>NoAlloc after camera registration. Only reduced consumers are visited on camera invalidation.</remarks>
        internal void SetCamera(int cameraId, long revision)
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i].Id != cameraId) continue;
                if (_cameras[i].Revision == revision) return;
                _cameras[i] = new CameraVersion(cameraId, revision, ++_cameraVersion); _invalidations++;
                RestoreReducedConsumers(); return;
            }
            _cameras.Add(new CameraVersion(cameraId, revision, ++_cameraVersion)); _invalidations++;
            RestoreReducedConsumers();
        }

        internal void RemoveCamera(int cameraId)
        {
            for (int i = 0; i < _cameras.Count; i++)
                if (_cameras[i].Id == cameraId)
                {
                    _cameras.RemoveAt(i); _invalidations++; RestoreReducedConsumers();
                    // Camera removal is a lifecycle event, not a frame scan. Bound cache storage across camera churn.
                    for (int s = 0; s < _subjects.Count; s++)
                        for (int c = _subjects[s].Cache.Count - 1; c >= 0; c--)
                            if (_subjects[s].Cache[c].CameraId == cameraId)
                            { _subjects[s].Cache.RemoveAt(c); _cacheCount--; }
                    return;
                }
        }

        /// <remarks>NoAlloc after a subject first observes the registered cameras. UI and cloth share this exact cache.</remarks>
        internal bool TryObserve(PresentationLodHandle handle, int cameraId, out PresentationLodObservation observation)
        {
            observation = default;
            Subject subject = Resolve(handle);
            if (subject == null) return false;
            for (int i = 0; i < _cameras.Count; i++)
                if (_cameras[i].Id == cameraId)
                { observation = Observe(subject, _cameras[i]); return IsRegistered(handle); }
            return false;
        }

        /// <summary>Processes only indexed due subjects. No deadline scan; routine work is quota bounded.</summary>
        internal int Tick(double now, int maximumEvaluations = 32)
        {
            if (maximumEvaluations < 0) throw new ArgumentOutOfRangeException(nameof(maximumEvaluations));
            int processed = 0;
            while (_heap.Count > 0 && _heap[0].Due <= now && processed < maximumEvaluations)
            {
                Subject subject = _heap[0]; RemoveDeadline(subject); processed++; _scheduled++;
                int generation = subject.Generation;
                bool needed = subject.Locked || subject.PoseProtected || subject.FeedbackUntil > now || _cameras.Count == 0;
                for (int i = 0; i < _cameras.Count && !needed; i++)
                {
                    PresentationLodObservation value = Observe(subject, _cameras[i]);
                    needed = !value.Known || value.Visible || value.ShadowNeedsPose;
                }
                if (!subject.Active || subject.Generation != generation) continue;
                if (needed) { subject.HiddenSince = -1; SetSuspended(subject, false); }
                else
                {
                    if (subject.HiddenSince < 0) subject.HiddenSince = now;
                    SetSuspended(subject, now - subject.HiddenSince >= HiddenDelay);
                }
                if (subject.Active && subject.Generation == generation) Schedule(subject, now + SampleInterval);
            }
            return processed;
        }

        internal PresentationLodCounters Counters => new PresentationLodCounters(_active, _cameras.Count,
            _requests, _samples, _hits, _invalidations, _scheduled, _transitions, _restores,
            _cacheCount, _heap.Count, _subjects.Count);

        internal void Clear()
        {
            // Call after owners release their consumers. Never revive them during teardown.
            _heap.Clear(); _suspended.Clear(); _cameras.Clear(); _free.Clear(); _active = 0; _cacheCount = 0;
            for (int i = _subjects.Count - 1; i >= 0; i--)
            {
                Subject subject = _subjects[i]; subject.Active = false; subject.ApplyCloth = null;
                subject.Cache.Clear(); subject.HeapIndex = -1; _free.Push(i);
            }
        }

        private Subject Resolve(PresentationLodHandle handle)
        {
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _subjects.Count) return null;
            Subject subject = _subjects[handle.Slot];
            return subject.Active && subject.Generation == handle.Generation ? subject : null;
        }

        private PresentationLodObservation Observe(Subject subject, CameraVersion camera)
        {
            _requests++;
            int generation = subject.Generation;
            int slot = -1;
            for (int i = 0; i < subject.Cache.Count; i++)
            {
                CachedObservation cached = subject.Cache[i];
                if (cached.CameraId != camera.Id) continue;
                slot = i;
                if (cached.SubjectRevision == subject.Revision && cached.CameraRevision == camera.CacheVersion)
                { _hits++; return cached.Value; }
                break;
            }
            PresentationLodObservation value = _observer.Observe(subject.Id, camera.Id); _samples++;
            // A UI demand can discover renewed visibility before the next routine cloth deadline.
            if (!value.Known || value.Visible || value.ShadowNeedsPose) Restore(subject);
            if (!subject.Active || subject.Generation != generation) return value;
            var updated = new CachedObservation { CameraId = camera.Id, CameraRevision = camera.CacheVersion,
                SubjectRevision = subject.Revision, Value = value };
            if (slot < 0) { subject.Cache.Add(updated); _cacheCount++; } else subject.Cache[slot] = updated;
            return value;
        }

        private void RestoreReducedConsumers()
        {
            while (_suspended.Count > 0) Restore(_suspended[_suspended.Count - 1]);
        }
        private void Restore(Subject subject)
        {
            if (!subject.Suspended) return;
            subject.HiddenSince = -1; _restores++; SetSuspended(subject, false);
        }
        private void SetSuspended(Subject subject, bool suspended)
        {
            if (subject.Suspended == suspended) return;
            subject.Suspended = suspended; _transitions++;
            if (suspended) _suspended.Add(subject); else _suspended.Remove(subject);
            subject.ApplyCloth?.Invoke(suspended);
        }
        private static int Next(int version) => version == int.MaxValue ? 1 : version + 1;

        private void Schedule(Subject subject, double due)
        {
            subject.Due = due;
            if (subject.HeapIndex < 0) { subject.HeapIndex = _heap.Count; _heap.Add(subject); }
            Reheap(subject.HeapIndex);
        }
        private void RemoveDeadline(Subject subject)
        {
            int index = subject.HeapIndex;
            if (index < 0) return;
            int last = _heap.Count - 1;
            Subject moved = _heap[last]; _heap.RemoveAt(last); subject.HeapIndex = -1;
            if (index < last) { _heap[index] = moved; moved.HeapIndex = index; Reheap(index); }
        }
        private void Reheap(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[parent].Due <= _heap[index].Due) break;
                Swap(index, parent); index = parent;
            }
            while (index * 2 + 1 < _heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < _heap.Count && _heap[child + 1].Due < _heap[child].Due) child++;
                if (_heap[index].Due <= _heap[child].Due) break;
                Swap(index, child); index = child;
            }
        }
        private void Swap(int a, int b)
        {
            Subject value = _heap[a]; _heap[a] = _heap[b]; _heap[b] = value;
            _heap[a].HeapIndex = a; _heap[b].HeapIndex = b;
        }
    }
}
