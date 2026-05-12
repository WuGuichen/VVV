using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;
using MxFramework.Modifiers;
using UnityEngine;

namespace MxFramework.Preview
{
    /// <summary>
    /// Runtime buff target for the preview server.
    /// Prefer creating it dynamically from <see cref="MxPreviewSceneTargetConfig"/>
    /// instead of saving it directly into scene assets.
    /// </summary>
    [AddComponentMenu("MxFramework/Preview/Scene Target")]
    public sealed class MxPreviewSceneTarget : MonoBehaviour, IBuffTarget
    {
        [Header("Identity")]
        [SerializeField] private string _targetId = "TestTarget";

        [Header("Initial Stats")]
        [SerializeField] private int _initialHp = 1000;
        [SerializeField] private int _initialAttack = 100;
        [SerializeField] private int _initialDefense = 20;

        [Header("Behavior")]
        [SerializeField] private bool _resetOnPreviewRun = true;
        [SerializeField] private bool _showOverlay = true;

        // Const attribute IDs matching framework convention
        private const int AttrHp = 1;
        private const int AttrAttack = 2;
        private const int AttrDefense = 3;

        private AttributeStore _store;
        private EventBus<BuffEvent> _buffEvents;
        private BuffPipeline _buffPipeline;
        private ModifierPipeline _modifierPipeline;

        // Reusable snapshots for the preview server callers
        private readonly System.Collections.Generic.List<AttributeChange> _attrChanges = new System.Collections.Generic.List<AttributeChange>();
        private readonly System.Collections.Generic.List<DamageTick> _damageTicks = new System.Collections.Generic.List<DamageTick>();
        private readonly System.Collections.Generic.List<StatusChange> _statusChanges = new System.Collections.Generic.List<StatusChange>();

        // --- Public properties ---
        public string TargetId => _targetId;
        public int InitialHp => _initialHp;
        public int InitialAttack => _initialAttack;
        public int InitialDefense => _initialDefense;
        public bool ResetOnPreviewRun => _resetOnPreviewRun;

        public AttributeStore Store => _store;
        public BuffPipeline Buffs => _buffPipeline;
        public ModifierPipeline Modifiers => _modifierPipeline;

        // --- IBuffTarget ---
        IAttributeOwner IBuffTarget.Attributes => _store;
        IAttributeModifierOwner IBuffTarget.AttributeModifiers => _store;
        IEventBus<BuffEvent> IBuffTarget.BuffEvents => _buffEvents;

        // --- Unity lifecycle ---
        private void Awake()
        {
            Initialize();
        }

        public void Configure(
            string targetId,
            int initialHp,
            int initialAttack,
            int initialDefense,
            bool resetOnPreviewRun,
            bool showOverlay)
        {
            _targetId = string.IsNullOrEmpty(targetId) ? "TestTarget" : targetId;
            _initialHp = initialHp;
            _initialAttack = initialAttack;
            _initialDefense = initialDefense;
            _resetOnPreviewRun = resetOnPreviewRun;
            _showOverlay = showOverlay;
            Initialize();
        }

        private void Initialize()
        {
            _store = new AttributeStore();
            _store.RegisterAttribute(AttrHp, _initialHp);
            _store.RegisterAttribute(AttrAttack, _initialAttack);
            _store.RegisterAttribute(AttrDefense, _initialDefense);
            _store.OnAttributeChanged.Subscribe(OnAttributeChanged);

            _buffEvents = new EventBus<BuffEvent>();
            _buffEvents.Subscribe(OnBuffEvent);

            _buffPipeline = new BuffPipeline();
            _modifierPipeline = new ModifierPipeline(_store, buffs: _buffPipeline);
        }

