using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LocalCloudStorage
{
    public class HttpRequestBuilder
    {
        private HttpClient _httpClient;
        
        private IEnumerable<KeyValuePair<string, string>> _contentHeaders;
        private HttpRequestMessage _request;
        private HttpCompletionOption _completionOption = HttpCompletionOption.ResponseHeadersRead;

        internal HttpRequestBuilder(HttpClient httpClient, string url, HttpMethod verb)
        {
            _httpClient = httpClient;
            _request = new HttpRequestMessage(verb, url);
        }

        #region Content
        public HttpRequestBuilder SetContent(Stream content)
        {
            _request.Content = new StreamContent(content);
            return this;
        }
        public HttpRequestBuilder SetContent(string content)
        {
            _request.Content = new StringContent(content);
            return this;
        }
        public HttpRequestBuilder SetContent(byte[] content)
        {
            return SetContent(new MemoryStream(content));
        }
        #endregion


        public HttpRequestBuilder SetAuthentication(AuthenticationHeaderValue authenticationHeader)
        {
            _request.Headers.Authorization = authenticationHeader;
            return this;
        }

        public HttpRequestBuilder SetRequestHeaders(IEnumerable<KeyValuePair<string, string>> requestHeaders)
        {
            foreach (var requestHeader in requestHeaders)
            {
                _request.Headers.Add(requestHeader.Key, requestHeader.Value);
            }
            return this;
        }

        public HttpRequestBuilder SetContentHeaders(IEnumerable<KeyValuePair<string, string>> contentHeaders)
        {
            _contentHeaders = contentHeaders;
            return this;
        }

        public HttpRequestBuilder SetCompletionOption(HttpCompletionOption completionOption)
        {
            _completionOption = completionOption;
            return this;
        }

        public async Task<HttpResponseMessage> SendAsync(bool retryOnTimeout, CancellationToken ct)
        {
            //add the headers now
            if (_contentHeaders != null)
            {
                if (_request.Content == null)
                {
                    _request.Content = new StringContent("");
                }
                foreach (var header in _contentHeaders)
                {
                    _request.Content.Headers.Add(header.Key, header.Value);
                }
            }

            Debug.WriteLine(_request.ToString());
            HttpResponseMessage ret = null;
            while (ret == null)
            {
                try
                {
                    ret = await _httpClient.SendAsync(_request, _completionOption, ct);
                    Debug.WriteLine(ret.ToString());
                }
                catch (Exception e)
                {
                    await Utils.DelayNoThrow(TimeSpan.FromSeconds(10), ct);
                }
            }
            return ret;
        }

        public async Task<HttpResponseMessage> SendAsync(CancellationToken ct)
        {
            //add the headers now
            if (_contentHeaders != null)
            {
                if (_request.Content == null)
                {
                    _request.Content = new StringContent("");
                }
                foreach (var header in _contentHeaders)
                {
                    _request.Content.Headers.Add(header.Key, header.Value);
                }
            }

            Debug.WriteLine(_request.ToString());
            var ret = await _httpClient.SendAsync(_request, _completionOption, ct);
            Debug.WriteLine(ret.ToString());
            return ret;
        }
    }

    public class HttpClientHelper : IDisposable
    {
        #region Private Fields
        private static HttpClient _httpClient = new HttpClient();
        private static int _refCounter;
        private readonly Atomic<AuthenticationHeaderValue> _authenticationHeader = new Atomic<AuthenticationHeaderValue>(null);
        #endregion

        public HttpClientHelper()
        {
            Interlocked.Increment(ref _refCounter);
        }

        #region Public Properties
        public static TimeSpan Timeout
        {
            get => _httpClient.Timeout;
            set => _httpClient.Timeout = value;
        }
        public AuthenticationHeaderValue AuthenticationHeader
        {
            get => _authenticationHeader.Value;
            set => _authenticationHeader.Value = value;
        }
        #endregion

        public HttpRequestBuilder StartRequest(string url, HttpMethod verb)
        {
            return new HttpRequestBuilder(_httpClient, url, verb);
        }
        public HttpRequestBuilder StartAuthenticatedRequest(string url, HttpMethod verb)
        {
            return new HttpRequestBuilder(_httpClient, url, verb).SetAuthentication(AuthenticationHeader);
        }

        #region Helper Methods
        public static async Task<string> ReadResponseAsStringAsync(HttpResponseMessage message)
        {
            var stream = new StreamReader(await message.Content.ReadAsStreamAsync());
            return await stream.ReadToEndAsync();
        }
        public static async Task<JObject> ReadResponseAsJObjectAsync(HttpResponseMessage message)
        {
            var text = await ReadResponseAsStringAsync(message);
            if (string.IsNullOrEmpty(text))
                return null;
            return (JObject)JsonConvert.DeserializeObject(text);
        }
        public async Task<bool> HasNetworkConnection()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://clients3.google.com/generate_204");
            var response = await _httpClient.SendAsync(request);
            return response.StatusCode == HttpStatusCode.NoContent;
        }
        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Decrement(ref _refCounter) == 0)
            {
                _httpClient?.Dispose();
            }
        }
    }
}
