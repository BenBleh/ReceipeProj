namespace Nibbles.Services;

public static class RecipePhotoProcessor
{
    public static async Task<byte[]> JpegAsync(byte[] bytes, bool thumbnail)
    {
#if WINDOWS
        using var input = new MemoryStream(bytes);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input.AsRandomAccessStream());
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore,
            new Windows.Graphics.Imaging.BitmapTransform(),
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
        using var bitmap = await Android.Graphics.BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length)
            ?? throw new ArgumentException("The selected photo could not be decoded.");
        using var output = new MemoryStream();
        if (thumbnail)
        {
            using var small = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, 120, 120, true)
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
        using var format = new UIKit.UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
        using var renderer = new UIKit.UIGraphicsImageRenderer(size, format);
        using var rendered = renderer.CreateImage(_ => image.Draw(new CoreGraphics.CGRect(CoreGraphics.CGPoint.Empty, size)));
        using var jpeg = rendered.AsJPEG(0.9f) ?? throw new IOException("The photo could not be encoded.");
        return await Task.FromResult(jpeg.ToArray());
#else
        throw new PlatformNotSupportedException("Photo encoding is not supported on this platform.");
#endif
    }
}
