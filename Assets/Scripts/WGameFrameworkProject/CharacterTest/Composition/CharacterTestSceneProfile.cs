using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Vfx.Unity;
using UnityEngine;
using UnityEngine.Serialization;

namespace MxFramework.Game.CharacterTest
{
    [CreateAssetMenu(menuName = "MxFramework/Game/Character Test/Scene Profile")]
    public sealed class CharacterTestSceneProfile : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";

        [Header("基础运行")]
        [SerializeField][InspectorName("启动时暂停")] private bool _startPaused;
        [SerializeField][InspectorName("目标帧率")][Min(1)] private int _targetFrameRate = 60;
        [SerializeField][InspectorName("启用控制台日志")] private bool _logConsole = true;
        [SerializeField][InspectorName("启用日志富文本颜色")] private bool _logConsoleColors = true;
        [SerializeField][InspectorName("启用日志分类颜色")] private bool _logCategoryColors = true;
        [SerializeField][InspectorName("Write Trace File")] private bool _enableTraceFileWrite = true;
        [SerializeField][InspectorName("GameManager 日志颜色")] private string _gameManagerLogColor = "#BB8FCE";
        [SerializeField][InspectorName("GameSlice 日志颜色")] private string _gameSliceLogColor = "#58D68D";
        [SerializeField][InspectorName("Story 日志颜色")] private string _storyLogColor = "#5DADE2";
        [SerializeField][InspectorName("使用外部 Story 草稿")] private bool _useExternalStoryAuthoring;
        [SerializeField][InspectorName("Story 草稿 StreamingAssets 路径")]
        private string _storyDraftStreamingAssetsPath =
            "MxFramework/CharacterTest/character_test_bootstrap.story.json";
        [SerializeField][InspectorName("资源运行模式")] private GlobalResourceRuntimeMode _resourceMode =
            GlobalResourceRuntimeMode.Editor;
        [SerializeField][InspectorName("优先资源系统场景流")] private bool _preferResourceManagedSceneFlow = true;
        [SerializeField][InspectorName("基础资源预加载组")] private string _baseResourcePreloadGroupId = "SpawnCritical";
        [SerializeField][InspectorName("Resources 日志颜色")] private string _resourcesLogColor = "#F4D03F";

        [Header("音频")]
        [SerializeField][InspectorName("启用音频播放")] private bool _enableAudioPlayback = true;

        [Header("头顶状态条")]
        [SerializeField][InspectorName("显示模式")] private CharacterTestHeadPadDisplayMode _headPadDisplayMode =
            CharacterTestHeadPadDisplayMode.CombatReadout;

        [Header("Runtime AI Planner")]
        [SerializeField][InspectorName("Profile JSON StreamingAssets 路径")]
        private string _runtimeAiPlannerProfilePath =
            CharacterTestRuntimeAiPlannerProfileJson.DefaultStreamingAssetsPath;

        [Header("网络调试")]
        [SerializeField][InspectorName("Loopback 延迟帧")][Min(0)] private int _networkLoopbackLatencyFrames;
        [SerializeField][InspectorName("Loopback 抖动帧")][Min(0)] private int _networkLoopbackJitterFrames;
        [SerializeField][InspectorName("Loopback 丢包率")][Range(0f, 1f)] private float _networkLoopbackDropRate;
        [SerializeField][InspectorName("Loopback 消息乱序")] private bool _networkLoopbackReorderMessages;
        [SerializeField][InspectorName("输入延迟帧")][Min(0)] private int _networkInputDelayFrames = 2;
        [SerializeField][InspectorName("快照间隔帧")][Min(1)] private int _networkSnapshotIntervalFrames = 2;
        [SerializeField][InspectorName("心跳间隔帧")][Min(1)] private int _networkHeartbeatIntervalFrames =
            NetworkSessionConfig.DefaultHeartbeatIntervalFrames;

        [Header("Unity Transport 调参")]
        [SerializeField][InspectorName("最大连接数")][Min(1)] private int _unityTransportMaxConnections = 16;
        [SerializeField][InspectorName("最大 Payload 字节数")][Min(256)] private int _unityTransportMaxPayloadBytes = 65536;
        [SerializeField][InspectorName("可靠窗口大小")][Range(1, 64)] private int _unityTransportReliableWindowSize = 64;
        [SerializeField][InspectorName("接收队列容量")][Min(1)] private int _unityTransportReceiveQueueCapacity = 2048;
        [SerializeField][InspectorName("发送队列容量")][Min(1)] private int _unityTransportSendQueueCapacity = 2048;

