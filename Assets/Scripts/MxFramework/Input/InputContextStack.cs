using System;
using System.Collections.Generic;

namespace MxFramework.Input
{
    public sealed class InputContextStack
    {
        private readonly List<Entry> _layers = new List<Entry>();
        private int _nextToken;

        public event Action Changed;

        public int Count => _layers.Count;

        public InputContext ActiveContext
        {
            get
            {
                if (_layers.Count == 0)
                {
                    return InputContext.Disabled;
                }

                return _layers[_layers.Count - 1].Layer.Context;
            }
        }

        public InputContextLayer ActiveLayer
        {
            get
            {
                if (_layers.Count == 0)
                {
                    return new InputContextLayer(InputContext.Disabled, InputContextPolicy.Exclusive);
                }

                return _layers[_layers.Count - 1].Layer;
            }
        }

        public void Set(InputContext context)
        {
            _layers.Clear();
            if (context != InputContext.Disabled)
            {
                _layers.Add(new Entry(_nextToken++, new InputContextLayer(context, InputContextPolicy.Exclusive)));
            }

            NotifyChanged();
        }

        public IDisposable Push(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive)
        {
            if (context == InputContext.Disabled)
            {
                Set(InputContext.Disabled);
                return NullScope.Instance;
            }

            int token = _nextToken++;
            _layers.Add(new Entry(token, new InputContextLayer(context, policy)));
            NotifyChanged();
            return new Scope(this, token);
        }

        public bool TryGetLayer(int index, out InputContextLayer layer)
        {
            if (index < 0 || index >= _layers.Count)
            {
                layer = default;
                return false;
            }

            layer = _layers[index].Layer;
            return true;
        }

        public void Clear()
        {
            if (_layers.Count == 0)
            {
                return;
            }

            _layers.Clear();
            NotifyChanged();
        }

        public int FillEnabledContexts(List<InputContext> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                InputContextLayer layer = _layers[i].Layer;
                if (layer.Context != InputContext.Disabled)
                {
                    destination.Add(layer.Context);
                }

                if (layer.BlocksLowerContexts)
                {
                    break;
                }
            }

            return destination.Count;
        }

        private void Pop(int token)
        {
            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                if (_layers[i].Token != token)
                {
                    continue;
                }

                _layers.RemoveAt(i);
                NotifyChanged();
                return;
            }
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private readonly struct Entry
        {
            public Entry(int token, InputContextLayer layer)
            {
                Token = token;
                Layer = layer;
            }

            public int Token { get; }
            public InputContextLayer Layer { get; }
        }

        private sealed class Scope : IDisposable
        {
            private InputContextStack _owner;
            private readonly int _token;

            public Scope(InputContextStack owner, int token)
            {
                _owner = owner;
                _token = token;
            }

            public void Dispose()
            {
                InputContextStack owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.Pop(_token);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
