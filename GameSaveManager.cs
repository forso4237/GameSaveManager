global using BTD_Mod_Helper.Extensions;
using System;
using MelonLoader;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using Il2CppAssets.Scripts.Unity.UI_New.Pause;
using UnityEngine;
using GameSaveManager;

[assembly: MelonInfo(typeof(GameSaveManager.GameSaveManager), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace GameSaveManager;

public enum ButtonPlacement
{
    OnScreenDuringGame,
    PauseMenuOnly,
    Hidden
}

public class GameSaveManager : BloonsTD6Mod
{

    public static readonly ModSettingHotkey SaveHotkey = new(KeyCode.F6);
    public static readonly ModSettingHotkey LoadHotkey = new(KeyCode.F7);

    public static readonly ModSettingString CurrentFolder = new(SaveStore.DefaultFolder)
    {
        description = "The folder new saves go into by default. Switch it with the < > buttons or in the catalog."
    };

    public static readonly ModSettingEnum<ButtonPlacement> ButtonLocation = new(ButtonPlacement.Hidden)
    {
        description = "Hidden by default (hotkeys/menu only). Set to on-screen during the whole game, or only while the pause menu is open."
    };

    public static readonly ModSettingButton SaveButton =
        new(new Action(() => GameStateManager.Save())) { buttonText = "Save current game" };

    public static readonly ModSettingButton LoadButton =
        new(new Action(() => GameStateManager.QuickLoad())) { buttonText = "Quick-load latest save" };

    public static readonly ModSettingButton CatalogButton =
        new(new Action(() => SaveCatalogUI.Open())) { buttonText = "Open save catalog" };

    public override void OnApplicationStart()
    {
        ModHelper.Msg<GameSaveManager>("Game Save Manager loaded! F6 = save (asks for a name), F7 = quick-load latest.");
    }

    public override void OnUpdate()
    {
        if (SaveHotkey.JustPressed()) GameStateManager.Save();
        if (LoadHotkey.JustPressed()) GameStateManager.QuickLoad();
    }

    public override void OnMatchStart() => SaveLoadUI.OnMatchStart();
    public override void OnRestart() => SaveLoadUI.OnMatchStart();
    public override void OnMatchEnd() => SaveLoadUI.OnMatchEnd();
    public override void OnPauseScreenOpened(PauseScreen pauseScreen) => SaveLoadUI.OnPause(true);
    public override void OnPauseScreenClosed(PauseScreen pauseScreen) => SaveLoadUI.OnPause(false);
}