        [Header("运行时物理调试")]
        [SerializeField][InspectorName("绘制 Gizmos")] private bool _drawRuntimePhysicsDebugGizmos = true;
        [SerializeField][InspectorName("仅选中时绘制")] private bool _drawRuntimePhysicsDebugOnlyWhenSelected;
        [SerializeField][InspectorName("AABB Gizmo 颜色")] private Color _runtimePhysicsAabbGizmoColor =
            new Color(0.15f, 0.65f, 1f, 0.85f);
        [SerializeField][InspectorName("角色 Gizmo 颜色")] private Color _runtimePhysicsCharacterGizmoColor =
            new Color(0.25f, 1f, 0.25f, 1f);
        [SerializeField][InspectorName("绘制部位接触点")] private bool _drawBodyPartContactDebugGizmos = true;
        [SerializeField][InspectorName("接触点未激活颜色")] private Color _bodyPartContactInactiveGizmoColor =
            new Color(0.45f, 0.45f, 0.45f, 0.55f);
        [SerializeField][InspectorName("接触点未命中颜色")] private Color _bodyPartContactProbeMissGizmoColor =
            new Color(1f, 0.68f, 0.18f, 0.9f);
        [SerializeField][InspectorName("接触点命中颜色")] private Color _bodyPartContactHitGizmoColor =
            new Color(0.18f, 1f, 0.4f, 1f);
        [SerializeField][InspectorName("接触点被过滤颜色")] private Color _bodyPartContactFilteredGizmoColor =
            new Color(1f, 0.22f, 0.18f, 1f);
        [SerializeField][InspectorName("接触点已入音频队列颜色")] private Color _bodyPartContactAudioQueuedGizmoColor =
            new Color(0.25f, 0.9f, 1f, 1f);

        [Header("攻击轨迹调试")]
        [SerializeField][InspectorName("绘制 Gizmos")] private bool _drawAttackTraceDebugGizmos = true;
        [SerializeField][InspectorName("仅选中时绘制")] private bool _drawAttackTraceDebugOnlyWhenSelected;
        [SerializeField][InspectorName("当前刀刃 Gizmo 颜色")] private Color _attackTraceCurrentBladeGizmoColor =
            new Color(1f, 0.95f, 0.1f, 1f);
        [SerializeField][InspectorName("扫掠 Gizmo 颜色")] private Color _attackTraceSweepGizmoColor =
            new Color(1f, 0.45f, 0.05f, 0.85f);
        [SerializeField][InspectorName("命中伤害点 Gizmo 颜色")] private Color _attackHitPointDamageGizmoColor =
            new Color(1f, 0.1f, 0.18f, 1f);
        [SerializeField][InspectorName("格挡命中点 Gizmo 颜色")] private Color _attackHitPointBlockedGizmoColor =
            new Color(0.35f, 0.75f, 1f, 1f);

