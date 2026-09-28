namespace EyesRest;

internal sealed class SettingsSavedEventArgs(EyeCareSettings settings, bool startupEnabled) : EventArgs
{
    public EyeCareSettings Settings { get; } = settings;

    public bool StartupEnabled { get; } = startupEnabled;
}

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _microWorkMinutes;
    private readonly NumericUpDown _microBreakSeconds;
    private readonly CheckBox _longBreaksEnabled;
    private readonly NumericUpDown _longWorkMinutes;
    private readonly NumericUpDown _longBreakMinutes;
    private readonly NumericUpDown _idleResetMinutes;
    private readonly CheckBox _startupEnabled;

    public SettingsForm(EyeCareSettings settings, bool startupEnabled)
    {
        Text = "EyesRest 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 640);
        BackColor = AppTheme.Surface;
        ForeColor = AppTheme.Ink;
        Font = AppTheme.UiFont(9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface,
            Padding = new Padding(26),
            ColumnCount = 1,
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);

        var surface = new RoundedSurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 22, 18)
        };
        root.Controls.Add(surface, 0, 1);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 11
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        for (var i = 0; i < table.RowCount; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / table.RowCount));
        }
        surface.Controls.Add(table);

        AddSectionHeader(table, 0, "短休息", "高频、低打扰，用来放松近距离聚焦");
        _microWorkMinutes = AddNumberRow(table, 1, "提醒间隔", "分钟", 5, 60, settings.MicroBreakWorkMinutes);
        _microBreakSeconds = AddNumberRow(table, 2, "休息时长", "秒", 10, 120, settings.MicroBreakSeconds);

        AddSectionHeader(table, 3, "长休息", "低频、完整离屏，用来恢复肩颈和专注力");
        _longBreaksEnabled = AddCheckRow(table, 4, "启用长休息提醒", settings.LongBreaksEnabled);
        _longWorkMinutes = AddNumberRow(table, 5, "提醒间隔", "分钟", 60, 240, settings.LongBreakWorkMinutes);
        _longBreakMinutes = AddNumberRow(table, 6, "休息时长", "分钟", 2, 20, settings.LongBreakMinutes);

        AddSectionHeader(table, 7, "系统行为", "锁屏或熄屏才算离开屏幕");
        _idleResetMinutes = AddNumberRow(table, 8, "离开多久算休息", "分钟", 1, 15, settings.IdleResetMinutes);
        _startupEnabled = AddCheckRow(table, 9, "开机后自动启动 EyesRest", startupEnabled);

        var restoreButton = new RoundedButton
        {
            Text = "恢复推荐值",
            Width = 128,
            Height = 36,
            Anchor = AnchorStyles.Right,
            Radius = 14
        };
        AppTheme.StyleSecondaryButton(restoreButton);
        restoreButton.Click += (_, _) => ApplySettings(EyeCareSettings.CreateDefault(), startupEnabled);
        table.Controls.Add(restoreButton, 1, 10);

        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = "推荐值遵循 20-20-20 短休息，并加入每 2 小时离屏长休息。",
            ForeColor = AppTheme.InkSoft,
            Font = AppTheme.UiFont(9F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(note, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 6, 0, 0)
        };
        root.Controls.Add(actions, 0, 3);

        var saveButton = new RoundedButton
        {
            Text = "保存",
            Width = 104,
            Height = 38,
            Radius = 15
        };
        AppTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new RoundedButton
        {
            Text = "取消",
            Width = 104,
            Height = 38,
            Radius = 15
        };
        AppTheme.StyleSecondaryButton(cancelButton);
        cancelButton.Click += (_, _) => Close();

        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        _longBreaksEnabled.CheckedChanged += (_, _) => RefreshLongBreakControls();
        RefreshLongBreakControls();
    }

    public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;

    private static Control CreateHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Surface
        };

        var mark = new HeaderMark
        {
            Location = new Point(0, 8),
            Size = new Size(52, 52)
        };

        var title = new Label
        {
            Text = "EyesRest 设置",
            AutoSize = false,
            Location = new Point(68, 5),
            Size = new Size(420, 34),
            Font = AppTheme.UiFont(18F, FontStyle.Bold),
            ForeColor = AppTheme.Ink
        };

        var subtitle = new Label
        {
            Text = "调整提醒节奏，保存后会重新开始本轮计时。",
            AutoSize = false,
            Location = new Point(70, 42),
            Size = new Size(460, 26),
            Font = AppTheme.UiFont(9.5F),
            ForeColor = AppTheme.InkSoft
        };

        panel.Controls.Add(mark);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        return panel;
    }

    private static void AddSectionHeader(TableLayoutPanel table, int row, string title, string subtitle)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, row == 0 ? 0 : 7, 0, 0)
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Location = new Point(0, row == 0 ? 1 : 8),
            Size = new Size(78, 22),
            Font = AppTheme.UiFont(10.5F, FontStyle.Bold),
            ForeColor = AppTheme.Ink
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = false,
            Location = new Point(84, row == 0 ? 2 : 9),
            Size = new Size(360, 22),
            Font = AppTheme.UiFont(8.6F),
            ForeColor = AppTheme.InkSoft
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(subtitleLabel);
        table.Controls.Add(panel, 0, row);
        table.SetColumnSpan(panel, 2);
    }

    private static NumericUpDown AddNumberRow(
        TableLayoutPanel table,
        int row,
        string labelText,
        string suffix,
        decimal minimum,
        decimal maximum,
        decimal value)
    {
        var label = new Label
        {
            Text = $"{labelText}（{suffix}）",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.UiFont(9.8F, FontStyle.Bold),
            ForeColor = AppTheme.Ink
        };

        var input = new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
            Dock = DockStyle.Right,
            Width = 118,
            TextAlign = HorizontalAlignment.Right,
            Font = AppTheme.UiFont(10.5F),
            BackColor = Color.White,
            ForeColor = AppTheme.Ink
        };

        table.Controls.Add(label, 0, row);
        table.Controls.Add(input, 1, row);
        return input;
    }

    private static CheckBox AddCheckRow(TableLayoutPanel table, int row, string text, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = AppTheme.UiFont(10F, FontStyle.Bold),
            ForeColor = AppTheme.Ink,
            AutoSize = false
        };

        table.Controls.Add(checkBox, 0, row);
        table.SetColumnSpan(checkBox, 2);
        return checkBox;
    }

    private void ApplySettings(EyeCareSettings settings, bool startupEnabled)
    {
        _microWorkMinutes.Value = settings.MicroBreakWorkMinutes;
        _microBreakSeconds.Value = settings.MicroBreakSeconds;
        _longBreaksEnabled.Checked = settings.LongBreaksEnabled;
        _longWorkMinutes.Value = settings.LongBreakWorkMinutes;
        _longBreakMinutes.Value = settings.LongBreakMinutes;
        _idleResetMinutes.Value = settings.IdleResetMinutes;
        _startupEnabled.Checked = startupEnabled;
    }

    private void RefreshLongBreakControls()
    {
        _longWorkMinutes.Enabled = _longBreaksEnabled.Checked;
        _longBreakMinutes.Enabled = _longBreaksEnabled.Checked;
    }

    private void SaveAndClose()
    {
        var updated = new EyeCareSettings
        {
            MicroBreakWorkMinutes = (int)_microWorkMinutes.Value,
            MicroBreakSeconds = (int)_microBreakSeconds.Value,
            LongBreaksEnabled = _longBreaksEnabled.Checked,
            LongBreakWorkMinutes = (int)_longWorkMinutes.Value,
            LongBreakMinutes = (int)_longBreakMinutes.Value,
            IdleResetMinutes = (int)_idleResetMinutes.Value,
            StartupPreferenceConfigured = true
        };

        SettingsSaved?.Invoke(this, new SettingsSavedEventArgs(updated, _startupEnabled.Checked));
        Close();
    }

    private sealed class RoundedSurfacePanel : Panel
    {
        public RoundedSurfacePanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = AppTheme.RoundedRectangle(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), AppTheme.MediumRadius);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle,
                Color.White,
                AppTheme.SurfaceGlass,
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            using var pen = new Pen(AppTheme.Line);

            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class HeaderMark : Control
    {
        public HeaderMark()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                bounds,
                AppTheme.Accent,
                Color.FromArgb(58, 126, 178),
                45F);
            e.Graphics.FillEllipse(bg, bounds);

            using var eyePen = new Pen(Color.White, 2.2F);
            e.Graphics.DrawArc(eyePen, 13, 19, 26, 15, 200, 140);
            e.Graphics.DrawArc(eyePen, 13, 19, 26, 15, 20, 140);

            using var eyeBrush = new SolidBrush(Color.FromArgb(244, 255, 255, 255));
            using var pupilBrush = new SolidBrush(AppTheme.Ink);
            e.Graphics.FillEllipse(eyeBrush, 18, 22, 16, 10);
            e.Graphics.FillEllipse(pupilBrush, 23, 23, 6, 6);
        }
    }
}
