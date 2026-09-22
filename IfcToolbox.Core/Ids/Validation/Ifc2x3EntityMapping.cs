using System.Collections.Generic;

namespace IfcToolbox.Core.Ids.Validation
{
    /// <summary>
    /// IFC2X3 has no occurrence class for a number of distribution and furnishing elements;
    /// they are expressed as a generic occurrence plus a specific type object. IDS asks for
    /// the IFC4 style name and leaves it to the implementation to resolve the pair.
    /// <para>
    /// Generated from Documentation/ImplementersDocumentation/ifc2x3-occurrence-type-mapping-table.md
    /// in buildingSMART/IDS.
    /// </para>
    /// </summary>
    public static class Ifc2x3EntityMapping
    {
        public readonly struct Ifc2x3TypeMapping
        {
            public Ifc2x3TypeMapping(string occurrenceEntity, string typeEntity)
            {
                OccurrenceEntity = occurrenceEntity;
                TypeEntity = typeEntity;
            }

            /// <summary>Upper case name of the generic occurrence class, e.g. IFCFLOWTERMINAL.</summary>
            public string OccurrenceEntity { get; }

            /// <summary>Upper case name of the type class that makes it specific, e.g. IFCAIRTERMINALTYPE.</summary>
            public string TypeEntity { get; }
        }

