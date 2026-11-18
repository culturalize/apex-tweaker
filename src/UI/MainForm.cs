using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using PCTweaker.Core;

namespace PCTweaker.UI
{
    /// <summary>
    /// Stage 2 Tactical Hardware Telemetry UI Shell.
    /// Implements Section 4 of implementation_plan.md on top of Argon's decoupled TweakerController.
    /// </summary>
    public class MainForm : Form
    {
        private readonly TweakerController _controller;
        private readonly System.Windows.Forms.Timer _telemetryTimer;

        private int _activeTabIndex;
        private int _selectedCategoryFilter = -1; // -1 = All, -2 = Recommended, 0..5 = TweakCategory
        private bool _isBusy;

        // Core Shell Regions
        private SmoothPanel _sidebar;
        private SmoothPanel _topBar;
        private SmoothPanel _bottomBar;
        private SmoothPanel _viewportHost;

        // Sidebar Navigation
        private readonly List<NavRailButton> _navButtons = new List<NavRailButton>();
        private SmoothPanel _sidebarBrandPanel;
        private SmoothPanel _sidebarFooterPanel;
        private TacticalButton _btnElevateSidebar;

        // Top Telemetry Bar
        private TelemetryMetricCard _metricCpu;
        private TelemetryMetricCard _metricRam;
        private TelemetryMetricCard _metricTimer;
        private TelemetryMetricCard _metricScore;
        private TacticalButton _btnQuickPurgeHeader;

        // Bottom Status Bar
        private Label _lblToastStatus;
        private Label _lblBottomApplied;
        private TacticalButton _btnBottomRevertAll;

        // 6 View Containers
        private SmoothPanel _viewDashboard;
        private SmoothPanel _viewTweaks;
        private SmoothPanel _viewBooster;
        private SmoothPanel _viewGames;
        private SmoothPanel _viewVault;
        private SmoothPanel _viewAudit;
        private Control[] _views;

        // View 1: Dashboard controls
        private SmoothPanel _dashHeroCard;
        private RadialScoreGauge _dashScoreGauge;
        private PresetHeroCard _presetPotato;
        private PresetHeroCard _presetCompetitive;
        private PresetHeroCard _presetBalanced;
        private PresetHeroCard _presetRevert;
        private SmoothPanel _dashTrustBanner;

        // View 2: Tweaks Explorer controls
        private SmoothPanel _tweaksHeaderPanel;
        private SmoothPanel _tweaksFilterStrip;
        private readonly List<FilterPillButton> _filterPills = new List<FilterPillButton>();
        private DarkSearchBox _searchTweaksBox;
        private Label _lblTweaksCountSummary;
        private TacticalButton _btnApplyVisibleCategory;
        private TacticalButton _btnReauditTweaks;
        private SmoothPanel _tweaksScrollPanel;
        private readonly List<TweakItemCard> _tweakCards = new List<TweakItemCard>();

        // View 3: Booster & RAM Cleaner controls
        private SmoothPanel _boostLeftCard;
        private SmoothPanel _boostRightCard;
        private SmoothPanel _boostConsoleCard;
        private TacticalButton _btnHeroBoostToggle;
        private StackedMemoryBar _memoryBar;
        private TacticalButton _btnPurgeStandby;
        private TacticalButton _btnCleanTemp;
        private TacticalButton _btnCleanShaders;
        private TextBox _txtBoostConsole;

        // View 4: Game Config Optimizer controls
        private SmoothPanel _gamesHeaderPanel;
        private DarkSearchBox _searchGamesBox;
        private Label _lblGamesSummary;
        private SmoothPanel _gamesScrollPanel;
        private readonly List<GameOptimizerCard> _gameCards = new List<GameOptimizerCard>();
        private TextBox _txtGameConsole;

        // View 5: Safety Vault controls
        private SmoothPanel _vaultHeaderPanel;
        private SmoothPanel _vaultSnapshotsScroll;
        private readonly List<SnapshotItemCard> _snapshotCards = new List<SnapshotItemCard>();
        private TextBox _txtVaultAuditLog;

        // View 6: Diagnostics controls
        private SmoothPanel _auditHeaderPanel;
        private TextBox _txtDiagnosticReport;

        public MainForm(TweakerController controller)
        {
            _controller = controller;

            Text = "Apex Tweaker — Gaming & Low-Spec Performance Suite";
            Size = new Size(1260, 830);
            MinimumSize = new Size(1060, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Icon appIcon = Theme.CreateAppIcon();
            if (appIcon != null) Icon = appIcon;
            BackColor = Theme.BgCanvas;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            BuildShell();
            BuildDashboardView();
            BuildTweaksView();
            BuildBoosterView();
            BuildGamesView();
            BuildVaultView();
            BuildAuditView();

            _views = new Control[] { _viewDashboard, _viewTweaks, _viewBooster, _viewGames, _viewVault, _viewAudit };
            foreach (Control v in _views)
            {
                v.Dock = DockStyle.Fill;
                v.Visible = false;
                _viewportHost.Controls.Add(v);
            }

            _controller.StatusMessageChanged += OnControllerStatusChanged;
            _controller.StateChanged += OnControllerStateChanged;

            _telemetryTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _telemetryTimer.Tick += (s, e) => UpdateTelemetryDisplay();
            _telemetryTimer.Start();

            SwitchView(0);
            RefreshAllViews();
            UpdateTelemetryDisplay();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(Handle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_telemetryTimer != null) _telemetryTimer.Dispose();
                _controller.StatusMessageChanged -= OnControllerStatusChanged;
                _controller.StateChanged -= OnControllerStateChanged;
            }
            base.Dispose(disposing);
        }

