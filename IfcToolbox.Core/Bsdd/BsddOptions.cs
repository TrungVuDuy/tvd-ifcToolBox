using System;

namespace IfcToolbox.Core.Bsdd
{
    /// <summary>
    /// Connection settings for <see cref="BsddClient"/>.
    /// </summary>
    public class BsddOptions
    {
        /// <summary>Production bSDD service.</summary>
        public const string ProductionBaseUrl = "https://api.bsdd.buildingsmart.org";

        /// <summary>Test service, useful when trying out data that is not published yet.</summary>
        public const string TestBaseUrl = "https://test.bsdd.buildingsmart.org";

        public string BaseUrl { get; set; } = ProductionBaseUrl;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Two letter or culture code (EN, nl-NL, ...). When set, bSDD returns names and
        /// descriptions in that language where a translation exists.
        /// </summary>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Page size used when walking a paged endpoint. bSDD caps this server side.
        /// </summary>
        public int PageSize { get; set; } = 250;

        /// <summary>
        /// Sent as User-Agent so bSDD can see which tool the traffic comes from.
        /// </summary>
        public string UserAgent { get; set; } = "IfcToolbox";
    }
}
