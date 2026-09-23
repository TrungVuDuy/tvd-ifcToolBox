namespace IfcToolbox.Cli
{
    /// <summary>
    /// Process exit codes, stable so that scripts and CI pipelines can rely on them.
    /// </summary>
    public static class ExitCodes
    {
        public const int Success = 0;

        /// <summary>The command ran, and the model did not satisfy what was checked (e.g. an IDS).</summary>
        public const int ValidationFailed = 1;

        /// <summary>The command line itself was wrong.</summary>
        public const int UsageError = 2;

        /// <summary>The command could not complete: unreadable file, unsupported schema, converter failure.</summary>
        public const int Error = 3;
    }
}
