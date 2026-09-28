using Microsoft.Win32;

namespace EyesRest;

internal sealed class EyeRestApplicationContext : ApplicationContext
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryName = "EyesRest";
    private const int MaxSnoozesPerBreak = 2;

    private static readonly TimeSpan MicroSnooze = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongSnooze = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MicroHeadsUp = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LongHeadsUp = TimeSpan.FromSeconds(60);
    // 全屏/演示时推迟提醒，但最多推迟这么久，防止整天挂着全屏视频永远不休息。
    private static readonly TimeSpan MaxFullscreenDeferral = TimeSpan.FromMinutes(30);

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _shortRestItem;
    private readonly ToolStripMenuItem _longRestItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _resumeItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PresenceMonitor _presence = new();

    private EyeCareSettings _settings;
    private DateTimeOffset _lastTick = DateTimeOffset.Now;
    private DateTimeOffset? _pausedUntil;
    private ReminderSession? _reminder;
    private SettingsForm? _settingsForm;
    private ReminderKind? _activeReminderKind;
    private TimeSpan _microWorkElapsed = TimeSpan.Zero;
    private TimeSpan _longWorkElapsed = TimeSpan.Zero;
    private DateTimeOffset? _snoozedUntil;
    private int _snoozeCount;
    private DateTimeOffset? _deferredSince;
    private bool _headsUpShown;

    public EyeRestApplicationContext()
    {
        _settings = EyeCareSettings.Load();
        EnsureStartupPreference();

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _shortRestItem = new ToolStripMenuItem("立即短休息", null, (_, _) => ShowReminder(ReminderKind.Micro, automatic: false));
        _longRestItem = new ToolStripMenuItem("立即长休息", null, (_, _) => ShowReminder(ReminderKind.Long, automatic: false));
        _pauseItem = new ToolStripMenuItem("暂停 1 小时", null, (_, _) => PauseFor(TimeSpan.FromHours(1)));
        _resumeItem = new ToolStripMenuItem("继续计时", null, (_, _) => ResumeTimer());
        _settingsItem = new ToolStripMenuItem("设置...", null, (_, _) => ShowSettings());
        _startupItem = new ToolStripMenuItem("开机自启", null, (_, _) => ToggleStartup());
        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => ExitApplication());

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
            new ToolStripItem[]
            {
                _statusItem,
                new ToolStripSeparator(),
                _shortRestItem,
                _longRestItem,
                _pauseItem,
                _resumeItem,
                new ToolStripSeparator(),
                _settingsItem,
                _startupItem,
                new ToolStripSeparator(),
                exitItem
            });
        _menu.Opening += (_, _) => RefreshTrayState();

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = TrayIconFactory.Create(),
            Text = "EyesRest 护眼提醒",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowReminder(ReminderKind.Micro, automatic: false);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        RefreshTrayState();
        _trayIcon.ShowBalloonTip(
            timeout: 3000,
            tipTitle: "EyesRest 最佳护眼模式已启动",
            tipText: $"每 {_settings.MicroBreakWorkMinutes} 分钟远眺 {_settings.MicroBreakSeconds} 秒，每 {_settings.LongBreakWorkMinutes / 60.0:0.#} 小时离屏 {_settings.LongBreakMinutes} 分钟。",
            tipIcon: ToolTipIcon.Info);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.Now;
        var elapsed = now - _lastTick;
        _lastTick = now;

        if (_reminder is not null)
        {
            RefreshTrayState();
            return;
        }

        if (_pausedUntil is { } pausedUntil)
        {
            if (now < pausedUntil)
            {
                RefreshTrayState();
                return;
            }

            _pausedUntil = null;
            ResetWorkTimers();
        }

        // 睡眠/休眠醒来：这段时间人不在屏幕前，按休息计。
        if (elapsed >= TimeSpan.FromMinutes(1))
        {
            ApplyRest(elapsed);
        }

        // 只有锁屏或熄屏才算离开；不碰键鼠（看文档、看视频、开会）照常计入用眼时间。
        if (_presence.AwayDuration is { } away)
        {
            ApplyRest(away);
            RefreshTrayState();
            return;
        }

        if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromMinutes(1))
        {
            _microWorkElapsed += elapsed;
            _longWorkElapsed += elapsed;
        }

        if (GetDueKind() is { } due)
        {
            if (_snoozedUntil is { } snoozedUntil && now < snoozedUntil)
            {
                RefreshTrayState();
                return;
            }

            if (PresenceMonitor.IsFullscreenBusy)
            {
                _deferredSince ??= now;
                if (now - _deferredSince.Value < MaxFullscreenDeferral)
                {
                    RefreshTrayState();
                    return;
                }
            }

            ShowReminder(due, automatic: true);
            return;
        }

        ShowHeadsUpIfNear();
        RefreshTrayState();
    }

    private ReminderKind? GetDueKind()
    {
        if (_settings.LongBreaksEnabled && _longWorkElapsed >= _settings.LongBreakWorkInterval)
        {
            return ReminderKind.Long;
        }

        return _microWorkElapsed >= _settings.MicroBreakWorkInterval ? ReminderKind.Micro : null;
    }

    private (ReminderKind Kind, TimeSpan Remaining) GetNextReminder()
    {
        var microRemaining = ClampRemaining(_settings.MicroBreakWorkInterval - _microWorkElapsed);
        if (_settings.LongBreaksEnabled)
        {
            var longRemaining = ClampRemaining(_settings.LongBreakWorkInterval - _longWorkElapsed);
            if (longRemaining <= microRemaining)
            {
                return (ReminderKind.Long, longRemaining);
            }
        }

        return (ReminderKind.Micro, microRemaining);
    }

    private void ShowHeadsUpIfNear()
    {
        if (_headsUpShown || PresenceMonitor.IsFullscreenBusy)
        {
            return;
        }

        var (kind, remaining) = GetNextReminder();
        var lead = kind == ReminderKind.Long ? LongHeadsUp : MicroHeadsUp;
        if (remaining > lead)
        {
            return;
        }

        _headsUpShown = true;
        _trayIcon.ShowBalloonTip(
            timeout: 5000,
            tipTitle: kind == ReminderKind.Long ? $"{(int)lead.TotalSeconds} 秒后长休息" : $"{(int)lead.TotalSeconds} 秒后远眺",
            tipText: "先把手头这句写完，到时也可以选择稍后。",
            tipIcon: ToolTipIcon.Info);
    }

    private void ApplyRest(TimeSpan rest)
    {
        if (rest >= _settings.LongBreakDuration)
        {
            ResetWorkTimers();
        }
        else if (rest >= _settings.IdleResetThreshold)
        {
            ResetMicroTimer();
        }
    }

    private void ShowReminder(ReminderKind kind, bool automatic)
    {
        if (_reminder is not null)
        {
            return;
        }

        _timer.Stop();
        _pausedUntil = null;
        _activeReminderKind = kind;

        string? snoozeText = null;
        if (automatic && _snoozeCount < MaxSnoozesPerBreak)
        {
            var snooze = kind == ReminderKind.Long ? LongSnooze : MicroSnooze;
            snoozeText = $"稍后 {(int)snooze.TotalMinutes} 分钟";
        }

        _reminder = new ReminderSession(BreakReminder.Create(kind, _settings), snoozeText);
        _reminder.Completed += OnReminderCompleted;
        _reminder.Snoozed += OnReminderSnoozed;
        _reminder.Show();

        RefreshTrayState();
    }

    private void OnReminderCompleted(object? sender, EventArgs e)
    {
        var kind = _activeReminderKind;
        EndReminder();

        if (kind == ReminderKind.Long)
        {
            ResetWorkTimers();
        }
        else
        {
            ResetMicroTimer();
        }

        RefreshTrayState();
    }

    private void OnReminderSnoozed(object? sender, EventArgs e)
    {
        var kind = _activeReminderKind;
        EndReminder();

        // 不清零计时：到点后补上这次欠下的休息。
        _snoozeCount++;
        _snoozedUntil = DateTimeOffset.Now + (kind == ReminderKind.Long ? LongSnooze : MicroSnooze);
        _deferredSince = null;
        RefreshTrayState();
    }

    private void EndReminder()
    {
        if (_reminder is not null)
        {
            _reminder.Completed -= OnReminderCompleted;
            _reminder.Snoozed -= OnReminderSnoozed;
            _reminder.Dispose();
            _reminder = null;
        }

        _activeReminderKind = null;
        _lastTick = DateTimeOffset.Now;
        _timer.Start();
    }

    private void PauseFor(TimeSpan duration)
    {
        if (_reminder is not null)
        {
            return;
        }

        _pausedUntil = DateTimeOffset.Now.Add(duration);
        ResetWorkTimers();
        RefreshTrayState();
    }

    private void ResumeTimer()
    {
        _pausedUntil = null;
        ResetWorkTimers();
        _lastTick = DateTimeOffset.Now;
        RefreshTrayState();
    }

    private void ToggleStartup()
    {
        var startupEnabled = IsStartupEnabled();
        var targetEnabled = !startupEnabled;
        if (!TrySetStartupEnabled(targetEnabled))
        {
            return;
        }

        _settings.StartupPreferenceConfigured = true;
        _settings.Save();
        RefreshTrayState();
    }

    private void ShowSettings()
    {
        if (_reminder is not null)
        {
            return;
        }

        if (_settingsForm is not null)
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings, IsStartupEnabled());
        _settingsForm.SettingsSaved += OnSettingsSaved;
        _settingsForm.FormClosed += OnSettingsFormClosed;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsSaved(object? sender, SettingsSavedEventArgs e)
    {
        _settings = e.Settings;
        var startupUpdated = TrySetStartupEnabledSilently(e.StartupEnabled);
        _settings.StartupPreferenceConfigured = true;
        _settings.Save();
        ResetWorkTimers();
        _lastTick = DateTimeOffset.Now;

        RefreshTrayState();
        _trayIcon.ShowBalloonTip(
            timeout: 1800,
            tipTitle: startupUpdated ? "EyesRest 设置已保存" : "EyesRest 提醒设置已保存",
            tipText: startupUpdated
                ? "新的提醒节奏已生效，本轮计时已重新开始。"
                : "新的提醒节奏已生效，但 Windows 自启项没有更新。",
            tipIcon: startupUpdated ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private void OnSettingsFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_settingsForm is not null)
        {
            _settingsForm.SettingsSaved -= OnSettingsSaved;
            _settingsForm.FormClosed -= OnSettingsFormClosed;
            _settingsForm.Dispose();
            _settingsForm = null;
        }
    }

    private void EnsureStartupPreference()
    {
        if (_settings.StartupPreferenceConfigured)
        {
            return;
        }

        if (TrySetStartupEnabledSilently(enabled: true))
        {
            _settings.StartupPreferenceConfigured = true;
            _settings.Save();
        }
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: false);
            var value = key?.GetValue(StartupRegistryName) as string;
            return string.Equals(value, Quote(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(StartupRegistryPath, writable: true);

        if (enabled)
        {
            key.SetValue(StartupRegistryName, Quote(Application.ExecutablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(StartupRegistryName, throwOnMissingValue: false);
        }
    }

    private bool TrySetStartupEnabled(bool enabled)
    {
        if (TrySetStartupEnabledSilently(enabled))
        {
            return true;
        }

        _trayIcon.ShowBalloonTip(
            timeout: 2200,
            tipTitle: "EyesRest 无法修改开机自启",
            tipText: "提醒设置已保存，但 Windows 自启项没有更新。",
            tipIcon: ToolTipIcon.Warning);
        return false;
    }

    private static bool TrySetStartupEnabledSilently(bool enabled)
    {
        try
        {
            SetStartupEnabled(enabled);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RefreshTrayState()
    {
        var statusText = GetStatusText();
        _statusItem.Text = statusText;
        _shortRestItem.Enabled = _reminder is null;
        _longRestItem.Enabled = _reminder is null;
        _pauseItem.Enabled = _pausedUntil is null && _reminder is null;
        _resumeItem.Enabled = _pausedUntil is not null && _reminder is null;
        _settingsItem.Enabled = _reminder is null;
        _startupItem.Checked = IsStartupEnabled();
        _trayIcon.Text = statusText.Length <= 63 ? statusText : "EyesRest 护眼提醒";
    }

    private string GetStatusText()
    {
        if (_reminder is not null)
        {
            return _activeReminderKind == ReminderKind.Long ? "EyesRest：正在长休息" : "EyesRest：正在远眺";
        }

        if (_pausedUntil is { } pausedUntil && DateTimeOffset.Now < pausedUntil)
        {
            return $"EyesRest：已暂停，{FormatRemaining(pausedUntil - DateTimeOffset.Now)} 后继续";
        }

        if (_presence.AwayDuration is not null)
        {
            return "EyesRest：已离开屏幕，按休息计";
        }

        if (_snoozedUntil is { } snoozedUntil && DateTimeOffset.Now < snoozedUntil)
        {
            return $"EyesRest：已延后，{FormatRemaining(snoozedUntil - DateTimeOffset.Now)} 后提醒";
        }

        if (_deferredSince is not null)
        {
            return "EyesRest：全屏中，退出后提醒";
        }

        var (kind, remaining) = GetNextReminder();
        return kind == ReminderKind.Long
            ? $"EyesRest：{FormatRemaining(remaining)} 后长休息"
            : $"EyesRest：{FormatRemaining(remaining)} 后远眺";
    }

    private static TimeSpan ClampRemaining(TimeSpan remaining)
    {
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        }

        return $"00:{Math.Max(0, remaining.Seconds):D2}";
    }

    private void ResetWorkTimers()
    {
        _longWorkElapsed = TimeSpan.Zero;
        ResetMicroTimer();
    }

    private void ResetMicroTimer()
    {
        _microWorkElapsed = TimeSpan.Zero;
        _snoozedUntil = null;
        _snoozeCount = 0;
        _deferredSince = null;
        _headsUpShown = false;
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _settingsForm?.Close();
        _reminder?.Dispose();
        _trayIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _presence.Dispose();
            _reminder?.Dispose();
            _settingsForm?.Dispose();
            _trayIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string Quote(string path) => $"\"{path}\"";
}
