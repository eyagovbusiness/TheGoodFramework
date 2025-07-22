using Microsoft.AspNetCore.Hosting;
using System.ComponentModel;

namespace TGF.CA.Infrastructure {
    /// <summary>
    /// Enumeration representing different cloud providers.
    /// </summary>
    public enum HostEnvironmentEnum {
        [Description("Azure")]
        Azure,
        [Description("AWS")]
        AWS,
        [Description("Docker")]
        Docker
    }

    /// <summary>
    /// Extension methods for IWebHostEnvironment to determine the current environment.
    /// </summary>
    public static class EnvironmentExtensions {
        /// <summary>
        /// Determines if the current environment is a development environment.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the environment name starts with "dev_", otherwise false.</returns>
        public static bool IsDevelopment(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.EnvironmentName.StartsWith("dev_");

        /// <summary>
        /// Determines if the current environment is a staging environment.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the environment name starts with "stg_", otherwise false.</returns>
        public static bool IsStaging(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.EnvironmentName.StartsWith("stg_");

        /// <summary>
        /// Determines if the current environment is a production environment.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the environment is neither development nor staging, otherwise false.</returns>
        public static bool IsProduction(this IWebHostEnvironment webHostEnvironment)
        => !(webHostEnvironment.IsDevelopment() || webHostEnvironment.IsStaging());

        /// <summary>
        /// Determines if the current environment is hosted on Azure.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the host environment is Azure, otherwise false.</returns>
        public static bool IsAzure(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.GetHostEnvironment() == HostEnvironmentEnum.Azure;

        /// <summary>
        /// Determines if the current environment is hosted on AWS.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the host environment is AWS, otherwise false.</returns>
        public static bool IsAWS(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.GetHostEnvironment() == HostEnvironmentEnum.AWS;

        /// <summary>
        /// Determines if the current environment is hosted on Docekr.
        /// </summary>
        /// <param name="webHostEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>True if the host environment is AWS, otherwise false.</returns>
        public static bool IsDocker(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.GetHostEnvironment() == HostEnvironmentEnum.Docker;

        /// <summary>
        /// Gets the cloud provider based on the environment name.
        /// </summary>
        public static HostEnvironmentEnum GetHostEnvironment(this IWebHostEnvironment webHostEnvironment) {
            var envName = webHostEnvironment.EnvironmentName;
            string cloudProvider;
            var parts = envName.Split('_');
            cloudProvider = parts.Length > 1 ? parts[1] : envName;
            Console.WriteLine($"Detected cloud provider: {cloudProvider}");
            return Enum.TryParse(typeof(HostEnvironmentEnum), cloudProvider, false, out var result) && result is HostEnvironmentEnum parsedResult
                ? parsedResult
                : throw new InvalidOperationException($"Invalid cloud provider: {cloudProvider}");
        }
    }
}
