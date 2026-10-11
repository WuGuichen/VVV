using System;
using MxFramework.Input;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;

namespace MxFramework.Demo.FairyGui
{
    public sealed class UiFoundationDemoPreviewBootstrap : MonoBehaviour
    {
        private const string PackageAddress = "fgui/MxUiFoundationDemo_fui.bytes";
        private const string AtlasAddress = "fgui/MxUiFoundationDemo_atlas0.png";
        private const string CursorOwner = "ui.foundation.demo.preview";

        [SerializeField] private TextAsset _packageBytes;
        [SerializeField] private Texture2D _atlas0;

        private readonly InputContextStack _inputContexts = new InputContextStack();
        private MxCursorController _cursorController;
        private IDisposable _gameplayCursorScope;
        private MxFairyGuiLayerHost _layerHost;
        private MxFairyGuiNavigator _navigator;
        private UiFoundationDemoCommandSink _commandSink;
        private int _executionCount;
        private bool _alternateStatus;
        private string _statusText = "等待操作";

        public int ExecutionCount => _executionCount;
        public string StatusText => _statusText;
        public bool IsOpen => _navigator != null && _navigator.IsOpen(UiFoundationDemoFairyGuiIds.ViewId);
        public InputContext ActiveInputContext => _inputContexts.ActiveContext;
        public MxCursorState CursorState => _cursorController != null
            ? _cursorController.Current
            : MxCursorState.DefaultReleased;

        public void Configure(TextAsset packageBytes, Texture2D atlas0)
        {
            _packageBytes = packageBytes;
            _atlas0 = atlas0;
        }

        private void Start()
        {
            Initialize();
        }

        public MxUiOpenResult Initialize()
        {
            if (_navigator != null)
                return Refresh();
            if (_packageBytes == null || _atlas0 == null)
            {
                return MxUiOpenResult.Fail(
                    MxUiOpenErrorCode.ViewCreateFailed,
                    "UI Foundation Demo published package assets are not assigned.");
            }

            var provider = new MemoryResourceProvider()
                .Register(PackageAddress, _packageBytes)
                .Register(AtlasAddress, _atlas0);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(CreateResourceCatalog());

            _inputContexts.Set(InputContext.Gameplay);
            _cursorController = new MxCursorController();
            _gameplayCursorScope = _cursorController.Push(MxCursorRequest.Captured(CursorOwner + ".gameplay"));
            _layerHost = new MxFairyGuiLayerHost();
            _commandSink = new UiFoundationDemoCommandSink(HandleCommand);
            var inputBridge = new MxFairyGuiInputContextBridge(_ => new UiInputScope(
                _inputContexts.Push(InputContext.UI, InputContextPolicy.Exclusive),
                _cursorController.Push(MxCursorRequest.Released(CursorOwner))));
            _navigator = UiFoundationDemoComposition.CreateNavigator(
                manager,
                _commandSink,
                _layerHost,
                inputBridge);

            return Refresh();
        }

        public MxUiOpenResult Refresh()
        {
            if (_navigator == null)
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "UI Foundation Demo is not initialized.");

            return _navigator.Open(UiFoundationDemoFairyGuiIds.ViewId, CreateViewModel());
        }

        public bool ClosePanel()
        {
            return _navigator != null && _navigator.Close(UiFoundationDemoFairyGuiIds.ViewId);
        }

        private ResourceCatalog CreateResourceCatalog()
        {
            return new ResourceCatalog(
                "ui.foundation.demo.preview",
                UiFoundationDemoFairyGuiIds.PackageId,
                new[]
                {
                    new ResourceCatalogEntry(
                        UiFoundationDemoFairyGuiIds.PackageBytesResourceId,
                        MxFairyGuiResourceTypeIds.PackageBytes,
                        MemoryResourceProvider.Id,
                        PackageAddress,
                        packageId: UiFoundationDemoFairyGuiIds.PackageId),
                    new ResourceCatalogEntry(
                        UiFoundationDemoFairyGuiIds.Atlas0ResourceId,
                        ResourceTypeIds.Texture2D,
                        MemoryResourceProvider.Id,
                        AtlasAddress,
                        packageId: UiFoundationDemoFairyGuiIds.PackageId)
                });
        }

        private UiFoundationDemoViewModel CreateViewModel()
        {
            return new UiFoundationDemoViewModel
            {
                StatusText = _statusText,
                PrimaryLabel = "主要操作",
                SecondaryLabel = "切换状态",
                CloseLabel = "关闭",
                CanInteract = true
            };
        }

        private void HandleCommand(string commandId)
        {
            switch (commandId)
            {
                case UiFoundationDemoCommandIds.Primary:
                    _executionCount++;
                    _statusText = "已执行 " + _executionCount + " 次";
                    Refresh();
                    break;
                case UiFoundationDemoCommandIds.Secondary:
                    _alternateStatus = !_alternateStatus;
                    _statusText = _alternateStatus ? "状态：已切换" : "状态：等待操作";
                    Refresh();
                    break;
                case UiFoundationDemoCommandIds.Close:
                    ClosePanel();
                    break;
            }
        }

        private void OnDestroy()
        {
            ClosePanel();
            _layerHost?.Dispose();
            _gameplayCursorScope?.Dispose();
            _cursorController?.Dispose();
            _navigator = null;
            _layerHost = null;
            _gameplayCursorScope = null;
            _cursorController = null;
        }

        private sealed class UiInputScope : IDisposable
        {
            private IDisposable _inputScope;
            private IDisposable _cursorScope;

            public UiInputScope(IDisposable inputScope, IDisposable cursorScope)
            {
                _inputScope = inputScope;
                _cursorScope = cursorScope;
            }

            public void Dispose()
            {
                IDisposable cursorScope = _cursorScope;
                IDisposable inputScope = _inputScope;
                _cursorScope = null;
                _inputScope = null;
                cursorScope?.Dispose();
                inputScope?.Dispose();
            }
        }
    }
}
