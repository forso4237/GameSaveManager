using System;
using System.Collections;
using System.Collections.Generic;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Testing;
using MelonLoader;
using UnityEngine;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Mod = GameSaveManager.GameSaveManager;

namespace GameSaveManager;

public static class GameStateManager
{

    public static void Save()
    {
        try
        {
            var save = Capture();
            if (save == null) return;
            SaveDialogs.OpenNameDialog(save);
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Save failed - see the MelonLoader console for details.");
        }
    }

    public static SavedGame Capture()
    {
        try
        {
            if (InGame.instance == null || InGame.Bridge == null)
            {
                Notify("Can't save - you need to be in a game.");
                return null;
            }

            var save = new SavedGame
            {
                Cash = InGame.instance.GetCash(),
                Lives = InGame.instance.GetHealth()
            };

            foreach (var tts in InGame.instance.GetAllTowerToSim())
            {
                var tower = tts?.tower;
                var model = tower?.towerModel;
                if (model == null) continue;

                var pos = tower.Position.ToVector2();

                save.Towers.Add(new SavedTower
                {
                    BaseId = model.baseId,
                    FullName = model.name,
                    X = pos.x,
                    Y = pos.y,
                    IsHero = model.IsHero(),
                    IsParagon = model.isParagon,
                    Tier0 = TierAt(model, 0),
                    Tier1 = TierAt(model, 1),
                    Tier2 = TierAt(model, 2)
                });
            }

            CaptureContext(save);
            return save;
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Save failed - see the MelonLoader console for details.");
            return null;
        }
    }

    public static void CommitSave(SavedGame save, string folder, string name)
    {
        try
        {
            var r = SaveStore.Write(folder, name, save);
            if (r != null)
                Notify($"Saved \"{r.DisplayName}\" to {r.Folder}: " +
                       $"{save.Towers.Count} towers, ${(long)save.Cash}, {(long)save.Lives} lives.");
            else
                Notify("Save failed - see the MelonLoader console for details.");
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Save failed - see the MelonLoader console for details.");
        }
    }

    public static string DefaultSaveName(SavedGame s)
    {
        try
        {
            if (s != null && !string.IsNullOrEmpty(s.Map))
                return s.Round > 0 ? $"{s.Map} R{s.Round}" : s.Map;
        }
        catch {  }
        return "Save " + DateTime.Now.ToString("MMM d HH:mm");
    }

    public static void Load() => QuickLoad();

    public static void QuickLoad()
    {
        try
        {
            if (InGame.instance == null || InGame.Bridge == null)
            {
                Notify("Can't load - start/enter a game first, then load.");
                return;
            }

            var recent = SaveStore.MostRecent();
            if (recent == null)
            {
                Notify("No saves yet. Press save (F6) to make one first.");
                return;
            }

            Notify($"Quick-loading most recent: \"{recent.DisplayName}\" ({recent.Folder}).");
            LoadGame(recent.Data);
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Load failed - see the MelonLoader console for details.");
        }
    }

    public static void LoadGame(SavedGame save)
    {
        try
        {
            if (save == null)
            {
                Notify("That save couldn't be read.");
                return;
            }
            if (InGame.instance == null || InGame.Bridge == null)
            {
                Notify("Can't load - start/enter a game first, then load.");
                return;
            }

            MelonCoroutines.Start(LoadRoutine(save));
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Load failed - see the MelonLoader console for details.");
        }
    }

    public static bool ImportCode(string code, string folder)
    {
        try
        {
            var save = SaveCodec.Decode(code);
            if (save == null)
            {
                Notify("That code isn't valid - make sure you copied the whole thing.");
                return false;
            }

            var name = string.IsNullOrWhiteSpace(save.Name) ? DefaultSaveName(save) : save.Name;
            var r = SaveStore.Write(folder, name, save);
            if (r != null)
            {
                Notify($"Imported \"{r.DisplayName}\" into {r.Folder}.");
                return true;
            }

            Notify("Import failed - see the MelonLoader console for details.");
            return false;
        }
        catch (Exception e)
        {
            ModHelper.Error<Mod>(e);
            Notify("Import failed - see the MelonLoader console for details.");
            return false;
        }
    }

