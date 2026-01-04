using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.IO.Compression;
using System.Collections.Generic;

public static class SqxSerializer
{
    // Save plain XML .sqx (file itself contains serialized XML)
    public static void SaveAsSqxPlain<T>(string filePath, T model)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(fs, new XmlWriterSettings { Indent = true, Encoding = System.Text.Encoding.UTF8 });
        serializer.Serialize(writer, model);
    }

    // Load plain XML .sqx
    public static T LoadSqxPlain<T>(string filePath)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return (T)serializer.Deserialize(fs);
    }

    // Save zipped .sqx package (panel.xml + assets/)
    public static void SaveAsSqxZip<T>(string filePath, T model, IEnumerable<string>? assetPaths = null)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var mem = new MemoryStream();
        using (var writer = XmlWriter.Create(mem, new XmlWriterSettings { Indent = true, Encoding = System.Text.Encoding.UTF8 }))
            serializer.Serialize(writer, model);
        mem.Seek(0, SeekOrigin.Begin);

        using var zipFs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipFs, ZipArchiveMode.Create);

        var panelEntry = archive.CreateEntry("panel.xml", CompressionLevel.Optimal);
        using (var entryStream = panelEntry.Open())
            mem.CopyTo(entryStream);

        if (assetPaths != null)
        {
            foreach (var assetPath in assetPaths)
            {
                if (!File.Exists(assetPath)) continue;
                var entryName = Path.Combine("assets", Path.GetFileName(assetPath)).Replace('\\', '/');
                var assetEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var assetStream = File.OpenRead(assetPath);
                using var entryStream = assetEntry.Open();
                assetStream.CopyTo(entryStream);
            }
        }
    }

    // Load zipped .sqx package into model and optionally extract assets
    public static T LoadSqxZip<T>(string filePath, string? extractAssetsTo = null)
    {
        using var zipFs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipFs, ZipArchiveMode.Read);

        var panelEntry = archive.GetEntry("panel.xml");
        if (panelEntry == null) throw new InvalidDataException("panel.xml not found in sqx package");

        using var panelStream = panelEntry.Open();
        var serializer = new XmlSerializer(typeof(T));
        var model = (T)serializer.Deserialize(panelStream);

        if (!string.IsNullOrEmpty(extractAssetsTo))
        {
            Directory.CreateDirectory(extractAssetsTo);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var outPath = Path.Combine(extractAssetsTo, Path.GetFileName(entry.FullName));
                    using var inS = entry.Open();
                    using var outFs = File.Create(outPath);
                    inS.CopyTo(outFs);
                }
            }
        }

        return model;
    }
}
