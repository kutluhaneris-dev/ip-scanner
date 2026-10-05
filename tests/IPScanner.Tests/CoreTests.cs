using System.Net;
using IPScanner.Core;
using Xunit;

namespace IPScanner.Tests;

public class IpRangeTests
{
    private static List<string> P(string s) => IpRange.Parse(s).Select(IpRange.ToString).ToList();

    [Fact]
    public void SingleAddress() => Assert.Equal(new[] { "10.0.0.5" }, P("10.0.0.5"));

    [Fact]
    public void FullRange()
    {
        var r = P("192.168.1.250-192.168.2.2");
        Assert.Equal(new[] { "192.168.1.250", "192.168.1.251", "192.168.1.252", "192.168.1.253", "192.168.1.254", "192.168.1.255", "192.168.2.0", "192.168.2.1", "192.168.2.2" }, r);
    }

    [Fact]
    public void ShortRangeWithSpaces() => Assert.Equal(3, P("192.168.1.10 - 12").Count);

    [Fact]
    public void Cidr24SkipsNetworkAndBroadcast()
    {
        var r = P("192.168.5.77/24");
        Assert.Equal(254, r.Count);
        Assert.Equal("192.168.5.1", r[0]);
        Assert.Equal("192.168.5.254", r[^1]);
    }

    [Fact]
    public void Cidr32() => Assert.Equal(new[] { "10.1.1.1" }, P("10.1.1.1/32"));

    [Fact]
    public void Wildcard()
    {
        var r = P("172.16.3.*");
        Assert.Equal(254, r.Count);
        Assert.Equal("172.16.3.1", r[0]);
    }

    [Fact]
    public void MultipleTokensAreMergedWithoutDuplicates() =>
        Assert.Equal(4, P("10.0.0.1-3, 10.0.0.2; 10.0.0.9").Count);

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.300")]
    [InlineData("192.168.1.50-10")]
    [InlineData("10.0.0.0/33")]
    [InlineData("abc")]
    [InlineData("10.0.0.0/8")]
    public void Invalid(string s) => Assert.Throws<FormatException>(() => IpRange.Parse(s));

    [Theory]
    [InlineData("192.168.1.23", "255.255.255.0", "192.168.1.1-192.168.1.254")]
    [InlineData("10.20.30.40", "255.255.240.0", "10.20.16.1-10.20.31.254")]
    [InlineData("10.20.30.40", "255.0.0.0", "10.20.30.1-10.20.30.254")]
    public void SuggestedRangeFromSubnet(string ip, string mask, string expected) =>
        Assert.Equal(expected, IpRange.FromSubnet(IPAddress.Parse(ip), IPAddress.Parse(mask)));

    [Fact]
    public void PrefixLength() => Assert.Equal(22, IpRange.PrefixLength(IpRange.ToUInt(IPAddress.Parse("255.255.252.0"))));
}

public class PortListTests
{
    [Fact]
    public void ParsesListAndRanges() =>
        Assert.Equal(new[] { 22, 80, 81, 82, 443 }, PortList.Parse("443, 22, 80-82, 22"));

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("http")]
    [InlineData("1-5000")]
    public void Invalid(string s) => Assert.Throws<FormatException>(() => PortList.Parse(s));

    [Fact]
    public void DefaultIsValid() => Assert.Equal(8, PortList.Parse(PortList.Default).Count);
}

public class NetBiosTests
{
    [Fact]
    public void RequestIsWellFormed()
    {
        var p = NetBios.BuildRequest();
        Assert.Equal(50, p.Length);
        Assert.Equal((byte)'C', p[13]);
        Assert.Equal((byte)'K', p[14]);
        Assert.Equal(0x21, p[47]);
    }

    [Fact]
    public void ParsesStatusResponse()
    {
        var data = new byte[57 + 3 * 18 + 6];
        data[56] = 3;
        WriteName(data, 57, "WORKGROUP", 0x00, group: true);
        WriteName(data, 75, "OFIS-PC", 0x00, group: false);
        WriteName(data, 93, "OFIS-PC", 0x20, group: false);
        new byte[] { 0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E }.CopyTo(data, 111);

        var info = NetBios.Parse(data);
        Assert.NotNull(info);
        Assert.Equal("OFIS-PC", info!.Name);
        Assert.Equal("WORKGROUP", info.Workgroup);
        Assert.Equal("00-1A-2B-3C-4D-5E", info.Mac);
    }

    [Fact]
    public void ShortPacketIsIgnored() => Assert.Null(NetBios.Parse(new byte[20]));

    private static void WriteName(byte[] data, int pos, string name, byte suffix, bool group)
    {
        System.Text.Encoding.ASCII.GetBytes(name.PadRight(15)).CopyTo(data, pos);
        data[pos + 15] = suffix;
        data[pos + 16] = group ? (byte)0x80 : (byte)0x04;
    }
}

public class MiscTests
{
    [Fact]
    public void VendorLookup()
    {
        // 00-00-0C: Cisco'nun en eski OUI kaydı.
        Assert.Contains("Cisco", OuiDatabase.Lookup("00-00-0C-12-34-56"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", OuiDatabase.Lookup(""));
    }

    [Fact]
    public void MagicPacket()
    {
        var p = WakeOnLan.BuildMagicPacket("01:23:45:67:89:AB");
        Assert.Equal(102, p.Length);
        Assert.All(p.Take(6), b => Assert.Equal(0xFF, b));
        Assert.Equal(0xAB, p[101]);
    }

    [Fact]
    public void CsvEscapesSeparators()
    {
        var csv = CsvExporter.Build(new[]
        {
            new ScanResult { Ip = IpRange.ToUInt(IPAddress.Parse("10.0.0.1")), Status = HostStatus.Alive, HostName = "a;b", OpenPorts = { 80 } }
        });
        Assert.Contains("Canlı;10.0.0.1;\"a;b\"", csv);
        Assert.Contains("80 (HTTP)", csv);
    }
}
