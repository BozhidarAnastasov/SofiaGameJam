using TMPro;
using UnityEngine;

public enum HintItemType
{
    Crowbar,
    Keycard,
    Cloak,
    Light,
    HeightTip,
    Power
}

public class PlayerItemHintPopup : MonoBehaviour
{
    public static PlayerItemHintPopup Instance { get; private set; }
    public static bool HintsEnabled = true;

    [Header("Auto-Wired Hierarchy Children")]
    [SerializeField] private GameObject bubbleBackground;
    [SerializeField] private GameObject crowbarObj;
    [SerializeField] private GameObject keycardObj;
    [SerializeField] private GameObject cloakObj;
    [SerializeField] private GameObject lightObj;
    [SerializeField] private GameObject heightTipObj;
    [SerializeField] private GameObject powerObj;

    [Header("Overhead Placement & Motion")]
    [SerializeField] private Vector2 headOffset = new Vector2(0f, 2.38f);
    [SerializeField] private Vector2 crawlHeadOffset = new Vector2(0f, 1.45f);
    [SerializeField] private float bobSpeed = 3.2f;
    [SerializeField] private float bobAmount = 0.045f;
    [SerializeField] private float fadeSpeed = 9f;

    private TopDownMovement playerMove;
    private Object activeSource;
    private GameObject currentIcon;
    private SpriteRenderer frameRenderer;
    private SpriteRenderer innerGlowRenderer;
    private SpriteRenderer dotUpperRenderer;
    private SpriteRenderer dotLowerRenderer;
    private SpriteRenderer qBadgeRenderer;
    private TMP_Text questionTmp;

    private float visibility;
    private float currentHeadY;
    private Vector3 baseScale = Vector3.one;

    private static Sprite cachedFrameSprite;
    private static Sprite cachedDiscSprite;

    private void Awake()
    {
        Instance = this;
        playerMove = GetComponentInParent<TopDownMovement>();
        currentHeadY = headOffset.y;

        if (bubbleBackground == null) bubbleBackground = FindChildSafe("BubbleBackground");
        if (crowbarObj == null) crowbarObj = FindChildSafe("Crowbar");
        if (keycardObj == null) keycardObj = FindChildSafe("Keycard");
        if (cloakObj == null) cloakObj = FindChildSafe("Cloak");
        if (lightObj == null) lightObj = FindChildSafe("light");
        if (heightTipObj == null) heightTipObj = FindChildSafe("HeightTip");
        if (powerObj == null) powerObj = FindChildSafe("Power");

        ResolveSortingAndStyle();

        EnsureIcon(ref crowbarObj, "Crowbar", "DoorHints/Crowbar");
        EnsureIcon(ref keycardObj, "Keycard", "DoorHints/Keycard");
        EnsureIcon(ref lightObj, "light", "DoorHints/Flashlight");
        EnsureIcon(ref powerObj, "Power", "DoorHints/Power");
        EnsureIcon(ref cloakObj, "Cloak", "Coat_Hide");
        EnsureIcon(ref heightTipObj, "HeightTip", null);

        HideAllIcons();
        ApplyVisualAlpha(0f);
    }

