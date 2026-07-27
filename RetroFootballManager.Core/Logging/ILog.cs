namespace RetroFootballManager.Logging
{
    // Schlanke Logging-Fassade, damit Core-Code und ViewModels einheitlich loggen,
    // ohne direkt an den konkreten Logger (Serilog) gebunden zu sein.
    public interface ILog
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? exception = null);
        void Fatal(string message, Exception? exception = null);
    }
}
