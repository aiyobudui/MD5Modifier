using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MD5Modifier
{
    /// <summary>
    /// 视频 MD5 修改工具主窗口（C# WinForms 重写版）。
    /// 功能、配色、列宽、交互逻辑等价原 Qt 项目 MainWindow。
    /// </summary>
    public partial class MainForm : Form
    {
        // ===== 配色（与原 Qt 项目一致）=====
        private static readonly Color C_Hover = Color.FromArgb(0xB0, 0xE0, 0xE6);   // 悬停整行
        private static readonly Color C_HeaderBg = Color.FromArgb(0xE5, 0xE6, 0xEB); // 表头背景
        private static readonly Color C_HeaderHover = Color.FromArgb(0xD5, 0xD6, 0xDA);
        private static readonly Color C_Gray = Color.FromArgb(0x90, 0x93, 0x99);     // 等待/灰文字
        private static readonly Color C_Success = Color.FromArgb(0x67, 0xC2, 0x3A);  // 成功
        private static readonly Color C_Fail = Color.FromArgb(0xF5, 0x6C, 0x6C);     // 失败
        private static readonly Color C_Browse = Color.FromArgb(0x40, 0x9E, 0xFF);   // 浏览按钮
        private static readonly Color C_Start = Color.FromArgb(0x67, 0xC2, 0x3A);    // 开始（绿）
        private static readonly Color C_Pause = Color.FromArgb(0xE6, 0xA2, 0x3C);    // 暂停/继续（橙）
        private static readonly Color C_Border = Color.FromArgb(0xE5, 0xE6, 0xEB);   // 边框
        private static readonly Color C_Bg = Color.FromArgb(0xF5, 0xF7, 0xFA);       // 输入框背景
        private static readonly Color C_Text = Color.FromArgb(0x30, 0x31, 0x33);     // 主文字
        private static readonly Color C_SubText = Color.FromArgb(0x60, 0x62, 0x66);  // 次要文字

        // ===== 数据模型 =====
        private class FileItem
        {
            public bool Selected = true;
            public string FileName = string.Empty;
            public string Suffix = string.Empty;
            public long Size = 0;
            public string OriginalMd5 = "计算中...";
            public string NewMd5 = string.Empty;
            public string Status = "等待处理";
            public string FullPath = string.Empty;
        }

        private readonly List<FileItem> fileItems = new List<FileItem>();

        // ===== 控件 =====
        private TextBox pathEdit;
        private Button browseButton;
        private Button startButton;
        private Button aboutButton;
        private CheckBox selectAllCheck;
        private CheckBox createBackupCheck;
        private DataGridView grid;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label md5ProgressLabel;
        private Label fileCountLabel;

        // ===== 运行状态 =====
        private int processedCount, totalCount, successCount, failCount, md5CalculatedCount;
        private bool paused = false;
        private bool processing = false;
        private bool adjusting = false;   // 程序批量设置单元格时，抑制 CellValueChanged 联动
        private CancellationTokenSource md5Cts;
        private CancellationTokenSource fileCts;
        private readonly ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);
        private Task md5Task;
        private Task fileTask;
        private int hoverRow = -1;
        private int md5Epoch = 0;       // 每次重新加载路径自增，丢弃过期的 MD5 回调
        private bool isClosing = false;  // 窗体正在关闭，抑制完成弹窗

        private const string ColSelect = "选择";
        private const string ColName = "文件名称";
        private const string ColSuffix = "后缀";
        private const string ColSize = "大小";
        private const string ColOrig = "原始MD5";
        private const string ColNew = "新MD5";
        private const string ColStatus = "状态";

        public MainForm()
        {
            InitializeComponent();
            Load += (s, e) => UpdateStartButton();
            FormClosing += (s, e) =>
            {
                isClosing = true;
                try { fileCts?.Cancel(); } catch { }
                try { md5Cts?.Cancel(); } catch { }
                try { fileTask?.Wait(2000); } catch { }
                try { md5Task?.Wait(500); } catch { }
            };
        }

        /// <summary>
        /// 跨线程安全 Invoke：窗体已销毁或被取消时静默跳过，避免后台任务回调抛异常。
        /// </summary>
        private void SafeInvoke(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) Invoke(a); }
            catch { }
        }

        // =========================================================
        // UI 初始化
        // =========================================================
        private void InitializeComponent()
        {
            Font = new Font("Microsoft YaHei", 9.5F);
            Text = "视频 MD5 修改工具";
            MinimumSize = new Size(820, 440);
            ClientSize = new Size(1180, 640);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;

            // ---- 标题栏 ----
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 0, 14, 0)
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // 标题
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // 副标题
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 弹性间隔
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // 关于按钮

            var titleLabel = new Label
            {
                Text = "视频 MD5 修改工具",
                Font = new Font("Microsoft YaHei", 14F, FontStyle.Bold),
                ForeColor = C_Text,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var subLabel = new Label
            {
                Text = "批量修改视频文件的 MD5 值",
                Font = new Font("Microsoft YaHei", 9F),
                ForeColor = C_SubText,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(14, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            aboutButton = new Button
            {
                Text = "关于",
                Width = 64,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = C_Text,
                Anchor = AnchorStyles.None,
                Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold)
            };
            aboutButton.FlatAppearance.BorderColor = C_Border;
            aboutButton.FlatAppearance.BorderSize = 1;
            aboutButton.Click += OnAboutClicked;

            headerLayout.Controls.Add(titleLabel, 0, 0);
            headerLayout.Controls.Add(subLabel, 1, 0);
            headerLayout.Controls.Add(aboutButton, 3, 0);

            var headerLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = C_Border };
            header.Controls.Add(headerLayout);
            header.Controls.Add(headerLine);

            ApplyRoundRegion(aboutButton, 8);
            aboutButton.Resize += (s, e) => ApplyRoundRegion(aboutButton, 8);

            // ---- 顶部工具栏：路径框 + 浏览 + 开始 ----
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(14, 10, 14, 10), BackColor = Color.White };
            var topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));

            pathEdit = new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei", 9.5F),
                Text = "拖拽文件 / 文件夹到此处，或点击「浏览」选择"
            };
            pathEdit.GotFocus += (s, e) => pathEdit.Select(0, 0);

            browseButton = new Button
            {
                Text = "浏览",
                Width = 80,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_Browse,
                ForeColor = Color.White,
                Anchor = AnchorStyles.None,
                Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold)
            };
            browseButton.FlatAppearance.BorderSize = 0;
            browseButton.MouseEnter += (s, e) => browseButton.BackColor = Color.FromArgb(0x66, 0xB1, 0xFF);
            browseButton.MouseLeave += (s, e) => browseButton.BackColor = C_Browse;
            browseButton.Click += OnBrowseClicked;

            startButton = new Button
            {
                Text = "开始修改",
                Width = 108,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_Start,
                ForeColor = Color.White,
                Anchor = AnchorStyles.None,
                Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold)
            };
            startButton.FlatAppearance.BorderSize = 0;
            startButton.Click += OnStartClicked;

            topLayout.Controls.Add(pathEdit, 0, 0);
            topLayout.Controls.Add(browseButton, 1, 0);
            topLayout.Controls.Add(startButton, 2, 0);
            topPanel.Controls.Add(topLayout);

            // 圆角胶囊按钮
            ApplyRoundRegion(browseButton, 8);
            ApplyRoundRegion(startButton, 8);
            browseButton.Resize += (s, e) => ApplyRoundRegion(browseButton, 8);
            startButton.Resize += (s, e) => ApplyRoundRegion(startButton, 8);

            // ---- 选项行：全选 + 创建备份（左） | MD5进度 + 文件数（右）----
            var optionPanel = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.White };
            var optLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            optLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            optLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

            var leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(16, 7, 0, 0),
                BackColor = Color.Transparent
            };
            selectAllCheck = new CheckBox { Text = "全选", AutoSize = true, Checked = true };
            selectAllCheck.CheckedChanged += OnSelectAllClicked;
            createBackupCheck = new CheckBox { Text = "创建备份", AutoSize = true, Margin = new Padding(22, 3, 3, 3) };
            leftFlow.Controls.Add(selectAllCheck);
            leftFlow.Controls.Add(createBackupCheck);

            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 7, 12, 0),
                BackColor = Color.Transparent
            };
            fileCountLabel = new Label { Text = "文件数: 0", AutoSize = true, ForeColor = C_SubText };
            fileCountLabel.TextAlign = ContentAlignment.MiddleRight;
            md5ProgressLabel = new Label { Text = "", AutoSize = true, ForeColor = C_Browse };
            md5ProgressLabel.TextAlign = ContentAlignment.MiddleRight;
            rightFlow.Controls.Add(fileCountLabel);
            rightFlow.Controls.Add(md5ProgressLabel);

            optLayout.Controls.Add(leftFlow, 0, 0);
            optLayout.Controls.Add(rightFlow, 1, 0);
            optionPanel.Controls.Add(optLayout);

            // ---- 表格 ----
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                AllowUserToResizeColumns = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = C_Border,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = false,
                Font = new Font("Microsoft YaHei", 9.5F),
                RowTemplate = { Height = 36 }
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = C_HeaderBg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = C_Text;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersHeight = 36;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.SelectionBackColor = Color.White;
            grid.DefaultCellStyle.SelectionForeColor = C_Text;
            grid.ScrollBars = ScrollBars.Both;

            var colSelect = new DataGridViewCheckBoxColumn
            {
                Name = ColSelect,
                HeaderText = ColSelect,
                Width = 46,
                ReadOnly = false,
                FlatStyle = FlatStyle.Standard
            };
            colSelect.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colSelect.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var colName = new DataGridViewTextBoxColumn { Name = ColName, HeaderText = ColName, Width = 360, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
            var colSuffix = new DataGridViewTextBoxColumn { Name = ColSuffix, HeaderText = ColSuffix, Width = 70, ReadOnly = true };
            var colSize = new DataGridViewTextBoxColumn { Name = ColSize, HeaderText = ColSize, Width = 90, ReadOnly = true };
            var colOrig = new DataGridViewTextBoxColumn { Name = ColOrig, HeaderText = ColOrig, Width = 250, ReadOnly = true };
            var colNew = new DataGridViewTextBoxColumn { Name = ColNew, HeaderText = ColNew, Width = 250, ReadOnly = true };
            var colStatus = new DataGridViewTextBoxColumn { Name = ColStatus, HeaderText = ColStatus, Width = 80, ReadOnly = true };
            foreach (DataGridViewColumn c in new DataGridViewColumn[] { colSelect, colName, colSuffix, colSize, colOrig, colNew, colStatus })
            {
                c.SortMode = DataGridViewColumnSortMode.NotSortable;
                grid.Columns.Add(c);
            }

            grid.RowPrePaint += Grid_RowPrePaint;
            grid.CellMouseEnter += (s, e) => { if (e.RowIndex >= 0) { hoverRow = e.RowIndex; grid.InvalidateRow(e.RowIndex); } };
            grid.CellMouseLeave += (s, e) => { if (e.RowIndex >= 0) { int old = hoverRow; hoverRow = -1; if (old >= 0) grid.InvalidateRow(old); } };
            // 等价 Qt NoSelection：选中即清除，且不显示选中高亮（SelectionBackColor 已设为白色）
            grid.SelectionChanged += (s, e) => { if (grid.SelectedRows.Count > 0 || grid.SelectedCells.Count > 0) grid.ClearSelection(); };
            // 复选框列值变化后同步数据模型与“全选”状态
            grid.CellValueChanged += (s, e) =>
            {
                if (adjusting) return;
                if (e.ColumnIndex == 0 && e.RowIndex >= 0 && e.RowIndex < fileItems.Count)
                {
                    var v = grid.Rows[e.RowIndex].Cells[0].Value;
                    fileItems[e.RowIndex].Selected = v is bool b && b;
                    SyncSelectAllState();
                }
            };

            // ---- 底部：进度条 + 状态 ----
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(14, 8, 14, 8), BackColor = Color.White };
            var bottomLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = C_Border };
            var bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            progressBar = new ProgressBar
            {
                Visible = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 12, 1),
                ForeColor = C_Success
            };
            statusLabel = new Label
            {
                Text = "就绪",
                Dock = DockStyle.Right,
                AutoSize = true,
                ForeColor = C_SubText,
                TextAlign = ContentAlignment.MiddleRight
            };
            bottomLayout.Controls.Add(progressBar, 0, 0);
            bottomLayout.Controls.Add(statusLabel, 1, 0);
            bottomPanel.Controls.Add(bottomLine);
            bottomPanel.Controls.Add(bottomLayout);

            Controls.Add(grid);
            Controls.Add(bottomPanel);
            Controls.Add(optionPanel);
            Controls.Add(topPanel);
            Controls.Add(header);

            // 拖拽支持
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
        }

        /// <summary>
        /// 将按钮裁剪成圆角胶囊形状（约 radius 软圆角）。
        /// </summary>
        private static void ApplyRoundRegion(Button btn, int radius)
        {
            if (btn.Width < 1 || btn.Height < 1) return;
            var gp = new GraphicsPath();
            int w = btn.Width, h = btn.Height;
            int r = Math.Min(radius, Math.Min(w, h) / 2);
            gp.AddArc(0, 0, r, r, 180, 90);
            gp.AddArc(w - r, 0, r, r, 270, 90);
            gp.AddArc(w - r, h - r, r, r, 0, 90);
            gp.AddArc(0, h - r, r, r, 90, 90);
            gp.CloseFigure();
            btn.Region = new Region(gp);
            gp.Dispose();
        }

        // =========================================================
        // 表格绘制：仅在 RowPrePaint 统一绘制悬停整行高亮。
        // 不再拦截单元格绘制，避免与单元格编辑冲突、复选框渲染更稳定。
        // =========================================================
        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.RowIndex == hoverRow)
            {
                using (var brush = new SolidBrush(C_Hover))
                    e.Graphics.FillRectangle(brush, e.RowBounds);
            }
            // 背景已由此处或网格默认背景绘制，屏蔽默认背景重绘以免覆盖悬停色
            e.PaintParts &= ~DataGridViewPaintParts.Background;
        }

        // =========================================================
        // 拖拽
        // =========================================================
        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths != null && paths.Length > 0)
                LoadPaths(paths);
        }

        // =========================================================
        // 浏览
        // =========================================================
        private void OnBrowseClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择文件夹";
                dialog.UseDescriptionForTitle = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadPaths(new[] { dialog.SelectedPath });
                }
            }
        }

        // =========================================================
        // 关于
        // =========================================================
        private void OnAboutClicked(object sender, EventArgs e)
        {
            using (var dlg = new AboutForm())
            {
                dlg.Icon = this.Icon;
                dlg.ShowDialog(this);
            }
        }

        // =========================================================
        // 开始 / 暂停 / 继续（单按钮循环切换）
        // =========================================================
        private void OnStartClicked(object sender, EventArgs e)
        {
            if (!processing)
            {
                var selected = new List<FileItem>();
                foreach (var item in fileItems)
                    if (item.Selected) selected.Add(item);

                if (selected.Count == 0)
                {
                    MessageBox.Show(this, "请先选择要修改的文件。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = MessageBox.Show(this,
                    string.Format("确定要修改选中的 {0} 个文件吗？", selected.Count),
                    "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;

                successCount = 0;
                failCount = 0;
                paused = false;
                StartModify(selected);
            }
            else
            {
                paused = !paused;
                if (paused)
                {
                    pauseEvent.Reset();
                    statusLabel.Text = "已暂停";
                }
                else
                {
                    pauseEvent.Set();
                    statusLabel.Text = "继续处理...";
                }
                UpdateStartButton();
            }
        }

        private void UpdateStartButton()
        {
            if (paused)
            {
                startButton.Text = "继续修改";
                startButton.BackColor = C_Pause;
                startButton.MouseEnter -= StartHover;
                startButton.MouseLeave -= StartLeave;
                startButton.MouseEnter += StartHover;
                startButton.MouseLeave += StartLeave;
            }
            else if (processing)
            {
                startButton.Text = "暂停修改";
                startButton.BackColor = C_Pause;
                startButton.MouseEnter -= StartHover;
                startButton.MouseLeave -= StartLeave;
                startButton.MouseEnter += StartHover;
                startButton.MouseLeave += StartLeave;
            }
            else
            {
                startButton.Text = "开始修改";
                startButton.BackColor = C_Start;
                startButton.MouseEnter -= StartHover;
                startButton.MouseLeave -= StartLeave;
                startButton.MouseEnter += StartHover;
                startButton.MouseLeave += StartLeave;
            }
        }

        private void StartHover(object s, EventArgs e)
        {
            startButton.BackColor = processing || paused
                ? Color.FromArgb(0xF3, 0xB9, 0x5F)
                : Color.FromArgb(0x85, 0xCE, 0x61);
        }

        private void StartLeave(object s, EventArgs e)
        {
            startButton.BackColor = processing || paused ? C_Pause : C_Start;
        }

        private void OnSelectAllClicked(object sender, EventArgs e)
        {
            bool check = selectAllCheck.Checked;
            adjusting = true;
            for (int i = 0; i < fileItems.Count; i++)
            {
                fileItems[i].Selected = check;
                if (i < grid.Rows.Count)
                    grid.Rows[i].Cells[0].Value = check;
            }
            adjusting = false;
        }

        private void SyncSelectAllState()
        {
            bool all = fileItems.Count > 0;
            foreach (var item in fileItems)
                if (!item.Selected) { all = false; break; }
            selectAllCheck.CheckedChanged -= OnSelectAllClicked;
            selectAllCheck.Checked = all;
            selectAllCheck.CheckedChanged += OnSelectAllClicked;
        }

        // =========================================================
        // 加载路径（文件/文件夹），过滤非视频，启动 MD5 计算
        // =========================================================
        private void LoadPaths(string[] paths)
        {
            // 取消上一次 MD5 计算
            try { md5Cts?.Cancel(); } catch { }
            try { md5Task?.Wait(500); } catch { }

            fileItems.Clear();
            grid.Rows.Clear();
            md5CalculatedCount = 0;
            paused = false;
            UpdateStartButton();

            var folderPaths = new List<string>();
            var filePaths = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p)) folderPaths.Add(p);
                else if (File.Exists(p)) filePaths.Add(p);
            }

            foreach (var folder in folderPaths)
            {
                try
                {
                    var infos = new DirectoryInfo(folder).GetFiles();
                    Array.Sort(infos, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                    foreach (var info in infos) AddFileItem(info.FullName);
                }
                catch { }
            }
            foreach (var file in filePaths) AddFileItem(file);

            // 路径栏显示
            if (folderPaths.Count == 1 && filePaths.Count == 0)
                pathEdit.Text = folderPaths[0];
            else if (folderPaths.Count == 0 && filePaths.Count == 1)
                pathEdit.Text = filePaths[0];
            else if (folderPaths.Count > 0 || filePaths.Count > 0)
                pathEdit.Text = string.Format("已选择 {0} 个文件夹和 {1} 个文件", folderPaths.Count, filePaths.Count);

            // 填充表格（批量赋值期间抑制 CellValueChanged 联动）
            adjusting = true;
            for (int i = 0; i < fileItems.Count; i++)
                AddGridRow(fileItems[i], i);
            adjusting = false;

            fileCountLabel.Text = "文件数: " + fileItems.Count;
            SyncSelectAllState();

            // 启动 MD5 多线程计算
            if (fileItems.Count > 0)
            {
                md5ProgressLabel.Text = string.Format("正在计算MD5: 0/{0}", fileItems.Count);
                md5Epoch++;
                int epoch = md5Epoch;
                md5Cts = new CancellationTokenSource();
                var token = md5Cts.Token;
                md5Task = Task.Run(() =>
                {
                    for (int i = 0; i < fileItems.Count; i++)
                    {
                        if (token.IsCancellationRequested) break;
                        string md5 = FileMd5Strategy.CalculateMd5(fileItems[i].FullPath);
                        int idx = i;
                        SafeInvoke(() => OnMd5Calculated(idx, md5, epoch));
                    }
                    SafeInvoke(() => OnMd5CalculationFinished(epoch));
                }, token);
            }
        }

        private void AddFileItem(string fullPath)
        {
            string suffix = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant();
            var video = new[]
            {
                "mp4","mkv","avi","mov","wmv","flv",
                "mpg","mpeg","webm","m4v","ts","m2ts"
            };
            if (Array.IndexOf(video, suffix) < 0) return;

            var item = new FileItem
            {
                Selected = true,
                FileName = Path.GetFileName(fullPath),
                Suffix = suffix,
                Size = new FileInfo(fullPath).Length,
                OriginalMd5 = "计算中...",
                NewMd5 = string.Empty,
                Status = "等待处理",
                FullPath = fullPath
            };
            fileItems.Add(item);
        }

        private void AddGridRow(FileItem item, int row)
        {
            grid.Rows.Add();
            int r = row;
            grid.Rows[r].Cells[0].Value = item.Selected;
            grid.Rows[r].Cells[1].Value = item.FileName;
            grid.Rows[r].Cells[1].ToolTipText = item.FileName;
            grid.Rows[r].Cells[2].Value = item.Suffix;
            grid.Rows[r].Cells[2].Style.ForeColor = C_Gray;
            grid.Rows[r].Cells[3].Value = FormatSize(item.Size);
            grid.Rows[r].Cells[3].Style.ForeColor = C_SubText;
            grid.Rows[r].Cells[4].Value = Truncate(item.OriginalMd5);
            grid.Rows[r].Cells[4].Style.ForeColor = C_Gray;
            grid.Rows[r].Cells[4].ToolTipText = item.OriginalMd5;
            grid.Rows[r].Cells[5].Value = string.Empty;
            grid.Rows[r].Cells[5].Style.ForeColor = C_Gray;
            grid.Rows[r].Cells[6].Value = item.Status;
            grid.Rows[r].Cells[6].Style.ForeColor = C_Gray;
            grid.Rows[r].Height = 36;
        }

        private void OnMd5Calculated(int index, string md5, int epoch)
        {
            if (epoch != md5Epoch) return;
            if (index < 0 || index >= fileItems.Count || index >= grid.Rows.Count) return;
            fileItems[index].OriginalMd5 = md5;
            var cell = grid.Rows[index].Cells[4];
            cell.Value = Truncate(md5);
            cell.ToolTipText = md5;
            cell.Style.ForeColor = C_Text;
            md5CalculatedCount++;
            md5ProgressLabel.Text = string.Format("正在计算MD5: {0}/{1}", md5CalculatedCount, fileItems.Count);
        }

        private void OnMd5CalculationFinished(int epoch)
        {
            if (epoch != md5Epoch) return;
            md5ProgressLabel.Text = "MD5计算完成";
            var t = new System.Windows.Forms.Timer { Interval = 2000 };
            t.Tick += (s, e) => { md5ProgressLabel.Text = string.Empty; t.Stop(); t.Dispose(); };
            t.Start();
        }

        // =========================================================
        // 开始修改（后台线程逐个处理，支持暂停/继续）
        // =========================================================
        private void StartModify(List<FileItem> selected)
        {
            totalCount = selected.Count;
            processedCount = 0;
            successCount = 0;
            failCount = 0;

            progressBar.Visible = true;
            progressBar.Minimum = 0;
            progressBar.Maximum = totalCount;
            progressBar.Value = 0;
            statusLabel.Text = string.Format("正在处理: 0/{0} | 成功: 0 | 失败: 0", totalCount);

            browseButton.Enabled = false;
            processing = true;
            paused = false;
            pauseEvent.Set();
            AllowDrop = false;
            UpdateStartButton();

            fileCts = new CancellationTokenSource();
            var token = fileCts.Token;
            var workList = selected;
            bool backup = createBackupCheck.Checked; // 在 UI 线程读取，避免在后台线程跨线程访问控件

            fileTask = Task.Run(() =>
            {
                foreach (var item in workList)
                {
                    pauseEvent.Wait(token);
                    if (token.IsCancellationRequested) break;

                    bool ok = FileMd5Strategy.ModifyFile(
                        item.FullPath, backup,
                        out string newMd5, out _, out string error);

                    SafeInvoke(() =>
                    {
                        int idx = fileItems.IndexOf(item);
                        if (idx < 0) return;
                        fileItems[idx].Status = ok ? "成功" : "失败";
                        fileItems[idx].NewMd5 = ok ? newMd5 : string.Empty;
                        UpdateGridRow(idx, ok, newMd5);
                        if (ok) successCount++; else failCount++;
                        processedCount++;
                        progressBar.Value = Math.Min(processedCount, totalCount);
                        statusLabel.Text = string.Format(
                            "正在处理: {0}/{1} | 成功: {2} | 失败: {3}",
                            processedCount, totalCount, successCount, failCount);
                    });
                }
                SafeInvoke(OnProcessingFinished);
            }, token);
        }

        private void UpdateGridRow(int idx, bool ok, string newMd5)
        {
            if (idx < 0 || idx >= grid.Rows.Count) return;
            var newCell = grid.Rows[idx].Cells[5];
            newCell.Value = Truncate(newMd5);
            newCell.Style.ForeColor = C_Success;
            newCell.ToolTipText = newMd5;

            var statusCell = grid.Rows[idx].Cells[6];
            statusCell.Value = ok ? "成功" : "失败";
            statusCell.Style.ForeColor = ok ? C_Success : C_Fail;
            statusCell.Style.Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold);
        }

        private void OnProcessingFinished()
        {
            progressBar.Visible = false;
            browseButton.Enabled = true;
            processing = false;
            paused = false;
            AllowDrop = true;
            UpdateStartButton();

            statusLabel.Text = string.Format(
                "处理完成 | 成功: {0} | 失败: {1} | 共 {2} 个文件",
                successCount, failCount, totalCount);

            if (!isClosing)
            {
                MessageBox.Show(this,
                    string.Format("处理完成\n成功: {0} 个文件\n失败: {1} 个文件", successCount, failCount),
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // =========================================================
        // 工具方法
        // =========================================================
        private static string Truncate(string md5)
        {
            if (string.IsNullOrEmpty(md5)) return md5;
            return md5.Length > 24 ? md5.Substring(0, 24) : md5;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.00") + " GB";
            if (bytes >= 1 << 20) return (bytes / (double)(1 << 20)).ToString("0.00") + " MB";
            if (bytes >= 1 << 10) return (bytes / (double)(1 << 10)).ToString("0.00") + " KB";
            return bytes + " B";
        }
    }
}
