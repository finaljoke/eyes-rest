using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace EyesRest;

internal sealed class ReminderOverlayForm : Form
{
    private readonly BreakReminder _reminder;
    // 弹出瞬间用户可能正在打字，这段时间内的 Esc 和点击不算数，避免误跳过。
    private static readonly TimeSpan InputGrace = TimeSpan.FromSeconds(1.5);

    private readonly Button _doneButton;
    private readonly Button? _snoozeButton;
    private readonly DateTimeOffset _shownAt = DateTimeOffset.Now;
    private TimeSpan _remaining;

    public ReminderOverlayForm(Rectangle bounds, BreakReminder reminder, string? snoozeText)
    {
        _reminder = reminder;
        _remaining = reminder.Duration;

        _doneButton = new RoundedButton
        {
            Text = reminder.CompletionText,
            Size = new Size(176, 46),
            Radius = 18,
            TabStop = false
        };
        AppTheme.StylePrimaryButton(_doneButton);
        _doneButton.Click += (_, _) =>
        {
            if (IsPastInputGrace)
            {
                DoneRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        Controls.Add(_doneButton);

        if (snoozeText is not null)
        {
            _snoozeButton = new RoundedButton
            {
                Text = snoozeText,
                Size = new Size(150, 46),
                Radius = 18,
                TabStop = false
            };
            AppTheme.StyleSecondaryButton(_snoozeButton);
            _snoozeButton.Click += (_, _) =>
            {
                if (IsPastInputGrace)
                {
                    SnoozeRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            Controls.Add(_snoozeButton);
        }

        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = AppTheme.OverlayStart;
        Opacity = 0.97;
        Bounds = bounds;

        Resize += (_, _) => PositionButton();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && IsPastInputGrace)
            {
                DoneRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        PositionButton();
    }

    public event EventHandler? DoneRequested;

    public event EventHandler? SnoozeRequested;

    private bool IsPastInputGrace => DateTimeOffset.Now - _shownAt >= InputGrace;

    public void SetRemaining(TimeSpan remaining)
    {
        _remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        PaintBackdrop(graphics);
        PaintRestCard(graphics);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionButton();
        Invalidate();
    }

    private void PaintBackdrop(Graphics graphics)
    {
        using var backgroundBrush = new LinearGradientBrush(
            ClientRectangle,
            AppTheme.OverlayStart,
            AppTheme.OverlayEnd,
            36F);
        graphics.FillRectangle(backgroundBrush, ClientRectangle);

        PaintHorizonLayer(graphics, ClientSize.Height * 0.66F, Color.FromArgb(46, 86, 143, 138), -90F);
        PaintHorizonLayer(graphics, ClientSize.Height * 0.74F, Color.FromArgb(38, 50, 99, 104), 55F);
        PaintHorizonLayer(graphics, ClientSize.Height * 0.83F, Color.FromArgb(64, 8, 18, 24), 20F);

        using var ambientBrush = new SolidBrush(Color.FromArgb(24, 124, 221, 185));
        graphics.FillEllipse(
            ambientBrush,
            ClientSize.Width / 2F - 380F,
            ClientSize.Height / 2F - 330F,
            760F,
            620F);

        using var sunBrush = new SolidBrush(Color.FromArgb(38, AppTheme.Warning));
        graphics.FillEllipse(
            sunBrush,
            ClientSize.Width - 420F,
            ClientSize.Height * 0.62F - 140F,
            260F,
            260F);

        using var horizonPen = new Pen(Color.FromArgb(48, 190, 225, 205), 1.2F);
        graphics.DrawLine(horizonPen, 0, ClientSize.Height * 0.74F, ClientSize.Width, ClientSize.Height * 0.74F);
    }

    private void PaintHorizonLayer(Graphics graphics, float baseline, Color color, float offset)
    {
        using var path = new GraphicsPath();
        path.StartFigure();
        path.AddBezier(
            -80,
            baseline,
            ClientSize.Width * 0.25F,
            baseline + offset,
            ClientSize.Width * 0.58F,
            baseline - offset * 0.72F,
            ClientSize.Width + 80,
            baseline + offset * 0.35F);
        path.AddLine(ClientSize.Width + 80, ClientSize.Height + 80, -80, ClientSize.Height + 80);
        path.CloseFigure();

        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private void PaintRestCard(Graphics graphics)
    {
        var card = GetCardBounds();
        using var shadowPath = AppTheme.RoundedRectangle(card.OffsetBy(0, 16), AppTheme.LargeRadius);
        using var shadowBrush = new SolidBrush(Color.FromArgb(72, 0, 0, 0));
        graphics.FillPath(shadowBrush, shadowPath);

        using var cardPath = AppTheme.RoundedRectangle(card, AppTheme.LargeRadius);
        using var cardBrush = new LinearGradientBrush(
            card,
            Color.FromArgb(238, 18, 34, 42),
            Color.FromArgb(238, 27, 52, 57),
            LinearGradientMode.Vertical);
        graphics.FillPath(cardBrush, cardPath);

        using var borderPen = new Pen(Color.FromArgb(80, 193, 232, 214), 1F);
        graphics.DrawPath(borderPen, cardPath);

        PaintCardGlow(graphics, card);
        PaintBadge(graphics, card);
        PaintText(graphics, card);
        PaintProgress(graphics, card);
    }

    private static void PaintCardGlow(Graphics graphics, RectangleF card)
    {
        var topLine = new RectangleF(card.X + 32, card.Y + 1, card.Width - 64, 1.5F);
        using var lineBrush = new LinearGradientBrush(
            topLine,
            Color.FromArgb(0, AppTheme.Accent),
            Color.FromArgb(150, AppTheme.Accent),
            LinearGradientMode.Horizontal);
        graphics.FillRectangle(lineBrush, topLine);

        using var softLinePen = new Pen(Color.FromArgb(28, 255, 255, 255), 1F);
        graphics.DrawLine(softLinePen, card.X + 34, card.Y + 70, card.Right - 34, card.Y + 70);
    }

    private void PaintBadge(Graphics graphics, RectangleF card)
    {
        var text = _reminder.Kind == ReminderKind.Long ? "长休息" : "短休息";
        using var font = AppTheme.UiFont(10F, FontStyle.Bold);
        var textSize = graphics.MeasureString(text, font);
        var badge = new RectangleF(card.X + 36, card.Y + 30, textSize.Width + 28, 32);

        using var path = AppTheme.RoundedRectangle(badge, 16);
        using var brush = new SolidBrush(_reminder.Kind == ReminderKind.Long
            ? Color.FromArgb(235, AppTheme.Warning)
            : Color.FromArgb(235, AppTheme.Accent));
        graphics.FillPath(brush, path);

        using var textBrush = new SolidBrush(AppTheme.Ink);
        using var centerFormat = CenterText();
        graphics.DrawString(text, font, textBrush, badge, centerFormat);
    }

    private void PaintText(Graphics graphics, RectangleF card)
    {
        var content = RectangleF.Inflate(card, -36, -26);

        using var titleFont = AppTheme.UiFont(31F, FontStyle.Bold);
        using var subtitleFont = AppTheme.UiFont(15F);
        using var guidanceFont = AppTheme.UiFont(11.5F);
        using var timerFont = new Font("Segoe UI", _reminder.Kind == ReminderKind.Long ? 57F : 68F, FontStyle.Bold, GraphicsUnit.Point);

        using var titleBrush = new SolidBrush(Color.White);
        using var subtitleBrush = new SolidBrush(Color.FromArgb(220, 234, 239));
        using var guidanceBrush = new SolidBrush(Color.FromArgb(174, 204, 211));
        using var timerBrush = new SolidBrush(AppTheme.Accent);

        var titleBounds = new RectangleF(content.X, card.Y + 82, content.Width, 58);
        var subtitleBounds = new RectangleF(content.X, titleBounds.Bottom + 10, content.Width, 34);
        var guidanceBounds = new RectangleF(content.X + 24, subtitleBounds.Bottom + 8, content.Width - 48, 52);
        var timerBounds = new RectangleF(content.X, guidanceBounds.Bottom + 10, content.Width, 96);

        using var centerFormat = CenterText();
        graphics.DrawString(_reminder.Title, titleFont, titleBrush, titleBounds, centerFormat);
        graphics.DrawString(_reminder.Subtitle, subtitleFont, subtitleBrush, subtitleBounds, centerFormat);
        graphics.DrawString(_reminder.Guidance, guidanceFont, guidanceBrush, guidanceBounds, centerFormat);
        graphics.DrawString(FormatRemaining(_remaining), timerFont, timerBrush, timerBounds, centerFormat);
    }

    private void PaintProgress(Graphics graphics, RectangleF card)
    {
        var barWidth = Math.Min(420F, card.Width - 96);
        var bar = new RectangleF(card.X + (card.Width - barWidth) / 2F, card.Bottom - 98, barWidth, 10);
        var elapsedRatio = 1D - (_remaining.TotalMilliseconds / Math.Max(1D, _reminder.Duration.TotalMilliseconds));
        var filledWidth = (float)(bar.Width * Math.Clamp(elapsedRatio, 0D, 1D));

        using var trackPath = AppTheme.RoundedRectangle(bar, 5);
        using var trackBrush = new SolidBrush(Color.FromArgb(72, 255, 255, 255));
        graphics.FillPath(trackBrush, trackPath);

        if (filledWidth <= 0)
        {
            return;
        }

        var fill = new RectangleF(bar.X, bar.Y, filledWidth, bar.Height);
        using var fillPath = AppTheme.RoundedRectangle(fill, 5);
        using var fillBrush = new LinearGradientBrush(
            bar,
            AppTheme.Accent,
            AppTheme.Warning,
            LinearGradientMode.Horizontal);
        graphics.FillPath(fillBrush, fillPath);
    }

    private RectangleF GetCardBounds()
    {
        var width = Math.Min(760, Math.Max(520, ClientSize.Width - 120));
        var height = Math.Min(520, Math.Max(430, ClientSize.Height - 120));
        return new RectangleF(
            (ClientSize.Width - width) / 2F,
            (ClientSize.Height - height) / 2F,
            width,
            height);
    }

    private void PositionButton()
    {
        if (_doneButton is null)
        {
            return;
        }

        var card = GetCardBounds();
        var top = (int)Math.Round(card.Bottom - 72);
        if (_snoozeButton is null)
        {
            _doneButton.Location = new Point((int)Math.Round(card.X + (card.Width - _doneButton.Width) / 2F), top);
            return;
        }

        const int gap = 16;
        var left = card.X + (card.Width - _doneButton.Width - gap - _snoozeButton.Width) / 2F;
        _doneButton.Location = new Point((int)Math.Round(left), top);
        _snoozeButton.Location = new Point(_doneButton.Right + gap, top);
    }

    private string FormatRemaining(TimeSpan remaining)
    {
        if (_reminder.Kind == ReminderKind.Long)
        {
            return $"{Math.Max(0, (int)remaining.TotalMinutes):D2}:{Math.Max(0, remaining.Seconds):D2}";
        }

        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        return seconds.ToString("D2");
    }

    private static StringFormat CenterText()
    {
        return new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord
        };
    }

}

internal static class RectangleExtensions
{
    public static RectangleF OffsetBy(this RectangleF rectangle, float dx, float dy)
    {
        return new RectangleF(rectangle.X + dx, rectangle.Y + dy, rectangle.Width, rectangle.Height);
    }
}
