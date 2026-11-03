namespace CyrodiilRP.Server.Admin;

// The game's data the research's tools/esm/export_admin_data.py writes, built into the assembly.
internal static class EmbeddedData
{
    public const string Items = "items.json";
    public const string Places = "places.json";
    public const string Weathers = "weathers.json";

    public static string Read(string resource)
    {
        using var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"The server was built without {resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
