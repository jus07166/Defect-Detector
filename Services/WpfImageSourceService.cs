using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace OpenCvStudy.Services
{
    public sealed class WpfImageSourceService : IImageSourceService
    {
        public BitmapSource LoadFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public BitmapSource LoadFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("이미지 데이터가 비어 있습니다.", nameof(bytes));
            }

            var bitmap = new BitmapImage();

            using (var stream = new MemoryStream(bytes, false))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }

            bitmap.Freeze();
            return bitmap;
        }
    }
}
