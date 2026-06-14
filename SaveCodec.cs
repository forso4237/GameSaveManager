using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BTD_Mod_Helper;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

public static class SaveCodec
{
    private const string Prefix = "GSM1";

    private static readonly JsonSerializerOptions Compact =
        new JsonSerializerOptions { WriteIndented = false };

    public static string Encode(SavedGame save)
    {
        try
        {
            if (save == null) return null;

            var json = JsonSerializer.Serialize(save, Compact);
            var raw = Encoding.UTF8.GetBytes(json);

            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                gz.Write(raw, 0, raw.Length);

            return Prefix + Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return null; }
    }

    public static SavedGame Decode(string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            code = code.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
            if (!code.StartsWith(Prefix, StringComparison.Ordinal)) return null;

            var bytes = Convert.FromBase64String(code.Substring(Prefix.Length));

            using var ins = new MemoryStream(bytes);
            using var gz = new GZipStream(ins, CompressionMode.Decompress);
            using var outs = new MemoryStream();
            gz.CopyTo(outs);

            var json = Encoding.UTF8.GetString(outs.ToArray());
            return JsonSerializer.Deserialize<SavedGame>(json);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); return null; }
    }
}
