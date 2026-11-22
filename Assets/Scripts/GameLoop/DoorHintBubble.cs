using UnityEngine;
using UnityEngine.SceneManagement;

/// Door hints, using the team's thought bubble (PlayerItemHintPopup / "ItemHintPopup" on the Player prefab).
/// No hint zones to place: every LOCKED door in the room is found automatically, and when the girl stands
/// near one the bubble shows the thing that door needs - crowbar (boarded door), key card (office),
/// power (electric doors), flashlight (the dark operating room). Once a door is open it never shows again.
/// The bubble and the "E" prompt take turns: the bubble while she can't open it yet, the "E" as soon as
/// she can (e.g. crowbar / key card selected) - never both at once.
/// Hand-placed hint zones (PlayerItemHintArea / HintTriggerZone) keep working alongside; doors win if both apply.
/// Added to the player automatically in every room. The bubble itself: Tools > Horror Room > 31.
public class DoorHintBubble : MonoBehaviour
{
    public enum Need { None, Crowbar, Keycard, Power, Flashlight }

    [SerializeField] private float showDistance = 2.2f;
    [SerializeField] private float scanInterval = 0.2f;

    private PlayerItemHintPopup popup;
    private SpriteRenderer ePrompt;       // the "E" key prompt's circle (visible = it's the E's turn)
    private float nextScan;
    private Need shown = (Need)(-1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();

    static void Attach()
    {
        var p = Object.FindFirstObjectByType<TopDownMovement>();
        if (p != null && p.GetComponent<DoorHintBubble>() == null) p.gameObject.AddComponent<DoorHintBubble>();
    }

    void Start()
    {
        popup = GetComponentInChildren<PlayerItemHintPopup>(true);
        if (popup == null)
        {
            Debug.LogWarning("[DoorHintBubble] The player has no ItemHintPopup bubble - run Tools > Horror Room > 31 once.", this);
            enabled = false;
            return;
        }
        if (!popup.gameObject.activeSelf) popup.gameObject.SetActive(true);
        var ip = GetComponentInChildren<InteractPrompt>(true);
        if (ip != null) ePrompt = ip.GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (popup == null || Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        // whenever the "E" is up (she can use something right here) the bubble steps aside
        bool eShowing = ePrompt != null && ePrompt.enabled && ePrompt.color.a > 0.05f;
        Need need = eShowing ? Need.None : NearestLockedDoor();

        if (need == Need.None) popup.HideHint(this);              // only hides it if the bubble is ours
        else popup.ShowHint(ToItem(need), this);                   // no-op while it's already showing ours

        if (need != shown)
        {
            shown = need;
            if (need != Need.None) Debug.Log($"[DoorHintBubble] Locked door nearby - needs: {need}.", this);
        }
    }

    static HintItemType ToItem(Need n)
    {
        switch (n)
        {
            case Need.Crowbar: return HintItemType.Crowbar;
            case Need.Keycard: return HintItemType.Keycard;
            case Need.Flashlight: return HintItemType.Light;
            default: return HintItemType.Power;
        }
    }

    Need NearestLockedDoor()
    {
        Vector2 me = transform.position;
        Need best = Need.None;
        float bestD = showDistance;

        void Consider(Vector2 at, Need need)
        {
            if (need == Need.None) return;
            float d = Vector2.Distance(me, at);
            if (d <= bestD) { bestD = d; best = need; }
        }

        // boarded-up doors: the DoorExit stays switched off until the planks are pried off
        foreach (var b in FindObjectsByType<BarricadedDoor>(FindObjectsSortMode.None))
        {
            var exit = b.GetComponent<DoorExit>();
            if (exit != null && !exit.enabled && !b.HasToolSelected) Consider(b.transform.position, Need.Crowbar);   // crowbar in hand: "E" instead
        }

        // story locks on doors (power, key card ...)
        foreach (var l in FindObjectsByType<DoorLock>(FindObjectsSortMode.None))
        {
            if (l.IsOpen) continue;
            var jam = l.GetComponent<JammedDoor>();
            if (jam != null && jam.IsOpen) continue;
            Consider(l.transform.position, NeedOf(l));
        }

        // invisible barriers (the dark operating room)
        foreach (var z in FindObjectsByType<ZoneLock>(FindObjectsSortMode.None))
        {
            var col = z.GetComponent<Collider2D>();
            if (col == null || !col.enabled) continue;
            Consider(col.ClosestPoint(me), NeedOf(z.Flag));
        }

        // doors that only work with power (the Electrical exit before the lever is pulled)
        foreach (var d in FindObjectsByType<DoorExit>(FindObjectsSortMode.None))
            if (d.enabled && d.NeedsPower) Consider(d.transform.position, Need.Power);

        return best;
    }

    static Need NeedOf(DoorLock l) => l.Needs == DoorLock.Requirement.SelectedItem ? Need.Keycard : NeedOf(l.Flag);

    static Need NeedOf(StoryFlag f)
    {
        switch (f)
        {
            case StoryFlag.PowerOn: return Need.Power;
            case StoryFlag.HasFlashlight: return Need.Flashlight;
            case StoryFlag.HasCard: return Need.Keycard;
            default: return Need.None;
        }
    }
}
