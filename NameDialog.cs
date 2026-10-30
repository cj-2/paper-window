namespace PaperWindow;

/// <summary>Small modal prompt for a profile name.</summary>
internal sealed class NameDialog : Form
{
    readonly TextBox _name = new() { Width = 280 };

    NameDialog(string title, string prompt, string initial)
    {
        Text = title;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _name.Text = initial;
        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([cancel, ok]);

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.AddRange([new Label { Text = prompt, AutoSize = true }, _name, buttons]);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>Returns the trimmed name, or null if cancelled or left empty.</summary>
    public static string? Ask(IWin32Window owner, string title, string prompt, string initial)
    {
        using var dialog = new NameDialog(title, prompt, initial);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        string name = dialog._name.Text.Trim();
        return name.Length > 0 ? name : null;
    }
}
