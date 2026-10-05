using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace IPScanner.Core;

/// <summary>
/// Kullanıcının yazdığı IP aralığını ayrıştırır. Desteklenen biçimler (virgül, noktalı virgül
/// veya satır sonuyla birden fazlası birleştirilebilir):
///   192.168.1.5
///   192.168.1.10-192.168.1.50
///   192.168.1.10-50
///   192.168.1.0/24
///   192.168.1.*
/// </summary>
public static class IpRange
{
    /// <summary>Tek seferde taranabilecek en fazla adres sayısı.</summary>
    public const int MaxAddresses = 262_144;

    public static List<uint> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("IP aralığı boş.");

        // "1.2.3.4 - 1.2.3.9" gibi boşluklu yazımları birleştir.
        text = Regex.Replace(text, @"\s*-\s*", "-");
        text = Regex.Replace(text, @"\s*/\s*", "/");

        var tokens = text.Split(new[] { ',', ';', '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<uint>();
        var result = new List<uint>();

        foreach (var token in tokens)
        {
            var (start, end) = ParseToken(token);
            long count = (long)end - start + 1;
            if (result.Count + count > MaxAddresses)
                throw new FormatException($"Aralık çok büyük. En fazla {MaxAddresses:N0} adres taranabilir.");

            for (long i = start; i <= end; i++)
            {
                uint ip = (uint)i;
                if (seen.Add(ip)) result.Add(ip);
            }
        }

        return result;
    }

    /// <summary>Bir kartın adresi ve maskesinden taranacak aralığı metin olarak üretir.</summary>
    public static string FromSubnet(IPAddress address, IPAddress mask)
    {
        uint ip = ToUInt(address);
        uint m = ToUInt(mask);
        int prefix = PrefixLength(m);

        // Çok büyük ağlarda (/20'den geniş) yalnızca kartın bulunduğu /24'ü öner.
        if (prefix < 20)
        {
            prefix = 24;
            m = 0xFFFFFF00;
        }

        if (prefix >= 31)
            return ToString(ip);

        uint network = ip & m;
        uint broadcast = network | ~m;
        return $"{ToString(network + 1)}-{ToString(broadcast - 1)}";
    }

    public static bool SameSubnet(uint a, uint b, uint mask) => (a & mask) == (b & mask);

    public static uint ToUInt(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            throw new FormatException("Yalnızca IPv4 adresleri desteklenir.");
        var b = address.GetAddressBytes();
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }

    public static IPAddress ToAddress(uint ip) =>
        new(new[] { (byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip });

    public static string ToString(uint ip) => $"{ip >> 24}.{(ip >> 16) & 255}.{(ip >> 8) & 255}.{ip & 255}";

    public static int PrefixLength(uint mask)
    {
        int n = 0;
        while (n < 32 && (mask & (0x80000000u >> n)) != 0) n++;
        return n;
    }

    private static (uint start, uint end) ParseToken(string token)
    {
        // CIDR
        int slash = token.IndexOf('/');
        if (slash >= 0)
        {
            uint ip = ParseIp(token[..slash]);
            if (!int.TryParse(token[(slash + 1)..], out int prefix) || prefix < 0 || prefix > 32)
                throw new FormatException($"Geçersiz ağ öneki: {token}");
            uint mask = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
            uint network = ip & mask;
            uint broadcast = network | ~mask;
            // /31 ve /32 dışında ağ ve yayın adreslerini atla.
            return prefix <= 30 ? (network + 1, broadcast - 1) : (network, broadcast);
        }

        // Joker: 192.168.1.* veya 10.0.*.*
        if (token.Contains('*'))
        {
            var parts = token.Split('.');
            if (parts.Length != 4) throw new FormatException($"Geçersiz adres: {token}");
            var lo = new byte[4];
            var hi = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (parts[i] == "*") { lo[i] = i == 3 ? (byte)1 : (byte)0; hi[i] = i == 3 ? (byte)254 : (byte)255; }
                else { lo[i] = hi[i] = ParseOctet(parts[i], token); }
            }
            return (ToUInt(new IPAddress(lo)), ToUInt(new IPAddress(hi)));
        }

        // Aralık
        int dash = token.IndexOf('-');
        if (dash >= 0)
        {
            uint start = ParseIp(token[..dash]);
            string right = token[(dash + 1)..];
            uint end;
            if (right.Contains('.'))
            {
                end = ParseIp(right);
            }
            else
            {
                // 192.168.1.10-50: yalnızca son sekizli verilmiş.
                end = (start & 0xFFFFFF00) | ParseOctet(right, token);
            }
            if (end < start) throw new FormatException($"Aralığın sonu başından küçük: {token}");
            return (start, end);
        }

        uint single = ParseIp(token);
        return (single, single);
    }

    private static uint ParseIp(string s)
    {
        var parts = s.Split('.');
        if (parts.Length != 4) throw new FormatException($"Geçersiz IP adresi: {s}");
        uint ip = 0;
        foreach (var p in parts) ip = ip << 8 | ParseOctet(p, s);
        return ip;
    }

    private static byte ParseOctet(string s, string context)
    {
        if (!byte.TryParse(s, System.Globalization.NumberStyles.None, null, out byte b))
            throw new FormatException($"Geçersiz IP adresi: {context}");
        return b;
    }
}
