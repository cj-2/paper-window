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
    readonly TrackBar _blueLight = Slider();
    readonly Label _blueLightValue = ValueLabel();
    readonly ComboBox _texture = Combo(TextureStyles.All.Select(t => t.Name));
    readonly TrackBar _opacity = Slider();
    readonly Label _opacityValue = ValueLabel();
    readonly CheckBox _hideFromCapture = new() { Text = "Esconder textura em prints e compartilhamento de tela", AutoSize = true };
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
        AddRow(grid, "Filtro de luz azul", _blueLight, _blueLightValue);
        AddRow(grid, "Textura do papel", _texture);
        AddRow(grid, "Opacidade da textura", _opacity, _opacityValue);
        AddRow(grid, null, _hideFromCapture);
        AddRow(grid, null, _startup);

        var import = new Button { Text = "Importar...", AutoSize = true };
        var export = new Button { Text = "Exportar...", AutoSize = true };
        var reset = new Button { Text = "Restaurar padrão", AutoSize = true };
        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.OK };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(reset);
        buttons.Controls.Add(export);
        buttons.Controls.Add(import);
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 3);
        Controls.Add(grid);
        AcceptButton = CancelButton = close;

        reset.Click += (_, _) => { _settings.CopyFrom(new Settings()); RefreshControls(); _onChange(); };
        import.Click += (_, _) => ImportSettings();
        export.Click += (_, _) => ExportSettings();
        close.Click += (_, _) => Close();
        _enabled.CheckedChanged += (_, _) => Changed(() => _settings.Enabled = _enabled.Checked);
        _effect.SelectedIndexChanged += (_, _) => Changed(() => _settings.Effect = ColorEffects.All[_effect.SelectedIndex].Id);
        _strength.ValueChanged += (_, _) => Changed(() => _settings.FilterStrength = _strength.Value);
        _blueLight.ValueChanged += (_, _) => Changed(() => _settings.BlueLightFilter = _blueLight.Value);
        _texture.SelectedIndexChanged += (_, _) => Changed(() => _settings.Texture = TextureStyles.All[_texture.SelectedIndex].Id);
        _opacity.ValueChanged += (_, _) => Changed(() => _settings.TextureOpacity = _opacity.Value);
        _hideFromCapture.CheckedChanged += (_, _) => Changed(() => _settings.HideFromCapture = _hideFromCapture.Checked);
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
        _blueLight.Value = Math.Clamp(_settings.BlueLightFilter, 0, 100);
        _texture.SelectedIndex = Array.IndexOf(TextureStyles.All, TextureStyles.Find(_settings.Texture));
        _opacity.Value = Math.Clamp(_settings.TextureOpacity, 0, 100);
        _hideFromCapture.Checked = _settings.HideFromCapture;
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

    const string FileFilter = "Configuração do Paper Window (*.json)|*.json|Todos os arquivos (*.*)|*.*";

    void ExportSettings()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Exportar configurações",
            Filter = FileFilter,
            FileName = "paper-window.json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _settings.Write(dialog.FileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Não foi possível exportar:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void ImportSettings()
    {
        using var dialog = new OpenFileDialog { Title = "Importar configurações", Filter = FileFilter };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _settings.CopyFrom(Settings.Read(dialog.FileName));
            _settings.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"Arquivo de configuração inválido:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        RefreshControls();
        _onChange();
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
        _blueLightValue.Text = $"{_blueLight.Value}%";
        _opacityValue.Text = $"{_opacity.Value}%";
    }

    void UpdateEnabledState()
    {
        foreach (Control c in new Control[] { _effect, _strength, _blueLight, _texture, _opacity, _hideFromCapture })
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
