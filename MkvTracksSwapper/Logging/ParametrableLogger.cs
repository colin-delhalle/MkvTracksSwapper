using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MkvTracksSwapper.Logging
{
    public class ParametrableLogger : ILogger
    {
        private readonly LogLevel logLevel;

        private readonly object lockObject;

        public ParametrableLogger(LogLevel logLevel)
        {
            lockObject = new object();
            this.logLevel = logLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (this.logLevel == LogLevel.None)
            {
                return false;
            }

            return logLevel >= this.logLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                lock (lockObject)
                {
                    Console.WriteLine($"{state} in thread {Thread.CurrentThread.ManagedThreadId}");
                }
            }
        }
    }
}