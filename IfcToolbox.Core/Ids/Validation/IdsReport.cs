using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// Renders a validation result for people to read, and as a table for spreadsheets.
    /// </summary>
    public static class IdsReport
    {
        /// <summary>Short summary plus every failure, suitable for a console or a log.</summary>
        public static string ToText(IdsValidationResult result, bool includePassing = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine(result.Title ?? "IDS validation");
            if (!string.IsNullOrEmpty(result.ModelFile))
                sb.AppendLine($"Model: {Path.GetFileName(result.ModelFile)} ({result.ModelSchema})");
            sb.AppendLine($"{result.PassedCount} passed, {result.FailedCount} failed, " +
                          $"{result.NotApplicableCount} not applicable, {result.SkippedCount} skipped");
            sb.AppendLine();

            foreach (var specification in result.Specifications)
            {
                if (specification.Status != IdsStatus.Fail && !includePassing)
                    continue;

                sb.AppendLine($"[{specification.Status.ToString().ToUpperInvariant()}] {specification.Specification?.Name}");
                if (!string.IsNullOrEmpty(specification.Message))
                    sb.AppendLine($"    {specification.Message}");
                if (!string.IsNullOrEmpty(specification.Specification?.Instructions))
                    sb.AppendLine($"    Instructions: {specification.Specification.Instructions}");

                foreach (var entity in specification.Entities.Where(e => includePassing || !e.Passed))
                {
                    sb.AppendLine($"    #{entity.EntityLabel} {entity.EntityType} " +
                                  $"{(entity.GlobalId == null ? string.Empty : "[" + entity.GlobalId + "] ")}{entity.Name}");
                    foreach (var facet in entity.Facets.Where(f => includePassing || !f.Passed))
                        sb.AppendLine($"        {(facet.Passed ? "ok  " : "FAIL")} {facet.Message}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// One row per failing facet, for a spreadsheet or an issue tracker.
        /// </summary>
        public static IEnumerable<IdsReportRow> ToRows(IdsValidationResult result, bool includePassing = false)
        {
            foreach (var specification in result.Specifications)
                foreach (var entity in specification.Entities)
                    foreach (var facet in entity.Facets)
                    {
                        if (facet.Passed && !includePassing)
                            continue;
                        yield return new IdsReportRow
                        {
                            Specification = specification.Specification?.Name,
                            Identifier = specification.Specification?.Identifier,
                            EntityLabel = entity.EntityLabel,
                            GlobalId = entity.GlobalId,
                            EntityType = entity.EntityType,
                            EntityName = entity.Name,
                            Requirement = facet.Facet?.Describe(),
                            Cardinality = facet.Cardinality.ToString(),
                            Passed = facet.Passed,
                            Observed = facet.Observed,
                            Message = facet.Message,
                        };
                    }
        }

        /// <summary>Comma separated rows, with the header line.</summary>
        public static string ToCsv(IdsValidationResult result, bool includePassing = false, string delimiter = ",")
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(delimiter, new[]
            {
                "Specification", "Identifier", "EntityLabel", "GlobalId", "EntityType", "EntityName",
                "Requirement", "Cardinality", "Passed", "Observed", "Message",
            }));

            foreach (var row in ToRows(result, includePassing))
            {
                sb.AppendLine(string.Join(delimiter, new[]
                {
                    Escape(row.Specification, delimiter),
                    Escape(row.Identifier, delimiter),
                    row.EntityLabel.ToString(CultureInfo.InvariantCulture),
                    Escape(row.GlobalId, delimiter),
                    Escape(row.EntityType, delimiter),
                    Escape(row.EntityName, delimiter),
                    Escape(row.Requirement, delimiter),
                    Escape(row.Cardinality, delimiter),
                    row.Passed ? "true" : "false",
                    Escape(row.Observed, delimiter),
                    Escape(row.Message, delimiter),
                }));
            }

            return sb.ToString();
        }

        /// <summary>Several results (one per IDS document) in one table with a single header line.</summary>
        public static string ToCsv(IEnumerable<IdsValidationResult> results, bool includePassing = false, string delimiter = ",")
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var result in results)
            {
                var csv = ToCsv(result, includePassing, delimiter);
                sb.Append(first ? csv : csv.Substring(csv.IndexOf('\n') + 1));
                first = false;
            }
            return sb.ToString();
        }

        private static string Escape(string value, string delimiter)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (!value.Contains(delimiter) && !value.Contains("\"") && !value.Contains("\n"))
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public class IdsReportRow
    {
        public string Specification { get; set; }
        public string Identifier { get; set; }
        public int EntityLabel { get; set; }
        public string GlobalId { get; set; }
        public string EntityType { get; set; }
        public string EntityName { get; set; }
        public string Requirement { get; set; }
        public string Cardinality { get; set; }
        public bool Passed { get; set; }
        public string Observed { get; set; }
        public string Message { get; set; }
    }
}
