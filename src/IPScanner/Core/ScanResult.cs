namespace IPScanner.Core;

public enum HostStatus { Dead, Alive }

public class ScanResult
{
    public uint Ip { get; init; }
    public string IpText => IpRange.ToString(Ip);
    public HostStatus Status { get; set; }
    public string HostName { get; set; } = "";
    public string Workgroup { get; set; } = "";
    public string Mac { get; set; } = "";
    public string Vendor { get; set; } = "";
    /// <summary>Ping yanıt süresi (ms). Ping'e cevap vermediyse null.</summary>
    public long? PingMs { get; set; }
    public List<int> OpenPorts { get; set; } = new();
    /// <summary>"Bu bilgisayar", "Ağ geçidi" gibi açıklama.</summary>
    public string Note { get; set; } = "";
    /// <summary>Cihazın nasıl bulunduğu: Ping, ARP, Port.</summary>
    public string FoundBy { get; set; } = "";

    public string OpenPortsText => string.Join(", ", OpenPorts.Select(p =>
    {
        var d = PortList.Describe(p);
        return d == "" ? p.ToString() : $"{p} ({d})";
    }));
}
