﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;

namespace GunnyRequestStudio.Pages
{
    /// <summary>
    /// Trang "Giải mã XML": file .xml request của Gunny là zlib-compressed.
    /// Xem trước nội dung XML thật, giải mã 1 file / cả thư mục, và nén lại
    /// (.xml thường -> .xml zlib) để đưa vào WebSite\Request.
    /// </summary>
    public class DecodePage : PageBase
    {
        private const int MaxLogLines = 400;

        private TableLayoutPanel _root;
        private TextBox _txtSource, _txtOut;
        private CheckBox _chkKeepTree, _chkOverwrite, _chkAnalyze;
        private FlatButton _btnPickFile, _btnPickFolder, _btnScan, _btnDecodeDir, _btnDecodeFile, _btnEncode, _btnOpenOut, _btnCancel;
        private NiceProgress _progress;
        private Label _lblStatus, _lblSourceInfo;
        private DataGridView _grid;
        private RichTextBox _preview;
        private Label _lblMeta;
        private RichTextBox _log;
        private CancellationTokenSource _cts;
        private bool _busy;
        private DecodeService _svc;
        private List<DecodeResult> _results = new List<DecodeResult>();
        private Action<LogEntry> _logHandler;

        public DecodePage(StudioContext ctx) : base(ctx)
        {
            BuildUi();
            // nap schema/map trong nen de lan dau mo trang khong bi khung
            System.Threading.Tasks.Task.Run(() => { try { EnsureService(); } catch { } });
        }

