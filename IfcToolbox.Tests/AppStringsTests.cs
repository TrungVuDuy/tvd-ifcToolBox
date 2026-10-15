using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace IfcToolbox.Tests
{
    /// <summary>
    /// The desktop app's text lives in IfcToolbox.App\Strings\{language}\Resources.resw. A key the
    /// code asks for but no file has shows the bare key; a translation that drops a {0} throws when
    /// formatted. The app itself is a WinUI project the tests cannot reference, so its sources are
    /// read as text.
    /// </summary>
    public class AppStringsTests
    {
        private static readonly string AppFolder = Path.Combine(RepositoryRoot(), "IfcToolbox.App");

        private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
            Path.GetDirectoryName(Path.GetDirectoryName(thisFile));

        public static IEnumerable<object[]> Languages() =>
            Directory.GetDirectories(Path.Combine(AppFolder, "Strings")).Select(d => new object[] { Path.GetFileName(d) });

        private static Dictionary<string, string> Strings(string language) =>
            XDocument.Load(Path.Combine(AppFolder, "Strings", language, "Resources.resw"))
                .Root.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));

        private static IEnumerable<(string File, string Text)> Sources(string pattern) =>
            Directory.EnumerateFiles(AppFolder, pattern, SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

        private static IEnumerable<string> Placeholders(string text) =>
            Regex.Matches(text, @"\{(\d+)(?:[:,][^}]*)?\}").Select(m => m.Groups[1].Value).Distinct().OrderBy(p => p);

        [Fact]
        public void EnglishAndVietnameseAreShipped()
        {
            var languages = Languages().Select(l => (string)l[0]).ToList();
            Assert.Contains("en-US", languages);
            Assert.Contains("vi-VN", languages);
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void EveryLanguageHasTheEnglishKeys(string language)
        {
            var english = Strings("en-US");
            var translated = Strings(language);
            Assert.Empty(english.Keys.Except(translated.Keys));
            Assert.Empty(translated.Keys.Except(english.Keys));
            Assert.Empty(translated.Where(s => string.IsNullOrWhiteSpace(s.Value)).Select(s => s.Key));
            Assert.Equal(language, translated["LanguageTag"]);
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void TranslationsKeepThePlaceholders(string language)
        {
            var english = Strings("en-US");
            foreach (var (key, text) in Strings(language))
            {
                Assert.True(Placeholders(english[key]).SequenceEqual(Placeholders(text)), $"{language} {key}: '{text}'");
                // Throws on a broken format item such as "{0" or "{x}".
                var arguments = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
                string.Format(CultureInfo.InvariantCulture, text, arguments);
            }
        }

        [Fact]
        public void EveryXUidHasText()
        {
            var keys = Strings("en-US").Keys.ToList();
            var missing = Sources("*.xaml")
                .SelectMany(s => Regex.Matches(s.Text, "x:Uid=\"([^\"]+)\"").Select(m => (s.File, Uid: m.Groups[1].Value)))
                .Where(u => !keys.Any(k => k.StartsWith(u.Uid + ".", StringComparison.Ordinal)))
                .Select(u => $"{u.File}: {u.Uid}")
                .ToList();
            Assert.Empty(missing);
        }

        [Fact]
        public void EveryKeyTheCodeAsksForExists()
        {
            var strings = Strings("en-US");
            var wanted = new List<(string File, string Key)>();
            foreach (var (file, text) in Sources("*.cs"))
            {
                wanted.AddRange(Regex.Matches(text, @"Loc\.(?:Get|Format)\(""([^""]+)""").Select(m => (file, m.Groups[1].Value)));
                foreach (Match m in Regex.Matches(text, @"Loc\.Count\(""([^""]+)"""))
                    wanted.AddRange(new[] { (file, m.Groups[1].Value + "_One"), (file, m.Groups[1].Value + "_Many") });
                // Keys picked with a condition: Loc.Get(a ? "X" : "Y").
                foreach (Match m in Regex.Matches(text, @"Loc\.(?:Get|Format)\([^""()]+\?\s*""([^""]+)""\s*:\s*""([^""]+)"""))
                    wanted.AddRange(new[] { (file, m.Groups[1].Value), (file, m.Groups[2].Value) });
                // Counted keys passed to a helper, e.g. Count(a?.Sites, "Splitter_Sites", "Splitter_BySiteHint").
                foreach (Match m in Regex.Matches(text, @"Count\([^,()]+(?:\([^()]*\))?[^,()]*,\s*""([^""]+)"",\s*""([^""]+)""\)"))
                    wanted.AddRange(new[] { (file, m.Groups[1].Value + "_One"), (file, m.Groups[1].Value + "_Many"), (file, m.Groups[2].Value) });
                foreach (Match m in Regex.Matches(text, @"new ToolInfo\(""(\w+)"""))
                    foreach (var part in new[] { "Title", "Summary", "Action" })
                        wanted.Add((file, $"Tool_{m.Groups[1].Value}_{part}"));
            }
            // Row labels named in XAML: ImportedFile.Label('Row_RemoveName', Name).
            foreach (var (file, text) in Sources("*.xaml"))
                wanted.AddRange(Regex.Matches(text, @"ImportedFile\.Label\('([^']+)'").Select(m => (file, m.Groups[1].Value)));

            Assert.True(wanted.Count > 100, "The source scan found too few keys; has the Loc API changed?");
            Assert.Empty(wanted.Where(w => !strings.ContainsKey(w.Key)).Select(w => $"{w.File}: {w.Key}").Distinct());
        }
    }
}
