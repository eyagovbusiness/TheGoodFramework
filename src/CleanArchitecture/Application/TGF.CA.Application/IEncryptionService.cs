
namespace TGF.CA.Application
{
    /// <summary>
    /// Provides methods for encrypting and decrypting strings using AES encryption.
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts the specified plain text using AES encryption.
        /// </summary>
        /// <param name="aPlainText">The plain text to encrypt.</param>
        /// <returns>The encrypted text, encoded as a Base64 string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the plainText is null.</exception>
        Task<string> EncryptAsync(string aPlainText);

        /// <summary>
        /// Decrypts the specified cipher text using AES decryption.
        /// </summary>
        /// <param name="aCipherText">The encrypted text, encoded as a Base64 string.</param>
        /// <returns>The decrypted plain text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the cipherText is null.</exception>
        /// <exception cref="FormatException">Thrown when the cipherText is not a valid Base64 string.</exception>
        Task<string> DecryptAsync(string aCipherText);
    }
}
