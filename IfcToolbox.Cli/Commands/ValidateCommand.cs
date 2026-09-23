using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>
    /// ifctoolbox validate model.ifc --ids requirements.ids
    /// Exits with 1 when the model fails, so it can gate a pipeline.
    /// </summary>
    public static class ValidateCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument("The IFC model to check.");
            var ids = new Option<FileInfo[]>("--ids", "-i")
            {
                Description = "IDS file(s) to check against. Repeat for several.",
                Required = true,
                AllowMultipleArgumentsPerToken = true,
            }.AcceptExistingOnly();
            var report = new Option<FileInfo>("--report", "-r")
            {
                Description = "Also write every failing requirement to this CSV file.",
            };
            var json = CommandHelpers.JsonOption();
            var all = new Option<bool>("--all")
            {
                Description = "List passing specifications too, not only failures.",
            };

            var command = new Command("validate", "Check a model against Information Delivery Specifications (IDS 1.0). Exits with 1 when it fails.");
            command.Arguments.Add(model);
            command.Options.Add(ids);
            command.Options.Add(report);
            command.Options.Add(json);
            command.Options.Add(all);

            command.SetAction(result =>
            {
                var output = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);
                var idsFiles = result.GetValue(ids);

                var results = IdsProcessor.Process(modelFile.FullName, idsFiles.Select(f => f.FullName));

                var reportFile = result.GetValue(report);
                if (reportFile != null)
                    WriteCsv(reportFile, results);

                if (result.GetValue(json))
                    CommandHelpers.WriteJson(output, ToJson(modelFile, idsFiles, results));
                else
                    WriteText(output, results, result.GetValue(all), reportFile);

                return results.All(r => r.Passed) ? ExitCodes.Success : ExitCodes.ValidationFailed;
            });
            return command;
        }

        private static void WriteText(TextWriter output, IList<IdsValidationResult> results, bool includePassing, FileInfo reportFile)
        {
            foreach (var result in results)
                output.Write(IdsReport.ToText(result, includePassing));

            int failed = results.Sum(r => r.FailedCount);
            int passed = results.Sum(r => r.PassedCount);
            output.WriteLine(failed == 0
                ? $"PASSED: {passed} specification(s) satisfied."
                : $"FAILED: {failed} specification(s) not satisfied, {passed} satisfied.");
            if (reportFile != null)
                output.WriteLine($"Report written to {reportFile.FullName}");
        }

        private static void WriteCsv(FileInfo reportFile, IList<IdsValidationResult> results)
        {
            reportFile.Directory?.Create();
            var sb = new StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                var csv = IdsReport.ToCsv(results[i]);
                // One header for the whole file.
                sb.Append(i == 0 ? csv : csv.Substring(csv.IndexOf('\n') + 1));
            }
            File.WriteAllText(reportFile.FullName, sb.ToString(), Encoding.UTF8);
        }

        private static object ToJson(FileInfo model, FileInfo[] idsFiles, IList<IdsValidationResult> results) => new
        {
            model = model.Name,
            passed = results.All(r => r.Passed),
            documents = results.Select((r, i) => new
            {
                ids = idsFiles[i].Name,
                title = r.Title,
                schema = r.ModelSchema,
                passed = r.Passed,
                specifications = r.Specifications.Select(s => new
                {
                    name = s.Specification?.Name,
                    identifier = s.Specification?.Identifier,
                    status = s.Status.ToString(),
                    message = s.Message,
                    failures = s.Failures.Select(e => new
                    {
                        label = e.EntityLabel,
                        globalId = e.GlobalId,
                        entity = e.EntityType,
                        name = e.Name,
                        requirements = e.Failures.Select(f => new
                        {
                            requirement = f.Facet?.Describe(),
                            cardinality = f.Cardinality.ToString(),
                            observed = f.Observed,
                            message = f.Message,
                        }),
                    }),
                }),
            }),
        };
    }
}
