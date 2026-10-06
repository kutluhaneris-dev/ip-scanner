using System.Collections.Concurrent;
using System.Diagnostics;
using IPScanner.Core;

namespace IPScanner.UI;

public class MainForm : Form
{
    private const int ColStatus = 0, ColIp = 1, ColName = 2, ColMac = 3, ColVendor = 4, ColPing = 5, ColPorts = 6, ColNote = 7;

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly Launcher _launcher;

    private readonly ComboBox _adapters = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _range = new() { Dock = DockStyle.Fill };
    private readonly Button _scanButton = new() { Text = "Tara", Width = 120, Height = 30 };
    private readonly TextBox _filter = new() { Dock = DockStyle.Fill, PlaceholderText = "IP, ad, MAC, üretici veya port ara…" };
    private readonly CheckBox _onlyAlive = new() { Text = "Sadece canlı cihazlar", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly ResultListView _list = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripProgressBar _progress = new() { Width = 200, Visible = false };
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _flushTimer = new() { Interval = 150 };

    private static readonly string[] EmptyRow = Enumerable.Repeat("", 8).ToArray();
    private readonly List<ScanResult> _results = new();
    private List<ScanResult> _view = new();
    private readonly ConcurrentQueue<ScanResult> _pending = new();
    private CancellationTokenSource? _cts;
    private int _scanned, _total;
    private readonly Stopwatch _watch = new();
    private int _sortColumn = ColIp;
    private bool _sortAscending = true;

    public MainForm()
    {
        _launcher = new Launcher(_settings, this);
        SuspendLayout();
        // Ölçüler 96 DPI'ya göre yazıldı; yüksek DPI ekranlarda orantılı büyütülür.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "IPZ – IP Tarayıcı";
        Font = new Font("Segoe UI", 9.5F);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        ClientSize = new Size(1080, 660);
        MinimumSize = new Size(760, 420);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* simge yoksa varsayılan */ }

        BuildLayout();
        ResumeLayout(false);
        PerformLayout();
        LoadAdapters();

        _flushTimer.Tick += (_, _) => Flush();
        FormClosing += (_, _) =>
        {
            _cts?.Cancel();
            _settings.LastRange = _range.Text.Trim();
            _settings.LastAdapterId = (_adapters.SelectedItem as AdapterInfo)?.Id;
            _settings.ShowOnlyAlive = _onlyAlive.Checked;
            _settings.Save();
        };
        KeyDown += OnFormKeyDown;
    }

    // ---------------------------------------------------------------- Arayüz

