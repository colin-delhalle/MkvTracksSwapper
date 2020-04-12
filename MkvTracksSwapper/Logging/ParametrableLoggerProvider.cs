using Microsoft.Extensions.Logging;

namespace MkvTracksSwapper.Logging
{
    public sealed class ParametrableLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel logLevel;

        public ParametrableLoggerProvider(LogLevel logLevel)
        {
            this.logLevel = logLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ParametrableLogger(logLevel);
        }

        public void Dispose()
        {
        }
    }
}