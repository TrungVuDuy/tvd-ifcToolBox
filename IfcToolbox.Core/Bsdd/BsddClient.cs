using IfcToolbox.Core.Bsdd.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IfcToolbox.Core.Bsdd
{
    /// <summary>
    /// Client for the buildingSMART Data Dictionary REST API.
    /// <para>
    /// Replaces the swagger generated client that targeted the retired Domain/Classification
    /// endpoints (/api/Domain/v2, /api/Classification/v3, /api/SearchList/v2 ...). bSDD renamed
    /// Domain to Dictionary and Classification to Class in 2023 and those routes are gone.
    /// </para>
    /// <para>
    /// Only the anonymous read endpoints are covered. Everything bSDD marks as secured needs an
    /// Azure AD B2C token and is out of scope here.
    /// </para>
    /// </summary>
    public class BsddClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly HttpClient _http;
        private readonly bool _ownsHttpClient;
        private readonly BsddOptions _options;

        public BsddClient(BsddOptions options = null)
            : this(new HttpClient(), options, ownsHttpClient: true)
        {
        }

        /// <summary>
        /// Uses a caller supplied <see cref="HttpClient"/>, so the client can take part in
        /// IHttpClientFactory pooling or be replaced by a stub in tests.
        /// </summary>
        public BsddClient(HttpClient httpClient, BsddOptions options = null, bool ownsHttpClient = false)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? new BsddOptions();
            _ownsHttpClient = ownsHttpClient;

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(_options.BaseUrl);
            if (_ownsHttpClient)
                _http.Timeout = _options.Timeout;
            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(_options.UserAgent))
                { /* a malformed user agent is not worth failing a request over */ }
        }

        public BsddOptions Options => _options;

        #region Dictionaries

        /// <summary>Lists dictionaries. Pass no uri to get everything bSDD publishes.</summary>
        public Task<BsddDictionaryResponse> GetDictionariesAsync(string dictionaryUri = null, int offset = 0,
            int? limit = null, bool includeTestDictionaries = false, CancellationToken cancellationToken = default)
        {
            var query = new QueryBuilder()
                .Add("Uri", dictionaryUri)
                .Add("Offset", offset)
                .Add("Limit", limit ?? _options.PageSize)
                .Add("IncludeTestDictionaries", includeTestDictionaries);
            return GetAsync<BsddDictionaryResponse>("/api/Dictionary/v1", query, cancellationToken);
        }

        /// <summary>Walks every page of the dictionary list.</summary>
        public async IAsyncEnumerable<BsddDictionary> EnumerateDictionariesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int offset = 0;
            while (true)
            {
                var page = await GetDictionariesAsync(offset: offset, cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var dictionary in page.Dictionaries)
                    yield return dictionary;
                offset += page.Count;
                if (page.Count == 0 || offset >= page.TotalCount)
                    yield break;
            }
        }

        /// <summary>
        /// Languages a dictionary is translated into. bSDD no longer exposes a standalone
        /// Language endpoint, the information comes with the dictionary.
        /// </summary>
        public async Task<IList<BsddLanguageInfo>> GetDictionaryLanguagesAsync(string dictionaryUri,
            CancellationToken cancellationToken = default)
        {
            var response = await GetDictionariesAsync(dictionaryUri, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.Dictionaries.Count == 0)
                return new List<BsddLanguageInfo>();
            return response.Dictionaries[0].AvailableLanguages;
        }

        #endregion

        #region Classes

        /// <summary>Lists the classes of one dictionary.</summary>
        public Task<BsddDictionaryClassesResponse> GetDictionaryClassesAsync(string dictionaryUri, int offset = 0,
            int? limit = null, string classType = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dictionaryUri))
                throw new ArgumentException("A dictionary uri is required.", nameof(dictionaryUri));

            var query = new QueryBuilder()
                .Add("Uri", dictionaryUri)
                .Add("Offset", offset)
                .Add("Limit", limit ?? _options.PageSize)
                .Add("ClassType", classType)
                .Add("languageCode", _options.LanguageCode);
            return GetAsync<BsddDictionaryClassesResponse>("/api/Dictionary/v1/Classes", query, cancellationToken);
        }

        /// <summary>Walks every page of a dictionary's classes.</summary>
        public async IAsyncEnumerable<BsddClassListItem> EnumerateDictionaryClassesAsync(string dictionaryUri,
            string classType = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int offset = 0;
            while (true)
            {
                var page = await GetDictionaryClassesAsync(dictionaryUri, offset, classType: classType,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var item in page.Classes)
                    yield return item;
                offset += page.Count;
                if (page.Count == 0 || offset >= page.TotalCount)
                    yield break;
            }
        }

        /// <summary>Full detail of a single class.</summary>
        public Task<BsddClass> GetClassAsync(string classUri, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(classUri))
                throw new ArgumentException("A class uri is required.", nameof(classUri));

            var query = new QueryBuilder()
                .Add("Uri", classUri)
                .Add("languageCode", _options.LanguageCode);
            return GetAsync<BsddClass>("/api/Class/v1", query, cancellationToken);
        }

        /// <summary>Free text search across one or more dictionaries.</summary>
        public Task<BsddClassSearchResponse> SearchClassesAsync(string searchText, string dictionaryUri = null,
            int offset = 0, int? limit = null, string relatedIfcEntity = null, CancellationToken cancellationToken = default)
        {
            var query = new QueryBuilder()
                .Add("SearchText", searchText)
                .Add("DictionaryUri", dictionaryUri)
                .Add("RelatedIfcEntity", relatedIfcEntity)
                .Add("Offset", offset)
                .Add("Limit", limit ?? _options.PageSize);
            return GetAsync<BsddClassSearchResponse>("/api/Class/Search/v1", query, cancellationToken);
        }

        #endregion

        #region Class properties

        /// <summary>
        /// Properties that apply to a class, one page at a time.
        /// This dedicated endpoint is the supported way to read them: the IncludeClassProperties
        /// flag on /api/Class/v1 is deprecated and heavily rate limited.
        /// </summary>
        public Task<BsddClassPropertiesResponse> GetClassPropertiesAsync(string classUri, int offset = 0,
            int? limit = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(classUri))
                throw new ArgumentException("A class uri is required.", nameof(classUri));

            var query = new QueryBuilder()
                .Add("ClassUri", classUri)
                .Add("Offset", offset)
                .Add("Limit", limit ?? _options.PageSize)
                .Add("languageCode", _options.LanguageCode);
            return GetAsync<BsddClassPropertiesResponse>("/api/Class/Properties/v1", query, cancellationToken);
        }

        /// <summary>Every property of a class, following pagination to the end.</summary>
        public async Task<IList<BsddClassProperty>> GetAllClassPropertiesAsync(string classUri,
            CancellationToken cancellationToken = default)
        {
            var all = new List<BsddClassProperty>();
            int offset = 0;
            while (true)
            {
                var page = await GetClassPropertiesAsync(classUri, offset, cancellationToken: cancellationToken).ConfigureAwait(false);
                all.AddRange(page.ClassProperties);
                offset += page.Count;
                if (page.Count == 0 || offset >= page.TotalCount)
                    return all;
            }
        }

        #endregion

        #region Plumbing

        private async Task<T> GetAsync<T>(string path, QueryBuilder query, CancellationToken cancellationToken)
        {
            var requestUri = path + query.Build();
            using var response = await _http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadAsStringAsync(response, cancellationToken).ConfigureAwait(false);
                throw new BsddException(response.StatusCode, requestUri, body);
            }

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        private static Task<string> ReadAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response.Content.ReadAsStringAsync();
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
                _http.Dispose();
        }

        /// <summary>Builds a query string, skipping parameters that were not supplied.</summary>
        private sealed class QueryBuilder
        {
            private readonly StringBuilder _sb = new StringBuilder();

            public QueryBuilder Add(string name, string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return this;
                _sb.Append(_sb.Length == 0 ? '?' : '&')
                   .Append(Uri.EscapeDataString(name))
                   .Append('=')
                   .Append(Uri.EscapeDataString(value));
                return this;
            }

            public QueryBuilder Add(string name, int value) => Add(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

            public QueryBuilder Add(string name, bool value) => Add(name, value ? "true" : "false");

            public string Build() => _sb.ToString();
        }

        #endregion
    }
}
