using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xbim.Common;
using Xbim.Common.Delta;

namespace IfcToolbox.Core.Utilities
{
    public class Marslogger
    {
        /// <summary>
        /// Where progress messages go. Console by default; a command line tool sets it to
        /// <see cref="TextWriter.Null"/> or to stderr so that its standard output stays
        /// machine readable (every processor reports its run time through here).
        /// </summary>
        public static TextWriter Output { get; set; } = Console.Out;

        private static bool IsConsole => ReferenceEquals(Output, Console.Out);

        public static void ConfigureConsoleLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .CreateLogger();
        }

        public static void Mark(object info)
        {
            SetColors(ConsoleColor.White, ConsoleColor.DarkBlue);
            Output.Write($"[{DateTime.Now.ToString("HH:mm:ss")} MAK] {info} ");
            ResetColors();
            Output.WriteLine();
        }

        public static void Action(string action, string className = null)
        {
            SetColors(ConsoleColor.Cyan);
            if (className == null)
                Output.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")} ACT] {action}");
            else
                Output.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")} ACT] {action} >>> {className}");
            ResetColors();
        }

        public static void Step(string action, bool finalStep = false)
        {
            SetColors(ConsoleColor.DarkCyan);
            Output.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")} STP] {action}");
            ResetColors();
            if (finalStep && IsConsole)
            {
                while (Console.ReadKey().Key != ConsoleKey.Enter) { }
            }
        }

        public static void PrintChanges(TransactionLog log, bool logDetail = false)
        {
            Action("Modification", "Serilog");
            if (logDetail)
            {
                foreach (var change in log.Changes)
                {
                    switch (change.ChangeType)
                    {
                        case ChangeType.New:
                            SetColors(ConsoleColor.White);
                            Output.WriteLine(@"New entity: {0}", change.CurrentEntity);
                            ResetColors();
                            break;
                        case ChangeType.Deleted:
                            SetColors(ConsoleColor.DarkGray);
                            Output.WriteLine(@"Deleted entity: {0}", change.OriginalEntity);
                            ResetColors();
                            break;
                        case ChangeType.Modified:
                            SetColors(ConsoleColor.DarkYellow);
                            Output.WriteLine(@"Changed Entity: #{0}={1}", change.Entity.EntityLabel, change.Entity.ExpressType.ExpressNameUpper);
                            foreach (var prop in change.ChangedProperties)
                                Output.WriteLine(@"        Property '{0}' changed from {1} to {2}", prop.Name, prop.OriginalValue, prop.CurrentValue);
                            ResetColors();
                            break;
                        default:
                            break;
                    }
                }
            }
            Log.Information("[New entity total] {0}", log.Changes.Where(o => o.ChangeType == ChangeType.New).Count().ToString());
            Log.Information("[Changed entity total] {0}", log.Changes.Where(o => o.ChangeType == ChangeType.Modified).Count().ToString());
            Log.Information("[Deleted entity total] {0}", log.Changes.Where(o => o.ChangeType == ChangeType.Deleted).Count().ToString());
        }

        public static StringBuilder CreateLog(TransactionLog log)
        {
            StringBuilder logContent = new StringBuilder();
            logContent.AppendLine($"[SUMMARY]");
            logContent.AppendLine($"New entity >> {log.Changes.Where(o => o.ChangeType == ChangeType.New).Count()}");
            logContent.AppendLine($"Changed entity >> {log.Changes.Where(o => o.ChangeType == ChangeType.Modified).Count()}");
            logContent.AppendLine($"Deleted entity >> {log.Changes.Where(o => o.ChangeType == ChangeType.Deleted).Count()}");
            logContent.AppendLine();

            foreach (var change in log.Changes)
            {
                switch (change.ChangeType)
                {
                    case ChangeType.New:
                        logContent.AppendLine($"New entity: {change.CurrentEntity}");
                        break;
                    case ChangeType.Deleted:
                        logContent.AppendLine($"Deleted entity: {change.OriginalEntity}");
                        break;
                    case ChangeType.Modified:
                        logContent.AppendLine($"Changed Entity: #{change.Entity.EntityLabel}={change.Entity.ExpressType.ExpressNameUpper}");
                        foreach (var prop in change.ChangedProperties)
                            logContent.AppendLine($"        Property '{prop.Name}' changed from {prop.OriginalValue} to {prop.CurrentValue}");
                        break;
                    default:
                        break;
                }
            }
            return logContent;
        }

        // Colours only make sense on the real console.
        private static void SetColors(ConsoleColor foreground, ConsoleColor? background = null)
        {
            if (!IsConsole)
                return;
            Console.ForegroundColor = foreground;
            if (background.HasValue)
                Console.BackgroundColor = background.Value;
        }

        private static void ResetColors()
        {
            if (IsConsole)
                Console.ResetColor();
        }
    }
}
