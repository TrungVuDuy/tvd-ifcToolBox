using IfcToolbox.Core.Ids.Model;
using System.Collections.Generic;
using System.Linq;

namespace IfcToolbox.Core.Ids.Validation
{
    public enum IdsStatus
    {
        Pass,
        Fail,

        /// <summary>No entity in the model matched the applicability, and none was required.</summary>
        NotApplicable,

        /// <summary>The specification does not target the model's IFC schema.</summary>
        Skipped,
    }

    /// <summary>Result of checking one IDS document against one model.</summary>
    public class IdsValidationResult
    {
        public string Title { get; set; }
        public string ModelFile { get; set; }
        public string ModelSchema { get; set; }
        public IList<IdsSpecificationResult> Specifications { get; set; } = new List<IdsSpecificationResult>();

        public bool Passed => Specifications.All(s => s.Status != IdsStatus.Fail);

        public int PassedCount => Specifications.Count(s => s.Status == IdsStatus.Pass);
        public int FailedCount => Specifications.Count(s => s.Status == IdsStatus.Fail);
        public int SkippedCount => Specifications.Count(s => s.Status == IdsStatus.Skipped);
        public int NotApplicableCount => Specifications.Count(s => s.Status == IdsStatus.NotApplicable);

        public override string ToString() =>
            $"{Title}: {PassedCount} passed, {FailedCount} failed, {NotApplicableCount} not applicable, {SkippedCount} skipped";
    }

    public class IdsSpecificationResult
    {
        public IdsSpecification Specification { get; set; }
        public IdsStatus Status { get; set; }

        /// <summary>Why the specification as a whole failed or was skipped, when that is not obvious from the entities.</summary>
        public string Message { get; set; }

        /// <summary>
        /// Entities the applicability selected: every failing one, and the passing ones only when
        /// <see cref="IdsValidator.ReportPassingEntities"/> is on.
        /// </summary>
        public IList<IdsEntityResult> Entities { get; set; } = new List<IdsEntityResult>();

        /// <summary>
        /// How many entities the applicability selected, including passing ones left out of
        /// <see cref="Entities"/>. Falls back to the list when it was never set.
        /// </summary>
        public int ApplicableCount
        {
            get => _applicableCount ?? Entities.Count;
            set => _applicableCount = value;
        }

        private int? _applicableCount;

        public int FailedCount => Entities.Count(e => !e.Passed);
        public int PassedCount => ApplicableCount - FailedCount;

        public IEnumerable<IdsEntityResult> Failures => Entities.Where(e => !e.Passed);

        public override string ToString() =>
            $"[{Status}] {Specification?.Name}: {PassedCount}/{ApplicableCount} entities pass";
    }

    public class IdsEntityResult
    {
        public int EntityLabel { get; set; }
        public string GlobalId { get; set; }
        public string EntityType { get; set; }
        public string Name { get; set; }
        public IList<IdsFacetResult> Facets { get; set; } = new List<IdsFacetResult>();

        public bool Passed => Facets.All(f => f.Passed);

        public IEnumerable<IdsFacetResult> Failures => Facets.Where(f => !f.Passed);

        public override string ToString() =>
            $"#{EntityLabel} {EntityType} '{Name}' {(Passed ? "pass" : "fail")}";
    }

    public class IdsFacetResult
    {
        public IIdsFacet Facet { get; set; }
        public IdsCardinality Cardinality { get; set; }
        public bool Passed { get; set; }

        /// <summary>Value found in the model, or null when nothing was found.</summary>
        public string Observed { get; set; }

        /// <summary>Explanation suitable for a report.</summary>
        public string Message { get; set; }

        public override string ToString() => $"{(Passed ? "pass" : "FAIL")} {Facet?.Describe()} - {Message}";
    }
}
