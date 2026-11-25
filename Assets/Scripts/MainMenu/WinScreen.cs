using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// Plays the 4-frame psycho sprite sheet from RIGHT to LEFT (Index 3 -> 2 -> 1 -> 0),
/// leaves a haunting ghostly afterimage that dissolves into darkness, reveals bloody
/// "YOU SURVIVED" text with your custom font, and transitions cleanly to the Main Menu.
public class WinScreen : MonoBehaviour
{
    [Header("1. Psycho Sprite Sheet Frames (Left = 0, Right = 3)")]
    [Tooltip("Drag the 4 sliced frames in normal order (0 = leftmost faint face, 3 = rightmost close-up). Script plays 3 -> 2 -> 1 -> 0!")]
    [SerializeField] private Sprite[] psychoFrames;
    [SerializeField] private float[] retreatStepDurations = { 0.65f, 0.75f, 0.90f };
    [SerializeField] private float afterimageHoldAndDissolve = 1.8f;

    [Header("2. Bloody 'YOU SURVIVED' Text & Custom Font")]
    [Tooltip("Drop your custom horror TMP Font Asset here!")]
    [SerializeField] private TMP_FontAsset surviveFont;
    [SerializeField] private string surviveMessage = "YOU SURVIVED";
    [SerializeField] private float fontSize = 105f;
    [SerializeField] private Color bloodTopColor = new Color(0.88f, 0.06f, 0.06f, 1f);    // Fresh crimson
    [SerializeField] private Color bloodBottomColor = new Color(0.28f, 0.0f, 0.02f, 1f);  // Dried dark blood

    [Header("3. Main Menu Interaction & Transition")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Shows an interactable 'MAIN MENU' button on screen.")]
    [SerializeField] private bool showMainMenuButton = true;
    [Tooltip("Also allow clicking anywhere / pressing Space or Enter once 'YOU SURVIVED' appears.")]
    [SerializeField] private bool clickOrPressAnyKeyToContinue = false;
    [Tooltip("If > 0, automatically transitions to Main Menu after this many seconds even if not clicked (set 0 to wait forever for button click).")]
    [SerializeField] private float autoTransitionAfterSeconds = 0f;
    [SerializeField] private float finalFadeOutTime = 1.1f;

    [Header("4. Optional Manual UI Overrides (Leave empty to Auto-Build!)")]
    [SerializeField] private Canvas customCanvas;
    [SerializeField] private TMP_Text customSurviveText;
    [SerializeField] private Button customMainMenuButton;

    [Header("5. Sound (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip retreatClip;
    [SerializeField] private AudioClip surviveStingerClip;

    public UnityEvent onSequenceStarted;
    public UnityEvent onAfterimageFinished;
    public UnityEvent onTransitioningToMenu;

    // Runtime UI elements
    private CanvasGroup masterCanvasGroup;
    private Image blackBg;
    private Image faceCurrent;
    private Image faceNextBlend;
    private Image faceAfterimage;
    private CanvasGroup surviveTextGroup;
    private TMP_Text activeSurviveText;
    private Button activeMenuButton;
    private Image finalCurtainFade;

    private bool triggered;
    private bool transitioningToMenu;
    private bool uiConstructed;

    private readonly Color btnNormal = new Color(0.75f, 0.15f, 0.15f, 1f);
    private readonly Color btnHover = new Color(1f, 0.32f, 0.32f, 1f);
    private readonly Color btnPressed = new Color(0.45f, 0.05f, 0.05f, 1f);

    private void Awake()
    {
        if (customMainMenuButton != null) customMainMenuButton.gameObject.SetActive(false);
        if (customSurviveText != null) customSurviveText.gameObject.SetActive(false);
    }

    [ContextMenu("Test Win Screen Now (In Play Mode)")]
    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (triggered) return;
        triggered = true;

        EnsureWorkingEventSystem();
        BuildUIIfNeeded();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        Time.timeScale = 0f;
        onSequenceStarted?.Invoke();

        // 1. Cut/fade into dark screen
        masterCanvasGroup.blocksRaycasts = true;
        masterCanvasGroup.interactable = true;
        for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
        {
            masterCanvasGroup.alpha = Mathf.Clamp01(e / 0.3f);
            yield return null;
        }
        masterCanvasGroup.alpha = 1f;

        if (audioSource != null && retreatClip != null)
        {
            audioSource.PlayOneShot(retreatClip);
        }

        yield return new WaitForSecondsRealtime(0.15f);

        // 2. Play Sprite Sheet from RIGHT (Index 3: Close-up) -> LEFT (Index 0: Faint shadow)
        if (psychoFrames != null && psychoFrames.Length > 0 && HasAnySprite(psychoFrames))
        {
            int rightMostIdx = psychoFrames.Length - 1;

            faceCurrent.enabled = true;
            faceCurrent.sprite = psychoFrames[rightMostIdx];
            faceCurrent.color = Color.white;
            faceCurrent.rectTransform.localScale = Vector3.one;

            int step = 0;
            for (int i = rightMostIdx; i > 0; i--)
            {
                Sprite closeSprite = psychoFrames[i];
                Sprite fartherSprite = psychoFrames[i - 1];
                float duration = (retreatStepDurations != null && step < retreatStepDurations.Length)
                    ? retreatStepDurations[step]
                    : 0.75f;
                step++;

                faceCurrent.sprite = closeSprite;
                faceNextBlend.enabled = true;
                faceNextBlend.sprite = fartherSprite;

                for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, e / duration);

                    // Subtle receding pull-back scale as he slips into the dark
                    faceCurrent.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, k);
                    faceNextBlend.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, k);

                    faceCurrent.color = new Color(1f, 1f, 1f, 1f - k);
                    faceNextBlend.color = new Color(1f, 1f, 1f, k);
                    yield return null;
                }

                faceCurrent.sprite = fartherSprite;
                faceCurrent.color = Color.white;
                faceCurrent.rectTransform.localScale = Vector3.one;
                faceNextBlend.enabled = false;
            }

