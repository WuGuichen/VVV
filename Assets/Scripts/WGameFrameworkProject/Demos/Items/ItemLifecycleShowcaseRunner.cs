using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Items
{
    [AddComponentMenu("MxFramework/Demo/Item Lifecycle Showcase")]
    public sealed class ItemLifecycleShowcaseRunner : MonoBehaviour
    {
        public const string IronSwordIconResourceId = "showcase.item.iron_sword.icon";
        public const string IronSwordIconAddress = "item-lifecycle/icons/iron-sword";
        public const string IconCatalogId = "mxframework.demo.items.lifecycle.icons";
        public const string IconCatalogLabel = "items.lifecycle.icons";

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private Sprite _ironSwordIcon = null;
        [SerializeField] [Min(1)] private int _targetFrameRate = 60;

        private ItemLifecycleShowcase _showcase;
        private ResourceManager _iconResources;
        private readonly Dictionary<ResourceKey, ResourceHandle<Sprite>> _iconHandles =
            new Dictionary<ResourceKey, ResourceHandle<Sprite>>();
        private readonly HashSet<ResourceKey> _missingIconKeys = new HashSet<ResourceKey>();
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;
        private Font _runtimeFont;

        private VisualElement _root;
        private Label _frameLabel;
        private Label _positionLabel;
        private Label _worldCountLabel;
        private Label _hashLabel;
        private Label _saveLabel;
        private Label _hashStatusLabel;
        private Label _equipmentLabel;
        private Label _targetSlotLabel;
        private VisualElement _worldList;
        private VisualElement _inventoryGrid;
        private VisualElement _equipmentGrid;
        private VisualElement _logList;
        private Button _openButton;
        private Button _pickupButton;
        private Button _equipButton;
        private Button _unequipButton;
        private Button _saveButton;
        private Button _loadButton;
        private Button _hashButton;
        private Button _resetButton;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private Button _moveLeftButton;
        private Button _moveRightButton;

        public void ConfigureAssets(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            Sprite ironSwordIcon = null)
        {
            _document = document;
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            if (ironSwordIcon != null)
                _ironSwordIcon = ironSwordIcon;
        }

        private void OnEnable()
        {
            ApplyTargetFrameRate();
            EnsureIconResources();
            _showcase = new ItemLifecycleShowcase();
            EnsureDocument();
            RefreshUi();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            if (_showcase != null)
            {
                _showcase.Dispose();
                _showcase = null;
            }

            RestoreTargetFrameRate();
            ReleaseIconResources();
            _root = null;
        }

        private void Update()
        {
            EnsureDocument();
            if (_showcase == null)
                _showcase = new ItemLifecycleShowcase();

            bool changed = HandleKeyboard();
            if (!changed)
                _showcase.TickIdle();
            RefreshUi();
        }

        private bool HandleKeyboard()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                _showcase.Move(0, 1);
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                _showcase.Move(0, -1);
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _showcase.Move(-1, 0);
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                _showcase.Move(1, 0);
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.O))
            {
                _showcase.OpenChest();
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                _showcase.PickupNearest();
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                _showcase.EquipFirstWeapon();
                return true;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                _showcase.Reset();
                return true;
            }

            return false;
#else
            return false;
