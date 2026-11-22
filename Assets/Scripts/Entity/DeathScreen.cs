using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DeathScreen : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image background;
    [SerializeField] private Image face;

    [Tooltip("psycho_death_screen_00 ... 03. If left empty, uses the Sprite already on 'face'.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameTimes = { 0.35f, 0.18f, 0.12f, 0.10f };
    [SerializeField] private float holdTime = 1.8f;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Forced Hover Highlight Settings")]
    [SerializeField] private Color buttonNormalColor = new Color(0.65f, 0.7f, 0.75f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color buttonPressedColor = new Color(0.5f, 0.1f, 0.1f, 1f);
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
    private Vector2 initialFacePos;
    private Vector3 initialFaceScale = Vector3.one;
    private bool cachedFaceTransform;

    private void Awake()
    {
        CacheFaceTransform();

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        if (background != null && group == null)
        {
            background.gameObject.SetActive(false);
        }

        if (face != null)
        {
            face.enabled = false;
        }

        SetupButton(retryButton, Retry);
        SetupButton(mainMenuButton, ReturnToMainMenu);

        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    [ContextMenu("Test Death Screen Now")]
    public void Show()
    {
        ActivateWithParents(gameObject);

        if (shown) return;
        shown = true;

        EnsureWorkingEventSystem();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.enabled = true;
            rootCanvas.overrideSorting = true;
            if (rootCanvas.sortingOrder < 100) rootCanvas.sortingOrder = 100;

            if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            DisableBlockingRaycasts(rootCanvas.transform);
        }

        if (audioSource != null && stinger != null) audioSource.PlayOneShot(stinger);
        else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.youDied : null);
        onShown.Invoke();
        if (face != null && frames != null && frames.Length > 0)
        {
            ActivateWithParents(group.gameObject);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        if (background != null)
        {
            ActivateWithParents(background.gameObject);
            background.enabled = true;
            background.color = Color.black;
            background.raycastTarget = true;
            background.transform.SetAsFirstSibling();
        }

        yield return new WaitForSecondsRealtime(0.2f);

        if (audioSource != null && stinger != null)
        {
            audioSource.PlayOneShot(stinger);
        }

        onShown?.Invoke();

        if (face != null)
        {
            CacheFaceTransform();
            ActivateWithParents(face.gameObject);
            face.enabled = true;
            face.raycastTarget = false;
            face.color = Color.white;

            RectTransform rt = face.rectTransform;
            bool hasFrameArray = frames != null && frames.Length > 0 && HasAnyValidSprite(frames);

            if (hasFrameArray)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] != null) face.sprite = frames[i];

                    float t = (frameTimes != null && i < frameTimes.Length && frameTimes[i] > 0f) ? frameTimes[i] : 0.12f;
                    bool isLast = (i == frames.Length - 1);
                    float punch = isLast ? 1.12f : 1.04f;
                    float shake = isLast ? 14f : 4f;

                    for (float e = 0f; e < t; e += Time.unscaledDeltaTime)
                    {
                        float k = e / t;
                        rt.localScale = initialFaceScale * Mathf.Lerp(punch, 1f, k);
                        rt.anchoredPosition = initialFacePos + Random.insideUnitCircle * shake * (1f - k);
                        yield return null;
                    }
                }
            }
            else if (face.sprite != null)
            {
                for (float e = 0f; e < 0.3f; e += Time.unscaledDeltaTime)
                {
                    float k = e / 0.3f;
                    rt.localScale = initialFaceScale * Mathf.Lerp(1.15f, 1f, k);
                    rt.anchoredPosition = initialFacePos + Random.insideUnitCircle * 12f * (1f - k);
                    yield return null;
                }
            }

            for (float e = 0f; e < holdTime; e += Time.unscaledDeltaTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(e * 6f);
                face.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f, 1f), pulse * 0.35f);
                rt.anchoredPosition = initialFacePos + Random.insideUnitCircle * 1.5f;
                yield return null;
            }

            face.color = Color.white;
            rt.localScale = initialFaceScale;
            rt.anchoredPosition = initialFacePos;
        }
        else
        {
            yield return new WaitForSecondsRealtime(holdTime);
        }

        onFinished?.Invoke();

        if (retryButton != null || mainMenuButton != null)
        {
            RevealButtonSafe(retryButton);
            RevealButtonSafe(mainMenuButton);
            SeparateOverlappingButtons(retryButton, mainMenuButton);
            yield break;
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
            while (!retryAction.action.WasPressedThisFrame()) yield return null;
            Retry();
        }
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
        else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EnsureWorkingEventSystem()
    {
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            es = FindFirstObjectByType<EventSystem>();
        }

        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            es.gameObject.SetActive(true);
            es.enabled = true;

            StandaloneInputModule legacyModule = es.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Destroy(legacyModule);
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }

    private void DisableBlockingRaycasts(Transform root)
    {
        Graphic[] allGraphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < allGraphics.Length; i++)
        {
            Graphic g = allGraphics[i];
            if (g == background) continue;

            bool belongsToButton = (retryButton != null && g.transform.IsChildOf(retryButton.transform)) ||
                                   (mainMenuButton != null && g.transform.IsChildOf(mainMenuButton.transform));

            if (!belongsToButton)
            {
                g.raycastTarget = false;
            }
        }
    }

    private void CacheFaceTransform()
    {
        if (cachedFaceTransform || face == null) return;
        initialFacePos = face.rectTransform.anchoredPosition;
        initialFaceScale = face.rectTransform.localScale;
        if (initialFaceScale == Vector3.zero) initialFaceScale = Vector3.one;
        cachedFaceTransform = true;
    }

    private bool HasAnyValidSprite(Sprite[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null) return true;
        }
        return false;
    }

    private void RevealButtonSafe(Button btn)
    {
        if (btn == null) return;

        ActivateWithParents(btn.gameObject);
        for (int i = 0; i < btn.transform.childCount; i++)
        {
            btn.transform.GetChild(i).gameObject.SetActive(true);
        }

        btn.interactable = true;

        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3 pos = rt.localPosition;
            pos.z = 0f;
            rt.localPosition = pos;

            if (rt.localScale.x == 0f || rt.localScale.y == 0f)
            {
                rt.localScale = Vector3.one;
            }
        }

        // Ensure clickable hitbox exists on the button itself
        Image btnImg = btn.GetComponent<Image>();
        if (btnImg == null)
        {
            btnImg = btn.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0f, 0f, 0f, 0.01f);
        }
        btnImg.enabled = true;
        btnImg.raycastTarget = true;

        // Uncheck raycastTarget on child text so it doesn't swallow hover events
        TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enabled = true;
            label.raycastTarget = false;
        }

        Text legacyLabel = btn.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.enabled = true;
            legacyLabel.raycastTarget = false;
        }

        CanvasGroup[] parentGroups = btn.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < parentGroups.Length; i++)
        {
            parentGroups[i].alpha = 1f;
            parentGroups[i].blocksRaycasts = true;
            parentGroups[i].interactable = true;
        }

        // Fix nested sub-canvas sorting/raycast bugs (e.g., Dead_BTN_Canva)
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Canvas[] parentCanvases = btn.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < parentCanvases.Length; i++)
        {
            parentCanvases[i].enabled = true;
            if (parentCanvases[i] != rootCanvas && rootCanvas != null)
            {
                parentCanvases[i].overrideSorting = true;
                parentCanvases[i].sortingOrder = rootCanvas.sortingOrder + 5;
            }
            if (parentCanvases[i].GetComponent<GraphicRaycaster>() == null)
            {
                parentCanvases[i].gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (btn.transform.parent != null)
        {
            btn.transform.parent.SetAsLastSibling();
        }

        AttachForcedHoverHighlight(btn, btnImg, label, legacyLabel);
    }

    private void SeparateOverlappingButtons(Button btnA, Button btnB)
    {
        if (btnA == null || btnB == null) return;
        RectTransform rtA = btnA.GetComponent<RectTransform>();
        RectTransform rtB = btnB.GetComponent<RectTransform>();
        if (rtA == null || rtB == null) return;

        // If both buttons were left at the exact same position, push the 2nd button below the 1st
        if (Vector2.Distance(rtA.anchoredPosition, rtB.anchoredPosition) < 5f)
        {
            float offset = Mathf.Max(rtA.rect.height, 50f) + 20f;
            rtB.anchoredPosition = rtA.anchoredPosition + new Vector2(0f, -offset);
        }
    }

    private void AttachForcedHoverHighlight(Button btn, Image bg, TMP_Text tmpText, Text legacyText)
    {
        // Disable built-in Transition fade issues when Time.timeScale == 0 and drive hover directly
        btn.transition = Selectable.Transition.None;
        Vector3 baseScale = btn.transform.localScale;

        Graphic colorTarget = (tmpText != null) ? (Graphic)tmpText :
                              (legacyText != null) ? (Graphic)legacyText : bg;

        if (colorTarget != null) colorTarget.color = buttonNormalColor;

        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTriggerEvent(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (!btn.interactable) return;
            btn.transform.localScale = baseScale * hoverScale;
            if (colorTarget != null) colorTarget.color = buttonHoverColor;
        });

        AddTriggerEvent(trigger, EventTriggerType.PointerExit, () =>
        {
            btn.transform.localScale = baseScale;
            if (colorTarget != null) colorTarget.color = buttonNormalColor;
        });

        AddTriggerEvent(trigger, EventTriggerType.PointerDown, () =>
        {
            if (!btn.interactable) return;
            btn.transform.localScale = baseScale * 0.95f;
            if (colorTarget != null) colorTarget.color = buttonPressedColor;
        });

        AddTriggerEvent(trigger, EventTriggerType.PointerUp, () =>
        {
            btn.transform.localScale = baseScale * hoverScale;
            if (colorTarget != null) colorTarget.color = buttonHoverColor;
        });
    }

    private void AddTriggerEvent(EventTrigger trigger, EventTriggerType type, UnityAction callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    private void ActivateWithParents(GameObject target)
    {
        if (target == null) return;
        Transform current = target.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }
            current = current.parent;
        }
    }

    private void SetupButton(Button btn, UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }
}