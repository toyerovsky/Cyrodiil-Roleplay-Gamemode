using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Roleplay.Jobs.Server;

namespace Roleplay.Jobs.Tests;

public class PluginMapMarkersTests
{
    private const uint Compressed = 0x00040000;

    [Fact]
    public void A_named_map_marker_in_Tamriel_is_found_with_its_position()
    {
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Record("WRLD", 0x0000003C, 0, Sub("EDID", ZString("Tamriel"))),
                Group(0x0000003C, 1,
                    Group(0, 6,
                        Record("REFR", 0x01000801, 0,
                            Sub("NAME", BitConverter.GetBytes(0x00000010u)),
                            Sub("XMRK", []),
                            Sub("FNAM", [1]),
                            Sub("FULL", ZString("Great Forest Quarry")),
                            Sub("TNAM", [8, 0]),
                            Sub("DATA", Floats(1000f, -2000f, 300f, 0f, 0f, 1.5f)))))));

        var marker = Assert.Single(PluginMapMarkers.Read(plugin));

        Assert.Equal(new PluginMapMarker("Great Forest Quarry", "Tamriel", new Vector3(1000f, -2000f, 300f)), marker);
    }

    [Fact]
    public void A_marker_in_Oblivion_esm_s_Tamriel_is_known_without_the_plugin_carrying_the_worldspace()
    {
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Group(0x0000003C, 1,
                    Record("REFR", 0x01000801, 0, Sub("XMRK", []), Sub("FULL", ZString("Quarry")), Sub("DATA", Floats(1f, 2f, 3f, 0f, 0f, 0f))))));

        Assert.Equal("Tamriel", Assert.Single(PluginMapMarkers.Read(plugin)).Worldspace);
    }

    [Fact]
    public void A_compressed_reference_is_read_through_its_zlib_stream()
    {
        var body = Concat(Sub("XMRK", []), Sub("FULL", ZString("Deep Quarry")), Sub("DATA", Floats(5f, 6f, 7f, 0f, 0f, 0f)));
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Group(0x0000003C, 1,
                    Record("REFR", 0x01000802, Compressed, Compress(body)))));

        Assert.Equal(new PluginMapMarker("Deep Quarry", "Tamriel", new Vector3(5f, 6f, 7f)), Assert.Single(PluginMapMarkers.Read(plugin)));
    }

    [Fact]
    public void References_without_marker_data_or_a_name_are_not_markers()
    {
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Group(0x0000003C, 1,
                    Record("REFR", 0x01000803, 0, Sub("NAME", BitConverter.GetBytes(0x00012345u)), Sub("DATA", Floats(1f, 1f, 1f, 0f, 0f, 0f))),
                    Record("REFR", 0x01000804, 0, Sub("XMRK", []), Sub("DATA", Floats(2f, 2f, 2f, 0f, 0f, 0f))))));

        Assert.Empty(PluginMapMarkers.Read(plugin));
    }

    [Fact]
    public void A_marker_in_another_worldspace_carries_that_worldspace()
    {
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0,
                Record("WRLD", 0x01000900, 0, Sub("EDID", ZString("ChorrolWorld"))),
                Group(0x01000900, 1,
                    Record("REFR", 0x01000901, 0, Sub("XMRK", []), Sub("FULL", ZString("Town Quarry")), Sub("DATA", Floats(9f, 9f, 9f, 0f, 0f, 0f))))));

        Assert.Equal("ChorrolWorld", Assert.Single(PluginMapMarkers.Read(plugin)).Worldspace);
    }

    [Fact]
    public void A_truncated_plugin_is_refused_not_half_read()
    {
        var plugin = Plugin(["Oblivion.esm"],
            Group("WRLD", 0, Group(0x0000003C, 1, Record("REFR", 1, 0, Sub("XMRK", []), Sub("FULL", ZString("Quarry"))))));

        Assert.Throws<InvalidDataException>(() => PluginMapMarkers.Read(plugin.AsSpan(0, plugin.Length - 5)));
    }

    // ---- a plugin, built the way the game files lay one out

    internal static byte[] Plugin(string[] masters, params byte[][] groups)
        => Concat([Record("TES4", 0, 0, [.. masters.SelectMany(master => Sub("MAST", ZString(master)).Concat(Sub("DATA", new byte[8])))]), .. groups]);

    internal static byte[] Group(string label, int type, params byte[][] children) => Group(BinaryPrimitives.ReadUInt32LittleEndian(Encoding.ASCII.GetBytes(label)), type, children);

    internal static byte[] Group(uint label, int type, params byte[][] children)
    {
        var body = Concat(children);
        var header = new byte[20];
        Encoding.ASCII.GetBytes("GRUP").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)(20 + body.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), label);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), type);
        return Concat(header, body);
    }

    internal static byte[] Record(string type, uint formId, uint flags, params byte[][] subrecords)
    {
        var body = Concat(subrecords);
        var header = new byte[20];
        Encoding.ASCII.GetBytes(type).CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), formId);
        return Concat(header, body);
    }

    internal static byte[] Sub(string type, byte[] data)
    {
        var header = new byte[6];
        Encoding.ASCII.GetBytes(type).CopyTo(header, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)data.Length);
        return Concat(header, data);
    }

    private static byte[] Compress(byte[] body)
    {
        using var packed = new MemoryStream();
        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(body);
        return Concat(BitConverter.GetBytes((uint)body.Length), packed.ToArray());
    }

    internal static byte[] ZString(string text) => [.. Encoding.Latin1.GetBytes(text), 0];

    internal static byte[] Floats(params float[] values) => [.. values.SelectMany(BitConverter.GetBytes)];

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
