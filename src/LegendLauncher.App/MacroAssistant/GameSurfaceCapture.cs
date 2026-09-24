using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LegendLauncher.App.GameHosting;

namespace LegendLauncher.App.MacroAssistant;

internal sealed record CapturedSurface(SurfaceGeometry Geometry, byte[] JpegBytes);

internal sealed class GameSurfaceCapture
{
    private const uint PrintWindowRenderFullContent = 0x00000002;
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;

    public SurfaceGeometry GetGeometry(nint gameWindow)
    {
        NativeClientGeometry nativeGeometry = NativeWindowMethods.GetClientGeometry(gameWindow);
        return new SurfaceGeometry(
            nativeGeometry.X,
            nativeGeometry.Y,
            nativeGeometry.Width,
            nativeGeometry.Height);
    }

    public CapturedSurface Capture(nint gameWindow)
    {
        SurfaceGeometry geometry = GetGeometry(gameWindow);
        if (!geometry.HasArea)
        {
            throw new InvalidOperationException("A superfície da sessão não possui área visível.");
        }

        nint windowDc = GetDC(gameWindow);
        if (windowDc == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "A superfície do jogo não pôde ser capturada.");
        }

        nint memoryDc = nint.Zero;
        nint bitmap = nint.Zero;
        nint previousBitmap = nint.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(windowDc);
            if (memoryDc == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "O contexto de captura não pôde ser criado.");
            }

            bitmap = CreateCompatibleBitmap(windowDc, geometry.Width, geometry.Height);
            if (bitmap == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "O bitmap de captura não pôde ser criado.");
            }

            previousBitmap = SelectObject(memoryDc, bitmap);
            bool printed = PrintWindow(gameWindow, memoryDc, PrintWindowRenderFullContent);
            if (!printed)
            {
                printed = BitBlt(
                    memoryDc,
                    0,
                    0,
                    geometry.Width,
                    geometry.Height,
                    windowDc,
                    0,
                    0,
                    SourceCopy | CaptureBlt);
            }

            if (!printed)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "A sessão do jogo não respondeu à captura.");
            }

            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = 84 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return new CapturedSurface(geometry, stream.ToArray());
        }
        finally
        {
            if (memoryDc != nint.Zero && previousBitmap != nint.Zero)
            {
                _ = SelectObject(memoryDc, previousBitmap);
            }

            if (bitmap != nint.Zero)
            {
                _ = DeleteObject(bitmap);
            }

            if (memoryDc != nint.Zero)
            {
                _ = DeleteDC(memoryDc);
            }

            _ = ReleaseDC(gameWindow, windowDc);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseDC(nint windowHandle, nint dc);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint windowHandle, nint hdc, uint flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint SelectObject(nint dc, nint obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        nint destinationDc,
        int x,
        int y,
        int width,
        int height,
        nint sourceDc,
        int sourceX,
        int sourceY,
        uint rop);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);
}
