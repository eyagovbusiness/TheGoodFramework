using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Slascone.Client;
using Slascone.Client.Interfaces;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TGF.CA.Infrastructure.InvariantConstants;
using TGF.CA.Infrastructure.Licensing.Slascone.Contracts;
using TGF.CA.Infrastructure.Secrets.SecretsFiles;

namespace TGF.CA.Infrastructure.Licensing.Slascone.Services;

public enum SignatureValidationMode {
    None = 0,
    Symmetric = 1,
    Asymmetric = 2
}

/// <summary>
/// Factory class to create and configure an instance of ISlasconeClientV2. Uses RSA public key for Asymetric signature validation.
/// </summary>
/// <remarks>
/// It depends on ISecretFilesService to retrieve the RSA public key from a secret file. For this appsetttings must configure <see cref="ConfigurationKeys.SecretsFiles.SecretsFileNames.LicensePemSecret"/> to point to the secret file containing the PEM encoded RSA public key.
/// </remarks>
public class SlasconeClientFactory(
    ISecretFilesService secretFilesService,
    IOptions<SlasconeOptions> slasconeOptions,
    IConfiguration configuration
) : ISlasconeClientFactory {
    public async Task<ISlasconeClientV2> GetClientAsync() {
        var provisioningKey = await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.LicenseProvisioningKeySecret);
        var slasconeSingletonClient = SlasconeClientV2Factory.BuildClient(slasconeOptions.Value.ApiBaseUrl, slasconeOptions.Value.IsvId, provisioningKey);
        var signaturePubKeyPem = await secretFilesService.GetSecretFromConfigAsync(ConfigurationKeys.SecretsFiles.SecretsFileNames.LicensePemSecret);
        using (var rsa = RSA.Create()) {
            rsa.ImportFromPem(signaturePubKeyPem.ToCharArray());
            slasconeSingletonClient
                .SetSignaturePublicKey(new PublicKey(rsa))
                .SetSignatureValidationMode((int)SignatureValidationMode.Asymmetric);
        }

        slasconeSingletonClient.SetCheckHttpsCertificate().SetLastModifiedByHeader(configuration[ConfigurationKeys.AppMetadata.AppName]);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            var appDataFolder =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Assembly.GetExecutingAssembly().GetName().Name!);
            slasconeSingletonClient.SetAppDataFolder(appDataFolder);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            slasconeSingletonClient.SetAppDataFolder(Environment.CurrentDirectory);
        }

        return slasconeSingletonClient;
    }
}

