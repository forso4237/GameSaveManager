using System;
using System.Collections.Generic;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppTMPro;
using UnityEngine;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

internal static class Clipboard
{
    public static bool Set(string text)
    {
        try { GUIUtility.systemCopyBuffer = text ?? ""; return true; }
        catch (Exception e) { ModHelper.Warning<Mod>(e); return false; }
    }

    public static string Get()
    {
        try { return GUIUtility.systemCopyBuffer ?? ""; }
        catch (Exception e) { ModHelper.Warning<Mod>(e); return ""; }
    }
}

public static class SaveDialogs
{
    private const string NameRoot = "GSM_NameDialog";
    private const string FolderRoot = "GSM_NewFolder";
    private const string CodeExportRoot = "GSM_CodeExport";
    private const string CodeImportRoot = "GSM_CodeImport";
    private const string ConfirmRoot = "GSM_Confirm";

    private static SavedGame _pending;
    private static List<string> _folders = new();
    private static int _folderIndex;
    private static ModHelperText _folderLabel;
    private static ModHelperInputField _nameField;

    public static void OpenNameDialog(SavedGame pending)
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            _pending = pending;
            CloseByName(NameRoot);

            _folders = SaveStore.ListFolders();
            var cur = (string) Mod.CurrentFolder;
            _folderIndex = _folders.FindIndex(f => string.Equals(f, cur, StringComparison.OrdinalIgnoreCase));
            if (_folderIndex < 0) _folderIndex = 0;

