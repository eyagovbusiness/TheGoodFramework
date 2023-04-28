using Consul;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace TGF.CA.Infrastructure.Discovery
{
    //CODE FROM https://github.com/ElectNewt/Distribt
    public record DiscoveryData(string Server, int Port);
    public class ConsulServiceDiscovery : IServiceDiscovery
    {
        private readonly IConsulClient _client;
        private readonly MemoryCache _cache;

        public ConsulServiceDiscovery(IConsulClient aClient)
        {
            _client = aClient;
            _cache = new MemoryCache(new MemoryCacheOptions());
        }


        public async Task<string> GetFullAddress(string aServiceKey, CancellationToken aCancellationToken = default)
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (_cache.TryGetValue(aServiceKey, out DiscoveryData lCachedData))
            {
#pragma warning disable CS8604 // Possible null reference argument.
                return GetAddressFromData(lCachedData);
#pragma warning restore CS8604 // Possible null reference argument.
            }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            DiscoveryData lData = await GetDiscoveryData(aServiceKey, aCancellationToken);
            return GetAddressFromData(lData);
        }

        public async Task<DiscoveryData> GetDiscoveryData(string aServiceKey, CancellationToken aCancellationToken = default)
        {
            var lServices = await _client.Catalog.Service(aServiceKey, aCancellationToken);
            if (lServices.Response != null && lServices.Response.Any())
            {
                var lService = lServices.Response.First();
                DiscoveryData lData = new(lService.ServiceAddress, lService.ServicePort);
                AddToCache(aServiceKey, lData);

                return lData;
            }
            throw new ArgumentException($"seems like the service your are trying to access ({aServiceKey}) does not exist ");
        }


        private static string GetAddressFromData(DiscoveryData aData)
        {
            StringBuilder lServiceAddress = new();
            lServiceAddress.Append(aData.Server);
            if (aData.Port != 0)
            {
                lServiceAddress.Append($":{aData.Port}");
            }

            string lServiceAddressString = lServiceAddress.ToString();

            return lServiceAddressString;
        }


        private void AddToCache(string aServiceKey, DiscoveryData aServiceAddress)
        {
            _cache.Set(aServiceKey, aServiceAddress);
        }
    }
}