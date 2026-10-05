namespace IPScanner.Core;

public class ScanOptions
{
    public int PingTimeoutMs { get; set; } = 1000;
    public int PortTimeoutMs { get; set; } = 500;
    public int Parallelism { get; set; } = 128;
    public List<int> Ports { get; set; } = PortList.Parse(PortList.Default);
    public bool ResolveNames { get; set; } = true;
    /// <summary>Ping'e cevap vermeyen (ve ARP ile bulunamayan) adreslerde port denensin mi?</summary>
    public bool ProbePortsOnDead { get; set; } = true;
    /// <summary>Yerel alt ağlar (adres, maske). Bu ağlardaki adreslerde ARP kullanılır.</summary>
    public List<(uint Address, uint Mask)> LocalSubnets { get; set; } = new();
    public uint? LocalAddress { get; set; }
    public HashSet<uint> Gateways { get; set; } = new();
}
