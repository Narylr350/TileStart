using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace TileStart.Host.Shell;

internal sealed class TileStartTrayRenderer : Forms.ToolStripProfessionalRenderer
{
    internal static readonly Drawing.Color BackgroundColor = Drawing.Color.FromArgb(0xFC, 0x23, 0x23, 0x23);
    internal static readonly Drawing.Color BorderColor = Drawing.Color.FromArgb(0x45, 0x45, 0x45);
    internal static readonly Drawing.Color SeparatorColor = Drawing.Color.FromArgb(0x58, 0x58, 0x58);
    internal static readonly Drawing.Color DisabledTextColor = Drawing.Color.FromArgb(0x78, 0xFF, 0xFF, 0xFF);

    public TileStartTrayRenderer()
        : base(new TileStartTrayColorTable(ToDrawingColor(Win10Theme.ContextMenuHighlightBrush.Color)))
    {
        RoundedEdges = false;
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