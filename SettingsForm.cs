namespace PaperWindow;

/// <summary>
/// Settings window. Look controls edit a draft of the active profile that is previewed live through <c>onChange</c>
/// and only stored in the profile when the user clicks "Salvar". General options are saved on close.
/// </summary>
internal sealed class SettingsForm : Form
{
    readonly Settings _settings;
    readonly HotkeyManager _hotkeys;
    readonly Action _onChange;

    readonly CheckBox _enabled = new() { Text = "Efeito ativo", AutoSize = true };
    readonly ComboBox _profile = Combo([]);
    readonly Button _saveProfile = new() { Text = "Salvar", AutoSize = true };
    readonly Button _deleteProfile = new() { Text = "Excluir", AutoSize = true };
    readonly ComboBox _effect = Combo(ColorEffects.All.Select(e => e.Name));
    readonly TrackBar _strength = Slider();
    readonly Label _strengthValue = ValueLabel();
    readonly TrackBar _blueLight = Slider();
    readonly Label _blueLightValue = ValueLabel();
    readonly TrackBar _brightness = Slider(Profile.MinBrightness);
    readonly Label _brightnessValue = ValueLabel();
    readonly ComboBox _texture = Combo(TextureStyles.All.Select(t => t.Name));
    readonly TrackBar _opacity = Slider();
    readonly Label _opacityValue = ValueLabel();
    readonly CheckBox _cursorTheme = new() { Text = "Cursor temático", AutoSize = true };
    readonly ComboBox _cursorStyle = Combo(CursorStyles.All.Select(s => s.Name));
    readonly ComboBox _cursorSize = Combo(Profile.CursorSizes.Select((px, i) => i == 0 ? $"{px} px (padrão)" : $"{px} px"));
    readonly CheckBox _inkTrail = new() { Text = "Rastro de tinta ao arrastar", AutoSize = true };
    readonly TrackBar _trailLength = Slider(Profile.MinTrailLength);
    readonly Label _trailLengthValue = ValueLabel();
    readonly TrackBar _trailWidth = new()
    {
        Minimum = Profile.MinTrailWidth, Maximum = Profile.MaxTrailWidth, TickFrequency = 1, SmallChange = 1, LargeChange = 2,
        Dock = DockStyle.Fill,
    };
    readonly Label _trailWidthValue = ValueLabel();
    readonly CheckBox _inkDrop = new() { Text = "Pingo de tinta ao clicar", AutoSize = true };
    readonly CheckBox _schedule = new() { Text = "Trocar de perfil automaticamente entre dia e noite", AutoSize = true };
    readonly ComboBox _dayProfile = ScheduleCombo();
    readonly DateTimePicker _dayStart = TimePicker();
    readonly ComboBox _nightProfile = ScheduleCombo();
    readonly DateTimePicker _nightStart = TimePicker();
    readonly CheckBox _hideTexture = new() { Text = "Esconder a textura em prints, gravações e compartilhamento de tela", AutoSize = true };
    readonly CheckBox _hideMouse = new() { Text = "Esconder rastro e pingos de tinta em prints, gravações e compartilhamento", AutoSize = true };
    readonly TextBox _hotkey = new() { ReadOnly = true, Dock = DockStyle.Fill, ShortcutsEnabled = false, Cursor = Cursors.Hand };
    readonly Label _hotkeyStatus = new() { AutoSize = true, MaximumSize = new Size(300, 0), Margin = new Padding(0, 0, 0, 6) };
    readonly CheckBox _startup = new() { Text = "Iniciar com o Windows", AutoSize = true };
    readonly TabControl _tabs = new() { Margin = new Padding(0, 6, 0, 0) };
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

        var newProfile = new Button { Text = "Novo...", AutoSize = true };
        var renameProfile = new Button { Text = "Renomear...", AutoSize = true };
        var profileButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        profileButtons.Controls.AddRange([_saveProfile, newProfile, renameProfile, _deleteProfile]);

        // Above the tabs: what the look tabs act on (on/off and which profile is being edited).
        var header = Grid();
        AddRow(header, null, _enabled);
        AddRow(header, "Perfil", _profile);
        AddRow(header, "", profileButtons);

