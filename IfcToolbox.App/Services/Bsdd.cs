using IfcToolbox.Core.Bsdd;
using IfcToolbox.Core.Bsdd.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IfcToolbox.App.Services
{
    /// <summary>One bSDD client for the app, and the dictionary list fetched once per run.</summary>
    public static class Bsdd
    {
        public static BsddClient Client { get; } = new(new BsddOptions { UserAgent = "IfcToolbox-App" });

        private static IList<BsddDictionary> _dictionaries;

        /// <summary>
        /// Published dictionaries, the latest version of each, sorted by name. A dictionary that
        /// marks no version as latest is listed with all of its versions.
        /// </summary>
        public static async Task<IList<BsddDictionary>> GetDictionariesAsync(CancellationToken cancellationToken = default)
        {
            if (_dictionaries != null)
                return _dictionaries;

            var all = new List<BsddDictionary>();
            await foreach (var dictionary in Client.EnumerateDictionariesAsync(cancellationToken))
                all.Add(dictionary);

            _dictionaries = all
                .GroupBy(d => (d.OrganizationCodeOwner, d.Code))
                .SelectMany(g => g.Any(d => d.IsLatestVersion) ? g.Where(d => d.IsLatestVersion) : g)
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(d => d.Version)
                .ToList();
            return _dictionaries;
        }
    }
}
