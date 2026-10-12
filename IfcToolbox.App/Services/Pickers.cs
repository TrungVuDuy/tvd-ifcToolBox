using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace IfcToolbox.App.Services
{
    /// <summary>File dialogs, tied to the main window as unpackaged apps must do by hand.</summary>
    public static class Pickers
    {
        /// <summary>Asks for one existing file; returns its path, or null when cancelled.</summary>
        public static async Task<string> OpenFileAsync(params string[] extensions)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            foreach (var extension in extensions)
                picker.FileTypeFilter.Add(extension);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

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
