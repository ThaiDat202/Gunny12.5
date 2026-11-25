using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;
using GunnyRequestStudio.Pages;

namespace UiB
{
    /// <summary>
    /// Preview host cho 2 trang Import + Bomb (chi de build/test UI, khong thuoc src/App).
    /// Chay tu _dev/uiB: dotnet run -c Release
    ///   uiB.exe --dump layout.txt   -> ghi cay layout ra file de kiem tra kich thuoc
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
            {
                try { MessageBox.Show(e.Exception.ToString(), "uiB preview - loi UI", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            };
            var args = Environment.GetCommandLineArgs();
            try
            {
                var ctx = new StudioContext();
                bool dump = args.Length > 1 && string.Equals(args[1], "--dump", StringComparison.OrdinalIgnoreCase);
                bool shot = args.Length > 1 && string.Equals(args[1], "--shot", StringComparison.OrdinalIgnoreCase);
                bool bombscan = args.Length > 1 && string.Equals(args[1], "--bombscan", StringComparison.OrdinalIgnoreCase);
                if (dump)
                {
                    string outPath = args.Length > 2 ? args[2] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "layout.txt");
                    RunDump(ctx, outPath);
                    return;
                }
                if (shot)
                {
                    string outPath = args.Length > 2 ? args[2] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shot.png");
                    int page = args.Length > 3 ? int.Parse(args[3]) : 0;
                    int delay = args.Length > 4 ? int.Parse(args[4]) : 9000;
                    string action = args.Length > 5 ? args[5] : "";
                    RunShot(ctx, outPath, page, delay, action);
                    return;
                }
                if (bombscan)
                {
                    string outPath = args.Length > 2 ? args[2] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bombscan.txt");
                    RunBombScan(ctx, outPath);
                    return;
                }
                Application.Run(new PreviewForm(ctx));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "uiB preview - khong khoi dong duoc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RunBombScan(StudioContext ctx, string outPath)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sb = new StringBuilder();
            try
            {
                var svc = new BombService(ctx.Log);
                var mirror = ctx.Settings.MirrorDir;
                var ball = XmlCodec.Inspect(Path.Combine(mirror, "BallList.xml"));
                var cfg = XmlCodec.Inspect(Path.Combine(mirror, "bombconfig.xml"));
                sb.AppendLine("ball error=" + (ball.Error ?? "-") + " rows=" + ball.Rows.Count + " attrs=" + ball.Attributes.Count);
                sb.AppendLine("cfg  error=" + (cfg.Error ?? "-") + " rows=" + cfg.Rows.Count + " attrs=" + cfg.Attributes.Count);
                var ids = svc.CollectReferencedIds(ball, cfg);
                sb.AppendLine("referenced ids=" + ids.Count + " : " + string.Join(",", ids.Take(40)));
                sb.AppendLine("loading request docs...");
                var docs = ctx.LoadRequestDocs(null, true, null);
                sb.AppendLine("request docs=" + docs.Count);
                int done = 0;
                var infos = svc.Scan(ctx.Settings, docs, info =>
                {
                    int n = Interlocked.Increment(ref done);
                    if (n % 50 == 0) sb.AppendLine("  scanned " + n + " (" + sw.Elapsed.TotalSeconds.ToString("0.0") + "s)");
                }, System.Threading.CancellationToken.None);
                sw.Stop();
                sb.AppendLine("scan done: " + infos.Count + " ids in " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s");
                sb.AppendLine("withMask=" + infos.Count(i => i.ServerMask) + " missing=" + infos.Count(i => !i.ServerMask)
                              + " localAny=" + infos.Count(i => i.LocalCrater || i.LocalBrink || i.LocalBullet || i.LocalBlastOut));
                foreach (var i in infos.Take(12))
                {
                    sb.AppendLine(string.Format("  id={0} mask={1} crater={2} brink={3} bullet={4} blast={5} local={6}/{7}/{8}/{9} res={10} note={11}",
                        i.Id, i.ServerMask, i.CraterPng, i.CraterBrink, i.Bullet, i.BlastOut,
                        i.LocalCrater, i.LocalBrink, i.LocalBullet, i.LocalBlastOut, i.Resolution, i.Note));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR: " + ex);
            }
            try { File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8); } catch { }
        }

        private static void RunShot(StudioContext ctx, string outPath, int pageIndex, int delayMs, string action)
        {
            var form = new PreviewForm(ctx);
            form.Shown += (s, e) =>
            {
                if (pageIndex > 0) form.SelectPageForDump(pageIndex);
                if (!string.IsNullOrEmpty(action))
                {
                    int startDelay = string.Equals(action, "dryrun", StringComparison.OrdinalIgnoreCase) ? 12000 : 3000;
                    var start = new System.Windows.Forms.Timer { Interval = startDelay };
                    start.Tick += (s3, e3) =>
                    {
                        start.Stop();
                        try
                        {
                            var page = form.GetPage(pageIndex);
                            if (page != null)
                            {
                                object[] callArgs = null;
                                string method = null;
                                if (string.Equals(action, "scan", StringComparison.OrdinalIgnoreCase)) { method = "ScanAsync"; callArgs = new object[0]; }
                                if (string.Equals(action, "dryrun", StringComparison.OrdinalIgnoreCase)) { method = "StartImport"; callArgs = new object[] { true, true }; }
                                if (method != null)
                                {
                                    var mi = page.GetType().GetMethod(method,
                                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                    if (mi != null) mi.Invoke(page, callArgs);
                                }
                            }
                        }
                        catch { }
                    };
                    start.Start();
                }
                var timer = new System.Windows.Forms.Timer { Interval = Math.Max(1500, delayMs) };
                timer.Tick += (s2, e2) =>
                {
                    timer.Stop();
                    try
                    {
                        form.Activate();
                        form.TopMost = true;
                        Application.DoEvents();
                        Thread.Sleep(500);
                        var b = form.Bounds;
                        using (var bmp = new Bitmap(b.Width, b.Height))
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                            bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    catch { }
                    form.Close();
                };
                timer.Start();
            };
            Application.Run(form);
        }

        private static void RunDump(StudioContext ctx, string outPath)
        {
            var form = new PreviewForm(ctx);
            int pageIndex = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, e) =>
            {
                try
                {
                    string file = pageIndex == 0
                        ? outPath
                        : Path.Combine(Path.GetDirectoryName(outPath) ?? ".", Path.GetFileNameWithoutExtension(outPath) + "-" + pageIndex + ".txt");
                    using (var sw = new StreamWriter(file, false, Encoding.UTF8))
                    {
                        sw.WriteLine("Form client: " + form.ClientSize + "  bounds=" + form.Bounds);
                        Dump(sw, form, 0);
                    }
                }
                catch { }
                pageIndex++;
                if (pageIndex < form.PageCount)
                {
                    form.SelectPageForDump(pageIndex);
                }
                else
                {
                    timer.Stop();
                    form.Close();
                }
            };
            form.Shown += (s, e) => timer.Start();
            Application.Run(form);
        }

        private static void Dump(StreamWriter sw, Control c, int depth)
        {
            var pad = new string(' ', depth * 2);
            var text = c.Text == null ? "" : c.Text.Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 44) text = text.Substring(0, 44) + "…";
            sw.WriteLine("{0}{1} [{2}] bounds={3} margin={4} dock={5} anchor={6} vis={7} text=\"{8}\"",
                pad, c.GetType().Name, c.Name, c.Bounds, c.Margin, c.Dock, c.Anchor, c.Visible, text);
            var tlp = c as TableLayoutPanel;
            if (tlp != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < tlp.RowStyles.Count; i++)
                {
                    var rs = tlp.RowStyles[i];
                    sb.Append(i).Append(':').Append(rs.SizeType).Append('=').Append(rs.Height).Append("  ");
                }
                sw.WriteLine(pad + "  rows: " + sb);
                sb.Clear();
                for (int i = 0; i < tlp.ColumnStyles.Count; i++)
                {
                    var cs = tlp.ColumnStyles[i];
                    sb.Append(i).Append(':').Append(cs.SizeType).Append('=').Append(cs.Width).Append("  ");
                }
                sw.WriteLine(pad + "  cols: " + sb);
            }
            foreach (Control child in c.Controls) Dump(sw, child, depth + 1);
        }
    }

