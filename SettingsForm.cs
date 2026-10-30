namespace PaperWindow;

/// <summary>Settings window. Every change is applied live through <c>onChange</c>; settings are saved on close.</summary>
internal sealed class SettingsForm : Form
{
    readonly Settings _settings;
    readonly Action _onChange;

    readonly CheckBox _enabled = new() { Text = "Efeito ativo", AutoSize = true };
    readonly ComboBox _effect = Combo(ColorEffects.All.Select(e => e.Name));
    readonly TrackBar _strength = Slider();
    readonly Label _strengthValue = ValueLabel();
    readonly ComboBox _texture = Combo(TextureStyles.All.Select(t => t.Name));
    readonly TrackBar _opacity = Slider();
    readonly Label _opacityValue = ValueLabel();
    readonly CheckBox _startup = new() { Text = "Iniciar com o Windows", AutoSize = true };
    bool _loading;

    public SettingsForm(Settings settings, Action onChange)
    {
        _settings = settings;
        _onChange = onChange;

        Text = "Paper Window — Configurações";
        Icon = AppIcon.Get(SystemInformation.IconSize);
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));

        AddRow(grid, null, _enabled);
        AddRow(grid, "Efeito de cor", _effect);
        AddRow(grid, "Intensidade do filtro", _strength, _strengthValue);
        AddRow(grid, "Textura do papel", _texture);
        AddRow(grid, "Opacidade da textura", _opacity, _opacityValue);
        AddRow(grid, null, _startup);

        var reset = new Button { Text = "Restaurar padrão", AutoSize = true };
        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(reset);
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 3);
        Controls.Add(grid);
        AcceptButton = CancelButton = close;

        reset.Click += (_, _) => { _settings.CopyFrom(new Settings()); RefreshControls(); _onChange(); };
        close.Click += (_, _) => Close();
        _enabled.CheckedChanged += (_, _) => Changed(() => _settings.Enabled = _enabled.Checked);
        _effect.SelectedIndexChanged += (_, _) => Changed(() => _settings.Effect = ColorEffects.All[_effect.SelectedIndex].Id);
        _strength.ValueChanged += (_, _) => Changed(() => _settings.FilterStrength = _strength.Value);
        _texture.SelectedIndexChanged += (_, _) => Changed(() => _settings.Texture = TextureStyles.All[_texture.SelectedIndex].Id);
        _opacity.ValueChanged += (_, _) => Changed(() => _settings.TextureOpacity = _opacity.Value);
        _startup.CheckedChanged += (_, _) => { if (!_loading) Startup.IsEnabled = _startup.Checked; };

        RefreshControls();
    }

    /// <summary>Pulls current values into the controls (e.g. after the tray toggled the effect).</summary>
    public void RefreshControls()
    {
        _loading = true;
        _enabled.Checked = _settings.Enabled;
        _effect.SelectedIndex = Array.IndexOf(ColorEffects.All, ColorEffects.Find(_settings.Effect));
        _strength.Value = Math.Clamp(_settings.FilterStrength, 0, 100);
        _texture.SelectedIndex = Array.IndexOf(TextureStyles.All, TextureStyles.Find(_settings.Texture));
        _opacity.Value = Math.Clamp(_settings.TextureOpacity, 0, 100);
        _startup.Checked = Startup.IsEnabled;
        _loading = false;
        UpdateLabels();
        UpdateEnabledState();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _settings.Save();
        base.OnFormClosed(e);
    }

    void Changed(Action update)
    {
        if (_loading) return;
        update();
        UpdateLabels();
        UpdateEnabledState();
        _onChange();
    }

    void UpdateLabels()
    {
        _strengthValue.Text = $"{_strength.Value}%";
        _opacityValue.Text = $"{_opacity.Value}%";
    }

    void UpdateEnabledState()
    {
        foreach (Control c in new Control[] { _effect, _strength, _texture, _opacity })
            c.Enabled = _settings.Enabled;
    }

    static void AddRow(TableLayoutPanel grid, string? label, Control control, Control? trailing = null)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (label != null)
            grid.Controls.Add(new Label
            {
                Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6),
            }, 0, row);
        grid.Controls.Add(control, 1, row);
        if (label == null)
        {
            grid.SetColumn(control, 0);
            grid.SetColumnSpan(control, 3);
        }
        if (trailing != null) grid.Controls.Add(trailing, 2, row);
    }

    static ComboBox Combo(IEnumerable<string> items)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        combo.Items.AddRange(items.Cast<object>().ToArray());
        return combo;
    }

    static TrackBar Slider() => new()
    {
        Minimum = 0, Maximum = 100, TickFrequency = 10, SmallChange = 1, LargeChange = 10, Dock = DockStyle.Fill,
    };

    static Label ValueLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Left };
}
