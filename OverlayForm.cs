namespace PaperWindow;

/// <summary>Borderless, click-through, always-on-top layered window that draws the paper grain over one monitor.</summary>
internal sealed class OverlayForm : Form
{
    readonly Rectangle _bounds;
    readonly TextureStyle _style;
    byte _alpha;

    public OverlayForm(Rectangle bounds, TextureStyle style, byte alpha)
    {
        _bounds = bounds;
        _style = style;
        _alpha = alpha;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                        | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        using var bmp = PaperTexture.Create(_style, _bounds.Width, _bounds.Height);
        ApplyBitmap(bmp);
    }

    /// <summary>Changes the whole window's opacity without re-uploading the texture.</summary>
    public void SetOpacity(byte alpha)
    {
        _alpha = alpha;
        var blend = Blend();
        Native.UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0,
            ref blend, Native.ULW_ALPHA);
    }

    /// <summary>Taskbar/other topmost windows can climb above us; call periodically to stay on top.</summary>
    public void KeepOnTop() =>
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

    Native.BLENDFUNCTION Blend() => new()
    {
        BlendOp = Native.AC_SRC_OVER,
        SourceConstantAlpha = _alpha,
        AlphaFormat = Native.AC_SRC_ALPHA,
    };

    void ApplyBitmap(Bitmap bmp)
    {
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBmp);
        try
        {
            var size = new Native.SIZE { Cx = bmp.Width, Cy = bmp.Height };
            var src = new Native.POINT();
            var dst = new Native.POINT { X = _bounds.X, Y = _bounds.Y };
            var blend = Blend();
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