        // The first three tabs edit the profile's look; the last two hold options shared by all profiles.
        _tabs.TabPages.AddRange([
            Page("Cor e brilho",
                ("Efeito de cor", _effect, null),
                ("Intensidade do filtro", _strength, _strengthValue),
                ("Filtro de luz azul", _blueLight, _blueLightValue),
                ("Brilho", _brightness, _brightnessValue)),
            Page("Textura",
                ("Textura do papel", _texture, null),
                ("Opacidade da textura", _opacity, _opacityValue)),
            Page("Mouse",
                (null, _cursorTheme, null),
                ("Estilo do cursor", _cursorStyle, null),
                ("Tamanho do cursor", _cursorSize, null),
                (null, _inkTrail, null),
                ("Duração do rastro", _trailLength, _trailLengthValue),
                ("Espessura do rastro", _trailWidth, _trailWidthValue),
                (null, _inkDrop, null)),
            Page("Dia e noite",
                (null, Hint("Vale para todos os perfis. Um perfil escolhido à mão vale até a próxima troca de período."), null),
                (null, _schedule, null),
                ("Perfil do dia", ScheduleRow(_dayProfile, _dayStart), null),
                ("Perfil da noite", ScheduleRow(_nightProfile, _nightStart), null)),
            Page("Geral",
                (null, Hint("Vale para todos os perfis."), null),
                (null, _hideTexture, null),
                (null, _hideMouse, null),
                ("Atalho liga/desliga", _hotkey, null),
                ("", _hotkeyStatus, null),
                (null, _startup, null)),
        ]);

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

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(header);
        root.Controls.Add(_tabs);
        root.Controls.Add(buttons);
        Controls.Add(root);
        AcceptButton = CancelButton = close;

        var tips = new ToolTip();
        tips.SetToolTip(_saveProfile, "Guarda as alterações no perfil atual");
        tips.SetToolTip(newProfile, "Cria um perfil novo com o visual que está na tela");
        tips.SetToolTip(reset, "Volta os controles para os valores padrão (clique em Salvar para manter)");
        tips.SetToolTip(_hotkey, "Clique e pressione a nova combinação de teclas");

