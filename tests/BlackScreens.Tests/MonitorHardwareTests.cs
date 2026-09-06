using Xunit;

namespace BlackScreens.Tests;

/// <summary>
/// These build EDID blocks from scratch rather than reading the machine the tests run on, so they
/// give the same answer on any desktop and on the build server, which has no monitor at all.
/// </summary>
public sealed class MonitorHardwareTests
{
    /// <summary>
    /// Builds a block that looks enough like an EDID to parse: the fixed header, a packed
    /// manufacturer id, a product code, and optionally the 0xFC descriptor that carries the model.
    /// </summary>
    private static byte[] Edid(string manufacturer, int productCode, string? model)
    {
        var edid = new byte[128];
        for (var i = 1; i <= 6; i++)
        {
            edid[i] = 0xFF;
        }

        var packed = ((manufacturer[0] - 'A' + 1) << 10)
                   | ((manufacturer[1] - 'A' + 1) << 5)
                   | (manufacturer[2] - 'A' + 1);
        edid[8] = (byte)(packed >> 8);
        edid[9] = (byte)(packed & 0xFF);

        edid[10] = (byte)(productCode & 0xFF);
        edid[11] = (byte)(productCode >> 8);

        if (model is not null)
        {
            // The second of the four descriptors, to prove the search does not just read the first.
            const int offset = 72;
            edid[offset + 3] = 0xFC;
            for (var i = 0; i < model.Length; i++)
            {
                edid[offset + 5 + i] = (byte)model[i];
            }

            if (model.Length < 13)
            {
                edid[offset + 5 + model.Length] = 0x0A;
            }
        }

        return edid;
    }

    /// <summary>
    /// The baseline. Each row is a monitor described the way a real EDID describes one, next to the
    /// name the Monitors page should end up showing for it.
    /// </summary>
    [Theory]
    // A model descriptor is used whenever there is one.
    [InlineData("GSM", 0x5B09, "27GL850", "LG 27GL850")]
    [InlineData("DEL", 0x41B2, "U2723QE", "Dell U2723QE")]
    [InlineData("BNQ", 0x78AA, "XL2411P", "BenQ XL2411P")]
    // Makers that already put the brand in the descriptor should not have it added twice.
    [InlineData("GSM", 0x5B09, "LG ULTRAGEAR", "LG ULTRAGEAR")]
    [InlineData("ACR", 0x0771, "Acer XV272U", "Acer XV272U")]
    // With no descriptor the product code stands in, so two identical panels still differ.
    [InlineData("SAM", 0x0C1B, null, "Samsung 0C1B")]
    [InlineData("AUS", 0x27A1, null, "ASUS 27A1")]
    // A maker not in the table keeps its raw three letter id.
    [InlineData("ZZZ", 0x1234, null, "ZZZ 1234")]
    public void An_edid_reads_out_as_its_baseline_name(
        string manufacturer, int productCode, string? model, string expected)
    {
        Assert.Equal(expected, MonitorHardware.Describe(Edid(manufacturer, productCode, model)));
    }

    /// <summary>
    /// The fallback for a monitor whose EDID the driver never cached. The vendor and product code
    /// are still in the device id Windows hands out.
    /// </summary>
    [Theory]
    [InlineData("GSM5B09", "LG 5B09")]
    [InlineData("DEL41B2", "Dell 41B2")]
    [InlineData("ZZZ0001", "ZZZ 0001")]
    // Too short to hold a vendor and a product code.
    [InlineData("SAM", null)]
    [InlineData("", null)]
    public void A_device_id_reads_out_as_its_baseline_name(string hardwareId, string? expected)
    {
        Assert.Equal(expected, MonitorHardware.DescribeHardwareId(hardwareId));
    }

    [Fact]
    public void Anything_without_the_edid_header_is_rejected()
    {
        Assert.Null(MonitorHardware.Describe(new byte[128]));
        Assert.Null(MonitorHardware.Describe(new byte[8]));
        Assert.Null(MonitorHardware.Describe([]));
    }

    [Fact]
    public void A_descriptor_holding_binary_falls_back_to_the_product_code()
    {
        // Some panels tag a descriptor 0xFC and then put a range limit or padding in it.
        var edid = Edid("GSM", 0x5B09, model: null);
        edid[72 + 3] = 0xFC;
        edid[72 + 5] = 0x01;

        Assert.Equal("LG 5B09", MonitorHardware.Describe(edid));
    }
}
