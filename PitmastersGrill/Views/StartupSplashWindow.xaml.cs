using PitmastersGrill.Models;
using System.Windows;

namespace PitmastersGrill.Views
{
    public partial class StartupSplashWindow : Window
    {
        public StartupSplashWindow()
        {
            InitializeComponent();
        }

        public void ApplyState(StartupUpdateState state)
        {
            StatusTextBlock.Text = state.StatusText;
            DetailTextBlock.Text = state.DetailText;
            ProgressBarControl.IsIndeterminate = state.IsIndeterminate;

            if (!state.IsIndeterminate)
            {
                ProgressBarControl.Value = state.ProgressValue;
            }

            ExceptionTextBlock.Visibility = state.IsExceptionMessage
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}