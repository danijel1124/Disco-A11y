using System.Windows.Forms;

namespace KeybindEditor;

public sealed class MainForm : Form
{
    private const string DefaultGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Disco Elysium";

    private readonly Label languageLabel;
    private readonly ComboBox languageCombo;
    private readonly Label gamePathLabel;
    private readonly TextBox gamePathBox;
    private readonly Button browseButton;
    private readonly ListView bindingsList;
    private readonly Button rebindButton;
    private readonly Button cancelRebindButton;
    private readonly Button resetSelectedButton;
    private readonly Button defaultPresetButton;
    private readonly Button safePresetButton;
    private readonly Button stardewPresetButton;
    private readonly GroupBox generalGroup;
    private readonly Label dialogModeLabel;
    private readonly ComboBox dialogModeCombo;
    private readonly CheckBox orbAnnouncementsCheck;
    private readonly CheckBox speechInterruptCheck;
    private readonly CheckBox speakAudioCaptionsCheck;
    private readonly CheckBox dialogAutoAdvanceCheck;
    private readonly CheckBox autoInteractCheck;
    private readonly CheckBox speechLogCheck;
    private readonly Button saveButton;
    private readonly Label statusLabel;

    private ModConfig config = new();
    private int capturingIndex = -1;
    private bool loaded;

