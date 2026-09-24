using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LegendLauncher.App.Services;

internal sealed class ProfileAvatarStore
{
    internal const int MaximumDimension = 512;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    private readonly string _directory;

    public ProfileAvatarStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    public static bool IsSupportedExtension(string path) =>
        !string.IsNullOrWhiteSpace(path) && SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task<string> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected profile image was not found.", fullPath);
        }

        if (!IsSupportedExtension(fullPath))
        {
            throw new InvalidDataException("Only PNG, JPEG, and WebP profile images are supported.");
        }

        string fileName = $"{Guid.NewGuid():N}.png";
        string destinationPath = Path.Combine(_directory, fileName);
        string temporaryPath = destinationPath + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapSource source = await Task.Run(
                () => DecodeSource(fullPath),
                cancellationToken).ConfigureAwait(false);

            double scale = Math.Min(
                1.0,
                MaximumDimension / (double)Math.Max(source.PixelWidth, source.PixelHeight));
            BitmapSource resized = source;
            if (scale < 1.0)
            {
                int targetWidth = Math.Max(
                    1,
                    (int)Math.Floor(source.PixelWidth * scale));
                int targetHeight = Math.Max(
                    1,
                    (int)Math.Floor(source.PixelHeight * scale));
                var transformed = new TransformedBitmap(
                    source,
                    new ScaleTransform(scale, scale));
                resized = transformed.PixelWidth == targetWidth &&
                          transformed.PixelHeight == targetHeight
                    ? transformed
                    : transformed.PixelWidth >= targetWidth &&
                      transformed.PixelHeight >= targetHeight
                        ? new CroppedBitmap(
                            transformed,
                            new Int32Rect(0, 0, targetWidth, targetHeight))
                        : transformed;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(resized));
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous))
            {
                encoder.Save(stream);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            return fileName;
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or
            NotSupportedException or
            ArgumentException or
            InvalidDataException or
            IOException)
        {
            TryDelete(temporaryPath);
            TryDelete(destinationPath);
            throw;
        }
    }

    public ImageSource? Load(string? fileName)
    {
        if (!TryGetPath(fileName, out string path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = MaximumDimension;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            COMException or
            ArgumentException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string? fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetPath(fileName, out string path))
        {
            TryDelete(path);
        }

        return Task.CompletedTask;
    }

    private static BitmapSource DecodeSource(string path)
    {
        BitmapDecoder decoder = BitmapDecoder.Create(
            new Uri(path, UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
        {
            throw new InvalidDataException("The selected profile image has no usable pixels.");
        }

        return frame;
    }

    private bool TryGetPath(string? fileName, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(_directory, fileName));
        if (!candidate.StartsWith(
                _directory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
