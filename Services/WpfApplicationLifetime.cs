using System.Windows;

namespace OpenCvStudy.Services
{
    public sealed class WpfApplicationLifetime : IApplicationLifetime
    {
        public void Shutdown()
        {
            Application.Current.Shutdown();
        }
    }
}
