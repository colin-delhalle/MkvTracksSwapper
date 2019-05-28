using Microsoft.Extensions.Logging;

namespace MkvTracksSwapper
{
    public class ParametrableLoggerProvider : ILoggerProvider
    {
        private LogLevel _logLevel;

        public ParametrableLoggerProvider(LogLevel logLevel)
        {
            _logLevel = logLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ParametrableLogger(_logLevel);
        }

        public void Dispose()
        {
        }
    }
}
