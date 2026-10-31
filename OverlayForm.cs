namespace PaperWindow;

/// <summary>Layered window that draws the paper grain over one monitor.</summary>
internal sealed class OverlayForm : LayeredWindow
{
    readonly Rectangle _bounds;
    readonly TextureStyle _style;

    public OverlayForm(Rectangle bounds, TextureStyle style, byte alpha) : base(alpha)
    {
        _bounds = bounds;
        _style = style;
        Bounds = bounds;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using var bmp = PaperTexture.Create(_style, _bounds.Width, _bounds.Height);
        Present(bmp, _bounds.Location);
    }
}
