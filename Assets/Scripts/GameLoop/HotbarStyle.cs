using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Gives the hotbar the game's look: a worn, riveted metal plate with dark recessed slots, and the
/// selected slot soaked in blood (dripping) instead of the yellow outline.
/// Works on the existing HotBar of every room at runtime - the Inventory / Slot code and the scenes
/// stay as they are. Art: Assets/Resources/Hotbar/Panel.png, Slot.png, Blood.png.
public class HotbarStyle : MonoBehaviour
{
    [SerializeField] private float bloodFadeIn = 0.15f;

    private Inventory inv;
    private Slot[] slots;
    private Image[] blood;
    private int lastSelected = -1;
    private float fade;

    static Sprite panelSprite, slotSprite, bloodSprite;

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
        var inv = Object.FindFirstObjectByType<Inventory>();
        if (inv != null && inv.GetComponent<HotbarStyle>() == null) inv.gameObject.AddComponent<HotbarStyle>();
    }

    void Start()
    {
        inv = GetComponent<Inventory>();
        if (inv == null || inv.inventoryPanel == null) { enabled = false; return; }
        panelSprite = panelSprite != null ? panelSprite : Load("Panel");
        slotSprite = slotSprite != null ? slotSprite : Load("Slot");
        bloodSprite = bloodSprite != null ? bloodSprite : Load("Blood");

        var panel = inv.inventoryPanel.GetComponent<Image>();
        if (panel != null && panelSprite != null) { panel.sprite = panelSprite; panel.type = Image.Type.Simple; panel.color = Color.white; }

        slots = inv.inventoryPanel.GetComponentsInChildren<Slot>(true);
        blood = new Image[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var img = slots[i].GetComponent<Image>();
            if (img != null && slotSprite != null) { img.sprite = slotSprite; img.type = Image.Type.Simple; img.color = Color.white; }
            var outline = slots[i].GetComponent<Outline>();               // the old yellow frame: invisible now
            if (outline != null) outline.effectColor = new Color(0, 0, 0, 0);
            blood[i] = MakeBlood(slots[i].transform);
        }
        lastSelected = -1;
    }

    Image MakeBlood(Transform slot)
    {
        var go = new GameObject("Blood", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slot, false);
        go.transform.SetAsFirstSibling();                                   // under the item icon
        var rt = (RectTransform)go.transform;
        var srt = (RectTransform)slot;
        float w = srt.rect.width > 1f ? srt.rect.width : 60f;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, w * 160f / 120f);                    // the drips run below the slot
        var img = go.GetComponent<Image>();
        img.sprite = bloodSprite; img.raycastTarget = false; img.preserveAspect = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        go.SetActive(false);
        return img;
    }

    void Update()
    {
        if (slots == null) return;
        int sel = inv.SelectedSlotIndex;
        if (sel != lastSelected)
        {
            for (int i = 0; i < blood.Length; i++) if (blood[i] != null) blood[i].gameObject.SetActive(i == sel);
            lastSelected = sel;
            fade = 0f;
        }
        if (sel >= 0 && sel < blood.Length && blood[sel] != null && fade < 1f)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, bloodFadeIn));
            blood[sel].color = new Color(1f, 1f, 1f, fade);
        }
    }

    static Sprite Load(string n)
    {
        var tex = Resources.Load<Texture2D>("Hotbar/" + n);
        if (tex == null) { Debug.LogWarning("[HotbarStyle] Missing Assets/Resources/Hotbar/" + n + ".png"); return null; }
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
