using System.IO.Compression;

namespace IPScanner.Core;

/// <summary>MAC adresinin ilk 3 baytından üretici adını bulur (IEEE OUI listesi, programa gömülü).</summary>
public static class OuiDatabase
{
    private static readonly Lazy<Dictionary<string, string>> Table = new(Load);

    public static string Lookup(string? mac)
    {
        if (string.IsNullOrEmpty(mac)) return "";
        var hex = new string(mac.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (hex.Length < 6) return "";
        return Table.Value.TryGetValue(hex[..6], out var vendor) ? vendor : "";
    }

    private static Dictionary<string, string> Load()
    {
        var dict = new Dictionary<string, string>(40_000, StringComparer.Ordinal);
        using var stream = typeof(OuiDatabase).Assembly.GetManifestResourceStream("IPScanner.oui.txt.gz");
        if (stream == null) return dict;
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int tab = line.IndexOf('\t');
            if (tab == 6) dict[line[..6]] = line[7..];
        }
        return dict;
    }
}
