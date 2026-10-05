using System.Net;
using System.Runtime.InteropServices;

namespace IPScanner.Core;

internal static class NativeMethods
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref int physicalAddrLen);

    /// <summary>
    /// Aynı alt ağdaki bir cihazın MAC adresini ARP ile sorar. Ping'i engelleyen cihazlar da
    /// ARP'a cevap vermek zorunda olduğu için yerel ağda en güvenilir bulma yöntemidir.
    /// </summary>
    public static string? GetMacAddress(IPAddress address)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var mac = new byte[6];
        int len = mac.Length;
        uint dest = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
        int rc = SendARP(dest, 0, mac, ref len);
        if (rc != 0 || len < 6 || mac.All(b => b == 0)) return null;
        return string.Join("-", mac.Take(6).Select(b => b.ToString("X2")));
    }
}
