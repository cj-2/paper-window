namespace PaperWindow;

/// <summary>
/// Settings window. Look controls edit the active profile; every change is applied live through <c>onChange</c>.
/// Settings are saved on close (and right away for profile operations).
/// </summary>
internal sealed class SettingsForm : Form
{
    readonly Settings _settings;
    readonly HotkeyManager _hotkeys;
    readonly Action _onChange;

    readonly CheckBox _enabled = new() { Text = "Efeito ativo", AutoSize = true };
    readonly ComboBox _profile = Combo([]);
    readonly Button _deleteProfile = new() { Text = "Excluir", AutoSize = true };
    readonly ComboBox _effect = Combo(ColorEffects.All.Select(e => e.Name));
    readonly TrackBar _strength = Slider();
    readonly Label _strengthValue = ValueLabel();
    readonly TrackBar _blueLight = Slider();
    readonly Label _blueLightValue = ValueLabel();
    readonly ComboBox _texture = Combo(TextureStyles.All.Select(t => t.Name));
    readonly TrackBar _opacity = Slider();
    readonly Label _opacityValue = ValueLabel();
    readonly CheckBox _hideFromCapture = new() { Text = "Esconder textura em prints e compartilhamento de tela", AutoSize = true };
    readonly TextBox _hotkey = new() { ReadOnly = true, Dock = DockStyle.Fill, ShortcutsEnabled = false, Cursor = Cursors.Hand };
    readonly Label _hotkeyStatus = new() { AutoSize = true, MaximumSize = new Size(300, 0), Margin = new Padding(0, 0, 0, 6) };
    readonly CheckBox _startup = new() { Text = "Iniciar com o Windows", AutoSize = true };
    bool _loading;

