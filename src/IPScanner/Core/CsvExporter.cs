using System.Text;

namespace IPScanner.Core;

public static class CsvExporter
{
    /// <summary>Türkçe Excel'in doğrudan açabilmesi için ';' ayraçlı, UTF-8 BOM'lu CSV yazar.</summary>
    public static string Build(IEnumerable<ScanResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Durum;IP;Ad;Çalışma grubu;MAC;Üretici;Yanıt (ms);Açık portlar;Not");
        foreach (var r in results)
        {
            sb.AppendJoin(';', new[]
            {
                r.Status == HostStatus.Alive ? "Canlı" : "Yanıt yok",
                r.IpText, r.HostName, r.Workgroup, r.Mac, r.Vendor,
                r.PingMs?.ToString() ?? "", r.OpenPortsText, r.Note
            }.Select(Escape));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static void Write(string path, IEnumerable<ScanResult> results) =>
        File.WriteAllText(path, Build(results), new UTF8Encoding(true));

    private static string Escape(string s) =>
        s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
