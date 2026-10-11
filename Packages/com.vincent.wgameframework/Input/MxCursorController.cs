using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Input
{
    /// <summary>
    /// Cursor ownership mode requested by gameplay, UI, or tooling scopes.
    /// </summary>
    public enum MxCursorMode
    {
        Released = 0,
        Captured = 1,
        Confined = 2
    }

    /// <summary>
    /// A low-frequency cursor request owned by a named gameplay, UI, or tool surface.
    /// </summary>
    public readonly struct MxCursorRequest
    {
        public MxCursorRequest(MxCursorMode mode, string owner)
        {
            Mode = mode;
            Owner = string.IsNullOrWhiteSpace(owner) ? "unknown" : owner;
        }

        public MxCursorMode Mode { get; }
        public string Owner { get; }

        public static MxCursorRequest Released(string owner)
        {
            return new MxCursorRequest(MxCursorMode.Released, owner);
        }

        public static MxCursorRequest Captured(string owner)
        {
            return new MxCursorRequest(MxCursorMode.Captured, owner);
        }

        public static MxCursorRequest Confined(string owner)
        {
            return new MxCursorRequest(MxCursorMode.Confined, owner);
        }
    }

    /// <summary>
    /// Effective Unity cursor state after resolving the active cursor request stack.
    /// </summary>
    public readonly struct MxCursorState
    {
        public static readonly MxCursorState DefaultReleased =
            FromRequest(MxCursorRequest.Released("default"));

        public MxCursorState(MxCursorMode mode, bool visible, CursorLockMode lockState, string owner)
        {
            Mode = mode;
            Visible = visible;
            LockState = lockState;
            Owner = string.IsNullOrWhiteSpace(owner) ? "unknown" : owner;
        }

        public MxCursorMode Mode { get; }
        public bool Visible { get; }
        public CursorLockMode LockState { get; }
        public string Owner { get; }

        public static MxCursorState FromRequest(MxCursorRequest request)
        {
            switch (request.Mode)
            {
                case MxCursorMode.Captured:
                    return new MxCursorState(
                        MxCursorMode.Captured,
                        visible: false,
                        lockState: CursorLockMode.Locked,
                        owner: request.Owner);
                case MxCursorMode.Confined:
                    return new MxCursorState(
                        MxCursorMode.Confined,
                        visible: true,
                        lockState: CursorLockMode.Confined,
                        owner: request.Owner);
                default:
                    return new MxCursorState(
                        MxCursorMode.Released,
                        visible: true,
                        lockState: CursorLockMode.None,
                        owner: request.Owner);
            }
        }
    }

    /// <summary>
    /// Applies the effective cursor state to a platform-specific cursor backend.
    /// </summary>
    public interface IMxCursorDriver
    {
        bool Visible { get; }
        CursorLockMode LockState { get; }
        void Apply(MxCursorState state);
    }

    /// <summary>
    /// The only default implementation that writes UnityEngine.Cursor.
    /// </summary>
    public sealed class UnityCursorDriver : IMxCursorDriver
    {
        public bool Visible => Cursor.visible;
        public CursorLockMode LockState => Cursor.lockState;

        public void Apply(MxCursorState state)
        {
            if (state.LockState == CursorLockMode.Locked)
            {
                Cursor.lockState = state.LockState;
                Cursor.visible = Cursor.lockState == CursorLockMode.Locked
                    ? state.Visible
                    : true;
                return;
            }

            Cursor.lockState = state.LockState;
            Cursor.visible = state.Visible;
        }
    }

    /// <summary>
    /// Resolves cursor requests and exposes the current effective cursor state.
    /// </summary>
    public interface IMxCursorController
    {
        MxCursorState Current { get; }
        bool IsCaptured { get; }
        IDisposable Push(MxCursorRequest request);
        void Refresh();
    }

    /// <summary>
    /// Scope stack cursor controller. Visible cursor requests override captured
    /// requests; newest visible request wins among visible requests.
    /// </summary>
    public sealed class MxCursorController : IMxCursorController, IDisposable
    {
        private readonly IMxCursorDriver _driver;
        private readonly List<Entry> _entries = new List<Entry>();
        private int _nextId;
        private bool _disposed;

        public MxCursorController(IMxCursorDriver driver = null)
        {
            _driver = driver ?? new UnityCursorDriver();
            Current = MxCursorState.DefaultReleased;
            _driver.Apply(Current);
        }

        public MxCursorState Current { get; private set; }
        public bool IsCaptured => Current.Mode == MxCursorMode.Captured
                                  && _driver.LockState == CursorLockMode.Locked;

        public IDisposable Push(MxCursorRequest request)
        {
            ThrowIfDisposed();
            var entry = new Entry(++_nextId, request);
            _entries.Add(entry);
            Recompute();
            return new Scope(this, entry.Id);
        }

        /// <summary>
        /// Reapplies the effective request after the operating system or Unity Editor changes cursor state.
        /// </summary>
        /// <remarks>NoAlloc after initialization. Call only for low-frequency lifecycle recovery.</remarks>
        public void Refresh()
        {
            ThrowIfDisposed();
            _driver.Apply(Current);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _entries.Clear();
            Apply(MxCursorState.DefaultReleased);
        }

        private void Release(int id)
        {
            if (_disposed)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id != id)
                    continue;

                _entries.RemoveAt(i);
                Recompute();
                return;
            }
        }

        private void Recompute()
        {
            if (_entries.Count == 0)
            {
                Apply(MxCursorState.DefaultReleased);
                return;
            }

            MxCursorRequest request = _entries[_entries.Count - 1].Request;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Request.Mode == MxCursorMode.Captured)
                    continue;

                request = _entries[i].Request;
                break;
            }

            Apply(MxCursorState.FromRequest(request));
        }

        private void Apply(MxCursorState state)
        {
            Current = state;
            _driver.Apply(state);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MxCursorController));
        }

        private sealed class Entry
        {
            public Entry(int id, MxCursorRequest request)
            {
                Id = id;
                Request = request;
            }

            public int Id { get; }
            public MxCursorRequest Request { get; }
        }

        private sealed class Scope : IDisposable
        {
            private MxCursorController _owner;
            private int _id;

            public Scope(MxCursorController owner, int id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                MxCursorController owner = _owner;
                if (owner == null)
                    return;

                int id = _id;
                _owner = null;
                _id = 0;
                owner.Release(id);
            }
        }
    }
}
