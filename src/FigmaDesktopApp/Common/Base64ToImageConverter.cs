using System.IO;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace FigmaDesktopApp.Common;

public sealed class Base64ToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not string base64 || string.IsNullOrWhiteSpace(base64))
            return null;

        try
        {
            if (base64.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                int comma = base64.IndexOf(',');
                if (comma >= 0) base64 = base64.Substring(comma + 1);
            }

            byte[] bytes = System.Convert.FromBase64String(base64.Trim());
            var image = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            }
            image.SetSource(stream);
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
