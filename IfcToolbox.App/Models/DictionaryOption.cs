using IfcToolbox.Core.Bsdd.Models;

namespace IfcToolbox.App.Models
{
    /// <summary>A bSDD dictionary as a list entry: "IFC 4.3 · buildingSMART International".</summary>
    public sealed class DictionaryOption
    {
        public DictionaryOption(BsddDictionary dictionary)
        {
            Dictionary = dictionary;
            Label = $"{dictionary.Name} {dictionary.Version}" +
                    (string.IsNullOrWhiteSpace(dictionary.OrganizationNameOwner) ? string.Empty : $" · {dictionary.OrganizationNameOwner}");
        }

        public BsddDictionary Dictionary { get; }
        public string Label { get; }
    }
}
