using Serilog;

namespace RetroFootballManager.Logging
{
    // Static facade in the usual GetLogger<T>() style. Delegates to the globally
    // configured Serilog logger (set up in MauiProgram). If no logger is configured,
    // Serilog silently discards the output - callers don't need to check anything.
    public static class LogManager
    {
        public static ILog GetLogger<T>() => new SerilogLog(Log.ForContext<T>());

        public static ILog GetLogger(string context) =>
            new SerilogLog(Log.ForContext("SourceContext", context));

        private sealed class SerilogLog : ILog
        {
            private readonly ILogger _logger;

            public SerilogLog(ILogger logger) => _logger = logger;

            public void Debug(string message) => _logger.Debug(message);
            public void Info(string message) => _logger.Information(message);
            public void Warn(string message) => _logger.Warning(message);

            public void Error(string message, Exception? exception = null)
            {
                if (exception is null) _logger.Error(message);
                else _logger.Error(exception, message);
            }

            public void Fatal(string message, Exception? exception = null)
            {
                if (exception is null) _logger.Fatal(message);
                else _logger.Fatal(exception, message);
            }
        }
    }
}
