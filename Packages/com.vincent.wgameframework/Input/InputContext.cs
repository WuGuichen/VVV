using System;

namespace MxFramework.Input
{
    public enum InputContext
    {
        Disabled = 0,
        Gameplay = 10,
        UI = 20,
        Vehicle = 30,
        PhotoMode = 40,
        Cutscene = 50,
        Rebinding = 60,
        Debug = 70
    }

    public enum InputContextPolicy
    {
        Exclusive = 0,
        Overlay = 1
    }

    public readonly struct InputContextLayer : IEquatable<InputContextLayer>
    {
        public InputContextLayer(InputContext context, InputContextPolicy policy)
        {
            Context = context;
            Policy = policy;
        }

        public InputContext Context { get; }
        public InputContextPolicy Policy { get; }

        public bool BlocksLowerContexts => Policy == InputContextPolicy.Exclusive;

        public bool Equals(InputContextLayer other)
        {
            return Context == other.Context && Policy == other.Policy;
        }

        public override bool Equals(object obj)
        {
            return obj is InputContextLayer other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Context * 397) ^ (int)Policy;
            }
        }

        public override string ToString()
        {
            return Context + ":" + Policy;
        }
    }
}
