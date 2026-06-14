using System;
using System.Collections.Generic;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

public static class SaveCatalogUI
{
    private const string RootName = "GameSaveManagerCatalog";

    private const string FolderMark = "";

    private static string _current = SaveStore.DefaultFolder;

    private static readonly List<(RectTransform rect, string dest)> _dropTargets = new();
    private static readonly List<object> _keepAlive = new();
    private static RectTransform _panelRect;
    private static ModHelperComponent _ghost;
    private static bool _dragging;
    private static bool _dragIsFolder;
    private static string _dragFolder;
    private static SaveRef _dragSave;

    public static void Open()
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            _current = (string) Mod.CurrentFolder;
            if (string.IsNullOrWhiteSpace(_current)) _current = SaveStore.DefaultFolder;
            Build();
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void Build()
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            Close();
            ClearDrag();
            _dropTargets.Clear();
            _keepAlive.Clear();
            PersistCurrent();

            bool atRoot = AtRoot();
            var childFolders = SaveStore.ListChildFolders(_current);
            var saves = atRoot ? new List<SaveRef>() : SaveStore.ListSaves(_current);

            var parent = CommonForegroundScreen.instance.transform;
            var panel = parent.gameObject.AddModHelperPanel(
                new Info(RootName, 0, 0, 1180, 940, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();
            _panelRect = panel.GetComponent<RectTransform>();

            panel.AddText(new Info("Title", 0, 410, 760, 64), "Save Catalog", 52f);
            panel.AddText(new Info("Path", 0, 352, 1000, 48), Breadcrumb(), 30f);

            var close = panel.AddButton(
                new Info("Close", 535, 410, 90, 90), VanillaSprites.RedBtn, new Action(Close));
            close.AddText(new Info("X", InfoPreset.FillParent), "X", 48f);

            if (!atRoot)
            {
                var back = panel.AddButton(
                    new Info("Back", -505, 410, 160, 88), VanillaSprites.BlueBtnLong,
                    new Action(() => Navigate(Parent(_current))));
                back.AddText(new Info("b", InfoPreset.FillParent), "< Back", 34f);

                _dropTargets.Add((back.GetComponent<RectTransform>(), Parent(_current)));
            }

            var importFile = panel.AddButton(
                new Info("ImpFile", -370, 270, 350, 80), VanillaSprites.BlueBtnLong,
                new Action(ImportFiles));
            importFile.AddText(new Info("L", InfoPreset.FillParent), "Import files", 34f);

            var importCode = panel.AddButton(
                new Info("ImpCode", 0, 270, 350, 80), VanillaSprites.BlueBtnLong,
                new Action(() => SaveDialogs.OpenCodeImport(TargetFolder(), new Action(Refresh))));
            importCode.AddText(new Info("L", InfoPreset.FillParent), "Import code", 34f);

            var exportsFolder = panel.AddButton(
                new Info("ExpDir", 370, 270, 350, 80), VanillaSprites.BlueBtnLong,
                new Action(ShowExportsFolder));
            exportsFolder.AddText(new Info("L", InfoPreset.FillParent), "Export folder", 32f);

            var list = panel.AddScrollPanel(
                new Info("List", 0, -120, 1110, 560, new Vector2(0.5f, 0.5f)),
                RectTransform.Axis.Vertical, VanillaSprites.BlueInsertPanelRound, 18, 18);

            list.AddScrollContent(MakeNewFolderRow());

            foreach (var child in childFolders)
                list.AddScrollContent(MakeFolderRow(child));

            if (childFolders.Count == 0 && saves.Count == 0)
                list.AddScrollContent(MakeMessageRow(
                    "Nothing here yet. Make a folder, or save a game (F6) to add one."));

            foreach (var save in saves)
                list.AddScrollContent(MakeSaveRow(save));
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    public static void Close()
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            var existing = CommonForegroundScreen.instance.transform.FindChild(RootName);
            if (existing != null)
            {
                existing.gameObject.name = RootName + "_old";
                existing.gameObject.Destroy();
            }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void Refresh() => Build();

    private static bool AtRoot() => string.IsNullOrEmpty(_current);

    private static void Navigate(string path)
    {
        _current = path ?? "";
        PersistCurrent();
        Build();
    }

    private static void PersistCurrent()
    {
        try
        {
            if (!string.IsNullOrEmpty(_current) &&
                !string.Equals(_current, SaveStore.LegacyFolder, StringComparison.OrdinalIgnoreCase))
                Mod.CurrentFolder.SetValueAndSave(_current);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static string TargetFolder()
    {
        if (AtRoot() || string.Equals(_current, SaveStore.LegacyFolder, StringComparison.OrdinalIgnoreCase))
            return SaveStore.DefaultFolder;
        return _current;
    }

    private static string Parent(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (string.Equals(path, SaveStore.LegacyFolder, StringComparison.OrdinalIgnoreCase)) return "";
        int i = path.LastIndexOf('/');
        return i < 0 ? "" : path.Substring(0, i);
    }

    private static string LeafName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (string.Equals(path, SaveStore.LegacyFolder, StringComparison.OrdinalIgnoreCase))
            return SaveStore.LegacyFolder;
        int i = path.LastIndexOf('/');
        return i < 0 ? path : path.Substring(i + 1);
    }

    private static string Breadcrumb()
    {
        if (AtRoot()) return "All folders";
        return _current.Replace("/", "  /  ");
    }

    private static void OnFolderCreated(string created) => Build();

    private static void ConfirmDeleteFolder(string folderPath, int saveCount, int subCount)
    {
        string contents;
        if (saveCount == 0 && subCount == 0)
        {
            contents = "It's empty.";
        }
        else
        {
            var parts = new List<string>();
            if (saveCount > 0) parts.Add($"{saveCount} save{(saveCount == 1 ? "" : "s")}");
            if (subCount > 0) parts.Add($"{subCount} folder{(subCount == 1 ? "" : "s")}");
            contents = "It contains " + string.Join(" and ", parts) + " (and anything inside them).";
        }

        SaveDialogs.OpenConfirm(
            "Delete folder?",
            $"Delete \"{LeafName(folderPath)}\"?\n{contents}\nThis can't be undone.",
            "Delete",
            new Action(() =>
            {
                if (SaveStore.DeleteFolder(folderPath))
                {
                    GameStateManager.Notify($"Deleted folder \"{LeafName(folderPath)}\".");
                    Build();
                }
                else
                {
                    GameStateManager.Notify("Couldn't delete that folder.");
                }
            }));
    }

    private static void ImportFiles()
    {
        try
        {
            var target = TargetFolder();
            int n = SaveStore.ImportFromFolder(target);
            if (n > 0)
            {
                GameStateManager.Notify($"Imported {n} save{(n == 1 ? "" : "s")} into \"{target}\".");
                Refresh();
            }
            else
            {
                GameStateManager.Notify("Drop .json saves into:\n" + SaveStore.ImportsPath);
            }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void ShowExportsFolder()
    {
        try { GameStateManager.Notify("Exported saves are written to:\n" + SaveStore.ExportsPath); }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static ModHelperPanel MakeNewFolderRow()
    {
        var row = ModHelperPanel.Create(new Info("NewFolderRow", 1070, 110), null);
        var btn = row.AddButton(
            new Info("New", 0, 0, 560, 92), VanillaSprites.GreenBtn,
            new Action(() => SaveDialogs.OpenNewFolderPrompt(_current, new Action<string>(OnFolderCreated))));
        btn.AddText(new Info("t", InfoPreset.FillParent), "+ New folder", 36f);
        return row;
    }

    private static ModHelperPanel MakeFolderRow(string childPath)
    {
        int saveCount = SaveStore.ListSaves(childPath).Count;
        int subCount = SaveStore.ListChildFolders(childPath).Count;
        string meta = subCount > 0
            ? $"{saveCount} save{(saveCount == 1 ? "" : "s")} \u00b7 {subCount} folder{(subCount == 1 ? "" : "s")}"
            : $"{saveCount} save{(saveCount == 1 ? "" : "s")}";

        var row = ModHelperPanel.Create(new Info("Folder_" + childPath, 1070, 120), null);

        var btn = row.AddButton(
            new Info("Open", -45, 0, 960, 104), VanillaSprites.BlueInsertPanel,
            new Action(() => Navigate(childPath)));
        btn.AddText(new Info("t", InfoPreset.FillParent),
            $"{FolderMark}<b>{LeafName(childPath)}/</b>   <size=26>{meta}</size>",
            36f, TextAlignmentOptions.Center);

        var del = row.AddButton(
            new Info("Del", 485, 0, 92, 92), VanillaSprites.RedBtn,
            new Action(() => ConfirmDeleteFolder(childPath, saveCount, subCount)));
        del.AddText(new Info("D", InfoPreset.FillParent), "X", 42f);

        AddDragSource(btn, true, childPath, null);
        _dropTargets.Add((btn.GetComponent<RectTransform>(), childPath));
        return row;
    }

    private static ModHelperPanel MakeMessageRow(string text)
    {
        var row = ModHelperPanel.Create(new Info("MsgRow", 1070, 140), null);
        row.AddText(new Info("Txt", InfoPreset.FillParent), text, 36f);
        return row;
    }

    private static ModHelperPanel MakeSaveRow(SaveRef save)
    {
        var row = ModHelperPanel.Create(new Info("Row_" + save.DisplayName, 1070, 200),
            VanillaSprites.BlueInsertPanel);

        row.AddText(new Info("Info", -240, 0, 560, 180), Describe(save), 28f,
            TextAlignmentOptions.Left);

        var load = row.AddButton(
            new Info("Load", 250, 45, 230, 92), VanillaSprites.GreenBtn,
            new Action(() => { GameStateManager.LoadGame(save.Data); Close(); }));
        load.AddText(new Info("L", InfoPreset.FillParent), "Load", 36f);

        var del = row.AddButton(
            new Info("Del", 445, 45, 92, 92), VanillaSprites.RedBtn,
            new Action(() => { SaveStore.Delete(save); GameStateManager.Notify($"Deleted \"{save.DisplayName}\"."); Refresh(); }));
        del.AddText(new Info("D", InfoPreset.FillParent), "X", 42f);

        var code = row.AddButton(
            new Info("Code", 250, -55, 230, 92), VanillaSprites.BlueBtnLong,
            new Action(() => SaveDialogs.OpenCodeExport(save)));
        code.AddText(new Info("C", InfoPreset.FillParent), "Code", 34f);

        var file = row.AddButton(
            new Info("File", 445, -55, 92, 92), VanillaSprites.BlueBtnSquare,
            new Action(() => ExportFile(save)));
        file.AddText(new Info("F", InfoPreset.FillParent), "File", 26f);

        AddDragSource(row, false, null, save);
        return row;
    }

    private static void ExportFile(SaveRef save)
    {
        try
        {
            var path = SaveStore.ExportToFile(save);
            GameStateManager.Notify(path != null ? "Exported to:\n" + path : "Couldn't export that save.");
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static string Describe(SaveRef save)
    {
        var d = save.Data;
        string map = string.IsNullOrEmpty(d.Map) ? "Unknown map" : d.Map;
        string mode = string.IsNullOrEmpty(d.Difficulty) && string.IsNullOrEmpty(d.Mode)
            ? ""
            : $" ({Join(d.Difficulty, d.Mode)})";
        string round = d.Round > 0 ? $"Round {d.Round} \u00b7 " : "";
        string age = Age(d.SavedAtUtc);

        return $"<b>{save.DisplayName}</b>\n{map}{mode}\n" +
               $"{round}{d.Towers.Count} towers \u00b7 ${(long)d.Cash} \u00b7 {(long)d.Lives} lives{age}";
    }

    private static string Join(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b ?? "";
        if (string.IsNullOrEmpty(b)) return a;
        return a + " / " + b;
    }

    private static string Age(DateTime savedUtc)
    {
        if (savedUtc == default) return "";
        var span = DateTime.UtcNow - savedUtc;
        string ago;
        if (span.TotalMinutes < 1) ago = "just now";
        else if (span.TotalHours < 1) ago = $"{(int)span.TotalMinutes}m ago";
        else if (span.TotalDays < 1) ago = $"{(int)span.TotalHours}h ago";
        else ago = $"{(int)span.TotalDays}d ago";
        return " \u00b7 saved " + ago;
    }

    private static void AddDragSource(ModHelperComponent comp, bool isFolder, string folderPath, SaveRef save)
    {
        try
        {
            var go = comp.gameObject;
            var trigger = go.GetComponent<EventTrigger>();
            if (trigger == null) trigger = go.AddComponent<EventTrigger>();

            Action<BaseEventData> begin = _ => BeginDrag(isFolder, folderPath, save);
            Action<BaseEventData> drag = d => OnDrag(d);
            Action<BaseEventData> end = d => OnEndDrag(d);

            AddEntry(trigger, EventTriggerType.BeginDrag, begin);
            AddEntry(trigger, EventTriggerType.Drag, drag);
            AddEntry(trigger, EventTriggerType.EndDrag, end);

            _keepAlive.Add(begin);
            _keepAlive.Add(drag);
            _keepAlive.Add(end);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> cb)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(cb);
        trigger.triggers.Add(entry);
    }

    private static void BeginDrag(bool isFolder, string folderPath, SaveRef save)
    {
        try
        {
            _dragging = true;
            _dragIsFolder = isFolder;
            _dragFolder = folderPath;
            _dragSave = save;

            if (_panelRect == null) return;
            string label = isFolder ? LeafName(folderPath) + "/" : (save?.DisplayName ?? "save");

            var ghost = _panelRect.gameObject.AddModHelperPanel(
                new Info("DragGhost", 0, 0, 380, 92, new Vector2(0.5f, 0.5f)),
                VanillaSprites.BlueInsertPanel);
            ghost.gameObject.transform.SetAsLastSibling();
            ghost.AddText(new Info("t", InfoPreset.FillParent), label, 30f);
            _ghost = ghost;
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void OnDrag(BaseEventData data)
    {
        try
        {
            if (!_dragging || _ghost == null || _panelRect == null) return;
            var ped = data.Cast<PointerEventData>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panelRect, ped.position, CamFor(_panelRect), out var local))
                _ghost.transform.localPosition = new Vector3(local.x, local.y, 0f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void OnEndDrag(BaseEventData data)
    {
        try
        {
            if (!_dragging) { ClearDrag(); return; }

            var ped = data.Cast<PointerEventData>();
            string dest = null;
            bool found = false;
            foreach (var t in _dropTargets)
            {
                if (t.rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(t.rect, ped.position, CamFor(t.rect)))
                {
                    dest = t.dest;
                    found = true;
                    break;
                }
            }

            bool changed = false;
            if (found)
                changed = _dragIsFolder ? DoMoveFolder(_dragFolder, dest) : DoMoveSave(_dragSave, dest);

            ClearDrag();
            if (changed) Build();
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); ClearDrag(); }
    }

    private static bool DoMoveFolder(string src, string destParent)
    {
        string where = string.IsNullOrEmpty(destParent) ? "the top level" : destParent;
        if (SaveStore.MoveFolder(src, destParent))
        {
            GameStateManager.Notify($"Moved \"{LeafName(src)}\" to {where}.");
            return true;
        }
        GameStateManager.Notify("Can't move it there (a folder with that name may already exist).");
        return false;
    }

    private static bool DoMoveSave(SaveRef save, string dest)
    {
        var moved = SaveStore.MoveSave(save, dest);
        if (moved != null)
        {
            GameStateManager.Notify($"Moved \"{moved.DisplayName}\" to {moved.Folder}.");
            return true;
        }
        GameStateManager.Notify("Couldn't move that save.");
        return false;
    }

    private static void ClearDrag()
    {
        _dragging = false;
        _dragIsFolder = false;
        _dragFolder = null;
        _dragSave = null;
        if (_ghost != null)
        {
            try { _ghost.gameObject.Destroy(); } catch {  }
            _ghost = null;
        }
    }

    private static Camera CamFor(RectTransform rt)
    {
        try
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        catch { return null; }
    }
}
