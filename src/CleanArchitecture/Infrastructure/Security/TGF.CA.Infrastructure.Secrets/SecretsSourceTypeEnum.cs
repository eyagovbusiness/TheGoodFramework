using System.ComponentModel;

namespace TGF.CA.Infrastructure.Secrets;
/// <summary>
/// Enum to specify the source of the secrets.
/// </summary>
public enum SecretsSourceTypeEnum {
    /// <summary>
    /// The secrets are stored in a file.
    /// </summary>
    [Description("File")]
    File,
    /// <summary>
    /// The secrets are stored in a secrets manager.
    /// </summary>
    [Description("SecretsManager")]
    SecretsManager
}