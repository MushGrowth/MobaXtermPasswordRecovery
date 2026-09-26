using System.Diagnostics;
using System.Text;
using MobaXtermPasswordRecovery.Softwares;
using MobaXtermPasswordRecovery.Utils;

namespace MobaXtermPasswordRecovery;

internal sealed class MainForm : Form
{
    private readonly RadioButton _autoModeRadio = new();
    private readonly RadioButton _iniModeRadio = new();
    private readonly TextBox _iniPathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _detectButton = new();
    private readonly Button _recoverButton = new();
    private readonly Button _copyButton = new();
    private readonly Button _clearButton = new();
    private readonly CheckBox _showPasswordsCheckBox = new();
    private readonly RichTextBox _resultTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly List<string> _rawLines = new();
    private readonly object _resultLock = new();

    public MainForm()
    {
        Text = "MobaXterm 密码恢复工具";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 600);
        Size = new Size(920, 680);
        Font = new Font("Microsoft YaHei UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildInterface();
        UpdateModeControls();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "MobaXterm 密码恢复工具",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 17F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(title, 0, 0);

        var description = new Label
        {
            Text = "请选择配置来源，然后点击“开始恢复”。结果只显示在当前窗口，不会写入日志文件。",
            AutoSize = true,
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(0, 0, 0, 14),
        };
        root.Controls.Add(description, 0, 1);

        var sourceGroup = new GroupBox
        {
            Text = "1. 选择配置来源",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 12),
        };
        root.Controls.Add(sourceGroup, 0, 2);

