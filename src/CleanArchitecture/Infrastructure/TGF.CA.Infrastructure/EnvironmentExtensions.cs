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
        Docker,
        [Description("GCP")]
        GCP
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

        public static bool IsGCP(this IWebHostEnvironment webHostEnvironment)
        => webHostEnvironment.GetHostEnvironment() == HostEnvironmentEnum.GCP;

        /// <summary>
        /// Gets the cloud provider based on the environment name.
        /// </summary>
        public static HostEnvironmentEnum GetHostEnvironment(this IWebHostEnvironment webHostEnvironment) {
            var environmentName = webHostEnvironment.EnvironmentName;
            var parts = environmentName.Split('_', StringSplitOptions.None);
            var providerName = parts.Length switch {
                1 => parts[0],
                2 when parts[0] is "dev" or "stg" => parts[1],
                _ => throw InvalidHostEnvironment()
            };

            return Enum.TryParse(providerName, ignoreCase: false, out HostEnvironmentEnum provider)
                && Enum.IsDefined(provider)
                ? provider
                : throw InvalidHostEnvironment();
        }

        private static InvalidOperationException InvalidHostEnvironment()
            => new("Invalid ASPNETCORE_ENVIRONMENT provider configuration. Use Azure, AWS, Docker, or GCP, optionally prefixed by dev_ or stg_.");
    }
}
