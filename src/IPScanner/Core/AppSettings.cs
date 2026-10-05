using System.Text.Json;

namespace IPScanner.Core;

public class CustomTool
{
    public string Name { get; set; } = "";
    /// <summary>Çalıştırılacak program (ör. C:\Tools\winbox.exe veya putty.exe).</summary>
    public string Program { get; set; } = "";
    /// <summary>Parametreler. {ip}, {host} ve {mac} yer tutucuları kullanılabilir.</summary>
    public string Arguments { get; set; } = "{ip}";
}

public class AppSettings
{
    public string? LastAdapterId { get; set; }
    public string LastRange { get; set; } = "";
    public string Ports { get; set; } = PortList.Default;
    public int PingTimeoutMs { get; set; } = 1000;
    public int PortTimeoutMs { get; set; } = 500;
    public int Parallelism { get; set; } = 128;
    public bool ResolveNames { get; set; } = true;
    public bool ProbePortsOnDead { get; set; } = true;
    public bool ShowOnlyAlive { get; set; } = true;
    public string SshUser { get; set; } = "";
    public string PuttyPath { get; set; } = "";
    public List<CustomTool> CustomTools { get; set; } = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IPScanner", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception)
        {
            // Bozuk ayar dosyası programın açılmasını engellemesin.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Ayar kaydedilemezse sessizce devam et.
        }
    }
}
