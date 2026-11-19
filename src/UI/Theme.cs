using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PCTweaker.UI
{
    /// <summary>
    /// SkyTweaks Atmospheric Sky & Frosted Slate Design System.
    /// Modern, friendly, high-trust Windows 11 consumer utility aesthetic.
    /// </summary>
    public static class Theme
    {
        // =========================================================================
        // CORE SURFACE & BORDER PALETTE TOKENS (SKY & FROSTED SLATE)
        // =========================================================================
        public static readonly Color BgCanvas = ColorTranslator.FromHtml("#0F172A");
        public static readonly Color BgSidebar = ColorTranslator.FromHtml("#131E33");
        public static readonly Color BgSidebarTop = ColorTranslator.FromHtml("#182949");
        public static readonly Color BgHeader = ColorTranslator.FromHtml("#152238");
        public static readonly Color BgCard = ColorTranslator.FromHtml("#1E293B");
        public static readonly Color BgCardTop = ColorTranslator.FromHtml("#243248");
        public static readonly Color BgCardHover = ColorTranslator.FromHtml("#26354D");
        public static readonly Color BgCardElevated = ColorTranslator.FromHtml("#2B3B57");
        public static readonly Color BgRecessed = ColorTranslator.FromHtml("#141E30");
        public static readonly Color BgInput = ColorTranslator.FromHtml("#17243A");

        public static readonly Color BorderSubtle = ColorTranslator.FromHtml("#2E3F5C");
        public static readonly Color BorderHighlight = ColorTranslator.FromHtml("#3E5478");
        public static readonly Color BorderHover = ColorTranslator.FromHtml("#506D9B");
        public static readonly Color BorderActive = ColorTranslator.FromHtml("#38BDF8");

        // =========================================================================
        // TYPOGRAPHY COLORS
        // =========================================================================
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#F8FAFC");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#CBD5E1");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#879AB5");

        // =========================================================================
        // SEMANTIC ACCENTS (ATMOSPHERIC SKY, MINT EMERALD, SUNBURST AMBER)
        // =========================================================================
        public static readonly Color AccentPrimary = ColorTranslator.FromHtml("#38BDF8");
        public static readonly Color AccentPrimaryDark = ColorTranslator.FromHtml("#0284C7");
        public static readonly Color AccentSafe = ColorTranslator.FromHtml("#34D399");
        public static readonly Color AccentSafeDark = ColorTranslator.FromHtml("#059669");
        public static readonly Color AccentWarning = ColorTranslator.FromHtml("#FBBF24");
        public static readonly Color AccentWarningDark = ColorTranslator.FromHtml("#D97706");
        public static readonly Color AccentDanger = ColorTranslator.FromHtml("#F87171");
        public static readonly Color AccentIndigo = ColorTranslator.FromHtml("#818CF8");

        // =========================================================================
        // TYPOGRAPHY SCALE (CLEAN SEGOE UI CONSUMER HIERARCHY)
        // =========================================================================
        public static readonly Font FontBrand = new Font("Segoe UI Semibold", 14.5f, FontStyle.Bold);
        public static readonly Font FontBrandSub = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontPageTitle = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold);
        public static readonly Font FontSectionTitle = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        public static readonly Font FontCardTitle = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        public static readonly Font FontSmallBold = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold);
        public static readonly Font FontBadge = new Font("Segoe UI Semibold", 7.75f, FontStyle.Bold);
        public static readonly Font FontNav = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);

        public static readonly Font FontMono = new Font("Consolas", 9f, FontStyle.Regular);
        public static readonly Font FontMonoBold = new Font("Consolas", 9.5f, FontStyle.Bold);
        public static readonly Font FontMonoSmall = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        public static readonly Font FontMetricValue = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
        public static readonly Font FontGaugeBig = new Font("Segoe UI Semibold", 25f, FontStyle.Bold);
        public static readonly Font FontHeroButton = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);

        // =========================================================================
        // WINDOWS 11 NATIVE DARK CHROME & SCROLLBAR INTEROP
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
                    DrawVectorIcon(g, "logo", new RectangleF(1, 1, 30, 30), AccentPrimary);
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
                DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int));

                int captionColor = (BgSidebar.B << 16) | (BgSidebar.G << 8) | BgSidebar.R;
                DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));

                int borderColor = (BorderSubtle.B << 16) | (BorderSubtle.G << 8) | BorderSubtle.R;
                DwmSetWindowAttribute(handle, 34, ref borderColor, sizeof(int));
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
        // COLOR, CARD & GEOMETRY HELPERS
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

        /// <summary>
        /// Draws a frosted-slate elevated card with subtle vertical surface gradient and top specular highlight.
        /// </summary>
        public static void DrawElevatedCard(Graphics g, RectangleF rect, float radius, bool hovered, Color customBorder)
        {
            if (rect.Width <= 2 || rect.Height <= 2) return;
            Color topCol = hovered ? Blend(BgCardTop, AccentPrimary, 0.06f) : BgCardTop;
            Color botCol = hovered ? BgCardHover : BgCard;

            using (LinearGradientBrush bg = new LinearGradientBrush(rect, topCol, botCol, 90f))
            {
                FillRoundedRect(g, bg, rect, radius);
            }

            // Soft top inner frost sheen
            using (Pen sheen = new Pen(Alpha(Color.White, hovered ? 22 : 13), 1f))
            {
                g.DrawLine(sheen, rect.X + radius, rect.Y + 1f, rect.Right - radius, rect.Y + 1f);
            }

            Color borderCol = customBorder.IsEmpty ? (hovered ? BorderHover : BorderSubtle) : customBorder;
            using (Pen border = new Pen(borderCol, 1f))
            {
                DrawRoundedRect(g, border, rect, radius);
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
        // CLEAN VECTOR ICON PAINTER (SKYTWEAKS BRANDING & NAVIGATION)
        // =========================================================================
        public static void DrawVectorIcon(Graphics g, string iconKey, RectangleF r, Color color)
        {
            SmoothingMode prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(color, 1.75f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (SolidBrush brush = new SolidBrush(color))
            {
                float x = r.X;
                float y = r.Y;
                float w = r.Width;
                float h = r.Height;

                switch (iconKey)
                {
                    case "logo":
                        // SkyTweaks gradient badge + cloud & sparkle emblem
                        RectangleF badgeRect = new RectangleF(x, y, w, h);
                        using (LinearGradientBrush grad = new LinearGradientBrush(
                            badgeRect, AccentPrimary, AccentPrimaryDark, 45f))
                        {
                            FillRoundedRect(g, grad, badgeRect, w * 0.28f);
                        }
                        using (Pen badgeBorder = new Pen(Alpha(Color.White, 65), 1f))
                        {
                            DrawRoundedRect(g, badgeBorder, badgeRect, w * 0.28f);
                        }

                        // Crisp white cloud silhouette inside badge
                        using (SolidBrush whiteBr = new SolidBrush(Color.White))
                        {
                            g.FillEllipse(whiteBr, x + w * 0.20f, y + h * 0.44f, w * 0.26f, h * 0.26f);
                            g.FillEllipse(whiteBr, x + w * 0.34f, y + h * 0.30f, w * 0.32f, h * 0.34f);
                            g.FillEllipse(whiteBr, x + w * 0.54f, y + h * 0.42f, w * 0.26f, h * 0.28f);
                            g.FillRectangle(whiteBr, x + w * 0.31f, y + h * 0.50f, w * 0.38f, h * 0.20f);

                            // Small 4-point sky sparkle top-right
                            PointF[] star = new PointF[]
                            {
                                new PointF(x + w * 0.75f, y + h * 0.16f),
                                new PointF(x + w * 0.78f, y + h * 0.25f),
                                new PointF(x + w * 0.87f, y + h * 0.28f),
                                new PointF(x + w * 0.78f, y + h * 0.31f),
                                new PointF(x + w * 0.75f, y + h * 0.40f),
                                new PointF(x + w * 0.72f, y + h * 0.31f),
                                new PointF(x + w * 0.63f, y + h * 0.28f),
                                new PointF(x + w * 0.72f, y + h * 0.25f)
                            };
                            g.FillPolygon(whiteBr, star);
                        }
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
                        float[] ys = new float[] { y + h * 0.22f, y + h * 0.50f, y + h * 0.78f };
                        float[] knobs = new float[] { x + w * 0.35f, x + w * 0.68f, x + w * 0.45f };
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawLine(pen, x + 1, ys[i], x + w - 1, ys[i]);
                            g.FillEllipse(brush, knobs[i] - 2.6f, ys[i] - 2.6f, 5.2f, 5.2f);
                        }
                        break;

                    case "booster":
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
                        // Friendly gamepad controller outline
                        DrawRoundedRect(g, pen, new RectangleF(x + 1, y + h * 0.22f, w - 2, h * 0.56f), 4.5f);
                        g.DrawLine(pen, x + w * 0.24f, y + h * 0.50f, x + w * 0.40f, y + h * 0.50f);
                        g.DrawLine(pen, x + w * 0.32f, y + h * 0.42f, x + w * 0.32f, y + h * 0.58f);
                        g.FillEllipse(brush, x + w * 0.62f, y + h * 0.40f, 2.5f, 2.5f);
                        g.FillEllipse(brush, x + w * 0.72f, y + h * 0.52f, 2.5f, 2.5f);
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
                        DrawRoundedRect(g, pen, new RectangleF(x + 1, y + h * 0.24f, w - 2, h * 0.52f), 2.5f);
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
                    case "restart":
                        g.DrawArc(pen, x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f, 30, 295);
                        g.DrawLine(pen, x + w * 0.76f, y + h * 0.14f, x + w * 0.88f, y + h * 0.34f);
                        g.DrawLine(pen, x + w * 0.64f, y + h * 0.34f, x + w * 0.88f, y + h * 0.34f);
                        if (iconKey == "restart")
                        {
                            g.FillEllipse(brush, x + w * 0.42f, y + h * 0.42f, w * 0.16f, h * 0.16f);
                        }
                        break;
                }
            }

            g.SmoothingMode = prev;
        }
    }
}
