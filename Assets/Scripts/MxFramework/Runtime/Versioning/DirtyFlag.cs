namespace MxFramework.Runtime
{
    public sealed class DirtyFlag
    {
        private bool _isDirty;
        private int _version;

        public bool IsDirty => _isDirty;

        public int Version => _version;

        public void MarkDirty()
        {
            _isDirty = true;
            _version++;
        }

        public bool Consume()
        {
            bool wasDirty = _isDirty;
            _isDirty = false;
            return wasDirty;
        }
    }
}
