namespace TGF.CA.Application;

/// <summary>Canonical deployment platform identity shared by application contracts.</summary>
public enum DeploymentPlatform {
    Docker = 0,
    AWS = 1,
    Azure = 2,
    GCP = 3
}

/// <summary>Canonical deployment stage identity shared by application contracts.</summary>
public enum DeploymentStage {
    Development = 0,
    Staging = 1,
    Production = 2
}

public static class DeploymentPlatformName {
    public const string Docker = nameof(DeploymentPlatform.Docker);
    public const string AWS = nameof(DeploymentPlatform.AWS);
    public const string Azure = nameof(DeploymentPlatform.Azure);
    public const string GCP = nameof(DeploymentPlatform.GCP);

    public static string GetName(DeploymentPlatform platform) => platform switch {
        DeploymentPlatform.Docker => Docker,
        DeploymentPlatform.AWS => AWS,
        DeploymentPlatform.Azure => Azure,
        DeploymentPlatform.GCP => GCP,
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static bool TryParse(string? name, out DeploymentPlatform platform) {
        platform = name switch {
            Docker => DeploymentPlatform.Docker,
            AWS => DeploymentPlatform.AWS,
            Azure => DeploymentPlatform.Azure,
            GCP => DeploymentPlatform.GCP,
            _ => default
        };

        return name is Docker or AWS or Azure or GCP;
    }
}

public interface IDeploymentEnvironment {
    DeploymentPlatform Platform { get; }
    DeploymentStage Stage { get; }
    string StageTag { get; }
}

public sealed record DeploymentEnvironment(DeploymentPlatform Platform, DeploymentStage Stage) : IDeploymentEnvironment {
    public string StageTag => DeploymentStageTag.GetTag(Stage);
}

public static class DeploymentEnvironmentExtensions {
    public static bool IsDevelopment(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Stage == DeploymentStage.Development;

    public static bool IsStaging(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Stage == DeploymentStage.Staging;

    public static bool IsProduction(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Stage == DeploymentStage.Production;

    public static bool IsAzure(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Platform == DeploymentPlatform.Azure;

    public static bool IsAws(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Platform == DeploymentPlatform.AWS;

    public static bool IsDocker(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Platform == DeploymentPlatform.Docker;

    public static bool IsGcp(this IDeploymentEnvironment deploymentEnvironment)
        => deploymentEnvironment.Platform == DeploymentPlatform.GCP;
}

public static class DeploymentStageTag {
    public const string Development = "dev";
    public const string Staging = "stg";
    public const string Production = "";

    public static string GetTag(DeploymentStage stage) => stage switch {
        DeploymentStage.Development => Development,
        DeploymentStage.Staging => Staging,
        DeploymentStage.Production => Production,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    public static bool TryParse(string? tag, out DeploymentStage stage) {
        stage = tag switch {
            Development => DeploymentStage.Development,
            Staging => DeploymentStage.Staging,
            Production => DeploymentStage.Production,
            _ => default
        };

        return tag is Development or Staging or Production;
    }
}
