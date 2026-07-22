using Microsoft.Win32;
using System.Windows;

namespace OpenCvStudy.Services
{
    public sealed class WpfDialogService : IDialogService
    {
        public string SelectImageFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
