using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TGF.CA.Infrastructure.Discovery
{
    //CODE FROM https://github.com/ElectNewt/Distribt
    public interface IServiceDiscovery
    {
        Task<string> GetFullAddress(string aServiceKey, CancellationToken aCancellationToken = default);
        Task<DiscoveryData> GetDiscoveryData(string aServiceKey, CancellationToken aCancellationToken = default);
    }
    public static class DiscoveryDependencyInjection
    {
        public static IServiceCollection AddDiscoveryService(this IServiceCollection aServiceList, IConfiguration aConfiguration)
        {
            return aServiceList.AddSingleton<IConsulClient, ConsulClient>(provider => new ConsulClient(consulConfig =>
            {
                var lAddress = aConfiguration["Discovery:Address"];
                if (lAddress == null)
                    throw new Exception("Error: while adding DiscoveryService, could not get the address from appsettings!!");
                consulConfig.Address = new Uri(lAddress);
            }))
            .AddSingleton<IServiceDiscovery, ConsulServiceDiscovery>();
        }
    }
}