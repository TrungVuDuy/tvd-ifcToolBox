using IfcToolbox.Core.Ids;
using IfcToolbox.Core.Ids.Model;
using IfcToolbox.Core.Ids.Validation;
using IfcToolbox.Core.Utilities;
using IfcToolbox.Tools.Processors;
using Serilog;
using System.IO;

namespace IfcToolbox.Examples.Samples
{
    /// <summary>
    /// Checking a model against an Information Delivery Specification.
    /// IDS 1.0 has been the official buildingSMART standard since June 2024; it replaces
    /// hand-written requirement trees with a file anyone can exchange and any IDS tool can read.
    /// </summary>
    public class IfcIdsSample
    {
        /// <summary>Validates a model against an existing .ids file and writes a CSV report.</summary>
        public static void ValidateAgainstIds(string filePath, string idsFilePath)
        {
            Log.Information("IfcIds - Start");
            var result = IdsProcessor.ProcessToCsv(filePath, idsFilePath, true);
            foreach (var reportPath in result.FilePaths)
                Marslogger.Mark($"Report written to {reportPath}");
        }

        /// <summary>
        /// Builds a small IDS in code, saves it, then runs it. Useful as a starting point for
        /// a project's own requirements.
        /// </summary>
        public static void CreateAndRunIds(string filePath)
        {
            Log.Information("IfcIds - Start");

            var document = new IdsDocument
            {
                Info = new IdsInfo
                {
                    Title = "Example requirements",
                    Description = "External walls must state a fire rating and must not be timber.",
                },
                Specifications =
                {
                    new IdsSpecification
                    {
                        Name = "External walls carry a fire rating",
                        Identifier = "EX-01",
                        IfcVersions = { IdsIfcVersion.Ifc4 },
                        Applicability = new IdsApplicability
                        {
                            // minOccurs 0 makes the specification optional: if the model has no
                            // external wall at all, that is not a failure.
                            MinOccurs = 0,
                            Entity = new IdsEntityFacet { Name = new IdsValue("IFCWALL") },
                            Properties =
                            {
                                new IdsPropertyFacet
                                {
                                    PropertySet = new IdsValue("Pset_WallCommon"),
                                    BaseName = new IdsValue("IsExternal"),
                                    Value = new IdsValue("true"),
                                },
                            },
                        },
                        Requirements = new IdsRequirements
                        {
                            Properties =
                            {
                                new IdsPropertyFacet
                                {
                                    PropertySet = new IdsValue("Pset_WallCommon"),
                                    BaseName = new IdsValue("FireRating"),
                                    DataType = "IFCLABEL",
                                    Instructions = "Use the rating from the fire strategy report.",
                                },
                            },
                            Materials =
                            {
                                new IdsMaterialFacet
                                {
                                    Value = new IdsValue("Timber"),
                                    Cardinality = IdsCardinality.Prohibited,
                                },
                            },
                        },
                    },
                },
            };

            var idsPath = Path.ChangeExtension(filePath, ".ids");
            IdsWriter.Save(document, idsPath);
            Marslogger.Mark($"IDS written to {idsPath}");

            var processorResult = IdsProcessor.Process(filePath, document, true);
            var result = (IdsValidationResult)processorResult.Value;
            Log.Information(IdsReport.ToText(result));
        }
    }
}
