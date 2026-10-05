using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IPScanner.Core;

public class Scanner
{
    private readonly ScanOptions _options;

    public Scanner(ScanOptions options) => _options = options;

    /// <summary>
    /// Verilen adresleri paralel olarak tarar. Her adres bittiğinde <paramref name="onResult"/>
    /// çağrılır (canlı ya da değil); çağrı tarama iş parçacıklarından gelir.
    /// </summary>
    public async Task RunAsync(IReadOnlyList<uint> addresses, Action<ScanResult> onResult, CancellationToken ct)
    {
        // SendARP engelleyici bir çağrıdır; iş parçacığı havuzunun yetişmesi için alt sınırı yükselt.
        ThreadPool.GetMinThreads(out int worker, out int io);
        ThreadPool.SetMinThreads(Math.Max(worker, _options.Parallelism + 16), io);

        await Parallel.ForEachAsync(addresses,
            new ParallelOptions { MaxDegreeOfParallelism = _options.Parallelism, CancellationToken = ct },
            async (ip, token) =>
            {
                var result = await ScanHostAsync(ip, token);
                if (!token.IsCancellationRequested) onResult(result);
            });
    }

    public async Task<ScanResult> ScanHostAsync(uint ip, CancellationToken ct)
    {
        var address = IpRange.ToAddress(ip);
        var result = new ScanResult { Ip = ip };
        bool local = _options.LocalSubnets.Any(s => IpRange.SameSubnet(ip, s.Address, s.Mask));

        var pingTask = PingAsync(address);
        var arpTask = local ? Task.Run(() => NativeMethods.GetMacAddress(address), ct) : Task.FromResult<string?>(null);
        await Task.WhenAll(pingTask, arpTask);

        result.PingMs = pingTask.Result;
        if (arpTask.Result != null) result.Mac = arpTask.Result;

        var found = new List<string>();
        if (result.PingMs != null) found.Add("Ping");
        if (result.Mac != "") found.Add("ARP");

        bool alive = found.Count > 0;
        if (!alive && !local && _options.ProbePortsOnDead && _options.Ports.Count > 0)
        {
            result.OpenPorts = await ProbePortsAsync(address, ct);
            if (result.OpenPorts.Count > 0) { alive = true; found.Add("Port"); }
        }

        if (_options.LocalAddress == ip)
        {
            alive = true;
            result.Note = "Bu bilgisayar";
            if (result.Mac == "") result.Mac = LocalMac(ip);
        }
        else if (_options.Gateways.Contains(ip))
        {
            result.Note = "Ağ geçidi";
        }

        result.Status = alive ? HostStatus.Alive : HostStatus.Dead;
        result.FoundBy = string.Join(", ", found);
        if (!alive || ct.IsCancellationRequested) return result;

        var portsTask = result.OpenPorts.Count > 0 || _options.Ports.Count == 0
            ? Task.FromResult(result.OpenPorts)
            : ProbePortsAsync(address, ct);
        var nameTask = _options.ResolveNames ? ResolveNameAsync(address, ct) : Task.FromResult<string?>(null);
        var nbTask = _options.ResolveNames || result.Mac == ""
            ? NetBios.QueryAsync(address, 800, ct)
            : Task.FromResult<NetBios.Info?>(null);

        try { await Task.WhenAll(portsTask, nameTask, nbTask); }
        catch (OperationCanceledException) { return result; }

        result.OpenPorts = portsTask.Result;
        var nb = nbTask.Result;
        result.HostName = nameTask.Result ?? "";
        if (nb != null)
        {
            if (result.HostName == "") result.HostName = nb.Name;
            result.Workgroup = nb.Workgroup;
            if (result.Mac == "") result.Mac = nb.Mac;
        }
        result.Vendor = OuiDatabase.Lookup(result.Mac);
        return result;
    }

    private async Task<long?> PingAsync(IPAddress address)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, _options.PingTimeoutMs);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException) { return null; }
    }

    private async Task<List<int>> ProbePortsAsync(IPAddress address, CancellationToken ct)
    {
        var tasks = _options.Ports.Select(async port =>
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.PortTimeoutMs);
            try
            {
                await client.ConnectAsync(address, port, cts.Token);
                return port;
            }
            catch (Exception) { return -1; }
        });
        var results = await Task.WhenAll(tasks);
        return results.Where(p => p > 0).OrderBy(p => p).ToList();
    }

    private static async Task<string?> ResolveNameAsync(IPAddress address, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(1500);
            var entry = await Dns.GetHostEntryAsync(address.ToString(), cts.Token);
            // Kayıt yoksa Windows bazen adresin kendisini döndürür.
            return entry.HostName == address.ToString() ? null : entry.HostName;
        }
        catch (Exception) { return null; }
    }

    private static string LocalMac(uint ip)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork && IpRange.ToUInt(ua.Address) == ip)
                {
                    var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                    return bytes.Length == 6 ? string.Join("-", bytes.Select(b => b.ToString("X2"))) : "";
                }
            }
        }
        return "";
    }
}
