using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// The thought bubble over the girl's head ("ItemHintPopup" on the Player prefab).
/// It stays hidden, and only pops up when she stands near a LOCKED door, showing the thing that door
/// needs: the crowbar (boarded door), the key card (office), power (electric doors), the flashlight
/// (the dark operating room). Once a door is open it never shows a bubble again.
/// The bubble and the "E" prompt take turns: the bubble while she can't open it yet, the "E" as soon
/// as she can (e.g. crowbar / key card selected) - never both at once.
/// Added to the player automatically in every room - no prefab or scene changes needed.
public class DoorHintBubble : MonoBehaviour
{
    public enum Need { None, Crowbar, Keycard, Power, Flashlight }

    [SerializeField] private float showDistance = 2.2f;
    [SerializeField] private float scanInterval = 0.2f;

    private GameObject bubble;            // BubbleBackground
    private Image icon;                   // BubbleBackground/ItemIcon
    private TMPro.TMP_Text tip;           // BubbleBackground/Tip
    private float nextScan;
    private SpriteRenderer ePrompt;       // the "E" key prompt's circle (visible = it's the E's turn)
    private Need shown = (Need)(-1);
    private static readonly Dictionary<Need, Sprite> icons = new Dictionary<Need, Sprite>();

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

    void Awake()
    {
        var popup = transform.Find("ItemHintPopup");
        var bg = popup != null ? popup.Find("BubbleBackground") : null;
        if (bg == null) { enabled = false; return; }
        bubble = bg.gameObject;
        var ic = bg.Find("ItemIcon");
        icon = ic != null ? ic.GetComponent<Image>() : null;
        var tp = bg.Find("Tip");
        tip = tp != null ? tp.GetComponent<TMPro.TMP_Text>() : null;

        // the team's older hint script would fight over the same bubble: we drive it now
        foreach (var mb in popup.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "PlayerItemPopupHint") mb.enabled = false;
        var ip = GetComponentInChildren<InteractPrompt>(true);
        if (ip != null) ePrompt = ip.GetComponent<SpriteRenderer>();
        if (popup.gameObject.activeSelf == false) popup.gameObject.SetActive(true);
        Show(Need.None);
    }

    void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        // whenever the "E" is up (she can use something right here) the bubble steps aside
        bool eShowing = ePrompt != null && ePrompt.enabled && ePrompt.color.a > 0.05f;
        Show(eShowing ? Need.None : NearestLockedDoor());
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

    void Show(Need need)
    {
        if (need == shown || bubble == null) return;
        shown = need;
        if (need == Need.None) { bubble.SetActive(false); return; }
        bubble.SetActive(true);
        if (icon != null) { icon.sprite = Icon(need); icon.preserveAspect = true; icon.enabled = icon.sprite != null; }
        if (tip != null) tip.text = need == Need.Power ? "No power" : need == Need.Flashlight ? "Too dark" : "";
        Debug.Log($"[DoorHintBubble] Locked door nearby - needs: {need}.", this);
    }

    static Sprite Icon(Need need)
    {
        if (icons.TryGetValue(need, out var sp) && sp != null) return sp;
        var tex = Resources.Load<Texture2D>("DoorHints/" + need);         // Assets/Resources/DoorHints/<Need>.png
        sp = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
        icons[need] = sp;
        return sp;
    }
}
