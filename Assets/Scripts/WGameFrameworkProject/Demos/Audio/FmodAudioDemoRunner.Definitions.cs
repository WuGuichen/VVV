using System;
using System.Collections;
using System.IO;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.FMOD;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo
{
    public sealed partial class FmodAudioDemoRunner
    {
        private sealed class DemoAudioDefinitions : IAudioDefinitionProvider
        {
            private readonly AudioBusDefinition _bus;
            private readonly AudioEventDefinition _oneShot;
            private readonly AudioEventDefinition _loop;
            private readonly AudioEventDefinition _oneShot3D;
            private readonly AudioParameterDefinition _parameter;

            public DemoAudioDefinitions(
                AudioBusDefinition bus,
                AudioEventDefinition oneShot,
                AudioEventDefinition loop,
                AudioEventDefinition oneShot3D,
                AudioParameterDefinition parameter)
            {
                _bus = bus;
                _oneShot = oneShot;
                _loop = loop;
                _oneShot3D = oneShot3D;
                _parameter = parameter;
            }

            public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
            {
                if (eventId == _oneShot.Id)
                {
                    definition = _oneShot;
                    return true;
                }

                if (eventId == _loop.Id)
                {
                    definition = _loop;
                    return true;
                }

                if (eventId == _oneShot3D.Id)
                {
                    definition = _oneShot3D;
                    return true;
                }

                definition = default;
                return false;
            }

            public bool TryGetBus(int busId, out AudioBusDefinition definition)
            {
                if (busId == _bus.Id)
                {
                    definition = _bus;
                    return true;
                }

                definition = default;
                return false;
            }

            public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
            {
                if ((eventId == _oneShot.Id || eventId == _loop.Id || eventId == _oneShot3D.Id)
                    && parameterId == _parameter.Id
                    && !string.IsNullOrEmpty(_parameter.Name))
                {
                    definition = _parameter;
                    return true;
                }

                definition = default;
                return false;
            }
        }
    }
}