        private void OnControllerStatusChanged(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<string>)OnControllerStatusChanged, msg);
                return;
            }
            if (_lblToastStatus != null)
                _lblToastStatus.Text = msg;
        }

        private void OnControllerStateChanged()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)OnControllerStateChanged);
                return;
            }
            RefreshAllViews();
        }

        private void RunAsyncAction(TacticalButton triggerButton, string workingStatus, Action work, Action onCompletedUi = null)
        {
            if (_isBusy) return;
            _isBusy = true;
            if (triggerButton != null)
            {
                triggerButton.IsBusy = true;
                triggerButton.Enabled = false;
                triggerButton.Invalidate();
            }
            if (_lblToastStatus != null) _lblToastStatus.Text = workingStatus;

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    work();
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            _isBusy = false;
                            if (triggerButton != null && !triggerButton.IsDisposed)
                            {
                                triggerButton.IsBusy = false;
                                triggerButton.Enabled = true;
                                triggerButton.Invalidate();
                            }
                            if (onCompletedUi != null) onCompletedUi();
                            RefreshAllViews();
                            UpdateTelemetryDisplay();
                        }));
                    }
                }
            });
        }

        // =========================================================================
        // CORE APPLICATION SHELL (SIDEBAR, TOP TELEMETRY BAR, BOTTOM TOAST BAR)
        // =========================================================================
        private void BuildShell()
        {
            // 1. Left Navigation Rail (238px fixed)
            _sidebar = new SmoothPanel
            {
                Dock = DockStyle.Left,
                Width = 238,
                BackColor = Theme.BgSidebar
            };
            _sidebar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            };

            // Sidebar Brand Header
            _sidebarBrandPanel = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = Theme.BgSidebar
            };
            _sidebarBrandPanel.Paint += PaintSidebarBrand;

            // Sidebar Nav Stack
            SmoothPanel navListPanel = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgSidebar,
                Padding = new Padding(12, 8, 12, 8)
            };

            string[,] navSpecs = new string[,]
            {
                { "Dashboard & Boost", "dashboard" },
                { "System Tweaks", "tweaks" },
                { "Game Booster & RAM", "booster" },
                { "Game Configs", "games" },
                { "Safety Vault", "vault" },
                { "Hardware & Audit", "audit" }
            };

            for (int i = 0; i < navSpecs.GetLength(0); i++)
            {
                int tabIdx = i;
                NavRailButton btn = new NavRailButton
                {
                    Text = navSpecs[i, 0],
                    IconKey = navSpecs[i, 1],
                    Location = new Point(12, 8 + i * 48),
                    Size = new Size(213, 42)
                };
                btn.Click += (s, e) => SwitchView(tabIdx);
                _navButtons.Add(btn);
                navListPanel.Controls.Add(btn);
            }

            // Sidebar Bottom Footer (Anti-Cheat Safe + Admin Privilege Pill)
            _sidebarFooterPanel = new SmoothPanel
            {
                Dock = DockStyle.Bottom,
                Height = _controller.Hardware.IsRunningAsAdmin ? 104 : 144,
                BackColor = Theme.BgSidebar
            };
            _sidebarFooterPanel.Paint += PaintSidebarFooter;

            if (!_controller.Hardware.IsRunningAsAdmin)
            {
                _btnElevateSidebar = new TacticalButton
                {
                    Text = "Run as Administrator",
                    IconKey = "shield",
                    ButtonStyle = TacticalButtonStyle.Warning,
                    Font = Theme.FontSmallBold,
                    Location = new Point(14, 94),
                    Size = new Size(209, 34)
                };
                _btnElevateSidebar.Click += (s, e) =>
                {
                    if (_controller.RelaunchAsAdministrator())
                        Close();
                };
                _sidebarFooterPanel.Controls.Add(_btnElevateSidebar);
            }

            _sidebar.Controls.Add(navListPanel);
            _sidebar.Controls.Add(_sidebarBrandPanel);
            _sidebar.Controls.Add(_sidebarFooterPanel);

            // 2. Right Workspace Wrapper
            SmoothPanel rightContainer = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas
            };

            // Persistent Top Telemetry Header Bar (80px)
            _topBar = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Theme.BgHeader
            };
            _topBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };
            _topBar.Resize += (s, e) => LayoutTopBar();

            _metricCpu = new TelemetryMetricCard { MetricTitle = "CPU LOAD", AccentColor = Theme.AccentPrimary };
            _metricRam = new TelemetryMetricCard { MetricTitle = "PHYSICAL RAM", AccentColor = Theme.AccentSafe };
            _metricTimer = new TelemetryMetricCard { MetricTitle = "KERNEL TIMER", AccentColor = Theme.AccentIndigo };
            _metricScore = new TelemetryMetricCard { MetricTitle = "OPT SCORE", AccentColor = Theme.AccentWarning };

            _btnQuickPurgeHeader = new TacticalButton
            {
                Text = "Quick Purge RAM",
                IconKey = "ram",
                ButtonStyle = TacticalButtonStyle.Primary,
                Font = Theme.FontSmallBold,
                Size = new Size(152, 42)
            };
            _btnQuickPurgeHeader.Click += (s, e) =>
            {
                RunAsyncAction(_btnQuickPurgeHeader, "Purging working sets & standby memory...", () =>
                {
                    MemoryPurgeResult res = _controller.PurgeMemory(true);
                    BeginInvoke((Action)(() => AppendBoostConsole(res.SummaryMessage)));
                });
            };

            _topBar.Controls.Add(_metricCpu);
            _topBar.Controls.Add(_metricRam);
            _topBar.Controls.Add(_metricTimer);
            _topBar.Controls.Add(_metricScore);
            _topBar.Controls.Add(_btnQuickPurgeHeader);

            // Persistent Bottom Status & Transaction Bar (38px)
            _bottomBar = new SmoothPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                BackColor = Theme.BgSidebar
            };
            _bottomBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, 0, 0, _bottomBar.Width, 0);
                using (SolidBrush dot = new SolidBrush(_isBusy ? Theme.AccentWarning : Theme.AccentSafe))
                    e.Graphics.FillEllipse(dot, 16, 15, 8, 8);
            };
            _bottomBar.Resize += (s, e) => LayoutBottomBar();

            _lblToastStatus = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = _controller.LastStatusMessage
            };

            _lblBottomApplied = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontMonoSmall,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "0/36 ACTIVE"
            };

            _btnBottomRevertAll = new TacticalButton
            {
                Text = "Revert All",
                ButtonStyle = TacticalButtonStyle.Danger,
                Font = Theme.FontBadge,
                Size = new Size(88, 24),
                CornerRadius = 5f
            };
            _btnBottomRevertAll.Click += (s, e) => ConfirmAndRevertAll(_btnBottomRevertAll);

            _bottomBar.Controls.Add(_lblToastStatus);
            _bottomBar.Controls.Add(_lblBottomApplied);
            _bottomBar.Controls.Add(_btnBottomRevertAll);

            // Center Viewport Host
            _viewportHost = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas
            };

            rightContainer.Controls.Add(_viewportHost);
            rightContainer.Controls.Add(_topBar);
            rightContainer.Controls.Add(_bottomBar);

            Controls.Add(rightContainer);
            Controls.Add(_sidebar);

            LayoutTopBar();
            LayoutBottomBar();
        }

        private void PaintSidebarBrand(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Theme.DrawVectorIcon(g, "logo", new RectangleF(16, 18, 34, 34), Theme.AccentPrimary);

            using (SolidBrush titleBr = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush subBr = new SolidBrush(Theme.AccentPrimary))
            {
                g.DrawString("APEX TWEAKER", Theme.FontBrand, titleBr, 58, 17);
                g.DrawString("FPS & LATENCY ENGINE", Theme.FontBadge, subBr, 60, 39);
            }

            // Detected Hardware Tier Pill
            string tierShort;
            Color tierColor;
            switch (_controller.Hardware.DetectedTier)
            {
                case SystemTier.PotatoLowSpec:
                    tierShort = "TIER 1 • POTATO RESCUE";
                    tierColor = Theme.AccentWarning;
                    break;
                case SystemTier.CompetitiveEsports:
                    tierShort = "TIER 3 • COMPETITIVE RIG";
                    tierColor = Theme.AccentPrimary;
                    break;
                default:
                    tierShort = "TIER 2 • MID-RANGE GAMER";
                    tierColor = Theme.AccentSafe;
                    break;
            }

            Theme.DrawPillBadge(g, tierShort, Theme.FontBadge,
                Theme.Alpha(tierColor, 20), Theme.Alpha(tierColor, 100), tierColor,
                16f, 68f, 22f, 16);

            using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                g.DrawLine(div, 16, _sidebarBrandPanel.Height - 1, _sidebarBrandPanel.Width - 17, _sidebarBrandPanel.Height - 1);
        }

        private void PaintSidebarFooter(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                g.DrawLine(div, 16, 0, _sidebarFooterPanel.Width - 17, 0);

            // Anti-Cheat Safe Badge Box
            RectangleF acRect = new RectangleF(14, 12, 209, 44);
            using (SolidBrush bg = new SolidBrush(Theme.Alpha(Theme.AccentSafe, 16)))
                Theme.FillRoundedRect(g, bg, acRect, 7f);
            using (Pen b = new Pen(Theme.Alpha(Theme.AccentSafe, 85), 1f))
                Theme.DrawRoundedRect(g, b, acRect, 7f);

            Theme.DrawVectorIcon(g, "shield", new RectangleF(24, 24, 18, 18), Theme.AccentSafe);
            using (SolidBrush safeBr = new SolidBrush(Theme.AccentSafe))
            using (SolidBrush mutedBr = new SolidBrush(Theme.TextMuted))
            {
                g.DrawString("100% ANTI-CHEAT SAFE", Theme.FontBadge, safeBr, 48, 18);
                g.DrawString("EAC • BattlEye • Vanguard", Theme.FontSmall, mutedBr, 48, 32);
            }

            // Admin Privilege Status Pill
            bool isAdmin = _controller.Hardware.IsRunningAsAdmin;
            Color admCol = isAdmin ? Theme.AccentSafe : Theme.AccentWarning;
            string admTxt = isAdmin ? "● ADMIN PRIVILEGES ACTIVE" : "● STANDARD USER (LIMITED)";
            using (SolidBrush br = new SolidBrush(admCol))
                g.DrawString(admTxt, Theme.FontBadge, br, 16, 68);
        }

        private void LayoutTopBar()
        {
            if (_topBar == null || _metricCpu == null) return;
            int pad = 16;
            int btnW = 152;
            int gap = 10;
            int availW = _topBar.ClientSize.Width - pad * 2 - btnW - gap;
            int cardW = Math.Max(130, (availW - gap * 3) / 4);
            int y = 11;

            _metricCpu.SetBounds(pad, y, cardW, 58);
            _metricRam.SetBounds(pad + (cardW + gap) * 1, y, cardW, 58);
            _metricTimer.SetBounds(pad + (cardW + gap) * 2, y, cardW, 58);
            _metricScore.SetBounds(pad + (cardW + gap) * 3, y, cardW, 58);
            _btnQuickPurgeHeader.SetBounds(_topBar.ClientSize.Width - pad - btnW, 19, btnW, 42);
        }

        private void LayoutBottomBar()
        {
            if (_bottomBar == null || _lblToastStatus == null) return;
            int w = _bottomBar.ClientSize.Width;
            _btnBottomRevertAll.Location = new Point(w - 104, 7);
            _lblBottomApplied.SetBounds(w - 282, 8, 168, 22);
            _lblToastStatus.SetBounds(32, 8, Math.Max(120, w - 322), 22);
        }

        private void SwitchView(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _navButtons.Count; i++)
                _navButtons[i].IsActive = (i == index);
            for (int i = 0; i < _views.Length; i++)
                _views[i].Visible = (i == index);

            if (index == 0) LayoutDashboardView();
            else if (index == 1) LayoutTweaksView();
            else if (index == 2) LayoutBoosterView();
            else if (index == 3) LayoutGamesView();
            else if (index == 4) LayoutVaultView();
        }

        // =========================================================================
        // VIEW 1: DASHBOARD & 1-CLICK QUICK BOOST
        // =========================================================================
        private void BuildDashboardView()
        {
            _viewDashboard = new SmoothPanel
            {
                BackColor = Theme.BgCanvas,
                AutoScroll = true,
                Padding = new Padding(22, 18, 22, 22)
            };
            _viewDashboard.Resize += (s, e) => LayoutDashboardView();

            // Hero Hardware & Score Banner Card
            _dashHeroCard = new SmoothPanel
            {
                BackColor = Color.Transparent,
                Height = 198
            };
            _dashHeroCard.Paint += PaintDashboardHeroCard;

            _dashScoreGauge = new RadialScoreGauge
            {
                Size = new Size(164, 164)
            };
            _dashHeroCard.Controls.Add(_dashScoreGauge);

            // 4 Preset Hero Cards
            _presetPotato = new PresetHeroCard
            {
                TierTag = "TIER 1 • POTATO RESCUE",
                Title = "Potato PC Rescue (Max FPS & VRAM Saver)",
                Description = "Best for low-spec PCs, laptops & older GPUs. Disables DWM transparency & background bloat, consolidates svchost RAM overhead, and stabilizes 1% lows.",
                AccentColor = Theme.AccentWarning
            };
            _presetPotato.ActionButton.ButtonStyle = TacticalButtonStyle.Warning;
            _presetPotato.ActionButton.Click += (s, e) => RunPresetAction(PresetProfile.PotatoRescue, _presetPotato.ActionButton);

            _presetCompetitive = new PresetHeroCard
            {
                TierTag = "TIER 3 • ESPORTS LOW-LATENCY",
                Title = "Competitive FPS & Ultra-Low Latency",
                Description = "Engineered for competitive shooters. Unparks CPU cores, sets Win32PrioritySeparation (0x26), optimizes MMCSS, disables Nagle & enforces 1:1 mouse aim.",
                AccentColor = Theme.AccentPrimary
            };
            _presetCompetitive.ActionButton.ButtonStyle = TacticalButtonStyle.Primary;
            _presetCompetitive.ActionButton.Click += (s, e) => RunPresetAction(PresetProfile.CompetitiveFps, _presetCompetitive.ActionButton);

            _presetBalanced = new PresetHeroCard
            {
                TierTag = "TIER 2 • ZERO-DOWNSIDE SAFE",
                Title = "Balanced Daily Gaming (Safe Defaults)",
                Description = "Applies every zero-downside Safe gaming optimization—GameDVR off, DX10/11 Flip Model, network unthrottling—while keeping Windows Search and visuals intact.",
                AccentColor = Theme.AccentSafe
            };
            _presetBalanced.ActionButton.ButtonStyle = TacticalButtonStyle.Safe;
            _presetBalanced.ActionButton.Click += (s, e) => RunPresetAction(PresetProfile.BalancedSafe, _presetBalanced.ActionButton);

            _presetRevert = new PresetHeroCard
            {
                TierTag = "SAFETY ROLLBACK",
                Title = "Restore Windows Stock Baseline",
                Description = "Instantly reverts all applied tweaks back to your exact pre-tweak values stored in SafetyVault (or clean Windows factory defaults).",
                AccentColor = Theme.AccentDanger
            };
            _presetRevert.ActionButton.Text = "Revert All Applied Tweaks";
            _presetRevert.ActionButton.ButtonStyle = TacticalButtonStyle.Danger;
            _presetRevert.ActionButton.Click += (s, e) => ConfirmAndRevertAll(_presetRevert.ActionButton);

            // Trust Banner
            _dashTrustBanner = new SmoothPanel
            {
                BackColor = Color.Transparent,
                Height = 52
            };
            _dashTrustBanner.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                RectangleF r = new RectangleF(0.5f, 0.5f, _dashTrustBanner.Width - 1.5f, _dashTrustBanner.Height - 1.5f);
                using (SolidBrush bg = new SolidBrush(Theme.BgRecessed))
                    Theme.FillRoundedRect(g, bg, r, 8f);
                using (Pen border = new Pen(Theme.Alpha(Theme.AccentSafe, 70), 1f))
                    Theme.DrawRoundedRect(g, border, r, 8f);

                Theme.DrawVectorIcon(g, "shield", new RectangleF(14, 15, 20, 20), Theme.AccentSafe);
                using (SolidBrush b1 = new SolidBrush(Theme.AccentSafe))
                using (SolidBrush b2 = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString("Kernel Anti-Cheat & Transactional Safety Verified", Theme.FontSmallBold, b1, 44, 9);
                    g.DrawString("Zero process injection, zero input hooks, zero DXGI overlay conflicts. Every tweak saves exact pre-tweak values to SafetyVault.", Theme.FontSmall, b2, 44, 27);
                }
            };

            _viewDashboard.Controls.Add(_dashHeroCard);
            _viewDashboard.Controls.Add(_presetPotato);
            _viewDashboard.Controls.Add(_presetCompetitive);
            _viewDashboard.Controls.Add(_presetBalanced);
            _viewDashboard.Controls.Add(_presetRevert);
            _viewDashboard.Controls.Add(_dashTrustBanner);
        }

        private void PaintDashboardHeroCard(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = _dashHeroCard.Width;
            int h = _dashHeroCard.Height;
            RectangleF rect = new RectangleF(0.5f, 0.5f, w - 1.5f, h - 1.5f);

            using (SolidBrush bg = new SolidBrush(Theme.BgCard))
                Theme.FillRoundedRect(g, bg, rect, 10f);
            using (Pen border = new Pen(Theme.BorderSubtle, 1f))
                Theme.DrawRoundedRect(g, border, rect, 10f);

            // Subtle divider separating hardware specs from Radial Gauge
            float gaugeDivX = w - 208f;
            using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                g.DrawLine(div, gaugeDivX, 18, gaugeDivX, h - 18);

            HardwareProfile hw = _controller.Hardware;
            Color tierColor = hw.DetectedTier == SystemTier.PotatoLowSpec ? Theme.AccentWarning
                            : (hw.DetectedTier == SystemTier.CompetitiveEsports ? Theme.AccentPrimary : Theme.AccentSafe);

            // Top Row: Detected Tier Badge
            float tx = 18f;
            float tierW = Theme.DrawPillBadge(g, hw.TierDisplayName.ToUpperInvariant(), Theme.FontBadge,
                Theme.Alpha(tierColor, 24), Theme.Alpha(tierColor, 110), tierColor, tx, 14f, 21f, 16);

            string chassisBadge = hw.IsLaptop ? "LAPTOP CHASSIS" : "DESKTOP CHASSIS";
            Theme.DrawPillBadge(g, chassisBadge, Theme.FontBadge,
                Theme.BgRecessed, Theme.BorderSubtle, Theme.TextSecondary, tx + tierW + 8f, 14f, 21f, 12);

            // 2x2 Hardware Specs Matrix
            float leftAreaW = gaugeDivX - 36f;
            float colW = (leftAreaW - 14f) / 2f;
            DrawSpecTile(g, 18f, 46f, colW, "PROCESSOR", hw.CpuName,
                string.Format("{0} Cores / {1} Threads  •  {2} MHz", hw.PhysicalCores, hw.LogicalThreads, hw.MaxClockMhz));
            DrawSpecTile(g, 18f + colW + 14f, 46f, colW, "GRAPHICS ADAPTER", hw.GpuName,
                string.Format("{0:N0} MB VRAM  •  HAGS: {1}  •  Drv {2}", hw.GpuVramMb, hw.IsHagsEnabled ? "ON" : "OFF", hw.GpuDriverVersion));
            DrawSpecTile(g, 18f, 102f, colW, "MEMORY & STORAGE", string.Format("{0:F1} GB Physical RAM  •  {1}", hw.TotalRamMb / 1024.0, hw.PrimaryDiskType),
                string.Format("{0:N0} MB Total System Memory", hw.TotalRamMb));
            DrawSpecTile(g, 18f + colW + 14f, 102f, colW, "OPERATING SYSTEM", hw.OsCaption,
                string.Format("Windows Build {0}  •  {1}", hw.OsBuild, hw.IsRunningAsAdmin ? "Elevated Admin" : "Standard User"));

            // Strategy Summary Callout Strip
            RectangleF stratRect = new RectangleF(18f, 156f, leftAreaW, 30f);
            using (SolidBrush sbg = new SolidBrush(Theme.BgRecessed))
                Theme.FillRoundedRect(g, sbg, stratRect, 6f);
            using (SolidBrush lbl = new SolidBrush(Theme.AccentPrimary))
            using (SolidBrush txt = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString("STRATEGY:", Theme.FontBadge, lbl, new RectangleF(stratRect.X + 10, stratRect.Y, 68, stratRect.Height), sf);
                g.DrawString(hw.TierRecommendationSummary, Theme.FontSmall, txt, new RectangleF(stratRect.X + 76, stratRect.Y, stratRect.Width - 84, stratRect.Height), sf);
            }
        }

        private void DrawSpecTile(Graphics g, float x, float y, float w, string label, string value, string sub)
        {
            using (SolidBrush lb = new SolidBrush(Theme.TextMuted))
            using (SolidBrush vb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(label, Theme.FontBadge, lb, new RectangleF(x, y, w, 14f), sf);
                g.DrawString(value, Theme.FontBodyBold, vb, new RectangleF(x, y + 14f, w, 18f), sf);
                g.DrawString(sub, Theme.FontMonoSmall, sb, new RectangleF(x, y + 32f, w, 16f), sf);
            }
        }

        private void LayoutDashboardView()
        {
            if (_viewDashboard == null || _dashHeroCard == null) return;
            int pad = 20;
            int contentW = Math.Max(680, _viewDashboard.ClientSize.Width - pad * 2 - 6);
            int y = 18;

            _dashHeroCard.SetBounds(pad, y, contentW, 198);
            _dashScoreGauge.Location = new Point(_dashHeroCard.Width - 186, 17);
            y += _dashHeroCard.Height + 16;

            int gap = 14;
            int cardW = (contentW - gap) / 2;
            int cardH = 176;

            _presetPotato.SetBounds(pad, y, cardW, cardH);
            _presetCompetitive.SetBounds(pad + cardW + gap, y, cardW, cardH);
            y += cardH + gap;

            _presetBalanced.SetBounds(pad, y, cardW, cardH);
            _presetRevert.SetBounds(pad + cardW + gap, y, cardW, cardH);
            y += cardH + 16;

            _dashTrustBanner.SetBounds(pad, y, contentW, 52);
        }

        private void RunPresetAction(PresetProfile preset, TacticalButton triggerBtn)
        {
            RunAsyncAction(triggerBtn, "Applying " + preset + " preset (auto-saving safety snapshot)...", () =>
            {
                string summary = _controller.ApplyPreset(preset);
                BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, summary, "Preset Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }

        private void ConfirmAndRevertAll(TacticalButton triggerBtn)
        {
            DialogResult dr = MessageBox.Show(this,
                "Revert all currently applied tweaks back to your pre-tweak SafetyVault baseline / Windows defaults?",
                "Confirm Full Rollback",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            RunAsyncAction(triggerBtn, "Reverting all active tweaks to baseline...", () =>
            {
                string summary = _controller.RevertAllTweaks();
                BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, summary, "Rollback Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }

        // =========================================================================
        // VIEW 2: SYSTEM TWEAKS EXPLORER (36 EXPANDABLE CARDS)
        // =========================================================================
        private void BuildTweaksView()
        {
            _viewTweaks = new SmoothPanel { BackColor = Theme.BgCanvas };
            _viewTweaks.Resize += (s, e) => LayoutTweaksView();

            _tweaksHeaderPanel = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 132,
                BackColor = Theme.BgCanvas
            };
            _tweaksHeaderPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString("System Tweaks Explorer", Theme.FontPageTitle, tb, 20, 10);
                    g.DrawString("Toggle individual Windows, GPU, memory, network & input optimizations — click 'Technical Details' on any card to inspect exact keys.", Theme.FontSmall, sb, 20, 36);
                }
                using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                    g.DrawLine(div, 20, _tweaksHeaderPanel.Height - 1, _tweaksHeaderPanel.Width - 20, _tweaksHeaderPanel.Height - 1);
            };

            _btnApplyVisibleCategory = new TacticalButton
            {
                Text = "Apply Visible",
                ButtonStyle = TacticalButtonStyle.Primary,
                Font = Theme.FontSmallBold,
                Size = new Size(124, 32)
            };
            _btnApplyVisibleCategory.Click += (s, e) => ApplyAllVisibleTweaks();

            _btnReauditTweaks = new TacticalButton
            {
                Text = "Re-Audit",
                IconKey = "refresh",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Font = Theme.FontSmallBold,
                Size = new Size(106, 32)
            };
            _btnReauditTweaks.Click += (s, e) =>
            {
                RunAsyncAction(_btnReauditTweaks, "Re-auditing live state of all 36 tweaks...", () =>
                {
                    _controller.AuditAllTweakStates();
                });
            };

            // Filter Pills Strip
            _tweaksFilterStrip = new SmoothPanel
            {
                Location = new Point(20, 56),
                Height = 34,
                BackColor = Color.Transparent
            };

            AddFilterPill("All (" + _controller.Tweaks.Count + ")", -1, true);
            AddFilterPill("★ Recommended", -2, false);
            AddFilterPill("CPU & Power", (int)TweakCategory.CpuAndPower, false);
            AddFilterPill("GPU & Display", (int)TweakCategory.GpuAndDisplay, false);
            AddFilterPill("Memory & Storage", (int)TweakCategory.MemoryAndStorage, false);
            AddFilterPill("Network & Ping", (int)TweakCategory.NetworkAndPing, false);
            AddFilterPill("Input & Mouse", (int)TweakCategory.InputAndMouse, false);
            AddFilterPill("Bloat & Services", (int)TweakCategory.BloatAndServices, false);

            _searchTweaksBox = new DarkSearchBox("Search tweaks by name, symptom, or registry path...")
            {
                Location = new Point(20, 92),
                Size = new Size(380, 32)
            };
            _searchTweaksBox.SearchTextChanged += (s, e) => FilterAndLayoutTweakCards();

            _lblTweaksCountSummary = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontMonoSmall,
                TextAlign = ContentAlignment.MiddleRight,
                Height = 24
            };

            _tweaksHeaderPanel.Controls.Add(_btnApplyVisibleCategory);
            _tweaksHeaderPanel.Controls.Add(_btnReauditTweaks);
            _tweaksHeaderPanel.Controls.Add(_tweaksFilterStrip);
            _tweaksHeaderPanel.Controls.Add(_searchTweaksBox);
            _tweaksHeaderPanel.Controls.Add(_lblTweaksCountSummary);

            // Scrollable Cards Panel
            _tweaksScrollPanel = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas,
                AutoScroll = true
            };
            _tweaksScrollPanel.Resize += (s, e) => ReflowTweakCardsLayout();

            // Pre-instantiate all 36 cards ONCE for zero flicker & instant filtering
            foreach (TweakDefinition tweak in _controller.Tweaks)
            {
                TweakItemCard card = new TweakItemCard(tweak, _controller.Hardware);
                card.ToggleRequested += OnTweakCardToggleRequested;
                card.ExpandChanged += (c) => ReflowTweakCardsLayout();
                _tweakCards.Add(card);
                _tweaksScrollPanel.Controls.Add(card);
            }

            _viewTweaks.Controls.Add(_tweaksScrollPanel);
            _viewTweaks.Controls.Add(_tweaksHeaderPanel);
        }

        private void AddFilterPill(string text, int filterTag, bool active)
        {
            FilterPillButton pill = new FilterPillButton
            {
                Text = text,
                Tag = filterTag,
                IsActive = active
            };
            pill.FitWidthToText();
            pill.Click += (s, e) =>
            {
                _selectedCategoryFilter = (int)pill.Tag;
                foreach (FilterPillButton p in _filterPills)
                    p.IsActive = ((int)p.Tag == _selectedCategoryFilter);
                FilterAndLayoutTweakCards();
            };
            _filterPills.Add(pill);
            _tweaksFilterStrip.Controls.Add(pill);
        }

        private void OnTweakCardToggleRequested(TweakDefinition tweak)
        {
            if (tweak.RequiresAdmin && !_controller.Hardware.IsRunningAsAdmin)
            {
                DialogResult dr = MessageBox.Show(this,
                    "'" + tweak.Title + "' modifies machine-level Windows settings (HKLM / Services / BCD) and requires Administrator privileges.\r\n\r\nRelaunch Apex Tweaker as Administrator now?",
                    "Administrator Privileges Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (dr == DialogResult.Yes && _controller.RelaunchAsAdministrator())
                    Close();
                return;
            }

            TweakOperationResult res = _controller.ToggleTweak(tweak.Id);
            if (!res.Success)
            {
                MessageBox.Show(this, res.Message, "Tweak Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyAllVisibleTweaks()
        {
            List<TweakDefinition> targets = new List<TweakDefinition>();
            foreach (TweakItemCard card in _tweakCards)
            {
                if (card.Visible && card.Tweak.CurrentStatus != TweakStatus.Applied)
                    targets.Add(card.Tweak);
            }
            if (targets.Count == 0)
            {
                OnControllerStatusChanged("All tweaks in the current filter are already applied.");
                return;
            }

            RunAsyncAction(_btnApplyVisibleCategory, "Applying " + targets.Count + " tweaks in visible category...", () =>
            {
                _controller.CreateManualSnapshot("Auto-Snapshot Before Category Batch (" + targets.Count + " tweaks)");
                int okCount = 0;
                int adminSkip = 0;
                foreach (TweakDefinition t in targets)
                {
                    if (t.RequiresAdmin && !_controller.Hardware.IsRunningAsAdmin)
                    {
                        adminSkip++;
                        continue;
                    }
                    if (_controller.ApplyTweak(t.Id).Success) okCount++;
                }
                string msg = string.Format("Category batch complete: {0} applied{1}.",
                    okCount, adminSkip > 0 ? (", " + adminSkip + " skipped — Admin needed") : "");
                BeginInvoke((Action)(() => OnControllerStatusChanged(msg)));
            });
        }

        private void LayoutTweaksView()
        {
            if (_tweaksHeaderPanel == null) return;
            int w = _tweaksHeaderPanel.ClientSize.Width;

            _btnReauditTweaks.Location = new Point(w - 20 - _btnReauditTweaks.Width, 12);
            _btnApplyVisibleCategory.Location = new Point(_btnReauditTweaks.Left - 8 - _btnApplyVisibleCategory.Width, 12);

            _tweaksFilterStrip.Width = Math.Max(400, w - 40);
            int px = 0;
            foreach (FilterPillButton pill in _filterPills)
            {
                pill.Location = new Point(px, 2);
                px += pill.Width + 6;
            }

            _searchTweaksBox.Width = Math.Min(420, Math.Max(240, w - 340));
            _lblTweaksCountSummary.SetBounds(_searchTweaksBox.Right + 12, 96, Math.Max(140, w - _searchTweaksBox.Right - 32), 24);

            ReflowTweakCardsLayout();
        }

        private void FilterAndLayoutTweakCards()
        {
            string query = _searchTweaksBox != null ? (_searchTweaksBox.SearchText ?? "").Trim() : "";
            int visibleCount = 0;
            int visibleApplied = 0;

            _tweaksScrollPanel.SuspendLayout();
            foreach (TweakItemCard card in _tweakCards)
            {
                TweakDefinition t = card.Tweak;
                bool match = true;

                if (_selectedCategoryFilter == -2)
                    match = t.IsRecommendedForTier(_controller.Hardware.DetectedTier);
                else if (_selectedCategoryFilter >= 0)
                    match = ((int)t.Category == _selectedCategoryFilter);

                if (match && !string.IsNullOrEmpty(query))
                {
                    match = (t.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             t.ShortSummary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             t.TechnicalDetails.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             t.WhyItHelpsGaming.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             t.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                card.Visible = match;
                if (match)
                {
                    visibleCount++;
                    if (t.CurrentStatus == TweakStatus.Applied) visibleApplied++;
                }
            }
            _tweaksScrollPanel.ResumeLayout(false);

            if (_lblTweaksCountSummary != null)
                _lblTweaksCountSummary.Text = string.Format("Showing {0} of {1} tweaks  •  {2} applied", visibleCount, _controller.Tweaks.Count, visibleApplied);

            ReflowTweakCardsLayout();
        }

        private void ReflowTweakCardsLayout()
        {
            if (_tweaksScrollPanel == null) return;
            _tweaksScrollPanel.SuspendLayout();

            int scrollY = _tweaksScrollPanel.AutoScrollPosition.Y;
            int padX = 20;
            int cardW = Math.Max(560, _tweaksScrollPanel.ClientSize.Width - padX * 2 - 18);
            int y = 14 + scrollY;

            foreach (TweakItemCard card in _tweakCards)
            {
                if (!card.Visible) continue;
                card.SetBounds(padX, y, cardW, card.Height);
                y += card.Height + 10;
            }

            _tweaksScrollPanel.AutoScrollMinSize = new Size(0, (y - scrollY) + 16);
            _tweaksScrollPanel.ResumeLayout(true);
        }

        // =========================================================================
        // VIEW 3: LIVE GAME BOOSTER & NATIVE RAM CLEANER
        // =========================================================================
        private void BuildBoosterView()
        {
            _viewBooster = new SmoothPanel
            {
                BackColor = Theme.BgCanvas,
                AutoScroll = true
            };
            _viewBooster.Resize += (s, e) => LayoutBoosterView();

            // Left Card: Live Session Game Booster
            _boostLeftCard = new SmoothPanel { BackColor = Color.Transparent };
            _boostLeftCard.Paint += PaintBoostLeftCard;

            _btnHeroBoostToggle = new TacticalButton
            {
                Text = "ENGAGE GAME SESSION BOOST",
                IconKey = "booster",
                Font = Theme.FontHeroButton,
                ButtonStyle = TacticalButtonStyle.SolidPrimary,
                CornerRadius = 8f,
                Height = 52
            };
            _btnHeroBoostToggle.Click += (s, e) =>
            {
                RunAsyncAction(_btnHeroBoostToggle, "Updating live gaming session boost...", () =>
                {
                    GameBoostSessionState st = _controller.ToggleGameBoostSession();
                    BeginInvoke((Action)(() => AppendBoostConsole(st.StatusSummary)));
                });
            };
            _boostLeftCard.Controls.Add(_btnHeroBoostToggle);

            // Right Card: Native ntdll.dll Memory & Shader Cache Cleaner
            _boostRightCard = new SmoothPanel { BackColor = Color.Transparent };
            _boostRightCard.Paint += PaintBoostRightCard;

            _memoryBar = new StackedMemoryBar { Height = 74 };

            _btnPurgeStandby = new TacticalButton
            {
                Text = "Purge Standby List & Background Working Sets (ntdll)",
                IconKey = "ram",
                ButtonStyle = TacticalButtonStyle.Primary,
                Height = 40
            };
            _btnPurgeStandby.Click += (s, e) =>
            {
                RunAsyncAction(_btnPurgeStandby, "Executing native ntdll memory purge...", () =>
                {
                    MemoryPurgeResult res = _controller.PurgeMemory(true);
                    BeginInvoke((Action)(() => AppendBoostConsole(res.SummaryMessage)));
                });
            };

            _btnCleanTemp = new TacticalButton
            {
                Text = "Clean Windows & User Temp Folders",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Height = 36
            };
            _btnCleanTemp.Click += (s, e) =>
            {
                RunAsyncAction(_btnCleanTemp, "Cleaning temporary folders...", () =>
                {
                    CacheCleanResult res = _controller.CleanCaches(false);
                    BeginInvoke((Action)(() => AppendBoostConsole(res.SummaryMessage)));
                });
            };

            _btnCleanShaders = new TacticalButton
            {
                Text = "Clean Temp + DirectX / NVIDIA / AMD Shader Caches",
                ButtonStyle = TacticalButtonStyle.Warning,
                Height = 36
            };
            _btnCleanShaders.Click += (s, e) =>
            {
                RunAsyncAction(_btnCleanShaders, "Cleaning stale shader & temp caches...", () =>
                {
                    CacheCleanResult res = _controller.CleanCaches(true);
                    BeginInvoke((Action)(() => AppendBoostConsole(res.SummaryMessage)));
                });
            };

            _boostRightCard.Controls.Add(_memoryBar);
            _boostRightCard.Controls.Add(_btnPurgeStandby);
            _boostRightCard.Controls.Add(_btnCleanTemp);
            _boostRightCard.Controls.Add(_btnCleanShaders);

            // Bottom Activity Console Card
            _boostConsoleCard = new SmoothPanel { BackColor = Color.Transparent };
            _boostConsoleCard.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                RectangleF r = new RectangleF(0.5f, 0.5f, _boostConsoleCard.Width - 1.5f, _boostConsoleCard.Height - 1.5f);
                using (SolidBrush bg = new SolidBrush(Theme.BgCard))
                    Theme.FillRoundedRect(g, bg, r, 10f);
                using (Pen border = new Pen(Theme.BorderSubtle, 1f))
                    Theme.DrawRoundedRect(g, border, r, 10f);
                using (SolidBrush lb = new SolidBrush(Theme.TextMuted))
                    g.DrawString("BOOSTER & MEMORY ENGINE LOG", Theme.FontBadge, lb, 16, 12);
            };

            _txtBoostConsole = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.BgRecessed,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontMonoSmall,
                ScrollBars = ScrollBars.Vertical,
                Text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] Booster & Native ntdll.dll Memory Engine ready. Click 'Engage Game Session Boost' before launching a game.\r\n"
            };
            _txtBoostConsole.HandleCreated += (s, e) => Theme.ApplyDarkScrollbars(_txtBoostConsole.Handle);
            _boostConsoleCard.Controls.Add(_txtBoostConsole);

            _viewBooster.Controls.Add(_boostLeftCard);
            _viewBooster.Controls.Add(_boostRightCard);
            _viewBooster.Controls.Add(_boostConsoleCard);
        }

        private void PaintBoostLeftCard(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool active = _controller.Booster.SessionState.IsActive;
            Color accent = active ? Theme.AccentSafe : Theme.AccentPrimary;
            RectangleF r = new RectangleF(0.5f, 0.5f, _boostLeftCard.Width - 1.5f, _boostLeftCard.Height - 1.5f);

            using (SolidBrush bg = new SolidBrush(Theme.BgCard))
                Theme.FillRoundedRect(g, bg, r, 10f);
            using (Pen border = new Pen(active ? Theme.Alpha(Theme.AccentSafe, 140) : Theme.BorderSubtle, 1f))
                Theme.DrawRoundedRect(g, border, r, 10f);

            using (SolidBrush stripe = new SolidBrush(accent))
                Theme.FillRoundedRect(g, stripe, new RectangleF(16, 1.5f, 64, 3f), 1.5f);

            Theme.DrawPillBadge(g, active ? "● SESSION BOOST ACTIVE" : "● STANDBY — DESKTOP MODE", Theme.FontBadge,
                Theme.Alpha(accent, 24), Theme.Alpha(accent, 110), accent, 18f, 16f, 20f, 14);

            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                g.DrawString("Live Gaming Session Booster", Theme.FontSectionTitle, tb, 18f, 44f);

            // Checklist items below hero button
            string[] steps = new string[]
            {
                "Activate Ultimate / High Performance power plan (saves current GUID)",
                "Close safe desktop bloat (OneDrive, Widgets, GameBar, Adobe helpers)",
                "Pause background indexing & prefetcher daemons (WSearch, SysMain)",
                "Purge Working Sets & Standby Page List — 100% Anti-Cheat Safe"
            };

            float cy = 148f;
            using (SolidBrush checkBr = new SolidBrush(active ? Theme.AccentSafe : Theme.AccentPrimary))
            using (SolidBrush txtBr = new SolidBrush(Theme.TextSecondary))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                foreach (string step in steps)
                {
                    g.DrawString(active ? "✓" : "▸", Theme.FontBodyBold, checkBr, 20f, cy);
                    g.DrawString(step, Theme.FontSmall, txtBr, new RectangleF(38f, cy + 1f, _boostLeftCard.Width - 54f, 18f), sf);
                    cy += 24f;
                }
            }

            // Session status recessed box
            RectangleF stBox = new RectangleF(18f, _boostLeftCard.Height - 70f, _boostLeftCard.Width - 36f, 52f);
            using (SolidBrush sbg = new SolidBrush(Theme.BgRecessed))
                Theme.FillRoundedRect(g, sbg, stBox, 6f);
            using (SolidBrush sTxt = new SolidBrush(active ? Theme.AccentSafe : Theme.TextMuted))
            using (StringFormat sfWrap = new StringFormat { Trimming = StringTrimming.EllipsisWord })
            {
                g.DrawString(_controller.Booster.SessionState.StatusSummary, Theme.FontMonoSmall, sTxt,
                    new RectangleF(stBox.X + 10, stBox.Y + 8, stBox.Width - 20, stBox.Height - 16), sfWrap);
            }
        }

        private void PaintBoostRightCard(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF r = new RectangleF(0.5f, 0.5f, _boostRightCard.Width - 1.5f, _boostRightCard.Height - 1.5f);
            using (SolidBrush bg = new SolidBrush(Theme.BgCard))
                Theme.FillRoundedRect(g, bg, r, 10f);
            using (Pen border = new Pen(Theme.BorderSubtle, 1f))
                Theme.DrawRoundedRect(g, border, r, 10f);

            using (SolidBrush stripe = new SolidBrush(Theme.AccentSafe))
                Theme.FillRoundedRect(g, stripe, new RectangleF(16, 1.5f, 64, 3f), 1.5f);

            Theme.DrawPillBadge(g, "NATIVE NTDLL.DLL + PSAPI", Theme.FontBadge,
                Theme.Alpha(Theme.AccentSafe, 22), Theme.Alpha(Theme.AccentSafe, 100), Theme.AccentSafe, 18f, 16f, 20f, 14);

            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("Standby Memory & Cache Purger", Theme.FontSectionTitle, tb, 18f, 44f);
                g.DrawString("Reclaims cached Windows Standby List pages & trims background working sets to eliminate mid-game RAM allocation hitches.",
                    Theme.FontSmall, sb, new RectangleF(18f, 162f, _boostRightCard.Width - 36f, 34f));
            }
        }

        private void LayoutBoosterView()
        {
            if (_viewBooster == null || _boostLeftCard == null) return;
            int pad = 20;
            int gap = 16;
            int contentW = Math.Max(680, _viewBooster.ClientSize.Width - pad * 2 - 6);
            int halfW = (contentW - gap) / 2;
            int topH = 342;

            _boostLeftCard.SetBounds(pad, 18, halfW, topH);
            _btnHeroBoostToggle.SetBounds(18, 78, halfW - 36, 52);

            _boostRightCard.SetBounds(pad + halfW + gap, 18, halfW, topH);
            _memoryBar.SetBounds(18, 78, halfW - 36, 74);
            _btnPurgeStandby.SetBounds(18, 202, halfW - 36, 40);
            _btnCleanTemp.SetBounds(18, 250, halfW - 36, 36);
            _btnCleanShaders.SetBounds(18, 294, halfW - 36, 36);

            int consoleY = 18 + topH + 16;
            int consoleH = Math.Max(160, _viewBooster.ClientSize.Height - consoleY - 20);
            _boostConsoleCard.SetBounds(pad, consoleY, contentW, consoleH);
            _txtBoostConsole.SetBounds(14, 34, contentW - 28, consoleH - 46);
        }

        private void AppendBoostConsole(string msg)
        {
            if (_txtBoostConsole == null || _txtBoostConsole.IsDisposed) return;
            _txtBoostConsole.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
        }

        // =========================================================================
        // VIEW 4: GAME CONFIG & ENGINE OPTIMIZER
        // =========================================================================
        private void BuildGamesView()
        {
            _viewGames = new SmoothPanel { BackColor = Theme.BgCanvas };
            _viewGames.Resize += (s, e) => LayoutGamesView();

            _gamesHeaderPanel = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Theme.BgCanvas
            };
            _gamesHeaderPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString("Game Config & Engine Optimizer", Theme.FontPageTitle, tb, 20, 10);
                    g.DrawString("Kernel-native IFEO High CPU/IO Priority (Anti-Cheat Safe), DirectX GPU Preference & Unreal Engine scalability presets with auto .bak backups.", Theme.FontSmall, sb, 20, 36);
                }
                using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                    g.DrawLine(div, 20, _gamesHeaderPanel.Height - 1, _gamesHeaderPanel.Width - 20, _gamesHeaderPanel.Height - 1);
            };

            TacticalButton btnRescan = new TacticalButton
            {
                Text = "Rescan Installed Games",
                IconKey = "refresh",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Font = Theme.FontSmallBold,
                Location = new Point(20, 56),
                Size = new Size(184, 32)
            };
            btnRescan.Click += (s, e) => _controller.RefreshDetectedGames();

            TacticalButton btnAddExe = new TacticalButton
            {
                Text = "+ Browse Custom Game .exe",
                ButtonStyle = TacticalButtonStyle.Primary,
                Font = Theme.FontSmallBold,
                Location = new Point(214, 56),
                Size = new Size(204, 32)
            };
            btnAddExe.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Game Executables (*.exe)|*.exe";
                    ofd.Title = "Select Game Binary";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _controller.AddCustomGameExecutable(ofd.FileName);
                        AppendGameConsole("Added custom binary: " + ofd.FileName);
                    }
                }
            };

            _searchGamesBox = new DarkSearchBox("Filter discovered games...")
            {
                Location = new Point(428, 56),
                Size = new Size(240, 32)
            };
            _searchGamesBox.SearchTextChanged += (s, e) => FilterAndReflowGameCards();

            _lblGamesSummary = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontMonoSmall,
                TextAlign = ContentAlignment.MiddleRight,
                Height = 24
            };

            _gamesHeaderPanel.Controls.Add(btnRescan);
            _gamesHeaderPanel.Controls.Add(btnAddExe);
            _gamesHeaderPanel.Controls.Add(_searchGamesBox);
            _gamesHeaderPanel.Controls.Add(_lblGamesSummary);

            SmoothPanel gameConsoleWrap = new SmoothPanel
            {
                Dock = DockStyle.Bottom,
                Height = 114,
                BackColor = Theme.BgCanvas,
                Padding = new Padding(20, 6, 20, 14)
            };
            _txtGameConsole = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.BgRecessed,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontMonoSmall,
                ScrollBars = ScrollBars.Vertical,
                Text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] Game Config Optimizer ready. Every Unreal Engine edit automatically preserves a .pctweaker.bak restore file.\r\n"
            };
            _txtGameConsole.HandleCreated += (s, e) => Theme.ApplyDarkScrollbars(_txtGameConsole.Handle);
            gameConsoleWrap.Controls.Add(WrapDarkBorderedConsole(_txtGameConsole));

            _gamesScrollPanel = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas,
                AutoScroll = true
            };
            _gamesScrollPanel.Resize += (s, e) => ReflowGameCards();

            _viewGames.Controls.Add(_gamesScrollPanel);
            _viewGames.Controls.Add(_gamesHeaderPanel);
            _viewGames.Controls.Add(gameConsoleWrap);
        }

        private void RebuildGameCards()
        {
            if (_gamesScrollPanel == null) return;
            _gamesScrollPanel.SuspendLayout();
            foreach (GameOptimizerCard c in _gameCards) c.Dispose();
            _gameCards.Clear();
            _gamesScrollPanel.Controls.Clear();

            foreach (DetectedGame g in _controller.DetectedGames)
            {
                GameOptimizerCard card = new GameOptimizerCard(g);
                card.IfeoClicked += (game) =>
                {
                    string msg;
                    bool ok = _controller.ToggleGameHighPriority(game, out msg);
                    AppendGameConsole(msg);
                    if (!ok && !_controller.Hardware.IsRunningAsAdmin)
                    {
                        DialogResult dr = MessageBox.Show(this, msg + "\r\n\r\nRelaunch as Administrator?", "Elevation Needed", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (dr == DialogResult.Yes && _controller.RelaunchAsAdministrator()) Close();
                    }
                };
                card.GpuPrefClicked += (game) =>
                {
                    if (!EnsureGameExePathLinked(game, "register DirectX High-Perf GPU Preference")) return;
                    string msg;
                    _controller.ToggleGameGpuPreference(game, out msg);
                    AppendGameConsole(msg);
                };
                card.FsoClicked += (game) =>
                {
                    if (!EnsureGameExePathLinked(game, "toggle Fullscreen Optimizations")) return;
                    string msg;
                    _controller.ToggleGameFullscreenOptimizations(game, out msg);
                    AppendGameConsole(msg);
                };
                card.PotatoIniClicked += (game) =>
                {
                    string msg;
                    _controller.ApplyUnrealConfigPreset(game, true, out msg);
                    AppendGameConsole(msg);
                };
                card.CompIniClicked += (game) =>
                {
                    string msg;
                    _controller.ApplyUnrealConfigPreset(game, false, out msg);
                    AppendGameConsole(msg);
                };
                card.RestoreIniClicked += (game) =>
                {
                    string msg;
                    _controller.RestoreUnrealConfigBackup(game, out msg);
                    AppendGameConsole(msg);
                };

                _gameCards.Add(card);
                _gamesScrollPanel.Controls.Add(card);
            }

            _gamesScrollPanel.ResumeLayout(false);
            FilterAndReflowGameCards();
        }

        private bool EnsureGameExePathLinked(DetectedGame game, string purpose)
        {
            if (game == null) return false;
            if (!string.IsNullOrEmpty(game.ExecutablePath)) return true;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Game Executable (*.exe)|*.exe";
                ofd.Title = "Locate " + game.ExecutableName + " to " + purpose;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    game.ExecutablePath = ofd.FileName;
                    game.ExecutableName = Path.GetFileName(ofd.FileName);
                    GameConfigOptimizer.RefreshGameStatus(game);
                    return true;
                }
            }
            return false;
        }

        private void LayoutGamesView()
        {
            if (_gamesHeaderPanel != null && _lblGamesSummary != null && _searchGamesBox != null)
            {
                int w = _gamesHeaderPanel.ClientSize.Width;
                _searchGamesBox.Width = Math.Min(280, Math.Max(160, w - 650));
                _lblGamesSummary.SetBounds(_searchGamesBox.Right + 10, 60, Math.Max(140, w - _searchGamesBox.Right - 30), 24);
            }
            ReflowGameCards();
        }

        private void FilterAndReflowGameCards()
        {
            string query = _searchGamesBox != null ? (_searchGamesBox.SearchText ?? "").Trim() : "";
            int shown = 0;
            int linked = 0;
            foreach (GameOptimizerCard card in _gameCards)
            {
                DetectedGame g = card.Game;
                if (!string.IsNullOrEmpty(g.ExecutablePath)) linked++;
                bool match = string.IsNullOrEmpty(query) ||
                             g.GameName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             g.ExecutableName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             g.EngineName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                card.Visible = match;
                if (match) shown++;
            }
            if (_lblGamesSummary != null)
                _lblGamesSummary.Text = string.Format("{0} games shown  •  {1} auto-linked", shown, linked);
            ReflowGameCards();
        }

        private void ReflowGameCards()
        {
            if (_gamesScrollPanel == null) return;
            _gamesScrollPanel.SuspendLayout();
            int scrollY = _gamesScrollPanel.AutoScrollPosition.Y;
            int padX = 20;
            int cardW = Math.Max(600, _gamesScrollPanel.ClientSize.Width - padX * 2 - 18);
            int y = 14 + scrollY;

            foreach (GameOptimizerCard card in _gameCards)
            {
                if (!card.Visible) continue;
                card.SetBounds(padX, y, cardW, card.Height);
                y += card.Height + 10;
            }
            _gamesScrollPanel.AutoScrollMinSize = new Size(0, (y - scrollY) + 14);
            _gamesScrollPanel.ResumeLayout(true);
        }

        private void AppendGameConsole(string msg)
        {
            if (_txtGameConsole == null || _txtGameConsole.IsDisposed) return;
            _txtGameConsole.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
        }

        // =========================================================================
        // VIEW 5: SAFETY VAULT & ROLLBACK CENTER
        // =========================================================================
        private void BuildVaultView()
        {
            _viewVault = new SmoothPanel { BackColor = Theme.BgCanvas };
            _viewVault.Resize += (s, e) => LayoutVaultView();

            _vaultHeaderPanel = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Theme.BgCanvas
            };
            _vaultHeaderPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString("Safety Vault & Rollback Center", Theme.FontPageTitle, tb, 20, 10);
                    g.DrawString("Transactional JSON snapshots in %APPDATA%\\PCTweaker\\Snapshots, Windows System Restore & chronological audit log.", Theme.FontSmall, sb, 20, 36);
                }
                using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                    g.DrawLine(div, 20, _vaultHeaderPanel.Height - 1, _vaultHeaderPanel.Width - 20, _vaultHeaderPanel.Height - 1);
            };

            TacticalButton btnRestorePt = new TacticalButton
            {
                Text = "Create Windows Restore Point",
                IconKey = "vault",
                ButtonStyle = TacticalButtonStyle.Primary,
                Font = Theme.FontSmallBold,
                Location = new Point(20, 56),
                Size = new Size(218, 32)
            };
            btnRestorePt.Click += (s, e) =>
            {
                RunAsyncAction(btnRestorePt, "Creating Windows System Restore Point...", () =>
                {
                    string msg;
                    _controller.CreateSystemRestorePoint(out msg);
                    BeginInvoke((Action)(() => MessageBox.Show(this, msg, "Windows System Restore", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                });
            };

            TacticalButton btnSaveSnap = new TacticalButton
            {
                Text = "Save Manual Snapshot",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Font = Theme.FontSmallBold,
                Location = new Point(246, 56),
                Size = new Size(168, 32)
            };
            btnSaveSnap.Click += (s, e) =>
            {
                _controller.CreateManualSnapshot("Manual User Snapshot (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            };

            TacticalButton btnRevertAllVault = new TacticalButton
            {
                Text = "Revert All Tweaks to Baseline",
                ButtonStyle = TacticalButtonStyle.Danger,
                Font = Theme.FontSmallBold,
                Location = new Point(422, 56),
                Size = new Size(204, 32)
            };
            btnRevertAllVault.Click += (s, e) => ConfirmAndRevertAll(btnRevertAllVault);

            TacticalButton btnOpenFolder = new TacticalButton
            {
                Text = "Open Vault Folder",
                ButtonStyle = TacticalButtonStyle.Ghost,
                Font = Theme.FontSmallBold,
                Location = new Point(634, 56),
                Size = new Size(134, 32)
            };
            btnOpenFolder.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", _controller.Vault.VaultDirectory); } catch { }
            };

            _vaultHeaderPanel.Controls.Add(btnRestorePt);
            _vaultHeaderPanel.Controls.Add(btnSaveSnap);
            _vaultHeaderPanel.Controls.Add(btnRevertAllVault);
            _vaultHeaderPanel.Controls.Add(btnOpenFolder);

            // Bottom Audit Log Section
            SmoothPanel auditWrap = new SmoothPanel
            {
                Dock = DockStyle.Bottom,
                Height = 200,
                BackColor = Theme.BgCanvas,
                Padding = new Padding(20, 26, 20, 14)
            };
            auditWrap.Paint += (s, e) =>
            {
                e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush lb = new SolidBrush(Theme.TextMuted))
                    e.Graphics.DrawString("SESSION TRANSACTIONAL AUDIT LOG (%APPDATA%\\PCTWEAKER\\AUDIT.LOG)", Theme.FontBadge, lb, 20, 6);
            };

            _txtVaultAuditLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.BgRecessed,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontMonoSmall,
                ScrollBars = ScrollBars.Vertical
            };
            _txtVaultAuditLog.HandleCreated += (s, e) => Theme.ApplyDarkScrollbars(_txtVaultAuditLog.Handle);
            auditWrap.Controls.Add(WrapDarkBorderedConsole(_txtVaultAuditLog));

            _vaultSnapshotsScroll = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas,
                AutoScroll = true
            };
            _vaultSnapshotsScroll.Resize += (s, e) => ReflowSnapshotCards();

            _viewVault.Controls.Add(_vaultSnapshotsScroll);
            _viewVault.Controls.Add(_vaultHeaderPanel);
            _viewVault.Controls.Add(auditWrap);
        }

        private void RebuildVaultCards()
        {
            if (_vaultSnapshotsScroll == null) return;
            _vaultSnapshotsScroll.SuspendLayout();
            foreach (SnapshotItemCard c in _snapshotCards) c.Dispose();
            _snapshotCards.Clear();
            _vaultSnapshotsScroll.Controls.Clear();

            List<StateSnapshot> snaps = _controller.GetSnapshots();
            foreach (StateSnapshot snap in snaps)
            {
                SnapshotItemCard card = new SnapshotItemCard(snap);
                card.RestoreClicked += (selectedSnap) =>
                {
                    if (string.Equals(selectedSnap.SnapshotId, "baseline", StringComparison.OrdinalIgnoreCase))
                    {
                        ConfirmAndRevertAll(card.BtnRestore);
                    }
                    else
                    {
                        DialogResult dr = MessageBox.Show(this,
                            "Restore your system tweaks and registry backup items to snapshot:\r\n\r\n" +
                            "  " + selectedSnap.Label + "\r\n  (" + selectedSnap.CreatedAtUtc + ")\r\n\r\n" +
                            "This will replay all tracked backup items and reconcile all 36 tweaks to match this snapshot.",
                            "Confirm Point-in-Time Snapshot Restore",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        if (dr != DialogResult.Yes) return;

                        RunAsyncAction(card.BtnRestore, "Restoring snapshot '" + selectedSnap.Label + "'...", () =>
                        {
                            string restoreMsg;
                            _controller.RestoreSnapshot(selectedSnap.SnapshotId, out restoreMsg);
                            BeginInvoke((Action)(() =>
                            {
                                MessageBox.Show(this, restoreMsg, "Snapshot Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        });
                    }
                };
                card.DeleteClicked += (selectedSnap) =>
                {
                    DialogResult dr = MessageBox.Show(this,
                        "Permanently delete snapshot '" + selectedSnap.Label + "' from SafetyVault?",
                        "Delete Snapshot",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (dr != DialogResult.Yes) return;

                    string delMsg;
                    _controller.DeleteSnapshot(selectedSnap.SnapshotId, out delMsg);
                };
                _snapshotCards.Add(card);
                _vaultSnapshotsScroll.Controls.Add(card);
            }
            _vaultSnapshotsScroll.ResumeLayout(false);
            ReflowSnapshotCards();

            if (_txtVaultAuditLog != null)
            {
                if (_controller.Vault.SessionAuditLog.Count == 0)
                    _txtVaultAuditLog.Text = "No mutations recorded in this session yet. All registry, service, and config edits appear here automatically.";
                else
                    _txtVaultAuditLog.Text = string.Join("\r\n", _controller.Vault.SessionAuditLog.ToArray());
            }
        }

        private void LayoutVaultView() { ReflowSnapshotCards(); }

        private void ReflowSnapshotCards()
        {
            if (_vaultSnapshotsScroll == null) return;
            _vaultSnapshotsScroll.SuspendLayout();
            int scrollY = _vaultSnapshotsScroll.AutoScrollPosition.Y;
            int padX = 20;
            int cardW = Math.Max(580, _vaultSnapshotsScroll.ClientSize.Width - padX * 2 - 18);
            int y = 14 + scrollY;
            foreach (SnapshotItemCard card in _snapshotCards)
            {
                card.SetBounds(padX, y, cardW, card.Height);
                y += card.Height + 10;
            }
            _vaultSnapshotsScroll.AutoScrollMinSize = new Size(0, (y - scrollY) + 12);
            _vaultSnapshotsScroll.ResumeLayout(true);
        }

        // =========================================================================
        // VIEW 6: HARDWARE & DIAGNOSTIC AUDIT
        // =========================================================================
        private void BuildAuditView()
        {
            _viewAudit = new SmoothPanel { BackColor = Theme.BgCanvas };

            _auditHeaderPanel = new SmoothPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Theme.BgCanvas
            };
            _auditHeaderPanel.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString("Hardware & System Audit Report", Theme.FontPageTitle, tb, 20, 10);
                    g.DrawString("Complete live telemetry snapshot of CPU, 64-bit GPU VRAM, timer resolution, all 36 tweak states & detected game configs.", Theme.FontSmall, sb, 20, 36);
                }
                using (Pen div = new Pen(Theme.BorderSubtle, 1f))
                    g.DrawLine(div, 20, _auditHeaderPanel.Height - 1, _auditHeaderPanel.Width - 20, _auditHeaderPanel.Height - 1);
            };

            TacticalButton btnReaudit = new TacticalButton
            {
                Text = "Re-Run Full Live Audit",
                IconKey = "refresh",
                ButtonStyle = TacticalButtonStyle.Primary,
                Font = Theme.FontSmallBold,
                Location = new Point(20, 56),
                Size = new Size(180, 32)
            };
            btnReaudit.Click += (s, e) =>
            {
                RunAsyncAction(btnReaudit, "Running live hardware & 36-tweak audit...", () =>
                {
                    _controller.AuditAllTweakStates();
                });
            };

            TacticalButton btnCopy = new TacticalButton
            {
                Text = "Copy to Clipboard",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Font = Theme.FontSmallBold,
                Location = new Point(208, 56),
                Size = new Size(150, 32)
            };
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_txtDiagnosticReport.Text);
                    OnControllerStatusChanged("Diagnostic audit report copied to clipboard.");
                }
                catch { }
            };

            TacticalButton btnExport = new TacticalButton
            {
                Text = "Export Report (.txt)",
                ButtonStyle = TacticalButtonStyle.Secondary,
                Font = Theme.FontSmallBold,
                Location = new Point(366, 56),
                Size = new Size(156, 32)
            };
            btnExport.Click += (s, e) =>
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Text Report (*.txt)|*.txt";
                    sfd.FileName = "apex-tweaker-audit-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, _txtDiagnosticReport.Text, Encoding.UTF8);
                        OnControllerStatusChanged("Saved diagnostic report to " + sfd.FileName);
                    }
                }
            };

            _auditHeaderPanel.Controls.Add(btnReaudit);
            _auditHeaderPanel.Controls.Add(btnCopy);
            _auditHeaderPanel.Controls.Add(btnExport);

            SmoothPanel diagWrap = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgCanvas,
                Padding = new Padding(20, 14, 20, 16)
            };

            _txtDiagnosticReport = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.BgRecessed,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontMono,
                ScrollBars = ScrollBars.Both
            };
            _txtDiagnosticReport.HandleCreated += (s, e) => Theme.ApplyDarkScrollbars(_txtDiagnosticReport.Handle);
            diagWrap.Controls.Add(WrapDarkBorderedConsole(_txtDiagnosticReport));

            _viewAudit.Controls.Add(diagWrap);
            _viewAudit.Controls.Add(_auditHeaderPanel);
        }

        private SmoothPanel WrapDarkBorderedConsole(TextBox inner)
        {
            SmoothPanel frame = new SmoothPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgRecessed,
                Padding = new Padding(8, 6, 6, 6)
            };
            frame.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawRectangle(p, 0, 0, frame.Width - 1, frame.Height - 1);
            };
            frame.Controls.Add(inner);
            return frame;
        }

        // =========================================================================
        // STATE SYNCHRONIZATION & LIVE TELEMETRY UPDATES
        // =========================================================================
        private void RefreshAllViews()
        {
            // 1. Update Preset Hero Cards dynamic tweak counts & progress
            int potTotal = 0, potApplied = 0;
            int cmpTotal = 0, cmpApplied = 0;
            int balTotal = 0, balApplied = 0;
            int allApplied = 0;

            foreach (TweakDefinition t in _controller.Tweaks)
            {
                bool on = t.CurrentStatus == TweakStatus.Applied;
                if (on) allApplied++;
                if (t.RecommendedForPotato) { potTotal++; if (on) potApplied++; }
                if (t.RecommendedForCompetitive) { cmpTotal++; if (on) cmpApplied++; }
                if (t.IncludedInBalancedSafe && t.RiskLevel == TweakRisk.Safe) { balTotal++; if (on) balApplied++; }
            }

            _presetPotato.ProgressBadge = string.Format("{0} / {1} ACTIVE", potApplied, potTotal);
            _presetPotato.ActionButton.Text = string.Format("Apply Potato Rescue ({0} Tweaks)", potTotal);
            _presetPotato.Invalidate();

            _presetCompetitive.ProgressBadge = string.Format("{0} / {1} ACTIVE", cmpApplied, cmpTotal);
            _presetCompetitive.ActionButton.Text = string.Format("Apply Competitive Profile ({0} Tweaks)", cmpTotal);
            _presetCompetitive.Invalidate();

            _presetBalanced.ProgressBadge = string.Format("{0} / {1} ACTIVE", balApplied, balTotal);
            _presetBalanced.ActionButton.Text = string.Format("Apply Balanced Safe ({0} Tweaks)", balTotal);
            _presetBalanced.Invalidate();

            _presetRevert.ProgressBadge = string.Format("{0} APPLIED", allApplied);
            _presetRevert.Invalidate();

            // 2. Sync Sidebar Nav Badges
            _navButtons[1].BadgeText = allApplied + "/" + _controller.Tweaks.Count;
            _navButtons[1].BadgeColor = allApplied > 0 ? Theme.AccentSafe : Theme.AccentPrimary;

            bool boostActive = _controller.Booster.SessionState.IsActive;
            _navButtons[2].BadgeText = boostActive ? "ON" : "";
            _navButtons[2].BadgeColor = Theme.AccentSafe;

            _navButtons[3].BadgeText = _controller.DetectedGames.Count.ToString();
            _navButtons[4].BadgeText = _controller.GetSnapshots().Count.ToString();

            // 3. Sync Bottom Bar
            if (_lblBottomApplied != null)
                _lblBottomApplied.Text = string.Format("{0} / {1} TWEAKS ACTIVE", allApplied, _controller.Tweaks.Count);

            // 4. Sync Tweak Cards
            foreach (TweakItemCard card in _tweakCards)
                card.SyncStateFromModel();
            FilterAndLayoutTweakCards();

            // 5. Sync Game Booster Hero Button
            if (_btnHeroBoostToggle != null)
            {
                _btnHeroBoostToggle.Text = boostActive
                    ? "GAME BOOST ACTIVE — RESTORE NORMAL MODE"
                    : "ENGAGE GAME SESSION BOOST";
                _btnHeroBoostToggle.ButtonStyle = boostActive
                    ? TacticalButtonStyle.SolidSafe
                    : TacticalButtonStyle.SolidPrimary;
                _btnHeroBoostToggle.Invalidate();
                _boostLeftCard.Invalidate();
            }

            // 6. Sync Games & Vault & Audit
            RebuildGameCards();
            RebuildVaultCards();

            if (_txtDiagnosticReport != null)
                _txtDiagnosticReport.Text = _controller.GenerateDiagnosticReport();

            _dashHeroCard.Invalidate();
        }

        private void UpdateTelemetryDisplay()
        {
            if (IsDisposed) return;
            try
            {
                TelemetrySample s = _controller.SampleTelemetry();

                // Header CPU
                _metricCpu.PrimaryValue = string.Format("{0:F1}%", s.CpuUsagePercent);
                _metricCpu.SubText = string.Format("{0}C / {1}T", _controller.Hardware.PhysicalCores, _controller.Hardware.LogicalThreads);
                _metricCpu.ProgressPercent = (float)s.CpuUsagePercent;
                _metricCpu.AccentColor = s.CpuUsagePercent >= 85 ? Theme.AccentDanger : (s.CpuUsagePercent >= 65 ? Theme.AccentWarning : Theme.AccentPrimary);
                _metricCpu.Invalidate();

                // Header RAM
                _metricRam.PrimaryValue = string.Format("{0:F1} / {1:F1} GB", s.UsedRamMb / 1024.0, s.TotalRamMb / 1024.0);
                _metricRam.SubText = string.Format("{0:F1} GB FREE", s.AvailableRamMb / 1024.0);
                _metricRam.ProgressPercent = s.MemoryLoadPercent;
                _metricRam.AccentColor = s.MemoryLoadPercent >= 85 ? Theme.AccentDanger : (s.MemoryLoadPercent >= 70 ? Theme.AccentWarning : Theme.AccentSafe);
                _metricRam.Invalidate();

                // Header Kernel Timer Resolution
                _metricTimer.PrimaryValue = string.Format("{0:F3} ms", s.TimerResCurrentMs);
                _metricTimer.SubText = string.Format("MAX {0:F2} MS", s.TimerResMaxMs);
                float timerPerf = s.TimerResCurrentMs <= 1.05 ? 95f : (s.TimerResCurrentMs <= 5.0 ? 60f : 25f);
                _metricTimer.ProgressPercent = timerPerf;
                _metricTimer.AccentColor = s.TimerResCurrentMs <= 1.05 ? Theme.AccentSafe : Theme.AccentWarning;
                _metricTimer.Invalidate();

                // Header Opt Score
                _metricScore.PrimaryValue = s.OptimizationScorePercent + "%";
                _metricScore.SubText = string.Format("{0}/{1} REC", s.RecommendedAppliedCount, s.RecommendedTotalCount);
                _metricScore.ProgressPercent = s.OptimizationScorePercent;
                _metricScore.AccentColor = s.OptimizationScorePercent >= 75 ? Theme.AccentSafe : (s.OptimizationScorePercent >= 35 ? Theme.AccentPrimary : Theme.AccentWarning);
                _metricScore.Invalidate();

                // Dashboard Radial Gauge
                if (_dashScoreGauge != null)
                {
                    _dashScoreGauge.ScorePercent = s.OptimizationScorePercent;
                    _dashScoreGauge.Subtitle = string.Format("{0} / {1} REC", s.RecommendedAppliedCount, s.RecommendedTotalCount);
                }

                // View 3 3-Segment Stacked Memory Bar
                if (_memoryBar != null)
                {
                    _memoryBar.TotalMb = s.TotalRamMb;
                    _memoryBar.UsedMb = s.UsedRamMb;
                    _memoryBar.StandbyCacheMb = s.StandbyCacheMb;
                    _memoryBar.FreeRamMb = s.FreeRamMb;
                    _memoryBar.AvailableMb = s.AvailableRamMb;
                    _memoryBar.LoadPercent = s.MemoryLoadPercent;
                    _memoryBar.ProcessCount = s.ProcessCount;
                    _memoryBar.HandleCount = s.HandleCount;
                    _memoryBar.Invalidate();
                }
            }
            catch { }
        }
    }
}
