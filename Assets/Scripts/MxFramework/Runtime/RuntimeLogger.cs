using System.Text;

namespace MxFramework.Runtime
{
    public enum RuntimeLogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public interface IRuntimeLogger
    {
        void Log(RuntimeLogLevel level, string category, string message);
    }

    /// <summary>
    /// 可复用 <see cref="StringBuilder"/>，用于组合日志正文，避免多次字符串拼接分配。
    /// </summary>
    public sealed class RuntimeLogBuffer
    {
        private readonly StringBuilder _builder;

        public RuntimeLogBuffer(int capacity = 128)
        {
            _builder = new StringBuilder(capacity);
        }

        public int Length => _builder.Length;

        public RuntimeLogBuffer Clear()
        {
            _builder.Clear();
            return this;
        }

        public RuntimeLogBuffer Append(string value)
        {
            if (!string.IsNullOrEmpty(value))
                _builder.Append(value);
            return this;
        }

        public RuntimeLogBuffer Append(char value)
        {
            _builder.Append(value);
            return this;
        }

        public RuntimeLogBuffer Append(int value)
        {
            _builder.Append(value);
            return this;
        }

        public RuntimeLogBuffer Append(long value)
        {
            _builder.Append(value);
            return this;
        }

        public RuntimeLogBuffer Append(bool value)
        {
            _builder.Append(value);
            return this;
        }

        internal string MaterializeAndClear()
        {
            if (_builder.Length == 0)
                return string.Empty;

            string text = _builder.ToString();
            _builder.Clear();
            return text;
        }
    }

    public sealed class NullRuntimeLogger : IRuntimeLogger
    {
        public static readonly NullRuntimeLogger Instance = new NullRuntimeLogger();

        private NullRuntimeLogger()
        {
        }

        public void Log(RuntimeLogLevel level, string category, string message)
        {
        }
    }

    public static class RuntimeLoggerExtensions
    {
        public static void Info(this IRuntimeLogger logger, string category, string message)
        {
            (logger ?? NullRuntimeLogger.Instance).Log(RuntimeLogLevel.Info, category, message);
        }

        public static void Warning(this IRuntimeLogger logger, string category, string message)
        {
            (logger ?? NullRuntimeLogger.Instance).Log(RuntimeLogLevel.Warning, category, message);
        }

        public static void Error(this IRuntimeLogger logger, string category, string message)
        {
            (logger ?? NullRuntimeLogger.Instance).Log(RuntimeLogLevel.Error, category, message);
        }

        public static void Info(this IRuntimeLogger logger, string category, RuntimeLogBuffer buffer)
        {
            LogBuffered(logger, RuntimeLogLevel.Info, category, buffer);
        }

        public static void Warning(this IRuntimeLogger logger, string category, RuntimeLogBuffer buffer)
        {
            LogBuffered(logger, RuntimeLogLevel.Warning, category, buffer);
        }

        public static void Error(this IRuntimeLogger logger, string category, RuntimeLogBuffer buffer)
        {
            LogBuffered(logger, RuntimeLogLevel.Error, category, buffer);
        }

        private static void LogBuffered(IRuntimeLogger logger, RuntimeLogLevel level, string category, RuntimeLogBuffer buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return;

            (logger ?? NullRuntimeLogger.Instance).Log(level, category, buffer.MaterializeAndClear());
        }
    }
}
