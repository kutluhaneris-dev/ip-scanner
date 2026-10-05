using System.Net;
using System.Net.Sockets;

namespace IPScanner.Core;

public static class WakeOnLan
{
    public static byte[] BuildMagicPacket(string mac)
    {
        var hex = new string(mac.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length != 12) throw new FormatException("Geçersiz MAC adresi.");
        var macBytes = Convert.FromHexString(hex);
        var packet = new byte[6 + 16 * 6];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 0; i < 16; i++) Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6);
        return packet;
    }

    /// <summary>Sihirli paketi hem genel yayına hem de cihazın alt ağ yayın adresine gönderir.</summary>
    public static void Send(string mac, IPAddress? subnetBroadcast)
    {
        var packet = BuildMagicPacket(mac);
        using var udp = new UdpClient { EnableBroadcast = true };
        udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
        if (subnetBroadcast != null)
            udp.Send(packet, packet.Length, new IPEndPoint(subnetBroadcast, 9));
    }
}
