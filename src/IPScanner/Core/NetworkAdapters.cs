using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IPScanner.Core;

public record AdapterInfo(string Id, string Name, string Description, IPAddress Address, IPAddress Mask, IPAddress? Gateway)
{
    public int Prefix => IpRange.PrefixLength(IpRange.ToUInt(Mask));
    public string SuggestedRange => IpRange.FromSubnet(Address, Mask);
    public override string ToString() => $"{Name}  –  {Address}/{Prefix}  ({Description})";
}

public static class NetworkAdapters
{
    /// <summary>Çalışır durumdaki ve IPv4 adresi olan ağ kartlarını listeler.</summary>
    public static List<AdapterInfo> GetActive()
    {
        var list = new List<AdapterInfo>();
        NetworkInterface[] nics;
        try { nics = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { return list; }

        foreach (var nic in nics)
        {
            // Kart o anda bağlanıyor/kopuyorsa (ör. Wi-Fi ağı değişirken) bilgileri okunamayabilir; o kartı atla.
            try
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                var props = nic.GetIPProperties();
                var gateway = props.GatewayAddresses
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (ua.IPv4Mask == null || ua.IPv4Mask.Equals(IPAddress.Any)) continue;
                    // 169.254.x.x: DHCP'den adres alamamış kart.
                    if (ua.Address.GetAddressBytes()[0] == 169 && ua.Address.GetAddressBytes()[1] == 254) continue;
                    list.Add(new AdapterInfo(nic.Id, nic.Name, nic.Description, ua.Address, ua.IPv4Mask, gateway));
                }
            }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException or PlatformNotSupportedException)
            {
            }
        }
        // Ağ geçidi olan kartlar (genelde asıl bağlantı) önce gelsin.
        return list.OrderBy(a => a.Gateway == null).ThenBy(a => a.Name).ToList();
    }
}
