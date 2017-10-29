using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <inheritdoc />
    public class HttpResult : IHttpResult
    {
        public HttpResult(HttpResponseMessage responseMessage)
        {
            HttpMessage = responseMessage;
        }

        public HttpResponseMessage HttpMessage { get; }

        public bool Success => HttpMessage != null && HttpMessage.IsSuccessStatusCode;

        /// <inheritdoc />
        public virtual void Dispose()
        {
            HttpMessage?.Dispose();
        }
    }

    /// <inheritdoc cref="IHttpResult{ReturnType}" />
    public class HttpResult<ReturnType> : HttpResult, IHttpResult<ReturnType>
    {
        public HttpResult(HttpResponseMessage responseMessage, ReturnType returnValue) : base(responseMessage)
        {
            Value = returnValue;
        }

        public ReturnType Value { get; }

        /// <inheritdoc cref="HttpResult" />
        public override void Dispose()
        {
            var val = Value as IDisposable;
            val?.Dispose();

            base.Dispose();
        }
    }
}
