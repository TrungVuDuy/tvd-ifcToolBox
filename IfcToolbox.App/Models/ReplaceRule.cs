using CommunityToolkit.Mvvm.ComponentModel;

namespace IfcToolbox.App.Models
{
    /// <summary>One row of the Anonymizer's replace rules: text to find in products of a class, and what replaces it.</summary>
    public sealed partial class ReplaceRule : ObservableObject
    {
        public ReplaceRule(string entityType, string keyword, string replacement = "Unknown")
        {
            EntityType = entityType;
            _keyword = keyword;
            _replacement = replacement;
        }

        public string EntityType { get; }

        [ObservableProperty]
        private string _keyword;

        [ObservableProperty]
        private string _replacement;
    }
}