    internal sealed class PreviewForm : Form
    {
        private readonly StudioContext _ctx;
        private readonly Panel _content;
        private readonly Label _status;
        private readonly List<PageBase> _pages = new List<PageBase>();
        private readonly List<FlatButton> _nav = new List<FlatButton>();
        private int _current = -1;

        public PreviewForm(StudioContext ctx)
        {
            _ctx = ctx;
            Text = "Gunny Request Studio - uiB preview (Import + Bomb)";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1460, 940);
            MinimumSize = new Size(1180, 760);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.FontBase();
            DoubleBuffered = true;

            // ---------- header
            var top = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.BgDeep, Name = "topBar" };
            top.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, top.Height - 1, top.Width, top.Height - 1);
                using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 16, 14, 3, 30);
            };
            var title = new Label
            {
                Text = "\u25C8  uiB preview",
                Font = Theme.FontTitle(15f),
                ForeColor = Theme.Text,
                AutoSize = true,
                Left = 30,
                Top = 12
            };
            var sub = new Label
            {
                Text = "Import CSDL  ·  Bomb / Vũ khí   —   scratch project, không build src\\App",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextFaint,
                AutoSize = true,
                Left = 32,
                Top = 34
            };
            _status = new Label
            {
                Text = "sẵn sàng",
                Font = Theme.FontMono(8.4f),
                ForeColor = Theme.TextDim,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 620,
                Height = 20,
                Top = 18,
                Name = "statusLabel"
            };
            top.Controls.Add(title);
            top.Controls.Add(sub);
            top.Controls.Add(_status);
            top.Resize += (s, e) => { _status.Left = Math.Max(420, top.Width - _status.Width - 18); };

            // ---------- status bar
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Theme.BgDeep, Name = "bottomBar" };
            bottom.Paint += (s, e) => { using (var pen = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0); };
            var ver = new Label
            {
                Text = "mirror: " + (ctx.Settings == null ? "" : ctx.Settings.MirrorDir) + "   ·   reports: " + ctx.ReportsDir,
                Font = Theme.FontMono(8f),
                ForeColor = Theme.TextFaint,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            bottom.Controls.Add(ver);

            // ---------- nav
            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Theme.Bg,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 8, 0, 0),
                Name = "navBar"
            };

            // ---------- content
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(14, 10, 14, 8), Name = "content" };

            Controls.Add(_content);
            Controls.Add(nav);
            Controls.Add(bottom);
            Controls.Add(top);

            TryAddPage(new ImportPage(ctx));
            TryAddPage(new BombPage(ctx));

            for (int i = 0; i < _pages.Count; i++)
            {
                int idx = i;
                var btn = new FlatButton
                {
                    Text = _pages[i].PageIcon + "  " + _pages[i].PageTitle,
                    Width = 210,
                    Height = 34,
                    Margin = new Padding(0, 0, 8, 0),
                    AccentColor = Color.FromArgb(24, 36, 58),
                    HoverColor = Theme.PanelAlt,
                    Radius = 8
                };
                btn.Click += (s, e) => SelectPage(idx);
                _nav.Add(btn);
                nav.Controls.Add(btn);
            }

            _ctx.Log.OnLog += OnLog;
            SelectPage(0);
        }

        private void TryAddPage(PageBase page)
        {
            try
            {
                page.Dock = DockStyle.Fill;
                _pages.Add(page);
            }
            catch (Exception ex)
            {
                _status.Text = "loi tao trang: " + ex.Message;
            }
        }

        public int PageCount { get { return _pages.Count; } }

        public PageBase GetPage(int index)
        {
            return index >= 0 && index < _pages.Count ? _pages[index] : null;
        }

        public void SelectPageForDump(int index)
        {
            SelectPage(index);
        }

        private void SelectPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            if (_current == index && _content.Controls.Count > 0) return;
            _current = index;
            for (int i = 0; i < _nav.Count; i++)
            {
                _nav[i].AccentColor = i == index ? Color.FromArgb(30, 78, 112) : Color.FromArgb(24, 36, 58);
                _nav[i].Invalidate();
            }
            _content.Controls.Clear();
            var page = _pages[index];
            _content.Controls.Add(page);
            try { page.OnShownPage(); } catch (Exception ex) { _status.Text = "loi trang: " + ex.Message; }
            page.BringToFront();
        }

        private void OnLog(LogEntry e)
        {
            if (e == null) return;
            var ui = SynchronizationContext.Current;
            if (ui == null) return;
            try { ui.Post(_ => { if (!IsDisposed && _status != null) _status.Text = e.ToString(); }, null); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _ctx.Log.OnLog -= OnLog; } catch { }
            base.OnFormClosed(e);
        }
    }
}