        public override string PageTitle { get { return "Giải mã XML"; } }
        public override string PageIcon { get { return "◧"; } }
        public override string PageSubtitle { get { return "Giải nén zlib → XML đọc được · nén lại cho Request"; } }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_logHandler != null) Ctx.Log.OnLog -= _logHandler; } catch { }
                try { if (_cts != null) _cts.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        public override void OnShownPage()
        {
            try
            {
                if (_txtSource != null && _txtSource.TextLength == 0 && Ctx.Settings != null)
                    _txtSource.Text = Ctx.Settings.MirrorDir;
                if (!_busy)
                {
                    if (_lblStatus != null) _lblStatus.Text = "Sẵn sàng. Chọn file hoặc thư mục .xml để bắt đầu.";
                    if (_progress != null) { _progress.Value = 0; _progress.Caption = ""; }
                }
                UpdateSourceInfo();
            }
            catch (Exception ex) { Ctx.Log.Warn("Giải mã XML: " + ex.Message); }
        }

        // ============================================================ UI

        private void BuildUi()
        {
            SuspendLayout();
            BackColor = Theme.Bg;
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(0)
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));    // header
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f));   // paths + options
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));    // buttons
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));    // progress
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));    // grid + preview
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));   // log

            _root.Controls.Add(BuildHeader(), 0, 0);
            _root.Controls.Add(BuildPathCard(), 0, 1);
            _root.Controls.Add(BuildActions(), 0, 2);
            _root.Controls.Add(BuildProgress(), 0, 3);
            _root.Controls.Add(BuildCenter(), 0, 4);
            _root.Controls.Add(BuildLogCard(), 0, 5);

            Controls.Add(_root);

            _svc = null;
            try { EnsureService(); } catch (Exception ex) { Ctx.Log.Warn("Giải mã: " + ex.Message); }

            _logHandler = OnLogEntry;
            Ctx.Log.OnLog += _logHandler;

            AllowDrop = true;
            DragEnter += (s, e) =>
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += (s, e) => { try { OnDrop((string[])e.Data.GetData(DataFormats.FileDrop)); } catch { } };
            ResumeLayout(true);
        }

        private void EnsureService()
        {
            if (_svc != null) return;
            TableMapper mapper = null;
            try { mapper = Ctx.Mapper; } catch { }
            _svc = new DecodeService(Ctx.Log, mapper);
        }

        private Control BuildHeader()
        {
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0) };
            var icon = new Label
            {
                Text = "◧",
                Font = new Font("Segoe UI Symbol", 22f, FontStyle.Regular),
                ForeColor = Theme.Orange,
                AutoSize = false,
                Width = 42,
                Height = 44,
                Left = 0,
                Top = 2,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.Bg
            };
            var title = new Label
            {
                Text = PageTitle,
                Font = Theme.FontTitle(21f),
                ForeColor = Theme.Text,
                BackColor = Theme.Bg,
                AutoSize = true,
                Left = 44,
                Top = 0
            };
            var sub = new Label
            {
                Text = "File .xml request của Gunny là zlib — xem nội dung thật, giải mã ra thư mục, hoặc nén lại để đưa vào Request",
                Font = Theme.FontBase(9.2f),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Bg,
                AutoSize = true,
                Left = 46,
                Top = 34
            };
            _btnPickFile = new FlatButton
            {
                Text = "◫  Chọn file .xml...",
                Width = 158,
                Height = 32,
                AccentColor = Theme.AccentDeep,
                HoverColor = Theme.Accent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnPickFile.Click += (s, e) => PickFile();
            _btnPickFolder = new FlatButton
            {
                Text = "▤  Chọn thư mục...",
                Width = 158,
                Height = 32,
                AccentColor = Color.White,
                HoverColor = Theme.SkySoft,
                BorderColor2 = Theme.Border,
                ForeColor = Theme.AccentDeep,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnPickFolder.Click += (s, e) => PickFolder();
            head.Controls.Add(icon);
            head.Controls.Add(title);
            head.Controls.Add(sub);
            head.Controls.Add(_btnPickFile);
            head.Controls.Add(_btnPickFolder);
            head.Resize += (s, e) =>
            {
                _btnPickFolder.Left = Math.Max(360, head.Width - _btnPickFolder.Width - 2);
                _btnPickFolder.Top = 8;
                _btnPickFile.Left = Math.Max(200, _btnPickFolder.Left - _btnPickFile.Width - 10);
                _btnPickFile.Top = 8;
            };
            return head;
        }

        private Control BuildPathCard()
        {
            var card = new ThemedPanel { Dock = DockStyle.Fill, Radius = 12, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(14, 9, 14, 9) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 4, RowCount = 3 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var l1 = MakeLabel("Nguồn");
            _txtSource = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 8, 3) };
            Style.Apply(_txtSource);
            _txtSource.TextChanged += (s, e) => UpdateSourceInfo();
            _lblSourceInfo = MakeLabel("");
            _lblSourceInfo.ForeColor = Theme.TextFaint;
            _lblSourceInfo.Font = Theme.FontBase(8.4f);

            var l2 = MakeLabel("Thư mục đích");
            _txtOut = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 8, 3) };
            Style.Apply(_txtOut);

            var opts = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            _chkKeepTree = new CheckBox { Text = "Giữ cấu trúc thư mục", AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Color.Transparent, Margin = new Padding(0, 0, 18, 0) };
            _chkOverwrite = new CheckBox { Text = "Ghi đè file đã có", AutoSize = true, Checked = false, ForeColor = Theme.Text, BackColor = Color.Transparent, Margin = new Padding(0, 0, 18, 0) };
            _chkAnalyze = new CheckBox { Text = "Phân tích map bảng CSDL", AutoSize = true, Checked = true, ForeColor = Theme.Text, BackColor = Color.Transparent };
            opts.Controls.Add(_chkKeepTree);
            opts.Controls.Add(_chkOverwrite);
            opts.Controls.Add(_chkAnalyze);

            t.Controls.Add(l1, 0, 0);
            t.Controls.Add(_txtSource, 1, 0);
            t.SetColumnSpan(_txtSource, 3);
            t.Controls.Add(l2, 0, 1);
            t.Controls.Add(_txtOut, 1, 1);
            t.SetColumnSpan(_txtOut, 3);
            t.Controls.Add(new Label { Text = "", Width = 1 }, 0, 2);
            t.Controls.Add(opts, 1, 2);
            t.SetColumnSpan(opts, 2);
            t.Controls.Add(_lblSourceInfo, 3, 2);
            card.Controls.Add(t);
            return card;
        }

        private Control BuildActions()
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, WrapContents = false, Padding = new Padding(0, 2, 0, 4) };
            _btnScan = MakeButton("⌕  Xem trước", Theme.AccentDeep, Theme.Accent, 132);
            _btnScan.Click += (s, e) => ScanAsync();
            _btnDecodeDir = MakeButton("↓  Giải mã ra thư mục", Theme.Yellow, Theme.Orange, 196);
            _btnDecodeDir.Click += (s, e) => DecodeBatchAsync(false);
            _btnDecodeFile = MakeButton("◫  Lưu file thành .xml", Color.FromArgb(37, 86, 72), Theme.Green, 180);
            _btnDecodeFile.Click += (s, e) => SaveDecodedFile();
            _btnEncode = MakeButton("▸  Nén lại (.xml → zlib)", Color.FromArgb(64, 48, 96), Theme.Purple, 196);
            _btnEncode.Click += (s, e) => EncodeAsync();
            _btnOpenOut = MakeButton("▤  Mở thư mục đích", Color.White, Theme.SkySoft, 164, Theme.AccentDeep, Theme.Border);
            _btnOpenOut.Click += (s, e) => OpenFolder();
            _btnCancel = MakeButton("✕  Hủy", Color.White, Color.FromArgb(253, 238, 241), 92, Theme.Red, Theme.Border);
            _btnCancel.Click += (s, e) => Cancel();
            _btnCancel.Enabled = false;
            p.Controls.Add(_btnScan);
            p.Controls.Add(_btnDecodeDir);
            p.Controls.Add(_btnDecodeFile);
            p.Controls.Add(_btnEncode);
            p.Controls.Add(_btnOpenOut);
            p.Controls.Add(_btnCancel);
            return p;
        }

        private Control BuildProgress()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, ColumnCount = 2, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));
            _progress = new NiceProgress { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 12, 2) };
            _lblStatus = new Label
            {
                Text = "Sẵn sàng. Chọn file hoặc thư mục .xml để bắt đầu.",
                ForeColor = Theme.TextDim,
                BackColor = Theme.Bg,
                Font = Theme.FontBase(8.8f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill
            };
            t.Controls.Add(_progress, 0, 0);
            t.Controls.Add(_lblStatus, 1, 0);
            return t;
        }

        private Control BuildCenter()
        {
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // --- grid card
            var gridCard = new ThemedPanel { Dock = DockStyle.Fill, Radius = 12, Margin = new Padding(0, 0, 8, 8), Padding = new Padding(12, 8, 12, 8) };
            var gridHead = new Label
            {
                Text = "KẾT QUẢ GIẢI MÃ",
                Font = Theme.FontBold(8.4f),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 22
            };
            _grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Style.Apply(_grid);
            _grid.Columns.Add(MakeCol("File", 170));
            _grid.Columns.Add(MakeCol("Dạng", 70));
            _grid.Columns.Add(MakeCol("Nén", 70, true));
            _grid.Columns.Add(MakeCol("Giải nén", 78, true));
            _grid.Columns.Add(MakeCol("Root/Items", 100));
            _grid.Columns.Add(MakeCol("Bảng CSDL", 130));
            _grid.Columns.Add(MakeCol("Trạng thái", 110));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SaveDecodedFile(); };
            _grid.SelectionChanged += (s, e) => ShowSelectedPreview();
            gridCard.Controls.Add(gridHead);
            gridCard.Controls.Add(_grid);

            // --- preview card
            var prevCard = new ThemedPanel { Dock = DockStyle.Fill, Radius = 12, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(12, 8, 12, 8) };
            var prevHead = new Label
            {
                Text = "NỘI DUNG XML (đã giải nén)",
                Font = Theme.FontBold(8.4f),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 22
            };
            _lblMeta = new Label
            {
                Text = "",
                Font = Theme.FontBase(8.5f),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 36
            };
            _preview = new RichTextBox { Dock = DockStyle.Fill, WordWrap = false };
            Style.Apply(_preview);
            // (preview)
            prevCard.Controls.Add(prevHead);
            prevCard.Controls.Add(_lblMeta);
            prevCard.Controls.Add(_preview);

            split.Controls.Add(gridCard, 0, 0);
            split.Controls.Add(prevCard, 1, 0);
            return split;
        }


        private Control BuildLogCard()
        {
            var card = new ThemedPanel { Dock = DockStyle.Fill, Radius = 12, Margin = new Padding(0, 0, 0, 2), Padding = new Padding(12, 8, 12, 8) };
            var head = new Label
            {
                Text = "NHẬT KÝ",
                Font = Theme.FontBold(8.4f),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 20
            };
            _log = new RichTextBox { Dock = DockStyle.Fill };
            Style.Apply(_log);
            card.Controls.Add(head);
            card.Controls.Add(_log);
            return card;
        }

        private Label MakeLabel(string text)
        {
            return new Label { Text = text, ForeColor = Theme.TextDim, BackColor = Color.Transparent, Font = Theme.FontBase(9f), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        }

        private FlatButton MakeButton(string text, Color accent, Color hover, int width, Color? fore = null, Color? border = null)
        {
            var b = new FlatButton
            {
                Text = text,
                Width = width,
                Height = 32,
                AccentColor = accent,
                HoverColor = hover,
                Margin = new Padding(0, 0, 8, 0)
            };
            if (fore.HasValue) b.ForeColor = fore.Value;
            if (border.HasValue) b.BorderColor2 = border.Value;
            return b;
        }

        private static DataGridViewTextBoxColumn MakeCol(string header, int width, bool right = false)
        {
            var c = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return c;
        }

        // ============================================================ chon nguon

        private void PickFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn file .xml request (zlib hoặc XML thường)";
                dlg.Filter = "XML request (*.xml)|*.xml|Tất cả file (*.*)|*.*";
                dlg.InitialDirectory = SafeDir(Ctx.Settings == null ? null : Ctx.Settings.MirrorDir);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtSource.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(_txtOut.Text))
                    _txtOut.Text = Path.GetDirectoryName(dlg.FileName);
                PreviewFile(dlg.FileName);
            }
        }

        private void PickFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Chọn thư mục chứa .xml (ví dụ: WebSite\\Request\\vng-quest)";
                dlg.SelectedPath = SafeDir(Ctx.Settings == null ? null : Ctx.Settings.RequestDir);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                _txtSource.Text = dlg.SelectedPath;
                if (string.IsNullOrWhiteSpace(_txtOut.Text)) _txtOut.Text = dlg.SelectedPath + "_decoded";
                UpdateSourceInfo();
            }
        }

        private void OnDrop(string[] files)
        {
            if (files == null || files.Length == 0) return;
            _txtSource.Text = files[0];
            if (string.IsNullOrWhiteSpace(_txtOut.Text))
                _txtOut.Text = Directory.Exists(files[0]) ? files[0] + "_decoded" : Path.GetDirectoryName(files[0]);
            if (File.Exists(files[0])) PreviewFile(files[0]);
            else UpdateSourceInfo();
            Ctx.Log.Info("Giải mã: nhận " + files.Length + " đường dẫn (kéo-thả)");
        }

        private void UpdateSourceInfo()
        {
            if (_lblSourceInfo == null) return;
            try
            {
                var src = _txtSource.Text.Trim();
                if (File.Exists(src))
                {
                    var fi = new FileInfo(src);
                    bool packed = DecodeService.IsPackedXml(src);
                    _lblSourceInfo.Text = string.Format("file · {0} · {1}", Paths.FormatSize(fi.Length), packed ? "zlib" : "xml thường");
                    _lblSourceInfo.ForeColor = packed ? Theme.Orange : Theme.Green;
                }
                else if (Directory.Exists(src))
                {
                    int n = 0;
                    try { n = Directory.GetFiles(src, "*.xml", SearchOption.TopDirectoryOnly).Length; } catch { }
                    _lblSourceInfo.Text = n + " file .xml";
                    _lblSourceInfo.ForeColor = Theme.TextDim;
                }
                else
                {
                    _lblSourceInfo.Text = "chưa chọn";
                    _lblSourceInfo.ForeColor = Theme.TextFaint;
                }
            }
            catch { }
        }

        // ============================================================ preview

        private void PreviewFile(string path)
        {
            try
            {
                EnsureService();
                var r = _svc.DecodeFile(path, true, _chkAnalyze.Checked);
                _results = new List<DecodeResult> { r };
                FillGrid(_results);
                ShowPreview(r);
                UpdateSourceInfo();
                if (r.Status != "ok") Ctx.Log.Warn("Giải mã file lỗi: " + r.Name + " — " + r.Error);
                else Ctx.Log.Okay(string.Format("Giải mã {0}: {1} → {2}, root={3}, items={4}",
                    r.Name, Paths.FormatSize(r.FileSize), Paths.FormatSize(r.DecodedSize), r.Root, r.ItemCount));
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Giải mã file lỗi: " + ex.Message);
            }
        }

        private void ShowSelectedPreview()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return;
            var idx = _grid.SelectedRows[0].Index;
            if (idx < 0 || idx >= _results.Count) return;
            ShowPreview(_results[idx]);
        }

        private void ShowPreview(DecodeResult r)
        {
            if (r == null) return;
            try
            {
                _preview.Text = r.XmlText ?? ("(không đọc được: " + (r.Error ?? "?") + ")");
                ColorizeXml(_preview);
                _preview.SelectionStart = 0;
                _preview.ScrollToCaret();
                _lblMeta.Text = string.Format("{0}   ·   {1} → {2}   ·   root={3}{4}{5}",
                    r.Name,
                    Paths.FormatSize(r.FileSize), Paths.FormatSize(r.DecodedSize),
                    r.Root ?? "?",
                    r.Wrapper != null ? " / " + r.Wrapper : "",
                    string.IsNullOrEmpty(r.TableName) ? "" : "   ·   bảng: " + r.TableName + " [" + (r.MapStatus ?? "?") + "]");
            }
            catch (Exception ex)
            {
                Ctx.Log.Warn("Không hiển thị được preview: " + ex.Message);
            }
        }

        /// <summary>To mau XML don gian (tag xanh, thuoc tinh vang, gia tri xanh la) de de doc.</summary>
        private void ColorizeXml(RichTextBox box)
        {
            try
            {
                var text = box.Text;
                if (text.Length > 400000) return;   // file qua lon: de nguyen cho nhanh
                box.SuspendLayout();
                box.SelectAll();
                box.SelectionColor = Theme.Text;
                int i = 0;
                while (i < text.Length)
                {
                    int open = text.IndexOf('<', i);
                    if (open < 0) break;
                    int close = text.IndexOf('>', open);
                    if (close < 0) break;
                    box.Select(open, 1);
                    box.SelectionColor = Theme.AccentDeep;
                    // ten tag
                    int nameEnd = open + 1;
                    if (nameEnd < text.Length && (text[nameEnd] == '/' || text[nameEnd] == '?' || text[nameEnd] == '!')) nameEnd++;
                    int nameStart = nameEnd;
                    while (nameEnd < close && !char.IsWhiteSpace(text[nameEnd]) && text[nameEnd] != '/' && text[nameEnd] != '?') nameEnd++;
                    if (nameEnd > nameStart)
                    {
                        box.Select(nameStart, nameEnd - nameStart);
                        box.SelectionColor = Theme.Accent;
                    }
                    // thuoc tinh = gia tri
                    int k = nameEnd;
                    while (k < close)
                    {
                        int eq = text.IndexOf('=', k);
                        if (eq < 0 || eq > close) break;
                        int q1 = text.IndexOf('"', eq);
                        if (q1 < 0 || q1 > close) break;
                        int q2 = text.IndexOf('"', q1 + 1);
                        if (q2 < 0 || q2 > close + 1) break;
                        int attrStart = k;
                        while (attrStart < eq && char.IsWhiteSpace(text[attrStart])) attrStart++;
                        box.Select(attrStart, eq - attrStart);
                        box.SelectionColor = Theme.Orange;
                        box.Select(q1, q2 - q1 + 1);
                        box.SelectionColor = Theme.Green;
                        k = q2 + 1;
                    }
                    i = close + 1;
                }
                box.SelectionStart = 0;
                box.SelectionLength = 0;
                box.SelectionColor = Theme.Text;
                box.ResumeLayout();
            }
            catch { }
        }

        private void FillGrid(List<DecodeResult> results)
        {
            if (_grid == null) return;
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            foreach (var r in results)
            {
                int idx = _grid.Rows.Add(
                    r.Name,
                    r.WasCompressed ? "zlib" : "xml",
                    Paths.FormatSize(r.FileSize),
                    Paths.FormatSize(r.DecodedSize),
                    (r.Root ?? "?") + (r.ItemCount > 0 ? " / " + r.ItemCount : ""),
                    r.TableName ?? "—",
                    r.Status == "ok" ? (r.OutputPath != null ? "đã ghi" : "ok") : r.Status);
                if (idx >= 0)
                {
                    var row = _grid.Rows[idx];
                    row.Cells[1].Style.ForeColor = r.WasCompressed ? Theme.Orange : Theme.Green;
                    row.Cells[5].Style.ForeColor = string.IsNullOrEmpty(r.TableName) ? Theme.TextFaint : Theme.AccentDeep;
                    row.Cells[6].Style.ForeColor = r.Status == "ok" ? Theme.Green : Theme.Red;
                }
            }
            _grid.ResumeLayout();
        }

        // ============================================================ thao tac

        private void ScanAsync()
        {
            RunAsync(async token =>
            {
                var src = _txtSource.Text.Trim();
                if (string.IsNullOrEmpty(src))
                {
                    Ctx.Log.Warn("Giải mã: chưa chọn file/thư mục nguồn");
                    return;
                }
                if (File.Exists(src))
                {
                    var r = await Task.Run(() => { EnsureService(); return _svc.DecodeFile(src, true, _chkAnalyze.Checked); }, token);
                    _results = new List<DecodeResult> { r };
                    Post(() => { FillGrid(_results); ShowPreview(r); _progress.Value = 100; _progress.Caption = "1 file"; _lblStatus.Text = "Đã giải mã xem trước: " + r.Name; });
                    return;
                }
                if (!Directory.Exists(src)) { Ctx.Log.Warn("Giải mã: không thấy " + src); return; }
                int done = 0;
                _scanTotal = SafeCount(src);
                var results = await Task.Run(() =>
                {
                    EnsureService();
                    return _svc.Scan(src, "*.xml", r =>
                    {
                        done++;
                        if (done % 20 != 0) return;
                        int n = done;
                        Post(() =>
                        {
                            _progress.Value = Math.Min(99, n * 100 / Math.Max(1, _scanTotal));
                            _progress.Caption = "đã xem " + n + " file";
                        });
                    }, token);
                }, token);
                _results = results;
                int packed = results.Count(x => x.WasCompressed);
                Post(() =>
                {
                    FillGrid(results);
                    _progress.Value = 100;
                    _progress.Caption = results.Count + " file";
                    _lblStatus.Text = string.Format("Xem trước {0} file · {1} zlib · {2} XML thường", results.Count, packed, results.Count - packed);
                    if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
                });
                Ctx.Log.Okay(string.Format("Xem trước: {0} file ({1} zlib, {2} xml thường)", results.Count, packed, results.Count - packed));
            }, "Xem trước");
        }

        private int _scanTotal;

        private void DecodeBatchAsync(bool _)
        {
            RunAsync(async token =>
            {
                var src = _txtSource.Text.Trim();
                var dst = _txtOut.Text.Trim();
                if (string.IsNullOrEmpty(src)) { Ctx.Log.Warn("Giải mã: chưa chọn nguồn"); return; }
                if (string.IsNullOrEmpty(dst)) { Ctx.Log.Warn("Giải mã: chưa chọn thư mục đích"); return; }
                EnsureService();
                if (File.Exists(src))
                {
                    var r = await Task.Run(() => _svc.DecodeFile(src, true, _chkAnalyze.Checked), token);
                    if (r.Status != "ok") { Ctx.Log.Error("Không giải mã được: " + r.Error); return; }
                    var outPath = Path.Combine(dst, Path.GetFileNameWithoutExtension(src) + ".xml");
                    Paths.EnsureDir(dst);
                    if (File.Exists(outPath) && !_chkOverwrite.Checked) { Ctx.Log.Warn("Đã tồn tại (bật Ghi đè): " + outPath); return; }
                    Paths.WriteAtomic(outPath, Encoding.UTF8.GetBytes(r.XmlText));
                    r.OutputPath = outPath;
                    Post(() => { FillGrid(new List<DecodeResult> { r }); _progress.Value = 100; _lblStatus.Text = "Đã ghi " + outPath; });
                    Ctx.Log.Okay("Đã giải mã ra file: " + outPath);
                    return;
                }
                if (!Directory.Exists(src)) { Ctx.Log.Warn("Giải mã: không thấy " + src); return; }
                var files = SafeCount(src);
                int done = 0;
                var results = await Task.Run(() => _svc.DecodeDirectory(src, dst, "*.xml", _chkKeepTree.Checked, _chkOverwrite.Checked, _chkAnalyze.Checked,
                    r =>
                    {
                        done++;
                        if (done % 10 != 0) return;
                        int n = done;
                        Post(() =>
                        {
                            _progress.Value = files > 0 ? Math.Min(99, n * 100 / files) : 0;
                            _progress.Caption = "đã giải mã " + n + "/" + (files > 0 ? files.ToString() : "?");
                        });
                    }, token), token);
                _results = results;
                Post(() =>
                {
                    FillGrid(results);
                    _progress.Value = 100;
                    int ok = results.Count(x => x.Status == "ok");
                    _progress.Caption = ok + " file";
                    _lblStatus.Text = string.Format("Đã giải mã {0}/{1} file → {2}", ok, results.Count, dst);
                });
                SaveReport("decode");
            }, "Giải mã");
        }

        private void SaveDecodedFile()
        {
            try
            {
                DecodeResult r = null;
                if (_grid != null && _grid.SelectedRows.Count > 0)
                {
                    var idx = _grid.SelectedRows[0].Index;
                    if (idx >= 0 && idx < _results.Count) r = _results[idx];
                }
                var src = _txtSource.Text.Trim();
                if (r == null && File.Exists(src))
                {
                    EnsureService();
                    r = _svc.DecodeFile(src, true, _chkAnalyze.Checked);
                }
                if (r == null || r.Status != "ok")
                {
                    MessageBox.Show(this, "Chưa có nội dung XML để lưu. Hãy chọn file và bấm \"Xem trước\".",
                        "Giải mã XML", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "Lưu XML đã giải mã";
                    dlg.Filter = "XML file (*.xml)|*.xml";
                    dlg.FileName = r.Name + ".xml";
                    var baseDir = !string.IsNullOrWhiteSpace(_txtOut.Text) ? _txtOut.Text : Path.GetDirectoryName(r.Path);
                    dlg.InitialDirectory = SafeDir(baseDir);
                    if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                    Paths.WriteAtomic(dlg.FileName, Encoding.UTF8.GetBytes(r.XmlText));
                    r.OutputPath = dlg.FileName;
                    Ctx.Log.Okay("Đã lưu XML giải mã: " + dlg.FileName);
                    _lblStatus.Text = "Đã lưu " + dlg.FileName;
                    FillGrid(_results);
                }
            }
            catch (Exception ex)
            {
                Ctx.Log.Error("Lưu file giải mã lỗi: " + ex.Message);
            }
        }

        private void EncodeAsync()
        {
            RunAsync(async token =>
            {
                var src = _txtSource.Text.Trim();
                var dst = _txtOut.Text.Trim();
                if (string.IsNullOrEmpty(src)) { Ctx.Log.Warn("Nén lại: chưa chọn nguồn"); return; }
                EnsureService();
                if (File.Exists(src))
                {
                    var outPath = Path.Combine(string.IsNullOrEmpty(dst) ? Path.GetDirectoryName(src) : dst,
                        Path.GetFileNameWithoutExtension(src) + ".xml");
                    var r = await Task.Run(() => _svc.EncodeFile(src, outPath, true), token);
                    Post(() =>
                    {
                        if (r.Status == "ok")
                        {
                            _progress.Value = 100;
                            _progress.Caption = "đã nén";
                            _lblStatus.Text = "Đã nén: " + Strings.Shorten(Paths.FormatSize(r.FileSize), 12) + " → " + Paths.FormatSize(new FileInfo(r.OutputPath).Length);
                            Ctx.Log.Okay("Đã nén lại: " + r.OutputPath);
                        }
                        else Ctx.Log.Error("Nén lỗi: " + r.Error);
                    });
                    return;
                }
                if (!Directory.Exists(src)) { Ctx.Log.Warn("Nén lại: không thấy " + src); return; }
                var outDir = string.IsNullOrEmpty(dst) ? src + "_packed" : dst;
                int total = SafeCount(src);
                int done = 0;
                var results = await Task.Run(() => _svc.EncodeDirectory(src, outDir, "*.xml", _chkKeepTree.Checked, _chkOverwrite.Checked,
                    r =>
                    {
                        done++;
                        if (done % 10 != 0) return;
                        int n = done;
                        Post(() => { _progress.Value = total > 0 ? Math.Min(99, n * 100 / total) : 0; _progress.Caption = "đã nén " + n + "/" + (total > 0 ? total.ToString() : "?"); });
                    }, token), token);
                _results = results;
                Post(() =>
                {
                    FillGrid(results);
                    _progress.Value = 100;
                    int ok = results.Count(x => x.Status == "ok");
                    _lblStatus.Text = string.Format("Đã nén {0}/{1} file → {2}", ok, results.Count, outDir);
                });
                SaveReport("encode");
            }, "Nén lại");
        }

        private void SaveReport(string mode)
        {
            try
            {
                var path = Path.Combine(Ctx.ReportsDir, "decode-report.json");
                Json.SaveFile(path, new DecodeCliReport
                {
                    GeneratedAt = DateTime.Now.ToString("s"),
                    Source = _txtSource.Text.Trim(),
                    Target = _txtOut.Text.Trim(),
                    Mode = mode,
                    Total = _results.Count,
                    Ok = _results.Count(x => x.Status == "ok"),
                    Skipped = _results.Count(x => x.Status == "skipped"),
                    Failed = _results.Count(x => x.Status == "failed"),
                    Entries = _results
                });
                Ctx.Log.Info("Đã lưu report giải mã: " + path);
            }
            catch (Exception ex) { Ctx.Log.Warn("Không lưu được report giải mã: " + ex.Message); }
        }

        private void OpenFolder()
        {
            try
            {
                var dir = _txtOut.Text.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    var src = _txtSource.Text.Trim();
                    dir = File.Exists(src) ? Path.GetDirectoryName(src) : src;
                }
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                else Ctx.Log.Warn("Không có thư mục để mở.");
            }
            catch (Exception ex) { Ctx.Log.Warn("Mở thư mục lỗi: " + ex.Message); }
        }

        private void Cancel()
        {
            try { if (_cts != null) _cts.Cancel(); } catch { }
        }

        // ============================================================ helpers

        private void RunAsync(Func<CancellationToken, Task> work, string title)
        {
            if (_busy) { Ctx.Log.Warn("Đang bận: " + title); return; }
            _busy = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Post(() =>
            {
                _progress.Value = 0;
                _progress.Caption = title.ToLowerInvariant() + "...";
                _lblStatus.Text = title + "...";
                SetButtons(false);
            });
            Task.Run(async () =>
            {
                try
                {
                    await work(token);
                }
                catch (OperationCanceledException)
                {
                    Ctx.Log.Warn("Đã hủy: " + title);
                    Post(() => { _progress.Caption = "đã hủy"; _lblStatus.Text = "Đã hủy."; });
                }
                catch (Exception ex)
                {
                    Ctx.Log.Error(title + " lỗi: " + ex.Message);
                    Post(() => _lblStatus.Text = "Lỗi: " + ex.Message);
                }
                finally
                {
                    Post(() => { _busy = false; SetButtons(true); });
                }
            });
        }

        private void SetButtons(bool enabled)
        {
            foreach (var b in new[] { _btnScan, _btnDecodeDir, _btnDecodeFile, _btnEncode, _btnOpenOut })
                if (b != null) b.Enabled = enabled;
            if (_btnCancel != null) _btnCancel.Enabled = !enabled;
        }

        private static int SafeCount(string dir)
        {
            try { return Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories).Length; }
            catch { return 0; }
        }

        private static string SafeDir(string dir)
        {
            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            }
            catch { }
            try { return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); } catch { return "C:\\"; }
        }

        private void Post(Action action)
        {
            if (action == null) return;
            if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
            else action();
        }

        private void OnLogEntry(LogEntry e)
        {
            if (e == null) return;
            if (IsHandleCreated && !Visible) return;   // trang dang an: khong day UI
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
                int max = 60000;
                if (_log.TextLength > max)
                {
                    _log.Select(0, _log.TextLength - max);
                    _log.SelectedText = "";
                }
                _log.ScrollToCaret();
            }
            catch { }
        }

        private Color LevelColor(string level)
        {
            switch ((level ?? "").ToUpperInvariant())
            {
                case "OK": return Theme.Green;
                case "WARN": return Theme.Yellow;
                case "ERR": case "ERROR": return Theme.Red;
                case "STEP": return Theme.Accent;
                default: return Theme.TextDim;
            }
        }

        // ============================================================ DTO report

        [System.Runtime.Serialization.DataContract]
        public class DecodeCliReport
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string GeneratedAt;
            [System.Runtime.Serialization.DataMember(Order = 2)] public string Source;
            [System.Runtime.Serialization.DataMember(Order = 3)] public string Target;
            [System.Runtime.Serialization.DataMember(Order = 4)] public string Mode;
            [System.Runtime.Serialization.DataMember(Order = 5)] public int Total;
            [System.Runtime.Serialization.DataMember(Order = 6)] public int Ok;
            [System.Runtime.Serialization.DataMember(Order = 7)] public int Skipped;
            [System.Runtime.Serialization.DataMember(Order = 8)] public int Failed;
            [System.Runtime.Serialization.DataMember(Order = 9)] public List<DecodeResult> Entries = new List<DecodeResult>();
        }
    }

    /// <summary>Vai ham tien ich nho cho UI.</summary>
    internal static class Strings
    {
        public static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, Math.Max(1, max - 1)) + "…";
        }
    }
}
