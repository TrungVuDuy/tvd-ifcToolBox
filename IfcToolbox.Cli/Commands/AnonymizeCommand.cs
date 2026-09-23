using IfcToolbox.Core.Editors;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace IfcToolbox.Cli.Commands
{
    /// <summary>ifctoolbox anonymize model.ifc [--replace IfcFurniture:Acme:Supplier]</summary>
    public static class AnonymizeCommand
    {
        public static Command Create()
        {
            var model = CommandHelpers.ModelArgument();
            var output = CommandHelpers.OutputOption("Output file. Defaults to <model>_Anonymized.ifc next to the model.");
            var replace = new Option<string[]>("--replace")
            {
                Description = "Also replace text in product names, object types and properties: IfcClass:find:replacement. Repeat for several.",
                AllowMultipleArgumentsPerToken = true,
            };

            var command = new Command("anonymize",
                "Remove people, organisations, addresses, applications and file header authorship; optionally replace product text.");
            command.Arguments.Add(model);
            command.Options.Add(output);
            command.Options.Add(replace);

            command.SetAction(result =>
            {
                var writer = result.InvocationConfiguration.Output;
                var config = ConfigFactory.CreateConfigAnonymize();
                config.Suffix = "Anonymized";
                config.AnonymeUserInfo = true;
                config.RemoveAllUserInfo();

                var rules = ParseRules(result.GetValue(replace));
                if (rules.Count > 0)
                {
                    config.AnonymeProductInfo = true;
                    config.ReplaceAllInProductInfo();
                    config.Rules = rules;
                }

                var processorResult = AnonymizerProcessor.Process(result.GetValue(model).FullName, config);
                // The processor also writes a change log; the model is the last file.
                var produced = processorResult.FilePaths.Last();
                writer.WriteLine(CommandHelpers.PlaceOutput(produced, result.GetValue(output)));
                return ExitCodes.Success;
            });
            return command;
        }

        private static List<AnonymeRule> ParseRules(string[] values)
        {
            var rules = new List<AnonymeRule>();
            foreach (var value in values ?? new string[0])
            {
                var parts = value.Split(':');
                // The replacement may be empty, which removes the text.
                if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrEmpty(parts[1]))
                    throw new CliUsageException($"--replace '{value}' must look like IfcClass:find:replacement.");
                rules.Add(new AnonymeRule(parts[0], parts[1], parts[2]));
            }
            return rules;
        }
    }
}