    public SettingsForm(Settings settings, HotkeyManager hotkeys, Action onChange)
    {
        _settings = settings;
        _hotkeys = hotkeys;
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
        _hotkey.BackColor = SystemColors.Window;

        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));

        var newProfile = new Button { Text = "Novo...", AutoSize = true };
        var renameProfile = new Button { Text = "Renomear...", AutoSize = true };
        var profileButtons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        profileButtons.Controls.AddRange([newProfile, renameProfile, _deleteProfile]);

        AddRow(grid, null, _enabled);
        AddRow(grid, "Perfil", _profile);
        AddRow(grid, "", profileButtons);
        AddRow(grid, "Efeito de cor", _effect);
        AddRow(grid, "Intensidade do filtro", _strength, _strengthValue);
        AddRow(grid, "Filtro de luz azul", _blueLight, _blueLightValue);
        AddRow(grid, "Textura do papel", _texture);
        AddRow(grid, "Opacidade da textura", _opacity, _opacityValue);
        AddRow(grid, null, _hideFromCapture);
        AddRow(grid, "Atalho liga/desliga", _hotkey);
        AddRow(grid, "", _hotkeyStatus);
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
        buttons.Controls.AddRange([close, reset, export, import]);
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 3);
        Controls.Add(grid);
        AcceptButton = CancelButton = close;

        var tips = new ToolTip();
        tips.SetToolTip(reset, "Volta o perfil atual para os valores padrão");
        tips.SetToolTip(_hotkey, "Clique e pressione a nova combinação de teclas");

        _profile.SelectedIndexChanged += (_, _) => SwitchProfile();
        newProfile.Click += (_, _) => NewProfile();
        renameProfile.Click += (_, _) => RenameProfile();
        _deleteProfile.Click += (_, _) => DeleteProfile();
        reset.Click += (_, _) =>
        {
            _settings.Current.CopyLookFrom(new Profile());
            RefreshControls();
            _onChange();
        };
        import.Click += (_, _) => ImportSettings();
        export.Click += (_, _) => ExportSettings();
        close.Click += (_, _) => Close();
        _enabled.CheckedChanged += (_, _) => Changed(() => _settings.Enabled = _enabled.Checked);
        _effect.SelectedIndexChanged += (_, _) => Changed(() => _settings.Current.Effect = ColorEffects.All[_effect.SelectedIndex].Id);
        _strength.ValueChanged += (_, _) => Changed(() => _settings.Current.FilterStrength = _strength.Value);
        _blueLight.ValueChanged += (_, _) => Changed(() => _settings.Current.BlueLightFilter = _blueLight.Value);
        _texture.SelectedIndexChanged += (_, _) => Changed(() => _settings.Current.Texture = TextureStyles.All[_texture.SelectedIndex].Id);
        _opacity.ValueChanged += (_, _) => Changed(() => _settings.Current.TextureOpacity = _opacity.Value);
        _hideFromCapture.CheckedChanged += (_, _) => Changed(() => _settings.HideFromCapture = _hideFromCapture.Checked);
        _startup.CheckedChanged += (_, _) => { if (!_loading) Startup.IsEnabled = _startup.Checked; };

        // While recording a new hotkey, release the current one so pressing it reaches the text box.
        _hotkey.Enter += (_, _) => { _hotkeys.Suspend(true); _hotkey.Text = "Pressione a combinação..."; };
        _hotkey.Leave += (_, _) => { _hotkeys.Suspend(false); UpdateHotkeyDisplay(); };
        _hotkey.KeyDown += OnHotkeyKeyDown;

        RefreshControls();
    }

    /// <summary>Pulls current values into the controls (e.g. after the tray toggled the effect or switched profile).</summary>
    public void RefreshControls()
    {
        _loading = true;
        var p = _settings.Current;
        _enabled.Checked = _settings.Enabled;
        _profile.Items.Clear();
        _profile.Items.AddRange(_settings.Profiles.Select(x => (object)x.Name).ToArray());
        _profile.SelectedIndex = _settings.Profiles.IndexOf(p);
        _effect.SelectedIndex = Array.IndexOf(ColorEffects.All, ColorEffects.Find(p.Effect));
        _strength.Value = Math.Clamp(p.FilterStrength, 0, 100);
        _blueLight.Value = Math.Clamp(p.BlueLightFilter, 0, 100);
        _texture.SelectedIndex = Array.IndexOf(TextureStyles.All, TextureStyles.Find(p.Texture));
        _opacity.Value = Math.Clamp(p.TextureOpacity, 0, 100);
        _hideFromCapture.Checked = _settings.HideFromCapture;
        _startup.Checked = Startup.IsEnabled;
        _loading = false;
        UpdateLabels();
        UpdateEnabledState();
        if (!_hotkey.Focused) UpdateHotkeyDisplay();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hotkeys.Suspend(false);
        _settings.Save();
        base.OnFormClosed(e);
    }

    /// <summary>Lets the hotkey box receive Enter/Escape instead of them closing the window (Tab still moves on).</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_hotkey.Focused && (keyData & Keys.KeyCode) != Keys.Tab) return false;
        return base.ProcessDialogKey(keyData);
    }

    // ---- Profiles ----

    void SwitchProfile()
    {
        if (_loading || _profile.SelectedIndex < 0) return;
        _settings.ActiveProfile = _settings.Profiles[_profile.SelectedIndex].Name;
        ProfilesChanged();
    }

    void NewProfile()
    {
        string? name = NameDialog.Ask(this, "Novo perfil", "Nome do novo perfil (começa como cópia do atual):",
            _settings.UniqueName(_settings.Current.Name));
        if (name == null) return;
        _settings.AddProfile(name, _settings.Current);
        ProfilesChanged();
    }

    void RenameProfile()
    {
        var p = _settings.Current;
        string? name = NameDialog.Ask(this, "Renomear perfil", "Novo nome:", p.Name);
        if (name == null || name.Trim() == p.Name) return;
        if (!_settings.RenameProfile(p, name))
        {
            MessageBox.Show(this, $"Já existe um perfil chamado \"{name.Trim()}\".", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ProfilesChanged();
    }

    void DeleteProfile()
    {
        var p = _settings.Current;
        if (MessageBox.Show(this, $"Excluir o perfil \"{p.Name}\"?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            != DialogResult.Yes) return;
        _settings.DeleteProfile(p);
        ProfilesChanged();
    }

    void ProfilesChanged()
    {
        _settings.Save();
        RefreshControls();
        _onChange();
    }

    // ---- Hotkey ----

    void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return; // wait for the real key

        bool win = Hotkey.WinKeyDown;
        bool bare = e.Modifiers == Keys.None && !win;
        if (bare && e.KeyCode == Keys.Escape)
        {
            ActiveControl = null;   // cancel: keep the current hotkey
            return;
        }

        Hotkey? hotkey = null;
        if (!(bare && e.KeyCode is Keys.Back or Keys.Delete))   // Delete/Backspace = no hotkey
        {
            var candidate = new Hotkey(e.KeyCode, e.Control, e.Alt, e.Shift, win);
            if (!candidate.IsValid)
            {
                _hotkey.Text = "Use Ctrl, Alt ou Win junto com uma tecla";
                return;
            }
            hotkey = candidate;
        }

        _settings.ToggleHotkey = hotkey?.ToString() ?? "";
        _onChange();
        ActiveControl = null;   // leaving the box re-enables the hotkey and shows whether it registered
    }

    void UpdateHotkeyDisplay()
    {
        _hotkey.Text = _settings.ToggleHotkey.Length > 0 ? _settings.ToggleHotkey : "Nenhum";
        if (_hotkeys.Failed)
        {
            _hotkeyStatus.ForeColor = Color.Firebrick;
            _hotkeyStatus.Text = "Esse atalho já está em uso por outro programa. Clique no campo e escolha outra combinação.";
        }
        else
        {
            _hotkeyStatus.ForeColor = SystemColors.GrayText;
            _hotkeyStatus.Text = "Clique no campo e pressione a nova combinação. Delete = sem atalho.";
        }
    }

    // ---- Import / export ----

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
        using var dialog = new OpenFileDialog { Title = "Importar perfis", Filter = FileFilter };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int added;
        try
        {
            added = _settings.ImportProfiles(Settings.Read(dialog.FileName));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, $"Arquivo de configuração inválido:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        ProfilesChanged();
        MessageBox.Show(this, added == 0 ? "Esses perfis já existiam; nada novo foi adicionado." : $"{added} perfil(is) importado(s).",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---- Helpers ----

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
        _deleteProfile.Enabled = _settings.Profiles.Count > 1;
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
        else if (trailing == null)
        {
            grid.SetColumnSpan(control, 2);
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
