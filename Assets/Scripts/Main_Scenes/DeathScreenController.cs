using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DeathScreenController : MonoBehaviour
{
    [Header("Panel & Crossfade Face Layers")]
    [SerializeField] private GameObject deathScreenPanel;
    [SerializeField] private Image faceBaseImage;
    [SerializeField] private Image faceBlendImage;
    [Tooltip("Drag the 4 sliced frames here in order from Left (darkest _0) to Right (brightest _3)")]
    [SerializeField] private Sprite[] faceStages;

    [Header("Text & Buttons")]
    [SerializeField] private TMP_Text youDiedText;
    [SerializeField] private CanvasGroup buttonsCanvasGroup;
    [SerializeField] private Button retryButton;    // Index 0: TRY AGAIN (leave empty to auto-find)
    [SerializeField] private Button mainMenuButton; // Index 1: MAIN MENU (leave empty to auto-find)
    [SerializeField] private string mainMenuSceneName = "Main_scene";

    [Header("Selection Highlight Style")]
    [SerializeField] private Color selectedTextColor = new Color(0.65f, 0.08f, 0.10f, 1f); // Deep blood red
    [SerializeField] private Color unselectedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f); // Muted ash grey
    [SerializeField] private float selectedButtonScale = 1.12f;
    [SerializeField] private bool showSelectionBrackets = true;

    [Header("Snappy Timing")]
    [SerializeField] private float totalFaceRevealTime = 1.1f;
    [SerializeField] private float textFadeTime = 0.4f;
    [SerializeField] private float startScale = 0.95f;
    [SerializeField] private float endScale = 1.03f;

    [Header("Joystick Settings")]
    [SerializeField] private float stickDeadzone = 0.45f;
    [SerializeField] private float stickRepeatDelay = 0.25f;

    [Header("Death Audio (Optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip deathStingerClip;

    private bool isDead = false;
    private bool canInteractWithButtons = false;
    private int selectedIndex = 0; // 0 = Retry, 1 = Main Menu
    private Button[] menuButtons;
    private string[] originalButtonLabels;

    private ArcadeControls arcadeInput;
    private float nextStickMoveTime = 0f;
    private bool stickReturnedToCenter = true;
    private Canvas parentCanvas;

    private void Awake()
    {
        arcadeInput = new ArcadeControls();
        parentCanvas = GetComponentInParent<Canvas>();

        // Auto-find buttons inside Dead_BTN_Canva if slots were left empty
        if (buttonsCanvasGroup != null && (retryButton == null || mainMenuButton == null))
        {
            Button[] found = buttonsCanvasGroup.GetComponentsInChildren<Button>(true);
            if (found.Length >= 1 && retryButton == null) retryButton = found[0];
            if (found.Length >= 2 && mainMenuButton == null) mainMenuButton = found[1];
        }

        menuButtons = new Button[] { retryButton, mainMenuButton };
        originalButtonLabels = new string[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;

            menuButtons[i].transition = Selectable.Transition.None;

            TMP_Text txt = menuButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                originalButtonLabels[i] = txt.text;
                txt.color = Color.white;
            }
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryRoom);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (deathStingerClip != null)
            deathStingerClip.LoadAudioData();

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (arcadeInput != null) arcadeInput.Arcade.Enable();
    }

    private void OnDisable()
    {
        if (arcadeInput != null) arcadeInput.Arcade.Disable();
    }

    private void OnDestroy()
    {
        if (arcadeInput != null) arcadeInput.Dispose();
    }

    public void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;
        canInteractWithButtons = true;

        nextStickMoveTime = Time.unscaledTime + 0.2f;
        stickReturnedToCenter = false;

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);

        if (sfxSource != null && deathStingerClip != null)
            sfxSource.PlayOneShot(deathStingerClip);

        HighlightButton(0);
        StartCoroutine(PlayDeathSequence());
    }

    private IEnumerator PlayDeathSequence()
    {
        SetTextAlpha(0f);
        if (buttonsCanvasGroup != null)
        {
            buttonsCanvasGroup.alpha = 0f;
            buttonsCanvasGroup.interactable = true;
            buttonsCanvasGroup.blocksRaycasts = true;
        }

        if (faceStages != null && faceStages.Length >= 2 && faceBaseImage != null && faceBlendImage != null)
        {
            int transitions = faceStages.Length - 1;
            float timePerStage = Mathf.Max(0.05f, totalFaceRevealTime / transitions);
            float elapsedTotal = 0f;

            faceBaseImage.sprite = faceStages[0];
            SetImageAlpha(faceBaseImage, 1f);
            SetImageAlpha(faceBlendImage, 0f);

            for (int i = 0; i < transitions; i++)
            {
                faceBaseImage.sprite = faceStages[i];
                faceBlendImage.sprite = faceStages[i + 1];
                SetImageAlpha(faceBaseImage, 1f);
                SetImageAlpha(faceBlendImage, 0f);

                float stageTimer = 0f;
                while (stageTimer < timePerStage)
                {
                    stageTimer += Time.unscaledDeltaTime;
                    elapsedTotal += Time.unscaledDeltaTime;

                    float blendAlpha = Mathf.Clamp01(stageTimer / timePerStage);
                    SetImageAlpha(faceBlendImage, blendAlpha);

                    float overallProgress = Mathf.Clamp01(elapsedTotal / totalFaceRevealTime);
                    float currentScale = Mathf.Lerp(startScale, endScale, overallProgress);
                    Vector3 scaleVec = new Vector3(currentScale, currentScale, 1f);
                    faceBaseImage.rectTransform.localScale = scaleVec;
                    faceBlendImage.rectTransform.localScale = scaleVec;

                    if (overallProgress > 0.4f)
                    {
                        float uiAlpha = Mathf.Clamp01((overallProgress - 0.4f) / 0.6f);
                        SetTextAlpha(uiAlpha);
                        if (buttonsCanvasGroup != null) buttonsCanvasGroup.alpha = uiAlpha;
                    }

                    yield return null;
                }
            }

            faceBaseImage.sprite = faceStages[faceStages.Length - 1];
            SetImageAlpha(faceBaseImage, 1f);
            SetImageAlpha(faceBlendImage, 0f);
        }
        else
        {
            float t = 0f;
            while (t < textFadeTime)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / textFadeTime);
                SetTextAlpha(a);
                if (buttonsCanvasGroup != null) buttonsCanvasGroup.alpha = a;
                yield return null;
            }
        }

        SetTextAlpha(1f);
        if (buttonsCanvasGroup != null) buttonsCanvasGroup.alpha = 1f;
    }

    private void Update()
    {
        // Quick test shortcut: Press P in Play Mode to preview the death screen anytime!
        if (!isDead && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            TriggerDeath();
            return;
        }

        if (!isDead || !canInteractWithButtons) return;

        HandleMouseHoverAndClick();
        HandleKeyboardInput();
        HandleJoystickAndArcadeInput();
    }

    // 1. MOUSE: Hover + Left Click
    private void HandleMouseHoverAndClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || menuButtons == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;

            RectTransform btnRect = menuButtons[i].GetComponent<RectTransform>();
            TMP_Text txt = menuButtons[i].GetComponentInChildren<TMP_Text>();
            RectTransform txtRect = txt != null ? txt.GetComponent<RectTransform>() : null;

            bool hoveringBtn = btnRect != null && RectTransformUtility.RectangleContainsScreenPoint(btnRect, mousePos, uiCam);
            bool hoveringTxt = txtRect != null && RectTransformUtility.RectangleContainsScreenPoint(txtRect, mousePos, uiCam);

            if (hoveringBtn || hoveringTxt)
            {
                if (selectedIndex != i)
                    HighlightButton(i);

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ConfirmSelectedButton();
                    return;
                }
            }
        }
    }

    // 2. KEYBOARD: Arrow Keys to choose + Enter to select
    private void HandleKeyboardInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
        {
            SwitchSelection(-1);
        }
        else if (kb.downArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
        {
            SwitchSelection(1);
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            ConfirmSelectedButton();
        }
    }

    // 3. JOYSTICK / USB ARCADE STICK / GAMEPAD
    private void HandleJoystickAndArcadeInput()
    {
        Vector2 stick = Vector2.zero;

        if (Joystick.current != null)
        {
            stick = Joystick.current.stick.ReadValue();
        }

        if (Gamepad.current != null && stick.sqrMagnitude < 0.05f)
        {
            Vector2 padStick = Gamepad.current.leftStick.ReadValue();
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            stick = padStick.sqrMagnitude > dpad.sqrMagnitude ? padStick : dpad;
        }

        if (Mathf.Abs(stick.x) < 0.2f && Mathf.Abs(stick.y) < 0.2f)
        {
            stickReturnedToCenter = true;
        }
        else if (stickReturnedToCenter || Time.unscaledTime >= nextStickMoveTime)
        {
            if (stick.y > stickDeadzone || stick.x < -stickDeadzone)
            {
                stickReturnedToCenter = false;
                nextStickMoveTime = Time.unscaledTime + stickRepeatDelay;
                SwitchSelection(-1);
            }
            else if (stick.y < -stickDeadzone || stick.x > stickDeadzone)
            {
                stickReturnedToCenter = false;
                nextStickMoveTime = Time.unscaledTime + stickRepeatDelay;
                SwitchSelection(1);
            }
        }

        bool joystickConfirm = (Joystick.current != null && Joystick.current.trigger.wasPressedThisFrame) ||
                               (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                                            Gamepad.current.buttonNorth.wasPressedThisFrame ||
                                                            Gamepad.current.startButton.wasPressedThisFrame));

        if (arcadeInput != null)
        {
            var startCtrl = arcadeInput.Arcade.ButtonStart.activeControl;
            if (arcadeInput.Arcade.ButtonStart.WasPressedThisFrame() && (startCtrl == null || !(startCtrl.device is Keyboard)))
            {
                joystickConfirm = true;
            }

            var btnACtrl = arcadeInput.Arcade.ButtonA.activeControl;
            if (arcadeInput.Arcade.ButtonA.WasPressedThisFrame() && (btnACtrl == null || !(btnACtrl.device is Keyboard)))
            {
                joystickConfirm = true;
            }

            var exitCtrl = arcadeInput.Arcade.ButtonExit.activeControl;
            if (arcadeInput.Arcade.ButtonExit.WasPressedThisFrame() && (exitCtrl == null || !(exitCtrl.device is Keyboard)))
            {
                ReturnToMainMenu();
                return;
            }
        }

        if (joystickConfirm)
        {
            ConfirmSelectedButton();
        }
    }

    private void SwitchSelection(int direction)
    {
        if (menuButtons == null || menuButtons.Length == 0) return;
        selectedIndex = (selectedIndex + direction + menuButtons.Length) % menuButtons.Length;
        HighlightButton(selectedIndex);
    }

    private void HighlightButton(int index)
    {
        selectedIndex = index;
        if (menuButtons == null) return;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null) continue;

            bool isChosen = (i == selectedIndex);

            menuButtons[i].transform.localScale = isChosen
                ? new Vector3(selectedButtonScale, selectedButtonScale, 1f)
                : Vector3.one;

            TMP_Text label = menuButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = isChosen ? selectedTextColor : unselectedTextColor;

                if (originalButtonLabels != null && i < originalButtonLabels.Length && !string.IsNullOrEmpty(originalButtonLabels[i]))
                {
                    label.text = (isChosen && showSelectionBrackets)
                        ? $"> {originalButtonLabels[i]} <"
                        : originalButtonLabels[i];
                }
            }
        }

        if (index >= 0 && index < menuButtons.Length && menuButtons[index] != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
        }
    }

    private void ConfirmSelectedButton()
    {
        if (selectedIndex == 0)
            RetryRoom();
        else
            ReturnToMainMenu();
    }

    public void RetryRoom()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetImageAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private void SetTextAlpha(float a)
    {
        if (youDiedText == null) return;
        Color c = youDiedText.color;
        c.a = a;
        youDiedText.color = c;
    }
}