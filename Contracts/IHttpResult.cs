using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage
{
    /// <inheritdoc />
    /// <summary>
    /// An HttpResponse with a simple "success" property
    /// </summary>
    public interface IHttpResult : IDisposable
    {
        /// <summary>
        /// The response to the http request, null if timed out
        /// </summary>
        HttpResponseMessage HttpMessage { get; }
        /// <summary>
        /// Whether the response was successful
        /// </summary>
        bool Success { get; }
    }
    /// <inheritdoc />
    /// <summary>
    /// An HttpResult with an associated return data
    /// </summary>
    /// <typeparam name="ReturnType"></typeparam>
    public interface IHttpResult<ReturnType> : IHttpResult
    {
        /// <summary>
        /// The value of the completed request, null if not <see cref="HttpResponseMessage.IsSuccessStatusCode"/>
        /// </summary>
        ReturnType Value { get; }
    }

}
