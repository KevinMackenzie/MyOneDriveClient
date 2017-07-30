using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    public class HttpResult : IDisposable
    {
        private HttpResponseMessage _responseMessage;
        public HttpResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage;
        }

        /// <summary>
        /// The response to the http request, null if timed out
        /// </summary>
        public HttpResponseMessage HttpMessage => _responseMessage;

        /// <summary>
        /// Whether the response was successful
        /// </summary>
        public bool Success => HttpMessage != null && HttpMessage.IsSuccessStatusCode;

        /// <inheritdoc />
        public virtual void Dispose()
        {
            _responseMessage?.Dispose();
        }
    }
    public class HttpResult<ReturnType> : HttpResult
    {
        public HttpResult(HttpResponseMessage responseMessage, ReturnType returnValue) : base(responseMessage)
        {
            Value = returnValue;
        }

        /// <summary>
        /// The value of the completed request, null if not <see cref="HttpResponseMessage.IsSuccessStatusCode"/>
        /// </summary>
        public ReturnType Value { get; }

        /// <inheritdoc />
        public override void Dispose()
        {
            var val = Value as IDisposable;
            val?.Dispose();

            base.Dispose();
        }
    }
}
