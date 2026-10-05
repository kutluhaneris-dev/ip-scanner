namespace IPScanner.Core;

public static class PortList
{
    public const string Default = "21,22,23,80,443,445,3389,8080";

    /// <summary>"21,22,80-85" biçimindeki port listesini ayrıştırır.</summary>
    public static List<int> Parse(string? text)
    {
        var ports = new SortedSet<int>();
        if (string.IsNullOrWhiteSpace(text)) return ports.ToList();

        foreach (var raw in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim();
            int dash = part.IndexOf('-');
            if (dash > 0)
            {
                int a = ParsePort(part[..dash]), b = ParsePort(part[(dash + 1)..]);
                if (b < a) (a, b) = (b, a);
                if (b - a > 1024) throw new FormatException($"Port aralığı çok geniş: {part}");
                for (int p = a; p <= b; p++) ports.Add(p);
            }
            else
            {
                ports.Add(ParsePort(part));
            }
        }
        return ports.ToList();
    }

    private static int ParsePort(string s)
    {
        if (!int.TryParse(s.Trim(), out int p) || p < 1 || p > 65535)
            throw new FormatException($"Geçersiz port: {s}");
        return p;
    }

    public static string Describe(int port) => port switch
    {
        21 => "FTP",
        22 => "SSH",
        23 => "Telnet",
        25 => "SMTP",
        53 => "DNS",
        80 => "HTTP",
        139 => "NetBIOS",
        443 => "HTTPS",
        445 => "SMB",
        554 => "RTSP",
        1883 => "MQTT",
        3306 => "MySQL",
        3389 => "RDP",
        5900 => "VNC",
        8080 => "HTTP",
        8443 => "HTTPS",
        8291 => "Winbox",
        9100 => "Yazıcı",
        _ => ""
    };

    public static bool IsHttp(int port) => port is 80 or 8000 or 8008 or 8080 or 8081 or 8088 or 8888;
    public static bool IsHttps(int port) => port is 443 or 8443 or 4443 or 9443;
}
