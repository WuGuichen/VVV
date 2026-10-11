using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Core;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    internal sealed partial class CharacterActionPreviewRuntimeSession
    {
        private void LoadPreviewVfxDefinitions()
        {
            _previewVfxDefinitions = new VfxDefinitionRegistry();
            _vfxDefinitionsByStableName = new Dictionary<string, VfxDefinition>(StringComparer.Ordinal);
            if (!File.Exists(GeneratedVfxDefinitionsPath))
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "未找到生成的特效定义：" + GeneratedVfxDefinitionsPath);
                return;
            }

            try
            {
                _previewVfxDefinitions = VfxConfigJsonImporter.ImportDefinitionFile(GeneratedVfxDefinitionsPath);
                IReadOnlyList<VfxDefinition> definitions = _previewVfxDefinitions.CreateSnapshot();
                for (int i = 0; i < definitions.Count; i++)
                {
                    VfxDefinition definition = definitions[i];
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.StableName))
                        _vfxDefinitionsByStableName[definition.StableName] = definition;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "生成的特效定义加载失败：" + ex.Message);
            }
        }

        private void StartPreviewVfx(CharacterActionVfxRequest request)
        {
            if ((request.VfxConfig.UseWeaponBladeTrailStyle || IsBladeTrailRequest(request.ResourceKey)) && request.Metadata.HasLifecycle)
            {
                if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                {
                    StopPreviewBladeTrail(request);
                    return;
                }
                if (request.Metadata.LifecyclePhase != CharacterActionTrackLifecyclePhase.Enter)
                    return;
                for (int i = 0; i < _activeVfxPreviews.Count; i++)
                {
                    PreviewActiveVfx active = _activeVfxPreviews[i];
                    if (active.BladeTrail != null && active.SourceInstanceId == request.Metadata.InstanceId
                        && active.StableEventId == request.Metadata.StableEventId)
                        return;
                }
            }
            string resourceKey = request.ResourceKey;
            int residueMilliseconds = request.VfxConfig.Parameter3;
            int bladeTrailStyleId = 0;
            if (request.VfxConfig.UseWeaponBladeTrailStyle
                && (!MxFramework.CharacterRuntimeSpawn.CharacterWeaponBladeTrailStyleResolver.TryResolve(
                    _currentPreviewBinding.Package, request.VfxConfig.Parameter0, request.VfxConfig.Parameter1,
                    out resourceKey, out residueMilliseconds, out bladeTrailStyleId)
                    || !_vfxDefinitionsByStableName.Values.Any(definition =>
                        string.Equals(definition.PrefabResource.ToString(), resourceKey, StringComparison.Ordinal))))
                return;
            EnsurePreviewAuthoringResources();
            PreviewCastAnchor anchor = ResolvePreviewVfxAnchor(request.VfxConfig);
            ResourceKey prefabKey = ResolvePreviewVfxResourceKey(resourceKey);
            GameObject view = TryCreatePreviewView(
                prefabKey,
                PreviewVfxNamePrefix + SafePreviewNameFragment(request.ResourceKey),
                out ResourceHandle<GameObject> handle,
                "特效 " + EmptyAsDash(request.ResourceKey));
            if (view != null)
                ApplyPreviewVfxTransform(view, anchor, request.VfxConfig);

            UnityVfxLifecycleController lifecycle = view != null
                ? view.GetComponent<UnityVfxLifecycleController>()
                : null;
            ParticleSystem[] particles = view != null
                ? view.GetComponentsInChildren<ParticleSystem>(includeInactive: true)
                : Array.Empty<ParticleSystem>();
            if (lifecycle != null)
            {
                lifecycle.SetPlaybackRate(request.VfxConfig.PlaybackRate);
                lifecycle.PlayBuild(new VfxInstanceId(_nextPreviewVisualId));
                lifecycle.SeekSourceFrames(request.VfxConfig.StartOffsetFrames);
            }
            ApplyPreviewParticlePlaybackRate(particles, request.VfxConfig.PlaybackRate);
            if (lifecycle != null)
            {
                PreSimulatePreviewParticles(
                    particles,
                    request.VfxConfig.StartOffsetFrames,
                    request.VfxConfig.PlaybackRate,
                    restart: false);
            }

            UnityBladeTrailBackend bladeTrail = view != null ? view.GetComponent<UnityBladeTrailBackend>() : null;
            if (bladeTrail != null)
            {
                bladeTrail.BindSource(CreatePreviewBladeTrailSource(request), _previewBladeTrailBudget);
                bladeTrail.ReinitializeAndPlay(null);
                bladeTrail.ConfigureStyle(bladeTrailStyleId);
                bladeTrail.ConfigureResidueMilliseconds(residueMilliseconds);
            }
            _activeVfxPreviews.Add(new PreviewActiveVfx(
                _nextPreviewVisualId++,
                request.ResourceKey,
                prefabKey,
                request.Metadata.LocalFrame,
                ResolvePreviewVfxEndFrame(request),
                request.VfxConfig,
                anchor,
                view,
                handle,
                view != null,
                lifecycle,
                particles)
            {
                BladeTrail = bladeTrail,
                SourceInstanceId = request.Metadata.InstanceId,
                StableEventId = request.Metadata.StableEventId,
                LastTrailFrame = request.Metadata.LocalFrame
            });
        }

        private void UpdatePreviewVfx(int frame, bool sampleBladeTrails)
        {
            _removePreviewIndices.Clear();
            _vfxPreviewSnapshots.Clear();
            for (int i = 0; i < _activeVfxPreviews.Count; i++)
            {
                PreviewActiveVfx active = _activeVfxPreviews[i];
                if (active.BladeTrail != null)
                {
                    if (!sampleBladeTrails) continue;
                    if (frame < active.LastTrailFrame) { active.BladeTrail.StopAndClear(); _removePreviewIndices.Add(i); continue; }
                    if (frame > active.EndFrame && !active.TrailExiting)
                    { active.TrailExiting = true; active.BladeTrail.BeginPresentationExit(VfxStopMode.Dissipate); }
                    if (!active.TrailExiting && active.View != null)
                        ApplyPreviewVfxTransform(active.View, ResolvePreviewVfxAnchor(active.Config), active.Config);
                    active.BladeTrail.TickPresentation((frame - active.LastTrailFrame) / (float)CharacterActionTimelineLayout.EditorTimelineFps);
                    active.LastTrailFrame = frame;
                    if (active.TrailExiting && active.BladeTrail.IsPresentationComplete) _removePreviewIndices.Add(i);
                    continue;
                }
                if (frame < active.StartFrame || frame > active.EndFrame)
                {
                    _removePreviewIndices.Add(i);
                    continue;
                }

                PreviewCastAnchor anchor = active.Config.Attachment.FollowsSubject()
                    ? ResolvePreviewVfxAnchor(active.Config)
                    : active.InitialAnchor;
                if (active.View != null)
                {
                    ApplyPreviewVfxTransform(active.View, anchor, active.Config);
                    active.SampleFrame(frame);
                }
                _vfxPreviewSnapshots.Add(new CharacterActionPreviewVfxSnapshot(
                    active.VisualId,
                    active.ResourceId,
                    active.PrefabResource,
                    active.StartFrame,
                    active.EndFrame,
                    anchor.Position,
                    anchor.Forward,
                    active.HasPrefabView));
            }

            RemovePreviewVfxByCollectedIndices();
        }

        private PreviewCastAnchor ResolvePreviewVfxAnchor(CharacterActionVfxEventConfig config)
        {
            PreviewCastAnchor baseAnchor = ResolvePreviewCastAnchor();
            MxAttachmentTransform attachment = config.Attachment;
            MxTransformOffset transform = attachment.Transform;
            if (_previewInstance != null
                && attachment.Mode == MxAttachmentMode.FollowSocket
                && !string.IsNullOrWhiteSpace(attachment.SocketId))
            {
                Transform socket = FindSocket(_previewInstance.transform, attachment.SocketId);
                if (socket != null)
                    baseAnchor = new PreviewCastAnchor(socket.position, socket.forward);
            }

            Vector3 offset = new Vector3(
                transform.PositionX,
                transform.PositionY,
                transform.PositionZ);
            if (attachment.OffsetSpace == MxTransformSpace.Local)
            {
                Quaternion basis = Quaternion.LookRotation(baseAnchor.Forward, Vector3.up);
                offset = basis * offset;
            }

            return new PreviewCastAnchor(baseAnchor.Position + offset, baseAnchor.Forward);
        }

        private static void ApplyPreviewVfxTransform(
            GameObject view,
            PreviewCastAnchor anchor,
            CharacterActionVfxEventConfig config)
        {
            if (view == null)
                return;

            MxTransformOffset transform = config.Attachment.Transform;
            Quaternion rotation = Quaternion.LookRotation(anchor.Forward, Vector3.up)
                * Quaternion.Euler(
                    transform.RotationEulerX,
                    transform.RotationEulerY,
                    transform.RotationEulerZ);
            view.transform.SetPositionAndRotation(anchor.Position, rotation);
            float scale = config.ScaleMultiplier;
            view.transform.localScale = new Vector3(
                transform.ScaleX * scale,
                transform.ScaleY * scale,
                transform.ScaleZ * scale);
        }

        private int ResolvePreviewVfxEndFrame(CharacterActionVfxRequest request)
        {
            int lifetime = request.EffectiveLifetimeFrames;
            string stableName = request.ResourceKey;
            if (TryParseResourceKey(request.ResourceKey, ResourceTypeIds.GameObject, out ResourceKey parsed))
                stableName = parsed.Id;
            if (lifetime <= 0
                && _vfxDefinitionsByStableName.TryGetValue(stableName, out VfxDefinition definition))
            {
                lifetime = definition.DefaultLifetimeFrames;
                if (lifetime > 0)
                {
                    lifetime = Math.Max(
                        1,
                        lifetime
                            - request.VfxConfig.StartOffsetFrames
                            - request.VfxConfig.EndOffsetFrames);
                    lifetime = Math.Max(1, (int)Math.Ceiling(lifetime / request.VfxConfig.PlaybackRate));
                }
            }

            if (lifetime <= 0)
                lifetime = request.DurationFrames;
            return request.Metadata.LocalFrame + lifetime - 1;
        }

        private static void ApplyPreviewParticlePlaybackRate(ParticleSystem[] particles, float playbackRate)
        {
            if (particles == null)
                return;

            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                    continue;
                ParticleSystem.MainModule main = particle.main;
                main.simulationSpeed *= playbackRate;
            }
        }

        private static void PreSimulatePreviewParticles(
            ParticleSystem[] particles,
            int sourceStartOffsetFrames,
            float playbackRate,
            bool restart)
        {
            if (particles == null || sourceStartOffsetFrames <= 0)
                return;

            float safeRate = Mathf.Clamp(playbackRate, 0.05f, 16f);
            float wallSeconds = sourceStartOffsetFrames
                / (CharacterActionTimelineLayout.EditorTimelineFps * safeRate);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle != null)
                    particle.Simulate(wallSeconds, withChildren: false, restart: restart, fixedTimeStep: true);
            }
        }

        internal bool TryDescribeAttachmentSocket(string socketId, out string description)
        {
            description = string.Empty;
            if (_previewInstance == null)
                return false;

            Transform socket = string.IsNullOrWhiteSpace(socketId)
                ? _previewInstance.transform
                : FindSocket(_previewInstance.transform, socketId);
            if (socket == null)
                return false;

            description = string.IsNullOrWhiteSpace(socketId)
                ? "角色根节点"
                : "角色骨架 / " + socket.name + " / " + socketId;
            return true;
        }

        internal bool LocateAttachmentSocket(string socketId)
        {
            if (_previewInstance == null)
                return false;

            Transform socket = string.IsNullOrWhiteSpace(socketId)
                ? _previewInstance.transform
                : FindSocket(_previewInstance.transform, socketId);
            if (socket == null)
                return false;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(new Bounds(socket.position, Vector3.one * 0.35f), instant: false);
                sceneView.Repaint();
            }
            return true;
        }

        private void RemovePreviewVfxByCollectedIndices()
        {
            for (int i = _removePreviewIndices.Count - 1; i >= 0; i--)
            {
                int index = _removePreviewIndices[i];
                if (index < 0 || index >= _activeVfxPreviews.Count)
                    continue;
                ReleasePreviewObject(_activeVfxPreviews[index].View, _activeVfxPreviews[index].Handle);
                _activeVfxPreviews.RemoveAt(index);
            }

            _removePreviewIndices.Clear();
        }

        private ResourceKey ResolvePreviewVfxResourceKey(string requestResourceKey)
        {
            if (TryParseResourceKey(requestResourceKey, ResourceTypeIds.GameObject, out ResourceKey parsed))
            {
                if (_vfxDefinitionsByStableName.TryGetValue(parsed.Id, out VfxDefinition definition))
                    return definition.PrefabResource;
                return parsed;
            }

            return default;
        }

        private void DrawVfxPreviewSnapshot(CharacterActionPreviewVfxSnapshot snapshot)
        {
            Handles.color = snapshot.HasPrefabView
                ? new Color(0.35f, 0.8f, 1f, 0.85f)
                : new Color(0.35f, 0.8f, 1f, 0.45f);
            Handles.SphereHandleCap(0, snapshot.Position, Quaternion.identity, 0.12f, EventType.Repaint);
            DrawPoseAxes(snapshot.Position, Quaternion.LookRotation(SafeDirection(snapshot.Forward), Vector3.up), 0.32f);
            Handles.Label(
                snapshot.Position + Vector3.up * 0.18f,
                "特效预览：" + EmptyAsDash(snapshot.ResourceId)
                + (snapshot.HasPrefabView ? "\n预制体显示" : "\n辅助图形显示"));
        }

        CharacterActionAdapterSinkResult ICharacterActionVfxRequestSink.SubmitVfxRequest(CharacterActionVfxRequest request)
        {
            _lastVfxRequests.Add(request);
            StartPreviewVfx(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        public readonly struct CharacterActionPreviewVfxSnapshot
        {
            public CharacterActionPreviewVfxSnapshot(
                long visualId,
                string resourceId,
                ResourceKey prefabResource,
                int startFrame,
                int endFrame,
                Vector3 position,
                Vector3 forward,
                bool hasPrefabView)
            {
                VisualId = visualId;
                ResourceId = resourceId ?? string.Empty;
                PrefabResource = prefabResource;
                StartFrame = startFrame < 0 ? 0 : startFrame;
                EndFrame = Math.Max(StartFrame, endFrame);
                Position = position;
                Forward = SafeDirection(forward);
                HasPrefabView = hasPrefabView;
            }

            public long VisualId { get; }
            public string ResourceId { get; }
            public ResourceKey PrefabResource { get; }
            public int StartFrame { get; }
            public int EndFrame { get; }
            public Vector3 Position { get; }
            public Vector3 Forward { get; }
            public bool HasPrefabView { get; }
        }

        private sealed class PreviewActiveVfx
        {
            public PreviewActiveVfx(
                long visualId,
                string resourceId,
                ResourceKey prefabResource,
                int startFrame,
                int endFrame,
                CharacterActionVfxEventConfig config,
                PreviewCastAnchor initialAnchor,
                GameObject view,
                ResourceHandle<GameObject> handle,
                bool hasPrefabView,
                UnityVfxLifecycleController lifecycle,
                ParticleSystem[] particles)
            {
                VisualId = visualId;
                ResourceId = resourceId ?? string.Empty;
                PrefabResource = prefabResource;
                StartFrame = startFrame < 0 ? 0 : startFrame;
                EndFrame = Math.Max(StartFrame, endFrame);
                Config = config;
                InitialAnchor = initialAnchor;
                View = view;
                Handle = handle;
                HasPrefabView = hasPrefabView;
                Lifecycle = lifecycle;
                Particles = particles ?? Array.Empty<ParticleSystem>();
                LastSampledLocalFrame = 0;
            }

            public long VisualId { get; }
            public string ResourceId { get; }
            public ResourceKey PrefabResource { get; }
            public int StartFrame { get; }
            public int EndFrame { get; }
            public CharacterActionVfxEventConfig Config { get; }
            public PreviewCastAnchor InitialAnchor { get; }
            public GameObject View { get; }
            public ResourceHandle<GameObject> Handle { get; }
            public bool HasPrefabView { get; }
            public UnityVfxLifecycleController Lifecycle { get; }
            public ParticleSystem[] Particles { get; }
            public int LastSampledLocalFrame { get; private set; }
            public UnityBladeTrailBackend BladeTrail { get; set; }
            public long SourceInstanceId { get; set; }
            public int StableEventId { get; set; }
            public int LastTrailFrame { get; set; }
            public bool TrailExiting { get; set; }

            public void SampleFrame(int timelineFrame)
            {
                int localFrame = Math.Max(0, timelineFrame - StartFrame);
                if (Lifecycle == null)
                {
                    float safeRate = Mathf.Clamp(Config.PlaybackRate, 0.05f, 16f);
                    float targetSeconds = localFrame / (float)CharacterActionTimelineLayout.EditorTimelineFps
                        + Config.StartOffsetFrames
                        / (CharacterActionTimelineLayout.EditorTimelineFps * safeRate);
                    for (int i = 0; i < Particles.Length; i++)
                    {
                        ParticleSystem particle = Particles[i];
                        if (particle != null)
                            particle.Simulate(targetSeconds, withChildren: false, restart: true, fixedTimeStep: true);
                    }

                    LastSampledLocalFrame = localFrame;
                    return;
                }

                if (localFrame < LastSampledLocalFrame)
                {
                    Lifecycle.PlayBuild(new VfxInstanceId(VisualId));
                    Lifecycle.SeekSourceFrames(Config.StartOffsetFrames);
                    PreSimulatePreviewParticles(
                        Particles,
                        Config.StartOffsetFrames,
                        Config.PlaybackRate,
                        restart: false);
                    LastSampledLocalFrame = 0;
                }

                while (LastSampledLocalFrame < localFrame)
                {
                    float deltaSeconds = 1f / CharacterActionTimelineLayout.EditorTimelineFps;
                    Lifecycle.Advance(deltaSeconds);
                    for (int i = 0; i < Particles.Length; i++)
                    {
                        ParticleSystem particle = Particles[i];
                        if (particle != null)
                            particle.Simulate(deltaSeconds, withChildren: false, restart: false, fixedTimeStep: true);
                    }

                    LastSampledLocalFrame++;
                }
            }
        }
    }
}
