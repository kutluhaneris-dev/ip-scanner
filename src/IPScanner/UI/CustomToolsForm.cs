using IPScanner.Core;

namespace IPScanner.UI;

/// <summary>Sağ tık menüsüne kullanıcının kendi programlarını eklemesi için pencere.</summary>
public class CustomToolsForm : Form
{
    private readonly AppSettings _settings;
    private readonly List<CustomTool> _tools;
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false,
        Dock = DockStyle.Fill, Height = 180
    };
    private readonly TextBox _name = new() { Width = 360 };
    private readonly TextBox _program = new() { Width = 360 };
    private readonly TextBox _args = new() { Width = 360, Text = "{ip}" };

    public CustomToolsForm(AppSettings settings)
    {
        _settings = settings;
        _tools = settings.CustomTools.Select(t => new CustomTool { Name = t.Name, Program = t.Program, Arguments = t.Arguments }).ToList();

        Text = "Özel araçlar";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        AutoScaleMode = AutoScaleMode.Dpi;

        _list.Columns.Add("Ad", 140);
        _list.Columns.Add("Program", 220);
        _list.Columns.Add("Parametreler", 160);
        _list.SelectedIndexChanged += (_, _) => LoadSelected();

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.Controls.Add(_list);
        grid.SetColumnSpan(_list, 2);
        _list.MinimumSize = new Size(540, 180);

        void Row(string label, Control c)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 6) });
            grid.Controls.Add(c);
        }

        var programRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        var browse = new Button { Text = "Gözat…", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Programlar|*.exe;*.bat;*.cmd|Tüm dosyalar|*.*" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _program.Text = dlg.FileName;
        };
        programRow.Controls.AddRange(new Control[] { _program, browse });

        Row("Ad:", _name);
        Row("Program:", programRow);
        Row("Parametreler:", _args);

        var hint = new Label
        {
            Text = "Parametrelerde {ip}, {host} ve {mac} kullanılabilir. Örnek: winbox.exe  →  {ip}",
            AutoSize = true, ForeColor = SystemColors.GrayText
        };
        grid.Controls.Add(hint);
        grid.SetColumnSpan(hint, 2);

        var editButtons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        var add = new Button { Text = "Yeni ekle", AutoSize = true };
        var update = new Button { Text = "Seçileni güncelle", AutoSize = true };
        var remove = new Button { Text = "Sil", AutoSize = true };
        add.Click += (_, _) => AddOrUpdate(-1);
        update.Click += (_, _) => { if (_list.SelectedIndices.Count > 0) AddOrUpdate(_list.SelectedIndices[0]); };
        remove.Click += (_, _) =>
        {
            if (_list.SelectedIndices.Count == 0) return;
            _tools.RemoveAt(_list.SelectedIndices[0]);
            Refill();
        };
        editButtons.Controls.AddRange(new Control[] { add, update, remove });
        grid.Controls.Add(editButtons);
        grid.SetColumnSpan(editButtons, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "Kaydet", AutoSize = true };
        ok.Click += (_, _) =>
        {
            _settings.CustomTools = _tools;
            _settings.Save();
            DialogResult = DialogResult.OK;
        };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        CancelButton = cancel;
        Refill();
    }

    private void AddOrUpdate(int index)
    {
        if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_program.Text))
        {
            MessageBox.Show(this, "Ad ve program alanları boş olamaz.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var tool = new CustomTool { Name = _name.Text.Trim(), Program = _program.Text.Trim(), Arguments = _args.Text.Trim() };
        if (index < 0) _tools.Add(tool); else _tools[index] = tool;
        Refill();
    }

    private void LoadSelected()
    {
        if (_list.SelectedIndices.Count == 0) return;
        var t = _tools[_list.SelectedIndices[0]];
        _name.Text = t.Name;
        _program.Text = t.Program;
        _args.Text = t.Arguments;
    }

    private void Refill()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var t in _tools)
            _list.Items.Add(new ListViewItem(new[] { t.Name, t.Program, t.Arguments }));
        _list.EndUpdate();
    }
}
