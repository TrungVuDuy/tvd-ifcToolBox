using System;
using System.Text.RegularExpressions;

namespace IfcToolbox.Core.Convert
{
    /// <summary>
    /// Major/minor version of an IfcConvert executable, read from its --version output.
    /// <para>
    /// This matters because IfcOpenShell renamed several command line options between 0.6 and
    /// 0.8. Without knowing which build is present, those options would be passed with the
    /// wrong spelling and IfcConvert would reject the whole command line.
    /// </para>
    /// </summary>
    public sealed class IfcConvertVersion : IComparable<IfcConvertVersion>, IEquatable<IfcConvertVersion>
    {
        /// <summary>Used when the executable could not be asked, e.g. it is not on disk yet.</summary>
        public static readonly IfcConvertVersion Unknown = new IfcConvertVersion();

        private IfcConvertVersion()
        {
            IsUnknown = true;
        }

        public IfcConvertVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public int Major { get; }
        public int Minor { get; }
        public bool IsUnknown { get; }

        /// <summary>Raw --version line, kept for logging.</summary>
        public string RawText { get; private set; }

        /// <summary>
        /// Parses output such as "IfcOpenShell IfcConvert 0.6.0b0 (OCC 7.3.0)" or
        /// "IfcOpenShell IfcConvert 0.8.5 (OCC 7.8.1)". Returns <see cref="Unknown"/> when
        /// no version number can be found.
        /// </summary>
        public static IfcConvertVersion Parse(string versionOutput)
        {
            if (string.IsNullOrWhiteSpace(versionOutput))
                return Unknown;

            // The output can come back UTF-16 encoded, which leaves NUL bytes between
            // characters when read as single byte text.
            var cleaned = versionOutput.Replace("\0", string.Empty);

            var match = Regex.Match(cleaned, @"IfcConvert\s+v?(\d+)\.(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                match = Regex.Match(cleaned, @"(\d+)\.(\d+)\.\d+");
            if (!match.Success)
                return Unknown;

            return new IfcConvertVersion(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value))
            {
                RawText = cleaned.Trim(),
            };
        }

        /// <summary>True for IfcConvert 0.8 and newer, which is where the option renames landed.</summary>
        public bool IsAtLeast08 => !IsUnknown && this >= new IfcConvertVersion(0, 8);

        public int CompareTo(IfcConvertVersion other)
        {
            if (other == null) return 1;
            if (IsUnknown && other.IsUnknown) return 0;
            if (IsUnknown) return -1;
            if (other.IsUnknown) return 1;
            int byMajor = Major.CompareTo(other.Major);
            return byMajor != 0 ? byMajor : Minor.CompareTo(other.Minor);
        }

        public bool Equals(IfcConvertVersion other) => CompareTo(other) == 0 && IsUnknown == (other?.IsUnknown ?? false);

        public override bool Equals(object obj) => Equals(obj as IfcConvertVersion);

        public override int GetHashCode() => IsUnknown ? -1 : (Major * 397) ^ Minor;

        public override string ToString() => IsUnknown ? "unknown" : $"{Major}.{Minor}";

        public static bool operator >=(IfcConvertVersion left, IfcConvertVersion right) =>
            Comparer(left, right) >= 0;

        public static bool operator <=(IfcConvertVersion left, IfcConvertVersion right) =>
            Comparer(left, right) <= 0;

        public static bool operator >(IfcConvertVersion left, IfcConvertVersion right) =>
            Comparer(left, right) > 0;

        public static bool operator <(IfcConvertVersion left, IfcConvertVersion right) =>
            Comparer(left, right) < 0;

        public static bool operator ==(IfcConvertVersion left, IfcConvertVersion right) =>
            ReferenceEquals(left, right) || (left is object && left.Equals(right));

        public static bool operator !=(IfcConvertVersion left, IfcConvertVersion right) => !(left == right);

        private static int Comparer(IfcConvertVersion left, IfcConvertVersion right)
        {
            if (left is null && right is null) return 0;
            if (left is null) return -1;
            return left.CompareTo(right);
        }
    }
}