        private static readonly Dictionary<string, Ifc2x3TypeMapping> Mappings =
            new Dictionary<string, Ifc2x3TypeMapping>
            {
            ["IFCFURNITURE"] = new Ifc2x3TypeMapping("IFCFURNISHINGELEMENT", "IFCFURNITURETYPE"),
            ["IFCSYSTEMFURNITUREELEMENT"] = new Ifc2x3TypeMapping("IFCFURNISHINGELEMENT", "IFCSYSTEMFURNITUREELEMENTTYPE"),
            ["IFCACTUATOR"] = new Ifc2x3TypeMapping("IFCDISTRIBUTIONCONTROLELEMENT", "IFCACTUATORTYPE"),
            ["IFCALARM"] = new Ifc2x3TypeMapping("IFCDISTRIBUTIONCONTROLELEMENT", "IFCALARMTYPE"),
            ["IFCCONTROLLER"] = new Ifc2x3TypeMapping("IFCDISTRIBUTIONCONTROLELEMENT", "IFCCONTROLLERTYPE"),
            ["IFCFLOWINSTRUMENT"] = new Ifc2x3TypeMapping("IFCDISTRIBUTIONCONTROLELEMENT", "IFCFLOWINSTRUMENTTYPE"),
            ["IFCSENSOR"] = new Ifc2x3TypeMapping("IFCDISTRIBUTIONCONTROLELEMENT", "IFCSENSORTYPE"),
            ["IFCAIRTOAIRHEATRECOVERY"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCAIRTOAIRHEATRECOVERYTYPE"),
            ["IFCBOILER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCBOILERTYPE"),
            ["IFCCHILLER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCCHILLERTYPE"),
            ["IFCCOIL"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCCOILTYPE"),
            ["IFCCONDENSER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCCONDENSERTYPE"),
            ["IFCCOOLEDBEAM"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCCOOLEDBEAMTYPE"),
            ["IFCCOOLINGTOWER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCCOOLINGTOWERTYPE"),
            ["IFCELECTRICGENERATOR"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCELECTRICGENERATORTYPE"),
            ["IFCELECTRICMOTOR"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCELECTRICMOTORTYPE"),
            ["IFCEVAPORATIVECOOLER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCEVAPORATIVECOOLERTYPE"),
            ["IFCEVAPORATOR"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCEVAPORATORTYPE"),
            ["IFCHEATEXCHANGER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCHEATEXCHANGERTYPE"),
            ["IFCHUMIDIFIER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCHUMIDIFIERTYPE"),
            ["IFCMOTORCONNECTION"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCMOTORCONNECTIONTYPE"),
            ["IFCSPACEHEATER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCSPACEHEATERTYPE"),
            ["IFCTRANSFORMER"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCTRANSFORMERTYPE"),
            ["IFCTUBEBUNDLE"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCTUBEBUNDLETYPE"),
            ["IFCUNITARYEQUIPMENT"] = new Ifc2x3TypeMapping("IFCENERGYCONVERSIONDEVICE", "IFCUNITARYEQUIPMENTTYPE"),
            ["IFCAIRTERMINALBOX"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCAIRTERMINALBOXTYPE"),
            ["IFCDAMPER"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCDAMPERTYPE"),
            ["IFCELECTRICTIMECONTROL"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCELECTRICTIMECONTROLTYPE"),
            ["IFCFLOWMETER"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCFLOWMETERTYPE"),
            ["IFCPROTECTIVEDEVICE"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCPROTECTIVEDEVICETYPE"),
            ["IFCSWITCHINGDEVICE"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCSWITCHINGDEVICETYPE"),
            ["IFCVALVE"] = new Ifc2x3TypeMapping("IFCFLOWCONTROLLER", "IFCVALVETYPE"),
            ["IFCCABLECARRIERFITTING"] = new Ifc2x3TypeMapping("IFCFLOWFITTING", "IFCCABLECARRIERFITTINGTYPE"),
            ["IFCDUCTFITTING"] = new Ifc2x3TypeMapping("IFCFLOWFITTING", "IFCDUCTFITTINGTYPE"),
            ["IFCJUNCTIONBOX"] = new Ifc2x3TypeMapping("IFCFLOWFITTING", "IFCJUNCTIONBOXTYPE"),
            ["IFCPIPEFITTING"] = new Ifc2x3TypeMapping("IFCFLOWFITTING", "IFCPIPEFITTINGTYPE"),
            ["IFCCOMPRESSOR"] = new Ifc2x3TypeMapping("IFCFLOWMOVINGDEVICE", "IFCCOMPRESSORTYPE"),
            ["IFCFAN"] = new Ifc2x3TypeMapping("IFCFLOWMOVINGDEVICE", "IFCFANTYPE"),
            ["IFCPUMP"] = new Ifc2x3TypeMapping("IFCFLOWMOVINGDEVICE", "IFCPUMPTYPE"),
            ["IFCCABLECARRIERSEGMENT"] = new Ifc2x3TypeMapping("IFCFLOWSEGMENT", "IFCCABLECARRIERSEGMENTTYPE"),
            ["IFCCABLESEGMENT"] = new Ifc2x3TypeMapping("IFCFLOWSEGMENT", "IFCCABLESEGMENTTYPE"),
            ["IFCDUCTSEGMENT"] = new Ifc2x3TypeMapping("IFCFLOWSEGMENT", "IFCDUCTSEGMENTTYPE"),
            ["IFCPIPESEGMENT"] = new Ifc2x3TypeMapping("IFCFLOWSEGMENT", "IFCPIPESEGMENTTYPE"),
            ["IFCELECTRICFLOWSTORAGEDEVICE"] = new Ifc2x3TypeMapping("IFCFLOWSTORAGEDEVICE", "IFCELECTRICFLOWSTORAGEDEVICETYPE"),
            ["IFCTANK"] = new Ifc2x3TypeMapping("IFCFLOWSTORAGEDEVICE", "IFCTANKTYPE"),
            ["IFCAIRTERMINAL"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCAIRTERMINALTYPE"),
            ["IFCELECTRICAPPLIANCE"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCELECTRICAPPLIANCETYPE"),
            ["IFCFIRESUPPRESSIONTERMINAL"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCFIRESUPPRESSIONTERMINALTYPE"),
            ["IFCLAMP"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCLAMPTYPE"),
            ["IFCLIGHTFIXTURE"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCLIGHTFIXTURETYPE"),
            ["IFCOUTLET"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCOUTLETTYPE"),
            ["IFCSANITARYTERMINAL"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCSANITARYTERMINALTYPE"),
            ["IFCSTACKTERMINAL"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCSTACKTERMINALTYPE"),
            ["IFCWASTETERMINAL"] = new Ifc2x3TypeMapping("IFCFLOWTERMINAL", "IFCWASTETERMINALTYPE"),
            ["IFCDUCTSILENCER"] = new Ifc2x3TypeMapping("IFCFLOWTREATMENTDEVICE", "IFCDUCTSILENCERTYPE"),
            ["IFCFILTER"] = new Ifc2x3TypeMapping("IFCFLOWTREATMENTDEVICE", "IFCFILTERTYPE"),
            ["IFCVIBRATIONISOLATOR"] = new Ifc2x3TypeMapping("IFCEQUIPMENTELEMENT", "IFCVIBRATIONISOLATORTYPE"),
            };

        /// <summary>
        /// True when the IDS name is one of the IFC4 style names that IFC2X3 expresses as an
        /// occurrence plus type pair.
        /// </summary>
        public static bool TryGet(string idsEntityName, out Ifc2x3TypeMapping mapping)
        {
            mapping = default;
            return idsEntityName != null && Mappings.TryGetValue(idsEntityName.ToUpperInvariant(), out mapping);
        }

        /// <summary>Every IDS name that needs resolving through the table.</summary>
        public static IEnumerable<string> MappedNames => Mappings.Keys;
    }
}
