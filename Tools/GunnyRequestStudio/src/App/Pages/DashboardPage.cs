using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio.Pages
{
    /// <summary>
    /// Trang tong quan: hero card, 5 KPI (request / mirror / template map duoc / bang CSDL / bomb mask),
    /// checklist suc khoe (nha phat hanh, CSDL, duong dan) va nhat ky gan day.
    /// </summary>
    public class DashboardPage : PageBase
    {
        public override string PageTitle { get { return "Tổng quan"; } }
        public override string PageIcon { get { return "📊"; } }
        public override string PageSubtitle { get { return "Trạng thái tool trong một màn hình"; } }

        /// <summary>Su kien xin dieu huong trang (MainForm co the dang ky neu muon; hien tai chi log).</summary>
        public event Action<string> NavigationRequested;

        private readonly List<LogEntry> _recentLog = new List<LogEntry>();
        private const int MaxLogLines = 30;

        private StatCard _cardRequest, _cardMirror, _cardMapped, _cardDb, _cardBomb;
        private Label _lastRunLabel;
        private Label _lastRefreshLabel;
        private FlatButton _btnRefresh;
        private RichTextBox _logBox;
        private Panel _healthHost;
        private TableLayoutPanel _healthGrid;
        private ToolTip _tips;
        private int _refreshing;
        private bool _loadedOnce;
        private bool _needsRefresh;

        public DashboardPage(StudioContext ctx) : base(ctx)
        {
            BuildUi();
            try { Ctx.Log.OnLog += OnLogEntry; } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { Ctx.Log.OnLog -= OnLogEntry; } catch { }
                try { if (_tips != null) _tips.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        // ============================================================ lifecycle

        public override void OnShownPage()
        {
            if (!_loadedOnce)
            {
                _loadedOnce = true;
                _needsRefresh = false;
                InitLogFromHistory();
                RefreshAll();
            }
            else if (_needsRefresh) { _needsRefresh = false; InitLogFromHistory(); RefreshAll(); }
        }

        public override void OnSettingsChanged()
        {
            _needsRefresh = true;
            if (IsHandleCreated && Visible) { _needsRefresh = false; RefreshAll(); }
        }

        // ============================================================ layout

        private void BuildUi()
        {
            SuspendLayout();
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));    // header
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // body
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 146f));  // log console

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildBody(), 0, 1);
            root.Controls.Add(BuildLogCard(), 0, 2);
            Controls.Add(root);
            ResumeLayout(true);
        }

        private Control BuildHeader()
        {
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0) };
            var icon = new Label
            {
                Text = PageIcon,
                Font = new Font("Segoe UI Emoji", 21f),
                ForeColor = Theme.Accent,
                BackColor = Theme.Bg,
                AutoSize = false,
                Width = 42,
                Height = 40,
                Left = 0,
                Top = 4,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var title = new Label
            {
                Text = PageTitle,
                Font = Theme.FontTitle(21f),
                ForeColor = Theme.Text,
                BackColor = Theme.Bg,
                AutoSize = true,
                Left = 46,
                Top = 2
            };
            var sub = new Label
            {
                Text = PageSubtitle,
                Font = Theme.FontBase(9.2f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Bg,
                AutoSize = true,
                Left = 48,
                Top = 40
            };
            _lastRefreshLabel = new Label
            {
                Text = "",
                Font = Theme.FontBase(8.5f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Bg,
                AutoSize = false,
                Height = 22,
                Width = 210,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnRefresh = new FlatButton
            {
                Text = "Làm mới",
                Width = 116,
                Height = 34,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnRefresh.Click += (s, e) => RefreshAll();
            head.Controls.Add(icon);
            head.Controls.Add(title);
            head.Controls.Add(sub);
            head.Controls.Add(_lastRefreshLabel);
            head.Controls.Add(_btnRefresh);
            head.Resize += (s, e) =>
            {
                _btnRefresh.Left = Math.Max(300, head.Width - _btnRefresh.Width - 2);
                _btnRefresh.Top = 14;
                _lastRefreshLabel.Left = Math.Max(60, _btnRefresh.Left - _lastRefreshLabel.Width - 10);
                _lastRefreshLabel.Top = 20;
            };
            return head;
        }

        private Control BuildBody()
        {
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));  // hero
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));  // stat cards
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // health checklist

            body.Controls.Add(BuildHero(), 0, 0);
            body.Controls.Add(BuildStatsRow(), 0, 1);
            body.Controls.Add(BuildHealthCard(), 0, 2);
            return body;
        }

        private Control BuildHero()
        {
            var hero = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = Theme.Card,
                BorderColor = Theme.BorderSoft,
                Radius = 12,
                Padding = new Padding(10, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 8)
            };
            hero.Paint += (s, e) =>
            {
                try
                {
                    Theme.SetupGraphics(e.Graphics);
                    var r = new Rectangle(hero.Width - 430, 10, 410, Math.Max(10, hero.Height - 20));
                    using (var p = Theme.RoundedRect(r, 16))
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(0, Theme.Accent), Color.FromArgb(34, Theme.Accent), LinearGradientMode.Horizontal))
                        e.Graphics.FillPath(b, p);
                }
                catch { }
            };

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 336f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var icon = new Label
            {
                Text = "🔧",
                Font = new Font("Segoe UI Emoji", 25f),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };

            var texts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            texts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            texts.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            texts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            texts.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));

            var name = new Label
            {
                Text = "Gunny Request Studio",
                Font = Theme.FontTitle(17f),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            var desc = new Label
            {
                Text = "Rip request XML từ nhà phát hành vào bản mirror, promote template vào Request, import dữ liệu vào CSDL Db_Tank_All và đồng bộ bomb/weapon UI cho Gunny 12.6.",
                Font = Theme.FontBase(9f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 2, 12, 0)
            };
            _lastRunLabel = new Label
            {
                Text = "Chưa có dữ liệu rip/import gần đây.",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            texts.Controls.Add(name, 0, 0);
            texts.Controls.Add(desc, 0, 1);
            texts.Controls.Add(_lastRunLabel, 0, 2);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 18, 0, 0),
                Margin = new Padding(0)
            };
            var btnRip = new FlatButton
            {
                Text = "⬇  Rip ngay",
                Width = 140,
                Height = 40,
                AccentColor = Theme.Yellow,
                HoverColor = Theme.Orange,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnRip.Click += (s, e) => QuickAction("Rip Request", "Rip ngay");
            var btnImport = new FlatButton
            {
                Text = "⬆  Import ngay",
                Width = 152,
                Height = 40,
                AccentColor = Theme.AccentDeep,
                HoverColor = Theme.Accent,
                Margin = new Padding(0)
            };
            btnImport.Click += (s, e) => QuickAction("Import CSDL", "Import ngay");
            actions.Controls.Add(btnRip);
            actions.Controls.Add(btnImport);

            tlp.Controls.Add(icon, 0, 0);
            tlp.Controls.Add(texts, 1, 0);
            tlp.Controls.Add(actions, 2, 0);
            hero.Controls.Add(tlp);

            _tips = new ToolTip();
            _tips.SetToolTip(_lastRunLabel, "");
            return hero;
        }

        private Control BuildStatsRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            for (int i = 0; i < 5; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cardRequest = NewStat("File request", "📄", Theme.Accent, "*.xml trong RequestDir");
            _cardMirror = NewStat("Bản mirror", "🗂", Theme.Purple, "*.xml trong MirrorDir");
            _cardMapped = NewStat("Template map được", "🔗", Theme.Green, "đang tải…");
            _cardDb = NewStat("Bảng CSDL", "🗄", Theme.Yellow, "đang tải…");
            _cardBomb = NewStat("File bomb mask", "💣", Theme.Pink, "*.bomb · Fight\\Bomb");

            row.Controls.Add(_cardRequest, 0, 0);
            row.Controls.Add(_cardMirror, 1, 0);
            row.Controls.Add(_cardMapped, 2, 0);
            row.Controls.Add(_cardDb, 3, 0);
            row.Controls.Add(_cardBomb, 4, 0);
            return row;
        }

        private StatCard NewStat(string caption, string icon, Color color, string hint)
        {
            return new StatCard
            {
                Caption = caption,
                Icon = icon,
                ValueColor = color,
                Value = "…",
                Hint = hint,
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };
        }

        private Control BuildHealthCard()
        {
            Panel body;
            var card = MakeCard("Sức khỏe hệ thống", "Nhà phát hành · CSDL · đường dẫn", out body);
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);

            var scroll = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true, Margin = new Padding(0) };
            _healthGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 0,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _healthGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _healthGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _healthHost = scroll;
            scroll.Controls.Add(_healthGrid);
            body.Controls.Add(scroll);
            ShowHealthPlaceholder();
            return card;
        }

        private Control BuildLogCard()
        {
            Panel body;
            var card = MakeCard("Nhật ký gần đây", "30 dòng mới nhất · cập nhật trực tiếp", out body);
            card.Margin = new Padding(0, 8, 0, 0);
            _logBox = new RichTextBox { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_logBox);
            body.Controls.Add(_logBox);
            return card;
        }

        private ThemedPanel MakeCard(string title, string subtitle, out Panel body)
        {
            var card = new ThemedPanel
            {
                FillColor = Theme.Card,
                BorderColor = Theme.BorderSoft,
                Radius = 12,
                Padding = new Padding(14, 8, 14, 12),
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            int rows = string.IsNullOrEmpty(title) ? 1 : (string.IsNullOrEmpty(subtitle) ? 2 : 3);
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = rows,
                Margin = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            int row = 0;
            if (!string.IsNullOrEmpty(title))
            {
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
                tlp.Controls.Add(new Label
                {
                    Text = title,
                    Font = Theme.FontH2(11.5f),
                    ForeColor = Theme.Text,
                    BackColor = Theme.Card,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(2, 0, 2, 0)
                }, 0, row);
                row++;
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
                tlp.Controls.Add(new Label
                {
                    Text = subtitle,
                    Font = Theme.FontBase(8.7f),
                    ForeColor = Theme.TextDim,
                    BackColor = Theme.Card,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(2, 0, 2, 0)
                }, 0, row);
                row++;
            }
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0) };
            tlp.Controls.Add(body, 0, row);
            card.Controls.Add(tlp);
            return card;
        }

        // ============================================================ refresh

        private void RefreshAll()
        {
            if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0) return;
            if (_btnRefresh != null) _btnRefresh.Enabled = false;
            if (_lastRefreshLabel != null) _lastRefreshLabel.Text = "Đang tải…";
            Task.Run(() =>
            {
                try
                {
                    var stats = new DashStats();
                    try { ComputeStats(stats); }
                    catch (Exception ex) { stats.Error = ex.Message; Ctx.Log.Error("Tổng quan: không tải được số liệu: " + ex.Message); }
                    SafeUi(() => ApplyStats(stats));

                    var health = new List<HealthItem>();
                    try { ComputeHealth(health); }
                    catch (Exception ex)
                    {
                        health.Add(new HealthItem { Ok = false, Warn = false, Text = "Lỗi kiểm tra sức khỏe: " + ex.Message });
                        Ctx.Log.Error("Tổng quan: lỗi health check: " + ex.Message);
                    }
                    SafeUi(() => ApplyHealth(health));
                    SafeUi(() =>
                    {
                        if (_btnRefresh != null) _btnRefresh.Enabled = true;
                        if (_lastRefreshLabel != null) _lastRefreshLabel.Text = "Cập nhật lúc " + DateTime.Now.ToString("HH:mm:ss");
                    });
                }
                catch (Exception ex)
                {
                    try { Ctx.Log.Error("Tổng quan: lỗi làm mới: " + ex.Message); } catch { }
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshing, 0);
                }
            });
        }

        private void SafeUi(Action action)
        {
            try { UiInvoke(action); } catch { }
        }

        private static int CountFiles(string dir, string pattern)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
                return Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly).Length;
            }
            catch { return 0; }
        }

        private void ComputeStats(DashStats d)
        {
            var s = Ctx.Settings;
            d.RequestFiles = CountFiles(s.RequestDir, "*.xml");
            d.MirrorFiles = CountFiles(s.MirrorDir, "*.xml");
            d.BombFiles = CountFiles(Path.Combine(s.ServiceRoot ?? string.Empty, "Fight", "Bomb"), "*.bomb");

            // template map duoc: parse mirror + map sang bang CSDL (chay nen, cache lai)
            try
            {
                var docs = Ctx.GetRequestDocs(s.MirrorDir, false);
                d.MappedTotal = docs.Count;
                int mapped = 0, ambiguous = 0, none = 0;
                foreach (var doc in docs)
                {
                    try
                    {
                        var map = Ctx.MapDoc(doc);
                        if (DbImporter.IsImportable(map)) mapped++;
                        else if (map != null && map.Status == "ambiguous") ambiguous++;
                        else none++;
                    }
                    catch { none++; }
                }
                d.Mapped = mapped;
                d.MappedAmbiguous = ambiguous;
                d.MappedNone = none;
            }
            catch (Exception ex)
            {
                d.Mapped = -1;
                d.MappedError = ex.Message;
            }

            try { d.DbTables = Ctx.Schema.Tables.Count; }
            catch (Exception ex) { d.DbTables = -1; d.DbError = ex.Message; }

            d.LastRip = ReadLastRip();
            d.LastImport = ReadLastImport();
        }

        private string ReadLastRip()
        {
            try
            {
                var path = Path.Combine(Ctx.ReportsDir, "request-rip-report.json");
                var report = Json.LoadFile<RipReport>(path);
                if (report == null) return null;
                return "Rip gần nhất: " + FormatTime(report.GeneratedAt) + " · OK " + report.Stats.Ok + "/" + report.Stats.Total
                       + " file · " + Paths.FormatSize(report.Stats.Bytes);
            }
            catch { return null; }
        }

        private string ReadLastImport()
        {
            try
            {
                var path = Path.Combine(Ctx.ReportsDir, "import-report.json");
                var report = Json.LoadFile<ImportReport>(path);
                if (report == null) return null;
                return "Import gần nhất: " + FormatTime(report.GeneratedAt) + " · " + report.Imported + "/" + report.Entries.Count
                       + " bảng · " + report.Failed + " lỗi";
            }
            catch { return null; }
        }

        private static string FormatTime(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "?";
            DateTime dt;
            if (DateTime.TryParse(iso, out dt)) return dt.ToString("HH:mm dd/MM");
            return iso;
        }

        private void ApplyStats(DashStats d)
        {
            if (_cardRequest == null || _cardRequest.IsDisposed) return;
            _cardRequest.Value = d.RequestFiles.ToString("N0");
            _cardRequest.Hint = "*.xml trong RequestDir";
            _cardMirror.Value = d.MirrorFiles.ToString("N0");
            _cardMirror.Hint = "*.xml trong MirrorDir";
            _cardBomb.Value = d.BombFiles.ToString("N0");
            _cardBomb.Hint = "*.bomb · Fight\\Bomb";

            if (d.Mapped < 0)
            {
                _cardMapped.Value = "—";
                _cardMapped.Hint = Trim(d.MappedError, 40);
            }
            else
            {
                _cardMapped.Value = d.Mapped.ToString("N0");
                _cardMapped.Hint = d.Mapped + "/" + d.MappedTotal + " mirror map được";
            }

            if (d.DbTables < 0)
            {
                _cardDb.Value = "—";
                _cardDb.Hint = "không kết nối được CSDL";
            }
            else
            {
                _cardDb.Value = d.DbTables.ToString("N0");
                _cardDb.Hint = "bảng trong Db_Tank_All";
            }

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(d.LastRip)) parts.Add(d.LastRip);
            if (!string.IsNullOrEmpty(d.LastImport)) parts.Add(d.LastImport);
            if (parts.Count == 0) parts.Add("Chưa có báo cáo rip/import trong " + Ctx.ReportsDir);
            var text = string.Join("   ·   ", parts.ToArray());
            if (_lastRunLabel != null)
            {
                _lastRunLabel.Text = text;
                if (_tips != null) _tips.SetToolTip(_lastRunLabel, text);
            }
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        // ============================================================ health checklist

        private class HealthItem
        {
            public bool Ok;
            public bool Warn;
            public string Text;
            public string Tip;
        }

        private void ComputeHealth(List<HealthItem> items)
        {
            var s = Ctx.Settings;
            var bases = s.PublisherBases ?? new List<string>();
            if (bases.Count == 0)
            {
                items.Add(new HealthItem { Ok = false, Warn = true, Text = "⚠  Chưa cấu hình nhà phát hành" });
            }
            else
            {
                foreach (var b in bases)
                {
                    if (string.IsNullOrWhiteSpace(b)) continue;
                    var url = HttpUtil.Combine(b, "BallList.xml");
                    string statusText;
                    bool ok;
                    string tip = url;
                    try
                    {
                        var r = HttpUtil.Head(url, 8000, 1);
                        ok = r.Status == 200;
                        statusText = ok ? "OK (HTTP 200)" : (r.Status == 0 ? "không phản hồi" : "HTTP " + r.Status);
                        if (!string.IsNullOrEmpty(r.Error)) tip = url + "  ·  " + r.Error;
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        statusText = "lỗi: " + ex.Message;
                    }
                    items.Add(new HealthItem
                    {
                        Ok = ok,
                        Warn = !ok,
                        Text = (ok ? "✓  " : "⚠  ") + "Nhà phát hành " + ShortUrl(b) + " — " + statusText,
                        Tip = tip
                    });
                }
            }

            try
            {
                string msg;
                bool ok = Ctx.TestDb(out msg);
                items.Add(new HealthItem
                {
                    Ok = ok,
                    Warn = false,
                    Text = (ok ? "✓  " : "✗  ") + "CSDL Db_Tank_All — " + Trim(msg, 84),
                    Tip = msg
                });
            }
            catch (Exception ex)
            {
                items.Add(new HealthItem { Ok = false, Text = "✗  CSDL Db_Tank_All — " + ex.Message, Tip = ex.ToString() });
            }

            AddPath(items, "Request", s.RequestDir, false);
            AddPath(items, "Mirror", s.MirrorDir, false);
            AddPath(items, "ServiceRoot", s.ServiceRoot, false);
            AddPath(items, "Full Source", s.FullSourceRoot, false);
            AddPath(items, "WebSite", s.WebSiteRoot, false);
            AddPath(items, "Bomb asset", s.BombAssetDir, false);
            AddPath(items, "Service Bomb", Path.Combine(s.ServiceRoot ?? string.Empty, "Fight", "Bomb"), false);
            AddPath(items, "config.xml", s.ClientConfigPath, true);
        }

        private static void AddPath(List<HealthItem> items, string label, string path, bool isFile)
        {
            bool ok;
            try { ok = isFile ? File.Exists(path) : Directory.Exists(path); }
            catch { ok = false; }
            var shown = string.IsNullOrEmpty(path) ? "(trống)" : path;
            items.Add(new HealthItem
            {
                Ok = ok,
                Warn = !ok,
                Text = (ok ? "✓  " : "⚠  ") + label + " — " + shown,
                Tip = shown
            });
        }

        private static string ShortUrl(string url)
        {
            try
            {
                var u = new Uri(url);
                return u.Host;
            }
            catch { return (url ?? "").TrimEnd('/'); }
        }

        private void ShowHealthPlaceholder()
        {
            if (_healthGrid == null) return;
            _healthGrid.SuspendLayout();
            _healthGrid.Controls.Clear();
            _healthGrid.RowStyles.Clear();
            _healthGrid.RowCount = 1;
            _healthGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            var placeholder = new Label
            {
                Text = "Đang kiểm tra nhà phát hành, CSDL và đường dẫn…",
                Font = Theme.FontBase(8.8f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 0, 2, 0)
            };
            _healthGrid.Controls.Add(placeholder, 0, 0);
            if (_healthGrid.ColumnStyles.Count < 2) _healthGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _healthGrid.SetColumnSpan(placeholder, 2);
            _healthGrid.ResumeLayout(true);
        }

        private void ApplyHealth(List<HealthItem> items)
        {
            if (_healthGrid == null || _healthGrid.IsDisposed) return;
            _healthGrid.SuspendLayout();
            _healthGrid.Controls.Clear();
            _healthGrid.RowStyles.Clear();
            while (_healthGrid.ColumnStyles.Count > 2) _healthGrid.ColumnStyles.RemoveAt(_healthGrid.ColumnStyles.Count - 1);
            if (_healthGrid.ColumnStyles.Count < 2) _healthGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            int rows = (items.Count + 1) / 2;
            _healthGrid.RowCount = Math.Max(1, rows);
            for (int r = 0; r < rows; r++) _healthGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var lbl = new Label
                {
                    Text = item.Text,
                    Font = Theme.FontBase(8.8f),
                    ForeColor = item.Ok ? Theme.Green : (item.Warn ? Theme.Yellow : Theme.Red),
                    BackColor = Theme.Card,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Margin = new Padding(2, 0, 12, 0)
                };
                if (_tips != null && !string.IsNullOrEmpty(item.Tip)) _tips.SetToolTip(lbl, item.Tip);
                _healthGrid.Controls.Add(lbl, i % 2, i / 2);
            }
            if (items.Count == 0)
            {
                _healthGrid.RowCount = 1;
                _healthGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
                _healthGrid.Controls.Add(new Label
                {
                    Text = "Chưa có kết quả kiểm tra.",
                    Font = Theme.FontBase(8.8f),
                    ForeColor = Theme.TextFaint,
                    BackColor = Theme.Card,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(2, 0, 2, 0)
                }, 0, 0);
            }
            _healthGrid.ResumeLayout(true);
        }

        // ============================================================ log console

        private void OnLogEntry(LogEntry entry)
        {
            if (entry == null) return;
            if (IsHandleCreated && !Visible) return;   // trang dang an: khong render lai log
            SafeUi(() =>
            {
                _recentLog.Add(entry);
                while (_recentLog.Count > MaxLogLines) _recentLog.RemoveAt(0);
                RenderLog();
            });
        }

        /// <summary>Nap 30 dong log cuoi tu history khi trang duoc mo lan dau.</summary>
        private void InitLogFromHistory()
        {
            try
            {
                var history = Ctx.Log.History;
                if (history == null) return;
                LogEntry[] snapshot;
                try { snapshot = history.ToArray(); } catch { return; }
                _recentLog.Clear();
                for (int i = Math.Max(0, snapshot.Length - MaxLogLines); i < snapshot.Length; i++)
                    _recentLog.Add(snapshot[i]);
                RenderLog();
            }
            catch { }
        }

        private void RenderLog()
        {
            if (_logBox == null || _logBox.IsDisposed) return;
            _logBox.SuspendLayout();
            _logBox.Clear();
            foreach (var e in _recentLog)
            {
                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.SelectionLength = 0;
                _logBox.SelectionColor = LevelColor(e.Level);
                _logBox.AppendText(e.ToString() + Environment.NewLine);
            }
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionColor = Theme.Text;
            _logBox.ScrollToCaret();
            _logBox.ResumeLayout();
        }

        private static Color LevelColor(string level)
        {
            switch ((level ?? "").ToUpperInvariant())
            {
                case "OK": return Theme.Green;
                case "WARN": return Theme.Yellow;
                case "ERR": return Theme.Red;
                case "STEP": return Theme.Accent;
                default: return Theme.TextDim;
            }
        }

        private void QuickAction(string page, string action)
        {
            Ctx.Log.Info("Tổng quan: yêu cầu mở trang " + page + " (" + action + ")");
            Ctx.SetStatus("Mở trang " + page + "…");
            var h = NavigationRequested;
            if (h != null) { try { h(page); } catch { } }
        }

        private class DashStats
        {
            public int RequestFiles;
            public int MirrorFiles;
            public int Mapped = -1;
            public int MappedTotal;
            public int MappedAmbiguous;
            public int MappedNone;
            public string MappedError;
            public int DbTables = -1;
            public string DbError;
            public int BombFiles;
            public string LastRip;
            public string LastImport;
            public string Error;
        }
    }
}
