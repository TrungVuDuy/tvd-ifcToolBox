using IfcToolbox.Core.Ids.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// Checks an IFC model against an IDS 1.0 document.
    /// <para>
    /// For each specification the applicability facets select entities (all must match), then
    /// every requirement facet is tested against each selected entity according to its
    /// cardinality: required must match, prohibited must not, optional always passes.
    /// </para>
    /// </summary>
    public class IdsValidator
    {
        private readonly IdsFacetMatcher _matcher = new IdsFacetMatcher();

        /// <summary>
        /// Include an entry per passing entity in the result. Off by default because a large
        /// model produces an enormous amount of uninteresting detail; failures are always kept.
        /// </summary>
        public bool ReportPassingEntities { get; set; }

        public IdsValidationResult Validate(IdsDocument document, IModel model, string modelFile = null,
            CancellationToken cancellationToken = default)
        {
            var result = new IdsValidationResult
            {
                Title = document.Info?.Title,
                ModelFile = modelFile,
                ModelSchema = model.SchemaVersion.ToString(),
            };

            foreach (var specification in document.Specifications)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Specifications.Add(ValidateSpecification(specification, model, cancellationToken));
            }

            return result;
        }

        private IdsSpecificationResult ValidateSpecification(IdsSpecification specification, IModel model,
            CancellationToken cancellationToken)
        {
            var result = new IdsSpecificationResult { Specification = specification };

            // ifcVersion is recorded but does not gate evaluation. The official test cases
            // declare IFC2X3 specifications and run them against IFC4 models expecting real
            // results, so treating a mismatch as "skip" would silently pass those checks.
            if (!specification.AppliesTo(model.SchemaVersion))
                result.Message = $"Specification declares {string.Join(", ", specification.IfcVersions)} " +
                                 $"but the model is {model.SchemaVersion}; checked anyway.";

            var applicable = SelectApplicable(specification.Applicability, model, cancellationToken).ToList();

            // maxOccurs 0 makes the specification prohibited: the applicable set must be
            // empty and the requirements are not evaluated at all.
            if (specification.Applicability.MaxOccurs == 0)
            {
                result.Status = applicable.Count == 0 ? IdsStatus.Pass : IdsStatus.Fail;
                if (applicable.Count > 0)
                    result.Message = $"{applicable.Count} entities match an applicability that prohibits any.";
                return result;
            }

            // Kept separately because passing entities are normally not listed.
            result.ApplicableCount = applicable.Count;
            foreach (var entity in applicable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entityResult = CheckRequirements(specification.Requirements, entity);
                if (!entityResult.Passed || ReportPassingEntities)
                    result.Entities.Add(entityResult);
            }

            // Counting has to use the real number of applicable entities, not the trimmed list.
            int failed = applicable.Count == 0 ? 0 : result.Entities.Count(e => !e.Passed);
            result.Status = DetermineStatus(specification, applicable.Count, failed, result);
            return result;
        }

        private IdsStatus DetermineStatus(IdsSpecification specification, int applicableCount, int failedCount,
            IdsSpecificationResult result)
        {
            var occurs = specification.Applicability;

            // maxOccurs 0 means the applicable entities must not exist at all.
            if (occurs.MaxOccurs.HasValue && applicableCount > occurs.MaxOccurs.Value)
            {
                result.Message = occurs.MaxOccurs.Value == 0
                    ? $"{applicableCount} entities match an applicability that prohibits any."
                    : $"{applicableCount} entities match, at most {occurs.MaxOccurs.Value} allowed.";
                return IdsStatus.Fail;
            }

            // xs:occurs defaults minOccurs to 1, which makes a specification required unless
            // it says otherwise: if nothing matches, that is a failure, not a no-op.
            int minOccurs = occurs.MinOccurs ?? 1;
            if (applicableCount < minOccurs)
            {
                result.Message = $"{applicableCount} entities match, at least {minOccurs} required.";
                return IdsStatus.Fail;
            }

            if (failedCount > 0)
                return IdsStatus.Fail;

            if (applicableCount == 0)
            {
                result.Message = "No entity in the model matched the applicability.";
                return IdsStatus.NotApplicable;
            }

            return IdsStatus.Pass;
        }

        #region Applicability

        private IEnumerable<IPersistEntity> SelectApplicable(IdsApplicability applicability, IModel model,
            CancellationToken cancellationToken)
        {
            foreach (var entity in CandidateEntities(applicability, model))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsApplicable(applicability, entity))
                    yield return entity;
            }
        }

        /// <summary>
        /// Narrows the search when the applicability names a single entity type, which avoids
        /// walking every instance in the model for the common case.
        /// <para>
        /// The universe is every IFC instance, not just IfcObjectDefinition: IDS specifications
        /// legitimately target resource level entities such as IfcSurfaceStyleRefraction.
        /// </para>
        /// </summary>
        private static IEnumerable<IPersistEntity> CandidateEntities(IdsApplicability applicability, IModel model)
        {
            var name = applicability.Entity?.Name;
            if (name != null && !name.IsRestriction && !string.IsNullOrWhiteSpace(name.SimpleValue))
            {
                var candidates = OfType(model, name.SimpleValue);

                // On IFC2X3 the name may also stand for an occurrence/type pair, which lives
                // under a different class entirely.
                if (model.SchemaVersion == Xbim.Common.Step21.XbimSchemaVersion.Ifc2X3
                    && Ifc2x3EntityMapping.TryGet(name.SimpleValue, out var mapping))
                {
                    candidates = candidates
                        .Concat(OfType(model, mapping.OccurrenceEntity))
                        .GroupBy(e => e.EntityLabel).Select(g => g.First());
                }

                return candidates;
            }

            return model.Instances.OfType<IPersistEntity>();
        }

        /// <summary>
        /// Instances of a class by name. xbim throws when the name is not part of the model's
        /// schema, which is routine here: an IDS written for IFC4 can name classes that simply
        /// do not exist in an IFC2X3 model.
        /// </summary>
        private static IEnumerable<IPersistEntity> OfType(IModel model, string expressType)
        {
            try
            {
                return model.Instances.OfType(expressType, activate: true).OfType<IPersistEntity>().ToList();
            }
            catch (ArgumentException)
            {
                return Enumerable.Empty<IPersistEntity>();
            }
        }

        private bool IsApplicable(IdsApplicability applicability, IPersistEntity entity)
        {
            // An empty applicability selects everything, which is what the schema implies
            // when no facet is present.
            foreach (var facet in applicability.AllFacets())
                if (!_matcher.Match(facet, entity).Matched)
                    return false;
            return true;
        }

        #endregion

        #region Requirements

        private IdsEntityResult CheckRequirements(IdsRequirements requirements, IPersistEntity entity)
        {
            var root = entity as IIfcRoot;
            var result = new IdsEntityResult
            {
                EntityLabel = entity.EntityLabel,
                GlobalId = root == null ? null : IdsFacetMatcher.ToText(root.GlobalId),
                EntityType = entity.ExpressType.ExpressNameUpper,
                Name = root == null ? null : IdsFacetMatcher.ToText(root.Name),
            };

            if (requirements == null)
                return result;

            foreach (var facet in requirements.AllFacets())
                result.Facets.Add(CheckFacet(facet, entity));

            return result;
        }

        private IdsFacetResult CheckFacet(IIdsFacet facet, IPersistEntity entity)
        {
            var match = _matcher.Match(facet, entity);
            var result = new IdsFacetResult
            {
                Facet = facet,
                Cardinality = facet.Cardinality,
                Observed = match.Observed,
            };

            switch (facet.Cardinality)
            {
                case IdsCardinality.Required:
                    result.Passed = match.Matched;
                    result.Message = match.Matched
                        ? $"{facet.Describe()} found: {match.Observed}"
                        : Describe("Required", facet, match);
                    break;

                case IdsCardinality.Prohibited:
                    result.Passed = !match.Matched;
                    result.Message = match.Matched
                        ? $"Prohibited {facet.Describe()} is present: {match.Observed}"
                        : $"Prohibited {facet.Describe()} is absent";
                    break;

                case IdsCardinality.Optional:
                    // "Matching entities either don't have the property, or if they do, it is
                    // of the expected datatype and value." So an optional facet only fails
                    // when the thing exists but does not satisfy the constraint.
                    result.Passed = !match.Located || match.Matched;
                    result.Message = !match.Located
                        ? $"Optional {facet.Describe()} not present"
                        : match.Matched
                            ? $"Optional {facet.Describe()} found: {match.Observed}"
                            : $"Optional {facet.Describe()} is present but does not match, found: {match.Observed}";
                    break;
            }

            if (!result.Passed && !string.IsNullOrWhiteSpace(facet.Instructions))
                result.Message += $" - {facet.Instructions}";

            return result;
        }

        private static string Describe(string prefix, IIdsFacet facet, IdsFacetMatch match)
        {
            return match.Observed == null
                ? $"{prefix} {facet.Describe()} is missing"
                : $"{prefix} {facet.Describe()}, found: {match.Observed}";
        }

        #endregion
    }
}
