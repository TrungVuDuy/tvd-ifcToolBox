using IfcToolbox.Core.Convert;
using IfcToolbox.Core.Hierarchy;
using IfcToolbox.Core.Utilities;
using IfcToolbox.Tools.Configurations;
using System.IO;
using System.Reflection;
using Xbim.Ifc;

namespace IfcToolbox.Tools.Processors
{
    public class ConverterProcessor
    {
        public static SpatialTrees Analyse(string filePath, IConfigBase config = null, bool consoleMode = false)
        {
            if (config == null)
                config = ConfigFactory.CreateConfigBase();
            if (consoleMode)
                Marslogger.Step($"{filePath} in processing");
            using var watch = new Superwatch();
            using var model = IfcStore.Open(filePath);
            if (consoleMode)
                Marslogger.Step("Processing...");
            var result = TreesReader.GetOnlyTypedNodes(model, true);
            return result;
        }

        public static IProcessorResult Process(string filePath, IConfigConvert config, bool consoleMode = false)
        {
            if (consoleMode)
                Marslogger.Step($"{filePath} in processing");
            var processorResult = ProcessorResultFactory.CreateNew();
            using (var watch = new Superwatch())
            {
                if (consoleMode)
                    Marslogger.Step("Processing...");

                // With UseExternalResource the caller supplies its own IfcConvert in
                // ExternalWorkingDirectory; otherwise the embedded one is written out.
                // IfcConvert.ResolveExecutable applies the same order either way.
                if (!config.UseExternalResource)
                    IfcConvert.EnsureExecutable();

                string targetFilePath = Path.ChangeExtension(filePath, ConvertFormatInfo.GetExtension(config.TargetFormat));
                // "model.log.txt", as the command line tool writes. This used to be "model.txt",
                // which the clean up below then deleted: a user's own notes next to the model
                // were lost on every conversion.
                string logFilePath = Path.ChangeExtension(filePath, ".log.txt");
                // A previous run's log would otherwise be appended to.
                if (File.Exists(logFilePath))
                    File.Delete(logFilePath);
                var finalFiles = IfcConvert.Convert(filePath, targetFilePath, config.ConvertOptions, config.TargetFormat, logFilePath, consoleMode, config.ExternalWorkingDirectory);
                
                foreach (var finalFile in finalFiles)
                    processorResult.FilePaths.Add(finalFile);
                processorResult.Success = true;

                return processorResult;
            }
        }
    }
}
