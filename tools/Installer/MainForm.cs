using System.Windows.Forms;

namespace Installer;

public sealed class MainForm : Form
{
    private readonly Label languageLabel;
    private readonly ComboBox languageCombo;
    private readonly Label gamePathLabel;
    private readonly TextBox gamePathBox;
    private readonly Button browseButton;
    private readonly Button installButton;
    private readonly Button openKeybindEditorButton;
    private readonly CheckBox prereleaseCheck;
    private readonly CheckBox devBridgeCheck;
    private readonly ListBox logList;

    public MainForm()
    {
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;

        languageLabel = new Label { Left = 12, Top = 15, Width = 90 };
        languageCombo = new ComboBox { Left = 105, Top = 12, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        languageCombo.Items.Add("English");
        languageCombo.Items.Add("Deutsch");
        languageCombo.SelectedIndex = Strings.Current == Language.German ? 1 : 0;
        languageCombo.SelectedIndexChanged += (_, _) =>
        {
            Strings.Current = languageCombo.SelectedIndex == 1 ? Language.German : Language.English;
            ApplyLocalization();
        };

        gamePathLabel = new Label { Left = 12, Top = 50, Width = 90 };
        gamePathBox = new TextBox { Left = 105, Top = 47, Width = 400 };
        browseButton = new Button { Left = 515, Top = 46, Width = 100 };
        browseButton.Click += BrowseButton_Click;

        installButton = new Button { Left = 12, Top = 85, Width = 200, Height = 32 };
        installButton.Click += InstallButton_Click;

        openKeybindEditorButton = new Button { Left = 220, Top = 85, Width = 200, Height = 32 };
        openKeybindEditorButton.Click += OpenKeybindEditorButton_Click;

        prereleaseCheck = new CheckBox { Left = 430, Top = 82, Width = 190 };
        devBridgeCheck = new CheckBox { Left = 430, Top = 104, Width = 190 };

        logList = new ListBox { Left = 12, Top = 130, Width = 603, Height = 300, HorizontalScrollbar = true };

        Controls.AddRange(new Control[]
        {
            languageLabel, languageCombo,
            gamePathLabel, gamePathBox, browseButton,
            installButton, openKeybindEditorButton, prereleaseCheck, devBridgeCheck,
            logList,
        });

        ApplyLocalization();

        Load += async (_, _) =>
        {
            Tolk.Initialize();

            // Mandatory-update gate: an outdated installer must not install.
            if (Program.UpdateBlockReason != null)
            {
                installButton.Enabled = false;
                Log(Program.UpdateBlockReason);
            }

            var found = GamePathFinder.FindGamePath();
            if (found != null)
            {
                gamePathBox.Text = found;
                Log(Strings.Get("StatusGameFound", found));
            }
            else
            {
                Log(Strings.Get("StatusGameNotFoundAuto"));
            }
            await Task.CompletedTask;
        };
        FormClosed += (_, _) => Tolk.Shutdown();
    }

    private void ApplyLocalization()
    {
        Text = Strings.Get("WindowTitle");
        languageLabel.Text = Strings.Get("LanguageLabel");
        gamePathLabel.Text = Strings.Get("GamePathLabel");
        browseButton.Text = Strings.Get("Browse");
        installButton.Text = Strings.Get("Install");
        openKeybindEditorButton.Text = Strings.Get("OpenKeybindEditor");
        prereleaseCheck.Text = Strings.Get("PrereleaseCheck");
        prereleaseCheck.AccessibleName = Strings.Get("PrereleaseCheck");
        devBridgeCheck.Text = Strings.Get("DevBridgeCheck");
        devBridgeCheck.AccessibleName = Strings.Get("DevBridgeCheck");
        logList.AccessibleName = Strings.Get("LogAccessible");
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = Strings.Get("BrowseDialogTitle") };
        if (Directory.Exists(gamePathBox.Text)) dialog.SelectedPath = gamePathBox.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            gamePathBox.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        var gamePath = gamePathBox.Text.Trim();
        if (!GamePathFinder.IsValid(gamePath))
        {
            Log(Strings.Get("StatusGamePathMissing"));
            return;
        }

        installButton.Enabled = false;
        browseButton.Enabled = false;

        try
        {
            if (!DotNetRuntime.IsModRuntimePresent())
            {
                var answer = MessageBox.Show(this, Strings.Get("DotNetMissingPrompt"), Strings.Get("DialogTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    await DotNetRuntime.InstallAsync(Log);
                }
                else
                {
                    Log(Strings.Get("DotNetSkipped"));
                }
            }

            var freshConfig = ModInstaller.IsFreshConfig(gamePath);

            Log(Strings.Get("StepCheckMelonLoader"));
            var installMelonLoader = true;
            if (MelonLoaderInstaller.IsInstalled(gamePath))
            {
                Log(Strings.Get("StepMelonLoaderPresent"));
                var result = MessageBox.Show(this, Strings.Get("ReinstallPromptText"), Strings.Get("ReinstallPromptTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                installMelonLoader = result == DialogResult.Yes;
            }

            if (installMelonLoader)
            {
                var exePath = Path.Combine(gamePath, "disco.exe");
                await MelonLoaderInstaller.InstallAsync(gamePath, exePath, Log);
                Log(Strings.Get("StepMelonLoaderDone"));
            }

            var tag = await ModInstaller.InstallLatestAsync(gamePath, Log,
                includePrerelease: prereleaseCheck.Checked,
                confirmOverwrite: (installed, next) =>
                    MessageBox.Show(this, Strings.Get("ModOverwritePrompt", installed, next), Strings.Get("DialogTitle"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
            Log($"[{tag}] " + Strings.Get("StepDone"));

            if (freshConfig)
            {
                var hasNumpad = MessageBox.Show(this, Strings.Get("PresetPromptText"), Strings.Get("PresetPromptTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                await ModInstaller.ApplyPresetAsync(gamePath, hasNumpad == DialogResult.Yes ? "numpad" : "stardew", Log);
            }

            // The configurator and the dev bridge come from the release, like everything else
            // this installer puts on disk - it no longer unpacks executables out of itself.
            await ToolsBundle.EnsureAsync(ModInstaller.DefaultOwner, ModInstaller.DefaultRepo,
                prereleaseCheck.Checked, Log);

            var bridgeResult = ModInstaller.SetDevBridgeEnabled(gamePath, devBridgeCheck.Checked);
            LogDevBridgeResult(bridgeResult);

            CreateStartMenuShortcut(gamePath);

            Tolk.Speak(Strings.Get("InstallCompleteDialog"));
            MessageBox.Show(this, Strings.Get("InstallCompleteDialog"), Strings.Get("DialogTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(Strings.Get("StepError", ex.Message));
            var message = Strings.Get("InstallErrorDialog", ex.Message);
            Tolk.Speak(message);
            MessageBox.Show(this, message, Strings.Get("DialogTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            installButton.Enabled = true;
            browseButton.Enabled = true;
        }
    }

    private void OpenKeybindEditorButton_Click(object? sender, EventArgs e)
    {
        var exe = KeybindEditorLocator.Find();
        if (exe == null)
        {
            Log(Strings.Get("KeybindEditorNotFound"));
            MessageBox.Show(this, Strings.Get("KeybindEditorNotFound"), Strings.Get("DialogTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var gamePath = gamePathBox.Text.Trim();
        var args = GamePathFinder.IsValid(gamePath) ? $"\"{gamePath}\"" : "";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(exe),
        });
    }

    private void LogDevBridgeResult(ModInstaller.DevBridgeResult result)
    {
        switch (result)
        {
            case ModInstaller.DevBridgeResult.Installed:
                Log(Strings.Get("StepDevBridgeInstalled"));
                break;
            case ModInstaller.DevBridgeResult.Removed:
                Log(Strings.Get("StepDevBridgeRemoved"));
                break;
            case ModInstaller.DevBridgeResult.SourceMissing:
                Log(Strings.Get("StepDevBridgeMissing"));
                break;
            // Absent: bridge neither requested nor present - nothing worth logging
        }
    }

    private void CreateStartMenuShortcut(string gamePath)
    {
        var exe = KeybindEditorLocator.Find();
        if (exe == null)
        {
            Log(Strings.Get("StepShortcutSkipped"));
            return;
        }

        Log(StartMenuShortcut.TryCreate(gamePath, exe, out var result)
            ? Strings.Get("StepShortcutCreated", result)
            : Strings.Get("StepShortcutFailed", result));
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => Log(message)));
            return;
        }

        logList.Items.Add(message);
        logList.TopIndex = logList.Items.Count - 1;
        Tolk.Speak(message);
    }
}
