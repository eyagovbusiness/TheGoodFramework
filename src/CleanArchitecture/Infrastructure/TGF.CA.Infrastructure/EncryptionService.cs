using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using TGF.CA.Application;

namespace TGF.CA.Infrastructure
{
    public class EncryptionService : IEncryptionService
    {

        #region Private
        private class EncryptionSecrets
        {
            public string? Key { get; set; }
            public string? InitializationVector { get; set; }

            public bool IsValid()
            {
                if(Key == null || InitializationVector == null)
                    return false;

                var lKey = Encoding.UTF8.GetBytes(Key);
                var lInitializationVector = Encoding.UTF8.GetBytes(InitializationVector);

                bool lIsKeyValid = lKey != null && lKey.Length == 32;
                bool lIsInitializationVectorValid = lInitializationVector != null && lInitializationVector.Length == 16;

                return lIsKeyValid && lIsInitializationVectorValid;
            }

            public byte[] getKeyAsByteArray()
            => Encoding.UTF8.GetBytes(Key!);
            public byte[] getInitializationVectorAsByteArray()
            => Encoding.UTF8.GetBytes(InitializationVector!);
        }

        private readonly Lazy<Task<EncryptionSecrets>> _encryptionSecrets;
        private readonly ILogger<EncryptionService> _logger;

        /// <summary>
        /// Used for Lazy initialization of the encryption secrets.
        /// </summary>
        private async Task<EncryptionSecrets> GetEncryptionSecrets(ISecretsManager aSecretsManager)
        {
            try
            {
                var lEncryptionSecrets = await aSecretsManager.Get<EncryptionSecrets>("encryptionSecrets")
                    ?? throw new Exception("Error loading the encryption secrets!!");

                if (!lEncryptionSecrets.IsValid())
                    throw new Exception("The loaded encryption secrets are not valid, ensure they follow the constrains defined in the EncryptionService!!");

                return lEncryptionSecrets;
            }
            catch (Exception lEx)
            {
                _logger.LogError(lEx, "Error initializing the EncryptionService.");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionService"/> class with the specified key and initialization vector.
        /// </summary>
        /// <param name="aSecretsManager">The secrets manager from where to get the encryption secrets.</param>
        /// <param name="aLogger">Logger for this service.</param>
        public EncryptionService(ISecretsManager aSecretsManager, ILogger<EncryptionService> aLogger)
        {
            if(aSecretsManager == null)
                throw new ArgumentNullException(nameof(aSecretsManager), "Secrets manager was null.");
            _logger = aLogger;
            _encryptionSecrets = new Lazy<Task<EncryptionSecrets>>(GetEncryptionSecrets(aSecretsManager));
        }

        #region IEncryptionService
        public async Task<string> EncryptAsync(string aPlainText)
        {
            using var lAes = Aes.Create();
            lAes.Key = (await _encryptionSecrets.Value).getKeyAsByteArray();
            lAes.IV = (await _encryptionSecrets.Value).getInitializationVectorAsByteArray();

            var lEncryptor = lAes.CreateEncryptor(lAes.Key, lAes.IV);
            using var lMs = new MemoryStream();
            using (var lCs = new CryptoStream(lMs, lEncryptor, CryptoStreamMode.Write))
            using (var lSw = new StreamWriter(lCs))
            {
                lSw.Write(aPlainText);
            }
            return Convert.ToBase64String(lMs.ToArray());
        }

        public async Task<string> DecryptAsync(string aCipherText)
        {
            using var lAes = Aes.Create();
            lAes.Key = (await _encryptionSecrets.Value).getKeyAsByteArray();
            lAes.IV = (await _encryptionSecrets.Value).getInitializationVectorAsByteArray();

            var lDecryptor = lAes.CreateDecryptor(lAes.Key, lAes.IV);
            using var lMs = new MemoryStream(Convert.FromBase64String(aCipherText));
            using var lCs = new CryptoStream(lMs, lDecryptor, CryptoStreamMode.Read);
            using var lSr = new StreamReader(lCs);
            return lSr.ReadToEnd();
        }
        #endregion

    }

}