    public MainForm(string? initialGamePath = null)
    {
        Width = 720;
        Height = 730;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        languageLabel = new Label { Left = 12, Top = 15, Width = 90 };
        languageCombo = new ComboBox { Left = 105, Top = 12, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        languageCombo.Items.Add("English");
        languageCombo.Items.Add("Deutsch");
        languageCombo.SelectedIndex = Strings.Current == Language.German ? 1 : 0;
        languageCombo.SelectedIndexChanged += LanguageCombo_SelectedIndexChanged;

        gamePathLabel = new Label { Left = 12, Top = 50, Width = 90 };
        var startPath = initialGamePath ?? (Directory.Exists(DefaultGamePath) ? DefaultGamePath : "");
        gamePathBox = new TextBox { Left = 105, Top = 47, Width = 460, Text = startPath };
        browseButton = new Button { Left = 575, Top = 46, Width = 120 };
        browseButton.Click += BrowseButton_Click;

        bindingsList = new ListView
        {
            Left = 12,
            Top = 85,
            Width = 683,
            Height = 340,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
        };
        bindingsList.Columns.Add("", 660);
        bindingsList.KeyDown += BindingsList_KeyDown;

        rebindButton = new Button { Left = 12, Top = 435, Width = 150 };
        rebindButton.Click += (_, _) => BeginRebind();

        cancelRebindButton = new Button { Left = 170, Top = 435, Width = 120, Enabled = false };
        cancelRebindButton.Click += (_, _) => CancelRebind();

        resetSelectedButton = new Button { Left = 300, Top = 435, Width = 190 };
        resetSelectedButton.Click += ResetSelectedButton_Click;

        defaultPresetButton = new Button { Left = 12, Top = 470, Width = 200 };
        defaultPresetButton.Click += (_, _) => ApplyPreset(Preset.Default);

        safePresetButton = new Button { Left = 220, Top = 470, Width = 235 };
        safePresetButton.Click += (_, _) => ApplyPreset(Preset.NumpadSafe);

        stardewPresetButton = new Button { Left = 463, Top = 470, Width = 232 };
        stardewPresetButton.Click += (_, _) => ApplyPreset(Preset.Stardew);

        generalGroup = new GroupBox { Left = 12, Top = 510, Width = 683, Height = 160 };
        dialogModeLabel = new Label { Left = 12, Top = 28, Width = 130 };
        dialogModeCombo = new ComboBox { Left = 150, Top = 25, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        orbAnnouncementsCheck = new CheckBox { Left = 12, Top = 60, Width = 200 };
        speechInterruptCheck = new CheckBox { Left = 230, Top = 60, Width = 220 };
        speakAudioCaptionsCheck = new CheckBox { Left = 460, Top = 60, Width = 210 };
        dialogAutoAdvanceCheck = new CheckBox { Left = 12, Top = 92, Width = 400 };
        autoInteractCheck = new CheckBox { Left = 420, Top = 92, Width = 250 };
        speechLogCheck = new CheckBox { Left = 12, Top = 124, Width = 660 };
        generalGroup.Controls.AddRange(new Control[] { dialogModeLabel, dialogModeCombo, orbAnnouncementsCheck, speechInterruptCheck, speakAudioCaptionsCheck, dialogAutoAdvanceCheck, autoInteractCheck, speechLogCheck });

        saveButton = new Button { Left = 12, Top = 680, Width = 150 };
        saveButton.Click += SaveButton_Click;

        statusLabel = new Label { Left = 170, Top = 685, Width = 525, Text = "" };

        Controls.AddRange(new Control[]
        {
            languageLabel, languageCombo,
            gamePathLabel, gamePathBox, browseButton,
            bindingsList, rebindButton, cancelRebindButton, resetSelectedButton,
            defaultPresetButton, safePresetButton, stardewPresetButton,
            generalGroup, saveButton, statusLabel,
        });

        ApplyLocalization();

        Load += (_, _) =>
        {
            Tolk.Initialize();
            loaded = true;
            LoadConfigFromDisk();
        };
        FormClosed += (_, _) => Tolk.Shutdown();
    }

    private void LanguageCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        Strings.Current = languageCombo.SelectedIndex == 1 ? Language.German : Language.English;
        ApplyLocalization();
        if (loaded) RefreshBindingsList();
    }

    private void ApplyLocalization()
    {
        Text = Strings.Get("WindowTitle");
        languageLabel.Text = Strings.Get("LanguageLabel");
        gamePathLabel.Text = Strings.Get("GamePathLabel");
        gamePathBox.AccessibleName = Strings.Get("GamePathAccessible");
        browseButton.Text = Strings.Get("Browse");
        bindingsList.AccessibleName = Strings.Get("BindingsListAccessible");
        bindingsList.Columns[0].Text = Strings.Get("ColumnHeader");
        rebindButton.Text = Strings.Get("Rebind");
        cancelRebindButton.Text = Strings.Get("CancelRebind");
        resetSelectedButton.Text = Strings.Get("ResetSelected");
        defaultPresetButton.Text = Strings.Get("PresetDefault");
        safePresetButton.Text = Strings.Get("PresetSafe");
        stardewPresetButton.Text = Strings.Get("PresetStardew");
        generalGroup.Text = Strings.Get("GeneralGroup");
        dialogModeLabel.Text = Strings.Get("DialogModeLabel");
        dialogModeCombo.AccessibleName = Strings.Get("DialogModeLabel");

        var selectedDialogMode = dialogModeCombo.SelectedIndex;
        dialogModeCombo.Items.Clear();
        dialogModeCombo.Items.Add(Strings.Get("DialogModeOff"));
        dialogModeCombo.Items.Add(Strings.Get("DialogModeFull"));
        dialogModeCombo.Items.Add(Strings.Get("DialogModeSpeakerOnly"));
        dialogModeCombo.SelectedIndex = selectedDialogMode >= 0 ? selectedDialogMode : 0;

        orbAnnouncementsCheck.Text = Strings.Get("OrbAnnouncements");
        orbAnnouncementsCheck.AccessibleName = Strings.Get("OrbAnnouncements");
        speechInterruptCheck.Text = Strings.Get("SpeechInterrupt");
        speechInterruptCheck.AccessibleName = Strings.Get("SpeechInterrupt");
        speakAudioCaptionsCheck.Text = Strings.Get("SpeakAudioCaptions");
        speakAudioCaptionsCheck.AccessibleName = Strings.Get("SpeakAudioCaptions");
        dialogAutoAdvanceCheck.Text = Strings.Get("DialogAutoAdvance");
        dialogAutoAdvanceCheck.AccessibleName = Strings.Get("DialogAutoAdvance");
        autoInteractCheck.Text = Strings.Get("AutoInteract");
        autoInteractCheck.AccessibleName = Strings.Get("AutoInteract");
        speechLogCheck.Text = Strings.Get("SpeechLog");
        speechLogCheck.AccessibleName = Strings.Get("SpeechLog");
        saveButton.Text = Strings.Get("Save");
        statusLabel.AccessibleName = Strings.Get("StatusAccessible");
    }

    private string ConfigPath => Path.Combine(gamePathBox.Text.Trim(), "UserData", "AccessibilityMod.cfg");

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = Strings.Get("BrowseDialogTitle") };
        if (Directory.Exists(gamePathBox.Text)) dialog.SelectedPath = gamePathBox.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            gamePathBox.Text = dialog.SelectedPath;
            LoadConfigFromDisk();
        }
    }

    private void LoadConfigFromDisk()
    {
        if (!Directory.Exists(gamePathBox.Text.Trim()))
        {
            SetStatus(Strings.Get("StatusGamePathMissing"));
            return;
        }

        config = ModConfig.LoadOrDefault(ConfigPath);
        RefreshBindingsList();
        dialogModeCombo.SelectedIndex = Math.Clamp(config.DialogReadingMode, 0, 2);
        orbAnnouncementsCheck.Checked = config.OrbAnnouncements;
        speechInterruptCheck.Checked = config.SpeechInterrupt;
        speakAudioCaptionsCheck.Checked = config.SpeakAudioCaptions;
        dialogAutoAdvanceCheck.Checked = config.DialogAutoAdvance;
        autoInteractCheck.Checked = config.AutoInteract;
        speechLogCheck.Checked = config.SpeechLog;
        SetStatus(File.Exists(ConfigPath) ? Strings.Get("StatusConfigLoaded") : Strings.Get("StatusConfigNotFound"));
    }

    private void RefreshBindingsList()
    {
        bindingsList.Items.Clear();
        foreach (var action in GameKeyCatalog.Actions)
        {
            var item = new ListViewItem(RowText(action.Label, config.KeyBindings[action.Name])) { Tag = action.Name };
            bindingsList.Items.Add(item);
        }

        AppendGameControls();
    }

    /// <summary>
    /// The game's own controls, appended to the same list so they can be arrowed through
    /// alongside the mod's. They carry no action tag, which is what marks them read-only:
    /// rebinding or resetting one is refused with an explanation rather than silently
    /// writing a key the game will never read.
    /// </summary>
    private void AppendGameControls()
    {
        var gameControls = GameKeybindReference.Load(gamePathBox.Text.Trim());

        bindingsList.Items.Add(new ListViewItem(
            gameControls.Count > 0 ? Strings.Get("GameControlsHeader") : Strings.Get("GameControlsMissing")));

        foreach (var entry in gameControls)
        {
            bindingsList.Items.Add(new ListViewItem(
                Strings.Get("GameControlRow", entry.Action, entry.Keys)));
        }
    }

    /// <summary>Rows without an action tag are the game's own keys - reference, not settings.</summary>
    private bool IsGameControlRow(int index) => bindingsList.Items[index].Tag == null;

    // Screen readers announce a ListView row from its first column only - a second
    // column with just the key never gets read when arrowing through rows. Putting both
    // the action and its current key into the single column's text guarantees both get
    // announced together.
    private static string RowText(string label, string binding) => $"{label} — {DescribeBinding(binding)}";

    private static string DescribeBinding(string binding)
    {
        var parts = binding.Split('|');
        if (parts.Length != 4) return binding;

        var mods = "";
        if (parts[1] == "True") mods += Strings.Get("ModCtrl");
        if (parts[2] == "True") mods += Strings.Get("ModAlt");
        if (parts[3] == "True") mods += Strings.Get("ModShift");
        return mods + KeyCodeMap.ToFriendly(parts[0]);
    }

    private void BeginRebind()
    {
        if (bindingsList.SelectedIndices.Count == 0)
        {
            SetStatus(Strings.Get("StatusSelectFirst"));
            return;
        }

        var index = bindingsList.SelectedIndices[0];
        if (IsGameControlRow(index))
        {
            SetStatus(Strings.Get("StatusGameControlReadOnly"));
            return;
        }

        capturingIndex = index;
        cancelRebindButton.Enabled = true;
        SetStatus(Strings.Get("StatusPressKey"));
    }

    // Capturing is cancelled via this dedicated button (or by starting a rebind on a
    // different row) rather than via the Escape key, so Escape itself stays assignable
    // like any other key through the capture flow below.
    private void CancelRebind()
    {
        if (capturingIndex < 0) return;

        capturingIndex = -1;
        cancelRebindButton.Enabled = false;
        SetStatus(Strings.Get("StatusRebindCancelled"));
    }

    private void BindingsList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (capturingIndex < 0) return;

        e.Handled = true;
        e.SuppressKeyPress = true;

        var baseKey = e.KeyCode;
        if (baseKey is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return; // wait for a non-modifier key
        }

        var unityName = KeyCodeMap.ToUnityName(baseKey);
        if (unityName == null)
        {
            SetStatus(Strings.Get("StatusKeyUnsupported", baseKey));
            return;
        }

        var actionName = (string)bindingsList.Items[capturingIndex].Tag!;
        var binding = $"{unityName}|{e.Control}|{e.Alt}|{e.Shift}";

        var conflict = config.KeyBindings.FirstOrDefault(kvp => kvp.Key != actionName && kvp.Value == binding);
        if (conflict.Key != null)
        {
            var conflictLabel = GameKeyCatalog.Actions.First(a => a.Name == conflict.Key).Label;
            SetStatus(Strings.Get("StatusRebindConflict", DescribeBinding(binding), conflictLabel));
            return; // stay in capture mode so the user can try a different key
        }

        config.KeyBindings[actionName] = binding;
        var label = GameKeyCatalog.Actions.First(a => a.Name == actionName).Label;
        bindingsList.Items[capturingIndex].Text = RowText(label, binding);
        SetStatus(Strings.Get("StatusRebound", label, DescribeBinding(binding)));
        capturingIndex = -1;
        cancelRebindButton.Enabled = false;
    }

    private void ResetSelectedButton_Click(object? sender, EventArgs e)
    {
        if (bindingsList.SelectedIndices.Count == 0)
        {
            SetStatus(Strings.Get("StatusSelectFirst"));
            return;
        }

        var index = bindingsList.SelectedIndices[0];
        if (IsGameControlRow(index))
        {
            SetStatus(Strings.Get("StatusGameControlReadOnly"));
            return;
        }

        var actionName = (string)bindingsList.Items[index].Tag!;
        var action = GameKeyCatalog.Actions.First(a => a.Name == actionName);
        config.KeyBindings[actionName] = action.DefaultBinding;
        bindingsList.Items[index].Text = RowText(action.Label, action.DefaultBinding);
        SetStatus(Strings.Get("StatusReset", action.Label));
    }

    private enum Preset { Default, NumpadSafe, Stardew }

    private void ApplyPreset(Preset preset)
    {
        foreach (var action in GameKeyCatalog.Actions)
        {
            config.KeyBindings[action.Name] = preset switch
            {
                Preset.NumpadSafe => action.SafeBinding,
                Preset.Stardew => action.StardewBinding,
                _ => action.DefaultBinding,
            };
        }
        RefreshBindingsList();
        SetStatus(preset switch
        {
            Preset.NumpadSafe => Strings.Get("StatusSafePreset"),
            Preset.Stardew => Strings.Get("StatusStardewPreset"),
            _ => Strings.Get("StatusDefaultPreset"),
        });
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!Directory.Exists(gamePathBox.Text.Trim()))
        {
            SetStatus(Strings.Get("StatusGamePathMissing"));
            return;
        }

        config.DialogReadingMode = dialogModeCombo.SelectedIndex;
        config.OrbAnnouncements = orbAnnouncementsCheck.Checked;
        config.SpeechInterrupt = speechInterruptCheck.Checked;
        config.SpeakAudioCaptions = speakAudioCaptionsCheck.Checked;
        config.DialogAutoAdvance = dialogAutoAdvanceCheck.Checked;
        config.AutoInteract = autoInteractCheck.Checked;
        config.SpeechLog = speechLogCheck.Checked;

        try
        {
            config.Save(ConfigPath);
            SetStatus(Strings.Get("StatusSaved"));
            MessageBox.Show(this, Strings.Get("StatusSaved"), Strings.Get("SaveDialogTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            var message = Strings.Get("StatusSaveError", ex.Message);
            SetStatus(message);
            MessageBox.Show(this, message, Strings.Get("SaveDialogTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string message)
    {
        statusLabel.Text = message;
        Tolk.Speak(message);
    }
}
