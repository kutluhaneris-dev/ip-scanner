using IPScanner.Core;

namespace IPScanner.UI;

public class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox _ports = new() { Width = 300 };
    private readonly NumericUpDown _pingTimeout = new() { Minimum = 100, Maximum = 10000, Increment = 100, Width = 90 };
    private readonly NumericUpDown _portTimeout = new() { Minimum = 100, Maximum = 10000, Increment = 100, Width = 90 };
    private readonly NumericUpDown _parallel = new() { Minimum = 1, Maximum = 512, Width = 90 };
    private readonly CheckBox _resolve = new() { Text = "Cihaz adlarını bul (DNS / NetBIOS)", AutoSize = true };
    private readonly CheckBox _probeDead = new() { Text = "Ping'e cevap vermeyen adreslerde portları da dene", AutoSize = true };
    private readonly TextBox _putty = new() { Width = 300 };

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Ayarlar";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        AutoScaleMode = AutoScaleMode.Dpi;

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        void Row(string label, Control c)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) });
            grid.Controls.Add(c);
        }

        var puttyRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var browse = new Button { Text = "Gözat…", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "putty.exe|putty.exe|Programlar|*.exe", Title = "PuTTY'yi seçin" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _putty.Text = dlg.FileName;
        };
        puttyRow.Controls.AddRange(new Control[] { _putty, browse });

        Row("Kontrol edilecek portlar:", _ports);
        Row("Ping zaman aşımı (ms):", _pingTimeout);
        Row("Port zaman aşımı (ms):", _portTimeout);
        Row("Aynı anda taranan adres:", _parallel);
        Row("PuTTY yolu (isteğe bağlı):", puttyRow);
        grid.Controls.Add(_resolve);
        grid.SetColumnSpan(_resolve, 2);
        grid.Controls.Add(_probeDead);
        grid.SetColumnSpan(_probeDead, 2);

        var hint = new Label
        {
            Text = "Port örneği: 21,22,23,80,443,8000-8100",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 0)
        };
        grid.Controls.Add(hint);
        grid.SetColumnSpan(hint, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "Kaydet", AutoSize = true };
        var reset = new Button { Text = "Varsayılan portlar", AutoSize = true };
        reset.Click += (_, _) => _ports.Text = PortList.Default;
        ok.Click += (_, _) => SaveAndClose();
        buttons.Controls.AddRange(new Control[] { cancel, ok, reset });
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        AcceptButton = ok;
        CancelButton = cancel;

        _ports.Text = settings.Ports;
        _pingTimeout.Value = Math.Clamp(settings.PingTimeoutMs, 100, 10000);
        _portTimeout.Value = Math.Clamp(settings.PortTimeoutMs, 100, 10000);
        _parallel.Value = Math.Clamp(settings.Parallelism, 1, 512);
        _resolve.Checked = settings.ResolveNames;
        _probeDead.Checked = settings.ProbePortsOnDead;
        _putty.Text = settings.PuttyPath;
    }

    private void SaveAndClose()
    {
        try
        {
            PortList.Parse(_ports.Text);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, "Port listesi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Ports = _ports.Text.Trim();
        _settings.PingTimeoutMs = (int)_pingTimeout.Value;
        _settings.PortTimeoutMs = (int)_portTimeout.Value;
        _settings.Parallelism = (int)_parallel.Value;
        _settings.ResolveNames = _resolve.Checked;
        _settings.ProbePortsOnDead = _probeDead.Checked;
        _settings.PuttyPath = _putty.Text.Trim();
        _settings.Save();
        DialogResult = DialogResult.OK;
    }
}
