using System.Collections.Generic;

namespace MxFramework.UI
{
    public sealed class InMemoryMxUiNavigator : IMxUiNavigator
    {
        private readonly IMxUiViewRegistry _registry;
        private readonly Dictionary<MxUiViewId, IMxUiView> _openViews = new Dictionary<MxUiViewId, IMxUiView>();

        public InMemoryMxUiNavigator(IMxUiViewRegistry registry)
        {
            _registry = registry;
        }

        public MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args)
        {
            if (!id.IsValid)
            {
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.InvalidViewId, "UI view id is required.");
            }

            IMxUiView view;
            if (_openViews.TryGetValue(id, out view))
            {
                BindView(view, args);
                view.Show();
                return MxUiOpenResult.Opened(view);
            }

            if (_registry == null || !_registry.TryCreate(id, out view))
            {
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewNotFound, "UI view is not registered: " + id + ".");
            }

            if (view == null)
            {
                return MxUiOpenResult.Fail(MxUiOpenErrorCode.ViewCreateFailed, "UI view factory returned null: " + id + ".");
            }

            BindView(view, args);
            view.Show();
            _openViews[id] = view;
            return MxUiOpenResult.Opened(view);
        }

        public MxUiOpenOperation OpenAsync<TArgs>(MxUiViewId id, TArgs args)
        {
            return MxUiOpenOperation.CompletedWith(Open(id, args));
        }

        public bool Close(MxUiViewId id)
        {
            IMxUiView view;
            if (!_openViews.TryGetValue(id, out view))
            {
                return false;
            }

            view.Hide();
            _openViews.Remove(id);
            return true;
        }

        public bool IsOpen(MxUiViewId id)
        {
            return _openViews.ContainsKey(id);
        }

        private static void BindView<TArgs>(IMxUiView view, TArgs args)
        {
            var typedView = view as IMxUiView<TArgs>;
            if (typedView != null)
            {
                typedView.Bind(args);
            }
        }
    }
}
