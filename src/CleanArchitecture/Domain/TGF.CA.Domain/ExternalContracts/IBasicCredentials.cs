using System.Text.Json.Serialization;

namespace TGF.CA.Domain.ExternalContracts {
    /// <summary>
    /// Interface for classes that represent credentials with a username and password.
    /// </summary>
    public interface IBasicCredentials {
        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        /// <value>
        /// The username.
        /// </value>
        [JsonPropertyName("username")]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        /// <value>
        /// The password.
        /// </value>
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}
