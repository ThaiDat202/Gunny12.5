using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio.Pages
{
    /// <summary>
    /// Trang Import: quet cac template request XML trong thu muc mirror, map sang bang CSDL
    /// Db_Tank_All va import hang loat (co backup / force / dry-run / progress / bao cao).
    /// </summary>
    public class ImportPage : PageBase
    {
        // ---- chi so hang cua layout goc
        private const int RowHeader = 0;
        private const int RowFilter = 1;
        private const int RowStats = 2;
        private const int RowProgress = 3;
        private const int RowCenter = 4;
        private const int RowResult = 5;
        private const int RowLog = 6;

        // ---- du lieu 1 dong template
        private sealed class ImportRow
        {
            public XmlDocInfo Doc;
            public XmlTableMapping Map;
            public string Sha;
            public string ManifestState = "mới";   // mới | đã import | đã đổi | lỗi

            public bool Importable
            {
                get { return DbImporter.IsImportable(Map) && Doc != null && Doc.Rows != null && Doc.Rows.Count > 0; }
            }
        }

        private sealed class ScanData
        {
            public List<ImportRow> Rows = new List<ImportRow>();
            public int TableCount = -1;
            public string Problem;
        }

        // ---- trang thai
        private readonly List<ImportRow> _all = new List<ImportRow>();
        private Dictionary<string, ImportHash> _manifest = new Dictionary<string, ImportHash>(StringComparer.OrdinalIgnoreCase);
        private int _tableCount = -1;
        private string _schemaProblem;
        private bool _busy;
        private bool _autoScanStarted;
        private CancellationTokenSource _cts;
        private SynchronizationContext _ui;
        private int _cntImported, _cntUnchanged, _cntSkipped, _cntFailed;
        private Action<LogEntry> _logHandler;
        private ToolTip _tip;
        private TableLayoutPanel _root;

        // ---- dieu khien
        private TextBox _txtSearch;
        private ComboBox _cboStatus;
        private CheckBox _chkOnlyImportable;
        private CheckBox _chkBackup, _chkForce, _chkDryRun, _chkPreserve;
        private NumericUpDown _numBatch;
        private FlatButton _btnScan, _btnImportAll, _btnImportOne, _btnDryAll, _btnReportDir, _btnCancel;
        private NiceProgress _progress;
        private Label _lblStatus, _lblDbState, _lblGridCount, _lblSelected, _lblResultCount;
        private DataGridView _grid, _result;
        private RichTextBox _log;
        private StatCard _statImported, _statUnchanged, _statSkipped, _statFailed;
        private Label _lblImportableValue, _lblTableValue, _lblManifestPath, _lblMirrorPath;

        // ---- API cho MainForm
        public override string PageTitle { get { return "Import CSDL"; } }
        public override string PageIcon { get { return "\U0001F5C4"; } }   // 🗄
        public override string PageSubtitle { get { return "Template XML → bảng CSDL"; } }

        public ImportPage(StudioContext context) : base(context)
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
                RowCount = 7,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));   // header
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 114f));  // filter + options
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));   // stat cards
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));   // progress
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // center: danh sach + tong quan
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160f));  // ket qua import
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f));   // log

            _root.Controls.Add(BuildHeader(), 0, RowHeader);
            _root.Controls.Add(BuildFilterCard(), 0, RowFilter);
            _root.Controls.Add(BuildStatsRow(), 0, RowStats);
            _root.Controls.Add(BuildProgressRow(), 0, RowProgress);
            _root.Controls.Add(BuildCenter(), 0, RowCenter);
            _root.Controls.Add(BuildResultCard(), 0, RowResult);
            _root.Controls.Add(BuildLogCard(), 0, RowLog);

            Controls.Add(_root);
            ResumeLayout();
        }

        private Control BuildHeader()
        {
            var p = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 2)
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            var icon = new Label
            {
                Text = "\U0001F4E5",
                Font = new Font("Segoe UI Emoji", 12.5f),
                ForeColor = Theme.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(2, 2, 6, 0)
            };
            var title = new Label
            {
                Text = "Import dữ liệu template",
                Font = Theme.FontH2(12f),
                ForeColor = Theme.Text,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 12, 0)
            };
            var sub = new Label
            {
                Text = "Quét request XML trong mirror → map bảng CSDL → import hàng loạt",
                Font = Theme.FontBase(8.8f),
                ForeColor = Theme.TextDim,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 9, 0, 0)
            };
            left.Controls.Add(icon);
            left.Controls.Add(title);
            left.Controls.Add(sub);

            _lblDbState = new Label
            {
                Text = "● CSDL: chưa nạp",
                Font = Theme.FontBold(8.8f),
                ForeColor = Theme.TextFaint,
                AutoSize = true,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 7, 2, 0)
            };
            p.Controls.Add(left, 0, 0);
            p.Controls.Add(_lblDbState, 1, 0);
            return p;
        }

        private Control BuildFilterCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Radius = 12,
                Padding = new Padding(12, 6, 12, 6)
            };
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ---------- hang 1: tim kiem + loc + nut quet
            var r1 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 8,
                RowCount = 1,
                Margin = new Padding(0)
            };
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142f));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r1.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lSearch = MakeLabel("\U0001F50E  Tìm:", Theme.FontBase(9.3f), Theme.TextDim);
            lSearch.Margin = new Padding(0, 14, 6, 0);
            _txtSearch = new TextBox { Margin = new Padding(0, 10, 14, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right };
            Style.Apply(_txtSearch);
            _txtSearch.HandleCreated += (s, e) => SetCue(_txtSearch, "Tên file XML hoặc tên bảng CSDL...");
            if (_txtSearch.IsHandleCreated) SetCue(_txtSearch, "Tên file XML hoặc tên bảng CSDL...");

            var lMap = MakeLabel("Map:", Theme.FontBase(9.3f), Theme.TextDim);
            lMap.Margin = new Padding(0, 14, 6, 0);
            _cboStatus = new ComboBox { Margin = new Padding(0, 9, 14, 0), Anchor = AnchorStyles.Left | AnchorStyles.Right, DropDownStyle = ComboBoxStyle.DropDownList };
            Style.Apply(_cboStatus);
            _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboStatus.Items.AddRange(new object[] { "Tất cả", "exact", "auto", "override", "ambiguous", "none" });
            _cboStatus.SelectedIndex = 0;

            _chkOnlyImportable = MakeCheck("Chỉ hiện template import được", false);
            _chkOnlyImportable.Margin = new Padding(0, 12, 10, 0);
            _chkOnlyImportable.Anchor = AnchorStyles.Left;

            _btnScan = MakeButton("⚡  Quét && phân tích", Theme.AccentDeep, Theme.Accent, 156);
            _btnScan.Margin = new Padding(6, 6, 0, 6);
            _btnScan.Anchor = AnchorStyles.Right;

            _btnReportDir = MakeButton("\U0001F4C2  Mở thư mục report", Color.FromArgb(45, 58, 84), Color.FromArgb(68, 86, 118), 162);
            _btnReportDir.Margin = new Padding(8, 6, 0, 6);
            _btnReportDir.Anchor = AnchorStyles.Right;

            r1.Controls.Add(lSearch, 0, 0);
            r1.Controls.Add(_txtSearch, 1, 0);
            r1.Controls.Add(lMap, 2, 0);
            r1.Controls.Add(_cboStatus, 3, 0);
            r1.Controls.Add(_chkOnlyImportable, 4, 0);
            r1.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0) }, 5, 0);
            r1.Controls.Add(_btnScan, 6, 0);
            r1.Controls.Add(_btnReportDir, 7, 0);

            // ---------- hang 2: tuy chon + nut import
            var r2 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 11,
                RowCount = 1,
                Margin = new Padding(0)
            };
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            r2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            r2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _chkBackup = MakeCheck("Backup bảng trước khi ghi đè", Ctx != null && Ctx.Settings != null && Ctx.Settings.BackupBeforeReplace);
            _chkBackup.Margin = new Padding(0, 4, 14, 0);
            _chkBackup.Anchor = AnchorStyles.Left;
            _chkForce = MakeCheck("Force import lại", false);
            _chkForce.Margin = new Padding(0, 4, 14, 0);
            _chkForce.Anchor = AnchorStyles.Left;
            _chkDryRun = MakeCheck("Dry-run", false);
            _chkDryRun.Margin = new Padding(0, 4, 14, 0);
            _chkDryRun.Anchor = AnchorStyles.Left;
            _chkPreserve = MakeCheck("Giữ cột server-local", true);
            _chkPreserve.Margin = new Padding(0, 4, 14, 0);
            _chkPreserve.Anchor = AnchorStyles.Left;

            var lBatch = MakeLabel("Batch size:", Theme.FontBase(9f), Theme.TextDim);
            lBatch.Margin = new Padding(0, 5, 6, 0);
            lBatch.Anchor = AnchorStyles.Left;
            _numBatch = new NumericUpDown
            {
                Minimum = 500,
                Maximum = 10000,
                Increment = 500,
                Value = 2000,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBase(9.3f),
                Margin = new Padding(0, 3, 10, 0),
                TextAlign = HorizontalAlignment.Center,
                Width = 80
            };
            _numBatch.Anchor = AnchorStyles.Left;

            _btnImportAll = MakeButton("⬆  Import tất cả", Theme.AccentDeep, Theme.Accent, 142);
            _btnImportAll.Margin = new Padding(6, 2, 0, 2);
            _btnImportAll.Height = 30;
            _btnImportAll.Anchor = AnchorStyles.Left;
            _btnImportOne = MakeButton("\U0001F4C4  Import dòng chọn", Color.FromArgb(37, 86, 72), Theme.Green, 158);
            _btnImportOne.Margin = new Padding(6, 2, 0, 2);
            _btnImportOne.Height = 30;
            _btnImportOne.Anchor = AnchorStyles.Left;
            _btnDryAll = MakeButton("\U0001F9EA  Dry-run tất cả", Color.FromArgb(84, 66, 20), Theme.Yellow, 146);
            _btnDryAll.Margin = new Padding(6, 2, 0, 2);
            _btnDryAll.Height = 30;
            _btnDryAll.Anchor = AnchorStyles.Left;

            r2.Controls.Add(_chkBackup, 0, 0);
            r2.Controls.Add(_chkForce, 1, 0);
            r2.Controls.Add(_chkDryRun, 2, 0);
            r2.Controls.Add(_chkPreserve, 3, 0);
            r2.Controls.Add(lBatch, 4, 0);
            r2.Controls.Add(_numBatch, 5, 0);
            r2.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = new Padding(0) }, 6, 0);
            r2.Controls.Add(_btnImportAll, 7, 0);
            r2.Controls.Add(_btnImportOne, 8, 0);
            r2.Controls.Add(_btnDryAll, 9, 0);
            r2.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Width = 2, Margin = new Padding(0) }, 10, 0);

            table.Controls.Add(r1, 0, 0);
            table.Controls.Add(r2, 0, 1);
            card.Controls.Add(table);
            return card;
        }

        private Control BuildStatsRow()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 4; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _statImported = MakeStat("Đã import", "✔", Theme.Green, "template ghi vào CSDL");
            _statUnchanged = MakeStat("Không đổi", "≡", Theme.Accent, "hash trùng lần trước");
            _statSkipped = MakeStat("Bỏ qua", "→", Theme.Yellow, "dry-run / chưa map được");
            _statFailed = MakeStat("Lỗi", "✖", Theme.Red, "xem cột Ghi chú");

            t.Controls.Add(_statImported, 0, 0);
            t.Controls.Add(_statUnchanged, 1, 0);
            t.Controls.Add(_statSkipped, 2, 0);
            t.Controls.Add(_statFailed, 3, 0);
            return t;
        }

        private StatCard MakeStat(string caption, string icon, Color valueColor, string hint)
        {
            var c = new StatCard
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 10, 4),
                Caption = caption,
                Icon = icon,
                ValueColor = valueColor,
                Hint = hint,
                Value = "0"
            };
            return c;
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
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330f));
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
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.Controls.Add(BuildGridCard(), 0, 0);
            t.Controls.Add(BuildSideCard(), 1, 0);
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
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var title = MakeLabel("Template XML trong mirror", Theme.FontH2(10.2f), Theme.Text);
            title.Margin = new Padding(2, 4, 0, 0);
            _lblGridCount = MakeLabel("0 template", Theme.FontBase(8.8f), Theme.TextDim);
            _lblGridCount.Anchor = AnchorStyles.Right;
            _lblGridCount.Margin = new Padding(0, 6, 2, 0);
            _lblSelected = MakeLabel("", Theme.FontBase(8.8f), Theme.Accent);
            _lblSelected.Anchor = AnchorStyles.Right;
            _lblSelected.Margin = new Padding(0, 6, 14, 0);
            head.Controls.Add(title, 0, 0);
            head.Controls.Add(_lblSelected, 1, 0);
            head.Controls.Add(_lblGridCount, 2, 0);

            _grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_grid);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            AddCol(_grid, "File XML", 30, 190);
            AddCol(_grid, "Số dòng", 8, 62);
            AddCol(_grid, "Bảng CSDL", 20, 120);
            AddCol(_grid, "Map", 9, 66);
            AddCol(_grid, "Cần chú ý", 23, 150);
            AddCol(_grid, "Trạng thái", 12, 92);

            t.Controls.Add(head, 0, 0);
            t.Controls.Add(_grid, 0, 1);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildSideCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Radius = 12,
                Padding = new Padding(10, 6, 6, 6)
            };
            // panel co scroll doc de noi dung khong bi cat khi cua so thap
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Card,
                Margin = new Padding(0)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 7,
                Margin = new Padding(0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));

            var title = MakeLabel("Tổng quan", Theme.FontH2(10.2f), Theme.Text);
            title.Margin = new Padding(2, 4, 0, 0);

            _lblImportableValue = new Label
            {
                Text = "0",
                Font = Theme.FontTitle(19f),
                ForeColor = Theme.Green,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(2, 0, 0, 0)
            };
            var kpi1 = MakeKpi("Template import được (có bảng + đủ cột)", _lblImportableValue, Theme.Green);

            _lblTableValue = new Label
            {
                Text = "—",
                Font = Theme.FontTitle(19f),
                ForeColor = Theme.Accent,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(2, 0, 0, 0)
            };
            var kpi2 = MakeKpi("Bảng CSDL trong schema", _lblTableValue, Theme.Accent);

            var lManifest = MakeLabel("Manifest import lần trước:", Theme.FontBase(8.6f), Theme.TextFaint);
            lManifest.Margin = new Padding(2, 2, 0, 0);
            _lblManifestPath = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoSize = false };
            _lblManifestPath.Text = Ctx != null ? SafeManifestPath() : "";

            var lMirror = MakeLabel("Thư mục mirror:", Theme.FontBase(8.6f), Theme.TextFaint);
            lMirror.Margin = new Padding(2, 2, 0, 0);
            _lblMirrorPath = new PathLabel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoSize = false };
            if (Ctx != null && Ctx.Settings != null) _lblMirrorPath.Text = Ctx.Settings.MirrorDir;

            t.Controls.Add(title, 0, 0);
            t.Controls.Add(kpi1, 0, 1);
            t.Controls.Add(kpi2, 0, 2);
            t.Controls.Add(lManifest, 0, 3);
            t.Controls.Add(_lblManifestPath, 0, 4);
            t.Controls.Add(lMirror, 0, 5);
            t.Controls.Add(_lblMirrorPath, 0, 6);

            scroll.Controls.Add(t);
            card.Controls.Add(scroll);
            return card;
        }

        private Control MakeKpi(string caption, Label value, Color color)
        {
            var p = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var cap = new Label
            {
                Text = caption,
                Font = Theme.FontBase(8.4f),
                ForeColor = Theme.TextDim,
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(2, 0, 0, 0)
            };
            value.Dock = DockStyle.Fill;
            value.AutoSize = false;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.ForeColor = color;
            p.Controls.Add(cap, 0, 0);
            p.Controls.Add(value, 0, 1);
            return p;
        }

        private Control BuildResultCard()
        {
            var card = new ThemedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                Radius = 12,
                Padding = new Padding(10, 4, 10, 8)
            };
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
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
            var title = MakeLabel("Kết quả import", Theme.FontH2(10.2f), Theme.Text);
            title.Margin = new Padding(2, 3, 0, 0);
            _lblResultCount = MakeLabel("chưa chạy lần nào", Theme.FontBase(8.8f), Theme.TextDim);
            _lblResultCount.AutoSize = false;
            _lblResultCount.Width = 420;
            _lblResultCount.Height = 20;
            _lblResultCount.TextAlign = ContentAlignment.MiddleRight;
            _lblResultCount.AutoEllipsis = true;
            _lblResultCount.Anchor = AnchorStyles.Right;
            _lblResultCount.Margin = new Padding(0, 3, 2, 0);
            head.Controls.Add(title, 0, 0);
            head.Controls.Add(_lblResultCount, 1, 0);

            _result = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_result);
            _result.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            AddCol(_result, "File", 18, 120);
            AddCol(_result, "Bảng", 15, 100);
            AddCol(_result, "Trạng thái", 9, 76);
            AddCol(_result, "Dòng đọc", 7, 64);
            AddCol(_result, "Dòng ghi", 7, 64);
            AddCol(_result, "Backup", 15, 100);
            AddCol(_result, "Ghi chú", 24, 140);
            AddCol(_result, "ms", 5, 46);

            t.Controls.Add(head, 0, 0);
            t.Controls.Add(_result, 0, 1);
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

            var title = MakeLabel("Log", Theme.FontH2(9.8f), Theme.Text);
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
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = fillWeight,
                MinimumWidth = minWidth,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            };
            grid.Columns.Add(col);
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

        private FlatButton MakeButton(string text, Color accent, Color hover, int width)
        {
            return new FlatButton
            {
                Text = text,
                Width = width,
                Height = 32,
                AccentColor = accent,
                HoverColor = hover,
                Font = Theme.FontBold(9.2f),
                Radius = 8
            };
        }

        // ============================================================ WIRING

        private void WireEvents()
        {
            _txtSearch.TextChanged += (s, e) => ApplyFilter();
            _cboStatus.SelectedIndexChanged += (s, e) => ApplyFilter();
            _chkOnlyImportable.CheckedChanged += (s, e) => ApplyFilter();

            _btnScan.Click += (s, e) => { ScanAsync(); };
            _btnImportAll.Click += (s, e) => StartImport(true, false);
            _btnImportOne.Click += (s, e) => StartImport(false, false);
            _btnDryAll.Click += (s, e) => StartImport(true, true);
            _btnReportDir.Click += OnOpenReportDir;
            _btnCancel.Click += OnCancelClick;

            _grid.SelectionChanged += OnGridSelectionChanged;
            _grid.CellDoubleClick += OnGridDoubleClick;

            _tip = new ToolTip();
            _tip.SetToolTip(_btnScan, "Đọc toàn bộ XML trong thư mục mirror, map sang bảng CSDL và đối chiếu manifest.");
            _tip.SetToolTip(_btnImportAll, "Import tất cả template map được (bỏ qua template chưa map / rỗng).");
            _tip.SetToolTip(_btnImportOne, "Chỉ import dòng đang chọn trong bảng template.");
            _tip.SetToolTip(_btnDryAll, "Chạy thử toàn bộ: không ghi CSDL, chỉ kiểm tra và báo cáo.");
            _tip.SetToolTip(_btnReportDir, "Mở thư mục reports (chứa import-report.json và import-manifest.json).");
            _tip.SetToolTip(_chkForce, "Import lại cả khi hash dữ liệu không đổi.");
            _tip.SetToolTip(_chkPreserve, "Giữ các cột server tự thêm mà XML publisher không gửi (Game_Map.PosX, Ball.HasTunnel/Delay, NPC_Info.X/Y...). Nên bật để Fight.exe không lỗi sau import.");
            _tip.SetToolTip(_numBatch, "Số dòng mỗi lô SqlBulkCopy (500 - 10000).");

            _logHandler = OnLogEntry;
            Ctx.Log.OnLog += _logHandler;
        }

        private void InitState()
        {
            _lblStatus.Text = "Sẵn sàng. Bấm \"Quét & phân tích\" để nạp danh sách template.";
            _progress.Value = 0;
            _progress.Caption = "chưa chạy";
            try
            {
                var hist = Ctx.Log.History;
                int from = Math.Max(0, hist.Count - 16);
                for (int i = from; i < hist.Count; i++) AppendLog(hist[i]);
            }
            catch { }
        }

        public override void OnShownPage()
        {
            if (_autoScanStarted || _busy) return;
            _autoScanStarted = true;
            ScanAsync();
        }

        public override void OnSettingsChanged()
        {
            _all.Clear();
            if (_grid != null) _grid.Rows.Clear();
            _lblStatus.Text = "Cấu hình đã thay đổi — bấm \"Quét & phân tích\" để nạp lại.";
            if (_chkBackup != null && Ctx.Settings != null) _chkBackup.Checked = Ctx.Settings.BackupBeforeReplace;
            if (_lblMirrorPath != null && Ctx.Settings != null) _lblMirrorPath.Text = Ctx.Settings.MirrorDir;
            UpdateSummary();
        }

        // ============================================================ QUET / PHAN TICH

        private async void ScanAsync()
        {
            if (_busy) return;
            BeginBusy("Đang quét template...");
            var token = _cts.Token;
            try
            {
                Post(() =>
                {
                    _result.Rows.Clear();
                    _cntImported = _cntUnchanged = _cntSkipped = _cntFailed = 0;
                    UpdateStatsCards();
                    _lblResultCount.Text = "chưa chạy lần nào";
                });

                var manifest = await Task.Run(() => Json.LoadFile<ImportManifest>(Ctx.Importer.ManifestPath));
                ReplaceManifest(manifest);

                Ctx.Log.Step("Quét template XML trong mirror: " + (Ctx.Settings == null ? "" : Ctx.Settings.MirrorDir));
                Post(() => { _lblStatus.Text = "Đang đọc danh sách XML trong thư mục mirror..."; _progress.Caption = "đang đọc..."; });
                var docs = await Task.Run(() => Ctx.GetRequestDocs(null, false, name => Post(() => _lblStatus.Text = "Đang đọc " + name)), token);

                Post(() => { _lblStatus.Text = "Đang map " + docs.Count + " template vào bảng CSDL..."; });
                var data = await Task.Run(() => BuildScanData(docs, token), token);

                _all.Clear();
                _all.AddRange(data.Rows);
                _tableCount = data.TableCount;
                _schemaProblem = data.Problem;

                Post(() =>
                {
                    ApplyFilter();
                    UpdateSummary();
                    _progress.Value = 100;
                    _progress.Caption = _all.Count + " template";
                    if (_schemaProblem == null)
                        _lblStatus.Text = string.Format("Đã quét {0} template · {1} import được · {2} bảng CSDL.", _all.Count, _all.Count(r => r.Importable), _tableCount);
                    else
                        _lblStatus.Text = "Đã quét " + _all.Count + " template nhưng chưa nạp được schema CSDL: " + _schemaProblem;
                    if (_all.Count == 0)
                        _lblStatus.Text = "Không tìm thấy file XML nào trong thư mục mirror. Kiểm tra lại cấu hình.";
                });
                Ctx.Log.Okay(string.Format("Quét xong: {0} template, {1} import được, schema {2} bảng.", _all.Count, _all.Count(r => r.Importable), _tableCount));
                if (_schemaProblem != null) Ctx.Log.Warn("Schema CSDL: " + _schemaProblem);
            }
            catch (OperationCanceledException)
            {
                Post(() => { _lblStatus.Text = "Đã hủy quét."; _progress.Caption = "đã hủy"; });
                Ctx.Log.Warn("Hủy quét template");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Quét template lỗi: " + ex.Message);
                Post(() => { _lblStatus.Text = "Lỗi quét: " + ex.Message; });
            }
            finally
            {
                EndBusy();
            }
        }

        private ScanData BuildScanData(List<XmlDocInfo> docs, CancellationToken token)
        {
            var data = new ScanData();
            TableMapper mapper = null;
            try { mapper = Ctx.Mapper; }
            catch (Exception ex) { data.Problem = ex.Message; }

            if (docs == null) return data;
            for (int i = 0; i < docs.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var doc = docs[i];
                var row = new ImportRow { Doc = doc };
                if (mapper != null)
                {
                    try { row.Map = Ctx.MapDoc(doc); }
                    catch (Exception ex)
                    {
                        row.Map = new XmlTableMapping { XmlName = doc.Name, Status = "none", Problem = "lỗi map: " + ex.Message };
                    }
                }
                else
                {
                    row.Map = new XmlTableMapping { XmlName = doc.Name, Status = "none", Problem = "CSDL chưa sẵn sàng: " + data.Problem };
                }
                try { row.Sha = DbImporter.ComputeRowsHash(doc); } catch { row.Sha = null; }
                ApplyManifestState(row);
                data.Rows.Add(row);

                if ((i + 1) % 10 == 0 || i + 1 == docs.Count)
                {
                    int n = i + 1;
                    Post(() => { _lblStatus.Text = "Đang map " + n + "/" + docs.Count + " template..."; _progress.Caption = "map " + n + "/" + docs.Count; });
                }
            }
            if (mapper != null) data.TableCount = mapper.Schema.Tables.Count;
            return data;
        }

        private void ReplaceManifest(ImportManifest manifest)
        {
            var dict = new Dictionary<string, ImportHash>(StringComparer.OrdinalIgnoreCase);
            if (manifest != null && manifest.Entries != null)
            {
                foreach (var e in manifest.Entries)
                {
                    if (e != null && !string.IsNullOrEmpty(e.XmlName)) dict[e.XmlName] = e;
                }
            }
            _manifest = dict;
        }

        private void ApplyManifestState(ImportRow row)
        {
            row.ManifestState = "mới";
            if (row.Doc == null || row.Doc.Name == null || _manifest == null) return;
            ImportHash h;
            if (!_manifest.TryGetValue(row.Doc.Name, out h) || h == null) return;
            var table = row.Map == null ? null : row.Map.TableName;
            bool same = row.Sha != null
                        && string.Equals(h.Sha256, row.Sha, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(h.TableName, table, StringComparison.OrdinalIgnoreCase);
            row.ManifestState = same ? "đã import" : "đã đổi";
        }

        // ============================================================ IMPORT

        private void StartImport(bool all, bool forceDryRun)
        {
            if (_busy) return;
            List<ImportRow> targets;
            if (all)
            {
                targets = _all.Where(r => r.Importable).ToList();
            }
            else
            {
                var sel = SelectedRow();
                if (sel == null)
                {
                    MessageBox.Show(this, "Hãy chọn một dòng trong bảng template trước khi import.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!sel.Importable)
                {
                    MessageBox.Show(this, "Dòng \"" + sel.Doc.Name + "\" chưa map được bảng CSDL (hoặc file rỗng) nên không import được.",
                        "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                targets = new List<ImportRow> { sel };
            }

            if (targets.Count == 0)
            {
                MessageBox.Show(this, "Không có template nào import được. Hãy bấm \"Quét & phân tích\" trước (và kiểm tra kết nối CSDL).",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var opt = new ImportOptions
            {
                Backup = _chkBackup.Checked,
                Force = _chkForce.Checked,
                DryRun = forceDryRun || _chkDryRun.Checked,
                BatchSize = (int)_numBatch.Value,
                StopOnError = false,
                PreserveLocalColumns = _chkPreserve == null || _chkPreserve.Checked,
                FillMissingDefaults = true
            };
            try
            {
                var ov = Ctx.Overrides;
                if (ov != null && ov.SkipTables != null)
                    foreach (var tn in ov.SkipTables)
                        if (!opt.SkipTables.Any(x => string.Equals(x, tn, StringComparison.OrdinalIgnoreCase))) opt.SkipTables.Add(tn);
            }
            catch { }
            RunImportAsync(targets, opt, all);
        }

        private async void RunImportAsync(List<ImportRow> targets, ImportOptions opt, bool viaAll)
        {
            if (_busy) return;
            var modeText = opt.DryRun ? "DRY-RUN" : "IMPORT";
            BeginBusy(modeText + " · " + targets.Count + " template...");
            var token = _cts.Token;
            Post(() =>
            {
                _result.Rows.Clear();
                _cntImported = _cntUnchanged = _cntSkipped = _cntFailed = 0;
                UpdateStatsCards();
                _lblResultCount.Text = "0 kết quả";
            });
            var results = new List<ImportResult>();
            var sw = Stopwatch.StartNew();
            int total = targets.Count;
            int done = 0;
            try
            {
                await Task.Run(() =>
                {
                    TableMapper mapper;
                    try { mapper = Ctx.Mapper; }
                    catch (Exception ex) { throw new InvalidOperationException("Không nạp được schema CSDL: " + ex.Message, ex); }

                    foreach (var row in targets)
                    {
                        token.ThrowIfCancellationRequested();
                        ImportResult res;
                        try
                        {
                            if (viaAll)
                            {
                                var list = Ctx.Importer.ImportAll(new[] { row.Doc }, mapper, opt);
                                res = (list != null && list.Count > 0) ? list[0] : null;
                            }
                            else
                            {
                                var map = row.Map ?? Ctx.MapDoc(row.Doc);
                                res = Ctx.Importer.ImportOne(row.Doc, map, opt);
                            }
                        }
                        catch (Exception ex)
                        {
                            res = new ImportResult
                            {
                                XmlName = row.Doc.Name,
                                TableName = row.Map == null ? null : row.Map.TableName,
                                Status = "failed",
                                RowsRead = row.Doc.Rows.Count,
                                Message = ex.Message
                            };
                        }
                        if (res == null)
                        {
                            res = new ImportResult
                            {
                                XmlName = row.Doc.Name,
                                TableName = row.Map == null ? null : row.Map.TableName,
                                Status = "skipped",
                                Message = "không import được (map/bảng không phù hợp)"
                            };
                        }
                        results.Add(res);
                        done++;
                        int n = done;
                        int pct = (int)Math.Round(n * 100.0 / total);
                        var captured = res;
                        Post(() =>
                        {
                            _progress.Value = pct;
                            _progress.Caption = n + "/" + total + " template";
                            _lblStatus.Text = "Đang import " + n + "/" + total + " · " + captured.XmlName;
                            AddResultRow(captured);
                        });
                        try
                        {
                            Ctx.Log.Info((opt.DryRun ? "[dry] " : "") + "Import " + res.XmlName + ": " + res.Status
                                         + (string.IsNullOrEmpty(res.Message) ? "" : " - " + res.Message));
                        }
                        catch { }
                    }
                }, token);

                sw.Stop();
                var report = BuildReport(results, opt, sw.Elapsed);
                string reportPath = Path.Combine(Ctx.ReportsDir, "import-report.json");
                await Task.Run(() =>
                {
                    try { Json.SaveFile(reportPath, report); }
                    catch (Exception ex) { Ctx.Log.Warn("Không ghi được import-report.json: " + ex.Message); }
                });
                Ctx.Log.Okay(string.Format("{0} xong: {1} import · {2} không đổi · {3} bỏ qua · {4} lỗi ({5:0.0}s)",
                    modeText, report.Imported, report.Unchanged, report.Skipped, report.Failed, sw.Elapsed.TotalSeconds));

                foreach (var w in report.Warnings) Ctx.Log.Warn("[server-local] " + w);
                foreach (var w in report.ServerSafety) Ctx.Log.Error("[server-safety] " + w);

                Post(() =>
                {
                    _progress.Value = 100;
                    _progress.Caption = modeText + " xong · " + total + " template";
                    _lblStatus.Text = string.Format("{0} xong: {1} import · {2} không đổi · {3} bỏ qua · {4} lỗi · report: {5}",
                        modeText, report.Imported, report.Unchanged, report.Skipped, report.Failed, reportPath);
                    _lblResultCount.Text = results.Count + " kết quả · " + reportPath;
                });
                await RefreshManifestStatesAsync();
            }
            catch (OperationCanceledException)
            {
                Post(() =>
                {
                    _lblStatus.Text = "Đã hủy import (xử lý " + done + "/" + total + " template).";
                    _progress.Caption = "đã hủy";
                });
                Ctx.Log.Warn("Import bị hủy sau " + done + "/" + total + " template");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Import lỗi: " + ex.Message);
                Post(() =>
                {
                    _lblStatus.Text = "Lỗi import: " + ex.Message;
                    _progress.Caption = "lỗi";
                });
            }
            finally
            {
                EndBusy();
            }
        }

        private ImportReport BuildReport(List<ImportResult> results, ImportOptions opt, TimeSpan elapsed)
        {
            var rep = new ImportReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                Connection = Ctx.Settings == null ? "" : StudioContext.MaskConnection(Ctx.Settings.DbConnection),
                Entries = results.ToList()
            };
            foreach (var r in results)
            {
                if (r == null) continue;
                switch (r.Status)
                {
                    case "imported": rep.Imported++; break;
                    case "unchanged": rep.Unchanged++; break;
                    case "skipped": rep.Skipped++; break;
                    default: rep.Failed++; break;
                }
            }
            var warnList = new List<string>();
            foreach (var r in results)
            {
                if (r == null || string.IsNullOrEmpty(r.Warnings)) continue;
                foreach (var line in r.Warnings.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    warnList.Add(r.XmlName + " (" + r.TableName + "): " + line.Trim());
            }
            rep.Warnings = warnList;
            try { rep.ServerSafety = Ctx.Importer.CheckServerSafety(); }
            catch (Exception ex) { rep.ServerSafety = new List<string> { "khong kiem tra duoc: " + ex.Message }; }

            rep.Note = (opt.DryRun ? "DRY-RUN" : "Import thật")
                       + (opt.Force ? " · force" : "")
                       + " · backup=" + opt.Backup
                       + " · batch=" + opt.BatchSize
                       + " · " + elapsed.TotalSeconds.ToString("0.0") + "s";
            return rep;
        }

        private async Task RefreshManifestStatesAsync()
        {
            try
            {
                var manifest = await Task.Run(() => Json.LoadFile<ImportManifest>(Ctx.Importer.ManifestPath));
                ReplaceManifest(manifest);
                foreach (var r in _all) ApplyManifestState(r);
                Post(() => { ApplyFilter(); UpdateSummary(); });
            }
            catch (Exception ex)
            {
                Ctx.Log.Warn("Không đọc lại manifest: " + ex.Message);
            }
        }

        // ============================================================ HIEN THI

        private void ApplyFilter()
        {
            if (_grid == null) return;
            string q = (_txtSearch.Text ?? "").Trim();
            string status = _cboStatus.SelectedItem == null ? "Tất cả" : Convert.ToString(_cboStatus.SelectedItem);
            bool onlyImportable = _chkOnlyImportable.Checked;

            var filtered = new List<ImportRow>();
            foreach (var r in _all)
            {
                if (onlyImportable && !r.Importable) continue;
                if (!MatchStatus(r, status)) continue;
                if (q.Length > 0 && !MatchText(r, q)) continue;
                filtered.Add(r);
            }

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            foreach (var r in filtered)
            {
                string attention = AttentionText(r);
                int i = _grid.Rows.Add(
                    r.Doc.Name + ".xml",
                    r.Doc.Rows.Count,
                    string.IsNullOrEmpty(MapTable(r)) ? "—" : MapTable(r),
                    MapStatus(r),
                    attention,
                    r.ManifestState);
                var gr = _grid.Rows[i];
                gr.Tag = r;
                gr.Cells[3].Style.ForeColor = MapColor(MapStatus(r));
                gr.Cells[5].Style.ForeColor = ManifestColor(r.ManifestState);
                if (attention.Length > 0)
                    gr.Cells[4].Style.ForeColor = (r.Map != null && r.Map.MissingRequiredColumns.Count > 0) ? Theme.Red : Theme.Yellow;
                if (!r.Importable)
                {
                    gr.DefaultCellStyle.ForeColor = Theme.TextFaint;
                    gr.Cells[0].Style.ForeColor = Theme.TextDim;
                }
            }
            _grid.ResumeLayout();
            _lblGridCount.Text = filtered.Count + "/" + _all.Count + " template";
        }

        private static bool MatchStatus(ImportRow r, string filter)
        {
            var st = r.Map == null ? "none" : (r.Map.Status ?? "none");
            switch (filter)
            {
                case "exact": return st == "exact" || st == "exact-warn";
                case "auto": return st == "auto" || st == "auto-warn";
                case "override": return st == "override";
                case "ambiguous": return st == "ambiguous";
                case "none": return st == "none";
                default: return true;
            }
        }

        private static bool MatchText(ImportRow r, string q)
        {
            if (r.Doc == null) return false;
            var hay = (r.Doc.Name ?? "") + "|" + MapTable(r) + "|" + (r.Doc.FilePath ?? "") + "|" + (r.Doc.Error ?? "");
            return hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string MapTable(ImportRow r)
        {
            return r.Map == null ? null : r.Map.TableName;
        }

        private static string MapStatus(ImportRow r)
        {
            return r.Map == null || string.IsNullOrEmpty(r.Map.Status) ? "none" : r.Map.Status;
        }

        private static string AttentionText(ImportRow r)
        {
            var m = r.Map;
            if (m == null) return "";
            var parts = new List<string>();
            if (m.MissingRequiredColumns != null && m.MissingRequiredColumns.Count > 0)
            {
                var names = m.MissingRequiredColumns.Take(3).ToArray();
                parts.Add("Thiếu NOT NULL: " + string.Join(", ", names) + (m.MissingRequiredColumns.Count > 3 ? "…" : ""));
            }
            if (m.IgnoredAttributes != null && m.IgnoredAttributes.Count > 0)
                parts.Add("Bỏ qua " + m.IgnoredAttributes.Count + " attr");
            if (!string.IsNullOrEmpty(m.Problem) && (m.Status == "ambiguous" || m.Status == "none" || parts.Count == 0))
                parts.Add(m.Problem);
            if (r.Doc != null && r.Doc.Rows.Count == 0) parts.Add("file rỗng");
            return string.Join(" · ", parts.ToArray());
        }

        private static Color MapColor(string status)
        {
            switch (status)
            {
                case "exact": return Theme.Green;
                case "exact-warn": return Theme.Yellow;
                case "auto": return Theme.Accent;
                case "auto-warn": return Theme.Yellow;
                case "override": return Theme.Purple;
                case "ambiguous": return Theme.Orange;
                default: return Theme.Red;
            }
        }

        private static Color ManifestColor(string state)
        {
            switch (state)
            {
                case "đã import": return Theme.Green;
                case "đã đổi": return Theme.Yellow;
                default: return Theme.Accent;
            }
        }

        private void UpdateSummary()
        {
            if (_lblImportableValue != null)
                _lblImportableValue.Text = _all.Count(r => r.Importable).ToString();
            if (_lblTableValue != null)
                _lblTableValue.Text = _tableCount >= 0 ? _tableCount.ToString() : "—";
            if (_lblDbState != null)
            {
                if (_tableCount >= 0)
                {
                    _lblDbState.Text = "● CSDL: " + _tableCount + " bảng";
                    _lblDbState.ForeColor = Theme.Green;
                }
                else if (!string.IsNullOrEmpty(_schemaProblem))
                {
                    _lblDbState.Text = "● CSDL: lỗi — kiểm tra tab Cấu hình";
                    _lblDbState.ForeColor = Theme.Red;
                }
                else
                {
                    _lblDbState.Text = "● CSDL: chưa nạp";
                    _lblDbState.ForeColor = Theme.TextFaint;
                }
            }
        }

        private void AddResultRow(ImportResult r)
        {
            if (_result == null || r == null) return;
            int i = _result.Rows.Add(
                r.XmlName ?? "",
                string.IsNullOrEmpty(r.TableName) ? "—" : r.TableName,
                ResultStatusText(r.Status),
                r.RowsRead,
                r.RowsWritten,
                string.IsNullOrEmpty(r.BackupTable) ? "—" : r.BackupTable,
                r.Message ?? "",
                r.ElapsedMs);
            var gr = _result.Rows[i];
            gr.Cells[2].Style.ForeColor = ResultStatusColor(r.Status);
            if (!string.IsNullOrEmpty(r.BackupTable)) gr.Cells[5].Style.ForeColor = Theme.Accent;
            if (r.Status == "failed")
            {
                gr.Cells[6].Style.ForeColor = Theme.Red;
                gr.Cells[2].Style.ForeColor = Theme.Red;
            }
            if (_result.Rows.Count > 300) _result.Rows.RemoveAt(0);

            switch (r.Status)
            {
                case "imported": _cntImported++; break;
                case "unchanged": _cntUnchanged++; break;
                case "skipped": _cntSkipped++; break;
                default: _cntFailed++; break;
            }
            UpdateStatsCards();
            _lblResultCount.Text = (_cntImported + _cntUnchanged + _cntSkipped + _cntFailed) + " kết quả";
        }

        private static string ResultStatusText(string status)
        {
            switch (status)
            {
                case "imported": return "đã import";
                case "unchanged": return "không đổi";
                case "skipped": return "bỏ qua";
                case "failed": return "lỗi";
                default: return status ?? "";
            }
        }

        private static Color ResultStatusColor(string status)
        {
            switch (status)
            {
                case "imported": return Theme.Green;
                case "unchanged": return Theme.TextDim;
                case "skipped": return Theme.Yellow;
                default: return Theme.Red;
            }
        }

        private void UpdateStatsCards()
        {
            if (_statImported == null) return;
            _statImported.Value = _cntImported.ToString();
            _statImported.ValueColor = _cntImported > 0 ? Theme.Green : Theme.Text;
            _statUnchanged.Value = _cntUnchanged.ToString();
            _statUnchanged.ValueColor = _cntUnchanged > 0 ? Theme.Accent : Theme.Text;
            _statSkipped.Value = _cntSkipped.ToString();
            _statSkipped.ValueColor = _cntSkipped > 0 ? Theme.Yellow : Theme.Text;
            _statFailed.Value = _cntFailed.ToString();
            _statFailed.ValueColor = _cntFailed > 0 ? Theme.Red : Theme.Text;
            _statImported.Invalidate();
            _statUnchanged.Invalidate();
            _statSkipped.Invalidate();
            _statFailed.Invalidate();
        }

        // ============================================================ SU KIEN

        private void OnGridSelectionChanged(object sender, EventArgs e)
        {
            var r = SelectedRow();
            if (_lblSelected != null)
                _lblSelected.Text = r == null ? "" : "chọn: " + r.Doc.Name + (r.Importable ? "" : " (không import được)");
        }

        private void OnGridDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var r = SelectedRow();
            if (r == null || r.Doc == null || string.IsNullOrEmpty(r.Doc.FilePath)) return;
            try
            {
                if (File.Exists(r.Doc.FilePath))
                    Process.Start("explorer.exe", "/select,\"" + r.Doc.FilePath + "\"");
            }
            catch (Exception ex) { Ctx.Log.Warn("Không mở được file: " + ex.Message); }
        }

        private void OnOpenReportDir(object sender, EventArgs e)
        {
            try
            {
                var dir = Ctx.ReportsDir;
                Directory.CreateDirectory(dir);
                var report = Path.Combine(dir, "import-report.json");
                if (File.Exists(report)) Process.Start("explorer.exe", "/select,\"" + report + "\"");
                else Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Không mở được thư mục report: " + ex.Message);
            }
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            if (_cts == null) return;
            _lblStatus.Text = "Đang hủy...";
            _btnCancel.Enabled = false;
            try { _cts.Cancel(); } catch { }
        }

        private ImportRow SelectedRow()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as ImportRow;
        }

        // ============================================================ BUSY / LOG / DISPOSE

        private bool BeginBusy(string caption)
        {
            if (_busy) return false;
            _busy = true;
            _cts = new CancellationTokenSource();
            SetControlsEnabled(false);
            _progress.Value = 0;
            _progress.Caption = caption ?? "Đang xử lý...";
            if (_lblStatus != null) _lblStatus.Text = caption;
            return true;
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
            _btnImportAll.Enabled = on;
            _btnImportOne.Enabled = on;
            _btnDryAll.Enabled = on;
            _btnReportDir.Enabled = on;
            _chkBackup.Enabled = on;
            _chkForce.Enabled = on;
            _chkDryRun.Enabled = on;
            _numBatch.Enabled = on;
            _txtSearch.Enabled = on;
            _cboStatus.Enabled = on;
            _chkOnlyImportable.Enabled = on;
            if (_btnCancel != null)
            {
                _btnCancel.Visible = !on;
                _btnCancel.Enabled = !on;
            }
            Cursor = on ? Cursors.Default : Cursors.WaitCursor;
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

        private string SafeManifestPath()
        {
            try { return Ctx.Importer.ManifestPath; }
            catch { return ""; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_root == null || _root.RowStyles.Count < 7) return;
            bool compact = Height < 700;
            _root.RowStyles[RowStats].Height = compact ? 84f : 96f;
            _root.RowStyles[RowResult].Height = compact ? 122f : 160f;
            _root.RowStyles[RowLog].Height = compact ? 62f : 90f;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCue(TextBox box, string text)
        {
            try { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); } catch { }
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
