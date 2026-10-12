using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace IfcToolbox.App.Services
{
    /// <summary>File dialogs, tied to the main window as unpackaged apps must do by hand.</summary>
    public static class Pickers
    {
        /// <summary>Asks where to save; returns the chosen path, or null when cancelled.</summary>
        public static async Task<string> SaveFileAsync(string suggestedName, string typeLabel, string extension)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedName,
            };
            picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
    }
}