    private void BuildLayout()
    {
        var menuStrip = new MenuStrip { Renderer = new Theme.MenuRenderer(), BackColor = Theme.Surface, Padding = new Padding(6, 3, 0, 3) };
        var file = new ToolStripMenuItem("&Dosya");
        file.DropDownItems.Add(new ToolStripMenuItem("CSV olarak &dışa aktar…", null, (_, _) => ExportCsv(), Keys.Control | Keys.S));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Çı&kış", null, (_, _) => Close());
        var tools = new ToolStripMenuItem("&Araçlar");
        tools.DropDownItems.Add("&Ayarlar…", null, (_, _) => { using var f = new SettingsForm(_settings); f.ShowDialog(this); });
        tools.DropDownItems.Add("Ö&zel araçlar…", null, (_, _) => { using var f = new CustomToolsForm(_settings); f.ShowDialog(this); });
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("Ağ kartlarını &yenile", null, (_, _) => LoadAdapters());
        var help = new ToolStripMenuItem("&Yardım");
        help.DropDownItems.Add("&Hakkında", null, (_, _) => ShowAbout());
        menuStrip.Items.AddRange(new ToolStripItem[] { file, tools, help });
        MainMenuStrip = menuStrip;

        var header = new HeaderPanel();

        var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(14, 12, 14, 10), BackColor = Theme.Surface };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Label L(string text) => new()
        {
            Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 10, 0),
            ForeColor = Theme.MutedText, Font = new Font(Font, FontStyle.Bold)
        };

        var refresh = new Button { Text = "Yenile", Width = 120, Height = 30 };
        Theme.StyleSecondary(refresh);
        SetScanButton(scanning: false);
        foreach (var c in new Control[] { _adapters, _range, _filter }) c.Margin = new Padding(3, 4, 8, 4);
        refresh.Margin = _scanButton.Margin = new Padding(3, 3, 0, 3);
        refresh.Click += (_, _) => LoadAdapters();
        top.Controls.Add(L("Ağ kartı:"), 0, 0);
        top.Controls.Add(_adapters, 1, 0);
        top.Controls.Add(refresh, 2, 0);

        top.Controls.Add(L("IP aralığı:"), 0, 1);
        top.Controls.Add(_range, 1, 1);
        top.Controls.Add(_scanButton, 2, 1);

        var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.Controls.Add(_filter, 0, 0);
        filterRow.Controls.Add(_onlyAlive, 1, 0);
        _onlyAlive.Margin = new Padding(12, 4, 0, 0);
        top.Controls.Add(L("Ara:"), 0, 2);
        top.Controls.Add(filterRow, 1, 2);
        top.SetColumnSpan(filterRow, 2);

        var hint = new ToolTip();
        hint.SetToolTip(_range, "Örnekler:\n192.168.1.1-192.168.1.254\n192.168.1.10-50\n10.0.0.0/24\n192.168.1.*\nBirden fazla aralığı virgülle ayırabilirsiniz.");

        _list.Dock = DockStyle.Fill;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = Theme.Surface;
        _list.ForeColor = Theme.Text;
        _list.Columns.Add("Durum", 80);
        _list.Columns.Add("IP adresi", 120);
        _list.Columns.Add("Ad", 180);
        _list.Columns.Add("MAC adresi", 130);
        _list.Columns.Add("Üretici", 190);
        _list.Columns.Add("Yanıt (ms)", 75, HorizontalAlignment.Right);
        _list.Columns.Add("Açık portlar", 220);
        _list.Columns.Add("Not", 110);
        _list.SmallImageList = BuildStatusIcons();
        // Liste küçülürken Windows eski satır numaralarını bir süre daha isteyebilir
        // (odaklı satır, erişilebilirlik araçları). Var olmayan satır için boş öğe ver.
        _list.RetrieveVirtualItem += (_, e) =>
            e.Item = e.ItemIndex >= 0 && e.ItemIndex < _view.Count ? MakeItem(_view[e.ItemIndex], e.ItemIndex) : new ListViewItem(EmptyRow);
        _list.ColumnClick += (_, e) => SortBy(e.Column);
        _list.DoubleClick += (_, _) => { if (Selected is { } r) OpenWeb(r, preferHttps: false); };
        _list.ContextMenuStrip = _menu;
        _menu.Opening += BuildContextMenu;

        var status = new StatusStrip { Renderer = new Theme.MenuRenderer(), BackColor = Theme.Surface, SizingGrip = false };
        _statusLabel.ForeColor = Theme.MutedText;
        status.Items.AddRange(new ToolStripItem[] { _statusLabel, _progress });

        // Liste ile üst panel arasında ince bir ayırıcı çizgi.
        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 1, 0, 0), BackColor = Theme.Border };
        listHost.Controls.Add(_list);

        // Doldurma sırası: önce Fill olan liste, sonra kenara yapışanlar.
        Controls.Add(listHost);
        Controls.Add(top);
        Controls.Add(header);
        Controls.Add(menuStrip);
        Controls.Add(status);

        _scanButton.Click += (_, _) => { if (_cts == null) StartScan(); else StopScan(); };
        _range.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (_cts == null) StartScan(); } };
        _adapters.SelectedIndexChanged += (_, _) =>
        {
            if (_adapters.SelectedItem is AdapterInfo a) _range.Text = a.SuggestedRange;
        };
        _filter.TextChanged += (_, _) => RebuildView();
        _onlyAlive.Checked = _settings.ShowOnlyAlive;
        _onlyAlive.CheckedChanged += (_, _) => RebuildView();

        AcceptButton = null;
        _statusLabel.Text = "Bir ağ kartı seçin veya IP aralığı yazın, sonra Tara'ya basın.";
    }

    private static ImageList BuildStatusIcons()
    {
        // Görüntü yüksekliği satır yüksekliğini de belirler; 22 piksel satırlara nefes aldırır.
        var images = new ImageList { ImageSize = new Size(16, 22), ColorDepth = ColorDepth.Depth32Bit };
        images.Images.Add(Dot(Theme.Alive)); // 0: canlı
        images.Images.Add(Dot(Theme.Dead));  // 1: yanıt yok
        return images;

        static Bitmap Dot(Color c)
        {
            var bmp = new Bitmap(16, 22);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var halo = new SolidBrush(Color.FromArgb(50, c));
            using var brush = new SolidBrush(c);
            g.FillEllipse(halo, 1, 4, 14, 14);
            g.FillEllipse(brush, 4, 7, 8, 8);
            return bmp;
        }
    }

    private void LoadAdapters()
    {
        var previous = (_adapters.SelectedItem as AdapterInfo)?.Id ?? _settings.LastAdapterId;
        string savedRange = _settings.LastRange;
        bool firstLoad = _adapters.Items.Count == 0;

        _adapters.Items.Clear();
        foreach (var a in NetworkAdapters.GetActive()) _adapters.Items.Add(a);

        if (_adapters.Items.Count == 0)
        {
            _statusLabel.Text = "Etkin bir ağ kartı bulunamadı. IP aralığını elle yazabilirsiniz.";
            if (firstLoad) _range.Text = savedRange;
            return;
        }

        int index = _adapters.Items.Cast<AdapterInfo>().ToList().FindIndex(a => a.Id == previous);
        _adapters.SelectedIndex = index >= 0 ? index : 0;

        // İlk açılışta, son kullanılan aralık aynı karta aitse onu geri getir.
        if (firstLoad && index >= 0 && savedRange != "") _range.Text = savedRange;
    }

    // ---------------------------------------------------------------- Tarama

    private void StartScan()
    {
        List<uint> addresses;
        try
        {
            addresses = IpRange.Parse(_range.Text);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, "IP aralığı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _range.Focus();
            return;
        }

        if (addresses.Count > 4096 &&
            MessageBox.Show(this, $"{addresses.Count:N0} adres taranacak. Bu biraz zaman alabilir. Devam edilsin mi?",
                "Büyük aralık", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        ScanOptions options;
        try
        {
            options = BuildOptions();
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, "Ayarlardaki port listesi hatalı: " + ex.Message, "Ayarlar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _results.Clear();
        while (_pending.TryDequeue(out _)) { }
        RebuildView();

        _scanned = 0;
        _total = addresses.Count;
        _progress.Maximum = _total;
        _progress.Value = 0;
        _progress.Visible = true;
        SetScanButton(scanning: true);
        _adapters.Enabled = false;
        _range.ReadOnly = true;
        _watch.Restart();
        _flushTimer.Start();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var scanner = new Scanner(options);

        Task.Run(async () =>
        {
            try
            {
                await scanner.RunAsync(addresses, r =>
                {
                    _pending.Enqueue(r);
                    Interlocked.Increment(ref _scanned);
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                BeginInvoke(() => MessageBox.Show(this, "Tarama sırasında hata: " + ex.Message, "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
        }).ContinueWith(_ => { if (!IsDisposed) BeginInvoke(ScanFinished); });
    }

    private ScanOptions BuildOptions()
    {
        var adapters = NetworkAdapters.GetActive();
        var selected = _adapters.SelectedItem as AdapterInfo;
        return new ScanOptions
        {
            PingTimeoutMs = _settings.PingTimeoutMs,
            PortTimeoutMs = _settings.PortTimeoutMs,
            Parallelism = _settings.Parallelism,
            Ports = PortList.Parse(_settings.Ports),
            ResolveNames = _settings.ResolveNames,
            ProbePortsOnDead = _settings.ProbePortsOnDead,
            LocalSubnets = adapters.Select(a => (IpRange.ToUInt(a.Address), IpRange.ToUInt(a.Mask))).ToList(),
            LocalAddress = selected != null ? IpRange.ToUInt(selected.Address) : null,
            Gateways = adapters.Where(a => a.Gateway != null).Select(a => IpRange.ToUInt(a.Gateway!)).ToHashSet(),
        };
    }

    private void StopScan()
    {
        _cts?.Cancel();
        _scanButton.Enabled = false;
        _statusLabel.Text = "Durduruluyor…";
    }

    private void ScanFinished()
    {
        bool cancelled = _cts?.IsCancellationRequested ?? false;
        _cts?.Dispose();
        _cts = null;
        _watch.Stop();
        _flushTimer.Stop();
        Flush();

        SetScanButton(scanning: false);
        _scanButton.Enabled = true;
        _adapters.Enabled = true;
        _range.ReadOnly = false;
        _progress.Visible = false;

        int alive = _results.Count(r => r.Status == HostStatus.Alive);
        _statusLabel.Text = (cancelled ? "Durduruldu. " : "Tamamlandı. ") +
                            $"{alive} canlı cihaz, {_scanned:N0} / {_total:N0} adres tarandı, {_watch.Elapsed.TotalSeconds:0.0} sn.";
    }

    private void Flush()
    {
        bool changed = false;
        while (_pending.TryDequeue(out var r))
        {
            _results.Add(r);
            changed = true;
        }
        if (changed) RebuildView();

        if (_cts != null)
        {
            _progress.Value = Math.Min(_scanned, _progress.Maximum);
            int alive = _results.Count(r => r.Status == HostStatus.Alive);
            _statusLabel.Text = $"Taranıyor… {_scanned:N0} / {_total:N0}  ·  {alive} canlı cihaz";
        }
    }

    // ---------------------------------------------------------------- Liste

    private void RebuildView()
    {
        var selectedIps = _list.SelectedIndices.Cast<int>().Where(i => i < _view.Count).Select(i => _view[i].Ip).ToHashSet();

        IEnumerable<ScanResult> q = _results;
        if (_onlyAlive.Checked) q = q.Where(r => r.Status == HostStatus.Alive);
        var term = _filter.Text.Trim();
        if (term != "")
        {
            q = q.Where(r =>
                r.IpText.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.HostName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Mac.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Vendor.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.OpenPortsText.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Note.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = q.ToList();
        list.Sort(Compare);
        if (!_sortAscending) list.Reverse();
        _view = list;

        _list.BeginUpdate();
        _list.SelectedIndices.Clear();
        _list.VirtualListSize = _view.Count;
        if (selectedIps.Count > 0)
        {
            for (int i = 0; i < _view.Count; i++)
                if (selectedIps.Contains(_view[i].Ip)) _list.SelectedIndices.Add(i);
        }
        _list.EndUpdate();
        _list.Invalidate();
    }

    private int Compare(ScanResult a, ScanResult b)
    {
        int c = _sortColumn switch
        {
            ColStatus => b.Status.CompareTo(a.Status),
            ColName => CompareText(a.HostName, b.HostName),
            ColMac => CompareText(a.Mac, b.Mac),
            ColVendor => CompareText(a.Vendor, b.Vendor),
            ColPing => (a.PingMs ?? long.MaxValue).CompareTo(b.PingMs ?? long.MaxValue),
            ColPorts => b.OpenPorts.Count.CompareTo(a.OpenPorts.Count),
            ColNote => CompareText(a.Note, b.Note),
            _ => 0
        };
        return c != 0 ? c : a.Ip.CompareTo(b.Ip);
    }

    // Boş değerler her zaman sona gitsin.
    private static int CompareText(string a, string b) =>
        a == "" ? (b == "" ? 0 : 1) : b == "" ? -1 : string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);

    private void SortBy(int column)
    {
        if (_sortColumn == column) _sortAscending = !_sortAscending;
        else { _sortColumn = column; _sortAscending = true; }
        RebuildView();
    }

    private void SetScanButton(bool scanning)
    {
        _scanButton.Text = scanning ? "■  Durdur" : "▶  Tara";
        if (scanning) Theme.StylePrimary(_scanButton, Theme.Danger, Theme.DangerDark);
        else Theme.StylePrimary(_scanButton, Theme.Primary, Theme.PrimaryDark);
    }

    private static ListViewItem MakeItem(ScanResult r, int index)
    {
        bool alive = r.Status == HostStatus.Alive;
        var item = new ListViewItem(new[]
        {
            alive ? "Canlı" : "Yanıt yok",
            r.IpText,
            r.HostName,
            r.Mac,
            r.Vendor,
            r.PingMs?.ToString() ?? "",
            r.OpenPortsText,
            r.Note
        }, alive ? 0 : 1);
        if (!alive) item.ForeColor = Theme.Dead;
        if (index % 2 == 1) item.BackColor = Theme.AltRow;
        return item;
    }

    private ScanResult? Selected =>
        _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _view.Count ? _view[_list.SelectedIndices[0]] : null;

    private IEnumerable<ScanResult> SelectedAll =>
        _list.SelectedIndices.Cast<int>().Where(i => i < _view.Count).Select(i => _view[i]);

    // ---------------------------------------------------------------- Sağ tık menüsü

    private void BuildContextMenu(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _menu.Items.Clear();
        var r = Selected;
        if (r == null) { e.Cancel = true; return; }
        string ip = r.IpText;

        ToolStripMenuItem Item(string text, Action action, bool bold = false)
        {
            var item = new ToolStripMenuItem(text, null, (_, _) => action());
            if (bold) item.Font = new Font(item.Font, FontStyle.Bold);
            return item;
        }

        _menu.Items.Add(Item($"Web tarayıcıda aç  (http://{ip})", () => _launcher.OpenUrl($"http://{ip}"), bold: true));
        _menu.Items.Add(Item($"Web tarayıcıda aç  (https://{ip})", () => _launcher.OpenUrl($"https://{ip}")));
        foreach (var port in r.OpenPorts.Where(p => p is not 80 and not 443))
        {
            if (PortList.IsHttp(port)) _menu.Items.Add(Item($"Web tarayıcıda aç  (http://{ip}:{port})", () => _launcher.OpenUrl($"http://{ip}:{port}")));
            else if (PortList.IsHttps(port)) _menu.Items.Add(Item($"Web tarayıcıda aç  (https://{ip}:{port})", () => _launcher.OpenUrl($"https://{ip}:{port}")));
        }
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Telnet", () => _launcher.Telnet(ip)));
        _menu.Items.Add(Item("SSH…", () => _launcher.Ssh(ip)));
        _menu.Items.Add(Item("Uzak Masaüstü (RDP)", () => _launcher.RemoteDesktop(ip)));
        _menu.Items.Add(Item("Paylaşılan klasörler", () => _launcher.Shares(ip)));
        _menu.Items.Add(Item("FTP", () => _launcher.Ftp(ip)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Sürekli ping (ping -t)", () => _launcher.PingContinuous(ip)));
        _menu.Items.Add(Item("Yol izle (tracert)", () => _launcher.Tracert(ip)));
        _menu.Items.Add(Item("Bu cihazı yeniden tara", () => RescanHost(r)));

        if (_settings.CustomTools.Count > 0)
        {
            _menu.Items.Add(new ToolStripSeparator());
            foreach (var tool in _settings.CustomTools)
                _menu.Items.Add(Item(tool.Name, () => _launcher.RunCustom(tool, r)));
        }

        _menu.Items.Add(new ToolStripSeparator());
        var copy = new ToolStripMenuItem("Kopyala");
        copy.DropDownItems.Add(Item("IP adresi", () => CopyText(string.Join(Environment.NewLine, SelectedAll.Select(x => x.IpText)))));
        if (r.Mac != "") copy.DropDownItems.Add(Item("MAC adresi", () => CopyText(r.Mac)));
        if (r.HostName != "") copy.DropDownItems.Add(Item("Ad", () => CopyText(r.HostName)));
        copy.DropDownItems.Add(Item("Satırın tamamı", () => CopyText(string.Join(Environment.NewLine,
            SelectedAll.Select(x => string.Join("\t", x.IpText, x.HostName, x.Mac, x.Vendor, x.PingMs?.ToString() ?? "", x.OpenPortsText))))));
        _menu.Items.Add(copy);

        if (r.Mac != "" && r.Note != "Bu bilgisayar")
            _menu.Items.Add(Item("Uyandır (Wake-on-LAN)", () => Wake(r)));
    }

    private void OpenWeb(ScanResult r, bool preferHttps)
    {
        bool https = preferHttps || (r.OpenPorts.Contains(443) && !r.OpenPorts.Contains(80));
        _launcher.OpenUrl($"{(https ? "https" : "http")}://{r.IpText}");
    }

    private async void RescanHost(ScanResult old)
    {
        ScanOptions options;
        try { options = BuildOptions(); }
        catch (FormatException ex) { MessageBox.Show(this, ex.Message); return; }

        _statusLabel.Text = $"{old.IpText} yeniden taranıyor…";
        ScanResult fresh;
        try
        {
            fresh = await Task.Run(() => new Scanner(options).ScanHostAsync(old.Ip, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"{old.IpText} yeniden taranamadı: {ex.Message}";
            return;
        }
        int i = _results.FindIndex(x => x.Ip == old.Ip);
        if (i >= 0) _results[i] = fresh; else _results.Add(fresh);
        RebuildView();
        _statusLabel.Text = $"{old.IpText}: {(fresh.Status == HostStatus.Alive ? "canlı" : "yanıt yok")}.";
    }

    private void Wake(ScanResult r)
    {
        try
        {
            System.Net.IPAddress? broadcast = null;
            foreach (var a in NetworkAdapters.GetActive())
            {
                uint mask = IpRange.ToUInt(a.Mask);
                if (IpRange.SameSubnet(r.Ip, IpRange.ToUInt(a.Address), mask))
                    broadcast = IpRange.ToAddress((r.Ip & mask) | ~mask);
            }
            WakeOnLan.Send(r.Mac, broadcast);
            _statusLabel.Text = $"{r.IpText} ({r.Mac}) için uyandırma paketi gönderildi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Wake-on-LAN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void CopyText(string text)
    {
        if (text != "") Clipboard.SetText(text);
    }

    // ---------------------------------------------------------------- Diğer

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5 && _cts == null) { StartScan(); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape && _cts != null) { StopScan(); e.Handled = true; }
        else if (e.KeyCode == Keys.Enter && _list.Focused && Selected is { } r) { OpenWeb(r, false); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.C && _list.Focused)
        {
            CopyText(string.Join(Environment.NewLine, SelectedAll.Select(x => x.IpText)));
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.F) { _filter.Focus(); e.Handled = true; }
    }

    private void ExportCsv()
    {
        if (_view.Count == 0)
        {
            MessageBox.Show(this, "Dışa aktarılacak sonuç yok.", "CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV dosyası (*.csv)|*.csv",
            FileName = $"ip-tarama-{DateTime.Now:yyyy-MM-dd-HHmm}.csv"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            CsvExporter.Write(dlg.FileName, _view);
            _statusLabel.Text = $"{_view.Count} satır kaydedildi: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSV kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowAbout()
    {
        var version = typeof(MainForm).Assembly.GetName().Version;
        MessageBox.Show(this,
            $"IPZ IP Tarayıcı {version?.ToString(3)}\n" +
            $"Geliştiren: {Theme.Author} – {Theme.Company}\n" +
            "ipzproje.com.tr\n\n" +
            "Kısayollar:\n" +
            "  F5  Taramayı başlat\n" +
            "  Esc  Taramayı durdur\n" +
            "  Enter / çift tık  Web tarayıcıda aç\n" +
            "  Ctrl+C  Seçili IP'leri kopyala\n" +
            "  Ctrl+F  Arama kutusuna git\n" +
            "  Ctrl+S  CSV olarak kaydet\n\n" +
            $"Ayar dosyası: {AppSettings.FilePath}",
            "Hakkında", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Titremeyi önlemek için çift tamponlu, sanal modda çalışan liste.</summary>
    private sealed class ResultListView : ListView
    {
        public ResultListView()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = false;
            HideSelection = false;
            VirtualMode = true;
            DoubleBuffered = true;
        }
    }
}
