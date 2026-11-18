using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PCTweaker.UI
{
    /// <summary>
    /// Tactical Hardware Telemetry Design System & GDI+ Drawing Primitives.
    /// Implements Section 4.1 of implementation_plan.md.
    /// </summary>
    public static class Theme
    {
        // =========================================================================
        // CORE SURFACE & BORDER PALETTE TOKENS
        // =========================================================================
        public static readonly Color BgCanvas = ColorTranslator.FromHtml("#0A0D14");
        public static readonly Color BgSidebar = ColorTranslator.FromHtml("#0F131D");
        public static readonly Color BgHeader = ColorTranslator.FromHtml("#0D111A");
        public static readonly Color BgCard = ColorTranslator.FromHtml("#151B28");
        public static readonly Color BgCardHover = ColorTranslator.FromHtml("#1C2436");
        public static readonly Color BgCardElevated = ColorTranslator.FromHtml("#1A2233");
        public static readonly Color BgRecessed = ColorTranslator.FromHtml("#0B0F18");
        public static readonly Color BgInput = ColorTranslator.FromHtml("#101521");

        public static readonly Color BorderSubtle = ColorTranslator.FromHtml("#232C40");
        public static readonly Color BorderHover = ColorTranslator.FromHtml("#33415C");
        public static readonly Color BorderActive = ColorTranslator.FromHtml("#00E5FF");

        // =========================================================================
        // TYPOGRAPHY COLORS
        // =========================================================================
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#F3F6FC");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#9BA6BC");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#637089");

        // =========================================================================
        // SEMANTIC ACCENTS
        // =========================================================================
        public static readonly Color AccentPrimary = ColorTranslator.FromHtml("#00E5FF");
        public static readonly Color AccentPrimaryDark = ColorTranslator.FromHtml("#00A3B8");
        public static readonly Color AccentSafe = ColorTranslator.FromHtml("#10B981");
        public static readonly Color AccentWarning = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color AccentDanger = ColorTranslator.FromHtml("#EF4444");
        public static readonly Color AccentIndigo = ColorTranslator.FromHtml("#6366F1");

        // =========================================================================
        // TYPOGRAPHY SCALE (CACHED FOR ZERO ALLOCATION IN PAINT LOOPS)
        // =========================================================================
        public static readonly Font FontBrand = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
        public static readonly Font FontBrandSub = new Font("Segoe UI", 8f, FontStyle.Regular);
        public static readonly Font FontPageTitle = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold);
        public static readonly Font FontSectionTitle = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        public static readonly Font FontCardTitle = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        public static readonly Font FontSmallBold = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold);
        public static readonly Font FontBadge = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
        public static readonly Font FontNav = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);

        public static readonly Font FontMono = new Font("Consolas", 9f, FontStyle.Regular);
        public static readonly Font FontMonoBold = new Font("Consolas", 9.5f, FontStyle.Bold);
        public static readonly Font FontMonoSmall = new Font("Consolas", 8.25f, FontStyle.Regular);
        public static readonly Font FontMetricValue = new Font("Consolas", 13f, FontStyle.Bold);
        public static readonly Font FontGaugeBig = new Font("Segoe UI", 26f, FontStyle.Bold);
        public static readonly Font FontHeroButton = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);

        // =========================================================================
        // WINDOWS 11 NATIVE DARK CHROME & SCROLLBAR INTEROP (UI LAYER ONLY)
        // =========================================================================
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateAppIcon()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush bg = new SolidBrush(BgSidebar))
                        FillRoundedRect(g, bg, new RectangleF(1, 1, 30, 30), 7f);
                    DrawVectorIcon(g, "logo", new RectangleF(4, 4, 24, 24), AccentPrimary);
                    IntPtr hIcon = bmp.GetHicon();
                    using (Icon temp = Icon.FromHandle(hIcon))
                    {
                        Icon cloned = (Icon)temp.Clone();
                        DestroyIcon(hIcon);
                        return cloned;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static void ApplyDarkTitleBar(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                int useDark = 1;
                DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE

                // Match caption bar color to BgSidebar (#0F131D -> COLORREF BGR 0x001D130F)
                int captionColor = (BgSidebar.B << 16) | (BgSidebar.G << 8) | BgSidebar.R;
                DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int)); // DWMWA_CAPTION_COLOR

                int borderColor = (BorderSubtle.B << 16) | (BorderSubtle.G << 8) | BorderSubtle.R;
                DwmSetWindowAttribute(handle, 34, ref borderColor, sizeof(int)); // DWMWA_BORDER_COLOR
            }
            catch { }
        }

        public static void ApplyDarkScrollbars(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                SetWindowTheme(handle, "DarkMode_Explorer", null);
            }
            catch { }
        }

        // =========================================================================
        // COLOR & GEOMETRY HELPERS
        // =========================================================================
        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(ClampByte(alpha), c.R, c.G, c.B);
        }

        public static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                ClampByte((int)(a.A + (b.A - a.A) * t)),
                ClampByte((int)(a.R + (b.R - a.R) * t)),
                ClampByte((int)(a.G + (b.G - a.G) * t)),
                ClampByte((int)(a.B + (b.B - a.B) * t)));
        }

        private static int ClampByte(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }

        public static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            float d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
            if (d <= 0.5f)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (GraphicsPath path = CreateRoundedRectPath(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, RectangleF rect, float radius)
        {
            using (GraphicsPath path = CreateRoundedRectPath(rect, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        public static float MeasurePillWidth(Graphics g, string text, Font font, int horizPad = 14)
        {
            SizeF sz = g.MeasureString(text, font);
            return (float)Math.Ceiling(sz.Width) + horizPad;
        }

        public static float DrawPillBadge(
            Graphics g,
            string text,
            Font font,
            Color fillColor,
            Color borderColor,
            Color textColor,
            float x,
            float y,
            float height = 20f,
            int horizPad = 14)
        {
            float w = MeasurePillWidth(g, text, font, horizPad);
            RectangleF rect = new RectangleF(x, y, w, height);
            using (SolidBrush br = new SolidBrush(fillColor))
            {
                FillRoundedRect(g, br, rect, height / 2f);
            }
            using (Pen p = new Pen(borderColor, 1f))
            {
                DrawRoundedRect(g, p, rect, height / 2f);
            }
            using (SolidBrush tb = new SolidBrush(textColor))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, font, tb, new RectangleF(rect.X, rect.Y + 0.5f, rect.Width, rect.Height), sf);
            }
            return w;
        }

        // =========================================================================
        // CRISP GEOMETRIC VECTOR ICON PAINTER (ZERO EXTERNAL BITMAP DEPENDENCIES)
        // =========================================================================
        public static void DrawVectorIcon(Graphics g, string iconKey, RectangleF r, Color color)
        {
            SmoothingMode prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(color, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (SolidBrush brush = new SolidBrush(color))
            {
                float x = r.X;
                float y = r.Y;
                float w = r.Width;
                float h = r.Height;

                switch (iconKey)
                {
                    case "logo":
                        // Tactical hexagonal chassis + pulse line
                        PointF[] hex = new PointF[]
                        {
                            new PointF(x + w * 0.50f, y + h * 0.05f),
                            new PointF(x + w * 0.92f, y + h * 0.28f),
                            new PointF(x + w * 0.92f, y + h * 0.72f),
                            new PointF(x + w * 0.50f, y + h * 0.95f),
                            new PointF(x + w * 0.08f, y + h * 0.72f),
                            new PointF(x + w * 0.08f, y + h * 0.28f)
                        };
                        using (SolidBrush bgBr = new SolidBrush(Alpha(color, 26)))
                            g.FillPolygon(bgBr, hex);
                        g.DrawPolygon(pen, hex);
                        PointF[] pulse = new PointF[]
                        {
                            new PointF(x + w * 0.22f, y + h * 0.54f),
                            new PointF(x + w * 0.37f, y + h * 0.54f),
                            new PointF(x + w * 0.46f, y + h * 0.30f),
                            new PointF(x + w * 0.56f, y + h * 0.70f),
                            new PointF(x + w * 0.65f, y + h * 0.46f),
                            new PointF(x + w * 0.78f, y + h * 0.46f)
                        };
                        g.DrawLines(pen, pulse);
                        break;

                    case "dashboard":
                        float gap = w * 0.14f;
                        float cell = (w - gap) / 2f;
                        DrawRoundedRect(g, pen, new RectangleF(x, y, cell, cell * 1.15f), 2.5f);
                        DrawRoundedRect(g, pen, new RectangleF(x + cell + gap, y, cell, cell * 0.78f), 2.5f);
                        DrawRoundedRect(g, pen, new RectangleF(x, y + cell * 1.15f + gap, cell, h - cell * 1.15f - gap), 2.5f);
                        DrawRoundedRect(g, pen, new RectangleF(x + cell + gap, y + cell * 0.78f + gap, cell, h - cell * 0.78f - gap), 2.5f);
                        break;

                    case "tweaks":
                        // 3 horizontal tactical sliders
                        float[] ys = new float[] { y + h * 0.22f, y + h * 0.50f, y + h * 0.78f };
                        float[] knobs = new float[] { x + w * 0.35f, x + w * 0.68f, x + w * 0.45f };
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawLine(pen, x + 1, ys[i], x + w - 1, ys[i]);
                            g.FillEllipse(brush, knobs[i] - 2.6f, ys[i] - 2.6f, 5.2f, 5.2f);
                        }
                        break;

                    case "booster":
                        // Lightning bolt
                        PointF[] bolt = new PointF[]
                        {
                            new PointF(x + w * 0.58f, y + h * 0.06f),
                            new PointF(x + w * 0.18f, y + h * 0.54f),
                            new PointF(x + w * 0.48f, y + h * 0.54f),
                            new PointF(x + w * 0.40f, y + h * 0.94f),
                            new PointF(x + w * 0.82f, y + h * 0.44f),
                            new PointF(x + w * 0.52f, y + h * 0.44f)
                        };
                        g.DrawPolygon(pen, bolt);
                        break;

                    case "games":
                        // Precision crosshair target
                        g.DrawEllipse(pen, x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f);
                        g.DrawLine(pen, x + w * 0.50f, y + 1, x + w * 0.50f, y + h * 0.30f);
                        g.DrawLine(pen, x + w * 0.50f, y + h * 0.70f, x + w * 0.50f, y + h - 1);
                        g.DrawLine(pen, x + 1, y + h * 0.50f, x + w * 0.30f, y + h * 0.50f);
                        g.DrawLine(pen, x + w * 0.70f, y + h * 0.50f, x + w - 1, y + h * 0.50f);
                        g.FillEllipse(brush, x + w * 0.43f, y + h * 0.43f, w * 0.14f, h * 0.14f);
                        break;

                    case "vault":
                    case "shield":
                        PointF[] shield = new PointF[]
                        {
                            new PointF(x + w * 0.50f, y + h * 0.06f),
                            new PointF(x + w * 0.88f, y + h * 0.22f),
                            new PointF(x + w * 0.84f, y + h * 0.62f),
                            new PointF(x + w * 0.50f, y + h * 0.94f),
                            new PointF(x + w * 0.16f, y + h * 0.62f),
                            new PointF(x + w * 0.12f, y + h * 0.22f)
                        };
                        g.DrawPolygon(pen, shield);
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(x + w * 0.34f, y + h * 0.50f),
                            new PointF(x + w * 0.46f, y + h * 0.62f),
                            new PointF(x + w * 0.68f, y + h * 0.38f)
                        });
                        break;

                    case "audit":
                        // Hardware CPU microchip
                        DrawRoundedRect(g, pen, new RectangleF(x + w * 0.22f, y + h * 0.22f, w * 0.56f, h * 0.56f), 2.5f);
                        g.FillRectangle(brush, x + w * 0.38f, y + h * 0.38f, w * 0.24f, h * 0.24f);
                        g.DrawLine(pen, x + w * 0.36f, y + 1, x + w * 0.36f, y + h * 0.22f);
                        g.DrawLine(pen, x + w * 0.64f, y + 1, x + w * 0.64f, y + h * 0.22f);
                        g.DrawLine(pen, x + w * 0.36f, y + h * 0.78f, x + w * 0.36f, y + h - 1);
                        g.DrawLine(pen, x + w * 0.64f, y + h * 0.78f, x + w * 0.64f, y + h - 1);
                        g.DrawLine(pen, x + 1, y + h * 0.36f, x + w * 0.22f, y + h * 0.36f);
                        g.DrawLine(pen, x + 1, y + h * 0.64f, x + w * 0.22f, y + h * 0.64f);
                        g.DrawLine(pen, x + w * 0.78f, y + h * 0.36f, x + w - 1, y + h * 0.36f);
                        g.DrawLine(pen, x + w * 0.78f, y + h * 0.64f, x + w - 1, y + h * 0.64f);
                        break;

                    case "ram":
                        DrawRoundedRect(g, pen, new RectangleF(x + 1, y + h * 0.24f, w - 2, h * 0.52f), 2f);
                        g.DrawLine(pen, x + w * 0.30f, y + h * 0.76f, x + w * 0.30f, y + h * 0.92f);
                        g.DrawLine(pen, x + w * 0.50f, y + h * 0.76f, x + w * 0.50f, y + h * 0.92f);
                        g.DrawLine(pen, x + w * 0.70f, y + h * 0.76f, x + w * 0.70f, y + h * 0.92f);
                        g.FillRectangle(brush, x + w * 0.24f, y + h * 0.38f, w * 0.16f, h * 0.22f);
                        g.FillRectangle(brush, x + w * 0.60f, y + h * 0.38f, w * 0.16f, h * 0.22f);
                        break;

                    case "search":
                        g.DrawEllipse(pen, x + w * 0.12f, y + h * 0.12f, w * 0.56f, h * 0.56f);
                        g.DrawLine(pen, x + w * 0.60f, y + h * 0.60f, x + w * 0.88f, y + h * 0.88f);
                        break;

                    case "refresh":
                        g.DrawArc(pen, x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f, 35, 290);
                        g.DrawLine(pen, x + w * 0.76f, y + h * 0.16f, x + w * 0.86f, y + h * 0.34f);
                        g.DrawLine(pen, x + w * 0.64f, y + h * 0.34f, x + w * 0.86f, y + h * 0.34f);
                        break;
                }
            }

            g.SmoothingMode = prev;
        }
    }
}