        _profile.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading && _profile.SelectedIndex >= 0) SwitchProfile(_settings.Profiles[_profile.SelectedIndex].Name);
        };
        _saveProfile.Click += (_, _) => SaveProfile();
        newProfile.Click += (_, _) => NewProfile();
        renameProfile.Click += (_, _) => RenameProfile();
        _deleteProfile.Click += (_, _) => DeleteProfile();
        reset.Click += (_, _) =>
        {
            _settings.EditDraft(p => p.CopyLookFrom(new Profile()));
            RefreshControls();
            _onChange();
        };
        import.Click += (_, _) => ImportSettings();
        export.Click += (_, _) => ExportSettings();
        close.Click += (_, _) => Close();
        _enabled.CheckedChanged += (_, _) => Changed(() => _settings.Enabled = _enabled.Checked);
        _effect.SelectedIndexChanged += (_, _) => EditLook(p => p.Effect = ColorEffects.All[_effect.SelectedIndex].Id);
        _strength.ValueChanged += (_, _) => EditLook(p => p.FilterStrength = _strength.Value);
        _blueLight.ValueChanged += (_, _) => EditLook(p => p.BlueLightFilter = _blueLight.Value);
        _brightness.ValueChanged += (_, _) => EditLook(p => p.Brightness = _brightness.Value);
        _texture.SelectedIndexChanged += (_, _) => EditLook(p => p.Texture = TextureStyles.All[_texture.SelectedIndex].Id);
        _opacity.ValueChanged += (_, _) => EditLook(p => p.TextureOpacity = _opacity.Value);
        _cursorTheme.CheckedChanged += (_, _) => EditLook(p => p.CursorTheme = _cursorTheme.Checked);
        _cursorStyle.SelectedIndexChanged += (_, _) => EditLook(p => p.CursorStyle = CursorStyles.All[_cursorStyle.SelectedIndex].Id);
        _cursorSize.SelectedIndexChanged += (_, _) => EditLook(p => p.CursorSize = Profile.CursorSizes[_cursorSize.SelectedIndex]);
        _inkTrail.CheckedChanged += (_, _) => EditLook(p => p.InkTrail = _inkTrail.Checked);
        _trailLength.ValueChanged += (_, _) => EditLook(p => p.InkTrailLength = _trailLength.Value);
        _trailWidth.ValueChanged += (_, _) => EditLook(p => p.InkTrailWidth = _trailWidth.Value);
        _inkDrop.CheckedChanged += (_, _) => EditLook(p => p.InkDrop = _inkDrop.Checked);
        _schedule.CheckedChanged += (_, _) => Changed(() => _settings.ScheduleEnabled = _schedule.Checked);
        _dayProfile.SelectedIndexChanged += (_, _) =>
            Changed(() => _settings.DayProfile = _settings.Profiles[_dayProfile.SelectedIndex].Name);
        _nightProfile.SelectedIndexChanged += (_, _) =>
            Changed(() => _settings.NightProfile = _settings.Profiles[_nightProfile.SelectedIndex].Name);
        _dayStart.ValueChanged += (_, _) => Changed(() => _settings.DayStart = HourMinute(_dayStart));
        _nightStart.ValueChanged += (_, _) => Changed(() => _settings.NightStart = HourMinute(_nightStart));
        _hideTexture.CheckedChanged += (_, _) => Changed(() => _settings.HideTextureFromCapture = _hideTexture.Checked);
        _hideMouse.CheckedChanged += (_, _) => Changed(() => _settings.HideMouseFromCapture = _hideMouse.Checked);
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
        var p = _settings.Shown;
        _enabled.Checked = _settings.Enabled;
        _profile.Items.Clear();
        _profile.Items.AddRange(_settings.Profiles.Select(x => (object)x.Name).ToArray());
        _profile.SelectedIndex = _settings.Profiles.IndexOf(_settings.Current);
        _effect.SelectedIndex = Array.IndexOf(ColorEffects.All, ColorEffects.Find(p.Effect));
        _strength.Value = Math.Clamp(p.FilterStrength, 0, 100);
        _blueLight.Value = Math.Clamp(p.BlueLightFilter, 0, 100);
        _brightness.Value = Math.Clamp(p.Brightness, Profile.MinBrightness, 100);
        _texture.SelectedIndex = Array.IndexOf(TextureStyles.All, TextureStyles.Find(p.Texture));
        _opacity.Value = Math.Clamp(p.TextureOpacity, 0, 100);
        _cursorTheme.Checked = p.CursorTheme;
        _cursorStyle.SelectedIndex = Array.IndexOf(CursorStyles.All, CursorStyles.Find(p.CursorStyle));
        _cursorSize.SelectedIndex = Math.Max(0, Array.IndexOf(Profile.CursorSizes, p.CursorSize));
        _inkTrail.Checked = p.InkTrail;
        _trailLength.Value = Math.Clamp(p.InkTrailLength, Profile.MinTrailLength, 100);
        _trailWidth.Value = Math.Clamp(p.InkTrailWidth, Profile.MinTrailWidth, Profile.MaxTrailWidth);
        _inkDrop.Checked = p.InkDrop;
        _schedule.Checked = _settings.ScheduleEnabled;
        var names = _settings.Profiles.Select(x => (object)x.Name).ToArray();
        _dayProfile.Items.Clear();
        _dayProfile.Items.AddRange(names);
        _dayProfile.SelectedIndex = Math.Max(0, _settings.Profiles.FindIndex(x => x.Name == _settings.DayProfile));
        _nightProfile.Items.Clear();
        _nightProfile.Items.AddRange(names);
        _nightProfile.SelectedIndex = Math.Max(0, _settings.Profiles.FindIndex(x => x.Name == _settings.NightProfile));
        _dayStart.Value = DateTime.Today + _settings.DayStart;
        _nightStart.Value = DateTime.Today + _settings.NightStart;
        _hideTexture.Checked = _settings.HideTextureFromCapture;
        _hideMouse.Checked = _settings.HideMouseFromCapture;
        _startup.Checked = Startup.IsEnabled;
        _loading = false;
        UpdateLabels();
        UpdateEnabledState();
        if (!_hotkey.Focused) UpdateHotkeyDisplay();
    }

    /// <summary>A TabControl does not size itself: make it fit the tallest and widest page.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var content = Size.Empty;
        foreach (TabPage page in _tabs.TabPages)
        {
            var s = page.Controls[0].GetPreferredSize(Size.Empty);
            content = new Size(Math.Max(content.Width, s.Width), Math.Max(content.Height, s.Height));
        }
        var chrome = _tabs.Size - _tabs.DisplayRectangle.Size;   // tab strip and borders
        _tabs.Size = content + _tabs.TabPages[0].Padding.Size + chrome;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // On Windows shutdown there is no one to answer: keep the saved profile.
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
            _settings.DiscardDraft();
        else if (!ConfirmUnsaved())
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _hotkeys.Suspend(false);
        _settings.Save();
        _onChange();   // a discarded draft must give the screen back the saved look
        base.OnFormClosed(e);
    }

    /// <summary>Lets the hotkey box receive Enter/Escape instead of them closing the window (Tab still moves on).</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_hotkey.Focused && (keyData & Keys.KeyCode) != Keys.Tab) return false;
        return base.ProcessDialogKey(keyData);
    }

    // ---- Profiles ----

    /// <summary>Switches the active profile, first asking what to do with unsaved changes. Also used by the tray menu.</summary>
    public void SwitchProfile(string name)
    {
        if (name != _settings.ActiveProfile && ConfirmUnsaved())
            _settings.ActiveProfile = name;
        ProfilesChanged();   // also puts the combo back if the user cancelled
    }

    void SaveProfile()
    {
        _settings.SaveDraft();
        ProfilesChanged();
    }

    /// <summary>
    /// If the active profile has unsaved changes, asks whether to save them. Returns false when the user cancels;
    /// otherwise the changes are saved or discarded and the caller may go on.
    /// </summary>
    bool ConfirmUnsaved()
    {
        if (!_settings.HasUnsavedChanges)
        {
            _settings.DiscardDraft();
            return true;
        }
        var answer = MessageBox.Show(this, $"Salvar as alterações no perfil \"{_settings.Current.Name}\"?", Text,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.Yes) _settings.SaveDraft();
        else _settings.DiscardDraft();
        _settings.Save();
        return true;
    }

    void NewProfile()
    {
        string? name = NameDialog.Ask(this, "Novo perfil", "Nome do novo perfil (usa o visual que está na tela):",
            _settings.UniqueName(_settings.Current.Name));
        if (name == null) return;
        // "Save as": the new profile gets the on-screen look; the original profile keeps what was saved.
        _settings.AddProfile(name, _settings.Shown);
        _settings.DiscardDraft();
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
        if (_settings.DeleteProfile(p)) _settings.DiscardDraft();
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
        if (!ConfirmUnsaved()) return;
        RefreshControls();
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
        if (!ConfirmUnsaved()) return;
        RefreshControls();
        _onChange();
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

    /// <summary>Look edits go to the draft: previewed on screen, stored only when "Salvar" is clicked.</summary>
    void EditLook(Action<Profile> change) => Changed(() => _settings.EditDraft(change));

    void UpdateLabels()
    {
        _strengthValue.Text = $"{_strength.Value}%";
        _blueLightValue.Text = $"{_blueLight.Value}%";
        _brightnessValue.Text = $"{_brightness.Value}%";
        _trailLengthValue.Text = $"{_trailLength.Value * 15 / 1000.0:0.0} s";
        _trailWidthValue.Text = $"{_trailWidth.Value} px";
        _opacityValue.Text = $"{_opacity.Value}%";
    }

    void UpdateEnabledState()
    {
        foreach (Control c in new Control[]
                 { _effect, _strength, _blueLight, _brightness, _texture, _opacity, _cursorTheme, _inkTrail, _inkDrop, _hideTexture, _hideMouse })
            c.Enabled = _settings.Enabled;
        // Each mouse effect's own setting is only editable while that effect is on.
        _cursorStyle.Enabled = _cursorSize.Enabled = _settings.Enabled && _settings.Shown.CursorTheme;
        _trailLength.Enabled = _trailWidth.Enabled = _settings.Enabled && _settings.Shown.InkTrail;
        foreach (Control c in new Control[] { _dayProfile, _dayStart, _nightProfile, _nightStart })
            c.Enabled = _settings.ScheduleEnabled;
        _deleteProfile.Enabled = _settings.Profiles.Count > 1;
        _saveProfile.Enabled = _settings.HasUnsavedChanges;
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

    static TableLayoutPanel Grid()
    {
        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        return grid;
    }

    static TabPage Page(string title, params (string? Label, Control Control, Control? Trailing)[] rows)
    {
        var grid = Grid();
        grid.Dock = DockStyle.Top;   // with Fill, the last row would soak up the spare height of shorter pages
        foreach (var (label, control, trailing) in rows) AddRow(grid, label, control, trailing);
        var page = new TabPage(title) { Padding = new Padding(10), UseVisualStyleBackColor = true };
        page.Controls.Add(grid);
        return page;
    }

    static Label Hint(string text) => new()
    {
        Text = text, AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 0, 0, 8),
    };

    static ComboBox Combo(IEnumerable<string> items)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        combo.Items.AddRange(items.Cast<object>().ToArray());
        return combo;
    }

    static TrackBar Slider(int minimum = 0) => new()
    {
        Minimum = minimum, Maximum = 100, TickFrequency = 10, SmallChange = 1, LargeChange = 10, Dock = DockStyle.Fill,
    };

    static Label ValueLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Left };

    static ComboBox ScheduleCombo()
    {
        var combo = Combo([]);
        combo.Dock = DockStyle.None;
        combo.Width = 170;
        return combo;
    }

    static DateTimePicker TimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 64,
    };

    static TimeSpan HourMinute(DateTimePicker picker) => new(picker.Value.Hour, picker.Value.Minute, 0);

    /// <summary>[profile] a partir de [time], on one grid row.</summary>
    static FlowLayoutPanel ScheduleRow(ComboBox profile, DateTimePicker start)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange([
            profile,
            new Label { Text = "a partir de", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 6, 3, 0) },
            start,
        ]);
        return row;
    }
}
