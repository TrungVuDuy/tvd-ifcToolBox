using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Core.Utilities;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xbim.Ifc;

namespace IfcToolbox.Tools.Processors
{
    /// <summary>
    /// Checks a model against an IDS 1.0 document.
    /// <para>
    /// This is the standards-based successor to <see cref="ValidatorProcessor"/>: instead of a
    /// requirement tree built in code, the requirements come from an .ids file that can be
    /// exchanged with anyone, and the check covers entities, attributes, properties,
    /// classifications, materials and relationships rather than property existence alone.
    /// </para>
    /// </summary>
    public class IdsProcessor
    {
        /// <summary>
        /// Validates <paramref name="filePath"/> against the IDS at <paramref name="idsFilePath"/>.
        /// The result value is an <see cref="IdsValidationResult"/>.
        /// </summary>
        public static IProcessorResult Process(string filePath, string idsFilePath, bool consoleMode = false,
            bool reportPassingEntities = false, CancellationToken cancellationToken = default)
        {
            var document = IdsReader.Load(idsFilePath);
            return Process(filePath, document, consoleMode, reportPassingEntities, cancellationToken);
        }

        public static IProcessorResult Process(string filePath, IdsDocument document, bool consoleMode = false,
            bool reportPassingEntities = false, CancellationToken cancellationToken = default)
        {
            if (consoleMode)
                Marslogger.Step($"{filePath} in processing");

            var processorResult = ProcessorResultFactory.CreateNew();

            using (var watch = new Superwatch())
            using (var model = IfcStore.Open(filePath))
            {
                if (consoleMode)
                {
                    Log.Information($"IFC Schema - {model.SchemaVersion}");
                    Log.Information($"IDS - {document.Info?.Title} ({document.Specifications.Count} specifications)");
                    Marslogger.Step("Processing...");
                }

                var validator = new IdsValidator { ReportPassingEntities = reportPassingEntities };
                var result = validator.Validate(document, model, filePath, cancellationToken);

                processorResult.Value = result;
                // The processor ran; whether the model satisfies the IDS is in the result.
                processorResult.Success = true;

                if (consoleMode)
                    Marslogger.Mark(result.ToString());

                return processorResult;
            }
        }

        /// <summary>
        /// Validates and writes the findings next to the model as a CSV, returning the report path.
        /// </summary>
        public static IProcessorResult ProcessToCsv(string filePath, string idsFilePath, bool consoleMode = false)
        {
            var processorResult = Process(filePath, idsFilePath, consoleMode);
            var result = (IdsValidationResult)processorResult.Value;

            var reportPath = Path.ChangeExtension(ConsoleFile.AddSuffixToName(filePath, "_IdsReport"), ".csv");
            File.WriteAllText(reportPath, IdsReport.ToCsv(result), System.Text.Encoding.UTF8);
            processorResult.FilePaths.Add(reportPath);

            if (consoleMode)
                Log.Information(IdsReport.ToText(result));

            return processorResult;
        }

        /// <summary>Validates one model against several IDS documents.</summary>
        public static IList<IdsValidationResult> Process(string filePath, IEnumerable<string> idsFilePaths,
            bool reportPassingEntities = false, CancellationToken cancellationToken = default)
        {
            var results = new List<IdsValidationResult>();
            using (var model = IfcStore.Open(filePath))
            {
                var validator = new IdsValidator { ReportPassingEntities = reportPassingEntities };
                foreach (var idsFilePath in idsFilePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var document = IdsReader.Load(idsFilePath);
                    results.Add(validator.Validate(document, model, filePath, cancellationToken));
                }
            }
            return results;
        }
    }
}
