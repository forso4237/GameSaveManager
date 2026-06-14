using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BTD_Mod_Helper;
using MelonLoader.Utils;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

public class SaveRef
{
    public string Folder;
    public string FilePath;
    public SavedGame Data;

    public string DisplayName =>
        !string.IsNullOrEmpty(Data?.Name) ? Data.Name : Path.GetFileNameWithoutExtension(FilePath);
}

public static class SaveStore
{
    public const string DefaultFolder = "General";
    public const string LegacyFolder = "Legacy";
    private const string ExportsDir = "_Exports";
    private const string ImportsDir = "_Imports";

    private static readonly char[] Separators = { '/', '\\' };

    private static readonly JsonSerializerOptions JsonPretty =
        new JsonSerializerOptions { WriteIndented = true };

    public static string Root =>
        Path.Combine(MelonEnvironment.UserDataDirectory, "TowerSaveStates");

    public static string ExportsPath => Path.Combine(Root, ExportsDir);
    public static string ImportsPath => Path.Combine(Root, ImportsDir);

    private static bool IsReserved(string segment) =>
        string.Equals(segment, ExportsDir, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(segment, ImportsDir, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(segment, "imported", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegacy(string folder) =>
        string.IsNullOrWhiteSpace(folder) ||
        string.Equals(folder, LegacyFolder, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "";
        var clean = new List<string>();
        foreach (var part in folder.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var s = SanitizeName(part);
            if (!string.IsNullOrEmpty(s)) clean.Add(s);
        }
        return string.Join("/", clean);
    }

    public static string CombineFolder(string parent, string child)
    {
        bool fromRoot = !string.IsNullOrEmpty(child) && (child[0] == '/' || child[0] == '\\');
        var p = (IsLegacy(parent) || fromRoot) ? "" : NormalizeFolder(parent);
        var c = NormalizeFolder(child);
        if (string.IsNullOrEmpty(p)) return c;
        if (string.IsNullOrEmpty(c)) return p;
        return p + "/" + c;
    }

    public static string DirFor(string folder)
    {
        if (IsLegacy(folder)) return Root;
        var rel = NormalizeFolder(folder);
        if (string.IsNullOrEmpty(rel)) return Path.Combine(Root, DefaultFolder);
        var path = Root;
        foreach (var seg in rel.Split('/')) path = Path.Combine(path, seg);
        return path;
    }

    public static List<string> ListFolders()
    {
        var result = new List<string>();
        try
        {
            Directory.CreateDirectory(Root);

            var rels = new List<string>();
            foreach (var dir in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories))
            {
                var rel = dir.Substring(Root.Length).TrimStart('/', '\\').Replace('\\', '/');
                if (string.IsNullOrEmpty(rel)) continue;

                if (rel.Split('/').Any(IsReserved)) continue;
                rels.Add(rel);
            }
            rels.Sort(StringComparer.OrdinalIgnoreCase);

            rels.RemoveAll(n => string.Equals(n, DefaultFolder, StringComparison.OrdinalIgnoreCase));
            rels.Insert(0, DefaultFolder);
            result.AddRange(rels);

            if (Directory.GetFiles(Root, "*.json").Length > 0)
                result.Add(LegacyFolder);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }

        if (result.Count == 0) result.Add(DefaultFolder);
        return result;
    }

    public static List<string> ListChildFolders(string parent)
    {
        var result = new List<string>();
        try
        {
            Directory.CreateDirectory(Root);

            if (string.Equals(parent, LegacyFolder, StringComparison.OrdinalIgnoreCase))
                return result;

            var rel = NormalizeFolder(parent);
            var dir = string.IsNullOrEmpty(rel) ? Root : DirFor(rel);
            if (Directory.Exists(dir))
            {
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (string.IsNullOrEmpty(name) || IsReserved(name)) continue;
                    result.Add(string.IsNullOrEmpty(rel) ? name : rel + "/" + name);
                }
                result.Sort(StringComparer.OrdinalIgnoreCase);
            }

            if (string.IsNullOrEmpty(rel))
            {

                result.RemoveAll(n => string.Equals(n, DefaultFolder, StringComparison.OrdinalIgnoreCase));
                result.Insert(0, DefaultFolder);

                if (Directory.GetFiles(Root, "*.json").Length > 0)
                    result.Add(LegacyFolder);
            }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
        return result;
    }

    public static bool CreateFolder(string relPath)
    {
        try
        {
            var rel = NormalizeFolder(relPath);
            if (string.IsNullOrEmpty(rel)) return false;

            foreach (var seg in rel.Split('/'))
                if (IsReserved(seg) ||
                    string.Equals(seg, LegacyFolder, StringComparison.OrdinalIgnoreCase))
                    return false;

            Directory.CreateDirectory(DirFor(rel));
            return true;
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); return false; }
    }

    public static List<SaveRef> ListSaves(string folder)
    {
        var list = new List<SaveRef>();
        try
        {
            var dir = DirFor(folder);
            if (!Directory.Exists(dir)) return list;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var data = Read(file);
                if (data != null)
                    list.Add(new SaveRef { Folder = folder, FilePath = file, Data = data });
            }
            list.Sort((a, b) => b.Data.SavedAtUtc.CompareTo(a.Data.SavedAtUtc));
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
        return list;
    }

    public static SaveRef MostRecent()
    {
        SaveRef best = null;
        try
        {
            foreach (var folder in ListFolders())
                foreach (var s in ListSaves(folder))
                    if (best == null || s.Data.SavedAtUtc > best.Data.SavedAtUtc) best = s;
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
        return best;
    }

    public static SavedGame Read(string path)
    {
        try { return JsonSerializer.Deserialize<SavedGame>(File.ReadAllText(path)); }
        catch { return null; }
    }

    public static SaveRef Write(string folder, string displayName, SavedGame data)
    {
        try
        {
            if (data == null) return null;

            if (IsLegacy(folder)) folder = DefaultFolder;
            folder = NormalizeFolder(folder);
            if (string.IsNullOrEmpty(folder)) folder = DefaultFolder;

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Save" : displayName.Trim();
            data.Name = displayName;
            data.Folder = folder;

            var dir = DirFor(folder);
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, SanitizeFileName(displayName) + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonPretty));

            return new SaveRef { Folder = folder, FilePath = path, Data = data };
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return null; }
    }