    private void LateUpdate()
    {
        bool blocked = !HintsEnabled || CoatHide.IsHidden || Time.timeScale <= 0.001f;
        float targetVis = (activeSource != null && !blocked) ? 1f : 0f;
        visibility = Mathf.MoveTowards(visibility, targetVis, Time.unscaledDeltaTime * fadeSpeed);

        if (visibility <= 0.001f)
        {
            ApplyVisualAlpha(0f);
            if (targetVis <= 0f && currentIcon != null)
            {
                currentIcon.SetActive(false);
                currentIcon = null;
            }
            return;
        }

        float targetY = (playerMove != null && playerMove.IsCrawling) ? crawlHeadOffset.y : headOffset.y;
        currentHeadY = Mathf.Lerp(currentHeadY, targetY, Time.deltaTime * 10f);
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmount;
        transform.localPosition = new Vector3(headOffset.x, currentHeadY + bob, 0f);

        // Keep world X scale positive so icons and '?' never mirror when the player flips
        float parentSign = (transform.parent != null && transform.parent.lossyScale.x < 0f) ? -1f : 1f;
        float popScale = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 1f, visibility));
        transform.localScale = new Vector3(baseScale.x * popScale * parentSign, baseScale.y * popScale, 1f);

        ApplyVisualAlpha(visibility);
    }

    public void ShowHint(HintItemType itemType, Object source)
    {
        if (!HintsEnabled) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        GameObject nextIcon = GetItemObject(itemType);
        if (activeSource == source && currentIcon == nextIcon && nextIcon != null && nextIcon.activeSelf)
            return;

        activeSource = source;
        if (currentIcon != nextIcon)
        {
            HideAllIcons();
            currentIcon = nextIcon;
            if (currentIcon != null) currentIcon.SetActive(true);
        }
        else if (currentIcon != null && !currentIcon.activeSelf)
        {
            currentIcon.SetActive(true);
        }

        if (bubbleBackground != null && !bubbleBackground.activeSelf)
            bubbleBackground.SetActive(true);
    }

    public void HideHint(Object source)
    {
        if (activeSource == source || source == null)
            activeSource = null;
    }

    private void ResolveSortingAndStyle()
    {
        // Match InteractPrompt sorting layer so walls/darkness never clip the overhead thought badge
        int sortLayerId = 0;
        int baseOrder = 92;
        var prompt = transform.parent != null ? transform.parent.GetComponentInChildren<InteractPrompt>(true) : null;
        if (prompt != null && prompt.TryGetComponent(out SpriteRenderer promptSr))
        {
            sortLayerId = promptSr.sortingLayerID;
            baseOrder = promptSr.sortingOrder - 4;
        }
        else if (bubbleBackground != null && bubbleBackground.TryGetComponent(out SpriteRenderer existingBg))
        {
            sortLayerId = existingBg.sortingLayerID;
            baseOrder = existingBg.sortingOrder;
        }

        Material unlitMat = null;
        if (bubbleBackground != null && bubbleBackground.TryGetComponent(out SpriteRenderer bgSr))
        {
            frameRenderer = bgSr;
            unlitMat = bgSr.sharedMaterial;
        }
        else
        {
            bubbleBackground = new GameObject("BubbleBackground");
            bubbleBackground.transform.SetParent(transform, false);
            frameRenderer = bubbleBackground.AddComponent<SpriteRenderer>();
        }

        // Replace stark white cartoon cloud with the game's dark worn iron slot frame
        Sprite frameSp = GetFrameSprite();
        bubbleBackground.transform.localPosition = Vector3.zero;
        if (frameSp != null)
        {
            frameRenderer.sprite = frameSp;
            frameRenderer.drawMode = SpriteDrawMode.Simple;
            float s = 0.72f / Mathf.Max(frameSp.bounds.size.x, frameSp.bounds.size.y);
            bubbleBackground.transform.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            bubbleBackground.transform.localScale = Vector3.one * 0.16f;
        }
        frameRenderer.sortingLayerID = sortLayerId;
        frameRenderer.sortingOrder = baseOrder;

        // Soft warm inner glow behind dark metal icons (Crowbar / Power lever) for readability in dark rooms
        Sprite disc = GetSoftDiscSprite();
        innerGlowRenderer = CreateHelperSprite("IconContrastGlow", Vector3.zero, 0.56f, disc, unlitMat, sortLayerId, baseOrder + 1);

        // Two subtle trailing thought wisps leading down toward the protagonist's head
        dotUpperRenderer = CreateHelperSprite("ThoughtDotUpper", new Vector3(-0.08f, -0.43f, 0f), 0.13f, disc, unlitMat, sortLayerId, baseOrder);
        dotLowerRenderer = CreateHelperSprite("ThoughtDotLower", new Vector3(-0.03f, -0.54f, 0f), 0.085f, disc, unlitMat, sortLayerId, baseOrder);

        // Restyle the '?' into a compact corner clue badge instead of a giant floating cartoon '?'
        Transform qTrans = bubbleBackground.transform.Find("QuestionMark") ?? transform.Find("QuestionMark");
        if (qTrans != null)
        {
            qTrans.SetParent(transform, false);
            qTrans.localPosition = new Vector3(0.29f, 0.28f, 0f);
            qTrans.localScale = Vector3.one;
            questionTmp = qTrans.GetComponent<TMP_Text>();
            if (questionTmp != null)
            {
                questionTmp.text = "?";
                questionTmp.fontSize = 2.6f;
                questionTmp.enableAutoSizing = false;
                questionTmp.alignment = TextAlignmentOptions.Center;
                TMP_FontAsset horrorFont = FindHorrorFont();
                if (horrorFont != null) questionTmp.font = horrorFont;

                if (qTrans.TryGetComponent(out MeshRenderer mr))
                {
                    mr.sortingLayerID = sortLayerId;
                    mr.sortingOrder = baseOrder + 4;
                }
            }
            qBadgeRenderer = CreateHelperSprite("QuestionBadgeBg", new Vector3(0.29f, 0.28f, 0f), 0.24f, disc, unlitMat, sortLayerId, baseOrder + 3);
        }
    }

    private SpriteRenderer CreateHelperSprite(string name, Vector3 localPos, float size, Sprite sp, Material mat, int layerId, int order)
    {
        Transform existing = transform.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        float s = sp != null ? size / Mathf.Max(0.01f, sp.bounds.size.x) : size;
        go.transform.localScale = new Vector3(s, s, 1f);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
        return sr;
    }

    private void EnsureIcon(ref GameObject slotObj, string childName, string resourcePath)
    {
        Sprite fallback = !string.IsNullOrEmpty(resourcePath) ? LoadTightSprite(resourcePath) : null;

        if (slotObj == null && fallback != null)
        {
            slotObj = new GameObject(childName);
            slotObj.transform.SetParent(transform, false);
            slotObj.AddComponent<SpriteRenderer>().sprite = fallback;
        }
        if (slotObj == null) return;

        SpriteRenderer sr = slotObj.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) return;
        if (sr.sprite == null && fallback != null) sr.sprite = fallback;
        // Replace unsliced full-rect Power sprite if needed so transparent padding never shifts it down
        if (childName == "Power" && fallback != null) sr.sprite = fallback;

        if (frameRenderer != null)
        {
            sr.sortingLayerID = frameRenderer.sortingLayerID;
            sr.sortingOrder = frameRenderer.sortingOrder + 2;
            if (frameRenderer.sharedMaterial != null) sr.sharedMaterial = frameRenderer.sharedMaterial;
        }

        // Normalize every icon to uniform size and center its visible bounds at (0, 0) inside the frame
        if (sr.sprite != null)
        {
            Bounds b = sr.sprite.bounds;
            float maxDim = Mathf.Max(b.size.x, b.size.y);
            float scale = maxDim > 0.001f ? (0.44f / maxDim) : 1f;
            slotObj.transform.localScale = new Vector3(scale, scale, 1f);
            slotObj.transform.localPosition = new Vector3(-b.center.x * scale, -b.center.y * scale, 0f);
        }
        slotObj.SetActive(false);
    }

    private void ApplyVisualAlpha(float a)
    {
        bool show = a > 0.001f;
        if (bubbleBackground != null && bubbleBackground.activeSelf != show)
            bubbleBackground.SetActive(show);

        SetSpriteColor(frameRenderer, new Color(0.86f, 0.84f, 0.88f, 0.96f * a), show);
        SetSpriteColor(innerGlowRenderer, new Color(0.95f, 0.88f, 0.78f, 0.30f * a), show);
        SetSpriteColor(dotUpperRenderer, new Color(0.18f, 0.16f, 0.19f, 0.90f * a), show);
        SetSpriteColor(dotLowerRenderer, new Color(0.18f, 0.16f, 0.19f, 0.72f * a), show);
        SetSpriteColor(qBadgeRenderer, new Color(0.10f, 0.03f, 0.04f, 0.92f * a), show);

        if (questionTmp != null)
        {
            questionTmp.gameObject.SetActive(show);
            questionTmp.color = new Color(0.95f, 0.28f, 0.26f, a);
        }

        if (currentIcon != null)
        {
            var sr = currentIcon.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) sr.color = new Color(1f, 1f, 1f, a);
        }
    }

    private static void SetSpriteColor(SpriteRenderer sr, Color c, bool show)
    {
        if (sr == null) return;
        sr.enabled = show;
        sr.color = c;
    }

    private GameObject GetItemObject(HintItemType type) => type switch
    {
        HintItemType.Crowbar => crowbarObj,
        HintItemType.Keycard => keycardObj,
        HintItemType.Cloak => cloakObj,
        HintItemType.Light => lightObj,
        HintItemType.HeightTip => heightTipObj,
        HintItemType.Power => powerObj,
        _ => null
    };

    private void HideAllIcons()
    {
        if (crowbarObj != null) crowbarObj.SetActive(false);
        if (keycardObj != null) keycardObj.SetActive(false);
        if (cloakObj != null) cloakObj.SetActive(false);
        if (lightObj != null) lightObj.SetActive(false);
        if (heightTipObj != null) heightTipObj.SetActive(false);
        if (powerObj != null) powerObj.SetActive(false);
    }

    private GameObject FindChildSafe(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.gameObject : null;
    }

    private static Sprite GetFrameSprite()
    {
        if (cachedFrameSprite != null) return cachedFrameSprite;
        Texture2D tex = Resources.Load<Texture2D>("Hotbar/Slot");
        if (tex == null) return null;
        tex.filterMode = FilterMode.Bilinear;
        cachedFrameSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return cachedFrameSprite;
    }

    private static Sprite GetSoftDiscSprite()
    {
        if (cachedDiscSprite != null) return cachedDiscSprite;
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float alpha = Mathf.Clamp01(1f - d * d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        cachedDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return cachedDiscSprite;
    }

    private static Sprite LoadTightSprite(string resourcePath)
    {
        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null && tex.isReadable)
        {
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height, minX = w, minY = h, maxX = 0, maxY = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > 12)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX >= minX && maxY >= minY)
            {
                Rect rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Mathf.Max(rect.width, rect.height));
            }
        }
        Sprite sp = Resources.Load<Sprite>(resourcePath);
        if (sp != null) return sp;
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height)) : null;
    }

    private static TMP_FontAsset FindHorrorFont()
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("ChainsawCarnage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        return null;
    }
}
