using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using PCTweaker.Core;

namespace PCTweaker.UI
{
    public enum TacticalButtonStyle
    {
        Primary = 0,
        SolidPrimary = 1,
        Safe = 2,
        SolidSafe = 3,
        Warning = 4,
        SolidWarning = 8,
        Danger = 5,
        Secondary = 6,
        Ghost = 7
    }

    // =========================================================================
    // 1. DOUBLE-BUFFERED SMOOTH PANEL WITH DARK SCROLLBARS
    // =========================================================================
    public class SmoothPanel : Panel
    {
        public SmoothPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkScrollbars(Handle);
        }

        public void ForwardMouseWheel(int delta)
        {
            if (!AutoScroll) return;
            int step = -(delta / 120) * 48;
            int newY = Math.Max(0, -AutoScrollPosition.Y + step);
            AutoScrollPosition = new Point(-AutoScrollPosition.X, newY);
        }
    }

    // =========================================================================
    // 2. TACTICAL OWNER-DRAWN BUTTON
    // =========================================================================
    public class TacticalButton : Control
    {
        private bool _hover;
        private bool _pressed;

        public TacticalButtonStyle ButtonStyle { get; set; }
        public string IconKey { get; set; }
        public float CornerRadius { get; set; }
        public bool IsBusy { get; set; }

        public TacticalButton()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Hand;
            Font = Theme.FontBodyBold;
            ButtonStyle = TacticalButtonStyle.Secondary;
            CornerRadius = 8f;
            Size = new Size(140, 34);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed) { _pressed = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg, bgBottom, border, fg;
            ResolveColors(out bg, out bgBottom, out border, out fg);

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (bg != bgBottom && rect.Width > 2 && rect.Height > 2)
            {
                using (LinearGradientBrush br = new LinearGradientBrush(rect, bg, bgBottom, 90f))
                    Theme.FillRoundedRect(g, br, rect, CornerRadius);
            }
            else
            {
                using (SolidBrush br = new SolidBrush(bg))
                    Theme.FillRoundedRect(g, br, rect, CornerRadius);
            }

            if (border.A > 0)
            {
                using (Pen pen = new Pen(border, 1f))
                    Theme.DrawRoundedRect(g, pen, rect, CornerRadius);
            }

            string label = IsBusy ? "Working..." : Text;
            SizeF textSz = g.MeasureString(label, Font);
            bool hasIcon = !string.IsNullOrEmpty(IconKey) && !IsBusy;
            float iconSize = 14f;
            float gap = hasIcon ? 7f : 0f;
            float totalW = textSz.Width + (hasIcon ? iconSize + gap : 0f);
            float startX = (Width - totalW) / 2f;
            if (startX < 8f) startX = 8f;

            if (hasIcon)
            {
                RectangleF iconRect = new RectangleF(startX, (Height - iconSize) / 2f, iconSize, iconSize);
                Theme.DrawVectorIcon(g, IconKey, iconRect, fg);
                startX += iconSize + gap;
            }

            using (SolidBrush tb = new SolidBrush(fg))
            using (StringFormat sf = new StringFormat
            {
                Alignment = hasIcon ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                RectangleF textRect = hasIcon
                    ? new RectangleF(startX, 0, Width - startX - 6f, Height)
                    : new RectangleF(6f, 0, Width - 12f, Height);
                g.DrawString(label, Font, tb, textRect, sf);
            }
        }

        private void ResolveColors(out Color bgTop, out Color bgBottom, out Color border, out Color fg)
        {
            if (!Enabled)
            {
                bgTop = Theme.Alpha(Theme.BgCardElevated, 120);
                bgBottom = bgTop;
                border = Theme.Alpha(Theme.BorderSubtle, 100);
                fg = Theme.TextMuted;
                return;
            }

            switch (ButtonStyle)
            {
                case TacticalButtonStyle.SolidPrimary:
                    bgTop = _pressed ? Theme.AccentPrimaryDark
                          : (_hover ? Theme.Blend(Theme.AccentPrimary, Color.White, 0.16f) : Theme.AccentPrimary);
                    bgBottom = _pressed ? Theme.Blend(Theme.AccentPrimaryDark, Color.Black, 0.2f)
                             : (_hover ? Theme.AccentPrimary : Theme.AccentPrimaryDark);
                    border = Theme.Alpha(Color.White, 55);
                    fg = Color.White;
                    break;

                case TacticalButtonStyle.Primary:
                    bgTop = _pressed ? Theme.Alpha(Theme.AccentPrimary, 55)
                          : (_hover ? Theme.Alpha(Theme.AccentPrimary, 40) : Theme.Alpha(Theme.AccentPrimary, 24));
                    bgBottom = bgTop;
                    border = _hover ? Theme.AccentPrimary : Theme.Alpha(Theme.AccentPrimary, 145);
                    fg = Theme.AccentPrimary;
                    break;

                case TacticalButtonStyle.SolidSafe:
                    bgTop = _pressed ? Theme.AccentSafeDark
                          : (_hover ? Theme.Blend(Theme.AccentSafe, Color.White, 0.15f) : Theme.AccentSafe);
                    bgBottom = _pressed ? Theme.Blend(Theme.AccentSafeDark, Color.Black, 0.2f)
                             : (_hover ? Theme.AccentSafe : Theme.AccentSafeDark);
                    border = Theme.Alpha(Color.White, 55);
                    fg = Color.White;
                    break;

                case TacticalButtonStyle.Safe:
                    bgTop = _pressed ? Theme.Alpha(Theme.AccentSafe, 55)
                          : (_hover ? Theme.Alpha(Theme.AccentSafe, 40) : Theme.Alpha(Theme.AccentSafe, 24));
                    bgBottom = bgTop;
                    border = _hover ? Theme.AccentSafe : Theme.Alpha(Theme.AccentSafe, 145);
                    fg = Theme.AccentSafe;
                    break;

                case TacticalButtonStyle.SolidWarning:
                    bgTop = _pressed ? Theme.AccentWarningDark
                          : (_hover ? Theme.Blend(Theme.AccentWarning, Color.White, 0.18f) : Theme.AccentWarning);
                    bgBottom = _pressed ? Theme.Blend(Theme.AccentWarningDark, Color.Black, 0.2f)
                             : (_hover ? Theme.AccentWarning : Theme.AccentWarningDark);
                    border = Theme.Alpha(Color.White, 65);
                    fg = Theme.BgCanvas;
                    break;

                case TacticalButtonStyle.Warning:
                    bgTop = _pressed ? Theme.Alpha(Theme.AccentWarning, 55)
                          : (_hover ? Theme.Alpha(Theme.AccentWarning, 40) : Theme.Alpha(Theme.AccentWarning, 24));
                    bgBottom = bgTop;
                    border = _hover ? Theme.AccentWarning : Theme.Alpha(Theme.AccentWarning, 150);
                    fg = Theme.AccentWarning;
                    break;

                case TacticalButtonStyle.Danger:
                    bgTop = _pressed ? Theme.Alpha(Theme.AccentDanger, 55)
                          : (_hover ? Theme.Alpha(Theme.AccentDanger, 38) : Theme.Alpha(Theme.AccentDanger, 20));
                    bgBottom = bgTop;
                    border = _hover ? Theme.AccentDanger : Theme.Alpha(Theme.AccentDanger, 130);
                    fg = Theme.AccentDanger;
                    break;

                case TacticalButtonStyle.Ghost:
                    bgTop = _pressed ? Theme.BgCardElevated : (_hover ? Theme.BgCardHover : Color.Transparent);
                    bgBottom = bgTop;
                    border = _hover ? Theme.BorderSubtle : Color.Transparent;
                    fg = _hover ? Theme.TextPrimary : Theme.TextSecondary;
                    break;

                default: // Secondary
                    bgTop = _pressed ? Theme.BgCard : (_hover ? Theme.BgCardElevated : Theme.BgCardTop);
                    bgBottom = _pressed ? Theme.BgCard : (_hover ? Theme.BgCardHover : Theme.BgCard);
                    border = _hover ? Theme.BorderHover : Theme.BorderSubtle;
                    fg = _hover ? Theme.TextPrimary : Theme.TextSecondary;
                    break;
            }
        }
    }

    // =========================================================================
    // 3. SMOOTH ANIMATED TOGGLE SWITCH
    // =========================================================================
    public class TacticalToggleSwitch : Control
    {
        private bool _checked;
        private float _animPos;
        private bool _hover;
        private readonly Timer _animTimer;

        public event EventHandler UserToggled;

        public Color ActiveColor { get; set; }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    _animTimer.Start();
                    Invalidate();
                }
            }
        }

        public void SetCheckedImmediate(bool value)
        {
            _checked = value;
            _animPos = value ? 1f : 0f;
            _animTimer.Stop();
            Invalidate();
        }

        public TacticalToggleSwitch()
        {
            DoubleBuffered = true;
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            ActiveColor = Theme.AccentSafe;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += (s, e) =>
            {
                float target = _checked ? 1f : 0f;
                float diff = target - _animPos;
                if (Math.Abs(diff) < 0.06f)
                {
                    _animPos = target;
                    _animTimer.Stop();
                }
                else
                {
                    _animPos += diff * 0.35f;
                }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null) _animTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (UserToggled != null) UserToggled(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF track = new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f);
            Color offTrack = _hover ? Theme.BgCardHover : Theme.BgRecessed;
            Color onTrack = Theme.Alpha(ActiveColor, _hover ? 235 : 210);
            Color trackFill = Theme.Blend(offTrack, onTrack, _animPos);
            Color trackBorder = Theme.Blend(_hover ? Theme.BorderHover : Theme.BorderSubtle, ActiveColor, _animPos);

            using (SolidBrush br = new SolidBrush(trackFill))
                Theme.FillRoundedRect(g, br, track, track.Height / 2f);
            using (Pen pen = new Pen(trackBorder, 1.2f))
                Theme.DrawRoundedRect(g, pen, track, track.Height / 2f);

            float thumbSize = track.Height - 6f;
            float minX = track.X + 3f;
            float maxX = track.Right - thumbSize - 3f;
            float tx = minX + (maxX - minX) * _animPos;
            float ty = track.Y + 3f;

            Color thumbColor = Theme.Blend(Theme.TextSecondary, Color.White, _animPos);
            using (SolidBrush tb = new SolidBrush(thumbColor))
                g.FillEllipse(tb, tx, ty, thumbSize, thumbSize);
        }
    }

    // =========================================================================
    // 4. LEFT SIDEBAR NAVIGATION RAIL BUTTON
    // =========================================================================
    public class NavRailButton : Control
    {
        private bool _hover;
        private bool _isActive;
        private string _badgeText;

        public string IconKey { get; set; }
        public Color BadgeColor { get; set; }

        public bool IsActive
        {
            get { return _isActive; }
            set { if (_isActive != value) { _isActive = value; Invalidate(); } }
        }

        public string BadgeText
        {
            get { return _badgeText; }
            set { if (_badgeText != value) { _badgeText = value; Invalidate(); } }
        }

        public NavRailButton()
        {
            DoubleBuffered = true;
            Size = new Size(214, 42);
            Cursor = Cursors.Hand;
            Font = Theme.FontNav;
            BadgeColor = Theme.AccentPrimary;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 1f, Width - 1.5f, Height - 2f);

            if (_isActive)
            {
                using (SolidBrush br = new SolidBrush(Theme.Alpha(Theme.AccentPrimary, 22)))
                    Theme.FillRoundedRect(g, br, rect, 7f);
                using (Pen pen = new Pen(Theme.Alpha(Theme.AccentPrimary, 75), 1f))
                    Theme.DrawRoundedRect(g, pen, rect, 7f);

                // Left active cyan indicator bar
                using (SolidBrush bar = new SolidBrush(Theme.AccentPrimary))
                    Theme.FillRoundedRect(g, bar, new RectangleF(1.5f, 9f, 3.2f, Height - 18f), 1.6f);
            }
            else if (_hover)
            {
                using (SolidBrush br = new SolidBrush(Theme.BgCard))
                    Theme.FillRoundedRect(g, br, rect, 7f);
            }

            Color iconCol = _isActive ? Theme.AccentPrimary : (_hover ? Theme.TextPrimary : Theme.TextMuted);
            Color textCol = _isActive ? Theme.TextPrimary : (_hover ? Theme.TextPrimary : Theme.TextSecondary);

            Theme.DrawVectorIcon(g, IconKey ?? "dashboard", new RectangleF(14f, (Height - 16f) / 2f, 16f, 16f), iconCol);

            float rightLimit = Width - 10f;
            if (!string.IsNullOrEmpty(_badgeText))
            {
                float bw = Theme.MeasurePillWidth(g, _badgeText, Theme.FontBadge, 12);
                float bx = Width - bw - 10f;
                float by = (Height - 18f) / 2f;
                Color pillBg = _isActive ? Theme.Alpha(BadgeColor, 38) : Theme.BgRecessed;
                Color pillBorder = _isActive ? Theme.Alpha(BadgeColor, 140) : Theme.BorderSubtle;
                Color pillTxt = _isActive ? BadgeColor : Theme.TextMuted;
                Theme.DrawPillBadge(g, _badgeText, Theme.FontBadge, pillBg, pillBorder, pillTxt, bx, by, 18f, 12);
                rightLimit = bx - 6f;
            }

            using (SolidBrush tb = new SolidBrush(textCol))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Text, _isActive ? Theme.FontBodyBold : Font, tb, new RectangleF(39f, 0, rightLimit - 39f, Height), sf);
            }
        }
    }

    // =========================================================================
    // 5. CATEGORY FILTER PILL BUTTON
    // =========================================================================
    public class FilterPillButton : Control
    {
        private bool _hover;
        private bool _active;

        public bool IsActive
        {
            get { return _active; }
            set { if (_active != value) { _active = value; Invalidate(); } }
        }

        public FilterPillButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.FontSmallBold;
            Height = 30;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void FitWidthToText()
        {
            using (Graphics g = CreateGraphics())
            {
                SizeF sz = g.MeasureString(Text, Font);
                Width = (int)Math.Ceiling(sz.Width) + 24;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg = _active ? Theme.Alpha(Theme.AccentPrimary, 28) : (_hover ? Theme.BgCardHover : Theme.BgCard);
            Color border = _active ? Theme.AccentPrimary : (_hover ? Theme.BorderHover : Theme.BorderSubtle);
            Color fg = _active ? Theme.AccentPrimary : (_hover ? Theme.TextPrimary : Theme.TextSecondary);

            using (SolidBrush br = new SolidBrush(bg))
                Theme.FillRoundedRect(g, br, rect, rect.Height / 2f);
            using (Pen pen = new Pen(border, 1f))
                Theme.DrawRoundedRect(g, pen, rect, rect.Height / 2f);

            using (SolidBrush tb = new SolidBrush(fg))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Text, Font, tb, rect, sf);
            }
        }
    }

    // =========================================================================
    // 6. PERSISTENT TOP TELEMETRY METRIC CARD
    // =========================================================================
    public class TelemetryMetricCard : Control
    {
        public string MetricTitle { get; set; }
        public string PrimaryValue { get; set; }
        public string SubText { get; set; }
        public float ProgressPercent { get; set; } // 0..100
        public Color AccentColor { get; set; }

        public TelemetryMetricCard()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            MetricTitle = "METRIC";
            PrimaryValue = "--";
            SubText = "";
            AccentColor = Theme.AccentPrimary;
            Size = new Size(185, 58);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.DrawElevatedCard(g, rect, 10f, false, Color.Empty);

            // Top row: Title left, SubText right
            using (SolidBrush mb = new SolidBrush(Theme.TextMuted))
            using (StringFormat sfL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap })
            using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(MetricTitle, Theme.FontBadge, mb, new RectangleF(10, 7, Width - 20, 14), sfL);
                if (!string.IsNullOrEmpty(SubText))
                    g.DrawString(SubText, Theme.FontBadge, mb, new RectangleF(10, 7, Width - 20, 14), sfR);
            }

            // Middle row: Clean value
            using (SolidBrush vb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sfV = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(PrimaryValue, Theme.FontMetricValue, vb, new RectangleF(10, 20, Width - 20, 24), sfV);
            }

            // Bottom progress bar
            RectangleF barBg = new RectangleF(10, Height - 10, Width - 20, 4);
            using (SolidBrush track = new SolidBrush(Theme.BgRecessed))
                Theme.FillRoundedRect(g, track, barBg, 2f);

            float clamped = Math.Max(0f, Math.Min(100f, ProgressPercent));
            if (clamped > 0.5f)
            {
                float fillW = Math.Max(4f, barBg.Width * (clamped / 100f));
                using (SolidBrush fill = new SolidBrush(AccentColor))
                    Theme.FillRoundedRect(g, fill, new RectangleF(barBg.X, barBg.Y, fillW, barBg.Height), 2f);
            }
        }
    }

    // =========================================================================
    // 7. DASHBOARD RADIAL ARC OPTIMIZATION SCORE GAUGE
    // =========================================================================
    public class RadialScoreGauge : Control
    {
        private int _scorePercent;
        public int ScorePercent
        {
            get { return _scorePercent; }
            set { _scorePercent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public string Subtitle { get; set; }

        public RadialScoreGauge()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(168, 168);
            Subtitle = "OPTIMIZED";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float pad = 14f;
            float size = Math.Min(Width, Height) - pad * 2f;
            RectangleF arcRect = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);

            float startAngle = 145f;
            float totalSweep = 250f;
            float activeSweep = totalSweep * (_scorePercent / 100f);

            Color scoreColor = _scorePercent >= 75 ? Theme.AccentSafe
                             : (_scorePercent >= 35 ? Theme.AccentPrimary : Theme.AccentWarning);

            // Background arc
            using (Pen bgPen = new Pen(Theme.BgRecessed, 11f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(bgPen, arcRect, startAngle, totalSweep);

            using (Pen ringPen = new Pen(Theme.BorderSubtle, 1f))
                g.DrawArc(ringPen, new RectangleF(arcRect.X - 6, arcRect.Y - 6, arcRect.Width + 12, arcRect.Height + 12), startAngle, totalSweep);

            if (activeSweep > 1f)
            {
                // Subtle outer halo glow
                using (Pen glowPen = new Pen(Theme.Alpha(scoreColor, 45), 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(glowPen, arcRect, startAngle, activeSweep);

                // Crisp progress arc
                using (Pen fgPen = new Pen(scoreColor, 11f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(fgPen, arcRect, startAngle, activeSweep);
            }

            // Center score readout
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(_scorePercent + "%", Theme.FontGaugeBig, tb, new RectangleF(0, Height * 0.27f, Width, 44), sf);
            }

            using (SolidBrush sb = new SolidBrush(scoreColor))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("OPTIMIZED", Theme.FontBadge, sb, new RectangleF(0, Height * 0.56f, Width, 16), sf);
            }

            if (!string.IsNullOrEmpty(Subtitle))
            {
                using (SolidBrush mb = new SolidBrush(Theme.TextSecondary))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(Subtitle, Theme.FontBadge, mb, new RectangleF(4, Height * 0.80f, Width - 8, 18), sf);
                }
            }
        }
    }

    // =========================================================================
    // 8. STACKED MEMORY ALLOCATION BAR (VIEW 3)
    // =========================================================================
    public class StackedMemoryBar : Control
    {
        public long UsedMb { get; set; }
        public long StandbyCacheMb { get; set; }
        public long FreeRamMb { get; set; }
        public long AvailableMb { get; set; }
        public long TotalMb { get; set; }
        public int LoadPercent { get; set; }
        public int ProcessCount { get; set; }
        public int HandleCount { get; set; }

        public StackedMemoryBar()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(460, 74);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Header row
            using (SolidBrush mb = new SolidBrush(Theme.TextMuted))
            using (SolidBrush pb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sfL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
            {
                g.DrawString("PHYSICAL MEMORY POOL", Theme.FontBadge, mb, new RectangleF(0, 0, Width, 16), sfL);
                string readout = string.Format("{0:N0} MB Used / {1:N0} MB Total ({2}%)", UsedMb, TotalMb, LoadPercent);
                g.DrawString(readout, Theme.FontMonoSmall, pb, new RectangleF(0, 0, Width, 16), sfR);
            }

            // 3-Segment Stacked Bar clipped smoothly inside rounded pill track
            RectangleF barRect = new RectangleF(0, 22, Width - 1, 18);
            using (SolidBrush track = new SolidBrush(Theme.BgRecessed))
                Theme.FillRoundedRect(g, track, barRect, 6f);

            RectangleF inner = new RectangleF(barRect.X + 1, barRect.Y + 1, Math.Max(4f, barRect.Width - 2), barRect.Height - 2);
            float total = TotalMb > 0 ? (float)TotalMb : 1f;
            float usedRatio = Math.Max(0f, Math.Min(1f, UsedMb / total));
            float standbyRatio = Math.Max(0f, Math.Min(1f - usedRatio, StandbyCacheMb / total));
            float freeRatio = Math.Max(0f, 1f - usedRatio - standbyRatio);

            float usedW = inner.Width * usedRatio;
            float standbyW = inner.Width * standbyRatio;
            float freeW = Math.Max(0f, inner.Width - usedW - standbyW);

            Color usedColor = LoadPercent >= 85 ? Theme.AccentDanger
                            : (LoadPercent >= 68 ? Theme.AccentWarning : Theme.AccentPrimary);
            Color standbyColor = Theme.AccentIndigo;
            Color freeColor = Theme.AccentSafe;

            GraphicsState savedState = g.Save();
            using (GraphicsPath clipPath = Theme.CreateRoundedRectPath(inner, 5f))
            {
                g.SetClip(clipPath);

                // Segment 1: In-Use RAM
                if (usedW > 0.5f)
                {
                    RectangleF seg1 = new RectangleF(inner.X, inner.Y, Math.Max(2f, usedW), inner.Height);
                    using (LinearGradientBrush usedBr = new LinearGradientBrush(
                        seg1, Theme.Blend(usedColor, Theme.BgCanvas, 0.18f), usedColor, 0f))
                    {
                        g.FillRectangle(usedBr, seg1);
                    }
                }

                // Segment 2: Windows Standby Page List / System Cache
                if (standbyW > 0.5f)
                {
                    RectangleF seg2 = new RectangleF(inner.X + usedW, inner.Y, standbyW, inner.Height);
                    using (LinearGradientBrush sbBr = new LinearGradientBrush(
                        seg2, Theme.Alpha(standbyColor, 185), Theme.Alpha(standbyColor, 235), 0f))
                    {
                        g.FillRectangle(sbBr, seg2);
                    }
                }

                // Segment 3: Pure Zeroed / Free Physical RAM
                if (freeW > 0.5f)
                {
                    RectangleF seg3 = new RectangleF(inner.X + usedW + standbyW, inner.Y, freeW + 1f, inner.Height);
                    using (SolidBrush freeBr = new SolidBrush(Theme.Alpha(freeColor, 175)))
                        g.FillRectangle(freeBr, seg3);
                }

                // Crisp 1.5px dark dividers between segments
                using (Pen sep = new Pen(Theme.BgCanvas, 1.5f))
                {
                    if (usedW > 2f && standbyW > 1f)
                        g.DrawLine(sep, inner.X + usedW, inner.Y, inner.X + usedW, inner.Bottom);
                    if ((usedW + standbyW) > 2f && freeW > 1f)
                        g.DrawLine(sep, inner.X + usedW + standbyW, inner.Y, inner.X + usedW + standbyW, inner.Bottom);
                }
            }
            g.Restore(savedState);

            using (Pen border = new Pen(Theme.BorderSubtle, 1f))
                Theme.DrawRoundedRect(g, border, barRect, 6f);

            // Bottom 3-Segment Legend Row (dynamically measured so labels never overlap)
            float ly = 48f;
            using (SolidBrush dotUsed = new SolidBrush(usedColor))
            using (SolidBrush dotStandby = new SolidBrush(standbyColor))
            using (SolidBrush dotFree = new SolidBrush(freeColor))
            using (SolidBrush txtBr = new SolidBrush(Theme.TextSecondary))
            using (SolidBrush mutedBr = new SolidBrush(Theme.TextMuted))
            {
                float lx = 2f;
                g.FillEllipse(dotUsed, lx, ly + 4, 8, 8);
                string sUsed = string.Format("Used: {0:F1} GB", UsedMb / 1024.0);
                g.DrawString(sUsed, Theme.FontSmall, txtBr, lx + 12f, ly);
                lx += 12f + g.MeasureString(sUsed, Theme.FontSmall).Width + 12f;

                g.FillEllipse(dotStandby, lx, ly + 4, 8, 8);
                string sStandby = string.Format("Standby: {0:F1} GB", StandbyCacheMb / 1024.0);
                g.DrawString(sStandby, Theme.FontSmall, txtBr, lx + 12f, ly);
                lx += 12f + g.MeasureString(sStandby, Theme.FontSmall).Width + 12f;

                g.FillEllipse(dotFree, lx, ly + 4, 8, 8);
                string sFree = string.Format("Free: {0:F1} GB", FreeRamMb / 1024.0);
                g.DrawString(sFree, Theme.FontSmall, txtBr, lx + 12f, ly);
                lx += 12f + g.MeasureString(sFree, Theme.FontSmall).Width + 10f;

                string procStr = string.Format("{0} procs", ProcessCount);
                float procW = g.MeasureString(procStr, Theme.FontMonoSmall).Width;
                if (Width - procW > lx)
                {
                    using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
                    {
                        g.DrawString(procStr, Theme.FontMonoSmall, mutedBr, new RectangleF(lx, ly + 1, Width - lx, 16), sfR);
                    }
                }
            }
        }
    }

    // =========================================================================
    // 9. DASHBOARD 1-CLICK PRESET HERO CARD
    // =========================================================================
    public class PresetHeroCard : Control
    {
        private bool _hover;
        public string TierTag { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ProgressBadge { get; set; }
        public Color AccentColor { get; set; }
        public TacticalButton ActionButton { get; private set; }

        public PresetHeroCard()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            AccentColor = Theme.AccentPrimary;
            Size = new Size(440, 174);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            ActionButton = new TacticalButton
            {
                Height = 36,
                ButtonStyle = TacticalButtonStyle.Primary,
                CornerRadius = 6f
            };
            Controls.Add(ActionButton);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ActionButton != null)
            {
                ActionButton.SetBounds(16, Height - 50, Width - 32, 36);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.DrawElevatedCard(g, rect, 12f, _hover, _hover ? Theme.Alpha(AccentColor, 145) : Color.Empty);

            // Top accent stripe
            using (SolidBrush stripe = new SolidBrush(AccentColor))
                Theme.FillRoundedRect(g, stripe, new RectangleF(16f, 1.5f, 54f, 3f), 1.5f);

            // Tier tag pill left
            if (!string.IsNullOrEmpty(TierTag))
            {
                Theme.DrawPillBadge(g, TierTag, Theme.FontBadge,
                    Theme.Alpha(AccentColor, 26),
                    Theme.Alpha(AccentColor, 110),
                    AccentColor, 16f, 15f, 20f, 14);
            }

            // Progress badge right
            if (!string.IsNullOrEmpty(ProgressBadge))
            {
                float pw = Theme.MeasurePillWidth(g, ProgressBadge, Theme.FontBadge, 14);
                Theme.DrawPillBadge(g, ProgressBadge, Theme.FontBadge,
                    Theme.BgRecessed, Theme.BorderSubtle, Theme.TextSecondary,
                    Width - pw - 16f, 15f, 20f, 14);
            }

            // Title
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Title ?? "", Theme.FontSectionTitle, tb, new RectangleF(16f, 44f, Width - 32f, 24f), sfTitle);
            }

            // Description
            using (SolidBrush db = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sfDesc = new StringFormat { Trimming = StringTrimming.EllipsisWord })
            {
                g.DrawString(Description ?? "", Theme.FontBody, db, new RectangleF(16f, 68f, Width - 32f, 48f), sfDesc);
            }
        }
    }

    // =========================================================================
    // 10. EXPANDABLE SYSTEM TWEAK CARD (VIEW 2) WITH DIRECT REBOOT BUTTON
    // =========================================================================
    public class TweakItemCard : Control
    {
        public const int CollapsedHeight = 100;
        public const int ExpandedHeight = 214;

        private bool _hover;
        private bool _isExpanded;
        private bool _isPendingReboot;
        private RectangleF _detailsLinkRect;
        private readonly TacticalToggleSwitch _toggle;
        private readonly TacticalButton _btnRebootCard;

        public TweakDefinition Tweak { get; private set; }
        public HardwareProfile Hardware { get; private set; }

        public event Action<TweakDefinition> ToggleRequested;
        public event Action<TweakDefinition> RebootRequested;
        public event Action<TweakItemCard> ExpandChanged;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    Height = _isExpanded ? ExpandedHeight : CollapsedHeight;
                    if (Tweak.RequiresReboot && _btnRebootCard != null)
                    {
                        _btnRebootCard.Visible = _isPendingReboot || Tweak.CurrentStatus == TweakStatus.Applied || _isExpanded;
                        PositionRebootButton();
                    }
                    Invalidate();
                    if (ExpandChanged != null) ExpandChanged(this);
                }
            }
        }

        public TweakItemCard(TweakDefinition tweak, HardwareProfile hardware)
        {
            Tweak = tweak;
            Hardware = hardware;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Height = CollapsedHeight;
            Width = 860;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            _toggle = new TacticalToggleSwitch
            {
                Location = new Point(18, 22),
                ActiveColor = Theme.AccentPrimary
            };
            _toggle.SetCheckedImmediate(tweak.CurrentStatus == TweakStatus.Applied);
            _toggle.UserToggled += (s, e) =>
            {
                if (ToggleRequested != null) ToggleRequested(Tweak);
            };
            Controls.Add(_toggle);

            _btnRebootCard = new TacticalButton
            {
                Text = "Restart to Apply",
                IconKey = "restart",
                Font = Theme.FontBadge,
                ButtonStyle = TacticalButtonStyle.Warning,
                CornerRadius = 6f,
                Size = new Size(124, 24),
                Visible = tweak.RequiresReboot && tweak.CurrentStatus == TweakStatus.Applied
            };
            _btnRebootCard.Click += (s, e) =>
            {
                if (RebootRequested != null) RebootRequested(Tweak);
            };
            Controls.Add(_btnRebootCard);
            PositionRebootButton();
        }

        public void SyncStateFromModel(bool isPendingReboot = false)
        {
            _isPendingReboot = isPendingReboot;
            bool isApplied = Tweak.CurrentStatus == TweakStatus.Applied;
            _toggle.SetCheckedImmediate(isApplied);

            if (Tweak.RequiresReboot)
            {
                bool showRebootBtn = isPendingReboot || isApplied || _isExpanded;
                _btnRebootCard.Visible = showRebootBtn;
                _btnRebootCard.Text = isPendingReboot ? "Restart Now" : "Restart to Apply";
                _btnRebootCard.ButtonStyle = isPendingReboot
                    ? TacticalButtonStyle.SolidWarning
                    : TacticalButtonStyle.Warning;
                PositionRebootButton();
            }
            else
            {
                _btnRebootCard.Visible = false;
            }

            Invalidate();
        }

        private void PositionRebootButton()
        {
            if (_btnRebootCard == null) return;
            _btnRebootCard.Location = new Point(Width - _btnRebootCard.Width - 14, 62);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionRebootButton();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Cursor = Cursors.Default; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _detailsLinkRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _detailsLinkRect.Contains(e.Location))
            {
                IsExpanded = !IsExpanded;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            SmoothPanel parentScroll = Parent as SmoothPanel;
            if (parentScroll != null) parentScroll.ForwardMouseWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isApplied = Tweak.CurrentStatus == TweakStatus.Applied;
            bool isRecommended = Tweak.IsRecommendedForTier(Hardware.DetectedTier);

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color customBorder = _isPendingReboot
                ? Theme.Alpha(Theme.AccentWarning, 165)
                : (isApplied ? Theme.Alpha(Theme.AccentPrimary, _hover ? 160 : 100) : Color.Empty);

            Theme.DrawElevatedCard(g, rect, 11f, _hover, customBorder);

            // Left status stripe
            Color stripeCol = _isPendingReboot
                ? Theme.AccentWarning
                : (isApplied ? Theme.AccentPrimary : Theme.BorderSubtle);
            using (SolidBrush stripe = new SolidBrush(stripeCol))
                Theme.FillRoundedRect(g, stripe, new RectangleF(1.5f, 14f, 3.2f, CollapsedHeight - 28f), 1.6f);

            // Right-aligned badges flowing right-to-left on Row 1
            float badgeX = Width - 14f;
            float badgeY = 13f;

            // Status Pill (Enabled / Default)
            string statusTxt = isApplied ? "Enabled" : "Off";
            Color stCol = isApplied ? Theme.AccentSafe : Theme.TextMuted;
            float stW = Theme.MeasurePillWidth(g, statusTxt, Theme.FontBadge, 14);
            badgeX -= stW;
            Theme.DrawPillBadge(g, statusTxt, Theme.FontBadge,
                isApplied ? Theme.Alpha(Theme.AccentSafe, 28) : Theme.BgRecessed,
                isApplied ? Theme.Alpha(Theme.AccentSafe, 120) : Theme.BorderSubtle,
                stCol, badgeX, badgeY, 19f, 14);

            // Risk Pill
            string riskTxt = Tweak.RiskLevel == TweakRisk.Safe ? "Safe"
                           : (Tweak.RiskLevel == TweakRisk.Moderate ? "Moderate" : "Advanced");
            Color riskCol = Tweak.RiskLevel == TweakRisk.Safe ? Theme.AccentSafe
                          : (Tweak.RiskLevel == TweakRisk.Moderate ? Theme.AccentWarning : Theme.AccentDanger);
            float rW = Theme.MeasurePillWidth(g, riskTxt, Theme.FontBadge, 12);
            badgeX -= (rW + 6f);
            Theme.DrawPillBadge(g, riskTxt, Theme.FontBadge,
                Theme.Alpha(riskCol, 22), Theme.Alpha(riskCol, 95), riskCol, badgeX, badgeY, 19f, 12);

            // Reboot pill if needed
            if (Tweak.RequiresReboot)
            {
                string rbLabel = _isPendingReboot ? "Restart Pending" : "Needs Restart";
                float rbW = Theme.MeasurePillWidth(g, rbLabel, Theme.FontBadge, 12);
                badgeX -= (rbW + 6f);
                Theme.DrawPillBadge(g, rbLabel, Theme.FontBadge,
                    Theme.Alpha(Theme.AccentWarning, _isPendingReboot ? 38 : 20),
                    Theme.Alpha(Theme.AccentWarning, _isPendingReboot ? 150 : 95),
                    Theme.AccentWarning,
                    badgeX, badgeY, 19f, 12);
            }

            // Admin pill if requires admin & currently non-admin
            if (Tweak.RequiresAdmin && !Hardware.IsRunningAsAdmin)
            {
                float aW = Theme.MeasurePillWidth(g, "Admin Needed", Theme.FontBadge, 12);
                badgeX -= (aW + 6f);
                Theme.DrawPillBadge(g, "Admin Needed", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentWarning, 20), Theme.Alpha(Theme.AccentWarning, 95), Theme.AccentWarning,
                    badgeX, badgeY, 19f, 12);
            }

            // Recommended star pill
            if (isRecommended)
            {
                float recW = Theme.MeasurePillWidth(g, "★ Recommended", Theme.FontBadge, 12);
                badgeX -= (recW + 6f);
                Theme.DrawPillBadge(g, "★ Recommended", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentPrimary, 24), Theme.Alpha(Theme.AccentPrimary, 115), Theme.AccentPrimary,
                    badgeX, badgeY, 19f, 12);
            }

            // Title (Left of badges)
            float contentX = 78f;
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Tweak.Title, Theme.FontCardTitle, tb, new RectangleF(contentX, 12f, Math.Max(120f, badgeX - contentX - 10f), 22f), sfTitle);
            }

            // Plain-English Summary (Row 2)
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sfDesc = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Tweak.ShortSummary, Theme.FontBody, sb, new RectangleF(contentX, 37f, Width - contentX - 16f, 20f), sfDesc);
            }

            // Row 3: Impact Micro-Meters + Live Value + Details Expander + Direct Reboot Button
            float row3Y = 66f;
            float mx = contentX;
            mx = DrawImpactMeter(g, "FPS", Tweak.ImpactFps, Theme.AccentPrimary, mx, row3Y);
            mx = DrawImpactMeter(g, "Latency", Tweak.ImpactLatency, Theme.AccentSafe, mx + 12f, row3Y);
            mx = DrawImpactMeter(g, "Memory", Tweak.ImpactRam, Theme.AccentIndigo, mx + 12f, row3Y);

            // Technical Details expander link (positioned left of _btnRebootCard if visible)
            string detailsLabel = _isExpanded ? "Hide Details ▲" : "View Details ▼";
            SizeF dlSz = g.MeasureString(detailsLabel, Theme.FontSmallBold);
            float dlW = (float)Math.Ceiling(dlSz.Width) + 8f;
            float rightAnchor = (_btnRebootCard != null && _btnRebootCard.Visible)
                ? (_btnRebootCard.Left - 10f)
                : (Width - 14f);

            _detailsLinkRect = new RectangleF(rightAnchor - dlW, row3Y - 2f, dlW, 20f);
            using (SolidBrush lb = new SolidBrush(Theme.AccentPrimary))
            using (StringFormat sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(detailsLabel, Theme.FontSmallBold, lb, _detailsLinkRect, sfR);
            }

            // Live Audited Value between meters and expander link
            float liveX = mx + 16f;
            float liveMaxW = _detailsLinkRect.X - liveX - 10f;
            if (liveMaxW > 80f)
            {
                using (SolidBrush lvBr = new SolidBrush(Theme.TextMuted))
                using (StringFormat sfLv = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    g.DrawString("State: " + Tweak.LiveValueSummary, Theme.FontSmall, lvBr, new RectangleF(liveX, row3Y - 1f, liveMaxW, 18f), sfLv);
                }
            }

            // Expanded Technical Details Drawer
            if (_isExpanded)
            {
                RectangleF drawer = new RectangleF(16f, CollapsedHeight + 2f, Width - 32f, ExpandedHeight - CollapsedHeight - 14f);
                using (SolidBrush dbg = new SolidBrush(Theme.BgRecessed))
                    Theme.FillRoundedRect(g, dbg, drawer, 8f);
                using (Pen dpen = new Pen(Theme.BorderSubtle, 1f))
                    Theme.DrawRoundedRect(g, dpen, drawer, 8f);

                float dx = drawer.X + 14f;
                float dw = drawer.Width - 28f;
                using (SolidBrush lblBr = new SolidBrush(Theme.AccentPrimary))
                using (SolidBrush bodyBr = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush monoBr = new SolidBrush(Theme.TextSecondary))
                using (SolidBrush safeBr = new SolidBrush(Theme.AccentSafe))
                using (StringFormat sfWrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
                {
                    g.DrawString("How it helps:", Theme.FontSmallBold, lblBr, dx, drawer.Y + 8f);
                    g.DrawString(Tweak.WhyItHelpsGaming, Theme.FontSmall, bodyBr, new RectangleF(dx + 112f, drawer.Y + 8f, dw - 112f, 28f), sfWrap);

                    g.DrawString("System setting:", Theme.FontSmallBold, lblBr, dx, drawer.Y + 40f);
                    g.DrawString(Tweak.TechnicalDetails, Theme.FontMono, monoBr, new RectangleF(dx + 112f, drawer.Y + 40f, dw - 112f, 30f), sfWrap);

                    g.DrawString("✓ Automatic backup saved in SkyGuard   •   Safe for all anti-cheat games",
                        Theme.FontBadge, safeBr, dx, drawer.Y + 75f);
                }
            }
        }

        private float DrawImpactMeter(Graphics g, string label, int level, Color color, float x, float y)
        {
            SizeF sz = g.MeasureString(label, Theme.FontBadge);
            using (SolidBrush lb = new SolidBrush(Theme.TextMuted))
                g.DrawString(label, Theme.FontBadge, lb, x, y + 1f);

            float px = x + (float)Math.Ceiling(sz.Width) + 4f;
            for (int i = 1; i <= 5; i++)
            {
                Color pipCol = i <= level ? color : Theme.Alpha(Theme.BorderSubtle, 160);
                using (SolidBrush pb = new SolidBrush(pipCol))
                    Theme.FillRoundedRect(g, pb, new RectangleF(px, y + 4f, 6f, 8f), 1.5f);
                px += 8f;
            }
            return px;
        }
    }

    // =========================================================================
    // 11. GAME CONFIG & ENGINE OPTIMIZER CARD (VIEW 4)
    // =========================================================================
    public class GameOptimizerCard : Control
    {
        private bool _hover;
        public DetectedGame Game { get; private set; }

        public TacticalButton BtnIfeo { get; private set; }
        public TacticalButton BtnGpuPref { get; private set; }
        public TacticalButton BtnFso { get; private set; }
        public TacticalButton BtnPotatoIni { get; private set; }
        public TacticalButton BtnCompIni { get; private set; }
        public TacticalButton BtnRestoreIni { get; private set; }

        public event Action<DetectedGame> IfeoClicked;
        public event Action<DetectedGame> GpuPrefClicked;
        public event Action<DetectedGame> FsoClicked;
        public event Action<DetectedGame> PotatoIniClicked;
        public event Action<DetectedGame> CompIniClicked;
        public event Action<DetectedGame> RestoreIniClicked;

        public GameOptimizerCard(DetectedGame game)
        {
            Game = game;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Height = 122;
            Width = 860;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            BtnIfeo = new TacticalButton { Size = new Size(154, 30), Font = Theme.FontSmallBold };
            BtnIfeo.Click += (s, e) => { if (IfeoClicked != null) IfeoClicked(Game); };

            BtnGpuPref = new TacticalButton { Size = new Size(140, 30), Font = Theme.FontSmallBold };
            BtnGpuPref.Click += (s, e) => { if (GpuPrefClicked != null) GpuPrefClicked(Game); };

            BtnFso = new TacticalButton { Size = new Size(138, 30), Font = Theme.FontSmallBold };
            BtnFso.Click += (s, e) => { if (FsoClicked != null) FsoClicked(Game); };

            BtnPotatoIni = new TacticalButton
            {
                Text = "Max-FPS Config (.ini)",
                Size = new Size(146, 30),
                Font = Theme.FontSmallBold,
                ButtonStyle = TacticalButtonStyle.Warning
            };
            BtnPotatoIni.Click += (s, e) => { if (PotatoIniClicked != null) PotatoIniClicked(Game); };

            BtnCompIni = new TacticalButton
            {
                Text = "Competitive Config",
                Size = new Size(132, 30),
                Font = Theme.FontSmallBold,
                ButtonStyle = TacticalButtonStyle.Primary
            };
            BtnCompIni.Click += (s, e) => { if (CompIniClicked != null) CompIniClicked(Game); };

            BtnRestoreIni = new TacticalButton
            {
                Text = "Restore Backup",
                Size = new Size(112, 30),
                Font = Theme.FontSmallBold,
                ButtonStyle = TacticalButtonStyle.Secondary
            };
            BtnRestoreIni.Click += (s, e) => { if (RestoreIniClicked != null) RestoreIniClicked(Game); };

            Controls.Add(BtnIfeo);
            Controls.Add(BtnGpuPref);
            Controls.Add(BtnFso);
            Controls.Add(BtnPotatoIni);
            Controls.Add(BtnCompIni);
            Controls.Add(BtnRestoreIni);

            SyncStateFromModel();
            LayoutButtons();
        }

        public void SyncStateFromModel()
        {
            BtnIfeo.Text = Game.IsIfeoHighPriorityEnabled ? "High CPU Priority: ON" : "High CPU Priority";
            BtnIfeo.ButtonStyle = Game.IsIfeoHighPriorityEnabled ? TacticalButtonStyle.Safe : TacticalButtonStyle.Secondary;

            if (string.IsNullOrEmpty(Game.ExecutablePath))
            {
                BtnGpuPref.Text = "Locate .exe (GPU)";
                BtnGpuPref.ButtonStyle = TacticalButtonStyle.Secondary;
                BtnFso.Text = "Locate .exe (Fullscreen)";
                BtnFso.ButtonStyle = TacticalButtonStyle.Secondary;
            }
            else
            {
                BtnGpuPref.Text = Game.IsHighPerfGpuPreferred ? "High-Perf GPU: ON" : "Use High-Perf GPU";
                BtnGpuPref.ButtonStyle = Game.IsHighPerfGpuPreferred ? TacticalButtonStyle.Safe : TacticalButtonStyle.Primary;

                BtnFso.Text = Game.IsFullscreenOptDisabled ? "Fullscreen Opt: OFF" : "Disable Fullscreen Opt";
                BtnFso.ButtonStyle = Game.IsFullscreenOptDisabled ? TacticalButtonStyle.Safe : TacticalButtonStyle.Secondary;
            }

            BtnPotatoIni.Visible = Game.HasUnrealIni;
            BtnCompIni.Visible = Game.HasUnrealIni;
            BtnRestoreIni.Visible = Game.HasUnrealIni;
            BtnRestoreIni.Enabled = Game.HasIniBackup;

            Invalidate();
        }

        private void LayoutButtons()
        {
            if (BtnIfeo == null) return;
            TacticalButton[] allBtns = new TacticalButton[] { BtnIfeo, BtnGpuPref, BtnFso, BtnPotatoIni, BtnCompIni, BtnRestoreIni };
            int x = 16;
            int y = 78;
            int maxRight = Math.Max(420, Width - 16);

            foreach (TacticalButton b in allBtns)
            {
                if (!b.Visible) continue;
                if (x > 16 && x + b.Width > maxRight)
                {
                    x = 16;
                    y += 38;
                }
                b.Location = new Point(x, y);
                x += b.Width + 8;
            }

            int desiredHeight = y + 44;
            if (Height != desiredHeight) Height = desiredHeight;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutButtons(); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            SmoothPanel parentScroll = Parent as SmoothPanel;
            if (parentScroll != null) parentScroll.ForwardMouseWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.DrawElevatedCard(g, rect, 11f, _hover, Color.Empty);

            // Right-aligned live badges flowing right-to-left
            float rx = Width - 16f;
            if (Game.HasIniBackup)
            {
                float bw = Theme.MeasurePillWidth(g, ".INI BACKUP SAVED", Theme.FontBadge, 12);
                rx -= bw;
                Theme.DrawPillBadge(g, ".INI BACKUP SAVED", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentSafe, 24), Theme.Alpha(Theme.AccentSafe, 110), Theme.AccentSafe, rx, 14f, 19f, 12);
                rx -= 6f;
            }
            if (Game.IsFullscreenOptDisabled)
            {
                float bw = Theme.MeasurePillWidth(g, "FSO DISABLED", Theme.FontBadge, 12);
                rx -= bw;
                Theme.DrawPillBadge(g, "FSO DISABLED", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentPrimary, 24), Theme.Alpha(Theme.AccentPrimary, 110), Theme.AccentPrimary, rx, 14f, 19f, 12);
                rx -= 6f;
            }
            if (Game.IsHighPerfGpuPreferred)
            {
                float bw = Theme.MeasurePillWidth(g, "HIGH-PERF GPU", Theme.FontBadge, 12);
                rx -= bw;
                Theme.DrawPillBadge(g, "HIGH-PERF GPU", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentSafe, 24), Theme.Alpha(Theme.AccentSafe, 110), Theme.AccentSafe, rx, 14f, 19f, 12);
                rx -= 6f;
            }
            if (Game.IsIfeoHighPriorityEnabled)
            {
                float bw = Theme.MeasurePillWidth(g, "IFEO HIGH PRIO", Theme.FontBadge, 12);
                rx -= bw;
                Theme.DrawPillBadge(g, "IFEO HIGH PRIO", Theme.FontBadge,
                    Theme.Alpha(Theme.AccentSafe, 26), Theme.Alpha(Theme.AccentSafe, 120), Theme.AccentSafe, rx, 14f, 19f, 12);
                rx -= 6f;
            }

            // Title & Engine Pill (respecting right-aligned badges)
            string engTag = Game.EngineName.ToUpperInvariant();
            float engW = Theme.MeasurePillWidth(g, engTag, Theme.FontBadge, 12);
            float maxTitleW = Math.Max(120f, rx - 16f - engW - 18f);
            float measuredNameW = Math.Min(maxTitleW, g.MeasureString(Game.GameName, Theme.FontSectionTitle).Width);

            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Game.GameName, Theme.FontSectionTitle, tb, new RectangleF(16f, 13f, maxTitleW, 22f), sfTitle);
            }
            Theme.DrawPillBadge(g, engTag, Theme.FontBadge,
                Theme.Alpha(Theme.AccentPrimary, 20), Theme.Alpha(Theme.AccentPrimary, 95), Theme.AccentPrimary,
                16f + measuredNameW + 10f, 14f, 19f, 12);

            // Monospace target executable + config path
            string pathInfo;
            if (!string.IsNullOrEmpty(Game.ExecutablePath) && !string.IsNullOrEmpty(Game.GameUserSettingsIniPath))
                pathInfo = string.Format("EXE: {0}   •   INI: {1}", Game.ExecutablePath, Game.GameUserSettingsIniPath);
            else if (!string.IsNullOrEmpty(Game.ExecutablePath))
                pathInfo = string.Format("EXE: {0}", Game.ExecutablePath);
            else
                pathInfo = string.Format("Binary: {0} (unlinked)   •   INI: {1}", Game.ExecutableName, Game.GameUserSettingsIniPath);

            using (SolidBrush mb = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(pathInfo, Theme.FontMonoSmall, mb, new RectangleF(16f, 44f, Width - 32f, 20f), sf);
            }
        }
    }

    // =========================================================================
    // 12. SAFETY VAULT SNAPSHOT CARD (VIEW 5)
    // =========================================================================
    public class SnapshotItemCard : Control
    {
        private bool _hover;
        private readonly bool _isBaseline;
        public StateSnapshot Snapshot { get; private set; }
        public TacticalButton BtnRestore { get; private set; }
        public TacticalButton BtnDelete { get; private set; }
        public event Action<StateSnapshot> RestoreClicked;
        public event Action<StateSnapshot> DeleteClicked;

        public SnapshotItemCard(StateSnapshot snap)
        {
            Snapshot = snap;
            _isBaseline = string.Equals(snap.SnapshotId, "baseline", StringComparison.OrdinalIgnoreCase);
            DoubleBuffered = true;
            ResizeRedraw = true;
            Height = 68;
            Width = 860;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            BtnRestore = new TacticalButton
            {
                Text = _isBaseline ? "Revert to Baseline" : "Restore Snapshot",
                Size = new Size(148, 32),
                Font = Theme.FontSmallBold,
                ButtonStyle = _isBaseline ? TacticalButtonStyle.Warning : TacticalButtonStyle.Primary
            };
            BtnRestore.Click += (s, e) => { if (RestoreClicked != null) RestoreClicked(Snapshot); };
            Controls.Add(BtnRestore);

            if (!_isBaseline)
            {
                BtnDelete = new TacticalButton
                {
                    Text = "Delete",
                    Size = new Size(68, 32),
                    Font = Theme.FontSmallBold,
                    ButtonStyle = TacticalButtonStyle.Danger
                };
                BtnDelete.Click += (s, e) => { if (DeleteClicked != null) DeleteClicked(Snapshot); };
                Controls.Add(BtnDelete);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (BtnRestore != null)
            {
                BtnRestore.Location = new Point(Width - BtnRestore.Width - 14, (Height - BtnRestore.Height) / 2);
                if (BtnDelete != null)
                    BtnDelete.Location = new Point(BtnRestore.Left - BtnDelete.Width - 8, (Height - BtnDelete.Height) / 2);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            SmoothPanel parentScroll = Parent as SmoothPanel;
            if (parentScroll != null) parentScroll.ForwardMouseWheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.DrawElevatedCard(g, rect, 10f, _hover, _isBaseline ? Theme.Alpha(Theme.AccentSafe, 95) : Color.Empty);

            Theme.DrawVectorIcon(g, "vault", new RectangleF(14f, (Height - 20f) / 2f, 20f, 20f),
                _isBaseline ? Theme.AccentSafe : Theme.AccentPrimary);

            float rightButtonsLeft = (BtnDelete != null ? BtnDelete.Left : BtnRestore.Left);

            // Right badges: TweakStates count + Tracked Backup Items count
            float badgeX = rightButtonsLeft - 12f;
            int itemCount = Snapshot.Items != null ? Snapshot.Items.Count : 0;
            string itemBadge = itemCount + (itemCount == 1 ? " ITEM" : " ITEMS");
            float iw = Theme.MeasurePillWidth(g, itemBadge, Theme.FontBadge, 12);
            badgeX -= iw;
            Theme.DrawPillBadge(g, itemBadge, Theme.FontBadge,
                Theme.BgRecessed, Theme.BorderSubtle, Theme.TextSecondary,
                badgeX, (Height - 20f) / 2f, 20f, 12);

            if (Snapshot.TweakStates != null && Snapshot.TweakStates.Count > 0)
            {
                string twBadge = Snapshot.TweakStates.Count + " TWEAKS";
                float tw = Theme.MeasurePillWidth(g, twBadge, Theme.FontBadge, 12);
                badgeX -= (tw + 6f);
                Theme.DrawPillBadge(g, twBadge, Theme.FontBadge,
                    Theme.Alpha(Theme.AccentPrimary, 20), Theme.Alpha(Theme.AccentPrimary, 90), Theme.AccentPrimary,
                    badgeX, (Height - 20f) / 2f, 20f, 12);
            }

            float textRight = badgeX - 10f;
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(Snapshot.Label, Theme.FontCardTitle, tb, new RectangleF(46f, 12f, Math.Max(120f, textRight - 46f), 20f), sf);
            }

            string meta = string.Format("ID: {0}   •   Captured: {1}", Snapshot.SnapshotId, Snapshot.CreatedAtUtc);
            using (SolidBrush mb = new SolidBrush(Theme.TextMuted))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(meta, Theme.FontMonoSmall, mb, new RectangleF(46f, 36f, Math.Max(120f, textRight - 46f), 18f), sf);
            }
        }
    }

    // =========================================================================
    // 13. SLEEK DARK SEARCH INPUT BOX
    // =========================================================================
    public class DarkSearchBox : Control
    {
        private readonly TextBox _input;
        private readonly Label _placeholder;
        private readonly Label _btnClear;
        private bool _focused;

        public event EventHandler SearchTextChanged;

        public string SearchText
        {
            get { return _input.Text; }
            set { _input.Text = value ?? ""; }
        }

        public DarkSearchBox(string placeholderText)
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(280, 34);
            BackColor = Color.Transparent;

            _input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Location = new Point(32, 9),
                Width = 216,
                Visible = false
            };

            _placeholder = new Label
            {
                Text = placeholderText,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontBody,
                Location = new Point(32, 8),
                AutoSize = true,
                Cursor = Cursors.IBeam
            };
            _placeholder.Click += (s, e) => ActivateInput();

            _btnClear = new Label
            {
                Text = "×",
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextMuted,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnClear.Click += (s, e) =>
            {
                _input.Text = "";
                ActivateInput();
            };

            _input.GotFocus += (s, e) => { _focused = true; _placeholder.Visible = false; Invalidate(); };
            _input.LostFocus += (s, e) =>
            {
                _focused = false;
                if (string.IsNullOrEmpty(_input.Text))
                {
                    _input.Visible = false;
                    _placeholder.Visible = true;
                }
                Invalidate();
            };
            _input.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrEmpty(_input.Text);
                _placeholder.Visible = !hasText && !_focused;
                _btnClear.Visible = hasText;
                if (SearchTextChanged != null) SearchTextChanged(this, EventArgs.Empty);
            };

            Controls.Add(_input);
            Controls.Add(_placeholder);
            Controls.Add(_btnClear);
            _placeholder.BringToFront();
            _btnClear.BringToFront();
        }

        private void ActivateInput()
        {
            _placeholder.Visible = false;
            _input.Visible = true;
            _input.Focus();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_input == null) return;
            _input.SetBounds(32, 9, Math.Max(40, Width - 60), 18);
            _placeholder.Location = new Point(32, 8);
            _btnClear.Location = new Point(Width - 26, 7);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ActivateInput();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (SolidBrush bg = new SolidBrush(Theme.BgInput))
                Theme.FillRoundedRect(g, bg, rect, 7f);
            using (Pen border = new Pen(_focused ? Theme.AccentPrimary : Theme.BorderSubtle, 1f))
                Theme.DrawRoundedRect(g, border, rect, 7f);

            Theme.DrawVectorIcon(g, "search", new RectangleF(10f, (Height - 13f) / 2f, 13f, 13f),
                _focused ? Theme.AccentPrimary : Theme.TextMuted);
        }
    }
}
