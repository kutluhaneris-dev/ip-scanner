namespace IPScanner.UI;

/// <summary>Tek satırlık metin isteyen küçük pencere.</summary>
public static class InputDialog
{
    public const string Cancelled = "\0";

    public static string Show(IWin32Window owner, string title, string prompt, string value)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            ShowInTaskbar = false,
        };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        var label = new Label { Text = prompt, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var box = new TextBox { Text = value, Width = 320 };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        layout.Controls.AddRange(new Control[] { label, box, buttons });
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : Cancelled;
    }
}
