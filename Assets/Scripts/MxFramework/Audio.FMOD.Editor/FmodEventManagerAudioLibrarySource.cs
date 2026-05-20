using System;
using FMODUnity;

namespace MxFramework.Audio.FMOD.Editor
{
    public static class FmodEventManagerAudioLibrarySource
    {
        public static FmodAudioLibrarySourceData Capture()
        {
            try
            {
                if (Settings.Instance == null)
                {
                    return FmodAudioLibrarySourceData.Unavailable("FMOD Settings asset was not found.");
                }

                var data = new FmodAudioLibrarySourceData
                {
                    IsAvailable = true,
                    IsCacheValid = EventManager.IsValid,
                    Source = "FMODUnity.EventManager",
                    CacheTimeUtc = ToUtc(EventManager.CacheTime)
                };

                CopyBanks(data);
                CopyGlobalParameters(data);
                CopyEvents(data);
                return data;
            }
            catch (Exception exception)
            {
                return FmodAudioLibrarySourceData.Unavailable("Failed to read FMOD editor cache: " + exception.Message);
            }
        }

        private static void CopyBanks(FmodAudioLibrarySourceData data)
        {
            var banks = EventManager.Banks;
            if (banks == null)
            {
                return;
            }

            for (int i = 0; i < banks.Count; i++)
            {
                EditorBankRef bankRef = banks[i];
                if (bankRef == null)
                {
                    continue;
                }

                var bank = new FmodAudioLibrarySourceBank
                {
                    Name = bankRef.Name ?? string.Empty,
                    Path = bankRef.Path ?? string.Empty,
                    StudioPath = bankRef.StudioPath ?? string.Empty,
                    LastModifiedUtc = ToUtc(bankRef.LastModified)
                };

                if (bankRef.FileSizes != null)
                {
                    for (int sizeIndex = 0; sizeIndex < bankRef.FileSizes.Count; sizeIndex++)
                    {
                        EditorBankRef.NameValuePair size = bankRef.FileSizes[sizeIndex];
                        if (size == null)
                        {
                            continue;
                        }

                        bank.FileSizes.Add(new FmodAudioLibrarySourceBankFileSize
                        {
                            Platform = size.Name ?? string.Empty,
                            SizeBytes = size.Value
                        });
                    }
                }

                data.Banks.Add(bank);
            }
        }

        private static void CopyGlobalParameters(FmodAudioLibrarySourceData data)
        {
            var parameters = EventManager.Parameters;
            if (parameters == null)
            {
                return;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                EditorParamRef paramRef = parameters[i];
                if (paramRef != null)
                {
                    data.GlobalParameters.Add(CopyParameter(paramRef));
                }
            }
        }

        private static void CopyEvents(FmodAudioLibrarySourceData data)
        {
            var events = EventManager.Events;
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                EditorEventRef eventRef = events[i];
                if (eventRef == null)
                {
                    continue;
                }

                var audioEvent = new FmodAudioLibrarySourceEvent
                {
                    Path = eventRef.Path ?? string.Empty,
                    Guid = eventRef.Guid.ToString(),
                    Kind = IsSnapshotPath(eventRef.Path) ? "Snapshot" : "Event",
                    Is3D = eventRef.Is3D,
                    IsLoop = !eventRef.IsOneShot,
                    IsStream = eventRef.IsStream,
                    MinDistance = eventRef.MinDistance,
                    MaxDistance = eventRef.MaxDistance,
                    LengthMs = eventRef.Length
                };

                if (eventRef.Banks != null)
                {
                    for (int bankIndex = 0; bankIndex < eventRef.Banks.Count; bankIndex++)
                    {
                        EditorBankRef bankRef = eventRef.Banks[bankIndex];
                        if (bankRef != null && !string.IsNullOrWhiteSpace(bankRef.Name))
                        {
                            audioEvent.Banks.Add(bankRef.Name);
                        }
                    }
                }

                if (eventRef.Parameters != null)
                {
                    for (int parameterIndex = 0; parameterIndex < eventRef.Parameters.Count; parameterIndex++)
                    {
                        EditorParamRef paramRef = eventRef.Parameters[parameterIndex];
                        if (paramRef != null)
                        {
                            audioEvent.Parameters.Add(CopyParameter(paramRef));
                        }
                    }
                }

                data.Events.Add(audioEvent);
            }
        }

        private static FmodAudioLibrarySourceParameter CopyParameter(EditorParamRef paramRef)
        {
            var parameter = new FmodAudioLibrarySourceParameter
            {
                Name = paramRef.Name ?? string.Empty,
                StudioPath = paramRef.StudioPath ?? string.Empty,
                IdData1 = paramRef.ID.data1,
                IdData2 = paramRef.ID.data2,
                Kind = paramRef.Type.ToString(),
                DefaultValue = paramRef.Default,
                MinValue = paramRef.Min,
                MaxValue = paramRef.Max,
                IsGlobal = paramRef.IsGlobal
            };

            if (paramRef.Labels != null)
            {
                for (int i = 0; i < paramRef.Labels.Length; i++)
                {
                    parameter.Labels.Add(paramRef.Labels[i] ?? string.Empty);
                }
            }

            return parameter;
        }

        private static bool IsSnapshotPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("snapshot:/", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
            {
                return DateTime.MinValue;
            }

            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
        }
    }
}
