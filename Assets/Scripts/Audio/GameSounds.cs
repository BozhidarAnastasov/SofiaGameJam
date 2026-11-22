using UnityEngine;

/// Every sound effect the game uses, in one place (Assets/Resources/GameSounds.asset).
/// Each slot is a list: when a slot holds several clips, one is picked at random each time,
/// so you can drop in extra variations later without touching any code.
[CreateAssetMenu(menuName = "Horror/Game Sounds", fileName = "GameSounds")]
public class GameSounds : ScriptableObject
{
    [Header("Girl")]
    public AudioClip[] girlWalk;
    public AudioClip[] girlRun;
    [Tooltip("Looped while she crawls and moves.")]
    public AudioClip[] girlCrawl;
    [Tooltip("Lobby bed, used from the lobby side (its foot): she climbs up and over.")]
    public AudioClip[] bedFirstUse;
    [Tooltip("Lobby bed, used from the information desk side (the metal headboard): she kicks it and slides down.")]
    public AudioClip[] bedSecondUse;
    public AudioClip[] girlExhaust;
    public AudioClip[] girlNoticeEnemy;
    public AudioClip[] pickUpItem;
    public AudioClip[] switchItem;
    public AudioClip[] flashlightOn;
    public AudioClip[] flashlightOff;
    [Tooltip("Looped while she drags the box.")]
    public AudioClip[] boxDrag;

    [Header("Psycho")]
    public AudioClip[] enemyWalk;
    public AudioClip[] enemyHitDoor;
    [Tooltip("The kicked-in door slamming onto the floor.")]
    public AudioClip[] doorSlam;
    [Tooltip("When he shows up in the Lobby (his walk to the stairs).")]
    public AudioClip[] knifeGrinding;

    [Header("World events")]
    [Tooltip("Lobby intro: the entrance caves in.")]
    public AudioClip[] rocksPilingUp;

    [Header("UI")]
    [Tooltip("Menu / death screen buttons (also used quieter for hover).")]
    public AudioClip[] uiClick;

    [Header("Doors and furniture")]
    public AudioClip[] doorOpen;
    public AudioClip[] doorClose;
    public AudioClip[] electricalDoor;
    public AudioClip[] cupboardOpen;
    public AudioClip[] cupboardClose;
    public AudioClip[] breakWindow;

    [Header("Lights and electricity")]
    public AudioClip[] leverPush;
    public AudioClip[] lightsStarting;
    public AudioClip[] lightsExploding;

    [Header("Kill sequence (beats are fired by animation events on Psycho_Grab_Left/Right)")]
    public AudioClip[] killGrab;
    public AudioClip[] killLift;
    public AudioClip[] killGasp;
    public AudioClip[] killKnifeRaise;
    public AudioClip[] killStab;
    [Tooltip("The whole kill as one file. Not used while the separate beats above are filled in.")]
    public AudioClip[] killFull;
    public AudioClip[] youDied;

    /// Look a slot up by name (used by animation events, e.g. "KillStab").
    public AudioClip[] Get(string id)
    {
        switch (id)
        {
            case "GirlWalk": return girlWalk;
            case "GirlRun": return girlRun;
            case "GirlCrawl": return girlCrawl;
            case "BedFirstUse": return bedFirstUse;
            case "BedSecondUse": return bedSecondUse;
            case "GirlExhaust": return girlExhaust;
            case "GirlNoticeEnemy": return girlNoticeEnemy;
            case "PickUpItem": return pickUpItem;
            case "SwitchItem": return switchItem;
            case "FlashlightOn": return flashlightOn;
            case "FlashlightOff": return flashlightOff;
            case "EnemyWalk": return enemyWalk;
            case "EnemyHitDoor": return enemyHitDoor;
            case "DoorSlam": return doorSlam;
            case "KnifeGrinding": return knifeGrinding;
            case "RocksPilingUp": return rocksPilingUp;
            case "UIClick": return uiClick;
            case "BoxDrag": return boxDrag;
            case "DoorOpen": return doorOpen;
            case "DoorClose": return doorClose;
            case "ElectricalDoor": return electricalDoor;
            case "CupboardOpen": return cupboardOpen;
            case "CupboardClose": return cupboardClose;
            case "BreakWindow": return breakWindow;
            case "LeverPush": return leverPush;
            case "LightsStarting": return lightsStarting;
            case "LightsExploding": return lightsExploding;
            case "KillGrab": return killGrab;
            case "KillLift": return killLift;
            case "KillGasp": return killGasp;
            case "KillKnifeRaise": return killKnifeRaise;
            case "KillStab": return killStab;
            case "KillFull": return killFull;
            case "YouDied": return youDied;
            default: return null;
        }
    }
}
