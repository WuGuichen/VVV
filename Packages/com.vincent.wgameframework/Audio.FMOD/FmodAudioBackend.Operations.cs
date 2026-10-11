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
#if MXFRAMEWORK_FMOD
        private bool TryCreateInstance(AudioEventDefinition definition, out global::FMOD.Studio.EventInstance instance, out string error)
        {
            instance = default;
            error = null;

            if (TryResolveEventReference(definition, out ResolvedEventReference resolved))
            {
                try
                {
                    instance = global::FMODUnity.RuntimeManager.CreateInstance(resolved.EventReference);
                    if (instance.isValid())
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    error = "FMOD guid lookup failed for event " + definition.Id + ": " + exception.Message;
                }
            }

            if (!string.IsNullOrEmpty(definition.FmodEventPath))
            {
                try
                {
                    instance = global::FMODUnity.RuntimeManager.CreateInstance(definition.FmodEventPath);
                    if (instance.isValid())
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    error = "FMOD path lookup failed for event " + definition.Id + ": " + exception.Message;
                }
            }

            error = error ?? "FMOD event has no usable guid or path: " + definition.Id;
            return false;
        }

        private static bool TryResolveEventReference(AudioEventDefinition definition, out ResolvedEventReference resolved)
        {
            resolved = default;
            if (string.IsNullOrWhiteSpace(definition.FmodEventGuid))
            {
                return false;
            }

            if (!TryParseFmodGuid(definition.FmodEventGuid, out global::FMOD.GUID guid))
            {
                return false;
            }

            global::FMODUnity.EventReference eventReference = new global::FMODUnity.EventReference
            {
                Guid = guid
            };

            resolved = new ResolvedEventReference(eventReference, definition.FmodEventPath);
            return true;
        }

        private static bool TryParseFmodGuid(string value, out global::FMOD.GUID fmodGuid)
        {
            fmodGuid = default;
            if (!Guid.TryParse(value, out Guid guid))
            {
                return false;
            }

            byte[] bytes = guid.ToByteArray();
            fmodGuid.Data1 = BitConverter.ToInt32(bytes, 0);
            fmodGuid.Data2 = BitConverter.ToInt32(bytes, 4);
            fmodGuid.Data3 = BitConverter.ToInt32(bytes, 8);
            fmodGuid.Data4 = BitConverter.ToInt32(bytes, 12);
            return true;
        }

        private AudioResult ApplyInitialParameters(global::FMOD.Studio.EventInstance instance, AudioEventDefinition definition, in AudioPlayRequest request)
        {
            for (int i = 0; i < request.ParameterCount; i++)
            {
                AudioParameterValue value = request.Parameters[i];
                if (!definitions.TryGetParameter(definition.Id, value.ParameterId, out AudioParameterDefinition parameter))
                {
                    return Fail(AudioErrorCode.InvalidParameter, "Audio parameter was not found: event=" + definition.Id + " parameter=" + value.ParameterId, request.Frame, request.TraceId);
                }

                AudioResult backendParameterResult = TryApplyBackendParameter(
                    instance,
                    parameter,
                    value.Value,
                    request.Frame,
                    request.TraceId,
                    out bool handled);
                if (handled)
                {
                    if (backendParameterResult.Failed)
                    {
                        return backendParameterResult;
                    }

                    continue;
                }

                global::FMOD.RESULT result = instance.setParameterByName(parameter.Name, value.Value);
                if (result != global::FMOD.RESULT.OK)
                {
                    if (IsOptionalPhysicalParameter(parameter.Name)
                        && (result == global::FMOD.RESULT.ERR_EVENT_NOTFOUND
                            || result == global::FMOD.RESULT.ERR_INVALID_PARAM))
                    {
                        continue;
                    }

                    return Fail(AudioErrorCode.BackendFailed, "FMOD parameter failed: " + parameter.Name + " result=" + result, request.Frame, request.TraceId);
                }
            }

            return AudioResult.Ok();
        }

        private AudioResult TryApplyBackendParameter(
            global::FMOD.Studio.EventInstance instance,
            AudioParameterDefinition parameter,
            float value,
            long frame,
            AudioTraceIdentity traceId,
            out bool handled)
        {
            handled = true;
            global::FMOD.RESULT result;
            if (string.Equals(parameter.Name, PhysicalVolumeParameterName, StringComparison.Ordinal))
            {
                result = instance.setVolume(Clamp(value, 0f, 2f));
            }
            else if (string.Equals(parameter.Name, PhysicalPitchParameterName, StringComparison.Ordinal))
            {
                result = instance.setPitch(Clamp(value, 0.01f, 4f));
            }
            else
            {
                handled = false;
                return AudioResult.Ok();
            }

            return result == global::FMOD.RESULT.OK
                ? AudioResult.Ok()
                : Fail(AudioErrorCode.BackendFailed, "FMOD backend parameter failed: " + parameter.Name + " result=" + result, frame, traceId);
        }

        private static bool IsOptionalPhysicalParameter(string parameterName)
        {
            return string.Equals(parameterName, PhysicalLowPassParameterName, StringComparison.Ordinal)
                || string.Equals(parameterName, PhysicalBrightnessParameterName, StringComparison.Ordinal)
                || string.Equals(parameterName, PhysicalScrapeParameterName, StringComparison.Ordinal);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value <= min)
            {
                return min;
            }

            return value >= max ? max : value;
        }

        private AudioResult Apply3DAttributes(global::FMOD.Studio.EventInstance instance, AudioEventDefinition definition, in AudioPlayRequest request)
        {
            if (!definition.Is3D)
            {
                return AudioResult.Ok();
            }

            global::FMOD.RESULT result = instance.set3DAttributes(ToFmodAttributes(request.Transform));
            return result == global::FMOD.RESULT.OK
                ? AudioResult.Ok()
                : Fail(
                    AudioErrorCode.BackendFailed,
                    "FMOD 3D attributes failed: event=" + definition.Id + " result=" + result,
                    request.Frame,
                    request.TraceId);
        }

        private static global::FMOD.ATTRIBUTES_3D ToFmodAttributes(in AudioTransform transform)
        {
            return new global::FMOD.ATTRIBUTES_3D
            {
                position = new global::FMOD.VECTOR { x = transform.X, y = transform.Y, z = transform.Z },
                forward = new global::FMOD.VECTOR { x = transform.ForwardX, y = transform.ForwardY, z = transform.ForwardZ },
                up = new global::FMOD.VECTOR { x = transform.UpX, y = transform.UpY, z = transform.UpZ },
                velocity = new global::FMOD.VECTOR { x = transform.VelocityX, y = transform.VelocityY, z = transform.VelocityZ }
            };
        }

#endif
    }
}