        // --- Public API ---
        public void ResetState()
        {
            if (_store == null) Initialize();
            _buffPipeline.RemoveAllBuffs();
            _modifierPipeline.RemoveAll();

            // Reset attributes to initial values
            _store.SetAttribute(AttrHp, _initialHp, this);
            _store.SetAttribute(AttrAttack, _initialAttack, this);
            _store.SetAttribute(AttrDefense, _initialDefense, this);

            _attrChanges.Clear();
            _damageTicks.Clear();
            _statusChanges.Clear();
        }

        public BuffSnapshot[] SnapshotBuffs()
        {
            if (_store == null) Initialize();
            var raw = _buffPipeline.CreateSnapshot();
            var list = new BuffSnapshot[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                var s = raw[i];
                list[i] = new BuffSnapshot
                {
                    BuffId = s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    OwnerId = _targetId,
                    Stack = s.CurrentLayers,
                    RemainingMs = s.IsPermanent ? -1 : (long)(s.RemainingTime * 1000f),
                    TotalMs = s.IsPermanent ? -1 : (long)(s.Duration * 1000f),
                };
            }
            return list;
        }

        public AttributeChange[] DrainAttributeChanges()
        {
            var copy = _attrChanges.ToArray();
            _attrChanges.Clear();
            return copy;
        }

        public DamageTick[] DrainDamageTicks()
        {
            var copy = _damageTicks.ToArray();
            _damageTicks.Clear();
            return copy;
        }

        public StatusChange[] DrainStatusChanges()
        {
            var copy = _statusChanges.ToArray();
            _statusChanges.Clear();
            return copy;
        }

        // --- Event handlers ---
        private void OnAttributeChanged(AttributeChangedEvent e)
        {
            _attrChanges.Add(new AttributeChange
            {
                OwnerId = _targetId,
                Attribute = AttributeName(e.AttributeId),
                Before = e.OldValue,
                After = e.NewValue,
                DeltaSource = e.Source?.ToString() ?? string.Empty,
            });
        }

        private void OnBuffEvent(BuffEvent e)
        {
            if (e.Type == BuffEventType.Added)
                _statusChanges.Add(new StatusChange { OwnerId = _targetId, Status = "Buff" + e.BuffId, Applied = true });
            else if (e.Type == BuffEventType.Removed)
                _statusChanges.Add(new StatusChange { OwnerId = _targetId, Status = "Buff" + e.BuffId, Applied = false });
        }

        // --- OnGUI overlay ---
        private void OnGUI()
        {
            if (!_showOverlay || _store == null) return;

            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                };
                _overlayBg = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.6f)) },
                };
            }

            float x = 10, y = Screen.height - 200, w = 400, h = 20, gap = 4;

            GUI.Box(new Rect(x - 4, y - 4, w + 8, 200), "", _overlayBg);
            GUI.Label(new Rect(x, y, w, h), $"[Preview] {_targetId}", _overlayStyle);
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"HP:   {_store.GetAttribute(AttrHp)} / {_initialHp}");
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"ATK:  {_store.GetAttribute(AttrAttack)} (base={_initialAttack})");
            y += h + gap;
            GUI.Label(new Rect(x, y, w, h), $"DEF:  {_store.GetAttribute(AttrDefense)}");
            y += h + gap * 2;

            var buffs = _buffPipeline.CreateSnapshot();
            GUI.Label(new Rect(x, y, w, h), "--- Buffs ---", _overlayStyle);
            y += h + gap;
            if (buffs.Length > 0)
            {
                foreach (var b in buffs)
                {
                    string expire = b.IsPermanent ? "perm" : $"{b.RemainingTime:F1}s";
                    GUI.Label(new Rect(x, y, w, h), $"  [{b.Id}] lv={b.CurrentLayers} left={expire}");
                    y += h + gap;
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, w, h), "  (none)");
            }
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private GUIStyle _overlayStyle;
        private GUIStyle _overlayBg;

        private static string AttributeName(int id)
        {
            return id switch
            {
                AttrHp => "Hp",
                AttrAttack => "Attack",
                AttrDefense => "Defense",
                _ => "Attr" + id.ToString(),
            };
        }
    }
}
