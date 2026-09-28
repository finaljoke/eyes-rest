using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace EyesRest;

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var backgroundBrush = new LinearGradientBrush(
            new Rectangle(0, 0, 32, 32),
            Color.FromArgb(45, 135, 132),
            Color.FromArgb(45, 87, 150),
            45F);
        graphics.FillEllipse(backgroundBrush, 2, 2, 28, 28);

        using var eyeBrush = new SolidBrush(Color.White);
        using var pupilBrush = new SolidBrush(Color.FromArgb(20, 36, 45));
        using var highlightBrush = new SolidBrush(Color.FromArgb(116, 223, 185));
        using var outlinePen = new Pen(Color.White, 2F);

        graphics.DrawArc(outlinePen, 7, 10, 18, 12, 195, 150);
        graphics.DrawArc(outlinePen, 7, 10, 18, 12, 15, 150);
        graphics.FillEllipse(eyeBrush, 10, 12, 12, 8);
        graphics.FillEllipse(pupilBrush, 13, 13, 6, 6);
        graphics.FillEllipse(highlightBrush, 15, 14, 2, 2);

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
