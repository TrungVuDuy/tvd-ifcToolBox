using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IfcToolbox.App.Services
{
    public enum AppTheme
    {
        System,
        Light,
        Dark,
    }

    /// <summary>
    /// User settings, kept in %LOCALAPPDATA%\IfcToolbox\settings.json. The app runs unpackaged,
    /// so the ApplicationData store the original UWP app used is not available.
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool UseCustomLanguage { get; set; }
        /// <summary>BCP 47 tag, e.g. "en-US". Only used when <see cref="UseCustomLanguage"/> is on.</summary>
        public string Language { get; set; } = "en-US";

        public static string Folder { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IfcToolbox");

        /// <summary>Scratch space for tool runs, emptied by "Clear cache files".</summary>
        public static string CacheFolder { get; } = Path.Combine(Folder, "Cache");

        private static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppSettings Current { get; private set; } = Load();

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                // A damaged or unreadable file falls back to the defaults rather than stopping the app.
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Settings are a convenience; failing to keep them must not break the app.
            }
        }

        /// <summary>Deletes the cache folder's contents and returns how many bytes were freed.</summary>
        public static long ClearCache()
        {
            long freed = 0;
            if (!Directory.Exists(CacheFolder))
                return 0;
            foreach (var file in Directory.EnumerateFiles(CacheFolder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    long length = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += length;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // In use by a running tool: leave it for next time.
                }
            }
            foreach (var directory in Directory.GetDirectories(CacheFolder))
            {
                try { Directory.Delete(directory, true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            return freed;
        }

        public static long CacheSize()
        {
            if (!Directory.Exists(CacheFolder))
                return 0;
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(CacheFolder, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch (IOException) { }
            }
            return total;
        }
    }
}
