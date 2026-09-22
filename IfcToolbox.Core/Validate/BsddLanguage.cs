using IfcToolbox.Core.Bsdd.Models;

namespace IfcToolbox.Core.Validate
{
    public class BsddLanguage
    {
        public BsddLanguage() { }
        public BsddLanguage(string displayName)
        {
            DisplayName = displayName;
        }

        /// <remarks>
        /// bSDD dropped the standalone Language endpoint together with the old
        /// Domain/Classification API; the languages a dictionary is translated into now come
        /// back as part of the dictionary itself (see BsddClient.GetDictionaryLanguagesAsync).
        /// The old contract called the two fields Name and IsoCode, they are now Name and Code.
        /// </remarks>
        public BsddLanguage(BsddLanguageInfo language)
        {
            Name = language.Name;
            IsoCode = language.Code;
            DisplayName = $"{language.Name} ({language.Code})";
        }

        public string Name { get; set; }
        public string IsoCode { get; set; }
        public string DisplayName { get; set; }
    }
}