            // 3. Lingering Ghostly Afterimage on Leftmost Frame (Index 0)
            // Creates a faint expanding double-vision echo while the main silhouette sinks into black
            faceAfterimage.enabled = true;
            faceAfterimage.sprite = psychoFrames[0];
            faceAfterimage.rectTransform.localScale = Vector3.one;

            for (float e = 0f; e < afterimageHoldAndDissolve; e += Time.unscaledDeltaTime)
            {
                float k = e / afterimageHoldAndDissolve;
                float smoothK = Mathf.SmoothStep(0f, 1f, k);

                // Base face shrinks further back and fades out
                faceCurrent.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.86f, smoothK);
                faceCurrent.color = new Color(1f, 1f, 1f, 1f - smoothK);

                // Ghostly afterimage drifts slightly outward with a cold eerie echo before vanishing
                float echoAlpha = Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI) * 0.42f * (1f - k);
                faceAfterimage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.01f, 1.12f, smoothK);
                faceAfterimage.rectTransform.anchoredPosition = Random.insideUnitCircle * (1.5f * (1f - k));
                faceAfterimage.color = new Color(0.85f, 0.75f, 0.75f, echoAlpha);

                yield return null;
            }

            faceCurrent.enabled = false;
            faceAfterimage.enabled = false;
        }
        else
        {
            Debug.LogWarning("[WinScreen] Psycho Frames array is empty! Slice the 4-frame sheet and assign elements 0..3.");
            yield return new WaitForSecondsRealtime(1.0f);
        }

        onAfterimageFinished?.Invoke();
        yield return new WaitForSecondsRealtime(0.25f);

        // 4. Reveal Bloody "YOU SURVIVED" Screen
        if (audioSource != null && surviveStingerClip != null)
        {
            audioSource.PlayOneShot(surviveStingerClip);
        }

        if (surviveTextGroup != null)
        {
            surviveTextGroup.gameObject.SetActive(true);
            RectTransform textRT = surviveTextGroup.GetComponent<RectTransform>();

            for (float e = 0f; e < 1.0f; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / 1.0f);
                surviveTextGroup.alpha = k;
                if (textRT != null) textRT.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, k);
                yield return null;
            }
            surviveTextGroup.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(0.4f);

        // 5. Reveal Interactable Main Menu Button & Wait for Click / Auto-Transition
        if (showMainMenuButton && activeMenuButton != null)
        {
            RevealButtonWithFade(activeMenuButton);
        }

        float waitTimer = 0f;
        while (!transitioningToMenu)
        {
            // Subtle blood pulse on "YOU SURVIVED" while waiting
            if (activeSurviveText != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
                Color top = Color.Lerp(bloodTopColor, new Color(1f, 0.15f, 0.15f, 1f), pulse * 0.25f);
                activeSurviveText.colorGradient = new VertexGradient(top, top, bloodBottomColor, bloodBottomColor);
            }

            if (clickOrPressAnyKeyToContinue && WasContinueInputPressed())
            {
                TriggerMainMenuTransition();
                yield break;
            }

            if (autoTransitionAfterSeconds > 0f)
            {
                waitTimer += Time.unscaledDeltaTime;
                if (waitTimer >= autoTransitionAfterSeconds)
                {
                    TriggerMainMenuTransition();
                    yield break;
                }
            }

            yield return null;
        }
    }

    public void TriggerMainMenuTransition()
    {
        if (transitioningToMenu) return;
        transitioningToMenu = true;
        StartCoroutine(FadeOutAndLoadMainMenu());
    }

    private IEnumerator FadeOutAndLoadMainMenu()
    {
        onTransitioningToMenu?.Invoke();

        if (activeMenuButton != null) activeMenuButton.interactable = false;

        // Smooth curtain transition over everything into Main Menu
        if (finalCurtainFade != null)
        {
            finalCurtainFade.enabled = true;
            finalCurtainFade.transform.SetAsLastSibling();

            for (float e = 0f; e < finalFadeOutTime; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / finalFadeOutTime);
                finalCurtainFade.color = new Color(0f, 0f, 0f, k);
                yield return null;
            }
            finalCurtainFade.color = Color.black;
        }

        yield return new WaitForSecondsRealtime(0.15f);

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else if (Application.CanStreamedLevelBeLoaded("Main_scene 1"))
            SceneManager.LoadScene("Main_scene 1");
        else
            SceneManager.LoadScene(0);
    }

    private void RevealButtonWithFade(Button btn)
    {
        ActivateParents(btn.gameObject);
        btn.interactable = true;

        CanvasGroup cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, 0.5f));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        cg.alpha = from;
        for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
        {
            cg.alpha = Mathf.Lerp(from, to, e / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private bool WasContinueInputPressed()
    {
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.eKey.wasPressedThisFrame))
        {
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !showMainMenuButton)
        {
            return true;
        }
        return false;
    }

    // =========================================================================
    // AUTO-BUILDER (Builds Clean 1920x1080 Overlay If Custom Slots Left Empty)
    // =========================================================================
    private void BuildUIIfNeeded()
    {
        if (uiConstructed) return;
        uiConstructed = true;

        Canvas canvas = customCanvas;
        if (canvas == null)
        {
            GameObject cGO = new GameObject("WinScreen_Canvas");
            cGO.transform.SetParent(transform, false);
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = cGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            cGO.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject root = CreateRect("WinRoot", canvas.transform);
        StretchFull(root.GetComponent<RectTransform>());
        masterCanvasGroup = root.AddComponent<CanvasGroup>();
        masterCanvasGroup.alpha = 0f;

        // Solid pitch-black background matching your psycho sprite sheet edges
        blackBg = root.AddComponent<Image>();
        blackBg.color = Color.black;
        blackBg.raycastTarget = true;

        // Fullscreen 16:9 / Preserve-Aspect Psycho Face Layers
        faceCurrent = CreateFaceLayer("Psycho_Current", root.transform);
        faceNextBlend = CreateFaceLayer("Psycho_Blend", root.transform);
        faceAfterimage = CreateFaceLayer("Psycho_Afterimage", root.transform);

        // Bloody "YOU SURVIVED" Text
        if (customSurviveText != null)
        {
            activeSurviveText = customSurviveText;
            if (surviveFont != null) activeSurviveText.font = surviveFont;
            ApplyBloodyTextStyling(activeSurviveText);

            surviveTextGroup = activeSurviveText.GetComponent<CanvasGroup>();
            if (surviveTextGroup == null) surviveTextGroup = activeSurviveText.gameObject.AddComponent<CanvasGroup>();
            surviveTextGroup.alpha = 0f;
            activeSurviveText.gameObject.SetActive(false);
        }
        else
        {
            GameObject txtGO = CreateRect("Txt_YouSurvived", root.transform);
            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.5f, 0.58f);
            txtRT.anchorMax = new Vector2(0.5f, 0.58f);
            txtRT.sizeDelta = new Vector2(1500f, 260f);
            txtRT.anchoredPosition = Vector2.zero;

            surviveTextGroup = txtGO.AddComponent<CanvasGroup>();
            surviveTextGroup.alpha = 0f;
            surviveTextGroup.blocksRaycasts = false;

            TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = surviveMessage;
            tmp.fontSize = fontSize;
            if (surviveFont != null) tmp.font = surviveFont;
            ApplyBloodyTextStyling(tmp);

            activeSurviveText = tmp;
            txtGO.SetActive(false);
        }

        // Interactable Main Menu Button
        if (customMainMenuButton != null)
        {
            activeMenuButton = customMainMenuButton;
            TMP_Text customBtnLabel = activeMenuButton.GetComponentInChildren<TMP_Text>(true);
            if (customBtnLabel != null && surviveFont != null) customBtnLabel.font = surviveFont;

            WireMenuButton(activeMenuButton, customBtnLabel);
            activeMenuButton.gameObject.SetActive(false);
        }
        else
        {
            GameObject btnGO = CreateRect("Btn_MainMenu", root.transform);
            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.24f);
            btnRT.anchorMax = new Vector2(0.5f, 0.24f);
            btnRT.sizeDelta = new Vector2(420f, 84f);
            btnRT.anchoredPosition = Vector2.zero;

            Image btnBg = btnGO.AddComponent<Image>();
            btnBg.color = new Color(0.08f, 0.01f, 0.01f, 0.9f);
            btnBg.raycastTarget = true;

            Outline border = btnGO.AddComponent<Outline>();
            border.effectColor = new Color(0.55f, 0.06f, 0.06f, 0.8f);
            border.effectDistance = new Vector2(2f, -2f);

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            GameObject lblGO = CreateRect("Label", btnGO.transform);
            StretchFull(lblGO.GetComponent<RectTransform>());
            TextMeshProUGUI lbl = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text = "MAIN MENU";
            lbl.fontSize = 38f;
            lbl.characterSpacing = 8f;
            lbl.alignment = TextAlignmentOptions.Center;
            if (surviveFont != null) lbl.font = surviveFont;
            lbl.color = btnNormal;
            lbl.raycastTarget = false;

            WireMenuButton(btn, lbl, border);
            activeMenuButton = btn;
            btnGO.SetActive(false);
        }

        // Topmost black curtain for smooth transition out to Main Menu
        GameObject curtainGO = CreateRect("FinalTransitionCurtain", root.transform);
        StretchFull(curtainGO.GetComponent<RectTransform>());
        finalCurtainFade = curtainGO.AddComponent<Image>();
        finalCurtainFade.color = new Color(0f, 0f, 0f, 0f);
        finalCurtainFade.raycastTarget = false;
        finalCurtainFade.enabled = false;
    }

    private void ApplyBloodyTextStyling(TMP_Text tmp)
    {
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 10f;
        tmp.enableVertexGradient = true;
        tmp.color = Color.white;
        tmp.colorGradient = new VertexGradient(bloodTopColor, bloodTopColor, bloodBottomColor, bloodBottomColor);
        tmp.raycastTarget = false;

        // Crisp outline + dark crimson drop-shadow for readability
        Outline outline = tmp.GetComponent<Outline>();
        if (outline == null) outline = tmp.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow bloodShadow = tmp.GetComponent<Shadow>();
        if (bloodShadow == null) bloodShadow = tmp.gameObject.AddComponent<Shadow>();
        bloodShadow.effectColor = new Color(0.22f, 0f, 0f, 0.9f);
        bloodShadow.effectDistance = new Vector2(0f, -6f);
    }

    private void WireMenuButton(Button btn, TMP_Text label, Outline border = null)
    {
        btn.transition = Selectable.Transition.None;
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(TriggerMainMenuTransition);

        Graphic colorTarget = label != null ? (Graphic)label : btn.targetGraphic;
        if (colorTarget != null) colorTarget.color = btnNormal;

        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (!btn.interactable) return;
            btn.transform.localScale = Vector3.one * 1.08f;
            if (colorTarget != null) colorTarget.color = btnHover;
            if (border != null) border.effectColor = new Color(0.95f, 0.15f, 0.15f, 1f);
        });

        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            btn.transform.localScale = Vector3.one;
            if (colorTarget != null) colorTarget.color = btnNormal;
            if (border != null) border.effectColor = new Color(0.55f, 0.06f, 0.06f, 0.8f);
        });

        AddTrigger(trigger, EventTriggerType.PointerDown, () =>
        {
            if (!btn.interactable) return;
            btn.transform.localScale = Vector3.one * 0.95f;
            if (colorTarget != null) colorTarget.color = btnPressed;
        });

        AddTrigger(trigger, EventTriggerType.PointerUp, () =>
        {
            btn.transform.localScale = Vector3.one * 1.08f;
            if (colorTarget != null) colorTarget.color = btnHover;
        });
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private Image CreateFaceLayer(string name, Transform parent)
    {
        GameObject go = CreateRect(name, parent);
        StretchFull(go.GetComponent<RectTransform>());
        Image img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    private GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void ActivateParents(GameObject go)
    {
        Transform t = go.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private bool HasAnySprite(Sprite[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null) return true;
        return false;
    }

    private void EnsureWorkingEventSystem()
    {
        EventSystem es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            es.gameObject.SetActive(true);
            es.enabled = true;
            StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}