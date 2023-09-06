namespace TGF.CA.Application
{
    public interface ISecretsManager
    {
        public Task<T> Get<T>(string aPath) where T : new();
        public Task<object> GetValueObject(string aPath, string aKey);
        public Task<IBasicCredentials> GetRabbitMQCredentials(string aRoleName);
        public Task<string> GetTokenSecret(string aTokenName);
        public Task<string> GetServiceKey(string aServiceName);
        public Task<bool> GetIsHealthy();
    }
}