using System;
using System.Net;

namespace IfcToolbox.Core.Bsdd
{
    /// <summary>
    /// Raised when bSDD answers with a non success status code.
    /// Carries the status and the raw body, which is what actually says what went wrong.
    /// </summary>
    public class BsddException : Exception
    {
        public BsddException(HttpStatusCode statusCode, string requestUri, string responseBody)
            : base($"bSDD request failed with {(int)statusCode} {statusCode}: {requestUri}")
        {
            StatusCode = statusCode;
            RequestUri = requestUri;
            ResponseBody = responseBody;
        }

        public HttpStatusCode StatusCode { get; }
        public string RequestUri { get; }
        public string ResponseBody { get; }
    }
}
