using System.Diagnostics;
using IPScanner.Core;

namespace IPScanner.UI;

/// <summary>Bulunan bir cihaza tarayıcı, Telnet, SSH, RDP vb. ile bağlanır.</summary>
public class Launcher
{
    private readonly AppSettings _settings;
    private readonly IWin32Window _owner;

    public Launcher(AppSettings settings, IWin32Window owner)
    {
        _settings = settings;
        _owner = owner;
    }

    private static string System32 => Environment.GetFolderPath(Environment.SpecialFolder.System);

    public void OpenUrl(string url) => Shell(url);

    public void Telnet(string ip, int port = 23)
    {
        string telnet = Path.Combine(System32, "telnet.exe");
        if (File.Exists(telnet))
        {
            Run(telnet, port == 23 ? ip : $"{ip} {port}");
            return;
        }

        string? putty = FindPutty();
        if (putty != null)
        {
            Run(putty, $"-telnet {ip} -P {port}");
            return;
        }

        var answer = MessageBox.Show(_owner,
            "Windows'un Telnet istemcisi bu bilgisayarda kurulu değil ve PuTTY de bulunamadı.\n\n" +
            "Telnet istemcisini şimdi kurmak ister misiniz? (Yönetici izni istenecek.)\n\n" +
            "Alternatif: Ayarlar'dan PuTTY'nin yerini gösterebilirsiniz.",
            "Telnet bulunamadı", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(System32, "dism.exe"),
                Arguments = "/online /Enable-Feature /FeatureName:TelnetClient /NoRestart",
                UseShellExecute = true,
                Verb = "runas"
            })?.WaitForExit();
            if (File.Exists(telnet)) Run(telnet, port == 23 ? ip : $"{ip} {port}");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Kullanıcı yönetici iznini reddetti.
        }
    }

    public void Ssh(string ip)
    {
        string user = InputDialog.Show(_owner, "SSH bağlantısı", $"{ip} için kullanıcı adı (boş bırakılabilir):", _settings.SshUser);
        if (user == InputDialog.Cancelled) return;
        _settings.SshUser = user;
        _settings.Save();
        string target = user == "" ? ip : $"{user}@{ip}";

        string ssh = Path.Combine(System32, "OpenSSH", "ssh.exe");
        if (File.Exists(ssh))
        {
            // cmd /k: bağlantı koparsa hata mesajı okunabilsin diye pencere açık kalır.
            Run("cmd.exe", $"/k \"\"{ssh}\" {target}\"");
            return;
        }

        string? putty = FindPutty();
        if (putty != null)
        {
            Run(putty, $"-ssh {target}");
            return;
        }

        MessageBox.Show(_owner,
            "SSH istemcisi bulunamadı. Windows Ayarları > Uygulamalar > İsteğe bağlı özellikler bölümünden " +
            "\"OpenSSH İstemcisi\"ni ekleyin ya da Ayarlar'dan PuTTY'nin yerini gösterin.",
            "SSH bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void RemoteDesktop(string ip) => Run(Path.Combine(System32, "mstsc.exe"), $"/v:{ip}");

    public void Shares(string ip) => Run("explorer.exe", $"\\\\{ip}");

    public void Ftp(string ip) => Run("explorer.exe", $"ftp://{ip}/");

    public void PingContinuous(string ip) => Run("cmd.exe", $"/k ping -t {ip}");

    public void Tracert(string ip) => Run("cmd.exe", $"/k tracert {ip}");

    public void RunCustom(CustomTool tool, ScanResult r)
    {
        string args = tool.Arguments
            .Replace("{ip}", r.IpText, StringComparison.OrdinalIgnoreCase)
            .Replace("{host}", r.HostName, StringComparison.OrdinalIgnoreCase)
            .Replace("{mac}", r.Mac, StringComparison.OrdinalIgnoreCase);
        Run(Environment.ExpandEnvironmentVariables(tool.Program), args);
    }

    private string? FindPutty()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.PuttyPath)) candidates.Add(_settings.PuttyPath);
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY", "putty.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PuTTY", "putty.exe"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "putty.exe"));
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(dir.Trim(), "putty.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }

    private void Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(target, ex);
        }
    }

    private void Run(string program, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(program, arguments) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError(program, ex);
        }
    }

    private void ShowError(string what, Exception ex) =>
        MessageBox.Show(_owner, $"Açılamadı: {what}\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
