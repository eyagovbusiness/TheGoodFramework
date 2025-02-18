using Microsoft.AspNetCore.Hosting;

namespace TGF.CA.Infrastructure {
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
    }
}
