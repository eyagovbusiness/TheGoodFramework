namespace TheGoodFramework.Common.Logging.Loggers
{
    /// <summary>
    /// Class to bind json configurarion to serilog Graylog sink.
    /// </summary>
    public sealed class GraylogLoggerConfiguration : LoggerConfigurationBase
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }

    }
}