#endif
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings(_panelSettings);

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("item-lifecycle-root");
            if (_root == nextRoot)
                return;

            UnregisterCallbacks();
            _root = nextRoot;
            CacheElements(documentRoot);
            ApplyRuntimeTextStyles();
            RegisterCallbacks();
        }

        private void CacheElements(VisualElement documentRoot)
        {
            _frameLabel = documentRoot.Q<Label>("frame-label");
            _positionLabel = documentRoot.Q<Label>("position-label");
            _worldCountLabel = documentRoot.Q<Label>("world-count-label");
            _hashLabel = documentRoot.Q<Label>("hash-label");
            _saveLabel = documentRoot.Q<Label>("save-label");
            _hashStatusLabel = documentRoot.Q<Label>("hash-status-label");
            _equipmentLabel = documentRoot.Q<Label>("equipment-label");
            _targetSlotLabel = documentRoot.Q<Label>("target-slot-label");
            _worldList = documentRoot.Q<VisualElement>("world-list");
            _inventoryGrid = documentRoot.Q<VisualElement>("inventory-grid");
            _equipmentGrid = documentRoot.Q<VisualElement>("equipment-grid");
            _logList = documentRoot.Q<VisualElement>("log-list");
            _openButton = documentRoot.Q<Button>("open-button");
            _pickupButton = documentRoot.Q<Button>("pickup-button");
            _equipButton = documentRoot.Q<Button>("equip-button");
            _unequipButton = documentRoot.Q<Button>("unequip-button");
            _saveButton = documentRoot.Q<Button>("save-button");
            _loadButton = documentRoot.Q<Button>("load-button");
            _hashButton = documentRoot.Q<Button>("hash-button");
            _resetButton = documentRoot.Q<Button>("reset-button");
            _moveUpButton = documentRoot.Q<Button>("move-up-button");
            _moveDownButton = documentRoot.Q<Button>("move-down-button");
            _moveLeftButton = documentRoot.Q<Button>("move-left-button");
            _moveRightButton = documentRoot.Q<Button>("move-right-button");
        }

        private void RegisterCallbacks()
        {
            if (_openButton != null)
                _openButton.clicked += OnOpenClicked;
            if (_pickupButton != null)
                _pickupButton.clicked += OnPickupClicked;
            if (_equipButton != null)
                _equipButton.clicked += OnEquipClicked;
            if (_unequipButton != null)
                _unequipButton.clicked += OnUnequipClicked;
            if (_saveButton != null)
                _saveButton.clicked += OnSaveClicked;
            if (_loadButton != null)
                _loadButton.clicked += OnLoadClicked;
            if (_hashButton != null)
                _hashButton.clicked += OnHashClicked;
            if (_resetButton != null)
                _resetButton.clicked += OnResetClicked;
            if (_moveUpButton != null)
                _moveUpButton.clicked += OnMoveUpClicked;
            if (_moveDownButton != null)
                _moveDownButton.clicked += OnMoveDownClicked;
            if (_moveLeftButton != null)
                _moveLeftButton.clicked += OnMoveLeftClicked;
            if (_moveRightButton != null)
                _moveRightButton.clicked += OnMoveRightClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_openButton != null)
                _openButton.clicked -= OnOpenClicked;
            if (_pickupButton != null)
                _pickupButton.clicked -= OnPickupClicked;
            if (_equipButton != null)
                _equipButton.clicked -= OnEquipClicked;
            if (_unequipButton != null)
                _unequipButton.clicked -= OnUnequipClicked;
            if (_saveButton != null)
                _saveButton.clicked -= OnSaveClicked;
            if (_loadButton != null)
                _loadButton.clicked -= OnLoadClicked;
            if (_hashButton != null)
                _hashButton.clicked -= OnHashClicked;
            if (_resetButton != null)
                _resetButton.clicked -= OnResetClicked;
            if (_moveUpButton != null)
                _moveUpButton.clicked -= OnMoveUpClicked;
            if (_moveDownButton != null)
                _moveDownButton.clicked -= OnMoveDownClicked;
            if (_moveLeftButton != null)
                _moveLeftButton.clicked -= OnMoveLeftClicked;
            if (_moveRightButton != null)
                _moveRightButton.clicked -= OnMoveRightClicked;
        }

        private void OnOpenClicked()
        {
            _showcase.OpenChest();
            RefreshUi();
        }

        private void OnPickupClicked()
        {
            _showcase.PickupNearest();
            RefreshUi();
        }

        private void OnEquipClicked()
        {
            _showcase.EquipFirstWeapon();
            RefreshUi();
        }

        private void OnUnequipClicked()
        {
            _showcase.UnequipSelectedSlot();
            RefreshUi();
        }

        private void OnSaveClicked()
        {
            _showcase.Save();
            RefreshUi();
        }

        private void OnLoadClicked()
        {
            _showcase.Load();
            RefreshUi();
        }

        private void OnHashClicked()
        {
            _showcase.CompareSavedHash();
            RefreshUi();
        }

        private void OnResetClicked()
        {
            _showcase.Reset();
            RefreshUi();
        }

        private void OnMoveUpClicked()
        {
            MoveAndRefresh(0, 1);
        }

        private void OnMoveDownClicked()
        {
            MoveAndRefresh(0, -1);
        }

        private void OnMoveLeftClicked()
        {
            MoveAndRefresh(-1, 0);
        }

        private void OnMoveRightClicked()
        {
            MoveAndRefresh(1, 0);
        }

        private void MoveAndRefresh(int deltaX, int deltaZ)
        {
            _showcase.Move(deltaX, deltaZ);
            RefreshUi();
        }

        private void RefreshUi()
        {
            if (_showcase == null || _root == null)
                return;

            ItemLifecycleShowcaseSnapshot snapshot = _showcase.CreateSnapshot();
            SetText(_frameLabel, "Frame " + snapshot.Frame.Value);
            SetText(_positionLabel, snapshot.PlayerX + "," + snapshot.PlayerZ);
            SetText(_worldCountLabel, snapshot.WorldItemCount.ToString());
            SetText(_hashLabel, snapshot.StateHash.ToString());
            SetText(_saveLabel, snapshot.SaveStatus);
            SetText(_hashStatusLabel, snapshot.HashStatus);
            SetText(_equipmentLabel, snapshot.EquipmentProfile);
            SetText(_targetSlotLabel, snapshot.SelectedEquipmentSlotName);
            RebuildWorldList(snapshot);
            RebuildSlots(_inventoryGrid, snapshot.InventorySlots, false);
            RebuildSlots(_equipmentGrid, snapshot.EquipmentSlots, true);
            RebuildLog(snapshot);
        }

        private void RebuildWorldList(ItemLifecycleShowcaseSnapshot snapshot)
        {
            if (_worldList == null)
                return;

            _worldList.Clear();
            if (snapshot.WorldItems.Length == 0)
            {
                _worldList.Add(CreateTextRow("No world items", "muted-row"));
                return;
            }

            for (int i = 0; i < snapshot.WorldItems.Length; i++)
            {
                ItemLifecycleWorldItemSnapshot item = snapshot.WorldItems[i];
                string text = "#" + item.WorldItemId + " " + item.ItemName + " x" + item.Count
                    + " @ " + item.X + "," + item.Z
                    + " d=" + item.Distance
                    + (item.InRange ? "  IN RANGE" : string.Empty);
                _worldList.Add(CreateItemRow(item.Icon, text, item.InRange ? "positive-row" : "text-row"));
            }
        }

        private void RebuildSlots(VisualElement target, ItemLifecycleSlotSnapshot[] slots, bool allowSelection)
        {
            if (target == null)
                return;

            target.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                ItemLifecycleSlotSnapshot slot = slots[i];
                var element = new VisualElement();
                element.AddToClassList("slot-cell");
                if (slot.IsEmpty)
                    element.AddToClassList("slot-empty");
                if (slot.IsSelected)
                    element.AddToClassList("slot-selected");
                if (allowSelection)
                {
                    element.AddToClassList("slot-clickable");
                    int slotIndex = slot.SlotIndex;
                    element.RegisterCallback<ClickEvent>(_ =>
                    {
                        _showcase.SelectEquipmentSlot(slotIndex);
                        RefreshUi();
                    });
                }

                var body = new VisualElement();
                body.AddToClassList("slot-body");
                body.Add(CreateItemIcon(slot.Icon));

                var textGroup = new VisualElement();
                textGroup.AddToClassList("slot-text");
                var title = new Label(slot.SlotName);
                title.AddToClassList("slot-title");
                var value = new Label(slot.IsEmpty ? "Empty" : slot.ItemName + " x" + slot.Count);
                value.AddToClassList("slot-value");
                ApplyLabelStyle(title, 10, new Color(0.62f, 0.68f, 0.75f), FontStyle.Normal, TextAnchor.MiddleLeft);
                ApplyLabelStyle(value, 12, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleLeft);
                textGroup.Add(title);
                textGroup.Add(value);
                body.Add(textGroup);
                element.Add(body);
                target.Add(element);
            }
        }

        private void RebuildLog(ItemLifecycleShowcaseSnapshot snapshot)
        {
            if (_logList == null)
                return;

            _logList.Clear();
            int start = Mathf.Max(0, snapshot.Log.Length - 8);
            for (int i = start; i < snapshot.Log.Length; i++)
                _logList.Add(CreateTextRow(snapshot.Log[i], "text-row"));
        }

        private Label CreateTextRow(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            ApplyLabelStyle(label, 12, new Color(0.88f, 0.91f, 0.95f), FontStyle.Normal, TextAnchor.MiddleLeft);
            return label;
        }

        private VisualElement CreateItemRow(ItemLifecycleIconSnapshot icon, string text, string className)
        {
            var row = new VisualElement();
            row.AddToClassList("item-row");
            row.Add(CreateItemIcon(icon));

            var label = new Label(text);
            label.AddToClassList(className);
            label.AddToClassList("item-row-text");
            ApplyLabelStyle(label, 12, new Color(0.88f, 0.91f, 0.95f), FontStyle.Normal, TextAnchor.MiddleLeft);
            row.Add(label);
            return row;
        }

        private VisualElement CreateItemIcon(ItemLifecycleIconSnapshot icon)
        {
            var image = new Image();
            image.AddToClassList("item-icon");
            image.scaleMode = ScaleMode.ScaleToFit;

            Sprite sprite = ResolveIcon(icon);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.AddToClassList("item-icon-loaded");
                return image;
            }

            image.AddToClassList(icon.IsValid ? "item-icon-missing" : "item-icon-empty");
            return image;
        }

        private Sprite ResolveIcon(ItemLifecycleIconSnapshot icon)
        {
            if (!icon.IsValid)
                return null;

            EnsureIconResources();
            if (_iconResources == null)
                return null;

            ResourceKey key = icon.ToResourceKey();
            if (_iconHandles.TryGetValue(key, out ResourceHandle<Sprite> cached) && cached != null && !cached.IsReleased)
                return cached.Value;
            if (_missingIconKeys.Contains(key))
                return null;

            ResourceLoadResult<ResourceHandle<Sprite>> load = _iconResources.Load<Sprite>(key);
            if (!load.Success)
            {
                LogMissingIconOnce(key, load.Error.Message);
                return null;
            }

            _iconHandles[key] = load.Value;
            return load.Value.Value;
        }

        private void EnsureIconResources()
        {
            if (_iconResources != null)
                return;

            _iconResources = CreateIconResourceManager(_ironSwordIcon);
            if (_ironSwordIcon == null)
                Debug.LogWarning("Item Lifecycle Showcase iron sword icon is not assigned.");
        }

        private void ReleaseIconResources()
        {
            if (_iconResources != null)
            {
                foreach (KeyValuePair<ResourceKey, ResourceHandle<Sprite>> pair in _iconHandles)
                    _iconResources.Release(pair.Value);
                _iconResources.ReleaseAllLoaded("itemLifecycleShowcaseDisable");
            }

            _iconHandles.Clear();
            _missingIconKeys.Clear();
            _iconResources = null;
        }

        private void LogMissingIconOnce(ResourceKey key, string message)
        {
            if (!_missingIconKeys.Add(key))
                return;

            Debug.LogWarning("Item Lifecycle Showcase icon missing: " + key + " / " + message);
        }

        private void ApplyRuntimeTextStyles()
        {
            EnsureRuntimeFont();
            ApplyLabelsByClass(_root, "section-title", 14, new Color(0.80f, 0.86f, 0.92f), FontStyle.Bold, TextAnchor.MiddleLeft);
            ApplyLabelsByClass(_root, "stat-name", 12, new Color(0.58f, 0.64f, 0.70f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLabelsByClass(_root, "stat-value", 13, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyLabelStyle(_root?.Q<Label>(className: "showcase-title"), 24, new Color(0.96f, 0.97f, 0.98f), FontStyle.Bold, TextAnchor.MiddleLeft);
            ApplyLabelStyle(_hashLabel, 11, new Color(0.86f, 0.88f, 0.91f), FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLabelStyle(_equipmentLabel, 12, new Color(0.93f, 0.95f, 0.97f), FontStyle.Bold, TextAnchor.MiddleRight);
            ApplyButtons(_root);
        }

        private void ApplyLabelsByClass(VisualElement root, string className, int size, Color color, FontStyle style, TextAnchor alignment)
        {
            if (root == null)
                return;
            var labels = root.Query<Label>(className: className).ToList();
            for (int i = 0; i < labels.Count; i++)
                ApplyLabelStyle(labels[i], size, color, style, alignment);
        }

        private void ApplyButtons(VisualElement root)
        {
            if (root == null)
                return;
            var buttons = root.Query<Button>(className: "command-button").ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                button.focusable = false;
                if (_runtimeFont != null)
                    button.style.unityFont = new StyleFont(_runtimeFont);
                button.style.fontSize = 12;
                button.style.color = new Color(0.96f, 0.97f, 0.98f);
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.unityTextAlign = TextAnchor.MiddleCenter;
                button.style.whiteSpace = WhiteSpace.NoWrap;
                button.style.minHeight = 28;
            }
        }

        private void ApplyLabelStyle(Label label, int size, Color color, FontStyle style, TextAnchor alignment)
        {
            if (label == null)
                return;
            if (_runtimeFont != null)
                label.style.unityFont = new StyleFont(_runtimeFont);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = style;
            label.style.unityTextAlign = alignment;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        private void EnsureRuntimeFont()
        {
            if (_runtimeFont != null)
                return;
            _runtimeFont = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void ApplyTargetFrameRate()
        {
            if (_hasPreviousTargetFrameRate)
                return;
            _previousTargetFrameRate = Application.targetFrameRate;
            _hasPreviousTargetFrameRate = true;
            Application.targetFrameRate = _targetFrameRate;
        }

        private void RestoreTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
                return;
            Application.targetFrameRate = _previousTargetFrameRate;
            _hasPreviousTargetFrameRate = false;
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && !string.Equals(label.text, text, StringComparison.Ordinal))
                label.text = text;
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings template)
        {
            PanelSettings settings = template != null
                ? Instantiate(template)
                : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "ItemLifecycleShowcasePanelSettingsInstance";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            return settings;
        }

        public static ResourceKey IronSwordIconKey => new ResourceKey(IronSwordIconResourceId, ResourceTypeIds.Sprite);

        public static ResourceManager CreateIconResourceManager(Sprite ironSwordIcon)
        {
            var provider = new MemoryResourceProvider();
            if (ironSwordIcon != null)
                provider.Register(IronSwordIconAddress, ironSwordIcon);

            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(CreateIconCatalog());
            return manager;
        }

        public static ResourceCatalog CreateIconCatalog()
        {
            return new ResourceCatalog(
                IconCatalogId,
                string.Empty,
                new[]
                {
                    new ResourceCatalogEntry(
                        IronSwordIconResourceId,
                        ResourceTypeIds.Sprite,
                        MemoryResourceProvider.Id,
                        IronSwordIconAddress,
                        labels: new[] { IconCatalogLabel })
                });
        }
    }
}
