using System.Collections.Generic;

namespace MxFramework.Network
{
    internal sealed class NetworkSessionDiagnostics
    {
        private readonly List<string> _messages = new List<string>();
        private readonly int _capacity;

        public NetworkSessionDiagnostics(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 32;
        }

        public IReadOnlyList<string> RecentMessages => _messages;

        public void Add(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _messages.Add(message);
            if (_messages.Count > _capacity)
                _messages.RemoveAt(0);
        }
    }
}
