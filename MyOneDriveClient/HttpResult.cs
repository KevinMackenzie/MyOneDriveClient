using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class HttpResult<ReturnType> : IDisposable
    {
        private HttpResponseMessage _responseMessage;
        public HttpResult(HttpResponseMessage responseMessage, ReturnType returnValue)
        {
            Value = returnValue;
            _responseMessage = responseMessage;
        }

        /// <summary>
        /// The response to the http request, null if timed out
        /// </summary>
        public HttpResponseMessage HttpMessage => _responseMessage;
        /// <summary>
        /// The value of the completed request, null if not <see cref="HttpResponseMessage.IsSuccessStatusCode"/>
        /// </summary>
        public ReturnType Value { get; }


        /// <inheritdoc />
        public void Dispose()
        {
            _responseMessage?.Dispose();
        }
    }
}
