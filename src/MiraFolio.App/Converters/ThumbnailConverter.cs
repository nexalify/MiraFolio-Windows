using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MiraFolio.App.Converters;

/// <summary>
/// Converts a file path to a BitmapImage decoded at thumbnail size,
/// preventing full-resolution wallpapers from being loaded into memory.
/// </summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public sealed class ThumbnailConverter : IValueConverter
{
    /// <summary>Decode width in pixels. Matches the display size of the preview card.</summary>
    public int DecodePixelWidth { get; set; } = 300;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource         = new Uri(path);
            bitmap.DecodePixelWidth  = DecodePixelWidth;   // decode at display size, not original
            bitmap.CacheOption       = BitmapCacheOption.OnLoad;  // release file handle immediately
            bitmap.CreateOptions     = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();
            bitmap.Freeze();           // make thread-safe and prevent further allocations
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
