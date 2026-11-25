using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio.Pages
{
    /// <summary>
    /// Trang cai dat: sua toan bo AppSettings (duong dan, nha phat hanh, CSDL, tuy chon),
    /// kiem tra CSDL, luu / khoi phuc mac dinh, mo nhanh cac thu muc.
    /// </summary>
    public class SettingsPage : PageBase
    {
        public override string PageTitle { get { return "Cài đặt"; } }
        public override string PageIcon { get { return "⚙"; } }
        public override string PageSubtitle { get { return "Đường dẫn · nhà phát hành · CSDL · tùy chọn"; } }

        private readonly List<LogEntry> _log = new List<LogEntry>();
        private const int MaxLogLines = 120;

        private TextBox _txtRequestDir, _txtMirrorDir, _txtServiceRoot, _txtFullSource, _txtWebSite;
        private TextBox _txtClientConfig, _txtBombAsset, _txtReportsDir;
        private TextBox _txtPublisherBases, _txtResBases, _txtDbConnection;
        private NumericUpDown _numConcurrency;
        private CheckBox _chkBackup, _chkPromoteTemplates;
        private Label _lblDbState, _lblSaveState;
        private PathLabel _lblSettingsPath, _lblOverridesPath;
        private FlatButton _btnTestDb, _btnSave, _btnRestoreDb, _btnOpenRequest, _btnOpenMirror, _btnOpenReports;
        private RichTextBox _logBox;
        private ToolTip _tips;

        private bool _loadedOnce;
        private bool _applying;
        private bool _logDirty;

        public SettingsPage(StudioContext ctx) : base(ctx)
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
                InitLogFromHistory();
                LoadFromSettings(Ctx.Settings);
            }
            if (_logDirty) { _logDirty = false; RenderLog(); }
        }

        public override void OnSettingsChanged()
        {
            if (_applying) return;
            LoadFromSettings(Ctx.Settings);
            AddLocalLog("INFO", "Cấu hình vừa được thay đổi từ nơi khác — đã nạp lại form.");
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
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildScrollBody(), 0, 1);
            root.Controls.Add(BuildLogCard(), 0, 2);
            Controls.Add(root);
            ResumeLayout(true);
            _tips = new ToolTip();
        }

        private Control BuildHeader()
        {
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0) };
            var icon = new Label
            {
                Text = PageIcon,
                Font = new Font("Segoe UI Emoji", 19f),
                ForeColor = Theme.Accent,
                BackColor = Theme.Bg,
                AutoSize = false,
                Width = 40,
                Height = 38,
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
                Left = 44,
                Top = 1
            };
            var sub = new Label
            {
                Text = PageSubtitle,
                Font = Theme.FontBase(9.2f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Bg,
                AutoSize = true,
                Left = 46,
                Top = 38
            };
            _btnRestoreDb = new FlatButton
            {
                Text = "Khôi phục mặc định",
                Width = 162,
                Height = 32,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnRestoreDb.Click += (s, e) => RestoreDefaults();
            _btnSave = new FlatButton
            {
                Text = "Lưu cài đặt",
                Width = 132,
                Height = 32,
                AccentColor = Theme.GreenDeep,
                HoverColor = Theme.Green,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnSave.Click += (s, e) => SaveAll();
            _btnTestDb = new FlatButton
            {
                Text = "Kiểm tra CSDL",
                Width = 132,
                Height = 32,
                AccentColor = Theme.AccentDeep,
                HoverColor = Theme.Accent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnTestDb.Click += (s, e) => TestConnectionAsync();

            head.Controls.Add(icon);
            head.Controls.Add(title);
            head.Controls.Add(sub);
            head.Controls.Add(_btnRestoreDb);
            head.Controls.Add(_btnSave);
            head.Controls.Add(_btnTestDb);
            head.Resize += (s, e) =>
            {
                _btnRestoreDb.Left = Math.Max(520, head.Width - _btnRestoreDb.Width - 2);
                _btnRestoreDb.Top = 14;
                _btnSave.Left = _btnRestoreDb.Left - _btnSave.Width - 10;
                _btnSave.Top = 14;
                _btnTestDb.Left = _btnSave.Left - _btnTestDb.Width - 10;
                _btnTestDb.Top = 14;
            };
            return head;
        }

        private Control BuildScrollBody()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                AutoScroll = true,
                Margin = new Padding(0)
            };
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 5,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 366f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 258f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 200f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 168f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 170f));

            stack.Controls.Add(BuildPathsCard(), 0, 0);
            stack.Controls.Add(BuildPublishersCard(), 0, 1);
            stack.Controls.Add(BuildDbCard(), 0, 2);
            stack.Controls.Add(BuildOptionsCard(), 0, 3);
            stack.Controls.Add(BuildFoldersCard(), 0, 4);

            scroll.Controls.Add(stack);
            return scroll;
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
                Margin = new Padding(0, 0, 0, 10)
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

        // ============================================================ cards

        private Control BuildPathsCard()
        {
            Panel body;
            var card = MakeCard("Đường dẫn", "Các thư mục tool đọc / ghi dữ liệu", out body);
            var grid = NewFormGrid(8);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76f));

            _txtRequestDir = NewTextBox();
            _txtMirrorDir = NewTextBox();
            _txtServiceRoot = NewTextBox();
            _txtFullSource = NewTextBox();
            _txtWebSite = NewTextBox();
            _txtClientConfig = NewTextBox();
            _txtBombAsset = NewTextBox();
            _txtReportsDir = NewTextBox();

            int r = 0;
            AddPathRow(grid, r++, "Request", _txtRequestDir, true);
            AddPathRow(grid, r++, "Mirror request", _txtMirrorDir, true);
            AddPathRow(grid, r++, "Service root", _txtServiceRoot, true);
            AddPathRow(grid, r++, "Full Source", _txtFullSource, true);
            AddPathRow(grid, r++, "WebSite root", _txtWebSite, true);
            AddPathRow(grid, r++, "Client config", _txtClientConfig, false);
            AddPathRow(grid, r++, "Bomb asset dir", _txtBombAsset, true);
            AddPathRow(grid, r++, "Reports dir", _txtReportsDir, true);

            body.Controls.Add(grid);
            return card;
        }

        private Control BuildPublishersCard()
        {
            Panel body;
            var card = MakeCard("Nhà phát hành & CDN", "Mỗi dòng một URL (kết thúc bằng /)", out body);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            var lblPub = NewFieldLabel("PublisherBases (quest.gn.zing.vn …)");
            _txtPublisherBases = NewMultiTextBox(1);
            _txtPublisherBases.AcceptsReturn = true;
            _txtPublisherBases.ScrollBars = ScrollBars.Vertical;

            var lblRes = NewFieldLabel("ResBases (res.gn.zing.vn, gunny.vcdn.vn …)");
            _txtResBases = NewMultiTextBox(1);
            _txtResBases.AcceptsReturn = true;
            _txtResBases.ScrollBars = ScrollBars.Vertical;

            grid.Controls.Add(lblPub, 0, 0);
            grid.Controls.Add(_txtPublisherBases, 0, 1);
            grid.Controls.Add(lblRes, 0, 2);
            grid.Controls.Add(_txtResBases, 0, 3);
            body.Controls.Add(grid);
            return card;
        }

        private Control BuildDbCard()
        {
            Panel body;
            var card = MakeCard("CSDL SQL Server", "Chuỗi kết nối tới Db_Tank_All", out body);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            var lbl = NewFieldLabel("DbConnection");
            _txtDbConnection = NewMultiTextBox(2);
            _txtDbConnection.WordWrap = true;
            _txtDbConnection.ScrollBars = ScrollBars.Vertical;

            var btnTest = new FlatButton
            {
                Text = "Kiểm tra CSDL",
                Width = 124,
                Height = 28,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0, 2, 0, 0)
            };
            btnTest.Click += (s, e) => TestConnectionAsync();

            _lblDbState = new Label
            {
                Text = "Chưa kiểm tra trong phiên này.",
                Font = Theme.FontBase(8.8f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 10, 0)
            };

            grid.Controls.Add(lbl, 0, 0);
            grid.SetColumnSpan(lbl, 2);
            grid.Controls.Add(_txtDbConnection, 0, 1);
            grid.SetColumnSpan(_txtDbConnection, 2);
            grid.Controls.Add(_lblDbState, 0, 2);
            grid.Controls.Add(btnTest, 1, 2);
            body.Controls.Add(grid);
            return card;
        }

        private Control BuildOptionsCard()
        {
            Panel body;
            var card = MakeCard("Tùy chọn rip / import", "Áp dụng cho các lần chạy tiếp theo", out body);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

            var rowConc = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            rowConc.Controls.Add(new Label
            {
                Text = "Số luồng tải (Concurrency):",
                Font = Theme.FontBase(9f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                AutoSize = false,
                Width = 170,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            });
            _numConcurrency = new NumericUpDown
            {
                Width = 72,
                Height = 30,
                Minimum = 1,
                Maximum = 16,
                Value = 8,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBase(9.5f),
                Margin = new Padding(0, 2, 10, 0)
            };
            rowConc.Controls.Add(_numConcurrency);
            rowConc.Controls.Add(new Label
            {
                Text = "(1 – 16)",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                AutoSize = true,
                Height = 30,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0)
            });

            _chkBackup = NewCheck("Sao lưu bảng trước khi ghi đè (BackupBeforeReplace)");
            _chkPromoteTemplates = NewCheck("Promote template vào Request sau khi rip (PromoteTemplatesToRequest)");

            grid.Controls.Add(rowConc, 0, 0);
            grid.SetColumnSpan(rowConc, 2);
            grid.Controls.Add(_chkBackup, 0, 1);
            grid.SetColumnSpan(_chkBackup, 2);
            grid.Controls.Add(_chkPromoteTemplates, 0, 2);
            grid.SetColumnSpan(_chkPromoteTemplates, 2);
            body.Controls.Add(grid);
            return card;
        }

        private Control BuildFoldersCard()
        {
            Panel body;
            var card = MakeCard("Thư mục & tệp cấu hình", "Mở nhanh bằng Explorer", out body);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _lblSettingsPath = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 1) };
            _lblOverridesPath = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 1) };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };
            _btnOpenRequest = NewSmallButton("Mở Request");
            _btnOpenRequest.Click += (s, e) => OpenFolder(_txtRequestDir.Text, "Request");
            _btnOpenMirror = NewSmallButton("Mở Mirror");
            _btnOpenMirror.Click += (s, e) => OpenFolder(_txtMirrorDir.Text, "Mirror");
            _btnOpenReports = NewSmallButton("Mở Reports");
            _btnOpenReports.Click += (s, e) => OpenFolder(Ctx.ReportsDir, "Reports");
            buttons.Controls.Add(_btnOpenRequest);
            buttons.Controls.Add(_btnOpenMirror);
            buttons.Controls.Add(_btnOpenReports);

            _lblSaveState = new Label
            {
                Text = "",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                AutoSize = false,
                Width = 340,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0)
            };
            buttons.Controls.Add(_lblSaveState);

            grid.Controls.Add(_lblSettingsPath, 0, 0);
            grid.Controls.Add(_lblOverridesPath, 0, 1);
            grid.Controls.Add(buttons, 0, 2);
            body.Controls.Add(grid);
            return card;
        }

        // ============================================================ small helpers

        private TableLayoutPanel NewFormGrid(int rowCount)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 3,
                RowCount = rowCount,
                Margin = new Padding(0)
            };
            for (int i = 0; i < rowCount; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            return grid;
        }

        private void AddPathRow(TableLayoutPanel grid, int row, string label, TextBox box, bool isFolder)
        {
            var lbl = new Label
            {
                Text = label + ":",
                Font = Theme.FontBase(9f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 8, 0)
            };
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 4, 8, 4);
            var btn = new FlatButton
            {
                Text = isFolder ? "Chọn…" : "Tệp…",
                Width = 68,
                Height = 28,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            var copy = box;
            btn.Click += (s, e) => BrowsePath(copy, isFolder);
            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(box, 1, row);
            grid.Controls.Add(btn, 2, row);
        }

        private TextBox NewTextBox()
        {
            var t = new TextBox { BorderStyle = BorderStyle.FixedSingle };
            Style.Apply(t);
            return t;
        }

        private TextBox NewMultiTextBox(int lines)
        {
            var t = new TextBox
            {
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false,
                Height = Math.Max(46, lines * 22),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2)
            };
            Style.Apply(t);
            return t;
        }

        private Label NewFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = Theme.FontBase(8.9f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
        }

        private CheckBox NewCheck(string text)
        {
            return new CheckBox
            {
                Text = text,
                AutoSize = true,
                Height = 24,
                Font = Theme.FontBase(9f),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 0)
            };
        }

        private FlatButton NewSmallButton(string text)
        {
            return new FlatButton
            {
                Text = text,
                Width = 126,
                Height = 32,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Margin = new Padding(0, 2, 8, 0)
            };
        }

        // ============================================================ data binding

        private void LoadFromSettings(AppSettings s)
        {
            if (s == null) return;
            if (_txtRequestDir == null) return;
            _txtRequestDir.Text = s.RequestDir ?? "";
            _txtMirrorDir.Text = s.MirrorDir ?? "";
            _txtServiceRoot.Text = s.ServiceRoot ?? "";
            _txtFullSource.Text = s.FullSourceRoot ?? "";
            _txtWebSite.Text = s.WebSiteRoot ?? "";
            _txtClientConfig.Text = s.ClientConfigPath ?? "";
            _txtBombAsset.Text = s.BombAssetDir ?? "";
            _txtReportsDir.Text = s.ReportsDir ?? "";
            _txtPublisherBases.Lines = (s.PublisherBases ?? new List<string>()).ToArray();
            _txtResBases.Lines = (s.ResBases ?? new List<string>()).ToArray();
            _txtDbConnection.Text = s.DbConnection ?? "";
            _numConcurrency.Value = Clamp(s.Concurrency, 1, 16);
            _chkBackup.Checked = s.BackupBeforeReplace;
            _chkPromoteTemplates.Checked = s.PromoteTemplatesToRequest;
            UpdatePathLabels();
            SetSaveState("Đang dùng cấu hình đã lưu.", Theme.TextFaint);
            if (_lblDbState != null)
            {
                _lblDbState.Text = "Trạng thái phiên này: " + (Ctx.DbStatus ?? "chưa kết nối");
                _lblDbState.ForeColor = Theme.TextFaint;
            }
        }

        private void UpdatePathLabels()
        {
            if (_lblSettingsPath != null) _lblSettingsPath.Text = "settings.json  →  " + Paths.SettingsPath();
            if (_lblOverridesPath != null) _lblOverridesPath.Text = "table-map.overrides.json  →  " + Paths.OverridesPath();
            if (_tips != null)
            {
                if (_lblSettingsPath != null) _tips.SetToolTip(_lblSettingsPath, Paths.SettingsPath());
                if (_lblOverridesPath != null) _tips.SetToolTip(_lblOverridesPath, Paths.OverridesPath());
            }
        }

        private AppSettings CollectSettings()
        {
            var s = Ctx.Settings == null ? AppSettings.CreateDefault() : Ctx.Settings.Clone();
            s.RequestDir = Clean(_txtRequestDir.Text);
            s.MirrorDir = Clean(_txtMirrorDir.Text);
            s.ServiceRoot = Clean(_txtServiceRoot.Text);
            s.FullSourceRoot = Clean(_txtFullSource.Text);
            s.WebSiteRoot = Clean(_txtWebSite.Text);
            s.ClientConfigPath = Clean(_txtClientConfig.Text);
            s.BombAssetDir = Clean(_txtBombAsset.Text);
            s.ReportsDir = Clean(_txtReportsDir.Text);
            s.PublisherBases = SplitLines(_txtPublisherBases.Text);
            s.ResBases = SplitLines(_txtResBases.Text);
            s.DbConnection = (_txtDbConnection.Text ?? "").Trim();
            s.Concurrency = (int)_numConcurrency.Value;
            s.BackupBeforeReplace = _chkBackup.Checked;
            s.PromoteTemplatesToRequest = _chkPromoteTemplates.Checked;
            return s;
        }

        private static string Clean(string text)
        {
            return (text ?? "").Trim().Trim('"');
        }

        private static List<string> SplitLines(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var v = (raw ?? "").Trim();
                if (v.Length == 0) continue;
                if (!list.Contains(v, StringComparer.OrdinalIgnoreCase)) list.Add(v);
            }
            return list;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ============================================================ actions

        private bool ValidateSettings(AppSettings s, out string error, out List<string> warnings)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(s.RequestDir)) missing.Add("RequestDir");
            if (string.IsNullOrWhiteSpace(s.MirrorDir)) missing.Add("MirrorDir");
            if (string.IsNullOrWhiteSpace(s.ServiceRoot)) missing.Add("ServiceRoot");
            if (string.IsNullOrWhiteSpace(s.WebSiteRoot)) missing.Add("WebSiteRoot");
            if (string.IsNullOrWhiteSpace(s.DbConnection)) missing.Add("DbConnection");
            if (missing.Count > 0)
            {
                error = "Thiếu đường dẫn bắt buộc: " + string.Join(", ", missing.ToArray());
                warnings = new List<string>();
                return false;
            }
            if (s.DbConnection.IndexOf('=') < 0)
            {
                error = "Chuỗi kết nối CSDL không hợp lệ (thiếu khóa=giá trị).";
                warnings = new List<string>();
                return false;
            }
            warnings = new List<string>();
            WarnIfMissing(warnings, "RequestDir", s.RequestDir, true);
            WarnIfMissing(warnings, "MirrorDir", s.MirrorDir, true);
            WarnIfMissing(warnings, "ServiceRoot", s.ServiceRoot, true);
            WarnIfMissing(warnings, "FullSourceRoot", s.FullSourceRoot, true);
            WarnIfMissing(warnings, "WebSiteRoot", s.WebSiteRoot, true);
            WarnIfMissing(warnings, "ClientConfigPath", s.ClientConfigPath, false);
            WarnIfMissing(warnings, "BombAssetDir", s.BombAssetDir, true);
            WarnIfMissing(warnings, "ReportsDir", Paths.Resolve(s.ReportsDir), true);
            if (s.PublisherBases == null || s.PublisherBases.Count == 0) warnings.Add("PublisherBases đang trống — rip sẽ không tải được từ nhà phát hành.");
            error = null;
            return true;
        }

        private static void WarnIfMissing(List<string> warnings, string name, string path, bool isFolder)
        {
            bool ok;
            try { ok = isFolder ? Directory.Exists(path) : File.Exists(path); }
            catch { ok = false; }
            if (!ok) warnings.Add(name + " không tồn tại: " + (string.IsNullOrEmpty(path) ? "(trống)" : path));
        }

        private void SaveAll()
        {
            try
            {
                var s = CollectSettings();
                string error;
                List<string> warnings;
                if (!ValidateSettings(s, out error, out warnings))
                {
                    Ctx.Log.Error("Cài đặt chưa hợp lệ: " + error);
                    SetSaveState(error, Theme.Red);
                    MessageBox.Show(this, error, "Cài đặt chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _applying = true;
                try { Ctx.ApplySettings(s); }
                finally { _applying = false; }
                Ctx.Log.Okay("Đã lưu cài đặt vào " + Paths.SettingsPath());
                foreach (var w in warnings)
                {
                    try { Ctx.Log.Warn("Cảnh báo cấu hình: " + w); } catch { }
                }
                UpdatePathLabels();
                SetSaveState("Đã lưu lúc " + DateTime.Now.ToString("HH:mm:ss") + (warnings.Count > 0 ? " · " + warnings.Count + " cảnh báo" : ""),
                    warnings.Count > 0 ? Theme.Yellow : Theme.Green);
                Ctx.SetStatus("Đã lưu cài đặt");
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Lưu cài đặt lỗi: " + ex.Message); } catch { }
                SetSaveState("Lỗi lưu cài đặt: " + ex.Message, Theme.Red);
            }
        }

        private void RestoreDefaults()
        {
            try
            {
                LoadFromSettings(AppSettings.CreateDefault());
                Ctx.Log.Warn("Đã khôi phục cài đặt mặc định trên form — bấm \"Lưu cài đặt\" để áp dụng.");
                SetSaveState("Đã khôi phục mặc định (chưa lưu)", Theme.Yellow);
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Khôi phục mặc định lỗi: " + ex.Message); } catch { }
            }
        }

        private void TestConnectionAsync()
        {
            if (_btnTestDb == null) return;
            _btnTestDb.Enabled = false;
            SetDbState("Đang kiểm tra kết nối CSDL…", Theme.TextDim);
            var edited = (_txtDbConnection.Text ?? "").Trim();
            bool useEdited = !string.IsNullOrEmpty(edited) && (Ctx.Settings == null || !string.Equals(edited, Ctx.Settings.DbConnection, StringComparison.Ordinal));
            Task.Run(() =>
            {
                bool ok;
                string msg;
                try
                {
                    if (useEdited)
                    {
                        ok = TestConnectionString(edited, out msg);
                        if (ok) Ctx.Log.Okay("CSDL (chuỗi trong form) OK: " + msg);
                        else Ctx.Log.Error("CSDL (chuỗi trong form) lỗi: " + msg);
                    }
                    else
                    {
                        ok = Ctx.TestDb(out msg);
                    }
                }
                catch (Exception ex)
                {
                    ok = false;
                    msg = "Lỗi kiểm tra CSDL: " + ex.Message;
                    try { Ctx.Log.Error(msg); } catch { }
                }
                SafeUi(() =>
                {
                    SetDbState((ok ? "✓ " : "✗ ") + msg, ok ? Theme.Green : Theme.Red);
                    if (_btnTestDb != null) _btnTestDb.Enabled = true;
                });
            });
        }

        private static bool TestConnectionString(string connectionString, out string message)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT @@VERSION";
                        var v = Convert.ToString(cmd.ExecuteScalar());
                        var first = (v ?? "").Split('\n').FirstOrDefault();
                        message = "Kết nối OK: " + (first ?? "").Trim();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                message = "Kết nối thất bại: " + ex.Message;
                return false;
            }
        }

        private void SetDbState(string text, Color color)
        {
            if (_lblDbState == null || _lblDbState.IsDisposed) return;
            _lblDbState.Text = text ?? "";
            _lblDbState.ForeColor = color;
            if (_tips != null) _tips.SetToolTip(_lblDbState, text ?? "");
        }

        private void SetSaveState(string text, Color color)
        {
            if (_lblSaveState == null || _lblSaveState.IsDisposed) return;
            _lblSaveState.Text = text ?? "";
            _lblSaveState.ForeColor = color;
        }

        private void BrowsePath(TextBox box, bool isFolder)
        {
            try
            {
                if (box == null) return;
                var start = Clean(box.Text);
                if (isFolder)
                {
                    using (var dlg = new FolderBrowserDialog())
                    {
                        dlg.Description = "Chọn thư mục";
                        dlg.ShowNewFolderButton = true;
                        if (Directory.Exists(start)) dlg.SelectedPath = start;
                        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
                            box.Text = dlg.SelectedPath;
                    }
                }
                else
                {
                    using (var dlg = new OpenFileDialog())
                    {
                        dlg.Title = "Chọn tệp";
                        dlg.Filter = "Tất cả tệp (*.*)|*.*";
                        if (File.Exists(start)) dlg.FileName = start;
                        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName))
                            box.Text = dlg.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Warn("Không chọn được đường dẫn: " + ex.Message); } catch { }
            }
        }

        private void OpenFolder(string path, string label)
        {
            try
            {
                var dir = Clean(path);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    SetSaveState("Thư mục " + label + " không tồn tại: " + (string.IsNullOrEmpty(dir) ? "(trống)" : dir), Theme.Yellow);
                    Ctx.Log.Warn("Không mở được thư mục " + label + " (không tồn tại): " + dir);
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                Ctx.Log.Info("Mở thư mục " + label + ": " + dir);
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Không mở được thư mục " + label + ": " + ex.Message); } catch { }
                SetSaveState("Không mở được thư mục " + label + ": " + ex.Message, Theme.Red);
            }
        }

        // ============================================================ log console

        private Control BuildLogCard()
        {
            Panel body;
            var card = MakeCard("Nhật ký", "Thao tác cài đặt gần đây", out body);
            card.Margin = new Padding(0, 8, 0, 0);
            _logBox = new RichTextBox { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_logBox);
            body.Controls.Add(_logBox);
            return card;
        }

        private void AddLocalLog(string level, string message)
        {
            var entry = new LogEntry { Level = level, Message = message };
            _log.Add(entry);
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
            RenderLog();
        }

        private void OnLogEntry(LogEntry entry)
        {
            if (entry == null) return;
            if (IsHandleCreated && !Visible)
            {
                _log.Add(entry);
                while (_log.Count > MaxLogLines) _log.RemoveAt(0);
                _logDirty = true;
                return;
            }
            SafeUi(() =>
            {
                _log.Add(entry);
                while (_log.Count > MaxLogLines) _log.RemoveAt(0);
                RenderLog();
            });
        }

        /// <summary>Nap log cuoi tu history khi trang duoc mo lan dau.</summary>
        private void InitLogFromHistory()
        {
            try
            {
                var history = Ctx.Log.History;
                if (history == null) return;
                LogEntry[] snapshot;
                try { snapshot = history.ToArray(); } catch { return; }
                _log.Clear();
                for (int i = Math.Max(0, snapshot.Length - 40); i < snapshot.Length; i++) _log.Add(snapshot[i]);
                RenderLog();
            }
            catch { }
        }

        private void RenderLog()
        {
            if (_logBox == null || _logBox.IsDisposed) return;
            _logBox.SuspendLayout();
            _logBox.Clear();
            foreach (var e in _log)
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

        private void SafeUi(Action action)
        {
            try { UiInvoke(action); } catch { }
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
    }
}
