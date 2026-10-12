using IfcToolbox.Core.Analyse;
using IfcToolbox.Core.Editors;
using IfcToolbox.Core.Utilities;
using IfcToolbox.Tools.Configurations;
using IfcToolbox.Tools.Processors;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xunit;

namespace IfcToolbox.Tests
{
    public class IfcAnonymizerTests
    {
        [Fact]
        public static void AnonymizeUserInfoShouldPass()
        {
            var filePath = LocalFiles.Ifc4_SampleHouse;
            var outputFolder = LocalFiles.TestOutputFolder;
            string outputFileName = ConsoleFile.GetOutputFileName(filePath, outputFolder, "");
            ConsoleFile.CreateCopyIfcFile(filePath, outputFileName);

            IConfigAnonymize config = ConfigFactory.CreateConfigAnonymize();
            config.LogDetail = true;
            config.Suffix = "Anonymizer_UserInfo";
            config.AnonymeUserInfo = true;
            config.RemoveAllUserInfo();
            
            using var model = IfcStore.Open(outputFileName);
            using var txn = model.BeginTransaction("Modification");
            AnonymizerProcessor.Anonymize(model, config);
            txn.Commit();
            model.SaveAs(ConsoleFile.AddSuffixToName(outputFileName, "_" + config.Suffix));

            Assert.True(!model.Instances.OfType<IIfcPostalAddress>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcTelecomAddress>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcActorRole>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcPerson>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcOrganization>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcPersonAndOrganization>().ToList().Any());
            Assert.True(!model.Instances.OfType<IIfcApplication>().ToList().Any());
        }

        [Fact]
        public static void AnonymizeProductInfoShouldPass()
        {
            var filePath = LocalFiles.Ifc4_SampleHouse;
            var outputFolder = LocalFiles.TestOutputFolder;
            string outputFileName = ConsoleFile.GetOutputFileName(filePath, outputFolder, "");
            ConsoleFile.CreateCopyIfcFile(filePath, outputFileName);

            IConfigAnonymize config = ConfigFactory.CreateConfigAnonymize();
            config.LogDetail = true;
            config.Suffix = "Anonymizer_ProductInfo";
            config.AnonymeProductInfo = true;
            config.ReplaceAllInProductInfo();
            List<AnonymeRule> rules = new List<AnonymeRule>();
            rules.Add(new AnonymeRule("IfcFurniture", "Furniture_Couch_Viper", "Unknown"));
            rules.Add(new AnonymeRule("IfcFurniture", "Furniture_Chair_Viper", "Unknown"));
            rules.Add(new AnonymeRule("IfcFurniture", "Chair - Dining", "Unknown"));
            rules.Add(new AnonymeRule("IfcFurniture", "Furniture_Table_Dining_w", "Unknown"));
            config.Rules = rules;

            using var model = IfcStore.Open(outputFileName);
            using var txn = model.BeginTransaction("Modification");
            AnonymizerProcessor.Anonymize(model, config);
            txn.Commit();
            model.SaveAs(ConsoleFile.AddSuffixToName(outputFileName, "_" + config.Suffix));

            Assert.True(!model.Instances.OfType<IIfcFurniture>().Where(x => x.Name.ToString().Contains("Furniture_Couch_Viper")).Any());
            Assert.True(!model.Instances.OfType<IIfcFurniture>().Where(x => x.Name.ToString().Contains("Furniture_Chair_Viper")).Any());
            Assert.True(!model.Instances.OfType<IIfcFurniture>().Where(x => x.Name.ToString().Contains("Chair - Dining")).Any());
            Assert.True(!model.Instances.OfType<IIfcFurniture>().Where(x => x.Name.ToString().Contains("Furniture_Table_Dining_w")).Any());
        }

        /// <summary>
        /// Property values used to be replaced only in IFC4 models: the check was for the IFC4
        /// IfcLabel class, which IFC2X3 and IFC 4.3 values are not.
        /// </summary>
        [Theory]
        [InlineData(LocalFiles.Ifc2x3_Duplex_Architecture)]
        [InlineData(LocalFiles.Ifc4_SampleHouse)]
        [InlineData(LocalFiles.Ifc4x3_Building)]
        public static void ReplacesPropertyValuesInEverySchema(string file)
        {
            using var model = IfcStore.Open(file);
            // Any product with a label property to anonymize.
            var (product, property) = model.Instances.OfType<IIfcProduct>()
                .Where(p => !(p is IIfcSpatialStructureElement))
                .SelectMany(p => PropertiesReader.GetAllProperties(p).Select(prop => (p, prop)))
                .First(x => x.prop.NominalValue?.GetType().Name == "IfcLabel" && x.prop.NominalValue.ToString().Length > 3);
            var keyword = property.NominalValue.ToString();

            var config = ConfigFactory.CreateConfigAnonymize();
            config.AnonymeProductInfo = true;
            config.ReplaceAllInProductInfo();
            // The empty rule must be ignored, not crash string.Replace.
            config.Rules = new List<AnonymeRule>
            {
                new(product.ExpressType.Name, "", "Unknown"),
                new(product.ExpressType.Name, keyword, "Unknown"),
            };

            using (var txn = model.BeginTransaction("Anonymize"))
            {
                AnonymizerProcessor.Anonymize(model, config);
                txn.Commit();
            }

            Assert.Equal("Unknown", property.NominalValue.ToString());
            Assert.Equal("IfcLabel", property.NominalValue.GetType().Name);
        }
    }
}
