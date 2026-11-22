using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Story flags for the whole game loop. They live in static memory, so they survive room changes,
/// and they are cleared when a new game starts (Play in the editor / launching the build).
///
/// PowerOn and HasFlashlight are read from the existing systems (LeverSwitch / PlayerFlashlight),
/// so there is only one source of truth for them.
public enum StoryFlag
{
    PowerOn,              // the lever in Electrical was pulled
    HasFlashlight,        // picked up the flashlight in the Lobby
    BedDropped,           // the bed was pushed through the hole on the second floor
    HasCoat,              // picked up the coat in SurgicalWing (can hide under it)
    HasCard,              // picked up the office key card in the operating room
    EntranceCollapsed,    // the Lobby intro rockfall already happened
    ElectricalDoorKicked, // the psycho kicked the Electrical door in
    OfficeOpen,           // the office door was forced open
    CoatEventDone,        // the psycho's walk through the Lobby after she found the coat is over
    EnemyInElectrical,    // after that walk he waits in Electrical and goes straight for her
    BedTakenOut,          // she dragged the bed out of Dr. Mundo's cabinet onto the second floor
    CabinetVisitDone,     // the psycho's visit to Dr. Mundo's cabinet (when she first moves the bed) is over
    Floor2PatrolDone,     // after the bed fell he came up to the second floor and patrolled until she left it
}

public static class GameProgress
{
    static readonly HashSet<StoryFlag> flags = new HashSet<StoryFlag>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart()
    {
        flags.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // back in the main menu (first scene in the build) = the next run is a brand-new game
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == 0) NewGame();
    }

    /// Forget everything from the last run: story flags, power, flashlight, removed planks, the saved hotbar.
    public static void NewGame()
    {
        flags.Clear();
        Inventory.ClearRoomSave();
        foreach (var t in new[] { typeof(LeverSwitch), typeof(PlayerFlashlight), typeof(BarricadedDoor) })
        {
            var m = t.GetMethod("ResetOnGameStart", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(null, null);
        }
        Debug.Log("[GameProgress] New game: all progress cleared.");
    }

    /// Keep the hotbar (items + what was picked up) for a Retry of this room after she got caught.
    public static void SaveHotbarForRetry()
    {
        var inv = Object.FindFirstObjectByType<Inventory>();
        if (inv != null) inv.SaveForRoom(SceneManager.GetActiveScene().name);
    }

    public static bool Get(StoryFlag flag)
    {
        switch (flag)
        {
            case StoryFlag.PowerOn: return LeverSwitch.PowerRestored || flags.Contains(flag);
            case StoryFlag.HasFlashlight: return PlayerFlashlight.HasFlashlight || flags.Contains(flag);
            default: return flags.Contains(flag);
        }
    }

    public static void Set(StoryFlag flag, bool value = true)
    {
        if (value == flags.Contains(flag)) return;
        if (value) flags.Add(flag); else flags.Remove(flag);
        Debug.Log($"[GameProgress] {flag} = {value}");
    }

    public static bool BedDropped => Get(StoryFlag.BedDropped);
    public static bool HasCoat => Get(StoryFlag.HasCoat);
    public static bool HasCard => Get(StoryFlag.HasCard);
    public static bool PowerOn => Get(StoryFlag.PowerOn);
    public static bool HasFlashlight => Get(StoryFlag.HasFlashlight);

    /// Name of the item in the selected hotbar slot ("" when empty). Read-only: the hotbar is not changed.
    public static string SelectedItemName()
    {
        var inv = Object.FindFirstObjectByType<Inventory>();
        var item = inv != null && inv.SelectedSlot != null ? inv.SelectedSlot.currentItem : null;
        return item != null ? item.name : "";
    }
}
