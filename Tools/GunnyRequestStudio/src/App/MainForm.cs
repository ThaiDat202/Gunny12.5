using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio
{
    /// <summary>Nut dieu huong ben trai (sidebar navy) - kieu web: item active la card trang.</summary>
    public class NavButton : Control
    {
        public bool Active { get; set; }
        public string Icon { get; set; }
        public Color IconColor { get; set; }

        public NavButton(string text, string icon)
        {
            Text = text;
            Icon = icon;
            IconColor = Color.FromArgb(185, 201, 222);
            Height = 42;
            Cursor = Cursors.Hand;
            Font = Theme.FontBold(10f);
            ForeColor = Color.FromArgb(223, 232, 244);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.SetupGraphics(e.Graphics);
            var g = e.Graphics;
            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            var r = new Rectangle(0, 2, Width - 1, Height - 5);
            if (Active)
            {
                using (var sh = Theme.RoundedRect(new Rectangle(r.X, r.Y + 1, r.Width, r.Height), 10))
                using (var b = new SolidBrush(Color.FromArgb(46, 10, 25, 50)))
                    g.FillPath(b, sh);
                using (var p = Theme.RoundedRect(r, 10))
                using (var b = new SolidBrush(Color.FromArgb(247, 249, 252)))
                    g.FillPath(b, p);
                using (var b = new SolidBrush(Theme.Yellow))
                    g.FillRectangle(b, 5, 12, 3, Height - 24);
            }
            else if (hover)
            {
                using (var p = Theme.RoundedRect(r, 10))
                using (var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            var iconColor = Active ? Theme.Orange : (hover ? Color.White : IconColor);
            using (var f = Theme.FontIcon(11.5f))
                TextRenderer.DrawText(g, Theme.Glyph(Icon), f, new Rectangle(18, 0, 28, Height), iconColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            var textColor = Active ? Theme.AccentDeep : (hover ? Color.White : ForeColor);
            using (var f = Font)
                TextRenderer.DrawText(g, Text, f, new Rectangle(52, 0, Width - 58, Height), textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Logo badge amber gradient phia tren cua so.</summary>
    public class BrandBadge : Control
    {
        public BrandBadge()
        {
            Width = 44; Height = 44;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.SetupGraphics(e.Graphics);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.RoundedRect(r, 12))
            using (var b = new LinearGradientBrush(r, Color.FromArgb(240, 195, 126), Color.FromArgb(201, 138, 60), LinearGradientMode.ForwardDiagonal))
                e.Graphics.FillPath(b, p);
            using (var p = Theme.RoundedRect(r, 12))
            using (var pen = new Pen(Color.FromArgb(90, 60, 20), 1))
                e.Graphics.DrawPath(pen, p);
            TextRenderer.DrawText(e.Graphics, "\u25C8", new Font("Segoe UI", 17f, FontStyle.Bold), r,
                Color.FromArgb(74, 50, 8), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Nut cua so (min/max/close) kieu sang.</summary>
    public class WindowButton : Control
    {
        public Color HoverBack { get; set; }
        public Color HoverFore { get; set; }
        private bool _hover;

        public WindowButton()
        {
            Width = 44; Height = 32;
            ForeColor = Theme.AccentDeep;
            HoverBack = Color.FromArgb(238, 242, 248);
            HoverFore = Theme.AccentDeep;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_hover)
            {
                using (var b = new SolidBrush(HoverBack)) e.Graphics.FillRectangle(b, ClientRectangle);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, _hover ? HoverFore : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Cua so chinh: sidebar + header + noi dung trang + status bar (light theme).</summary>
    public class MainForm : Form
    {
        private readonly StudioContext _ctx;
        private readonly List<PageDescriptor> _descriptors;
        private readonly PageBase[] _pages;          // null = chua tao (lazy)
        private readonly List<NavButton> _navButtons = new List<NavButton>();
        private Panel _sidebar;
        private Panel _content;
        private Panel _topBar;
        private Panel _statusBar;
        private Label _statusText;
        private Label _dbPill;
        private Label _pageTitle;
        private PageBase _current;
        private System.Windows.Forms.Timer _statusTimer;
        private volatile string _pendingStatus;

        private const int ResizeBorder = 6;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        public MainForm(StudioContext ctx, List<PageDescriptor> descriptors)
        {
            _ctx = ctx;
            _descriptors = descriptors;
            _pages = new PageBase[descriptors.Count];
            Text = "Gunny Request Studio - Rip Request & Import Database";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1440, 900);
            MinimumSize = new Size(1180, 720);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontBase();
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BuildLayout();
            _ctx.StatusChanged += s => UiInvoke(() =>
            {
                if (_dbPill == null) return;
                _dbPill.Text = "  " + s + "  ";
                bool ok = s != null && (s.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Ket noi", StringComparison.OrdinalIgnoreCase) >= 0);
                _dbPill.ForeColor = ok ? Theme.Green : Theme.Red;
                _dbPill.BackColor = ok ? Color.FromArgb(233, 246, 239) : Color.FromArgb(253, 238, 241);
            });
            _ctx.SettingsChanged += () => UiInvoke(() =>
            {
                foreach (var p in _pages) { if (p == null) continue; try { p.OnSettingsChanged(); } catch { } }
            });
            SelectPage(0);
            var dashboard = _pages[0] as Pages.DashboardPage;
            if (dashboard != null)
            {
                dashboard.NavigationRequested += title => UiInvoke(() =>
                {
                    for (int i = 0; i < _descriptors.Count; i++)
                    {
                        if (string.Equals(_descriptors[i].Title, title, StringComparison.OrdinalIgnoreCase)) { SelectPage(i); return; }
                    }
                });
            }
        }

        public List<PageBase> Pages
        {
            get
            {
                var list = new List<PageBase>();
                for (int i = 0; i < _pages.Length; i++) { if (_pages[i] != null) list.Add(_pages[i]); }
                return list;
            }
        }

        public void SelectPagePublic(int index) { SelectPage(index); }

        private PageBase GetPage(int index)
        {
            if (index < 0 || index >= _pages.Length) return null;
            if (_pages[index] == null)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _pages[index] = _descriptors[index].Factory();
                sw.Stop();
                if (sw.ElapsedMilliseconds > 50)
                    _ctx.Log.Info(string.Format("Tạo trang \"{0}\" trong {1} ms", _descriptors[index].Title, sw.ElapsedMilliseconds));
            }
            return _pages[index];
        }

        private void UiInvoke(Action action)
        {
            if (IsHandleCreated && InvokeRequired) BeginInvoke(action); else action();
        }

        private void BuildLayout()
        {
            // ---------- top bar (trang, vien duoi + dai mau navy->amber)
            _topBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
            _topBar.Paint += TopBarPaint;

            var logo = new BrandBadge { Left = 18, Top = 10 };
            logo.MouseDown += DragWindow;
            var title = new Label
            {
                Text = "GUNNY REQUEST STUDIO",
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                ForeColor = Theme.AccentDeep,
                AutoSize = true,
                Left = 74,
                Top = 13,
                BackColor = Color.Transparent
            };
            var sub = new Label
            {
                Text = "Rip request · Import database · Bomb / Weapon UI  —  Gunny 12.6",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextDim,
                AutoSize = true,
                Left = 76,
                Top = 36,
                BackColor = Color.Transparent
            };
            _dbPill = new Label
            {
                Text = "  CSDL: " + _ctx.DbStatus + "  ",
                Font = Theme.FontBold(8.6f),
                ForeColor = Theme.Green,
                BackColor = Color.FromArgb(233, 246, 239),
                AutoSize = true,
                Padding = new Padding(6, 4, 6, 4),
                Left = 700,
                Top = 20
            };
            _pageTitle = new Label
            {
                Text = "",
                Font = Theme.FontH2(11.5f),
                ForeColor = Theme.AccentDeep,
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var btnMin = WindowButton("\u2013");
            var btnMax = WindowButton("\u25A1");
            var btnClose = WindowButton("\u2715");
            btnClose.HoverBack = Color.FromArgb(184, 73, 91);
            btnClose.HoverFore = Color.White;
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;
            btnMax.Click += (s, e) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            btnClose.Click += (s, e) => Close();

            _topBar.Controls.AddRange(new Control[] { logo, title, sub, _dbPill, _pageTitle, btnMin, btnMax, btnClose });
            _topBar.Resize += (s, e) =>
            {
                btnClose.Left = _topBar.Width - 44; btnClose.Top = 0;
                btnMax.Left = _topBar.Width - 84; btnMax.Top = 0;
                btnMin.Left = _topBar.Width - 124; btnMin.Top = 0;
                _pageTitle.Left = Math.Max(700, _topBar.Width - 520);
                _pageTitle.Top = 24;
                _dbPill.Left = Math.Max(520, _topBar.Width - 760);
            };
            _topBar.MouseDown += DragWindow;
            title.MouseDown += DragWindow;
            sub.MouseDown += DragWindow;

            // ---------- status bar
            _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.White };
            _statusBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0);
            };
            _statusText = new Label
            {
                Text = "Sẵn sàng",
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Font = Theme.FontBase(8.6f),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            var ver = new Label
            {
                Text = "v1.0  ·  " + Environment.MachineName + "  ·  Db_Tank_All",
                ForeColor = Theme.TextFaint,
                BackColor = Color.Transparent,
                Font = Theme.FontBase(8.4f),
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 6, 16, 0)
            };
            _statusBar.Controls.Add(_statusText);
            _statusBar.Controls.Add(ver);
            // Throttle status bar: log co the ban hang nghin dong -> chi cap nhat 6-7 lan/giay
            _ctx.Log.OnLog += le => { if (le != null) _pendingStatus = le.ToString(); };
            _statusTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _statusTimer.Tick += (s, e) =>
            {
                var pending = _pendingStatus;
                if (pending == null) return;
                _pendingStatus = null;
                if (_statusText != null && !_statusText.IsDisposed) _statusText.Text = pending;
            };
            _statusTimer.Start();

            // ---------- sidebar (navy gradient + dai amber)
            _sidebar = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = Theme.Sidebar };
            _sidebar.Paint += SidebarPaint;

            var brand = new Label
            {
                Text = "CÔNG CỤ",
                ForeColor = Color.FromArgb(184, 200, 222),
                BackColor = Color.Transparent,
                Font = Theme.FontBold(8f),
                AutoSize = true,
                Left = 22,
                Top = 22
            };
            _sidebar.Controls.Add(brand);

            int y = 55;
            for (int i = 0; i < _descriptors.Count; i++)
            {
                int idx = i;
                var btn = new NavButton(_descriptors[i].Title, _descriptors[i].Icon)
                {
                    Left = 10,
                    Top = y,
                    Width = _sidebar.Width - 20,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                btn.Click += (s, e) => SelectPage(idx);
                _navButtons.Add(btn);
                _sidebar.Controls.Add(btn);
                y += 46;
            }

            var footer = new Label
            {
                Text = "Gunny 12.6 · Request + DB + Bomb",
                ForeColor = Color.FromArgb(170, 188, 212),
                BackColor = Color.Transparent,
                Font = Theme.FontBase(8f),
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _sidebar.Controls.Add(footer);

            // ---------- content
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(18, 14, 18, 10) };

            Controls.Add(_content);
            Controls.Add(_sidebar);
            Controls.Add(_topBar);
            Controls.Add(_statusBar);
        }

        private WindowButton WindowButton(string text)
        {
            return new WindowButton { Text = text };
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }
        }

        private void TopBarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var pen = new Pen(Theme.BorderSoft)) g.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            // dai gradient navy -> amber o day topbar
            var r = new Rectangle(0, _topBar.Height - 4, _topBar.Width, 3);
            using (var b = new LinearGradientBrush(new Rectangle(0, r.Y, Math.Max(10, r.Width), Math.Max(3, r.Height)),
                       Theme.AccentDeep, Theme.Yellow, LinearGradientMode.Horizontal))
                g.FillRectangle(b, r);
        }

        private void SidebarPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, _sidebar.Width, _sidebar.Height);
            using (var b = new LinearGradientBrush(rect, Theme.SidebarTop, Theme.SidebarBottom, LinearGradientMode.Vertical))
                g.FillRectangle(b, rect);
            // dai amber ben phai (giong web)
            var strip = new Rectangle(_sidebar.Width - 3, 0, 3, _sidebar.Height);
            using (var b = new LinearGradientBrush(strip, Color.FromArgb(217, 161, 90), Color.FromArgb(176, 118, 44), LinearGradientMode.Vertical))
                g.FillRectangle(b, strip);
        }

        private void SelectPage(int index)
        {
            if (index < 0 || index >= _descriptors.Count) return;
            for (int i = 0; i < _navButtons.Count; i++) { _navButtons[i].Active = i == index; _navButtons[i].Invalidate(); }
            var page = GetPage(index);
            if (page == null) return;
            if (_current != page)
            {
                _content.Controls.Clear();
                _content.Controls.Add(page);
                _current = page;
            }
            _pageTitle.Text = _descriptors[index].Title.ToUpperInvariant() + (string.IsNullOrEmpty(_descriptors[index].Subtitle) ? "" : "  ·  " + _descriptors[index].Subtitle);
            try { page.OnShownPage(); } catch (Exception ex) { _ctx.Log.Error("Loi trang " + _descriptors[index].Title + ": " + ex.Message); }
            page.BringToFront();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.SetupGraphics(e.Graphics);
            using (var p = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawPath(pen, p);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            try
            {
                using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 12))
                    Region = new Region(path);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                var pos = PointToClient(new Point(m.LParam.ToInt32()));
                if (pos.X <= ResizeBorder && pos.Y <= ResizeBorder) m.Result = (IntPtr)HTTOPLEFT;
                else if (pos.X >= ClientSize.Width - ResizeBorder && pos.Y <= ResizeBorder) m.Result = (IntPtr)HTTOPRIGHT;
                else if (pos.X <= ResizeBorder && pos.Y >= ClientSize.Height - ResizeBorder) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (pos.X >= ClientSize.Width - ResizeBorder && pos.Y >= ClientSize.Height - ResizeBorder) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (pos.X <= ResizeBorder) m.Result = (IntPtr)HTLEFT;
                else if (pos.X >= ClientSize.Width - ResizeBorder) m.Result = (IntPtr)HTRIGHT;
                else if (pos.Y <= ResizeBorder) m.Result = (IntPtr)HTTOP;
                else if (pos.Y >= ClientSize.Height - ResizeBorder) m.Result = (IntPtr)HTBOTTOM;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
