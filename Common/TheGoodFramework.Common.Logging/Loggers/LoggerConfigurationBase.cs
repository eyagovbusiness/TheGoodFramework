using Serilog.Events;

namespace TheGoodFramework.Common.Logging.Loggers
{
    /// <summary>
    /// Base class that any serilog configuration in this library should inherit.
    /// </summary>
    public abstract class LoggerConfigurationBase
    {
        public bool Enabled { get; set; } = false;
        public LogEventLevel MinimumLevel { get; set; }
    }
}
