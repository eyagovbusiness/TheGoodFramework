using Microsoft.AspNetCore.Hosting;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure;

/// <summary>
/// Resolves the canonical deployment platform and stage from an ASP.NET Core host environment.
/// </summary>
public static class DeploymentEnvironmentResolver {
    /// <summary>
    /// Resolves the deployment environment from the specified ASP.NET Core host environment.
    /// </summary>
    /// <param name="webHostEnvironment">The host environment containing the configured environment name.</param>
    /// <returns>The resolved deployment environment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="webHostEnvironment"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The configured environment name does not use the supported deployment grammar.</exception>
    public static IDeploymentEnvironment Resolve(IWebHostEnvironment webHostEnvironment) {
        ArgumentNullException.ThrowIfNull(webHostEnvironment);
        return Resolve(webHostEnvironment.EnvironmentName);
    }

    /// <summary>
    /// Resolves the deployment environment from its canonical environment name.
    /// </summary>
    /// <param name="environmentName">The provider name, optionally prefixed with <c>dev_</c> or <c>stg_</c>.</param>
    /// <returns>The resolved deployment environment.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="environmentName"/> does not use the supported deployment grammar.</exception>
    public static IDeploymentEnvironment Resolve(string? environmentName) {
        var parts = (environmentName ?? string.Empty).Split('_', StringSplitOptions.None);
        var (stage, targetName) = parts.Length switch {
            >= 2 when DeploymentStageTag.TryParse(parts[0], out var parsedStage)
                && parsedStage != DeploymentStage.Production => (parsedStage, parts[1]),
            >= 1 => (DeploymentStage.Production, parts[0]),
            _ => throw InvalidDeploymentEnvironment()
        };

        return !DeploymentPlatformName.TryParse(targetName, out var target)
            ? throw InvalidDeploymentEnvironment()
            : (IDeploymentEnvironment)new DeploymentEnvironment(target, stage);
    }

    private static InvalidOperationException InvalidDeploymentEnvironment() {
        var platforms = string.Join(", ", Enum.GetValues<DeploymentPlatform>().Select(DeploymentPlatformName.GetName));
        var stagePrefixes = string.Join(" or ", Enum.GetValues<DeploymentStage>()
            .Select(DeploymentStageTag.GetTag)
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Select(tag => $"{tag}_"));

        return new InvalidOperationException(
            $"Invalid ASPNETCORE_ENVIRONMENT provider configuration. Use {platforms}, optionally prefixed by {stagePrefixes}.");
    }
}
