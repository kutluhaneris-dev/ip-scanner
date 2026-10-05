using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IPScanner.Core;

/// <summary>
/// NetBIOS durum sorgusu (UDP 137). Windows bilgisayarların adını ve MAC adresini,
/// DNS kaydı olmasa da veya cihaz başka bir alt ağda olsa da döndürür.
/// </summary>
public static class NetBios
{
    public record Info(string Name, string Workgroup, string Mac);

    public static async Task<Info?> QueryAsync(IPAddress address, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            var request = BuildRequest();
            await udp.SendAsync(request, new IPEndPoint(address, 137), ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            var response = await udp.ReceiveAsync(cts.Token);
            return Parse(response.Buffer);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    public static byte[] BuildRequest()
    {
        var packet = new byte[50];
        packet[0] = (byte)Random.Shared.Next(256);
        packet[1] = (byte)Random.Shared.Next(256);
        packet[5] = 1; // QDCOUNT = 1
        packet[12] = 0x20;
        // "*" adı, 16 bayta sıfırla doldurulmuş ve "first level" kodlanmış.
        packet[13] = (byte)('A' + ('*' >> 4));
        packet[14] = (byte)('A' + ('*' & 0x0F));
        for (int i = 15; i < 45; i++) packet[i] = (byte)'A';
        packet[45] = 0;
        packet[47] = 0x21; // NBSTAT
        packet[49] = 0x01; // IN
        return packet;
    }

    public static Info? Parse(byte[] data)
    {
        const int countOffset = 56;
        if (data.Length < countOffset + 1) return null;
        int count = data[countOffset];
        int pos = countOffset + 1;
        if (data.Length < pos + count * 18) return null;

        string name = "", workgroup = "";
        for (int i = 0; i < count; i++, pos += 18)
        {
            string n = Encoding.ASCII.GetString(data, pos, 15).TrimEnd(' ', '\0');
            byte suffix = data[pos + 15];
            bool group = (data[pos + 16] & 0x80) != 0;
            if (suffix == 0x00 && !group && name == "") name = n;
            else if (suffix == 0x00 && group && workgroup == "") workgroup = n;
        }

        string mac = "";
        if (data.Length >= pos + 6)
        {
            var b = data.AsSpan(pos, 6);
            if (b.ToArray().Any(x => x != 0))
                mac = string.Join("-", b.ToArray().Select(x => x.ToString("X2")));
        }

        return name == "" && mac == "" ? null : new Info(name, workgroup, mac);
    }
}