        [Header("特效资源")]
        // Scene-profile dependency: runtime-created Meteor materials need this Shader before resource bootstrap.
        [SerializeField][InspectorName("启动 Prop Shader")] private Shader _runtimeVfxPropShader;
        [SerializeField]
        [InspectorName("Published VFX Bindings")]
        private VfxPublishedUnityBindingAsset _publishedVfxBindings;
        [SerializeField][InspectorName("火球特效 Prefab")] private GameObject _fireballVfxPrefab;
        [SerializeField][InspectorName("火球爆裂飞行特效 Prefab")] private GameObject _fireballBurstFlightVfxPrefab;
        [SerializeField][InspectorName("火球爆裂命中特效 Prefab")] private GameObject _fireballBurstImpactVfxPrefab;
        [SerializeField][InspectorName("火焰长枪飞行特效 Prefab")] private GameObject _flameLanceFlightVfxPrefab;
        [SerializeField][InspectorName("火焰长枪命中特效 Prefab")] private GameObject _flameLanceImpactVfxPrefab;
        [SerializeField][InspectorName("防御光环特效 Prefab")] private GameObject _guardAuraVfxPrefab;
        [SerializeField][InspectorName("火球爆炸命中特效 Prefab")] private GameObject _fireballExplosionImpactVfxPrefab;
        [SerializeField][InspectorName("Physical Shield VFX Prefab")] private GameObject _physicalShieldVfxPrefab;
        [SerializeField]
        [FormerlySerializedAs("_laserLanceBeamVfxPrefab")]
        [InspectorName("Beam Lance Beam VFX Prefab")]
        private GameObject _beamLanceBeamVfxPrefab;
        [SerializeField]
        [FormerlySerializedAs("_laserLanceImpactVfxPrefab")]
        [InspectorName("Beam Lance Impact VFX Prefab")]
        private GameObject _beamLanceImpactVfxPrefab;
        [SerializeField]
        [FormerlySerializedAs("_laserLanceChargeVfxPrefab")]
        [InspectorName("Beam Lance Charge VFX Prefab")]
        private GameObject _beamLanceChargeVfxPrefab;
        [SerializeField][InspectorName("Channel Beam Charge VFX Prefab")] private GameObject _channelBeamChargeVfxPrefab;
        [SerializeField][InspectorName("Channel Beam Beam VFX Prefab")] private GameObject _channelBeamBeamVfxPrefab;
        [SerializeField][InspectorName("Channel Beam Impact VFX Prefab")] private GameObject _channelBeamImpactVfxPrefab;
        [SerializeField][InspectorName("Laser Lance Charge VFX Prefab")] private GameObject _laserLanceChargeVfxPrefab;
        [SerializeField][InspectorName("Laser Lance Path VFX Prefab")] private GameObject _laserLancePathVfxPrefab;
        [SerializeField][InspectorName("Laser Lance Impact VFX Prefab")] private GameObject _laserLanceImpactVfxPrefab;
        [SerializeField][InspectorName("Chain Lightning Charge VFX Prefab")] private GameObject _chainLightningChargeVfxPrefab;
        [SerializeField][InspectorName("Chain Lightning Arc VFX Prefab")] private GameObject _chainLightningArcVfxPrefab;
        [SerializeField][InspectorName("Chain Lightning Impact VFX Prefab")] private GameObject _chainLightningImpactVfxPrefab;
        [SerializeField][InspectorName("Poison Bottle Flight VFX Prefab")] private GameObject _poisonDartFlightVfxPrefab;
        [SerializeField][InspectorName("Poison Bottle Shatter VFX Prefab")] private GameObject _poisonDartImpactVfxPrefab;
        [SerializeField][InspectorName("Poison Bottle Pool VFX Prefab")] private GameObject _poisonBottlePoolVfxPrefab;
        [SerializeField][InspectorName("Poisoned Tick VFX Prefab")] private GameObject _poisonedTickVfxPrefab;
        [SerializeField][InspectorName("投掷地雷飞行特效 Prefab")] private GameObject _arcMineFlightVfxPrefab;
        [SerializeField][InspectorName("投掷地雷落地特效 Prefab")] private GameObject _arcMineDeployVfxPrefab;
        [SerializeField][InspectorName("投掷地雷引信特效 Prefab")] private GameObject _arcMineFuseVfxPrefab;
        [SerializeField][InspectorName("投掷地雷爆炸特效 Prefab")] private GameObject _arcMineTriggerVfxPrefab;
        [SerializeField][InspectorName("符文炮台部署特效 Prefab")] private GameObject _runeTurretDeployVfxPrefab;
        [SerializeField][InspectorName("符文炮台待机特效 Prefab")] private GameObject _runeTurretIdleVfxPrefab;
        [SerializeField][InspectorName("符文炮台开火特效 Prefab")] private GameObject _runeTurretFireVfxPrefab;
        [SerializeField][InspectorName("符文炮台飞行特效 Prefab")] private GameObject _runeTurretFlightVfxPrefab;
        [SerializeField][InspectorName("符文炮台命中特效 Prefab")] private GameObject _runeTurretImpactVfxPrefab;
        [SerializeField][InspectorName("符文炮台消失特效 Prefab")] private GameObject _runeTurretExpireVfxPrefab;
        [SerializeField][InspectorName("Tornado Flight VFX Prefab")] private GameObject _tornadoFlightVfxPrefab;
        [SerializeField][InspectorName("Tornado Charge VFX Prefab")] private GameObject _tornadoChargeVfxPrefab;
        [SerializeField][InspectorName("Tornado Impact VFX Prefab")] private GameObject _tornadoImpactVfxPrefab;
        [SerializeField][InspectorName("Tornado Tick VFX Prefab")] private GameObject _tornadoTickVfxPrefab;
        [SerializeField][InspectorName("Area Confinement Zone VFX Prefab")] private GameObject _areaConfinementZoneVfxPrefab;

        [Header("程序化效果预览")]
        [SerializeField][InspectorName("启用预览热键")] private bool _enableProceduralEffectPreviewHotkeys = true;

        [Header("第三人称相机")]
        [SerializeField][InspectorName("启用第三人称相机")] private bool _enableThirdPersonCamera = true;
        [SerializeField][InspectorName("相机模式")] private CharacterTestCameraMode _cameraMode =
            CharacterTestCameraMode.ThirdPersonControlled;

