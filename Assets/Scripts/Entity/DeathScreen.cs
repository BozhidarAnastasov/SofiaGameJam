using System.Collections;
using ArcadesTemplate.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeathScreen : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image background;
    [SerializeField] private Image face;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameTimes = { 0.35f, 0.18f, 0.12f, 0.10f };
    [SerializeField] private float holdTime = 1.8f;

    [Header("You Died Text (TMP)")]
    [SerializeField] private TMP_Text youDiedText;
    [SerializeField] private TMP_FontAsset youDiedFont;
    [SerializeField] private string youDiedMessage = "YOU DIED";
    [SerializeField] private float youDiedFontSize = 125f;
    [SerializeField] private Color youDiedColor = new Color(0.95f, 0.12f, 0.14f, 1f);
    [SerializeField] private Color youDiedBottomColor = new Color(0.42f, 0.02f, 0.04f, 1f);
    [SerializeField] private Vector2 youDiedOffset = Vector2.zero;

    [Header("End-State Readability")]
    [Range(0f, 0.95f)] [SerializeField] private float endDimmerAlpha = 0.74f;
    [Range(0f, 1f)] [SerializeField] private float endFaceAlpha = 0.26f;
    [SerializeField] private float maxButtonFontSize = 48f;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private bool autoCreateButtonsIfMissing = true;
    [SerializeField] private string mainMenuSceneName = "Main_scene 1";

    [Header("Forced Hover Highlight Settings")]
    [SerializeField] private Color buttonNormalColor = new Color(0.90f, 0.92f, 0.94f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(1f, 0.28f, 0.28f, 1f);
    [SerializeField] private Color buttonPressedColor = new Color(0.55f, 0.1f, 0.1f, 1f);
    [SerializeField] private float hoverScale = 1.08f;

    [Header("Afterwards (Fallback if no buttons assigned)")]
    [SerializeField] private bool goToLoseScreen = false;
    [SerializeField] private InputActionReference retryAction;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stinger;

    public UnityEvent onShown;
    public UnityEvent onFinished;

    private bool shown;
    private bool cachedFace;
    private Vector2 initialFacePos;
    private Vector3 initialFaceScale = Vector3.one;
    private CanvasGroup youDiedGroup;
    private Image faceEcho;
    private Image dimmer;
    private static Sprite cachedPanelSprite;

    private void Awake()
    {
        CacheFace();
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        else if (background != null) background.gameObject.SetActive(false);

        if (face != null) face.enabled = false;
        if (youDiedText != null) youDiedText.gameObject.SetActive(false);

        WireButton(retryButton, Retry, false);
        WireButton(mainMenuButton, ReturnToMainMenu, false);
    }

    private void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    [ContextMenu("Test Death Screen Now")]
    public void Show()
    {
        ActivateHierarchy(gameObject);
        if (shown) return;
        shown = true;

        EnsureEventSystem();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
            if (canvas.sortingOrder < 100) canvas.sortingOrder = 100;
            GetOrAdd<GraphicRaycaster>(canvas.gameObject);
        }

        Transform uiRoot = background != null && background.transform.parent != null
            ? background.transform.parent : (canvas != null ? canvas.transform : transform);

        EnsureLayersAndText(uiRoot);
        if (autoCreateButtonsIfMissing && (retryButton == null || mainMenuButton == null))
            CreateDefaultButtons(uiRoot);

        if (canvas != null)
        {
            foreach (var g in canvas.GetComponentsInChildren<Graphic>(true))
            {
                bool onBtn = (retryButton != null && g.transform.IsChildOf(retryButton.transform)) ||
                             (mainMenuButton != null && g.transform.IsChildOf(mainMenuButton.transform));
                if (g != background && !onBtn) g.raycastTarget = false;
            }
        }

        if (group != null) { ActivateHierarchy(group.gameObject); group.alpha = 1f; group.blocksRaycasts = group.interactable = true; }
        if (background != null) { ActivateHierarchy(background.gameObject); background.enabled = background.raycastTarget = true; background.color = Color.black; background.transform.SetAsFirstSibling(); }

        if (audioSource != null && stinger != null) audioSource.PlayOneShot(stinger);
        else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.youDied : null);
        onShown?.Invoke();

        dimmer.enabled = true;
        for (float e = 0f; e < 0.18f; e += Time.unscaledDeltaTime)
        {
            float k = e / 0.18f;
            dimmer.color = new Color(0.38f, 0.01f, 0.02f, Mathf.Lerp(0.38f, 0f, k * k));
            yield return null;
        }
        dimmer.color = Color.clear;

        if (face != null)
        {
            CacheFace();
            ActivateHierarchy(face.gameObject);
            face.enabled = true;
            faceEcho.transform.SetSiblingIndex(face.transform.GetSiblingIndex() + 1);
            dimmer.transform.SetAsLastSibling();
            youDiedText.transform.SetAsLastSibling();

            Sprite[] seq = (frames != null && System.Array.Exists(frames, s => s != null)) ? frames
                         : (face.sprite != null ? new[] { face.sprite } : System.Array.Empty<Sprite>());
            Sprite prev = null;
            for (int i = 0; i < seq.Length; i++)
            {
                if (seq[i] != null) face.sprite = seq[i];
                bool last = i == seq.Length - 1;
                float dur = (frameTimes != null && i < frameTimes.Length && frameTimes[i] > 0f) ? frameTimes[i] : (seq.Length == 1 ? 0.32f : 0.12f);
                float prog = seq.Length > 1 ? (float)i / (seq.Length - 1) : 1f;
                float bright = Mathf.Lerp(0.42f, 1f, prog);
                float s0 = last ? 1.23f : Mathf.Lerp(0.92f, 1.08f, prog);
                float s1 = last ? 1.03f : Mathf.Lerp(0.98f, 1.02f, prog);
                float shake = last ? 17f : Mathf.Lerp(3f, 7f, prog);

                faceEcho.sprite = last ? face.sprite : prev;
                faceEcho.enabled = faceEcho.sprite != null;

                for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
                {
                    float k = Mathf.Clamp01(e / dur), ease = 1f - Mathf.Pow(1f - k, 3f), damp = (1f - k) * (1f - k);
                    face.rectTransform.localScale = initialFaceScale * Mathf.Lerp(s0, s1, ease);
                    face.rectTransform.anchoredPosition = initialFacePos + Random.insideUnitCircle * (shake * damp);
                    face.color = new Color(bright, bright, bright, 1f);

                    if (faceEcho.enabled)
                    {
                        faceEcho.rectTransform.localScale = initialFaceScale * (last ? Mathf.Lerp(1.04f, 1.24f, ease) : Mathf.Lerp(1f, 1.05f, k));
                        faceEcho.rectTransform.anchoredPosition = last ? initialFacePos + Random.insideUnitCircle * (8f * damp) : initialFacePos;
                        faceEcho.color = last ? new Color(0.95f, 0.18f, 0.18f, 0.55f * (1f - ease)) : new Color(bright * 0.85f, bright * 0.7f, bright * 0.7f, 0.45f * (1f - k));
                    }
                    if (last) dimmer.color = new Color(0.55f, 0.02f, 0.02f, 0.45f * damp);
                    yield return null;
                }
                prev = face.sprite;
            }
            faceEcho.enabled = false;
            yield return new WaitForSecondsRealtime(0.12f);
        }

        ActivateHierarchy(youDiedText.gameObject);
        youDiedText.transform.SetAsLastSibling();
        float hold = Mathf.Max(0.6f, holdTime), slamDur = Mathf.Min(0.48f, hold * 0.45f);
        Color endFaceColor = new Color(0.42f, 0.18f, 0.18f, endFaceAlpha);

        for (float e = 0f; e < hold; e += Time.unscaledDeltaTime)
        {
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e / hold - 0.18f) / 0.82f));
            if (face != null)
            {
                Color scareTint = Color.Lerp(Color.white, new Color(1f, 0.48f, 0.48f, 1f), (0.5f + 0.5f * Mathf.Sin(e * 5.5f)) * 0.35f);
                face.color = Color.Lerp(scareTint, endFaceColor, settle);
                face.rectTransform.localScale = initialFaceScale * Mathf.Lerp(1.03f, 0.95f, settle);
                face.rectTransform.anchoredPosition = initialFacePos + Random.insideUnitCircle * (1.8f * (1f - settle));
            }
            dimmer.color = new Color(0.02f, 0f, 0f, Mathf.Lerp(0f, endDimmerAlpha, settle));
            UpdateTitleSlam(e, slamDur);
            PulseTitle(e);
            yield return null;
        }
        UpdateTitleSlam(slamDur, slamDur);
        if (face != null) { face.color = endFaceColor; face.rectTransform.localScale = initialFaceScale * 0.95f; face.rectTransform.anchoredPosition = initialFacePos; }
        dimmer.color = new Color(0.02f, 0f, 0f, endDimmerAlpha);

        onFinished?.Invoke();

        if (retryButton != null || mainMenuButton != null)
        {
            StyleAndShowButton(retryButton, canvas);
            StyleAndShowButton(mainMenuButton, canvas);
            LayoutButtons(retryButton, mainMenuButton);
            StartCoroutine(SlideButtonsIn());
            StartCoroutine(DeathScreenArcadeInput.WaitForChoice(retryButton != null ? Retry : (System.Action)null, mainMenuButton != null ? ReturnToMainMenu : (System.Action)null));
            for (float t = 0f; ; t += Time.unscaledDeltaTime) { PulseTitle(t); yield return null; }
        }

        if (goToLoseScreen && ArcadeSceneManager.Instance != null)
        {
            Time.timeScale = 1f;
            ArcadeSceneManager.Instance.LoadLoseScreen(0);
            yield break;
        }

        if (retryAction != null)
        {
            retryAction.action.Enable();
            for (float t = 0f; !retryAction.action.WasPressedThisFrame(); t += Time.unscaledDeltaTime) { PulseTitle(t); yield return null; }
            Retry();
        }
    }

    public void Retry()
    {
        GameAudio.UiClick();
        Time.timeScale = 1f;
        Scene s = SceneManager.GetActiveScene();
        if (s.buildIndex >= 0) SceneManager.LoadScene(s.buildIndex);
#if UNITY_EDITOR
        else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(s.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }

    public void ReturnToMainMenu()
    {
        GameAudio.UiClick();
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName)) SceneManager.LoadScene(mainMenuSceneName);
        else SceneManager.LoadScene(0);
    }

    private void EnsureLayersAndText(Transform uiRoot)
    {
        if (face != null && faceEcho == null)
        {
            var go = new GameObject("Face_Echo", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(face.transform.parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = face.rectTransform.anchorMin; rt.anchorMax = face.rectTransform.anchorMax;
            rt.pivot = face.rectTransform.pivot; rt.sizeDelta = face.rectTransform.sizeDelta;
            rt.anchoredPosition = face.rectTransform.anchoredPosition;
            faceEcho = go.GetComponent<Image>();
            faceEcho.preserveAspect = face.preserveAspect;
            faceEcho.enabled = false;
        }
        if (dimmer == null)
        {
            var go = new GameObject("Readability_Dimmer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(uiRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            dimmer = go.GetComponent<Image>();
            dimmer.enabled = false;
        }

        bool created = false;
        if (youDiedText == null)
        {
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                bool inBtn = (retryButton != null && t.transform.IsChildOf(retryButton.transform)) ||
                             (mainMenuButton != null && t.transform.IsChildOf(mainMenuButton.transform));
                if (!inBtn) { youDiedText = t; break; }
            }
        }
        if (youDiedText == null)
        {
            var go = new GameObject("Txt_YouDied", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(uiRoot, false);
            youDiedText = go.GetComponent<TextMeshProUGUI>();
            created = true;
        }

        CenterRect(youDiedText.rectTransform, youDiedOffset, new Vector2(Mathf.Max(youDiedText.rectTransform.sizeDelta.x, 1400f), Mathf.Max(youDiedText.rectTransform.sizeDelta.y, 240f)));
        TMP_FontAsset font = ResolveFont();
        if (youDiedFont != null) youDiedText.font = youDiedFont;
        else if (created && font != null) youDiedText.font = font;
        if (!string.IsNullOrEmpty(youDiedMessage) && (created || string.IsNullOrWhiteSpace(youDiedText.text) || youDiedText.text == "New Text"))
            youDiedText.text = youDiedMessage;
        if (created) youDiedText.fontSize = youDiedFontSize;

        youDiedText.color = Color.white;
        youDiedText.enableVertexGradient = true;
        youDiedText.colorGradient = new VertexGradient(youDiedColor, youDiedColor, youDiedBottomColor, youDiedBottomColor);
        youDiedText.alignment = TextAlignmentOptions.Center;
        youDiedText.textWrappingMode = TextWrappingModes.NoWrap;
        youDiedText.overflowMode = TextOverflowModes.Overflow;
        AddShadow(youDiedText.gameObject, -6f);

        youDiedGroup = GetOrAdd<CanvasGroup>(youDiedText.gameObject);
        youDiedGroup.alpha = 0f;
        youDiedGroup.blocksRaycasts = youDiedGroup.interactable = false;
        youDiedText.gameObject.SetActive(false);
    }

    private void CreateDefaultButtons(Transform uiRoot)
    {
        var grp = new GameObject("Btn_group", typeof(RectTransform), typeof(CanvasGroup));
        grp.transform.SetParent(uiRoot, false);
        CenterRect(grp.GetComponent<RectTransform>(), Vector2.zero, new Vector2(100f, 100f));
        TMP_FontAsset font = ResolveFont();
        if (retryButton == null) retryButton = MakeButton("Btn_retry", "Press A to Retry", grp.transform, font, Retry);
        if (mainMenuButton == null) mainMenuButton = MakeButton("Btn_mainMenu", "Press B for Main Menu", grp.transform, font, ReturnToMainMenu);
    }

    private Button MakeButton(string name, string label, Transform parent, TMP_FontAsset font, UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        CenterRect(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(760f, 68f));

        var txtGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        if (font != null) tmp.font = font;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = tmp;
        WireButton(btn, action, false);
        return btn;
    }

    private void StyleAndShowButton(Button btn, Canvas rootCanvas)
    {
        if (btn == null) return;
        ActivateHierarchy(btn.gameObject);
        for (int i = 0; i < btn.transform.childCount; i++) btn.transform.GetChild(i).gameObject.SetActive(true);
        btn.interactable = true;

        var rt = btn.GetComponent<RectTransform>();
        Vector2 size = (rt.sizeDelta.x < 680f || rt.sizeDelta.y < 56f || rt.sizeDelta.y > 90f) ? new Vector2(760f, 68f) : rt.sizeDelta;
        CenterRect(rt, rt.anchoredPosition, size);

        var img = GetOrAdd<Image>(btn.gameObject);
        Sprite panel = GetPanelSprite();
        img.sprite = panel;
        img.type = panel != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = panel != null ? new Color(0.88f, 0.86f, 0.88f, 0.96f) : new Color(0.06f, 0.01f, 0.01f, 0.72f);
        img.enabled = img.raycastTarget = true;

        var border = GetOrAdd<Outline>(btn.gameObject);
        border.effectColor = new Color(0.55f, 0.08f, 0.10f, 0.55f);
        border.effectDistance = new Vector2(1.5f, -1.5f);

        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enabled = true;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = Mathf.Max(26f, maxButtonFontSize > 0f ? maxButtonFontSize : 48f);
            label.characterSpacing = 3f;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(20f, 4f); label.rectTransform.offsetMax = new Vector2(-20f, -4f);
            AddShadow(label.gameObject, -3f);
        }

        foreach (var cg in btn.GetComponentsInParent<CanvasGroup>(true)) { cg.alpha = 1f; cg.blocksRaycasts = cg.interactable = true; }
        foreach (var c in btn.GetComponentsInParent<Canvas>(true))
        {
            c.enabled = true;
            if (rootCanvas != null && c != rootCanvas) { c.overrideSorting = true; c.sortingOrder = rootCanvas.sortingOrder + 5; }
            GetOrAdd<GraphicRaycaster>(c.gameObject);
        }
        (btn.transform.parent != null && btn.transform.parent != transform && (rootCanvas == null || btn.transform.parent != rootCanvas.transform)
            ? btn.transform.parent : btn.transform).SetAsLastSibling();

        AttachHover(btn, img, label, border);
    }

    private void LayoutButtons(Button a, Button b)
    {
        if (a != null && b != null)
        {
            float step = Mathf.Max(a.GetComponent<RectTransform>().rect.height, 68f) + 20f;
            a.GetComponent<RectTransform>().anchoredPosition = youDiedOffset + new Vector2(0f, -80f);
            b.GetComponent<RectTransform>().anchoredPosition = youDiedOffset + new Vector2(0f, -80f - step);
        }
        else if (a != null) a.GetComponent<RectTransform>().anchoredPosition = youDiedOffset + new Vector2(0f, -100f);
        else if (b != null) b.GetComponent<RectTransform>().anchoredPosition = youDiedOffset + new Vector2(0f, -100f);
    }

    private IEnumerator SlideButtonsIn()
    {
        CanvasGroup cgA = retryButton != null ? GetOrAdd<CanvasGroup>(retryButton.gameObject) : null;
        CanvasGroup cgB = mainMenuButton != null ? GetOrAdd<CanvasGroup>(mainMenuButton.gameObject) : null;
        RectTransform rtA = retryButton != null ? retryButton.GetComponent<RectTransform>() : null;
        RectTransform rtB = mainMenuButton != null ? mainMenuButton.GetComponent<RectTransform>() : null;
        Vector2 posA = rtA != null ? rtA.anchoredPosition : Vector2.zero, posB = rtB != null ? rtB.anchoredPosition : Vector2.zero;
        Vector2 title0 = youDiedText.rectTransform.anchoredPosition, title1 = youDiedOffset + new Vector2(0f, 55f);

        for (float e = 0f; e < 0.42f; e += Time.unscaledDeltaTime)
        {
            youDiedText.rectTransform.anchoredPosition = Vector2.Lerp(title0, title1, 1f - Mathf.Pow(1f - Mathf.Clamp01(e / 0.42f), 3f));
            if (rtA != null) { float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / 0.34f), 3f); cgA.alpha = k; rtA.anchoredPosition = posA + new Vector2(0f, Mathf.Lerp(-20f, 0f, k)); }
            if (rtB != null) { float k = 1f - Mathf.Pow(1f - Mathf.Clamp01((e - 0.08f) / 0.34f), 3f); cgB.alpha = k; rtB.anchoredPosition = posB + new Vector2(0f, Mathf.Lerp(-20f, 0f, k)); }
            yield return null;
        }
        youDiedText.rectTransform.anchoredPosition = title1;
        if (rtA != null) { cgA.alpha = 1f; rtA.anchoredPosition = posA; }
        if (rtB != null) { cgB.alpha = 1f; rtB.anchoredPosition = posB; }
    }

    private void AttachHover(Button btn, Image bg, TMP_Text label, Outline border)
    {
        btn.transition = Selectable.Transition.None;
        Vector3 baseScale = btn.transform.localScale;
        Color normal = new Color(Mathf.Max(buttonNormalColor.r, 0.88f), Mathf.Max(buttonNormalColor.g, 0.88f), Mathf.Max(buttonNormalColor.b, 0.90f), 1f);
        Graphic target = label != null ? (Graphic)label : bg;
        if (target != null) target.color = normal;

        var trigger = GetOrAdd<EventTrigger>(btn.gameObject);
        trigger.triggers.Clear();
        void Bind(EventTriggerType t, UnityAction cb) { var e = new EventTrigger.Entry { eventID = t }; e.callback.AddListener(_ => cb()); trigger.triggers.Add(e); }

        Bind(EventTriggerType.PointerEnter, () => {
            if (!btn.interactable) return;
            GameAudio.UiHover();
            btn.transform.localScale = baseScale * hoverScale;
            if (target != null) target.color = buttonHoverColor;
            bg.color = bg.sprite != null ? new Color(1f, 0.68f, 0.68f, 1f) : new Color(0.18f, 0.02f, 0.03f, 0.88f);
            border.effectColor = new Color(0.95f, 0.20f, 0.20f, 0.9f);
        });
        Bind(EventTriggerType.PointerExit, () => {
            btn.transform.localScale = baseScale;
            if (target != null) target.color = normal;
            bg.color = bg.sprite != null ? new Color(0.88f, 0.86f, 0.88f, 0.96f) : new Color(0.06f, 0.01f, 0.01f, 0.72f);
            border.effectColor = new Color(0.55f, 0.08f, 0.10f, 0.55f);
        });
        Bind(EventTriggerType.PointerDown, () => { if (btn.interactable) { btn.transform.localScale = baseScale * 0.95f; if (target != null) target.color = buttonPressedColor; } });
        Bind(EventTriggerType.PointerUp, () => { btn.transform.localScale = baseScale * hoverScale; if (target != null) target.color = buttonHoverColor; });
    }

    private void UpdateTitleSlam(float e, float dur)
    {
        float k = dur > 0f ? Mathf.Clamp01(e / dur) : 1f, ease = 1f - Mathf.Pow(1f - k, 3f);
        youDiedGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.25f));
        youDiedText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.26f, 1f, ease);
        youDiedText.characterSpacing = Mathf.Lerp(22f, 8f, ease);
        youDiedText.rectTransform.anchoredPosition = youDiedOffset;
    }

    private void PulseTitle(float t)
    {
        Color top = Color.Lerp(youDiedColor, new Color(1f, 0.28f, 0.28f, 1f), (0.5f + 0.5f * Mathf.Sin(t * 2.4f)) * 0.3f);
        youDiedText.colorGradient = new VertexGradient(top, top, youDiedBottomColor, youDiedBottomColor);
    }

    private TMP_FontAsset ResolveFont()
    {
        if (youDiedFont != null) return youDiedFont;
        TMP_FontAsset btnFont = (retryButton != null ? retryButton.GetComponentInChildren<TMP_Text>(true)?.font : null)
                             ?? (mainMenuButton != null ? mainMenuButton.GetComponentInChildren<TMP_Text>(true)?.font : null);
        if (btnFont != null) return btnFont;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("ChainsawCarnage", System.StringComparison.OrdinalIgnoreCase) >= 0) return f;
        return TMP_Settings.defaultFontAsset;
    }

    private void CacheFace()
    {
        if (cachedFace || face == null) return;
        initialFacePos = face.rectTransform.anchoredPosition;
        initialFaceScale = face.rectTransform.localScale == Vector3.zero ? Vector3.one : face.rectTransform.localScale;
        cachedFace = true;
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null) { new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); return; }
        es.gameObject.SetActive(true);
        es.enabled = true;
        if (es.TryGetComponent(out StandaloneInputModule legacy)) Destroy(legacy);
        GetOrAdd<InputSystemUIInputModule>(es.gameObject);
    }

    private static void WireButton(Button btn, UnityAction action, bool active)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.gameObject.SetActive(active);
    }

    private static void CenterRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
        if (rt.localScale.x == 0f || rt.localScale.y == 0f) rt.localScale = Vector3.one;
    }

    private static void AddShadow(GameObject go, float yOffset)
    {
        var o = GetOrAdd<Outline>(go); o.effectColor = new Color(0f, 0f, 0f, 0.92f); o.effectDistance = new Vector2(2f, -2f);
        var s = GetOrAdd<Shadow>(go); s.effectColor = new Color(0f, 0f, 0f, 0.9f); s.effectDistance = new Vector2(0f, yOffset);
    }

    private static void ActivateHierarchy(GameObject go)
    {
        for (Transform t = go != null ? go.transform : null; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static Sprite GetPanelSprite()
    {
        if (cachedPanelSprite != null) return cachedPanelSprite;
        Texture2D tex = Resources.Load<Texture2D>("Hotbar/Panel");
        if (tex == null) return null;
        tex.filterMode = FilterMode.Bilinear;
        cachedPanelSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
        return cachedPanelSprite;
    }
}