    public static bool Delete(SaveRef save)
    {
        try
        {
            if (save != null && File.Exists(save.FilePath)) { File.Delete(save.FilePath); return true; }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
        return false;
    }

    public static SaveRef MoveSave(SaveRef save, string destFolder)
    {
        try
        {
            if (save == null || !File.Exists(save.FilePath)) return null;

            if (IsLegacy(destFolder)) destFolder = DefaultFolder;
            destFolder = NormalizeFolder(destFolder);
            if (string.IsNullOrEmpty(destFolder)) destFolder = DefaultFolder;

            var destDir = DirFor(destFolder);
            Directory.CreateDirectory(destDir);

            var destPath = Path.Combine(destDir, Path.GetFileName(save.FilePath));

            if (string.Equals(Path.GetFullPath(destPath), Path.GetFullPath(save.FilePath),
                    StringComparison.OrdinalIgnoreCase))
                return save;

            destPath = UniquePath(destDir, Path.GetFileNameWithoutExtension(destPath));
            File.Move(save.FilePath, destPath);

            var data = save.Data;
            if (data != null)
            {
                data.Folder = destFolder;
                try { File.WriteAllText(destPath, JsonSerializer.Serialize(data, JsonPretty)); }
                catch {  }
            }

            return new SaveRef { Folder = destFolder, FilePath = destPath, Data = data };
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return null; }
    }

    public static bool MoveFolder(string folderPath, string destParent)
    {
        try
        {
            var src = NormalizeFolder(folderPath);
            if (string.IsNullOrEmpty(src) ||
                string.Equals(src, LegacyFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            var srcDir = DirFor(src);
            if (!Directory.Exists(srcDir)) return false;

            var leaf = src.Contains('/') ? src.Substring(src.LastIndexOf('/') + 1) : src;
            var destP = IsLegacy(destParent) ? "" : NormalizeFolder(destParent);
            var newRel = string.IsNullOrEmpty(destP) ? leaf : destP + "/" + leaf;

            if (string.Equals(newRel, src, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(destP, src, StringComparison.OrdinalIgnoreCase) ||
                (destP + "/").StartsWith(src + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            var destDir = DirFor(newRel);
            if (Directory.Exists(destDir)) return false;

            var parentDir = string.IsNullOrEmpty(destP) ? Root : DirFor(destP);
            Directory.CreateDirectory(parentDir);
            Directory.Move(srcDir, destDir);
            return true;
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return false; }
    }

    public static bool DeleteFolder(string folderPath)
    {
        try
        {
            var rel = NormalizeFolder(folderPath);
            if (string.IsNullOrEmpty(rel) ||
                string.Equals(rel, LegacyFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var seg in rel.Split('/'))
                if (IsReserved(seg)) return false;

            var dir = DirFor(rel);
            if (!Directory.Exists(dir)) return false;

            Directory.Delete(dir, true);
            return true;
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return false; }
    }

    private static string UniquePath(string dir, string stem)
    {
        var path = Path.Combine(dir, stem + ".json");
        int n = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(dir, $"{stem} ({n}).json");
            n++;
        }
        return path;
    }

    public static string ExportToFile(SaveRef save)
    {
        try
        {
            if (save == null || !File.Exists(save.FilePath)) return null;
            Directory.CreateDirectory(ExportsPath);
            var stem = SanitizeFileName($"{save.Folder}_{save.DisplayName}");
            var path = Path.Combine(ExportsPath, stem + ".json");
            File.Copy(save.FilePath, path, true);
            return path;
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); return null; }
    }

    public static int ImportFromFolder(string targetFolder)
    {
        int count = 0;
        try
        {
            Directory.CreateDirectory(ImportsPath);
            var done = Path.Combine(ImportsPath, "imported");

            foreach (var file in Directory.GetFiles(ImportsPath, "*.json"))
            {
                var data = Read(file);
                if (data == null) continue;

                var name = string.IsNullOrWhiteSpace(data.Name)
                    ? Path.GetFileNameWithoutExtension(file)
                    : data.Name;

                if (Write(targetFolder, name, data) != null)
                {
                    count++;
                    try
                    {
                        Directory.CreateDirectory(done);
                        File.Move(file, Path.Combine(done, Path.GetFileName(file)), true);
                    }
                    catch {  }
                }
            }
        }
        catch (Exception e) { ModHelper.Error<Mod>(e); }
        return count;
    }

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Replace(Path.DirectorySeparatorChar, '_')
                   .Replace(Path.AltDirectorySeparatorChar, '_')
                   .Trim()
                   .Trim('.');
        if (name.Length > 60) name = name.Substring(0, 60).Trim();
        return name;
    }

    public static string SanitizeFileName(string name)
    {
        var n = SanitizeName(name);
        return string.IsNullOrEmpty(n) ? "save" : n;
    }
}
