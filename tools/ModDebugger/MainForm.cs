namespace ModDebugger;

/// <summary>
/// A window outside the game that shows everything the mod says, live, and lets the player
/// pin a comment to any line.
///
/// It exists because bug reports from a blind player were being written from memory:
/// something was announced wrong twenty minutes ago, and by the time the game is paused the
/// wording is gone. Here the remark sticks to the exact line, with its timestamp, and comes
/// out as a file.
///
/// Accessibility rules this window is built around: one column (a screen reader reads
/// lines, not grids), everything reachable by keyboard, and the list NEVER scrolls away
/// under the player's fingers - live-follow switches itself off the moment they select a
/// line by hand, or nothing could ever be read while the game keeps talking.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly Label gamePathLabel = new();
    private readonly TextBox gamePathBox = new();
    private readonly Button browseButton = new();

    private readonly Label transcriptLabel = new();
    private readonly ListBox transcriptList = new();
    private readonly CheckBox followCheck = new();

    private readonly Button connectButton = new();
    private readonly Button reloadButton = new();
    private readonly Button commentButton = new();
    private readonly Button exportButton = new();
    private readonly Button saveAsButton = new();

    private readonly Label commandLabel = new();
    private readonly TextBox commandBox = new();
    private readonly Button sendButton = new();
    private readonly TextBox responseBox = new();

    private readonly Label statusLabel = new();

    private readonly Transcript transcript = new();
    private BridgeClient? bridge;

    public MainForm(string? gamePath)
    {
        Text = Strings.Get("Title");
        Width = 900;
        Height = 780;
        StartPosition = FormStartPosition.CenterScreen;

        gamePathLabel.SetBounds(12, 15, 100, 23);
        gamePathLabel.Text = Strings.Get("GamePath");
        gamePathBox.SetBounds(120, 12, 620, 25);
        gamePathBox.AccessibleName = Strings.Get("GamePath");
        gamePathBox.Text = gamePath ?? Installer.GamePathFinder.FindGamePath() ?? "";
        browseButton.SetBounds(750, 11, 120, 27);
        browseButton.Text = Strings.Get("Browse");
        browseButton.Click += (_, _) => Browse();

        transcriptLabel.SetBounds(12, 50, 700, 23);
        transcriptLabel.Text = Strings.Get("Transcript");
        transcriptList.SetBounds(12, 76, 858, 380);
        transcriptList.AccessibleName = Strings.Get("Transcript");
        transcriptList.IntegralHeight = false;
        transcriptList.HorizontalScrollbar = true;
        transcriptList.KeyDown += TranscriptList_KeyDown;
        // Touching the list by hand means "I am reading" - the live feed must not yank the
        // selection away mid-sentence.
        transcriptList.SelectedIndexChanged += (_, _) => { if (userIsReading) followCheck.Checked = false; };
        transcriptList.MouseDown += (_, _) => userIsReading = true;

        followCheck.SetBounds(12, 462, 500, 23);
        followCheck.Text = Strings.Get("Follow");
        followCheck.AccessibleName = Strings.Get("Follow");
        followCheck.Checked = true;

        connectButton.SetBounds(12, 492, 220, 30);
        connectButton.Text = Strings.Get("Connect");
        connectButton.Click += (_, _) => ToggleConnection();

        reloadButton.SetBounds(242, 492, 170, 30);
        reloadButton.Text = Strings.Get("Reload");
        reloadButton.Click += (_, _) => LoadLog();

        commentButton.SetBounds(422, 492, 200, 30);
        commentButton.Text = Strings.Get("Comment");
        commentButton.Click += (_, _) => CommentOnSelection();

        exportButton.SetBounds(632, 492, 110, 30);
        exportButton.Text = Strings.Get("Export");
        exportButton.Click += (_, _) => Export();

        // The report is written to send to somebody: a tester finds the bugs, someone else
        // fixes them. Save-As lets it land on the desktop, or straight into a mail folder,
        // instead of somewhere inside the game's install directory.
        saveAsButton.SetBounds(752, 492, 118, 30);
        saveAsButton.Text = Strings.Get("SaveAs");
        saveAsButton.Click += (_, _) => SaveAs();

        commandLabel.SetBounds(12, 536, 120, 23);
        commandLabel.Text = Strings.Get("CommandLabel");
        commandBox.SetBounds(140, 533, 600, 25);
        commandBox.AccessibleName = Strings.Get("CommandLabel");
        commandBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SendCommand();
        };
        sendButton.SetBounds(750, 532, 120, 27);
        sendButton.Text = Strings.Get("Send");
        sendButton.Click += (_, _) => SendCommand();

        responseBox.SetBounds(12, 566, 858, 130);
        responseBox.AccessibleName = Strings.Get("Response");
        responseBox.Multiline = true;
        responseBox.ReadOnly = true;
        responseBox.ScrollBars = ScrollBars.Vertical;

        statusLabel.SetBounds(12, 704, 858, 30);

        Controls.AddRange(new Control[]
        {
            gamePathLabel, gamePathBox, browseButton,
            transcriptLabel, transcriptList, followCheck,
            connectButton, reloadButton, commentButton, exportButton, saveAsButton,
            commandLabel, commandBox, sendButton, responseBox,
            statusLabel,
        });

        Load += (_, _) =>
        {
            Tolk.Initialize();
            LoadLog();
            if (!string.IsNullOrEmpty(gamePathBox.Text)) ToggleConnection();
        };
        FormClosed += (_, _) =>
        {
            bridge?.Dispose();
            Tolk.Shutdown();
        };
    }

    private bool userIsReading;

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            gamePathBox.Text = dialog.SelectedPath;
            LoadLog();
        }
    }

    private void LoadLog()
    {
        var path = gamePathBox.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        transcript.LoadSpeechLog(path);
        // Comments written in an earlier session come back attached to their lines.
        try { transcript.LoadNotes(path); } catch { /* a missing/damaged notes file is not fatal */ }
        RebuildList();

        Status(transcript.Lines.Count == 0 ? Strings.Get("NoLog") : Strings.Get("Loaded", transcript.Lines.Count));
    }

    private void RebuildList()
    {
        transcriptList.BeginUpdate();
        transcriptList.Items.Clear();
        foreach (var line in transcript.Lines)
        {
            transcriptList.Items.Add(line);
        }
        transcriptList.EndUpdate();
        ScrollToEndIfFollowing();
    }

    private void ScrollToEndIfFollowing()
    {
        if (!followCheck.Checked || transcriptList.Items.Count == 0) return;
        userIsReading = false;
        transcriptList.TopIndex = transcriptList.Items.Count - 1;
        transcriptList.SelectedIndex = transcriptList.Items.Count - 1;
        userIsReading = true;
    }

    private void ToggleConnection()
    {
        if (bridge is { IsConnected: true })
        {
            bridge.Dispose();
            bridge = null;
            connectButton.Text = Strings.Get("Connect");
            Status(Strings.Get("Disconnect"));
            return;
        }

        bridge = new BridgeClient(OnSpoken, OnBridgeStatus);
        if (bridge.Connect(gamePathBox.Text.Trim()))
        {
            connectButton.Text = Strings.Get("Disconnect");
        }
    }

    private void OnSpoken(string text)
    {
        // Arrives on the socket's read thread.
        BeginInvoke(() =>
        {
            var line = transcript.Append(text);
            transcriptList.Items.Add(line);
            ScrollToEndIfFollowing();
        });
    }

    private void OnBridgeStatus(string text) => BeginInvoke(() => Status(text));

    private void CommentOnSelection()
    {
        if (transcriptList.SelectedItem is not SpokenLine line)
        {
            Status(Strings.Get("NoSelection"));
            return;
        }

        using var dialog = new CommentDialog(line, Strings.Get("CommentTitle"), Strings.Get("CommentPrompt"));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        line.Comment = dialog.Comment;
        var index = transcriptList.SelectedIndex;
        transcriptList.Items[index] = line;   // forces the list to re-read ToString()
        transcriptList.SelectedIndex = index;

        // Straight to disk. A comment that only exists in a window is a comment that closing
        // the window destroys - and this tool exists so that nothing has to be remembered.
        try
        {
            transcript.SaveNotes(gamePathBox.Text.Trim());
            Status(Strings.Get("CommentAdded"));
        }
        catch (Exception ex)
        {
            Status(Strings.Get("CommentNotSaved", ex.Message));
        }
    }

    private void Export()
    {
        var path = gamePathBox.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var file = transcript.Export(path);
            Status(Strings.Get("Exported", file));
        }
        catch (Exception ex)
        {
            Status(ex.Message);
        }
    }

    /// <summary>The report, wherever the tester wants it - usually somewhere they can attach it to a message.</summary>
    private void SaveAs()
    {
        var gamePath = gamePathBox.Text.Trim();

        using var dialog = new SaveFileDialog
        {
            Title = Strings.Get("SaveAs"),
            FileName = $"Disco-Bericht-{DateTime.Now:yyyy-MM-dd-HHmm}.txt",
            Filter = Strings.Get("TextFiles") + " (*.txt)|*.txt",
            DefaultExt = "txt",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var file = transcript.Export(gamePath, dialog.FileName);
            Status(Strings.Get("Exported", file));
        }
        catch (Exception ex)
        {
            Status(ex.Message);
        }
    }

    private void SendCommand()
    {
        var command = commandBox.Text.Trim();
        if (command.Length == 0 || bridge is not { IsConnected: true }) return;

        responseBox.Text = bridge.SendCommand(command);
        commandBox.SelectAll();
    }

    private void TranscriptList_KeyDown(object? sender, KeyEventArgs e)
    {
        userIsReading = true;

        if (e.KeyCode is Keys.Enter or Keys.F2)
        {
            e.SuppressKeyPress = true;
            CommentOnSelection();
        }
    }

    /// <summary>Status goes to the screen reader as well - a label nobody hears is a label nobody has.</summary>
    private void Status(string text)
    {
        statusLabel.Text = text;
        Tolk.Speak(text);
    }
}
