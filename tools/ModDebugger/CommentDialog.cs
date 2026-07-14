namespace ModDebugger;

/// <summary>Type a remark about one spoken line. Keyboard only: Enter saves, Escape cancels.</summary>
internal sealed class CommentDialog : Form
{
    private readonly TextBox commentBox = new();

    public string Comment => commentBox.Text.Trim();

    public CommentDialog(SpokenLine line, string title, string prompt)
    {
        Text = title;
        Width = 640;
        Height = 260;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        // The line being commented on is read out with the dialog, so the player never has
        // to remember which one they picked.
        var lineLabel = new Label { Left = 12, Top = 12, Width = 600, Height = 60, Text = line.ToString() };

        var promptLabel = new Label { Left = 12, Top = 80, Width = 600, Text = prompt };

        commentBox.SetBounds(12, 106, 600, 25);
        commentBox.AccessibleName = prompt;
        commentBox.Text = line.Comment ?? "";
        commentBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            DialogResult = DialogResult.OK;
        };

        var okButton = new Button { Left = 12, Top = 145, Width = 120, Height = 30, Text = "OK", DialogResult = DialogResult.OK };
        var cancelButton = new Button { Left = 142, Top = 145, Width = 120, Height = 30, Text = "Abbrechen", DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { lineLabel, promptLabel, commentBox, okButton, cancelButton });
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Shown += (_, _) => commentBox.Focus();
    }
}
