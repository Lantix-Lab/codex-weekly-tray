using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexWeeklyTray
{
    internal static class TrayIconRenderer
    {
        private const int CanvasSize = 64;
        private static readonly Color WeeklyOutlineColor = Color.FromArgb(66, 135, 245);
        private static readonly Color FiveHourOutlineColor = Color.FromArgb(176, 92, 255);

        public static Icon RenderWeekly(UsageSnapshot snapshot)
        {
            return Render(snapshot, WeeklyOutlineColor);
        }

        public static Icon RenderFiveHour(UsageSnapshot snapshot)
        {
            return Render(snapshot, FiveHourOutlineColor);
        }

        public static Icon RenderWeeklyUnavailable()
        {
            return RenderUnavailable(WeeklyOutlineColor);
        }

        public static Icon RenderFiveHourUnavailable()
        {
            return RenderUnavailable(FiveHourOutlineColor);
        }

        private static Icon Render(UsageSnapshot snapshot, Color outlineColor)
        {
            if (snapshot == null)
            {
                return RenderUnavailable(outlineColor);
            }

            return RenderPie(
                snapshot.RemainingPercent,
                GetAccentColor(snapshot.RemainingPercent),
                outlineColor);
        }

        private static Icon RenderUnavailable(Color outlineColor)
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
                DrawOutline(graphics, bounds, outlineColor);
                return CreateIcon(bitmap);
            }
        }

        private static Icon RenderPie(double remainingPercent, Color accent, Color outlineColor)
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

                DrawOutline(graphics, bounds, outlineColor);

                return CreateIcon(bitmap);
            }
        }

        private static void DrawOutline(Graphics graphics, RectangleF bounds, Color outlineColor)
        {
            using (Pen outline = new Pen(outlineColor, 5.0f))
            {
                outline.Alignment = PenAlignment.Inset;
                graphics.DrawEllipse(outline, bounds);
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