    private static IEnumerator LoadRoutine(SavedGame save)
    {
        Notify($"Loading \"{(string.IsNullOrEmpty(save.Name) ? "save" : save.Name)}\" " +
               $"({save.Towers.Count} towers)... reload on the same map you saved on.");

        ClearExistingTowers();
        yield return null;

        GiveTempCash();
        yield return null;

        int placed = 0;
        foreach (var st in save.Towers)
        {
            if (st == null) continue;

            var tower = PlaceTower(st, out bool placedFromBase);
            yield return null;
            if (tower == null) continue;
            placed++;

            if (placedFromBase && !st.IsHero && !st.IsParagon)
            {
                for (int path = 0; path < 3; path++)
                {
                    int target = TierForPath(st, path);
                    for (int tier = 0; tier < target; tier++)
                    {
                        DoUpgrade(tower, path);
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }

            yield return null;
        }

        SetCashAndLives(save);
        RestoreRound(save);

        string roundNote = save.Round > 0 ? $", round {save.Round}" : "";
        Notify($"Done. Restored {placed}/{save.Towers.Count} towers, " +
               $"${(long)save.Cash}, {(long)save.Lives} lives{roundNote}.");
    }

    private static void GiveTempCash()
    {
        try { InGame.instance.AddCash(10_000_000); }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static Tower PlaceTower(SavedTower st, out bool placedFromBase)
    {
        placedFromBase = false;
        try
        {
            var gameModel = InGame.instance.GetGameModel();
            if (gameModel == null) return null;

            TowerModel model = null;
            if (!string.IsNullOrEmpty(st.FullName))
                model = gameModel.GetTowerWithName(st.FullName);

            if (model == null && !string.IsNullOrEmpty(st.BaseId))
            {
                model = gameModel.GetTower(st.BaseId);
                placedFromBase = true;
            }

            if (model == null) return null;

            var tts = ModTest.CreateTowerAt(InGame.Bridge, new Vector2(st.X, st.Y), model, true, true, -1);
            return tts?.tower;
        }
        catch (Exception e)
        {
            ModHelper.Warning<Mod>(e);
            return null;
        }
    }

    private static void DoUpgrade(Tower tower, int path)
    {
        try
        {
            if (tower != null) ModTest.UpgradeTower(InGame.Bridge, tower, path);
        }
        catch (Exception e)
        {
            ModHelper.Warning<Mod>(e);
        }
    }

    private static void SetCashAndLives(SavedGame save)
    {
        try
        {

            InGame.instance.SetCash(save.Cash);

            double cur = InGame.instance.GetHealth();
            if (save.Lives > cur) InGame.instance.AddMaxHealth(save.Lives - cur);
            InGame.instance.SetHealth(save.Lives);
        }
        catch (Exception e)
        {
            ModHelper.Warning<Mod>(e);
        }
    }

    private static void ClearExistingTowers()
    {
        try
        {

            var towers = new List<Tower>();
            foreach (var tts in InGame.instance.GetAllTowerToSim())
            {
                var t = tts?.tower;
                if (t != null) towers.Add(t);
            }
            if (towers.Count > 0) InGame.instance.SellTowers(towers);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static void RestoreRound(SavedGame save)
    {
        try
        {
            if (save.Round > 0) InGame.instance.SetRound(save.Round - 1);
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }
    }

    private static int TierAt(TowerModel model, int path)
    {
        try
        {
            if (model?.tiers == null) return 0;
            if (path < 0 || path >= model.tiers.Length) return 0;
            return model.tiers[path];
        }
        catch { return 0; }
    }

    private static int TierForPath(SavedTower st, int path)
    {
        return path switch
        {
            0 => st.Tier0,
            1 => st.Tier1,
            2 => st.Tier2,
            _ => 0
        };
    }

    private static void CaptureContext(SavedGame save)
    {
        save.SavedAtUtc = DateTime.UtcNow;
        try
        {
            var cur = InGameData.CurrentGame;
            if (cur != null)
            {
                save.Map = cur.selectedMap;
                save.Difficulty = cur.selectedDifficulty;
                save.Mode = cur.selectedMode;
            }
        }
        catch (Exception e) { ModHelper.Warning<Mod>(e); }

        try { save.Round = InGame.Bridge.GetCurrentRound() + 1; }
        catch { save.Round = 0; }
    }

    public static void Notify(string msg)
    {
        ModHelper.Msg<Mod>(msg);
        try { Il2CppAssets.Scripts.Unity.Game.instance?.ShowMessage(msg); }
        catch {  }
    }
}