        public bool StartPaused => _startPaused;
        public int TargetFrameRate => _targetFrameRate;
        public bool LogConsole => _logConsole;
        public bool LogConsoleColors => _logConsoleColors;
        public bool LogCategoryColors => _logCategoryColors;
        public bool EnableTraceFileWrite => _enableTraceFileWrite;
        public string GameManagerLogColor => _gameManagerLogColor;
        public string GameSliceLogColor => _gameSliceLogColor;
        public string StoryLogColor => _storyLogColor;
        public bool UseExternalStoryAuthoring => _useExternalStoryAuthoring;
        public string StoryDraftStreamingAssetsPath => _storyDraftStreamingAssetsPath;
        public GlobalResourceRuntimeMode ResourceMode => _resourceMode;
        public bool PreferResourceManagedSceneFlow => _preferResourceManagedSceneFlow;
        public string BaseResourcePreloadGroupId => _baseResourcePreloadGroupId;
        public string ResourcesLogColor => _resourcesLogColor;
        public bool EnableAudioPlayback => _enableAudioPlayback;
        public CharacterTestHeadPadDisplayMode HeadPadDisplayMode => _headPadDisplayMode;
        public string RuntimeAiPlannerProfilePath => _runtimeAiPlannerProfilePath;
        public int NetworkLoopbackLatencyFrames => _networkLoopbackLatencyFrames;
        public int NetworkLoopbackJitterFrames => _networkLoopbackJitterFrames;
        public float NetworkLoopbackDropRate => _networkLoopbackDropRate;
        public bool NetworkLoopbackReorderMessages => _networkLoopbackReorderMessages;
        public int NetworkInputDelayFrames => _networkInputDelayFrames;
        public int NetworkSnapshotIntervalFrames => _networkSnapshotIntervalFrames;
        public int NetworkHeartbeatIntervalFrames => _networkHeartbeatIntervalFrames;
        public int UnityTransportMaxConnections => _unityTransportMaxConnections;
        public int UnityTransportMaxPayloadBytes => _unityTransportMaxPayloadBytes;
        public int UnityTransportReliableWindowSize => _unityTransportReliableWindowSize;
        public int UnityTransportReceiveQueueCapacity => _unityTransportReceiveQueueCapacity;
        public int UnityTransportSendQueueCapacity => _unityTransportSendQueueCapacity;
        public bool DrawRuntimePhysicsDebugGizmos => _drawRuntimePhysicsDebugGizmos;
        public bool DrawRuntimePhysicsDebugOnlyWhenSelected => _drawRuntimePhysicsDebugOnlyWhenSelected;
        public Color RuntimePhysicsAabbGizmoColor => _runtimePhysicsAabbGizmoColor;
        public Color RuntimePhysicsCharacterGizmoColor => _runtimePhysicsCharacterGizmoColor;
        public bool DrawBodyPartContactDebugGizmos => _drawBodyPartContactDebugGizmos;
        public Color BodyPartContactInactiveGizmoColor => _bodyPartContactInactiveGizmoColor;
        public Color BodyPartContactProbeMissGizmoColor => _bodyPartContactProbeMissGizmoColor;
        public Color BodyPartContactHitGizmoColor => _bodyPartContactHitGizmoColor;
        public Color BodyPartContactFilteredGizmoColor => _bodyPartContactFilteredGizmoColor;
        public Color BodyPartContactAudioQueuedGizmoColor => _bodyPartContactAudioQueuedGizmoColor;
        public bool DrawAttackTraceDebugGizmos => _drawAttackTraceDebugGizmos;
        public bool DrawAttackTraceDebugOnlyWhenSelected => _drawAttackTraceDebugOnlyWhenSelected;
        public Color AttackTraceCurrentBladeGizmoColor => _attackTraceCurrentBladeGizmoColor;
        public Color AttackTraceSweepGizmoColor => _attackTraceSweepGizmoColor;
        public Color AttackHitPointDamageGizmoColor => _attackHitPointDamageGizmoColor;
        public Color AttackHitPointBlockedGizmoColor => _attackHitPointBlockedGizmoColor;
        public VfxPublishedUnityBindingAsset PublishedVfxBindings => _publishedVfxBindings;
        internal Shader RuntimeVfxPropShader => _runtimeVfxPropShader;
        [SerializeField] private GameObject _bladeTrailVfxPrefab;
        public GameObject BladeTrailVfxPrefab => _bladeTrailVfxPrefab;
        public GameObject FireballVfxPrefab => _fireballVfxPrefab;
        public GameObject FireballBurstFlightVfxPrefab => _fireballBurstFlightVfxPrefab;
        public GameObject FireballBurstImpactVfxPrefab => _fireballBurstImpactVfxPrefab;
        public GameObject FlameLanceFlightVfxPrefab => _flameLanceFlightVfxPrefab;
        public GameObject FlameLanceImpactVfxPrefab => _flameLanceImpactVfxPrefab;
        public GameObject BeamLanceBeamVfxPrefab => _beamLanceBeamVfxPrefab;
        public GameObject BeamLanceImpactVfxPrefab => _beamLanceImpactVfxPrefab;
        public GameObject BeamLanceChargeVfxPrefab => _beamLanceChargeVfxPrefab;
        public GameObject ChannelBeamChargeVfxPrefab => _channelBeamChargeVfxPrefab;
        public GameObject ChannelBeamBeamVfxPrefab => _channelBeamBeamVfxPrefab;
        public GameObject ChannelBeamImpactVfxPrefab => _channelBeamImpactVfxPrefab;
        public GameObject LaserLanceChargeVfxPrefab => _laserLanceChargeVfxPrefab;
        public GameObject LaserLancePathVfxPrefab => _laserLancePathVfxPrefab;
        public GameObject LaserLanceImpactVfxPrefab => _laserLanceImpactVfxPrefab;
        public GameObject ChainLightningChargeVfxPrefab => _chainLightningChargeVfxPrefab;
        public GameObject ChainLightningArcVfxPrefab => _chainLightningArcVfxPrefab;
        public GameObject ChainLightningImpactVfxPrefab => _chainLightningImpactVfxPrefab;
        public GameObject PoisonBottleFlightVfxPrefab => _poisonDartFlightVfxPrefab;
        public GameObject PoisonBottleShatterVfxPrefab => _poisonDartImpactVfxPrefab;
        public GameObject PoisonDartFlightVfxPrefab => PoisonBottleFlightVfxPrefab;
        public GameObject PoisonDartImpactVfxPrefab => PoisonBottleShatterVfxPrefab;
        public GameObject PoisonBottlePoolVfxPrefab => _poisonBottlePoolVfxPrefab;
        public GameObject PoisonedTickVfxPrefab => _poisonedTickVfxPrefab;
        public GameObject ArcMineFlightVfxPrefab => _arcMineFlightVfxPrefab;
        public GameObject ArcMineDeployVfxPrefab => _arcMineDeployVfxPrefab;
        public GameObject ArcMineFuseVfxPrefab => _arcMineFuseVfxPrefab;
        public GameObject ArcMineTriggerVfxPrefab => _arcMineTriggerVfxPrefab;
        public GameObject RuneTurretDeployVfxPrefab => _runeTurretDeployVfxPrefab;
        public GameObject RuneTurretIdleVfxPrefab => _runeTurretIdleVfxPrefab;
        public GameObject RuneTurretFireVfxPrefab => _runeTurretFireVfxPrefab;
        public GameObject RuneTurretFlightVfxPrefab => _runeTurretFlightVfxPrefab;
        public GameObject RuneTurretImpactVfxPrefab => _runeTurretImpactVfxPrefab;
        public GameObject RuneTurretExpireVfxPrefab => _runeTurretExpireVfxPrefab;
        public GameObject GuardAuraVfxPrefab => _guardAuraVfxPrefab;
        public GameObject PhysicalShieldVfxPrefab => _physicalShieldVfxPrefab;
        public GameObject FireballExplosionImpactVfxPrefab => _fireballExplosionImpactVfxPrefab;
        public GameObject TornadoFlightVfxPrefab => _tornadoFlightVfxPrefab;
        public GameObject TornadoChargeVfxPrefab => _tornadoChargeVfxPrefab;
        public GameObject TornadoImpactVfxPrefab => _tornadoImpactVfxPrefab;
        public GameObject TornadoTickVfxPrefab => _tornadoTickVfxPrefab;
        public GameObject AreaConfinementZoneVfxPrefab => _areaConfinementZoneVfxPrefab;
        public bool EnableProceduralEffectPreviewHotkeys => _enableProceduralEffectPreviewHotkeys;
        public bool EnableThirdPersonCamera => _enableThirdPersonCamera;
        public CharacterTestCameraMode CameraMode => _cameraMode;

        internal static CharacterTestSceneProfile ResolveStartupProfile(CharacterTestSceneProfile configuredProfile)
            => configuredProfile != null ? configuredProfile : LoadDefault();

        public static CharacterTestSceneProfile LoadDefault()
        {
            var profile = CreateInstance<CharacterTestSceneProfile>();
            profile.name = "CharacterTestSceneProfileRuntimeDefault";
            return profile;
        }

    }
}
