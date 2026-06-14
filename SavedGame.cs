using System;
using System.Collections.Generic;

namespace GameSaveManager;

public class SavedGame
{

    public string Name { get; set; }
    public string Folder { get; set; }

    public double Cash { get; set; }
    public double Lives { get; set; }
    public List<SavedTower> Towers { get; set; } = new List<SavedTower>();

    public string Map { get; set; }
    public string Difficulty { get; set; }
    public string Mode { get; set; }
    public int Round { get; set; }
    public DateTime SavedAtUtc { get; set; }
}

public class SavedTower
{

    public string BaseId { get; set; }
    public string FullName { get; set; }

    public float X { get; set; }
    public float Y { get; set; }

    public bool IsHero { get; set; }
    public bool IsParagon { get; set; }

    public int Tier0 { get; set; }
    public int Tier1 { get; set; }
    public int Tier2 { get; set; }
}
