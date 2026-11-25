using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;
using GunnyRequestStudio.Pages;

namespace UiA_Preview
{
    /// <summary>
    /// Scratch preview host cho 3 trang uiA (Dashboard / Rip / Settings).
    /// Chay: uiA.exe             -> mo cua so preview co sidebar de doi trang.
    ///       uiA.exe --smoke     -> tu chup screenshot tung trang vao _dev\uiA\shots roi thoat.
    ///       uiA.exe --exercise  -> seed du lieu smoke, bam thu Rip + Promote / Kiem tra CSDL / Lam moi, chup ket qua.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool smoke = HasArg(args, "--smoke");
            bool exercise = HasArg(args, "--exercise");

            try
            {
                if (exercise) SeedExerciseSettings();
                var ctx = new StudioContext();
                var pages = new List<PageBase>
                {
                    new DashboardPage(ctx),
                    new RipPage(ctx),
                    new SettingsPage(ctx)
                };
                Application.Run(new PreviewForm(ctx, pages, smoke, exercise));
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview-error.txt"), ex.ToString(), Encoding.UTF8);
                }
                catch { }
                try { MessageBox.Show(ex.ToString(), "uiA preview - loi"); } catch { }
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            foreach (var a in args)
            {
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Seed settings.json + vai XML nho vao thu muc scratch de test Rip/Promote an toan.</summary>
        private static void SeedExerciseSettings()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var root = Path.Combine(baseDir, "smoke-data");
            var request = Path.Combine(root, "request");
            var mirror = Path.Combine(root, "mirror");
            var reports = Path.Combine(root, "reports");
            Directory.CreateDirectory(request);
            Directory.CreateDirectory(mirror);
            Directory.CreateDirectory(reports);

            var source = @"C:\Gunny\Gunny12.6\WebSite\Request\vng-quest";
            try
            {
                if (Directory.Exists(source))
                {
                    var files = Directory.GetFiles(source, "*.xml")
                        .OrderBy(f => new FileInfo(f).Length).Take(4).ToArray();
                    foreach (var f in files)
                    {
                        var dst = Path.Combine(request, Path.GetFileName(f));
                        if (!File.Exists(dst)) File.Copy(f, dst, true);
                    }
                }
            }
            catch { }

            var s = AppSettings.CreateDefault();
            s.RequestDir = request;
            s.MirrorDir = mirror;
            s.ReportsDir = reports;
            Json.SaveFile(Paths.SettingsPath(), s);
        }
    }

    internal class PreviewForm : Form
    {
        private readonly StudioContext _ctx;
        private readonly List<PageBase> _pages;
        private readonly bool _smoke;
        private readonly bool _exercise;
        private readonly List<Button> _navButtons = new List<Button>();
        private Panel _content;
        private Panel _titleBar;
        private Label _statusLabel;
        private Label _pageInfoLabel;
        private int _currentIndex = -1;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public PreviewForm(StudioContext ctx, List<PageBase> pages, bool smoke, bool exercise)
        {
            _ctx = ctx;
            _pages = pages;
            _smoke = smoke;
            _exercise = exercise;

            Text = "Gunny Request Studio - uiA preview";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1440, 900);
            MinimumSize = new Size(1180, 720);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontBase();
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            Padding = new Padding(1);

            BuildLayout();
            _ctx.StatusChanged += s => Ui(() =>
            {
                if (_statusLabel != null) _statusLabel.Text = "  " + (s ?? "");
            });
            SelectPage(0);
        }

        // ------------------------------------------------------------ layout

        private void BuildLayout()
        {
            _titleBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.BgDeep };
            _titleBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
                using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 18, 15, 3, 28);
            };
            _titleBar.MouseDown += DragWindow;

            var logo = new Label
            {
                Text = "\u25C8",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                AutoSize = false,
                Width = 42,
                Height = 44,
                Left = 32,
                Top = 6,
                TextAlign = ContentAlignment.MiddleCenter
            };
            logo.MouseDown += DragWindow;
            var title = new Label
            {
                Text = "GUNNY REQUEST STUDIO  \u00B7  uiA preview",
                Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = true,
                Left = 78,
                Top = 11
            };
            title.MouseDown += DragWindow;
            _pageInfoLabel = new Label
            {
                Text = "Dashboard / Rip Request / Settings",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextFaint,
                AutoSize = true,
                Left = 80,
                Top = 33
            };
            _pageInfoLabel.MouseDown += DragWindow;

            var btnClose = SmallButton("\u2715", 38, (s, e) => Close());
            btnClose.HoverColor = Theme.Red;
            var btn1180 = SmallButton("1180 x 720", 96, (s, e) => SetPreset(1180, 720));
            var btn1440 = SmallButton("1440 x 900", 96, (s, e) => SetPreset(1440, 900));

            _titleBar.Controls.AddRange(new Control[] { logo, title, _pageInfoLabel, btn1440, btn1180, btnClose });
            _titleBar.Resize += (s, e) =>
            {
                btnClose.Left = _titleBar.Width - 42; btnClose.Top = 12;
                btn1180.Left = _titleBar.Width - 142; btn1180.Top = 14;
                btn1440.Left = _titleBar.Width - 242; btn1440.Top = 14;
            };

            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDeep };
            statusBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0);
            };
            _statusLabel = new Label
            {
                Text = "  San sang",
                ForeColor = Theme.TextDim,
                Font = Theme.FontBase(8.6f),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            var ver = new Label
            {
                Text = "uiA scratch preview  \u00B7  net48  \u00B7  " + Environment.MachineName + "  ",
                ForeColor = Theme.TextFaint,
                Font = Theme.FontBase(8.2f),
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 6, 12, 0)
            };
            statusBar.Controls.Add(_statusLabel);
            statusBar.Controls.Add(ver);

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = Theme.Sidebar };
            sidebar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };
            var brand = new Label
            {
                Text = "TRANG UI-A",
                ForeColor = Theme.TextFaint,
                Font = Theme.FontBold(8f),
                AutoSize = true,
                Left = 22,
                Top = 20
            };
            sidebar.Controls.Add(brand);

            int y = 56;
            for (int i = 0; i < _pages.Count; i++)
            {
                int idx = i;
                var page = _pages[i];
                var btn = new Button
                {
                    Text = "  " + page.PageIcon + "   " + page.PageTitle,
                    Left = 10,
                    Top = y,
                    Width = 204,
                    Height = 44,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.Sidebar,
                    ForeColor = Theme.TextDim,
                    Font = Theme.FontBold(10f),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    TabStop = false
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Theme.PanelAlt;
                btn.FlatAppearance.MouseDownBackColor = Theme.PanelAlt;
                btn.Click += (s, e) => SelectPage(idx);
                _navButtons.Add(btn);
                sidebar.Controls.Add(btn);
                y += 50;
            }

            var footer = new Label
            {
                Text = "Gunny 12.6  \u00B7  Request + DB + Bomb",
                ForeColor = Theme.TextFaint,
                Font = Theme.FontBase(8f),
                AutoSize = false,
                Height = 36,
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter
            };
            sidebar.Controls.Add(footer);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(16, 12, 16, 8) };

            Controls.Add(_content);
            Controls.Add(sidebar);
            Controls.Add(_titleBar);
            Controls.Add(statusBar);
        }

        private FlatButton SmallButton(string text, int width, EventHandler onClick)
        {
            var b = new FlatButton
            {
                Text = text,
                Width = width,
                Height = 30,
                Font = Theme.FontBase(9f),
                AccentColor = Theme.BgDeep,
                HoverColor = Theme.PanelAlt,
                Radius = 0,
                TabStop = false
            };
            b.Click += onClick;
            return b;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                try
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
                }
                catch { }
            }
        }

        private void SetPreset(int width, int height)
        {
            Size = new Size(width, height);
        }

        // ------------------------------------------------------------ pages

        private void SelectPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            for (int i = 0; i < _navButtons.Count; i++)
            {
                bool active = i == index;
                _navButtons[i].BackColor = active ? Theme.PanelAlt : Theme.Sidebar;
                _navButtons[i].ForeColor = active ? Theme.Text : Theme.TextDim;
            }
            var page = _pages[index];
            if (!_content.Controls.Contains(page))
            {
                _content.Controls.Clear();
                _content.Controls.Add(page);
            }
            _currentIndex = index;
            _pageInfoLabel.Text = page.PageIcon + "  " + page.PageTitle + "  \u00B7  " + (page.PageSubtitle ?? "");
            try { page.OnShownPage(); }
            catch (Exception ex) { try { _ctx.Log.Error("Loi trang " + page.PageTitle + ": " + ex.Message); } catch { } }
            page.BringToFront();
        }

        private void Ui(Action action)
        {
            if (IsHandleCreated && InvokeRequired) BeginInvoke(action); else action();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_exercise) BeginInvoke(new Action(async () => await ExerciseRun()));
            else if (_smoke) BeginInvoke(new Action(async () => await SmokeRun()));
        }

        // ------------------------------------------------------------ screenshot dir

        private static string ShotsDir()
        {
            // bin\Release -> uiA -> shots
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "shots"));
        }

        // ------------------------------------------------------------ smoke screenshots

        private async Task SmokeRun()
        {
            var shotsDir = ShotsDir();
            var log = new StringBuilder();
            log.AppendLine("uiA preview smoke - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                Directory.CreateDirectory(shotsDir);
                for (int i = 0; i < _pages.Count; i++)
                {
                    SelectPage(i);
                    await Task.Delay(i == 0 ? 9000 : 2500);
                    CaptureShot(shotsDir, "page" + (i + 1) + "-" + Slug(_pages[i].PageTitle) + "_1440x900", log);
                }

                SetPreset(1180, 720);
                await Task.Delay(800);
                SelectPage(0); await Task.Delay(900);
                CaptureShot(shotsDir, "dashboard_1180x720", log);
                SelectPage(1); await Task.Delay(1500);
                CaptureShot(shotsDir, "rip_1180x720", log);
                SelectPage(2); await Task.Delay(900);
                CaptureShot(shotsDir, "settings_1180x720", log);

                log.AppendLine();
                foreach (var p in _pages)
                {
                    log.AppendLine("PAGE " + p.PageTitle + " | icon=" + p.PageIcon + " | subtitle=" + p.PageSubtitle
                        + " | size=" + p.Width + "x" + p.Height + " | controls=" + CountControls(p));
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("SMOKE ERROR: " + ex);
            }
            try { File.WriteAllText(Path.Combine(shotsDir, "smoke.txt"), log.ToString(), Encoding.UTF8); } catch { }
            Close();
        }

        // ------------------------------------------------------------ functional exercise

        private async Task ExerciseRun()
        {
            var shotsDir = ShotsDir();
            var log = new StringBuilder();
            log.AppendLine("uiA preview exercise - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                Directory.CreateDirectory(shotsDir);

                // ---- 1) Rip + Promote (du lieu smoke, 4 file)
                SelectPage(1);
                await Task.Delay(1500);
                var ripBtn = FindButton(_pages[1], "Rip + Promote");
                log.AppendLine("Rip+Promote button: " + (ripBtn == null ? "NOT FOUND" : "found"));
                if (ripBtn != null)
                {
                    ripBtn.PerformClick();
                    var deadline = DateTime.Now.AddSeconds(150);
                    bool done = false;
                    while (DateTime.Now < deadline)
                    {
                        await Task.Delay(600);
                        if (ripBtn.Enabled) { done = true; break; }
                    }
                    log.AppendLine("rip finished: " + done);
                    await Task.Delay(1200);
                    CaptureShot(shotsDir, "exercise-rip-after-run", log);
                    log.AppendLine("grid rows: " + GridRows(_pages[1]));
                    log.AppendLine("stat cards: " + string.Join(" | ", StatValues(_pages[1]).ToArray()));
                }

                // ---- 2) Settings: test DB, scroll xuong, luu cai dat
                SelectPage(2);
                await Task.Delay(800);
                var dbBtn = FindButton(_pages[2], "Kiểm tra CSDL");
                log.AppendLine("Test DB button: " + (dbBtn == null ? "NOT FOUND" : "found"));
                if (dbBtn != null)
                {
                    dbBtn.PerformClick();
                    await Task.Delay(5000);
                    CaptureShot(shotsDir, "exercise-settings-db", log);
                    log.AppendLine("db state: " + FindTextByPrefix(_pages[2], new[] { "✓", "✗", "Kết nối", "Đang kiểm tra" }));
                }
                var scroll = FindScrollPanel(_pages[2]);
                log.AppendLine("settings scroll panel: " + (scroll == null ? "NOT FOUND" : "found"));
                if (scroll != null)
                {
                    scroll.AutoScrollPosition = new Point(0, 3000);
                    await Task.Delay(700);
                    CaptureShot(shotsDir, "exercise-settings-bottom", log);
                }
                var saveBtn = FindButton(_pages[2], "Lưu cài đặt");
                log.AppendLine("Save button: " + (saveBtn == null ? "NOT FOUND" : "found"));
                if (saveBtn != null)
                {
                    saveBtn.PerformClick();
                    await Task.Delay(1500);
                    CaptureShot(shotsDir, "exercise-settings-saved", log);
                    log.AppendLine("save state: " + FindTextByPrefix(_pages[2], new[] { "Đã lưu", "Đang dùng", "Lỗi" }));
                }

                // ---- 3) Dashboard: refresh
                SelectPage(0);
                await Task.Delay(1200);
                var refreshBtn = FindButton(_pages[0], "Làm mới");
                log.AppendLine("Refresh button: " + (refreshBtn == null ? "NOT FOUND" : "found"));
                if (refreshBtn != null)
                {
                    refreshBtn.PerformClick();
                    await Task.Delay(9000);
                    CaptureShot(shotsDir, "exercise-dashboard-refresh", log);
                    log.AppendLine("dashboard stats: " + string.Join(" | ", StatValues(_pages[0]).ToArray()));
                }
                log.AppendLine("EXERCISE OK");
            }
            catch (Exception ex)
            {
                log.AppendLine("EXERCISE ERROR: " + ex);
            }
            try { File.WriteAllText(Path.Combine(shotsDir, "exercise.txt"), log.ToString(), Encoding.UTF8); } catch { }
            Close();
        }

        private static Button FindButton(Control root, string textPart)
        {
            var buttons = new List<Button>();
            CollectButtons(root, buttons);
            foreach (var b in buttons)
            {
                if (!string.IsNullOrEmpty(b.Text) && b.Text.IndexOf(textPart, StringComparison.OrdinalIgnoreCase) >= 0) return b;
            }
            return null;
        }

        private static void CollectButtons(Control root, List<Button> output)
        {
            foreach (Control c in root.Controls)
            {
                var b = c as Button;
                if (b != null) output.Add(b);
                CollectButtons(c, output);
            }
        }

        private static int GridRows(Control root)
        {
            var grids = new List<DataGridView>();
            CollectGrids(root, grids);
            return grids.Count == 0 ? -1 : grids[0].RowCount;
        }

        private static void CollectGrids(Control root, List<DataGridView> output)
        {
            foreach (Control c in root.Controls)
            {
                var g = c as DataGridView;
                if (g != null) output.Add(g);
                CollectGrids(c, output);
            }
        }

        private static List<string> StatValues(Control root)
        {
            var result = new List<string>();
            CollectStats(root, result);
            return result;
        }

        private static void CollectStats(Control root, List<string> output)
        {
            foreach (Control c in root.Controls)
            {
                var card = c as StatCard;
                if (card != null) output.Add(card.Caption + "=" + card.Value);
                CollectStats(c, output);
            }
        }

        private static string FindTextByPrefix(Control root, string[] prefixes)
        {
            foreach (Control c in root.Controls)
            {
                var lbl = c as Label;
                if (lbl != null && !string.IsNullOrEmpty(lbl.Text))
                {
                    foreach (var p in prefixes)
                        if (lbl.Text.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return lbl.Text;
                }
                var nested = FindTextByPrefix(c, prefixes);
                if (nested != null) return nested;
            }
            return null;
        }

        private static ScrollableControl FindScrollPanel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var sc = c as ScrollableControl;
                if (sc != null && sc.AutoScroll) return sc;
                var nested = FindScrollPanel(c);
                if (nested != null) return nested;
            }
            return null;
        }

        // ------------------------------------------------------------ capture helpers

        private static int CountControls(Control root)
        {
            int n = 1;
            foreach (Control c in root.Controls) n += CountControls(c);
            return n;
        }

        private void CaptureShot(string dir, string name, StringBuilder log)
        {
            var path = Path.Combine(dir, name + ".png");
            try
            {
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(path, ImageFormat.Png);
                }
                var page = (_currentIndex >= 0 && _currentIndex < _pages.Count) ? _pages[_currentIndex] : null;
                log.AppendLine(name + " -> " + path
                    + " | form=" + Width + "x" + Height
                    + " | page=" + (page == null ? "?" : page.PageTitle)
                    + " | pageSize=" + (page == null ? "?" : page.Width + "x" + page.Height));
            }
            catch (Exception ex)
            {
                log.AppendLine(name + " CAPTURE ERROR: " + ex.Message);
            }
        }

        private static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "page";
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.Length == 0 ? "page" : sb.ToString();
        }
    }
}
