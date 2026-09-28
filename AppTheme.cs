using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace EyesRest;

internal static class AppTheme
{
    public static readonly Color Ink = Color.FromArgb(16, 30, 35);
    public static readonly Color InkSoft = Color.FromArgb(74, 101, 108);
    public static readonly Color Surface = Color.FromArgb(248, 252, 250);
    public static readonly Color SurfaceMuted = Color.FromArgb(232, 243, 240);
    public static readonly Color SurfaceWarm = Color.FromArgb(246, 249, 244);
    public static readonly Color SurfaceGlass = Color.FromArgb(236, 247, 243);
    public static readonly Color Line = Color.FromArgb(204, 225, 219);
    public static readonly Color Accent = Color.FromArgb(85, 190, 157);
    public static readonly Color AccentHover = Color.FromArgb(110, 207, 176);
    public static readonly Color AccentDown = Color.FromArgb(64, 166, 136);
    public static readonly Color Warning = Color.FromArgb(235, 177, 92);
    public static readonly Color OverlayStart = Color.FromArgb(10, 20, 26);
    public static readonly Color OverlayEnd = Color.FromArgb(22, 48, 53);

    public const string UiFontName = "Microsoft YaHei UI";
    public const int SmallRadius = 10;
    public const int MediumRadius = 18;
    public const int LargeRadius = 26;

    public static Font UiFont(float size, FontStyle style = FontStyle.Regular) =>
        new(UiFontName, size, style, GraphicsUnit.Point);

    public static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2F, Math.Min(rectangle.Width, rectangle.Height));

        if (diameter <= 0)
        {
            path.AddRectangle(rectangle);
            return path;
        }

        var arc = new RectangleF(rectangle.Location, new SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);

        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);

        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    public static void StylePrimaryButton(Button button)
    {
        if (button is RoundedButton roundedButton)
        {
            roundedButton.BaseColor = Accent;
            roundedButton.HoverColor = AccentHover;
            roundedButton.PressedColor = AccentDown;
            roundedButton.BorderColor = Color.Transparent;
            roundedButton.ForeColor = Ink;
            roundedButton.Font = UiFont(10.5F, FontStyle.Bold);
            return;
        }

        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = Accent;
        button.ForeColor = Ink;
        button.Font = UiFont(10.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = true;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentDown;
    }

    public static void StyleSecondaryButton(Button button)
    {
        if (button is RoundedButton roundedButton)
        {
            roundedButton.BaseColor = SurfaceMuted;
            roundedButton.HoverColor = Color.FromArgb(220, 238, 232);
            roundedButton.PressedColor = Color.FromArgb(202, 225, 218);
            roundedButton.BorderColor = Line;
            roundedButton.ForeColor = Ink;
            roundedButton.Font = UiFont(10.5F, FontStyle.Bold);
            return;
        }

        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = SurfaceMuted;
        button.ForeColor = Ink;
        button.Font = UiFont(10.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.TabStop = true;
        button.FlatAppearance.BorderColor = Line;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 238, 232);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(202, 225, 218);
    }
}

internal sealed class RoundedButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            value: true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Radius = AppTheme.SmallRadius;
        BaseColor = AppTheme.Accent;
        HoverColor = AppTheme.AccentHover;
        PressedColor = AppTheme.AccentDown;
        DisabledColor = Color.FromArgb(224, 232, 229);
        BorderColor = Color.Transparent;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BaseColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DisabledColor { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ResolveBackColor());

        var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        using var path = AppTheme.RoundedRectangle(bounds, Radius);
        using var fillBrush = new SolidBrush(GetFillColor());
        e.Graphics.FillPath(fillBrush, path);

        if (BorderColor.A > 0)
        {
            using var borderPen = new Pen(BorderColor);
            e.Graphics.DrawPath(borderPen, path);
        }

        var textColor = Enabled ? ForeColor : AppTheme.InkSoft;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            var focusBounds = Rectangle.Inflate(ClientRectangle, -5, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, focusBounds);
        }
    }

    private Color GetFillColor()
    {
        if (!Enabled)
        {
            return DisabledColor;
        }

        if (_isPressed)
        {
            return PressedColor;
        }

        return _isHovered ? HoverColor : BaseColor;
    }

    private Color ResolveBackColor()
    {
        for (var control = Parent; control is not null; control = control.Parent)
        {
            if (control.BackColor.A == 255)
            {
                return control.BackColor;
            }
        }

        return AppTheme.Surface;
    }
}
