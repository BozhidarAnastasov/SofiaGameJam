using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuControler : MonoBehaviour
{
    [Header("Scene Setup")]
    [SerializeField] private string firstLevelSceneName = "Lobby";

    [Header("Main Menu Buttons")]
    [SerializeField] private Button enterButton;    // Index 0: ENTER / PLAY
    [SerializeField] private Button settingsButton; // Index 1: SETTINGS
    [SerializeField] private Button leaveButton;    // Index 2: LEAVE / QUIT

    [Header("Optional Settings Integration")]
    [SerializeField] private MainMenuAudioAndSettings settingsManager;
    [SerializeField] private GameObject settingsPanel;

    [Header("Selection Highlight Style")]
    [SerializeField] private Color selectedTextColor = new Color(0.65f, 0.08f, 0.10f, 1f);
    [SerializeField] private Color unselectedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private float selectedButtonScale = 1.12f;
    [SerializeField] private bool showSelectionBrackets = true;

    [Header("Joystick Settings")]
    [SerializeField] private float stickDeadzone = 0.45f;
    [SerializeField] private float stickRepeatDelay = 0.22f;

    [Header("Optional Hover / Click Audio")]
    [SerializeField] private AudioSource uiSfxSource;
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    private Button[] menuButtons;
    private string[] originalButtonLabels;
    private int selectedIndex = 0;

    private ArcadeControls arcadeInput;
    private float nextStickMoveTime = 0f;
    private bool stickReturnedToCenter = true;
    private bool wasSettingsOpen = false;
    private Canvas parentCanvas;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        arcadeInput = new ArcadeControls();
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindFirstObjectByType<Canvas>();

        if (settingsManager == null)
            settingsManager = FindFirstObjectByType<MainMenuAudioAndSettings>();

        var validButtons = new List<Button>();
        if (enterButton != null) validButtons.Add(enterButton);
        if (settingsButton != null) validButtons.Add(settingsButton);
        if (leaveButton != null) validButtons.Add(leaveButton);

        menuButtons = validButtons.ToArray();
        originalButtonLabels = new string[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            menuButtons[i].transition = Selectable.Transition.None;

            TMP_Text txt = menuButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                originalButtonLabels[i] = txt.text;
                txt.color = Color.white;
            }
        }

        if (enterButton != null) enterButton.onClick.RemoveAllListeners();
        if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
        if (leaveButton != null) leaveButton.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        HighlightButton(0, playSound: false);
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

    private void Update()
    {
        bool isSettingsOpen = settingsPanel != null && settingsPanel.activeInHierarchy;
        if (isSettingsOpen)
        {
            wasSettingsOpen = true;
            return;
        }

        if (wasSettingsOpen)
        {
            wasSettingsOpen = false;
            int settingsIdx = System.Array.IndexOf(menuButtons, settingsButton);
            HighlightButton(settingsIdx >= 0 ? settingsIdx : 0, playSound: false);
            return;
        }

        if (menuButtons == null || menuButtons.Length == 0) return;

        HandleMouseHoverAndClick();
        HandleKeyboardInput();
        HandleJoystickAndArcadeInput();
    }

    private void HandleMouseHoverAndClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null || !menuButtons[i].gameObject.activeInHierarchy) continue;

            RectTransform btnRect = menuButtons[i].GetComponent<RectTransform>();
            TMP_Text txt = menuButtons[i].GetComponentInChildren<TMP_Text>();
            RectTransform txtRect = txt != null ? txt.GetComponent<RectTransform>() : null;

            bool hoveringBtn = btnRect != null && RectTransformUtility.RectangleContainsScreenPoint(btnRect, mousePos, uiCam);
            bool hoveringTxt = txtRect != null && RectTransformUtility.RectangleContainsScreenPoint(txtRect, mousePos, uiCam);

            if (hoveringBtn || hoveringTxt)
            {
                if (selectedIndex != i)
                    HighlightButton(i, playSound: true);

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ConfirmSelectedButton();
                    return;
                }
            }
        }
    }

    private void HandleKeyboardInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.upArrowKey.wasPressedThisFrame)
            SwitchSelection(-1);
        else if (kb.downArrowKey.wasPressedThisFrame)
            SwitchSelection(1);

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            ConfirmSelectedButton();
    }
    private void HandleJoystickAndArcadeInput()
    {
        Vector2 stick = Vector2.zero;

        if (Joystick.current != null)
            stick = Joystick.current.stick.ReadValue();

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
                joystickConfirm = true;

            var btnACtrl = arcadeInput.Arcade.ButtonA.activeControl;
            if (arcadeInput.Arcade.ButtonA.WasPressedThisFrame() && (btnACtrl == null || !(btnACtrl.device is Keyboard)))
                joystickConfirm = true;
        }

        if (joystickConfirm)
            ConfirmSelectedButton();
    }

    private void SwitchSelection(int direction)
    {
        if (menuButtons == null || menuButtons.Length == 0) return;
        int nextIndex = (selectedIndex + direction + menuButtons.Length) % menuButtons.Length;
        HighlightButton(nextIndex, playSound: true);
    }

    private void HighlightButton(int index, bool playSound)
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

        if (playSound && uiSfxSource != null && hoverClip != null)
            uiSfxSource.PlayOneShot(hoverClip, 0.5f);
    }

    private void ConfirmSelectedButton()
    {
        if (selectedIndex < 0 || selectedIndex >= menuButtons.Length) return;

        if (uiSfxSource != null && clickClip != null)
            uiSfxSource.PlayOneShot(clickClip);

        Button chosen = menuButtons[selectedIndex];

        if (chosen == enterButton)
        {
            StartGame();
        }
        else if (chosen == settingsButton)
        {
            if (settingsManager != null)
                settingsManager.OpenSettings();
            else if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }
        else if (chosen == leaveButton)
        {
            QuitGame();
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Game Quit Triggered!");
        Application.Quit();
    }
}