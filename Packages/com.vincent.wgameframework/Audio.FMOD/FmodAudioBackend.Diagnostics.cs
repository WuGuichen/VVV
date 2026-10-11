using System;
using System.Collections.Generic;
using MxFramework.Audio;

#if MXFRAMEWORK_FMOD
using UnityEngine;
#endif

namespace MxFramework.Audio.FMOD
{
    public sealed partial class FmodAudioBackend
    {
        private AudioPlayResult FailPlay(AudioErrorCode errorCode, string message, long frame = 0, AudioTraceIdentity traceId = default)
        {
            PushError(errorCode, message, frame, traceId);
            return AudioPlayResult.Fail(errorCode, message);
        }

        private AudioResult Fail(AudioErrorCode errorCode, string message, long frame = 0, AudioTraceIdentity traceId = default)
        {
            PushError(errorCode, message, frame, traceId);
            return AudioResult.Fail(errorCode, message);
        }

        private AudioResult FailUnavailable()
        {
            return Fail(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
        }

        private AudioPlayResult FailUnavailablePlay(out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;
            return FailPlay(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
        }

        private void PushCommand(string message)
        {
            recentCommands.Add(message);
        }

        private void PushError(AudioErrorCode errorCode, string message, long frame, AudioTraceIdentity traceId)
        {
            recentErrors.Add(new AudioDebugError(errorCode, message, frame, traceId));
        }

        private sealed class FixedRing<T>
        {
            private readonly T[] _items;
            private int _count;
            private int _writeIndex;

            public FixedRing(int capacity)
            {
                _items = new T[capacity];
            }

            public void Add(T item)
            {
                _items[_writeIndex] = item;
                _writeIndex = (_writeIndex + 1) % _items.Length;
                if (_count < _items.Length)
                    _count++;
            }

            public List<T> ToList()
            {
                var result = new List<T>(_count);
                int start = _count == _items.Length ? _writeIndex : 0;
                for (int i = 0; i < _count; i++)
                    result.Add(_items[(start + i) % _items.Length]);
                return result;
            }
        }
    }
}
