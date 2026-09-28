namespace EyesRest;

internal sealed class ReminderSession : IDisposable
{
    private readonly BreakReminder _reminder;
    private readonly string? _snoozeText;
    private readonly List<ReminderOverlayForm> _forms = [];
    private readonly System.Windows.Forms.Timer _timer;
    private DateTimeOffset _endsAt;
    private bool _completed;

    public ReminderSession(BreakReminder reminder, string? snoozeText)
    {
        _reminder = reminder;
        _snoozeText = snoozeText;
        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += OnTick;
    }

    public event EventHandler? Completed;

    /// <summary>用户选择稍后再休息；此时不会再触发 Completed。</summary>
    public event EventHandler? Snoozed;

    public void Show()
    {
        _endsAt = DateTimeOffset.Now.Add(_reminder.Duration);

        foreach (var screen in Screen.AllScreens)
        {
            var form = new ReminderOverlayForm(screen.Bounds, _reminder, _snoozeText);
            form.DoneRequested += OnDoneRequested;
            form.SnoozeRequested += OnSnoozeRequested;
            _forms.Add(form);
            form.Show();
        }

        System.Media.SystemSounds.Asterisk.Play();
        UpdateForms();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var remaining = _endsAt - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            Complete();
            return;
        }

        UpdateForms();
    }

    private void UpdateForms()
    {
        var remaining = _endsAt - DateTimeOffset.Now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        foreach (var form in _forms)
        {
            form.SetRemaining(remaining);
        }
    }

    private void OnDoneRequested(object? sender, EventArgs e) => Complete();

    private void OnSnoozeRequested(object? sender, EventArgs e)
    {
        if (CloseForms())
        {
            Snoozed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Complete()
    {
        if (CloseForms())
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool CloseForms()
    {
        if (_completed)
        {
            return false;
        }

        _completed = true;
        _timer.Stop();

        foreach (var form in _forms.ToArray())
        {
            form.DoneRequested -= OnDoneRequested;
            form.SnoozeRequested -= OnSnoozeRequested;
            form.Close();
            form.Dispose();
        }

        _forms.Clear();
        return true;
    }

    public void Dispose()
    {
        _timer.Dispose();

        foreach (var form in _forms.ToArray())
        {
            form.DoneRequested -= OnDoneRequested;
            form.SnoozeRequested -= OnSnoozeRequested;
            form.Dispose();
        }

        _forms.Clear();
    }
}
