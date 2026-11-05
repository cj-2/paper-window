namespace PaperWindow;

/// <summary>
/// Borderless, click-through, always-on-top layered window whose content is a per-pixel-alpha bitmap.
/// Base for the paper texture overlay and the mouse ink layer.
/// </summary>
internal class LayeredWindow : Form
{
    byte _alpha;

    public LayeredWindow(byte alpha = 255)
    {
        _alpha = alpha;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
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

    /// <summary>Shows <paramref name="bmp"/> with its top-left corner at <paramref name="location"/> (screen pixels).</summary>
    public void Present(Bitmap bmp, Point location)
    {
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = Native.SelectObject(memDc, hBmp);
        try
        {
            var size = new Native.SIZE { Cx = bmp.Width, Cy = bmp.Height };
            var src = new Native.POINT();
            var dst = new Native.POINT { X = location.X, Y = location.Y };
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

    /// <summary>Changes the whole window's opacity without re-uploading the bitmap.</summary>
    public void SetOpacity(byte alpha)
    {
        _alpha = alpha;
        var blend = Blend();
        Native.UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0,
            ref blend, Native.ULW_ALPHA);
    }

    /// <summary>
    /// Keeps the window visible on screen but out of screenshots, recordings and screen sharing.
    /// Needs Windows 10 2004+; older builds would treat the flag as WDA_MONITOR (a black box in captures), so skip there.
    /// </summary>
    public void SetCaptureExcluded(bool excluded)
    {
        if (Environment.OSVersion.Version.Build < 19041) return;
        Native.SetWindowDisplayAffinity(Handle, excluded ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE);
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
}
