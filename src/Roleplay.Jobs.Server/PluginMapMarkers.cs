using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace Roleplay.Jobs.Server;

/// <summary>
/// Reads the named map markers out of an Oblivion plugin (.esp or .esm): the references that carry map marker data
/// (an XMRK subrecord) and a name (FULL), with the worldspace each is placed in. Reads only what that needs and nothing
/// else of the format. Records are 20 byte headers (type, size, flags, form id, version control); a group is a
/// GRUP header whose size counts itself; a compressed record (flag 0x40000) holds its size and a zlib stream.
/// </summary>
public static class PluginMapMarkers
{
    private const int HeaderSize = 20;
    private const uint CompressedFlag = 0x00040000;
    // A group of a worldspace's cells, labelled with the worldspace's form id
    private const int WorldChildrenGroup = 1;
    // Tamriel is Oblivion.esm's 0x3C: a plugin placing a marker there need not carry the WRLD record
    private const uint TamrielFormId = 0x3C;

    public static IReadOnlyList<PluginMapMarker> Read(ReadOnlySpan<byte> plugin)
    {
        if (plugin.Length < HeaderSize || !plugin[..4].SequenceEqual("TES4"u8))
            throw new InvalidDataException("Not an Oblivion plugin: it does not start with a TES4 record.");

        var masters = new List<string>();
        var worldspaces = new Dictionary<uint, string>();
        var found = new List<(string Name, uint World, Vector3 Position)>();
        Walk(plugin, masters, worldspaces, found, world: 0);

        return found.Select(marker => new PluginMapMarker(marker.Name, WorldspaceName(marker.World, masters, worldspaces), marker.Position))
            .ToList();
    }

    private static void Walk(ReadOnlySpan<byte> data, List<string> masters, Dictionary<uint, string> worldspaces,
        List<(string, uint, Vector3)> found, uint world)
    {
        var at = 0;
        while (at + HeaderSize <= data.Length)
        {
            var type = Encoding.ASCII.GetString(data.Slice(at, 4));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 4)..]);
            if (type == "GRUP")
            {
                if (size < HeaderSize || at + size > data.Length)
                    throw new InvalidDataException($"A group at {at} claims {size} bytes, past the end of the plugin.");
                var label = BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 8)..]);
                var groupType = BinaryPrimitives.ReadInt32LittleEndian(data[(at + 12)..]);
                Walk(data.Slice(at + HeaderSize, (int)size - HeaderSize), masters, worldspaces, found,
                    groupType == WorldChildrenGroup ? label : world);
                at += (int)size;
                continue;
            }

            if (at + HeaderSize + size > data.Length)
                throw new InvalidDataException($"A {type} record at {at} claims {size} bytes, past the end of the plugin.");
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 8)..]);
            var formId = BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 12)..]);
            var body = data.Slice(at + HeaderSize, (int)size);
            switch (type)
            {
                case "TES4":
                    masters.AddRange(Subrecords(body).Where(sub => sub.Type == "MAST").Select(sub => ZString(sub.Data)));
                    break;
                case "WRLD":
                    if (Subrecords(Body(body, flags)).FirstOrDefault(sub => sub.Type == "EDID") is { Data: not null } editorId)
                        worldspaces[formId] = ZString(editorId.Data);
                    break;
                case "REFR":
                    if (MarkerOf(Body(body, flags)) is { } marker)
                        found.Add((marker.Name, world, marker.Position));
                    break;
            }
            at += HeaderSize + (int)size;
        }

        if (at != data.Length)
            throw new InvalidDataException($"{data.Length - at} bytes left over at {at}, too few for a record.");
    }

    private static (string Name, Vector3 Position)? MarkerOf(byte[] body)
    {
        var isMarker = false;
        string? name = null;
        Vector3? position = null;
        foreach (var (type, data) in Subrecords(body))
        {
            switch (type)
            {
                case "XMRK":
                    isMarker = true;
                    break;
                case "FULL" when isMarker:
                    name = ZString(data);
                    break;
                case "DATA" when data.Length >= 12:
                    position = new Vector3(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4), BitConverter.ToSingle(data, 8));
                    break;
            }
        }
        return isMarker && name is { Length: > 0 } && position is { } at ? (name, at) : null;
    }

    private static byte[] Body(ReadOnlySpan<byte> body, uint flags)
    {
        if ((flags & CompressedFlag) == 0)
            return body.ToArray();

        var size = BinaryPrimitives.ReadUInt32LittleEndian(body);
        using var zlib = new ZLibStream(new MemoryStream(body[4..].ToArray()), CompressionMode.Decompress);
        var inflated = new byte[size];
        zlib.ReadExactly(inflated);
        return inflated;
    }

    // Subrecords are a 4 byte type and a 2 byte size; an XXXX one gives the next one's size as 4 bytes instead
    private static IEnumerable<(string Type, byte[] Data)> Subrecords(ReadOnlySpan<byte> body) => Subrecords(body.ToArray());

    private static IEnumerable<(string Type, byte[] Data)> Subrecords(byte[] body)
    {
        var at = 0;
        uint? nextSize = null;
        while (at + 6 <= body.Length)
        {
            var type = Encoding.ASCII.GetString(body, at, 4);
            var size = (int)(nextSize ?? BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(at + 4)));
            nextSize = null;
            if (at + 6 + size > body.Length)
                throw new InvalidDataException($"A {type} subrecord at {at} claims {size} bytes, past the end of its record.");
            var data = body.AsSpan(at + 6, size).ToArray();
            at += 6 + size;
            if (type == "XXXX")
            {
                nextSize = BinaryPrimitives.ReadUInt32LittleEndian(data);
                continue;
            }
            yield return (type, data);
        }
    }

    private static string ZString(byte[] data)
    {
        var end = Array.IndexOf(data, (byte)0);
        return Encoding.Latin1.GetString(data, 0, end < 0 ? data.Length : end);
    }

    // A form id's top byte picks one of the plugin's masters, or the plugin itself past the last of them
    private static string WorldspaceName(uint world, List<string> masters, Dictionary<uint, string> worldspaces)
    {
        if (worldspaces.TryGetValue(world, out var name))
            return name;
        var master = (int)(world >> 24);
        var fromOblivion = master < masters.Count && masters[master].Equals("Oblivion.esm", StringComparison.OrdinalIgnoreCase);
        return fromOblivion && (world & 0x00FFFFFF) == TamrielFormId ? "Tamriel" : "";
    }
}
