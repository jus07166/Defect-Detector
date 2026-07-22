using OpenCvStudy.Services;
using OpenCvStudy.ViewModels;
using System.Windows;

namespace OpenCvStudy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var viewModel = new MainViewModel(
                new NativeInspectionService(),
                new WpfDialogService(),
                new WpfImageSourceService(),
                new WpfApplicationLifetime());

            var window = new MainWindow
            {
                DataContext = viewModel
            };

            window.Closed += (sender, args) => viewModel.Dispose();
            MainWindow = window;
            window.Show();
        }
    }
}
