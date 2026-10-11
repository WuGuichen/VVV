using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FMODUnity;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Audio.FMOD.Editor
{
    /// <summary>Consumes Audio Workbench preview and snapshot-refresh requests in Edit Mode.</summary>
    [InitializeOnLoad]
    public static class FmodAudioPreviewRequestBridge
    {
        private const string PreviewRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/preview_request.json";
        private const string PreviewStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/status";
        private const string RefreshRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/refresh_snapshot_request.json";
        private const string ApplyRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/unity_apply_request.json";
        private const string ApplyStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/unity_apply_status";
        private const string PhysicalCaptureRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_capture_request.json";
        private const string PhysicalCaptureStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_capture_status";
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };
        private static string s_lastPreviewRequestId = string.Empty;
        private static string s_lastRefreshRequestId = string.Empty;
        private static string s_lastApplyRequestId = string.Empty;
        private static string s_lastPhysicalCaptureRequestId = string.Empty;
        private static global::FMOD.Studio.EventInstance s_previewInstance;

        static FmodAudioPreviewRequestBridge()
        {
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            ConsumePreviewRequest();
            ConsumeRefreshRequest();
            ConsumeApplyRequest();
            ConsumePhysicalCaptureRequest();
        }

        private static void ConsumeApplyRequest()
        {
            string path = Resolve(ApplyRequestRelativePath);
            if (!File.Exists(path)) return;
            AudioApplyRequest request;
            try { request = JsonConvert.DeserializeObject<AudioApplyRequest>(File.ReadAllText(path), JsonSettings); }
            catch { return; }
            if (request == null || string.IsNullOrWhiteSpace(request.requestId) || request.requestId == s_lastApplyRequestId) return;
            s_lastApplyRequestId = request.requestId;
            var status = new AudioApplyStatus { requestId = request.requestId, applicationId = request.applicationId, setId = request.setId, bankHash = request.bankHash, updatedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                if (!EditorApplication.ExecuteMenuItem("FMOD/Refresh Banks")) throw new InvalidOperationException("FMOD/Refresh Banks menu item 不可用。");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CaptureCurrentProject();
                FmodAudioLibraryExporter.WriteSnapshot(FmodAudioLibraryExporter.DefaultExportPath, snapshot);
                string json = JsonConvert.SerializeObject(snapshot, JsonSettings);
                using (SHA256 sha256 = SHA256.Create())
                    status.snapshotHash = "sha256:" + BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", string.Empty).ToLowerInvariant();
                status.status = "Succeeded"; status.message = "Unity Banks 与 FMOD snapshot 已刷新。";
            }
            catch (Exception exception) { status.status = "Failed"; status.message = exception.Message; }
            string statusPath = Resolve(Path.Combine(ApplyStatusDirectoryRelativePath, request.requestId + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            WriteTextAtomic(statusPath, JsonConvert.SerializeObject(status, JsonSettings));
        }

        private static void ConsumePreviewRequest()
        {
            string path = Resolve(PreviewRequestRelativePath);
            if (!File.Exists(path))
                return;
            AudioPreviewRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<AudioPreviewRequest>(File.ReadAllText(path), JsonSettings);
            }
            catch
            {
                return;
            }
            if (request == null || string.IsNullOrWhiteSpace(request.requestId) || string.Equals(request.requestId, s_lastPreviewRequestId, StringComparison.Ordinal))
                return;
            s_lastPreviewRequestId = request.requestId;

            try
            {
                if (string.Equals(request.action, "Stop", StringComparison.OrdinalIgnoreCase))
                {
                    StopPreview();
                    WriteStatus(request, "stopped", "FMOD 试听已停止。");
                    return;
                }

                PhysicalAudioPreviewResolution physicalResolution = null;
                Dictionary<string, float> previewParameters = request.parameters
                    ?? new Dictionary<string, float>(StringComparer.Ordinal);
                if (string.Equals(request.previewKind, "Physical", StringComparison.Ordinal))
                {
                    if (!PhysicalAudioPreviewBridgeResolver.TryResolve(
                            Path.GetDirectoryName(Application.dataPath),
                            request.physical,
                            out physicalResolution,
                            out string resolveError))
                        throw new InvalidOperationException(resolveError);
                    if (!string.Equals(physicalResolution.audioCueId, request.audioCueId, StringComparison.Ordinal))
                        throw new InvalidOperationException("Runtime Physical Audio resolved a different audioCueId than the Event binding request.");
                    previewParameters = physicalResolution.eventParameters;
                }

                EditorEventRef eventRef = FindEvent(request.fmodEventPath, request.fmodEventGuid);
                if (eventRef == null)
                    throw new InvalidOperationException("FMOD event 不在当前 EventManager cache 中。");
                ValidateEventBinding(eventRef, request.fmodEventPath, request.fmodEventGuid);
                StopPreview();
                EnsurePreviewEventAvailable(eventRef);
                s_previewInstance = EditorUtils.PreviewEvent(
                    eventRef,
                    previewParameters);
                if (!s_previewInstance.isValid())
                    throw new InvalidOperationException("FMOD PreviewEvent 未返回有效的 EventInstance。");
                if (physicalResolution != null)
                    ApplyPhysicalBackendParameters(physicalResolution);
                if (eventRef.Is3D)
                {
                    global::FMOD.RESULT positionResult = s_previewInstance.set3DAttributes(CreateAttributes(request.position));
                    if (positionResult != global::FMOD.RESULT.OK)
                        throw new InvalidOperationException("FMOD 3D 试听位置设置失败：" + positionResult);
                }
                WriteStatus(
                    request,
                    "succeeded",
                    physicalResolution == null
                        ? "Unity/FMОD 真实 event 试听已开始。"
                        : "Unity Runtime PhysicalAudioResolver 已解析并开始试听真实 FMOD Event。",
                    physicalResolution);
            }
            catch (Exception exception)
            {
                StopPreview();
                WriteStatus(request, "failed", "FMOD 试听失败：" + exception.Message);
            }
        }

        private static void ConsumePhysicalCaptureRequest()
        {
            string path = Resolve(PhysicalCaptureRequestRelativePath);
            if (!File.Exists(path))
                return;
            PhysicalAudioCaptureRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<PhysicalAudioCaptureRequest>(File.ReadAllText(path), JsonSettings);
            }
            catch
            {
                return;
            }
            if (request == null
                || string.IsNullOrWhiteSpace(request.requestId)
                || string.Equals(request.requestId, s_lastPhysicalCaptureRequestId, StringComparison.Ordinal))
                return;
            s_lastPhysicalCaptureRequestId = request.requestId;

            var parameterBuffer = new AudioParameterValue[PhysicalAudioLatestFactCapture.ParameterCapacity];
            bool hasCapture = PhysicalAudioLatestFactCapture.TryRead(
                parameterBuffer,
                out PhysicalAudioLatestFactSnapshot snapshot);
            var status = hasCapture
                ? CreatePhysicalCaptureSuccessStatus(request.requestId, snapshot, parameterBuffer)
                : new PhysicalAudioCaptureStatus
                {
                    requestId = request.requestId,
                    status = "unavailable",
                    code = "AUDIO_PHYSICAL_CAPTURE_SOURCE_UNAVAILABLE",
                    message = "尚未捕获到已进入播放队列的真实 Physical Audio 交互；未生成模拟事实。",
                    updatedUtc = DateTime.UtcNow.ToString("O")
                };
            string statusPath = Resolve(Path.Combine(PhysicalCaptureStatusDirectoryRelativePath, request.requestId + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath));
            WriteTextAtomic(statusPath, JsonConvert.SerializeObject(status, JsonSettings));
        }

        private static PhysicalAudioCaptureStatus CreatePhysicalCaptureSuccessStatus(
            string requestId,
            in PhysicalAudioLatestFactSnapshot snapshot,
            ReadOnlySpan<AudioParameterValue> parameterBuffer)
        {
            PhysicalAudioInteractionFact fact = snapshot.Fact;
            var parameters = new List<PhysicalAudioCaptureParameter>(snapshot.ParameterCount);
            for (int i = 0; i < snapshot.ParameterCount; i++)
            {
                AudioParameterValue parameter = parameterBuffer[i];
                parameters.Add(new PhysicalAudioCaptureParameter
                {
                    parameterId = parameter.ParameterId,
                    value = parameter.Value
                });
            }
            PhysicalAudioPreviewFact previewFact = CreatePhysicalPreviewFact(fact);
            if (snapshot.HasDynamics)
            {
                previewFact.dynamicsSource = snapshot.Dynamics.Source.ToString();
                previewFact.rawNormalImpulseNewtonSeconds =
                    snapshot.Dynamics.NormalImpulseNewtonSeconds;
                previewFact.rawAveragePressurePascals =
                    snapshot.Dynamics.AveragePressurePascals;
            }
            return new PhysicalAudioCaptureStatus
            {
                requestId = requestId,
                status = "succeeded",
                code = "AUDIO_PHYSICAL_CAPTURE_SUCCEEDED",
                message = "已捕获最近一次进入播放队列的真实 Physical Audio 交互。",
                fact = previewFact,
                runtimeFamilyId = snapshot.Result.FamilyId.Value,
                runtimeBasisId = snapshot.Result.BasisId.Value,
                runtimeCueId = snapshot.Result.CueId,
                usedFallback = snapshot.Result.UsedFallback,
                matchDistance = snapshot.Result.MatchDistance,
                captureRevision = snapshot.Revision,
                parameters = parameters,
                updatedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        internal static PhysicalAudioPreviewFact CreatePhysicalPreviewFact(
            in PhysicalAudioInteractionFact fact)
        {
            return new PhysicalAudioPreviewFact
            {
                interactionKind = fact.Kind.ToString(),
                interactionMode = fact.Mode.ToString(),
                phase = fact.Phase.ToString(),
                dynamicsSource = fact.DynamicsSource.ToString(),
                impactor = ToPreviewBody(fact.Impactor),
                receiver = ToPreviewBody(fact.Receiver),
                relativeSpeed = fact.RelativeSpeed,
                normalSpeed = fact.NormalSpeed,
                tangentialSpeed = fact.TangentialSpeed,
                pressure = fact.Pressure,
                impulse = fact.Impulse,
                angleDegrees = fact.AngleDegrees,
                intensity = fact.Intensity,
                contactNormal = new PhysicalAudioPreviewDirection
                {
                    x = fact.ContactNormal.X,
                    y = fact.ContactNormal.Y,
                    z = fact.ContactNormal.Z
                },
                position = new PhysicalAudioPreviewPosition
                {
                    x = fact.Transform.X,
                    y = fact.Transform.Y,
                    z = fact.Transform.Z
                },
                emitterId = fact.EmitterId,
                frame = fact.Frame,
                sequence = fact.Sequence
            };
        }

        private static PhysicalAudioPreviewBody ToPreviewBody(in AcousticBodyProperties body)
        {
            return new PhysicalAudioPreviewBody
            {
                runtimeMaterialId = body.MaterialId.Value,
                mass = body.Mass,
                contactArea = body.ContactArea,
                surface = body.Surface.ToString(),
                construction = body.Construction.ToString()
            };
        }

        private static void ConsumeRefreshRequest()
        {
            string path = Resolve(RefreshRequestRelativePath);
            if (!File.Exists(path))
                return;
            AudioRefreshRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<AudioRefreshRequest>(File.ReadAllText(path), JsonSettings);
            }
            catch
            {
                return;
            }
            if (request == null || string.IsNullOrWhiteSpace(request.requestId) || string.Equals(request.requestId, s_lastRefreshRequestId, StringComparison.Ordinal))
                return;
            s_lastRefreshRequestId = request.requestId;
            try
            {
                FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CaptureCurrentProject();
                FmodAudioLibraryExporter.WriteSnapshot(FmodAudioLibraryExporter.DefaultExportPath, snapshot);
                AssetDatabase.Refresh();
                Debug.Log("[MxAudio] Audio Workbench refreshed FMOD snapshot. requestId=" + request.requestId);
            }
            catch (Exception exception)
            {
                Debug.LogError("[MxAudio] Audio Workbench snapshot refresh failed: " + exception.Message);
            }
        }

        private static EditorEventRef FindEvent(string path, string guid)
        {
            IReadOnlyList<EditorEventRef> events = EventManager.Events;
            if (events == null)
                return null;
            for (int i = 0; i < events.Count; i++)
            {
                EditorEventRef item = events[i];
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(path) && string.Equals(item.Path, path, StringComparison.Ordinal))
                    return item;
                if (!string.IsNullOrWhiteSpace(guid) && string.Equals(item.Guid.ToString(), guid, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private static void ValidateEventBinding(EditorEventRef eventRef, string path, string guid)
        {
            if (!string.IsNullOrWhiteSpace(path) && !string.Equals(eventRef.Path, path, StringComparison.Ordinal))
                throw new InvalidOperationException("FMOD event path 与预期 binding 不一致。");
            if (!string.IsNullOrWhiteSpace(guid)
                && !string.Equals(eventRef.Guid.ToString(), guid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("FMOD event guid 与预期 binding 不一致。");
        }

        private static void ApplyPhysicalBackendParameters(PhysicalAudioPreviewResolution resolution)
        {
            global::FMOD.RESULT volumeResult = s_previewInstance.setVolume(resolution.volume);
            if (volumeResult != global::FMOD.RESULT.OK)
                throw new InvalidOperationException("FMOD Physical volume 设置失败：" + volumeResult);
            global::FMOD.RESULT pitchResult = s_previewInstance.setPitch(resolution.pitch);
            if (pitchResult != global::FMOD.RESULT.OK)
                throw new InvalidOperationException("FMOD Physical pitch 设置失败：" + pitchResult);
        }

        private static void EnsurePreviewEventAvailable(EditorEventRef eventRef)
        {
            EditorUtils.LoadPreviewBanks();
            if (!EditorUtils.PreviewBanksLoaded)
                throw new InvalidOperationException("FMOD Preview Banks 未加载。");

            global::FMOD.Studio.EventDescription eventDescription;
            global::FMOD.RESULT lookupResult = EditorUtils.System.getEventByID(eventRef.Guid, out eventDescription);
            if (lookupResult != global::FMOD.RESULT.OK || !eventDescription.isValid())
                throw new InvalidOperationException("FMOD Preview Banks 中找不到 event：" + eventRef.Path + " (" + lookupResult + ")");
        }

        private static global::FMOD.ATTRIBUTES_3D CreateAttributes(AudioPreviewPosition position)
        {
            position ??= new AudioPreviewPosition();
            return new global::FMOD.ATTRIBUTES_3D
            {
                position = new global::FMOD.VECTOR { x = position.x, y = position.y, z = position.z },
                velocity = new global::FMOD.VECTOR(),
                forward = new global::FMOD.VECTOR { z = 1f },
                up = new global::FMOD.VECTOR { y = 1f }
            };
        }

        private static void StopPreview()
        {
            if (s_previewInstance.isValid())
                EditorUtils.PreviewStop(s_previewInstance);
            s_previewInstance.clearHandle();
        }

        private static void WriteStatus(
            AudioPreviewRequest request,
            string status,
            string message,
            PhysicalAudioPreviewResolution physicalResolution = null)
        {
            var value = new AudioPreviewStatus
            {
                requestId = request.requestId,
                status = status,
                message = message,
                audioCueId = request.audioCueId,
                eventBindingHash = request.eventBindingHash,
                snapshotHash = request.snapshotHash,
                previewKind = string.IsNullOrWhiteSpace(request.previewKind) ? "Event" : request.previewKind,
                updatedUtc = DateTime.UtcNow.ToString("O")
            };
            if (request.physical != null)
            {
                value.expectedBasisId = request.physical.expectedBasisId;
                value.expectedRuntimeBasisId = request.physical.expectedRuntimeBasisId;
                value.expectedRuntimeCueId = request.physical.expectedRuntimeCueId;
                value.physicalCatalogHash = request.physical.runtimeCatalogHash;
            }
            if (physicalResolution != null)
            {
                value.resolvedBasisId = physicalResolution.basisId;
                value.resolvedRuntimeBasisId = physicalResolution.runtimeBasisId;
                value.resolvedAudioCueId = physicalResolution.audioCueId;
                value.resolvedRuntimeCueId = physicalResolution.runtimeCueId;
                value.usedFallback = physicalResolution.usedFallback;
                value.matchDistance = physicalResolution.matchDistance;
                value.resolvedParameters = physicalResolution.parameters;
            }
            string path = Resolve(Path.Combine(PreviewStatusDirectoryRelativePath, request.requestId + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteTextAtomic(path, JsonConvert.SerializeObject(value, JsonSettings));
        }

        private static void WriteTextAtomic(string path, string value)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, value);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }

        private static string Resolve(string relativePath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), relativePath);
        }

        private sealed class AudioPreviewRequest
        {
            public string requestId = string.Empty;
            public string action = "Play";
            public string previewKind = "Event";
            public string audioCueId = string.Empty;
            public string fmodEventPath = string.Empty;
            public string fmodEventGuid = string.Empty;
            public Dictionary<string, float> parameters = new Dictionary<string, float>(StringComparer.Ordinal);
            public AudioPreviewPosition position = new AudioPreviewPosition();
            public string eventBindingHash = string.Empty;
            public string snapshotHash = string.Empty;
            public PhysicalAudioPreviewRequest physical;
        }
        private sealed class AudioPreviewPosition
        {
            public float x;
            public float y;
            public float z;
        }
        private sealed class AudioPreviewStatus
        {
            public string requestId = string.Empty;
            public string status = string.Empty;
            public string message = string.Empty;
            public string audioCueId = string.Empty;
            public string eventBindingHash = string.Empty;
            public string snapshotHash = string.Empty;
            public string previewKind = "Event";
            public string expectedBasisId = string.Empty;
            public int expectedRuntimeBasisId;
            public int expectedRuntimeCueId;
            public string physicalCatalogHash = string.Empty;
            public string resolvedBasisId = string.Empty;
            public int resolvedRuntimeBasisId;
            public string resolvedAudioCueId = string.Empty;
            public int resolvedRuntimeCueId;
            public bool usedFallback;
            public float matchDistance;
            public List<PhysicalAudioPreviewResolvedParameter> resolvedParameters = new List<PhysicalAudioPreviewResolvedParameter>();
            public string updatedUtc = string.Empty;
        }
        private sealed class AudioRefreshRequest
        {
            public string requestId = string.Empty;
        }
        private sealed class AudioApplyRequest { public string requestId = string.Empty; public string applicationId = string.Empty; public string setId = string.Empty; public string bankHash = string.Empty; }
        private sealed class AudioApplyStatus { public string requestId = string.Empty; public string applicationId = string.Empty; public string setId = string.Empty; public string bankHash = string.Empty; public string snapshotHash = string.Empty; public string status = string.Empty; public string message = string.Empty; public string updatedUtc = string.Empty; }
        private sealed class PhysicalAudioCaptureRequest { public string requestId = string.Empty; public string createdUtc = string.Empty; }
        private sealed class PhysicalAudioCaptureStatus
        {
            public string requestId = string.Empty;
            public string status = string.Empty;
            public string code = string.Empty;
            public string message = string.Empty;
            public PhysicalAudioPreviewFact fact;
            public int runtimeFamilyId;
            public int runtimeBasisId;
            public int runtimeCueId;
            public bool usedFallback;
            public float matchDistance;
            public int captureRevision;
            public List<PhysicalAudioCaptureParameter> parameters = new List<PhysicalAudioCaptureParameter>();
            public string updatedUtc = string.Empty;
        }
        private sealed class PhysicalAudioCaptureParameter { public int parameterId; public float value; }
    }
}
