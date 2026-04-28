using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace DoododAutoInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class PackageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string status;

        public string FileName { get; set; }
        public string Type { get; set; }
        public string PackageName { get; set; }
        public long? ApkVersionCode { get; set; }
        public string ApkVersionName { get; set; }
        public long? InstalledVersionCode { get; set; }
        public string Action { get; set; }
        public string FullPath { get; set; }

        public string Status
        {
            get { return status; }
            set
            {
                if (status == value) return;
                status = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Status"));
            }
        }
    }

    public sealed class MainForm : Form
    {
        private readonly TranslucentPathBox folderTextBox = new TranslucentPathBox();
        private readonly Button browseButton = new Button();
        private readonly Button browseFolderButton = new Button();
        private readonly Button deviceButton = new Button();
        private readonly Button scanButton = new Button();
        private readonly Button startButton = new Button();
        private readonly Button createFolderButton = new Button();
        private readonly LightweightPackageGrid grid = new LightweightPackageGrid();
        private readonly TranslucentLogView logBox = new TranslucentLogView();
        private readonly Label deviceLabel = new Label();
        private readonly Label summaryLabel = new Label();
        private readonly TranslucentPathBox remotePathBox = new TranslucentPathBox();
        private readonly CheckBox apkSmartInstallCheckBox = new CheckBox();
        private readonly BindingList<PackageItem> items = new BindingList<PackageItem>();

        private const float BackgroundImageOpacity = 0.70f;
        private Image backgroundImage;
        private Bitmap backgroundCanvas;
        private string adbPath;
        private bool busy;
        private bool liveResizing;
        private bool backgroundCanvasDirty = true;
        private bool updatingSourceText;
        private readonly List<string> selectedFilePaths = new List<string>();

        public Bitmap BackgroundCanvas
        {
            get
            {
                EnsureBackgroundCanvas();
                return backgroundCanvas;
            }
        }

        public bool IsLiveResizing
        {
            get { return liveResizing; }
        }

        public MainForm()
        {
            Text = "APK 自动安装工具";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 660);
            Size = new Size(1120, 760);
            Font = new Font("Microsoft YaHei UI", 9F);
            DoubleBuffered = true;

            adbPath = Adb.FindAdb();
            LoadAppIcon();
            LoadBackgroundImage();

            BuildLayout();
            folderTextBox.Text = AppSettings.LoadLastFolder();
            remotePathBox.Text = AppSettings.LoadRemoteFolder();
            grid.Items = items;
            Log("工具已启动。");
            Log(adbPath == null ? "未找到 adb.exe，请先安装 platform-tools 或把 adb.exe 放入 PATH。" : "ADB: " + adbPath);
            UpdateDeviceStatus(false);
        }

        private void LoadAppIcon()
        {
            string iconPath = Path.Combine(AppPaths.AppDirectory, "app_icon.ico");
            if (!File.Exists(iconPath)) return;

            try
            {
                Icon = new Icon(iconPath);
            }
            catch
            {
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (liveResizing)
            {
                if (backgroundCanvas != null)
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                    e.Graphics.DrawImage(backgroundCanvas, ClientRectangle);
                    return;
                }

                using (var brush = new SolidBrush(Color.FromArgb(248, 244, 247)))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
                return;
            }

            if (backgroundCanvas != null)
            {
                if (backgroundCanvas.Width == ClientSize.Width && backgroundCanvas.Height == ClientSize.Height)
                {
                    e.Graphics.DrawImageUnscaled(backgroundCanvas, Point.Empty);
                }
                else
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                    e.Graphics.DrawImage(backgroundCanvas, ClientRectangle);
                }
                return;
            }

            EnsureBackgroundCanvas();
            if (backgroundCanvas != null)
            {
                e.Graphics.DrawImageUnscaled(backgroundCanvas, Point.Empty);
                return;
            }

            using (var brush = new SolidBrush(Color.FromArgb(248, 244, 247)))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }

        private void LoadBackgroundImage()
        {
            string imagePath = Path.Combine(AppPaths.AppDirectory, "background.png");
            if (!File.Exists(imagePath)) return;

            using (var source = Image.FromFile(imagePath))
            {
                backgroundImage = new Bitmap(source);
            }
        }

        private void EnsureBackgroundCanvas()
        {
            if (backgroundImage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            if (backgroundCanvas != null &&
                backgroundCanvas.Width == ClientSize.Width &&
                backgroundCanvas.Height == ClientSize.Height &&
                !backgroundCanvasDirty)
            {
                return;
            }

            if (liveResizing && backgroundCanvas != null)
            {
                return;
            }

            if (backgroundCanvas != null)
            {
                backgroundCanvas.Dispose();
                backgroundCanvas = null;
            }

            backgroundCanvas = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (Graphics graphics = Graphics.FromImage(backgroundCanvas))
            {
                using (var brush = new SolidBrush(Color.FromArgb(248, 244, 247)))
                {
                    graphics.FillRectangle(brush, new Rectangle(Point.Empty, ClientSize));
                }

                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                Rectangle destination = ThemePainter.GetCoverRectangle(backgroundImage.Size, ClientSize);
                using (var attributes = new ImageAttributes())
                {
                    var matrix = new ColorMatrix();
                    matrix.Matrix33 = BackgroundImageOpacity;
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    graphics.DrawImage(
                        backgroundImage,
                        destination,
                        0,
                        0,
                        backgroundImage.Width,
                        backgroundImage.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }
            }

            backgroundCanvasDirty = false;
        }

        protected override void OnResize(EventArgs e)
        {
            backgroundCanvasDirty = true;

            if (!liveResizing && backgroundCanvas != null)
            {
                backgroundCanvas.Dispose();
                backgroundCanvas = null;
            }

            base.OnResize(e);
            Invalidate(false);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_ENTERSIZEMOVE = 0x0231;
            const int WM_EXITSIZEMOVE = 0x0232;

            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                liveResizing = true;
                Invalidate(false);
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                liveResizing = false;
                backgroundCanvasDirty = true;
                if (backgroundCanvas != null)
                {
                    backgroundCanvas.Dispose();
                    backgroundCanvas = null;
                }
                Invalidate(false);
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (backgroundCanvas != null)
                {
                    backgroundCanvas.Dispose();
                    backgroundCanvas = null;
                }

                if (backgroundImage != null)
                {
                    backgroundImage.Dispose();
                    backgroundImage = null;
                }
            }

            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.Transparent;
            root.RowCount = 5;
            root.ColumnCount = 1;
            root.Padding = new Padding(10);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            Controls.Add(root);

            var top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.BackColor = Color.Transparent;
            top.ColumnCount = 6;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            root.Controls.Add(top, 0, 0);

            folderTextBox.Dock = DockStyle.Fill;
            folderTextBox.HostForm = this;
            folderTextBox.ThemeImage = backgroundImage;
            folderTextBox.Margin = new Padding(0, 5, 8, 5);
            folderTextBox.TextChanged += SourceTextChanged;
            top.Controls.Add(folderTextBox, 0, 0);

            browseButton.Text = "选择文件";
            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(0, 4, 8, 4);
            browseButton.Click += BrowseButtonClick;
            top.Controls.Add(browseButton, 1, 0);

            browseFolderButton.Text = "选择文件夹";
            browseFolderButton.Dock = DockStyle.Fill;
            browseFolderButton.Margin = new Padding(0, 4, 8, 4);
            browseFolderButton.Click += BrowseFolderButtonClick;
            top.Controls.Add(browseFolderButton, 2, 0);

            deviceButton.Text = "检测手机";
            deviceButton.Dock = DockStyle.Fill;
            deviceButton.Margin = new Padding(0, 4, 8, 4);
            deviceButton.Click += DeviceButtonClick;
            top.Controls.Add(deviceButton, 3, 0);

            scanButton.Text = "扫描";
            scanButton.Dock = DockStyle.Fill;
            scanButton.Margin = new Padding(0, 4, 8, 4);
            scanButton.Click += ScanButtonClick;
            top.Controls.Add(scanButton, 4, 0);

            startButton.Text = "开始执行";
            startButton.Dock = DockStyle.Fill;
            startButton.Margin = new Padding(0, 4, 0, 4);
            startButton.Click += StartButtonClick;
            top.Controls.Add(startButton, 5, 0);

            var remote = new TableLayoutPanel();
            remote.Dock = DockStyle.Fill;
            remote.BackColor = Color.Transparent;
            remote.ColumnCount = 3;
            remote.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            remote.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            remote.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
            root.Controls.Add(remote, 0, 1);

            remotePathBox.Dock = DockStyle.Fill;
            remotePathBox.HostForm = this;
            remotePathBox.ThemeImage = backgroundImage;
            remotePathBox.Margin = new Padding(0, 5, 8, 5);
            remote.Controls.Add(remotePathBox, 0, 0);

            createFolderButton.Text = "选择手机目录";
            createFolderButton.Dock = DockStyle.Fill;
            createFolderButton.Margin = new Padding(0, 4, 8, 4);
            createFolderButton.Click += CreateFolderButtonClick;
            remote.Controls.Add(createFolderButton, 1, 0);

            apkSmartInstallCheckBox.Text = "APK智能安装";
            apkSmartInstallCheckBox.Checked = true;
            apkSmartInstallCheckBox.BackColor = Color.Transparent;
            apkSmartInstallCheckBox.ForeColor = Color.FromArgb(35, 35, 35);
            apkSmartInstallCheckBox.Dock = DockStyle.Fill;
            apkSmartInstallCheckBox.Margin = new Padding(0, 6, 0, 4);
            apkSmartInstallCheckBox.CheckedChanged += delegate { if (!busy) ScanButtonClick(null, EventArgs.Empty); };
            remote.Controls.Add(apkSmartInstallCheckBox, 2, 0);

            var status = new TableLayoutPanel();
            status.Dock = DockStyle.Fill;
            status.BackColor = Color.Transparent;
            status.ColumnCount = 2;
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            root.Controls.Add(status, 0, 2);

            deviceLabel.Dock = DockStyle.Fill;
            deviceLabel.BackColor = Color.Transparent;
            deviceLabel.TextAlign = ContentAlignment.MiddleLeft;
            status.Controls.Add(deviceLabel, 0, 0);

            summaryLabel.Dock = DockStyle.Fill;
            summaryLabel.BackColor = Color.Transparent;
            summaryLabel.TextAlign = ContentAlignment.MiddleRight;
            summaryLabel.ForeColor = Color.FromArgb(70, 70, 70);
            status.Controls.Add(summaryLabel, 1, 0);

            grid.Dock = DockStyle.Fill;
            grid.HostForm = this;
            grid.ThemeImage = backgroundImage;
            root.Controls.Add(grid, 0, 3);

            logBox.Dock = DockStyle.Fill;
            logBox.HostForm = this;
            logBox.ThemeImage = backgroundImage;
            logBox.ForeColor = Color.FromArgb(230, 235, 241);
            logBox.Font = new Font("Consolas", 9F);
            root.Controls.Add(logBox, 0, 4);
        }

        private void BrowseButtonClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择一个或多个文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                dialog.Multiselect = true;
                dialog.InitialDirectory = GetInitialSourceDirectory();

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedFilePaths.Clear();
                    selectedFilePaths.AddRange(dialog.FileNames);

                    string firstDirectory = Path.GetDirectoryName(dialog.FileNames[0]);
                    if (!string.IsNullOrEmpty(firstDirectory))
                    {
                        AppSettings.SaveLastFolder(firstDirectory);
                    }

                    updatingSourceText = true;
                    try
                    {
                        folderTextBox.Text = dialog.FileNames.Length == 1
                            ? dialog.FileNames[0]
                            : "已选择 " + dialog.FileNames.Length + " 个文件";
                    }
                    finally
                    {
                        updatingSourceText = false;
                    }
                }
            }
        }

        private void BrowseFolderButtonClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含待传输文件的文件夹";
                dialog.SelectedPath = GetInitialSourceDirectory();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedFilePaths.Clear();
                    updatingSourceText = true;
                    try
                    {
                        folderTextBox.Text = dialog.SelectedPath;
                    }
                    finally
                    {
                        updatingSourceText = false;
                    }
                    AppSettings.SaveLastFolder(dialog.SelectedPath);
                }
            }
        }

        private void SourceTextChanged(object sender, EventArgs e)
        {
            if (!updatingSourceText)
            {
                selectedFilePaths.Clear();
            }
        }

        private string GetInitialSourceDirectory()
        {
            string text = folderTextBox.Text.Trim();
            if (Directory.Exists(text)) return text;
            if (File.Exists(text)) return Path.GetDirectoryName(text);

            string saved = AppSettings.LoadLastFolder();
            if (Directory.Exists(saved)) return saved;

            return AppPaths.AppDirectory;
        }

        private void DeviceButtonClick(object sender, EventArgs e)
        {
            RunWorker("检测手机", delegate
            {
                CheckDeviceOrThrow();
            });
        }

        private void ScanButtonClick(object sender, EventArgs e)
        {
            RunWorker("扫描文件", delegate
            {
                ScanFolder();
            });
        }

        private void StartButtonClick(object sender, EventArgs e)
        {
            RunWorker("执行安装和复制", delegate
            {
                if (items.Count == 0) ScanFolder();
                ExecuteItems();
            });
        }

        private void CreateFolderButtonClick(object sender, EventArgs e)
        {
            if (busy) return;

            try
            {
                CheckDeviceOrThrow();

                using (var dialog = new RemoteFolderDialog(adbPath, GetRemoteFolder()))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        remotePathBox.Text = dialog.SelectedPath;
                        AppSettings.SaveRemoteFolder(dialog.SelectedPath);
                        Log("[OK] 已选择手机目录: " + dialog.SelectedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("[错误] " + ex.Message);
                MessageBox.Show(this, ex.Message, "选择手机目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RunWorker(string title, Action action)
        {
            if (busy) return;
            busy = true;
            SetButtonsEnabled(false);
            Log("==== " + title + " ====");

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log("[错误] " + ex.Message);
                }
                finally
                {
                    BeginInvoke(new Action(delegate
                    {
                        busy = false;
                        SetButtonsEnabled(true);
                    }));
                }
            });
        }

        private void SetButtonsEnabled(bool enabled)
        {
            browseButton.Enabled = enabled;
            browseFolderButton.Enabled = enabled;
            deviceButton.Enabled = enabled;
            scanButton.Enabled = enabled;
            startButton.Enabled = enabled;
            createFolderButton.Enabled = enabled;
            folderTextBox.Enabled = enabled;
            remotePathBox.Enabled = enabled;
            apkSmartInstallCheckBox.Enabled = enabled;
        }

        private void CheckDeviceOrThrow()
        {
            if (adbPath == null)
            {
                adbPath = Adb.FindAdb();
            }

            if (adbPath == null)
            {
                UpdateDeviceStatus(false);
                throw new InvalidOperationException("未找到 adb.exe。");
            }

            var result = Adb.Run(adbPath, "devices", 15000);
            if (result.ExitCode != 0)
            {
                UpdateDeviceStatus(false);
                throw new InvalidOperationException(result.AllOutput.Trim());
            }

            var devices = Adb.ParseConnectedDevices(result.AllOutput);
            if (devices.Count == 0)
            {
                UpdateDeviceStatus(false);
                throw new InvalidOperationException("未检测到可用设备，请确认 USB 调试已开启并授权。");
            }

            UpdateDeviceStatus(true);
            Log("[OK] 设备已连接: " + string.Join(", ", devices.ToArray()));
        }

        private void ScanFolder()
        {
            CheckDeviceOrThrow();

            string sourceDescription;
            string[] files = GetSelectedSourceFiles(out sourceDescription);
            if (files.Length == 0)
            {
                throw new FileNotFoundException("没有找到可传输的文件。");
            }

            AppSettings.SaveRemoteFolder(GetRemoteFolder());

            var newItems = new List<PackageItem>();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            Log("扫描来源: " + sourceDescription);
            Log("找到文件: " + files.Length);

            foreach (string filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                bool isApk = string.Equals(Path.GetExtension(filePath), ".apk", StringComparison.OrdinalIgnoreCase);

                if (isApk)
                {
                    if (!IsApkSmartInstallEnabled())
                    {
                        newItems.Add(new PackageItem
                        {
                            FileName = fileName,
                            Type = "APK",
                            PackageName = "",
                            Action = "复制",
                            Status = "待执行",
                            FullPath = filePath
                        });
                        continue;
                    }

                    try
                    {
                        var info = ApkManifestReader.Read(filePath);
                        long? installed = Adb.GetInstalledVersionCode(adbPath, info.PackageName);
                        string action;

                        if (!installed.HasValue)
                        {
                            action = "安装";
                        }
                        else if (info.VersionCode > installed.Value)
                        {
                            action = "升级";
                        }
                        else
                        {
                            action = "复制";
                        }

                        newItems.Add(new PackageItem
                        {
                            FileName = fileName,
                            Type = "APK",
                            PackageName = info.PackageName,
                            ApkVersionCode = info.VersionCode,
                            ApkVersionName = info.VersionName,
                            InstalledVersionCode = installed,
                            Action = action,
                            Status = "待执行",
                            FullPath = filePath
                        });
                    }
                    catch (Exception ex)
                    {
                        newItems.Add(new PackageItem
                        {
                            FileName = fileName,
                            Type = "APK",
                            PackageName = "",
                            Action = "复制",
                            Status = "无法读取版本，改为复制",
                            FullPath = filePath
                        });
                        Log("[APK 读取失败] " + fileName + ": " + ex.Message + "，将按普通文件复制");
                    }

                    continue;
                }

                string type = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
                if (string.IsNullOrEmpty(type)) type = "文件";

                newItems.Add(new PackageItem
                {
                    FileName = fileName,
                    Type = type,
                    PackageName = "",
                    Action = "复制",
                    Status = "待执行",
                    FullPath = filePath
                });
            }

            BeginInvoke(new Action(delegate
            {
                items.Clear();
                foreach (var item in newItems) items.Add(item);
                UpdateSummary();
            }));
        }

        private void ExecuteItems()
        {
            CheckDeviceOrThrow();

            foreach (PackageItem item in items)
            {
                if (item.Type == "APK")
                {
                    ExecuteApk(item);
                }
                else
                {
                    ExecuteFilePush(item);
                }
            }

            Log("执行完成。");
            BeginInvoke(new Action(UpdateSummary));
        }

        private void ExecuteApk(PackageItem item)
        {
            if (item.Action == "错误")
            {
                item.Status = "已跳过";
                return;
            }

            if (item.Action == "复制")
            {
                ExecuteFilePush(item);
                return;
            }

            item.Status = "安装中";
            Log("安装: " + item.FileName + " (" + item.Action + ")");
            var result = Adb.Run(adbPath, "install -r -g -t " + Adb.Quote(item.FullPath), 35000);
            Log(result.AllOutput.Trim());

            if (result.ExitCode == 0 && result.AllOutput.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.Status = "安装成功";
            }
            else
            {
                item.Status = "安装失败";
            }
        }

        private void ExecuteFilePush(PackageItem item)
        {
            item.Status = "复制中";
            EnsureRemoteFolder();
            string remote = BuildRemotePath(GetRemoteFolder(), item.FileName);
            Log("复制: " + item.FileName + " -> " + remote);
            var result = Adb.Run(adbPath, "push " + Adb.Quote(item.FullPath) + " " + Adb.Quote(remote), 120000);
            Log(result.AllOutput.Trim());

            item.Status = result.ExitCode == 0 ? "复制成功" : "复制失败";
        }

        private string GetFolder()
        {
            if (InvokeRequired)
            {
                return (string)Invoke(new Func<string>(GetFolder));
            }

            return folderTextBox.Text.Trim();
        }

        private string[] GetSelectedSourceFiles(out string sourceDescription)
        {
            if (InvokeRequired)
            {
                var result = (SourceSelection)Invoke(new Func<SourceSelection>(GetSelectedSourceSelection));
                sourceDescription = result.Description;
                return result.Files;
            }

            SourceSelection selection = GetSelectedSourceSelection();
            sourceDescription = selection.Description;
            return selection.Files;
        }

        private SourceSelection GetSelectedSourceSelection()
        {
            if (selectedFilePaths.Count > 0)
            {
                string[] files = selectedFilePaths.FindAll(File.Exists).ToArray();
                string folder = files.Length > 0 ? Path.GetDirectoryName(files[0]) : "";
                if (!string.IsNullOrEmpty(folder))
                {
                    AppSettings.SaveLastFolder(folder);
                }

                return new SourceSelection
                {
                    Files = files,
                    Description = "已选择文件 " + files.Length + " 个"
                };
            }

            string source = folderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new FileNotFoundException("请先选择文件或文件夹。");
            }

            if (File.Exists(source))
            {
                string folder = Path.GetDirectoryName(source);
                if (!string.IsNullOrEmpty(folder))
                {
                    AppSettings.SaveLastFolder(folder);
                }

                return new SourceSelection
                {
                    Files = new[] { source },
                    Description = source
                };
            }

            if (Directory.Exists(source))
            {
                AppSettings.SaveLastFolder(source);
                return new SourceSelection
                {
                    Files = Directory.GetFiles(source),
                    Description = source
                };
            }

            throw new FileNotFoundException("路径不存在: " + source);
        }

        private sealed class SourceSelection
        {
            public string[] Files;
            public string Description;
        }

        private string GetRemoteFolder()
        {
            if (InvokeRequired)
            {
                return (string)Invoke(new Func<string>(GetRemoteFolder));
            }

            return NormalizeRemoteFolder(remotePathBox.Text);
        }

        private bool IsApkSmartInstallEnabled()
        {
            if (InvokeRequired)
            {
                return (bool)Invoke(new Func<bool>(IsApkSmartInstallEnabled));
            }

            return apkSmartInstallCheckBox.Checked;
        }

        private void EnsureRemoteFolder()
        {
            string remoteFolder = GetRemoteFolder();
            AppSettings.SaveRemoteFolder(remoteFolder);
            var result = Adb.Run(adbPath, "shell mkdir -p " + Adb.ShellQuote(remoteFolder), 30000);
            if (result.ExitCode != 0)
            {
                Log(result.AllOutput.Trim());
                throw new InvalidOperationException("无法创建或访问手机目录: " + remoteFolder);
            }
        }

        private static string NormalizeRemoteFolder(string remoteFolder)
        {
            if (string.IsNullOrWhiteSpace(remoteFolder)) return "/sdcard";

            remoteFolder = remoteFolder.Trim().Replace('\\', '/');
            while (remoteFolder.Contains("//"))
            {
                remoteFolder = remoteFolder.Replace("//", "/");
            }

            if (!remoteFolder.StartsWith("/", StringComparison.Ordinal))
            {
                remoteFolder = "/sdcard/" + remoteFolder;
            }

            if (remoteFolder.Length > 1)
            {
                remoteFolder = remoteFolder.TrimEnd('/');
            }

            return remoteFolder.Length == 0 ? "/sdcard" : remoteFolder;
        }

        private static string BuildRemotePath(string remoteFolder, string fileName)
        {
            remoteFolder = NormalizeRemoteFolder(remoteFolder);
            return remoteFolder.TrimEnd('/') + "/" + fileName;
        }

        private void UpdateDeviceStatus(bool connected)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(UpdateDeviceStatus), connected);
                return;
            }

            deviceLabel.Text = connected ? "设备状态: 已连接" : "设备状态: 未确认";
            deviceLabel.ForeColor = connected ? Color.FromArgb(0, 120, 70) : Color.FromArgb(170, 80, 0);
        }

        private void UpdateSummary()
        {
            int apk = 0, other = 0, install = 0, upgrade = 0, copy = 0;
            foreach (PackageItem item in items)
            {
                if (item.Type == "APK")
                {
                    apk++;
                    if (item.Action == "安装") install++;
                    else if (item.Action == "升级") upgrade++;
                    else if (item.Action == "复制") copy++;
                }
                else
                {
                    other++;
                    if (item.Action == "复制") copy++;
                }
            }

            summaryLabel.Text = "APK " + apk + " 个，其他文件 " + other + " 个；安装 " + install + "，升级 " + upgrade + "，复制 " + copy;
        }

        private void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine;
            logBox.AppendText(line);
        }

        private static string NullableText(long? value)
        {
            return value.HasValue ? value.Value.ToString() : "未安装";
        }
    }

    public static class ThemePainter
    {
        public static void PaintThemedSurface(Graphics graphics, Form hostForm, Control control, Image image, Color baseColor, float imageOpacity, Color overlayColor)
        {
            using (var brush = new SolidBrush(baseColor))
            {
                graphics.FillRectangle(brush, control.ClientRectangle);
            }

            if (hostForm != null && image != null && control.Width > 0 && control.Height > 0)
            {
                MainForm mainForm = hostForm as MainForm;
                Bitmap cachedBackground = mainForm == null ? null : mainForm.BackgroundCanvas;

                if (cachedBackground != null)
                {
                    Point controlOnForm = hostForm.PointToClient(control.PointToScreen(Point.Empty));
                    float scaleX = hostForm.ClientSize.Width <= 0 ? 1f : cachedBackground.Width / (float)hostForm.ClientSize.Width;
                    float scaleY = hostForm.ClientSize.Height <= 0 ? 1f : cachedBackground.Height / (float)hostForm.ClientSize.Height;
                    Rectangle sourceRectangle = new Rectangle(
                        Math.Max(0, (int)Math.Floor(controlOnForm.X * scaleX)),
                        Math.Max(0, (int)Math.Floor(controlOnForm.Y * scaleY)),
                        Math.Max(1, (int)Math.Ceiling(control.Width * scaleX)),
                        Math.Max(1, (int)Math.Ceiling(control.Height * scaleY)));

                    if (sourceRectangle.Right > cachedBackground.Width) sourceRectangle.Width = cachedBackground.Width - sourceRectangle.X;
                    if (sourceRectangle.Bottom > cachedBackground.Height) sourceRectangle.Height = cachedBackground.Height - sourceRectangle.Y;

                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                    graphics.DrawImage(
                        cachedBackground,
                        control.ClientRectangle,
                        sourceRectangle,
                        GraphicsUnit.Pixel);
                }
                else
                {
                    Rectangle formImageRect = GetCoverRectangle(image.Size, hostForm.ClientSize);
                    Point controlOnForm = hostForm.PointToClient(control.PointToScreen(Point.Empty));
                    Rectangle destination = new Rectangle(
                        formImageRect.X - controlOnForm.X,
                        formImageRect.Y - controlOnForm.Y,
                        formImageRect.Width,
                        formImageRect.Height);

                    using (var attributes = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix();
                        matrix.Matrix33 = imageOpacity;
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        graphics.DrawImage(
                            image,
                            destination,
                            0,
                            0,
                            image.Width,
                            image.Height,
                            GraphicsUnit.Pixel,
                            attributes);
                    }
                }
            }

            using (var overlay = new SolidBrush(overlayColor))
            {
                graphics.FillRectangle(overlay, control.ClientRectangle);
            }
        }

        public static Rectangle GetCoverRectangle(Size imageSize, Size targetSize)
        {
            float scale = Math.Max(
                targetSize.Width / (float)imageSize.Width,
                targetSize.Height / (float)imageSize.Height);
            int width = (int)Math.Ceiling(imageSize.Width * scale);
            int height = (int)Math.Ceiling(imageSize.Height * scale);
            int x = (targetSize.Width - width) / 2;
            int y = (targetSize.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }
    }

    public sealed class TranslucentDataGridView : DataGridView
    {
        public Form HostForm { get; set; }
        public Image ThemeImage { get; set; }

        public TranslucentDataGridView()
        {
            DoubleBuffered = true;
        }

        protected override void PaintBackground(Graphics graphics, Rectangle clipBounds, Rectangle gridBounds)
        {
            ThemePainter.PaintThemedSurface(
                graphics,
                HostForm,
                this,
                ThemeImage,
                Color.FromArgb(248, 244, 247),
                0.70f,
                Color.FromArgb(120, 255, 255, 255));
        }
    }

    public sealed class LightweightPackageGrid : Control
    {
        private readonly string[] headers = { "文件", "类型", "包名", "APK版本", "版本名", "手机版本", "动作", "状态" };
        private readonly int[] widths = { 230, 60, 210, 86, 110, 86, 86, 170 };
        private BindingList<PackageItem> items;
        private int scrollOffset;

        public Form HostForm { get; set; }
        public Image ThemeImage { get; set; }

        public BindingList<PackageItem> Items
        {
            get { return items; }
            set
            {
                if (items != null)
                {
                    items.ListChanged -= ItemsListChanged;
                }

                items = value;

                if (items != null)
                {
                    items.ListChanged += ItemsListChanged;
                }

                scrollOffset = 0;
                Invalidate();
            }
        }

        public LightweightPackageGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(248, 244, 247);
            ForeColor = Color.FromArgb(35, 35, 35);
        }

        private void ItemsListChanged(object sender, ListChangedEventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Invalidate));
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (items == null || items.Count == 0) return;

            int rowHeight = GetRowHeight();
            int visibleRows = Math.Max(1, (ClientSize.Height - GetHeaderHeight() - 2) / rowHeight);
            int maxOffset = Math.Max(0, items.Count - visibleRows);
            scrollOffset = Math.Max(0, Math.Min(maxOffset, scrollOffset - Math.Sign(e.Delta) * 3));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePainter.PaintThemedSurface(
                e.Graphics,
                HostForm,
                this,
                ThemeImage,
                Color.FromArgb(248, 244, 247),
                0.70f,
                Color.FromArgb(118, 255, 255, 255));

            Rectangle border = ClientRectangle;
            border.Width--;
            border.Height--;
            using (var pen = new Pen(Color.FromArgb(190, 90, 90, 95)))
            {
                e.Graphics.DrawRectangle(pen, border);
            }

            DrawHeader(e.Graphics);
            DrawRows(e.Graphics);
        }

        private void DrawHeader(Graphics graphics)
        {
            int headerHeight = GetHeaderHeight();
            using (var brush = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
            {
                graphics.FillRectangle(brush, new Rectangle(1, 1, ClientSize.Width - 2, headerHeight));
            }

            using (var pen = new Pen(Color.FromArgb(170, 175, 175, 180)))
            {
                graphics.DrawLine(pen, 1, headerHeight, ClientSize.Width - 2, headerHeight);

                int x = 1;
                int[] actualWidths = GetActualWidths();
                for (int i = 0; i < headers.Length; i++)
                {
                    Rectangle cell = new Rectangle(x + 8, 1, Math.Max(1, actualWidths[i] - 12), headerHeight - 1);
                    TextRenderer.DrawText(
                        graphics,
                        headers[i],
                        Font,
                        cell,
                        ForeColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

                    x += actualWidths[i];
                    graphics.DrawLine(pen, x, 1, x, headerHeight);
                }
            }
        }

        private void DrawRows(Graphics graphics)
        {
            if (items == null || items.Count == 0) return;

            int headerHeight = GetHeaderHeight();
            int rowHeight = GetRowHeight();
            int y = headerHeight + 1;
            int[] actualWidths = GetActualWidths();

            using (var linePen = new Pen(Color.FromArgb(95, 210, 210, 215)))
            using (var evenBrush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
            using (var oddBrush = new SolidBrush(Color.FromArgb(38, 255, 255, 255)))
            {
                for (int rowIndex = scrollOffset; rowIndex < items.Count && y < ClientSize.Height - 1; rowIndex++)
                {
                    PackageItem item = items[rowIndex];
                    Rectangle rowRect = new Rectangle(1, y, ClientSize.Width - 2, rowHeight);
                    graphics.FillRectangle(((rowIndex % 2) == 0) ? evenBrush : oddBrush, rowRect);

                    string[] values = {
                        item.FileName,
                        item.Type,
                        item.PackageName,
                        NullableText(item.ApkVersionCode),
                        item.ApkVersionName,
                        NullableText(item.InstalledVersionCode),
                        item.Action,
                        item.Status
                    };

                    int x = 1;
                    for (int col = 0; col < values.Length; col++)
                    {
                        Rectangle cell = new Rectangle(x + 8, y, Math.Max(1, actualWidths[col] - 12), rowHeight);
                        TextRenderer.DrawText(
                            graphics,
                            values[col] ?? "",
                            Font,
                            cell,
                            ForeColor,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                        x += actualWidths[col];
                    }

                    graphics.DrawLine(linePen, 1, y + rowHeight, ClientSize.Width - 2, y + rowHeight);
                    y += rowHeight;
                }
            }
        }

        private int[] GetActualWidths()
        {
            int[] actual = new int[widths.Length];
            int totalFixed = 0;
            for (int i = 0; i < widths.Length - 1; i++)
            {
                actual[i] = widths[i];
                totalFixed += widths[i];
            }

            actual[widths.Length - 1] = Math.Max(widths[widths.Length - 1], ClientSize.Width - 2 - totalFixed);
            return actual;
        }

        private int GetHeaderHeight()
        {
            return Math.Max(28, Font.Height + 10);
        }

        private int GetRowHeight()
        {
            return Math.Max(27, Font.Height + 9);
        }

        private static string NullableText(long? value)
        {
            return value.HasValue ? value.Value.ToString() : "";
        }
    }

    public sealed class TranslucentPathBox : Control
    {
        private int caretIndex;

        public Form HostForm { get; set; }
        public Image ThemeImage { get; set; }

        public TranslucentPathBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.IBeam;
            BackColor = Color.FromArgb(248, 244, 247);
            ForeColor = Color.FromArgb(32, 32, 32);
            Padding = new Padding(8, 0, 8, 0);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (caretIndex > Text.Length) caretIndex = Text.Length;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            caretIndex = Text.Length;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            caretIndex = Text.Length;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (!char.IsControl(e.KeyChar))
            {
                Text = Text.Insert(caretIndex, e.KeyChar.ToString());
                caretIndex++;
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Control && e.KeyCode == Keys.V)
            {
                string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                if (text.Length > 0)
                {
                    Text = Text.Insert(caretIndex, text);
                    caretIndex += text.Length;
                }
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                if (Text.Length > 0) Clipboard.SetText(Text);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.X)
            {
                if (Text.Length > 0) Clipboard.SetText(Text);
                Text = "";
                caretIndex = 0;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Back && caretIndex > 0)
            {
                Text = Text.Remove(caretIndex - 1, 1);
                caretIndex--;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete && caretIndex < Text.Length)
            {
                Text = Text.Remove(caretIndex, 1);
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                caretIndex = Math.Max(0, caretIndex - 1);
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                caretIndex = Math.Min(Text.Length, caretIndex + 1);
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                caretIndex = 0;
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                caretIndex = Text.Length;
                Invalidate();
                e.SuppressKeyPress = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePainter.PaintThemedSurface(
                e.Graphics,
                HostForm,
                this,
                ThemeImage,
                Color.FromArgb(248, 244, 247),
                0.70f,
                Color.FromArgb(130, 255, 255, 255));

            Rectangle border = ClientRectangle;
            border.Width--;
            border.Height--;
            using (var pen = new Pen(Focused ? Color.FromArgb(210, 120, 150, 185) : Color.FromArgb(180, 205, 205, 210)))
            {
                e.Graphics.DrawRectangle(pen, border);
            }

            Rectangle textBounds = new Rectangle(
                Padding.Left,
                0,
                Math.Max(1, ClientSize.Width - Padding.Left - Padding.Right),
                ClientSize.Height);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused)
            {
                string beforeCaret = caretIndex <= 0 ? "" : Text.Substring(0, Math.Min(caretIndex, Text.Length));
                int caretX = Padding.Left + TextRenderer.MeasureText(beforeCaret, Font, new Size(int.MaxValue, ClientSize.Height), TextFormatFlags.NoPadding).Width;
                caretX = Math.Min(ClientSize.Width - Padding.Right, caretX);
                int caretTop = Math.Max(4, (ClientSize.Height - Font.Height) / 2);
                using (var pen = new Pen(ForeColor))
                {
                    e.Graphics.DrawLine(pen, caretX, caretTop, caretX, caretTop + Font.Height);
                }
            }
        }
    }

    public sealed class TranslucentLogView : Control
    {
        private readonly List<string> lines = new List<string>();

        public Form HostForm { get; set; }
        public Image ThemeImage { get; set; }

        public TranslucentLogView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.FromArgb(14, 18, 24);
            Padding = new Padding(8);
        }

        public void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] newLines = normalized.Split('\n');
            foreach (string line in newLines)
            {
                if (line.Length == 0) continue;
                lines.Add(line);
            }

            while (lines.Count > 500)
            {
                lines.RemoveAt(0);
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePainter.PaintThemedSurface(
                e.Graphics,
                HostForm,
                this,
                ThemeImage,
                Color.FromArgb(248, 244, 247),
                0.70f,
                Color.FromArgb(168, 14, 18, 24));

            Rectangle border = ClientRectangle;
            border.Width--;
            border.Height--;
            using (var pen = new Pen(Color.FromArgb(170, 230, 230, 235)))
            {
                e.Graphics.DrawRectangle(pen, border);
            }

            int lineHeight = TextRenderer.MeasureText("Ag", Font).Height + 2;
            int capacity = Math.Max(1, (ClientSize.Height - Padding.Top - Padding.Bottom) / lineHeight);
            int start = Math.Max(0, lines.Count - capacity);
            int y = Padding.Top;

            for (int i = start; i < lines.Count; i++)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    lines[i],
                    Font,
                    new Rectangle(Padding.Left, y, ClientSize.Width - Padding.Left - Padding.Right, lineHeight),
                    ForeColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                y += lineHeight;
            }
        }
    }

    public sealed class RemoteFolderDialog : Form
    {
        private readonly string adbPath;
        private readonly TreeView tree = new TreeView();
        private readonly TextBox pathTextBox = new TextBox();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button refreshButton = new Button();
        private readonly Button newFolderButton = new Button();
        private readonly Button exportButton = new Button();

        public string SelectedPath { get; private set; }

        public RemoteFolderDialog(string adbPath, string initialPath)
        {
            this.adbPath = adbPath;
            SelectedPath = string.IsNullOrWhiteSpace(initialPath) ? "/sdcard" : initialPath;

            Text = "选择手机目录";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 520);
            MinimumSize = new Size(520, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildLayout();
            LoadRoot();
            pathTextBox.Text = SelectedPath;
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            pathTextBox.Dock = DockStyle.Fill;
            root.Controls.Add(pathTextBox, 0, 0);

            tree.Dock = DockStyle.Fill;
            tree.HideSelection = false;
            tree.BeforeExpand += TreeBeforeExpand;
            tree.AfterSelect += TreeAfterSelect;
            root.Controls.Add(tree, 0, 1);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            root.Controls.Add(buttons, 0, 2);

            okButton.Text = "确定";
            okButton.Width = 82;
            okButton.Height = 28;
            okButton.Click += OkButtonClick;
            buttons.Controls.Add(okButton);

            cancelButton.Text = "取消";
            cancelButton.Width = 82;
            cancelButton.Height = 28;
            cancelButton.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancelButton);

            refreshButton.Text = "刷新";
            refreshButton.Width = 82;
            refreshButton.Height = 28;
            refreshButton.Click += delegate { LoadSelectedNode(tree.SelectedNode); };
            buttons.Controls.Add(refreshButton);

            newFolderButton.Text = "新建文件夹";
            newFolderButton.Width = 96;
            newFolderButton.Height = 28;
            newFolderButton.Click += NewFolderButtonClick;
            buttons.Controls.Add(newFolderButton);

            exportButton.Text = "导出到电脑";
            exportButton.Width = 96;
            exportButton.Height = 28;
            exportButton.Click += ExportButtonClick;
            buttons.Controls.Add(exportButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadRoot()
        {
            tree.Nodes.Clear();
            TreeNode rootNode = CreateNode("[D] /", "/", true);
            tree.Nodes.Add(rootNode);
            LoadNode(rootNode);
            rootNode.Expand();

            AddShortcut("/sdcard");
            AddShortcut("/storage/emulated/0");
            AddShortcut("/data/local/tmp");
        }

        private void AddShortcut(string path)
        {
            foreach (TreeNode node in tree.Nodes)
            {
                RemoteNodeInfo info = node.Tag as RemoteNodeInfo;
                if (info != null && string.Equals(info.Path, path, StringComparison.Ordinal)) return;
            }

            TreeNode shortcut = CreateNode("[D] " + path, path, true);
            tree.Nodes.Add(shortcut);
        }

        private TreeNode CreateNode(string text, string path, bool isDirectory)
        {
            TreeNode node = new TreeNode(text);
            node.Tag = new RemoteNodeInfo { Path = path, IsDirectory = isDirectory };
            if (isDirectory)
            {
                node.Nodes.Add(new TreeNode("加载中..."));
            }
            return node;
        }

        private void TreeBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            LoadNode(e.Node);
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            RemoteNodeInfo info = GetNodeInfo(e.Node);
            if (info != null)
            {
                pathTextBox.Text = info.Path;
            }
        }

        private void LoadSelectedNode(TreeNode node)
        {
            if (node == null) node = tree.Nodes.Count > 0 ? tree.Nodes[0] : null;
            if (node == null) return;
            LoadNode(node, true);
            node.Expand();
        }

        private void LoadNode(TreeNode node)
        {
            LoadNode(node, false);
        }

        private void LoadNode(TreeNode node, bool force)
        {
            RemoteNodeInfo nodeInfo = GetNodeInfo(node);
            if (node == null || nodeInfo == null || !nodeInfo.IsDirectory) return;
            if (!force && node.Nodes.Count != 1) return;
            if (!force && node.Nodes.Count == 1 && node.Nodes[0].Tag != null) return;

            string path = nodeInfo.Path;
            node.Nodes.Clear();

            try
            {
                List<RemoteEntry> entries = Adb.ListEntries(adbPath, path);
                foreach (RemoteEntry entry in entries)
                {
                    string childPath = CombineRemotePath(path, entry.Name);
                    node.Nodes.Add(CreateNode((entry.IsDirectory ? "[D] " : "[F] ") + entry.Name, childPath, entry.IsDirectory));
                }
            }
            catch (Exception ex)
            {
                node.Nodes.Add(new TreeNode("无法读取: " + ex.Message));
            }
        }

        private void NewFolderButtonClick(object sender, EventArgs e)
        {
            string parent = pathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(parent)) parent = "/";

            string name = PromptText.Show(this, "新建文件夹", "文件夹名称:");
            if (string.IsNullOrWhiteSpace(name)) return;

            name = name.Trim().Trim('/', '\\');
            if (name.Length == 0) return;

            string newPath = CombineRemotePath(parent, name);
            ProcessResult result = Adb.Run(adbPath, "shell mkdir -p " + Adb.ShellQuote(newPath), 30000);
            if (result.ExitCode != 0)
            {
                MessageBox.Show(this, result.AllOutput.Trim(), "新建文件夹失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            pathTextBox.Text = newPath;
            TreeNode selected = tree.SelectedNode;
            if (selected != null)
            {
                RemoteNodeInfo info = GetNodeInfo(selected);
                if (info != null && !info.IsDirectory && selected.Parent != null)
                {
                    selected = selected.Parent;
                }

                LoadNode(selected, true);
                selected.Expand();
            }
        }

        private void ExportButtonClick(object sender, EventArgs e)
        {
            RemoteNodeInfo info = GetNodeInfo(tree.SelectedNode);
            if (info == null)
            {
                MessageBox.Show(this, "请先选择一个手机文件或文件夹。", "导出到电脑", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择导出到电脑的文件夹";
                dialog.SelectedPath = AppSettings.LoadExportFolder();
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string exportFolder = dialog.SelectedPath;
                Directory.CreateDirectory(exportFolder);
                AppSettings.SaveExportFolder(exportFolder);

                ExportRemotePath(info, exportFolder);
                MessageBox.Show(this, "导出完成: " + exportFolder, "导出到电脑", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportRemotePath(RemoteNodeInfo info, string exportFolder)
        {
            string stagingRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "DoododAdbExportTemp");
            string stagingFolder = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingFolder);

            try
            {
                string pullTarget = Path.Combine(stagingFolder, info.IsDirectory ? "payload_dir" : "payload.bin");
                ProcessResult result = Adb.Run(adbPath, "pull " + Adb.Quote(info.Path) + " " + Adb.Quote(pullTarget), 300000);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(result.AllOutput.Trim());
                }

                string name = GetRemoteName(info.Path);
                string destinationPath = Path.Combine(exportFolder, name);

                if (File.Exists(pullTarget))
                {
                    File.Copy(pullTarget, destinationPath, true);
                }
                else if (Directory.Exists(pullTarget))
                {
                    CopyDirectory(pullTarget, destinationPath);
                }
                else
                {
                    throw new FileNotFoundException("adb 已完成但未找到导出的文件: " + pullTarget);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingFolder)) Directory.Delete(stagingFolder, true);
                }
                catch
                {
                }
            }
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string file in Directory.GetFiles(sourceDirectory))
            {
                string target = Path.Combine(destinationDirectory, Path.GetFileName(file));
                File.Copy(file, target, true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                string target = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                CopyDirectory(directory, target);
            }
        }

        private static string GetRemoteName(string path)
        {
            path = NormalizeRemotePath(path);
            if (path == "/") return "root";

            int index = path.LastIndexOf('/');
            if (index < 0 || index == path.Length - 1) return path.Trim('/');
            return path.Substring(index + 1);
        }

        private void OkButtonClick(object sender, EventArgs e)
        {
            RemoteNodeInfo info = GetNodeInfo(tree.SelectedNode);
            if (info != null && !info.IsDirectory)
            {
                SelectedPath = GetRemoteParent(info.Path);
            }
            else
            {
                SelectedPath = NormalizeRemotePath(pathTextBox.Text);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static RemoteNodeInfo GetNodeInfo(TreeNode node)
        {
            return node == null ? null : node.Tag as RemoteNodeInfo;
        }

        private static string CombineRemotePath(string parent, string child)
        {
            parent = NormalizeRemotePath(parent);
            child = child.Trim().Trim('/', '\\');
            if (parent == "/") return "/" + child;
            return parent.TrimEnd('/') + "/" + child;
        }

        private static string NormalizeRemotePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "/sdcard";
            path = path.Trim().Replace('\\', '/');
            while (path.Contains("//")) path = path.Replace("//", "/");
            if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/sdcard/" + path;
            if (path.Length > 1) path = path.TrimEnd('/');
            return path.Length == 0 ? "/" : path;
        }

        private static string GetRemoteParent(string path)
        {
            path = NormalizeRemotePath(path);
            int index = path.LastIndexOf('/');
            if (index <= 0) return "/";
            return path.Substring(0, index);
        }

        private sealed class RemoteNodeInfo
        {
            public string Path;
            public bool IsDirectory;
        }
    }

    public static class PromptText
    {
        public static string Show(IWin32Window owner, string title, string label)
        {
            using (var form = new Form())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            using (var labelControl = new Label())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(360, 112);
                form.Font = new Font("Microsoft YaHei UI", 9F);

                labelControl.Text = label;
                labelControl.SetBounds(12, 12, 330, 20);
                form.Controls.Add(labelControl);

                textBox.SetBounds(12, 36, 336, 24);
                form.Controls.Add(textBox);

                okButton.Text = "确定";
                okButton.SetBounds(174, 74, 82, 28);
                okButton.DialogResult = DialogResult.OK;
                form.Controls.Add(okButton);

                cancelButton.Text = "取消";
                cancelButton.SetBounds(266, 74, 82, 28);
                cancelButton.DialogResult = DialogResult.Cancel;
                form.Controls.Add(cancelButton);

                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : "";
            }
        }
    }

    public sealed class ProcessResult
    {
        public int ExitCode;
        public string Stdout;
        public string Stderr;

        public string AllOutput
        {
            get
            {
                string value = "";
                if (!string.IsNullOrEmpty(Stdout)) value += Stdout;
                if (!string.IsNullOrEmpty(Stderr)) value += Stderr;
                return value;
            }
        }
    }

    public sealed class RemoteEntry
    {
        public string Name;
        public bool IsDirectory;
    }

    public static class Adb
    {
        public static string FindAdb()
        {
            string local = Path.Combine(AppPaths.AppDirectory, "adb.exe");
            if (File.Exists(local)) return local;

            string known = @"C:\app\platform-tools\adb.exe";
            if (File.Exists(known)) return known;

            var result = Run("where", "adb", 10000);
            if (result.ExitCode == 0)
            {
                using (var reader = new StringReader(result.Stdout))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0 && File.Exists(line)) return line;
                    }
                }
            }

            return null;
        }

        public static List<string> ParseConnectedDevices(string output)
        {
            var devices = new List<string>();
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.EndsWith("\tdevice", StringComparison.Ordinal))
                    {
                        devices.Add(line.Split('\t')[0]);
                    }
                }
            }

            return devices;
        }

        public static long? GetInstalledVersionCode(string adbPath, string packageName)
        {
            var result = Run(adbPath, "shell dumpsys package " + packageName, 30000);
            string output = result.AllOutput;
            if (output.IndexOf("Unable to find package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                output.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            Match match = Regex.Match(output, @"versionCode=(\d+)");
            if (match.Success)
            {
                long version;
                if (long.TryParse(match.Groups[1].Value, out version)) return version;
            }

            return null;
        }

        public static List<string> ListDirectories(string adbPath, string path)
        {
            List<RemoteEntry> entries = ListEntries(adbPath, path);
            var directories = new List<string>();
            foreach (RemoteEntry entry in entries)
            {
                if (entry.IsDirectory) directories.Add(entry.Name);
            }

            return directories;
        }

        public static List<RemoteEntry> ListEntries(string adbPath, string path)
        {
            var result = Run(adbPath, "shell ls -1 -p -a " + ShellQuote(path), 30000);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.AllOutput.Trim());
            }

            var entries = new List<RemoteEntry>();
            using (var reader = new StringReader(result.Stdout))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line == "." || line == "..") continue;

                    bool isDirectory = line.EndsWith("/", StringComparison.Ordinal);
                    if (isDirectory) line = line.TrimEnd('/');
                    if (line.Length == 0 || line == "." || line == "..") continue;
                    entries.Add(new RemoteEntry { Name = line, IsDirectory = isDirectory });
                }
            }

            entries.Sort(delegate(RemoteEntry left, RemoteEntry right)
            {
                if (left.IsDirectory != right.IsDirectory)
                {
                    return left.IsDirectory ? -1 : 1;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            });

            return entries;
        }

        public static ProcessResult Run(string fileName, string arguments, int timeoutMs)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = fileName;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            using (var process = new Process())
            {
                process.StartInfo = psi;
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return new ProcessResult { ExitCode = -1, Stdout = stdout.ToString(), Stderr = stderr.ToString() + "命令超时" + Environment.NewLine };
                }

                process.WaitForExit();
                return new ProcessResult { ExitCode = process.ExitCode, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
            }
        }

        public static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }
    }

    public static class AppPaths
    {
        public static string AppDirectory
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
        }
    }

    public static class AppSettings
    {
        private static string SettingsDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DoododAutoInstaller");
            }
        }

        private static string SettingsFile
        {
            get { return Path.Combine(SettingsDirectory, "settings.txt"); }
        }

        private static string RemoteSettingsFile
        {
            get { return Path.Combine(SettingsDirectory, "remote_folder.txt"); }
        }

        private static string ExportSettingsFile
        {
            get { return Path.Combine(SettingsDirectory, "export_folder.txt"); }
        }

        public static string LoadLastFolder()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return "";
                string folder = File.ReadAllText(SettingsFile, Encoding.UTF8).Trim();
                return Directory.Exists(folder) ? folder : "";
            }
            catch
            {
                return "";
            }
        }

        public static void SaveLastFolder(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(SettingsFile, folder, Encoding.UTF8);
            }
            catch
            {
            }
        }

        public static string LoadExportFolder()
        {
            try
            {
                if (File.Exists(ExportSettingsFile))
                {
                    string folder = File.ReadAllText(ExportSettingsFile, Encoding.UTF8).Trim();
                    if (Directory.Exists(folder) && !IsRootChineseShortcut(folder)) return folder;
                }
            }
            catch
            {
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktop)) return desktop;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(userProfile) ? userProfile : AppPaths.AppDirectory;
        }

        private static bool IsRootChineseShortcut(string folder)
        {
            try
            {
                string full = Path.GetFullPath(folder).TrimEnd('\\', '/');
                string root = Path.GetPathRoot(full).TrimEnd('\\', '/');
                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return false;

                string parent = Path.GetDirectoryName(full);
                if (!string.Equals(parent == null ? "" : parent.TrimEnd('\\', '/'), root, StringComparison.OrdinalIgnoreCase)) return false;

                string name = Path.GetFileName(full);
                foreach (char ch in name)
                {
                    if (ch > 127) return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SaveExportFolder(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(ExportSettingsFile, folder, Encoding.UTF8);
            }
            catch
            {
            }
        }

        public static string LoadRemoteFolder()
        {
            try
            {
                if (!File.Exists(RemoteSettingsFile)) return "/sdcard";
                string folder = File.ReadAllText(RemoteSettingsFile, Encoding.UTF8).Trim();
                return string.IsNullOrWhiteSpace(folder) ? "/sdcard" : folder;
            }
            catch
            {
                return "/sdcard";
            }
        }

        public static void SaveRemoteFolder(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) return;
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(RemoteSettingsFile, folder.Trim(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    public sealed class ApkInfo
    {
        public string PackageName;
        public long VersionCode;
        public string VersionName;
    }

    public static class ApkManifestReader
    {
        public static ApkInfo Read(string apkPath)
        {
            byte[] bytes;
            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                ZipArchiveEntry entry = archive.GetEntry("AndroidManifest.xml");
                if (entry == null) throw new InvalidDataException("未找到 AndroidManifest.xml");

                using (Stream input = entry.Open())
                using (var memory = new MemoryStream())
                {
                    input.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }

            if (ReadUInt16(bytes, 0) != 0x0003) throw new InvalidDataException("Manifest 不是 Android 二进制 XML");

            int offset = ReadUInt16(bytes, 2);
            string[] strings = null;

            while (offset < bytes.Length)
            {
                ushort chunkType = ReadUInt16(bytes, offset);
                int chunkSize = (int)ReadUInt32(bytes, offset + 4);

                if (chunkType == 0x0001)
                {
                    strings = ReadStringPool(bytes, offset);
                }
                else if (chunkType == 0x0102 && strings != null)
                {
                    uint nameIndex = ReadUInt32(bytes, offset + 20);
                    if (nameIndex < strings.Length && strings[nameIndex] == "manifest")
                    {
                        return ReadManifestAttributes(bytes, offset, strings);
                    }
                }

                if (chunkSize <= 0) throw new InvalidDataException("Manifest chunk 长度异常");
                offset += chunkSize;
            }

            throw new InvalidDataException("未找到 manifest 节点");
        }

        private static ApkInfo ReadManifestAttributes(byte[] bytes, int offset, string[] strings)
        {
            int attributeStart = ReadUInt16(bytes, offset + 24);
            int attributeSize = ReadUInt16(bytes, offset + 26);
            int attributeCount = ReadUInt16(bytes, offset + 28);

            string packageName = null;
            long? versionCode = null;
            string versionName = "";

            for (int i = 0; i < attributeCount; i++)
            {
                int attributeOffset = offset + 16 + attributeStart + (i * attributeSize);
                uint attrNameIndex = ReadUInt32(bytes, attributeOffset + 4);
                if (attrNameIndex == 0xFFFFFFFF || attrNameIndex >= strings.Length) continue;

                string attrName = strings[attrNameIndex];
                uint rawValueIndex = ReadUInt32(bytes, attributeOffset + 8);
                byte dataType = bytes[attributeOffset + 15];
                uint data = ReadUInt32(bytes, attributeOffset + 16);
                object value = ConvertTypedValue(strings, rawValueIndex, dataType, data);

                if (attrName == "package") packageName = Convert.ToString(value);
                else if (attrName == "versionCode") versionCode = Convert.ToInt64(value);
                else if (attrName == "versionName") versionName = Convert.ToString(value);
            }

            if (string.IsNullOrEmpty(packageName) || !versionCode.HasValue)
            {
                throw new InvalidDataException("无法读取 package 或 versionCode");
            }

            return new ApkInfo { PackageName = packageName, VersionCode = versionCode.Value, VersionName = versionName };
        }

        private static object ConvertTypedValue(string[] strings, uint rawValueIndex, byte dataType, uint data)
        {
            if (rawValueIndex != 0xFFFFFFFF && rawValueIndex < strings.Length) return strings[rawValueIndex];
            if (dataType == 0x03 && data < strings.Length) return strings[data];
            if (dataType == 0x10 || dataType == 0x11) return (long)data;
            if (dataType == 0x12) return data != 0;
            return (long)data;
        }

        private static string[] ReadStringPool(byte[] bytes, int chunkOffset)
        {
            int stringCount = (int)ReadUInt32(bytes, chunkOffset + 8);
            uint flags = ReadUInt32(bytes, chunkOffset + 16);
            int stringsStart = (int)ReadUInt32(bytes, chunkOffset + 20);
            bool isUtf8 = (flags & 0x00000100) != 0;

            string[] strings = new string[stringCount];
            for (int i = 0; i < stringCount; i++)
            {
                int stringOffset = (int)ReadUInt32(bytes, chunkOffset + 28 + (i * 4));
                int cursor = chunkOffset + stringsStart + stringOffset;

                if (isUtf8)
                {
                    ReadStringPoolLength8(bytes, ref cursor);
                    int byteLength = ReadStringPoolLength8(bytes, ref cursor);
                    strings[i] = Encoding.UTF8.GetString(bytes, cursor, byteLength);
                }
                else
                {
                    int charLength = ReadStringPoolLength16(bytes, ref cursor);
                    strings[i] = Encoding.Unicode.GetString(bytes, cursor, charLength * 2);
                }
            }

            return strings;
        }

        private static int ReadStringPoolLength8(byte[] bytes, ref int offset)
        {
            int first = bytes[offset++];
            if ((first & 0x80) != 0)
            {
                int second = bytes[offset++];
                return ((first & 0x7F) << 8) | second;
            }

            return first;
        }

        private static int ReadStringPoolLength16(byte[] bytes, ref int offset)
        {
            int first = ReadUInt16(bytes, offset);
            offset += 2;
            if ((first & 0x8000) != 0)
            {
                int second = ReadUInt16(bytes, offset);
                offset += 2;
                return ((first & 0x7FFF) << 16) | second;
            }

            return first;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return BitConverter.ToUInt16(bytes, offset);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return BitConverter.ToUInt32(bytes, offset);
        }
    }
}
