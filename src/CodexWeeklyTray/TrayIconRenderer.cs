using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexWeeklyTray
{
    internal static class TrayIconRenderer
    {
        private const int CanvasSize = 64;

        public static Icon Render(UsageSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return RenderUnavailable();
            }

            return RenderPie(snapshot.RemainingPercent, GetAccentColor(snapshot.RemainingPercent));
        }

        public static Icon RenderUnavailable()
        {
            using (Bitmap bitmap = CreateBitmap())
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                ConfigureGraphics(graphics);
                RectangleF bounds = new RectangleF(5.0f, 5.0f, 54.0f, 54.0f);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(145, 145, 145)))
                using (Pen slash = new Pen(Color.FromArgb(238, 238, 238), 6.0f))
                {
                    slash.StartCap = LineCap.Round;
                    slash.EndCap = LineCap.Round;
                    graphics.FillEllipse(brush, bounds);
                    graphics.DrawLine(slash, 20.0f, 44.0f, 44.0f, 20.0f);
                }
                return CreateIcon(bitmap);
            }
        }

        private static Icon RenderPie(double remainingPercent, Color accent)
        {
            using (Bitmap bitmap = CreateBitmap())
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                ConfigureGraphics(graphics);
                RectangleF bounds = new RectangleF(5.0f, 5.0f, 54.0f, 54.0f);

                using (SolidBrush remaining = new SolidBrush(accent))
                {
                    graphics.FillEllipse(remaining, bounds);
                }

                double usedPercent = 100.0 - remainingPercent;
                if (usedPercent >= 99.95)
                {
                    using (SolidBrush used = new SolidBrush(Color.FromArgb(70, 73, 78)))
                    {
                        graphics.FillEllipse(used, bounds);
                    }
                }
                else if (usedPercent > 0.05)
                {
                    using (SolidBrush used = new SolidBrush(Color.FromArgb(70, 73, 78)))
                    {
                        graphics.FillPie(
                            used,
                            Rectangle.Round(bounds),
                            -90.0f,
                            (float)(usedPercent * 3.6));
                    }
                }

                using (Pen outline = new Pen(Color.FromArgb(210, 230, 230, 230), 1.5f))
                {
                    graphics.DrawEllipse(outline, bounds);
                }

                return CreateIcon(bitmap);
            }
        }

        private static Bitmap CreateBitmap()
        {
            return new Bitmap(CanvasSize, CanvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        private static void ConfigureGraphics(Graphics graphics)
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
        }

        private static Icon CreateIcon(Bitmap bitmap)
        {
            IntPtr iconHandle = bitmap.GetHicon();
            try
            {
                using (Icon temporary = Icon.FromHandle(iconHandle))
                {
                    return (Icon)temporary.Clone();
                }
            }
            finally
            {
                DestroyIcon(iconHandle);
            }
        }

        private static Color GetAccentColor(double remainingPercent)
        {
            if (remainingPercent <= 20.0)
            {
                return Color.FromArgb(255, 86, 92);
            }
            if (remainingPercent <= 50.0)
            {
                return Color.FromArgb(255, 193, 7);
            }
            return Color.FromArgb(55, 214, 122);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
