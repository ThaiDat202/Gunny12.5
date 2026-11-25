using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Trang Bomb: quet cac id bomb/crater duoc tham chieu boi BallList.xml + bombconfig.xml,
    /// kiem tra mask .bomb trong Service, rip asset client, tao mask thieu va deploy, kiem tra resource site.
    /// </summary>
    public class BombPage : PageBase
    {
        private const int RowBanner = 0;
        private const int RowToolbar = 1;
        private const int RowStats = 2;
        private const int RowProgress = 3;
        private const int RowCenter = 4;
        private const int RowLog = 5;

        // ---- trang thai
        private readonly List<BombIdInfo> _infos = new List<BombIdInfo>();
        private List<int> _referencedIds = new List<int>();
        private readonly List<int> _unresolved = new List<int>();
        private int _generatedMasks;
        private int _deployed;
        private int _assetsDownloaded;
        private int _assetsUnavailable;
        private string _clientSiteText = "";
        private int _scanCount;
        private bool _busy;
        private bool _preloaded;
        private CancellationTokenSource _cts;
        private SynchronizationContext _ui;
        private Action<LogEntry> _logHandler;
        private ToolTip _tip;
        private TableLayoutPanel _root;

        // ---- dieu khien
        private FlatButton _btnScan, _btnRip, _btnMask, _btnSite, _btnSave, _btnCancel;
        private CheckBox _chkOverwrite, _chkOnlyMissing;
        private NiceProgress _progress;
        private Label _lblStatus, _lblRefInfo, _lblUnresolved, _lblLastReport, _lblAssetPath;
        private DataGridView _grid;
        private TextBox _txtSite;
        private ListBox _lstUnresolved;
        private RichTextBox _log;
        private StatCard _statTotal, _statMask, _statMissing, _statAssets, _statNoSource;

        // ---- API cho MainForm
        public override string PageTitle { get { return "Bomb / Vũ khí"; } }
        public override string PageIcon { get { return "\U0001F4A3"; } }   // 💣
        public override string PageSubtitle { get { return "Mask .bomb · crater · bullet"; } }

        public BombPage(StudioContext context) : base(context)
        {
            _ui = SynchronizationContext.Current;
            BuildUi();
            WireEvents();
            InitState();
        }

        // ============================================================ LAYOUT

        private void BuildUi()
        {
            SuspendLayout();
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));   // banner canh bao
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));  // toolbar
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));   // stat cards
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));   // progress
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // grid + site panel
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));  // log

            _root.Controls.Add(BuildBanner(), 0, RowBanner);
            _root.Controls.Add(BuildToolbarCard(), 0, RowToolbar);
            _root.Controls.Add(BuildStatsRow(), 0, RowStats);
            _root.Controls.Add(BuildProgressRow(), 0, RowProgress);
            _root.Controls.Add(BuildCenter(), 0, RowCenter);
            _root.Controls.Add(BuildLogCard(), 0, RowLog);

            Controls.Add(_root);
            ResumeLayout();
        }

        private Control BuildBanner()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 2),
                Radius = 10,
                FillColor = Theme.AmberSoft,
                BorderColor = Color.FromArgb(231, 207, 166)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(12, 4, 12, 4)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var icon = new Label
            {
                Text = "⚠",
                Font = new Font("Segoe UI Symbol", 18f, FontStyle.Bold),
                ForeColor = Theme.Yellow,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 12, 0)
            };
            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            var main = new Label
            {
                Text = "Vũ khí mới cần bomb/crater asset trong trận — thiếu sẽ không dùng được trong battle",
                Font = Theme.FontBold(10f),
                ForeColor = Color.FromArgb(122, 85, 36),
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 0)
            };
            var sub = new Label
            {
                Text = "Quét id bomb tham chiếu từ BallList.xml + bombconfig.xml → kiểm tra mask .bomb trong Service → rip asset client → tạo mask thiếu + deploy.",
                Font = Theme.FontBase(8.6f),
                ForeColor = Theme.Yellow,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 0)
            };
            stack.Controls.Add(main, 0, 0);
            stack.Controls.Add(sub, 0, 1);
            t.Controls.Add(icon, 0, 0);
            t.Controls.Add(stack, 1, 0);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildToolbarCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Radius = 12,
                Padding = new Padding(12, 6, 12, 6)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ---------- title + thong tin id tham chieu
            var head = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var title = MakeLabel("\U0001F4A3  Vũ khí trong trận / Bomb", Theme.FontH2(11.5f), Theme.Text);
            title.Margin = new Padding(2, 3, 0, 0);
            _lblRefInfo = MakeLabel("chưa nạp BallList.xml / bombconfig.xml", Theme.FontBase(8.7f), Theme.TextDim);
            _lblRefInfo.Anchor = AnchorStyles.Right;
            _lblRefInfo.Margin = new Padding(0, 6, 2, 0);
            head.Controls.Add(title, 0, 0);
            head.Controls.Add(_lblRefInfo, 1, 0);

            // ---------- hang nut
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _btnScan = MakeButton("⟳  Quét Bomb", Theme.AccentDeep, Theme.Accent, 138);
            _btnRip = MakeButton("↓  Rip asset client → WebSite\\image\\bomb", Color.FromArgb(43, 62, 96), Theme.Accent, 300);
            _btnMask = MakeButton("＋  Tạo mask thiếu + deploy vào Service", Color.FromArgb(37, 86, 72), Theme.Green, 286);
            _btnSite = MakeButton("◎  Kiểm tra resource site", Color.FromArgb(64, 48, 96), Theme.Purple, 212);
            _btnSave = MakeButton("▸  Lưu report", Color.FromArgb(45, 58, 84), Color.FromArgb(68, 86, 118), 138);
            _btnScan.Margin = new Padding(0, 4, 8, 4);
            _btnRip.Margin = new Padding(0, 4, 8, 4);
            _btnMask.Margin = new Padding(0, 4, 8, 4);
            _btnSite.Margin = new Padding(0, 4, 8, 4);
            _btnSave.Margin = new Padding(0, 4, 0, 4);
            buttons.Controls.Add(_btnScan);
            buttons.Controls.Add(_btnRip);
            buttons.Controls.Add(_btnMask);
            buttons.Controls.Add(_btnSite);
            buttons.Controls.Add(_btnSave);

            // ---------- hang tuy chon
            var opts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            opts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            opts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            opts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            opts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _chkOverwrite = MakeCheck("Ghi đè asset / mask .bomb đã có", false);
            _chkOverwrite.Margin = new Padding(2, 4, 18, 0);
            _chkOverwrite.Anchor = AnchorStyles.Left;
            _chkOnlyMissing = MakeCheck("Chỉ id thiếu mask", true);
            _chkOnlyMissing.Margin = new Padding(0, 4, 18, 0);
            _chkOnlyMissing.Anchor = AnchorStyles.Left;

            var pathRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pathRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var lAsset = MakeLabel("Asset dir:", Theme.FontBase(8.6f), Theme.TextFaint);
            lAsset.Anchor = AnchorStyles.Left;
            lAsset.Margin = new Padding(0, 4, 8, 0);
            _lblAssetPath = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0), AutoSize = false };
            if (Ctx != null && Ctx.Settings != null) _lblAssetPath.Text = Ctx.Settings.BombAssetDir;
            pathRow.Controls.Add(lAsset, 0, 0);
            pathRow.Controls.Add(_lblAssetPath, 1, 0);

            opts.Controls.Add(_chkOverwrite, 0, 0);
            opts.Controls.Add(_chkOnlyMissing, 1, 0);
            opts.Controls.Add(pathRow, 2, 0);

            t.Controls.Add(head, 0, 0);
            t.Controls.Add(buttons, 0, 1);
            t.Controls.Add(opts, 0, 2);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildStatsRow()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 5; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _statTotal = MakeStat("Tổng id", "∑", Theme.Text, "bomb được tham chiếu");
            _statMask = MakeStat("Có mask", "✔", Theme.Green, "mask .bomb trong Service");
            _statMissing = MakeStat("Thiếu mask", "✖", Theme.Yellow, "cần tạo + deploy");
            _statAssets = MakeStat("Asset đã mirror", "↓", Theme.Accent, "có trong WebSite\\image\\bomb");
            _statNoSource = MakeStat("Không có nguồn", "!", Theme.Red, "không tìm thấy asset gốc");

            t.Controls.Add(_statTotal, 0, 0);
            t.Controls.Add(_statMask, 1, 0);
            t.Controls.Add(_statMissing, 2, 0);
            t.Controls.Add(_statAssets, 3, 0);
            t.Controls.Add(_statNoSource, 4, 0);
            return t;
        }

        private StatCard MakeStat(string caption, string icon, Color valueColor, string hint)
        {
            return new StatCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 10, 4),
                Caption = caption,
                Icon = icon,
                ValueColor = valueColor,
                Hint = hint,
                Value = "0"
            };
        }

        private Control BuildProgressRow()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _progress = new NiceProgress { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 12, 5) };
            _lblStatus = new Label
            {
                Text = "",
                Font = Theme.FontBase(9f),
                ForeColor = Theme.TextDim,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            _btnCancel = new FlatButton
            {
                Text = "✖  Hủy",
                Width = 82,
                Height = 26,
                Font = Theme.FontBold(9f),
                AccentColor = Color.FromArgb(110, 32, 42),
                HoverColor = Theme.Red,
                Radius = 7,
                Visible = false,
                Margin = new Padding(8, 4, 0, 0)
            };

            t.Controls.Add(_progress, 0, 0);
            t.Controls.Add(_lblStatus, 1, 0);
            t.Controls.Add(_btnCancel, 2, 0);
            return t;
        }

        private Control BuildCenter()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.Controls.Add(BuildGridCard(), 0, 0);
            t.Controls.Add(BuildSiteCard(), 1, 0);
            return t;
        }

        private Control BuildGridCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 10, 2),
                Radius = 12,
                Padding = new Padding(10, 6, 10, 8)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var head = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var title = MakeLabel("Bomb id tham chiếu + trạng thái mask", Theme.FontH2(10.2f), Theme.Text);
            title.Margin = new Padding(2, 4, 0, 0);
            _lblUnresolved = MakeLabel("chưa quét", Theme.FontBase(8.7f), Theme.TextDim);
            _lblUnresolved.Anchor = AnchorStyles.Right;
            _lblUnresolved.Margin = new Padding(0, 6, 2, 0);
            head.Controls.Add(title, 0, 0);
            head.Controls.Add(_lblUnresolved, 1, 0);

            _grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_grid);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            AddCol(_grid, "ID", 7, 52);
            AddCol(_grid, "Mask server", 10, 82);
            AddCol(_grid, "crater", 8, 60);
            AddCol(_grid, "brink", 8, 60);
            AddCol(_grid, "bullet", 8, 60);
            AddCol(_grid, "blastOut", 9, 66);
            AddCol(_grid, "Local", 7, 56);
            AddCol(_grid, "Trạng thái", 17, 120);
            AddCol(_grid, "Ghi chú", 26, 160);

            t.Controls.Add(head, 0, 0);
            t.Controls.Add(_grid, 0, 1);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildSiteCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Radius = 12,
                Padding = new Padding(10, 6, 10, 8)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0)
            };
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var title = MakeLabel("Kiểm tra resource site của client", Theme.FontH2(10.2f), Theme.Text);
            title.Margin = new Padding(2, 3, 0, 0);

            _txtSite = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                Dock = DockStyle.Fill,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.TextDim,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontMono(8.2f),
                Margin = new Padding(0)
            };

            var lUn = MakeLabel("ID chưa xử lý (thiếu mask):", Theme.FontBase(8.6f), Theme.TextFaint);
            lUn.Margin = new Padding(2, 4, 0, 0);
            _lstUnresolved = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.Yellow,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontMono(8.4f),
                IntegralHeight = false,
                Margin = new Padding(0)
            };

            var lRep = MakeLabel("Report:", Theme.FontBase(8.6f), Theme.TextFaint);
            lRep.Margin = new Padding(2, 4, 0, 0);
            _lblLastReport = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoSize = false };
            _lblLastReport.Text = "(chưa lưu — bấm \"Lưu report\")";
            _lblLastReport.ForeColor = Theme.TextFaint;

            t.Controls.Add(title, 0, 0);
            t.Controls.Add(_txtSite, 0, 1);
            t.Controls.Add(lUn, 0, 2);
            t.Controls.Add(_lstUnresolved, 0, 3);
            t.Controls.Add(lRep, 0, 4);
            t.Controls.Add(_lblLastReport, 0, 5);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildLogCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 0),
                Radius = 12,
                Padding = new Padding(10, 3, 10, 6)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var title = MakeLabel("Log thao tác", Theme.FontH2(9.8f), Theme.Text);
            title.Margin = new Padding(2, 2, 0, 0);
            _log = new RichTextBox { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_log);

            t.Controls.Add(title, 0, 0);
            t.Controls.Add(_log, 0, 1);
            card.Controls.Add(t);
            return card;
        }

        private static void AddCol(DataGridView grid, string header, int fillWeight, int minWidth)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = fillWeight,
                MinimumWidth = minWidth,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = true,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static CheckBox MakeCheck(string text, bool value)
        {
            return new CheckBox
            {
                Text = text,
                Checked = value,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Font = Theme.FontBase(9.1f),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                TabStop = true
            };
        }

        private static FlatButton MakeButton(string text, Color accent, Color hover, int width)
        {
            return new FlatButton
            {
                Text = text,
                Width = width,
                Height = 34,
                AccentColor = accent,
                HoverColor = hover,
                Font = Theme.FontBold(9.2f),
                Radius = 8
            };
        }

        // ============================================================ WIRING

        private void WireEvents()
        {
            _btnScan.Click += (s, e) => ScanAsync();
            _btnRip.Click += (s, e) => RipAsync();
            _btnMask.Click += (s, e) => MaskAsync();
            _btnSite.Click += (s, e) => ClientSiteAsync();
            _btnSave.Click += (s, e) => SaveReportClick();
            _btnCancel.Click += OnCancelClick;

            _tip = new ToolTip();
            _tip.SetToolTip(_btnScan, "Đọc BallList.xml + bombconfig.xml, quét toàn bộ id bomb tham chiếu và kiểm tra mask/asset.");
            _tip.SetToolTip(_btnRip, "Tải asset bomb/crater/bullet từ client về WebSite\\image\\bomb (chỉ trong đường dẫn cấu hình).");
            _tip.SetToolTip(_btnMask, "Tạo mask .bomb cho các id còn thiếu từ asset đã rip rồi deploy vào Service Bomb.");
            _tip.SetToolTip(_btnSite, "Kiểm tra resource site của client có phục vụ asset bomb hay không.");
            _tip.SetToolTip(_btnSave, "Ghi bomb-report.json vào thư mục reports.");
            _tip.SetToolTip(_chkOverwrite, "Ghi đè cả khi file asset/mask đã tồn tại.");
            _tip.SetToolTip(_chkOnlyMissing, "Khi tạo mask: chỉ xử lý các id chưa có mask .bomb trong Service.");

            _logHandler = OnLogEntry;
            Ctx.Log.OnLog += _logHandler;
        }

        private void InitState()
        {
            _lblStatus.Text = "Sẵn sàng. Bấm \"Quét Bomb\" để kiểm tra mask/asset.";
            _progress.Value = 0;
            _progress.Caption = "chưa quét";
            try
            {
                var hist = Ctx.Log.History;
                int from = Math.Max(0, hist.Count - 14);
                for (int i = from; i < hist.Count; i++) AppendLog(hist[i]);
            }
            catch { }
            UpdateStats();
            UpdateUnresolved();
        }

        public override void OnShownPage()
        {
            if (_preloaded || _busy) return;
            _preloaded = true;
            PreloadAsync();
        }

        public override void OnSettingsChanged()
        {
            UpdateAssetPath();
            _lblStatus.Text = "Cấu hình đã thay đổi — bấm \"Quét Bomb\" để nạp lại.";
            Ctx.Log.Step("Bomb: cấu hình thay đổi, cần quét lại");
        }

        // ============================================================ NAP ID THAM CHIEU (nhe)

        private async void PreloadAsync()
        {
            if (_busy) return;
            BeginBusy("Đang nạp BallList.xml + bombconfig.xml...");
            var token = _cts.Token;
            try
            {
                var ids = await Task.Run(() =>
                {
                    var svc = new BombService(Ctx.Log);
                    var ball = XmlCodec.Inspect(Path.Combine(MirrorDir(), "BallList.xml"));
                    var cfg = XmlCodec.Inspect(Path.Combine(MirrorDir(), "bombconfig.xml"));
                    return svc.CollectReferencedIds(ball, cfg) ?? new List<int>();
                }, token);
                _referencedIds = ids;
                Ctx.Log.Info("Bomb: nạp id tham chiếu = " + ids.Count);
                Post(() =>
                {
                    _lblRefInfo.Text = "BallList + bombconfig: " + ids.Count + " id tham chiếu";
                    _lblStatus.Text = "Sẵn sàng. Bấm \"Quét Bomb\" để kiểm tra asset/mask.";
                    _progress.Value = 0;
                    _progress.Caption = "chưa quét";
                });
            }
            catch (OperationCanceledException)
            {
                Post(() => _lblStatus.Text = "Đã hủy nạp id tham chiếu.");
            }
            catch (Exception ex)
            {
                Ctx.Log.Warn("Bomb: không nạp được id tham chiếu: " + ex.Message);
                Post(() =>
                {
                    _lblStatus.Text = "Không đọc được BallList.xml / bombconfig.xml: " + ex.Message;
                    _lblRefInfo.Text = "không nạp được id tham chiếu";
                });
            }
            finally
            {
                EndBusy();
            }
        }

        // ============================================================ QUET BOM

        private async void ScanAsync()
        {
            if (_busy) return;
            BeginBusy("Đang quét Bomb...");
            var token = _cts.Token;
            try
            {
                var svc = new BombService(Ctx.Log);
                Post(() => { _grid.Rows.Clear(); _progress.Value = 0; });
                var infos = await LoadScanAsync(svc, token);
                _infos.Clear();
                _infos.AddRange(infos);
                _assetsUnavailable = _infos.Count(IsNoSource);
                Post(() =>
                {
                    RebuildGrid();
                    UpdateStats();
                    UpdateUnresolved();
                    _progress.Value = 100;
                    _progress.Caption = _infos.Count + " id";
                    _lblStatus.Text = string.Format("Quét xong: {0} id · {1} có mask · {2} thiếu mask · {3} không có nguồn.",
                        _infos.Count, _infos.Count(i => i.ServerMask), _infos.Count(i => !i.ServerMask), _assetsUnavailable);
                });
                Ctx.Log.Okay(string.Format("Bomb scan xong: {0} id, {1} có mask, {2} thiếu mask, {3} id tham chiếu.",
                    _infos.Count, _infos.Count(i => i.ServerMask), _infos.Count(i => !i.ServerMask), _referencedIds.Count));
                try
                {
                    var path = await Task.Run(() => SaveReport());
                    Post(() => SetLastReport(path));
                }
                catch (Exception ex) { Ctx.Log.Warn("Bomb: không ghi được report: " + ex.Message); }
            }
            catch (OperationCanceledException)
            {
                Post(() => { _lblStatus.Text = "Đã hủy quét Bomb."; _progress.Caption = "đã hủy"; });
                Ctx.Log.Warn("Hủy quét Bomb");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Quét Bomb lỗi: " + ex.Message);
                Post(() => { _lblStatus.Text = "Lỗi quét Bomb: " + ex.Message; _progress.Caption = "lỗi"; });
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<List<BombIdInfo>> LoadScanAsync(BombService svc, CancellationToken token)
        {
            var settings = Ctx.Settings;
            var mirror = MirrorDir();
            var ballPath = Path.Combine(mirror ?? "", "BallList.xml");
            var cfgPath = Path.Combine(mirror ?? "", "bombconfig.xml");
            _scanCount = 0;

            Post(() =>
            {
                _lblStatus.Text = "Đang đọc BallList.xml + bombconfig.xml...";
                _progress.Caption = "đang đọc id tham chiếu";
                _progress.Value = 0;
            });
            var docs = await Task.Run(() => Ctx.GetRequestDocs(null, false, n => Post(() => _lblStatus.Text = "Đang đọc request XML: " + n)), token);

            var ids = await Task.Run(() =>
            {
                var ball = XmlCodec.Inspect(ballPath);
                var cfg = XmlCodec.Inspect(cfgPath);
                var list = svc.CollectReferencedIds(ball, cfg);
                return list ?? new List<int>();
            }, token);
            _referencedIds = ids;
            Post(() =>
            {
                _lblRefInfo.Text = "BallList + bombconfig: " + ids.Count + " id tham chiếu";
                _lblStatus.Text = "Đang quét " + ids.Count + " id bomb...";
                _progress.Caption = "đã quét 0 id";
            });

            var infos = await Task.Run(() => svc.Scan(settings, docs, info =>
            {
                int n = Interlocked.Increment(ref _scanCount);
                if (n == 1 || n % 10 == 0)
                {
                    int nn = n;
                    int total = ids.Count;
                    int pct = total > 0 ? Math.Min(99, (int)Math.Round(nn * 100.0 / total)) : 0;
                    Post(() =>
                    {
                        _progress.Value = pct;
                        _progress.Caption = "đã quét " + nn + "/" + total + " id";
                        if (nn % 50 == 0) _lblStatus.Text = "Đang quét Bomb id " + nn + "/" + total + "...";
                    });
                }
            }, token), token);
            return infos ?? new List<BombIdInfo>();
        }

        // ============================================================ RIP ASSET

        private async void RipAsync()
        {
            if (_busy) return;
            if (_infos.Count == 0)
            {
                MessageBox.Show(this, "Hãy bấm \"Quét Bomb\" trước để có danh sách id cần rip asset.",
                    "Rip asset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginBusy("Đang rip asset client → WebSite\\image\\bomb...");
            var token = _cts.Token;
            try
            {
                var svc = new BombService(Ctx.Log);
                var settings = Ctx.Settings;
                var infos = _infos.ToList();
                bool overwrite = _chkOverwrite.Checked;
                var lastRipUi = DateTime.MinValue;
                int n = await Task.Run(() => svc.RipAssets(settings, infos, overwrite,
                    s =>
                    {
                        var now = DateTime.UtcNow;
                        if ((now - lastRipUi).TotalMilliseconds < 200) return;   // throttle UI
                        lastRipUi = now;
                        Post(() => { _progress.Caption = "rip asset..."; _lblStatus.Text = "Rip: " + s; });
                    }, token), token);
                var rip = svc.LastRipStats;
                if (n > 0) _assetsDownloaded += n;
                Ctx.Log.Okay(string.Format("Bomb rip asset: tải {0}, bỏ qua {1}, không có nguồn {2}, lỗi {3}",
                    rip.Downloaded, rip.Skipped, rip.Unavailable, rip.Failed));
                Post(() =>
                {
                    RebuildGrid();
                    UpdateStats();
                    UpdateUnresolved();
                    _progress.Value = 100;
                    _progress.Caption = "rip xong · " + n + " file";
                    _lblStatus.Text = string.Format("Rip xong {0} asset (bỏ qua {1}, không nguồn {2}, lỗi {3}) · bảng đã cập nhật.",
                        rip.Downloaded, rip.Skipped, rip.Unavailable, rip.Failed);
                });
                try { var path = await Task.Run(() => SaveReport()); Post(() => SetLastReport(path)); } catch { }
            }
            catch (OperationCanceledException)
            {
                Post(() => { _lblStatus.Text = "Đã hủy rip asset."; _progress.Caption = "đã hủy"; });
                Ctx.Log.Warn("Hủy rip asset Bomb");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Rip asset Bomb lỗi: " + ex.Message);
                Post(() => _lblStatus.Text = "Lỗi rip asset: " + ex.Message);
            }
            finally
            {
                EndBusy();
            }
        }

        // ============================================================ TAO MASK + DEPLOY

        private async void MaskAsync()
        {
            if (_busy) return;
            if (_infos.Count == 0)
            {
                MessageBox.Show(this, "Hãy bấm \"Quét Bomb\" trước để có danh sách id bomb.",
                    "Tạo mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var targets = _chkOnlyMissing.Checked ? _infos.Where(i => !i.ServerMask).ToList() : _infos.ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show(this, "Không có id nào cần tạo mask (tất cả đã có mask trong Service).",
                    "Tạo mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            BeginBusy("Đang tạo mask thiếu + deploy " + targets.Count + " id...");
            var token = _cts.Token;
            try
            {
                var svc = new BombService(Ctx.Log);
                var settings = Ctx.Settings;
                bool overwrite = _chkOverwrite.Checked;
                int written = await Task.Run(() => svc.GenerateAndDeployMasks(settings, targets, overwrite,
                    s => Post(() => { _progress.Caption = "tạo mask..."; _lblStatus.Text = "Mask: " + s; }), token), token);
                var mask = svc.LastMaskStats;
                _generatedMasks += Math.Max(0, mask.GeneratedIds);
                _deployed += Math.Max(0, written);
                Ctx.Log.Okay(string.Format("Bomb tạo mask: {0} id mới, ghi {1} file, bỏ qua {2}, không nguồn {3}, lỗi {4}",
                    mask.GeneratedIds, written, mask.SkippedIds, mask.NoSource, mask.Failed));
                Post(() =>
                {
                    RebuildGrid();
                    UpdateStats();
                    UpdateUnresolved();
                    _progress.Value = 100;
                    _progress.Caption = "mask xong · ghi " + written + " file";
                    _lblStatus.Text = string.Format("Tạo mask xong: {0} id mới, ghi {1} file · còn {2} id thiếu mask.",
                        mask.GeneratedIds, written, _infos.Count(i => !i.ServerMask));
                });
                try { var path = await Task.Run(() => SaveReport()); Post(() => SetLastReport(path)); } catch { }
            }
            catch (OperationCanceledException)
            {
                Post(() => { _lblStatus.Text = "Đã hủy tạo mask."; _progress.Caption = "đã hủy"; });
                Ctx.Log.Warn("Hủy tạo mask Bomb");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Tạo mask Bomb lỗi: " + ex.Message);
                Post(() => _lblStatus.Text = "Lỗi tạo mask: " + ex.Message);
            }
            finally
            {
                EndBusy();
            }
        }

        // ============================================================ KIEM TRA RESOURCE SITE

        private async void ClientSiteAsync()
        {
            if (_busy) return;
            BeginBusy("Đang kiểm tra resource site của client...");
            var token = _cts.Token;
            try
            {
                var svc = new BombService(Ctx.Log);
                var settings = Ctx.Settings;
                string text = await Task.Run(() => svc.ClientSiteCheck(settings), token);
                _clientSiteText = text ?? "";
                Post(() =>
                {
                    _txtSite.Text = _clientSiteText;
                    _progress.Value = 100;
                    _progress.Caption = "kiểm tra xong";
                    _lblStatus.Text = "Đã kiểm tra resource site của client (xem khung bên phải).";
                });
                Ctx.Log.Okay("Bomb ClientSiteCheck xong");
            }
            catch (OperationCanceledException)
            {
                Post(() => _lblStatus.Text = "Đã hủy kiểm tra resource site.");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Kiểm tra resource site lỗi: " + ex.Message);
                Post(() =>
                {
                    _lblStatus.Text = "Lỗi kiểm tra resource site: " + ex.Message;
                    _txtSite.Text = "LỖI: " + ex.Message + Environment.NewLine + "Cấu hình: " + (Ctx.Settings == null ? "" : Ctx.Settings.ServiceRoot);
                });
            }
            finally
            {
                EndBusy();
            }
        }

        // ============================================================ HIEN THI

        private void RebuildGrid()
        {
            if (_grid == null) return;
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            foreach (var info in _infos.OrderBy(i => i.Id))
            {
                int i = _grid.Rows.Add(
                    info.Id,
                    BoolText(info.ServerMask),
                    BoolText(info.CraterPng),
                    BoolText(info.CraterBrink),
                    BoolText(info.Bullet),
                    BoolText(info.BlastOut),
                    LocalText(info),
                    StatusText(info),
                    NoteText(info));
                var row = _grid.Rows[i];
                row.Tag = info;
                row.Cells[1].Style.ForeColor = info.ServerMask ? Theme.Green : Theme.Red;
                row.Cells[2].Style.ForeColor = info.CraterPng ? Theme.Green : Theme.Red;
                row.Cells[3].Style.ForeColor = info.CraterBrink ? Theme.Green : Theme.Yellow;
                row.Cells[4].Style.ForeColor = info.Bullet ? Theme.Green : Theme.TextFaint;
                row.Cells[5].Style.ForeColor = info.BlastOut ? Theme.Green : Theme.TextFaint;
                int local = LocalCount(info);
                row.Cells[6].Style.ForeColor = local >= 4 ? Theme.Green : (local == 0 ? Theme.Red : Theme.Yellow);
                row.Cells[7].Style.ForeColor = StatusColor(info);
                if (!string.IsNullOrEmpty(info.Note)) row.Cells[8].ToolTipText = info.Note;
            }
            _grid.ResumeLayout();
            _lblUnresolved.Text = _infos.Count == 0 ? "chưa quét" : (_infos.Count + " id · " + _infos.Count(i => !i.ServerMask) + " thiếu mask");
        }

        private void UpdateStats()
        {
            int total = _infos.Count;
            int withMask = _infos.Count(i => i.ServerMask);
            int assets = _infos.Count(i => LocalCount(i) > 0);
            int noSource = _infos.Count(IsNoSource);
            _statTotal.Value = total.ToString();
            _statTotal.ValueColor = Theme.Text;
            _statMask.Value = withMask.ToString();
            _statMask.ValueColor = withMask > 0 ? Theme.Green : Theme.Text;
            _statMissing.Value = (total - withMask).ToString();
            _statMissing.ValueColor = total - withMask > 0 ? Theme.Yellow : Theme.Green;
            _statAssets.Value = assets.ToString();
            _statAssets.ValueColor = assets > 0 ? Theme.Accent : Theme.Text;
            _statNoSource.Value = noSource.ToString();
            _statNoSource.ValueColor = noSource > 0 ? Theme.Red : Theme.Text;
            _statTotal.Invalidate();
            _statMask.Invalidate();
            _statMissing.Invalidate();
            _statAssets.Invalidate();
            _statNoSource.Invalidate();
        }

        private void UpdateUnresolved()
        {
            _unresolved.Clear();
            if (_lstUnresolved != null)
            {
                _lstUnresolved.BeginUpdate();
                _lstUnresolved.Items.Clear();
            }
            var ids = _infos.Where(i => !i.ServerMask).Select(i => i.Id).OrderBy(x => x).ToList();
            _unresolved.AddRange(ids);
            if (_lstUnresolved != null)
            {
                foreach (var id in ids.Take(500))
                {
                    var info = _infos.FirstOrDefault(x => x.Id == id);
                    var why = info != null && IsNoSource(info) ? "không có nguồn" : "thiếu mask";
                    _lstUnresolved.Items.Add(id + "  —  " + why);
                }
                _lstUnresolved.EndUpdate();
            }
        }

        private static int LocalCount(BombIdInfo i)
        {
            if (i == null) return 0;
            int n = 0;
            if (i.LocalCrater) n++;
            if (i.LocalBrink) n++;
            if (i.LocalBullet) n++;
            if (i.LocalBlastOut) n++;
            return n;
        }

        private static string LocalText(BombIdInfo i)
        {
            return LocalCount(i) + "/4";
        }

        private static string BoolText(bool value)
        {
            return value ? "✔" : "✘";
        }

        private static string StatusText(BombIdInfo i)
        {
            if (i == null) return "";
            var res = (i.Resolution ?? "").Trim().ToLowerInvariant();
            if (res == "ok") return i.ServerMask ? "OK" : "có mask (folder khác)";
            if (res.StartsWith("generated")) return "đã tạo mask";
            if (res == "missing-mask") return "thiếu mask (có nguồn)";
            if (res.StartsWith("no-source") || res.StartsWith("no source")) return "không có nguồn";
            if (i.ServerMask) return "có mask";
            if (i.CraterPng || i.LocalCrater) return "thiếu mask (có nguồn)";
            if (res.Length > 0) return i.Resolution;
            return "thiếu mask";
        }

        private static Color StatusColor(BombIdInfo i)
        {
            var text = StatusText(i);
            if (text == "OK" || text == "có mask") return Theme.Green;
            if (text.StartsWith("thiếu mask (có nguồn)")) return Theme.Yellow;
            if (text == "không có nguồn") return Theme.Red;
            if (text == "đã tạo mask" || text == "đã deploy" || text == "có mask (folder khác)") return Theme.Accent;
            return Theme.TextDim;
        }

        private static string NoteText(BombIdInfo i)
        {
            if (i == null) return "";
            if (!string.IsNullOrEmpty(i.Note)) return i.Note;
            if (i.ServerMask) return "";
            var miss = new List<string>();
            if (!i.CraterPng && !i.LocalCrater) miss.Add("crater.png");
            if (!i.CraterBrink && !i.LocalBrink) miss.Add("craterBrink.png");
            if (!i.Bullet && !i.LocalBullet) miss.Add("bullet.swf");
            if (!i.BlastOut && !i.LocalBlastOut) miss.Add("blastOut.swf");
            if (miss.Count == 0) return "";
            return "thiếu " + string.Join(", ", miss.Take(4).ToArray()) + (miss.Count > 4 ? "…" : "");
        }

        private static bool IsNoSource(BombIdInfo i)
        {
            if (i == null) return false;
            var res = (i.Resolution ?? "").Trim().ToLowerInvariant();
            if (res.StartsWith("no-source") || res.StartsWith("no source")) return true;
            if (!string.IsNullOrEmpty(i.Note))
            {
                var note = i.Note.ToLowerInvariant();
                if (note.Contains("no-source") || note.Contains("no source")
                    || note.Contains("không có nguồn") || note.Contains("khong co nguon")
                    || note.Contains("khong co crater") || note.Contains("không có crater"))
                    return true;
            }
            return false;
        }

        // ============================================================ REPORT

        private string SaveReport()
        {
            var report = BuildReport();
            var path = Path.Combine(Ctx.ReportsDir, "bomb-report.json");
            Json.SaveFile(path, report);
            Ctx.Log.Okay("Bomb report: " + path);
            return path;
        }

        private BombReport BuildReport()
        {
            var sorted = _infos.OrderBy(i => i.Id).ToList();
            var report = new BombReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                ReferencedIds = _referencedIds.Count > 0 ? _referencedIds.Count : sorted.Count,
                WithMask = sorted.Count(i => i.ServerMask),
                MissingMask = sorted.Count(i => !i.ServerMask),
                GeneratedMasks = _generatedMasks,
                Deployed = _deployed,
                AssetsDownloaded = _assetsDownloaded,
                AssetsUnavailable = sorted.Count(IsNoSource),
                Ids = sorted,
                UnresolvedIds = sorted.Where(i => !i.ServerMask && IsNoSource(i)).Select(i => i.Id).ToList(),
                Note = "client-site-check: " + (string.IsNullOrEmpty(_clientSiteText) ? "(chưa chạy)" : Shorten(_clientSiteText, 500))
            };
            return report;
        }

        private void SetLastReport(string path)
        {
            if (_lblLastReport == null) return;
            _lblLastReport.Text = path;
            _lblLastReport.ForeColor = Theme.Accent;
        }

        private void SaveReportClick()
        {
            try
            {
                var path = SaveReport();
                SetLastReport(path);
                _lblStatus.Text = "Đã lưu report: " + path;
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Không lưu được bomb report: " + ex.Message);
                MessageBox.Show(this, "Không lưu được report:\n" + ex.Message, "Bomb report",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var oneLine = text.Replace("\r", " ").Replace("\n", " ");
            if (oneLine.Length <= max) return oneLine;
            return oneLine.Substring(0, max) + "…";
        }

        // ============================================================ BUSY / LOG / DISPOSE

        private void BeginBusy(string caption)
        {
            _busy = true;
            _cts = new CancellationTokenSource();
            SetControlsEnabled(false);
            _progress.Value = 0;
            _progress.Caption = caption ?? "Đang xử lý...";
            _lblStatus.Text = caption;
        }

        private void EndBusy()
        {
            _busy = false;
            var cts = _cts;
            _cts = null;
            if (cts != null) { try { cts.Dispose(); } catch { } }
            SetControlsEnabled(true);
            Cursor = Cursors.Default;
        }

        private void SetControlsEnabled(bool on)
        {
            _btnScan.Enabled = on;
            _btnRip.Enabled = on;
            _btnMask.Enabled = on;
            _btnSite.Enabled = on;
            _btnSave.Enabled = on;
            _chkOverwrite.Enabled = on;
            _chkOnlyMissing.Enabled = on;
            if (_btnCancel != null)
            {
                _btnCancel.Visible = !on;
                _btnCancel.Enabled = !on;
            }
            Cursor = on ? Cursors.Default : Cursors.WaitCursor;
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (_cts == null) return;
            _lblStatus.Text = "Đang hủy...";
            _btnCancel.Enabled = false;
            try { _cts.Cancel(); } catch { }
        }

        private void OnLogEntry(LogEntry e)
        {
            if (e == null) return;
            if (IsHandleCreated && !Visible) return;   // trang dang an: khong day UI, tranh giat lag
            Post(() => AppendLog(e));
        }

        private void AppendLog(LogEntry e)
        {
            if (_log == null || _log.IsDisposed || e == null) return;
            try
            {
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = LevelColor(e.Level);
                _log.AppendText(e.ToString() + Environment.NewLine);
                _log.SelectionColor = Theme.Text;
                _log.ScrollToCaret();
            }
            catch { }
        }

        private static Color LevelColor(string level)
        {
            switch (level)
            {
                case "OK": return Theme.Green;
                case "STEP": return Theme.Accent;
                case "WARN": return Theme.Yellow;
                case "ERR": return Theme.Red;
                default: return Theme.TextDim;
            }
        }

        private void Post(Action action)
        {
            if (action == null) return;
            var ui = _ui;
            if (ui == null)
            {
                try
                {
                    if (IsHandleCreated) BeginInvoke(action);
                }
                catch { }
                return;
            }
            try
            {
                ui.Post(state =>
                {
                    if (IsDisposed) return;
                    try { action(); } catch { }
                }, null);
            }
            catch { }
        }

        private string MirrorDir()
        {
            var s = Ctx == null ? null : Ctx.Settings;
            if (s == null) return "";
            return string.IsNullOrEmpty(s.MirrorDir) ? (s.RequestDir ?? "") : s.MirrorDir;
        }

        private void UpdateAssetPath()
        {
            if (_lblAssetPath != null && Ctx != null && Ctx.Settings != null)
                _lblAssetPath.Text = Ctx.Settings.BombAssetDir;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_root == null || _root.RowStyles.Count < 6) return;
            bool compact = Height < 700;
            _root.RowStyles[RowStats].Height = compact ? 84f : 96f;
            _root.RowStyles[RowLog].Height = compact ? 70f : 112f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_logHandler != null) Ctx.Log.OnLog -= _logHandler; } catch { }
                try { if (_cts != null) _cts.Cancel(); } catch { }
                try { if (_tip != null) _tip.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
