using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IfcToolbox.App.Services
{
    /// <summary>A language the interface is translated into.</summary>
    public sealed record UiLanguage(string Tag, string NativeName);

    /// <summary>
    /// Interface text in the chosen language, from Strings\{language}\Resources.resw. XAML takes its
    /// text by x:Uid from the same files; code asks here.
    /// </summary>
    public static class Loc
    {
        /// <summary>The translations shipped, English first as the fallback for anything missing.</summary>
        public static IReadOnlyList<UiLanguage> Languages { get; } = new[]
        {
            new UiLanguage("en-US", "English"),
            new UiLanguage("vi-VN", "Tiếng Việt"),
        };

        private static ResourceLoader _loader;

        /// <summary>
        /// Picks the interface language for this run: <paramref name="commandLine"/> if given, then
        /// the one chosen in Settings, otherwise Windows' display language. Must run before the
        /// first XAML is loaded, in the App constructor; an unpackaged app cannot switch later.
        /// </summary>
        public static void Apply(string commandLine)
        {
            var settings = AppSettings.Current;
            string tag = commandLine ?? (settings.UseCustomLanguage ? settings.Language : null);
            if (string.IsNullOrEmpty(tag))
                return;
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = tag;
            }
            catch (Exception e) when (e is ArgumentException or System.Runtime.InteropServices.COMException)
            {
                // Not a language tag Windows knows: keep the display language.
            }
        }

        /// <summary>The text for <paramref name="key"/>, or the key itself when it is missing, so a gap shows rather than breaks.</summary>
        public static string Get(string key)
        {
            try
            {
                _loader ??= new ResourceLoader();
                var text = _loader.GetString(key);
                return string.IsNullOrEmpty(text) ? key : text;
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or ArgumentException)
            {
                return key;
            }
        }

        /// <summary><see cref="Get"/> with {0}, {1}... filled in, numbers in the user's format.</summary>
        public static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), args);

        /// <summary>"1 file" or "3 files": <paramref name="key"/>_One for one, <paramref name="key"/>_Many otherwise.</summary>
        public static string Count(string key, int count) =>
            Format(count == 1 ? key + "_One" : key + "_Many", count);

        /// <summary>The language of the text this run shows, from <see cref="Languages"/>.</summary>
        public static UiLanguage Current
        {
            get
            {
                var tag = Get("LanguageTag");
                return Languages.FirstOrDefault(l => l.Tag == tag) ?? Languages[0];
            }
        }
    }
}