            var parent = CommonForegroundScreen.instance.transform;
            var panel = parent.gameObject.AddModHelperPanel(
                new Info(NameRoot, 0, 0, 780, 540, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();

            panel.AddText(new Info("Title", 0, 215, 700, 60), "Name this save", 48f);

            panel.AddText(new Info("NameLbl", -250, 120, 240, 50), "Save name", 30f, TextAlignmentOptions.Left);
            _nameField = panel.AddInputField(
                new Info("NameInput", 60, 120, 420, 80),
                GameStateManager.DefaultSaveName(pending),
                VanillaSprites.BlueInsertPanelRound);

            panel.AddText(new Info("FolderLbl", -250, 25, 240, 50), "Folder", 30f, TextAlignmentOptions.Left);

            var prev = panel.AddButton(new Info("FPrev", -30, 25, 70, 70), VanillaSprites.BlueBtnSquare,
                new Action(() => CycleFolder(-1)));
            prev.AddText(new Info("l", InfoPreset.FillParent), "<", 44f);

            _folderLabel = panel.AddText(new Info("FName", 130, 25, 240, 60), "", 32f);

            var next = panel.AddButton(new Info("FNext", 290, 25, 70, 70), VanillaSprites.BlueBtnSquare,
                new Action(() => CycleFolder(1)));
            next.AddText(new Info("l", InfoPreset.FillParent), ">", 44f);

            var newF = panel.AddButton(new Info("FNew", 60, -65, 420, 70), VanillaSprites.BrightBlueBtn,
                new Action(OpenNewFolderFromNameDialog));
            newF.AddText(new Info("l", InfoPreset.FillParent), "+ New folder", 32f);

            UpdateFolderLabel();

            var save = panel.AddButton(new Info("Save", -135, -180, 300, 95), VanillaSprites.GreenBtn,
                new Action(ConfirmSave));
            save.AddText(new Info("l", InfoPreset.FillParent), "Save", 44f);

            var cancel = panel.AddButton(new Info("Cancel", 185, -180, 250, 95), VanillaSprites.RedBtn,
                new Action(() => CloseByName(NameRoot)));
            cancel.AddText(new Info("l", InfoPreset.FillParent), "Cancel", 40f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void CycleFolder(int delta)
    {
        if (_folders == null || _folders.Count == 0) return;
        _folderIndex = (_folderIndex + delta + _folders.Count) % _folders.Count;
        UpdateFolderLabel();
    }

    private static string CurrentFolderName()
    {
        if (_folders == null || _folders.Count == 0) return SaveStore.DefaultFolder;
        _folderIndex = Math.Clamp(_folderIndex, 0, _folders.Count - 1);
        return _folders[_folderIndex];
    }

    private static void UpdateFolderLabel()
    {
        try
        {
            if (_folderLabel == null) return;
            var f = CurrentFolderName();
            int n = SaveStore.ListSaves(f).Count;
            _folderLabel.SetText(n > 0 ? $"{f} ({n})" : f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void ConfirmSave()
    {
        try
        {
            string name = (_nameField?.InputField?.text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = GameStateManager.DefaultSaveName(_pending);

            string folder = CurrentFolderName();
            if (string.Equals(folder, SaveStore.LegacyFolder, StringComparison.OrdinalIgnoreCase))
                folder = SaveStore.DefaultFolder;

            try { Mod.CurrentFolder.SetValueAndSave(folder); } catch {  }

            GameStateManager.CommitSave(_pending, folder, name);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
        finally { CloseByName(NameRoot); }
    }

    private static void OpenNewFolderFromNameDialog()
    {
        OpenNewFolderPrompt(CurrentFolderName(), created =>
        {
            _folders = SaveStore.ListFolders();
            int i = _folders.FindIndex(f => string.Equals(f, created, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) _folderIndex = i;
            UpdateFolderLabel();
        });
    }

    public static void OpenNewFolderPrompt(string parentFolder, Action<string> onCreated)
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            CloseByName(FolderRoot);

            var parent = SaveStore.NormalizeFolder(parentFolder);

            var parentLabel = string.IsNullOrEmpty(parent)
                ? "the top level"
                : parent;

            var panel = CommonForegroundScreen.instance.transform.gameObject.AddModHelperPanel(
                new Info(FolderRoot, 0, 0, 720, 420, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();

            panel.AddText(new Info("Title", 0, 150, 620, 60), "New folder", 44f);
            panel.AddText(new Info("Hint", 0, 95, 640, 50),
                $"Will be created inside: {parentLabel}.  Use \"/\" to nest deeper.",
                24f);

            var input = panel.AddInputField(
                new Info("Inp", 0, 25, 600, 80), "", VanillaSprites.BlueInsertPanelRound);

            var create = panel.AddButton(new Info("Create", -135, -110, 260, 90), VanillaSprites.GreenBtn,
                new Action(() =>
                {
                    var nm = (input?.InputField?.text ?? "").Trim();
                    var full = SaveStore.CombineFolder(parent, nm);
                    if (!string.IsNullOrEmpty(full) && SaveStore.CreateFolder(full))
                        onCreated?.Invoke(full);
                    CloseByName(FolderRoot);
                }));
            create.AddText(new Info("l", InfoPreset.FillParent), "Create", 38f);

            var cancel = panel.AddButton(new Info("Cancel", 155, -110, 230, 90), VanillaSprites.RedBtn,
                new Action(() => CloseByName(FolderRoot)));
            cancel.AddText(new Info("l", InfoPreset.FillParent), "Cancel", 38f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    public static void OpenCodeExport(SaveRef save)
    {
        try
        {
            if (CommonForegroundScreen.instance == null || save?.Data == null) return;
            CloseByName(CodeExportRoot);

            var code = SaveCodec.Encode(save.Data) ?? "";

            var parent = CommonForegroundScreen.instance.transform;
            var panel = parent.gameObject.AddModHelperPanel(
                new Info(CodeExportRoot, 0, 0, 1040, 540, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();

            panel.AddText(new Info("Title", 0, 205, 960, 60), $"Share code - {save.DisplayName}", 40f);
            panel.AddText(new Info("Hint", 0, 140, 960, 50),
                "This code holds the entire save. Copy it and share - anyone can paste it into Import from code.",
                25f);

            panel.AddInputField(new Info("Code", 0, 40, 940, 120), code, VanillaSprites.BlueInsertPanelRound);

            var copy = panel.AddButton(new Info("Copy", -170, -130, 320, 95), VanillaSprites.GreenBtn,
                new Action(() =>
                {
                    bool ok = Clipboard.Set(code);
                    GameStateManager.Notify(ok
                        ? "Code copied to clipboard."
                        : "Couldn't reach the clipboard - select the text above and copy it manually.");
                }));
            copy.AddText(new Info("l", InfoPreset.FillParent), "Copy", 40f);

            var close = panel.AddButton(new Info("Close", 190, -130, 250, 95), VanillaSprites.RedBtn,
                new Action(() => CloseByName(CodeExportRoot)));
            close.AddText(new Info("l", InfoPreset.FillParent), "Close", 40f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    public static void OpenCodeImport(string targetFolder, Action onImported)
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            CloseByName(CodeImportRoot);

            var parent = CommonForegroundScreen.instance.transform;
            var panel = parent.gameObject.AddModHelperPanel(
                new Info(CodeImportRoot, 0, 0, 1040, 560, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();

            panel.AddText(new Info("Title", 0, 220, 960, 60), "Import from code", 44f);
            panel.AddText(new Info("Hint", 0, 155, 960, 50),
                $"Paste a code into the box (Ctrl+V) and Import, or import straight from your clipboard. Lands in: {targetFolder}.",
                25f);

            var input = panel.AddInputField(new Info("Inp", 0, 55, 940, 120), "", VanillaSprites.BlueInsertPanelRound);

            var importTyped = panel.AddButton(new Info("ImpTyped", -255, -110, 400, 95), VanillaSprites.GreenBtn,
                new Action(() =>
                {
                    var code = (input?.InputField?.text ?? "").Trim();
                    if (GameStateManager.ImportCode(code, targetFolder))
                    {
                        onImported?.Invoke();
                        CloseByName(CodeImportRoot);
                    }
                }));
            importTyped.AddText(new Info("l", InfoPreset.FillParent), "Import pasted code", 30f);

            var importClip = panel.AddButton(new Info("ImpClip", 130, -110, 320, 95), VanillaSprites.BrightBlueBtn,
                new Action(() =>
                {
                    var code = Clipboard.Get();
                    if (GameStateManager.ImportCode(code, targetFolder))
                    {
                        onImported?.Invoke();
                        CloseByName(CodeImportRoot);
                    }
                }));
            importClip.AddText(new Info("l", InfoPreset.FillParent), "From clipboard", 28f);

            var close = panel.AddButton(new Info("Close", 410, -110, 150, 95), VanillaSprites.RedBtn,
                new Action(() => CloseByName(CodeImportRoot)));
            close.AddText(new Info("l", InfoPreset.FillParent), "X", 44f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    public static void OpenConfirm(string title, string message, string confirmLabel, Action onConfirm)
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            CloseByName(ConfirmRoot);

            var panel = CommonForegroundScreen.instance.transform.gameObject.AddModHelperPanel(
                new Info(ConfirmRoot, 0, 0, 780, 440, new Vector2(0.5f, 0.5f)),
                VanillaSprites.MainBGPanelBlue);
            panel.gameObject.transform.SetAsLastSibling();

            panel.AddText(new Info("Title", 0, 150, 700, 60), title, 44f);
            panel.AddText(new Info("Msg", 0, 30, 700, 160), message, 30f);

            var yes = panel.AddButton(new Info("Yes", -150, -130, 280, 95), VanillaSprites.RedBtn,
                new Action(() =>
                {
                    CloseByName(ConfirmRoot);
                    try { onConfirm?.Invoke(); }
                    catch (Exception e) { ModHelper.Warning<Mod>(e); }
                }));
            yes.AddText(new Info("l", InfoPreset.FillParent), confirmLabel, 38f);

            var no = panel.AddButton(new Info("No", 150, -130, 280, 95), VanillaSprites.BlueBtnLong,
                new Action(() => CloseByName(ConfirmRoot)));
            no.AddText(new Info("l", InfoPreset.FillParent), "Cancel", 38f);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void CloseByName(string name)
    {
        try
        {
            if (CommonForegroundScreen.instance == null) return;
            var t = CommonForegroundScreen.instance.transform.FindChild(name);
            if (t != null)
            {

                t.gameObject.name = name + "_old";
                t.gameObject.Destroy();
            }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }
}
