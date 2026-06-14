using System;
using System.Collections.Generic;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New;
using UnityEngine;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

public static class SaveLoadUI
{
    private const string PanelName = "GameSaveManagerPanel";
    private static ModHelperText folderLabel;

    public static void OnMatchStart() => Apply(false);
    public static void OnPause(bool paused) => Apply(paused);

    public static void OnMatchEnd()
    {
        try
        {
            var go = Find();
            if (go != null) go.SetActive(false);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void Apply(bool paused)
    {
        try
        {
            var mode = (ButtonPlacement) Mod.ButtonLocation;

            bool visible = mode switch
            {
                ButtonPlacement.OnScreenDuringGame => true,
                ButtonPlacement.PauseMenuOnly => paused,
                _ => false
            };

            if (!visible)
            {
                var existing = Find();
                if (existing != null) existing.SetActive(false);
                return;
            }

            var go = EnsurePanel();
            if (go == null) return;
            go.SetActive(true);
            go.transform.SetAsLastSibling();
            UpdateFolderLabel();
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static GameObject Find()
    {
        if (CommonForegroundScreen.instance == null) return null;
        var t = CommonForegroundScreen.instance.transform.FindChild(PanelName);
        return t != null ? t.gameObject : null;
    }

    private static GameObject EnsurePanel()
    {
        var existing = Find();
        if (existing != null) return existing;
        if (CommonForegroundScreen.instance == null) return null;

        var parent = CommonForegroundScreen.instance.transform;

        var panel = parent.gameObject.AddModHelperPanel(
            new Info(PanelName, 240, 0, 360, 560, new Vector2(0, 0.5f)),
            VanillaSprites.MainBGPanelBlue);

        panel.AddText(new Info("Title", 0, 225, 320, 55), "Save State", 38f);

        var prev = panel.AddButton(
            new Info("Prev", -120, 130, 75, 75), VanillaSprites.BlueBtnSquare,
            new Action(() => ChangeFolder(-1)));
        prev.AddText(new Info("Label", InfoPreset.FillParent), "<", 50f);

        folderLabel = panel.AddText(new Info("Folder", 0, 130, 170, 60), "General", 32f);

        var next = panel.AddButton(
            new Info("Next", 120, 130, 75, 75), VanillaSprites.BlueBtnSquare,
            new Action(() => ChangeFolder(1)));
        next.AddText(new Info("Label", InfoPreset.FillParent), ">", 50f);

        var save = panel.AddButton(
            new Info("SaveBtn", 0, 35, 280, 90), VanillaSprites.GreenBtn,
            new Action(() => GameStateManager.Save()));
        save.AddText(new Info("Label", InfoPreset.FillParent), "Save", 44f);

        var load = panel.AddButton(
            new Info("LoadBtn", 0, -65, 280, 90), VanillaSprites.BlueBtnLong,
            new Action(() => GameStateManager.QuickLoad()));
        load.AddText(new Info("Label", InfoPreset.FillParent), "Quick Load", 34f);

        var catalog = panel.AddButton(
            new Info("CatBtn", 0, -165, 280, 90), VanillaSprites.BlueBtnLong,
            new Action(() => SaveCatalogUI.Open()));
        catalog.AddText(new Info("Label", InfoPreset.FillParent), "Catalog", 38f);

        return panel.gameObject;
    }

    private static void ChangeFolder(int delta)
    {
        try
        {
            var folders = SaveStore.ListFolders();
            if (folders.Count == 0) folders.Add(SaveStore.DefaultFolder);

            int idx = folders.IndexOf(Mod.CurrentFolder);
            if (idx < 0) idx = 0;

            int n = folders.Count;
            idx = (((idx + delta) % n) + n) % n;

            Mod.CurrentFolder.SetValueAndSave(folders[idx]);
            UpdateFolderLabel();
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void UpdateFolderLabel()
    {
        try
        {
            if (folderLabel == null) return;
            string folder = Mod.CurrentFolder;
            int count = SaveStore.ListSaves(folder).Count;
            folderLabel.SetText(count > 0 ? $"{folder}\n({count})" : $"{folder}\n(empty)");
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }
}