        var sourceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 3,
        };
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourceGroup.Controls.Add(sourceLayout);

        _autoModeRadio.Text = "自动检测安装版或注册表配置";
        _autoModeRadio.Checked = true;
        _autoModeRadio.AutoSize = true;
        _autoModeRadio.Margin = new Padding(3, 6, 3, 6);
        _autoModeRadio.CheckedChanged += (_, _) => UpdateModeControls();
        sourceLayout.Controls.Add(_autoModeRadio, 0, 0);
        sourceLayout.SetColumnSpan(_autoModeRadio, 4);

        _iniModeRadio.Text = "便携版 MobaXterm.ini";
        _iniModeRadio.AutoSize = true;
        _iniModeRadio.Margin = new Padding(3, 8, 10, 8);
        _iniModeRadio.CheckedChanged += (_, _) => UpdateModeControls();
        sourceLayout.Controls.Add(_iniModeRadio, 0, 1);

        _iniPathTextBox.Dock = DockStyle.Fill;
        _iniPathTextBox.PlaceholderText = "请选择 MobaXterm.ini 文件";
        _iniPathTextBox.Margin = new Padding(3, 5, 8, 5);
        sourceLayout.Controls.Add(_iniPathTextBox, 1, 1);

        _browseButton.Text = "浏览...";
        _browseButton.AutoSize = true;
        _browseButton.Click += BrowseButton_Click;
        sourceLayout.Controls.Add(_browseButton, 2, 1);

        _detectButton.Text = "自动查找";
        _detectButton.AutoSize = true;
        _detectButton.Click += DetectButton_Click;
        sourceLayout.Controls.Add(_detectButton, 3, 1);

        var hint = new Label
        {
            Text = "提示：便携版的 INI 通常与 MobaXterm.exe 位于同一目录。使用 DPAPI 时必须在原电脑、原 Windows 用户下运行。",
            AutoSize = true,
            ForeColor = Color.FromArgb(95, 95, 95),
            Margin = new Padding(3, 4, 3, 4),
        };
        sourceLayout.Controls.Add(hint, 0, 2);
        sourceLayout.SetColumnSpan(hint, 4);

        var resultGroup = new GroupBox
        {
            Text = "2. 恢复结果",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(resultGroup, 0, 3);

        var resultLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        resultLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        resultGroup.Controls.Add(resultLayout);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
        };
        resultLayout.Controls.Add(toolbar, 0, 0);

        _recoverButton.Text = "开始恢复";
        _recoverButton.AutoSize = true;
        _recoverButton.Font = new Font(Font, FontStyle.Bold);
        _recoverButton.Padding = new Padding(8, 3, 8, 3);
        _recoverButton.Click += RecoverButton_Click;
        toolbar.Controls.Add(_recoverButton);

        _showPasswordsCheckBox.Text = "显示明文密码";
        _showPasswordsCheckBox.AutoSize = true;
        _showPasswordsCheckBox.Margin = new Padding(16, 8, 5, 5);
        _showPasswordsCheckBox.CheckedChanged += (_, _) => RenderResults();
        toolbar.Controls.Add(_showPasswordsCheckBox);

        _copyButton.Text = "复制当前显示内容";
        _copyButton.AutoSize = true;
        _copyButton.Margin = new Padding(16, 3, 3, 3);
        _copyButton.Click += CopyButton_Click;
        toolbar.Controls.Add(_copyButton);

        _clearButton.Text = "清空";
        _clearButton.AutoSize = true;
        _clearButton.Click += (_, _) => ClearResults();
        toolbar.Controls.Add(_clearButton);

        _resultTextBox.Dock = DockStyle.Fill;
        _resultTextBox.ReadOnly = true;
        _resultTextBox.BackColor = Color.White;
        _resultTextBox.Font = new Font("Consolas", 10F);
        _resultTextBox.WordWrap = false;
        _resultTextBox.Text = "尚未执行恢复。\r\n";
        resultLayout.Controls.Add(_resultTextBox, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(footer, 0, 4);

        _statusLabel.Text = "就绪";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(65, 105, 160);
        footer.Controls.Add(_statusLabel, 0, 0);

        var safetyLabel = new Label
        {
            Text = "仅用于本人拥有或已获授权管理的凭据",
            AutoSize = true,
            ForeColor = Color.FromArgb(120, 80, 40),
        };
        footer.Controls.Add(safetyLabel, 1, 0);
    }

    private void UpdateModeControls()
    {
        bool portableMode = _iniModeRadio.Checked;
        _iniPathTextBox.Enabled = portableMode;
        _browseButton.Enabled = portableMode;
        _detectButton.Enabled = portableMode;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 MobaXterm.ini",
            Filter = "MobaXterm 配置文件 (MobaXterm.ini)|MobaXterm.ini|INI 文件 (*.ini)|*.ini|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (File.Exists(_iniPathTextBox.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_iniPathTextBox.Text);
            dialog.FileName = Path.GetFileName(_iniPathTextBox.Text);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _iniPathTextBox.Text = dialog.FileName;
        }
    }

    private void DetectButton_Click(object? sender, EventArgs e)
    {
        string? detectedPath = DetectIniPath();
        if (detectedPath is null)
        {
            MessageBox.Show(
                this,
                "没有自动找到 MobaXterm.ini。请点击“浏览...”手动选择文件。",
                "未找到配置文件",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        _iniPathTextBox.Text = detectedPath;
        _statusLabel.Text = "已找到配置文件";
    }

    private static string? DetectIniPath()
    {
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                string? executablePath = process.MainModule?.FileName;
                if (
                    process.ProcessName.Contains("MobaXterm", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(executablePath)
                )
                {
                    string candidate = Path.Combine(
                        Path.GetDirectoryName(executablePath)!,
                        "MobaXterm.ini"
                    );
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Some processes do not allow querying MainModule.
            }
            finally
            {
                process.Dispose();
            }
        }

        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "MobaXterm.ini"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MobaXterm",
                "MobaXterm.ini"
            ),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private async void RecoverButton_Click(object? sender, EventArgs e)
    {
        string[] arguments;
        if (_iniModeRadio.Checked)
        {
            string iniPath = _iniPathTextBox.Text.Trim().Trim('"');
            if (!File.Exists(iniPath))
            {
                MessageBox.Show(
                    this,
                    "请选择一个有效的 MobaXterm.ini 文件。",
                    "配置文件无效",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }
            arguments = new[] { iniPath };
        }
        else
        {
            arguments = Array.Empty<string>();
        }

        ClearResults();
        SetBusy(true);
        _statusLabel.Text = "正在读取并恢复凭据...";

        void CaptureMessage(string line)
        {
            lock (_resultLock)
            {
                _rawLines.Add(line);
            }
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(RenderResults);
            }
        }

        Logger.MessageLogged += CaptureMessage;
        try
        {
            await Task.Run(() =>
            {
                Logger.Initialize(false);
                new MobaXterm().Run(arguments);
            });

            string[] lines;
            lock (_resultLock)
            {
                lines = _rawLines.ToArray();
            }
            int passwordCount = lines.Count(IsPasswordLine);
            int failedCount = lines.Count(line => line.StartsWith("[-]", StringComparison.Ordinal));
            _statusLabel.Text = failedCount > 0
                ? $"处理结束：显示 {passwordCount} 条，失败 {failedCount} 条；请查看原因"
                : passwordCount > 0 ? $"完成：找到 {passwordCount} 条密码记录" : "完成，但没有找到可显示的密码记录";
        }
        catch (Exception exception)
        {
            Exception rootCause = exception.GetBaseException();
            CaptureMessage($"[-] {rootCause.GetType().Name}: {rootCause.Message}");
            _statusLabel.Text = "恢复失败，请查看结果区域中的错误信息";
            MessageBox.Show(
                this,
                $"恢复失败：\r\n{rootCause.Message}",
                "发生错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            Logger.MessageLogged -= CaptureMessage;
            RenderResults();
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _recoverButton.Enabled = !busy;
        _autoModeRadio.Enabled = !busy;
        _iniModeRadio.Enabled = !busy;
        if (busy)
        {
            _iniPathTextBox.Enabled = false;
            _browseButton.Enabled = false;
            _detectButton.Enabled = false;
            UseWaitCursor = true;
        }
        else
        {
            UseWaitCursor = false;
            UpdateModeControls();
        }
    }

    private void ClearResults()
    {
        lock (_resultLock)
        {
            _rawLines.Clear();
        }
        _resultTextBox.Clear();
        _statusLabel.Text = "就绪";
    }

    private void RenderResults()
    {
        if (InvokeRequired)
        {
            BeginInvoke(RenderResults);
            return;
        }

        string[] lines;
        lock (_resultLock)
        {
            lines = _rawLines.ToArray();
        }

        var builder = new StringBuilder();
        foreach (string line in lines)
        {
            builder.AppendLine(
                _showPasswordsCheckBox.Checked || !IsPasswordLine(line)
                    ? line
                    : MaskPassword(line)
            );
        }
        _resultTextBox.Text = builder.ToString();
    }

    private static bool IsPasswordLine(string line)
    {
        int markerIndex = line.IndexOf("Password:", StringComparison.OrdinalIgnoreCase);
        return markerIndex >= 0
            && !line.Contains("Passwords:", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskPassword(string line)
    {
        int markerIndex = line.IndexOf("Password:", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return line;
        }
        return line[..(markerIndex + "Password:".Length)] + " ••••••••";
    }

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_resultTextBox.Text))
        {
            MessageBox.Show(this, "当前没有可复制的内容。", "提示");
            return;
        }

        Clipboard.SetText(_resultTextBox.Text);
        _statusLabel.Text = _showPasswordsCheckBox.Checked
            ? "已复制包含明文密码的内容，请注意保护剪贴板"
            : "已复制当前显示的脱敏内容";
    }
}
