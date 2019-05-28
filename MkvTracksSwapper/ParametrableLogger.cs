using Microsoft.Extensions.Logging;
using System;

namespace MkvTracksSwapper
{
    public class ParametrableLogger : ILogger
    {
        private LogLevel _logLevel;

        public ParametrableLogger(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_logLevel == LogLevel.None)
                return false;

            return logLevel >= _logLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
                Console.WriteLine(state);
        }
    }
}
