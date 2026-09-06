using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace BlackScreens;

/// <summary>
/// Reads the make and model out of each monitor's EDID, so the Monitors page can say "LG 27GL850"
/// rather than only "Display 2".
/// </summary>
/// <remarks>
/// Windows does not hand this over directly. <c>EnumDisplayDevices</c> reports every panel as
/// "Generic PnP Monitor", so the real name has to come from the EDID block the driver caches in the
/// registry. The parsing below is pure and tested; only <see cref="NamesByDevice"/> touches Win32.
/// </remarks>
public static class MonitorHardware
{
    /// <summary>Maps an adapter device name such as <c>\\.\DISPLAY1</c> to a name for the panel.</summary>
    public static IReadOnlyDictionary<string, string> NamesByDevice()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            var adapter = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
            {
                break;
            }

            if ((adapter.StateFlags & AttachedToDesktop) == 0)
            {
                continue;
            }

            var monitor = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, GetDeviceInterfaceName))
            {
                continue;
            }

            var name = DescribeMonitor(monitor.DeviceID);
            if (!string.IsNullOrEmpty(name))
            {
                names[adapter.DeviceName] = name;
            }
        }

        return names;
    }

    /// <summary>
    /// Turns an interface path like
    /// <c>\\?\DISPLAY#GSM5B09#5&amp;1a2b3c&amp;0&amp;UID4353#{guid}</c> into a readable name.
    /// </summary>
    private static string? DescribeMonitor(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var parts = deviceId.Split('#');
        if (parts.Length < 3)
        {
            return null;
        }

        var hardwareId = parts[1];
        var instance = parts[2];

        var edid = ReadEdid(hardwareId, instance);
        if (edid is not null)
        {
            var described = Describe(edid);
            if (!string.IsNullOrEmpty(described))
            {
                return described;
            }
        }

        // No EDID cached, or it did not parse. The hardware id still carries the vendor and the
        // product code, which is enough to tell two monitors apart.
        return DescribeHardwareId(hardwareId);
    }

    private static byte[]? ReadEdid(string hardwareId, string instance)
    {
        try
        {
            var path = string.Concat(
                @"SYSTEM\CurrentControlSet\Enum\DISPLAY\", hardwareId, @"\", instance, @"\Device Parameters");
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue("EDID") as byte[];
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>Reads the vendor and model out of a raw EDID block, or null when it is not one.</summary>
    public static string? Describe(byte[] edid)
    {
        ArgumentNullException.ThrowIfNull(edid);

        // Every EDID starts 00 FF FF FF FF FF FF 00.
        if (edid.Length < 128 ||
            edid[0] != 0x00 || edid[7] != 0x00 ||
            edid[1] != 0xFF || edid[2] != 0xFF || edid[3] != 0xFF ||
            edid[4] != 0xFF || edid[5] != 0xFF || edid[6] != 0xFF)
        {
            return null;
        }

        var vendor = VendorName(ManufacturerId(edid[8], edid[9]));
        var model = ModelName(edid);

        if (string.IsNullOrEmpty(model))
        {
            // Fall back to the product code, which at least differs between two identical looking
            // monitors from the same maker.
            var product = (edid[11] << 8) | edid[10];
            model = product.ToString("X4", CultureInfo.InvariantCulture);
        }

        return Combine(vendor, model);
    }

    /// <summary>Reads a device hardware id such as <c>GSM5B09</c>, used when there is no EDID.</summary>
    public static string? DescribeHardwareId(string hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId) || hardwareId.Length < 7)
        {
            return null;
        }

        var vendor = VendorName(hardwareId[..3].ToUpperInvariant());
        return Combine(vendor, hardwareId[3..].ToUpperInvariant());
    }

    /// <summary>Unpacks the three letter PNP id packed into two bytes, five bits per letter.</summary>
    private static string ManufacturerId(byte high, byte low)
    {
        var packed = (high << 8) | low;
        return string.Create(3, packed, static (span, value) =>
        {
            span[0] = (char)('A' - 1 + ((value >> 10) & 0x1F));
            span[1] = (char)('A' - 1 + ((value >> 5) & 0x1F));
            span[2] = (char)('A' - 1 + (value & 0x1F));
        });
    }

    /// <summary>
    /// Pulls the model out of the four 18 byte descriptors, the one tagged 0xFC. It is padded with
    /// spaces and ended with a line feed.
    /// </summary>
    private static string? ModelName(byte[] edid)
    {
        for (var offset = 54; offset + 18 <= 126; offset += 18)
        {
            if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 2] != 0 || edid[offset + 3] != 0xFC)
            {
                continue;
            }

            var text = new StringBuilder(13);
            for (var i = offset + 5; i < offset + 18; i++)
            {
                var c = edid[i];
                if (c == 0x0A)
                {
                    break;
                }

                // Anything outside printable ASCII means the block is not really text.
                if (c is < 0x20 or > 0x7E)
                {
                    return null;
                }

                text.Append((char)c);
            }

            var trimmed = text.ToString().Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        return null;
    }

    private static string Combine(string vendor, string model)
    {
        // Plenty of monitors already put the brand in the model, "LG ULTRAGEAR" for one, so do not
        // say it twice.
        return model.StartsWith(vendor, StringComparison.OrdinalIgnoreCase)
            ? model
            : $"{vendor} {model}";
    }

    /// <summary>Maps a PNP id to the name people know the brand by, or returns the id itself.</summary>
    public static string VendorName(string pnpId) => pnpId switch
    {
        "ACI" or "ACR" => "Acer",
        "ACC" => "Accton",
        "AGO" => "AOC",
        "AOC" => "AOC",
        "APP" => "Apple",
        "AUO" => "AU Optronics",
        "AUS" => "ASUS",
        "BNQ" => "BenQ",
        "BOE" => "BOE",
        "CMN" => "Chi Mei",
        "CMO" => "Chi Mei",
        "DEL" => "Dell",
        "ENC" => "Eizo",
        "EIZ" => "Eizo",
        "GBT" => "Gigabyte",
        "GSM" => "LG",
        "HPN" or "HWP" => "HP",
        "HSD" => "Hannspree",
        "IVM" => "Iiyama",
        "LEN" => "Lenovo",
        "LGD" => "LG Display",
        "MSI" => "MSI",
        "NEC" => "NEC",
        "PHL" => "Philips",
        "PIO" => "Pioneer",
        "SAM" => "Samsung",
        "SDC" => "Samsung Display",
        "SEC" => "Samsung",
        "SHP" => "Sharp",
        "SNY" => "Sony",
        "TSB" => "Toshiba",
        "VSC" => "ViewSonic",
        _ => pnpId,
    };

    private const uint GetDeviceInterfaceName = 0x00000001;
    private const uint AttachedToDesktop = 0x00000001;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
}
