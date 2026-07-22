using System.Windows.Media.Imaging;

namespace OpenCvStudy.Services
{
    public interface IImageSourceService
    {
        BitmapSource LoadFromFile(string path);

        BitmapSource LoadFromBytes(byte[] bytes);
    }
}
