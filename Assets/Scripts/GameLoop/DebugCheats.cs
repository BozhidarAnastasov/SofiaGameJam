#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// Test keys (only in the Editor / development builds, never in the arcade build):
///   F10 = give the key card (into the hotbar, selected) - to test the jammed office door
///   F11 = give the coat (into the hotbar, selected) - to test hiding
///   F12 = give the flashlight (D switches it on/off)
/// Added automatically to every scene.
public class DebugCheats : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("DebugCheats");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugCheats>();
        Debug.Log("[DebugCheats] Test keys: F10 = key card, F11 = coat, F12 = flashlight.");
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.f10Key.wasPressedThisFrame) GiveItem("Card", "Keycard", StoryFlag.HasCard);
        if (k.f11Key.wasPressedThisFrame) GiveItem("Coat", "Coat_Hide", StoryFlag.HasCoat);
        if (k.f12Key.wasPressedThisFrame)
        {
            var f = FindFirstObjectByType<PlayerFlashlight>();
            if (f != null) f.Give();
            Debug.Log("[DebugCheats] Flashlight given.");
        }
    }

    static void GiveItem(string itemName, string resourceSprite, StoryFlag flag)
    {
        GameProgress.Set(flag);
        var inv = FindFirstObjectByType<Inventory>();
        if (inv == null) { Debug.LogWarning($"[DebugCheats] No hotbar in this scene - only set {flag}."); return; }

        // already in the hotbar? just select it
        if (SelectByName(inv, itemName)) { Debug.Log($"[DebugCheats] '{itemName}' selected."); return; }

        var temp = new GameObject(itemName);                       // the icon is named after this object
        var sr = temp.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>(resourceSprite);
        if (sr.sprite == null)                                     // fallback icon: a small white square
        {
            var tex = new Texture2D(8, 8); var px = new Color[64];
            for (int i = 0; i < 64; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }
        bool added = inv.AddItem(temp);
        Destroy(temp);
        if (added) SelectByName(inv, itemName);
        Debug.Log(added ? $"[DebugCheats] '{itemName}' added to the hotbar and selected ({flag} set)." : "[DebugCheats] Hotbar is full.");
    }

    static bool SelectByName(Inventory inv, string itemName)
    {
        for (int i = 0; i < inv.slotCount; i++)
        {
            inv.SelectSlot(i);
            var item = inv.SelectedSlot != null ? inv.SelectedSlot.currentItem : null;
            if (item != null && item.name == itemName) return true;
        }
        return false;
    }
}
#endif
