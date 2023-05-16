using System;
using System.Net.Http;

namespace TGF.Common.Net.Http
{
    /// <summary>
    /// Composition class of <see cref="IHttpClientFactory"/> that wraps the factory 
    /// providing a single method(<see cref="GetHttpClient"/>) to get a new single instance of <see cref="HttpClient"/> 
    /// that will be disposed and replaced by a new one every given interval from the constructor.
    /// </summary>
    public class TimedHttpClientProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _replacementInterval;
        private readonly string _baseAddress;
        private DateTime mLastReplacementTime;
        private HttpClient mHttpClient;

        /// <summary>
        /// Composition class of <see cref="IHttpClientFactory"/> that wraps the factory 
        /// providing a single method(<see cref="GetHttpClient"/>) to get a new single instance of <see cref="HttpClient"/> 
        /// that will be disposed and replaced by a new one every given interval from the constructor.
        /// </summary>
        /// <param name="aHttpClientFactory"></param>
        /// <param name="aReplacementInterval"><see cref="TimeSpan"/> representing the interval of time this class will use to replace the HttpClient returned from <see cref="GetHttpClient"/></param>
        /// <param name="aBaseAddress">If this parameter is provided, it will be used to define <see cref="HttpClient.BaseAddress"/> <see cref="Uri"/> address used by the returned <see cref="HttpClient"/> instance.</param>
        public TimedHttpClientProvider(IHttpClientFactory aHttpClientFactory, TimeSpan aReplacementInterval, string aBaseAddress = default)
        {
            _httpClientFactory = aHttpClientFactory;
            _replacementInterval = aReplacementInterval;
            mLastReplacementTime = DateTime.Now;
            _baseAddress = aBaseAddress;

        }

        /// <summary>
        /// Gets a single instance of <see cref="HttpClient"/> that is replaced preiodically by a new one.
        /// </summary>
        /// <returns>An instance of <see cref="HttpClient"/> that is replaced by a new one every specified replacement interval.</returns>
        /// <exception cref="NullReferenceException">Throws null reference exception if the method was not able to get a valid HttpClient to return.</exception>
        public HttpClient GetHttpClient()
        {
            if (DateTime.Now - mLastReplacementTime >= _replacementInterval || mHttpClient == null)
            {
                ReplaceHttpClient();
                mLastReplacementTime = DateTime.Now;
            }
            if (mHttpClient == null)
                throw new NullReferenceException("Error: mHttpClient was null!!");
            return mHttpClient;
        }

        private void ReplaceHttpClient()
        {
            // Dispose the current HttpClient if exist
            mHttpClient?.Dispose();

            // Create a new HttpClient from the factory with the specified base address if any
            mHttpClient = _httpClientFactory.CreateClient();
            if(!string.IsNullOrEmpty(_baseAddress))
                mHttpClient.BaseAddress = new Uri(_baseAddress);
        }
    }

}