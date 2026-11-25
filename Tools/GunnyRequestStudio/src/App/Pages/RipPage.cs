using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio.Pages
{
    /// <summary>
    /// Trang rip request XML tu nha phat hanh vao thu muc mirror, promote template vao Request.
    /// Toan bo cong viec nang chay trong Task.Run, cap nhat UI qua BeginInvoke.
    /// </summary>
    public class RipPage : PageBase
    {
        public override string PageTitle { get { return "Rip Request"; } }
        public override string PageIcon { get { return "⬇"; } }
        public override string PageSubtitle { get { return "Mirror request XML & promote template"; } }

        private readonly List<string> _names = new List<string>();
        private readonly List<LogEntry> _log = new List<LogEntry>();
        private const int MaxLogLines = 250;

        private ListBox _lstBases;
        private Label _lblNameCount;
        private FlatButton _btnScan;
        private FlatButton _btnDryRun, _btnRip, _btnPromote, _btnCancel, _btnSaveReport, _btnOpenMirror;
        private CheckBox _chkOverwrite, _chkPromoteTemplates, _chkPromoteAll, _chkRefresh;
        private NumericUpDown _numConcurrency;
        private NiceProgress _progress;
        private Label _lblStatus;
        private StatCard _stOk, _stUnchanged, _stMissing, _stFailed, _stBytes;
        private DataGridView _grid;
        private RichTextBox _logBox;
        private ToolTip _tips;

        private int _running;
        private CancellationTokenSource _cts;
        private RipReport _lastReport;
        private bool _loadedOnce;
        private bool _logDirty;

        public RipPage(StudioContext ctx) : base(ctx)
        {
            BuildUi();
            try { Ctx.Log.OnLog += OnLogEntry; } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { Ctx.Log.OnLog -= OnLogEntry; } catch { }
                try { if (_cts != null) _cts.Cancel(); } catch { }
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
                RefreshSources();
                AutoScanNames();
            }
            if (_logDirty) { _logDirty = false; RenderLog(); }
        }

        public override void OnSettingsChanged()
        {
            _names.Clear();
            UpdateNameCount();
            RefreshSources();
            if (_numConcurrency != null && Ctx.Settings != null)
                _numConcurrency.Value = Clamp(Ctx.Settings.Concurrency, 1, 16);
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
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));    // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 172f));   // sources + options
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));    // action toolbar
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));    // stats
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 64f));     // result grid
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 36f));     // log console

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildTopCards(), 0, 1);
            root.Controls.Add(BuildToolbar(), 0, 2);
            root.Controls.Add(BuildStatsRow(), 0, 3);
            root.Controls.Add(BuildGridCard(), 0, 4);
            root.Controls.Add(BuildLogCard(), 0, 5);
            Controls.Add(root);
            ResumeLayout(true);
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
            _btnSaveReport = new FlatButton
            {
                Text = "Lưu báo cáo",
                Width = 120,
                Height = 32,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnSaveReport.Click += (s, e) => SaveReportManually();
            _btnOpenMirror = new FlatButton
            {
                Text = "Mở thư mục mirror",
                Width = 158,
                Height = 32,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnOpenMirror.Click += (s, e) => OpenFolder(Ctx.Settings == null ? null : Ctx.Settings.MirrorDir, "mirror");
            head.Controls.Add(icon);
            head.Controls.Add(title);
            head.Controls.Add(sub);
            head.Controls.Add(_btnSaveReport);
            head.Controls.Add(_btnOpenMirror);
            head.Resize += (s, e) =>
            {
                _btnSaveReport.Left = Math.Max(560, head.Width - _btnSaveReport.Width - 2);
                _btnSaveReport.Top = 14;
                _btnOpenMirror.Left = Math.Max(300, _btnSaveReport.Left - _btnOpenMirror.Width - 10);
                _btnOpenMirror.Top = 14;
            };
            return head;
        }

        private Control BuildTopCards()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var sources = BuildSourcesCard();
            sources.Margin = new Padding(0, 0, 6, 0);
            var options = BuildOptionsCard();
            options.Margin = new Padding(6, 0, 0, 0);
            row.Controls.Add(sources, 0, 0);
            row.Controls.Add(options, 1, 0);
            return row;
        }

        private Control BuildSourcesCard()
        {
            Panel body;
            var card = MakeCard("Nguồn phát hành", "Danh sách base URL và tên request", out body);
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            _lblNameCount = new Label
            {
                Text = "Chưa quét tên request",
                Font = Theme.FontBase(8.8f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            _lstBases = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontMono(8.6f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 0, 4)
            };
            _lstBases.DrawItem += BasesDrawItem;

            _btnScan = new FlatButton
            {
                Text = "Quét tên request",
                Width = 152,
                Height = 30,
                AccentColor = Theme.AccentDeep,
                HoverColor = Theme.Accent,
                Dock = DockStyle.Left,
                Margin = new Padding(0)
            };
            _btnScan.Click += (s, e) => ScanNames();

            tlp.Controls.Add(_lblNameCount, 0, 0);
            tlp.Controls.Add(_lstBases, 0, 1);
            tlp.Controls.Add(_btnScan, 0, 2);
            body.Controls.Add(tlp);
            RefreshSources();
            return card;
        }

        private void BasesDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var lb = (ListBox)sender;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var b = new SolidBrush(selected ? Theme.PanelAlt : Theme.BgDeep))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < lb.Items.Count)
            {
                var text = Convert.ToString(lb.Items[e.Index]);
                using (var f = Theme.FontMono(8.6f))
                using (var b = new SolidBrush(selected ? Theme.Text : Theme.TextDim))
                    e.Graphics.DrawString(text, f, b, e.Bounds.X + 6, e.Bounds.Y + 3);
            }
            using (var pen = new Pen(Theme.BorderSoft))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        private Control BuildOptionsCard()
        {
            Panel body;
            var card = MakeCard("Tùy chọn rip", "Ghi đè · promote · số luồng", out body);
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var checks = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _chkOverwrite = NewCheck("Ghi đè", "Ghi đè file mirror đã tồn tại");
            _chkPromoteTemplates = NewCheck("Promote template", "Promote các file template map được vào Request");
            _chkPromoteAll = NewCheck("Promote tất cả", "Promote mọi file rip được, không chỉ template");
            _chkRefresh = NewCheck("Refresh nguồn", "Kiểm tra lại base nhà phát hành trước khi rip");
            checks.Controls.Add(_chkOverwrite);
            checks.Controls.Add(_chkPromoteTemplates);
            checks.Controls.Add(_chkPromoteAll);
            checks.Controls.Add(_chkRefresh);

            var concurrency = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            var lblConcurrency = new Label
            {
                Text = "Số luồng tải:",
                Font = Theme.FontBase(9f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                AutoSize = false,
                Width = 84,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };
            _numConcurrency = new NumericUpDown
            {
                Width = 68,
                Height = 26,
                Minimum = 1,
                Maximum = 16,
                Value = 8,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBase(9.5f),
                Margin = new Padding(0, 2, 8, 0)
            };
            var lblRange = new Label
            {
                Text = "(1 – 16, khuyến nghị 6 – 10)",
                Font = Theme.FontBase(8.4f),
                ForeColor = Theme.TextFaint,
                BackColor = Theme.Card,
                AutoSize = true,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 6, 0, 0),
                Margin = new Padding(0)
            };
            concurrency.Controls.Add(lblConcurrency);
            concurrency.Controls.Add(_numConcurrency);
            concurrency.Controls.Add(lblRange);

            _progress = new NiceProgress { Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 0) };
            _lblStatus = new Label
            {
                Text = "Sẵn sàng",
                Font = Theme.FontBase(8.8f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Card,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            tlp.Controls.Add(checks, 0, 0);
            tlp.Controls.Add(concurrency, 0, 1);
            tlp.Controls.Add(_progress, 0, 2);
            tlp.Controls.Add(_lblStatus, 0, 3);
            body.Controls.Add(tlp);

            // mac dinh lay tu settings
            _chkOverwrite.Checked = true;
            _chkPromoteTemplates.Checked = true;
            _chkRefresh.Checked = true;
            _chkPromoteAll.Checked = false;
            _tips = new ToolTip();
            _tips.SetToolTip(_chkOverwrite, "Ghi đè file mirror đã tồn tại");
            _tips.SetToolTip(_chkPromoteTemplates, "Promote các file template map được vào Request");
            _tips.SetToolTip(_chkPromoteAll, "Promote mọi file rip được, không chỉ template");
            _tips.SetToolTip(_chkRefresh, "Kiểm tra lại base nhà phát hành trước khi rip");
            if (Ctx.Settings != null)
            {
                _numConcurrency.Value = Clamp(Ctx.Settings.Concurrency, 1, 16);
                _chkPromoteTemplates.Checked = Ctx.Settings.PromoteTemplatesToRequest;
            }
            return card;
        }

        private CheckBox NewCheck(string text, string tip)
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
                Margin = new Padding(0, 2, 18, 0),
                Cursor = Cursors.Hand
            };
        }

        private Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0, 0, 0, 6) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _btnDryRun = NewActionButton("Rip (dry-run)", 118, Theme.AccentDeep, Theme.Accent);
            _btnDryRun.Click += (s, e) => StartRip(true, false);
            _btnRip = NewActionButton("Rip từ nhà phát hành", 172, Theme.AccentDeep, Theme.Accent);
            _btnRip.Click += (s, e) => StartRip(false, false);
            _btnPromote = NewActionButton("Rip + Promote vào Request", 208, Theme.GreenDeep, Theme.Green);
            _btnPromote.Click += (s, e) => StartRip(false, true);
            _btnCancel = NewActionButton("Hủy", 76, Color.FromArgb(120, 40, 48), Theme.Red);
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => CancelRun();
            flow.Controls.Add(_btnDryRun);
            flow.Controls.Add(_btnRip);
            flow.Controls.Add(_btnPromote);
            flow.Controls.Add(_btnCancel);
            bar.Controls.Add(flow);
            return bar;
        }

        private FlatButton NewActionButton(string text, int width, Color accent, Color hover)
        {
            return new FlatButton
            {
                Text = text,
                Width = width,
                Height = 38,
                AccentColor = accent,
                HoverColor = hover,
                Margin = new Padding(0, 0, 8, 0)
            };
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
            _stOk = NewStat("OK", "✅", Theme.Green, "tải thành công");
            _stUnchanged = NewStat("Không đổi", "≡", Theme.TextDim, "giống bản mirror");
            _stMissing = NewStat("404", "⚠", Theme.Yellow, "không có trên server");
            _stFailed = NewStat("Lỗi", "✗", Theme.Red, "tải thất bại");
            _stBytes = NewStat("Dung lượng", "💾", Theme.Accent, "tổng file OK");
            _stBytes.Value = "0 B";
            row.Controls.Add(_stOk, 0, 0);
            row.Controls.Add(_stUnchanged, 1, 0);
            row.Controls.Add(_stMissing, 2, 0);
            row.Controls.Add(_stFailed, 3, 0);
            row.Controls.Add(_stBytes, 4, 0);
            return row;
        }

        private StatCard NewStat(string caption, string icon, Color color, string hint)
        {
            return new StatCard
            {
                Caption = caption,
                Icon = icon,
                ValueColor = color,
                Value = "0",
                Hint = hint,
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };
        }

        private Control BuildGridCard()
        {
            Panel body;
            var card = MakeCard("Kết quả rip", "Tên · bảng CSDL · map · trạng thái · dung lượng · ghi chú/URL", out body);
            card.Margin = new Padding(0);

            _grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_grid);
            EnableDoubleBuffer(_grid);
            _grid.Columns.Add(NewColumn("colName", "Tên", 170));
            _grid.Columns.Add(NewColumn("colTable", "Bảng CSDL", 132));
            _grid.Columns.Add(NewColumn("colMap", "Map", 84));
            _grid.Columns.Add(NewColumn("colStatus", "Trạng thái", 96));
            _grid.Columns.Add(NewColumn("colSize", "Dung lượng", 88));
            var note = NewColumn("colNote", "Ghi chú / URL", 200);
            note.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(note);
            body.Controls.Add(_grid);
            return card;
        }

        private static DataGridViewTextBoxColumn NewColumn(string name, string header, int width)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true,
                Resizable = DataGridViewTriState.True
            };
            return col;
        }

        private static void EnableDoubleBuffer(Control control)
        {
            try
            {
                var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null) prop.SetValue(control, true, null);
            }
            catch { }
        }

        private Control BuildLogCard()
        {
            Panel body;
            var card = MakeCard("Nhật ký rip", "Tiến trình quét / rip / promote", out body);
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

        // ============================================================ source list

        private void RefreshSources()
        {
            if (_lstBases == null) return;
            try
            {
                _lstBases.BeginUpdate();
                _lstBases.Items.Clear();
                var bases = Ctx.Settings == null ? null : Ctx.Settings.PublisherBases;
                if (bases == null || bases.Count == 0)
                {
                    _lstBases.Items.Add("(chưa cấu hình nhà phát hành)");
                }
                else
                {
                    foreach (var b in bases)
                        if (!string.IsNullOrWhiteSpace(b)) _lstBases.Items.Add(b);
                    if (_lstBases.Items.Count == 0) _lstBases.Items.Add("(chưa cấu hình nhà phát hành)");
                }
                _lstBases.EndUpdate();
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Warn("Không đọc được danh sách nguồn: " + ex.Message); } catch { }
            }
            UpdateNameCount();
        }

        private void UpdateNameCount()
        {
            if (_lblNameCount == null) return;
            _lblNameCount.Text = _names.Count > 0
                ? "Đã phát hiện " + _names.Count.ToString("N0") + " tên request"
                : "Chưa quét tên request";
        }

        private void AutoScanNames()
        {
            Task.Run(() =>
            {
                try
                {
                    var names = Ctx.Ripper.DiscoverNames(Ctx.Settings);
                    SafeUi(() =>
                    {
                        _names.Clear();
                        _names.AddRange(names);
                        UpdateNameCount();
                    });
                }
                catch (Exception ex)
                {
                    try { Ctx.Log.Warn("Không quét được tên request: " + ex.Message); } catch { }
                }
            });
        }

        private void ScanNames()
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
            SetRunningUi(true);
            SetStatus("Đang quét tên request trong Request + Mirror…");
            Task.Run(() =>
            {
                try
                {
                    var names = Ctx.Ripper.DiscoverNames(Ctx.Settings);
                    SafeUi(() =>
                    {
                        _names.Clear();
                        _names.AddRange(names);
                        UpdateNameCount();
                        SetStatus("Quét xong: " + _names.Count.ToString("N0") + " tên request.");
                    });
                }
                catch (Exception ex)
                {
                    try { Ctx.Log.Error("Quét tên request lỗi: " + ex.Message); } catch { }
                    SafeUi(() => SetStatus("Quét tên request lỗi: " + ex.Message));
                }
                finally
                {
                    SafeUi(() => SetRunningUi(false));
                    Interlocked.Exchange(ref _running, 0);
                }
            });
        }

        // ============================================================ run rip

        private void StartRip(bool dryRun, bool promote)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
            try
            {
                var s = Ctx.Settings;
                if (s == null) { SetStatus("Chưa có cấu hình."); Interlocked.Exchange(ref _running, 0); return; }
                List<string> names = _names.Count > 0 ? new List<string>(_names) : Ctx.Ripper.DiscoverNames(s);
                if (names.Count == 0)
                {
                    Ctx.Log.Warn("Không có request nào để rip. Kiểm tra RequestDir / MirrorDir trong Cài đặt.");
                    SetStatus("Không có request để rip. Kiểm tra đường dẫn trong Cài đặt.");
                    Interlocked.Exchange(ref _running, 0);
                    return;
                }
                _names.Clear();
                _names.AddRange(names);
                UpdateNameCount();

                var opt = new RipOptions
                {
                    Overwrite = _chkOverwrite.Checked,
                    PromoteTemplates = _chkPromoteTemplates.Checked,
                    PromoteAll = _chkPromoteAll.Checked,
                    DryRun = dryRun,
                    Concurrency = (int)_numConcurrency.Value,
                    RefreshFromPublisher = _chkRefresh.Checked
                };
                bool doPromote = promote && (_chkPromoteTemplates.Checked || _chkPromoteAll.Checked);
                if (promote && !doPromote)
                {
                    Ctx.Log.Warn("Bỏ chọn cả Promote template và Promote tất cả — chỉ rip, không promote.");
                }
                RunRipAsync(s, names, opt, doPromote);
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Không bắt đầu rip được: " + ex.Message); } catch { }
                SetStatus("Lỗi: " + ex.Message);
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private void RunRipAsync(AppSettings s, List<string> names, RipOptions opt, bool promote)
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _lastReport = null;
            _btnSaveReport.Enabled = false;
            _grid.Rows.Clear();
            ResetStats();
            _progress.Value = 0;
            _progress.Caption = "";
            SetRunningUi(true);
            string mode = opt.DryRun ? "dry-run" : (promote ? "rip + promote" : "rip");
            SetStatus("Bắt đầu " + mode + " " + names.Count.ToString("N0") + " request…");
            Ctx.Log.Step("Bắt đầu " + mode + " " + names.Count + " request (concurrency=" + opt.Concurrency + ")");

            int total = names.Count;
            int done = 0;
            Task.Run(() =>
            {
                try
                {
                    Action<RipEntry> onEntry = entry =>
                    {
                        int n = Interlocked.Increment(ref done);
                        SafeUi(() =>
                        {
                            AddGridRow(entry);
                            if (total > 0) _progress.Value = Math.Min(99, n * 100 / total);
                            _progress.Caption = n + "/" + total;
                            SetStatus("Đang xử lý " + n + "/" + total + " · " + (entry == null ? "" : entry.Name));
                        });
                    };

                    var report = Ctx.Ripper.Rip(s, names, opt, onEntry, ct);

                    if (promote && !opt.DryRun && !ct.IsCancellationRequested)
                    {
                        SafeUi(() => { SetStatus("Đang promote vào Request…"); _progress.Caption = "promote…"; });
                        report = Ctx.Ripper.Promote(s, report, opt.Overwrite, opt.PromoteAll, null, ct);
                    }

                    // file mirror/Request vua thay doi -> xoa cache de Dashboard/Import doc lai
                    try { Ctx.InvalidateRequestCaches(); } catch { }

                    var reportPath = Path.Combine(Ctx.ReportsDir, "request-rip-report.json");
                    try
                    {
                        Ctx.Ripper.SaveReport(report, reportPath);
                        Ctx.Log.Okay("Đã lưu báo cáo rip: " + reportPath);
                    }
                    catch (Exception ex)
                    {
                        Ctx.Log.Warn("Không lưu được báo cáo rip: " + ex.Message);
                    }

                    var fin = report;
                    SafeUi(() => FinishRun(fin, opt.DryRun, promote, reportPath));
                }
                catch (OperationCanceledException)
                {
                    try { Ctx.Log.Warn("Đã hủy rip theo yêu cầu."); } catch { }
                    SafeUi(() =>
                    {
                        SetStatus("Đã hủy.");
                        _progress.Caption = "Đã hủy";
                        SetRunningUi(false);
                    });
                }
                catch (Exception ex)
                {
                    try { Ctx.Log.Error("Rip lỗi: " + ex.Message); } catch { }
                    SafeUi(() =>
                    {
                        SetStatus("Rip lỗi: " + ex.Message);
                        SetRunningUi(false);
                    });
                }
                finally
                {
                    SafeUi(() => { if (_btnSaveReport.Enabled) _btnSaveReport.Enabled = _lastReport != null; });
                    Interlocked.Exchange(ref _running, 0);
                }
            });
        }

        private void FinishRun(RipReport report, bool dryRun, bool promote, string reportPath)
        {
            _lastReport = report;
            _btnSaveReport.Enabled = report != null;
            bool canceled = _cts != null && _cts.IsCancellationRequested;
            SetRunningUi(false);
            _progress.Value = 100;
            if (canceled) _progress.Caption = "Đã hủy";
            else _progress.Caption = report == null ? "Xong" : (dryRun ? "Dry-run xong" : "Hoàn tất");
            ApplyStats(report == null ? null : report.Stats);
            RebuildGrid(report);
            if (report == null) { SetStatus("Xong (không có báo cáo)."); return; }

            var st = report.Stats;
            var summary = string.Format("Xong: OK {0} · không đổi {1} · 404 {2} · lỗi {3} · {4}",
                st.Ok, st.Unchanged, st.Missing, st.Failed, Paths.FormatSize(st.Bytes));
            if (promote) summary += " · promote " + st.Promoted + " file";
            if (dryRun) summary = "Dry-run: " + summary;
            if (canceled) summary = "Đã hủy (kết quả một phần): " + summary;
            SetStatus(summary);
            Ctx.SetStatus("Rip " + (canceled ? "đã hủy" : (dryRun ? "dry-run" : "hoàn tất")) + ": OK " + st.Ok + "/" + report.Entries.Count);
            Ctx.Log.Okay(summary + "  ·  report: " + reportPath);
        }

        private void ResetStats()
        {
            if (_stOk != null) _stOk.Value = "0";
            if (_stUnchanged != null) _stUnchanged.Value = "0";
            if (_stMissing != null) _stMissing.Value = "0";
            if (_stFailed != null) _stFailed.Value = "0";
            if (_stBytes != null) _stBytes.Value = "0 B";
        }

        private void ApplyStats(RipStats st)
        {
            if (st == null) { ResetStats(); return; }
            _stOk.Value = st.Ok.ToString("N0");
            _stOk.Hint = "tải thành công / " + st.Total.ToString("N0");
            _stUnchanged.Value = st.Unchanged.ToString("N0");
            _stMissing.Value = st.Missing.ToString("N0");
            _stFailed.Value = st.Failed.ToString("N0");
            _stBytes.Value = Paths.FormatSize(st.Bytes);
            _stBytes.Hint = "thời gian " + st.Seconds.ToString("0.0") + "s";
        }

        private void SetRunningUi(bool running)
        {
            bool enabled = !running;
            foreach (var b in new[] { _btnDryRun, _btnRip, _btnPromote })
                if (b != null) b.Enabled = enabled;
            if (_btnCancel != null) _btnCancel.Enabled = running;
            if (_btnScan != null) _btnScan.Enabled = enabled;
            if (_btnOpenMirror != null) _btnOpenMirror.Enabled = enabled;
            if (_numConcurrency != null) _numConcurrency.Enabled = enabled;
            foreach (var c in new[] { _chkOverwrite, _chkPromoteTemplates, _chkPromoteAll, _chkRefresh })
                if (c != null) c.Enabled = enabled;
        }

        private void CancelRun()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    Ctx.Log.Warn("Đang hủy rip…");
                    SetStatus("Đang hủy…");
                    if (_btnCancel != null) _btnCancel.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Không hủy được: " + ex.Message); } catch { }
            }
        }

        private void SaveReportManually()
        {
            try
            {
                if (_lastReport == null)
                {
                    SetStatus("Chưa có báo cáo rip để lưu.");
                    return;
                }
                var path = Path.Combine(Ctx.ReportsDir, "request-rip-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                Ctx.Ripper.SaveReport(_lastReport, path);
                Ctx.Log.Okay("Đã lưu báo cáo rip: " + path);
                SetStatus("Đã lưu: " + path);
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Lưu báo cáo lỗi: " + ex.Message); } catch { }
                SetStatus("Lưu báo cáo lỗi: " + ex.Message);
            }
        }

        private void OpenFolder(string path, string label)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    SetStatus("Thư mục " + label + " không tồn tại: " + (path ?? "(trống)"));
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Error("Không mở được thư mục " + label + ": " + ex.Message); } catch { }
                SetStatus("Không mở được thư mục " + label + ": " + ex.Message);
            }
        }

        // ============================================================ grid

        private void AddGridRow(RipEntry e)
        {
            if (_grid == null || _grid.IsDisposed || e == null) return;
            try
            {
                int idx = _grid.Rows.Add(
                    e.Name,
                    e.TableName ?? "",
                    MapText(e.MapStatus),
                    StatusText(e.Status),
                    e.Size > 0 ? Paths.FormatSize(e.Size) : "",
                    NoteText(e));
                var row = _grid.Rows[idx];
                row.DefaultCellStyle.ForeColor = StatusColor(e.Status);
            }
            catch (Exception ex)
            {
                try { Ctx.Log.Warn("Không thêm dòng kết quả: " + ex.Message); } catch { }
            }
        }

        private void RebuildGrid(RipReport report)
        {
            if (_grid == null || _grid.IsDisposed) return;
            if (report == null || report.Entries == null) return;
            try
            {
                _grid.SuspendLayout();
                _grid.Rows.Clear();
                foreach (var e in report.Entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    AddGridRow(e);
                _grid.ResumeLayout();
            }
            catch { }
        }

        private static string StatusText(string status)
        {
            switch ((status ?? "").ToLowerInvariant())
            {
                case "ok": return "✓ OK";
                case "unchanged": return "= Không đổi";
                case "missing": return "⚠ 404";
                case "failed": return "✗ Lỗi";
                case "skipped": return "- Bỏ qua";
                default: return status ?? "";
            }
        }

        private static string MapText(string map)
        {
            switch ((map ?? "").ToLowerInvariant())
            {
                case "exact": return "đúng tên";
                case "exact-warn": return "đúng tên ⚠";
                case "auto": return "tự động";
                case "auto-warn": return "tự động ⚠";
                case "override": return "thủ công";
                case "ambiguous": return "mơ hồ";
                case "none": return "không";
                case "": return "";
                default: return map;
            }
        }

        private static Color StatusColor(string status)
        {
            switch ((status ?? "").ToLowerInvariant())
            {
                case "ok": return Theme.Green;
                case "unchanged": return Theme.TextDim;
                case "missing": return Theme.Yellow;
                case "failed": return Theme.Red;
                case "skipped": return Theme.TextFaint;
                default: return Theme.Text;
            }
        }

        private static string NoteText(RipEntry e)
        {
            if (!string.IsNullOrEmpty(e.Error)) return e.Error;
            return e.Url ?? "";
        }

        // ============================================================ helpers

        private void SetStatus(string text)
        {
            if (_lblStatus != null && !_lblStatus.IsDisposed)
            {
                _lblStatus.Text = text ?? "";
                if (_tips != null) _tips.SetToolTip(_lblStatus, text ?? "");
            }
        }

        private void SafeUi(Action action)
        {
            try { UiInvoke(action); } catch { }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ============================================================ log console

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
                for (int i = Math.Max(0, snapshot.Length - 60); i < snapshot.Length; i++) _log.Add(snapshot[i]);
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
