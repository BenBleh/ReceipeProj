namespace Nibbles.Services;

public static class RecipePhotoProcessor
{
    public static async Task<byte[]> JpegAsync(byte[] bytes, bool thumbnail, int maxDimension = 0)
    {
#if WINDOWS
        using var input = new MemoryStream(bytes);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input.AsRandomAccessStream());
        var transform = new Windows.Graphics.Imaging.BitmapTransform();
        if (maxDimension > 0)
        {
            var size = ImportedSize(decoder.PixelWidth, decoder.PixelHeight, maxDimension);
            transform.ScaledWidth = (uint)size.Width;
            transform.ScaledHeight = (uint)size.Height;
        }
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
            transform,
            Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
            Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb);
        using var output = new MemoryStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, output.AsRandomAccessStream());
        encoder.SetSoftwareBitmap(bitmap);
        if (thumbnail) { encoder.BitmapTransform.ScaledWidth = 120; encoder.BitmapTransform.ScaledHeight = 120; }
        await encoder.FlushAsync();
        return output.ToArray();
#elif ANDROID
        using var options = new Android.Graphics.BitmapFactory.Options();
        if (maxDimension > 0)
        {
            options.InJustDecodeBounds = true;
            using var bounds = await Android.Graphics.BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length, options);
            ImportedSize(options.OutWidth, options.OutHeight, maxDimension);
            options.InJustDecodeBounds = false;
            options.InSampleSize = 1;
            while (Math.Max(options.OutWidth, options.OutHeight) / (options.InSampleSize * 2) >= maxDimension)
                options.InSampleSize *= 2;
        }
        using var bitmap = await Android.Graphics.BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length, options)
            ?? throw new ArgumentException("The selected photo could not be decoded.");
        using var output = new MemoryStream();
        if (thumbnail || maxDimension > 0)
        {
            var size = thumbnail ? (Width: 120, Height: 120) : ImportedSize(bitmap.Width, bitmap.Height, maxDimension);
            using var small = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, size.Width, size.Height, true)
                ?? throw new ArgumentException("The photo thumbnail could not be created.");
            if (!await small.CompressAsync(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, output))
                throw new IOException("The photo thumbnail could not be encoded.");
        }
        else if (!await bitmap.CompressAsync(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, output))
            throw new IOException("The photo could not be encoded.");
        return output.ToArray();
#elif IOS || MACCATALYST
        using var data = Foundation.NSData.FromArray(bytes);
        using var image = UIKit.UIImage.LoadFromData(data) ?? throw new ArgumentException("The selected photo could not be decoded.");
        var size = thumbnail ? new CoreGraphics.CGSize(120, 120) : image.Size;
        if (maxDimension > 0 && !thumbnail)
        {
            var imported = ImportedSize(image.Size.Width, image.Size.Height, maxDimension);
            size = new CoreGraphics.CGSize(imported.Width, imported.Height);
        }
        using var format = new UIKit.UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
        using var renderer = new UIKit.UIGraphicsImageRenderer(size, format);
        using var rendered = renderer.CreateImage(_ => image.Draw(new CoreGraphics.CGRect(CoreGraphics.CGPoint.Empty, size)));
        using var jpeg = rendered.AsJPEG(0.9f) ?? throw new IOException("The photo could not be encoded.");
        return await Task.FromResult(jpeg.ToArray());
#else
        throw new PlatformNotSupportedException("Photo encoding is not supported on this platform.");
#endif
    }

    private static (int Width, int Height) ImportedSize(double width, double height, int maximum)
    {
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || width * height > 16_000_000)
            throw new ArgumentException("The source photo has unsupported dimensions (maximum 16 megapixels).");
        var scale = Math.Min(1, maximum / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
