using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TGF.CA.Infrastructure.Discovery;
using TGF.Common.ROP.Errors;
using TGF.Common.ROP.HttpResult;
using TGF.Common.ROP.Result;

namespace TGF.CA.Infrastructure.Communication.Http
{
    /// <summary>
    /// Abstract class with logic for communicating with other cloud services via HTTP. Requires <see cref="IHttpClientFactory"/> and <see cref="IServiceDiscovery"/> from DI.
    /// </summary>
    public abstract class HttpCommunicationService(IServiceDiscovery aServiceDiscovery, IHttpClientFactory aHttpClientFactory, string aJsonMediaType = "application/json")
    {
        protected readonly IServiceDiscovery _serviceDiscovery = aServiceDiscovery;
        protected readonly IHttpClientFactory _httpClientFactory = aHttpClientFactory;
        protected readonly string _jsonMediaType = aJsonMediaType;

        #region Protected

        protected async Task<IHttpResult<TResponse>> GetAsync<TResponse>(string aServiceName, string aRequestUri, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lResponse = await SendRequestAsync(HttpMethod.Get, aServiceName, aRequestUri, null, aAccessToken, aCancellationToken);
            return await GetResultAsync<TResponse>(lResponse, aCancellationToken);
        }

        protected async Task PostAsync<TBody>(string aServiceName, string aRequestUri, TBody aRequestBody, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lHttpContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(aRequestBody), Encoding.UTF8, _jsonMediaType);
            await SendRequestAsync(HttpMethod.Post, aServiceName, aRequestUri, lHttpContent, aAccessToken, aCancellationToken);
        }
        protected async Task<IHttpResult<TResponse>> PostAsync<TBody, TResponse>(string aServiceName, string aRequestUri, TBody aRequestBody, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lHttpContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(aRequestBody), Encoding.UTF8, _jsonMediaType);
            var lResponse = await SendRequestAsync(HttpMethod.Post, aServiceName, aRequestUri, lHttpContent, aAccessToken, aCancellationToken);
            return await GetResultAsync<TResponse>(lResponse, aCancellationToken);
        }

        protected async Task PutAsync<TBody>(string aServiceName, string aRequestUri, TBody aRequestBody, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lHttpContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(aRequestBody), Encoding.UTF8, _jsonMediaType);
            await SendRequestAsync(HttpMethod.Put, aServiceName, aRequestUri, lHttpContent, aAccessToken, aCancellationToken);
        }
        protected async Task<IHttpResult<TResponse>> PutAsync<TBody, TResponse>(string aServiceName, string aRequestUri, TBody aRequestBody, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lHttpContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(aRequestBody), Encoding.UTF8, _jsonMediaType);
            var lResponse = await SendRequestAsync(HttpMethod.Put, aServiceName, aRequestUri, lHttpContent, aAccessToken, aCancellationToken);
            return await GetResultAsync<TResponse>(lResponse, aCancellationToken);
        }

        protected async Task DeleteAsync(string aServiceName, string aRequestUri, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            await SendRequestAsync(HttpMethod.Delete, aServiceName, aRequestUri, null, aAccessToken, aCancellationToken);
        }
        protected async Task<IHttpResult<TResponse>> DeleteAsync<TResponse>(string aServiceName, string aRequestUri, string? aAccessToken = default, CancellationToken aCancellationToken = default)
        {
            var lResponse = await SendRequestAsync(HttpMethod.Delete, aServiceName, aRequestUri, null, aAccessToken, aCancellationToken);
            return await GetResultAsync<TResponse>(lResponse, aCancellationToken);
        }

        #endregion

        #region Private

        private async Task<HttpResponseMessage> SendRequestAsync(
            HttpMethod method,
            string aServiceName,
            string aRequestUri,
            HttpContent? content = null,
            string? aAccessToken = default,
            CancellationToken aCancellationToken = default)
        {
            var lHttpClient = await GetHttpClientAsync(aServiceName);
            var lRequest = new HttpRequestMessage(method, aRequestUri) { Content = content };

            if (!string.IsNullOrEmpty(aAccessToken))
                lRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aAccessToken);

            return await lHttpClient.SendAsync(lRequest, aCancellationToken);
        }


        private static async Task<IHttpResult<TResponse>> GetResultAsync<TResponse>(HttpResponseMessage aResponse, CancellationToken aCancellationToken = default)
        {
            var lJsonString = await aResponse.Content.ReadAsStringAsync(aCancellationToken);
            if (!aResponse.IsSuccessStatusCode)
            {
                var lProblemDetails = JsonConvert.DeserializeObject<ProblemDetails>(lJsonString)!;
                return Result.Failure<TResponse>(
                    new HttpError(new Error(lProblemDetails.Title!, lProblemDetails.Detail!),
                    (HttpStatusCode)lProblemDetails.Status!));
            }
            var lNewRegisteredMemberDto = JsonConvert.DeserializeObject<TResponse>(lJsonString)!;
            return Result.SuccessHttp(lNewRegisteredMemberDto);
        }

        private async Task<HttpClient> GetHttpClientAsync(string aServiceName)
        {
            var lDataAccessServiceAddress = await _serviceDiscovery.GetFullAddress(aServiceName);

            if (!lDataAccessServiceAddress.StartsWith("http://") && !lDataAccessServiceAddress.StartsWith("https://"))
                lDataAccessServiceAddress = "http://" + lDataAccessServiceAddress;
            if (!lDataAccessServiceAddress.EndsWith("/"))
                lDataAccessServiceAddress += "/";

            var lHttpClient = _httpClientFactory.CreateClient();
            lHttpClient.BaseAddress = new Uri(lDataAccessServiceAddress);

            return lHttpClient;
        }

        #endregion

    }
}
