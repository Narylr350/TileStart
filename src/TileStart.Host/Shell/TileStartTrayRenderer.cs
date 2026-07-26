using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace TileStart.Host.Shell;

internal sealed class TileStartTrayRenderer : Forms.ToolStripProfessionalRenderer
{
    private const int MenuCornerRadius = 8;
    private const int ItemCornerRadius = 4;
    private readonly Drawing.Color _highlightColor;

    internal static readonly Drawing.Color BackgroundColor = Drawing.Color.FromArgb(0xFC, 0x2C, 0x2C, 0x2C);
    internal static readonly Drawing.Color BorderColor = Drawing.Color.FromArgb(0x56, 0x56, 0x56);
    internal static readonly Drawing.Color SeparatorColor = Drawing.Color.FromArgb(0x4A, 0x4A, 0x4A);
    internal static readonly Drawing.Color DisabledTextColor = Drawing.Color.FromArgb(0x78, 0xFF, 0xFF, 0xFF);

    public TileStartTrayRenderer()
        : base(new TileStartTrayColorTable(ToDrawingColor(Win10Theme.ContextMenuHighlightBrush.Color)))
    {
        _highlightColor = ToDrawingColor(Win10Theme.ContextMenuHighlightBrush.Color);
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(e.AffectedBounds, MenuCornerRadius);
        using var brush = new Drawing.SolidBrush(BackgroundColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        var bounds = Drawing.Rectangle.Inflate(e.AffectedBounds, -1, -1);
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(bounds, MenuCornerRadius - 1);
        using var pen = new Drawing.Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed)
        {
            return;
        }

        var bounds = new Drawing.Rectangle(4, 1, Math.Max(1, e.Item.Width - 8), Math.Max(1, e.Item.Height - 2));
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(bounds, ItemCornerRadius);
        using var brush = new Drawing.SolidBrush(_highlightColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Drawing.Color.White : DisabledTextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled != false ? Drawing.Color.White : DisabledTextColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        var bounds = e.ImageRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = new Drawing.Rectangle(6, Math.Max(0, (e.Item.Height - 16) / 2), 16, 16);
        }

        var centerY = bounds.Top + (bounds.Height / 2f);
        using var pen = new Drawing.Pen(Drawing.Color.White, 1.8f)
        {
            StartCap = Drawing2D.LineCap.Round,
            EndCap = Drawing2D.LineCap.Round,
            LineJoin = Drawing2D.LineJoin.Round,
        };
        var previousSmoothingMode = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(
            pen,
            [
                new Drawing.PointF(bounds.Left + 3, centerY),
                new Drawing.PointF(bounds.Left + 7, centerY + 4),
                new Drawing.PointF(bounds.Right - 2, centerY - 5),
            ]);
        e.Graphics.SmoothingMode = previousSmoothingMode;
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Drawing.Pen(SeparatorColor);
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    internal static Drawing.Region CreateRoundedRegion(Drawing.Size size)
    {
        var bounds = new Drawing.Rectangle(Drawing.Point.Empty, size);
        using var path = CreateRoundedPath(bounds, MenuCornerRadius);
        return new Drawing.Region(path);
    }

    private static Drawing2D.GraphicsPath CreateRoundedPath(Drawing.Rectangle bounds, int radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new Drawing2D.GraphicsPath();
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Drawing.Rectangle(bounds.Location, new Drawing.Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Drawing.Color ToDrawingColor(System.Windows.Media.Color color) =>
        Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}

internal sealed class TileStartTrayColorTable(Drawing.Color highlightColor) : Forms.ProfessionalColorTable
{
    public override Drawing.Color ToolStripDropDownBackground => TileStartTrayRenderer.BackgroundColor;
    public override Drawing.Color ImageMarginGradientBegin => TileStartTrayRenderer.BackgroundColor;
    public override Drawing.Color ImageMarginGradientMiddle => TileStartTrayRenderer.BackgroundColor;
    public override Drawing.Color ImageMarginGradientEnd => TileStartTrayRenderer.BackgroundColor;
    public override Drawing.Color MenuBorder => TileStartTrayRenderer.BorderColor;
    public override Drawing.Color MenuItemBorder => highlightColor;
    public override Drawing.Color MenuItemSelected => highlightColor;
    public override Drawing.Color MenuItemSelectedGradientBegin => highlightColor;
    public override Drawing.Color MenuItemSelectedGradientEnd => highlightColor;
    public override Drawing.Color MenuItemPressedGradientBegin => highlightColor;
    public override Drawing.Color MenuItemPressedGradientMiddle => highlightColor;
    public override Drawing.Color MenuItemPressedGradientEnd => highlightColor;
    public override Drawing.Color SeparatorDark => TileStartTrayRenderer.SeparatorColor;
    public override Drawing.Color SeparatorLight => TileStartTrayRenderer.SeparatorColor;
    public override Drawing.Color CheckBackground => TileStartTrayRenderer.BackgroundColor;
    public override Drawing.Color CheckSelectedBackground => highlightColor;
    public override Drawing.Color CheckPressedBackground => highlightColor;
}