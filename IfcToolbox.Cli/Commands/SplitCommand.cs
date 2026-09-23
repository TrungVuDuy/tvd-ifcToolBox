using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>
    /// ifctoolbox split model.ifc --by facility --item "Road A"
    /// ifctoolbox split model.ifc --by facility --list
    /// </summary>
    public static class SplitCommand
    {
        private static readonly Dictionary<string, SplitStrategy> Strategies = new(StringComparer.OrdinalIgnoreCase)
        {
            ["site"] = SplitStrategy.BySite,
            ["building"] = SplitStrategy.ByBuilding,
            ["storey"] = SplitStrategy.ByBuildingStorey,
            ["facility"] = SplitStrategy.ByFacility,
            ["part"] = SplitStrategy.ByFacilityPart,
            ["product"] = SplitStrategy.ByProduct,
            ["type"] = SplitStrategy.ByObjectType,
            ["data"] = SplitStrategy.DataOnly,
        };

        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var by = new Option<string>("--by", "-b")
            {
                Description = "site, building, storey, facility (IFC 4.3), part (IFC 4.3), product, type (object type) or data (no geometry).",
                Required = true,
            };
            by.AcceptOnlyFromAmong(Strategies.Keys.ToArray());
            var items = new Option<string[]>("--item")
            {
                Description = "What to extract: an entity label (#123 or 123) or a name; an object type for --by type. Repeat for several.",
                AllowMultipleArgumentsPerToken = true,
            };
            var list = new Option<bool>("--list", "-l")
            {
                Description = "List what can be selected for --by, and exit.",
            };

            var command = new Command("split", "Write part of a model to a new file: a site, building, storey, IFC 4.3 facility or facility part, products, an object type, or the data without geometry.");
            command.Arguments.Add(model);
            command.Options.Add(by);
            command.Options.Add(items);
            command.Options.Add(list);

            command.SetAction(result =>
            {
                var output = result.InvocationConfiguration.Output;
                var modelFile = result.GetValue(model);
                var strategy = Strategies[result.GetValue(by)];

                if (result.GetValue(list))
                {
                    using var listModel = IfcStore.Open(modelFile.FullName);
                    var candidates = Candidates(listModel, strategy).ToList();
                    if (candidates.Count == 0)
                        output.WriteLine($"Nothing to select for --by {result.GetValue(by)} in this model.");
                    foreach (var line in candidates)
                        output.WriteLine(line.Describe());
                    return ExitCodes.Success;
                }

                var requested = result.GetValue(items) ?? Array.Empty<string>();
                if (strategy != SplitStrategy.DataOnly && requested.Length == 0)
                    throw new CliUsageException($"--by {result.GetValue(by)} needs at least one --item. Use --list to see the choices.");

                using var ifcModel = IfcStore.Open(modelFile.FullName);
                IEnumerable<string> selected = strategy == SplitStrategy.ByObjectType
                    ? requested
                    : Resolve(ifcModel, strategy, requested);

                var config = ConfigFactory.CreateConfigSplit();
                config.SplitStrategy = strategy;
                config.SelectedItems = selected;
                config.Suffix = strategy.ToString();

                var files = SplitterProcessor.Split(ifcModel, config, modelFile.FullName);
                if (files.Count == 0)
                    throw new InvalidOperationException("Nothing was written; the selection matched no spatial element of that kind.");
                foreach (var file in files)
                    output.WriteLine(file);
                return ExitCodes.Success;
            });
            return command;
        }

        private class Candidate
        {
            public int Label;
            public string Class;
            public string Name;
            public string Usage;

            public string Describe() =>
                Label == 0
                    ? Name
                    : $"#{Label}\t{Class}{(Usage == null ? "" : " [" + Usage + "]")}\t{Name}";
        }

        private static IEnumerable<Candidate> Candidates(IfcStore model, SplitStrategy strategy)
        {
            IEnumerable<IIfcRoot> roots;
            switch (strategy)
            {
                case SplitStrategy.BySite: roots = model.Instances.OfType<IIfcSite>(); break;
                case SplitStrategy.ByBuilding: roots = model.Instances.OfType<IIfcBuilding>(); break;
                case SplitStrategy.ByBuildingStorey: roots = model.Instances.OfType<IIfcBuildingStorey>(); break;
                case SplitStrategy.ByFacility: roots = Facilities.AllFacilities(model); break;
                case SplitStrategy.ByFacilityPart: roots = Facilities.AllFacilityParts(model); break;
                case SplitStrategy.ByProduct: roots = model.Instances.OfType<IIfcSpatialStructureElement>(); break;
                case SplitStrategy.ByObjectType:
                    return model.Instances.OfType<IIfcProduct>()
                        .Where(p => !(p is IIfcSpatialStructureElement) && p.ObjectType.HasValue)
                        .Select(p => p.ObjectType.Value.ToString())
                        .Distinct().OrderBy(t => t)
                        .Select(t => new Candidate { Name = t });
                default:
                    return Enumerable.Empty<Candidate>();
            }

            return roots.Select(r => new Candidate
            {
                Label = r.EntityLabel,
                Class = r.ExpressType.Name,
                Name = r.Name,
                Usage = Facilities.IsFacilityPart(r) ? Facilities.UsageOf(r) : null,
            });
        }

        /// <summary>Turns labels ("#12", "12") and names into entity labels.</summary>
        private static List<string> Resolve(IfcStore model, SplitStrategy strategy, IEnumerable<string> requested)
        {
            // Products can be anything, so names are looked up among all of them.
            var pool = strategy == SplitStrategy.ByProduct
                ? model.Instances.OfType<IIfcProduct>().Select(p => new Candidate { Label = p.EntityLabel, Name = p.Name }).ToList()
                : Candidates(model, strategy).ToList();

            var labels = new List<string>();
            foreach (var item in requested)
            {
                var text = item.Trim().TrimStart('#');
                if (int.TryParse(text, out var label))
                {
                    labels.Add(label.ToString());
                    continue;
                }

                var matches = pool.Where(c => string.Equals(c.Name, item, StringComparison.Ordinal)).ToList();
                if (matches.Count == 0)
                    throw new CliUsageException($"No {strategy.ToString().Replace("By", "").ToLowerInvariant()} named '{item}'. Use --list to see the choices.");
                // Names are not unique in IFC; take every match rather than guessing.
                labels.AddRange(matches.Select(m => m.Label.ToString()));
            }
            return labels.Distinct().ToList();
        }
    }
}
