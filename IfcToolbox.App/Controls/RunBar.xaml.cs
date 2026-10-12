using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;

namespace IfcToolbox.App.Controls
{
    public sealed partial class RunBar : UserControl
    {
        private CancellationTokenSource _cancellation;
        private bool _canRun;

        public RunBar()
        {
            InitializeComponent();
        }

        /// <summary>Label of the run button, e.g. "Split IFC".</summary>
        public string ActionText
        {
            get => ActionButton.Content as string;
            set
            {
                ActionButton.Content = value;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ActionButton, value);
            }
        }

        /// <summary>Raised when the run button is pressed; the page starts its work with <see cref="Begin"/>.</summary>
        public event EventHandler Run;

        public bool IsBusy => _cancellation != null;

        /// <summary>The hint or outcome shown left of the button.</summary>
        public string Status
        {
            get => StatusText.Text;
            set => StatusText.Text = value ?? string.Empty;
        }

        /// <summary>Whether the page has everything it needs; ignored while busy.</summary>
        public bool CanRun
        {
            get => _canRun;
            set
            {
                _canRun = value;
                ActionButton.IsEnabled = value && !IsBusy;
            }
        }

        /// <summary>Shows the work as started and hands out its cancellation token.</summary>
        public CancellationToken Begin(string status, bool cancellable = true)
        {
            _cancellation = new CancellationTokenSource();
            ErrorBar.IsOpen = false;
            Status = status;
            Progress.Visibility = Visibility.Visible;
            CancelButton.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
            ActionButton.IsEnabled = false;
            return _cancellation.Token;
        }

        /// <summary>Back to idle, showing the outcome.</summary>
        public void End(string outcome)
        {
            _cancellation?.Dispose();
            _cancellation = null;
            Progress.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            ActionButton.IsEnabled = _canRun;
            Status = outcome;
        }

        public void ShowError(string title, string message)
        {
            ErrorBar.Title = title;
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        public void ClearError() => ErrorBar.IsOpen = false;

        private void OnRun(object sender, RoutedEventArgs e) => Run?.Invoke(this, EventArgs.Empty);

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            _cancellation?.Cancel();
            Status = "Cancelling…";
        }
    }
}
